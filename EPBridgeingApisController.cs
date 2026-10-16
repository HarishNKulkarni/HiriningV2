using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.Data;
using System.Text.Json;

namespace Recruitment.Controllers
{
    [Route("api/EPBridgeingApisController")]
    [ApiController]
    public class EPBridgeingApisController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public EPBridgeingApisController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }





        //        [HttpPost("SyncEPBranchesDepartments")]
        //        public async Task<IActionResult> SyncEPBranchesDepartments(
        //            [FromBody] SyncEPBranchesDepartmentsRequest? request,[FromServices] IConfiguration configuration)
        //        {
        //            IActionResult Fail(int code, string message)
        //            {
        //                return StatusCode(code, new
        //                {
        //                    Status = false,
        //                    StatusCode = code,
        //                    Message = message
        //                });
        //            }

        //            if (request == null || request.CompanyId <= 0)
        //                return Fail(400, "Valid CompanyId is required.");

        //            try
        //            {
        //                var company = await _context.Companys
        //                    .AsNoTracking()
        //                    .Where(x => x.CompanyID == request.CompanyId)
        //                    .Select(x => new
        //                    {
        //                        x.CompanyID,
        //                        x.CompanyName,
        //                        x.EpId
        //                    })
        //                    .FirstOrDefaultAsync();

        //                if (company == null)
        //                    return Fail(404, "Company not found.");

        //                if (!(company.EpId > 0))
        //                {
        //                    return Fail(
        //                        409,
        //                        "EP synchronization is not configured for this company.");
        //                }

        //                int epOrgId = Convert.ToInt32(company.EpId);

        //                var connectionString =
        //                    configuration.GetConnectionString("EPDatabase");

        //                if (string.IsNullOrWhiteSpace(connectionString))
        //                    return Fail(500, "EP database connection is not configured.");

        //                var epBranches = new List<EPBranchSyncRow>();
        //                var epDepartments = new List<EPDepartmentSyncRow>();

        //                // Read EP data before starting the Recruitment transaction.
        //                await using (var connection = new SqlConnection(connectionString))
        //                {
        //                    await connection.OpenAsync();

        //                    using var command = connection.CreateCommand();

        //                    command.CommandText = @"
        //SELECT
        //    ID,
        //    Name,
        //    Location,
        //    City,
        //    District,
        //    State,
        //    BranchCode
        //FROM dbo.tblBranchMaster
        //WHERE OrgId = @OrgID
        //  AND IsActive = 1;

        //SELECT
        //    department.ID,
        //    department.Name,
        //    department.BranchID
        //FROM dbo.tblDepartmentMaster AS department
        //INNER JOIN dbo.tblBranchMaster AS branch
        //    ON branch.ID = department.BranchID
        //   AND branch.OrgId = department.OrgID
        //WHERE department.OrgID = @OrgID
        //  AND department.IsActive = 1
        //  AND branch.IsActive = 1;";

        //                    command.Parameters.Add(
        //                        "@OrgID",
        //                        SqlDbType.Int).Value = epOrgId;

        //                    using var reader = await command.ExecuteReaderAsync();

        //                    string? ReadString(string columnName)
        //                    {
        //                        return reader[columnName] == DBNull.Value
        //                            ? null
        //                            : Convert.ToString(reader[columnName]);
        //                    }

        //                    while (await reader.ReadAsync())
        //                    {
        //                        epBranches.Add(new EPBranchSyncRow
        //                        {
        //                            ID = Convert.ToInt32(reader["ID"]),
        //                            Name = ReadString("Name"),
        //                            Location = ReadString("Location"),
        //                            City = ReadString("City"),
        //                            District = ReadString("District"),
        //                            State = ReadString("State"),
        //                            BranchCode = ReadString("BranchCode")
        //                        });
        //                    }

        //                    if (!await reader.NextResultAsync())
        //                        return Fail(502, "EP department results were not returned.");

        //                    while (await reader.ReadAsync())
        //                    {
        //                        epDepartments.Add(new EPDepartmentSyncRow
        //                        {
        //                            ID = Convert.ToInt32(reader["ID"]),
        //                            BranchID = Convert.ToInt32(reader["BranchID"]),
        //                            Name = ReadString("Name")
        //                        });
        //                    }
        //                }

        //                if (epBranches.Any(x => x.ID <= 0) ||
        //                    epBranches.GroupBy(x => x.ID).Any(g => g.Count() > 1))
        //                {
        //                    return Fail(409, "EP returned invalid or duplicate branch IDs.");
        //                }

        //                if (epDepartments.Any(x => x.ID <= 0 || x.BranchID <= 0) ||
        //                    epDepartments.GroupBy(x => x.ID).Any(g => g.Count() > 1))
        //                {
        //                    return Fail(409, "EP returned invalid or duplicate department IDs.");
        //                }

        //                var epBranchIds = epBranches
        //                    .Select(x => x.ID)
        //                    .ToHashSet();

        //                if (epDepartments.Any(x => !epBranchIds.Contains(x.BranchID)))
        //                {
        //                    return Fail(
        //                        409,
        //                        "EP branch data changed during synchronization. Please retry.");
        //                }

        //                await using var transaction =
        //                    await _context.Database.BeginTransactionAsync(
        //                        IsolationLevel.Serializable);

        //                // Ensure the company mapping did not change while reading EP.
        //                var companyMappingMatches = await _context.Companys
        //                    .AnyAsync(x =>
        //                        x.CompanyID == request.CompanyId &&
        //                        x.EpId == epOrgId);

        //                if (!companyMappingMatches)
        //                {
        //                    return Fail(
        //                        409,
        //                        "The company's EP mapping changed. Please retry.");
        //                }

        //                var existingBranches = await _context.Branches
        //                    .AsTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EPBranchId > 0)
        //                    .ToListAsync();

        //                if (existingBranches
        //                    .GroupBy(x => x.EPBranchId)
        //                    .Any(g => g.Count() > 1))
        //                {
        //                    return Fail(
        //                        409,
        //                        "Duplicate EP branch mappings exist in Recruitment.");
        //                }

        //                var branchesByEpId = existingBranches
        //                    .ToDictionary(
        //                        x => Convert.ToInt32(x.EPBranchId),
        //                        x => x);

        //                var existingDepartments = await _context.Departments
        //                    .AsTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EPDepartmentId > 0)
        //                    .ToListAsync();

        //                // EP department ID identifies the department within the company.
        //                // Its local BranchId is updated if EP moves it to another branch.
        //                if (existingDepartments
        //                    .GroupBy(x => x.EPDepartmentId)
        //                    .Any(g => g.Count() > 1))
        //                {
        //                    return Fail(
        //                        409,
        //                        "Duplicate EP department mappings exist in Recruitment.");
        //                }

        //                var departmentsByEpId = existingDepartments
        //                    .ToDictionary(x => x.EPDepartmentId);

        //                var syncedBranches = new Dictionary<int, Branch>();
        //                var syncedDepartments = new List<Department>();

        //                var now = DateTime.UtcNow;

        //                int branchesAdded = 0;
        //                int branchesUpdated = 0;
        //                int branchesSkipped = 0;

        //                int departmentsAdded = 0;
        //                int departmentsUpdated = 0;
        //                int departmentsSkipped = 0;

        //                foreach (var epBranch in epBranches)
        //                {
        //                    if (!branchesByEpId.TryGetValue(
        //                            epBranch.ID,
        //                            out var branch))
        //                    {
        //                        branch = new Branch
        //                        {
        //                            CompanyId = request.CompanyId,
        //                            EPBranchId = epBranch.ID,

        //                            BranchName = epBranch.Name,
        //                            Location = epBranch.Location,
        //                            City = epBranch.City,
        //                            District = epBranch.District,
        //                            State = epBranch.State,
        //                            BranchCode = epBranch.BranchCode,

        //                            IsActive = true,
        //                            CreatedOn = now
        //                        };

        //                        _context.Branches.Add(branch);

        //                        branchesByEpId.Add(epBranch.ID, branch);
        //                        branchesAdded++;
        //                    }
        //                    else
        //                    {
        //                        bool changed =
        //                            branch.BranchName != epBranch.Name ||
        //                            branch.Location != epBranch.Location ||
        //                            branch.City != epBranch.City ||
        //                            branch.District != epBranch.District ||
        //                            branch.State != epBranch.State ||
        //                            branch.BranchCode != epBranch.BranchCode ||
        //                            !branch.IsActive;

        //                        if (changed)
        //                        {
        //                            branch.BranchName = epBranch.Name;
        //                            branch.Location = epBranch.Location;
        //                            branch.City = epBranch.City;
        //                            branch.District = epBranch.District;
        //                            branch.State = epBranch.State;
        //                            branch.BranchCode = epBranch.BranchCode;

        //                            branch.IsActive = true;
        //                            branch.ModifiedOn = now;

        //                            branchesUpdated++;
        //                        }
        //                        else
        //                        {
        //                            branchesSkipped++;
        //                        }
        //                    }

        //                    syncedBranches.Add(epBranch.ID, branch);
        //                }

        //                // Generate Recruitment BranchIds before inserting departments.
        //                await _context.SaveChangesAsync();

        //                foreach (var epDepartment in epDepartments)
        //                {
        //                    var localBranch = syncedBranches[epDepartment.BranchID];

        //                    if (!departmentsByEpId.TryGetValue(
        //                            epDepartment.ID,
        //                            out var department))
        //                    {
        //                        department = new Department
        //                        {
        //                            CompanyId = request.CompanyId,

        //                            // Use Recruitment's generated BranchId.
        //                            BranchId = localBranch.BranchId,

        //                            EPDepartmentId = epDepartment.ID,
        //                            DepartmentName = epDepartment.Name,

        //                            IsActive = true,
        //                            CreatedOn = now
        //                        };

        //                        _context.Departments.Add(department);

        //                        departmentsByEpId.Add(epDepartment.ID, department);
        //                        departmentsAdded++;
        //                    }
        //                    else
        //                    {
        //                        bool changed =
        //                            department.BranchId != localBranch.BranchId ||
        //                            department.DepartmentName != epDepartment.Name ||
        //                            !department.IsActive;

        //                        if (changed)
        //                        {
        //                            department.BranchId = localBranch.BranchId;
        //                            department.DepartmentName = epDepartment.Name;
        //                            department.IsActive = true;
        //                            department.ModifiedOn = now;

        //                            departmentsUpdated++;
        //                        }
        //                        else
        //                        {
        //                            departmentsSkipped++;
        //                        }
        //                    }

        //                    syncedDepartments.Add(department);
        //                }

        //                await _context.SaveChangesAsync();

        //                var departmentsByBranch = syncedDepartments
        //                    .ToLookup(x => x.BranchId);

        //                var data = syncedBranches.Values
        //                    .OrderBy(x => x.BranchName)
        //                    .Select(branch => new
        //                    {
        //                        branch.BranchId,
        //                        branch.EPBranchId,
        //                        branch.CompanyId,
        //                        branch.BranchName,
        //                        branch.BranchCode,
        //                        branch.Location,
        //                        branch.City,
        //                        branch.District,
        //                        branch.State,
        //                        branch.IsActive,

        //                        Departments = departmentsByBranch[branch.BranchId]
        //                            .OrderBy(x => x.DepartmentName)
        //                            .Select(department => new
        //                            {
        //                                department.DepartmentId,
        //                                department.EPDepartmentId,
        //                                department.CompanyId,
        //                                department.BranchId,
        //                                department.DepartmentName,
        //                                department.IsActive
        //                            })
        //                            .ToList()
        //                    })
        //                    .ToList();

        //                await transaction.CommitAsync();

        //                return Ok(new
        //                {
        //                    Status = true,
        //                    StatusCode = 200,
        //                    Message = data.Count > 0
        //                        ? "EP branches and departments synchronized successfully."
        //                        : "No active EP branches found.",

        //                    CompanyId = request.CompanyId,
        //                    CompanyName = company.CompanyName,
        //                    EPOrgId = epOrgId,

        //                    BranchesAdded = branchesAdded,
        //                    BranchesUpdated = branchesUpdated,
        //                    BranchesSkipped = branchesSkipped,

        //                    DepartmentsAdded = departmentsAdded,
        //                    DepartmentsUpdated = departmentsUpdated,
        //                    DepartmentsSkipped = departmentsSkipped,

        //                    TotalBranches = data.Count,
        //                    TotalDepartments = syncedDepartments.Count,

        //                    Data = data
        //                });
        //            }
        //            catch (DbUpdateException ex)
        //                when (ex.InnerException is SqlException sql &&
        //                      (sql.Number == 2601 || sql.Number == 2627))
        //            {
        //                Serilog.Log.Error(
        //                    ex,
        //                    "Duplicate record while synchronizing EP branches for CompanyId {CompanyId}.",
        //                    request.CompanyId);

        //                return Fail(
        //                    409,
        //                    "A branch or department conflicts with an existing Recruitment record. Check its EP mapping.");
        //            }
        //            catch (Exception ex)
        //            {
        //                Serilog.Log.Error(
        //                    ex,
        //                    "Failed to synchronize EP branches and departments for CompanyId {CompanyId}.",
        //                    request.CompanyId);

        //                return Fail(
        //                    500,
        //                    "Unable to synchronize EP branches and departments.");
        //            }
        //        }

        private sealed class CompanySyncResponse<T>
        {
            public bool Status { get; set; }
            public List<T>? Data { get; set; }
        }

        private sealed class CompanySyncBranch
        {
            public int ID { get; set; }
            public string? Name { get; set; }
            public string? Location { get; set; }
            public string? City { get; set; }
            public string? District { get; set; }
            public string? State { get; set; }
            public string? BranchCode { get; set; }
        }

        private sealed class CompanySyncDepartment
        {
            public int ID { get; set; }
            public int BranchID { get; set; }
            public string? Name { get; set; }
        }

        private sealed class CompanySyncEmployee
        {
            public int ID { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public int BranchId { get; set; }
            public int DeptId { get; set; }
        }

        private sealed class CompanySyncRole
        {
            public int RoleID { get; set; }
            public int OrgID { get; set; }
            public string? RoleName { get; set; }
            public bool Status { get; set; }
        }

        private sealed class CompanySyncMapping
        {
            public int MappingId { get; set; }
            public int UserId { get; set; }
            public int ID { get; set; }
            public int RoleId { get; set; }

            public string? Email { get; set; }
            public string? MobileNumber { get; set; }
            public string? Password { get; set; }

            public bool Status { get; set; }
        }

        [HttpPost("SyncEPCompany")]
        public async Task<IActionResult> SyncEPCompany(
            [FromBody] SyncEPCompanyRequest? request,
            [FromServices] IHttpClientFactory httpClientFactory,
            [FromServices] IConfiguration configuration)
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

            if (request == null || request.CompanyId <= 0)
                return Fail(400, "Valid CompanyId is required.");

            try
            {
                var company = await _context.Companys
                    .AsNoTracking()
                    .Where(x => x.CompanyID == request.CompanyId)
                    .Select(x => new
                    {
                        x.CompanyID,
                        x.CompanyName,
                        x.EpId
                    })
                    .FirstOrDefaultAsync();

                if (company == null)
                    return Fail(404, "Company not found.");

                if (!(company.EpId > 0))
                {
                    return Ok(new
                    {
                        Status = true,
                        StatusCode = 200,
                        Message = "EP synchronization is not enabled for this company.",
                        CompanyId = request.CompanyId,
                        EPSynchronized = false
                    });
                }

                int epOrgId = Convert.ToInt32(company.EpId);

                // Use an active company admin, as requested.
                var admin = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsAdmin == true &&
                        x.IsActive == true)
                    .OrderBy(x => x.EmployeeId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.EPID
                    })
                    .FirstOrDefaultAsync();

                if (admin == null)
                    return Fail(409, "No active admin employee exists for this company.");

                if (!(admin.EPID > 0))
                {
                    return Fail(
                        409,
                        "The selected company admin must have a valid EPID before synchronization.");
                }

                int auditUserId = admin.EmployeeId;
                int epUserId = admin.EPID.Value;

                var connectionString =
                    configuration.GetConnectionString("EPDatabase");

                var rolesUrl =
                    configuration["EPIntegration:GetAllRolesUrl"];

                var mappingsUrl =
                    configuration["EPIntegration:GetEmployeeRoleMappingsUrl"];

                var encryptionKey =
                    configuration["EncryptionSettings:StaticUniqueId"];

                if (string.IsNullOrWhiteSpace(connectionString))
                    return Fail(500, "EP database connection is not configured.");

                if (string.IsNullOrWhiteSpace(rolesUrl) ||
                    string.IsNullOrWhiteSpace(mappingsUrl))
                {
                    return Fail(500, "EP role and employee mapping URLs are required.");
                }

                if (string.IsNullOrWhiteSpace(encryptionKey))
                    return Fail(500, "Encryption key is not configured.");

                var epBranches = new List<CompanySyncBranch>();
                var epDepartments = new List<CompanySyncDepartment>();
                var epEmployees = new List<CompanySyncEmployee>();

                // Fetch external data before starting the Recruitment transaction.
                await using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT OrgID
