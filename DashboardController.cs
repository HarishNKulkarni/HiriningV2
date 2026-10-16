using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;

namespace Recruitment.Controllers
{
    [Route("api/DashboardController")]
    [ApiController]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public DashboardController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }



        [HttpPost("GetRecruitmentDashboard")]
        public async Task<IActionResult> GetRecruitmentDashboard(
    [FromBody] GetRecruitmentDashboardRequest? request)
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
                request.UserId <= 0)
            {
                return Fail(400, "Valid CompanyId and UserId are required.");
            }

            if (request.BranchIds?.Any(x => x < 0) == true ||
                request.DepartmentIds?.Any(x => x < 0) == true ||
                request.JobDescriptionIds?.Any(x => x < 0) == true)
            {
                return Fail(400, "Filter IDs cannot be negative.");
            }

            static int[] FilterIds(List<int>? values)
            {
                return values == null ||
                       values.Count == 0 ||
                       values.Contains(0)
                    ? Array.Empty<int>()
                    : values.Distinct().ToArray();
            }

            try
            {
                var user = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsAdmin,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                bool isAdmin = user.IsAdmin == true;

                var branchIds = FilterIds(request.BranchIds);
                var departmentIds = FilterIds(request.DepartmentIds);
                var jobIds = FilterIds(request.JobDescriptionIds);

                // My Tasks is the employee's configured skill assignments.
                // Load independently of applications: tasks remain visible even
                // when no candidate has applied. Candidate filters below retain
                // their original effect on counts and candidate lists only.
                var myTaskRows = await (
                    from assignment in _context.RoleTemplateAssignment.AsNoTracking()
                    join template in _context.RoleTemplates.AsNoTracking()
                        on new { assignment.CompanyId, assignment.TemplateId }
                        equals new { template.CompanyId, template.TemplateId }
                    join mapping in _context.RoleTemplateAssignmentSkills.AsNoTracking()
                        on new { assignment.CompanyId, assignment.AssignmentId }
                        equals new { mapping.CompanyId, mapping.AssignmentId }
                    join skill in _context.CompanySkillMasters.AsNoTracking()
                        on new { mapping.CompanyId, SkillId = mapping.SkillId }
                        equals new { skill.CompanyId, SkillId = skill.CompanySkillMasterId }
                    join category in _context.CompanySkillMappings.AsNoTracking()
                        on new { skill.CompanyId, skill.SkillMappingId }
                        equals new { category.CompanyId, category.SkillMappingId }
                    join role in _context.Roles.AsNoTracking()
                        on new { assignment.CompanyId, assignment.RoleId }
                        equals new { role.CompanyId, role.RoleId }
                    where assignment.CompanyId == request.CompanyId &&
                          assignment.EmployeeId == request.UserId &&
                          assignment.IsActive == true &&
                          template.IsActive == true &&
                          mapping.IsActive == true &&
                          skill.IsActive == true &&
                          skill.IsDisable != true &&
                          category.IsActive == true
                    select new
                    {
                        SkillId = skill.CompanySkillMasterId,
                        skill.SkillName,
                        CategoryId = category.SkillMappingId,
                        CategoryName = category.CategoryName,
                        assignment.AssignmentId,
                        assignment.TemplateId,
                        template.TemplateName,
                        assignment.RoleId,
                        role.RoleName,
                        assignment.EmployeeId,
                        assignment.HierarchyLevel,
                        assignment.Stage,
                        assignment.CanApprove,
                        assignment.CanReject,
                        assignment.CanReturn
                    }
                ).Distinct().ToListAsync();

                // One card per skill, retaining every assignment context.
                var myTasks = myTaskRows
                    .GroupBy(x => new
                    {
                        x.SkillId,
                        x.SkillName,
                        x.CategoryId,
                        x.CategoryName
                    })
                    .OrderBy(group => group.Key.SkillName)
                    .ThenBy(group => group.Key.SkillId)
                    .Select(group => new
                    {
                        group.Key.SkillId,
                        group.Key.SkillName,
                        group.Key.CategoryId,
                        group.Key.CategoryName,
                        IsAssigned = true,
                        AllowedForRoles = group
                            .Select(x => x.RoleName)
                            .Distinct()
                            .OrderBy(name => name)
                            .ToList(),
                        Assignments = group
                            .OrderBy(x => x.TemplateId)
                            .ThenBy(x => x.HierarchyLevel)
                            .ThenBy(x => x.AssignmentId)
                            .Select(x => new
                            {
                                x.AssignmentId,
                                x.TemplateId,
                                x.TemplateName,
                                x.EmployeeId,
                                x.RoleId,
                                x.RoleName,
                                x.HierarchyLevel,
                                x.Stage,
                                x.CanApprove,
                                x.CanReject,
                                x.CanReturn
                            })
                            .ToList()
                    })
                    .ToList();

                // Template configuration flows are independent of candidate counts.
                // Role users see all active templates assigned to them; admins see
                // all active company templates, including ones without candidates.
                var flowTemplatesQuery = _context.RoleTemplates
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId && x.IsActive == true);

                if (!isAdmin)
                {
                    flowTemplatesQuery = flowTemplatesQuery.Where(template =>
                        _context.RoleTemplateAssignment.Any(assignment =>
                            assignment.CompanyId == template.CompanyId &&
                            assignment.TemplateId == template.TemplateId &&
                            assignment.EmployeeId == request.UserId &&
                            assignment.IsActive == true));
                }

                var flowTemplates = await flowTemplatesQuery
                    .Select(x => new { x.TemplateId, x.TemplateName })
                    .OrderBy(x => x.TemplateName)
                    .ThenBy(x => x.TemplateId)
                    .ToListAsync();

                var flowTemplateIds = flowTemplates.Select(x => x.TemplateId).ToArray();

                // Load every role in the allowed templates, not just the user's row.
                var flowAssignments = await _context.RoleTemplateAssignment
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                flowTemplateIds.Contains(x.TemplateId) &&
                                x.IsActive == true)
                    .Select(x => new
                    {
                        x.TemplateId,
                        x.AssignmentId,
                        x.RoleId,
                        x.HierarchyLevel
                    })
                    .ToListAsync();

                var flowRoleIds = flowAssignments.Select(x => x.RoleId).Distinct().ToArray();
                var flowRoleNames = await _context.Roles.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId && flowRoleIds.Contains(x.RoleId))
                    .Select(x => new { x.RoleId, x.RoleName })
                    .ToDictionaryAsync(x => x.RoleId, x => x.RoleName);

                var flowAssignmentsByTemplate = flowAssignments.ToLookup(x => x.TemplateId);
                var templateApprovalPaths = flowTemplates.Select(template => new
                {
                    template.TemplateId,
                    template.TemplateName,
                    ApprovalPath = flowAssignmentsByTemplate[template.TemplateId]
                        .OrderBy(x => x.HierarchyLevel)
                        .ThenBy(x => x.AssignmentId)
                        .Select(x => new
                        {
                            x.AssignmentId,
                            x.RoleId,
                            RoleName = flowRoleNames.TryGetValue(x.RoleId, out var name) ? name : null,
                            x.HierarchyLevel,
                            HierarchyLabel = $"H{x.HierarchyLevel}"
                        })
                        .ToList()
                }).ToList();

                var applicationQuery = _context.CandidateApplications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive == true);

                if (branchIds.Length > 0)
                {
                    applicationQuery = applicationQuery.Where(x =>
                        x.BranchId.HasValue &&
                        branchIds.Contains(x.BranchId.Value));
                }

                if (departmentIds.Length > 0)
                {
                    applicationQuery = applicationQuery.Where(x =>
                        x.DepartmentId.HasValue &&
                        departmentIds.Contains(x.DepartmentId.Value));
                }

                if (jobIds.Length > 0)
                {
                    applicationQuery = applicationQuery.Where(x =>
                        jobIds.Contains(x.JobDescriptionId));
                }

                var applications = await (
                    from application in applicationQuery

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

                    join job in _context.JobDescriptions.AsNoTracking()
                        on new
                        {
                            application.CompanyId,
                            application.JobDescriptionId
                        }
                        equals new
                        {
                            job.CompanyId,
                            job.JobDescriptionId
                        }

                    select new
                    {
                        application.CandidateApplicationId,
                        application.JobDescriptionId,
                        application.BranchId,
                        application.DepartmentId,
                        application.AppliedOn,
                        application.Stage,
                        ApplicationStatus = application.Status,

                        candidate.CandidateId,
                        candidate.FirstName,
                        candidate.MiddleName,
                        candidate.LastName,
                        candidate.Email,
                        candidate.MobileNumber,
                        candidate.CurrentCity,

                        job.JobTitle,
                        TemplateId = (int?)job.TemplateId
                    }
                ).ToListAsync();

                var applicationIds = applications
                    .Select(x => x.CandidateApplicationId)
                    .Distinct()
                    .ToArray();

                var templateIds = applications
                    .Where(x => x.TemplateId.HasValue)
                    .Select(x => x.TemplateId!.Value)
                    .Distinct()
                    .ToArray();

                // Active assignments in active templates only.
                var assignments = await (
                    from assignment in _context.RoleTemplateAssignment
                        .AsNoTracking()

                    join template in _context.RoleTemplates.AsNoTracking()
                        on new
                        {
                            assignment.CompanyId,
                            assignment.TemplateId
                        }
                        equals new
                        {
                            template.CompanyId,
                            template.TemplateId
                        }

                    where assignment.CompanyId == request.CompanyId &&
                          templateIds.Contains(assignment.TemplateId) &&
                          assignment.IsActive == true &&
                          template.IsActive == true

                    select new
                    {
                        assignment.AssignmentId,
                        assignment.TemplateId,
                        assignment.EmployeeId,
                        assignment.RoleId,
                        assignment.HierarchyLevel,
                        assignment.Stage,
                        assignment.CanApprove,
                        assignment.CanReject,
                        assignment.CanReturn
                    }
                ).ToListAsync();

                var assignmentIds = assignments
                    .Select(x => x.AssignmentId)
                    .Distinct()
                    .ToArray();

                // Disabled skills are not individual requirements.
                // An assignment with no enabled skills uses Default Remark (0).
                var skillMappings = await (
                    from mapping in _context.RoleTemplateAssignmentSkills
                        .AsNoTracking()

                    join skill in _context.CompanySkillMasters.AsNoTracking()
                        on new
                        {
                            mapping.CompanyId,
                            SkillId = mapping.SkillId
                        }
                        equals new
                        {
                            skill.CompanyId,
                            SkillId = skill.CompanySkillMasterId
                        }

                    where mapping.CompanyId == request.CompanyId &&
                          assignmentIds.Contains(mapping.AssignmentId) &&
                          mapping.IsActive == true &&
                          skill.IsDisable != true

                    select new
                    {
                        mapping.AssignmentId,
                        mapping.SkillId
                    }
                ).Distinct().ToListAsync();

                var skillsByAssignment = skillMappings.ToLookup(
                    x => x.AssignmentId,
                    x => x.SkillId);

                // Read parent assessment status: history-only edits must not
                // change the dashboard's current assessment state.
                var assessments = await _context.CandidateSkillAssessments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        applicationIds.Contains(x.CandidateApplicationId) &&
                        x.IsActive == true)
                    .Select(x => new
                    {
                        x.CandidateSkillAssessmentId,
                        x.CandidateApplicationId,
                        x.JobDescriptionId,
                        x.AssignmentId,
                        x.CompanySkillMasterId,
                        x.Status,
                        x.CreatedBy,
                        x.CreatedOn,
                        x.ModifiedOn
                    })
                    .ToListAsync();

                var assessmentsByApplication = assessments.ToLookup(x => (
                    x.CandidateApplicationId,
                    x.JobDescriptionId
                ));

                // Used only to recognize whether initial routing has started.
                var decisionHistory = await _context
                    .CandidateSkillAssessmentHistories
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        applicationIds.Contains(x.CandidateApplicationId) &&
                        (
                            x.Action == "Approve" ||
                            x.Action == "Reject" ||
                            x.Action == "Return"
                        ))
                    .Select(x => new
                    {
                        x.CandidateApplicationId,
                        x.JobDescriptionId
                    })
                    .Distinct()
                    .ToListAsync();

                var applicationsWithDecisions = decisionHistory
                    .Select(x => (
                        x.CandidateApplicationId,
                        x.JobDescriptionId
                    ))
                    .ToHashSet();

                // Include completed requests as well, so role users can count
                // applications previously received and subsequently actioned.
                var notifications = await _context.RecruitmentNotifications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        applicationIds.Contains(x.CandidateApplicationId) &&
                        x.IsActionRequest == true)
                    .Select(x => new
                    {
                        x.NotificationId,
                        x.CandidateApplicationId,
                        x.JobDescriptionId,
                        x.TemplateId,
                        x.ToUserId,
                        x.ToAssignmentId,
                        x.ActionedByHistoryId
                    })
                    .ToListAsync();

                var notificationsByApplication = notifications.ToLookup(x => (
                    x.CandidateApplicationId,
                    x.JobDescriptionId
                ));

                var resultBranchIds = applications
                    .Where(x => x.BranchId.HasValue)
                    .Select(x => x.BranchId!.Value)
                    .Distinct()
                    .ToArray();

                var resultDepartmentIds = applications
                    .Where(x => x.DepartmentId.HasValue)
                    .Select(x => x.DepartmentId!.Value)
                    .Distinct()
                    .ToArray();

                var branches = await _context.Branches
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultBranchIds.Contains(x.BranchId))
                    .Select(x => new
                    {
                        x.BranchId,
                        x.BranchName
                    })
                    .ToDictionaryAsync(
                        x => x.BranchId,
                        x => x.BranchName);

                var departments = await _context.Departments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultDepartmentIds.Contains(x.DepartmentId))
                    .Select(x => new
                    {
                        x.DepartmentId,
                        x.DepartmentName
                    })
                    .ToDictionaryAsync(
                        x => x.DepartmentId,
                        x => x.DepartmentName);

                // One status per candidate application.
                // A candidate applying for two jobs counts as two applications.
                var dashboardStatuses = new Dictionary<int, string>();
                var actionableApplicationIds = new HashSet<int>();

                foreach (var application in applications)
                {
                    var key = (
                        application.CandidateApplicationId,
                        application.JobDescriptionId
                    );

                    var templateAssignments = assignments
                        .Where(x => x.TemplateId == application.TemplateId)
                        .OrderBy(x => x.HierarchyLevel)
                        .ThenBy(x => x.AssignmentId)
                        .ToList();

                    var applicationAssessments =
                        assessmentsByApplication[key].ToList();

                    var applicationNotifications =
                        notificationsByApplication[key]
                            .Where(x => x.TemplateId == application.TemplateId)
                            .ToList();

                    string GetAssignmentStatus(int assignmentId)
                    {
                        var requiredSkillIds = skillsByAssignment[assignmentId]
                            .Distinct()
                            .ToArray();

                        if (requiredSkillIds.Length == 0)
                            requiredSkillIds = new[] { 0 };

                        var currentStatuses = requiredSkillIds
                            .Select(skillId =>
                                applicationAssessments
                                    .Where(x =>
                                        x.AssignmentId == assignmentId &&
                                        x.CompanySkillMasterId == skillId)
                                    .OrderByDescending(x =>
                                        x.ModifiedOn ?? x.CreatedOn)
                                    .ThenByDescending(x =>
                                        x.CandidateSkillAssessmentId)
                                    .Select(x => x.Status)
                                    .FirstOrDefault())
                            .ToArray();

                        if (currentStatuses.Any(x =>
                            string.Equals(
                                x, "Rejected",
                                StringComparison.OrdinalIgnoreCase)))
                        {
                            return "Rejected";
                        }

                        if (currentStatuses.All(x =>
                            string.Equals(
                                x, "Approved",
                                StringComparison.OrdinalIgnoreCase)))
                        {
                            return "Approved";
                        }

                        // Missing, Pending, InProgress and Returned.
                        return "Pending";
                    }

                    var firstAssignment = templateAssignments
                        .FirstOrDefault(x =>
                            x.CanApprove == true ||
                            x.CanReject == true ||
                            x.CanReturn == true);

                    bool routingNotStarted =
                        !applicationsWithDecisions.Contains(key) &&
                        applicationNotifications.Count == 0;

                    var assignmentIdSet = templateAssignments
                        .Select(x => x.AssignmentId)
                        .ToHashSet();

                    if (isAdmin)
                    {
                        var requiredAssignments = templateAssignments
                            .Where(x => x.CanApprove == true)
                            .ToList();

                        var requiredStatuses = requiredAssignments
                            .Select(x => GetAssignmentStatus(x.AssignmentId))
                            .ToList();

                        // Also recognize rejection by an assignment permitted
                        // to reject, even if it cannot approve.
                        bool hasRejection = templateAssignments
                            .Where(x =>
                                x.CanApprove == true ||
                                x.CanReject == true)
                            .Any(x =>
                                GetAssignmentStatus(x.AssignmentId) ==
                                    "Rejected");

                        bool hasPendingRequest = applicationNotifications
                            .Any(x =>
                                !x.ActionedByHistoryId.HasValue &&
                                x.ToAssignmentId.HasValue &&
                                assignmentIdSet.Contains(
                                    x.ToAssignmentId.Value));

                        string status;

                        if (hasRejection)
                        {
                            status = "Rejected";
                        }
                        else if (requiredStatuses.Count > 0 &&
                                 requiredStatuses.All(x => x == "Approved") &&
                                 !hasPendingRequest)
                        {
                            status = "Approved";
                        }
                        else
                        {
                            status = "Pending";
                        }

                        dashboardStatuses[application.CandidateApplicationId] =
                            status;

                        // Admin sees actionable pending applications across
                        // the filtered company scope.
                        if (status == "Pending" &&
                            (
                                hasPendingRequest ||
                                (routingNotStarted && firstAssignment != null)
                            ))
                        {
                            actionableApplicationIds.Add(
                                application.CandidateApplicationId);
                        }
                    }
                    else
                    {
                        var ownAssignments = templateAssignments
                            .Where(x => x.EmployeeId == request.UserId)
                            .ToList();

                        var ownAssignmentIds = ownAssignments
                            .Select(x => x.AssignmentId)
                            .ToHashSet();

                        var ownRequests = applicationNotifications
                            .Where(x =>
                                x.ToUserId == request.UserId &&
                                x.ToAssignmentId.HasValue &&
                                ownAssignmentIds.Contains(
                                    x.ToAssignmentId.Value))
                            .ToList();

                        bool initialTurn =
                            routingNotStarted &&
                            firstAssignment != null &&
                            firstAssignment.EmployeeId == request.UserId;

                        // Retain previously actioned applications, including
                        // records created before notifications were introduced.
                        var actedAssignmentIds = applicationAssessments
                            .Where(x =>
                                x.CreatedBy == request.UserId &&
                                x.AssignmentId.HasValue &&
                                ownAssignmentIds.Contains(x.AssignmentId.Value))
                            .Select(x => x.AssignmentId!.Value)
                            .ToHashSet();

                        var receivedAssignmentIds = ownRequests
                            .Select(x => x.ToAssignmentId!.Value)
                            .Concat(actedAssignmentIds)
                            .ToHashSet();

                        if (initialTurn)
                        {
                            receivedAssignmentIds.Add(
                                firstAssignment!.AssignmentId);
                        }

                        // Future hierarchy assignments are not included merely
                        // because this employee belongs to the template.
                        if (receivedAssignmentIds.Count == 0)
                            continue;

                        bool hasPendingRequest = ownRequests.Any(x =>
                            !x.ActionedByHistoryId.HasValue);

                        var ownStatuses = receivedAssignmentIds
                            .Select(GetAssignmentStatus)
                            .ToList();

                        string status;

                        if (initialTurn || hasPendingRequest)
                        {
                            status = "Pending";

                            actionableApplicationIds.Add(
                                application.CandidateApplicationId);
                        }
                        else if (ownStatuses.Any(x => x == "Rejected"))
                        {
                            status = "Rejected";
                        }
                        else if (ownStatuses.All(x => x == "Approved"))
                        {
                            status = "Approved";
                        }
                        else
                        {
                            status = "Pending";
                        }

                        dashboardStatuses[application.CandidateApplicationId] =
                            status;
                    }
                }

                var visibleApplications = applications
                    .Where(x => dashboardStatuses.ContainsKey(
                        x.CandidateApplicationId))
                    .OrderByDescending(x => x.AppliedOn)
                    .ThenByDescending(x => x.CandidateApplicationId)
                    .ToList();

                var candidateRows = visibleApplications.Select(application =>
                {
                    string? branchName = null;
                    string? departmentName = null;

                    if (application.BranchId.HasValue)
                    {
                        branches.TryGetValue(
                            application.BranchId.Value,
                            out branchName);
                    }

                    if (application.DepartmentId.HasValue)
                    {
                        departments.TryGetValue(
                            application.DepartmentId.Value,
                            out departmentName);
                    }

                    return new
                    {
                        application.CandidateId,
                        application.CandidateApplicationId,

                        CandidateName = string.Join(
                            " ",
                            new[]
                            {
                    application.FirstName,
                    application.MiddleName,
                    application.LastName
                            }.Where(x => !string.IsNullOrWhiteSpace(x))),

                        application.Email,
                        application.MobileNumber,
                        application.CurrentCity,

                        application.JobDescriptionId,
                        application.JobTitle,
                        application.TemplateId,

                        application.BranchId,
                        BranchName = branchName,

                        application.DepartmentId,
                        DepartmentName = departmentName,

                        application.Stage,
                        application.ApplicationStatus,

                        Status = dashboardStatuses[
                            application.CandidateApplicationId],

                        application.AppliedOn,

                        IsAwaitingAction = actionableApplicationIds.Contains(
                            application.CandidateApplicationId)
                    };
                }).ToList();

                var pendingApprovals = candidateRows
                    .Where(x => x.IsAwaitingAction)
                    .ToList();

                return Ok(new
                {
                    Status = true,
                    Message = "Dashboard fetched successfully.",
                    IsAdmin = isAdmin,

                    Data = new
                    {
                        TotalFlowTemplates = templateApprovalPaths.Count,
                      //  TemplateApprovalPaths = templateApprovalPaths,

                        MyTaskCount = myTasks.Count,
                     //   MyTasks = myTasks,
                        MyTasks = templateApprovalPaths,

                        TotalCandidate = candidateRows.Count,

                        Approved = dashboardStatuses.Values.Count(x =>
                            x == "Approved"),

                        Rejected = dashboardStatuses.Values.Count(x =>
                            x == "Rejected"),

                        Pending = dashboardStatuses.Values.Count(x =>
                            x == "Pending"),

                        RecentCandidates = candidateRows.Take(8).ToList(),

                        PendingApprovalCount = pendingApprovals.Count,
                        PendingApprovals = pendingApprovals
                    }
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch dashboard for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch recruitment dashboard."
                });
            }
        }


        public class GetRecruitmentDashboardRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }

            public List<int>? BranchIds { get; set; }
            public List<int>? DepartmentIds { get; set; }
            public List<int>? JobDescriptionIds { get; set; }
        }
    }
}
