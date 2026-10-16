using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;

namespace Recruitment.Controllers
{
    [Route("api/JdController")]
    [ApiController]
    public class JdController : Controller
{
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public JdController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }



        [HttpPost("SaveJobDescriptions")]
        public async Task<IActionResult> SaveJobDescriptions(
    [FromBody] SaveJobDescriptionsRequest? request)
        {
            if (request == null || request.CompanyId <= 0)
                return Fail(400, "Valid CompanyId is required.");

            if (request.UserId.HasValue && request.UserId.Value <= 0)
                return Fail(400, "Invalid UserId.");

            if (request.Jobs == null ||
                request.Jobs.Count == 0 ||
                request.Jobs.Any(x => x == null))
            {
                return Fail(400, "Provide at least one valid job description.");
            }

            foreach (var job in request.Jobs)
            {
                job.JobTitle = job.JobTitle?.Trim();

                if (job.JobDescriptionId < 0)
                    return Fail(400, "Invalid JobDescriptionId.");

                if (string.IsNullOrWhiteSpace(job.JobTitle) ||
                    job.JobTitle.Length > 200)
                {
                    return Fail(
                        400,
                        "Job title is required and cannot exceed 200 characters.");
                }

                // Added: template is required for each JD.
                if (!job.TemplateId.HasValue || job.TemplateId.Value <= 0)
                {
                    return Fail(
                        400,
                        $"Valid TemplateId is required for '{job.JobTitle}'.");
                }


         


            }

            if (request.Jobs
                .GroupBy(x => x.JobTitle!, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                return Fail(409, "Duplicate job titles are present in the request.");
            }

            if (request.Jobs
                .Where(x => x.JobDescriptionId > 0)
                .GroupBy(x => x.JobDescriptionId)
                .Any(group => group.Count() > 1))
            {
                return Fail(400, "JobDescriptionId cannot repeat in the request.");
            }

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var companyExists = await _context.Companys
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                // Added: all selected templates must be active
                // and belong to the supplied company.
                var templateIds = request.Jobs
                    .Select(x => x.TemplateId!.Value)
                    .Distinct()
                    .ToList();

                var validTemplateIds = await _context.Set<RoleTemplate>()
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive == true &&
                        templateIds.Contains(x.TemplateId))
                    .Select(x => x.TemplateId)
                    .ToListAsync();

                if (validTemplateIds.Count != templateIds.Count)
                {
                    return Fail(
                        400,
                        "Select active templates belonging to this company.");
                }

                var editIds = request.Jobs
                    .Where(x => x.JobDescriptionId > 0)
                    .Select(x => x.JobDescriptionId)
                    .ToList();

                var existingJobs = await _context.JobDescriptions
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        editIds.Contains(x.JobDescriptionId))
                    .ToDictionaryAsync(x => x.JobDescriptionId);

                if (existingJobs.Count != editIds.Count)
                {
                    return Fail(
                        404,
                        "One or more job descriptions were not found for this company.");
                }

                // Validate all JD titles against the database.
                foreach (var job in request.Jobs)
                {
                    var duplicateTitle = await _context.JobDescriptions
                        .AnyAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.JobDescriptionId != job.JobDescriptionId &&
                            EF.Functions.Collate(
                                x.JobTitle.Trim(),
                                "SQL_Latin1_General_CP1_CI_AS") == job.JobTitle);