FROM dbo.tblEmp_Registration
WHERE ID = @UserID
  AND IsActive = 1
  AND (IsDelete = 0 OR IsDelete IS NULL);";

                        command.Parameters.Add(
                            "@UserID", SqlDbType.Int).Value = epUserId;

                        var value = await command.ExecuteScalarAsync();

                        if (value == null ||
                            value == DBNull.Value ||
                            Convert.ToInt32(value) != epOrgId)
                        {
                            return Fail(
                                409,
                                "The selected admin's EP employee is inactive, missing, or belongs to another organization.");
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT ID, Name, Location, City, District, State, BranchCode
FROM dbo.tblBranchMaster
WHERE OrgId = @OrgID
  AND IsActive = 1;

SELECT d.ID, d.Name, d.BranchID
FROM dbo.tblDepartmentMaster AS d
INNER JOIN dbo.tblBranchMaster AS b
    ON b.ID = d.BranchID
   AND b.OrgId = d.OrgID
WHERE d.OrgID = @OrgID
  AND d.IsActive = 1
  AND b.IsActive = 1;

SELECT ID, FirstName, LastName, BranchId, DeptId
FROM dbo.tblEmp_Registration
WHERE OrgID = @OrgID
  AND IsActive = 1
  AND (IsDelete = 0 OR IsDelete IS NULL);";

                        command.Parameters.Add(
                            "@OrgID", SqlDbType.Int).Value = epOrgId;

                        using var reader = await command.ExecuteReaderAsync();

                        string? TextValue(string column)
                        {
                            return reader[column] == DBNull.Value
                                ? null
                                : Convert.ToString(reader[column]);
                        }

                        int IntValue(string column)
                        {
                            return reader[column] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader[column]);
                        }

                        while (await reader.ReadAsync())
                        {
                            epBranches.Add(new CompanySyncBranch
                            {
                                ID = IntValue("ID"),
                                Name = TextValue("Name"),
                                Location = TextValue("Location"),
                                City = TextValue("City"),
                                District = TextValue("District"),
                                State = TextValue("State"),
                                BranchCode = TextValue("BranchCode")
                            });
                        }

                        if (!await reader.NextResultAsync())
                            return Fail(502, "EP department results are missing.");

                        while (await reader.ReadAsync())
                        {
                            epDepartments.Add(new CompanySyncDepartment
                            {
                                ID = IntValue("ID"),
                                BranchID = IntValue("BranchID"),
                                Name = TextValue("Name")
                            });
                        }

                        if (!await reader.NextResultAsync())
                            return Fail(502, "EP employee results are missing.");

                        while (await reader.ReadAsync())
                        {
                            epEmployees.Add(new CompanySyncEmployee
                            {
                                ID = IntValue("ID"),
                                FirstName = TextValue("FirstName"),
                                LastName = TextValue("LastName"),
                                BranchId = IntValue("BranchId"),
                                DeptId = IntValue("DeptId")
                            });
                        }
                    }
                }

                var client = httpClientFactory.CreateClient("EP");

                var roleRequestUrl = QueryHelpers.AddQueryString(
                    rolesUrl,
                    "OrgID",
                    epOrgId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));

                using var roleResponse = await client.GetAsync(roleRequestUrl);

                if (!roleResponse.IsSuccessStatusCode)
                    return Fail(502, "Unable to retrieve EP roles.");

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var rolePayload =
                    JsonSerializer.Deserialize<CompanySyncResponse<CompanySyncRole>>(
                        await roleResponse.Content.ReadAsStringAsync(),
                        jsonOptions);

                if (rolePayload?.Status != true || rolePayload.Data == null)
                    return Fail(502, "EP returned an unsuccessful role response.");

                var mappingRequestUrl = QueryHelpers.AddQueryString(
                    mappingsUrl,
                    "OrgID",
                    epOrgId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));

                mappingRequestUrl = QueryHelpers.AddQueryString(
                    mappingRequestUrl,
                    "UserID",
                    epUserId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));

                using var mappingResponse =
                    await client.GetAsync(mappingRequestUrl);

                if (!mappingResponse.IsSuccessStatusCode)
                    return Fail(502, "Unable to retrieve EP employee role mappings.");

                var mappingPayload =
                    JsonSerializer.Deserialize<CompanySyncResponse<CompanySyncMapping>>(
                        await mappingResponse.Content.ReadAsStringAsync(),
                        jsonOptions);

                if (mappingPayload?.Status != true || mappingPayload.Data == null)
                {
                    return Fail(
                        502,
                        "EP returned an unsuccessful employee mapping response.");
                }

                var epRoles = rolePayload.Data;

                if (epRoles.Any(x =>
                        x == null ||
                        x.RoleID <= 0 ||
                        x.OrgID != epOrgId ||
                        string.IsNullOrWhiteSpace(x.RoleName)) ||
                    epRoles.GroupBy(x => x.RoleID).Any(g => g.Count() > 1))
                {
                    return Fail(502, "EP returned invalid or duplicate roles.");
                }

                if (epBranches.Any(x => x.ID <= 0) ||
                    epBranches.GroupBy(x => x.ID).Any(g => g.Count() > 1) ||
                    epDepartments.Any(x => x.ID <= 0 || x.BranchID <= 0) ||
                    epDepartments.GroupBy(x => x.ID).Any(g => g.Count() > 1) ||
                    epEmployees.Any(x => x.ID <= 0) ||
                    epEmployees.GroupBy(x => x.ID).Any(g => g.Count() > 1))
                {
                    return Fail(502, "EP returned invalid or duplicate reference IDs.");
                }

                if (mappingPayload.Data.Any(x => x == null))
                    return Fail(502, "EP returned an invalid employee mapping.");

                var epBranchIds = epBranches.Select(x => x.ID).ToHashSet();

                if (epDepartments.Any(x => !epBranchIds.Contains(x.BranchID)))
                {
                    return Fail(
                        409,
                        "EP branch data changed during synchronization. Please retry.");
                }

                var epEmployeesById = epEmployees.ToDictionary(x => x.ID);
                var epDepartmentsById = epDepartments.ToDictionary(x => x.ID);

                var mappings = mappingPayload.Data
                    .Where(x => x.Status)
                    .ToList();

                foreach (var mapping in mappings)
                {
                    if (mapping.UserId > 0 &&
                        mapping.ID > 0 &&
                        mapping.UserId != mapping.ID)
                    {
                        return Fail(502, "EP returned inconsistent employee IDs.");
                    }

                    if (mapping.UserId <= 0)
                        mapping.UserId = mapping.ID;

                    if (mapping.UserId <= 0 ||
                        mapping.RoleId <= 0 ||
                        !epEmployeesById.ContainsKey(mapping.UserId))
                    {
                        return Fail(
                            502,
                            "An EP mapping has an invalid or unavailable employee/role reference.");
                    }
                }

                if (mappings.GroupBy(x => x.UserId).Any(g =>
                    g.Select(x => x.RoleId).Distinct().Count() > 1))
                {
                    return Fail(
                        409,
                        "An EP employee has multiple active roles. EmployeeMasters supports only one RoleId.");
                }

                if (mappings.GroupBy(x => x.UserId).Any(g =>
                    g.Select(x => new
                    {
                        x.Email,
                        x.MobileNumber,
                        x.Password
                    }).Distinct().Count() > 1))
                {
                    return Fail(502, "EP returned conflicting employee login details.");
                }

                mappings = mappings
                    .GroupBy(x => x.UserId)
                    .Select(g => g.First())
                    .ToList();

                // Capture before skipping mappings with an unknown local role.
                var activeEpEmployeeIds = mappings
                    .Select(x => x.UserId)
                    .ToHashSet();

                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var companyStillMatches = await _context.Companys
                    .AnyAsync(x =>
                        x.CompanyID == request.CompanyId &&
                        x.EpId == epOrgId);

                var adminStillMatches = await _context.EmployeeMasters
                    .AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == auditUserId &&
                        x.EPID == epUserId &&
                        x.IsAdmin == true &&
                        x.IsActive == true);

                if (!companyStillMatches || !adminStillMatches)
                {
                    return Fail(
                        409,
                        "The company or admin mapping changed. Please retry.");
                }

                var localBranches = await _context.Branches
                    .AsTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EPBranchId > 0)
                    .ToListAsync();

                var localDepartments = await _context.Departments
                    .AsTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EPDepartmentId > 0)
                    .ToListAsync();

                var localRoles = await _context.Roles
                    .AsTracking()
                    .Where(x => x.CompanyId == request.CompanyId)
                    .ToListAsync();

                var localEmployees = await _context.EmployeeMasters
                    .AsTracking()
                    .Where(x => x.CompanyId == request.CompanyId)
                    .ToListAsync();

                if (localBranches.GroupBy(x => x.EPBranchId)
                        .Any(g => g.Count() > 1) ||
                    localDepartments.GroupBy(x => x.EPDepartmentId)
                        .Any(g => g.Count() > 1) ||
                    localRoles.Where(x => x.EPRoleId > 0)
                        .GroupBy(x => x.EPRoleId)
                        .Any(g => g.Count() > 1) ||
                    localEmployees.Where(x => x.EPID > 0)
                        .GroupBy(x => x.EPID)
                        .Any(g => g.Count() > 1))
                {
                    return Fail(
                        409,
                        "Duplicate EP mappings exist in Recruitment. Resolve them before synchronizing.");
                }

                var branchesByEpId = localBranches.ToDictionary(
                    x => Convert.ToInt32(x.EPBranchId));

                var departmentsByEpId = localDepartments.ToDictionary(
                    x => x.EPDepartmentId);

                var rolesByEpId = localRoles
                    .Where(x => x.EPRoleId > 0)
                    .ToDictionary(x => x.EPRoleId!.Value);

                var employeesByEpId = localEmployees
                    .Where(x => x.EPID > 0)
                    .ToDictionary(x => x.EPID!.Value);

                var now = DateTime.UtcNow;

                int branchesAdded = 0, branchesUpdated = 0, branchesSkipped = 0;
                int departmentsAdded = 0, departmentsUpdated = 0, departmentsSkipped = 0;
                int rolesAdded = 0, rolesUpdated = 0, rolesSkipped = 0;
                int employeesAdded = 0, employeesUpdated = 0, employeesSkipped = 0;
                int employeesDeactivated = 0, adminsSkipped = 0, missingRolesSkipped = 0;

                // 1. BRANCHES
                foreach (var source in epBranches)
                {
                    if (!branchesByEpId.TryGetValue(source.ID, out var branch))
                    {
                        branch = new Branch
                        {
                            CompanyId = request.CompanyId,
                            EPBranchId = source.ID,
                            BranchName = source.Name,
                            Location = source.Location,
                            City = source.City,
                            District = source.District,
                            State = source.State,
                            BranchCode = source.BranchCode,
                            IsActive = true,
                            CreatedBy = auditUserId,
                            CreatedOn = now
                        };

                        _context.Branches.Add(branch);
                        branchesByEpId.Add(source.ID, branch);
                        branchesAdded++;
                    }
                    else
                    {
                        bool changed =
                            branch.BranchName != source.Name ||
                            branch.Location != source.Location ||
                            branch.City != source.City ||
                            branch.District != source.District ||
                            branch.State != source.State ||
                            branch.BranchCode != source.BranchCode ||
                            !branch.IsActive;

                        if (changed)
                        {
                            branch.BranchName = source.Name;
                            branch.Location = source.Location;
                            branch.City = source.City;
                            branch.District = source.District;
                            branch.State = source.State;
                            branch.BranchCode = source.BranchCode;
                            branch.IsActive = true;
                            branch.ModifiedBy = auditUserId;
                            branch.ModifiedOn = now;
                            branchesUpdated++;
                        }
                        else
                        {
                            branchesSkipped++;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // 2. DEPARTMENTS
                foreach (var source in epDepartments)
                {
                    var branch = branchesByEpId[source.BranchID];

                    if (!departmentsByEpId.TryGetValue(source.ID, out var department))
                    {
                        department = new Department
                        {
                            CompanyId = request.CompanyId,
                            BranchId = branch.BranchId,
                            EPDepartmentId = source.ID,
                            DepartmentName = source.Name,
                            IsActive = true,
                            CreatedBy = auditUserId,
                            CreatedOn = now
                        };

                        _context.Departments.Add(department);
                        departmentsByEpId.Add(source.ID, department);
                        departmentsAdded++;
                    }
                    else
                    {
                        bool changed =
                            department.BranchId != branch.BranchId ||
                            department.DepartmentName != source.Name ||
                            !department.IsActive;

                        if (changed)
                        {
                            department.BranchId = branch.BranchId;
                            department.DepartmentName = source.Name;
                            department.IsActive = true;
                            department.ModifiedBy = auditUserId;
                            department.ModifiedOn = now;
                            departmentsUpdated++;
                        }
                        else
                        {
                            departmentsSkipped++;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // 3. ROLES
                foreach (var source in epRoles)
                {
                    string roleName = source.RoleName!.Trim();

                    if (!rolesByEpId.TryGetValue(source.RoleID, out var role))
                    {
                        role = new Recruitment.Model.Entity.Role
                        {
                            CompanyId = request.CompanyId,
                            EPRoleId = source.RoleID,
                            RoleName = roleName,
                            IsActive = source.Status,
                            CreatedBy = auditUserId,
                            CreatedOn = now
                        };

                        _context.Roles.Add(role);
                        rolesByEpId.Add(source.RoleID, role);
                        rolesAdded++;
                    }
                    else
                    {
                        bool changed =
                            role.RoleName != roleName ||
                            role.IsActive != source.Status;

                        if (changed)
                        {
                            role.RoleName = roleName;
                            role.IsActive = source.Status;
                            role.ModifiedBy = auditUserId;
                            role.ModifiedOn = now;
                            rolesUpdated++;
                        }
                        else
                        {
                            rolesSkipped++;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                var synchronizedEmployees = new List<Employee>();

                // 4. EMPLOYEES
                foreach (var mapping in mappings)
                {
                    // Preserve your requested missing-role skip.
                    if (!rolesByEpId.TryGetValue(mapping.RoleId, out var role))
                    {
                        missingRolesSkipped++;
                        continue;
                    }

                    var source = epEmployeesById[mapping.UserId];

                    employeesByEpId.TryGetValue(mapping.UserId, out var employee);

                    var mobile = mapping.MobileNumber?.Trim();
                    var roleMail = mapping.Email?.Trim();

                    if (employee == null && !string.IsNullOrWhiteSpace(mobile))
                    {
                        var mobileMatches = localEmployees
                            .Where(x => string.Equals(
                                x.Mobilenumber?.Trim(),
                                mobile,
                                StringComparison.Ordinal))
                            .ToList();

                        if (mobileMatches.Count > 1)
                        {
                            return Fail(
                                409,
                                $"Multiple local employees match EP employee {mapping.UserId}'s mobile.");
                        }

                        employee = mobileMatches.FirstOrDefault();

                        if (employee?.EPID > 0 &&
                            employee.EPID != mapping.UserId)
                        {
                            return Fail(
                                409,
                                $"The mobile for EP employee {mapping.UserId} belongs to another EP mapping.");
                        }
                    }

                    if (employee?.IsAdmin == true)
                    {
                        adminsSkipped++;
                        continue;
                    }

                    // Require an active EP branch and department with a matching relationship.
                    if (!epBranchIds.Contains(source.BranchId) ||
                        !epDepartmentsById.TryGetValue(
                            source.DeptId, out var epDepartment) ||
                        epDepartment.BranchID != source.BranchId)
                    {
                        return Fail(
                            409,
                            $"EP employee {mapping.UserId} has a missing, inactive, or mismatched branch/department.");
                    }

                    var branch = branchesByEpId[source.BranchId];
                    var department = departmentsByEpId[source.DeptId];

                    if (!string.IsNullOrWhiteSpace(roleMail) &&
                        localEmployees.Any(x =>
                            x != employee &&
                            string.Equals(
                                x.RoleMail?.Trim(),
                                roleMail,
                                StringComparison.OrdinalIgnoreCase)))
                    {
                        return Fail(
                            409,
                            $"The mapped email for EP employee {mapping.UserId} already belongs to another local employee.");
                    }

                    string? encryptedPassword = null;

                    if (!string.IsNullOrWhiteSpace(mapping.Password))
                    {
                        encryptedPassword =
                            global::Recruitment.Helper.Helper.EncryptAPIData(
                                mapping.Password,
                                encryptionKey);
                    }

                    bool isNew = employee == null;

                    if (isNew)
                    {
                        employee = new Employee
                        {
                            CompanyId = request.CompanyId,
                            IsActive = true,
                            IsAdmin = false,
                            CreatedBy = auditUserId,
                            CreatedOn = now
                        };

                        _context.EmployeeMasters.Add(employee);
                        localEmployees.Add(employee);
                    }

                    bool changed =
                        employee!.EPID != mapping.UserId ||
                        employee.FirstName != source.FirstName ||
                        employee.LastName != source.LastName ||
                        employee.Mobilenumber != mobile ||
                        employee.RoleMail != roleMail ||
                        employee.RoleId != role.RoleId ||
                        employee.BranchId != branch.BranchId ||
                        employee.DepartmentId != department.DepartmentId ||
                        (encryptedPassword != null &&
                         employee.RolePW != encryptedPassword);

                    employee.EPID = mapping.UserId;
                    employee.FirstName = source.FirstName;
                    employee.LastName = source.LastName;
                    employee.Mobilenumber = mobile;
                    employee.RoleMail = roleMail;

                    // Store Recruitment IDs, not EP IDs.
                    employee.RoleId = role.RoleId;
                    employee.BranchId = branch.BranchId;
                    employee.DepartmentId = department.DepartmentId;

                    if (encryptedPassword != null)
                        employee.RolePW = encryptedPassword;

                    // Preserve normal Password, personal Email and existing IsActive.
                    if (isNew)
                    {
                        employeesAdded++;
                    }
                    else if (changed)
                    {
                        employee.ModifiedBy = auditUserId;
                        employee.ModifiedOn = now;
                        employeesUpdated++;
                    }
                    else
                    {
                        employeesSkipped++;
                    }

                    employeesByEpId[mapping.UserId] = employee;
                    synchronizedEmployees.Add(employee);
                }

                // Preserve your requested missing-EPID deactivation behavior.
                // Use the complete active mapping response, before missing-role skips.
                foreach (var employee in localEmployees.Where(x =>
                    x.EPID.HasValue &&
                    x.EPID.Value > 0 &&
                    x.IsAdmin != true &&
                    x.IsActive &&
                    !activeEpEmployeeIds.Contains(x.EPID.Value)))
                {
                    employee.IsActive = false;
                    employee.ModifiedBy = auditUserId;
                    employee.ModifiedOn = now;
                    employeesDeactivated++;
                }

                await _context.SaveChangesAsync();

                var branchesByLocalId = branchesByEpId.Values
                    .ToDictionary(x => x.BranchId);

                var departmentsByLocalId = departmentsByEpId.Values
                    .ToDictionary(x => x.DepartmentId);

                var rolesByLocalId = rolesByEpId.Values
                    .ToDictionary(x => x.RoleId);

                var data = synchronizedEmployees
                    .Where(x => x.IsActive && x.IsAdmin != true)
                    .OrderByDescending(x => x.EmployeeId)
                    .Select(employee => new
                    {
                        employee.EmployeeId,
                        employee.EPID,
                        employee.CompanyId,

                        EmployeeName =
                            ((employee.FirstName ?? "") + " " +
                             (employee.LastName ?? "")).Trim(),

                        employee.Mobilenumber,
                        employee.Email,
                        employee.RoleMail,

                        employee.BranchId,
                        BranchName =
                            branchesByLocalId[employee.BranchId].BranchName,

                        employee.DepartmentId,
                        DepartmentName =
                            departmentsByLocalId[employee.DepartmentId].DepartmentName,

                        employee.RoleId,
                        RoleName = employee.RoleId.HasValue
                            ? rolesByLocalId[employee.RoleId.Value].RoleName
                            : null,

                        employee.IsActive,
                        employee.CreatedBy,
                        employee.CreatedOn,
                        employee.ModifiedBy,
                        employee.ModifiedOn
                    })
                    .ToList();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    StatusCode = 200,
                    Message = "EP company synchronization completed successfully.",
                    EPSynchronized = true,

                    CompanyId = request.CompanyId,
                    company.CompanyName,
                    EPOrgId = epOrgId,
                    AuditUserId = auditUserId,

                    Branches = new
                    {
                        Added = branchesAdded,
                        Updated = branchesUpdated,
                        Skipped = branchesSkipped
                    },

                    Departments = new
                    {
                        Added = departmentsAdded,
                        Updated = departmentsUpdated,
                        Skipped = departmentsSkipped
                    },

                    Roles = new
                    {
                        Added = rolesAdded,
                        Updated = rolesUpdated,
                        Skipped = rolesSkipped
                    },

                    Employees = new
                    {
                        Added = employeesAdded,
                        Updated = employeesUpdated,
                        Skipped = employeesSkipped,
                        Deactivated = employeesDeactivated,
                        AdminsSkipped = adminsSkipped,
                        MissingRolesSkipped = missingRolesSkipped
                    },

                    Data = data
                });
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                Serilog.Log.Error(
                    ex,
                    "Duplicate record during EP synchronization for CompanyId {CompanyId}.",
                    request.CompanyId);

                return Fail(
                    409,
                    "A record conflicts with an existing Recruitment record. Check unique names, codes and EP mappings.");
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql && sql.Number == 547)
            {
                Serilog.Log.Error(
                    ex,
                    "Constraint failure during EP synchronization for CompanyId {CompanyId}.",
                    request.CompanyId);

                return Fail(
                    409,
                    "A database relationship or constraint prevented synchronization.");
            }
            catch (TaskCanceledException ex)
            {
                Serilog.Log.Error(ex, "EP synchronization request timed out.");
                return Fail(504, "The EP request timed out.");
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Error(ex, "EP synchronization HTTP request failed.");
                return Fail(502, "Unable to connect to the EP API.");
            }
            catch (JsonException ex)
            {
                Serilog.Log.Error(ex, "Invalid EP synchronization response.");
                return Fail(502, "EP returned an invalid response.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "EP synchronization failed for CompanyId {CompanyId}.",
                    request.CompanyId);

                return Fail(500, "Unable to synchronize EP company data.");
            }
        }








        public class SyncEPCompanyRequest
        {
            public int CompanyId { get; set; }
        }
    }
}
