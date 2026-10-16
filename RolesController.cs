using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;

namespace Recruitment.Controllers
{
    [Route("api/RolesController")]
    [ApiController]
    public class RolesController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public RolesController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveRole([FromBody] SaveRoleRequest request)
        {
            if (request.RoleId < 0)
                return BadRequest(new
                {
                    message = "Invalid RoleId."
                });

            if (request.CompanyId <= 0)
                return BadRequest(new
                {
                    message = "A valid CompanyId is required."
                });

            var roleName = request.RoleName?.Trim();

            if (string.IsNullOrWhiteSpace(roleName))
                return BadRequest(new
                {
                    message = "Role name is required."
                });

            if (roleName.Length > 150)
                return BadRequest(new
                {
                    message = "Role name cannot exceed 150 characters."
                });

            bool isNew = request.RoleId == 0;

            var userId = isNew
                ? request.CreatedBy
                : request.ModifiedBy;

            if (userId.HasValue && userId.Value <= 0)
                return BadRequest(new
                {
                    message = "Invalid creator or modifier user ID."
                });

            Role role;

            if (isNew)
            {
                role = new Role
                {
                    CompanyId = request.CompanyId,
                    
                };
            }
            else
            {
                // Editing cannot move a role into another company.
                var existing = await _context.Roles
                    .FirstOrDefaultAsync(r =>
                        r.RoleId == request.RoleId &&
                        r.CompanyId == request.CompanyId);

                if (existing == null)
                    return NotFound(new
                    {
                        message = "Role not found for this company."
                    });

                role = existing;
            }

            // Includes inactive roles and excludes the edited role.
            // Case-insensitive after applying the migration.
            var duplicate = await _context.Roles.AnyAsync(r =>
                r.CompanyId == request.CompanyId &&
                r.RoleName == roleName &&
                r.RoleId != request.RoleId);

            if (duplicate)
                return Conflict(new
                {
                    message = "Role name already exists in this company."
                });

            role.RoleName = roleName;
            role.IsActive = request.IsActive;

            if (isNew)
            {
                role.CreatedOn = DateTime.UtcNow;
                role.CreatedBy = request.CreatedBy;

                _context.Roles.Add(role);
            }
            else
            {
                role.ModifiedOn = DateTime.UtcNow;
                role.ModifiedBy = request.ModifiedBy;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                return Conflict(new
                {
                    message = "A role with these unique details already exists."
                });
            }

            return StatusCode(isNew ? 201 : 200, new
            {
                message = isNew
                    ? "Role created successfully."
                    : "Role updated successfully.",
                data = role
            });
        }

        //[HttpGet("list")]
        //public async Task<IActionResult> GetRoles( [FromQuery] int companyId)
        //{
        //    if (companyId <= 0)
        //        return BadRequest(new
        //        {
        //            message = "A valid CompanyId is required."
        //        });

        //    var roles = await _context.Roles
        //        .AsNoTracking()
        //        .Where(r => r.CompanyId == companyId)
        //        .OrderByDescending(r => r.RoleId)
        //        .Select(r => new
        //        {
        //            r.RoleId,
        //            r.RoleName,
        //            r.CompanyId,
                  
        //            r.IsActive,
        //            r.CreatedOn,
        //            r.CreatedBy,
        //            r.ModifiedOn,
        //            r.ModifiedBy
        //        })
        //        .ToListAsync();

        //    return Ok(new
        //    {
        //        message = "Roles fetched successfully.",
        //        data = roles
        //    });
        //}




        [HttpPost("list")]
        public async Task<IActionResult> GetRoles([FromBody] GetRecruitmentRolesRequest? request, [FromServices] IHttpClientFactory httpClientFactory, [FromServices] IConfiguration configuration)
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
                return Fail(400, "A valid CompanyId is required.");

            if (request.UserId.HasValue && request.UserId.Value <= 0)
                return Fail(400, "Invalid UserId.");

            try
            {
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

                if (request.UserId.HasValue)
                {
                    var userExists = await _context.EmployeeMasters
                        .AsNoTracking()
                        .AnyAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.EmployeeId == request.UserId.Value &&
                            x.IsActive == true);

                    if (!userExists)
                    {
                        return Fail(
                            400,
                            "Active user not found for this company.");
                    }
                }