                    if (duplicateTitle)
                    {
                        return Fail(
                            409,
                            $"Job title '{job.JobTitle}' already exists for this company.");
                    }
                }

                var branchIds = request.Jobs
                    .SelectMany(x => x.Placements)
                    .Select(x => x.BranchId)
                    .Distinct()
                    .ToList();

                var validBranchCount = await _context.Branches
                    .CountAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive == true &&
                        branchIds.Contains(x.BranchId));

                if (validBranchCount != branchIds.Count)
                {
                    return Fail(
                        400,
                        "Select active branches belonging to this company.");
                }

                var departmentIds = request.Jobs
                    .SelectMany(x => x.Placements)
                    .SelectMany(x => x.DepartmentIds)
                    .Distinct()
                    .ToList();

                var departments = await _context.Departments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive == true &&
                        departmentIds.Contains(x.DepartmentId))
                    .Select(x => new
                    {
                        x.DepartmentId,
                        x.BranchId
                    })
                    .ToListAsync();

                var validPairs = departments
                    .Select(x => (x.BranchId, x.DepartmentId))
                    .ToHashSet();

                foreach (var job in request.Jobs)
                {
                    foreach (var placement in job.Placements)
                    {
                        foreach (var departmentId in placement.DepartmentIds)
                        {
                            if (!validPairs.Contains(
                                (placement.BranchId, departmentId)))
                            {
                                return Fail(
                                    400,
                                    $"Department {departmentId} is inactive or does not belong to branch {placement.BranchId} in this company.");
                            }
                        }
                    }
                }

                var existingPlacements = await _context.JobDescriptionPlacements
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        editIds.Contains(x.JobDescriptionId))
                    .ToListAsync();

                var now = DateTime.UtcNow;

                var savedJobs = new List<(
                    JobDescription Entity,
                    SaveJobDescriptionRequest Input,
                    bool IsNew)>();

                foreach (var job in request.Jobs)
                {

                    if (job == null)
                        continue;

                    bool isNew = job.JobDescriptionId == 0;

                    var entity = isNew
                        ? new JobDescription
                        {
                            CompanyId = request.CompanyId,
                            CreatedOn = now,
                            CreatedBy = request.UserId
                        }
                        : existingJobs[job.JobDescriptionId];

                    entity.TemplateId = job.TemplateId;

                    entity.JobTitle = job.JobTitle ?? string.Empty;
                    entity.Location = job.Location?.Trim() ?? string.Empty;
                    entity.EmploymentType =
                        job.EmploymentType?.Trim() ?? string.Empty;

                    entity.Vacancies = job.Vacancies;
                    entity.ApplicationDeadline = job.ApplicationDeadline?.Date;

                    entity.MinSalary = job.MinSalary;
                    entity.MaxSalary = job.MaxSalary;

                    entity.Currency =
                        job.Currency?.Trim().ToUpperInvariant() ?? string.Empty;

                    entity.SalaryPeriod =
                        job.SalaryPeriod?.Trim() ?? string.Empty;

                    entity.JobLevel = job.JobLevel?.Trim() ?? string.Empty;
                    entity.ReportingTo = job.ReportingTo?.Trim() ?? string.Empty;

                    entity.MinExperienceYears = job.MinExperienceYears;
                    entity.MaxExperienceYears = job.MaxExperienceYears;

                    entity.RequiredSkills = job.RequiredSkills ?? string.Empty;
                    entity.RoleOverview = job.RoleOverview ?? string.Empty;
                    entity.Responsibilities = job.Responsibilities ?? string.Empty;
                    entity.Qualifications = job.Qualifications ?? string.Empty;
                    entity.NiceToHave = job.NiceToHave ?? string.Empty;
                    entity.PerksAndBenefits = job.PerksAndBenefits ?? string.Empty;

                    entity.InterviewRounds = job.InterviewRounds;
                    entity.InterviewMode =
                        job.InterviewMode?.Trim() ?? string.Empty;

                    entity.ExpectedJoiningDays = job.ExpectedJoiningDays;
                    entity.ImagePath = job.ImagePath?.Trim() ?? string.Empty;
                    entity.IsActive = job.IsActive;


                    if (isNew)
                    {
                        _context.JobDescriptions.Add(entity);
                    }
                    else
                    {
                        entity.ModifiedOn = now;
                        entity.ModifiedBy = request.UserId;
                    }

                    savedJobs.Add((entity, job, isNew));
                }

                // Generate IDs for new JDs.
                await _context.SaveChangesAsync();

                foreach (var saved in savedJobs)
                {
                    var selectedPairs = saved.Input.Placements
                        .SelectMany(
                            placement => placement.DepartmentIds,
                            (placement, departmentId) =>
                                (BranchId: placement.BranchId,
                                 DepartmentId: departmentId))
                        .ToHashSet();

                    var currentPlacements = existingPlacements
                        .Where(x =>
                            x.JobDescriptionId == saved.Entity.JobDescriptionId)
                        .ToList();

                    var currentPairs = currentPlacements
                        .Select(x => (x.BranchId, x.DepartmentId))
                        .ToHashSet();

                    // Remove deselected combinations.
                    foreach (var placement in currentPlacements)
                    {
                        if (!selectedPairs.Contains(
                            (placement.BranchId, placement.DepartmentId)))
                        {
                            _context.JobDescriptionPlacements.Remove(placement);
                        }
                    }

                    // Add newly selected combinations.
                    foreach (var pair in selectedPairs)
                    {
                        if (!currentPairs.Contains(pair))
                        {
                            _context.JobDescriptionPlacements.Add(
                                new JobDescriptionPlacement
                                {
                                    CompanyId = request.CompanyId,
                                    JobDescriptionId = saved.Entity.JobDescriptionId,
                                    BranchId = pair.BranchId,
                                    DepartmentId = pair.DepartmentId
                                });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Job descriptions saved successfully.",
                    Data = savedJobs.Select(x => new
                    {
                        x.Entity.JobDescriptionId,
                        x.Entity.CompanyId,
                        x.Entity.TemplateId,
                        x.Entity.JobTitle,
                        Operation = x.IsNew ? "Created" : "Updated"
                    }).ToList()
                });
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                return Fail(
                    409,
                    "A duplicate JD title or placement already exists.");
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      sql.Number == 547)
            {
                return Fail(
                    409,
                    "A related record changed or a database constraint was violated.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save job descriptions for company {CompanyId}",
                    request.CompanyId);

                return Fail(500, "Unable to save job descriptions.");
            }
        }


        private ObjectResult Fail(int statusCode, string message)
        {
            return StatusCode(statusCode, new
            {
                Status = false,
                Message = message
            });
        }



        [HttpPost("GetJobDescriptionList")]
        public async Task<IActionResult> GetJobDescriptionList(
            [FromBody] GetJobDescriptionListRequest? request)
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
                request.UserId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId and UserId are required.");
            }

            if (request.BranchIds != null &&
                request.BranchIds.Any(id => id < 0))
            {
                return Fail(400, "Branch IDs cannot be negative.");
            }

            if (request.DepartmentIds != null &&
                request.DepartmentIds.Any(id => id < 0))
            {
                return Fail(400, "Department IDs cannot be negative.");
            }

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
                    return Fail(404, "Company not found.");

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
                    return Fail(404, "User not found for this company.");

                if (currentUser.IsActive != true)
                    return Fail(403, "User is inactive.");

                bool isAdmin = currentUser.IsAdmin == true;

                bool allBranches =
                    request.BranchIds == null ||
                    request.BranchIds.Count == 0 ||
                    request.BranchIds.Contains(0);

                bool allDepartments =
                    request.DepartmentIds == null ||
                    request.DepartmentIds.Count == 0 ||
                    request.DepartmentIds.Contains(0);

                var branchIds = allBranches
                    ? Array.Empty<int>()
                    : request.BranchIds!.Distinct().ToArray();

                var departmentIds = allDepartments
                    ? Array.Empty<int>()
                    : request.DepartmentIds!.Distinct().ToArray();

                if (!allBranches)
                {
                    var validBranchCount = await _context.Branches
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            branchIds.Contains(x.BranchId));

                    if (validBranchCount != branchIds.Length)
                    {
                        return Fail(
                            400,
                            "One or more branches do not belong to this company.");
                    }
                }

                if (!allDepartments)
                {
                    var validDepartmentCount = await _context.Departments
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            departmentIds.Contains(x.DepartmentId));

                    if (validDepartmentCount != departmentIds.Length)
                    {
                        return Fail(
                            400,
                            "One or more departments do not belong to this company.");
                    }
                }

                var query = _context.JobDescriptions
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId);

                // Admin: no template restriction.
                // Non-admin: only jobs mapped to the user's assigned templates.
                if (!isAdmin)
                {
                    var assignedTemplateIds =
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
                              && assignment.EmployeeId == request.UserId
                              && assignment.IsActive == true
                              && template.IsActive == true
                              && role.IsActive == true

                        select assignment.TemplateId;

                    query = query.Where(job =>
                        assignedTemplateIds.Any(templateId =>
                            (int?)templateId == job.TemplateId));
                }

                if (!allBranches || !allDepartments)
                {
                    var matchingPlacements = _context.JobDescriptionPlacements
                        .AsNoTracking()
                        .Where(x => x.CompanyId == request.CompanyId);

                    if (!allBranches)
                    {
                        matchingPlacements = matchingPlacements.Where(x =>
                            branchIds.Contains(x.BranchId));
                    }

                    if (!allDepartments)
                    {
                        matchingPlacements = matchingPlacements.Where(x =>
                            departmentIds.Contains(x.DepartmentId));
                    }

                    // Branch and department must match the same placement.
                    query = query.Where(job =>
                        matchingPlacements.Any(placement =>
                            placement.JobDescriptionId == job.JobDescriptionId));
                }

                var jobs = await query
                    .OrderByDescending(x => x.JobDescriptionId)
                    .ToListAsync();

                if (jobs.Count == 0)
                {
                    return Ok(new
                    {
                        Status = true,
                        StatusCode = 200,
                        Message = "No job descriptions found.",
                        IsAdmin = isAdmin,
                        Data = Array.Empty<object>()
                    });
                }

                var jobIds = jobs
                    .Select(x => x.JobDescriptionId)
                    .ToList();

                // Return every saved placement for the matching JDs.
                var placements = await _context.JobDescriptionPlacements
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        jobIds.Contains(x.JobDescriptionId))
                    .Select(x => new
                    {
                        x.PlacementId,
                        x.JobDescriptionId,
                        x.BranchId,
                        x.DepartmentId,

                        BranchName = _context.Branches
                            .Where(b =>
                                b.BranchId == x.BranchId &&
                                b.CompanyId == x.CompanyId)
                            .Select(b => b.BranchName)
                            .FirstOrDefault(),

                        City = _context.Branches
                            .Where(b =>
                                b.BranchId == x.BranchId &&
                                b.CompanyId == x.CompanyId)
                            .Select(b => b.City)
                            .FirstOrDefault(),

                        DepartmentName = _context.Departments
                            .Where(d =>
                                d.DepartmentId == x.DepartmentId &&
                                d.BranchId == x.BranchId &&
                                d.CompanyId == x.CompanyId)
                            .Select(d => d.DepartmentName)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                var placementsByJob = placements
                    .ToLookup(x => x.JobDescriptionId);

                var auditUserIds = jobs
                    .SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    })
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

                var auditUsers = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        auditUserIds.Contains(x.EmployeeId))
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

                var data = jobs.Select(job =>
                {
                    string? createdByName = null;
                    string? modifiedByName = null;

                    if (job.CreatedBy.HasValue)
                    {
                        auditUsers.TryGetValue(
                            job.CreatedBy.Value,
                            out createdByName);
                    }

                    if (job.ModifiedBy.HasValue)
                    {
                        auditUsers.TryGetValue(
                            job.ModifiedBy.Value,
                            out modifiedByName);
                    }

                    return new
                    {
                        job.JobDescriptionId,
                        job.CompanyId,
                        CompanyName = company.CompanyName,

                        job.JobTitle,
                        job.Location,
                        job.EmploymentType,
                        job.Vacancies,
                        job.ApplicationDeadline,

                        job.MinSalary,
                        job.MaxSalary,
                        job.Currency,
                        job.SalaryPeriod,

                        job.JobLevel,
                        job.TemplateId,
                        job.ReportingTo,
                        job.MinExperienceYears,
                        job.MaxExperienceYears,

                        job.RequiredSkills,
                        job.RoleOverview,
                        job.Responsibilities,
                        job.Qualifications,
                        job.NiceToHave,
                        job.PerksAndBenefits,

                        job.InterviewRounds,
                        job.InterviewMode,
                        job.ExpectedJoiningDays,
                        job.ImagePath,
                        job.IsActive,

                        job.CreatedOn,
                        job.CreatedBy,
                        CreatedByName = createdByName,

                        job.ModifiedOn,
                        job.ModifiedBy,
                        ModifiedByName = modifiedByName,

                        Placements = placementsByJob[job.JobDescriptionId]
                            .GroupBy(x => new
                            {
                                x.BranchId,
                                x.BranchName,
                                x.City
                            })
                            .OrderBy(group => group.Key.BranchName)
                            .Select(group => new
                            {
                                group.Key.BranchId,
                                group.Key.BranchName,
                                group.Key.City,

                                DepartmentIds = group
                                    .Select(x => x.DepartmentId)
                                    .Distinct()
                                    .OrderBy(id => id)
                                    .ToList(),

                                Departments = group
                                    .OrderBy(x => x.DepartmentName)
                                    .Select(x => new
                                    {
                                        x.PlacementId,
                                        x.DepartmentId,
                                        x.DepartmentName
                                    })
                                    .ToList()
                            })
                            .ToList()
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    StatusCode = 200,
                    Message = "Job descriptions fetched successfully.",
                    IsAdmin = isAdmin,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch job descriptions for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return StatusCode(500, new
                {
                    Status = false,
                    StatusCode = 500,
                    Message = "Unable to fetch job descriptions."
                });
            }
        }











        public class GetJDStatisticsRequest
        {
            public int CompanyId { get; set; }

            public List<int>? BranchIds { get; set; }

            public List<int>? DepartmentIds { get; set; }

            public List<int>? JobDescriptionIds { get; set; }
        }


        public class GetJobDescriptionListRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }
            public List<int>? BranchIds { get; set; }

            public List<int>? DepartmentIds { get; set; }
        }

        public class SaveJobDescriptionsRequest
    {
        public int CompanyId { get; set; }

        public int? UserId { get; set; }

        public List<SaveJobDescriptionRequest> Jobs { get; set; } = new();
    }

    public class SaveJobDescriptionRequest
    {
        public int JobDescriptionId { get; set; }
            public int? TemplateId { get; set; }

            public string? JobTitle { get; set; }
        public string? Location { get; set; }
        public string? EmploymentType { get; set; }

        public int Vacancies { get; set; }
        public DateTime? ApplicationDeadline { get; set; }

        public decimal? MinSalary { get; set; }
        public decimal? MaxSalary { get; set; }

        public string? Currency { get; set; } = "INR";
        public string? SalaryPeriod { get; set; } = "Year";

        public string? JobLevel { get; set; }
        public string? ReportingTo { get; set; }

        public decimal? MinExperienceYears { get; set; }
        public decimal? MaxExperienceYears { get; set; }

        public string? RequiredSkills { get; set; }
        public string? RoleOverview { get; set; }
        public string? Responsibilities { get; set; }
        public string? Qualifications { get; set; }
        public string? NiceToHave { get; set; }
        public string? PerksAndBenefits { get; set; }

        public int? InterviewRounds { get; set; }
        public string? InterviewMode { get; set; }
        public int? ExpectedJoiningDays { get; set; }

        public string? ImagePath { get; set; }

        public bool IsActive { get; set; } = true;

        public List<JobDescriptionPlacementRequest> Placements
        { get; set; } = new();
    }

    public class JobDescriptionPlacementRequest
    {
        public int BranchId { get; set; }

        public List<int> DepartmentIds { get; set; } = new();
    }
}
}
