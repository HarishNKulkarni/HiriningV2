using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;

namespace Recruitment.Controllers
{
    [Route("api/NotificationController")]
    [ApiController]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public NotificationController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }

        [HttpPost("GetNotificationList")]
        public async Task<IActionResult> GetNotificationList(
            [FromBody] GetNotificationListRequest? request)
        {
            if (request == null ||
                request.CompanyId <= 0 ||
                request.UserId <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Valid CompanyId and UserId are required."
                });
            }

            try
            {
                var employee = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return NotFound(new
                    {
                        Status = false,
                        Message = "Employee not found for this company."
                    });
                }

                if (employee.IsActive != true)
                {
                    return StatusCode(403, new
                    {
                        Status = false,
                        Message = "Employee is inactive."
                    });
                }

                // Use Recruitment EmployeeId for both EP and non-EP employees.
                // Every notification has its Recruitment copy.
                var notifications = await _context.RecruitmentNotifications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.ToUserId == request.UserId)
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x => x.NotificationId)
                    .ToListAsync();

                var employeeIds = notifications
                    .SelectMany(x => new[]
                    {
                x.FromUserId,
                x.ToUserId
                    })
                    .Distinct()
                    .ToArray();

                var employeeNames = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        employeeIds.Contains(x.EmployeeId))
                    .Select(x => new
                    {
                        x.EmployeeId,
                        Name =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim()
                    })
                    .ToDictionaryAsync(
                        x => x.EmployeeId,
                        x => x.Name);

                var applicationIds = notifications
                    .Select(x => x.CandidateApplicationId)
                    .Distinct()
                    .ToArray();

                var applications = await (
                    from application in _context.CandidateApplications
                        .AsNoTracking()

                    join candidate in _context.Candidates.AsNoTracking()
                        on new
                        {
                            application.CompanyId,
                            application.CandidateId
                        }
                        equals new
                        {
                            candidate.CompanyId,
                            candidate.CandidateId
                        }

                    where application.CompanyId == request.CompanyId &&
                          applicationIds.Contains(
                              application.CandidateApplicationId)

                    select new
                    {
                        application.CandidateApplicationId,
                        application.JobDescriptionId,
                        candidate.CandidateId,
                        candidate.FirstName,
                        candidate.MiddleName,
                        candidate.LastName
                    }
                ).ToDictionaryAsync(x => x.CandidateApplicationId);

                var jobIds = notifications
                    .Select(x => x.JobDescriptionId)
                    .Distinct()
                    .ToArray();

                var jobNames = await _context.JobDescriptions
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        jobIds.Contains(x.JobDescriptionId))
                    .Select(x => new
                    {
                        x.JobDescriptionId,
                        x.JobTitle
                    })
                    .ToDictionaryAsync(
                        x => x.JobDescriptionId,
                        x => x.JobTitle);

                var skillIds = notifications
                    .Where(x => x.CompanySkillMasterId > 0)
                    .Select(x => x.CompanySkillMasterId)
                    .Distinct()
                    .ToArray();

                // Keep names available for historical notifications,
                // even when a skill is now inactive.
                var skillNames = await _context.CompanySkillMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        skillIds.Contains(x.CompanySkillMasterId))
                    .Select(x => new
                    {
                        x.CompanySkillMasterId,
                        x.SkillName
                    })
                    .ToDictionaryAsync(
                        x => x.CompanySkillMasterId,
                        x => x.SkillName);

                string? GetEmployeeName(int employeeId)
                {
                    return employeeNames.TryGetValue(employeeId, out var name)
                        ? name
                        : null;
                }

                var data = notifications.Select(notification =>
                {
                    jobNames.TryGetValue(
                        notification.JobDescriptionId,
                        out var jobTitle);

                    int? candidateId = null;
                    string? candidateName = null;

                    if (applications.TryGetValue(
                            notification.CandidateApplicationId,
                            out var application) &&
                        application.JobDescriptionId ==
                            notification.JobDescriptionId)
                    {
                        candidateId = application.CandidateId;

                        candidateName = string.Join(
                            " ",
                            new[]
                            {
                        application.FirstName,
                        application.MiddleName,
                        application.LastName
                            }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    }

                    string? skillName = "Default Remark";

                    if (notification.CompanySkillMasterId > 0)
                    {
                        skillNames.TryGetValue(
                            notification.CompanySkillMasterId,
                            out skillName);
                    }

                    string? decisionStatus = notification.Action switch
                    {
                        "Approve" => "Approved",
                        "Reject" => "Rejected",
                        "Return" => "Returned",
                        _ => null
                    };

                    string taskStatus = notification.IsActionRequest
                        ? notification.ActionedByHistoryId.HasValue
                            ? "Completed"
                            : "Pending"
                        : "Information";

                    return new
                    {
                        notification.NotificationId,
                        notification.CompanyId,

                        notification.FromUserId,
                        FromUserName = GetEmployeeName(
                            notification.FromUserId),

                        notification.ToUserId,
                        ToUserName = GetEmployeeName(
                            notification.ToUserId),

                        notification.Type,
                        notification.Title,
                        notification.Message,
                        notification.Action,

                        // Decision that generated this notification.
                        Status = decisionStatus,

                        // Whether the recipient still needs to act.
                        TaskStatus = taskStatus,

                        CandidateId = candidateId,
                        CandidateName = candidateName,
                        notification.CandidateApplicationId,

                        notification.JobDescriptionId,
                        JobTitle = jobTitle,
                        notification.TemplateId,

                        notification.FromAssignmentId,
                        notification.ToAssignmentId,
                        notification.ResumeAfterAssignmentId,

                        notification.CompanySkillMasterId,
                        SkillId = notification.CompanySkillMasterId,
                        SkillName = skillName,

                        IsDefaultRemark =
                            notification.CompanySkillMasterId == 0,

                        notification.CandidateSkillAssessmentHistoryId,
                        notification.IsActionRequest,
                        notification.ActionedByHistoryId,

                        notification.IsRead,
                        notification.ReadOn,
                        notification.CreatedOn
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Notifications fetched successfully."
                        : "No notifications found.",

                    TotalCount = data.Count,
                    UnreadCount = notifications.Count(x => !x.IsRead),
                    ReadCount = notifications.Count(x => x.IsRead),

                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch notifications for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch notifications."
                });
            }
        }








        [HttpPost("MarkNotificationRead")]
        public async Task<IActionResult> MarkNotificationRead(
    [FromBody] MarkNotificationReadRequest? request)
        {
            if (request == null ||
                request.CompanyId <= 0 ||
                request.UserId <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Valid CompanyId and UserId are required."
                });
            }

            if (request.NotificationIds == null ||
                request.NotificationIds.Count == 0 ||
                request.NotificationIds.Any(id => id <= 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Provide at least one valid notification ID."
                });
            }

            var notificationIds = request.NotificationIds
                .Distinct()
                .ToArray();

            try
            {
                var employee = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return NotFound(new
                    {
                        Status = false,
                        Message = "Employee not found for this company."
                    });
                }

                if (employee.IsActive != true)
                {
                    return StatusCode(403, new
                    {
                        Status = false,
                        Message = "Employee is inactive."
                    });
                }

                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var notifications = await _context.RecruitmentNotifications
                    .AsTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.ToUserId == request.UserId &&
                        notificationIds.Contains(x.NotificationId))
                    .ToListAsync();

                var foundIds = notifications
                    .Select(x => x.NotificationId)
                    .ToHashSet();

                var missingIds = notificationIds
                    .Where(id => !foundIds.Contains(id))
                    .ToArray();

                // Do not partially update if any requested ID is unavailable
                // or belongs to another employee/company.
                if (missingIds.Length > 0)
                {
                    return NotFound(new
                    {
                        Status = false,
                        Message =
                            "One or more notifications were not found for this employee. No notifications were updated.",
                        NotificationIds = missingIds
                    });
                }

                var now = DateTime.UtcNow;
                int updatedCount = 0;
                int alreadyReadCount = 0;

                foreach (var notification in notifications)
                {
                    if (notification.IsRead)
                    {
                        alreadyReadCount++;
                        continue;
                    }

                    notification.IsRead = true;
                    notification.ReadOn = now;
                    updatedCount++;
                }

                if (updatedCount > 0)
                    await _context.SaveChangesAsync();

                var counts = await _context.RecruitmentNotifications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.ToUserId == request.UserId)
                    .GroupBy(x => 1)
                    .Select(group => new
                    {
                        TotalCount = group.Count(),
                        UnreadCount = group.Count(x => !x.IsRead),
                        ReadCount = group.Count(x => x.IsRead)
                    })
                    .FirstOrDefaultAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = updatedCount > 0
                        ? "Notifications marked as read successfully."
                        : "All selected notifications are already marked as read.",

                    UpdatedCount = updatedCount,
                    AlreadyReadCount = alreadyReadCount,

                    TotalCount = counts?.TotalCount ?? 0,
                    UnreadCount = counts?.UnreadCount ?? 0,
                    ReadCount = counts?.ReadCount ?? 0,

                    Data = notifications
                        .OrderBy(x => x.NotificationId)
                        .Select(x => new
                        {
                            x.NotificationId,
                            x.CompanyId,
                            UserId = x.ToUserId,
                            x.IsRead,
                            x.ReadOn
                        })
                        .ToList()
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to mark notifications as read for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to mark notifications as read."
                });
            }
        }


        public class MarkNotificationReadRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }
            public List<int>? NotificationIds { get; set; }
        }

        public class GetNotificationListRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }

            // null = all, false = unread, true = read
           
        }
    }
}
