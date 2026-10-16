using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;

namespace Recruitment.Controllers
{
    [Route("api/OnBoardingController")]
    [ApiController]
    public class OnBoardingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public OnBoardingController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }





        [HttpPost("GetOnboardingCandidateList")]
        public async Task<IActionResult> GetOnboardingCandidateList(
    [FromBody] GetOnboardingCandidateListRequest? request)
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
                return Fail(
                    400,
                    "Valid CompanyId and UserId are required.");
            }

            if (request.BranchIds?.Any(id => id < 0) == true ||
                request.DepartmentIds?.Any(id => id < 0) == true ||
                request.JobDescriptionIds?.Any(id => id < 0) == true)
            {
                return Fail(400, "Filter IDs cannot be negative.");
            }

            static int[] GetFilterIds(List<int>? values)
            {
                return values == null ||
                       values.Count == 0 ||
                       values.Contains(0)
                    ? Array.Empty<int>()
                    : values.Distinct().ToArray();
            }

            try
            {
                var currentUser = await _context.EmployeeMasters
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

                if (currentUser == null)
                {
                    return Fail(
                        404,
                        "Employee not found for this company.");
                }

                if (currentUser.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                bool isAdmin = currentUser.IsAdmin == true;

                var branchIds = GetFilterIds(request.BranchIds);
                var departmentIds = GetFilterIds(request.DepartmentIds);
                var jobIds = GetFilterIds(request.JobDescriptionIds);

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

                var query =
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

                    where candidate.IsActive == true

                    select new
                    {
                        application.CompanyId,
                        application.CandidateApplicationId,
                        application.JobDescriptionId,
                        application.BranchId,
                        application.DepartmentId,
                        application.AppliedOn,
                        application.Stage,
                        ApplicationStatus = application.Status,
                        application.ExpectedCTC,
                        application.OfferedCTC,
                        application.Currency,

                        candidate.CandidateId,
                        candidate.FirstName,
                        candidate.MiddleName,
                        candidate.LastName,
                        candidate.Email,
                        candidate.MobileNumber,
                        candidate.CurrentCity,
                        candidate.TotalExperienceYears,
                        candidate.CurrentDesignation,
                        candidate.ResumePath,

                        job.JobTitle,
                        TemplateId = (int?)job.TemplateId
                    };

                // Non-admin users can see only their assigned templates.
                if (!isAdmin)
                {
                    query = query.Where(row =>
                        _context.RoleTemplateAssignment.Any(assignment =>
                            assignment.CompanyId == request.CompanyId &&
                            assignment.EmployeeId == request.UserId &&
                            assignment.TemplateId == row.TemplateId &&
                            assignment.IsActive == true));
                }

                var applications = await query.ToListAsync();

                var applicationIds = applications
                    .Select(x => x.CandidateApplicationId)
                    .Distinct()
                    .ToArray();

                var templateIds = applications
                    .Where(x =>
                        x.TemplateId.HasValue &&
                        x.TemplateId.Value > 0)
                    .Select(x => x.TemplateId!.Value)
                    .Distinct()
                    .ToArray();

                // Keep active assignments only, from active templates.
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
                        assignment.HierarchyLevel,
                        assignment.CanApprove,
                        assignment.CanReject,
                        assignment.CanReturn
                    }
                ).ToListAsync();

                var assignmentIds = assignments
                    .Select(x => x.AssignmentId)
                    .Distinct()
                    .ToArray();

                var mappings = await _context.RoleTemplateAssignmentSkills
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        assignmentIds.Contains(x.AssignmentId) &&
                        x.IsActive == true)
                    .Select(x => new
                    {
                        x.AssignmentId,
                        x.SkillId
                    })
                    .Distinct()
                    .ToListAsync();

                var mappedSkillIds = mappings
                    .Select(x => x.SkillId)
                    .Distinct()
                    .ToArray();

                var skills = await _context.CompanySkillMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        mappedSkillIds.Contains(x.CompanySkillMasterId))
                    .Select(x => new
                    {
                        x.CompanySkillMasterId,
                        x.IsDisable
                    })
                    .ToDictionaryAsync(x => x.CompanySkillMasterId);

                var mappingsByAssignment = mappings.ToLookup(
                    x => x.AssignmentId);

                var assignmentsByTemplate = assignments.ToLookup(
                    x => x.TemplateId);

                // Parent assessments represent current state.
                // Editing a history row does not change readiness.
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
                        x.CreatedOn,
                        x.ModifiedOn
                    })
                    .ToListAsync();

                var assessmentsByApplication = assessments.ToLookup(x => (
                    x.CandidateApplicationId,
                    x.JobDescriptionId
                ));

                // An outstanding approval request prevents onboarding readiness.
                var pendingRequests = await _context.RecruitmentNotifications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        applicationIds.Contains(x.CandidateApplicationId) &&
                        x.IsActionRequest == true &&
                        x.ActionedByHistoryId == null)
                    .Select(x => new
                    {
                        x.CandidateApplicationId,
                        x.JobDescriptionId,
                        x.TemplateId
                    })
                    .Distinct()
                    .ToListAsync();

                var pendingRequestKeys = pendingRequests
                    .Select(x => (
                        x.CandidateApplicationId,
                        x.JobDescriptionId,
                        x.TemplateId
                    ))
                    .ToHashSet();

                var readyApplicationIds = new HashSet<int>();

                foreach (var application in applications)
                {
                    if (!application.TemplateId.HasValue)
                        continue;

                    int templateId = application.TemplateId.Value;

                    var templateAssignments =
                        assignmentsByTemplate[templateId].ToList();

                    var requiredAssignments = templateAssignments
                        .Where(x => x.CanApprove == true)
                        .ToList();

                    // An application without required approval assignments
                    // must not automatically qualify.
                    if (requiredAssignments.Count == 0)
                        continue;

                    if (pendingRequestKeys.Contains((
                            application.CandidateApplicationId,
                            application.JobDescriptionId,
                            templateId)))
                    {
                        continue;
                    }

                    var applicationAssessments =
                        assessmentsByApplication[(
                            application.CandidateApplicationId,
                            application.JobDescriptionId
                        )].ToList();

                    // Select the current record for each assignment/skill pair.
                    var currentAssessments = applicationAssessments
                        .GroupBy(x => new
                        {
                            x.AssignmentId,
                            x.CompanySkillMasterId
                        })
                        .Select(group => group
                            .OrderByDescending(x =>
                                x.ModifiedOn ?? x.CreatedOn)
                            .ThenByDescending(x =>
                                x.CandidateSkillAssessmentId)
                            .First())
                        .ToList();

                    var currentAssignmentIds = templateAssignments
                        .Select(x => x.AssignmentId)
                        .ToHashSet();

                    // A current rejection or return from the active template
                    // prevents readiness.
                    bool hasBlockingDecision = currentAssessments.Any(x =>
                        x.AssignmentId.HasValue &&
                        currentAssignmentIds.Contains(x.AssignmentId.Value) &&
                        (
                            string.Equals(
                                x.Status,
                                "Rejected",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                x.Status,
                                "Returned",
                                StringComparison.OrdinalIgnoreCase)
                        ));

                    if (hasBlockingDecision)
                        continue;

                    bool allApproved = true;

                    foreach (var assignment in requiredAssignments)
                    {
                        var assignmentMappings =
                            mappingsByAssignment[assignment.AssignmentId]
                                .ToList();

                        // Missing mapped skills must not silently turn
                        // an assignment into an approved default assessment.
                        if (assignmentMappings.Any(mapping =>
                            !skills.ContainsKey(mapping.SkillId)))
                        {
                            allApproved = false;
                            break;
                        }

                        var requiredSkillIds = assignmentMappings
                            .Where(mapping =>
                                skills[mapping.SkillId].IsDisable != true)
                            .Select(mapping => mapping.SkillId)
                            .Distinct()
                            .ToArray();

                        // No enabled skill mappings = Default Remark.
                        if (requiredSkillIds.Length == 0)
                            requiredSkillIds = new[] { 0 };

                        foreach (var skillId in requiredSkillIds)
                        {
                            var assessment = currentAssessments
                                .FirstOrDefault(x =>
                                    x.AssignmentId == assignment.AssignmentId &&
                                    x.CompanySkillMasterId == skillId);

                            if (assessment == null ||
                                !string.Equals(
                                    assessment.Status,
                                    "Approved",
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                allApproved = false;
                                break;
                            }
                        }

                        if (!allApproved)
                            break;
                    }

                    if (allApproved)
                    {
                        readyApplicationIds.Add(
                            application.CandidateApplicationId);
                    }
                }

                var readyApplications = applications
                    .Where(x => readyApplicationIds.Contains(
                        x.CandidateApplicationId))
                    .OrderByDescending(x => x.AppliedOn)
                    .ThenByDescending(x => x.CandidateApplicationId)
                    .ToList();

                var resultBranchIds = readyApplications
                    .Where(x => x.BranchId.HasValue)
                    .Select(x => x.BranchId!.Value)
                    .Distinct()
                    .ToArray();

                var resultDepartmentIds = readyApplications
                    .Where(x => x.DepartmentId.HasValue)
                    .Select(x => x.DepartmentId!.Value)
                    .Distinct()
                    .ToArray();

                var branchNames = await _context.Branches
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultBranchIds.Contains(x.BranchId))
                    .ToDictionaryAsync(
                        x => x.BranchId,
                        x => x.BranchName);

                var departmentNames = await _context.Departments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultDepartmentIds.Contains(x.DepartmentId))
                    .ToDictionaryAsync(
                        x => x.DepartmentId,
                        x => x.DepartmentName);

                var data = readyApplications.Select(application =>
                {
                    string? branchName = null;
                    string? departmentName = null;

                    if (application.BranchId.HasValue)
                    {
                        branchNames.TryGetValue(
                            application.BranchId.Value,
                            out branchName);
                    }

                    if (application.DepartmentId.HasValue)
                    {
                        departmentNames.TryGetValue(
                            application.DepartmentId.Value,
                            out departmentName);
                    }

                    return new
                    {
                        application.CompanyId,
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
                        application.TotalExperienceYears,
                        application.CurrentDesignation,
                        application.ResumePath,

                        application.JobDescriptionId,
                        application.JobTitle,
                        application.TemplateId,

                        application.BranchId,
                        BranchName = branchName,

                        application.DepartmentId,
                        DepartmentName = departmentName,

                        application.ExpectedCTC,
                        application.OfferedCTC,
                        application.Currency,

                        application.Stage,
                        application.ApplicationStatus,
                        application.AppliedOn,

                        Status = "Approved",
                        IsReadyForOnboarding = true
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Candidates ready for onboarding fetched successfully."
                        : "No candidates are ready for onboarding.",
                    IsAdmin = isAdmin,
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch onboarding candidates for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch onboarding candidate list."
                });
            }
        }













        public class GetOnboardingCandidateListRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }

            public List<int>? BranchIds { get; set; }
            public List<int>? DepartmentIds { get; set; }
            public List<int>? JobDescriptionIds { get; set; }
        }
    }
}
