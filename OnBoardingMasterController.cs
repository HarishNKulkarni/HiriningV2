using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.Data;

namespace Recruitment.Controllers
{
    [Route("api/OnBoardingMasterController")]
    [ApiController]
    public class OnBoardingMasterController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IConfiguration _configuration;
        public OnBoardingMasterController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this.dbContext = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }

        [HttpGet("GetOnboardingDocumentTypes")]
        public async Task<IActionResult> GetOnboardingDocumentTypes()
        {
            try
            {
                var data = await dbContext.OnboardingDocumentTypes
                    .AsNoTracking()
                    .OrderBy(x => x.TypeId)
                    .Select(x => new
                    {
                        x.TypeId,
                        x.TypeName
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Document types fetched successfully."
                        : "No document types found.",
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch onboarding document types.");

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch document types."
                });
            }
        }

        [HttpPost("SaveCustomOnboardingMaster")]
        public async Task<IActionResult> SaveCustomOnboardingMaster(
    [FromBody] SaveCustomOnboardingMasterRequest? request)
        {
            IActionResult Fail(int code, string message)
            {
                return StatusCode(code, new
                {
                    Status = false,
                    Message = message
                });
            }

            if (request == null)
                return Fail(400, "Invalid request.");

            if (request.OnboardingMasterId < 0)
                return Fail(400, "Invalid OnboardingMasterId.");

            if (request.CompanyId <= 0 || request.UserId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId and UserId are required.");
            }

            var documentName = request.DocumentName?.Trim();

            if (string.IsNullOrWhiteSpace(documentName))
                return Fail(400, "DocumentName is required.");

            if (documentName.Length > 200)
            {
                return Fail(
                    400,
                    "DocumentName cannot exceed 200 characters.");
            }

            if (request.Type != 1 && request.Type != 2)
                return Fail(400, "Type must be 1 (FileUpload) or 2 (Template).");

            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var companyExists = await dbContext.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await dbContext.EmployeeMasters
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

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                var documentType = await dbContext.OnboardingDocumentTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TypeId == request.Type);

                if (documentType == null)
                    return Fail(404, "Document type not found.");

                bool isNew = request.OnboardingMasterId == 0;

                CustomOnboardingMaster master;

                if (isNew)
                {
                    master = new CustomOnboardingMaster
                    {
                        CompanyId = request.CompanyId
                    };
                }
                else
                {
                    var existingMaster = await dbContext.CustomOnboardingMasters
                        .AsTracking()
                        .FirstOrDefaultAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.OnboardingMasterId == request.OnboardingMasterId);

                    if (existingMaster == null)
                    {
                        return Fail(
                            404,
                            "Onboarding master not found for this company.");
                    }

                    master = existingMaster;
                }

                // Case-insensitive check across both types and inactive records.
                // Exclude this record when updating.
                var duplicateName = await dbContext.CustomOnboardingMasters
                    .AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingMasterId != request.OnboardingMasterId &&
                        EF.Functions.Collate(
                            x.DocumentName.Trim(),
                            "SQL_Latin1_General_CP1_CI_AS") == documentName);

                if (duplicateName)
                {
                    return Fail(
                        409,
                        "Document name already exists for this company.");
                }

                var now = DateTime.UtcNow;

                master.DocumentName = documentName;
                master.Type = request.Type;
                master.IsActive = request.IsActive;
                master.IsVisible = request.IsVisible?? (isNew ? true : master.IsVisible);
                if (isNew)
                {
                    master.CreatedBy = request.UserId;
                    master.CreatedOn = now;

                    dbContext.CustomOnboardingMasters.Add(master);
                }
                else
                {
                    // Preserve the original creation details.
                    master.ModifiedBy = request.UserId;
                    master.ModifiedOn = now;
                }

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = isNew
                        ? "Onboarding master created successfully."
                        : "Onboarding master updated successfully.",

                    Data = new
                    {
                        master.OnboardingMasterId,
                        master.CompanyId,
                        master.DocumentName,
                        master.Type,
                        TypeName = documentType.TypeName,
                        master.IsActive,
                        master.CreatedBy,
                        master.CreatedOn,
                        master.ModifiedBy,
                        master.ModifiedOn,
                        master.IsVisible,
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail(
                    409,
                    "The record changed. Refresh and try again.");
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                return Fail(
                    409,
                    "Document name already exists for this company.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save onboarding master for CompanyId {CompanyId}, MasterId {MasterId}.",
                    request.CompanyId,
                    request.OnboardingMasterId);

                return Fail(
                    500,
                    "Unable to save onboarding master.");
            }
        }

        [HttpPost("GetCustomOnboardingMasters")]
        public async Task<IActionResult> GetCustomOnboardingMasters(
      [FromBody] GetCustomOnboardingMastersRequest? request)
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

            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                // Serialize default creation for this company.
                // The lock is released when the transaction ends.
                var lockResource =
                    $"Recruitment:CustomOnboardingMasters:{request.CompanyId}";

                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            DECLARE @LockResult int;

            EXEC @LockResult = sys.sp_getapplock
                @Resource = {lockResource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;

            IF @LockResult < 0
                THROW 50001, 'Unable to acquire onboarding master initialization lock.', 1;
        ");

                var companyExists = await dbContext.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await dbContext.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                // Include inactive records: do not recreate deleted defaults.
                var anyMasterExists = await dbContext.CustomOnboardingMasters
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyId == request.CompanyId);

                if (!anyMasterExists)
                {
                    var requiredTypeIds = new[] { 1, 2 };

                    var existingTypeIds = await dbContext.OnboardingDocumentTypes
                        .AsNoTracking()
                        .Where(x => requiredTypeIds.Contains(x.TypeId))
                        .Select(x => x.TypeId)
                        .ToListAsync();

                    if (existingTypeIds.Count != requiredTypeIds.Length)
                    {
                        return Fail(
                            409,
                            "Configure document types 1 (FileUpload) and 2 (Template) before creating default masters.");
                    }

                    var now = DateTime.UtcNow;

                    dbContext.CustomOnboardingMasters.AddRange(
                        new CustomOnboardingMaster
                        {
                            CompanyId = request.CompanyId,
                            DocumentName = "Appointment Letter",
                            Type = 2,
                            IsActive = true,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        },
                        new CustomOnboardingMaster
                        {
                            CompanyId = request.CompanyId,
                            DocumentName = "Company Policies",
                            Type = 1,
                            IsActive = true,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        },
                        new CustomOnboardingMaster
                        {
                            CompanyId = request.CompanyId,
                            DocumentName = "Confirmation Letter",
                            Type = 2,
                            IsActive = true,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        }
                    );

                    await dbContext.SaveChangesAsync();
                }

                var data = await (
                    from master in dbContext.CustomOnboardingMasters
                        .AsNoTracking()

                    join type in dbContext.OnboardingDocumentTypes
                        .AsNoTracking()
                        on master.Type equals type.TypeId into typeGroup

                    from type in typeGroup.DefaultIfEmpty()

                    where master.CompanyId == request.CompanyId 
                    //&&
                        //  master.IsActive == true

                    orderby master.CreatedOn descending,
                            master.OnboardingMasterId descending

                    select new
                    {
                        master.OnboardingMasterId,
                        master.CompanyId,
                        master.DocumentName,
                        master.Type,
                        TypeName = type == null ? null : type.TypeName,
                        master.IsActive,
                        master.IsVisible,
                        master.CreatedBy,
                        master.CreatedOn,
                        master.ModifiedBy,
                        master.ModifiedOn
                    }
                ).ToListAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Onboarding masters fetched successfully."
                        : "No active onboarding masters found.",
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch onboarding masters for CompanyId {CompanyId}.",
                    request.CompanyId);

                return Fail(
                    500,
                    "Unable to fetch onboarding masters.");
            }
        }

        [HttpPost("DeleteCustomOnboardingMaster")]
        public async Task<IActionResult> DeleteCustomOnboardingMaster(
    [FromBody] DeleteCustomOnboardingMasterRequest? request)
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
                request.UserId <= 0 ||
                request.OnboardingMasterId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId, UserId and OnboardingMasterId are required.");
            }

            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var companyExists = await dbContext.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await dbContext.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                var master = await dbContext.CustomOnboardingMasters
                    .AsTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingMasterId == request.OnboardingMasterId);

                if (master == null)
                {
                    return Fail(
                        404,
                        "Onboarding master not found for this company.");
                }

                bool alreadyInactive = !master.IsActive;

                if (!alreadyInactive)
                {
                    master.IsActive = false;
                    master.ModifiedBy = request.UserId;
                    master.ModifiedOn = DateTime.UtcNow;

                    await dbContext.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = alreadyInactive
                        ? "Onboarding master is already inactive."
                        : "Onboarding master deleted successfully.",
                    Data = new
                    {
                        master.OnboardingMasterId,
                        master.CompanyId,
                        master.DocumentName,
                        master.Type,
                        master.IsActive,
                        master.CreatedBy,
                        master.CreatedOn,
                        master.ModifiedBy,
                        master.ModifiedOn
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail(
                    409,
                    "The record changed. Refresh and try again.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to deactivate onboarding master {MasterId} for CompanyId {CompanyId}.",
                    request.OnboardingMasterId,
                    request.CompanyId);

                return Fail(
                    500,
                    "Unable to delete onboarding master.");
            }
        }

        [HttpPost("SaveCustomOnboardingMasterFiles")]
        public async Task<IActionResult> SaveCustomOnboardingMasterFiles(
     [FromBody] SaveCustomOnboardingMasterFilesRequest? request)
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
                request.UserId <= 0 ||
                request.OnboardingMasterId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId, UserId and OnboardingMasterId are required.");
            }

            if (request.Type != 1 && request.Type != 2)
            {
                return Fail(
                    400,
                    "Type must be 1 (FileUpload) or 2 (Template).");
            }

            if (request.Files == null || request.Files.Count == 0)
                return Fail(400, "At least one file is required.");

            foreach (var item in request.Files)
            {
                if (item == null)
                    return Fail(400, "File item cannot be null.");

                if (item.FileId < 0)
                    return Fail(400, "Invalid FileId.");

                item.FileName = item.FileName?.Trim();
                item.FilePath = item.FilePath?.Trim();

                if (string.IsNullOrWhiteSpace(item.FileName))
                    return Fail(400, "FileName is required for every file.");

                if (item.FileName.Length > 255)
                    return Fail(400, "FileName cannot exceed 255 characters.");

                if (string.IsNullOrWhiteSpace(item.FilePath))
                    return Fail(400, "FilePath is required for every file.");

                if (item.FilePath.Length > 1000)
                    return Fail(400, "FilePath cannot exceed 1000 characters.");
            }

            if (request.Files
                .Where(x => x.FileId > 0)
                .GroupBy(x => x.FileId)
                .Any(group => group.Count() > 1))
            {
                return Fail(
                    400,
                    "The same FileId cannot appear multiple times.");
            }

            if (request.Files
                .GroupBy(x => x.FilePath!, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                return Fail(
                    409,
                    "The same file link cannot appear multiple times.");
            }

            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var companyExists = await dbContext.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await dbContext.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                var master = await dbContext.CustomOnboardingMasters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingMasterId == request.OnboardingMasterId);

                if (master == null)
                {
                    return Fail(
                        404,
                        "Onboarding master not found for this company.");
                }

                if (!master.IsActive)
                {
                    return Fail(
                        409,
                        "The selected onboarding master is inactive.");
                }

                if (master.Type != request.Type)
                {
                    return Fail(
                        400,
                        "The selected master does not belong to the supplied type.");
                }

                var documentType = await dbContext.OnboardingDocumentTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TypeId == request.Type);

                if (documentType == null)
                    return Fail(404, "Document type not found.");

                var existingFiles = await dbContext.CustomOnboardingMasterFiles
                    .AsTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingMasterId == request.OnboardingMasterId)
                    .ToListAsync();

                var existingById = existingFiles
                    .ToDictionary(x => x.FileId);

                // Validate every update before changing any records.
                foreach (var item in request.Files.Where(x => x.FileId > 0))
                {
                    if (!existingById.TryGetValue(item.FileId, out var existing))
                    {
                        return Fail(
                            404,
                            $"FileId {item.FileId} does not belong to this company and master.");
                    }

                    if (!existing.IsActive)
                    {
                        return Fail(
                            409,
                            $"FileId {item.FileId} is inactive.");
                    }
                }

                var updatedIds = request.Files
                    .Where(x => x.FileId > 0)
                    .Select(x => x.FileId)
                    .ToHashSet();

                // Include untouched active files when checking duplicate links.
                var finalPaths = existingFiles
                    .Where(x =>
                        x.IsActive &&
                        !updatedIds.Contains(x.FileId))
                    .Select(x => x.FilePath.Trim())
                    .Concat(request.Files.Select(x => x.FilePath!));

                if (finalPaths
                    .GroupBy(x => x, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
                {
                    return Fail(
                        409,
                        "A file with the same link already exists under this master.");
                }

                var now = DateTime.UtcNow;
                int createdCount = 0;
                int updatedCount = 0;

                var savedFiles = new List<CustomOnboardingMasterFile>();

                foreach (var item in request.Files)
                {
                    CustomOnboardingMasterFile file;

                    if (item.FileId == 0)
                    {
                        file = new CustomOnboardingMasterFile
                        {
                            CompanyId = request.CompanyId,
                            OnboardingMasterId = request.OnboardingMasterId,
                            IsActive = true,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        };

                        dbContext.CustomOnboardingMasterFiles.Add(file);
                        createdCount++;
                    }
                    else
                    {
                        file = existingById[item.FileId];

                        // Preserve CreatedBy and CreatedOn.
                        file.ModifiedBy = request.UserId;
                        file.ModifiedOn = now;

                        updatedCount++;
                    }

                    file.FileName = item.FileName!;
                    file.FilePath = item.FilePath!;

                    savedFiles.Add(file);
                }

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Onboarding master files saved successfully.",

                    Data = new
                    {
                        master.CompanyId,
                        master.OnboardingMasterId,
                        master.DocumentName,
                        master.Type,
                        TypeName = documentType.TypeName,

                        CreatedCount = createdCount,
                        UpdatedCount = updatedCount,

                        Files = savedFiles.Select(file => new
                        {
                            file.FileId,
                            file.CompanyId,
                            file.OnboardingMasterId,
                            file.FileName,
                            file.FilePath,
                            file.IsActive,
                            file.CreatedBy,
                            file.CreatedOn,
                            file.ModifiedBy,
                            file.ModifiedOn
                        }).ToList()
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail(
                    409,
                    "File records changed. Refresh and try again.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save onboarding master files for CompanyId {CompanyId}, MasterId {MasterId}.",
                    request.CompanyId,
                    request.OnboardingMasterId);

                return Fail(
                    500,
                    "Unable to save onboarding master files.");
            }
        }

        [HttpPost("GetCustomOnboardingMasterFiles")]
        public async Task<IActionResult> GetCustomOnboardingMasterFiles(
    [FromBody] GetCustomOnboardingMasterFilesRequest? request)
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

            int typeId = request.Type.GetValueOrDefault();
            int masterId = request.OnboardingMasterId.GetValueOrDefault();

            if (typeId < 0 || typeId > 2)
            {
                return Fail(
                    400,
                    "Type must be 0 (All), 1 (FileUpload) or 2 (Template).");
            }

            if (masterId < 0)
                return Fail(400, "OnboardingMasterId cannot be negative.");

            try
            {
                var companyExists = await dbContext.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await dbContext.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                // Validate the master only when one is selected.
                if (masterId > 0)
                {
                    var master = await dbContext.CustomOnboardingMasters
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.OnboardingMasterId == masterId);

                    if (master == null)
                    {
                        return Fail(
                            404,
                            "Onboarding master not found for this company.");
                    }

                    if (!master.IsActive)
                    {
                        return Fail(
                            409,
                            "The selected onboarding master is inactive.");
                    }

                    if (typeId > 0 && master.Type != typeId)
                    {
                        return Fail(
                            400,
                            "The selected master does not belong to the supplied type.");
                    }
                }

                if (typeId > 0)
                {
                    var typeExists = await dbContext.OnboardingDocumentTypes
                        .AsNoTracking()
                        .AnyAsync(x => x.TypeId == typeId);

                    if (!typeExists)
                        return Fail(404, "Document type not found.");
                }

                var query =
                    from file in dbContext.CustomOnboardingMasterFiles
                        .AsNoTracking()

                    join master in dbContext.CustomOnboardingMasters
                        .AsNoTracking()
                        on new
                        {
                            file.CompanyId,
                            file.OnboardingMasterId
                        }
                        equals new
                        {
                            master.CompanyId,
                            master.OnboardingMasterId
                        }

                    join type in dbContext.OnboardingDocumentTypes
                        .AsNoTracking()
                        on master.Type equals type.TypeId into typeGroup

                    from type in typeGroup.DefaultIfEmpty()

                    where file.CompanyId == request.CompanyId &&
                          file.IsActive == true &&
                          master.IsActive == true

                    select new
                    {
                        file.FileId,
                        file.CompanyId,
                        file.OnboardingMasterId,
                        master.DocumentName,
                        master.Type,
                        TypeName = type == null ? null : type.TypeName,
                        file.FileName,
                        file.FilePath,
                        file.IsActive,
                        file.CreatedBy,
                        file.CreatedOn,
                        file.ModifiedBy,
                        file.ModifiedOn
                    };

                if (typeId > 0)
                    query = query.Where(x => x.Type == typeId);

                if (masterId > 0)
                {
                    query = query.Where(x =>
                        x.OnboardingMasterId == masterId);
                }

                var files = await query
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x => x.FileId)
                    .ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = files.Count > 0
                        ? "Onboarding master files fetched successfully."
                        : "No active files found.",
                    TotalCount = files.Count,

                    Data = new
                    {
                        request.CompanyId,
                        Files = files
                    }
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch onboarding master files for CompanyId {CompanyId}, Type {Type}, MasterId {MasterId}.",
                    request.CompanyId,
                    request.Type,
                    request.OnboardingMasterId);

                return Fail(
                    500,
                    "Unable to fetch onboarding master files.");
            }
        }

        [HttpPost("DeleteCustomOnboardingMasterFile")]
        public async Task<IActionResult> DeleteCustomOnboardingMasterFile(
    [FromBody] DeleteCustomOnboardingMasterFileRequest? request)
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
                request.UserId <= 0 ||
                request.OnboardingMasterId <= 0 ||
                request.FileId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId, UserId, OnboardingMasterId and FileId are required.");
            }

            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var companyExists = await dbContext.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await dbContext.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                var masterExists = await dbContext.CustomOnboardingMasters
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingMasterId == request.OnboardingMasterId);

                if (!masterExists)
                {
                    return Fail(
                        404,
                        "Onboarding master not found for this company.");
                }

                var file = await dbContext.CustomOnboardingMasterFiles
                    .AsTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingMasterId == request.OnboardingMasterId &&
                        x.FileId == request.FileId);

                if (file == null)
                {
                    return Fail(
                        404,
                        "File not found for this company and master.");
                }

                bool alreadyInactive = !file.IsActive;

                if (!alreadyInactive)
                {
                    file.IsActive = false;
                    file.ModifiedBy = request.UserId;
                    file.ModifiedOn = DateTime.UtcNow;

                    await dbContext.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = alreadyInactive
                        ? "File is already inactive."
                        : "File deleted successfully.",

                    Data = new
                    {
                        file.FileId,
                        file.CompanyId,
                        file.OnboardingMasterId,
                        file.FileName,
                        file.FilePath,
                        file.IsActive,
                        file.CreatedBy,
                        file.CreatedOn,
                        file.ModifiedBy,
                        file.ModifiedOn
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail(
                    409,
                    "The file record changed. Refresh and try again.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to deactivate onboarding master file {FileId} " +
                    "for CompanyId {CompanyId}, MasterId {MasterId}.",
                    request.FileId,
                    request.CompanyId,
                    request.OnboardingMasterId);

                return Fail(
                    500,
                    "Unable to delete the file.");
            }
        }





        [HttpPost("SaveNoticeProbationMaster")]
        public async Task<IActionResult> SaveNoticeProbationMaster(
    [FromBody] SaveNoticeProbationMasterRequest? request)
        {
            IActionResult Fail(int code, string message)
            {
                return StatusCode(code, new
                {
                    Status = false,
                    Message = message
                });
            }

            if (request == null)
                return Fail(400, "Invalid request.");

            if (request.NoticeProbationMasterId < 0)
                return Fail(400, "Invalid NoticeProbationMasterId.");

            if (request.CompanyId <= 0 ||
                request.BranchId <= 0 ||
                request.UserId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId, BranchId and UserId are required.");
            }

            if (request.DepartmentId.GetValueOrDefault() < 0)
                return Fail(400, "DepartmentId cannot be negative.");

            // Omitted, null or zero means a branch-level default.
            int? departmentId =
                request.DepartmentId.GetValueOrDefault() == 0
                    ? null
                    : request.DepartmentId;

            var noticePeriod = request.NoticePeriod?.Trim();
            var probationPeriod = request.ProbationPeriod?.Trim();

            if (string.IsNullOrWhiteSpace(noticePeriod))
                return Fail(400, "NoticePeriod is required.");

            if (noticePeriod.Length > 100)
                return Fail(400, "NoticePeriod cannot exceed 100 characters.");

            if (string.IsNullOrWhiteSpace(probationPeriod))
                return Fail(400, "ProbationPeriod is required.");

            if (probationPeriod.Length > 100)
                return Fail(400, "ProbationPeriod cannot exceed 100 characters.");

            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var companyExists = await dbContext.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await dbContext.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                var branch = await dbContext.Branches
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.BranchId == request.BranchId)
                    .Select(x => new
                    {
                        x.BranchId,
                        x.BranchName,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (branch == null)
                    return Fail(404, "Branch not found for this company.");

                // An inactive branch can still have its setting deactivated.
                if (request.IsActive && branch.IsActive != true)
                    return Fail(409, "Select an active branch.");

                string? departmentName = null;

                if (departmentId.HasValue)
                {
                    var department = await dbContext.Departments
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            x.DepartmentId == departmentId.Value &&
                            x.BranchId == request.BranchId)
                        .Select(x => new
                        {
                            x.DepartmentName,
                            x.IsActive
                        })
                        .FirstOrDefaultAsync();

                    if (department == null)
                    {
                        return Fail(
                            400,
                            "The department does not belong to the selected company and branch.");
                    }

                    if (request.IsActive && department.IsActive != true)
                        return Fail(409, "Select an active department.");

                    departmentName = department.DepartmentName;
                }

                bool isNew = request.NoticeProbationMasterId == 0;

                OnboardingNoticeProbationMasters master;

                if (isNew)
                {
                    master = new OnboardingNoticeProbationMasters
                    {
                        CompanyId = request.CompanyId
                    };
                }
                else
                {
                    var existing = await dbContext.OnboardingNoticeProbationMasters
                        .AsTracking()
                        .FirstOrDefaultAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.NoticeProbationMasterId ==
                                request.NoticeProbationMasterId);

                    if (existing == null)
                    {
                        return Fail(
                            404,
                            "Notice and probation setting not found for this company.");
                    }

                    master = existing;
                }

                // Only one active setting for the exact company/branch/department.
                if (request.IsActive)
                {
                    var duplicateExists =
                        await dbContext.OnboardingNoticeProbationMasters
                            .AnyAsync(x =>
                                x.CompanyId == request.CompanyId &&
                                x.BranchId == request.BranchId &&
                                x.DepartmentId == departmentId &&
                                x.IsActive == true &&
                                x.NoticeProbationMasterId !=
                                    request.NoticeProbationMasterId);

                    if (duplicateExists)
                    {
                        return Fail(
                            409,
                            departmentId.HasValue
                                ? "An active setting already exists for this branch and department. Edit the existing record."
                                : "An active branch-level setting already exists. Edit the existing record.");
                    }
                }

                var now = DateTime.UtcNow;

                master.BranchId = request.BranchId;
                master.DepartmentId = departmentId;
                master.NoticePeriod = noticePeriod;
                master.ProbationPeriod = probationPeriod;
                master.IsActive = request.IsActive;

                if (isNew)
                {
                    master.CreatedBy = request.UserId;
                    master.CreatedOn = now;

                    dbContext.OnboardingNoticeProbationMasters.Add(master);
                }
                else
                {
                    master.ModifiedBy = request.UserId;
                    master.ModifiedOn = now;
                }

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = isNew
                        ? "Notice and probation setting created successfully."
                        : "Notice and probation setting updated successfully.",

                    Data = new
                    {
                        master.NoticeProbationMasterId,
                        master.CompanyId,

                        master.BranchId,
                        BranchName = branch.BranchName,

                        master.DepartmentId,
                        DepartmentName = departmentName,

                        master.NoticePeriod,
                        master.ProbationPeriod,
                        master.IsActive,

                        master.CreatedBy,
                        master.CreatedOn,
                        master.ModifiedBy,
                        master.ModifiedOn
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail(
                    409,
                    "The setting changed. Refresh and try again.");
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                return Fail(
                    409,
                    "An active setting already exists for this branch and department scope.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save notice and probation setting for CompanyId {CompanyId}, BranchId {BranchId}, DepartmentId {DepartmentId}.",
                    request.CompanyId,
                    request.BranchId,
                    departmentId);

                return Fail(
                    500,
                    "Unable to save notice and probation setting.");
            }
        }



        [HttpPost("GetNoticeProbationMasters")]
        public async Task<IActionResult> GetNoticeProbationMasters(
    [FromBody] GetNoticeProbationMastersRequest? request)
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

            int branchId = request.BranchId.GetValueOrDefault();
            int departmentId = request.DepartmentId.GetValueOrDefault();

            if (branchId < 0 || departmentId < 0)
            {
                return Fail(
                    400,
                    "BranchId and DepartmentId cannot be negative.");
            }

            try
            {
                var companyExists = await dbContext.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await dbContext.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                var mastersQuery = dbContext.OnboardingNoticeProbationMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive == true);

                if (branchId > 0)
                {
                    mastersQuery = mastersQuery.Where(x =>
                        x.BranchId == branchId);
                }

                if (departmentId > 0)
                {
                    mastersQuery = mastersQuery.Where(x =>
                        x.DepartmentId == departmentId);
                }

                var data = await (
                    from master in mastersQuery

                    join branch in dbContext.Branches.AsNoTracking()
                        on new
                        {
                            master.CompanyId,
                            master.BranchId
                        }
                        equals new
                        {
                            branch.CompanyId,
                            branch.BranchId
                        }
                        into branchGroup

                    from branch in branchGroup.DefaultIfEmpty()

                    join department in dbContext.Departments.AsNoTracking()
                        on new
                        {
                            master.CompanyId,
                            master.BranchId,
                            DepartmentId = master.DepartmentId
                        }
                        equals new
                        {
                            department.CompanyId,
                            department.BranchId,
                            DepartmentId = (int?)department.DepartmentId
                        }
                        into departmentGroup

                    from department in departmentGroup.DefaultIfEmpty()

                    orderby master.CreatedOn descending,
                            master.NoticeProbationMasterId descending

                    select new
                    {
                        master.NoticeProbationMasterId,
                        master.CompanyId,

                        master.BranchId,
                        BranchName = branch == null
                            ? null
                            : branch.BranchName,

                        master.DepartmentId,
                        DepartmentName = department == null
                            ? null
                            : department.DepartmentName,

                        master.NoticePeriod,
                        master.ProbationPeriod,
                        master.IsActive,

                        master.CreatedBy,
                        master.CreatedOn,
                        master.ModifiedBy,
                        master.ModifiedOn
                    }
                ).ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Notice and probation settings fetched successfully."
                        : "No active notice and probation settings found.",
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch notice and probation settings for CompanyId {CompanyId}, BranchId {BranchId}, DepartmentId {DepartmentId}.",
                    request.CompanyId,
                    request.BranchId,
                    request.DepartmentId);

                return Fail(
                    500,
                    "Unable to fetch notice and probation settings.");
            }
        }





        [HttpPost("SaveCandidateMasterDocuments")]
        public async Task<IActionResult> SaveCandidateMasterDocuments(
    [FromBody] SaveCandidateMasterDocumentsRequest? request)
        {
            IActionResult Fail(int code, string message)
            {
                return StatusCode(code, new
                {
                    Status = false,
                    Message = message
                });
            }

            if (request == null || request.OnboardingMasterId <= 0)
                return Fail(400, "Valid request and OnboardingMasterId are required.");

            if (request.Files == null || request.Files.Count == 0)
                return Fail(400, "At least one file is required.");

            foreach (var item in request.Files)
            {
                if (item == null)
                    return Fail(400, "File item cannot be null.");

                if (item.CandidateMasterDocumentId < 0)
                    return Fail(400, "Invalid CandidateMasterDocumentId.");

                item.FileName = item.FileName?.Trim();
                item.FilePath = item.FilePath?.Trim();

                if (string.IsNullOrWhiteSpace(item.FileName))
                    return Fail(400, "FileName is required.");

                if (item.FileName.Length > 255)
                    return Fail(400, "FileName cannot exceed 255 characters.");

                if (string.IsNullOrWhiteSpace(item.FilePath))
                    return Fail(400, "FilePath is required.");

                if (item.FilePath.Length > 1000)
                    return Fail(400, "FilePath cannot exceed 1000 characters.");
            }

            if (request.Files
                .Where(x => x.CandidateMasterDocumentId > 0)
                .GroupBy(x => x.CandidateMasterDocumentId)
                .Any(group => group.Count() > 1))
            {
                return Fail(
                    400,
                    "CandidateMasterDocumentId cannot repeat in the request.");
            }

            if (request.Files
                .GroupBy(x => x.FilePath!, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                return Fail(409, "Duplicate file links are not allowed.");
            }

            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var access = await ValidateCandidateMasterDocumentAccess(request);

                if (access.Error != null)
                    return access.Error;

                int applicationId = access.ApplicationId;

                var master = await dbContext.CustomOnboardingMasters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingMasterId == request.OnboardingMasterId);

                if (master == null)
                    return Fail(404, "Document master not found for this company.");

                if (!master.IsActive || !master.IsVisible)
                {
                    return Fail(
                        409,
                        "The selected document master is inactive or hidden.");
                }

                var existingFiles = await dbContext.CandidateMasterDocuments
                    .AsTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId &&
                        x.JobDescriptionId == request.JobDescriptionId &&
                        x.CandidateApplicationId == applicationId &&
                        x.OnboardingMasterId == request.OnboardingMasterId)
                    .ToListAsync();

                var existingById = existingFiles.ToDictionary(
                    x => x.CandidateMasterDocumentId);

                foreach (var item in request.Files
                    .Where(x => x.CandidateMasterDocumentId > 0))
                {
                    if (!existingById.TryGetValue(
                            item.CandidateMasterDocumentId, out var existing))
                    {
                        return Fail(
                            404,
                            $"File {item.CandidateMasterDocumentId} does not belong to this candidate application and master.");
                    }

                    if (!existing.IsActive)
                    {
                        return Fail(
                            409,
                            $"File {item.CandidateMasterDocumentId} is inactive.");
                    }
                }

                var editedIds = request.Files
                    .Where(x => x.CandidateMasterDocumentId > 0)
                    .Select(x => x.CandidateMasterDocumentId)
                    .ToHashSet();

                var finalPaths = existingFiles
                    .Where(x =>
                        x.IsActive &&
                        !editedIds.Contains(x.CandidateMasterDocumentId))
                    .Select(x => x.FilePath.Trim())
                    .Concat(request.Files.Select(x => x.FilePath!));

                if (finalPaths
                    .GroupBy(x => x, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
                {
                    return Fail(
                        409,
                        "A file with the same link already exists for this candidate application and master.");
                }

                var now = DateTime.UtcNow;
                int createdCount = 0;
                int updatedCount = 0;

                var savedFiles = new List<CandidateMasterDocument>();

                foreach (var item in request.Files)
                {
                    CandidateMasterDocument file;

                    if (item.CandidateMasterDocumentId == 0)
                    {
                        file = new CandidateMasterDocument
                        {
                            CompanyId = request.CompanyId,
                            CandidateId = request.CandidateId,
                            JobDescriptionId = request.JobDescriptionId,
                            CandidateApplicationId = applicationId,
                            OnboardingMasterId = request.OnboardingMasterId,
                            IsActive = true,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        };

                        dbContext.CandidateMasterDocuments.Add(file);
                        createdCount++;
                    }
                    else
                    {
                        file = existingById[item.CandidateMasterDocumentId];

                        file.ModifiedBy = request.UserId;
                        file.ModifiedOn = now;

                        updatedCount++;
                    }

                    file.FileName = item.FileName!;
                    file.FilePath = item.FilePath!;

                    savedFiles.Add(file);
                }

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Candidate master documents saved successfully.",
                    CreatedCount = createdCount,
                    UpdatedCount = updatedCount,

                    Data = new
                    {
                        request.CompanyId,
                        request.CandidateId,
                        request.JobDescriptionId,
                        CandidateApplicationId = applicationId,
                        master.OnboardingMasterId,
                        MasterDocumentName = master.DocumentName,
                        master.Type,
                        master.IsVisible,

                        Files = savedFiles.Select(file => new
                        {
                            file.CandidateMasterDocumentId,
                            file.FileName,
                            file.FilePath,
                            file.IsActive,
                            file.CreatedBy,
                            file.CreatedOn,
                            file.ModifiedBy,
                            file.ModifiedOn
                        }).ToList()
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail(409, "The file record changed. Refresh and try again.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save candidate master documents for CompanyId {CompanyId}, CandidateId {CandidateId}, JobId {JobId}.",
                    request.CompanyId,
                    request.CandidateId,
                    request.JobDescriptionId);

                return Fail(500, "Unable to save candidate master documents.");
            }
        }
        [HttpPost("GetCandidateMasterDocuments")]
        public async Task<IActionResult> GetCandidateMasterDocuments(
    [FromBody] GetCandidateMasterDocumentsRequest? request)
        {
            IActionResult Fail(int code, string message)
            {
                return StatusCode(code, new
                {
                    Status = false,
                    Message = message
                });
            }

            if (request == null)
                return Fail(400, "Invalid request.");

            try
            {
                // Existing company, employee, candidate, job,
                // application and onboarding-team access validation.
                var access = await ValidateCandidateMasterDocumentAccess(request);

                if (access.Error != null)
                    return access.Error;

                int applicationId = access.ApplicationId;

                // Get all active and visible masters for this company.
                // No master ID filter from the request.
                var masters = await (
      from master in dbContext.CustomOnboardingMasters
          .AsNoTracking()

      join type in dbContext.OnboardingDocumentTypes
          .AsNoTracking()
          on master.Type equals type.TypeId into typeGroup

      from type in typeGroup.DefaultIfEmpty()

          // Include visible, hidden, active and inactive masters.
      where master.CompanyId == request.CompanyId

      orderby master.OnboardingMasterId

      select new
      {
          master.OnboardingMasterId,
          master.DocumentName,
          master.Type,
          TypeName = type == null ? null : type.TypeName,
          master.IsVisible,
          master.IsActive
      }
  ).ToListAsync();

                var masterIds = masters
                    .Select(x => x.OnboardingMasterId)
                    .ToArray();

                // Load this candidate application's files for visible masters only.
                var files = await dbContext.CandidateMasterDocuments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId &&
                        x.JobDescriptionId == request.JobDescriptionId &&
                        x.CandidateApplicationId == applicationId &&
                        masterIds.Contains(x.OnboardingMasterId) &&
                        x.IsActive == true)
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x => x.CandidateMasterDocumentId)
                    .ToListAsync();

                var filesByMaster = files.ToLookup(
                    x => x.OnboardingMasterId);

                // Start from masters so names appear before any upload.
                var data = masters.Select(master => new
                {
                    master.OnboardingMasterId,
                    MasterDocumentName = master.DocumentName,
                    master.Type,
                    master.TypeName,
                    master.IsVisible,
                    master.IsActive,

                    FileCount = filesByMaster[
                        master.OnboardingMasterId].Count(),

                    Files = filesByMaster[master.OnboardingMasterId]
                        .Select(file => new
                        {
                            file.CandidateMasterDocumentId,
                            file.FileName,
                            file.FilePath,
                            file.IsActive,
                            file.CreatedBy,
                            file.CreatedOn,
                            file.ModifiedBy,
                            file.ModifiedOn
                        })
                        .ToList()
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Candidate master documents fetched successfully."
                        : "No active visible document masters found.",

                    request.CompanyId,
                    request.CandidateId,
                    request.JobDescriptionId,
                    CandidateApplicationId = applicationId,

                    TotalCount = data.Count,
                    TotalFileCount = files.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch candidate master documents for CompanyId {CompanyId}, CandidateId {CandidateId}, JobId {JobId}.",
                    request.CompanyId,
                    request.CandidateId,
                    request.JobDescriptionId);

                return Fail(
                    500,
                    "Unable to fetch candidate master documents.");
            }
        }

        private async Task<(IActionResult? Error, int ApplicationId)>
    ValidateCandidateMasterDocumentAccess(
        CandidateMasterDocumentReferenceRequest? request)
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
                request.UserId <= 0 ||
                request.CandidateId <= 0 ||
                request.JobDescriptionId <= 0 ||
                request.CandidateApplicationId < 0)
            {
                return (
                    Fail(400, "Provide valid company, user, candidate and job IDs."),
                    0);
            }

            var user = await dbContext.EmployeeMasters
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == request.CompanyId &&
                    x.EmployeeId == request.UserId)
                .Select(x => new
                {
                    x.IsActive,
                    x.IsAdmin
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return (Fail(404, "Employee not found for this company."), 0);

            if (user.IsActive != true)
                return (Fail(403, "Employee is inactive."), 0);

            var candidateExists = await dbContext.Candidates
                .AsNoTracking()
                .AnyAsync(x =>
                    x.CompanyId == request.CompanyId &&
                    x.CandidateId == request.CandidateId);

            if (!candidateExists)
                return (Fail(404, "Candidate not found for this company."), 0);

            var job = await dbContext.JobDescriptions
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == request.CompanyId &&
                    x.JobDescriptionId == request.JobDescriptionId)
                .Select(x => new
                {
                    TemplateId = (int?)x.TemplateId
                })
                .FirstOrDefaultAsync();

            if (job == null)
                return (Fail(404, "Job not found for this company."), 0);

            var applicationsQuery = dbContext.CandidateApplications
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == request.CompanyId &&
                    x.CandidateId == request.CandidateId &&
                    x.JobDescriptionId == request.JobDescriptionId);

            if (request.CandidateApplicationId > 0)
            {
                applicationsQuery = applicationsQuery.Where(x =>
                    x.CandidateApplicationId == request.CandidateApplicationId);
            }

            var applicationIds = await applicationsQuery
                .Select(x => x.CandidateApplicationId)
                .Take(2)
                .ToListAsync();

            if (applicationIds.Count == 0)
                return (Fail(404, "Candidate application not found."), 0);

            if (applicationIds.Count > 1)
            {
                return (
                    Fail(409, "Multiple applications exist. Send CandidateApplicationId."),
                    0);
            }

            if (user.IsAdmin != true)
            {
                var teamIds = await (
                    from member in dbContext.OnboardingTeamMembers.AsNoTracking()
                    join team in dbContext.OnboardingTeams.AsNoTracking()
                        on new
                        {
                            member.CompanyId,
                            member.OnboardingTeamId
                        }
                        equals new
                        {
                            team.CompanyId,
                            team.OnboardingTeamId
                        }
                    where member.CompanyId == request.CompanyId &&
                          member.EmployeeId == request.UserId &&
                          member.IsActive == true &&
                          team.IsActive == true
                    select team.OnboardingTeamId
                ).Distinct().ToListAsync();

                var mappings = await dbContext.OnboardingTeamTemplates
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        teamIds.Contains(x.OnboardingTeamId))
                    .Select(x => new
                    {
                        x.OnboardingTeamId,
                        x.TemplateId
                    })
                    .ToListAsync();

                bool hasAccess = teamIds.Any(teamId =>
                    !mappings.Any(x => x.OnboardingTeamId == teamId) ||
                    (job.TemplateId.HasValue &&
                     mappings.Any(x =>
                         x.OnboardingTeamId == teamId &&
                         x.TemplateId == job.TemplateId.Value)));

                if (!hasAccess)
                {
                    return (
                        Fail(
                            403,
                            "You are not assigned to onboarding for this candidate's template."),
                        0);
                }
            }

            return (null, applicationIds[0]);
        }


        public class CandidateMasterDocumentReferenceRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }

            public int CandidateId { get; set; }

            public int JobDescriptionId { get; set; }

            // 0 = resolve using candidate and job.
            public int CandidateApplicationId { get; set; }
        }

        public class SaveCandidateMasterDocumentsRequest
            : CandidateMasterDocumentReferenceRequest
        {
            public int OnboardingMasterId { get; set; }

            public List<SaveCandidateMasterDocumentItem>? Files { get; set; }
        }

        public class SaveCandidateMasterDocumentItem
        {
            // 0 = create; positive ID = edit.
            public int CandidateMasterDocumentId { get; set; }

            public string? FileName { get; set; }

            public string? FilePath { get; set; }
        }

        public class GetCandidateMasterDocumentsRequest
            : CandidateMasterDocumentReferenceRequest
        {
            // Omitted, null or 0 = all visible masters.
            public int? OnboardingMasterId { get; set; }
        }



        public class GetNoticeProbationMastersRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }

            public int? BranchId { get; set; }

            public int? DepartmentId { get; set; }
        }

        public class SaveNoticeProbationMasterRequest
        {
            public int NoticeProbationMasterId { get; set; }

            public int CompanyId { get; set; }

            public int BranchId { get; set; }

            public int? DepartmentId { get; set; }

            public int UserId { get; set; }

            public string? NoticePeriod { get; set; }

            public string? ProbationPeriod { get; set; }

            public bool IsActive { get; set; } = true;
        }

        public class DeleteCustomOnboardingMasterFileRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }

            public int OnboardingMasterId { get; set; }

            public int FileId { get; set; }
        }
        public class GetCustomOnboardingMasterFilesRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }

            public int? Type { get; set; }

            public int? OnboardingMasterId { get; set; }
        }

        public class SaveCustomOnboardingMasterFilesRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }

            public int Type { get; set; }

            public int OnboardingMasterId { get; set; }

            public List<CustomOnboardingMasterFileItem>? Files { get; set; }
        }

        public class CustomOnboardingMasterFileItem
        {
            public int FileId { get; set; }

            public string? FileName { get; set; }

            public string? FilePath { get; set; }
        }
        public class SaveCustomOnboardingMasterRequest
        {
            public int OnboardingMasterId { get; set; }

            public int CompanyId { get; set; }

            public int UserId { get; set; }

            public string? DocumentName { get; set; }

            public int Type { get; set; }
            public bool? IsVisible { get; set; }

            public bool IsActive { get; set; } = true;
        }

        public class GetCustomOnboardingMastersRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }
        }

        public class DeleteCustomOnboardingMasterRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }

            public int OnboardingMasterId { get; set; }
        }






    }
}