                int epOrgId = Convert.ToInt32(company.EpId);

                int addedCount = 0;
                int updatedCount = 0;
                int skippedCount = 0;

                bool synchronized = false;

                // Without EPID, return the existing Recruitment roles.
                if (epOrgId > 0)
                {
                    var endpoint = configuration[
                        "EPIntegration:GetAllRolesUrl"];

                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        return Fail(
                            500,
                            "EP role API URL is not configured.");
                    }

                    var url = Microsoft.AspNetCore.WebUtilities.QueryHelpers
                        .AddQueryString(
                            endpoint,
                            "OrgID",
                            epOrgId.ToString(
                                System.Globalization.CultureInfo.InvariantCulture));

                    var client = httpClientFactory.CreateClient("EP");

                    // Perform the external request before opening a DB transaction.
                    using var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        Serilog.Log.Warning(
                            "EP role API returned HTTP {HttpStatus} for OrgID {OrgId}.",
                            (int)response.StatusCode,
                            epOrgId);

                        return Fail(
                            502,
                            "Unable to retrieve roles from EP. Existing Recruitment roles were not changed.");
                    }

                    var json = await response.Content.ReadAsStringAsync();

                    EPRoleListResponse? epResponse;

                    try
                    {
                        epResponse =
                            System.Text.Json.JsonSerializer
                                .Deserialize<EPRoleListResponse>(
                                    json,
                                    new System.Text.Json.JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true
                                    });
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Serilog.Log.Error(
                            ex,
                            "Invalid EP role response for OrgID {OrgId}.",
                            epOrgId);

                        return Fail(
                            502,
                            "EP returned an invalid role response.");
                    }

                    if (epResponse == null ||
                        !epResponse.Status ||
                        epResponse.Data == null)
                    {
                        return Fail(
                            502,
                            "EP did not return a successful role list.");
                    }

                    var epRoles = epResponse.Data;

                    // Validate the complete response before changing local data.
                    if (epRoles.Any(x =>
                        x == null ||
                        x.RoleID <= 0 ||
                        x.OrgID != epOrgId ||
                        string.IsNullOrWhiteSpace(x.RoleName)))
                    {
                        return Fail(
                            502,
                            "EP returned invalid roles or roles belonging to a different organization.");
                    }

                    if (epRoles.GroupBy(x => x.RoleID).Any(x => x.Count() > 1))
                    {
                        return Fail(
                            502,
                            "EP returned duplicate role IDs.");
                    }

                    await using var transaction =
                        await _context.Database.BeginTransactionAsync(
                            System.Data.IsolationLevel.Serializable);

                    // Ensure the company mapping did not change during the API call.
                    var currentCompany = await _context.Companys
                        .AsNoTracking()
                        .Where(x => x.CompanyID == request.CompanyId)
                        .Select(x => new { x.EpId })
                        .FirstOrDefaultAsync();

                    if (currentCompany == null ||
                        Convert.ToInt32(currentCompany.EpId) != epOrgId)
                    {
                        return Fail(
                            409,
                            "The company's EP mapping changed. Please retry.");
                    }

                    var localRoles = await _context.Roles
                        .AsTracking()
                        .Where(x => x.CompanyId == request.CompanyId)
                        .ToListAsync();

                    var duplicateMappings = localRoles
                        .Where(x => x.EPRoleId.HasValue && x.EPRoleId.Value > 0)
                        .GroupBy(x => x.EPRoleId!.Value)
                        .Any(group => group.Count() > 1);

                    if (duplicateMappings)
                    {
                        return Fail(
                            409,
                            "Duplicate EP role mappings exist in Recruitment.");
                    }

                    var rolesByEPId = localRoles
                        .Where(x => x.EPRoleId.HasValue && x.EPRoleId.Value > 0)
                        .ToDictionary(x => x.EPRoleId!.Value);

                    var now = DateTime.UtcNow;

                    foreach (var epRole in epRoles)
                    {
                        string roleName = epRole.RoleName!.Trim();

                        // localRoles and rolesByEPId are already scoped to this company.
                        if (rolesByEPId.TryGetValue(epRole.RoleID, out var existingRole))
                        {
                            bool changed =
                                !string.Equals(
                                    existingRole.RoleName,
                                    roleName,
                                    StringComparison.Ordinal) ||
                                existingRole.IsActive != epRole.Status;

                            if (!changed)
                            {
                                skippedCount++;
                                continue;
                            }

                            existingRole.RoleName = roleName;
                            existingRole.IsActive = epRole.Status;
                            existingRole.ModifiedBy = request.UserId;
                            existingRole.ModifiedOn = now;

                            updatedCount++;
                        }
                        else
                        {
                            // A different EP role ID creates a new local role,
                            // even when another role has the same name.
                            var newRole = new Recruitment.Model.Entity.Role
                            {
                                CompanyId = request.CompanyId,
                                EPRoleId = epRole.RoleID,
                                RoleName = roleName,
                                IsActive = epRole.Status,
                                CreatedBy = request.UserId,
                                CreatedOn = now
                            };

                            _context.Roles.Add(newRole);

                            localRoles.Add(newRole);
                            rolesByEPId.Add(epRole.RoleID, newRole);

                            addedCount++;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    synchronized = true;
                }

                var roles = await _context.Roles
                    .AsNoTracking()
                    .Where(r => r.CompanyId == request.CompanyId)
                    .OrderByDescending(r => r.RoleId)
                    .Select(r => new
                    {
                        r.RoleId,
                        r.EPRoleId,
                        r.RoleName,
                        r.CompanyId,
                        r.IsActive,
                        r.CreatedOn,
                        r.CreatedBy,
                        r.ModifiedOn,
                        r.ModifiedBy
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Status = true,
                    StatusCode = 200,
                    Message = synchronized
                        ? "EP roles synchronized and roles fetched successfully."
                        : "Roles fetched successfully.",

                    EPSynchronized = synchronized,
                    AddedCount = addedCount,
                    UpdatedCount = updatedCount,
                    SkippedCount = skippedCount,

                    Data = roles
                });
            }
            catch (TaskCanceledException ex)
            {
                Serilog.Log.Error(
                    ex,
                    "EP role request timed out for CompanyId {CompanyId}.",
                    request.CompanyId);

                return Fail(504, "The EP role request timed out.");
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Unable to reach EP for CompanyId {CompanyId}.",
                    request.CompanyId);

                return Fail(502, "Unable to connect to the EP role API.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to synchronize roles for CompanyId {CompanyId}.",
                    request.CompanyId);

                return Fail(500, "Unable to synchronize or fetch roles.");
            }
        }



        [HttpDelete("delete/{roleId:int}")]
        public async Task<IActionResult> DeleteRole(
    int roleId,
    [FromQuery] int companyId)
        {
            if (roleId <= 0 || companyId <= 0)
                return BadRequest(new
                {
                    message = "Valid RoleId and CompanyId are required."
                });

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            var role = await _context.Roles.FirstOrDefaultAsync(r =>
                r.RoleId == roleId &&
                r.CompanyId == companyId);

            if (role == null)
                return NotFound(new
                {
                    message = "Role not found for this company."
                });

            var isUsed = await _context.EmployeeMasters
                .AnyAsync(e => e.RoleId == roleId);

            if (isUsed)
                return Conflict(new
                {
                    message = "Cannot delete this role because it is used in EmployeeMaster."
                });

            _context.Roles.Remove(role);

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      sql.Number == 547)
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    message = "Cannot delete this role because related records reference it."
                });
            }

            return Ok(new
            {
                message = "Role deleted successfully."
            });
        }
        public class SaveRoleRequest
        {
            public int RoleId { get; set; }
            public string? RoleName { get; set; }
            public int CompanyId { get; set; }
            public int? CreatedBy { get; set; }
            public int? ModifiedBy { get; set; }
            public bool IsActive { get; set; } = true;
        }





        public class GetRecruitmentRolesRequest
        {
            public int CompanyId { get; set; }

            // Optional Recruitment employee performing the synchronization.
            public int? UserId { get; set; }
        }

        public class EPRoleListResponse
        {
            public bool Status { get; set; }
            public string? Message { get; set; }
            public List<EPRoleResponse>? Data { get; set; }
        }

        public class EPRoleResponse
        {
            public int RoleID { get; set; }
            public string? RoleName { get; set; }
            public int OrgID { get; set; }

            public bool Status { get; set; }
        }
    }
}
