using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.Data;
using System.Text.Json;

namespace Recruitment.Controllers
{
    [Route("api/AssessmentController")]
    [ApiController]
    public class AssessmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AssessmentController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }


        //[HttpPost("SaveCandidateSkillAssessment")]
        //public async Task<IActionResult> SaveCandidateSkillAssessment(
        // [FromBody] SaveCandidateSkillAssessmentRequest? request)
        //{
        //    IActionResult Fail(int statusCode, string message)
        //    {
        //        return StatusCode(statusCode, new
        //        {
        //            Status = false,
        //            Message = message
        //        });
        //    }

        //    if (request == null)
        //        return Fail(400, "Invalid request.");

        //    if (request.CompanyId <= 0 ||
        //        request.CandidateId <= 0 ||
        //        request.JobDescriptionId <= 0 ||
        //        request.EmployeeId <= 0 ||
        //        request.SkillId <= 0 ||
        //        request.CandidateSkillAssessmentId < 0)
        //    {
        //        return Fail(400, "Provide valid reference IDs.");
        //    }

        //    var action = request.Action?.Trim().ToLowerInvariant() switch
        //    {
        //        null => "Save",
        //        "" => "Save",
        //        "save" => "Save",
        //        "addremark" => "AddRemark",
        //        _ => null
        //    };

        //    if (action == null)
        //    {
        //        return Fail(
        //            400,
        //            "This API supports Save and AddRemark. Approval/rejection is performed separately after saving.");
        //    }

        //    bool isRemarkAdded =
        //        action == "AddRemark" || request.IsRemarkAdded;

        //    if (isRemarkAdded &&
        //        string.IsNullOrWhiteSpace(request.Remarks))
        //    {
        //        return Fail(400, "Enter the remark to add.");
        //    }

        //    bool hasDetails =
        //        request.Details.HasValue &&
        //        request.Details.Value.ValueKind != JsonValueKind.Null &&
        //        request.Details.Value.ValueKind != JsonValueKind.Undefined;

        //    if (action == "Save" &&
        //        hasDetails &&
        //        request.Details!.Value.ValueKind != JsonValueKind.Object)
        //    {
        //        return Fail(400, "Details must be a JSON object.");
        //    }

        //    try
        //    {
        //        await using var transaction =
        //            await _context.Database.BeginTransactionAsync(
        //                System.Data.IsolationLevel.Serializable);

        //        // Acting employee.
        //        var employee = await _context.EmployeeMasters
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.EmployeeId == request.EmployeeId)
        //            .Select(x => new
        //            {
        //                x.EmployeeId,
        //                x.IsAdmin,
        //                x.IsActive,

        //                EmployeeName =
        //                    ((x.FirstName ?? "") + " " +
        //                     (x.LastName ?? "")).Trim()
        //            })
        //            .FirstOrDefaultAsync();

        //        if (employee == null)
        //            return Fail(404, "Employee not found for this company.");

        //        if (employee.IsActive != true)
        //            return Fail(403, "Employee is inactive.");

        //        bool isAdmin = employee.IsAdmin == true;

        //        var candidateExists = await _context.Candidates
        //            .AsNoTracking()
        //            .AnyAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CandidateId == request.CandidateId);

        //        if (!candidateExists)
        //            return Fail(404, "Candidate not found for this company.");

        //        var job = await _context.JobDescriptions
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.JobDescriptionId == request.JobDescriptionId)
        //            .Select(x => new
        //            {
        //                x.JobDescriptionId,
        //                x.TemplateId
        //            })
        //            .FirstOrDefaultAsync();

        //        if (job == null)
        //            return Fail(404, "Job description not found for this company.");

        //        // Resolve the candidate's application for this JD.
        //        var applications = await _context.CandidateApplications
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CandidateId == request.CandidateId &&
        //                x.JobDescriptionId == request.JobDescriptionId)
        //            .Select(x => new
        //            {
        //                x.CandidateApplicationId,
        //                x.IsActive
        //            })
        //            .Take(2)
        //            .ToListAsync();

        //        if (applications.Count == 0)
        //        {
        //            return Fail(
        //                404,
        //                "Candidate application not found for this JD.");
        //        }

        //        if (applications.Count > 1)
        //        {
        //            return Fail(
        //                409,
        //                "Multiple applications exist for this candidate and JD. CandidateApplicationId is needed to identify the correct application.");
        //        }

        //        var application = applications[0];

        //        if (application.IsActive != true)
        //            return Fail(409, "Candidate application is inactive.");

        //        // Skill and category.
        //        var skill = await _context.SkillMasters
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.SkillId == request.SkillId &&
        //                x.IsActive == true)
        //            .Select(x => new
        //            {
        //                x.SkillId,
        //                x.SkillName,
        //                x.CategoryId
        //            })
        //            .FirstOrDefaultAsync();

        //        if (skill == null)
        //            return Fail(404, "Active skill not found.");

        //        var category = await _context.SkillCategories
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.CategoryId == skill.CategoryId &&
        //                x.IsActive == true)
        //            .Select(x => new
        //            {
        //                x.CategoryId,
        //                x.CategoryName
        //            })
        //            .FirstOrDefaultAsync();

        //        if (category == null)
        //            return Fail(404, "Active skill category not found.");

        //        // Non-admin assignment is determined by the JD's template.
        //        int? resolvedAssignmentId = null;

        //        if (!isAdmin)
        //        {
        //            var assignments = await _context.RoleTemplateAssignment
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.EmployeeId == employee.EmployeeId &&
        //                    x.TemplateId == job.TemplateId &&
        //                    x.IsActive == true)
        //                .Where(x =>
        //                    _context.Set<RoleTemplate>().Any(template =>
        //                        template.CompanyId == x.CompanyId &&
        //                        template.TemplateId == x.TemplateId &&
        //                        template.IsActive == true))
        //                .Where(x =>
        //                    _context.Set<RoleTemplateAssignmentSkill>().Any(mapping =>
        //                        mapping.CompanyId == x.CompanyId &&
        //                        mapping.AssignmentId == x.AssignmentId &&
        //                        mapping.SkillId == request.SkillId))
        //                .Select(x => x.AssignmentId)
        //                .Take(2)
        //                .ToListAsync();

        //            if (assignments.Count == 0)
        //            {
        //                return Fail(
        //                    403,
        //                    "This skill is not assigned to the employee in the JD's active template.");
        //            }

        //            if (assignments.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    "Multiple matching assignments exist. AssignmentId is needed to identify the correct assignment.");
        //            }

        //            resolvedAssignmentId = assignments[0];
        //        }

        //        // Include all form versions when locating this employee's
        //        // existing assessment for the requested skill.
        //        var skillDefinitionIds = _context.SkillFormDefinitions
        //            .Where(x => x.SkillId == request.SkillId)
        //            .Select(x => x.SkillFormDefinitionId);

        //        var assessmentQuery = _context.CandidateSkillAssessments
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.JobDescriptionId == request.JobDescriptionId &&
        //                x.CandidateApplicationId ==
        //                    application.CandidateApplicationId &&
        //                x.CreatedBy == employee.EmployeeId &&
        //                skillDefinitionIds.Contains(x.SkillFormDefinitionId));

        //        CandidateSkillAssessment? assessment;

        //        if (request.CandidateSkillAssessmentId > 0)
        //        {
        //            assessment = await assessmentQuery
        //                .FirstOrDefaultAsync(x =>
        //                    x.CandidateSkillAssessmentId ==
        //                        request.CandidateSkillAssessmentId);

        //            if (assessment == null)
        //            {
        //                return Fail(
        //                    404,
        //                    "Assessment not found for this employee, candidate and skill.");
        //            }
        //        }
        //        else
        //        {
        //            var existingAssessments = await assessmentQuery
        //                .Take(2)
        //                .ToListAsync();

        //            if (existingAssessments.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    "Multiple assessments exist. Send CandidateSkillAssessmentId.");
        //            }

        //            assessment = existingAssessments.FirstOrDefault();
        //        }

        //        bool isNew = assessment == null;

        //        if (isNew && action == "AddRemark")
        //        {
        //            return Fail(
        //                400,
        //                "Save the assessment first, then add further remarks.");
        //        }

        //        if (!isNew)
        //        {
        //            if (assessment!.IsActive != true)
        //                return Fail(409, "Assessment is inactive.");

        //            if (!isAdmin &&
        //                assessment.AssignmentId != resolvedAssignmentId)
        //            {
        //                return Fail(
        //                    409,
        //                    "This assessment belongs to a different assignment.");
        //            }

        //            if (action == "Save" &&
        //                (assessment.Status == "Approved" ||
        //                 assessment.Status == "Rejected"))
        //            {
        //                return Fail(
        //                    409,
        //                    "This assessment is completed. You can still use AddRemark.");
        //            }
        //        }

        //        // Existing assessments keep their original form version.
        //        SkillFormDefinition? definition;

        //        if (isNew)
        //        {
        //            var activeDefinitions = await _context.SkillFormDefinitions
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.SkillId == request.SkillId &&
        //                    x.CategoryId == skill.CategoryId &&
        //                    x.IsActive == true)
        //                .Take(2)
        //                .ToListAsync();

        //            if (activeDefinitions.Count == 0)
        //            {
        //                return Fail(
        //                    400,
        //                    "No active form definition is configured for this skill.");
        //            }

        //            if (activeDefinitions.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    "Multiple active form definitions exist for this skill. Keep one active version.");
        //            }

        //            definition = activeDefinitions[0];
        //        }
        //        else
        //        {
        //            definition = await _context.SkillFormDefinitions
        //                .AsNoTracking()
        //                .FirstOrDefaultAsync(x =>
        //                    x.SkillFormDefinitionId ==
        //                        assessment!.SkillFormDefinitionId &&
        //                    x.SkillId == request.SkillId);

        //            if (definition == null)
        //            {
        //                return Fail(
        //                    409,
        //                    "The saved assessment's form definition is unavailable.");
        //            }
        //        }

        //        var now = DateTime.UtcNow;
        //        var previousStatus = assessment?.Status;

        //        if (isNew)
        //        {
        //            assessment = new CandidateSkillAssessment
        //            {
        //                CompanyId = request.CompanyId,
        //                JobDescriptionId = request.JobDescriptionId,
        //                CandidateApplicationId =
        //                    application.CandidateApplicationId,
        //                SkillFormDefinitionId =
        //                    definition.SkillFormDefinitionId,

        //                // Independent admin assessment has no assignment.
        //                AssignmentId = resolvedAssignmentId,

        //                DetailsJson = "{}",
        //                Status = "Pending",
        //                IsActive = true,

        //                CreatedBy = employee.EmployeeId,
        //                CreatedOn = now
        //            };

        //            _context.CandidateSkillAssessments.Add(assessment);
        //        }

        //        if (action == "Save")
        //        {
        //            if (hasDetails)
        //            {
        //                // Communication, background verification,
        //                // documents/KYC and salary have additional details.
        //                bool hasSkillSpecificDetails =
        //                    request.SkillId == 2 ||
        //                    request.SkillId == 6 ||
        //                    request.SkillId == 7 ||
        //                    request.SkillId == 8;

        //                if (hasSkillSpecificDetails)
        //                {
        //                    assessment!.DetailsJson =
        //                        request.Details!.Value.GetRawText();
        //                }
        //                else
        //                {
        //                    // Current requirement: other skills use
        //                    // only Score and Remarks.
        //                    if (request.Details!.Value
        //                        .EnumerateObject()
        //                        .Any())
        //                    {
        //                        return Fail(
        //                            400,
        //                            "For this skill, send Details as {} and use Score and Remarks.");
        //                    }

        //                    assessment!.DetailsJson = "{}";
        //                }
        //            }

        //            if (request.Score.HasValue)
        //            {
        //                assessment!.Score = request.Score.Value;
        //            }

        //            // Save does not approve or reject the assessment.
        //            assessment!.Status = "InProgress";
        //        }

        //        if (isRemarkAdded)
        //        {
        //            assessment!.Remarks = request.Remarks!.Trim();
        //        }

        //        if (!isNew)
        //        {
        //            assessment!.ModifiedBy = employee.EmployeeId;
        //            assessment.ModifiedOn = now;
        //        }

        //        await _context.SaveChangesAsync();

        //        var history = new CandidateSkillAssessmentHistory
        //        {
        //            CandidateSkillAssessmentId =
        //                assessment!.CandidateSkillAssessmentId,

        //            CompanyId = assessment.CompanyId,
        //            JobDescriptionId = assessment.JobDescriptionId,
        //            CandidateApplicationId =
        //                assessment.CandidateApplicationId,
        //            SkillFormDefinitionId =
        //                assessment.SkillFormDefinitionId,
        //            AssignmentId = assessment.AssignmentId,

        //            Action = action,
        //            PreviousStatus = previousStatus,
        //            NewStatus = assessment.Status,

        //            DetailsJson = assessment.DetailsJson,
        //            Score = assessment.Score,
        //            Remarks = assessment.Remarks,
        //            IsRemarkAdded = isRemarkAdded,

        //            CreatedBy = employee.EmployeeId,
        //            CreatedOn = now
        //        };

        //        _context.CandidateSkillAssessmentHistories.Add(history);

        //        await _context.SaveChangesAsync();

        //        var remarksCount =
        //            await _context.CandidateSkillAssessmentHistories
        //                .CountAsync(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateSkillAssessmentId ==
        //                        assessment.CandidateSkillAssessmentId &&
        //                    x.IsRemarkAdded);

        //        // Return an object rather than an escaped JSON string.
        //        using var detailsDocument =
        //            JsonDocument.Parse(assessment.DetailsJson);

        //        var responseDetails = detailsDocument.RootElement.Clone();

        //        await transaction.CommitAsync();

        //        return Ok(new
        //        {
        //            Status = true,

        //            Message = action == "AddRemark"
        //                ? "Remark added successfully."
        //                : "Assessment saved successfully.",

        //            Data = new
        //            {
        //                assessment.CandidateSkillAssessmentId,
        //                assessment.CompanyId,
        //                request.CandidateId,
        //                assessment.JobDescriptionId,
        //                assessment.CandidateApplicationId,

        //                skill.SkillId,
        //                skill.SkillName,
        //                category.CategoryId,
        //                category.CategoryName,

        //                assessment.SkillFormDefinitionId,
        //                FormVersion = definition.Version,
        //                assessment.AssignmentId,

        //                Details = responseDetails,
        //                assessment.Score,
        //                assessment.Remarks,
        //                AssessmentStatus = assessment.Status,

        //                RemarksCount = remarksCount,

        //                assessment.CreatedBy,
        //                CreatedByName = employee.EmployeeName,
        //                assessment.CreatedOn,

        //                assessment.ModifiedBy,
        //                ModifiedByName = assessment.ModifiedBy.HasValue
        //                    ? employee.EmployeeName
        //                    : null,
        //                assessment.ModifiedOn
        //            }
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Serilog.Log.Error(
        //            ex,
        //            "Failed to save skill assessment for CompanyId {CompanyId}, CandidateId {CandidateId}, JobDescriptionId {JobDescriptionId}, EmployeeId {EmployeeId}.",
        //            request.CompanyId,
        //            request.CandidateId,
        //            request.JobDescriptionId,
        //            request.EmployeeId);

        //        return StatusCode(500, new
        //        {
        //            Status = false,
        //            Message = "Unable to save skill assessment."
        //        });
        //    }
        //}


        //    [HttpPost("SaveCandidateSkillAssessment")]
        //    public async Task<IActionResult> SaveCandidateSkillAssessment(
        //[FromBody] SaveCandidateSkillAssessmentRequest? request)
        //    {
        //        IActionResult Fail(int statusCode, string message)
        //        {
        //            return StatusCode(statusCode, new
        //            {
        //                Status = false,
        //                Message = message
        //            });
        //        }

        //        if (request == null)
        //            return Fail(400, "Invalid request.");

        //        if (request.CompanyId <= 0 ||
        //            request.CandidateId <= 0 ||
        //            request.JobDescriptionId <= 0 ||
        //            request.EmployeeId <= 0 ||
        //            request.SkillId <= 0 ||
        //            request.CandidateSkillAssessmentId < 0)
        //        {
        //            return Fail(400, "Provide valid reference IDs.");
        //        }

        //        var action = request.Action?.Trim().ToLowerInvariant() switch
        //        {
        //            null => "Save",
        //            "" => "Save",
        //            "save" => "Save",
        //            "addremark" => "AddRemark",
        //            _ => null
        //        };

        //        if (action == null)
        //        {
        //            return Fail(
        //                400,
        //                "This API supports Save and AddRemark. Approval/rejection is performed separately after saving.");
        //        }

        //        bool isRemarkAdded =
        //            action == "AddRemark" || request.IsRemarkAdded;

        //        if (isRemarkAdded &&
        //            string.IsNullOrWhiteSpace(request.Remarks))
        //        {
        //            return Fail(400, "Enter the remark to add.");
        //        }

        //        bool hasDetails =
        //            request.Details.HasValue &&
        //            request.Details.Value.ValueKind != JsonValueKind.Null &&
        //            request.Details.Value.ValueKind != JsonValueKind.Undefined;

        //        if (action == "Save" &&
        //            hasDetails &&
        //            request.Details!.Value.ValueKind != JsonValueKind.Object)
        //        {
        //            return Fail(400, "Details must be a JSON object.");
        //        }

        //        try
        //        {
        //            await using var transaction =
        //                await _context.Database.BeginTransactionAsync(
        //                    System.Data.IsolationLevel.Serializable);

        //            // Acting employee.
        //            var employee = await _context.EmployeeMasters
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.EmployeeId == request.EmployeeId)
        //                .Select(x => new
        //                {
        //                    x.EmployeeId,
        //                    x.IsAdmin,
        //                    x.IsActive,

        //                    EmployeeName =
        //                        ((x.FirstName ?? "") + " " +
        //                         (x.LastName ?? "")).Trim()
        //                })
        //                .FirstOrDefaultAsync();

        //            if (employee == null)
        //                return Fail(404, "Employee not found for this company.");

        //            if (employee.IsActive != true)
        //                return Fail(403, "Employee is inactive.");

        //            bool isAdmin = employee.IsAdmin == true;

        //            var candidateExists = await _context.Candidates
        //                .AsNoTracking()
        //                .AnyAsync(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateId == request.CandidateId);

        //            if (!candidateExists)
        //                return Fail(404, "Candidate not found for this company.");

        //            var job = await _context.JobDescriptions
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.JobDescriptionId == request.JobDescriptionId)
        //                .Select(x => new
        //                {
        //                    x.JobDescriptionId,
        //                    x.TemplateId
        //                })
        //                .FirstOrDefaultAsync();

        //            if (job == null)
        //                return Fail(404, "Job description not found for this company.");

        //            // Resolve the candidate's application for this JD.
        //            var applications = await _context.CandidateApplications
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateId == request.CandidateId &&
        //                    x.JobDescriptionId == request.JobDescriptionId)
        //                .Select(x => new
        //                {
        //                    x.CandidateApplicationId,
        //                    x.IsActive
        //                })
        //                .Take(2)
        //                .ToListAsync();

        //            if (applications.Count == 0)
        //            {
        //                return Fail(
        //                    404,
        //                    "Candidate application not found for this JD.");
        //            }

        //            if (applications.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    "Multiple applications exist for this candidate and JD. CandidateApplicationId is needed to identify the correct application.");
        //            }

        //            var application = applications[0];

        //            if (application.IsActive != true)
        //                return Fail(409, "Candidate application is inactive.");

        //            // request.SkillId is CompanySkillMasterId.
        //            var skill = await _context.CompanySkillMasters
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CompanySkillMasterId == request.SkillId &&
        //                    x.IsActive == true)
        //                .Select(x => new
        //                {
        //                    x.CompanySkillMasterId,
        //                    x.SkillName,
        //                    x.SkillMappingId
        //                })
        //                .FirstOrDefaultAsync();

        //            if (skill == null)
        //            {
        //                return Fail(
        //                    404,
        //                    "Active company skill not found.");
        //            }

        //            var category = await _context.CompanySkillMappings
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.SkillMappingId == skill.SkillMappingId &&
        //                    x.IsActive == true)
        //                .Select(x => new
        //                {
        //                    x.SkillMappingId,
        //                    x.CategoryName
        //                })
        //                .FirstOrDefaultAsync();

        //            if (category == null)
        //            {
        //                return Fail(
        //                    404,
        //                    "Active company skill category not found.");
        //            }

        //            // Preserve assignment checks for non-admin employees.
        //            int? resolvedAssignmentId = null;

        //            if (!isAdmin)
        //            {
        //                var assignments = await _context.RoleTemplateAssignment
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EmployeeId == employee.EmployeeId &&
        //                        x.TemplateId == job.TemplateId &&
        //                        x.IsActive == true)
        //                    .Where(x =>
        //                        _context.Set<RoleTemplate>().Any(template =>
        //                            template.CompanyId == x.CompanyId &&
        //                            template.TemplateId == x.TemplateId &&
        //                            template.IsActive == true))
        //                    .Where(x =>
        //                        _context.Set<RoleTemplateAssignmentSkill>().Any(mapping =>
        //                            mapping.CompanyId == x.CompanyId &&
        //                            mapping.AssignmentId == x.AssignmentId &&
        //                            mapping.SkillId == skill.CompanySkillMasterId))
        //                    .Select(x => x.AssignmentId)
        //                    .Take(2)
        //                    .ToListAsync();

        //                if (assignments.Count == 0)
        //                {
        //                    return Fail(
        //                        403,
        //                        "This skill is not assigned to the employee in the JD's active template.");
        //                }

        //                if (assignments.Count > 1)
        //                {
        //                    return Fail(
        //                        409,
        //                        "Multiple matching assignments exist. AssignmentId is needed to identify the correct assignment.");
        //                }

        //                resolvedAssignmentId = assignments[0];
        //            }

        //            // Locate the employee's assessment by company skill directly.
        //            // No form-definition lookup is required.
        //            var assessmentQuery = _context.CandidateSkillAssessments
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.JobDescriptionId == request.JobDescriptionId &&
        //                    x.CandidateApplicationId ==
        //                        application.CandidateApplicationId &&
        //                    x.CreatedBy == employee.EmployeeId &&
        //                    x.CompanySkillMasterId == skill.CompanySkillMasterId);

        //            CandidateSkillAssessment? assessment;

        //            if (request.CandidateSkillAssessmentId > 0)
        //            {
        //                assessment = await assessmentQuery
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CandidateSkillAssessmentId ==
        //                            request.CandidateSkillAssessmentId);

        //                if (assessment == null)
        //                {
        //                    return Fail(
        //                        404,
        //                        "Assessment not found for this employee, candidate and skill.");
        //                }
        //            }
        //            else
        //            {
        //                var existingAssessments = await assessmentQuery
        //                    .Take(2)
        //                    .ToListAsync();

        //                if (existingAssessments.Count > 1)
        //                {
        //                    return Fail(
        //                        409,
        //                        "Multiple assessments exist. Send CandidateSkillAssessmentId.");
        //                }

        //                assessment = existingAssessments.FirstOrDefault();
        //            }

        //            bool isNew = assessment == null;

        //            if (isNew && action == "AddRemark")
        //            {
        //                return Fail(
        //                    400,
        //                    "Save the assessment first, then add further remarks.");
        //            }

        //            if (!isNew)
        //            {
        //                if (assessment!.IsActive != true)
        //                    return Fail(409, "Assessment is inactive.");

        //                if (!isAdmin &&
        //                    assessment.AssignmentId != resolvedAssignmentId)
        //                {
        //                    return Fail(
        //                        409,
        //                        "This assessment belongs to a different assignment.");
        //                }

        //                if (action == "Save" &&
        //                    (assessment.Status == "Approved" ||
        //                     assessment.Status == "Rejected"))
        //                {
        //                    return Fail(
        //                        409,
        //                        "This assessment is completed. You can still use AddRemark.");
        //                }
        //            }

        //            var now = DateTime.UtcNow;
        //            var previousStatus = assessment?.Status;

        //            if (isNew)
        //            {
        //                assessment = new CandidateSkillAssessment
        //                {
        //                    CompanyId = request.CompanyId,
        //                    JobDescriptionId = request.JobDescriptionId.Value,
        //                    CandidateApplicationId =
        //                        application.CandidateApplicationId,

        //                    CompanySkillMasterId = skill.CompanySkillMasterId,

        //                    // No form definition is required for new assessments.
        //                    SkillFormDefinitionId = 0,

        //                    // Independent admin assessment has no assignment.
        //                    AssignmentId = resolvedAssignmentId,

        //                    DetailsJson = "{}",
        //                    Status = "Pending",
        //                    IsActive = true,

        //                    CreatedBy = employee.EmployeeId,
        //                    CreatedOn = now
        //                };

        //                _context.CandidateSkillAssessments.Add(assessment);
        //            }

        //            if (action == "Save")
        //            {
        //                if (hasDetails)
        //                {
        //                    // Store frontend details for every company skill.
        //                    // Supplied Details replaces the previous JSON object.
        //                    assessment!.DetailsJson =
        //                        request.Details!.Value.GetRawText();
        //                }

        //                // Omitted/null Details preserves existing details.

        //                if (request.Score.HasValue)
        //                {
        //                    assessment!.Score = request.Score.Value;
        //                }

        //                // Save does not approve or reject the assessment.
        //                assessment!.Status = "InProgress";
        //            }

        //            if (isRemarkAdded)
        //            {
        //                assessment!.Remarks = request.Remarks!.Trim();
        //            }

        //            if (!isNew)
        //            {
        //                assessment!.ModifiedBy = employee.EmployeeId;
        //                assessment.ModifiedOn = now;
        //            }

        //            await _context.SaveChangesAsync();

        //            var history = new CandidateSkillAssessmentHistory
        //            {
        //                CandidateSkillAssessmentId =
        //                    assessment!.CandidateSkillAssessmentId,

        //                CompanyId = assessment.CompanyId,
        //                JobDescriptionId = assessment.JobDescriptionId,
        //                CandidateApplicationId =
        //                    assessment.CandidateApplicationId,

        //                CompanySkillMasterId = assessment.CompanySkillMasterId,

        //                // Retain a legacy reference if the existing assessment has one.
        //                SkillFormDefinitionId = assessment.SkillFormDefinitionId,

        //                AssignmentId = assessment.AssignmentId,

        //                Action = action,
        //                PreviousStatus = previousStatus,
        //                NewStatus = assessment.Status,

        //                DetailsJson = assessment.DetailsJson,
        //                Score = assessment.Score,
        //                Remarks = assessment.Remarks,
        //                IsRemarkAdded = isRemarkAdded,

        //                CreatedBy = employee.EmployeeId,
        //                CreatedOn = now
        //            };

        //            _context.CandidateSkillAssessmentHistories.Add(history);

        //            await _context.SaveChangesAsync();

        //            var remarksCount =
        //                await _context.CandidateSkillAssessmentHistories
        //                    .CountAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.CandidateSkillAssessmentId ==
        //                            assessment.CandidateSkillAssessmentId &&
        //                        x.IsRemarkAdded);

        //            // Return an object rather than an escaped JSON string.
        //            using var detailsDocument =
        //                JsonDocument.Parse(assessment.DetailsJson);

        //            var responseDetails = detailsDocument.RootElement.Clone();

        //            await transaction.CommitAsync();

        //            return Ok(new
        //            {
        //                Status = true,

        //                Message = action == "AddRemark"
        //                    ? "Remark added successfully."
        //                    : "Assessment saved successfully.",

        //                Data = new
        //                {
        //                    assessment.CandidateSkillAssessmentId,
        //                    assessment.CompanyId,
        //                    request.CandidateId,
        //                    assessment.JobDescriptionId,
        //                    assessment.CandidateApplicationId,

        //                    // Preserve previous response names using company IDs.
        //                    SkillId = skill.CompanySkillMasterId,
        //                    skill.SkillName,
        //                    CategoryId = category.SkillMappingId,
        //                    category.CategoryName,



        //                    // Compatibility fields; no form lookup is performed.
        //                    assessment.SkillFormDefinitionId,
        //                    FormVersion = (int?)null,

        //                    assessment.AssignmentId,

        //                    Details = responseDetails,
        //                    assessment.Score,
        //                    assessment.Remarks,
        //                    AssessmentStatus = assessment.Status,

        //                    RemarksCount = remarksCount,

        //                    assessment.CreatedBy,
        //                    CreatedByName = employee.EmployeeName,
        //                    assessment.CreatedOn,

        //                    assessment.ModifiedBy,
        //                    ModifiedByName = assessment.ModifiedBy.HasValue
        //                        ? employee.EmployeeName
        //                        : null,
        //                    assessment.ModifiedOn
        //                }
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            Serilog.Log.Error(
        //                ex,
        //                "Failed to save skill assessment for CompanyId {CompanyId}, CandidateId {CandidateId}, JobDescriptionId {JobDescriptionId}, EmployeeId {EmployeeId}.",
        //                request.CompanyId,
        //                request.CandidateId,
        //                request.JobDescriptionId,
        //                request.EmployeeId);

        //            return StatusCode(500, new
        //            {
        //                Status = false,
        //                Message = "Unable to save skill assessment."
        //            });
        //        }
        //    }

        //    [HttpPost("SaveCandidateSkillAssessment")]
        //    public async Task<IActionResult> SaveCandidateSkillAssessment(
        //[FromBody] SaveCandidateSkillAssessmentRequest? request)
        //    {
        //        IActionResult Fail(int statusCode, string message)
        //        {
        //            return StatusCode(statusCode, new
        //            {
        //                Status = false,
        //                Message = message
        //            });
        //        }

        //        if (request == null)
        //            return Fail(400, "Invalid request.");

        //        // Explicitly handle nullable reference IDs.
        //        if (request.CompanyId <= 0 ||
        //            request.CandidateId <= 0 ||
        //            request.JobDescriptionId.GetValueOrDefault() <= 0 ||
        //            request.EmployeeId.GetValueOrDefault() <= 0 ||
        //            request.SkillId.GetValueOrDefault() <= 0 ||
        //            request.CandidateSkillAssessmentId < 0 ||
        //            request.CandidateSkillAssessmentHistoryId < 0)
        //        {
        //            return Fail(400, "Provide valid reference IDs.");
        //        }

        //        bool isHistoryEdit =
        //            request.CandidateSkillAssessmentHistoryId > 0;

        //        var action = request.Action?.Trim().ToLowerInvariant() switch
        //        {
        //            null => "Save",
        //            "" => "Save",
        //            "save" => "Save",
        //            "addremark" => "AddRemark",
        //            _ => null
        //        };

        //        if (action == null)
        //        {
        //            return Fail(
        //                400,
        //                "This API supports Save and AddRemark. Approval/rejection is performed separately after saving.");
        //        }

        //        bool isRemarkAdded =
        //            action == "AddRemark" || request.IsRemarkAdded;

        //        if (isRemarkAdded &&
        //            string.IsNullOrWhiteSpace(request.Remarks))
        //        {
        //            return Fail(400, "Enter the remark to add.");
        //        }

        //        bool hasDetails =
        //            request.Details.HasValue &&
        //            request.Details.Value.ValueKind != JsonValueKind.Null &&
        //            request.Details.Value.ValueKind != JsonValueKind.Undefined;

        //        if (action == "Save" &&
        //            hasDetails &&
        //            request.Details!.Value.ValueKind != JsonValueKind.Object)
        //        {
        //            return Fail(400, "Details must be a JSON object.");
        //        }

        //        try
        //        {
        //            await using var transaction =
        //                await _context.Database.BeginTransactionAsync(
        //                    System.Data.IsolationLevel.Serializable);

        //            var employee = await _context.EmployeeMasters
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.EmployeeId == request.EmployeeId)
        //                .Select(x => new
        //                {
        //                    x.EmployeeId,
        //                    x.IsAdmin,
        //                    x.IsActive,

        //                    EmployeeName =
        //                        ((x.FirstName ?? "") + " " +
        //                         (x.LastName ?? "")).Trim()
        //                })
        //                .FirstOrDefaultAsync();

        //            if (employee == null)
        //                return Fail(404, "Employee not found for this company.");

        //            if (employee.IsActive != true)
        //                return Fail(403, "Employee is inactive.");

        //            bool isAdmin = employee.IsAdmin == true;

        //            var candidateExists = await _context.Candidates
        //                .AsNoTracking()
        //                .AnyAsync(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateId == request.CandidateId);

        //            if (!candidateExists)
        //                return Fail(404, "Candidate not found for this company.");

        //            var job = await _context.JobDescriptions
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.JobDescriptionId == request.JobDescriptionId)
        //                .Select(x => new
        //                {
        //                    x.JobDescriptionId,
        //                    x.TemplateId
        //                })
        //                .FirstOrDefaultAsync();

        //            if (job == null)
        //                return Fail(404, "Job description not found for this company.");

        //            var applications = await _context.CandidateApplications
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateId == request.CandidateId &&
        //                    x.JobDescriptionId == request.JobDescriptionId)
        //                .Select(x => new
        //                {
        //                    x.CandidateApplicationId,
        //                    x.IsActive
        //                })
        //                .Take(2)
        //                .ToListAsync();

        //            if (applications.Count == 0)
        //            {
        //                return Fail(
        //                    404,
        //                    "Candidate application not found for this JD.");
        //            }

        //            if (applications.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    "Multiple applications exist for this candidate and JD. CandidateApplicationId is needed to identify the correct application.");
        //            }

        //            var application = applications[0];

        //            if (application.IsActive != true)
        //                return Fail(409, "Candidate application is inactive.");

        //            var skill = await _context.CompanySkillMasters
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CompanySkillMasterId == request.SkillId &&
        //                    x.IsActive == true)
        //                .Select(x => new
        //                {
        //                    x.CompanySkillMasterId,
        //                    x.SkillName,
        //                    x.SkillMappingId
        //                })
        //                .FirstOrDefaultAsync();

        //            if (skill == null)
        //                return Fail(404, "Active company skill not found.");

        //            var category = await _context.CompanySkillMappings
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.SkillMappingId == skill.SkillMappingId &&
        //                    x.IsActive == true)
        //                .Select(x => new
        //                {
        //                    x.SkillMappingId,
        //                    x.CategoryName
        //                })
        //                .FirstOrDefaultAsync();

        //            if (category == null)
        //                return Fail(404, "Active company skill category not found.");

        //            int? resolvedAssignmentId = null;

        //            if (!isAdmin)
        //            {
        //                var assignments = await _context.RoleTemplateAssignment
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EmployeeId == employee.EmployeeId &&
        //                        x.TemplateId == job.TemplateId &&
        //                        x.IsActive == true)
        //                    .Where(x =>
        //                        _context.Set<RoleTemplate>().Any(template =>
        //                            template.CompanyId == x.CompanyId &&
        //                            template.TemplateId == x.TemplateId &&
        //                            template.IsActive == true))
        //                    .Where(x =>
        //                        _context.Set<RoleTemplateAssignmentSkill>().Any(mapping =>
        //                            mapping.CompanyId == x.CompanyId &&
        //                            mapping.AssignmentId == x.AssignmentId &&
        //                            mapping.SkillId == skill.CompanySkillMasterId))
        //                    .Select(x => x.AssignmentId)
        //                    .Take(2)
        //                    .ToListAsync();

        //                if (assignments.Count == 0)
        //                {
        //                    return Fail(
        //                        403,
        //                        "This skill is not assigned to the employee in the JD's active template.");
        //                }

        //                if (assignments.Count > 1)
        //                {
        //                    return Fail(
        //                        409,
        //                        "Multiple matching assignments exist. AssignmentId is needed to identify the correct assignment.");
        //                }

        //                resolvedAssignmentId = assignments[0];
        //            }

        //            var assessmentQuery = _context.CandidateSkillAssessments
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.JobDescriptionId == request.JobDescriptionId &&
        //                    x.CandidateApplicationId ==
        //                        application.CandidateApplicationId &&
        //                    x.CreatedBy == employee.EmployeeId &&
        //                    x.CompanySkillMasterId == skill.CompanySkillMasterId);

        //            CandidateSkillAssessmentHistory? historyToEdit = null;

        //            // Resolve the exact parent from the supplied history ID.
        //            if (isHistoryEdit)
        //            {
        //                historyToEdit = await _context.CandidateSkillAssessmentHistories
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CandidateSkillAssessmentHistoryId ==
        //                            request.CandidateSkillAssessmentHistoryId &&
        //                        x.CompanyId == request.CompanyId &&
        //                        x.JobDescriptionId == request.JobDescriptionId &&
        //                        x.CandidateApplicationId ==
        //                            application.CandidateApplicationId &&
        //                        x.CompanySkillMasterId == skill.CompanySkillMasterId &&
        //                        x.CreatedBy == employee.EmployeeId);

        //                if (historyToEdit == null)
        //                {
        //                    return Fail(
        //                        404,
        //                        "History record not found for this employee, candidate, job and skill.");
        //                }

        //                if (request.CandidateSkillAssessmentId > 0 &&
        //                    request.CandidateSkillAssessmentId !=
        //                        historyToEdit.CandidateSkillAssessmentId)
        //                {
        //                    return Fail(
        //                        400,
        //                        "History record does not belong to the supplied assessment.");
        //                }

        //                assessmentQuery = assessmentQuery.Where(x =>
        //                    x.CandidateSkillAssessmentId ==
        //                        historyToEdit.CandidateSkillAssessmentId);
        //            }

        //            CandidateSkillAssessment? assessment;

        //            if (isHistoryEdit)
        //            {
        //                assessment = await assessmentQuery.FirstOrDefaultAsync();

        //                if (assessment == null)
        //                {
        //                    return Fail(
        //                        404,
        //                        "Assessment not found for this history record and employee.");
        //                }
        //            }
        //            else if (request.CandidateSkillAssessmentId > 0)
        //            {
        //                assessment = await assessmentQuery
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CandidateSkillAssessmentId ==
        //                            request.CandidateSkillAssessmentId);

        //                if (assessment == null)
        //                {
        //                    return Fail(
        //                        404,
        //                        "Assessment not found for this employee, candidate and skill.");
        //                }
        //            }
        //            else
        //            {
        //                var existingAssessments = await assessmentQuery
        //                    .Take(2)
        //                    .ToListAsync();

        //                if (existingAssessments.Count > 1)
        //                {
        //                    return Fail(
        //                        409,
        //                        "Multiple assessments exist. Send CandidateSkillAssessmentId.");
        //                }

        //                assessment = existingAssessments.FirstOrDefault();
        //            }

        //            bool isNew = assessment == null;

        //            if (isNew && action == "AddRemark")
        //            {
        //                return Fail(
        //                    400,
        //                    "Save the assessment first, then add further remarks.");
        //            }

        //            if (!isNew)
        //            {
        //                if (assessment!.IsActive != true)
        //                    return Fail(409, "Assessment is inactive.");

        //                if (!isAdmin &&
        //                    assessment.AssignmentId != resolvedAssignmentId)
        //                {
        //                    return Fail(
        //                        409,
        //                        "This assessment belongs to a different assignment.");
        //                }

        //                // Preserve the completed-assessment restriction for normal saves.
        //                // A history-only edit does not change the assessment.
        //                if (!isHistoryEdit &&
        //                    action == "Save" &&
        //                    (assessment.Status == "Approved" ||
        //                     assessment.Status == "Rejected"))
        //                {
        //                    return Fail(
        //                        409,
        //                        "This assessment is completed. You can still use AddRemark.");
        //                }
        //            }

        //            var now = DateTime.UtcNow;

        //            // Update only the requested history row.
        //            if (isHistoryEdit)
        //            {
        //                if (!isAdmin &&
        //                    historyToEdit!.AssignmentId != resolvedAssignmentId)
        //                {
        //                    return Fail(
        //                        409,
        //                        "This history record belongs to a different assignment.");
        //                }

        //                if (action == "Save")
        //                {
        //                    if (hasDetails)
        //                    {
        //                        historyToEdit!.DetailsJson =
        //                            request.Details!.Value.GetRawText();
        //                    }

        //                    if (request.Score.HasValue)
        //                    {
        //                        historyToEdit!.Score = request.Score.Value;
        //                    }
        //                }

        //                if (isRemarkAdded)
        //                {
        //                    historyToEdit!.Remarks = request.Remarks!.Trim();

        //                    // Editing a remark does not create another history row.
        //                    historyToEdit.IsRemarkAdded = true;
        //                }

        //                historyToEdit!.ModifiedBy = request.EmployeeId.Value;
        //                historyToEdit.ModifiedOn = now;

        //                // Preserve Action, PreviousStatus, NewStatus,
        //                // CreatedBy, CreatedOn and the parent assessment.
        //                await _context.SaveChangesAsync();

        //                var historyRemarksCount =
        //                    await _context.CandidateSkillAssessmentHistories
        //                        .CountAsync(x =>
        //                            x.CompanyId == request.CompanyId &&
        //                            x.CandidateSkillAssessmentId ==
        //                                assessment!.CandidateSkillAssessmentId &&
        //                            x.IsRemarkAdded);

        //                using var historyDetailsDocument =
        //                    JsonDocument.Parse(historyToEdit.DetailsJson);

        //                var historyDetails =
        //                    historyDetailsDocument.RootElement.Clone();

        //                await transaction.CommitAsync();

        //                return Ok(new
        //                {
        //                    Status = true,
        //                    Message = "Assessment history updated successfully.",

        //                    Data = new
        //                    {
        //                        historyToEdit.CandidateSkillAssessmentHistoryId,
        //                        historyToEdit.CandidateSkillAssessmentId,
        //                        historyToEdit.CompanyId,
        //                        request.CandidateId,
        //                        historyToEdit.JobDescriptionId,
        //                        historyToEdit.CandidateApplicationId,

        //                        SkillId = skill.CompanySkillMasterId,
        //                        skill.SkillName,
        //                        CategoryId = category.SkillMappingId,
        //                        category.CategoryName,

        //                        historyToEdit.SkillFormDefinitionId,
        //                        FormVersion = (int?)null,
        //                        historyToEdit.AssignmentId,

        //                        Details = historyDetails,
        //                        historyToEdit.Score,
        //                        historyToEdit.Remarks,
        //                        historyToEdit.Action,
        //                        historyToEdit.PreviousStatus,
        //                        historyToEdit.NewStatus,
        //                        historyToEdit.IsRemarkAdded,

        //                        // The main assessment status remains unchanged.
        //                        AssessmentStatus = assessment!.Status,
        //                        RemarksCount = historyRemarksCount,

        //                        historyToEdit.CreatedBy,
        //                        CreatedByName = employee.EmployeeName,
        //                        historyToEdit.CreatedOn,

        //                        historyToEdit.ModifiedBy,
        //                        ModifiedByName = employee.EmployeeName,
        //                        historyToEdit.ModifiedOn
        //                    }
        //                });
        //            }

        //            // Existing normal-save logic.
        //            var previousStatus = assessment?.Status;

        //            if (isNew)
        //            {
        //                assessment = new CandidateSkillAssessment
        //                {
        //                    CompanyId = request.CompanyId,
        //                    JobDescriptionId = request.JobDescriptionId!.Value,
        //                    CandidateApplicationId =
        //                        application.CandidateApplicationId,

        //                    CompanySkillMasterId = skill.CompanySkillMasterId,

        //                    // Preserved from your supplied code.
        //                    SkillFormDefinitionId = 0,

        //                    AssignmentId = resolvedAssignmentId,
        //                    DetailsJson = "{}",
        //                    Status = "Pending",
        //                    IsActive = true,

        //                    CreatedBy = employee.EmployeeId,
        //                    CreatedOn = now
        //                };

        //                _context.CandidateSkillAssessments.Add(assessment);
        //            }

        //            if (action == "Save")
        //            {
        //                if (hasDetails)
        //                {
        //                    assessment!.DetailsJson =
        //                        request.Details!.Value.GetRawText();
        //                }

        //                if (request.Score.HasValue)
        //                {
        //                    assessment!.Score = request.Score.Value;
        //                }

        //                assessment!.Status = "InProgress";
        //            }

        //            if (isRemarkAdded)
        //            {
        //                assessment!.Remarks = request.Remarks!.Trim();
        //            }

        //            if (!isNew)
        //            {
        //                assessment!.ModifiedBy = employee.EmployeeId;
        //                assessment.ModifiedOn = now;
        //            }

        //            await _context.SaveChangesAsync();

        //            var history = new CandidateSkillAssessmentHistory
        //            {
        //                CandidateSkillAssessmentId =
        //                    assessment!.CandidateSkillAssessmentId,

        //                CompanyId = assessment.CompanyId,
        //                JobDescriptionId = assessment.JobDescriptionId,
        //                CandidateApplicationId =
        //                    assessment.CandidateApplicationId,
        //                CompanySkillMasterId = assessment.CompanySkillMasterId,
        //                SkillFormDefinitionId = assessment.SkillFormDefinitionId,
        //                AssignmentId = assessment.AssignmentId,

        //                Action = action,
        //                PreviousStatus = previousStatus,
        //                NewStatus = assessment.Status,

        //                DetailsJson = assessment.DetailsJson,
        //                Score = assessment.Score,
        //                Remarks = assessment.Remarks,
        //                IsRemarkAdded = isRemarkAdded,

        //                CreatedBy = employee.EmployeeId,
        //                CreatedOn = now
        //            };

        //            _context.CandidateSkillAssessmentHistories.Add(history);

        //            await _context.SaveChangesAsync();

        //            var remarksCount =
        //                await _context.CandidateSkillAssessmentHistories
        //                    .CountAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.CandidateSkillAssessmentId ==
        //                            assessment.CandidateSkillAssessmentId &&
        //                        x.IsRemarkAdded);

        //            using var detailsDocument =
        //                JsonDocument.Parse(assessment.DetailsJson);

        //            var responseDetails = detailsDocument.RootElement.Clone();

        //            await transaction.CommitAsync();

        //            return Ok(new
        //            {
        //                Status = true,
        //                Message = action == "AddRemark"
        //                    ? "Remark added successfully."
        //                    : "Assessment saved successfully.",

        //                Data = new
        //                {
        //                    // Return this ID for editing this history row later.
        //                    history.CandidateSkillAssessmentHistoryId,

        //                    assessment.CandidateSkillAssessmentId,
        //                    assessment.CompanyId,
        //                    request.CandidateId,
        //                    assessment.JobDescriptionId,
        //                    assessment.CandidateApplicationId,

        //                    SkillId = skill.CompanySkillMasterId,
        //                    skill.SkillName,
        //                    CategoryId = category.SkillMappingId,
        //                    category.CategoryName,

        //                    assessment.SkillFormDefinitionId,
        //                    FormVersion = (int?)null,
        //                    assessment.AssignmentId,

        //                    Details = responseDetails,
        //                    assessment.Score,
        //                    assessment.Remarks,
        //                    AssessmentStatus = assessment.Status,

        //                    RemarksCount = remarksCount,

        //                    assessment.CreatedBy,
        //                    CreatedByName = employee.EmployeeName,
        //                    assessment.CreatedOn,

        //                    assessment.ModifiedBy,
        //                    ModifiedByName = assessment.ModifiedBy.HasValue
        //                        ? employee.EmployeeName
        //                        : null,
        //                    assessment.ModifiedOn
        //                }
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            Serilog.Log.Error(
        //                ex,
        //                "Failed to save skill assessment for CompanyId {CompanyId}, CandidateId {CandidateId}, JobDescriptionId {JobDescriptionId}, EmployeeId {EmployeeId}.",
        //                request.CompanyId,
        //                request.CandidateId,
        //                request.JobDescriptionId,
        //                request.EmployeeId);

        //            return StatusCode(500, new
        //            {
        //                Status = false,
        //                Message = "Unable to save skill assessment."
        //            });
        //        }
        //    }

        //[HttpPost("ApproveRejectSkillAssessment")]


    //    [HttpPost("SaveCandidateSkillAssessment")]
    //    public async Task<IActionResult> SaveCandidateSkillAssessment(
    //[FromBody] SaveCandidateSkillAssessmentRequest? request)
    //    {
    //        IActionResult Fail(int statusCode, string message)
    //        {
    //            return StatusCode(statusCode, new
    //            {
    //                Status = false,
    //                Message = message
    //            });
    //        }

    //        if (request == null)
    //            return Fail(400, "Invalid request.");

    //        if (request.CompanyId <= 0 ||
    //            request.CandidateId <= 0 ||
    //            request.JobDescriptionId.GetValueOrDefault() <= 0 ||
    //            request.EmployeeId.GetValueOrDefault() <= 0 ||
    //            request.SkillId.GetValueOrDefault() < 0 ||
    //            request.CandidateSkillAssessmentId < 0 ||
    //            request.CandidateSkillAssessmentHistoryId < 0)
    //        {
    //            return Fail(400, "Provide valid reference IDs.");
    //        }

    //        // Null or zero means Default Remark.
    //        bool isDefaultRemark = request.SkillId.GetValueOrDefault() == 0;

    //        int? companySkillMasterId = isDefaultRemark
    //            ? (int?)null
    //            : request.SkillId.Value;

    //        bool isHistoryEdit =
    //            request.CandidateSkillAssessmentHistoryId > 0;

    //        var action = request.Action?.Trim().ToLowerInvariant() switch
    //        {
    //            null => "Save",
    //            "" => "Save",
    //            "save" => "Save",
    //            "addremark" => "AddRemark",
    //            _ => null
    //        };

    //        if (action == null)
    //        {
    //            return Fail(
    //                400,
    //                "This API supports Save and AddRemark. Approval/rejection is performed separately after saving.");
    //        }

    //        // Preserve skill-based behavior.
    //        // Default remarks also save supplied remark text without requiring the flag.
    //        bool isRemarkAdded =
    //            action == "AddRemark" ||
    //            request.IsRemarkAdded ||
    //            (isDefaultRemark && !string.IsNullOrWhiteSpace(request.Remarks));

    //        if (isRemarkAdded &&
    //            string.IsNullOrWhiteSpace(request.Remarks))
    //        {
    //            return Fail(400, "Enter the remark to add.");
    //        }

    //        bool hasDetails =
    //            request.Details.HasValue &&
    //            request.Details.Value.ValueKind != JsonValueKind.Null &&
    //            request.Details.Value.ValueKind != JsonValueKind.Undefined;

    //        if (action == "Save" &&
    //            hasDetails &&
    //            request.Details!.Value.ValueKind != JsonValueKind.Object)
    //        {
    //            return Fail(400, "Details must be a JSON object.");
    //        }

    //        try
    //        {
    //            await using var transaction =
    //                await _context.Database.BeginTransactionAsync(
    //                    System.Data.IsolationLevel.Serializable);

    //            var employee = await _context.EmployeeMasters
    //                .AsNoTracking()
    //                .Where(x =>
    //                    x.CompanyId == request.CompanyId &&
    //                    x.EmployeeId == request.EmployeeId)
    //                .Select(x => new
    //                {
    //                    x.EmployeeId,
    //                    x.IsAdmin,
    //                    x.IsActive,
    //                    EmployeeName =
    //                        ((x.FirstName ?? "") + " " +
    //                         (x.LastName ?? "")).Trim()
    //                })
    //                .FirstOrDefaultAsync();

    //            if (employee == null)
    //                return Fail(404, "Employee not found for this company.");

    //            if (employee.IsActive != true)
    //                return Fail(403, "Employee is inactive.");

    //            bool isAdmin = employee.IsAdmin == true;

    //            var candidateExists = await _context.Candidates
    //                .AsNoTracking()
    //                .AnyAsync(x =>
    //                    x.CompanyId == request.CompanyId &&
    //                    x.CandidateId == request.CandidateId);

    //            if (!candidateExists)
    //                return Fail(404, "Candidate not found for this company.");

    //            var job = await _context.JobDescriptions
    //                .AsNoTracking()
    //                .Where(x =>
    //                    x.CompanyId == request.CompanyId &&
    //                    x.JobDescriptionId == request.JobDescriptionId)
    //                .Select(x => new
    //                {
    //                    x.JobDescriptionId,
    //                    x.TemplateId
    //                })
    //                .FirstOrDefaultAsync();

    //            if (job == null)
    //                return Fail(404, "Job description not found for this company.");

    //            var applications = await _context.CandidateApplications
    //                .AsNoTracking()
    //                .Where(x =>
    //                    x.CompanyId == request.CompanyId &&
    //                    x.CandidateId == request.CandidateId &&
    //                    x.JobDescriptionId == request.JobDescriptionId)
    //                .Select(x => new
    //                {
    //                    x.CandidateApplicationId,
    //                    x.IsActive
    //                })
    //                .Take(2)
    //                .ToListAsync();

    //            if (applications.Count == 0)
    //            {
    //                return Fail(
    //                    404,
    //                    "Candidate application not found for this JD.");
    //            }

    //            if (applications.Count > 1)
    //            {
    //                return Fail(
    //                    409,
    //                    "Multiple applications exist for this candidate and JD. CandidateApplicationId is needed to identify the correct application.");
    //            }

    //            var application = applications[0];

    //            if (application.IsActive != true)
    //                return Fail(409, "Candidate application is inactive.");

    //            string? skillName = "Default Remark";
    //            int? categoryId = null;
    //            string? categoryName = null;

    //            // Default remarks require no company skill or category.
    //            if (!isDefaultRemark)
    //            {
    //                var skill = await _context.CompanySkillMasters
    //                    .AsNoTracking()
    //                    .Where(x =>
    //                        x.CompanyId == request.CompanyId &&
    //                        x.CompanySkillMasterId == companySkillMasterId &&
    //                        x.IsActive == true)
    //                    .Select(x => new
    //                    {
    //                        x.CompanySkillMasterId,
    //                        x.SkillName,
    //                        x.SkillMappingId
    //                    })
    //                    .FirstOrDefaultAsync();

    //                if (skill == null)
    //                    return Fail(404, "Active company skill not found.");

    //                var category = await _context.CompanySkillMappings
    //                    .AsNoTracking()
    //                    .Where(x =>
    //                        x.CompanyId == request.CompanyId &&
    //                        x.SkillMappingId == skill.SkillMappingId &&
    //                        x.IsActive == true)
    //                    .Select(x => new
    //                    {
    //                        x.SkillMappingId,
    //                        x.CategoryName
    //                    })
    //                    .FirstOrDefaultAsync();

    //                if (category == null)
    //                    return Fail(404, "Active company skill category not found.");

    //                skillName = skill.SkillName;
    //                categoryId = category.SkillMappingId;
    //                categoryName = category.CategoryName;
    //            }

    //            int? resolvedAssignmentId = null;

    //            if (!isAdmin)
    //            {
    //                var assignmentQuery = _context.RoleTemplateAssignment
    //                    .AsNoTracking()
    //                    .Where(x =>
    //                        x.CompanyId == request.CompanyId &&
    //                        x.EmployeeId == employee.EmployeeId &&
    //                        x.TemplateId == job.TemplateId &&
    //                        x.IsActive == true)
    //                    .Where(x =>
    //                        _context.Set<RoleTemplate>().Any(template =>
    //                            template.CompanyId == x.CompanyId &&
    //                            template.TemplateId == x.TemplateId &&
    //                            template.IsActive == true));

    //                // Default remarks still require an active job-template assignment.
    //                // Only the skill-mapping requirement is skipped.
    //                if (!isDefaultRemark)
    //                {
    //                    assignmentQuery = assignmentQuery.Where(x =>
    //                        _context.Set<RoleTemplateAssignmentSkill>().Any(mapping =>
    //                            mapping.CompanyId == x.CompanyId &&
    //                            mapping.AssignmentId == x.AssignmentId &&
    //                            mapping.SkillId == companySkillMasterId &&
    //                            mapping.IsActive == true));
    //                }

    //                var assignments = await assignmentQuery
    //                    .Select(x => x.AssignmentId)
    //                    .Take(2)
    //                    .ToListAsync();

    //                if (assignments.Count == 0)
    //                {
    //                    return Fail(
    //                        403,
    //                        isDefaultRemark
    //                            ? "No active role assignment exists for this employee in the JD's active template."
    //                            : "This skill is not assigned to the employee in the JD's active template.");
    //                }

    //                if (assignments.Count > 1)
    //                {
    //                    return Fail(
    //                        409,
    //                        "Multiple matching assignments exist. AssignmentId is needed to identify the correct assignment.");
    //                }

    //                resolvedAssignmentId = assignments[0];
    //            }

    //            // CreatedBy separates employees' assessments and default remarks.
    //            // A null skill matches only Default Remark records.
    //            var assessmentQuery = _context.CandidateSkillAssessments
    //                .Where(x =>
    //                    x.CompanyId == request.CompanyId &&
    //                    x.JobDescriptionId == request.JobDescriptionId &&
    //                    x.CandidateApplicationId ==
    //                        application.CandidateApplicationId &&
    //                  //  x.CreatedBy == employee.EmployeeId &&
    //                    x.CompanySkillMasterId == companySkillMasterId);

    //            CandidateSkillAssessmentHistory? historyToEdit = null;

    //            if (isHistoryEdit)
    //            {
    //                historyToEdit = await _context.CandidateSkillAssessmentHistories
    //                    .FirstOrDefaultAsync(x =>
    //                        x.CandidateSkillAssessmentHistoryId ==
    //                            request.CandidateSkillAssessmentHistoryId &&
    //                        x.CompanyId == request.CompanyId &&
    //                        x.JobDescriptionId == request.JobDescriptionId &&
    //                        x.CandidateApplicationId ==
    //                            application.CandidateApplicationId &&
    //                        x.CompanySkillMasterId == companySkillMasterId //&&
    //                        //x.CreatedBy == employee.EmployeeId
    //                        );

    //                if (historyToEdit == null)
    //                {
    //                    return Fail(
    //                        404,
    //                        "History record not found for this employee, candidate, job and skill/default remark.");
    //                }

    //                if (request.CandidateSkillAssessmentId > 0 &&
    //                    request.CandidateSkillAssessmentId !=
    //                        historyToEdit.CandidateSkillAssessmentId)
    //                {
    //                    return Fail(
    //                        400,
    //                        "History record does not belong to the supplied assessment.");
    //                }

    //                assessmentQuery = assessmentQuery.Where(x =>
    //                    x.CandidateSkillAssessmentId ==
    //                        historyToEdit.CandidateSkillAssessmentId);
    //            }

    //            CandidateSkillAssessment? assessment;

    //            if (isHistoryEdit)
    //            {
    //                assessment = await assessmentQuery.FirstOrDefaultAsync();

    //                if (assessment == null)
    //                {
    //                    return Fail(
    //                        404,
    //                        "Assessment not found for this history record and employee.");
    //                }
    //            }
    //            else if (request.CandidateSkillAssessmentId > 0)
    //            {
    //                assessment = await assessmentQuery
    //                    .FirstOrDefaultAsync(x =>
    //                        x.CandidateSkillAssessmentId ==
    //                            request.CandidateSkillAssessmentId);

    //                if (assessment == null)
    //                {
    //                    return Fail(
    //                        404,
    //                        "Assessment not found for this employee, candidate and skill/default remark.");
    //                }
    //            }
    //            else
    //            {
    //                var existingAssessments = await assessmentQuery
    //                    .Take(2)
    //                    .ToListAsync();

    //                if (existingAssessments.Count > 1)
    //                {
    //                    return Fail(
    //                        409,
    //                        "Multiple assessments exist. Send CandidateSkillAssessmentId.");
    //                }

    //                assessment = existingAssessments.FirstOrDefault();
    //            }

    //            bool isNew = assessment == null;

    //            // Preserve the existing Save-first behavior.
    //            if (isNew && action == "AddRemark")
    //            {
    //                return Fail(
    //                    400,
    //                    "Save the assessment first, then add further remarks.");
    //            }

    //            // A new Default Remark must contain remark text.
    //            if (isNew &&
    //                isDefaultRemark &&
    //                string.IsNullOrWhiteSpace(request.Remarks))
    //            {
    //                return Fail(400, "Enter the default remark.");
    //            }

    //            if (!isNew)
    //            {
    //                if (assessment!.IsActive != true)
    //                    return Fail(409, "Assessment is inactive.");

    //                if (!isAdmin &&
    //                    assessment.AssignmentId != resolvedAssignmentId)
    //                {
    //                    return Fail(
    //                        409,
    //                        "This assessment belongs to a different assignment.");
    //                }

    //                if (!isHistoryEdit &&
    //                    action == "Save" &&
    //                    (assessment.Status == "Approved" ||
    //                     assessment.Status == "Rejected"))
    //                {
    //                    return Fail(
    //                        409,
    //                        "This assessment is completed. You can still use AddRemark.");
    //                }
    //            }

    //            var now = DateTime.UtcNow;

    //            // Edit only the selected history row.
    //            if (isHistoryEdit)
    //            {
    //                if (!isAdmin &&
    //                    historyToEdit!.AssignmentId != resolvedAssignmentId)
    //                {
    //                    return Fail(
    //                        409,
    //                        "This history record belongs to a different assignment.");
    //                }

    //                if (action == "Save")
    //                {
    //                    if (hasDetails)
    //                    {
    //                        historyToEdit!.DetailsJson =
    //                            request.Details!.Value.GetRawText();
    //                    }

    //                    if (request.Score.HasValue)
    //                    {
    //                        historyToEdit!.Score = request.Score.Value;
    //                    }
    //                }

    //                if (isRemarkAdded)
    //                {
    //                    historyToEdit!.Remarks = request.Remarks!.Trim();
    //                    historyToEdit.IsRemarkAdded = true;
    //                }

    //                historyToEdit!.ModifiedBy = employee.EmployeeId;
    //                historyToEdit.ModifiedOn = now;

    //                // Preserve Action, status fields, creator and parent assessment.
    //                await _context.SaveChangesAsync();

    //                var historyRemarksCount =
    //                    await _context.CandidateSkillAssessmentHistories
    //                        .CountAsync(x =>
    //                            x.CompanyId == request.CompanyId &&
    //                            x.CandidateSkillAssessmentId ==
    //                                assessment!.CandidateSkillAssessmentId &&
    //                            x.IsRemarkAdded);

    //                using var historyDetailsDocument =
    //                    JsonDocument.Parse(historyToEdit.DetailsJson);

    //                var historyDetails =
    //                    historyDetailsDocument.RootElement.Clone();

    //                await transaction.CommitAsync();

    //                return Ok(new
    //                {
    //                    Status = true,
    //                    Message = "Assessment history updated successfully.",

    //                    Data = new
    //                    {
    //                        historyToEdit.CandidateSkillAssessmentHistoryId,
    //                        historyToEdit.CandidateSkillAssessmentId,
    //                        historyToEdit.CompanyId,
    //                        request.CandidateId,
    //                        historyToEdit.JobDescriptionId,
    //                        historyToEdit.CandidateApplicationId,

    //                        SkillId = companySkillMasterId,
    //                        SkillName = skillName,
    //                        CategoryId = categoryId,
    //                        CategoryName = categoryName,
    //                        IsDefaultRemark = isDefaultRemark,

    //                        historyToEdit.SkillFormDefinitionId,
    //                        FormVersion = (int?)null,
    //                        historyToEdit.AssignmentId,

    //                        Details = historyDetails,
    //                        historyToEdit.Score,
    //                        historyToEdit.Remarks,
    //                        historyToEdit.Action,
    //                        historyToEdit.PreviousStatus,
    //                        historyToEdit.NewStatus,
    //                        historyToEdit.IsRemarkAdded,

    //                        AssessmentStatus = assessment!.Status,
    //                        RemarksCount = historyRemarksCount,

    //                        historyToEdit.CreatedBy,
    //                        CreatedByName = employee.EmployeeName,
    //                        historyToEdit.CreatedOn,

    //                        historyToEdit.ModifiedBy,
    //                        ModifiedByName = employee.EmployeeName,
    //                        historyToEdit.ModifiedOn
    //                    }
    //                });
    //            }

    //            var previousStatus = assessment?.Status;

    //            if (isNew)
    //            {
    //                assessment = new CandidateSkillAssessment
    //                {
    //                    CompanyId = request.CompanyId,
    //                    JobDescriptionId = request.JobDescriptionId!.Value,
    //                    CandidateApplicationId =
    //                        application.CandidateApplicationId,

    //                    // Null identifies a Default Remark assessment.
    //                    CompanySkillMasterId = companySkillMasterId??0,

    //                    // Preserved from your existing API.
    //                    SkillFormDefinitionId = 0,

    //                    AssignmentId = resolvedAssignmentId,
    //                    DetailsJson = "{}",
    //                    Status = "Pending",
    //                    IsActive = true,

    //                    CreatedBy = employee.EmployeeId,
    //                    CreatedOn = now
    //                };

    //                _context.CandidateSkillAssessments.Add(assessment);
    //            }

    //            if (action == "Save")
    //            {
    //                if (hasDetails)
    //                {
    //                    assessment!.DetailsJson =
    //                        request.Details!.Value.GetRawText();
    //                }

    //                if (request.Score.HasValue)
    //                {
    //                    assessment!.Score = request.Score.Value;
    //                }

    //                assessment!.Status = "InProgress";
    //            }

    //            if (isRemarkAdded)
    //            {
    //                assessment!.Remarks = request.Remarks!.Trim();
    //            }

    //            if (!isNew)
    //            {
    //                assessment!.ModifiedBy = employee.EmployeeId;
    //                assessment.ModifiedOn = now;
    //            }

    //            await _context.SaveChangesAsync();

    //            var history = new CandidateSkillAssessmentHistory
    //            {
    //                CandidateSkillAssessmentId =
    //                    assessment!.CandidateSkillAssessmentId,

    //                CompanyId = assessment.CompanyId,
    //                JobDescriptionId = assessment.JobDescriptionId,
    //                CandidateApplicationId =
    //                    assessment.CandidateApplicationId,

    //                CompanySkillMasterId = assessment.CompanySkillMasterId,
    //                SkillFormDefinitionId = assessment.SkillFormDefinitionId,
    //                AssignmentId = assessment.AssignmentId,

    //                Action = action,
    //                PreviousStatus = previousStatus,
    //                NewStatus = assessment.Status,

    //                DetailsJson = assessment.DetailsJson,
    //                Score = assessment.Score,
    //                Remarks = assessment.Remarks,
    //                IsRemarkAdded = isRemarkAdded,

    //                CreatedBy = employee.EmployeeId,
    //                CreatedOn = now
    //            };

    //            _context.CandidateSkillAssessmentHistories.Add(history);

    //            await _context.SaveChangesAsync();

    //            var remarksCount =
    //                await _context.CandidateSkillAssessmentHistories
    //                    .CountAsync(x =>
    //                        x.CompanyId == request.CompanyId &&
    //                        x.CandidateSkillAssessmentId ==
    //                            assessment.CandidateSkillAssessmentId &&
    //                        x.IsRemarkAdded);

    //            using var detailsDocument =
    //                JsonDocument.Parse(assessment.DetailsJson);

    //            var responseDetails = detailsDocument.RootElement.Clone();

    //            await transaction.CommitAsync();

    //            return Ok(new
    //            {
    //                Status = true,
    //                Message = action == "AddRemark"
    //                    ? "Remark added successfully."
    //                    : "Assessment saved successfully.",

    //                Data = new
    //                {
    //                    history.CandidateSkillAssessmentHistoryId,
    //                    assessment.CandidateSkillAssessmentId,
    //                    assessment.CompanyId,
    //                    request.CandidateId,
    //                    assessment.JobDescriptionId,
    //                    assessment.CandidateApplicationId,

    //                    SkillId = companySkillMasterId,
    //                    SkillName = skillName,
    //                    CategoryId = categoryId,
    //                    CategoryName = categoryName,
    //                    IsDefaultRemark = isDefaultRemark,

    //                    assessment.SkillFormDefinitionId,
    //                    FormVersion = (int?)null,
    //                    assessment.AssignmentId,

    //                    Details = responseDetails,
    //                    assessment.Score,
    //                    assessment.Remarks,
    //                    AssessmentStatus = assessment.Status,

    //                    RemarksCount = remarksCount,

    //                    assessment.CreatedBy,
    //                    CreatedByName = employee.EmployeeName,
    //                    assessment.CreatedOn,

    //                    assessment.ModifiedBy,
    //                    ModifiedByName = assessment.ModifiedBy.HasValue
    //                        ? employee.EmployeeName
    //                        : null,
    //                    assessment.ModifiedOn
    //                }
    //            });
    //        }
    //        catch (Exception ex)
    //        {
    //            Serilog.Log.Error(
    //                ex,
    //                "Failed to save skill assessment for CompanyId {CompanyId}, CandidateId {CandidateId}, JobDescriptionId {JobDescriptionId}, EmployeeId {EmployeeId}.",
    //                request.CompanyId,
    //                request.CandidateId,
    //                request.JobDescriptionId,
    //                request.EmployeeId);

    //            return StatusCode(500, new
    //            {
    //                Status = false,
    //                Message = "Unable to save skill assessment."
    //            });
    //        }
    //    }



        [HttpPost("ApproveRejectSkillAssessment")]
        public async Task<IActionResult> ApproveRejectSkillAssessment(
[FromBody] UpdateSkillAssessmentStatusRequest? request,
    [FromServices] Microsoft.Extensions.Configuration.IConfiguration configuration)
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
                request.CandidateId <= 0 ||
                request.JobDescriptionId <= 0 ||
                request.CompanySkillMasterId.GetValueOrDefault() < 0 ||
                request.EmployeeId <= 0 ||
                request.CandidateSkillAssessmentId < 0 ||
                request.CandidateSkillAssessmentHistoryId < 0)
            {
                return Fail(400, "Provide valid reference IDs.");
            }

            var action = request.Action?.Trim().ToLowerInvariant() switch
            {
                "approve" => "Approve",
                "reject" => "Reject",
                "return" => "Return",
                _ => null
            };

            if (action == null)
                return Fail(400, "Action must be Approve, Reject or Return.");

            bool isHistoryEdit =
                request.CandidateSkillAssessmentHistoryId > 0;

            //bool isDefaultRemark =
            //    request.CompanySkillMasterId.GetValueOrDefault() == 0;

            //int companySkillMasterId = request.CompanySkillMasterId.GetValueOrDefault();

            int companySkillMasterId =
    request.CompanySkillMasterId.GetValueOrDefault();

            if (companySkillMasterId > 0)
            {
                bool isDisabled = await _context.Set<CompanySkillMaster>()
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CompanySkillMasterId == companySkillMasterId &&
                        x.IsDisable == true);

                if (isDisabled)
                    companySkillMasterId = 0;
            }

            bool isDefaultRemark = companySkillMasterId == 0;

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var employee = await _context.EmployeeMasters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.EmployeeId);

                if (employee == null)
                    return Fail(404, "Employee not found for this company.");

                if (employee.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                bool isAdmin = employee.IsAdmin == true;

                var employeeName =
                    ((employee.FirstName ?? "") + " " +
                     (employee.LastName ?? "")).Trim();

                var candidateExists = await _context.Candidates
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId);

                if (!candidateExists)
                    return Fail(404, "Candidate not found for this company.");

                var job = await _context.JobDescriptions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.JobDescriptionId == request.JobDescriptionId);

                if (job == null)
                    return Fail(404, "Job description not found for this company.");

                var applications = await _context.CandidateApplications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId &&
                        x.JobDescriptionId == request.JobDescriptionId)
                    .Take(2)
                    .ToListAsync();

                if (applications.Count == 0)
                    return Fail(404, "Candidate application not found for this job.");

                if (applications.Count > 1)
                {
                    return Fail(
                        409,
                        "Multiple applications exist for this candidate and job. CandidateApplicationId is needed.");
                }

                var application = applications[0];

                if (application.IsActive != true)
                    return Fail(409, "Candidate application is inactive.");

                string? skillName = "Default Remark";
                int? categoryId = null;
                string? categoryName = null;

                // A positive skill ID must remain valid.
                // Invalid skills do not fall back to Default Remark.
                if (!isDefaultRemark)
                {
                    var skill = await _context.Set<CompanySkillMaster>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.CompanySkillMasterId == companySkillMasterId &&
                            x.IsActive == true);

                    if (skill == null)
                        return Fail(409, "The company skill is unavailable.");

                    var category = await _context.Set<CompanySkillMapping>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.SkillMappingId == skill.SkillMappingId &&
                            x.IsActive == true);

                    if (category == null)
                        return Fail(409, "The skill category is unavailable.");

                    skillName = skill.SkillName;
                    categoryId = category.SkillMappingId;
                    categoryName = category.CategoryName;
                }

                CandidateSkillAssessmentHistory? historyToEdit = null;

                // Preserve the existing skill-based lookup.
                var assessmentQuery = _context.CandidateSkillAssessments
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CompanySkillMasterId == companySkillMasterId &&
                        x.JobDescriptionId == request.JobDescriptionId &&
                        x.CandidateApplicationId ==
                            application.CandidateApplicationId &&
                        x.CreatedBy == employee.EmployeeId);

                CandidateSkillAssessment? assessment;

                if (isHistoryEdit)
                {
                    // Edit only the employee's exact history record.
                    historyToEdit =
                        await _context.CandidateSkillAssessmentHistories
                            .FirstOrDefaultAsync(x =>
                                x.CandidateSkillAssessmentHistoryId ==
                                    request.CandidateSkillAssessmentHistoryId &&
                                x.CompanyId == request.CompanyId &&
                                x.CompanySkillMasterId == companySkillMasterId &&
                                x.JobDescriptionId == request.JobDescriptionId &&
                                x.CandidateApplicationId ==
                                    application.CandidateApplicationId &&
                                x.CreatedBy == employee.EmployeeId);

                    if (historyToEdit == null)
                    {
                        return Fail(
                            404,
                            "History record not found for this employee, candidate, job and skill/default remark.");
                    }

                    if (historyToEdit.Action != "Approve" &&
                        historyToEdit.Action != "Reject" &&
                        historyToEdit.Action != "Return")
                    {
                        return Fail(
                            400,
                            "Use this API to edit approval/rejection/return history only.");
                    }

                    if (request.CandidateSkillAssessmentId > 0 &&
                        request.CandidateSkillAssessmentId !=
                            historyToEdit.CandidateSkillAssessmentId)
                    {
                        return Fail(
                            400,
                            "History record does not belong to the supplied assessment.");
                    }

                    assessment = await assessmentQuery
                        .FirstOrDefaultAsync(x =>
                            x.CandidateSkillAssessmentId ==
                                historyToEdit.CandidateSkillAssessmentId);

                    if (assessment == null)
                    {
                        return Fail(
                            404,
                            "Assessment not found for this history record.");
                    }
                }
                else
                {
                    assessment = null;

                    if (request.CandidateSkillAssessmentId > 0)
                    {
                        assessment = await assessmentQuery
                            .FirstOrDefaultAsync(x =>
                                x.CandidateSkillAssessmentId ==
                                    request.CandidateSkillAssessmentId);

                        if (assessment == null)
                        {
                            return Fail(
                                404,
                                "Assessment not found for this employee, candidate, job and skill.");
                        }
                    }

                    // Without a supplied ID, find this employee's assessment.
                    // No match is created later after existing permission checks.
                    if (assessment == null)
                    {
                        var matchingAssessments = await assessmentQuery
                            .Take(2)
                            .ToListAsync();

                        if (matchingAssessments.Count > 1)
                        {
                            return Fail(
                                409,
                                "This employee has multiple assessments for this candidate, job and skill. Send the correct CandidateSkillAssessmentId.");
                        }

                        assessment = matchingAssessments.FirstOrDefault();
                    }
                }

                bool isNew = assessment == null;

                if (!isNew && assessment!.IsActive != true)
                    return Fail(409, "Skill assessment is inactive.");

                int? resolvedAssignmentId = assessment?.AssignmentId;
                if (!isAdmin)
                {
                    bool skipSkillMapping = isDefaultRemark;

                    if (!isDefaultRemark)
                    {
                        skipSkillMapping = await _context.Set<CompanySkillMaster>()
                            .AsNoTracking()
                            .AnyAsync(x =>
                                x.CompanyId == request.CompanyId &&
                                x.CompanySkillMasterId == companySkillMasterId &&
                                x.IsActive == true &&
                                x.IsDisable == true);
                    }

                    var assignmentQuery = _context.RoleTemplateAssignment
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            x.EmployeeId == employee.EmployeeId &&
                            x.TemplateId == job.TemplateId &&
                            x.IsActive == true)
                        .Where(x =>
                            _context.Set<RoleTemplate>().Any(template =>
                                template.CompanyId == x.CompanyId &&
                                template.TemplateId == x.TemplateId &&
                                template.IsActive == true));

                    // Default remarks and disabled skills skip this check only.
                    if (!skipSkillMapping)
                    {
                        assignmentQuery = assignmentQuery.Where(x =>
                            _context.Set<RoleTemplateAssignmentSkill>().Any(mapping =>
                                mapping.CompanyId == x.CompanyId &&
                                mapping.AssignmentId == x.AssignmentId &&
                                mapping.SkillId == companySkillMasterId &&
                                mapping.IsActive == true));
                    }

                    if (!isNew)
                    {
                        if (!assessment!.AssignmentId.HasValue)
                        {
                            return Fail(
                                403,
                                "No role assignment is linked to this assessment.");
                        }

                        int existingAssignmentId = assessment.AssignmentId.Value;

                        assignmentQuery = assignmentQuery.Where(x =>
                            x.AssignmentId == existingAssignmentId);
                    }

                    var assignments = await assignmentQuery
                        .Take(2)
                        .ToListAsync();

                    if (assignments.Count == 0)
                    {
                        return Fail(
                            403,
                            skipSkillMapping
                                ? "An active assignment for this employee and job template was not found."
                                : "An active assignment for this employee, job template and skill was not found.");
                    }

                    if (assignments.Count > 1)
                    {
                        return Fail(
                            409,
                            "Multiple matching assignments exist. AssignmentId is needed.");
                    }

                    var assignment = assignments[0];
                    resolvedAssignmentId = assignment.AssignmentId;

                    if (isHistoryEdit &&
                        historyToEdit!.AssignmentId != resolvedAssignmentId)
                    {
                        return Fail(
                            403,
                            "The history record belongs to a different assignment.");
                    }

                    if (action == "Approve" && assignment.CanApprove != true)
                        return Fail(403, "Approve permission is not available.");

                    if (action == "Reject" && assignment.CanReject != true)
                        return Fail(403, "Reject permission is not available.");

                    if (action == "Return" && assignment.CanReturn != true)
                        return Fail(403, "Return permission is not available.");
                }

                var now = DateTime.UtcNow;

                string newStatus = action switch
                {
                    "Approve" => "Approved",
                    "Reject" => "Rejected",
                    _ => "Returned"
                };

                bool isRemarkAdded =
                    !string.IsNullOrWhiteSpace(request.Remarks);

                if (isHistoryEdit)
                {
                    historyToEdit!.Action = action;
                    historyToEdit.NewStatus = newStatus;

                    if (request.Score.HasValue)
                        historyToEdit.Score = request.Score.Value;

                    if (isRemarkAdded)
                    {
                        historyToEdit.Remarks = request.Remarks!.Trim();
                        historyToEdit.IsRemarkAdded = true;
                    }

                    historyToEdit.ModifiedBy = employee.EmployeeId;
                    historyToEdit.ModifiedOn = now;

                    // Preserve CreatedBy, CreatedOn, PreviousStatus,
                    // DetailsJson and the parent assessment.
                    await _context.SaveChangesAsync();

                    var historyRemarksCount =
                        await _context.CandidateSkillAssessmentHistories
                            .CountAsync(x =>
                                x.CompanyId == request.CompanyId &&
                                x.CandidateSkillAssessmentId ==
                                    assessment!.CandidateSkillAssessmentId &&
                                x.IsRemarkAdded);

                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        Status = true,
                        Message = "Approval/rejection history updated successfully.",
                        Data = new
                        {
                            historyToEdit.CandidateSkillAssessmentHistoryId,
                            historyToEdit.CandidateSkillAssessmentId,
                            historyToEdit.CompanyId,
                            application.CandidateId,
                            historyToEdit.CandidateApplicationId,
                            historyToEdit.JobDescriptionId,
                            historyToEdit.CompanySkillMasterId,

                            SkillId = companySkillMasterId,
                            SkillName = skillName,
                            CategoryId = categoryId,
                            CategoryName = categoryName,
                            IsDefaultRemark = isDefaultRemark,
                            historyToEdit.AssignmentId,

                            historyToEdit.Action,
                            historyToEdit.PreviousStatus,
                            historyToEdit.NewStatus,
                            historyToEdit.Score,
                            historyToEdit.Remarks,
                            historyToEdit.IsRemarkAdded,

                            AssessmentStatus = assessment!.Status,
                            RemarksCount = historyRemarksCount,

                            historyToEdit.CreatedBy,
                            CreatedByName = employeeName,
                            historyToEdit.CreatedOn,

                            historyToEdit.ModifiedBy,
                            ModifiedByName = employeeName,
                            historyToEdit.ModifiedOn
                        }
                    });
                }

                // Preserve existing status restrictions.
                if (!isNew &&
                    assessment!.Status != "InProgress" &&
                    assessment.Status != "Approved" &&
                    assessment.Status != "Rejected" &&
                    assessment.Status != "Returned")
                {
                    return Fail(
                        409,
                        "Save the assessment before approving or rejecting it.");
                }

                var previousStatus = assessment?.Status;

                if (isNew)
                {
                    assessment = new CandidateSkillAssessment
                    {
                        CompanyId = request.CompanyId,
                        CandidateApplicationId =
                            application.CandidateApplicationId,
                        JobDescriptionId = request.JobDescriptionId,

                        // Zero means Default Remark.
                        CompanySkillMasterId = companySkillMasterId,

                        SkillFormDefinitionId = 0,

                        AssignmentId = resolvedAssignmentId,
                        DetailsJson = "{}",
                        Status = newStatus,
                        IsActive = true,

                        CreatedBy = employee.EmployeeId,
                        CreatedOn = now
                    };

                    _context.CandidateSkillAssessments.Add(assessment);
                }

                assessment!.Status = newStatus;

                if (request.Score.HasValue)
                    assessment.Score = request.Score.Value;

                if (isRemarkAdded)
                    assessment.Remarks = request.Remarks!.Trim();

                if (!isNew)
                {
                    assessment.ModifiedBy = employee.EmployeeId;
                    assessment.ModifiedOn = now;
                }

                await _context.SaveChangesAsync();

                var history = new CandidateSkillAssessmentHistory
                {
                    CandidateSkillAssessmentId =
                        assessment.CandidateSkillAssessmentId,

                    CompanyId = assessment.CompanyId,
                    CandidateApplicationId = assessment.CandidateApplicationId,
                    JobDescriptionId = assessment.JobDescriptionId,
                    CompanySkillMasterId = assessment.CompanySkillMasterId,
                    SkillFormDefinitionId = assessment.SkillFormDefinitionId,
                    AssignmentId = assessment.AssignmentId,

                    Action = action,
                    PreviousStatus = previousStatus,
                    NewStatus = assessment.Status,

                    DetailsJson = assessment.DetailsJson,
                    Score = assessment.Score,
                    Remarks = assessment.Remarks,
                    IsRemarkAdded = isRemarkAdded,

                    // The history creator identifies who made this decision.
                    CreatedBy = employee.EmployeeId,
                    CreatedOn = now
                };

                _context.CandidateSkillAssessmentHistories.Add(history);

                await _context.SaveChangesAsync();

                var remarksCount =
                    await _context.CandidateSkillAssessmentHistories
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.CandidateSkillAssessmentId ==
                                assessment.CandidateSkillAssessmentId &&
                            x.IsRemarkAdded);

                string? assessmentCreatorName = null;

                if (assessment.CreatedBy == employee.EmployeeId)
                {
                    assessmentCreatorName = employeeName;
                }
                else if (assessment.CreatedBy.HasValue)
                {
                    assessmentCreatorName = await _context.EmployeeMasters
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            x.EmployeeId == assessment.CreatedBy.Value)
                        .Select(x =>
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim())
                        .FirstOrDefaultAsync();
                }

                // Notification records are committed with the assessment decision.
                // Each skill/default-remark scope routes independently.
                var notificationIds = new List<int>();
                int notificationTemplateId = Convert.ToInt32(job.TemplateId);
                if (notificationTemplateId > 0)
                {
                    notificationIds = await SaveRoutedDecisionNotificationsAsync(
                        request.CompanyId,
                        application.CandidateApplicationId,
                        application.CandidateId,
                        assessment.JobDescriptionId,
                        notificationTemplateId,
                        companySkillMasterId,
                        history.CandidateSkillAssessmentHistoryId,
                        employee.EmployeeId,
                        resolvedAssignmentId,
                        employeeName,
                        job.JobTitle ?? "the job",
                        action);
                }

                await transaction.CommitAsync();

                // EP is a separate database. A failed copy does not undo the decision.
                // Failed/Pending copies can be retried using the same notification IDs.
                try
                {
                    await MirrorDecisionNotificationsToEPAsync(
                        request.CompanyId, notificationIds, configuration);
                }
                catch (Exception notificationException)
                {
                    Serilog.Log.Error(notificationException,
                        "EP notification processing failed after assessment commit for CompanyId {CompanyId}.",
                        request.CompanyId);
                }

                return Ok(new
                {
                    Status = true,
                    NotificationIds = notificationIds,
                    Message = isNew
                        ? "Assessment and decision created successfully."
                        : "Assessment decision saved successfully.",

                    Data = new
                    {
                        history.CandidateSkillAssessmentHistoryId,
                        assessment.CandidateSkillAssessmentId,
                        assessment.CompanyId,
                        application.CandidateId,
                        assessment.CandidateApplicationId,
                        assessment.JobDescriptionId,
                        assessment.CompanySkillMasterId,

                        SkillId = companySkillMasterId,
                        SkillName = skillName,
                        CategoryId = categoryId,
                        CategoryName = categoryName,
                        IsDefaultRemark = isDefaultRemark,
                        assessment.AssignmentId,

                        history.Action,
                        PreviousStatus = previousStatus,
                        AssessmentStatus = assessment.Status,
                        assessment.Score,
                        assessment.Remarks,
                        RemarksCount = remarksCount,

                        // Preserve existing assessment audit response fields.
                        assessment.CreatedBy,
                        CreatedByName = assessmentCreatorName,
                        assessment.CreatedOn,

                        assessment.ModifiedBy,
                        ModifiedByName = assessment.ModifiedBy.HasValue
                            ? employeeName
                            : null,
                        assessment.ModifiedOn,

                        // Explicitly identify this decision's author.
                        HistoryCreatedBy = history.CreatedBy,
                        HistoryCreatedByName = employeeName,
                        HistoryCreatedOn = history.CreatedOn
                    }
                });
            }
            catch (DecisionNotificationRoutingException ex)
            {
                return Fail(409, ex.Message);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save assessment decision for CompanyId {CompanyId}, AssessmentId {AssessmentId}, HistoryId {HistoryId}, EmployeeId {EmployeeId}.",
                    request.CompanyId,
                    request.CandidateSkillAssessmentId,
                    request.CandidateSkillAssessmentHistoryId,
                    request.EmployeeId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to save skill assessment decision."
                });
            }
        }




        // Paste these members INSIDE the same controller as ApproveRejectSkillAssessment.
        // Required namespaces: System.Data, System.Text.Json, Microsoft.Data.SqlClient,
        // Microsoft.EntityFrameworkCore, Microsoft.Extensions.Configuration,
        // Recruitment.Model.Entity.

        private sealed class DecisionNotificationRoutingException : Exception
        {
            public DecisionNotificationRoutingException(string message) : base(message) { }
        }

        private async Task<List<int>> SaveRoutedDecisionNotificationsAsync(
            int companyId, int candidateApplicationId, int candidateId,
            int jobDescriptionId, int templateId, int skillId,
            int historyId, int actorId, int? actingAssignmentId,
            string actorName, string jobTitle, string action)
        {
            // Called inside the existing Recruitment transaction, after history has an ID.
            // No notifications are created by the history-only edit branch.
            var company = await _context.Companys.AsNoTracking()
                .Where(x => x.CompanyID == companyId)
                .Select(x => new { x.EpId }).FirstOrDefaultAsync();
            if (company == null)
                throw new DecisionNotificationRoutingException("Company not found for notification routing.");

            var templateActive = await _context.RoleTemplates.AsNoTracking()
                .AnyAsync(x => x.CompanyId == companyId && x.TemplateId == templateId && x.IsActive == true);
            if (!templateActive)
                throw new DecisionNotificationRoutingException("The job template is unavailable for notification routing.");

            var assignments = await _context.RoleTemplateAssignment.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.TemplateId == templateId && x.IsActive == true)
                .ToListAsync();

            // Never route as another employee merely because the assessment belongs to them.
            var actorAssignments = assignments.Where(x => x.EmployeeId == actorId).ToList();
            var actor = actingAssignmentId.HasValue
                ? actorAssignments.FirstOrDefault(x => x.AssignmentId == actingAssignmentId.Value)
                : null;
            if (actor == null)
            {
                if (actorAssignments.Count > 1)
                    throw new DecisionNotificationRoutingException("Multiple acting assignments exist; notification routing is ambiguous.");
                actor = actorAssignments.FirstOrDefault();
            }
            // Standalone admin decisions retain their existing behavior: no invented hierarchy.
            if (actor == null)
                return new List<int>();

            if (assignments.GroupBy(x => x.HierarchyLevel).Any(g => g.Count() > 1))
                throw new DecisionNotificationRoutingException("Duplicate hierarchy levels make notification routing ambiguous.");

            var assignmentById = assignments.ToDictionary(x => x.AssignmentId);
            var employeeIds = assignments.Select(x => x.EmployeeId).Distinct().ToArray();
            var activeEmployeeIds = (await _context.EmployeeMasters.AsNoTracking()
                .Where(x => x.CompanyId == companyId && employeeIds.Contains(x.EmployeeId) && x.IsActive == true)
                .Select(x => x.EmployeeId).ToListAsync()).ToHashSet();

            var incoming = await _context.RecruitmentNotifications
                .Where(x => x.CompanyId == companyId &&
                    x.CandidateApplicationId == candidateApplicationId &&
                    x.JobDescriptionId == jobDescriptionId && x.TemplateId == templateId &&
                    x.CompanySkillMasterId == skillId && x.ToUserId == actorId &&
                    x.ToAssignmentId == actor.AssignmentId && x.IsActionRequest &&
                    x.ActionedByHistoryId == null)
                .ToListAsync();

            // Repeated notifications for the same context may be consumed together.
            // Different reporting continuations must not be guessed.
            var continuationIds = incoming.Select(x => x.ResumeAfterAssignmentId).Distinct().ToList();
            //if (continuationIds.Count > 1)
            //    throw new DecisionNotificationRoutingException("Multiple reporting continuations are pending for this employee and skill.");

            int? resumeAfterId = continuationIds.FirstOrDefault();
            var origin = actor;
            if (resumeAfterId.HasValue)
            {
                if (!assignmentById.TryGetValue(resumeAfterId.Value, out var savedOrigin))
                    throw new DecisionNotificationRoutingException("The original reporting-stage assignment is no longer active.");
                origin = savedOrigin;
            }

            // Recipient assignment, optional reporting continuation, actionable flag.
            var recipients = new List<(int AssignmentId, int? ResumeAfterId, bool ActionRequired)>();
            if (action == "Approve")
            {
                if (!resumeAfterId.HasValue && actor.ReportingToAssignmentId.HasValue)
                {
                    if (!assignmentById.TryGetValue(actor.ReportingToAssignmentId.Value, out var reporting))
                        throw new DecisionNotificationRoutingException("The reporting-to assignment is not active in this template.");
                    if (reporting.EmployeeId == actorId)
                        throw new DecisionNotificationRoutingException("An employee cannot approve their own reporting-to request.");
                    if (!activeEmployeeIds.Contains(reporting.EmployeeId) || reporting.CanApprove != true)
                        throw new DecisionNotificationRoutingException("The reporting-to employee is inactive or cannot approve.");
                    recipients.Add((reporting.AssignmentId, actor.AssignmentId, true));
                }
                else
                {
                    // After a reporting approval, resume after the original H1, not after
                    // the reporting employee's hierarchy (which might be H3).
                    var next = assignments.Where(x => x.HierarchyLevel > origin.HierarchyLevel && x.EmployeeId != actorId)
                        .OrderBy(x => x.HierarchyLevel).FirstOrDefault();
                    if (next != null)
                    {
                        if (!activeEmployeeIds.Contains(next.EmployeeId))
                            throw new DecisionNotificationRoutingException("The next hierarchy employee is inactive.");
                        recipients.Add((next.AssignmentId, null, true));
                    }
                }
            }
            else
            {
                // A reporting approver acts on behalf of the origin stage. Include that
                // original actor on rejection, even when it is the first hierarchy.
                var lower = assignments.Where(x =>
                    x.HierarchyLevel < origin.HierarchyLevel ||
                    (resumeAfterId.HasValue && x.AssignmentId == origin.AssignmentId)).ToList();

                var visited = new HashSet<int>();
                foreach (var lowerAssignment in lower)
                {
                    var current = lowerAssignment;
                    while (current != null && visited.Add(current.AssignmentId))
                    {
                        if (current.EmployeeId != actorId && activeEmployeeIds.Contains(current.EmployeeId))
                            recipients.Add((current.AssignmentId, null, false));

                        if (!current.ReportingToAssignmentId.HasValue ||
                            !assignmentById.TryGetValue(current.ReportingToAssignmentId.Value, out var parent))
                            break;
                        // The rejecting actor is excluded, but other reporting recipients
                        // are included even when their numeric hierarchy is higher.
                        if (parent.EmployeeId == actorId)
                            break;
                        current = parent;
                    }
                }
            }

            foreach (var item in incoming)
                item.ActionedByHistoryId = historyId;

            var distinctRecipients = recipients
                .GroupBy(x => assignmentById[x.AssignmentId].EmployeeId)
                .Select(g => g.First()).ToList();

            var candidate = await _context.Candidates.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.CandidateId == candidateId)
                .Select(x => new { x.FirstName, x.MiddleName, x.LastName }).FirstOrDefaultAsync();
            var candidateName = candidate == null ? candidateId.ToString() : string.Join(" ",
                new[] { candidate.FirstName, candidate.MiddleName, candidate.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(candidateName)) candidateName = candidateId.ToString();

            string verb = action == "Approve" ? "approved" : action == "Reject" ? "rejected" : "returned";
            var rows = new List<RecruitmentNotification>();
            foreach (var recipient in distinctRecipients)
            {
                int toUserId = assignmentById[recipient.AssignmentId].EmployeeId;
                var existing = await _context.RecruitmentNotifications.FirstOrDefaultAsync(x =>
                    x.CompanyId == companyId && x.CandidateSkillAssessmentHistoryId == historyId && x.ToUserId == toUserId);
                if (existing != null) { rows.Add(existing); continue; }

                var row = new RecruitmentNotification
                {
                    CompanyId = companyId,
                    FromUserId = actorId,
                    ToUserId = toUserId,
                    CandidateApplicationId = candidateApplicationId,
                    JobDescriptionId = jobDescriptionId,
                    TemplateId = templateId,
                    FromAssignmentId = actor.AssignmentId,
                    ToAssignmentId = recipient.AssignmentId,
                    CompanySkillMasterId = skillId,
                    CandidateSkillAssessmentHistoryId = historyId,
                    Action = action,
                    Type = "Recruitment",
                    Title = "Candidate " + verb,
                    Message = $"{actorName} {verb} candidate {candidateName} for {jobTitle}." +
                        (recipient.ActionRequired ? " Your action is required." : " Please review the decision and remarks."),
                    CreatedOn = DateTime.UtcNow,
                    IsRead = false,
                    DeliveryStatus = "NotRequired",
                    EPMirrorStatus = company.EpId > 0 ? "Pending" : "NotRequired",
                    IsActionRequest = recipient.ActionRequired,
                    ResumeAfterAssignmentId = recipient.ResumeAfterId
                };
                _context.RecruitmentNotifications.Add(row);
                rows.Add(row);
            }
            await _context.SaveChangesAsync();
            return rows.Select(x => x.NotificationId).ToList();
        }

        private async Task MirrorDecisionNotificationsToEPAsync(
            int companyId, IEnumerable<int> notificationIds, IConfiguration configuration)
        {
            var ids = notificationIds.Distinct().ToArray();
            var rows = await _context.RecruitmentNotifications.Where(x =>
                x.CompanyId == companyId && ids.Contains(x.NotificationId) &&
                (x.EPMirrorStatus == "Pending" || x.EPMirrorStatus == "Failed")).ToListAsync();

            foreach (var row in rows)
            {
                try
                {
                    var company = await _context.Companys.AsNoTracking()
                        .Where(x => x.CompanyID == companyId)
                        .Select(x => new { x.EpId }).FirstOrDefaultAsync();
                    if (company == null || !(company.EpId > 0))
                        throw new InvalidOperationException("Company EP mapping is missing.");

                    var employeeIds = new[] { row.FromUserId, row.ToUserId };
                    var employees = await _context.EmployeeMasters.AsNoTracking()
                        .Where(x => x.CompanyId == companyId && employeeIds.Contains(x.EmployeeId))
                        .Select(x => new { x.EmployeeId, x.EPID }).ToListAsync();
                    var sender = employees.FirstOrDefault(x => x.EmployeeId == row.FromUserId);
                    var recipient = employees.FirstOrDefault(x => x.EmployeeId == row.ToUserId);
                    if (sender == null || !(sender.EPID > 0) || recipient == null || !(recipient.EPID > 0))
                        throw new InvalidOperationException("Sender or recipient EPID is missing.");

                    var employeeConnectionString = configuration.GetConnectionString("EPDatabase");
                    var notificationConnectionString = configuration.GetConnectionString("EPNotificationDatabase");
                    if (string.IsNullOrWhiteSpace(employeeConnectionString) || string.IsNullOrWhiteSpace(notificationConnectionString))
                        throw new InvalidOperationException("EP database connection settings are missing.");

                    // Employee validation uses the original EP database; the notification
                    // table is in Easypagr, which may be a different database.
                    await using (var employeeConnection = new SqlConnection(employeeConnectionString))
                    {
                        await employeeConnection.OpenAsync();
                        using var check = employeeConnection.CreateCommand();
                        check.CommandText = @"SELECT COUNT(*) FROM dbo.tblEmp_Registration
WHERE OrgID = @OrgID AND ID IN (@FromUserID, @ToUserID);";
                        check.Parameters.Add("@OrgID", SqlDbType.Int).Value = Convert.ToInt32(company.EpId);
                        check.Parameters.Add("@FromUserID", SqlDbType.Int).Value = sender.EPID.Value;
                        check.Parameters.Add("@ToUserID", SqlDbType.Int).Value = recipient.EPID.Value;
                        int expected = sender.EPID.Value == recipient.EPID.Value ? 1 : 2;
                        if (Convert.ToInt32(await check.ExecuteScalarAsync()) != expected)
                            throw new InvalidOperationException("EP users do not belong to the company's EP organization.");
                    }

                    await using var connection = new SqlConnection(notificationConnectionString);
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.tblEmp_NotificationHistory WITH (UPDLOCK, HOLDLOCK)
        WHERE Type = @Type AND ReferenceID = @ReferenceID
          AND FromUserID = @FromUserID AND ToUserID = @ToUserID
    )
    BEGIN
        INSERT INTO dbo.tblEmp_NotificationHistory
        (FromUserID, ToUserID, Type, Title, Message, CreatedDate, CreatedBy,
         LanguageId, IsRead, ReferenceID, RecordCreated, ImageURL, Details, IsEmail)
        VALUES
        (@FromUserID, @ToUserID, @Type, @Title, @Message, @CreatedDate, @FromUserID,
         0, 0, @ReferenceID, @CreatedDate, NULL, @Details, 0);
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;";
                    command.Parameters.Add("@FromUserID", SqlDbType.Int).Value = sender.EPID.Value;
                    command.Parameters.Add("@ToUserID", SqlDbType.Int).Value = recipient.EPID.Value;
                    command.Parameters.Add("@Type", SqlDbType.NVarChar, 50).Value = "Recruitment";
                    command.Parameters.Add("@ReferenceID", SqlDbType.Int).Value = row.NotificationId;
                    command.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = row.Title;
                    command.Parameters.Add("@Message", SqlDbType.NVarChar, -1).Value = row.Message;
                    command.Parameters.Add("@CreatedDate", SqlDbType.DateTime2).Value = row.CreatedOn;
                    command.Parameters.Add("@Details", SqlDbType.NVarChar, -1).Value = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Source = "Recruitment",
                        row.NotificationId,
                        row.CompanyId,
                        row.CandidateApplicationId,
                        row.JobDescriptionId,
                        row.TemplateId,
                        row.CompanySkillMasterId,
                        row.CandidateSkillAssessmentHistoryId,
                        row.FromAssignmentId,
                        row.ToAssignmentId,
                        row.Action
                    });
                    await command.ExecuteNonQueryAsync();
                    row.EPMirrorStatus = "Saved";
                    row.EPMirrorError = null;
                }
                catch (Exception ex)
                {
                    row.EPMirrorStatus = "Failed";
                    row.EPMirrorError = "Unable to save EP notification. Check server logs.";
                    Serilog.Log.Error(ex, "Failed EP copy for Recruitment notification {NotificationId}.", row.NotificationId);
                }
            }
            await _context.SaveChangesAsync();
        }



        [HttpPost("SaveCandidateSkillAssessment")]
        public async Task<IActionResult> SaveCandidateSkillAssessment(
    [FromBody] SaveCandidateSkillAssessmentRequest? request)
        {
            IActionResult Fail(int statusCode, string message)
            {
                return StatusCode(statusCode, new
                {
                    Status = false,
                    Message = message
                });
            }

            if (request == null)
                return Fail(400, "Invalid request.");

            if (request.CompanyId <= 0 ||
                request.CandidateId <= 0 ||
                request.JobDescriptionId.GetValueOrDefault() <= 0 ||
                request.EmployeeId.GetValueOrDefault() <= 0 ||
                request.SkillId.GetValueOrDefault() < 0 ||
                request.CandidateSkillAssessmentId < 0 ||
                request.CandidateSkillAssessmentHistoryId < 0)
            {
                return Fail(400, "Provide valid reference IDs.");
            }

            bool isResume = request.IsResume;

            // Preserve the supplied API's normal lookup behavior.
            bool isDefaultRemark = request.SkillId.GetValueOrDefault() == 0;
            int? companySkillMasterId = isResume
                ? request.SkillId.GetValueOrDefault()
                : isDefaultRemark ? (int?)null : request.SkillId.Value;

            bool isHistoryEdit =
                request.CandidateSkillAssessmentHistoryId > 0;

            var action = request.Action?.Trim().ToLowerInvariant() switch
            {
                null => "Save",
                "" => "Save",
                "save" => "Save",
                "addremark" => "AddRemark",
                _ => null
            };

            if (action == null)
            {
                return Fail(
                    400,
                    "This API supports Save and AddRemark. Approval/rejection is performed separately after saving.");
            }

            // Preserve skill-based behavior.
            // Default remarks also save supplied remark text without requiring the flag.
            bool isRemarkAdded = !isResume &&
                (action == "AddRemark" ||
                 request.IsRemarkAdded ||
                 (isDefaultRemark && !string.IsNullOrWhiteSpace(request.Remarks)));

            if (isRemarkAdded &&
                string.IsNullOrWhiteSpace(request.Remarks))
            {
                return Fail(400, "Enter the remark to add.");
            }

            bool hasDetails =
                request.Details.HasValue &&
                request.Details.Value.ValueKind != JsonValueKind.Null &&
                request.Details.Value.ValueKind != JsonValueKind.Undefined;

            if (action == "Save" &&
                hasDetails &&
                request.Details!.Value.ValueKind != JsonValueKind.Object)
            {
                return Fail(400, "Details must be a JSON object.");
            }

            if (isResume)
            {
                if (action != "Save")
                    return Fail(400, "Resume details must use Action = Save.");

                if (companySkillMasterId != 0)
                    return Fail(400, "Resume details must use SkillId = 0.");

                if (request.IsRemarkAdded ||
                    !string.IsNullOrWhiteSpace(request.Remarks))
                {
                    return Fail(400,
                        "Save resume details with IsRemarkAdded = false and no Remarks. Use AddRemark separately for remarks.");
                }

                if (!hasDetails ||
                    request.Details!.Value.ValueKind != JsonValueKind.Object ||
                    !request.Details.Value.EnumerateObject().Any())
                {
                    return Fail(400, "Provide a non-empty Details JSON object for the resume.");
                }
            }

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var employee = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.EmployeeId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsAdmin,
                        x.IsActive,
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

                var candidateExists = await _context.Candidates
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId);

                if (!candidateExists)
                    return Fail(404, "Candidate not found for this company.");

                var job = await _context.JobDescriptions
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.JobDescriptionId == request.JobDescriptionId)
                    .Select(x => new
                    {
                        x.JobDescriptionId,
                        x.TemplateId
                    })
                    .FirstOrDefaultAsync();

                if (job == null)
                    return Fail(404, "Job description not found for this company.");

                var applications = await _context.CandidateApplications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId &&
                        x.JobDescriptionId == request.JobDescriptionId)
                    .Select(x => new
                    {
                        x.CandidateApplicationId,
                        x.IsActive
                    })
                    .Take(2)
                    .ToListAsync();

                if (applications.Count == 0)
                {
                    return Fail(
                        404,
                        "Candidate application not found for this JD.");
                }

                if (applications.Count > 1)
                {
                    return Fail(
                        409,
                        "Multiple applications exist for this candidate and JD. CandidateApplicationId is needed to identify the correct application.");
                }

                var application = applications[0];

                if (application.IsActive != true)
                    return Fail(409, "Candidate application is inactive.");

                string? skillName = "Default Remark";
                int? categoryId = null;
                string? categoryName = null;

                // Default remarks require no company skill or category.
                if (!isDefaultRemark)
                {
                    var skill = await _context.CompanySkillMasters
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            x.CompanySkillMasterId == companySkillMasterId &&
                            x.IsActive == true)
                        .Select(x => new
                        {
                            x.CompanySkillMasterId,
                            x.SkillName,
                            x.SkillMappingId
                        })
                        .FirstOrDefaultAsync();

                    if (skill == null)
                        return Fail(404, "Active company skill not found.");

                    var category = await _context.CompanySkillMappings
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            x.SkillMappingId == skill.SkillMappingId &&
                            x.IsActive == true)
                        .Select(x => new
                        {
                            x.SkillMappingId,
                            x.CategoryName
                        })
                        .FirstOrDefaultAsync();

                    if (category == null)
                        return Fail(404, "Active company skill category not found.");

                    skillName = skill.SkillName;
                    categoryId = category.SkillMappingId;
                    categoryName = category.CategoryName;
                }

                int? resumeAssignmentId = null;
                if (isResume)
                {
                    var firstAssignment = await _context.RoleTemplateAssignment
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            x.TemplateId == job.TemplateId &&
                            x.IsActive == true)
                        .Where(x => _context.RoleTemplates.Any(template =>
                            template.CompanyId == x.CompanyId &&
                            template.TemplateId == x.TemplateId &&
                            template.IsActive == true))
                        .OrderBy(x => x.HierarchyLevel)
                        .ThenBy(x => x.AssignmentId)
                        .Select(x => new { x.AssignmentId, x.EmployeeId })
                        .FirstOrDefaultAsync();

                    if (firstAssignment == null ||
                        firstAssignment.EmployeeId != employee.EmployeeId)
                    {
                        return Fail(403,
                            "Only the first hierarchy employee in this job's active template can save resume details.");
                    }

                    resumeAssignmentId = firstAssignment.AssignmentId;
                }

                int? resolvedAssignmentId = null;

                if (!isAdmin || isResume)
                {
                    var assignmentQuery = _context.RoleTemplateAssignment
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            x.EmployeeId == employee.EmployeeId &&
                            x.TemplateId == job.TemplateId &&
                            x.IsActive == true)
                        .Where(x =>
                            _context.Set<RoleTemplate>().Any(template =>
                                template.CompanyId == x.CompanyId &&
                                template.TemplateId == x.TemplateId &&
                                template.IsActive == true));

                    // Default remarks still require an active job-template assignment.
                    // Only the skill-mapping requirement is skipped.
                    if (!isDefaultRemark)
                    {
                        assignmentQuery = assignmentQuery.Where(x =>
                            _context.Set<RoleTemplateAssignmentSkill>().Any(mapping =>
                                mapping.CompanyId == x.CompanyId &&
                                mapping.AssignmentId == x.AssignmentId &&
                                mapping.SkillId == companySkillMasterId &&
                                mapping.IsActive == true));
                    }

                    if (isResume)
                    {
                        assignmentQuery = assignmentQuery.Where(x =>
                            x.AssignmentId == resumeAssignmentId);
                    }

                    var assignments = await assignmentQuery
                        .Select(x => x.AssignmentId)
                        .Take(2)
                        .ToListAsync();

                    if (assignments.Count == 0)
                    {
                        return Fail(
                            403,
                            isDefaultRemark
                                ? "No active role assignment exists for this employee in the JD's active template."
                                : "This skill is not assigned to the employee in the JD's active template.");
                    }

                    if (assignments.Count > 1)
                    {
                        return Fail(
                            409,
                            "Multiple matching assignments exist. AssignmentId is needed to identify the correct assignment.");
                    }

                    resolvedAssignmentId = assignments[0];
                }

                // Resume saves are employee-specific; normal saves retain the original lookup.
                // Resume uses stored skill 0; non-resume lookup is unchanged.
                var assessmentQuery = _context.CandidateSkillAssessments
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.JobDescriptionId == request.JobDescriptionId &&
                        x.CandidateApplicationId ==
                            application.CandidateApplicationId &&
                        (!isResume || x.CreatedBy == employee.EmployeeId) &&
                        x.CompanySkillMasterId == companySkillMasterId);

                CandidateSkillAssessmentHistory? historyToEdit = null;

                if (isHistoryEdit)
                {
                    historyToEdit = await _context.CandidateSkillAssessmentHistories
                        .FirstOrDefaultAsync(x =>
                            x.CandidateSkillAssessmentHistoryId ==
                                request.CandidateSkillAssessmentHistoryId &&
                            x.CompanyId == request.CompanyId &&
                            x.JobDescriptionId == request.JobDescriptionId &&
                            x.CandidateApplicationId ==
                                application.CandidateApplicationId &&
                            x.CompanySkillMasterId == companySkillMasterId &&
                            (!isResume || x.CreatedBy == employee.EmployeeId));

                    if (historyToEdit == null)
                    {
                        return Fail(
                            404,
                            "History record not found for this employee, candidate, job and skill/default remark.");
                    }

                    if (isResume &&
                        (historyToEdit.IsRemarkAdded ||
                         historyToEdit.Action != "Save" ||
                         historyToEdit.AssignmentId != resumeAssignmentId))
                    {
                        return Fail(409,
                            "Resume edits must target your non-remark Save history for the first hierarchy assignment.");
                    }

                    if (request.CandidateSkillAssessmentId > 0 &&
                        request.CandidateSkillAssessmentId !=
                            historyToEdit.CandidateSkillAssessmentId)
                    {
                        return Fail(
                            400,
                            "History record does not belong to the supplied assessment.");
                    }

                    assessmentQuery = assessmentQuery.Where(x =>
                        x.CandidateSkillAssessmentId ==
                            historyToEdit.CandidateSkillAssessmentId);
                }

                CandidateSkillAssessment? assessment;

                if (isHistoryEdit)
                {
                    assessment = await assessmentQuery.FirstOrDefaultAsync();

                    if (assessment == null)
                    {
                        return Fail(
                            404,
                            "Assessment not found for this history record and employee.");
                    }
                }
                else if (request.CandidateSkillAssessmentId > 0)
                {
                    assessment = await assessmentQuery
                        .FirstOrDefaultAsync(x =>
                            x.CandidateSkillAssessmentId ==
                                request.CandidateSkillAssessmentId);

                    if (assessment == null)
                    {
                        return Fail(
                            404,
                            "Assessment not found for this employee, candidate and skill/default remark.");
                    }
                }
                else
                {
                    var existingAssessments = await assessmentQuery
                        .Take(2)
                        .ToListAsync();

                    if (existingAssessments.Count > 1)
                    {
                        return Fail(
                            409,
                            "Multiple assessments exist. Send CandidateSkillAssessmentId.");
                    }

                    assessment = existingAssessments.FirstOrDefault();
                }

                bool isNew = assessment == null;

                // Preserve the existing Save-first behavior.
                if (isNew && action == "AddRemark")
                {
                    return Fail(
                        400,
                        "Save the assessment first, then add further remarks.");
                }

                // A new Default Remark must contain remark text.
                if (isNew &&
                    isDefaultRemark &&
                    !isResume &&
                    string.IsNullOrWhiteSpace(request.Remarks))
                {
                    return Fail(400, "Enter the default remark.");
                }

                if (!isNew)
                {
                    if (assessment!.IsActive != true)
                        return Fail(409, "Assessment is inactive.");

                    if ((!isAdmin || isResume) &&
                        assessment.AssignmentId != resolvedAssignmentId)
                    {
                        return Fail(
                            409,
                            "This assessment belongs to a different assignment.");
                    }

                    if (!isHistoryEdit &&
                        action == "Save" &&
                        (assessment.Status == "Approved" ||
                         assessment.Status == "Rejected"))
                    {
                        return Fail(
                            409,
                            "This assessment is completed. You can still use AddRemark.");
                    }
                }

                var now = DateTime.UtcNow;

                // Edit only the selected history row.
                if (isHistoryEdit)
                {
                    if ((!isAdmin || isResume) &&
                        historyToEdit!.AssignmentId != resolvedAssignmentId)
                    {
                        return Fail(
                            409,
                            "This history record belongs to a different assignment.");
                    }

                    if (action == "Save")
                    {
                        if (hasDetails)
                        {
                            historyToEdit!.DetailsJson =
                                request.Details!.Value.GetRawText();
                        }

                        if (request.Score.HasValue)
                        {
                            historyToEdit!.Score = request.Score.Value;
                        }
                    }

                    if (isRemarkAdded)
                    {
                        historyToEdit!.Remarks = request.Remarks!.Trim();
                        historyToEdit.IsRemarkAdded = true;
                    }

                    historyToEdit!.ModifiedBy = employee.EmployeeId;
                    historyToEdit.ModifiedOn = now;

                    // Preserve Action, status fields, creator and parent assessment.
                    await _context.SaveChangesAsync();

                    var historyRemarksCount =
                        await _context.CandidateSkillAssessmentHistories
                            .CountAsync(x =>
                                x.CompanyId == request.CompanyId &&
                                x.CandidateSkillAssessmentId ==
                                    assessment!.CandidateSkillAssessmentId &&
                                x.IsRemarkAdded);

                    using var historyDetailsDocument =
                        JsonDocument.Parse(historyToEdit.DetailsJson);

                    var historyDetails =
                        historyDetailsDocument.RootElement.Clone();

                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        Status = true,
                        Message = "Assessment history updated successfully.",

                        Data = new
                        {
                            historyToEdit.CandidateSkillAssessmentHistoryId,
                            historyToEdit.CandidateSkillAssessmentId,
                            historyToEdit.CompanyId,
                            request.CandidateId,
                            historyToEdit.JobDescriptionId,
                            historyToEdit.CandidateApplicationId,

                            SkillId = companySkillMasterId,
                            SkillName = skillName,
                            CategoryId = categoryId,
                            CategoryName = categoryName,
                            IsDefaultRemark = isDefaultRemark,
                            IsResume = isResume,

                            historyToEdit.SkillFormDefinitionId,
                            FormVersion = (int?)null,
                            historyToEdit.AssignmentId,

                            Details = historyDetails,
                            historyToEdit.Score,
                            historyToEdit.Remarks,
                            historyToEdit.Action,
                            historyToEdit.PreviousStatus,
                            historyToEdit.NewStatus,
                            historyToEdit.IsRemarkAdded,

                            AssessmentStatus = assessment!.Status,
                            RemarksCount = historyRemarksCount,

                            historyToEdit.CreatedBy,
                            CreatedByName = employee.EmployeeName,
                            historyToEdit.CreatedOn,

                            historyToEdit.ModifiedBy,
                            ModifiedByName = employee.EmployeeName,
                            historyToEdit.ModifiedOn
                        }
                    });
                }

                var previousStatus = assessment?.Status;

                if (isNew)
                {
                    assessment = new CandidateSkillAssessment
                    {
                        CompanyId = request.CompanyId,
                        JobDescriptionId = request.JobDescriptionId!.Value,
                        CandidateApplicationId =
                            application.CandidateApplicationId,

                        // Zero identifies the no-skill/default assessment.
                        CompanySkillMasterId = companySkillMasterId ?? 0,

                        // Preserved from your existing API.
                        SkillFormDefinitionId = 0,

                        AssignmentId = resolvedAssignmentId,
                        DetailsJson = "{}",
                        Status = "Pending",
                        IsActive = true,

                        CreatedBy = employee.EmployeeId,
                        CreatedOn = now
                    };

                    _context.CandidateSkillAssessments.Add(assessment);
                }

                if (action == "Save")
                {
                    if (hasDetails)
                    {
                        assessment!.DetailsJson =
                            request.Details!.Value.GetRawText();
                    }

                    if (request.Score.HasValue)
                    {
                        assessment!.Score = request.Score.Value;
                    }

                    assessment!.Status = "InProgress";
                }

                if (isRemarkAdded)
                {
                    assessment!.Remarks = request.Remarks!.Trim();
                }

                if (!isNew)
                {
                    assessment!.ModifiedBy = employee.EmployeeId;
                    assessment.ModifiedOn = now;
                }

                await _context.SaveChangesAsync();

                var history = new CandidateSkillAssessmentHistory
                {
                    CandidateSkillAssessmentId =
                        assessment!.CandidateSkillAssessmentId,

                    CompanyId = assessment.CompanyId,
                    JobDescriptionId = assessment.JobDescriptionId,
                    CandidateApplicationId =
                        assessment.CandidateApplicationId,

                    CompanySkillMasterId = assessment.CompanySkillMasterId,
                    SkillFormDefinitionId = assessment.SkillFormDefinitionId,
                    AssignmentId = assessment.AssignmentId,

                    Action = action,
                    PreviousStatus = previousStatus,
                    NewStatus = assessment.Status,

                    DetailsJson = assessment.DetailsJson,
                    Score = assessment.Score,
                    Remarks = assessment.Remarks,
                    IsRemarkAdded = isRemarkAdded,

                    CreatedBy = employee.EmployeeId,
                    CreatedOn = now
                };

                _context.CandidateSkillAssessmentHistories.Add(history);

                await _context.SaveChangesAsync();

                var remarksCount =
                    await _context.CandidateSkillAssessmentHistories
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.CandidateSkillAssessmentId ==
                                assessment.CandidateSkillAssessmentId &&
                            x.IsRemarkAdded);

                using var detailsDocument =
                    JsonDocument.Parse(assessment.DetailsJson);

                var responseDetails = detailsDocument.RootElement.Clone();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = action == "AddRemark"
                        ? "Remark added successfully."
                        : "Assessment saved successfully.",

                    Data = new
                    {
                        history.CandidateSkillAssessmentHistoryId,
                        assessment.CandidateSkillAssessmentId,
                        assessment.CompanyId,
                        request.CandidateId,
                        assessment.JobDescriptionId,
                        assessment.CandidateApplicationId,

                        SkillId = companySkillMasterId,
                        SkillName = skillName,
                        CategoryId = categoryId,
                        CategoryName = categoryName,
                        IsDefaultRemark = isDefaultRemark,
                        IsResume = isResume,

                        assessment.SkillFormDefinitionId,
                        FormVersion = (int?)null,
                        assessment.AssignmentId,

                        Details = responseDetails,
                        assessment.Score,
                        assessment.Remarks,
                        AssessmentStatus = assessment.Status,

                        RemarksCount = remarksCount,

                        assessment.CreatedBy,
                        CreatedByName = employee.EmployeeName,
                        assessment.CreatedOn,

                        assessment.ModifiedBy,
                        ModifiedByName = assessment.ModifiedBy.HasValue
                            ? employee.EmployeeName
                            : null,
                        assessment.ModifiedOn
                    }
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save skill assessment for CompanyId {CompanyId}, CandidateId {CandidateId}, JobDescriptionId {JobDescriptionId}, EmployeeId {EmployeeId}.",
                    request.CompanyId,
                    request.CandidateId,
                    request.JobDescriptionId,
                    request.EmployeeId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to save skill assessment."
                });
            }
        }









        [HttpPost("EditCandidateAssessmentRemark")]
        public async Task<IActionResult> EditCandidateAssessmentRemark(
    [FromBody] EditCandidateAssessmentRemarkRequest? request,
    [FromServices]
    Microsoft.Extensions.Configuration.IConfiguration configuration)
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
                request.EmployeeId <= 0 ||
                request.CandidateSkillAssessmentId <= 0 ||
                request.CandidateSkillAssessmentHistoryId <= 0)
            {
                return Fail(400, "Provide valid reference IDs.");
            }

            if (request.Remarks == null && !request.Score.HasValue)
            {
                return Fail(
                    400,
                    "Provide Remarks or Score to update.");
            }

            try
            {
                var employee = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.EmployeeId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsActive,
                        EmployeeName =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim()
                    })
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return Fail(
                        404,
                        "Employee not found for this company.");
                }

                if (employee.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var company = await _context.Companys
                    .AsNoTracking()
                    .Where(x => x.CompanyID == request.CompanyId)
                    .Select(x => new
                    {
                        x.CompanyID,
                        x.EpId
                    })
                    .FirstOrDefaultAsync();

                if (company == null)
                    return Fail(404, "Company not found.");

                var assessment = await _context.CandidateSkillAssessments
                    .AsTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateSkillAssessmentId ==
                            request.CandidateSkillAssessmentId);

                if (assessment == null)
                    return Fail(404, "Assessment not found.");

                if (assessment.IsActive != true)
                    return Fail(409, "Assessment is inactive.");

                // Only the original history author can edit this entry.
                var history = await _context.CandidateSkillAssessmentHistories
                    .AsTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateSkillAssessmentHistoryId ==
                            request.CandidateSkillAssessmentHistoryId &&
                        x.CandidateSkillAssessmentId ==
                            assessment.CandidateSkillAssessmentId &&
                        x.CreatedBy == employee.EmployeeId);

                if (history == null)
                {
                    return Fail(
                        404,
                        "History entry not found for this assessment and employee.");
                }

                if (history.CandidateApplicationId !=
                        assessment.CandidateApplicationId ||
                    history.JobDescriptionId !=
                        assessment.JobDescriptionId ||
                    history.CompanySkillMasterId !=
                        assessment.CompanySkillMasterId ||
                    history.AssignmentId != assessment.AssignmentId)
                {
                    return Fail(
                        409,
                        "History entry does not match the assessment context.");
                }

                var latestHistoryId = await _context
                    .CandidateSkillAssessmentHistories
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateSkillAssessmentId ==
                            assessment.CandidateSkillAssessmentId)
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x =>
                        x.CandidateSkillAssessmentHistoryId)
                    .Select(x => x.CandidateSkillAssessmentHistoryId)
                    .FirstOrDefaultAsync();

                bool isLatestHistory =
                    latestHistoryId ==
                        history.CandidateSkillAssessmentHistoryId;

                string? newRemarks = request.Remarks?.Trim();

                bool remarksChanged =
                    request.Remarks != null &&
                    !string.Equals(
                        history.Remarks,
                        newRemarks,
                        StringComparison.Ordinal);

                bool scoreChanged =
                    request.Score.HasValue &&
                    history.Score != request.Score.Value;

                bool hasChanges = remarksChanged || scoreChanged;

                var now = DateTime.UtcNow;
                var createdNotifications =
                    new List<RecruitmentNotification>();

                if (hasChanges)
                {
                    if (remarksChanged)
                    {
                        history.Remarks = newRemarks;
                        history.IsRemarkAdded =
                            !string.IsNullOrWhiteSpace(newRemarks);

                        if (isLatestHistory)
                            assessment.Remarks = newRemarks;
                    }

                    if (scoreChanged)
                    {
                        history.Score = request.Score!.Value;

                        if (isLatestHistory)
                            assessment.Score = request.Score.Value;
                    }

                    history.ModifiedBy = employee.EmployeeId;
                    history.ModifiedOn = now;

                    if (isLatestHistory)
                    {
                        assessment.ModifiedBy = employee.EmployeeId;
                        assessment.ModifiedOn = now;
                    }

                    // Use the recipients of the original decision.
                    // Do not recalculate or advance hierarchy routing.
                    var originalNotifications = await _context
                        .RecruitmentNotifications
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            x.CandidateSkillAssessmentHistoryId ==
                                history.CandidateSkillAssessmentHistoryId &&
                            x.CandidateApplicationId ==
                                history.CandidateApplicationId &&
                            x.JobDescriptionId ==
                                history.JobDescriptionId &&
                            x.FromUserId == employee.EmployeeId &&
                            x.Type == "Recruitment" &&
                            (
                                x.Action == "Approve" ||
                                x.Action == "Reject" ||
                                x.Action == "Return"
                            ))
                        .OrderBy(x => x.NotificationId)
                        .ToListAsync();

                    var recipients = originalNotifications
                        .Where(x => x.ToUserId != employee.EmployeeId)
                        .GroupBy(x => x.ToUserId)
                        .Select(group => group.First())
                        .ToList();

                    if (recipients.Count > 0)
                    {
                        var candidate = await (
                            from application in _context.CandidateApplications
                                .AsNoTracking()

                            join person in _context.Candidates.AsNoTracking()
                                on new
                                {
                                    application.CompanyId,
                                    application.CandidateId
                                }
                                equals new
                                {
                                    person.CompanyId,
                                    person.CandidateId
                                }

                            where application.CompanyId == request.CompanyId &&
                                  application.CandidateApplicationId ==
                                      assessment.CandidateApplicationId &&
                                  application.JobDescriptionId ==
                                      assessment.JobDescriptionId

                            select new
                            {
                                person.FirstName,
                                person.MiddleName,
                                person.LastName
                            }
                        ).FirstOrDefaultAsync();

                        string candidateName = candidate == null
                            ? $"application {assessment.CandidateApplicationId}"
                            : string.Join(
                                " ",
                                new[]
                                {
                            candidate.FirstName,
                            candidate.MiddleName,
                            candidate.LastName
                                }.Where(x => !string.IsNullOrWhiteSpace(x)));

                        if (string.IsNullOrWhiteSpace(candidateName))
                        {
                            candidateName =
                                $"application {assessment.CandidateApplicationId}";
                        }

                        var jobTitle = await _context.JobDescriptions
                            .AsNoTracking()
                            .Where(x =>
                                x.CompanyId == request.CompanyId &&
                                x.JobDescriptionId ==
                                    assessment.JobDescriptionId)
                            .Select(x => x.JobTitle)
                            .FirstOrDefaultAsync();

                        string remarkText =
                            string.IsNullOrWhiteSpace(history.Remarks)
                                ? "(empty)"
                                : history.Remarks;

                        string scoreText =
                            Convert.ToString(
                                history.Score,
                                System.Globalization.CultureInfo.InvariantCulture)
                            ?? "";

                        if (string.IsNullOrWhiteSpace(scoreText))
                            scoreText = "Not provided";

                        string message =
                            $"{employee.EmployeeName} updated the remark or score " +
                            $"for candidate {candidateName}, job {jobTitle ?? "Unknown"}. " +
                            $"Remark: {remarkText}. Score: {scoreText}.";

                        // A separate event key for each actual edit.
                  

                        foreach (var recipient in recipients)
                        {
                            var notification = new RecruitmentNotification
                            {
                                CompanyId = request.CompanyId,

                                FromUserId = employee.EmployeeId,
                                ToUserId = recipient.ToUserId,

                                CandidateApplicationId =
                                    assessment.CandidateApplicationId,

                                JobDescriptionId =
                                    assessment.JobDescriptionId,

                                // Preserve the original notification's context.
                                TemplateId = recipient.TemplateId,
                                FromAssignmentId = recipient.FromAssignmentId,
                                ToAssignmentId = recipient.ToAssignmentId,

                                CompanySkillMasterId =
                                    assessment.CompanySkillMasterId,

                                CandidateSkillAssessmentHistoryId =
                                    history.CandidateSkillAssessmentHistoryId,

                          
                                Action = "RemarkUpdated",
                                Type = "Recruitment",
                                Title = "Remark updated",
                                Message = message,

                                IsRead = false,
                                CreatedOn = now,

                                // Informational only: no approval routing.
                                IsActionRequest = false,
                                ResumeAfterAssignmentId = null,
                                ActionedByHistoryId = null,

                                DeliveryStatus = "NotRequired",

                                EPMirrorStatus = company.EpId > 0
                                    ? "Pending"
                                    : "NotRequired"
                            };

                            _context.RecruitmentNotifications.Add(notification);
                            createdNotifications.Add(notification);
                        }
                    }

                    // Edit and Recruitment notifications save together.
                    await _context.SaveChangesAsync();
                }

                var remarksCount = await _context
                    .CandidateSkillAssessmentHistories
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateSkillAssessmentId ==
                            assessment.CandidateSkillAssessmentId &&
                        x.IsRemarkAdded);

                var notificationIds = createdNotifications
                    .Select(x => x.NotificationId)
                    .ToList();

                await transaction.CommitAsync();

                // EP uses the existing helper:
                // Recruitment employee IDs -> EmployeeMasters.EPID.
                // EP failure must not undo a committed edit.
                if (notificationIds.Count > 0 && company.EpId > 0)
                {
                    try
                    {
                        await MirrorDecisionNotificationsToEPAsync(
                            request.CompanyId,
                            notificationIds,
                            configuration);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(
                            ex,
                            "EP notification processing failed after remark edit for CompanyId {CompanyId}, HistoryId {HistoryId}.",
                            request.CompanyId,
                            history.CandidateSkillAssessmentHistoryId);
                    }
                }

                return Ok(new
                {
                    Status = true,
                    Message = hasChanges
                        ? "Assessment remark updated successfully."
                        : "No changes were found.",

                    NotificationCount = notificationIds.Count,
                    NotificationIds = notificationIds,

                    Data = new
                    {
                        history.CandidateSkillAssessmentHistoryId,
                        history.CandidateSkillAssessmentId,
                        history.CompanyId,
                        history.CandidateApplicationId,
                        history.JobDescriptionId,

                        history.CompanySkillMasterId,
                        IsDefaultRemark =
                            history.CompanySkillMasterId == 0,

                        history.AssignmentId,

                        history.Remarks,
                        history.Score,
                        history.IsRemarkAdded,

                        // These decision fields are unchanged.
                        history.Action,
                        history.PreviousStatus,
                        history.NewStatus,

                        history.CreatedBy,
                        CreatedByName = employee.EmployeeName,
                        history.CreatedOn,

                        history.ModifiedBy,
                        history.ModifiedOn,

                        MainAssessmentUpdated =
                            hasChanges && isLatestHistory,

                        AssessmentStatus = assessment.Status,
                        CurrentRemarks = assessment.Remarks,
                        CurrentScore = assessment.Score,

                        RemarksCount = remarksCount
                    }
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to edit assessment remark for CompanyId {CompanyId}, AssessmentId {AssessmentId}, HistoryId {HistoryId}, EmployeeId {EmployeeId}.",
                    request.CompanyId,
                    request.CandidateSkillAssessmentId,
                    request.CandidateSkillAssessmentHistoryId,
                    request.EmployeeId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to edit assessment remark."
                });
            }
        }


        public class EditCandidateAssessmentRemarkRequest
        {
            public int CompanyId { get; set; }
            public int EmployeeId { get; set; }

            public int CandidateSkillAssessmentId { get; set; }
            public int CandidateSkillAssessmentHistoryId { get; set; }

            // null = unchanged; "" = clear remark.
            public string? Remarks { get; set; }

            // null = unchanged.
            public decimal? Score { get; set; }
        }




        //    [HttpPost("ApproveRejectSkillAssessment")]
        //    public async Task<IActionResult> ApproveRejectSkillAssessment(
        //[FromBody] UpdateSkillAssessmentStatusRequest? request)
        //    {
        //        IActionResult Fail(int code, string message)
        //        {
        //            return StatusCode(code, new
        //            {
        //                Status = false,
        //                Message = message
        //            });
        //        }

        //        if (request == null ||
        //            request.CompanyId <= 0 ||
        //            request.CandidateId <= 0 ||
        //            request.JobDescriptionId <= 0 ||
        //            request.CompanySkillMasterId.GetValueOrDefault() < 0 ||
        //            request.EmployeeId <= 0 ||
        //            request.CandidateSkillAssessmentId < 0 ||
        //            request.CandidateSkillAssessmentHistoryId < 0)
        //        {
        //            return Fail(400, "Provide valid reference IDs.");
        //        }

        //        var action = request.Action?.Trim().ToLowerInvariant() switch
        //        {
        //            "approve" => "Approve",
        //            "reject" => "Reject",
        //            _ => null
        //        };

        //        if (action == null)
        //            return Fail(400, "Action must be Approve or Reject.");

        //        bool isHistoryEdit =
        //            request.CandidateSkillAssessmentHistoryId > 0;

        //        bool isDefaultRemark =
        //            request.CompanySkillMasterId.GetValueOrDefault() == 0;

        //        int? companySkillMasterId = isDefaultRemark
        //            ? (int?)null
        //            : request.CompanySkillMasterId.Value;

        //        try
        //        {
        //            await using var transaction =
        //                await _context.Database.BeginTransactionAsync(
        //                    System.Data.IsolationLevel.Serializable);

        //            var employee = await _context.EmployeeMasters
        //                .AsNoTracking()
        //                .FirstOrDefaultAsync(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.EmployeeId == request.EmployeeId);

        //            if (employee == null)
        //                return Fail(404, "Employee not found for this company.");

        //            if (employee.IsActive != true)
        //                return Fail(403, "Employee is inactive.");

        //            bool isAdmin = employee.IsAdmin == true;

        //            var employeeName =
        //                ((employee.FirstName ?? "") + " " +
        //                 (employee.LastName ?? "")).Trim();

        //            var candidateExists = await _context.Candidates
        //                .AsNoTracking()
        //                .AnyAsync(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateId == request.CandidateId);

        //            if (!candidateExists)
        //                return Fail(404, "Candidate not found for this company.");

        //            var job = await _context.JobDescriptions
        //                .AsNoTracking()
        //                .FirstOrDefaultAsync(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.JobDescriptionId == request.JobDescriptionId);

        //            if (job == null)
        //                return Fail(404, "Job description not found for this company.");

        //            var applications = await _context.CandidateApplications
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateId == request.CandidateId &&
        //                    x.JobDescriptionId == request.JobDescriptionId)
        //                .Take(2)
        //                .ToListAsync();

        //            if (applications.Count == 0)
        //                return Fail(404, "Candidate application not found for this job.");

        //            if (applications.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    "Multiple applications exist for this candidate and job. CandidateApplicationId is needed.");
        //            }

        //            var application = applications[0];

        //            if (application.IsActive != true)
        //                return Fail(409, "Candidate application is inactive.");

        //            string? skillName = "Default Remark";
        //            int? categoryId = null;
        //            string? categoryName = null;

        //            // A positive skill ID must remain valid.
        //            // Invalid skills do not fall back to Default Remark.
        //            if (!isDefaultRemark)
        //            {
        //                var skill = await _context.Set<CompanySkillMaster>()
        //                    .AsNoTracking()
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.CompanySkillMasterId == companySkillMasterId &&
        //                        x.IsActive == true);

        //                if (skill == null)
        //                    return Fail(409, "The company skill is unavailable.");

        //                var category = await _context.Set<CompanySkillMapping>()
        //                    .AsNoTracking()
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.SkillMappingId == skill.SkillMappingId &&
        //                        x.IsActive == true);

        //                if (category == null)
        //                    return Fail(409, "The skill category is unavailable.");

        //                skillName = skill.SkillName;
        //                categoryId = category.SkillMappingId;
        //                categoryName = category.CategoryName;
        //            }

        //            CandidateSkillAssessmentHistory? historyToEdit = null;

        //            // Preserve the existing skill-based lookup.
        //            var assessmentQuery = _context.CandidateSkillAssessments
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CompanySkillMasterId == companySkillMasterId &&
        //                    x.JobDescriptionId == request.JobDescriptionId &&
        //                    x.CandidateApplicationId ==
        //                        application.CandidateApplicationId);

        //            // Default assessments belong to the employee who created them.
        //            // This keeps hierarchy members' decisions separate.
        //            if (isDefaultRemark)
        //            {
        //                assessmentQuery = assessmentQuery.Where(x =>
        //                    x.CreatedBy == employee.EmployeeId);
        //            }

        //            CandidateSkillAssessment? assessment;

        //            if (isHistoryEdit)
        //            {
        //                // Edit only the employee's exact history record.
        //                historyToEdit =
        //                    await _context.CandidateSkillAssessmentHistories
        //                        .FirstOrDefaultAsync(x =>
        //                            x.CandidateSkillAssessmentHistoryId ==
        //                                request.CandidateSkillAssessmentHistoryId &&
        //                            x.CompanyId == request.CompanyId &&
        //                            x.CompanySkillMasterId == companySkillMasterId &&
        //                            x.JobDescriptionId == request.JobDescriptionId &&
        //                            x.CandidateApplicationId ==
        //                                application.CandidateApplicationId &&
        //                            x.CreatedBy == employee.EmployeeId);

        //                if (historyToEdit == null)
        //                {
        //                    return Fail(
        //                        404,
        //                        "History record not found for this employee, candidate, job and skill/default remark.");
        //                }

        //                if (historyToEdit.Action != "Approve" &&
        //                    historyToEdit.Action != "Reject")
        //                {
        //                    return Fail(
        //                        400,
        //                        "Use this API to edit approval/rejection history only.");
        //                }

        //                if (request.CandidateSkillAssessmentId > 0 &&
        //                    request.CandidateSkillAssessmentId !=
        //                        historyToEdit.CandidateSkillAssessmentId)
        //                {
        //                    return Fail(
        //                        400,
        //                        "History record does not belong to the supplied assessment.");
        //                }

        //                assessment = await assessmentQuery
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CandidateSkillAssessmentId ==
        //                            historyToEdit.CandidateSkillAssessmentId);

        //                if (assessment == null)
        //                {
        //                    return Fail(
        //                        404,
        //                        "Assessment not found for this history record.");
        //                }
        //            }
        //            else
        //            {
        //                assessment = null;

        //                if (request.CandidateSkillAssessmentId > 0)
        //                {
        //                    assessment = await assessmentQuery
        //                        .FirstOrDefaultAsync(x =>
        //                            x.CandidateSkillAssessmentId ==
        //                                request.CandidateSkillAssessmentId);
        //                }

        //                // Preserve fallback lookup and creation.
        //                // For Default Remarks this lookup is employee-specific.
        //                if (assessment == null)
        //                {
        //                    var matchingAssessments = await assessmentQuery
        //                        .Take(2)
        //                        .ToListAsync();

        //                    if (matchingAssessments.Count > 1)
        //                    {
        //                        return Fail(
        //                            409,
        //                            "Multiple matching assessments exist. Send the matching CandidateSkillAssessmentId.");
        //                    }

        //                    assessment = matchingAssessments.FirstOrDefault();
        //                }
        //            }

        //            bool isNew = assessment == null;

        //            if (!isNew && assessment!.IsActive != true)
        //                return Fail(409, "Skill assessment is inactive.");

        //            int? resolvedAssignmentId = assessment?.AssignmentId;

        //            if (!isAdmin)
        //            {
        //                var assignmentQuery = _context.RoleTemplateAssignment
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EmployeeId == employee.EmployeeId &&
        //                        x.TemplateId == job.TemplateId &&
        //                        x.IsActive == true)
        //                    .Where(x =>
        //                        _context.Set<RoleTemplate>().Any(template =>
        //                            template.CompanyId == x.CompanyId &&
        //                            template.TemplateId == x.TemplateId &&
        //                            template.IsActive == true));

        //                // Default remarks skip only the skill-mapping requirement.
        //                if (!isDefaultRemark)
        //                {
        //                    assignmentQuery = assignmentQuery.Where(x =>
        //                        _context.Set<RoleTemplateAssignmentSkill>().Any(mapping =>
        //                            mapping.CompanyId == x.CompanyId &&
        //                            mapping.AssignmentId == x.AssignmentId &&
        //                            mapping.SkillId == companySkillMasterId &&
        //                            mapping.IsActive == true));
        //                }

        //                if (!isNew)
        //                {
        //                    if (!assessment!.AssignmentId.HasValue)
        //                    {
        //                        return Fail(
        //                            403,
        //                            "No role assignment is linked to this assessment.");
        //                    }

        //                    int existingAssignmentId = assessment.AssignmentId.Value;

        //                    assignmentQuery = assignmentQuery.Where(x =>
        //                        x.AssignmentId == existingAssignmentId);
        //                }

        //                var assignments = await assignmentQuery
        //                    .Take(2)
        //                    .ToListAsync();

        //                if (assignments.Count == 0)
        //                {
        //                    return Fail(
        //                        403,
        //                        isDefaultRemark
        //                            ? "An active assignment for this employee and job template was not found."
        //                            : "An active assignment for this employee, job template and skill was not found.");
        //                }

        //                if (assignments.Count > 1)
        //                {
        //                    return Fail(
        //                        409,
        //                        "Multiple matching assignments exist. AssignmentId is needed.");
        //                }

        //                var assignment = assignments[0];
        //                resolvedAssignmentId = assignment.AssignmentId;

        //                if (isHistoryEdit &&
        //                    historyToEdit!.AssignmentId != resolvedAssignmentId)
        //                {
        //                    return Fail(
        //                        403,
        //                        "The history record belongs to a different assignment.");
        //                }

        //                if (action == "Approve" && assignment.CanApprove != true)
        //                    return Fail(403, "Approve permission is not available.");

        //                if (action == "Reject" && assignment.CanReject != true)
        //                    return Fail(403, "Reject permission is not available.");
        //            }

        //            var now = DateTime.UtcNow;

        //            string newStatus = action == "Approve"
        //                ? "Approved"
        //                : "Rejected";

        //            bool isRemarkAdded =
        //                !string.IsNullOrWhiteSpace(request.Remarks);

        //            if (isHistoryEdit)
        //            {
        //                historyToEdit!.Action = action;
        //                historyToEdit.NewStatus = newStatus;

        //                if (request.Score.HasValue)
        //                    historyToEdit.Score = request.Score.Value;

        //                if (isRemarkAdded)
        //                {
        //                    historyToEdit.Remarks = request.Remarks!.Trim();
        //                    historyToEdit.IsRemarkAdded = true;
        //                }

        //                historyToEdit.ModifiedBy = employee.EmployeeId;
        //                historyToEdit.ModifiedOn = now;

        //                // Preserve CreatedBy, CreatedOn, PreviousStatus,
        //                // DetailsJson and the parent assessment.
        //                await _context.SaveChangesAsync();

        //                var historyRemarksCount =
        //                    await _context.CandidateSkillAssessmentHistories
        //                        .CountAsync(x =>
        //                            x.CompanyId == request.CompanyId &&
        //                            x.CandidateSkillAssessmentId ==
        //                                assessment!.CandidateSkillAssessmentId &&
        //                            x.IsRemarkAdded);

        //                await transaction.CommitAsync();

        //                return Ok(new
        //                {
        //                    Status = true,
        //                    Message = "Approval/rejection history updated successfully.",
        //                    Data = new
        //                    {
        //                        historyToEdit.CandidateSkillAssessmentHistoryId,
        //                        historyToEdit.CandidateSkillAssessmentId,
        //                        historyToEdit.CompanyId,
        //                        application.CandidateId,
        //                        historyToEdit.CandidateApplicationId,
        //                        historyToEdit.JobDescriptionId,
        //                        historyToEdit.CompanySkillMasterId,

        //                        SkillId = companySkillMasterId,
        //                        SkillName = skillName,
        //                        CategoryId = categoryId,
        //                        CategoryName = categoryName,
        //                        IsDefaultRemark = isDefaultRemark,
        //                        historyToEdit.AssignmentId,

        //                        historyToEdit.Action,
        //                        historyToEdit.PreviousStatus,
        //                        historyToEdit.NewStatus,
        //                        historyToEdit.Score,
        //                        historyToEdit.Remarks,
        //                        historyToEdit.IsRemarkAdded,

        //                        AssessmentStatus = assessment!.Status,
        //                        RemarksCount = historyRemarksCount,

        //                        historyToEdit.CreatedBy,
        //                        CreatedByName = employeeName,
        //                        historyToEdit.CreatedOn,

        //                        historyToEdit.ModifiedBy,
        //                        ModifiedByName = employeeName,
        //                        historyToEdit.ModifiedOn
        //                    }
        //                });
        //            }

        //            // Preserve existing status restrictions.
        //            if (!isNew &&
        //                assessment!.Status != "InProgress" &&
        //                assessment.Status != "Approved" &&
        //                assessment.Status != "Rejected")
        //            {
        //                return Fail(
        //                    409,
        //                    "Save the assessment before approving or rejecting it.");
        //            }

        //            var previousStatus = assessment?.Status;

        //            if (isNew)
        //            {
        //                assessment = new CandidateSkillAssessment
        //                {
        //                    CompanyId = request.CompanyId,
        //                    CandidateApplicationId =
        //                        application.CandidateApplicationId,
        //                    JobDescriptionId = request.JobDescriptionId,

        //                    // Null means Default Remark.
        //                    CompanySkillMasterId = companySkillMasterId??0,

        //                    SkillFormDefinitionId = 0,

        //                    AssignmentId = resolvedAssignmentId,
        //                    DetailsJson = "{}",
        //                    Status = newStatus,
        //                    IsActive = true,

        //                    CreatedBy = employee.EmployeeId,
        //                    CreatedOn = now
        //                };

        //                _context.CandidateSkillAssessments.Add(assessment);
        //            }

        //            assessment!.Status = newStatus;

        //            if (request.Score.HasValue)
        //                assessment.Score = request.Score.Value;

        //            if (isRemarkAdded)
        //                assessment.Remarks = request.Remarks!.Trim();

        //            if (!isNew)
        //            {
        //                assessment.ModifiedBy = employee.EmployeeId;
        //                assessment.ModifiedOn = now;
        //            }

        //            await _context.SaveChangesAsync();

        //            var history = new CandidateSkillAssessmentHistory
        //            {
        //                CandidateSkillAssessmentId =
        //                    assessment.CandidateSkillAssessmentId,

        //                CompanyId = assessment.CompanyId,
        //                CandidateApplicationId = assessment.CandidateApplicationId,
        //                JobDescriptionId = assessment.JobDescriptionId,
        //                CompanySkillMasterId = assessment.CompanySkillMasterId,
        //                SkillFormDefinitionId = assessment.SkillFormDefinitionId,
        //                AssignmentId = assessment.AssignmentId,

        //                Action = action,
        //                PreviousStatus = previousStatus,
        //                NewStatus = assessment.Status,

        //                DetailsJson = assessment.DetailsJson,
        //                Score = assessment.Score,
        //                Remarks = assessment.Remarks,
        //                IsRemarkAdded = isRemarkAdded,

        //                // The history creator identifies who made this decision.
        //                CreatedBy = employee.EmployeeId,
        //                CreatedOn = now
        //            };

        //            _context.CandidateSkillAssessmentHistories.Add(history);

        //            await _context.SaveChangesAsync();

        //            var remarksCount =
        //                await _context.CandidateSkillAssessmentHistories
        //                    .CountAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.CandidateSkillAssessmentId ==
        //                            assessment.CandidateSkillAssessmentId &&
        //                        x.IsRemarkAdded);

        //            string? assessmentCreatorName = null;

        //            if (assessment.CreatedBy == employee.EmployeeId)
        //            {
        //                assessmentCreatorName = employeeName;
        //            }
        //            else if (assessment.CreatedBy.HasValue)
        //            {
        //                assessmentCreatorName = await _context.EmployeeMasters
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EmployeeId == assessment.CreatedBy.Value)
        //                    .Select(x =>
        //                        ((x.FirstName ?? "") + " " +
        //                         (x.LastName ?? "")).Trim())
        //                    .FirstOrDefaultAsync();
        //            }

        //            await transaction.CommitAsync();

        //            return Ok(new
        //            {
        //                Status = true,
        //                Message = isNew
        //                    ? "Assessment and decision created successfully."
        //                    : "Assessment decision saved successfully.",

        //                Data = new
        //                {
        //                    history.CandidateSkillAssessmentHistoryId,
        //                    assessment.CandidateSkillAssessmentId,
        //                    assessment.CompanyId,
        //                    application.CandidateId,
        //                    assessment.CandidateApplicationId,
        //                    assessment.JobDescriptionId,
        //                    assessment.CompanySkillMasterId,

        //                    SkillId = companySkillMasterId,
        //                    SkillName = skillName,
        //                    CategoryId = categoryId,
        //                    CategoryName = categoryName,
        //                    IsDefaultRemark = isDefaultRemark,
        //                    assessment.AssignmentId,

        //                    history.Action,
        //                    PreviousStatus = previousStatus,
        //                    AssessmentStatus = assessment.Status,
        //                    assessment.Score,
        //                    assessment.Remarks,
        //                    RemarksCount = remarksCount,

        //                    // Preserve existing assessment audit response fields.
        //                    assessment.CreatedBy,
        //                    CreatedByName = assessmentCreatorName,
        //                    assessment.CreatedOn,

        //                    assessment.ModifiedBy,
        //                    ModifiedByName = assessment.ModifiedBy.HasValue
        //                        ? employeeName
        //                        : null,
        //                    assessment.ModifiedOn,

        //                    // Explicitly identify this decision's author.
        //                    HistoryCreatedBy = history.CreatedBy,
        //                    HistoryCreatedByName = employeeName,
        //                    HistoryCreatedOn = history.CreatedOn
        //                }
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            Serilog.Log.Error(
        //                ex,
        //                "Failed to save assessment decision for CompanyId {CompanyId}, AssessmentId {AssessmentId}, HistoryId {HistoryId}, EmployeeId {EmployeeId}.",
        //                request.CompanyId,
        //                request.CandidateSkillAssessmentId,
        //                request.CandidateSkillAssessmentHistoryId,
        //                request.EmployeeId);

        //            return StatusCode(500, new
        //            {
        //                Status = false,
        //                Message = "Unable to save skill assessment decision."
        //            });
        //        }
        //    }






        //[HttpPost("ApproveRejectSkillAssessment")]
        //public async Task<IActionResult> ApproveRejectSkillAssessment(
        //[FromBody] UpdateSkillAssessmentStatusRequest? request)
        //{
        //    IActionResult Fail(int code, string message)
        //    {
        //        return StatusCode(code, new
        //        {
        //            Status = false,
        //            Message = message
        //        });
        //    }

        //    if (request == null ||
        //        request.CompanyId <= 0 ||
        //        request.CandidateId <= 0 ||
        //        request.JobDescriptionId <= 0 ||
        //        request.CompanySkillMasterId <= 0 ||
        //        request.EmployeeId <= 0 ||
        //        request.CandidateSkillAssessmentId < 0 ||
        //        request.CandidateSkillAssessmentHistoryId < 0)
        //    {
        //        return Fail(400, "Provide valid reference IDs.");
        //    }

        //    var action = request.Action?.Trim().ToLowerInvariant() switch
        //    {
        //        "approve" => "Approve",
        //        "reject" => "Reject",
        //        _ => null
        //    };

        //    if (action == null)
        //        return Fail(400, "Action must be Approve or Reject.");

        //    bool isHistoryEdit =
        //        request.CandidateSkillAssessmentHistoryId > 0;

        //    try
        //    {
        //        await using var transaction =
        //            await _context.Database.BeginTransactionAsync(
        //                System.Data.IsolationLevel.Serializable);

        //        var employee = await _context.EmployeeMasters
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.EmployeeId == request.EmployeeId);

        //        if (employee == null)
        //            return Fail(404, "Employee not found for this company.");

        //        if (employee.IsActive != true)
        //            return Fail(403, "Employee is inactive.");

        //        bool isAdmin = employee.IsAdmin == true;

        //        var employeeName =
        //            ((employee.FirstName ?? "") + " " +
        //             (employee.LastName ?? "")).Trim();

        //        var candidateExists = await _context.Candidates
        //            .AsNoTracking()
        //            .AnyAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CandidateId == request.CandidateId);

        //        if (!candidateExists)
        //            return Fail(404, "Candidate not found for this company.");

        //        var job = await _context.JobDescriptions
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.JobDescriptionId == request.JobDescriptionId);

        //        if (job == null)
        //            return Fail(404, "Job description not found for this company.");

        //        var applications = await _context.CandidateApplications
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CandidateId == request.CandidateId &&
        //                x.JobDescriptionId == request.JobDescriptionId)
        //            .Take(2)
        //            .ToListAsync();

        //        if (applications.Count == 0)
        //            return Fail(404, "Candidate application not found for this job.");

        //        if (applications.Count > 1)
        //        {
        //            return Fail(
        //                409,
        //                "Multiple applications exist for this candidate and job. CandidateApplicationId is needed.");
        //        }

        //        var application = applications[0];

        //        if (application.IsActive != true)
        //            return Fail(409, "Candidate application is inactive.");

        //        var skill = await _context.Set<CompanySkillMaster>()
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CompanySkillMasterId == request.CompanySkillMasterId &&
        //                x.IsActive == true);

        //        if (skill == null)
        //            return Fail(409, "The company skill is unavailable.");

        //        var category = await _context.Set<CompanySkillMapping>()
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.SkillMappingId == skill.SkillMappingId &&
        //                x.IsActive == true);

        //        if (category == null)
        //            return Fail(409, "The skill category is unavailable.");

        //        CandidateSkillAssessmentHistory? historyToEdit = null;

        //        // Preserve your current lookup without a CreatedBy filter.
        //        var assessmentQuery = _context.CandidateSkillAssessments
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CompanySkillMasterId == request.CompanySkillMasterId &&
        //                x.JobDescriptionId == request.JobDescriptionId &&
        //                x.CandidateApplicationId ==
        //                    application.CandidateApplicationId);

        //        CandidateSkillAssessment? assessment;

        //        if (isHistoryEdit)
        //        {
        //            // A history edit must match the exact requested record.
        //            // Never create another record when a history ID is supplied.
        //            historyToEdit =
        //                await _context.CandidateSkillAssessmentHistories
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CandidateSkillAssessmentHistoryId ==
        //                            request.CandidateSkillAssessmentHistoryId &&
        //                        x.CompanyId == request.CompanyId &&
        //                        x.CompanySkillMasterId ==
        //                            request.CompanySkillMasterId &&
        //                        x.JobDescriptionId == request.JobDescriptionId &&
        //                        x.CandidateApplicationId ==
        //                            application.CandidateApplicationId &&
        //                        x.CreatedBy == employee.EmployeeId);

        //            if (historyToEdit == null)
        //            {
        //                return Fail(
        //                    404,
        //                    "History record not found for this employee, candidate, job and skill.");
        //            }

        //            if (historyToEdit.Action != "Approve" &&
        //                historyToEdit.Action != "Reject")
        //            {
        //                return Fail(
        //                    400,
        //                    "Use this API to edit approval/rejection history only.");
        //            }

        //            if (request.CandidateSkillAssessmentId > 0 &&
        //                request.CandidateSkillAssessmentId !=
        //                    historyToEdit.CandidateSkillAssessmentId)
        //            {
        //                return Fail(
        //                    400,
        //                    "History record does not belong to the supplied assessment.");
        //            }

        //            assessment = await assessmentQuery
        //                .FirstOrDefaultAsync(x =>
        //                    x.CandidateSkillAssessmentId ==
        //                        historyToEdit.CandidateSkillAssessmentId);

        //            if (assessment == null)
        //            {
        //                return Fail(
        //                    404,
        //                    "Assessment not found for this history record.");
        //            }
        //        }
        //        else
        //        {
        //            assessment = null;

        //            // First try the supplied assessment ID.
        //            // The ID must also match company, job, application and skill.
        //            if (request.CandidateSkillAssessmentId > 0)
        //            {
        //                assessment = await assessmentQuery
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CandidateSkillAssessmentId ==
        //                            request.CandidateSkillAssessmentId);
        //            }

        //            // New fallback: find the assessment for the requested skill
        //            // when the supplied ID does not match.
        //            if (assessment == null)
        //            {
        //                var matchingAssessments = await assessmentQuery
        //                    .Take(2)
        //                    .ToListAsync();

        //                if (matchingAssessments.Count > 1)
        //                {
        //                    return Fail(
        //                        409,
        //                        "Multiple assessments exist for this candidate, job and company skill. Send the matching CandidateSkillAssessmentId.");
        //                }

        //                assessment = matchingAssessments.FirstOrDefault();
        //            }

        //            // If still null, create the assessment below.
        //        }

        //        bool isNew = assessment == null;

        //        if (!isNew && assessment!.IsActive != true)
        //            return Fail(409, "Skill assessment is inactive.");

        //        int? resolvedAssignmentId = assessment?.AssignmentId;

        //        if (!isAdmin)
        //        {
        //            var assignmentQuery = _context.RoleTemplateAssignment
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.EmployeeId == employee.EmployeeId &&
        //                    x.TemplateId == job.TemplateId &&
        //                    x.IsActive == true)
        //                .Where(x =>
        //                    _context.Set<RoleTemplate>().Any(template =>
        //                        template.CompanyId == x.CompanyId &&
        //                        template.TemplateId == x.TemplateId &&
        //                        template.IsActive == true))
        //                .Where(x =>
        //                    _context.Set<RoleTemplateAssignmentSkill>().Any(mapping =>
        //                        mapping.CompanyId == x.CompanyId &&
        //                        mapping.AssignmentId == x.AssignmentId &&
        //                        mapping.SkillId == skill.CompanySkillMasterId));

        //            if (!isNew)
        //            {
        //                if (!assessment!.AssignmentId.HasValue)
        //                {
        //                    return Fail(
        //                        403,
        //                        "No role assignment is linked to this assessment.");
        //                }

        //                int existingAssignmentId = assessment.AssignmentId.Value;

        //                assignmentQuery = assignmentQuery.Where(x =>
        //                    x.AssignmentId == existingAssignmentId);
        //            }

        //            var assignments = await assignmentQuery
        //                .Take(2)
        //                .ToListAsync();

        //            if (assignments.Count == 0)
        //            {
        //                return Fail(
        //                    403,
        //                    "An active assignment for this employee, job template and skill was not found.");
        //            }

        //            if (assignments.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    "Multiple matching assignments exist. AssignmentId is needed.");
        //            }

        //            var assignment = assignments[0];
        //            resolvedAssignmentId = assignment.AssignmentId;

        //            if (isHistoryEdit &&
        //                historyToEdit!.AssignmentId != resolvedAssignmentId)
        //            {
        //                return Fail(
        //                    403,
        //                    "The history record belongs to a different assignment.");
        //            }

        //            if (action == "Approve" && assignment.CanApprove != true)
        //                return Fail(403, "Approve permission is not available.");

        //            if (action == "Reject" && assignment.CanReject != true)
        //                return Fail(403, "Reject permission is not available.");
        //        }

        //        var now = DateTime.UtcNow;

        //        string newStatus = action == "Approve"
        //            ? "Approved"
        //            : "Rejected";

        //        bool isRemarkAdded =
        //            !string.IsNullOrWhiteSpace(request.Remarks);

        //        // Edit only the selected history row.
        //        if (isHistoryEdit)
        //        {
        //            historyToEdit!.Action = action;
        //            historyToEdit.NewStatus = newStatus;

        //            if (request.Score.HasValue)
        //                historyToEdit.Score = request.Score.Value;

        //            if (isRemarkAdded)
        //            {
        //                historyToEdit.Remarks = request.Remarks!.Trim();
        //                historyToEdit.IsRemarkAdded = true;
        //            }

        //            historyToEdit.ModifiedBy = employee.EmployeeId;
        //            historyToEdit.ModifiedOn = now;

        //            // Preserve history creation fields, PreviousStatus,
        //            // DetailsJson, and the main assessment.
        //            await _context.SaveChangesAsync();

        //            var historyRemarksCount =
        //                await _context.CandidateSkillAssessmentHistories
        //                    .CountAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.CandidateSkillAssessmentId ==
        //                            assessment!.CandidateSkillAssessmentId &&
        //                        x.IsRemarkAdded);

        //            await transaction.CommitAsync();

        //            return Ok(new
        //            {
        //                Status = true,
        //                Message = "Approval/rejection history updated successfully.",
        //                Data = new
        //                {
        //                    historyToEdit.CandidateSkillAssessmentHistoryId,
        //                    historyToEdit.CandidateSkillAssessmentId,
        //                    historyToEdit.CompanyId,
        //                    application.CandidateId,
        //                    historyToEdit.CandidateApplicationId,
        //                    historyToEdit.JobDescriptionId,
        //                    historyToEdit.CompanySkillMasterId,

        //                    SkillId = skill.CompanySkillMasterId,
        //                    skill.SkillName,
        //                    CategoryId = category.SkillMappingId,
        //                    category.CategoryName,

        //                    historyToEdit.Action,
        //                    historyToEdit.PreviousStatus,
        //                    historyToEdit.NewStatus,
        //                    historyToEdit.Score,
        //                    historyToEdit.Remarks,

        //                    AssessmentStatus = assessment!.Status,
        //                    RemarksCount = historyRemarksCount,

        //                    historyToEdit.CreatedBy,
        //                    CreatedByName = employeeName,
        //                    historyToEdit.CreatedOn,

        //                    historyToEdit.ModifiedBy,
        //                    ModifiedByName = employeeName,
        //                    historyToEdit.ModifiedOn
        //                }
        //            });
        //        }

        //        // Preserve existing status restrictions.
        //        if (!isNew &&
        //            assessment!.Status != "InProgress" &&
        //            assessment.Status != "Approved" &&
        //            assessment.Status != "Rejected")
        //        {
        //            return Fail(
        //                409,
        //                "Save the assessment before approving or rejecting it.");
        //        }

        //        var previousStatus = assessment?.Status;

        //        if (isNew)
        //        {
        //            assessment = new CandidateSkillAssessment
        //            {
        //                CompanyId = request.CompanyId,
        //                CandidateApplicationId =
        //                    application.CandidateApplicationId,
        //                JobDescriptionId = request.JobDescriptionId,
        //                CompanySkillMasterId = request.CompanySkillMasterId,

        //                // Preserved from your current implementation.
        //                SkillFormDefinitionId = 0,

        //                AssignmentId = resolvedAssignmentId,
        //                DetailsJson = "{}",
        //                Status = newStatus,
        //                IsActive = true,

        //                CreatedBy = employee.EmployeeId,
        //                CreatedOn = now
        //            };

        //            _context.CandidateSkillAssessments.Add(assessment);
        //        }

        //        assessment!.Status = newStatus;

        //        if (request.Score.HasValue)
        //            assessment.Score = request.Score.Value;

        //        if (isRemarkAdded)
        //            assessment.Remarks = request.Remarks!.Trim();

        //        if (!isNew)
        //        {
        //            assessment.ModifiedBy = employee.EmployeeId;
        //            assessment.ModifiedOn = now;
        //        }

        //        // Get the generated assessment ID before inserting history.
        //        await _context.SaveChangesAsync();

        //        var history = new CandidateSkillAssessmentHistory
        //        {
        //            CandidateSkillAssessmentId =
        //                assessment.CandidateSkillAssessmentId,

        //            CompanyId = assessment.CompanyId,
        //            CandidateApplicationId = assessment.CandidateApplicationId,
        //            JobDescriptionId = assessment.JobDescriptionId,
        //            CompanySkillMasterId = assessment.CompanySkillMasterId,
        //            SkillFormDefinitionId = assessment.SkillFormDefinitionId,
        //            AssignmentId = assessment.AssignmentId,

        //            Action = action,
        //            PreviousStatus = previousStatus,
        //            NewStatus = assessment.Status,

        //            DetailsJson = assessment.DetailsJson,
        //            Score = assessment.Score,
        //            Remarks = assessment.Remarks,
        //            IsRemarkAdded = isRemarkAdded,

        //            CreatedBy = employee.EmployeeId,
        //            CreatedOn = now
        //        };

        //        _context.CandidateSkillAssessmentHistories.Add(history);

        //        await _context.SaveChangesAsync();

        //        var remarksCount =
        //            await _context.CandidateSkillAssessmentHistories
        //                .CountAsync(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateSkillAssessmentId ==
        //                        assessment.CandidateSkillAssessmentId &&
        //                    x.IsRemarkAdded);

        //        // The selected assessment may have a different creator
        //        // because your current lookup has no CreatedBy filter.
        //        string? assessmentCreatorName = null;

        //        if (assessment.CreatedBy == employee.EmployeeId)
        //        {
        //            assessmentCreatorName = employeeName;
        //        }
        //        else if (assessment.CreatedBy.HasValue)
        //        {
        //            assessmentCreatorName = await _context.EmployeeMasters
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.EmployeeId == assessment.CreatedBy.Value)
        //                .Select(x =>
        //                    ((x.FirstName ?? "") + " " +
        //                     (x.LastName ?? "")).Trim())
        //                .FirstOrDefaultAsync();
        //        }

        //        await transaction.CommitAsync();

        //        return Ok(new
        //        {
        //            Status = true,
        //            Message = isNew
        //                ? "Assessment and decision created successfully."
        //                : "Assessment decision saved successfully.",

        //            Data = new
        //            {
        //                history.CandidateSkillAssessmentHistoryId,
        //                assessment.CandidateSkillAssessmentId,
        //                assessment.CompanyId,
        //                application.CandidateId,
        //                assessment.CandidateApplicationId,
        //                assessment.JobDescriptionId,
        //                assessment.CompanySkillMasterId,

        //                SkillId = assessment.CompanySkillMasterId,
        //                skill.SkillName,
        //                CategoryId = category.SkillMappingId,
        //                category.CategoryName,

        //                PreviousStatus = previousStatus,
        //                AssessmentStatus = assessment.Status,
        //                assessment.Score,
        //                assessment.Remarks,
        //                RemarksCount = remarksCount,

        //                assessment.CreatedBy,
        //                CreatedByName = assessmentCreatorName,
        //                assessment.CreatedOn,

        //                assessment.ModifiedBy,
        //                ModifiedByName = assessment.ModifiedBy.HasValue
        //                    ? employeeName
        //                    : null,
        //                assessment.ModifiedOn
        //            }
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Serilog.Log.Error(
        //            ex,
        //            "Failed to save assessment decision for CompanyId {CompanyId}, AssessmentId {AssessmentId}, HistoryId {HistoryId}, EmployeeId {EmployeeId}.",
        //            request.CompanyId,
        //            request.CandidateSkillAssessmentId,
        //            request.CandidateSkillAssessmentHistoryId,
        //            request.EmployeeId);

        //        return StatusCode(500, new
        //        {
        //            Status = false,
        //            Message = "Unable to save skill assessment decision."
        //        });
        //    }
        //}
        //public async Task<IActionResult> ApproveRejectSkillAssessment(
        //[FromBody] UpdateSkillAssessmentStatusRequest? request)
        //{
        //    IActionResult Fail(int code, string message)
        //    {
        //        return StatusCode(code, new
        //        {
        //            Status = false,
        //            Message = message
        //        });
        //    }

        //    if (request == null ||
        //        request.CompanyId <= 0 ||
        //        request.CandidateId <= 0 ||
        //        request.JobDescriptionId <= 0 ||
        //        request.CompanySkillMasterId <= 0 ||
        //        request.EmployeeId <= 0 ||
        //        request.CandidateSkillAssessmentId < 0 
        //       )
        //    {
        //        return Fail(400, "Provide valid reference IDs.");
        //    }

        //    var action = request.Action?.Trim().ToLowerInvariant() switch
        //    {
        //        "approve" => "Approve",
        //        "reject" => "Reject",
        //        _ => null
        //    };

        //    if (action == null)
        //        return Fail(400, "Action must be Approve or Reject.");

        //    bool isHistoryEdit =
        //        request.CandidateSkillAssessmentHistoryId > 0;

        //    try
        //    {
        //        await using var transaction =
        //            await _context.Database.BeginTransactionAsync(
        //                System.Data.IsolationLevel.Serializable);

        //        var employee = await _context.EmployeeMasters
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.EmployeeId == request.EmployeeId);

        //        if (employee == null)
        //            return Fail(404, "Employee not found for this company.");

        //        if (employee.IsActive != true)
        //            return Fail(403, "Employee is inactive.");

        //        bool isAdmin = employee.IsAdmin == true;

        //        var employeeName =
        //            ((employee.FirstName ?? "") + " " +
        //             (employee.LastName ?? "")).Trim();

        //        var candidateExists = await _context.Candidates
        //            .AsNoTracking()
        //            .AnyAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CandidateId == request.CandidateId);

        //        if (!candidateExists)
        //            return Fail(404, "Candidate not found for this company.");

        //        var job = await _context.JobDescriptions
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.JobDescriptionId == request.JobDescriptionId);

        //        if (job == null)
        //            return Fail(404, "Job description not found for this company.");

        //        var applications = await _context.CandidateApplications
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CandidateId == request.CandidateId &&
        //                x.JobDescriptionId == request.JobDescriptionId)
        //            .Take(2)
        //            .ToListAsync();

        //        if (applications.Count == 0)
        //            return Fail(404, "Candidate application not found for this job.");

        //        if (applications.Count > 1)
        //        {
        //            return Fail(
        //                409,
        //                "Multiple applications exist for this candidate and job. CandidateApplicationId is needed.");
        //        }

        //        var application = applications[0];

        //        if (application.IsActive != true)
        //            return Fail(409, "Candidate application is inactive.");

        //        var skill = await _context.Set<CompanySkillMaster>()
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CompanySkillMasterId == request.CompanySkillMasterId &&
        //                x.IsActive == true);

        //        if (skill == null)
        //            return Fail(409, "The company skill is unavailable.");

        //        var category = await _context.Set<CompanySkillMapping>()
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.SkillMappingId == skill.SkillMappingId &&
        //                x.IsActive == true);

        //        if (category == null)
        //            return Fail(409, "The skill category is unavailable.");

        //        CandidateSkillAssessmentHistory? historyToEdit = null;

        //        var assessmentQuery = _context.CandidateSkillAssessments
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CompanySkillMasterId == request.CompanySkillMasterId &&
        //                x.JobDescriptionId == request.JobDescriptionId &&
        //                x.CandidateApplicationId ==
        //                    application.CandidateApplicationId 
        //                    //&&
        //                //x.CreatedBy == employee.EmployeeId
        //                );

        //        CandidateSkillAssessment? assessment;

        //        if (isHistoryEdit)
        //        {
        //            historyToEdit =
        //                await _context.CandidateSkillAssessmentHistories
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CandidateSkillAssessmentHistoryId ==
        //                            request.CandidateSkillAssessmentHistoryId &&
        //                        x.CompanyId == request.CompanyId &&
        //                        x.CompanySkillMasterId ==
        //                            request.CompanySkillMasterId &&
        //                        x.JobDescriptionId == request.JobDescriptionId &&
        //                        x.CandidateApplicationId ==
        //                            application.CandidateApplicationId &&
        //                        x.CreatedBy == employee.EmployeeId);

        //            if (historyToEdit == null)
        //            {
        //                return Fail(
        //                    404,
        //                    "History record not found for this employee, candidate, job and skill.");
        //            }

        //            if (historyToEdit.Action != "Approve" &&
        //                historyToEdit.Action != "Reject")
        //            {
        //                return Fail(
        //                    400,
        //                    "Use this API to edit approval/rejection history only.");
        //            }

        //            if (request.CandidateSkillAssessmentId > 0 &&
        //                request.CandidateSkillAssessmentId !=
        //                    historyToEdit.CandidateSkillAssessmentId)
        //            {
        //                return Fail(
        //                    400,
        //                    "History record does not belong to the supplied assessment.");
        //            }

        //            assessment = await assessmentQuery
        //                .FirstOrDefaultAsync(x =>
        //                    x.CandidateSkillAssessmentId ==
        //                        historyToEdit.CandidateSkillAssessmentId);

        //            if (assessment == null)
        //            {
        //                return Fail(
        //                    404,
        //                    "Assessment not found for this history record and employee.");
        //            }
        //        }
        //        else if (request.CandidateSkillAssessmentId > 0)
        //        {
        //            assessment = await assessmentQuery
        //                .FirstOrDefaultAsync(x =>
        //                    x.CandidateSkillAssessmentId ==
        //                        request.CandidateSkillAssessmentId);

        //            if (assessment == null)
        //            {
        //                return Fail(
        //                    404,
        //                    "Assessment not found for this employee, candidate, job and company skill.");
        //            }
        //        }
        //        else
        //        {
        //            var matchingAssessments = await assessmentQuery
        //                .Take(2)
        //                .ToListAsync();

        //            if (matchingAssessments.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    "Multiple assessments exist. Send CandidateSkillAssessmentId.");
        //            }

        //            assessment = matchingAssessments.FirstOrDefault();
        //        }

        //        bool isNew = assessment == null;

        //        if (!isNew && assessment!.IsActive != true)
        //            return Fail(409, "Skill assessment is inactive.");

        //        int? resolvedAssignmentId = assessment?.AssignmentId;

        //        if (!isAdmin)
        //        {
        //            var assignmentQuery = _context.RoleTemplateAssignment
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.EmployeeId == employee.EmployeeId &&
        //                    x.TemplateId == job.TemplateId &&
        //                    x.IsActive == true)
        //                .Where(x =>
        //                    _context.Set<RoleTemplate>().Any(template =>
        //                        template.CompanyId == x.CompanyId &&
        //                        template.TemplateId == x.TemplateId &&
        //                        template.IsActive == true))
        //                .Where(x =>
        //                    _context.Set<RoleTemplateAssignmentSkill>().Any(mapping =>
        //                        mapping.CompanyId == x.CompanyId &&
        //                        mapping.AssignmentId == x.AssignmentId &&
        //                        mapping.SkillId == skill.CompanySkillMasterId));

        //            if (!isNew)
        //            {
        //                if (!assessment!.AssignmentId.HasValue)
        //                {
        //                    return Fail(
        //                        403,
        //                        "No role assignment is linked to this assessment.");
        //                }

        //                int existingAssignmentId = assessment.AssignmentId.Value;

        //                assignmentQuery = assignmentQuery.Where(x =>
        //                    x.AssignmentId == existingAssignmentId);
        //            }

        //            var assignments = await assignmentQuery
        //                .Take(2)
        //                .ToListAsync();

        //            if (assignments.Count == 0)
        //            {
        //                return Fail(
        //                    403,
        //                    "An active assignment for this employee, job template and skill was not found.");
        //            }

        //            if (assignments.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    "Multiple matching assignments exist. AssignmentId is needed.");
        //            }

        //            var assignment = assignments[0];
        //            resolvedAssignmentId = assignment.AssignmentId;

        //            if (isHistoryEdit &&
        //                historyToEdit!.AssignmentId != resolvedAssignmentId)
        //            {
        //                return Fail(
        //                    403,
        //                    "The history record belongs to a different assignment.");
        //            }

        //            if (action == "Approve" && assignment.CanApprove != true)
        //                return Fail(403, "Approve permission is not available.");

        //            if (action == "Reject" && assignment.CanReject != true)
        //                return Fail(403, "Reject permission is not available.");
        //        }

        //        var now = DateTime.UtcNow;

        //        string newStatus = action == "Approve"
        //            ? "Approved"
        //            : "Rejected";

        //        bool isRemarkAdded =
        //            !string.IsNullOrWhiteSpace(request.Remarks);

        //        // Edit only the selected history row.
        //        if (isHistoryEdit)
        //        {
        //            historyToEdit!.Action = action;
        //            historyToEdit.NewStatus = newStatus;

        //            if (request.Score.HasValue)
        //                historyToEdit.Score = request.Score.Value;

        //            if (isRemarkAdded)
        //            {
        //                historyToEdit.Remarks = request.Remarks!.Trim();
        //                historyToEdit.IsRemarkAdded = true;
        //            }

        //            historyToEdit.ModifiedBy = employee.EmployeeId;
        //            historyToEdit.ModifiedOn = now;

        //            // Preserve PreviousStatus, CreatedBy, CreatedOn,
        //            // DetailsJson and the main assessment.
        //            await _context.SaveChangesAsync();

        //            var historyRemarksCount =
        //                await _context.CandidateSkillAssessmentHistories
        //                    .CountAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.CandidateSkillAssessmentId ==
        //                            assessment!.CandidateSkillAssessmentId &&
        //                        x.IsRemarkAdded);

        //            await transaction.CommitAsync();

        //            return Ok(new
        //            {
        //                Status = true,
        //                Message = "Approval/rejection history updated successfully.",
        //                Data = new
        //                {
        //                    historyToEdit.CandidateSkillAssessmentHistoryId,
        //                    historyToEdit.CandidateSkillAssessmentId,
        //                    historyToEdit.CompanyId,
        //                    application.CandidateId,
        //                    historyToEdit.CandidateApplicationId,
        //                    historyToEdit.JobDescriptionId,
        //                    historyToEdit.CompanySkillMasterId,

        //                    SkillId = skill.CompanySkillMasterId,
        //                    skill.SkillName,
        //                    CategoryId = category.SkillMappingId,
        //                    category.CategoryName,

        //                    historyToEdit.Action,
        //                    historyToEdit.PreviousStatus,
        //                    historyToEdit.NewStatus,
        //                    historyToEdit.Score,
        //                    historyToEdit.Remarks,

        //                    // History edit does not change this current status.
        //                    AssessmentStatus = assessment!.Status,
        //                    RemarksCount = historyRemarksCount,

        //                    historyToEdit.CreatedBy,
        //                    CreatedByName = employeeName,
        //                    historyToEdit.CreatedOn,
        //                    historyToEdit.ModifiedBy,
        //                    ModifiedByName = employeeName,
        //                    historyToEdit.ModifiedOn
        //                }
        //            });
        //        }

        //        // Existing assessments retain the previous status restrictions.
        //        if (!isNew &&
        //            assessment!.Status != "InProgress" &&
        //            assessment.Status != "Approved" &&
        //            assessment.Status != "Rejected")
        //        {
        //            return Fail(
        //                409,
        //                "Save the assessment before approving or rejecting it.");
        //        }

        //        var previousStatus = assessment?.Status;

        //        if (isNew)
        //        {
        //            assessment = new CandidateSkillAssessment
        //            {
        //                CompanyId = request.CompanyId,
        //                CandidateApplicationId =
        //                    application.CandidateApplicationId,
        //                JobDescriptionId = request.JobDescriptionId,
        //                CompanySkillMasterId = request.CompanySkillMasterId,

        //                // Requires the nullable property/column discussed earlier.
        //                SkillFormDefinitionId = 0,

        //                AssignmentId = resolvedAssignmentId,
        //                DetailsJson = "{}",
        //                Status = newStatus,
        //                IsActive = true,
        //                CreatedBy = employee.EmployeeId,
        //                CreatedOn = now
        //            };

        //            _context.CandidateSkillAssessments.Add(assessment);
        //        }

        //        assessment!.Status = newStatus;

        //        if (request.Score.HasValue)
        //            assessment.Score = request.Score.Value;

        //        if (isRemarkAdded)
        //            assessment.Remarks = request.Remarks!.Trim();

        //        if (!isNew)
        //        {
        //            assessment.ModifiedBy = employee.EmployeeId;
        //            assessment.ModifiedOn = now;
        //        }

        //        // Generate the assessment ID before inserting its history.
        //        await _context.SaveChangesAsync();

        //        var history = new CandidateSkillAssessmentHistory
        //        {
        //            CandidateSkillAssessmentId =
        //                assessment.CandidateSkillAssessmentId,
        //            CompanyId = assessment.CompanyId,
        //            CandidateApplicationId = assessment.CandidateApplicationId,
        //            JobDescriptionId = assessment.JobDescriptionId,
        //            CompanySkillMasterId = assessment.CompanySkillMasterId,
        //            SkillFormDefinitionId = assessment.SkillFormDefinitionId,
        //            AssignmentId = assessment.AssignmentId,

        //            Action = action,
        //            PreviousStatus = previousStatus,
        //            NewStatus = assessment.Status,

        //            DetailsJson = assessment.DetailsJson,
        //            Score = assessment.Score,
        //            Remarks = assessment.Remarks,
        //            IsRemarkAdded = isRemarkAdded,

        //            CreatedBy = employee.EmployeeId,
        //            CreatedOn = now
        //        };

        //        _context.CandidateSkillAssessmentHistories.Add(history);

        //        await _context.SaveChangesAsync();

        //        var remarksCount =
        //            await _context.CandidateSkillAssessmentHistories
        //                .CountAsync(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateSkillAssessmentId ==
        //                        assessment.CandidateSkillAssessmentId &&
        //                    x.IsRemarkAdded);

        //        await transaction.CommitAsync();

        //        return Ok(new
        //        {
        //            Status = true,
        //            Message = isNew
        //                ? "Assessment and decision created successfully."
        //                : "Assessment decision saved successfully.",

        //            Data = new
        //            {
        //                history.CandidateSkillAssessmentHistoryId,
        //                assessment.CandidateSkillAssessmentId,
        //                assessment.CompanyId,
        //                application.CandidateId,
        //                assessment.CandidateApplicationId,
        //                assessment.JobDescriptionId,
        //                assessment.CompanySkillMasterId,

        //                SkillId = assessment.CompanySkillMasterId,
        //                skill.SkillName,
        //                CategoryId = category.SkillMappingId,
        //                category.CategoryName,

        //                PreviousStatus = previousStatus,
        //                AssessmentStatus = assessment.Status,
        //                assessment.Score,
        //                assessment.Remarks,
        //                RemarksCount = remarksCount,

        //                assessment.CreatedBy,
        //                CreatedByName = employeeName,
        //                assessment.CreatedOn,
        //                assessment.ModifiedBy,
        //                ModifiedByName = assessment.ModifiedBy.HasValue
        //                    ? employeeName
        //                    : null,
        //                assessment.ModifiedOn
        //            }
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Serilog.Log.Error(
        //            ex,
        //            "Failed to save assessment decision for CompanyId {CompanyId}, AssessmentId {AssessmentId}, HistoryId {HistoryId}, EmployeeId {EmployeeId}.",
        //            request.CompanyId,
        //            request.CandidateSkillAssessmentId,
        //            request.CandidateSkillAssessmentHistoryId,
        //            request.EmployeeId);

        //        return StatusCode(500, new
        //        {
        //            Status = false,
        //            Message = "Unable to save skill assessment decision."
        //        });
        //    }
        //}





        //  [HttpPost("GetCandidateSkillAssessmentList")]
        //  public async Task<IActionResult> GetCandidateSkillAssessmentList(
        //[FromBody] GetCandidateSkillAssessmentListRequest? request)
        //  {
        //      if (request == null ||
        //          request.CompanyId <= 0 ||
        //          request.User <= 0)
        //      {
        //          return BadRequest(new
        //          {
        //              Status = false,
        //              Message = "Valid CompanyId and User are required."
        //          });
        //      }

        //      if (request.CandidateId < 0 ||
        //          request.JobDescriptionId < 0 ||
        //          request.SkillCategoryId < 0)
        //      {
        //          return BadRequest(new
        //          {
        //              Status = false,
        //              Message = "Filter IDs cannot be negative."
        //          });
        //      }

        //      try
        //      {
        //          var currentUser = await _context.EmployeeMasters
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  x.EmployeeId == request.User)
        //              .Select(x => new
        //              {
        //                  x.EmployeeId,
        //                  x.IsAdmin,
        //                  x.IsActive
        //              })
        //              .FirstOrDefaultAsync();

        //          if (currentUser == null)
        //          {
        //              return NotFound(new
        //              {
        //                  Status = false,
        //                  Message = "User not found for this company."
        //              });
        //          }

        //          if (currentUser.IsActive != true)
        //          {
        //              return StatusCode(403, new
        //              {
        //                  Status = false,
        //                  Message = "User is inactive."
        //              });
        //          }

        //          bool isAdmin = currentUser.IsAdmin == true;

        //          // Company-specific categories and skills.
        //          var skillsQuery =
        //              from skill in _context.Set<CompanySkillMaster>()
        //                  .AsNoTracking()

        //              join category in _context.Set<CompanySkillMapping>()
        //                  .AsNoTracking()
        //                  on new
        //                  {
        //                      skill.CompanyId,
        //                      skill.SkillMappingId
        //                  }
        //                  equals new
        //                  {
        //                      category.CompanyId,
        //                      category.SkillMappingId
        //                  }

        //              where skill.CompanyId == request.CompanyId
        //                    && skill.IsActive == true
        //                    && category.IsActive == true

        //              select new
        //              {
        //                  SkillCategoryId = category.SkillMappingId,
        //                  SkillCategoryName = category.CategoryName,

        //                  SkillMasterId = skill.CompanySkillMasterId,
        //                  skill.SkillName,
        //                  skill.IsDisable
        //              };

        //          if (request.SkillCategoryId > 0)
        //          {
        //              skillsQuery = skillsQuery.Where(x =>
        //                  x.SkillCategoryId == request.SkillCategoryId);
        //          }

        //          // IsDisable is intentionally not used as a filter.
        //          var companySkills = await skillsQuery
        //              .OrderBy(x => x.SkillCategoryId)
        //              .ThenBy(x => x.SkillMasterId)
        //              .ToListAsync();

        //          var applicationsQuery = _context.CandidateApplications
        //              .AsNoTracking()
        //              .Where(x => x.CompanyId == request.CompanyId);

        //          if (request.CandidateId > 0)
        //          {
        //              applicationsQuery = applicationsQuery.Where(x =>
        //                  x.CandidateId == request.CandidateId);
        //          }

        //          if (request.JobDescriptionId > 0)
        //          {
        //              applicationsQuery = applicationsQuery.Where(x =>
        //                  x.JobDescriptionId == request.JobDescriptionId);
        //          }

        //          var applications = await (
        //              from application in applicationsQuery

        //              join candidate in _context.Candidates.AsNoTracking()
        //                  on new
        //                  {
        //                      application.CompanyId,
        //                      application.CandidateId
        //                  }
        //                  equals new
        //                  {
        //                      candidate.CompanyId,
        //                      candidate.CandidateId
        //                  }

        //              join job in _context.JobDescriptions.AsNoTracking()
        //                  on new
        //                  {
        //                      application.CompanyId,
        //                      application.JobDescriptionId
        //                  }
        //                  equals new
        //                  {
        //                      job.CompanyId,
        //                      job.JobDescriptionId
        //                  }

        //              select new
        //              {
        //                  application.CandidateApplicationId,
        //                  application.JobDescriptionId,

        //                  candidate.CandidateId,
        //                  candidate.FirstName,
        //                  candidate.MiddleName,
        //                  candidate.LastName,

        //                  JobName = job.JobTitle,
        //                  TemplateId = (int?)job.TemplateId
        //              }
        //          )
        //          .OrderByDescending(x => x.CandidateApplicationId)
        //          .ToListAsync();

        //          if (companySkills.Count == 0 || applications.Count == 0)
        //          {
        //              return Ok(new
        //              {
        //                  Status = true,
        //                  Message = companySkills.Count == 0
        //                      ? "No active company skills found."
        //                      : "No candidate applications found.",
        //                  IsAdmin = isAdmin,
        //                  TotalCount = 0,
        //                  Data = Array.Empty<object>()
        //              });
        //          }

        //          var applicationIds = applications
        //              .Select(x => x.CandidateApplicationId)
        //              .Distinct()
        //              .ToArray();

        //          var templateIds = applications
        //              .Where(x => x.TemplateId.HasValue)
        //              .Select(x => x.TemplateId!.Value)
        //              .Distinct()
        //              .ToArray();

        //          var skillIds = companySkills
        //              .Select(x => x.SkillMasterId)
        //              .Distinct()
        //              .ToArray();

        //          // Used only to restrict non-admin visibility.
        //          var assignedSkillsQuery =
        //              from assignment in _context.RoleTemplateAssignment
        //                  .AsNoTracking()

        //              join template in _context.RoleTemplates.AsNoTracking()
        //                  on new
        //                  {
        //                      assignment.CompanyId,
        //                      assignment.TemplateId
        //                  }
        //                  equals new
        //                  {
        //                      template.CompanyId,
        //                      template.TemplateId
        //                  }

        //              join mapping in _context.RoleTemplateAssignmentSkills
        //                  .AsNoTracking()
        //                  on new
        //                  {
        //                      assignment.CompanyId,
        //                      assignment.AssignmentId
        //                  }
        //                  equals new
        //                  {
        //                      mapping.CompanyId,
        //                      mapping.AssignmentId
        //                  }

        //              where assignment.CompanyId == request.CompanyId
        //                    && assignment.EmployeeId == request.User
        //                    && assignment.IsActive == true
        //                    && template.IsActive == true
        //                    && templateIds.Contains(assignment.TemplateId)
        //                    && skillIds.Contains(mapping.SkillId)

        //              select new
        //              {
        //                  assignment.TemplateId,
        //                  assignment.AssignmentId,
        //                  assignment.EmployeeId,
        //                  assignment.RoleId,
        //                  assignment.HierarchyLevel,
        //                  SkillMasterId = mapping.SkillId
        //              };

        //          // Admin does not require any assignment.
        //          var assignedSkills = await assignedSkillsQuery
        //              .Where(x => !isAdmin)
        //              .Distinct()
        //              .ToListAsync();

        //          var assessmentsQuery = _context.CandidateSkillAssessments
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  applicationIds.Contains(x.CandidateApplicationId));

        //          if (!isAdmin)
        //          {
        //              assessmentsQuery = assessmentsQuery.Where(x =>
        //                  x.CreatedBy == request.User);
        //          }

        //          // Includes admin assessments with AssignmentId = null.
        //          var savedAssessments = await (
        //              from assessment in assessmentsQuery

        //              join definition in _context.SkillFormDefinitions
        //                  .AsNoTracking()
        //                  on assessment.SkillFormDefinitionId
        //                  equals definition.SkillFormDefinitionId

        //              where skillIds.Contains(definition.SkillId)

        //              select new
        //              {
        //                  Assessment = assessment,
        //                  SkillMasterId = definition.SkillId,
        //                  SkillCategoryId = definition.CategoryId,
        //                  FormVersion = definition.Version
        //              }
        //          ).ToListAsync();

        //          var assessmentLookup = savedAssessments.ToLookup(x => (
        //              x.Assessment.CandidateApplicationId,
        //              x.Assessment.JobDescriptionId,
        //              x.SkillCategoryId,
        //              x.SkillMasterId
        //          ));

        //          var assessmentIds = savedAssessments
        //              .Select(x => x.Assessment.CandidateSkillAssessmentId)
        //              .Distinct()
        //              .ToArray();

        //          var histories = await _context.CandidateSkillAssessmentHistories
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  assessmentIds.Contains(x.CandidateSkillAssessmentId))
        //              .OrderByDescending(x => x.CreatedOn)
        //              .ThenByDescending(x =>
        //                  x.CandidateSkillAssessmentHistoryId)
        //              .ToListAsync();

        //          var historyLookup = histories.ToLookup(x =>
        //              x.CandidateSkillAssessmentId);

        //          var employeeIds = savedAssessments
        //              .SelectMany(x => new int?[]
        //              {
        //          x.Assessment.CreatedBy,
        //          x.Assessment.ModifiedBy
        //              })
        //              .Concat(histories.Select(x => (int?)x.CreatedBy))
        //              .Concat(new int?[] { request.User })
        //              .Where(x => x.HasValue)
        //              .Select(x => x!.Value)
        //              .Distinct()
        //              .ToArray();

        //          var employeeNames = await _context.EmployeeMasters
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  employeeIds.Contains(x.EmployeeId))
        //              .Select(x => new
        //              {
        //                  x.EmployeeId,
        //                  EmployeeName =
        //                      ((x.FirstName ?? "") + " " +
        //                       (x.LastName ?? "")).Trim()
        //              })
        //              .ToDictionaryAsync(
        //                  x => x.EmployeeId,
        //                  x => x.EmployeeName);

        //          // Load assignment metadata for saved assessments and role rows.
        //          var assignmentIds = savedAssessments
        //              .Where(x => x.Assessment.AssignmentId.HasValue)
        //              .Select(x => x.Assessment.AssignmentId!.Value)
        //              .Concat(assignedSkills.Select(x => x.AssignmentId))
        //              .Distinct()
        //              .ToArray();

        //          var assignmentDetails = await _context.RoleTemplateAssignment
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  assignmentIds.Contains(x.AssignmentId))
        //              .Select(x => new
        //              {
        //                  x.AssignmentId,
        //                  x.RoleId,
        //                  x.HierarchyLevel
        //              })
        //              .ToDictionaryAsync(x => x.AssignmentId);

        //          var roleIds = assignmentDetails.Values
        //              .Select(x => x.RoleId)
        //              .Distinct()
        //              .ToArray();

        //          var roleNames = await _context.Roles
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  roleIds.Contains(x.RoleId))
        //              .ToDictionaryAsync(x => x.RoleId, x => x.RoleName);

        //          string? GetEmployeeName(int? employeeId)
        //          {
        //              return employeeId.HasValue &&
        //                     employeeNames.TryGetValue(
        //                         employeeId.Value, out var name)
        //                  ? name
        //                  : null;
        //          }

        //          static JsonElement ReadDetails(string? json)
        //          {
        //              using var document = JsonDocument.Parse(
        //                  string.IsNullOrWhiteSpace(json) ? "{}" : json);

        //              return document.RootElement.Clone();
        //          }

        //          var data = new System.Collections.Generic.List<object>();

        //          foreach (var application in applications)
        //          {
        //              foreach (var skill in companySkills)
        //              {
        //                  var matchingAssignments = assignedSkills
        //                      .Where(x =>
        //                          application.TemplateId == x.TemplateId &&
        //                          x.SkillMasterId == skill.SkillMasterId)
        //                      .ToList();

        //                  // Only non-admin users need a matching role assignment.
        //                  if (!isAdmin && matchingAssignments.Count == 0)
        //                  {
        //                      continue;
        //                  }

        //                  var matchingAssessments = assessmentLookup[(
        //                      application.CandidateApplicationId,
        //                      application.JobDescriptionId,
        //                      skill.SkillCategoryId,
        //                      skill.SkillMasterId
        //                  )].ToList();

        //                  if (!isAdmin)
        //                  {
        //                      var allowedAssignmentIds = matchingAssignments
        //                          .Select(x => x.AssignmentId)
        //                          .ToHashSet();

        //                      matchingAssessments = matchingAssessments
        //                          .Where(x =>
        //                              x.Assessment.AssignmentId.HasValue &&
        //                              allowedAssignmentIds.Contains(
        //                                  x.Assessment.AssignmentId.Value))
        //                          .ToList();
        //                  }

        //                  // Admin: all saved assessments for this company skill.
        //                  // If none exist, return one unsaved Pending row.
        //                  // Non-admin: one row per assignment, including unsaved rows.
        //                  var scopes = isAdmin
        //                      ? new int?[] { null }
        //                      : matchingAssignments
        //                          .Select(x => (int?)x.AssignmentId)
        //                          .Distinct()
        //                          .ToArray();

        //                  foreach (var scopeAssignmentId in scopes)
        //                  {
        //                      var scopeAssessments = matchingAssessments
        //                          .Where(x =>
        //                              isAdmin ||
        //                              x.Assessment.AssignmentId == scopeAssignmentId)
        //                          .ToList();

        //                      foreach (var saved in scopeAssessments.DefaultIfEmpty())
        //                      {
        //                          var assessment = saved?.Assessment;

        //                          int? assignmentId =
        //                              assessment?.AssignmentId ?? scopeAssignmentId;

        //                          int? roleId = null;
        //                          int? hierarchyLevel = null;
        //                          string? roleName = null;

        //                          if (assignmentId.HasValue &&
        //                              assignmentDetails.TryGetValue(
        //                                  assignmentId.Value, out var assignment))
        //                          {
        //                              roleId = assignment.RoleId;
        //                              hierarchyLevel = assignment.HierarchyLevel;

        //                              roleNames.TryGetValue(
        //                                  assignment.RoleId, out roleName);
        //                          }

        //                          // Unsaved admin rows have no assessment owner.
        //                          int? employeeId = assessment != null
        //                              ? assessment.CreatedBy
        //                              : isAdmin
        //                                  ? (int?)null
        //                                  : request.User;

        //                          var assessmentHistory = historyLookup[
        //                              assessment?.CandidateSkillAssessmentId ?? 0]
        //                              .ToList();

        //                          var remarkHistory = assessmentHistory
        //                              .Where(x => x.IsRemarkAdded)
        //                              .Select(x => new
        //                              {
        //                                  HistoryId =
        //                                      x.CandidateSkillAssessmentHistoryId,
        //                                  x.Action,
        //                                  x.Remarks,
        //                                  x.CreatedBy,
        //                                  CreatedByName =
        //                                      GetEmployeeName(x.CreatedBy),
        //                                  x.CreatedOn
        //                              })
        //                              .ToList();

        //                          data.Add(new
        //                          {
        //                              CandidateSkillAssessmentId =
        //                                  assessment?.CandidateSkillAssessmentId,

        //                              request.CompanyId,
        //                              application.CandidateId,

        //                              CandidateName = string.Join(
        //                                  " ",
        //                                  new[]
        //                                  {
        //                              application.FirstName,
        //                              application.MiddleName,
        //                              application.LastName
        //                                  }.Where(x =>
        //                                      !string.IsNullOrWhiteSpace(x))),

        //                              application.CandidateApplicationId,
        //                              application.JobDescriptionId,
        //                              application.JobName,
        //                              application.TemplateId,

        //                              skill.SkillCategoryId,
        //                              skill.SkillCategoryName,

        //                              // Existing response name retained.
        //                              SkillId = skill.SkillMasterId,
        //                              skill.SkillMasterId,
        //                              skill.SkillName,
        //                              skill.IsDisable,

        //                              AssignmentId = assignmentId,
        //                              RoleId = roleId,
        //                              RoleName = roleName,
        //                              HierarchyLevel = hierarchyLevel,

        //                              EmployeeId = employeeId,
        //                              EmployeeName = GetEmployeeName(employeeId),

        //                              SkillFormDefinitionId =
        //                                  assessment?.SkillFormDefinitionId,
        //                              FormVersion = saved?.FormVersion,

        //                              IsAssessmentSaved = assessment != null,

        //                              Details = ReadDetails(
        //                                  assessment?.DetailsJson),

        //                              Score = assessment?.Score,
        //                              Remarks = assessment?.Remarks,
        //                              AssessmentStatus =
        //                                  assessment?.Status ?? "Pending",

        //                              // Null means an assessment has not been saved.
        //                              IsActive = assessment?.IsActive,

        //                              CreatedBy = assessment?.CreatedBy,
        //                              CreatedByName =
        //                                  GetEmployeeName(assessment?.CreatedBy),
        //                              CreatedOn = assessment?.CreatedOn,

        //                              ModifiedBy = assessment?.ModifiedBy,
        //                              ModifiedByName =
        //                                  GetEmployeeName(assessment?.ModifiedBy),
        //                              ModifiedOn = assessment?.ModifiedOn,

        //                              RemarksCount = remarkHistory.Count,
        //                              RemarkHistory = remarkHistory,

        //                              History = assessmentHistory.Select(x => new
        //                              {
        //                                  HistoryId =
        //                                      x.CandidateSkillAssessmentHistoryId,
        //                                  x.Action,
        //                                  x.PreviousStatus,
        //                                  x.NewStatus,
        //                                  x.SkillFormDefinitionId,

        //                                  Details = ReadDetails(x.DetailsJson),

        //                                  x.Score,
        //                                  x.Remarks,
        //                                  x.IsRemarkAdded,

        //                                  x.CreatedBy,
        //                                  CreatedByName =
        //                                      GetEmployeeName(x.CreatedBy),
        //                                  x.CreatedOn
        //                              }).ToList()
        //                          });
        //                      }
        //                  }
        //              }
        //          }

        //          return Ok(new
        //          {
        //              Status = true,
        //              Message = data.Count > 0
        //                  ? "Skill assessments fetched successfully."
        //                  : "No assigned skills found for this user and job.",
        //              IsAdmin = isAdmin,
        //              TotalCount = data.Count,
        //              Data = data
        //          });
        //      }
        //      catch (Exception ex)
        //      {
        //          Serilog.Log.Error(
        //              ex,
        //              "Failed to fetch skill assessments for CompanyId {CompanyId}, User {UserId}.",
        //              request.CompanyId,
        //              request.User);

        //          return StatusCode(500, new
        //          {
        //              Status = false,
        //              Message = "Unable to fetch skill assessments."
        //          });
        //      }
        //  }

        //  [HttpPost("GetCandidateSkillAssessmentList")]
        //  public async Task<IActionResult> GetCandidateSkillAssessmentList(
        // [FromBody] GetCandidateSkillAssessmentListRequest? request)
        //  {
        //      if (request == null ||
        //          request.CompanyId <= 0 ||
        //          request.User <= 0)
        //      {
        //          return BadRequest(new
        //          {
        //              Status = false,
        //              Message = "Valid CompanyId and User are required."
        //          });
        //      }

        //      var baseUrl = $"{Request.Scheme}://{Request.Host}/HiringV2/";

        //      if (request.CandidateId < 0 ||
        //          request.JobDescriptionId < 0 ||
        //          request.SkillCategoryId < 0)
        //      {
        //          return BadRequest(new
        //          {
        //              Status = false,
        //              Message = "Filter IDs cannot be negative."
        //          });
        //      }

        //      try
        //      {
        //          var currentUser = await _context.EmployeeMasters
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  x.EmployeeId == request.User)
        //              .Select(x => new
        //              {
        //                  x.EmployeeId,
        //                  x.IsAdmin,
        //                  x.IsActive
        //              })
        //              .FirstOrDefaultAsync();

        //          if (currentUser == null)
        //          {
        //              return NotFound(new
        //              {
        //                  Status = false,
        //                  Message = "User not found for this company."
        //              });
        //          }

        //          if (currentUser.IsActive != true)
        //          {
        //              return StatusCode(403, new
        //              {
        //                  Status = false,
        //                  Message = "User is inactive."
        //              });
        //          }

        //          bool isAdmin = currentUser.IsAdmin == true;

        //          var skillsQuery =
        //              from skill in _context.Set<CompanySkillMaster>()
        //                  .AsNoTracking()

        //              join category in _context.Set<CompanySkillMapping>()
        //                  .AsNoTracking()
        //                  on new
        //                  {
        //                      skill.CompanyId,
        //                      skill.SkillMappingId
        //                  }
        //                  equals new
        //                  {
        //                      category.CompanyId,
        //                      category.SkillMappingId
        //                  }

        //              where skill.CompanyId == request.CompanyId
        //                    && skill.IsActive == true
        //                    && category.IsActive == true

        //              select new
        //              {
        //                  SkillCategoryId = category.SkillMappingId,
        //                  SkillCategoryName = category.CategoryName,
        //                  SkillMasterId = skill.CompanySkillMasterId,
        //                  skill.SkillName,
        //                  skill.IsDisable
        //              };

        //          if (request.SkillCategoryId > 0)
        //          {
        //              skillsQuery = skillsQuery.Where(x =>
        //                  x.SkillCategoryId == request.SkillCategoryId);
        //          }

        //          // IsDisable does not hide the skill.
        //          var companySkills = await skillsQuery
        //              .OrderBy(x => x.SkillCategoryId)
        //              .ThenBy(x => x.SkillMasterId)
        //              .ToListAsync();

        //          var applicationsQuery = _context.CandidateApplications
        //              .AsNoTracking()
        //              .Where(x => x.CompanyId == request.CompanyId);

        //          if (request.CandidateId > 0)
        //          {
        //              applicationsQuery = applicationsQuery.Where(x =>
        //                  x.CandidateId == request.CandidateId);
        //          }

        //          if (request.JobDescriptionId > 0)
        //          {
        //              applicationsQuery = applicationsQuery.Where(x =>
        //                  x.JobDescriptionId == request.JobDescriptionId);
        //          }

        //          var applications = await (
        //              from application in applicationsQuery

        //              join candidate in _context.Candidates.AsNoTracking()
        //                  on new
        //                  {
        //                      application.CompanyId,
        //                      application.CandidateId
        //                  }
        //                  equals new
        //                  {
        //                      candidate.CompanyId,
        //                      candidate.CandidateId
        //                  }

        //              join job in _context.JobDescriptions.AsNoTracking()
        //                  on new
        //                  {
        //                      application.CompanyId,
        //                      application.JobDescriptionId
        //                  }
        //                  equals new
        //                  {
        //                      job.CompanyId,
        //                      job.JobDescriptionId
        //                  }

        //              select new
        //              {
        //                  application.CandidateApplicationId,
        //                  application.JobDescriptionId,

        //                  candidate.CandidateId,
        //                  candidate.FirstName,
        //                  candidate.MiddleName,
        //                  candidate.LastName,

        //                  JobName = job.JobTitle,
        //                  TemplateId = (int?)job.TemplateId
        //              }
        //          )
        //          .OrderByDescending(x => x.CandidateApplicationId)
        //          .ToListAsync();

        //          if (companySkills.Count == 0 || applications.Count == 0)
        //          {
        //              return Ok(new
        //              {
        //                  Status = true,
        //                  Message = companySkills.Count == 0
        //                      ? "No active company skills found."
        //                      : "No candidate applications found.",
        //                  IsAdmin = isAdmin,
        //                  TotalCount = 0,
        //                  Data = Array.Empty<object>()
        //              });
        //          }

        //          var applicationIds = applications
        //              .Select(x => x.CandidateApplicationId)
        //              .Distinct()
        //              .ToArray();

        //          var templateIds = applications
        //              .Where(x => x.TemplateId.HasValue)
        //              .Select(x => x.TemplateId!.Value)
        //              .Distinct()
        //              .ToArray();

        //          var skillIds = companySkills
        //              .Select(x => x.SkillMasterId)
        //              .Distinct()
        //              .ToArray();

        //          var assignedSkillsQuery =
        //              from assignment in _context.RoleTemplateAssignment
        //                  .AsNoTracking()

        //              join template in _context.RoleTemplates.AsNoTracking()
        //                  on new
        //                  {
        //                      assignment.CompanyId,
        //                      assignment.TemplateId
        //                  }
        //                  equals new
        //                  {
        //                      template.CompanyId,
        //                      template.TemplateId
        //                  }

        //              join mapping in _context.RoleTemplateAssignmentSkills
        //                  .AsNoTracking()
        //                  on new
        //                  {
        //                      assignment.CompanyId,
        //                      assignment.AssignmentId
        //                  }
        //                  equals new
        //                  {
        //                      mapping.CompanyId,
        //                      mapping.AssignmentId
        //                  }

        //              where assignment.CompanyId == request.CompanyId
        //                    && assignment.EmployeeId == request.User
        //                    && assignment.IsActive == true
        //                    && template.IsActive == true
        //                    && templateIds.Contains(assignment.TemplateId)
        //                    && skillIds.Contains(mapping.SkillId)

        //              select new
        //              {
        //                  assignment.TemplateId,
        //                  assignment.AssignmentId,
        //                  assignment.EmployeeId,
        //                  assignment.RoleId,
        //                  assignment.HierarchyLevel,
        //                  SkillMasterId = mapping.SkillId
        //              };

        //          // Admin does not require a role assignment.
        //          var assignedSkills = await assignedSkillsQuery
        //              .Where(x => !isAdmin)
        //              .Distinct()
        //              .ToListAsync();

        //          var assessmentsQuery = _context.CandidateSkillAssessments
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  applicationIds.Contains(x.CandidateApplicationId));

        //          if (!isAdmin)
        //          {
        //              assessmentsQuery = assessmentsQuery.Where(x =>
        //                  x.CreatedBy == request.User);
        //          }

        //          var savedAssessments = await (
        //              from assessment in assessmentsQuery

        //              join companySkill in _context.Set<CompanySkillMaster>()
        //                  .AsNoTracking()
        //                  on new
        //                  {
        //                      assessment.CompanyId,
        //                      assessment.CompanySkillMasterId
        //                  }
        //                  equals new
        //                  {
        //                      companySkill.CompanyId,
        //                      companySkill.CompanySkillMasterId
        //                  }

        //              where skillIds.Contains(companySkill.CompanySkillMasterId)

        //              select new
        //              {
        //                  Assessment = assessment,
        //                  SkillMasterId = companySkill.CompanySkillMasterId,
        //                  SkillCategoryId = companySkill.SkillMappingId,
        //                  FormVersion = (int?)null
        //              }
        //          ).ToListAsync();

        //          var assessmentLookup = savedAssessments.ToLookup(x => (
        //              x.Assessment.CandidateApplicationId,
        //              x.Assessment.JobDescriptionId,
        //              x.SkillCategoryId,
        //              x.SkillMasterId
        //          ));

        //          var assessmentIds = savedAssessments
        //              .Select(x => x.Assessment.CandidateSkillAssessmentId)
        //              .Distinct()
        //              .ToArray();

        //          var histories = await _context.CandidateSkillAssessmentHistories
        //.AsNoTracking()
        //.Where(x =>
        //    x.CompanyId == request.CompanyId &&
        //    assessmentIds.Contains(x.CandidateSkillAssessmentId) &&
        //    x.IsRemarkAdded != true)
        //.OrderByDescending(x => x.CreatedOn)
        //.ThenByDescending(x =>
        //    x.CandidateSkillAssessmentHistoryId)
        //.ToListAsync();

        //          var historyLookup = histories.ToLookup(x =>
        //              x.CandidateSkillAssessmentId);

        //          // Include history creators and modifiers when loading names.
        //          var employeeIds = savedAssessments
        //              .SelectMany(x => new int?[]
        //              {
        //          x.Assessment.CreatedBy,
        //          x.Assessment.ModifiedBy
        //              })
        //              .Concat(histories.SelectMany(x => new int?[]
        //              {
        //          x.CreatedBy,
        //          x.ModifiedBy
        //              }))
        //              .Concat(new int?[] { request.User })
        //              .Where(x => x.HasValue)
        //              .Select(x => x!.Value)
        //              .Distinct()
        //              .ToArray();

        //          var employeeNames = await _context.EmployeeMasters
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  employeeIds.Contains(x.EmployeeId))
        //              .Select(x => new
        //              {
        //                  x.EmployeeId,
        //                  EmployeeName =
        //                      ((x.FirstName ?? "") + " " +
        //                       (x.LastName ?? "")).Trim()
        //              })
        //              .ToDictionaryAsync(
        //                  x => x.EmployeeId,
        //                  x => x.EmployeeName);

        //          var assignmentIds = savedAssessments
        //              .Where(x => x.Assessment.AssignmentId.HasValue)
        //              .Select(x => x.Assessment.AssignmentId!.Value)
        //              .Concat(assignedSkills.Select(x => x.AssignmentId))
        //              .Distinct()
        //              .ToArray();

        //          var assignmentDetails = await _context.RoleTemplateAssignment
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  assignmentIds.Contains(x.AssignmentId))
        //              .Select(x => new
        //              {
        //                  x.AssignmentId,
        //                  x.RoleId,
        //                  x.HierarchyLevel
        //              })
        //              .ToDictionaryAsync(x => x.AssignmentId);

        //          var roleIds = assignmentDetails.Values
        //              .Select(x => x.RoleId)
        //              .Distinct()
        //              .ToArray();

        //          var roleNames = await _context.Roles
        //              .AsNoTracking()
        //              .Where(x =>
        //                  x.CompanyId == request.CompanyId &&
        //                  roleIds.Contains(x.RoleId))
        //              .ToDictionaryAsync(x => x.RoleId, x => x.RoleName);

        //          string? GetEmployeeName(int? employeeId)
        //          {
        //              return employeeId.HasValue &&
        //                     employeeNames.TryGetValue(
        //                         employeeId.Value, out var name)
        //                  ? name
        //                  : null;
        //          }

        //          static JsonElement ReadDetails(string? json)
        //          {
        //              using var document = JsonDocument.Parse(
        //                  string.IsNullOrWhiteSpace(json) ? "{}" : json);

        //              return document.RootElement.Clone();
        //          }

        //          var data = new System.Collections.Generic.List<object>();

        //          foreach (var application in applications)
        //          {
        //              foreach (var skill in companySkills)
        //              {
        //                  var matchingAssignments = assignedSkills
        //                      .Where(x =>
        //                          application.TemplateId == x.TemplateId &&
        //                          x.SkillMasterId == skill.SkillMasterId)
        //                      .ToList();

        //                  if (!isAdmin && matchingAssignments.Count == 0)
        //                  {
        //                      continue;
        //                  }

        //                  var matchingAssessments = assessmentLookup[(
        //                      application.CandidateApplicationId,
        //                      application.JobDescriptionId,
        //                      skill.SkillCategoryId,
        //                      skill.SkillMasterId
        //                  )].ToList();

        //                  if (!isAdmin)
        //                  {
        //                      var allowedAssignmentIds = matchingAssignments
        //                          .Select(x => x.AssignmentId)
        //                          .ToHashSet();

        //                      matchingAssessments = matchingAssessments
        //                          .Where(x =>
        //                              x.Assessment.AssignmentId.HasValue &&
        //                              allowedAssignmentIds.Contains(
        //                                  x.Assessment.AssignmentId.Value))
        //                          .ToList();
        //                  }

        //                  var scopes = isAdmin
        //                      ? new int?[] { null }
        //                      : matchingAssignments
        //                          .Select(x => (int?)x.AssignmentId)
        //                          .Distinct()
        //                          .ToArray();

        //                  foreach (var scopeAssignmentId in scopes)
        //                  {
        //                      var scopeAssessments = matchingAssessments
        //                          .Where(x =>
        //                              isAdmin ||
        //                              x.Assessment.AssignmentId == scopeAssignmentId)
        //                          .ToList();

        //                      foreach (var saved in scopeAssessments.DefaultIfEmpty())
        //                      {
        //                          var assessment = saved?.Assessment;

        //                          int? assignmentId =
        //                              assessment?.AssignmentId ?? scopeAssignmentId;

        //                          int? roleId = null;
        //                          int? hierarchyLevel = null;
        //                          string? roleName = null;

        //                          if (assignmentId.HasValue &&
        //                              assignmentDetails.TryGetValue(
        //                                  assignmentId.Value, out var assignment))
        //                          {
        //                              roleId = assignment.RoleId;
        //                              hierarchyLevel = assignment.HierarchyLevel;

        //                              roleNames.TryGetValue(
        //                                  assignment.RoleId, out roleName);
        //                          }

        //                          int? employeeId = assessment != null
        //                              ? assessment.CreatedBy
        //                              : isAdmin
        //                                  ? (int?)null
        //                                  : request.User;

        //                          var assessmentHistory = historyLookup[
        //                              assessment?.CandidateSkillAssessmentId ?? 0]
        //                              .ToList();

        //                          var remarkHistory = assessmentHistory
        //                              .Where(x => x.IsRemarkAdded)
        //                              .Select(x => new
        //                              {
        //                                  // Existing field retained.
        //                                  HistoryId =
        //                                      x.CandidateSkillAssessmentHistoryId,

        //                                  // Send this ID when editing this row.
        //                                  x.CandidateSkillAssessmentHistoryId,
        //                                  x.CandidateSkillAssessmentId,
        //                                  x.CompanyId,
        //                                  x.JobDescriptionId,
        //                                  x.CompanySkillMasterId,

        //                                  x.Action,
        //                                  x.PreviousStatus,
        //                                  x.NewStatus,
        //                                  x.Score,
        //                                  x.Remarks,
        //                                  x.IsRemarkAdded,

        //                                  x.CreatedBy,
        //                                  CreatedByName =
        //                                      GetEmployeeName(x.CreatedBy),
        //                                  x.CreatedOn,

        //                                  x.ModifiedBy,
        //                                  ModifiedByName =
        //                                      GetEmployeeName(x.ModifiedBy),
        //                                  x.ModifiedOn
        //                              })
        //                              .ToList();

        //                          data.Add(new
        //                          {
        //                              CandidateSkillAssessmentId =
        //                                  assessment?.CandidateSkillAssessmentId,

        //                              request.CompanyId,
        //                              application.CandidateId,

        //                              CandidateName = string.Join(
        //                                  " ",
        //                                  new[]
        //                                  {
        //                              application.FirstName,
        //                              application.MiddleName,
        //                              application.LastName
        //                                  }.Where(x =>
        //                                      !string.IsNullOrWhiteSpace(x))),

        //                              application.CandidateApplicationId,
        //                              application.JobDescriptionId,
        //                              application.JobName,
        //                              application.TemplateId,

        //                              skill.SkillCategoryId,
        //                              skill.SkillCategoryName,

        //                              SkillId = skill.SkillMasterId,
        //                              skill.SkillMasterId,
        //                              skill.SkillName,
        //                              skill.IsDisable,

        //                              AssignmentId = assignmentId,
        //                              RoleId = roleId,
        //                              RoleName = roleName,
        //                              HierarchyLevel = hierarchyLevel,

        //                              EmployeeId = employeeId,
        //                              EmployeeName = GetEmployeeName(employeeId),

        //                              SkillFormDefinitionId =
        //                                  assessment?.SkillFormDefinitionId,
        //                              FormVersion = saved?.FormVersion,

        //                              IsAssessmentSaved = assessment != null,

        //                              // Main assessment values.
        //                              Details = ReadDetails(assessment?.DetailsJson),
        //                              Score = assessment?.Score,
        //                              Remarks = assessment?.Remarks,
        //                              AssessmentStatus =
        //                                  assessment?.Status ?? "Pending",
        //                              IsActive = assessment?.IsActive,

        //                              CreatedBy = assessment?.CreatedBy,
        //                              CreatedByName =
        //                                  GetEmployeeName(assessment?.CreatedBy),
        //                              CreatedOn = assessment?.CreatedOn,

        //                              ModifiedBy = assessment?.ModifiedBy,
        //                              ModifiedByName =
        //                                  GetEmployeeName(assessment?.ModifiedBy),
        //                              ModifiedOn = assessment?.ModifiedOn,

        //                              RemarksCount = remarkHistory.Count,
        //                              RemarkHistory = remarkHistory,

        //                              // Each entry returns its own editable ID and values.
        //                              History = assessmentHistory.Select(x => new
        //                              {
        //                                  HistoryId =
        //                                      x.CandidateSkillAssessmentHistoryId,

        //                                  x.CandidateSkillAssessmentHistoryId,
        //                                  x.CandidateSkillAssessmentId,
        //                                  x.CompanyId,
        //                                  x.CandidateApplicationId,
        //                                  x.JobDescriptionId,
        //                                  x.CompanySkillMasterId,
        //                                  x.AssignmentId,

        //                                  x.Action,
        //                                  x.PreviousStatus,
        //                                  x.NewStatus,
        //                                  x.SkillFormDefinitionId,

        //                                  Details = ReadDetails(x.DetailsJson),

        //                                  x.Score,
        //                                  x.Remarks,
        //                                  x.IsRemarkAdded,

        //                                  x.CreatedBy,
        //                                  CreatedByName =
        //                                      GetEmployeeName(x.CreatedBy),
        //                                  x.CreatedOn,

        //                                  x.ModifiedBy,
        //                                  ModifiedByName =
        //                                      GetEmployeeName(x.ModifiedBy),
        //                                  x.ModifiedOn
        //                              }).ToList()
        //                          });
        //                      }
        //                  }
        //              }
        //          }

        //          return Ok(new
        //          {
        //              Status = true,
        //              Message = data.Count > 0
        //                  ? "Skill assessments fetched successfully."
        //                  : "No assigned skills found for this user and job.",
        //              IsAdmin = isAdmin,
        //              TotalCount = data.Count,
        //              Data = data
        //          });
        //      }
        //      catch (Exception ex)
        //      {
        //          Serilog.Log.Error(
        //              ex,
        //              "Failed to fetch skill assessments for CompanyId {CompanyId}, User {UserId}.",
        //              request.CompanyId,
        //              request.User);

        //          return StatusCode(500, new
        //          {
        //              Status = false,
        //              Message = "Unable to fetch skill assessments."
        //          });
        //      }
        //  }

        [HttpPost("GetCandidateSkillAssessmentList")]
        public async Task<IActionResult> GetCandidateSkillAssessmentList(
    [FromBody] GetCandidateSkillAssessmentListRequest? request)
        {
            if (request == null ||
                request.CompanyId <= 0 ||
                request.User <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Valid CompanyId and User are required."
                });
            }

            if (request.CandidateId < 0 ||
                request.JobDescriptionId < 0 ||
                request.SkillCategoryId < 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Filter IDs cannot be negative."
                });
            }

            try
            {
                var currentUser = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.User)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsAdmin,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (currentUser == null)
                {
                    return NotFound(new
                    {
                        Status = false,
                        Message = "User not found for this company."
                    });
                }

                if (currentUser.IsActive != true)
                {
                    return StatusCode(403, new
                    {
                        Status = false,
                        Message = "User is inactive."
                    });
                }

                bool isAdmin = currentUser.IsAdmin == true;

                // Default Remarks have no category.
                // Include them when no specific category filter is selected.
                bool includeDefaultRemarks = !(request.SkillCategoryId > 0);

                var skillsQuery =
                    from skill in _context.Set<CompanySkillMaster>()
                        .AsNoTracking()

                    join category in _context.Set<CompanySkillMapping>()
                        .AsNoTracking()
                        on new
                        {
                            skill.CompanyId,
                            skill.SkillMappingId
                        }
                        equals new
                        {
                            category.CompanyId,
                            category.SkillMappingId
                        }

                    where skill.CompanyId == request.CompanyId
                          && skill.IsActive == true
                          && category.IsActive == true

                    select new
                    {
                        SkillCategoryId = category.SkillMappingId,
                        SkillCategoryName = category.CategoryName,
                        SkillMasterId = skill.CompanySkillMasterId,
                        skill.SkillName,
                        skill.IsDisable
                    };

                if (request.SkillCategoryId > 0)
                {
                    skillsQuery = skillsQuery.Where(x =>
                        x.SkillCategoryId == request.SkillCategoryId);
                }

                var companySkills = await skillsQuery
                    .OrderBy(x => x.SkillCategoryId)
                    .ThenBy(x => x.SkillMasterId)
                    .ToListAsync();

                var applicationsQuery = _context.CandidateApplications
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId);

                if (request.CandidateId > 0)
                {
                    applicationsQuery = applicationsQuery.Where(x =>
                        x.CandidateId == request.CandidateId);
                }

                if (request.JobDescriptionId > 0)
                {
                    applicationsQuery = applicationsQuery.Where(x =>
                        x.JobDescriptionId == request.JobDescriptionId);
                }

                var applications = await (
                    from application in applicationsQuery

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

                        candidate.CandidateId,
                        candidate.FirstName,
                        candidate.MiddleName,
                        candidate.LastName,

                        JobName = job.JobTitle,
                        TemplateId = (int?)job.TemplateId
                    }
                )
                .OrderByDescending(x => x.CandidateApplicationId)
                .ToListAsync();

                // Do not return early just because there are no company skills.
                // Default Remarks can still be returned.
                if (applications.Count == 0 ||
                    (companySkills.Count == 0 && !includeDefaultRemarks))
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = applications.Count == 0
                            ? "No candidate applications found."
                            : "No active company skills found.",
                        IsAdmin = isAdmin,
                        TotalCount = 0,
                        Data = Array.Empty<object>()
                    });
                }

                var applicationIds = applications
                    .Select(x => x.CandidateApplicationId)
                    .Distinct()
                    .ToArray();

                var templateIds = applications
                    .Where(x => x.TemplateId.HasValue)
                    .Select(x => x.TemplateId!.Value)
                    .Distinct()
                    .ToArray();

                var skillIds = companySkills
                    .Select(x => x.SkillMasterId)
                    .Distinct()
                    .ToArray();

                // Load assignments independently of skill mappings.
                // This allows Default Remarks when no skills are mapped.
                var userAssignments = await (
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

                    where assignment.CompanyId == request.CompanyId
                          && assignment.EmployeeId == request.User
                          && assignment.IsActive == true
                          && template.IsActive == true
                          && templateIds.Contains(assignment.TemplateId)
                          && !isAdmin

                    select new
                    {
                        assignment.TemplateId,
                        assignment.AssignmentId,
                        assignment.EmployeeId,
                        assignment.RoleId,
                        assignment.HierarchyLevel
                    }
                ).Distinct().ToListAsync();

                var userAssignmentIds = userAssignments
                    .Select(x => x.AssignmentId)
                    .Distinct()
                    .ToArray();

                var assignedSkills = await _context.RoleTemplateAssignmentSkills
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        userAssignmentIds.Contains(x.AssignmentId) &&
                        skillIds.Contains(x.SkillId) &&
                        x.IsActive == true)
                    .Select(x => new
                    {
                        x.AssignmentId,
                        x.SkillId
                    })
                    .Distinct()
                    .ToListAsync();

                var assignedSkillPairs = assignedSkills
                    .Select(x => (x.AssignmentId, x.SkillId))
                    .ToHashSet();

                // No inner join to company skills: null skill IDs must survive.
                var assessmentsQuery = _context.CandidateSkillAssessments
       .AsNoTracking()
       .Where(x =>
           x.CompanyId == request.CompanyId &&
           applicationIds.Contains(x.CandidateApplicationId) &&
           (
               (includeDefaultRemarks &&
                x.CompanySkillMasterId == 0)
               ||
               (x.CompanySkillMasterId > 0 &&
                skillIds.Contains(x.CompanySkillMasterId))
           ));

                if (!isAdmin)
                {
                    assessmentsQuery = assessmentsQuery.Where(x =>
                        x.CreatedBy == request.User);
                }

                var savedAssessments = await assessmentsQuery.ToListAsync();
                var assessmentLookup = savedAssessments.ToLookup(x => (
                    x.CandidateApplicationId,
                    x.JobDescriptionId,
                    x.CompanySkillMasterId
                ));

                var assessmentIds = savedAssessments
                    .Select(x => x.CandidateSkillAssessmentId)
                    .Distinct()
                    .ToArray();

                var defaultAssessmentIds = savedAssessments
                    .Where(x => x.CompanySkillMasterId == null)
                    .Select(x => x.CandidateSkillAssessmentId)
                    .ToArray();

                var histories = await _context.CandidateSkillAssessmentHistories
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        assessmentIds.Contains(x.CandidateSkillAssessmentId) &&
                        (
                            x.IsRemarkAdded != true ||
                            defaultAssessmentIds.Contains(
                                x.CandidateSkillAssessmentId)
                        ))
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x =>
                        x.CandidateSkillAssessmentHistoryId)
                    .ToListAsync();

                var historyLookup = histories.ToLookup(x =>
                    x.CandidateSkillAssessmentId);

                var employeeIds = savedAssessments
                    .SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    })
                    .Concat(histories.SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    }))
                    .Concat(new int?[] { request.User })
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
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
                        EmployeeName =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim()
                    })
                    .ToDictionaryAsync(
                        x => x.EmployeeId,
                        x => x.EmployeeName);

                var assignmentIds = savedAssessments
                    .Where(x => x.AssignmentId.HasValue)
                    .Select(x => x.AssignmentId!.Value)
                    .Concat(userAssignmentIds)
                    .Concat(histories
                        .Where(x => x.AssignmentId.HasValue)
                        .Select(x => x.AssignmentId!.Value))
                    .Distinct()
                    .ToArray();

                var assignmentDetails = await _context.RoleTemplateAssignment
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        assignmentIds.Contains(x.AssignmentId))
                    .Select(x => new
                    {
                        x.AssignmentId,
                        x.RoleId,
                        x.HierarchyLevel
                    })
                    .ToDictionaryAsync(x => x.AssignmentId);

                var roleIds = assignmentDetails.Values
                    .Select(x => x.RoleId)
                    .Distinct()
                    .ToArray();

                var roleNames = await _context.Roles
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        roleIds.Contains(x.RoleId))
                    .ToDictionaryAsync(x => x.RoleId, x => x.RoleName);

                string? GetEmployeeName(int? employeeId)
                {
                    return employeeId.HasValue &&
                           employeeNames.TryGetValue(
                               employeeId.Value, out var name)
                        ? name
                        : null;
                }

                static JsonElement ReadDetails(string? json)
                {
                    using var document = JsonDocument.Parse(
                        string.IsNullOrWhiteSpace(json) ? "{}" : json);

                    return document.RootElement.Clone();
                }

                // Build skill rows plus one virtual Default Remark option.
                // This does not insert a company skill.
                var displaySkills = companySkills
                    .Select(x => new
                    {
                        SkillCategoryId = (int?)x.SkillCategoryId,
                        SkillCategoryName = (string?)x.SkillCategoryName,
                        SkillMasterId = (int?)x.SkillMasterId,
                        SkillName = (string?)x.SkillName,
                        IsDisable = (bool?)x.IsDisable,
                        IsDefaultRemark = false
                    })
                    .ToList();

                if (includeDefaultRemarks)
                {
                    displaySkills.Add(new
                    {
                        SkillCategoryId = (int?)null,
                        SkillCategoryName = (string?)null,
                        SkillMasterId = (int?)null,
                        SkillName = (string?)"Default Remark",
                        IsDisable = (bool?)false,
                        IsDefaultRemark = true
                    });
                }

                var data = new List<object>();

                foreach (var application in applications)
                {
                    foreach (var skill in displaySkills)
                    {
                        var matchingAssignments = userAssignments
                            .Where(x =>
                                application.TemplateId == x.TemplateId &&
                                (
                                    skill.IsDefaultRemark ||
                                    (
                                        skill.SkillMasterId.HasValue &&
                                        assignedSkillPairs.Contains((
                                            x.AssignmentId,
                                            skill.SkillMasterId.Value))
                                    )
                                ))
                            .ToList();

                        if (!isAdmin && matchingAssignments.Count == 0)
                            continue;

                        var matchingAssessments = assessmentLookup[(
        application.CandidateApplicationId,
        application.JobDescriptionId,
        skill.SkillMasterId.GetValueOrDefault()
    )].ToList();
                        if (!isAdmin)
                        {
                            var allowedAssignmentIds = matchingAssignments
                                .Select(x => x.AssignmentId)
                                .ToHashSet();

                            matchingAssessments = matchingAssessments
                                .Where(x =>
                                    x.AssignmentId.HasValue &&
                                    allowedAssignmentIds.Contains(
                                        x.AssignmentId.Value))
                                .ToList();
                        }

                        var scopes = isAdmin
                            ? new int?[] { null }
                            : matchingAssignments
                                .Select(x => (int?)x.AssignmentId)
                                .Distinct()
                                .ToArray();

                        foreach (var scopeAssignmentId in scopes)
                        {
                            var scopeAssessments = matchingAssessments
                                .Where(x =>
                                    isAdmin ||
                                    x.AssignmentId == scopeAssignmentId)
                                .ToList();

                            // Return a Pending row when nothing has been saved.
                            foreach (var assessment in scopeAssessments.DefaultIfEmpty())
                            {
                                int? assignmentId =
                                    assessment?.AssignmentId ?? scopeAssignmentId;

                                int? roleId = null;
                                int? hierarchyLevel = null;
                                string? roleName = null;

                                if (assignmentId.HasValue &&
                                    assignmentDetails.TryGetValue(
                                        assignmentId.Value, out var assignment))
                                {
                                    roleId = assignment.RoleId;
                                    hierarchyLevel = assignment.HierarchyLevel;

                                    roleNames.TryGetValue(
                                        assignment.RoleId, out roleName);
                                }

                                int? employeeId = assessment != null
                                    ? assessment.CreatedBy
                                    : isAdmin
                                        ? (int?)null
                                        : request.User;

                                var assessmentHistory = historyLookup[
                                    assessment?.CandidateSkillAssessmentId ?? 0]
                                    .ToList();

                                var remarkHistory = assessmentHistory
                                    .Where(x => x.IsRemarkAdded)
                                    .Select(x => new
                                    {
                                        HistoryId =
                                            x.CandidateSkillAssessmentHistoryId,

                                        x.CandidateSkillAssessmentHistoryId,
                                        x.CandidateSkillAssessmentId,
                                        x.CompanyId,
                                        x.JobDescriptionId,
                                        x.CompanySkillMasterId,
                                        x.AssignmentId,

                                        IsDefaultRemark =
                                            x.CompanySkillMasterId == null,

                                        x.Action,
                                        x.PreviousStatus,
                                        x.NewStatus,
                                        x.Score,
                                        x.Remarks,
                                        x.IsRemarkAdded,

                                        x.CreatedBy,
                                        CreatedByName =
                                            GetEmployeeName(x.CreatedBy),
                                        x.CreatedOn,

                                        x.ModifiedBy,
                                        ModifiedByName =
                                            GetEmployeeName(x.ModifiedBy),
                                        x.ModifiedOn
                                    })
                                    .ToList();

                                data.Add(new
                                {
                                    CandidateSkillAssessmentId =
                                        assessment?.CandidateSkillAssessmentId,

                                    request.CompanyId,
                                    application.CandidateId,

                                    CandidateName = string.Join(
                                        " ",
                                        new[]
                                        {
                                    application.FirstName,
                                    application.MiddleName,
                                    application.LastName
                                        }.Where(x =>
                                            !string.IsNullOrWhiteSpace(x))),

                                    application.CandidateApplicationId,
                                    application.JobDescriptionId,
                                    application.JobName,
                                    application.TemplateId,

                                    skill.SkillCategoryId,
                                    skill.SkillCategoryName,

                                    SkillId = skill.SkillMasterId,
                                    skill.SkillMasterId,
                                    CompanySkillMasterId = skill.SkillMasterId,
                                    skill.SkillName,
                                    skill.IsDisable,
                                    skill.IsDefaultRemark,

                                    AssignmentId = assignmentId,
                                    RoleId = roleId,
                                    RoleName = roleName,
                                    HierarchyLevel = hierarchyLevel,

                                    EmployeeId = employeeId,
                                    EmployeeName = GetEmployeeName(employeeId),

                                    SkillFormDefinitionId =
                                        assessment?.SkillFormDefinitionId,
                                    FormVersion = (int?)null,

                                    IsAssessmentSaved = assessment != null,

                                    Details = ReadDetails(assessment?.DetailsJson),
                                    Score = assessment?.Score,
                                    Remarks = assessment?.Remarks,
                                    AssessmentStatus =
                                        assessment?.Status ?? "Pending",
                                    IsActive = assessment?.IsActive,

                                    CreatedBy = assessment?.CreatedBy,
                                    CreatedByName =
                                        GetEmployeeName(assessment?.CreatedBy),
                                    CreatedOn = assessment?.CreatedOn,

                                    ModifiedBy = assessment?.ModifiedBy,
                                    ModifiedByName =
                                        GetEmployeeName(assessment?.ModifiedBy),
                                    ModifiedOn = assessment?.ModifiedOn,

                                    RemarksCount = remarkHistory.Count,
                                    RemarkHistory = remarkHistory,

                                    History = assessmentHistory.Select(x => new
                                    {
                                        HistoryId =
                                            x.CandidateSkillAssessmentHistoryId,

                                        x.CandidateSkillAssessmentHistoryId,
                                        x.CandidateSkillAssessmentId,
                                        x.CompanyId,
                                        x.CandidateApplicationId,
                                        x.JobDescriptionId,
                                        x.CompanySkillMasterId,
                                        x.AssignmentId,

                                        IsDefaultRemark =
                                            x.CompanySkillMasterId == null,

                                        x.Action,
                                        x.PreviousStatus,
                                        x.NewStatus,
                                        x.SkillFormDefinitionId,

                                        Details = ReadDetails(x.DetailsJson),

                                        x.Score,
                                        x.Remarks,
                                        x.IsRemarkAdded,

                                        x.CreatedBy,
                                        CreatedByName =
                                            GetEmployeeName(x.CreatedBy),
                                        x.CreatedOn,

                                        x.ModifiedBy,
                                        ModifiedByName =
                                            GetEmployeeName(x.ModifiedBy),
                                        x.ModifiedOn
                                    }).ToList()
                                });
                            }
                        }
                    }
                }

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Skill assessments fetched successfully."
                        : "No assigned skills or default remark access found for this user and job.",
                    IsAdmin = isAdmin,
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch skill assessments for CompanyId {CompanyId}, User {UserId}.",
                    request.CompanyId,
                    request.User);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch skill assessments."
                });
            }
        }

        [HttpPost("GetApproveRejectSkillAssessmentList")]
        public async Task<IActionResult> GetApproveRejectSkillAssessmentList(
    [FromBody] GetApproveRejectSkillAssessmentListRequest? request)
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
                request.CandidateId <= 0 ||
                request.JobDescriptionId <= 0 ||
                request.CompanySkillMasterId <= 0 ||
                request.User <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId, CandidateId, JobDescriptionId, CompanySkillMasterId and User are required.");
            }

            try
            {
                var currentUser = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.User)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsAdmin,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (currentUser == null)
                    return Fail(404, "User not found for this company.");

                if (currentUser.IsActive != true)
                    return Fail(403, "User is inactive.");

                bool isAdmin = currentUser.IsAdmin == true;

                var assessmentsQuery = _context.CandidateSkillAssessments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.JobDescriptionId == request.JobDescriptionId &&
                        x.CompanySkillMasterId == request.CompanySkillMasterId);

                if (!isAdmin)
                {
                    assessmentsQuery = assessmentsQuery.Where(x =>
                        x.CreatedBy == request.User);
                }

                var query =
                    from assessment in assessmentsQuery

                    join application in _context.CandidateApplications
                        .AsNoTracking()
                        on new
                        {
                            assessment.CompanyId,
                            assessment.CandidateApplicationId,
                            assessment.JobDescriptionId
                        }
                        equals new
                        {
                            application.CompanyId,
                            application.CandidateApplicationId,
                            application.JobDescriptionId
                        }

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
                            assessment.CompanyId,
                            assessment.JobDescriptionId
                        }
                        equals new
                        {
                            job.CompanyId,
                            job.JobDescriptionId
                        }

                    join skill in _context.Set<CompanySkillMaster>()
                        .AsNoTracking()
                        on new
                        {
                            assessment.CompanyId,
                            assessment.CompanySkillMasterId
                        }
                        equals new
                        {
                            skill.CompanyId,
                            skill.CompanySkillMasterId
                        }

                    join category in _context.Set<CompanySkillMapping>()
                        .AsNoTracking()
                        on new
                        {
                            skill.CompanyId,
                            skill.SkillMappingId
                        }
                        equals new
                        {
                            category.CompanyId,
                            category.SkillMappingId
                        }

                    where application.CandidateId == request.CandidateId
                          && skill.IsActive == true
                          && category.IsActive == true

                    select new
                    {
                        Assessment = assessment,
                        candidate.CandidateId,
                        candidate.FirstName,
                        candidate.MiddleName,
                        candidate.LastName,
                        JobName = job.JobTitle,
                        job.TemplateId,
                        skill.SkillName,
                        skill.IsDisable,
                        SkillCategoryId = category.SkillMappingId,
                        SkillCategoryName = category.CategoryName
                    };

                if (!isAdmin)
                {
                    query = query.Where(row =>
                        _context.RoleTemplateAssignment.Any(assignment =>
                            assignment.CompanyId == request.CompanyId &&
                            assignment.EmployeeId == request.User &&
                            assignment.AssignmentId ==
                                row.Assessment.AssignmentId &&
                            assignment.TemplateId == row.TemplateId &&
                            assignment.IsActive == true &&

                            _context.RoleTemplates.Any(template =>
                                template.CompanyId == assignment.CompanyId &&
                                template.TemplateId == assignment.TemplateId &&
                                template.IsActive == true) &&

                            _context.RoleTemplateAssignmentSkills.Any(mapping =>
                                mapping.CompanyId == assignment.CompanyId &&
                                mapping.AssignmentId == assignment.AssignmentId &&
                                mapping.SkillId ==
                                    row.Assessment.CompanySkillMasterId)));
                }

                var assessments = await query.ToListAsync();

                var assessmentIds = assessments
                    .Select(x => x.Assessment.CandidateSkillAssessmentId)
                    .Distinct()
                    .ToArray();

                // Return every approval/rejection event, including later edits.
                var histories = await _context.CandidateSkillAssessmentHistories
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId && x.IsRemarkAdded == true &&
                        assessmentIds.Contains(x.CandidateSkillAssessmentId) &&
                        (x.Action == "Approve" || x.Action == "Reject"))
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x =>
                        x.CandidateSkillAssessmentHistoryId)
                    .ToListAsync();

                var assessmentLookup = assessments.ToDictionary(x =>
                    x.Assessment.CandidateSkillAssessmentId);

                var employeeIds = assessments
                    .SelectMany(x => new int?[]
                    {
                x.Assessment.CreatedBy,
                x.Assessment.ModifiedBy
                    })
                    .Concat(histories.Select(x => (int?)x.CreatedBy))
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
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
                    .ToDictionaryAsync(x => x.EmployeeId, x => x.Name);

                var assignmentIds = assessments
                    .Where(x => x.Assessment.AssignmentId.HasValue)
                    .Select(x => x.Assessment.AssignmentId!.Value)
                    .Concat(
                        histories
                            .Where(x => x.AssignmentId.HasValue)
                            .Select(x => x.AssignmentId!.Value))
                    .Distinct()
                    .ToArray();

                var assignments = await _context.RoleTemplateAssignment
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        assignmentIds.Contains(x.AssignmentId))
                    .Select(x => new
                    {
                        x.AssignmentId,
                        x.RoleId,
                        x.HierarchyLevel
                    })
                    .ToDictionaryAsync(x => x.AssignmentId);

                var roleIds = assignments.Values
                    .Select(x => x.RoleId)
                    .Distinct()
                    .ToArray();

                var roleNames = await _context.Roles
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        roleIds.Contains(x.RoleId))
                    .ToDictionaryAsync(x => x.RoleId, x => x.RoleName);

                string? GetEmployeeName(int? employeeId)
                {
                    return employeeId.HasValue &&
                           employeeNames.TryGetValue(
                               employeeId.Value, out var name)
                        ? name
                        : null;
                }

                var data = histories.Select(history =>
                {
                    var row = assessmentLookup[
                        history.CandidateSkillAssessmentId];

                    var assessment = row.Assessment;

                    int? roleId = null;
                    int? hierarchyLevel = null;
                    string? roleName = null;

                    if (history.AssignmentId.HasValue &&
                        assignments.TryGetValue(
                            history.AssignmentId.Value, out var assignment))
                    {
                        roleId = assignment.RoleId;
                        hierarchyLevel = assignment.HierarchyLevel;
                        roleNames.TryGetValue(assignment.RoleId, out roleName);
                    }

                    return new
                    {
                        HistoryId = history.CandidateSkillAssessmentHistoryId,
                        assessment.CandidateSkillAssessmentId,
                        assessment.CompanyId,

                        row.CandidateId,

                        CandidateName = string.Join(
                            " ",
                            new[]
                            {
                        row.FirstName,
                        row.MiddleName,
                        row.LastName
                            }.Where(x => !string.IsNullOrWhiteSpace(x))),

                        assessment.CandidateApplicationId,
                        assessment.JobDescriptionId,
                        row.JobName,

                        assessment.CompanySkillMasterId,
                        SkillId = assessment.CompanySkillMasterId,
                        row.SkillName,
                        row.SkillCategoryId,
                        row.SkillCategoryName,
                        row.IsDisable,

                        history.AssignmentId,
                        RoleId = roleId,
                        RoleName = roleName,
                        HierarchyLevel = hierarchyLevel,

                        // This event's actor, decision, score and remark.
                        EmployeeId = history.CreatedBy,
                        EmployeeName = GetEmployeeName(history.CreatedBy),

                        history.Action,
                        history.PreviousStatus,
                        history.NewStatus,
                        history.Score,
                        history.Remarks,
                        history.IsRemarkAdded,

                        // History creation identifies when this action occurred.
                        history.CreatedBy,
                        CreatedByName = GetEmployeeName(history.CreatedBy),
                        history.CreatedOn,

                        // Current assessment values are separate from the event.
                        CurrentAssessmentStatus = assessment.Status,
                        CurrentScore = assessment.Score,
                        CurrentRemarks = assessment.Remarks,
                        assessment.IsActive,

                        AssessmentCreatedBy = assessment.CreatedBy,
                        AssessmentCreatedByName =
                            GetEmployeeName(assessment.CreatedBy),
                        AssessmentCreatedOn = assessment.CreatedOn,

                        // Latest update to the main assessment.
                        ModifiedBy = assessment.ModifiedBy,
                        ModifiedByName =
                            GetEmployeeName(assessment.ModifiedBy),
                        ModifiedOn = assessment.ModifiedOn
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Approval/rejection history fetched successfully."
                        : "No approval/rejection history found.",
                    IsAdmin = isAdmin,
                    TotalCount = data.Count,

                    // Event counts, including repeated decisions and edits.
                    ApproveActionCount =
                        histories.Count(x => x.Action == "Approve"),
                    RejectActionCount =
                        histories.Count(x => x.Action == "Reject"),
                    RemarksCount =
                        histories.Count(x => x.IsRemarkAdded),

                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch approval/rejection history for CompanyId {CompanyId}, CandidateId {CandidateId}, JobDescriptionId {JobDescriptionId}, CompanySkillMasterId {CompanySkillMasterId}.",
                    request.CompanyId,
                    request.CandidateId,
                    request.JobDescriptionId,
                    request.CompanySkillMasterId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch approval/rejection history."
                });
            }
        }


        //    [HttpPost("GetApproveRejectSkillAssessmentList")]
        //    public async Task<IActionResult> GetApproveRejectSkillAssessmentList(
        //[FromBody] GetApproveRejectSkillAssessmentListRequest? request)
        //    {
        //        IActionResult Fail(int code, string message)
        //        {
        //            return StatusCode(code, new
        //            {
        //                Status = false,
        //                Message = message
        //            });
        //        }

        //        if (request == null ||
        //            request.CompanyId <= 0 ||
        //            request.CandidateId <= 0 ||
        //            request.JobDescriptionId <= 0 ||
        //            request.CompanySkillMasterId <= 0 ||
        //            request.User <= 0)
        //        {
        //            return Fail(
        //                400,
        //                "Valid CompanyId, CandidateId, JobDescriptionId, CompanySkillMasterId and User are required.");
        //        }

        //        try
        //        {
        //            var currentUser = await _context.EmployeeMasters
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.EmployeeId == request.User)
        //                .Select(x => new
        //                {
        //                    x.EmployeeId,
        //                    x.IsAdmin,
        //                    x.IsActive
        //                })
        //                .FirstOrDefaultAsync();

        //            if (currentUser == null)
        //                return Fail(404, "User not found for this company.");

        //            if (currentUser.IsActive != true)
        //                return Fail(403, "User is inactive.");

        //            bool isAdmin = currentUser.IsAdmin == true;

        //            var assessmentsQuery = _context.CandidateSkillAssessments
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.JobDescriptionId == request.JobDescriptionId &&
        //                    x.CompanySkillMasterId == request.CompanySkillMasterId);

        //            if (!isAdmin)
        //            {
        //                assessmentsQuery = assessmentsQuery.Where(x =>
        //                    x.CreatedBy == request.User);
        //            }

        //            var query =
        //                from assessment in assessmentsQuery

        //                join application in _context.CandidateApplications
        //                    .AsNoTracking()
        //                    on new
        //                    {
        //                        assessment.CompanyId,
        //                        assessment.CandidateApplicationId,
        //                        assessment.JobDescriptionId
        //                    }
        //                    equals new
        //                    {
        //                        application.CompanyId,
        //                        application.CandidateApplicationId,
        //                        application.JobDescriptionId
        //                    }

        //                join candidate in _context.Candidates.AsNoTracking()
        //                    on new
        //                    {
        //                        application.CompanyId,
        //                        application.CandidateId
        //                    }
        //                    equals new
        //                    {
        //                        candidate.CompanyId,
        //                        candidate.CandidateId
        //                    }

        //                join job in _context.JobDescriptions.AsNoTracking()
        //                    on new
        //                    {
        //                        assessment.CompanyId,
        //                        assessment.JobDescriptionId
        //                    }
        //                    equals new
        //                    {
        //                        job.CompanyId,
        //                        job.JobDescriptionId
        //                    }

        //                join skill in _context.Set<CompanySkillMaster>()
        //                    .AsNoTracking()
        //                    on new
        //                    {
        //                        assessment.CompanyId,
        //                        assessment.CompanySkillMasterId
        //                    }
        //                    equals new
        //                    {
        //                        skill.CompanyId,
        //                        skill.CompanySkillMasterId
        //                    }

        //                join category in _context.Set<CompanySkillMapping>()
        //                    .AsNoTracking()
        //                    on new
        //                    {
        //                        skill.CompanyId,
        //                        skill.SkillMappingId
        //                    }
        //                    equals new
        //                    {
        //                        category.CompanyId,
        //                        category.SkillMappingId
        //                    }

        //                where application.CandidateId == request.CandidateId
        //                      && skill.IsActive == true
        //                      && category.IsActive == true

        //                select new
        //                {
        //                    Assessment = assessment,
        //                    candidate.CandidateId,
        //                    candidate.FirstName,
        //                    candidate.MiddleName,
        //                    candidate.LastName,
        //                    JobName = job.JobTitle,
        //                    job.TemplateId,
        //                    skill.SkillName,
        //                    skill.IsDisable,
        //                    SkillCategoryId = category.SkillMappingId,
        //                    SkillCategoryName = category.CategoryName
        //                };

        //            if (!isAdmin)
        //            {
        //                query = query.Where(row =>
        //                    _context.RoleTemplateAssignment.Any(assignment =>
        //                        assignment.CompanyId == request.CompanyId &&
        //                        assignment.EmployeeId == request.User &&
        //                        assignment.AssignmentId ==
        //                            row.Assessment.AssignmentId &&
        //                        assignment.TemplateId == row.TemplateId &&
        //                        assignment.IsActive == true &&

        //                        _context.RoleTemplates.Any(template =>
        //                            template.CompanyId == assignment.CompanyId &&
        //                            template.TemplateId == assignment.TemplateId &&
        //                            template.IsActive == true) &&

        //                        _context.RoleTemplateAssignmentSkills.Any(mapping =>
        //                            mapping.CompanyId == assignment.CompanyId &&
        //                            mapping.AssignmentId == assignment.AssignmentId &&
        //                            mapping.SkillId ==
        //                                row.Assessment.CompanySkillMasterId)));
        //            }

        //            var assessments = await query.ToListAsync();

        //            var assessmentIds = assessments
        //                .Select(x => x.Assessment.CandidateSkillAssessmentId)
        //                .Distinct()
        //                .ToArray();

        //            // Return every approval/rejection event, including later edits.
        //            var histories = await _context.CandidateSkillAssessmentHistories
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId && x.IsRemarkAdded == true &&
        //                    assessmentIds.Contains(x.CandidateSkillAssessmentId) &&
        //                    (x.Action == "Approve" || x.Action == "Reject"))
        //                .OrderByDescending(x => x.CreatedOn)
        //                .ThenByDescending(x =>
        //                    x.CandidateSkillAssessmentHistoryId)
        //                .ToListAsync();

        //            var assessmentLookup = assessments.ToDictionary(x =>
        //                x.Assessment.CandidateSkillAssessmentId);

        //            var employeeIds = assessments
        //                .SelectMany(x => new int?[]
        //                {
        //            x.Assessment.CreatedBy,
        //            x.Assessment.ModifiedBy
        //                })
        //                .Concat(histories.Select(x => (int?)x.CreatedBy))
        //                .Where(x => x.HasValue)
        //                .Select(x => x!.Value)
        //                .Distinct()
        //                .ToArray();

        //            var employeeNames = await _context.EmployeeMasters
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    employeeIds.Contains(x.EmployeeId))
        //                .Select(x => new
        //                {
        //                    x.EmployeeId,
        //                    Name =
        //                        ((x.FirstName ?? "") + " " +
        //                         (x.LastName ?? "")).Trim()
        //                })
        //                .ToDictionaryAsync(x => x.EmployeeId, x => x.Name);

        //            var assignmentIds = assessments
        //                .Where(x => x.Assessment.AssignmentId.HasValue)
        //                .Select(x => x.Assessment.AssignmentId!.Value)
        //                .Concat(
        //                    histories
        //                        .Where(x => x.AssignmentId.HasValue)
        //                        .Select(x => x.AssignmentId!.Value))
        //                .Distinct()
        //                .ToArray();

        //            var assignments = await _context.RoleTemplateAssignment
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    assignmentIds.Contains(x.AssignmentId))
        //                .Select(x => new
        //                {
        //                    x.AssignmentId,
        //                    x.RoleId,
        //                    x.HierarchyLevel
        //                })
        //                .ToDictionaryAsync(x => x.AssignmentId);

        //            var roleIds = assignments.Values
        //                .Select(x => x.RoleId)
        //                .Distinct()
        //                .ToArray();

        //            var roleNames = await _context.Roles
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    roleIds.Contains(x.RoleId))
        //                .ToDictionaryAsync(x => x.RoleId, x => x.RoleName);

        //            string? GetEmployeeName(int? employeeId)
        //            {
        //                return employeeId.HasValue &&
        //                       employeeNames.TryGetValue(
        //                           employeeId.Value, out var name)
        //                    ? name
        //                    : null;
        //            }

        //            var data = histories.Select(history =>
        //            {
        //                var row = assessmentLookup[
        //                    history.CandidateSkillAssessmentId];

        //                var assessment = row.Assessment;

        //                int? roleId = null;
        //                int? hierarchyLevel = null;
        //                string? roleName = null;

        //                if (history.AssignmentId.HasValue &&
        //                    assignments.TryGetValue(
        //                        history.AssignmentId.Value, out var assignment))
        //                {
        //                    roleId = assignment.RoleId;
        //                    hierarchyLevel = assignment.HierarchyLevel;
        //                    roleNames.TryGetValue(assignment.RoleId, out roleName);
        //                }

        //                return new
        //                {
        //                    HistoryId = history.CandidateSkillAssessmentHistoryId,
        //                    assessment.CandidateSkillAssessmentId,
        //                    assessment.CompanyId,

        //                    row.CandidateId,

        //                    CandidateName = string.Join(
        //                        " ",
        //                        new[]
        //                        {
        //                    row.FirstName,
        //                    row.MiddleName,
        //                    row.LastName
        //                        }.Where(x => !string.IsNullOrWhiteSpace(x))),

        //                    assessment.CandidateApplicationId,
        //                    assessment.JobDescriptionId,
        //                    row.JobName,

        //                    assessment.CompanySkillMasterId,
        //                    SkillId = assessment.CompanySkillMasterId,
        //                    row.SkillName,
        //                    row.SkillCategoryId,
        //                    row.SkillCategoryName,
        //                    row.IsDisable,

        //                    history.AssignmentId,
        //                    RoleId = roleId,
        //                    RoleName = roleName,
        //                    HierarchyLevel = hierarchyLevel,

        //                    // This event's actor, decision, score and remark.
        //                    EmployeeId = history.CreatedBy,
        //                    EmployeeName = GetEmployeeName(history.CreatedBy),

        //                    history.Action,
        //                    history.PreviousStatus,
        //                    history.NewStatus,
        //                    history.Score,
        //                    history.Remarks,
        //                    history.IsRemarkAdded,

        //                    // History creation identifies when this action occurred.
        //                    history.CreatedBy,
        //                    CreatedByName = GetEmployeeName(history.CreatedBy),
        //                    history.CreatedOn,

        //                    // Current assessment values are separate from the event.
        //                    CurrentAssessmentStatus = assessment.Status,
        //                    CurrentScore = assessment.Score,
        //                    CurrentRemarks = assessment.Remarks,
        //                    assessment.IsActive,

        //                    AssessmentCreatedBy = assessment.CreatedBy,
        //                    AssessmentCreatedByName =
        //                        GetEmployeeName(assessment.CreatedBy),
        //                    AssessmentCreatedOn = assessment.CreatedOn,

        //                    // Latest update to the main assessment.
        //                    ModifiedBy = assessment.ModifiedBy,
        //                    ModifiedByName =
        //                        GetEmployeeName(assessment.ModifiedBy),
        //                    ModifiedOn = assessment.ModifiedOn
        //                };
        //            }).ToList();

        //            return Ok(new
        //            {
        //                Status = true,
        //                Message = data.Count > 0
        //                    ? "Approval/rejection history fetched successfully."
        //                    : "No approval/rejection history found.",
        //                IsAdmin = isAdmin,
        //                TotalCount = data.Count,

        //                // Event counts, including repeated decisions and edits.
        //                ApproveActionCount =
        //                    histories.Count(x => x.Action == "Approve"),
        //                RejectActionCount =
        //                    histories.Count(x => x.Action == "Reject"),
        //                RemarksCount =
        //                    histories.Count(x => x.IsRemarkAdded),

        //                Data = data
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            Serilog.Log.Error(
        //                ex,
        //                "Failed to fetch approval/rejection history for CompanyId {CompanyId}, CandidateId {CandidateId}, JobDescriptionId {JobDescriptionId}, CompanySkillMasterId {CompanySkillMasterId}.",
        //                request.CompanyId,
        //                request.CandidateId,
        //                request.JobDescriptionId,
        //                request.CompanySkillMasterId);

        //            return StatusCode(500, new
        //            {
        //                Status = false,
        //                Message = "Unable to fetch approval/rejection history."
        //            });
        //        }
        //    }

        public class GetApproveRejectSkillAssessmentListRequest
        {
            public int CompanyId { get; set; }
            public int CandidateId { get; set; }
            public int JobDescriptionId { get; set; }
            public int CompanySkillMasterId { get; set; }

            // EmployeeMasters.EmployeeId
            public int User { get; set; }
        }














        public class GetCandidateSkillAssessmentListRequest
        {
            public int CompanyId { get; set; }

            public int CandidateId { get; set; }

            public int JobDescriptionId { get; set; }

            public int SkillCategoryId { get; set; }

            // EmployeeMasters.EmployeeId
            public int User { get; set; }
        }

        public class UpdateSkillAssessmentStatusRequest
        {
            public int CompanyId { get; set; }

            public int CandidateId { get; set; }

            public int JobDescriptionId { get; set; }

            public int? CompanySkillMasterId { get; set; }

            public int EmployeeId { get; set; }

            // 0: Find matching assessment or create one.
            public int CandidateSkillAssessmentId { get; set; }

            // 0: Add history. > 0: Edit only this history row.
            public int CandidateSkillAssessmentHistoryId { get; set; }

            // Approve or Reject.
            public string? Action { get; set; }

            public decimal? Score { get; set; }

            public string? Remarks { get; set; }
        }


        public class SaveCandidateSkillAssessmentRequest
        {
            public int CandidateSkillAssessmentId { get; set; }

            // 0 = normal save; > 0 = edit this history row only.
            public int CandidateSkillAssessmentHistoryId { get; set; }

            public int CompanyId { get; set; }
            public int CandidateId { get; set; }

            public int? JobDescriptionId { get; set; }
            public int? EmployeeId { get; set; }
            public int? SkillId { get; set; }

            public string? Action { get; set; } = "Save";
            public bool IsResume { get; set; } = false;
            public JsonElement? Details { get; set; }
            public decimal? Score { get; set; }
            public string? Remarks { get; set; }
            public bool IsRemarkAdded { get; set; }
        }
    }
}
