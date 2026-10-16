using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.Text.Json;

namespace Recruitment.Controllers
{
    [Route("api/SkillSetsController")]
    [ApiController]
    public class SkillSetsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public SkillSetsController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }



        [HttpPost("SaveCompanySkills")]
        public async Task<IActionResult> SaveCompanySkills(  [FromBody] SaveCompanySkillsRequest? request)
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

            if (request.Categories == null ||
                request.Categories.Count == 0 ||
                request.Categories.Any(x => x == null))
            {
                return Fail(400, "Provide at least one category.");
            }

            foreach (var category in request.Categories)
            {
                if (category.SkillMappingId < 0)
                    return Fail(400, "Invalid SkillMappingId.");

                category.CategoryName = category.CategoryName?.Trim();

                if (string.IsNullOrWhiteSpace(category.CategoryName))
                    return Fail(400, "CategoryName is required.");

                if (category.Skills == null)
                    continue;

                if (category.Skills.Any(x => x == null))
                    return Fail(400, "Invalid skill item.");

                foreach (var skill in category.Skills)
                {
                    if (skill.CompanySkillMasterId < 0)
                        return Fail(400, "Invalid CompanySkillMasterId.");

                    skill.SkillName = skill.SkillName?.Trim();

                    if (string.IsNullOrWhiteSpace(skill.SkillName))
                    {
                        return Fail(
                            400,
                            $"SkillName is required in '{category.CategoryName}'.");
                    }
                }
            }

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var companyExists = await _context.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var userExists = await _context.EmployeeMasters
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId &&
                        x.IsActive == true);

                if (!userExists)
                    return Fail(400, "Active user not found for this company.");

                var now = DateTime.UtcNow;

                // Avoid duplicate response items.
                var savedCategories =
                    new Dictionary<int, CompanySkillMapping>();

                var savedSkills =
                    new Dictionary<int, CompanySkillMaster>();

                var createdCategoryIds = new HashSet<int>();
                var createdSkillIds = new HashSet<int>();

                // Tracks existing skills updated by this request.
                var updatedSkillIds = new HashSet<int>();

                foreach (var categoryRequest in request.Categories)
                {
                    // Includes inactive categories.
                    var categoryMatches = await _context.CompanySkillMappings
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            EF.Functions.Collate(
                                x.CategoryName.Trim(),
                                "SQL_Latin1_General_CP1_CI_AS")
                                == categoryRequest.CategoryName)
                        .Take(2)
                        .ToListAsync();

                    if (categoryMatches.Count > 1)
                    {
                        return Fail(
                            409,
                            $"Multiple categories named '{categoryRequest.CategoryName}' already exist. Resolve those duplicates first.");
                    }

                    var category = categoryMatches.FirstOrDefault();

                    if (categoryRequest.SkillMappingId > 0 &&
                        (category == null ||
                         category.SkillMappingId !=
                             categoryRequest.SkillMappingId))
                    {
                        return Fail(
                            400,
                            "The category ID and name do not match this company. Send SkillMappingId = 0 to add a new category name.");
                    }

                    if (category == null)
                    {
                        category = new CompanySkillMapping
                        {
                            CompanyId = request.CompanyId,
                            CategoryName = categoryRequest.CategoryName!,
                            IsActive = categoryRequest.IsActive,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        };

                        _context.CompanySkillMappings.Add(category);

                        await _context.SaveChangesAsync();

                        createdCategoryIds.Add(category.SkillMappingId);
                    }

                    // Existing category behavior remains unchanged.
                    savedCategories[category.SkillMappingId] = category;

                    foreach (var skillRequest in
                        categoryRequest.Skills ??
                        new List<SaveCompanySkillItemRequest>())
                    {
                        var skillMatches = await _context.CompanySkillMasters
                            .Where(x =>
                                x.CompanyId == request.CompanyId &&
                                x.SkillMappingId == category.SkillMappingId &&
                                EF.Functions.Collate(
                                    x.SkillName.Trim(),
                                    "SQL_Latin1_General_CP1_CI_AS")
                                    == skillRequest.SkillName)
                            .Take(2)
                            .ToListAsync();

                        if (skillMatches.Count > 1)
                        {
                            return Fail(
                                409,
                                $"Multiple skills named '{skillRequest.SkillName}' already exist in '{category.CategoryName}'. Resolve those duplicates first.");
                        }

                        var skill = skillMatches.FirstOrDefault();

                        if (skillRequest.CompanySkillMasterId > 0 &&
                            (skill == null ||
                             skill.CompanySkillMasterId !=
                                 skillRequest.CompanySkillMasterId))
                        {
                            return Fail(
                                400,
                                "The skill ID and name do not match the selected company category. Send CompanySkillMasterId = 0 to add a new skill name.");
                        }

                        if (skill == null)
                        {
                            skill = new CompanySkillMaster
                            {
                                CompanyId = request.CompanyId,
                                SkillMappingId = category.SkillMappingId,
                                SkillName = skillRequest.SkillName!,
                                IsActive = skillRequest.IsActive,
                                IsDisable = skillRequest.IsDisable,
                                CreatedBy = request.UserId,
                                CreatedOn = now
                            };

                            _context.CompanySkillMasters.Add(skill);

                            // Makes the insert visible to subsequent checks.
                            await _context.SaveChangesAsync();

                            createdSkillIds.Add(skill.CompanySkillMasterId);
                        }
                        else
                        {
                            // Update existing skill flags only when changed.
                            bool hasChanges =
                                skill.IsActive != skillRequest.IsActive ||
                                skill.IsDisable != skillRequest.IsDisable;

                            if (hasChanges)
                            {
                                skill.IsActive = skillRequest.IsActive;
                                skill.IsDisable = skillRequest.IsDisable;

                                skill.ModifiedBy = request.UserId;
                                skill.ModifiedOn = now;

                                if (!createdSkillIds.Contains(
                                    skill.CompanySkillMasterId))
                                {
                                    updatedSkillIds.Add(
                                        skill.CompanySkillMasterId);
                                }
                            }
                        }

                        savedSkills[skill.CompanySkillMasterId] = skill;
                    }
                }

                // Persist updates to existing skills.
                await _context.SaveChangesAsync();

                var auditIds = savedCategories.Values
                    .SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    })
                    .Concat(savedSkills.Values.SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    }))
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToArray();

                var employeeNames = await _context.EmployeeMasters
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
                           employeeNames.TryGetValue(id.Value, out var name)
                        ? name
                        : null;
                }

                var data = savedCategories.Values.Select(category => new
                {
                    category.SkillMappingId,
                    category.CompanyId,
                    category.CategoryName,
                    category.IsActive,

                    Operation = createdCategoryIds.Contains(
                        category.SkillMappingId)
                        ? "Created"
                        : "Skipped",

                    category.CreatedBy,
                    CreatedByName = GetName(category.CreatedBy),
                    category.CreatedOn,

                    category.ModifiedBy,
                    ModifiedByName = GetName(category.ModifiedBy),
                    category.ModifiedOn,

                    // Only skills mentioned in this request.
                    Skills = savedSkills.Values
                        .Where(skill =>
                            skill.SkillMappingId == category.SkillMappingId)
                        .Select(skill => new
                        {
                            skill.CompanySkillMasterId,
                            skill.SkillMappingId,
                            skill.SkillName,
                            skill.IsActive,
                            skill.IsDisable,

                            Operation = createdSkillIds.Contains(
                                skill.CompanySkillMasterId)
                                ? "Created"
                                : updatedSkillIds.Contains(
                                    skill.CompanySkillMasterId)
                                    ? "Updated"
                                    : "Skipped",

                            skill.CreatedBy,
                            CreatedByName = GetName(skill.CreatedBy),
                            skill.CreatedOn,

                            skill.ModifiedBy,
                            ModifiedByName = GetName(skill.ModifiedBy),
                            skill.ModifiedOn
                        })
                        .ToList()
                }).ToList();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Company categories and skills saved successfully.",
                    CreatedCategoryCount = createdCategoryIds.Count,
                    CreatedSkillCount = createdSkillIds.Count,
                    UpdatedSkillCount = updatedSkillIds.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save company skills for CompanyId {CompanyId}.",
                    request.CompanyId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to save company categories and skills."
                });
            }
        }




        //[HttpPost("SaveCompanySkills")]
        //public async Task<IActionResult> SaveCompanySkills(
        //    [FromBody] SaveCompanySkillsRequest? request)
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
        //        request.UserId <= 0)
        //    {
        //        return Fail(400, "Valid CompanyId and UserId are required.");
        //    }

        //    if (request.Categories == null ||
        //        request.Categories.Count == 0 ||
        //        request.Categories.Any(x => x == null))
        //    {
        //        return Fail(400, "Provide at least one category.");
        //    }

        //    foreach (var category in request.Categories)
        //    {
        //        if (category.SkillMappingId < 0)
        //            return Fail(400, "Invalid SkillMappingId.");

        //        category.CategoryName = category.CategoryName?.Trim();

        //        if (string.IsNullOrWhiteSpace(category.CategoryName))
        //            return Fail(400, "CategoryName is required.");

        //        if (category.Skills == null)
        //            continue;

        //        if (category.Skills.Any(x => x == null))
        //            return Fail(400, "Invalid skill item.");

        //        foreach (var skill in category.Skills)
        //        {
        //            if (skill.CompanySkillMasterId < 0)
        //                return Fail(400, "Invalid CompanySkillMasterId.");

        //            skill.SkillName = skill.SkillName?.Trim();

        //            if (string.IsNullOrWhiteSpace(skill.SkillName))
        //            {
        //                return Fail(
        //                    400,
        //                    $"SkillName is required in '{category.CategoryName}'.");
        //            }
        //        }
        //    }

        //    try
        //    {
        //        await using var transaction =
        //            await _context.Database.BeginTransactionAsync(
        //                System.Data.IsolationLevel.Serializable);

        //        var companyExists = await _context.Companys
        //            .AsNoTracking()
        //            .AnyAsync(x => x.CompanyID == request.CompanyId);

        //        if (!companyExists)
        //            return Fail(404, "Company not found.");

        //        var userExists = await _context.EmployeeMasters
        //            .AsNoTracking()
        //            .AnyAsync(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.EmployeeId == request.UserId &&
        //                x.IsActive == true);

        //        if (!userExists)
        //            return Fail(400, "Active user not found for this company.");

        //        var now = DateTime.UtcNow;

        //        // Dictionaries avoid repeating response items when the
        //        // same category or skill is submitted more than once.
        //        var savedCategories =
        //            new Dictionary<int, CompanySkillMapping>();

        //        var savedSkills =
        //            new Dictionary<int, CompanySkillMaster>();

        //        var createdCategoryIds = new HashSet<int>();
        //        var createdSkillIds = new HashSet<int>();

        //        foreach (var categoryRequest in request.Categories)
        //        {
        //            // Includes inactive records: matching data is preserved.
        //            var categoryMatches = await _context.CompanySkillMappings
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    EF.Functions.Collate(
        //                        x.CategoryName.Trim(),
        //                        "SQL_Latin1_General_CP1_CI_AS")
        //                        == categoryRequest.CategoryName)
        //                .Take(2)
        //                .ToListAsync();

        //            if (categoryMatches.Count > 1)
        //            {
        //                return Fail(
        //                    409,
        //                    $"Multiple categories named '{categoryRequest.CategoryName}' already exist. Resolve those duplicates first.");
        //            }

        //            var category = categoryMatches.FirstOrDefault();

        //            if (categoryRequest.SkillMappingId > 0 &&
        //                (category == null ||
        //                 category.SkillMappingId !=
        //                     categoryRequest.SkillMappingId))
        //            {
        //                return Fail(
        //                    400,
        //                    "The category ID and name do not match this company. Send SkillMappingId = 0 to add a new category name.");
        //            }

        //            if (category == null)
        //            {
        //                category = new CompanySkillMapping
        //                {
        //                    CompanyId = request.CompanyId,
        //                    CategoryName = categoryRequest.CategoryName!,
        //                    IsActive = categoryRequest.IsActive,
        //                    CreatedBy = request.UserId,
        //                    CreatedOn = now
        //                };

        //                _context.CompanySkillMappings.Add(category);

        //                await _context.SaveChangesAsync();

        //                createdCategoryIds.Add(category.SkillMappingId);
        //            }

        //            // Existing category is not updated or inactivated.
        //            savedCategories[category.SkillMappingId] = category;

        //            foreach (var skillRequest in
        //                categoryRequest.Skills ??
        //                new List<SaveCompanySkillItemRequest>())
        //            {
        //                var skillMatches = await _context.CompanySkillMasters
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.SkillMappingId == category.SkillMappingId &&
        //                        EF.Functions.Collate(
        //                            x.SkillName.Trim(),
        //                            "SQL_Latin1_General_CP1_CI_AS")
        //                            == skillRequest.SkillName)
        //                    .Take(2)
        //                    .ToListAsync();

        //                if (skillMatches.Count > 1)
        //                {
        //                    return Fail(
        //                        409,
        //                        $"Multiple skills named '{skillRequest.SkillName}' already exist in '{category.CategoryName}'. Resolve those duplicates first.");
        //                }

        //                var skill = skillMatches.FirstOrDefault();

        //                if (skillRequest.CompanySkillMasterId > 0 &&
        //                    (skill == null ||
        //                     skill.CompanySkillMasterId !=
        //                         skillRequest.CompanySkillMasterId))
        //                {
        //                    return Fail(
        //                        400,
        //                        "The skill ID and name do not match the selected company category. Send CompanySkillMasterId = 0 to add a new skill name.");
        //                }

        //                if (skill == null)
        //                {
        //                    skill = new CompanySkillMaster
        //                    {
        //                        CompanyId = request.CompanyId,
        //                        SkillMappingId = category.SkillMappingId,
        //                        SkillName = skillRequest.SkillName!,
        //                        IsActive = skillRequest.IsActive,
        //                        IsDisable = skillRequest.IsDisable,
        //                        CreatedBy = request.UserId,
        //                        CreatedOn = now
        //                    };

        //                    _context.CompanySkillMasters.Add(skill);

        //                    // Makes this insert visible to duplicate checks
        //                    // for later items in the same transaction.
        //                    await _context.SaveChangesAsync();

        //                    createdSkillIds.Add(skill.CompanySkillMasterId);
        //                }

        //                // Existing skill is skipped completely.
        //                // Its name, flags and audit fields stay unchanged.
        //                savedSkills[skill.CompanySkillMasterId] = skill;
        //            }
        //        }

        //        var auditIds = savedCategories.Values
        //            .SelectMany(x => new int?[]
        //            {
        //        x.CreatedBy,
        //        x.ModifiedBy
        //            })
        //            .Concat(savedSkills.Values.SelectMany(x => new int?[]
        //            {
        //        x.CreatedBy,
        //        x.ModifiedBy
        //            }))
        //            .Where(x => x.HasValue)
        //            .Select(x => x!.Value)
        //            .Distinct()
        //            .ToArray();

        //        var employeeNames = await _context.EmployeeMasters
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                auditIds.Contains(x.EmployeeId))
        //            .Select(x => new
        //            {
        //                x.EmployeeId,
        //                Name =
        //                    ((x.FirstName ?? "") + " " +
        //                     (x.LastName ?? "")).Trim()
        //            })
        //            .ToDictionaryAsync(x => x.EmployeeId, x => x.Name);

        //        string? GetName(int? id)
        //        {
        //            return id.HasValue &&
        //                   employeeNames.TryGetValue(id.Value, out var name)
        //                ? name
        //                : null;
        //        }

        //        var data = savedCategories.Values.Select(category => new
        //        {
        //            category.SkillMappingId,
        //            category.CompanyId,
        //            category.CategoryName,
        //            category.IsActive,

        //            Operation = createdCategoryIds.Contains(category.SkillMappingId)
        //                ? "Created"
        //                : "Skipped",

        //            category.CreatedBy,
        //            CreatedByName = GetName(category.CreatedBy),
        //            category.CreatedOn,

        //            category.ModifiedBy,
        //            ModifiedByName = GetName(category.ModifiedBy),
        //            category.ModifiedOn,

        //            // Only skills mentioned in this request.
        //            Skills = savedSkills.Values
        //                .Where(skill =>
        //                    skill.SkillMappingId == category.SkillMappingId)
        //                .Select(skill => new
        //                {
        //                    skill.CompanySkillMasterId,
        //                    skill.SkillMappingId,
        //                    skill.SkillName,
        //                    skill.IsActive,
        //                    skill.IsDisable,

        //                    Operation = createdSkillIds.Contains(
        //                        skill.CompanySkillMasterId)
        //                        ? "Created"
        //                        : "Skipped",

        //                    skill.CreatedBy,
        //                    CreatedByName = GetName(skill.CreatedBy),
        //                    skill.CreatedOn,

        //                    skill.ModifiedBy,
        //                    ModifiedByName = GetName(skill.ModifiedBy),
        //                    skill.ModifiedOn
        //                })
        //                .ToList()
        //        }).ToList();

        //        await transaction.CommitAsync();

        //        return Ok(new
        //        {
        //            Status = true,
        //            Message = "Missing categories and skills added. Existing records preserved.",
        //            CreatedCategoryCount = createdCategoryIds.Count,
        //            CreatedSkillCount = createdSkillIds.Count,
        //            Data = data
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Serilog.Log.Error(
        //            ex,
        //            "Failed to save company skills for CompanyId {CompanyId}.",
        //            request.CompanyId);

        //        return StatusCode(500, new
        //        {
        //            Status = false,
        //            Message = "Unable to save company categories and skills."
        //        });
        //    }
        //}


        [HttpPost("GetCompanySkills")]
        public async Task<IActionResult> GetCompanySkills(
       [FromBody] GetCompanySkillsRequest? request)
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
                {
                    return NotFound(new
                    {
                        Status = false,
                        Message = "Company not found."
                    });
                }

                var user = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsActive,

                        EmployeeName =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim()
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new
                    {
                        Status = false,
                        Message = "User not found for this company."
                    });
                }

                if (user.IsActive != true)
                {
                    return StatusCode(403, new
                    {
                        Status = false,
                        Message = "User is inactive."
                    });
                }

                // Check all company mappings, including inactive records.
                // Defaults are shown only if this company has no mappings.
                bool hasCompanyMappings = await _context.CompanySkillMappings
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyId == request.CompanyId);

                if (!hasCompanyMappings)
                {
                    var defaultCategories = await _context.SkillCategories
                        .AsNoTracking()
                        .Where(x => x.IsActive == true)
                        .OrderBy(x => x.CategoryId)
                        .Select(x => new
                        {
                            x.CategoryId,
                            x.CategoryName
                        })
                        .ToListAsync();

                    var defaultCategoryIds = defaultCategories
                        .Select(x => x.CategoryId)
                        .ToArray();

                    var defaultSkills = await _context.SkillMasters
                        .AsNoTracking()
                        .Where(x =>
                            x.IsActive == true &&
                            defaultCategoryIds.Contains(x.CategoryId))
                        .OrderBy(x => x.SkillId)
                        .Select(x => new
                        {
                            x.SkillId,
                            x.CategoryId,
                            x.SkillName
                        })
                        .ToListAsync();

                    var defaultsByCategory = defaultSkills
                        .ToLookup(x => x.CategoryId);

                    var defaultData = defaultCategories
                        .Select(category => new
                        {
                            SkillMappingId = 0,
                            DefaultCategoryId = category.CategoryId,
                            CompanyId = request.CompanyId,
                            category.CategoryName,
                            IsActive = true,

                            CreatedBy = (int?)null,
                            CreatedByName = (string?)null,
                            CreatedOn = (DateTime?)null,

                            ModifiedBy = (int?)null,
                            ModifiedByName = (string?)null,
                            ModifiedOn = (DateTime?)null,

                            Skills = defaultsByCategory[category.CategoryId]
                                .Select(skill => new
                                {
                                    CompanySkillMasterId = 0,
                                    SkillMappingId = 0,
                                    DefaultSkillId = skill.SkillId,
                                    CompanyId = request.CompanyId,
                                    skill.SkillName,

                                    IsActive = true,

                                    // Default skills are displayed as disabled.
                                    IsDisable = false,

                                    CreatedBy = (int?)null,
                                    CreatedByName = (string?)null,
                                    CreatedOn = (DateTime?)null,

                                    ModifiedBy = (int?)null,
                                    ModifiedByName = (string?)null,
                                    ModifiedOn = (DateTime?)null
                                })
                                .ToList()
                        })
                        .ToList();

                    return Ok(new
                    {
                        Status = true,
                        Message = defaultData.Count > 0
                            ? "Default categories and skills fetched successfully."
                            : "No default categories and skills found.",

                        request.CompanyId,
                        company.CompanyName,
                        request.UserId,
                        UserName = user.EmployeeName,

                        IsDefault = true,
                        TotalCount = defaultData.Count,
                        Data = defaultData
                    });
                }

                // Show active company categories only when they contain
                // at least one active company skill.
                var categories = await _context.CompanySkillMappings
                    .AsNoTracking()
                    .Where(category =>
                        category.CompanyId == request.CompanyId &&
                        category.IsActive == true &&
                        _context.CompanySkillMasters.Any(skill =>
                            skill.CompanyId == category.CompanyId &&
                            skill.SkillMappingId == category.SkillMappingId &&
                            skill.IsActive == true))
                    .OrderByDescending(category => category.SkillMappingId)
                    .ToListAsync();

                var categoryIds = categories
                    .Select(x => x.SkillMappingId)
                    .ToArray();

                // Include both disabled and enabled active skills.
                var skills = await _context.CompanySkillMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive == true &&
                        categoryIds.Contains(x.SkillMappingId))
                    .OrderByDescending(x => x.CompanySkillMasterId)
                    .ToListAsync();

                var auditIds = categories
                    .SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    })
                    .Concat(skills.SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    }))
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToArray();

                var employeeNames = await _context.EmployeeMasters
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

                string? GetName(int? id)
                {
                    return id.HasValue &&
                           employeeNames.TryGetValue(id.Value, out var name)
                        ? name
                        : null;
                }

                var skillsByCategory = skills
                    .ToLookup(x => x.SkillMappingId);

                var data = categories
                    .Select(category => new
                    {
                        category.SkillMappingId,
                        DefaultCategoryId = (int?)null,
                        category.CompanyId,
                        category.CategoryName,
                        category.IsActive,

                        category.CreatedBy,
                        CreatedByName = GetName(category.CreatedBy),
                        category.CreatedOn,

                        category.ModifiedBy,
                        ModifiedByName = GetName(category.ModifiedBy),
                        category.ModifiedOn,

                        Skills = skillsByCategory[category.SkillMappingId]
                            .Select(skill => new
                            {
                                skill.CompanySkillMasterId,
                                skill.SkillMappingId,
                                DefaultSkillId = (int?)null,
                                skill.CompanyId,
                                skill.SkillName,
                                skill.IsActive,

                                // Use the company's saved value.
                                skill.IsDisable,

                                skill.CreatedBy,
                                CreatedByName = GetName(skill.CreatedBy),
                                skill.CreatedOn,

                                skill.ModifiedBy,
                                ModifiedByName = GetName(skill.ModifiedBy),
                                skill.ModifiedOn
                            })
                            .ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Company categories and skills fetched successfully."
                        : "No active company categories with active skills found.",

                    request.CompanyId,
                    company.CompanyName,
                    request.UserId,
                    UserName = user.EmployeeName,

                    IsDefault = false,
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch company skills for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch company categories and skills."
                });
            }
        }














        [HttpPost("InactivateCompanySkills")]
        public async Task<IActionResult> InactivateCompanySkills(
    [FromBody] InactivateCompanySkillsRequest? request)
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

            if (request.CompanySkillMasterIds == null ||
                request.CompanySkillMasterIds.Count == 0 ||
                request.CompanySkillMasterIds.Any(id => id <= 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Provide valid CompanySkillMasterIds."
                });
            }

            var skillIds = request.CompanySkillMasterIds
                .Distinct()
                .ToList();

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var user = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId &&
                        x.IsActive == true)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        EmployeeName =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim()
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Active user not found for this company."
                    });
                }

                var skills = await _context.CompanySkillMasters
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        skillIds.Contains(x.CompanySkillMasterId))
                    .ToListAsync();

                if (skills.Count != skillIds.Count)
                {
                    var foundIds = skills
                        .Select(x => x.CompanySkillMasterId)
                        .ToHashSet();

                    return NotFound(new
                    {
                        Status = false,
                        Message = "One or more skills were not found for this company.",
                        MissingSkillIds = skillIds
                            .Where(id => !foundIds.Contains(id))
                            .ToList()
                    });
                }

                var now = DateTime.UtcNow;

                var changedIds = skills
                    .Where(x => x.IsActive == true)
                    .Select(x => x.CompanySkillMasterId)
                    .ToHashSet();

                foreach (var skill in skills)
                {
                    if (skill.IsActive != true)
                        continue;

                    skill.IsActive = false;
                    skill.ModifiedBy = request.UserId;
                    skill.ModifiedOn = now;
                }

                await _context.SaveChangesAsync();

                var modifierIds = skills
                    .Where(x => x.ModifiedBy.HasValue)
                    .Select(x => x.ModifiedBy!.Value)
                    .Distinct()
                    .ToArray();

                var modifierNames = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        modifierIds.Contains(x.EmployeeId))
                    .Select(x => new
                    {
                        x.EmployeeId,
                        Name = ((x.FirstName ?? "") + " " +
                                (x.LastName ?? "")).Trim()
                    })
                    .ToDictionaryAsync(x => x.EmployeeId, x => x.Name);

                var data = skills
                    .OrderByDescending(x => x.CompanySkillMasterId)
                    .Select(skill => new
                    {
                        skill.CompanySkillMasterId,
                        skill.SkillMappingId,
                        skill.CompanyId,
                        skill.SkillName,
                        skill.IsActive,
                        skill.IsDisable,

                        skill.ModifiedBy,

                        ModifiedByName =
                            skill.ModifiedBy.HasValue &&
                            modifierNames.TryGetValue(
                                skill.ModifiedBy.Value, out var name)
                                ? name
                                : null,

                        skill.ModifiedOn,

                        Operation = changedIds.Contains(skill.CompanySkillMasterId)
                            ? "Inactivated"
                            : "Already inactive"
                    })
                    .ToList();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Company skills processed successfully.",
                    UpdatedCount = changedIds.Count,
                    AlreadyInactiveCount = skills.Count - changedIds.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to inactivate company skills for CompanyId {CompanyId}.",
                    request.CompanyId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to inactivate company skills."
                });
            }
        }

        [HttpPost("AddCompanyCategorySkills")]
        public async Task<IActionResult> AddCompanyCategorySkills(
            [FromBody] AddCompanyCategorySkillsRequest? request)
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

            if (request.SkillMappingId < 0)
                return Fail(400, "Invalid SkillMappingId.");

            var categoryName = request.CategoryName?.Trim();

            if (request.SkillMappingId == 0 &&
                string.IsNullOrWhiteSpace(categoryName))
            {
                return Fail(400, "CategoryName is required.");
            }

            if (request.Skills == null ||
                request.Skills.Count == 0 ||
                request.Skills.Any(x =>
                    x == null ||
                    string.IsNullOrWhiteSpace(x.SkillName)))
            {
                return Fail(400, "Provide at least one valid skill name.");
            }

            foreach (var skill in request.Skills)
            {
                skill.SkillName = skill.SkillName!.Trim();
            }

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var companyExists = await _context.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId &&
                        x.IsActive == true)
                    .Select(x => new
                    {
                        x.EmployeeId
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Fail(
                        400,
                        "Active user not found for this company.");
                }

                var now = DateTime.UtcNow;
                bool categoryCreated = false;

                CompanySkillMapping category;

                if (request.SkillMappingId > 0)
                {
                    // Supplied ID identifies the existing category.
                    var existingCategory = await _context.CompanySkillMappings
                        .FirstOrDefaultAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.SkillMappingId == request.SkillMappingId);

                    if (existingCategory == null)
                    {
                        return Fail(
                            404,
                            "Category not found for this company.");
                    }

                    category = existingCategory;

                    // Preserve the existing category name and flags.
                }
                else
                {
                    // Match by company + category name.
                    // Includes inactive categories.
                    var matches = await _context.CompanySkillMappings
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            EF.Functions.Collate(
                                x.CategoryName.Trim(),
                                "SQL_Latin1_General_CP1_CI_AS") == categoryName)
                        .Take(2)
                        .ToListAsync();

                    if (matches.Count > 1)
                    {
                        return Fail(
                            409,
                            $"Multiple categories named '{categoryName}' already exist. Resolve those duplicates first.");
                    }

                    if (matches.Count == 1)
                    {
                        category = matches[0];
                    }
                    else
                    {
                        category = new CompanySkillMapping
                        {
                            CompanyId = request.CompanyId,
                            CategoryName = categoryName!,
                            IsActive = true,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        };

                        _context.CompanySkillMappings.Add(category);

                        await _context.SaveChangesAsync();

                        categoryCreated = true;
                    }
                }

                // Return each selected skill once, even if repeated in the request.
                var processedSkills =
                    new Dictionary<int, CompanySkillMaster>();

                var createdSkillIds = new HashSet<int>();

                foreach (var skillRequest in request.Skills)
                {
                    // Match within this company category, ignoring case.
                    // Existing inactive skills also count as matches.
                    var matches = await _context.CompanySkillMasters
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            x.SkillMappingId == category.SkillMappingId &&
                            EF.Functions.Collate(
                                x.SkillName.Trim(),
                                "SQL_Latin1_General_CP1_CI_AS")
                                == skillRequest.SkillName)
                        .Take(2)
                        .ToListAsync();

                    if (matches.Count > 1)
                    {
                        return Fail(
                            409,
                            $"Multiple skills named '{skillRequest.SkillName}' already exist in '{category.CategoryName}'. Resolve those duplicates first.");
                    }

                    CompanySkillMaster skill;

                    if (matches.Count == 1)
                    {
                        // Skip updates to existing records.
                        skill = matches[0];
                    }
                    else
                    {
                        skill = new CompanySkillMaster
                        {
                            CompanyId = request.CompanyId,
                            SkillMappingId = category.SkillMappingId,
                            SkillName = skillRequest.SkillName!,
                            IsActive = true,
                            IsDisable = skillRequest.IsDisable,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        };

                        _context.CompanySkillMasters.Add(skill);

                        // Subsequent checks can see this new skill,
                        // preventing duplicates within the same request.
                        await _context.SaveChangesAsync();

                        createdSkillIds.Add(skill.CompanySkillMasterId);
                    }

                    processedSkills[skill.CompanySkillMasterId] = skill;
                }

                var auditIds = processedSkills.Values
                    .SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    })
                    .Concat(new int?[]
                    {
                category.CreatedBy,
                category.ModifiedBy
                    })
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToArray();

                var employeeNames = await _context.EmployeeMasters
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

                string? GetName(int? id)
                {
                    return id.HasValue &&
                           employeeNames.TryGetValue(id.Value, out var name)
                        ? name
                        : null;
                }

                var data = new
                {
                    category.SkillMappingId,
                    category.CompanyId,
                    category.CategoryName,
                    category.IsActive,

                    CategoryOperation = categoryCreated
                        ? "Created"
                        : "Existing category used",

                    category.CreatedBy,
                    CreatedByName = GetName(category.CreatedBy),
                    category.CreatedOn,

                    category.ModifiedBy,
                    ModifiedByName = GetName(category.ModifiedBy),
                    category.ModifiedOn,

                    Skills = processedSkills.Values.Select(skill => new
                    {
                        skill.CompanySkillMasterId,
                        skill.SkillMappingId,
                        skill.CompanyId,
                        skill.SkillName,
                        skill.IsActive,
                        skill.IsDisable,

                        Operation = createdSkillIds.Contains(
                            skill.CompanySkillMasterId)
                            ? "Created"
                            : "Skipped",

                        skill.CreatedBy,
                        CreatedByName = GetName(skill.CreatedBy),
                        skill.CreatedOn,

                        skill.ModifiedBy,
                        ModifiedByName = GetName(skill.ModifiedBy),
                        skill.ModifiedOn
                    }).ToList()
                };

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Missing data added. Existing records preserved.",
                    CreatedSkillCount = createdSkillIds.Count,
                    SkippedSkillCount =
                        processedSkills.Count - createdSkillIds.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to add company category and skills for CompanyId {CompanyId}.",
                    request.CompanyId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to add company category and skills."
                });
            }
        }






        public class AddCompanyCategorySkillsRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }

            // 0 = new category; positive ID = existing company category.
            public int SkillMappingId { get; set; }

            // Required only for a new category.
            public string? CategoryName { get; set; }

            public List<AddCompanySkillRequest>? Skills { get; set; }
        }

        public class AddCompanySkillRequest
        {
            public string? SkillName { get; set; }

            public bool IsDisable { get; set; } = false;
        }
        public class InactivateCompanySkillsRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }

            public List<int>? CompanySkillMasterIds { get; set; }
        }

        public class GetCompanySkillsRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }
        }
        public class SaveCompanySkillsRequest
    {
        public int CompanyId { get; set; }

        public int UserId { get; set; }

        public List<SaveCompanySkillCategoryRequest>? Categories { get; set; }
    }

    public class SaveCompanySkillCategoryRequest
    {
        // 0 = create; positive ID = edit.
        public int SkillMappingId { get; set; }

        public string? CategoryName { get; set; }

        public bool IsActive { get; set; } = true;

        // null or [] means no skill changes.
        public List<SaveCompanySkillItemRequest>? Skills { get; set; }
    }

    public class SaveCompanySkillItemRequest
    {
        // 0 = add a new skill; positive ID = edit.
        public int CompanySkillMasterId { get; set; }
            public bool IsDisable { get; set; } = false;
            public string? SkillName { get; set; }

        public bool IsActive { get; set; } = true;
    }


    public class GetEmployeeSkillsRequest
        {
            public int CompanyId { get; set; }

            // EmployeeMasters.EmployeeId
            public int EmployeeId { get; set; }
        }
    }
}
