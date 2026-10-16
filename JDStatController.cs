using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;

namespace Recruitment.Controllers
{
    [Route("api/JDStatController")]
    [ApiController]
    public class JDStatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public JDStatController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }


        [HttpPost("GetJDStatistics")]
        public async Task<IActionResult> GetJDStatistics([FromBody] GetJDStatisticsRequest? request)
        {
            IActionResult Fail(int code, string message)
            {
                return StatusCode(code, new
                {
                    Status = false,
                    Message = message
                });
            }

            if (request == null ||
                request.CompanyId <= 0 ||
                request.User <= 0)
            {
                return Fail(400, "Valid CompanyId and User are required.");
            }

            if (request.JobDescriptionIds?.Any(x => x < 0) == true)
            {
                return Fail(400, "JobDescriptionIds cannot contain negative values.");
            }

            try
            {
                var employee = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.User)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsActive,
                        x.IsAdmin,
                        EmployeeName =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim()
                    })
                    .FirstOrDefaultAsync();

                if (employee == null)
                    return Fail(404, "Employee not found for this company.");

                if (employee.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                bool isAdmin = employee.IsAdmin == true;

                var assignmentsQuery = _context.RoleTemplateAssignment
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive == true)
                    .Where(x =>
                        _context.RoleTemplates.Any(template =>
                            template.CompanyId == x.CompanyId &&
                            template.TemplateId == x.TemplateId &&
                            template.IsActive == true));

                if (!isAdmin)
                {
                    assignmentsQuery = assignmentsQuery.Where(x =>
                        x.EmployeeId == request.User);
                }

                var jobsQuery = _context.JobDescriptions
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive == true);

                var selectedJobIds = request.JobDescriptionIds?
                    .Distinct()
                    .ToArray() ?? Array.Empty<int>();

                if (selectedJobIds.Length > 0 &&
                    !selectedJobIds.Contains(0))
                {
                    jobsQuery = jobsQuery.Where(x =>
                        selectedJobIds.Contains(x.JobDescriptionId));
                }

                if (!isAdmin)
                {
                    jobsQuery = jobsQuery.Where(job =>
                        assignmentsQuery.Any(assignment =>
                            (int?)assignment.TemplateId == job.TemplateId));
                }

                // Count accessible active templates, including templates with no jobs
                // independently of the selected job filter.
                var accessibleTemplatesQuery = _context.RoleTemplates
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive == true);

                if (!isAdmin)
                {
                    accessibleTemplatesQuery = accessibleTemplatesQuery.Where(template =>
                        assignmentsQuery.Any(assignment =>
                            assignment.TemplateId == template.TemplateId));
                }


                var totalTemplates = await accessibleTemplatesQuery
                    .Select(x => x.TemplateId)
                    .Distinct()
                    .CountAsync();

                var jobs = await jobsQuery
                    .Select(x => new
                    {
                        x.JobDescriptionId,
                        x.JobTitle,
                        TemplateId = (int?)x.TemplateId,
                        x.CreatedBy,
                        x.CreatedOn,
                        x.ModifiedBy,
                        x.ModifiedOn
                    })
                    .OrderByDescending(x => x.JobDescriptionId)
                    .ToListAsync();

                if (jobs.Count == 0)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "No job descriptions found.",
                        request.CompanyId,
                        IsAdmin = isAdmin,
                        Summary = new
                        {
                            TotalTemplates = totalTemplates,
                            TotalCandidates = 0,
                            TotalApplications = 0,
                            TotalJDs = 0,
                            TotalResumes = 0,
                            Approved = 0,
                            Rejected = 0,
                            Returned = 0,
                            DistinctCandidates = 0,
                            Pending = 0,
                            TotalTasks = 0
                        },
                        Data = Array.Empty<object>()
                    });
                }

                var jobIds = jobs
                    .Select(x => x.JobDescriptionId)
                    .ToArray();

                var templateIds = jobs
                    .Where(x => x.TemplateId.HasValue)
                    .Select(x => x.TemplateId!.Value)
                    .Distinct()
                    .ToArray();

                // Candidate applications are loaded once.
                // Multiple skills/history rows cannot multiply resume counts.
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

                    where application.CompanyId == request.CompanyId
                          && jobIds.Contains(application.JobDescriptionId)
                          && application.IsActive == true
                          && candidate.IsActive == true

                    select new
                    {
                        application.CandidateApplicationId,
                        application.JobDescriptionId,
                        application.CandidateId,
                        candidate.ResumePath
                    }
                ).ToListAsync();

                var applicationIds = applications
                    .Select(x => x.CandidateApplicationId)
                    .Distinct()
                    .ToArray();

                // Decisions are counted by candidate application, not by skill.
                var historyQuery = _context.CandidateSkillAssessmentHistories
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        jobIds.Contains(x.JobDescriptionId) &&
                        applicationIds.Contains(x.CandidateApplicationId) &&
                        (x.Action == "Approve" || x.Action == "Reject" || x.Action == "Return"));

                if (!isAdmin)
                {
                    historyQuery = historyQuery.Where(x => x.CreatedBy == request.User);
                }

                var decisions = await historyQuery
                    .Select(x => new
                    {
                        x.CandidateSkillAssessmentHistoryId,
                        x.JobDescriptionId,
                        x.CandidateApplicationId,
                        x.AssignmentId,
                        x.CreatedBy,
                        x.Action,
                        x.CreatedOn
                    })
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
                    .ToListAsync();

                var decisionsByApplication = decisions.ToLookup(x => (
                    x.JobDescriptionId,
                    x.CandidateApplicationId));

                // Admin completion requires every active approval assignment.
                var approvalAssignments = await assignmentsQuery
                    .Where(x => templateIds.Contains(x.TemplateId) && x.CanApprove == true)
                    .Select(x => new
                    {
                        x.TemplateId,
                        x.AssignmentId,
                        x.EmployeeId
                    })
                    .ToListAsync();

                var pendingApprovalRequests = await _context.RecruitmentNotifications
                    .AsNoTracking()
                    .Where(x =>
                        isAdmin &&
                        x.CompanyId == request.CompanyId &&
                        jobIds.Contains(x.JobDescriptionId) &&
                        applicationIds.Contains(x.CandidateApplicationId) &&
                        x.IsActionRequest == true &&
                        x.ActionedByHistoryId == null)
                    .Select(x => new
                    {
                        x.JobDescriptionId,
                        x.CandidateApplicationId,
                        x.TemplateId
                    })
                    .Distinct()
                    .ToListAsync();

                var pendingApprovalKeys = pendingApprovalRequests
                    .Select(x => (x.JobDescriptionId, x.CandidateApplicationId, (int?)x.TemplateId))
                    .ToHashSet();

                var applicationsByJob = applications.ToLookup(x => x.JobDescriptionId);

                string DecisionStatus(string? action)
                {
                    return action switch
                    {
                        "Approve" => "Approved",
                        "Reject" => "Rejected",
                        "Return" => "Returned",
                        _ => "Pending"
                    };
                }

                // JD placement data supplies branch/department labels
                // even when the JD has no candidates yet.
                var placements = await (
                    from placement in _context.Set<JobDescriptionPlacement>()
                        .AsNoTracking()

                    join branch in _context.Branches.AsNoTracking()
                        on new
                        {
                            placement.CompanyId,
                            placement.BranchId
                        }
                        equals new
                        {
                            branch.CompanyId,
                            branch.BranchId
                        }

                    join department in _context.Departments.AsNoTracking()
                        on new
                        {
                            placement.CompanyId,
                            placement.BranchId,
                            placement.DepartmentId
                        }
                        equals new
                        {
                            department.CompanyId,
                            department.BranchId,
                            department.DepartmentId
                        }

                    where placement.CompanyId == request.CompanyId
                          && jobIds.Contains(placement.JobDescriptionId)

                    select new
                    {
                        placement.JobDescriptionId,
                        branch.BranchId,
                        branch.BranchName,
                        department.DepartmentId,
                        department.DepartmentName
                    }
                )
                .Distinct()
                .ToListAsync();

                var placementsByJob = placements.ToLookup(x =>
                    x.JobDescriptionId);

                var auditIds = jobs
                    .SelectMany(x => new int?[]
                    {
  x.CreatedBy,
  x.ModifiedBy
                    })
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToArray();

                var auditNames = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        auditIds.Contains(x.EmployeeId))
                    .Select(x => new
                    {
                        x.EmployeeId,
                        Name =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim()
                    })
                    .ToDictionaryAsync(x => x.EmployeeId, x => x.Name);

                string? GetName(int? id)
                {
                    return id.HasValue &&
                           auditNames.TryGetValue(id.Value, out var name)
                        ? name
                        : null;
                }

                var data = jobs.Select(job =>
                {
                    var jobApplications =
                        applicationsByJob[job.JobDescriptionId].ToList();

                    int approved = 0;
                    int rejected = 0;
                    int returned = 0;
                    int pending = 0;

                    foreach (var application in jobApplications)
                    {
                        var applicationDecisions = decisionsByApplication[(
                            job.JobDescriptionId,
                            application.CandidateApplicationId)].ToList();

                        string status;
                        if (!isAdmin)
                        {
                            // Already restricted to the logged-in employee.
                            // A later Save/AddRemark does not replace a decision.
                            status = DecisionStatus(applicationDecisions.FirstOrDefault()?.Action);
                        }
                        else
                        {
                            var required = approvalAssignments
                                .Where(x => x.TemplateId == job.TemplateId)
                                .ToList();

                            var latestCompanyDecision = applicationDecisions.FirstOrDefault();
                            var latestByAssignment = required.Select(assignment =>
                                applicationDecisions.FirstOrDefault(x =>
                                    x.AssignmentId == assignment.AssignmentId &&
                                    x.CreatedBy == assignment.EmployeeId)).ToList();

                            if (latestCompanyDecision?.Action == "Reject")
                                status = "Rejected";
                            else if (latestCompanyDecision?.Action == "Return")
                                status = "Returned";
                            else if (latestByAssignment.Any(x => x?.Action == "Reject"))
                                status = "Rejected";
                            else if (latestByAssignment.Any(x => x?.Action == "Return"))
                                status = "Returned";
                            else if (required.Count > 0 &&
                                     latestByAssignment.All(x => x?.Action == "Approve") &&
                                     !pendingApprovalKeys.Contains((job.JobDescriptionId,
                                         application.CandidateApplicationId, job.TemplateId)))
                                status = "Approved";
                            else
                                status = "Pending";
                        }

                        switch (status)
                        {
                            case "Approved": approved++; break;
                            case "Rejected": rejected++; break;
                            case "Returned": returned++; break;
                            default: pending++; break;
                        }
                    }

                    var jobPlacements =
                        placementsByJob[job.JobDescriptionId].ToList();

                    var departments = jobPlacements
                        .Select(x => new
                        {
                            x.DepartmentId,
                            x.DepartmentName
                        })
                        .Distinct()
                        .ToList();

                    var branches = jobPlacements
                        .Select(x => new
                        {
                            x.BranchId,
                            x.BranchName
                        })
                        .Distinct()
                        .ToList();

                    return new
                    {
                        job.JobDescriptionId,
                        job.JobTitle,
                        job.TemplateId,

                        DepartmentNames = string.Join(
                            ", ",
                            departments
                                .Select(x => x.DepartmentName)
                                .Distinct()),

                        Departments = departments,
                        Branches = branches,

                        TotalCandidates = jobApplications.Count,
                        DistinctCandidates = jobApplications
                            .Select(x => x.CandidateId)
                            .Distinct()
                            .Count(),
                        TotalApplications = jobApplications.Count,

                        Resumes = jobApplications.Count(x =>
                            !string.IsNullOrWhiteSpace(x.ResumePath)),

                        Approved = approved,
                        Rejected = rejected,
                        Returned = returned,
                        Pending = pending,
                        TotalTasks = approved + rejected + returned + pending,

                        job.CreatedBy,
                        CreatedByName = GetName(job.CreatedBy),
                        job.CreatedOn,

                        job.ModifiedBy,
                        ModifiedByName = GetName(job.ModifiedBy),
                        job.ModifiedOn
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = "JD statistics fetched successfully.",
                    request.CompanyId,
                    EmployeeId = employee.EmployeeId,
                    employee.EmployeeName,
                    IsAdmin = isAdmin,

                    Summary = new
                    {
                        TotalTemplates = totalTemplates,
                        TotalCandidates = applications.Count,
                        DistinctCandidates = applications
                            .Select(x => x.CandidateId)
                            .Distinct()
                            .Count(),
                        TotalApplications = applications.Count,
                        TotalJDs = data.Count,
                        TotalResumes = data.Sum(x => x.Resumes),
                        Approved = data.Sum(x => x.Approved),
                        Rejected = data.Sum(x => x.Rejected),
                        Returned = data.Sum(x => x.Returned),
                        Pending = data.Sum(x => x.Pending),
                        TotalTasks = data.Sum(x => x.TotalTasks)
                    },

                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch JD statistics for CompanyId {CompanyId}, User {UserId}.",
                    request.CompanyId,
                    request.User);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch JD statistics."
                });
            }
        }




        public class GetJDStatisticsRequest
        {
            public int CompanyId { get; set; }

            // EmployeeMasters.EmployeeId
            public int User { get; set; }

            // Optional: null, [] or a list containing 0 means all.
            public List<int>? JobDescriptionIds { get; set; }
        }
    }
}
