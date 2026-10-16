using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using Serilog;
using System.Data;

namespace Recruitment.Controllers
{
    [Route("api/RoleAssignmentController")]
    [ApiController]
    public class RoleAssignmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public RoleAssignmentController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }

      
         

            

           
            private ObjectResult Fail(int statusCode, string message)
            {
                return StatusCode(statusCode, new
                {
                    Status = false,
                    Message = message
                });
            }

    //    [HttpPost("save")]
    //    public async Task<IActionResult> SaveTemplate(  [FromBody] SaveRoleTemplateRequest? request)
    //    {
    //        if (request == null)
    //            return Fail(400, "Invalid request.");

    //        if (request.TemplateId < 0)
    //            return Fail(400, "Invalid TemplateId.");

    //        if (request.CompanyId <= 0)
    //            return Fail(400, "Valid CompanyId is required.");

    //        bool isNew = request.TemplateId == 0;

    //        var templateName = request.TemplateName?.Trim();
    //        var area = request.Area?.Trim();

    //        var auditUserId = isNew
    //            ? request.CreatedBy
    //            : request.ModifiedBy;

    //        if (string.IsNullOrWhiteSpace(templateName))
    //            return Fail(400, "Template name is required.");

    //        if (templateName.Length > 150)
    //            return Fail(400, "Template name cannot exceed 150 characters.");

    //        if (area?.Length > 200)
    //            return Fail(400, "Area cannot exceed 200 characters.");

    //        if (auditUserId.HasValue && auditUserId.Value <= 0)
    //            return Fail(400, "Invalid creator or modifier user ID.");

    //        if (request.BranchIds == null ||
    //            request.BranchIds.Count == 0 ||
    //            request.BranchIds.Any(id => id <= 0))
    //        {
    //            return Fail(400, "Select at least one valid branch.");
    //        }

    //        if (request.BranchIds.Distinct().Count() != request.BranchIds.Count)
    //            return Fail(400, "Duplicate branch IDs are not allowed.");

    //        if (request.Assignments == null || request.Assignments.Count == 0)
    //            return Fail(400, "At least one role assignment is required.");

    //        foreach (var row in request.Assignments)
    //        {
    //            if (row == null)
    //                return Fail(400, "Assignment cannot be null.");

    //            row.Stage = row.Stage?.Trim();

    //            if (row.AssignmentId < 0)
    //                return Fail(400, "Invalid AssignmentId.");

    //            if (isNew && row.AssignmentId != 0)
    //                return Fail(400, "New templates must use AssignmentId = 0.");

    //            if (row.RoleId <= 0 || row.EmployeeId <= 0)
    //                return Fail(400, "Valid role and employee are required.");

    //            if (row.ReportingToEmployeeId.HasValue &&
    //                row.ReportingToEmployeeId.Value <0)
    //            {
    //                return Fail(
    //                    400,
    //                    "ReportingToEmployeeId must be a valid employee ID or null.");
    //            }

    //            if (row.HierarchyLevel <= 0)
    //                return Fail(400, "Hierarchy levels must be greater than zero.");

    //            if (string.IsNullOrWhiteSpace(row.Stage) ||
    //                row.Stage.Length > 100)
    //            {
    //                return Fail(
    //                    400,
    //                    "Stage is required and cannot exceed 100 characters.");
    //            }

    //            if (row.ApprovalDays <= 0)
    //                return Fail(400, "Approval days must be greater than zero.");

    //            if (row.SkillIds == null || row.SkillIds.Any(id => id <= 0))
    //                return Fail(400, "Invalid skill IDs.");

    //            if (row.SkillIds.Distinct().Count() != row.SkillIds.Count)
    //            {
    //                return Fail(
    //                    400,
    //                    $"Duplicate skills are not allowed for employee {row.EmployeeId}.");
    //            }
    //        }

    //        if (request.Assignments
    //            .GroupBy(x => x.EmployeeId)
    //            .Any(group => group.Count() > 1))
    //        {
    //            return Fail(
    //                400,
    //                "EmployeeId cannot repeat when reporting is identified by employee ID.");
    //        }

    //        if (request.Assignments
    //            .Where(x => x.AssignmentId > 0)
    //            .GroupBy(x => x.AssignmentId)
    //            .Any(group => group.Count() > 1))
    //        {
    //            return Fail(400, "AssignmentId cannot repeat.");
    //        }

    //        // Preserved from your supplied code.
    //        var duplicateLevels = request.Assignments
    //            .GroupBy(x => x.HierarchyLevel)
    //            .Where(group => group.Count() > 1)
    //            .Select(group => group.Key)
    //            .OrderBy(level => level)
    //            .ToList();

    //        if (duplicateLevels.Count > 0)
    //        {
    //            return Fail(
    //                409,
    //                "Duplicate hierarchy levels: " +
    //                string.Join(", ", duplicateLevels));
    //        }

    //        var requestRows = request.Assignments
    //            .ToDictionary(x => x.EmployeeId);

    //        // Prevent self-reporting and loops within this template.
    //        // Stop when the reporting employee is outside the template.
    //        foreach (var row in request.Assignments)
    //        {
    //            var visited = new HashSet<int>();
    //            int? currentEmployeeId = row.EmployeeId;

    //            while (currentEmployeeId.HasValue)
    //            {
    //                if (!visited.Add(currentEmployeeId.Value))
    //                {
    //                    return Fail(
    //                        400,
    //                        "Reporting relationships cannot contain a loop.");
    //                }

    //                if (!requestRows.TryGetValue(
    //                        currentEmployeeId.Value,
    //                        out var currentRow))
    //                {
    //                    break;
    //                }

    //                currentEmployeeId = currentRow.ReportingToEmployeeId;
    //            }
    //        }

    //        try
    //        {
    //            await using var transaction =
    //                await _context.Database.BeginTransactionAsync(
    //                    IsolationLevel.Serializable);

    //            var companyExists = await _context.Companys
    //                .AnyAsync(x => x.CompanyID == request.CompanyId);

    //            if (!companyExists)
    //                return Fail(404, "Company not found.");

    //            RoleTemplate template;

    //            List<RoleTemplateAssignments> existingAssignments = new();

    //            if (isNew)
    //            {
    //                template = new RoleTemplate
    //                {
    //                    CompanyId = request.CompanyId
    //                };
    //            }
    //            else
    //            {
    //                var existingTemplate = await _context.RoleTemplates
    //                    .FirstOrDefaultAsync(x =>
    //                        x.TemplateId == request.TemplateId &&
    //                        x.CompanyId == request.CompanyId);

    //                if (existingTemplate == null)
    //                    return Fail(404, "Template not found for this company.");

    //                template = existingTemplate;

    //                var savedBranchIds = await _context.RoleTemplateBranches
    //                    .Where(x =>
    //                        x.TemplateId == template.TemplateId &&
    //                        x.CompanyId == request.CompanyId)
    //                    .Select(x => x.BranchId)
    //                    .ToListAsync();

    //                if (!savedBranchIds.ToHashSet().SetEquals(request.BranchIds))
    //                {
    //                    return Fail(
    //                        409,
    //                        "Template branches cannot be changed after creation.");
    //                }

    //                existingAssignments = await _context.RoleTemplateAssignment
    //                    .Where(x =>
    //                        x.TemplateId == template.TemplateId &&
    //                        x.CompanyId == request.CompanyId)
    //                    .ToListAsync();
    //            }

    //            var duplicateName = await _context.RoleTemplates.AnyAsync(x =>
    //                x.CompanyId == request.CompanyId &&
    //                x.TemplateId != request.TemplateId &&
    //                EF.Functions.Collate(
    //                    x.TemplateName.Trim(),
    //                    "SQL_Latin1_General_CP1_CI_AS") == templateName);

    //            if (duplicateName)
    //                return Fail(409, "Template name already exists for this company.");

    //            var branches = await _context.Branches
    //                .AsNoTracking()
    //                .Where(x =>
    //                    request.BranchIds.Contains(x.BranchId) &&
    //                    x.CompanyId == request.CompanyId)
    //                .Select(x => new
    //                {
    //                    x.BranchId,
    //                    x.City,
    //                    x.IsActive
    //                })
    //                .ToListAsync();

    //            if (branches.Count != request.BranchIds.Count)
    //            {
    //                return Fail(
    //                    400,
    //                    "One or more branches do not belong to this company.");
    //            }

    //            if (isNew && branches.Any(x => x.IsActive != true))
    //                return Fail(400, "Only active branches can be selected.");

    //            if (request.Cities?.Count > 0)
    //            {
    //                var selectedCities = request.Cities
    //                    .Where(x => !string.IsNullOrWhiteSpace(x))
    //                    .Select(x => x.Trim())
    //                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

    //                if (branches.Any(x =>
    //                    string.IsNullOrWhiteSpace(x.City) ||
    //                    !selectedCities.Contains(x.City.Trim())))
    //                {
    //                    return Fail(
    //                        400,
    //                        "Selected branches must belong to the selected cities.");
    //                }
    //            }

    //            Dictionary<int, RoleTemplateAssignments> existingById =
    //                existingAssignments.ToDictionary(x => x.AssignmentId);

    //            HashSet<int> submittedIds = request.Assignments
    //                .Where(x => x.AssignmentId > 0)
    //                .Select(x => x.AssignmentId)
    //                .ToHashSet();

    //            if (submittedIds.Any(id => !existingById.ContainsKey(id)))
    //            {
    //                return Fail(
    //                    400,
    //                    "An assignment does not belong to this template.");
    //            }

    //            if (existingAssignments.Any(x =>
    //                !submittedIds.Contains(x.AssignmentId)))
    //            {
    //                return Fail(
    //                    400,
    //                    "Send all existing assignments. Removing rows is not supported by this API.");
    //            }

    //            var roleIds = request.Assignments
    //                .Select(x => x.RoleId)
    //                .Distinct()
    //                .ToList();

    //            var validRoleCount = await _context.Roles.CountAsync(x =>
    //                roleIds.Contains(x.RoleId) &&
    //                x.CompanyId == request.CompanyId &&
    //                x.IsActive == true);

    //            if (validRoleCount != roleIds.Count)
    //            {
    //                return Fail(
    //                    400,
    //                    "Select active roles belonging to this company.");
    //            }

    //            var employeeIds = request.Assignments
    //                .Select(x => x.EmployeeId)
    //                .Distinct()
    //                .ToList();

    //            var validEmployeeCount = await _context.EmployeeMasters
    //                .CountAsync(x =>
    //                    employeeIds.Contains(x.EmployeeId) &&
    //                    x.CompanyId == request.CompanyId);

    //            if (validEmployeeCount != employeeIds.Count)
    //            {
    //                return Fail(
    //                    400,
    //                    "One or more employees do not belong to this company.");
    //            }

    //            // Reporting employees do not need an assignment in this template.
    //            // They must exist in the same company.
    //            foreach (var row in request.Assignments)
    //            {
    //                if (row.ReportingToEmployeeId == 0)
    //                {
    //                    row.ReportingToEmployeeId = null;
    //                }
    //            }

    //            var reportingEmployeeIds = request.Assignments
    //                .Where(x =>
    //                    x.ReportingToEmployeeId.HasValue &&
    //                    x.ReportingToEmployeeId.Value > 0)
    //                .Select(x => x.ReportingToEmployeeId!.Value)
    //                .Distinct()
    //                .ToList();

    //            if (reportingEmployeeIds.Count > 0)
    //            {
    //                var validReportingEmployeeIds = await _context.EmployeeMasters
    //                    .Where(x =>
    //                        x.CompanyId == request.CompanyId &&
    //                        reportingEmployeeIds.Contains(x.EmployeeId))
    //                    .Select(x => x.EmployeeId)
    //                    .ToListAsync();

    //                var invalidReportingEmployeeIds = reportingEmployeeIds
    //                    .Except(validReportingEmployeeIds)
    //                    .ToList();

    //                if (invalidReportingEmployeeIds.Count > 0)
    //                {
    //                    return Fail(
    //                        400,
    //                        "Reporting EmployeeId(s) do not belong to this company: " +
    //                        string.Join(", ", invalidReportingEmployeeIds));
    //                }
    //            }

    //            var skillIds = request.Assignments
    //                .SelectMany(x => x.SkillIds)
    //                .Distinct()
    //                .ToList();

    //            var validSkillCount = await (
    //                from skill in _context.CompanySkillMasters
    //                join category in _context.CompanySkillMappings
    //                    on skill.SkillMappingId equals category.SkillMappingId
    //                where skillIds.Contains(skill.CompanySkillMasterId)
    //                      && skill.IsActive == true
    //                      && category.IsActive == true
    //                select skill.CompanySkillMasterId
    //            ).CountAsync();

    //            if (validSkillCount != skillIds.Count)
    //            {
    //                return Fail(
    //                    400,
    //                    "Select active skills from active categories.");
    //            }

    //            var now = DateTime.UtcNow;

    //            template.TemplateName = templateName;
    //            template.IsActive = request.IsActive;

    //            if (isNew)
    //            {
    //                template.CreatedOn = now;
    //                template.CreatedBy = request.CreatedBy;

    //                _context.RoleTemplates.Add(template);
    //            }
    //            else
    //            {
    //                template.ModifiedOn = now;
    //                template.ModifiedBy = request.ModifiedBy;
    //            }

    //            await _context.SaveChangesAsync();

    //            if (isNew)
    //            {
    //                foreach (var branchId in request.BranchIds)
    //                {
    //                    _context.RoleTemplateBranches.Add(
    //                        new RoleTemplateBranch
    //                        {
    //                            CompanyId = template.CompanyId,
    //                            TemplateId = template.TemplateId,
    //                            BranchId = branchId
    //                        });
    //                }
    //            }

    //            // Preserved hierarchy-swap handling.
    //            foreach (var assignment in existingAssignments)
    //            {
    //                assignment.HierarchyLevel = -assignment.AssignmentId;
    //            }

    //            if (existingAssignments.Count > 0)
    //                await _context.SaveChangesAsync();

    //            Dictionary<int, RoleTemplateAssignments> savedRows = new();

    //            foreach (var row in request.Assignments)
    //            {
    //                RoleTemplateAssignments assignment;

    //                if (row.AssignmentId == 0)
    //                {
    //                    assignment = new RoleTemplateAssignments
    //                    {
    //                        CompanyId = template.CompanyId,
    //                        TemplateId = template.TemplateId,
    //                        CreatedOn = now,
    //                        CreatedBy = auditUserId
    //                    };

    //                    _context.RoleTemplateAssignment.Add(assignment);
    //                }
    //                else
    //                {
    //                    assignment = existingById[row.AssignmentId];
    //                    assignment.ModifiedOn = now;
    //                    assignment.ModifiedBy = request.ModifiedBy;
    //                }

    //                assignment.RoleId = row.RoleId;
    //                assignment.EmployeeId = row.EmployeeId;
    //                assignment.HierarchyLevel = row.HierarchyLevel;
    //                assignment.CanApprove = row.CanApprove;
    //                assignment.CanReject = row.CanReject;
    //                assignment.CanReturn = row.CanReturn;
    //                assignment.Stage = row.Stage!;
    //                assignment.ApprovalDays = row.ApprovalDays;
    //                assignment.IsActive = row.IsActive;

    //                savedRows.Add(row.EmployeeId, assignment);
    //            }

    //            await _context.SaveChangesAsync();

    //            var savedAssignmentIds = savedRows.Values
    //                .Select(x => x.AssignmentId)
    //                .ToList();

    //            var existingSkills = await _context.RoleTemplateAssignmentSkills
    //                .Where(x =>
    //                    x.CompanyId == template.CompanyId &&
    //                    savedAssignmentIds.Contains(x.AssignmentId))
    //                .ToListAsync();

    //            foreach (var row in request.Assignments)
    //            {
    //                var assignment = savedRows[row.EmployeeId];

    //                // Always store the reporting employee ID directly.
    //                assignment.ReportingToAssignmentId = row.ReportingToEmployeeId;

    //                // Preserve the old assignment link when the reporting
    //                // employee also has an assignment in this template.
    //                assignment.ReportingToAssignmentId =
    //                    row.ReportingToEmployeeId.HasValue &&
    //                    savedRows.TryGetValue(
    //                        row.ReportingToEmployeeId.Value,
    //                        out var reportingAssignment)
    //                        ? reportingAssignment.AssignmentId
    //                        : null;

    //                var selectedSkills = row.SkillIds.ToHashSet();

    //                var currentSkills = existingSkills
    //                    .Where(x => x.AssignmentId == assignment.AssignmentId)
    //                    .ToList();

    //                foreach (var oldSkill in currentSkills)
    //                {
    //                    if (!selectedSkills.Contains(oldSkill.SkillId))
    //                    {
    //                        _context.RoleTemplateAssignmentSkills.Remove(oldSkill);
    //                    }
    //                }

    //                var currentSkillIds = currentSkills
    //                    .Select(x => x.SkillId)
    //                    .ToHashSet();

    //                foreach (var skillId in selectedSkills)
    //                {
    //                    if (!currentSkillIds.Contains(skillId))
    //                    {
    //                        _context.RoleTemplateAssignmentSkills.Add(
    //                            new RoleTemplateAssignmentSkill
    //                            {
    //                                CompanyId = template.CompanyId,
    //                                AssignmentId = assignment.AssignmentId,
    //                                SkillId = skillId
    //                            });
    //                    }
    //                }
    //            }
    //            var employeesToUpdate = await _context.EmployeeMasters
    //.AsTracking()
    //.Where(x =>
    //    x.CompanyId == request.CompanyId &&
    //    employeeIds.Contains(x.EmployeeId))
    //.ToDictionaryAsync(x => x.EmployeeId);

    //            foreach (var row in request.Assignments)
    //            {
    //                employeesToUpdate[row.EmployeeId].RoleId = row.RoleId;
    //            }

    //            await _context.SaveChangesAsync();
    //            await transaction.CommitAsync();

    //            return Ok(new
    //            {
    //                Status = true,
    //                Message = isNew
    //                    ? "Template created successfully."
    //                    : "Template updated successfully.",
    //                TemplateId = template.TemplateId,
    //                Assignments = savedRows
    //                    .OrderBy(x => x.Value.HierarchyLevel)
    //                    .Select(x => new
    //                    {
    //                        EmployeeId = x.Key,
    //                        AssignmentId = x.Value.AssignmentId,
    //                        HierarchyLevel = x.Value.HierarchyLevel,
    //                        ReportingToEmployeeId =
    //                            requestRows[x.Key].ReportingToEmployeeId
    //                    })
    //                    .ToList()
    //            });
    //        }
    //        catch (DbUpdateException ex)
    //            when (ex.InnerException is SqlException sql &&
    //                  (sql.Number == 2601 || sql.Number == 2627))
    //        {
    //            return Fail(
    //                409,
    //                "Duplicate template name, hierarchy level, or assignment mapping.");
    //        }
    //        catch (DbUpdateException ex)
    //            when (ex.InnerException is SqlException sql &&
    //                  sql.Number == 547)
    //        {
    //            return Fail(
    //                409,
    //                "Related records changed or a database constraint was violated.");
    //        }
    //        catch (Exception ex)
    //        {
    //            Log.Error(
    //                ex,
    //                "Failed to save role template for company {CompanyId}",
    //                request.CompanyId);

    //            return Fail(
    //                500,
    //                "Unable to save the template. Please try again.");
    //        }
    //    }

        [HttpGet("Templatelist")]
            public async Task<IActionResult> GetTemplateList(  [FromQuery] int companyId)
            {
                if (companyId <= 0)
                    return Fail(400, "Valid CompanyId is required.");

                try
                {
                    var companyExists = await _context.Companys
                        .AnyAsync(x => x.CompanyID == companyId);

                    if (!companyExists)
                        return Fail(404, "Company not found.");

                    var templates = await _context.RoleTemplates
                        .AsNoTracking()
                        .Where(x => x.CompanyId == companyId)
                        .OrderByDescending(x => x.TemplateId)
                        .Select(x => new
                        {
                            x.TemplateId,
                            x.TemplateName,
                            x.CompanyId,
                           
                            x.IsActive,
                            x.CreatedOn,
                            x.CreatedBy,
                            x.ModifiedOn,
                            x.ModifiedBy,

                            Branches = (
                                from mapping in _context.RoleTemplateBranches
                                join branch in _context.Branches
                                    on mapping.BranchId equals branch.BranchId
                                where mapping.TemplateId == x.TemplateId
                                      && mapping.CompanyId == companyId
                                      && branch.CompanyId == companyId
                                orderby branch.BranchName
                                select new
                                {
                                    mapping.TemplateBranchId,
                                    branch.BranchId,
                                    branch.BranchName,
                                    branch.City
                                }
                            ).ToList()
                        })
                        .ToListAsync();

                    return Ok(new
                    {
                        Status = true,
                        Message = templates.Count > 0
                            ? "Template list fetched successfully."
                            : "No templates found.",
                        Data = templates
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(
                        ex,
                        "Failed to fetch templates for company {CompanyId}",
                        companyId);

                    return Fail(500, "Unable to fetch template list.");
                }
            }



        [HttpPost("GetTemplateFlow")]
        public async Task<IActionResult> GetTemplateFlow(
    [FromBody] GetTemplateFlowRequest? request)
        {
            // Local helper adds StatusCode without changing your shared Fail method.
            IActionResult FlowFail(int statusCode, string message)
            {
                return StatusCode(statusCode, new
                {
                    StatusCode = statusCode,
                    Status = false,
                    Message = message
                });
            }

            if (request == null || request.CompanyId <= 0)
                return FlowFail(400, "Valid CompanyId is required.");

            bool allTemplates =
                request.TemplateIds == null ||
                request.TemplateIds.Count == 0 ||
                request.TemplateIds.Contains(0);

            var templateIds = allTemplates
                ? new List<int>()
                : request.TemplateIds!.Distinct().ToList();

            try
            {
                var company = await _context.Companys
                    .AsNoTracking()
                    .Where(x => x.CompanyID == request.CompanyId)
                    .Select(x => new
                    {
                        x.CompanyID,
                        x.CompanyName
                    })
                    .FirstOrDefaultAsync();

                if (company == null)
                    return FlowFail(404, "Company not found.");

                var templateQuery = _context.RoleTemplates
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId);

                if (!allTemplates)
                {
                    templateQuery = templateQuery.Where(x =>
                        templateIds.Contains(x.TemplateId));
                }

                var templates = await templateQuery
                    .OrderByDescending(x => x.TemplateId)
                    .Select(x => new
                    {
                        x.TemplateId,
                        x.TemplateName,
                        x.CompanyId,
                        x.IsActive
                    })
                    .ToListAsync();

                if (!allTemplates && templates.Count != templateIds.Count)
                {
                    return FlowFail(
                        404,
                        "One or more templates were not found for this company.");
                }

                // Use the fetched template IDs for the remaining queries.
                templateIds = templates
                    .Select(x => x.TemplateId)
                    .ToList();

                if (templates.Count == 0)
                {
                    return Ok(new
                    {
                        StatusCode = 200,
                        Status = true,
                        Message = "Template flows fetched successfully.",
                        Data = Array.Empty<object>()
                    });
                }

                var branches = await (
                    from mapping in _context.RoleTemplateBranches.AsNoTracking()
                    join branch in _context.Branches.AsNoTracking()
                        on new
                        {
                            mapping.CompanyId,
                            mapping.BranchId
                        }
                        equals new
                        {
                            branch.CompanyId,
                            branch.BranchId
                        }
                    where mapping.CompanyId == request.CompanyId
                          && templateIds.Contains(mapping.TemplateId)
                    orderby branch.BranchName
                    select new
                    {
                        mapping.TemplateId,
                        mapping.TemplateBranchId,
                        branch.BranchId,
                        branch.BranchName,
                        branch.City
                    }
                ).ToListAsync();

                var assignments = await _context.RoleTemplateAssignment
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        templateIds.Contains(x.TemplateId))
                    .OrderBy(x => x.HierarchyLevel)
                    .ThenBy(x => x.AssignmentId)
                    .Select(x => new
                    {
                        x.TemplateId,
                        x.AssignmentId,
                        x.RoleId,

                        RoleName = _context.Roles
                            .Where(r =>
                                r.RoleId == x.RoleId &&
                                r.CompanyId == request.CompanyId)
                            .Select(r => r.RoleName)
                            .FirstOrDefault(),

                        x.EmployeeId,

                        EmployeeName = _context.EmployeeMasters
                            .Where(e =>
                                e.EmployeeId == x.EmployeeId &&
                                e.CompanyId == request.CompanyId)
                            .Select(e =>
                                ((e.FirstName ?? "") + " " +
                                 (e.LastName ?? "")).Trim())
                            .FirstOrDefault(),

                        x.HierarchyLevel,
                        x.ReportingToAssignmentId,
                        x.CanApprove,
                        x.CanReject,
                        x.CanReturn,
                        x.Stage,
                        x.ApprovalDays,
                        x.IsActive
                    })
                    .ToListAsync();

                var assignmentIds = assignments
                    .Select(x => x.AssignmentId)
                    .ToList();

                // Correct relationship:
                // AssignmentSkills.SkillId -> CompanySkillMasterId
                // CompanySkillMasters.SkillMappingId -> CompanySkillMappings.SkillMappingId
                var skills = await (
                    from mapping in _context.RoleTemplateAssignmentSkills
                        .AsNoTracking()
                    join skill in _context.CompanySkillMasters.AsNoTracking()
                        on mapping.SkillId equals skill.CompanySkillMasterId
                    join categoryMapping in _context.CompanySkillMappings
                        .AsNoTracking()
                        on skill.SkillMappingId equals categoryMapping.SkillMappingId
                    where mapping.CompanyId == request.CompanyId
                          && skill.CompanyId == request.CompanyId
                          && categoryMapping.CompanyId == request.CompanyId
                          && assignmentIds.Contains(mapping.AssignmentId)
                    orderby categoryMapping.CategoryName, skill.SkillName
                    select new
                    {
                        mapping.AssignmentId,
                        skill.CompanySkillMasterId,
                        skill.SkillName,

                        // Preserve the existing response field names.
                        CategoryId = categoryMapping.SkillCategoryId,
                        CategoryName = categoryMapping.CategoryName,
                        SkillIsActive = skill.IsActive,
                        CategoryIsActive = categoryMapping.IsActive
                    }
                ).ToListAsync();

                var assignmentLookup = assignments
                    .ToDictionary(x => x.AssignmentId);

                // Reporting targets must belong to the SAME template.
                foreach (var assignment in assignments)
                {
                    if (!assignment.ReportingToAssignmentId.HasValue)
                        continue;

                    if (!assignmentLookup.TryGetValue(
                            assignment.ReportingToAssignmentId.Value,
                            out var parent) ||
                        parent.TemplateId != assignment.TemplateId)
                    {
                        return FlowFail(
                            409,
                            $"Invalid reporting relationship in template {assignment.TemplateId}.");
                    }
                }

                // Validate saved reporting chains.
                foreach (var assignment in assignments)
                {
                    var visited = new HashSet<int>();
                    int? currentId = assignment.AssignmentId;

                    while (currentId.HasValue)
                    {
                        if (!visited.Add(currentId.Value))
                        {
                            return FlowFail(
                                409,
                                $"Circular reporting relationship in template {assignment.TemplateId}.");
                        }

                        currentId = assignmentLookup[currentId.Value]
                            .ReportingToAssignmentId;
                    }
                }

                var branchesByTemplate = branches
                    .ToLookup(x => x.TemplateId);

                var assignmentsByTemplate = assignments
                    .ToLookup(x => x.TemplateId);

                var skillsByAssignment = skills
                    .ToLookup(x => x.AssignmentId);

                var data = templates.Select(template =>
                {
                    var templateBranches =
                        branchesByTemplate[template.TemplateId]
                            .Select(x => new
                            {
                                x.TemplateBranchId,
                                x.BranchId,
                                x.BranchName,
                                x.City
                            })
                            .ToList();

                    var templateAssignments =
                        assignmentsByTemplate[template.TemplateId]
                            .OrderBy(x => x.HierarchyLevel)
                            .ThenBy(x => x.AssignmentId)
                            .ToList();

                    var cities = templateBranches
                        .Where(x => !string.IsNullOrWhiteSpace(x.City))
                        .Select(x => x.City!.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList();

                    var nodes = templateAssignments.Select(assignment =>
                    {
                        var reportingTo =
                            assignment.ReportingToAssignmentId.HasValue
                                ? assignmentLookup[
                                    assignment.ReportingToAssignmentId.Value]
                                : null;

                        var selectedSkills =
                            skillsByAssignment[assignment.AssignmentId]
                                .Select(skill => new
                                {
                                    skill.CompanySkillMasterId,
                                    skill.SkillName,
                                    skill.CategoryId,
                                    skill.CategoryName,
                                    skill.SkillIsActive,
                                    skill.CategoryIsActive
                                })
                                .ToList();

                        return new
                        {
                            assignment.AssignmentId,
                            assignment.RoleId,
                            assignment.RoleName,
                            assignment.EmployeeId,
                            assignment.EmployeeName,
                            assignment.HierarchyLevel,

                            HierarchyLabel = "H" + assignment.HierarchyLevel,

                            assignment.ReportingToAssignmentId,
                            ReportingToRoleName = reportingTo?.RoleName,
                            ReportingToEmployeeId = reportingTo?.EmployeeId,
                            ReportingToEmployeeName = reportingTo?.EmployeeName,

                            assignment.CanApprove,
                            assignment.CanReject,
                            assignment.CanReturn,
                            assignment.Stage,
                            assignment.ApprovalDays,
                            assignment.IsActive,

                            SkillCount = selectedSkills.Count,
                            Skills = selectedSkills
                        };
                    }).ToList();

                    var connections = templateAssignments
                        .Where(x => x.ReportingToAssignmentId.HasValue)
                        .Select(x => new
                        {
                            FromAssignmentId = x.AssignmentId,
                            ToAssignmentId = x.ReportingToAssignmentId!.Value,
                            ConnectionType = "ReportsTo"
                        })
                        .ToList();

                    return new
                    {
                        template.TemplateId,
                        template.TemplateName,
                        template.CompanyId,
                        CompanyName = company.CompanyName,
                        template.IsActive,
                        BranchesLocked = true,
                        Cities = cities,
                        Branches = templateBranches,
                        AssignmentCount = nodes.Count,
                        Nodes = nodes,
                        Connections = connections
                    };
                }).ToList();

                return Ok(new
                {
                    StatusCode = 200,
                    Status = true,
                    Message = "Template flows fetched successfully.",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch template flows for CompanyId {CompanyId}",
                    request.CompanyId);

                return FlowFail(500, "Unable to fetch template flows.");
            }
        }

        //[HttpPost("GetTemplateFlow")]
        //public async Task<IActionResult> GetTemplateFlow([FromBody] GetTemplateFlowRequest? request)
        //{
        //    if (request == null || request.CompanyId <= 0)
        //        return Fail(400, "Valid CompanyId is required.");


        //    bool allTemplates =
        //        request.TemplateIds == null ||
        //        request.TemplateIds.Count == 0 ||
        //        request.TemplateIds.Contains(0);

        //    var templateIds = allTemplates
        //        ? new List<int>()
        //        : request.TemplateIds!.Distinct().ToList();

        //    try
        //    {
        //        var company = await _context.Companys
        //            .AsNoTracking()
        //            .Where(x => x.CompanyID == request.CompanyId)
        //            .Select(x => new
        //            {
        //                x.CompanyID,
        //                x.CompanyName
        //            })
        //            .FirstOrDefaultAsync();

        //        if (company == null)
        //            return Fail(404, "Company not found.");

        //        var templateQuery = _context.RoleTemplates
        //            .AsNoTracking()
        //            .Where(x => x.CompanyId == request.CompanyId);

        //        if (!allTemplates)
        //        {
        //            templateQuery = templateQuery.Where(x =>
        //                templateIds.Contains(x.TemplateId));
        //        }

        //        var templates = await templateQuery
        //            .OrderByDescending(x => x.TemplateId)
        //            .Select(x => new
        //            {
        //                x.TemplateId,
        //                x.TemplateName,
        //                x.CompanyId,
        //                x.IsActive
        //            })
        //            .ToListAsync();

        //        if (!allTemplates && templates.Count != templateIds.Count)
        //        {
        //            return Fail(
        //                404,
        //                "One or more templates were not found for this company.");
        //        }

        //        // Use the fetched template IDs for the remaining queries.
        //        templateIds = templates
        //            .Select(x => x.TemplateId)
        //            .ToList();

        //        if (templates.Count == 0)
        //        {
        //            return Ok(new
        //            {
        //                Status = true,
        //                Message = "Template flows fetched successfully.",
        //                Data = Array.Empty<object>()
        //            });
        //        }

        //        var branches = await (
        //            from mapping in _context.RoleTemplateBranches.AsNoTracking()
        //            join branch in _context.Branches.AsNoTracking()
        //                on new
        //                {
        //                    mapping.CompanyId,
        //                    mapping.BranchId
        //                }
        //                equals new
        //                {
        //                    branch.CompanyId,
        //                    branch.BranchId
        //                }
        //            where mapping.CompanyId == request.CompanyId
        //                  && templateIds.Contains(mapping.TemplateId)
        //            orderby branch.BranchName
        //            select new
        //            {
        //                mapping.TemplateId,
        //                mapping.TemplateBranchId,
        //                branch.BranchId,
        //                branch.BranchName,
        //                branch.City
        //            }
        //        ).ToListAsync();

        //        var assignments = await _context.RoleTemplateAssignment
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                templateIds.Contains(x.TemplateId))
        //            .OrderBy(x => x.HierarchyLevel)
        //            .ThenBy(x => x.AssignmentId)
        //            .Select(x => new
        //            {
        //                x.TemplateId,
        //                x.AssignmentId,
        //                x.RoleId,

        //                RoleName = _context.Roles
        //                    .Where(r =>
        //                        r.RoleId == x.RoleId &&
        //                        r.CompanyId == request.CompanyId)
        //                    .Select(r => r.RoleName)
        //                    .FirstOrDefault(),

        //                x.EmployeeId,

        //                EmployeeName = _context.EmployeeMasters
        //                    .Where(e =>
        //                        e.EmployeeId == x.EmployeeId &&
        //                        e.CompanyId == request.CompanyId)
        //                    .Select(e =>
        //                        ((e.FirstName ?? "") + " " +
        //                         (e.LastName ?? "")).Trim())
        //                    .FirstOrDefault(),

        //                x.HierarchyLevel,
        //                x.ReportingToAssignmentId,
        //                x.CanApprove,
        //                x.CanReject,
        //                x.CanReturn,
        //                x.Stage,
        //                x.ApprovalDays,
        //                x.IsActive
        //            })
        //            .ToListAsync();

        //        var assignmentIds = assignments
        //            .Select(x => x.AssignmentId)
        //            .ToList();

        //        var skills = await (
        //            from mapping in _context.RoleTemplateAssignmentSkills
        //                .AsNoTracking()
        //            join skill in _context.CompanySkillMasters.AsNoTracking()
        //                on mapping.SkillId equals skill.CompanySkillMasterId
        //            join category in _context.SkillCategories.AsNoTracking()
        //                on skill.CompanySkillMasterId equals category.CategoryId
        //            where mapping.CompanyId == request.CompanyId
        //                  && assignmentIds.Contains(mapping.AssignmentId)
        //            orderby category.CategoryName, skill.SkillName
        //            select new
        //            {
        //                mapping.AssignmentId,
        //                skill.CompanySkillMasterId,
        //                skill.SkillName,
        //                category.CategoryId,
        //                category.CategoryName,
        //                SkillIsActive = skill.IsActive,
        //                CategoryIsActive = category.IsActive
        //            }
        //        ).ToListAsync();

        //        var assignmentLookup = assignments
        //            .ToDictionary(x => x.AssignmentId);

        //        // Reporting targets must belong to the SAME template.
        //        foreach (var assignment in assignments)
        //        {
        //            if (!assignment.ReportingToAssignmentId.HasValue)
        //                continue;

        //            if (!assignmentLookup.TryGetValue(
        //                    assignment.ReportingToAssignmentId.Value,
        //                    out var parent) ||
        //                parent.TemplateId != assignment.TemplateId)
        //            {
        //                return Fail(
        //                    409,
        //                    $"Invalid reporting relationship in template {assignment.TemplateId}.");
        //            }
        //        }

        //        // Validate saved reporting chains.
        //        foreach (var assignment in assignments)
        //        {
        //            var visited = new HashSet<int>();
        //            int? currentId = assignment.AssignmentId;

        //            while (currentId.HasValue)
        //            {
        //                if (!visited.Add(currentId.Value))
        //                {
        //                    return Fail(
        //                        409,
        //                        $"Circular reporting relationship in template {assignment.TemplateId}.");
        //                }

        //                currentId = assignmentLookup[currentId.Value]
        //                    .ReportingToAssignmentId;
        //            }
        //        }

        //        var branchesByTemplate = branches
        //            .ToLookup(x => x.TemplateId);

        //        var assignmentsByTemplate = assignments
        //            .ToLookup(x => x.TemplateId);

        //        var skillsByAssignment = skills
        //            .ToLookup(x => x.AssignmentId);

        //        var data = templates.Select(template =>
        //        {
        //            var templateBranches = branchesByTemplate[template.TemplateId]
        //                .Select(x => new
        //                {
        //                    x.TemplateBranchId,
        //                    x.BranchId,
        //                    x.BranchName,
        //                    x.City
        //                })
        //                .ToList();

        //            var templateAssignments =
        //                assignmentsByTemplate[template.TemplateId]
        //                    .OrderBy(x => x.HierarchyLevel)
        //                    .ThenBy(x => x.AssignmentId)
        //                    .ToList();

        //            var cities = templateBranches
        //                .Where(x => !string.IsNullOrWhiteSpace(x.City))
        //                .Select(x => x.City!.Trim())
        //                .Distinct(StringComparer.OrdinalIgnoreCase)
        //                .OrderBy(x => x)
        //                .ToList();

        //            var nodes = templateAssignments.Select(assignment =>
        //            {
        //                var reportingTo =
        //                    assignment.ReportingToAssignmentId.HasValue
        //                        ? assignmentLookup[
        //                            assignment.ReportingToAssignmentId.Value]
        //                        : null;

        //                var selectedSkills =
        //                    skillsByAssignment[assignment.AssignmentId]
        //                        .Select(skill => new
        //                        {
        //                            skill.CompanySkillMasterId,
        //                            skill.SkillName,
        //                            skill.CategoryId,
        //                            skill.CategoryName,
        //                            skill.SkillIsActive,
        //                            skill.CategoryIsActive
        //                        })
        //                        .ToList();

        //                return new
        //                {
        //                    assignment.AssignmentId,
        //                    assignment.RoleId,
        //                    assignment.RoleName,
        //                    assignment.EmployeeId,
        //                    assignment.EmployeeName,
        //                    assignment.HierarchyLevel,

        //                    HierarchyLabel = "H" + assignment.HierarchyLevel,

        //                    assignment.ReportingToAssignmentId,
        //                    ReportingToRoleName = reportingTo?.RoleName,
        //                    ReportingToEmployeeId = reportingTo?.EmployeeId,
        //                    ReportingToEmployeeName = reportingTo?.EmployeeName,

        //                    assignment.CanApprove,
        //                    assignment.CanReject,
        //                    assignment.CanReturn,
        //                    assignment.Stage,
        //                    assignment.ApprovalDays,
        //                    assignment.IsActive,

        //                    SkillCount = selectedSkills.Count,
        //                    Skills = selectedSkills
        //                };
        //            }).ToList();

        //            var connections = templateAssignments
        //                .Where(x => x.ReportingToAssignmentId.HasValue)
        //                .Select(x => new
        //                {
        //                    FromAssignmentId = x.AssignmentId,
        //                    ToAssignmentId = x.ReportingToAssignmentId!.Value,
        //                    ConnectionType = "ReportsTo"
        //                })
        //                .ToList();

        //            return new
        //            {
        //                template.TemplateId,
        //                template.TemplateName,
        //                template.CompanyId,
        //                CompanyName = company.CompanyName,
        //                template.IsActive,
        //                BranchesLocked = true,
        //                Cities = cities,
        //                Branches = templateBranches,
        //                AssignmentCount = nodes.Count,
        //                Nodes = nodes,
        //                Connections = connections
        //            };
        //        }).ToList();

        //        return Ok(new
        //        {
        //            Status = true,
        //            Message = "Template flows fetched successfully.",
        //            Data = data
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Serilog.Log.Error(
        //            ex,
        //            "Failed to fetch template flows for CompanyId {CompanyId}",
        //            request.CompanyId);

        //        return Fail(500, "Unable to fetch template flows.");
        //    }
        //}


        [HttpPost("save")]
        public async Task<IActionResult> SaveTemplate(
         [FromBody] SaveRoleTemplateRequest? request)
        {
            if (request == null)
                return Fail(400, "Invalid request.");

            if (request.TemplateId < 0)
                return Fail(400, "Invalid TemplateId.");

            if (request.CompanyId <= 0)
                return Fail(400, "Valid CompanyId is required.");

            bool isNew = request.TemplateId == 0;

            var templateName = request.TemplateName?.Trim();
            var area = request.Area?.Trim();

            var auditUserId = isNew
                ? request.CreatedBy
                : request.ModifiedBy;

            if (string.IsNullOrWhiteSpace(templateName))
                return Fail(400, "Template name is required.");

            if (templateName.Length > 150)
                return Fail(400, "Template name cannot exceed 150 characters.");

            if (area?.Length > 200)
                return Fail(400, "Area cannot exceed 200 characters.");

            if (auditUserId.HasValue && auditUserId.Value <= 0)
                return Fail(400, "Invalid creator or modifier user ID.");

            if (request.BranchIds == null ||
                request.BranchIds.Count == 0 ||
                request.BranchIds.Any(id => id <= 0))
            {
                return Fail(400, "Select at least one valid branch.");
            }

            if (request.BranchIds.Distinct().Count() != request.BranchIds.Count)
                return Fail(400, "Duplicate branch IDs are not allowed.");

            if (request.Assignments == null || request.Assignments.Count == 0)
                return Fail(400, "At least one role assignment is required.");

            foreach (var row in request.Assignments)
            {
                if (row == null)
                    return Fail(400, "Assignment cannot be null.");

                row.Stage = row.Stage?.Trim();

                if (row.AssignmentId < 0)
                    return Fail(400, "Invalid AssignmentId.");

                if (isNew && row.AssignmentId != 0)
                    return Fail(400, "New templates must use AssignmentId = 0.");

                if (row.RoleId <= 0 || row.EmployeeId <= 0)
                    return Fail(400, "Valid role and employee are required.");

                if (row.ReportingToEmployeeId.HasValue &&
                    row.ReportingToEmployeeId.Value < 0)
                {
                    return Fail(
                        400,
                        "ReportingToEmployeeId must be a valid employee ID or null.");
                }

                if (row.HierarchyLevel <= 0)
                    return Fail(400, "Hierarchy levels must be greater than zero.");

                if (string.IsNullOrWhiteSpace(row.Stage) ||
                    row.Stage.Length > 100)
                {
                    return Fail(
                        400,
                        "Stage is required and cannot exceed 100 characters.");
                }

                //if (row.ApprovalDays <= 0)
                //    return Fail(400, "Approval days must be greater than zero.");

                // Skills are optional.
                // Null, omitted, [], and [0] mean no selected skills.
                row.SkillIds ??= new List<int>();

                if (row.SkillIds.Any(id => id < 0))
                    return Fail(400, "Invalid skill IDs.");

                // Ignore zero before subsequent validation and saving.
                row.SkillIds = row.SkillIds
                    .Where(id => id > 0)
                    .ToList();

                if (row.SkillIds.Distinct().Count() != row.SkillIds.Count)
                {
                    return Fail(
                        400,
                        $"Duplicate skills are not allowed for employee {row.EmployeeId}.");
                }
            }

            if (request.Assignments
                .GroupBy(x => x.EmployeeId)
                .Any(group => group.Count() > 1))
            {
                return Fail(
                    400,
                    "EmployeeId cannot repeat when reporting is identified by employee ID.");
            }

            if (request.Assignments
                .Where(x => x.AssignmentId > 0)
                .GroupBy(x => x.AssignmentId)
                .Any(group => group.Count() > 1))
            {
                return Fail(400, "AssignmentId cannot repeat.");
            }

            var duplicateLevels = request.Assignments
                .GroupBy(x => x.HierarchyLevel)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(level => level)
                .ToList();

            if (duplicateLevels.Count > 0)
            {
                return Fail(
                    409,
                    "Duplicate hierarchy levels: " +
                    string.Join(", ", duplicateLevels));
            }

            var requestRows = request.Assignments
                .ToDictionary(x => x.EmployeeId);

            // Prevent self-reporting and loops within this template.
            foreach (var row in request.Assignments)
            {
                var visited = new HashSet<int>();
                int? currentEmployeeId = row.EmployeeId;

                while (currentEmployeeId.HasValue)
                {
                    if (!visited.Add(currentEmployeeId.Value))
                    {
                        return Fail(
                            400,
                            "Reporting relationships cannot contain a loop.");
                    }

                    if (!requestRows.TryGetValue(
                            currentEmployeeId.Value,
                            out var currentRow))
                    {
                        break;
                    }

                    currentEmployeeId = currentRow.ReportingToEmployeeId;
                }
            }

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var companyExists = await _context.Companys
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                RoleTemplate template;

                List<RoleTemplateAssignments> existingAssignments = new();

                if (isNew)
                {
                    template = new RoleTemplate
                    {
                        CompanyId = request.CompanyId
                    };
                }
                else
                {
                    var existingTemplate = await _context.RoleTemplates
                        .FirstOrDefaultAsync(x =>
                            x.TemplateId == request.TemplateId &&
                            x.CompanyId == request.CompanyId);

                    if (existingTemplate == null)
                        return Fail(404, "Template not found for this company.");

                    template = existingTemplate;

                    var savedBranchIds = await _context.RoleTemplateBranches
                        .Where(x =>
                            x.TemplateId == template.TemplateId &&
                            x.CompanyId == request.CompanyId)
                        .Select(x => x.BranchId)
                        .ToListAsync();

                    if (!savedBranchIds.ToHashSet().SetEquals(request.BranchIds))
                    {
                        return Fail(
                            409,
                            "Template branches cannot be changed after creation.");
                    }

                    existingAssignments = await _context.RoleTemplateAssignment
                        .Where(x =>
                            x.TemplateId == template.TemplateId &&
                            x.CompanyId == request.CompanyId)
                        .ToListAsync();
                }

                var duplicateName = await _context.RoleTemplates.AnyAsync(x =>
                    x.CompanyId == request.CompanyId &&
                    x.TemplateId != request.TemplateId &&
                    EF.Functions.Collate(
                        x.TemplateName.Trim(),
                        "SQL_Latin1_General_CP1_CI_AS") == templateName);

                if (duplicateName)
                    return Fail(409, "Template name already exists for this company.");

                var branches = await _context.Branches
                    .AsNoTracking()
                    .Where(x =>
                        request.BranchIds.Contains(x.BranchId) &&
                        x.CompanyId == request.CompanyId)
                    .Select(x => new
                    {
                        x.BranchId,
                        x.City,
                        x.IsActive
                    })
                    .ToListAsync();

                if (branches.Count != request.BranchIds.Count)
                {
                    return Fail(
                        400,
                        "One or more branches do not belong to this company.");
                }

                if (isNew && branches.Any(x => x.IsActive != true))
                    return Fail(400, "Only active branches can be selected.");

                if (request.Cities?.Count > 0)
                {
                    var selectedCities = request.Cities
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    if (branches.Any(x =>
                        string.IsNullOrWhiteSpace(x.City) ||
                        !selectedCities.Contains(x.City.Trim())))
                    {
                        return Fail(
                            400,
                            "Selected branches must belong to the selected cities.");
                    }
                }

                Dictionary<int, RoleTemplateAssignments> existingById =
                    existingAssignments.ToDictionary(x => x.AssignmentId);

                HashSet<int> submittedIds = request.Assignments
                    .Where(x => x.AssignmentId > 0)
                    .Select(x => x.AssignmentId)
                    .ToHashSet();

                if (submittedIds.Any(id => !existingById.ContainsKey(id)))
                {
                    return Fail(
                        400,
                        "An assignment does not belong to this template.");
                }

                if (existingAssignments.Any(x =>
                    !submittedIds.Contains(x.AssignmentId)))
                {
                    return Fail(
                        400,
                        "Send all existing assignments. Removing rows is not supported by this API.");
                }

                var roleIds = request.Assignments
                    .Select(x => x.RoleId)
                    .Distinct()
                    .ToList();

                var validRoleCount = await _context.Roles.CountAsync(x =>
                    roleIds.Contains(x.RoleId) &&
                    x.CompanyId == request.CompanyId &&
                    x.IsActive == true);

                if (validRoleCount != roleIds.Count)
                {
                    return Fail(
                        400,
                        "Select active roles belonging to this company.");
                }

                var employeeIds = request.Assignments
                    .Select(x => x.EmployeeId)
                    .Distinct()
                    .ToList();

                var validEmployeeCount = await _context.EmployeeMasters
                    .CountAsync(x =>
                        employeeIds.Contains(x.EmployeeId) &&
                        x.CompanyId == request.CompanyId);

                if (validEmployeeCount != employeeIds.Count)
                {
                    return Fail(
                        400,
                        "One or more employees do not belong to this company.");
                }

                foreach (var row in request.Assignments)
                {
                    if (row.ReportingToEmployeeId == 0)
                        row.ReportingToEmployeeId = null;
                }

                var reportingEmployeeIds = request.Assignments
                    .Where(x =>
                        x.ReportingToEmployeeId.HasValue &&
                        x.ReportingToEmployeeId.Value > 0)
                    .Select(x => x.ReportingToEmployeeId!.Value)
                    .Distinct()
                    .ToList();

                if (reportingEmployeeIds.Count > 0)
                {
                    var validReportingEmployeeIds = await _context.EmployeeMasters
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            reportingEmployeeIds.Contains(x.EmployeeId))
                        .Select(x => x.EmployeeId)
                        .ToListAsync();

                    var invalidReportingEmployeeIds = reportingEmployeeIds
                        .Except(validReportingEmployeeIds)
                        .ToList();

                    if (invalidReportingEmployeeIds.Count > 0)
                    {
                        return Fail(
                            400,
                            "Reporting EmployeeId(s) do not belong to this company: " +
                            string.Join(", ", invalidReportingEmployeeIds));
                    }
                }

                var skillIds = request.Assignments
                    .SelectMany(x => x.SkillIds ?? new List<int>())
                    .Distinct()
                    .ToList();

                // Validate skills only when supplied.
                if (skillIds.Count > 0)
                {
                    var validSkillCount = await (
                        from skill in _context.CompanySkillMasters

                        join category in _context.CompanySkillMappings
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
                              && skillIds.Contains(skill.CompanySkillMasterId)
                              && skill.IsActive == true
                              && category.IsActive == true

                        select skill.CompanySkillMasterId
                    ).Distinct().CountAsync();

                    if (validSkillCount != skillIds.Count)
                    {
                        return Fail(
                            400,
                            "Select active skills from active categories belonging to this company.");
                    }
                }

                var now = DateTime.UtcNow;

                template.TemplateName = templateName;
                template.IsActive = request.IsActive;

                if (isNew)
                {
                    template.CreatedOn = now;
                    template.CreatedBy = request.CreatedBy;

                    _context.RoleTemplates.Add(template);
                }
                else
                {
                    template.ModifiedOn = now;
                    template.ModifiedBy = request.ModifiedBy;
                }

                await _context.SaveChangesAsync();

                if (isNew)
                {
                    foreach (var branchId in request.BranchIds)
                    {
                        _context.RoleTemplateBranches.Add(
                            new RoleTemplateBranch
                            {
                                CompanyId = template.CompanyId,
                                TemplateId = template.TemplateId,
                                BranchId = branchId
                            });
                    }
                }

                // Preserve hierarchy-swap handling.
                foreach (var assignment in existingAssignments)
                {
                    assignment.HierarchyLevel = -assignment.AssignmentId;
                }

                if (existingAssignments.Count > 0)
                    await _context.SaveChangesAsync();

                Dictionary<int, RoleTemplateAssignments> savedRows = new();

                foreach (var row in request.Assignments)
                {
                    RoleTemplateAssignments assignment;

                    if (row.AssignmentId == 0)
                    {
                        assignment = new RoleTemplateAssignments
                        {
                            CompanyId = template.CompanyId,
                            TemplateId = template.TemplateId,
                            CreatedOn = now,
                            CreatedBy = auditUserId
                        };

                        _context.RoleTemplateAssignment.Add(assignment);
                    }
                    else
                    {
                        assignment = existingById[row.AssignmentId];
                        assignment.ModifiedOn = now;
                        assignment.ModifiedBy = request.ModifiedBy;
                    }

                    assignment.RoleId = row.RoleId;
                    assignment.EmployeeId = row.EmployeeId;
                    assignment.HierarchyLevel = row.HierarchyLevel;
                    assignment.CanApprove = row.CanApprove;
                    assignment.CanReject = row.CanReject;
                    assignment.CanReturn = row.CanReturn;
                    assignment.Stage = row.Stage!;
                    assignment.ApprovalDays = row.ApprovalDays;
                    assignment.IsActive = row.IsActive;

                    savedRows.Add(row.EmployeeId, assignment);
                }

                await _context.SaveChangesAsync();

                var savedAssignmentIds = savedRows.Values
                    .Select(x => x.AssignmentId)
                    .ToList();

                // Load active AND inactive mappings for reactivation.
                var existingSkills = await _context.RoleTemplateAssignmentSkills
                    .Where(x =>
                        x.CompanyId == template.CompanyId &&
                        savedAssignmentIds.Contains(x.AssignmentId))
                    .ToListAsync();

                foreach (var row in request.Assignments)
                {
                    var assignment = savedRows[row.EmployeeId];

                    // Preserve your existing reporting-assignment behavior.
                    assignment.ReportingToAssignmentId = row.ReportingToEmployeeId;

                    assignment.ReportingToAssignmentId =
                        row.ReportingToEmployeeId.HasValue &&
                        savedRows.TryGetValue(
                            row.ReportingToEmployeeId.Value,
                            out var reportingAssignment)
                            ? reportingAssignment.AssignmentId
                            : null;

                    var selectedSkills = (row.SkillIds ?? new List<int>())
                        .ToHashSet();

                    var currentSkills = existingSkills
                        .Where(x => x.AssignmentId == assignment.AssignmentId)
                        .ToList();

                    // Selected mappings become active.
                    // Unselected mappings become inactive.
                    // Empty selection deactivates every existing skill mapping.
                    foreach (var existingSkill in currentSkills)
                    {
                        existingSkill.IsActive =
                            selectedSkills.Contains(existingSkill.SkillId);
                    }

                    var existingSkillIds = currentSkills
                        .Select(x => x.SkillId)
                        .ToHashSet();

                    // Insert only skills that have never been mapped.
                    foreach (var skillId in selectedSkills)
                    {
                        if (!existingSkillIds.Contains(skillId))
                        {
                            _context.RoleTemplateAssignmentSkills.Add(
                                new RoleTemplateAssignmentSkill
                                {
                                    CompanyId = template.CompanyId,
                                    AssignmentId = assignment.AssignmentId,
                                    SkillId = skillId,
                                    IsActive = true
                                });
                        }
                    }
                }

                var employeesToUpdate = await _context.EmployeeMasters
                    .AsTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        employeeIds.Contains(x.EmployeeId))
                    .ToDictionaryAsync(x => x.EmployeeId);

                foreach (var row in request.Assignments)
                {
                    employeesToUpdate[row.EmployeeId].RoleId = row.RoleId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = isNew
                        ? "Template created successfully."
                        : "Template updated successfully.",
                    TemplateId = template.TemplateId,
                    Assignments = savedRows
                        .OrderBy(x => x.Value.HierarchyLevel)
                        .Select(x => new
                        {
                            EmployeeId = x.Key,
                            AssignmentId = x.Value.AssignmentId,
                            HierarchyLevel = x.Value.HierarchyLevel,
                            ReportingToEmployeeId =
                                requestRows[x.Key].ReportingToEmployeeId
                        })
                        .ToList()
                });
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                return Fail(
                    409,
                    "Duplicate template name, hierarchy level, or assignment mapping.");
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      sql.Number == 547)
            {
                return Fail(
                    409,
                    "Related records changed or a database constraint was violated.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Failed to save role template for company {CompanyId}",
                    request.CompanyId);

                return Fail(
                    500,
                    "Unable to save the template. Please try again.");
            }
        }

        [HttpPost("GetEmployeeAssignedModuleAccess")]
        public async Task<IActionResult> GetEmployeeAssignedModuleAccess(  [FromBody] GetEmployeeAssignedModuleAccessRequest? request)
        {
            IActionResult Fail(int code, string message)
            {
                return StatusCode(code, new
                {
                    Status = false,
                    StatusCode = code,
                    Message = message
                });
            }

            if (request == null ||
                request.CompanyId <= 0 ||
                request.EmployeeId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId and EmployeeId are required.");
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
                        x.EmpCode,
                        x.CompanyId,
                        x.RoleId,
                        x.RoleMail,
                        x.IsAdmin,
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
                {
                    return Fail(403, "Employee is inactive.");
                }

                // Return only employees with an active mapped role.
                var mappedRoles = await (
                    from assignment in _context.RoleTemplateAssignment
                        .AsNoTracking()

                    join template in _context.Set<RoleTemplate>()
                        .AsNoTracking()
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

                    join role in _context.Roles.AsNoTracking()
                        on new
                        {
                            assignment.CompanyId,
                            assignment.RoleId
                        }
                        equals new
                        {
                            role.CompanyId,
                            role.RoleId
                        }

                    where assignment.CompanyId == request.CompanyId
                          && assignment.EmployeeId == request.EmployeeId
                          && assignment.IsActive == true
                          && template.IsActive == true
                          && role.IsActive == true

                    select new
                    {
                        role.RoleId,
                        role.RoleName
                    }
                )
                .Distinct()
                .OrderBy(x => x.RoleId)
                .ToListAsync();

                if (mappedRoles.Count == 0)
                {
                    return Fail(
                        403,
                        "No active role is mapped to this employee.");
                }

                var companyName = await _context.Companys
                    .AsNoTracking()
                    .Where(x => x.CompanyID == request.CompanyId)
                    .Select(x => x.CompanyName)
                    .FirstOrDefaultAsync();

                // Employee master role is returned only if currently mapped.
                var primaryRole = mappedRoles.FirstOrDefault(x =>
                    x.RoleId == employee.RoleId);

                var permissions = await (
                    from access in _context.EmployeeModuleAccesses
                        .AsNoTracking()

                    join module in _context.Modules.AsNoTracking()
                        on access.ModuleId equals module.ModuleId

                    where access.CompanyId == request.CompanyId
                          && access.EmployeeId == request.EmployeeId

                    orderby module.ModuleId

                    select new
                    {
                        access.EmployeeModuleAccessId,
                        module.ModuleId,
                        module.ModuleName,

                        access.CanAdd,
                        access.CanEdit,
                        access.CanView,
                        access.CanDelete,
                        access.CanDownload,

                        access.CreatedBy,
                        access.CreatedOn,
                        access.ModifiedBy,
                        access.ModifiedOn
                    }
                ).ToListAsync();

                var auditIds = permissions
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
                    .ToDictionaryAsync(
                        x => x.EmployeeId,
                        x => x.Name);

                string? GetEmployeeName(int? employeeId)
                {
                    return employeeId.HasValue &&
                           auditNames.TryGetValue(
                               employeeId.Value, out var name)
                        ? name
                        : null;
                }

                var modules = permissions.Select(x => new
                {
                    x.EmployeeModuleAccessId,
                    x.ModuleId,
                    x.ModuleName,

                    x.CanAdd,
                    x.CanEdit,
                    x.CanView,
                    x.CanDelete,
                    x.CanDownload,

                    x.CreatedBy,
                    CreatedByName = GetEmployeeName(x.CreatedBy),
                    x.CreatedOn,

                    x.ModifiedBy,
                    ModifiedByName = GetEmployeeName(x.ModifiedBy),
                    x.ModifiedOn
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    StatusCode = 200,
                    Message = modules.Count > 0
                        ? "Assigned modules and access fetched successfully."
                        : "Role is mapped, but no modules are assigned.",

                    Data = new
                    {
                        employee.CompanyId,
                        CompanyName = companyName,

                        employee.EmployeeId,
                        employee.EmpCode,
                        employee.EmployeeName,

                        RoleId = primaryRole?.RoleId,
                        RoleName = primaryRole?.RoleName,

                        // Supports employees mapped to multiple roles.
                        Roles = mappedRoles,
                        IsRoleMapped = true,

                        MappedUserName = employee.RoleMail,
                        IsAdmin = employee.IsAdmin == true,
                        employee.IsActive,

                        TotalModules = modules.Count,
                        Modules = modules
                    }
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch assigned module access for CompanyId {CompanyId}, EmployeeId {EmployeeId}.",
                    request.CompanyId,
                    request.EmployeeId);

                return StatusCode(500, new
                {
                    Status = false,
                    StatusCode = 500,
                    Message = "Unable to fetch assigned module access."
                });
            }
        }



        public class GetEmployeeAssignedModuleAccessRequest
        {
            public int CompanyId { get; set; }
            public int EmployeeId { get; set; }
        }




        public class GetTemplateFlowRequest
        {
            public int CompanyId { get; set; }

            public List<int>? TemplateIds { get; set; }
        }

        public class SaveRoleTemplateRequest
            {
                public int TemplateId { get; set; }
                public int CompanyId { get; set; }
                public string? TemplateName { get; set; }
                public string? Area { get; set; }

                public List<string> Cities { get; set; } = new();
                public List<int> BranchIds { get; set; } = new();

                public int? CreatedBy { get; set; }
                public int? ModifiedBy { get; set; }
                public bool IsActive { get; set; } = true;

                public List<SaveRoleAssignmentRequest> Assignments
                { get; set; } = new();
            }

            public class SaveRoleAssignmentRequest
            {
            // Unique within this request, for example "row-1".
            public int? ReportingToEmployeeId { get; set; }

            // 0 for a new assignment; existing ID for an edit.
            public int AssignmentId { get; set; }

                public int RoleId { get; set; }
                public int HierarchyLevel { get; set; }
                public int EmployeeId { get; set; }

            

                public bool CanApprove { get; set; }
                public bool CanReject { get; set; }
                public bool CanReturn { get; set; }

                public string? Stage { get; set; }
                public int ApprovalDays { get; set; }

            // In your assignment request class:
            public List<int>? SkillIds { get; set; }

            // In RoleTemplateAssignmentSkill:
            public bool IsActive { get; set; } = true;
        }
        }
    }

