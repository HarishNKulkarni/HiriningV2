using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
namespace Recruitment.Controllers

{
    [Route("api/ModuleController")]
    [ApiController]
    public class ModuleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public ModuleController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }
        [HttpPost("GetModuleList")]
        public async Task<IActionResult> GetModuleList()
        {
            try
            {
                var modules = await _context.Modules
                    .AsNoTracking()
                    .OrderBy(x => x.ModuleId)
                    .Select(x => new
                    {
                        x.ModuleId,
                        x.ModuleName
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Module list fetched successfully.",
                    Data = modules
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to fetch module list.");

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch module list."
                });
            }
        }

        [HttpPost("SaveEmployeeModuleAccess")]
        public async Task<IActionResult> SaveEmployeeModuleAccess(
            [FromBody] SaveEmployeeModuleAccessRequest? request)
        {
            if (request == null ||
                request.CompanyId <= 0 ||
                request.EmployeeId <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Valid CompanyId and EmployeeId are required."
                });
            }

            var email = request.Email?.Trim();

            if (string.IsNullOrWhiteSpace(email) ||
                email.Length > 256 ||
                !new EmailAddressAttribute().IsValid(email))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "A valid email address is required."
                });
            }

            if (request.Modules == null ||
                request.Modules.Count == 0 ||
                request.Modules.Any(x => x == null || x.ModuleId <= 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Provide at least one valid module."
                });
            }

            if (request.Modules.GroupBy(x => x.ModuleId)
                .Any(x => x.Count() > 1))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "ModuleId cannot repeat."
                });
            }

            if ((request.CreatedBy.HasValue && request.CreatedBy.Value <= 0) ||
                (request.ModifiedBy.HasValue && request.ModifiedBy.Value <= 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Invalid creator or modifier user ID."
                });
            }

            if (!string.IsNullOrEmpty(request.Password) &&
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Password cannot contain only spaces."
                });
            }

            try
            {
                var employee = await _context.EmployeeMasters
                    .FirstOrDefaultAsync(x =>
                        x.EmployeeId == request.EmployeeId &&
                        x.CompanyId == request.CompanyId);

                if (employee == null)
                {
                    return NotFound(new
                    {
                        Status = false,
                        Message = "Employee not found for this company."
                    });
                }

                if (!employee.IsActive)
                {
                    return Conflict(new
                    {
                        Status = false,
                        Message = "Cannot configure access for an inactive employee."
                    });
                }

                var duplicateUsername = await _context.EmployeeMasters
                    .AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId != request.EmployeeId &&
                        x.RoleMail != null &&
                        EF.Functions.Collate(
                            x.RoleMail.Trim(),
                            "SQL_Latin1_General_CP1_CI_AS") == email);

                if (duplicateUsername)
                {
                    return Conflict(new
                    {
                        Status = false,
                        Message = "Mapped email already belongs to another employee."
                    });
                }

                if (string.IsNullOrEmpty(request.Password) &&
                    string.IsNullOrWhiteSpace(employee.RolePW))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Password is required for the initial mapped login."
                    });
                }

                var moduleIds = request.Modules
                    .Select(x => x.ModuleId)
                    .ToList();

                var validModuleCount = await _context.Modules
                    .CountAsync(x => moduleIds.Contains(x.ModuleId));

                if (validModuleCount != moduleIds.Count)
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "One or more modules do not exist."
                    });
                }

                var existingPermissions = await _context.EmployeeModuleAccesses
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.EmployeeId &&
                        moduleIds.Contains(x.ModuleId))
                    .ToDictionaryAsync(x => x.ModuleId);

                employee.RoleMail = email;

                // Encrypt using the same helper and key as the Login API.
                // Null/empty password preserves the existing RolePW.
                if (!string.IsNullOrEmpty(request.Password))
                {
                    var staticEncyKey =
                        _configuration["EncryptionSettings:StaticUniqueId"];

                    if (string.IsNullOrWhiteSpace(staticEncyKey))
                    {
                        return StatusCode(500, new
                        {
                            Status = false,
                            Message = "Encryption key is not configured."
                        });
                    }

                    employee.RolePW = Helper.Helper.EncryptAPIData(
                        request.Password,
                        staticEncyKey);
                }

                var now = DateTime.UtcNow;

                foreach (var item in request.Modules)
                {
                    if (!existingPermissions.TryGetValue(
                            item.ModuleId, out var permission))
                    {
                        permission = new EmployeeModuleAccess
                        {
                            CompanyId = request.CompanyId,
                            EmployeeId = request.EmployeeId,
                            ModuleId = item.ModuleId,
                            CreatedOn = now,
                            CreatedBy = request.CreatedBy ?? request.ModifiedBy
                        };

                        _context.EmployeeModuleAccesses.Add(permission);
                    }
                    else
                    {
                        permission.ModifiedOn = now;
                        permission.ModifiedBy = request.ModifiedBy;
                    }

                    permission.CanAdd = item.CanAdd;
                    permission.CanEdit = item.CanEdit;
                    permission.CanView = item.CanView;
                    permission.CanDelete = item.CanDelete;
                    permission.CanDownload = item.CanDownload;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Employee module access saved successfully.",
                    CompanyId = request.CompanyId,
                    EmployeeId = request.EmployeeId
                });
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                return Conflict(new
                {
                    Status = false,
                    Message = "A duplicate access record or mapped username exists."
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to save employee module access.");

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to save employee module access."
                });
            }
        }



        [HttpPost("GetEmployeeModuleAccess")]
        public async Task<IActionResult> GetEmployeeModuleAccess(
      [FromBody] GetEmployeeModuleAccessRequest? request)
        {
            if (request == null || request.CompanyId <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Valid CompanyId is required."
                });
            }

            if (request.EmployeeIds != null &&
                request.EmployeeIds.Any(id => id < 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Employee IDs cannot be negative."
                });
            }

            if (request.BranchIds != null &&
                request.BranchIds.Any(id => id < 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Branch IDs cannot be negative."
                });
            }

            if (request.DepartmentIds != null &&
                request.DepartmentIds.Any(id => id < 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Department IDs cannot be negative."
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

                bool allEmployees =
                    request.EmployeeIds == null ||
                    request.EmployeeIds.Count == 0 ||
                    request.EmployeeIds.Contains(0);

                bool allBranches =
                    request.BranchIds == null ||
                    request.BranchIds.Count == 0 ||
                    request.BranchIds.Contains(0);

                bool allDepartments =
                    request.DepartmentIds == null ||
                    request.DepartmentIds.Count == 0 ||
                    request.DepartmentIds.Contains(0);

                var employeeIds = allEmployees
                    ? Array.Empty<int>()
                    : request.EmployeeIds!.Distinct().ToArray();

                var branchIds = allBranches
                    ? Array.Empty<int>()
                    : request.BranchIds!.Distinct().ToArray();

                var departmentIds = allDepartments
                    ? Array.Empty<int>()
                    : request.DepartmentIds!.Distinct().ToArray();

                if (!allEmployees)
                {
                    var validEmployeeCount = await _context.EmployeeMasters
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            employeeIds.Contains(x.EmployeeId));

                    if (validEmployeeCount != employeeIds.Length)
                    {
                        return BadRequest(new
                        {
                            Status = false,
                            Message = "One or more employees do not belong to this company."
                        });
                    }
                }

                if (!allBranches)
                {
                    var validBranchCount = await _context.Branches
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            branchIds.Contains(x.BranchId));

                    if (validBranchCount != branchIds.Length)
                    {
                        return BadRequest(new
                        {
                            Status = false,
                            Message = "One or more branches do not belong to this company."
                        });
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
                        return BadRequest(new
                        {
                            Status = false,
                            Message = "One or more departments do not belong to this company."
                        });
                    }
                }

                // Only employees with saved module-access records.
                var employeeQuery = _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(employee =>
                        employee.CompanyId == request.CompanyId &&
                        _context.EmployeeModuleAccesses.Any(access =>
                            access.CompanyId == employee.CompanyId &&
                            access.EmployeeId == employee.EmployeeId));

                if (!allEmployees)
                {
                    employeeQuery = employeeQuery.Where(x =>
                        employeeIds.Contains(x.EmployeeId));
                }

                if (!allBranches)
                {
                    employeeQuery = employeeQuery.Where(x =>
                        branchIds.Contains(x.BranchId));
                }

                if (!allDepartments)
                {
                    employeeQuery = employeeQuery.Where(x =>
                        departmentIds.Any(id => id == x.DepartmentId));
                }

                var employees = await employeeQuery
                    .OrderByDescending(x => x.EmployeeId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.EmpCode,
                        x.CompanyId,
                        CompanyName = company.CompanyName,

                        EmployeeName =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim(),

                        x.RoleId,

                        RoleName = _context.Roles
                            .Where(r =>
                                r.RoleId == x.RoleId &&
                                r.CompanyId == x.CompanyId)
                            .Select(r => r.RoleName)
                            .FirstOrDefault(),

                        x.BranchId,

                        BranchName = _context.Branches
                            .Where(b =>
                                b.BranchId == x.BranchId &&
                                b.CompanyId == x.CompanyId)
                            .Select(b => b.BranchName)
                            .FirstOrDefault(),

                        x.DepartmentId,

                        DepartmentName = _context.Departments
                            .Where(d =>
                                d.DepartmentId == x.DepartmentId &&
                                d.CompanyId == x.CompanyId &&
                                d.BranchId == x.BranchId)
                            .Select(d => d.DepartmentName)
                            .FirstOrDefault(),

                        x.CreatedBy,

                        CreatedByName = _context.EmployeeMasters
                            .Where(creator =>
                                creator.EmployeeId == x.CreatedBy &&
                                creator.CompanyId == x.CompanyId)
                            .Select(creator =>
                                ((creator.FirstName ?? "") + " " +
                                 (creator.LastName ?? "")).Trim())
                            .FirstOrDefault(),

                        x.CreatedOn,
                        MappedUserName = x.RoleMail,
                        x.IsActive
                    })
                    .ToListAsync();

                if (employees.Count == 0)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "No employees found for the selected filters.",
                        Data = Array.Empty<object>()
                    });
                }

                var modules = await _context.Modules
                    .AsNoTracking()
                    .OrderBy(x => x.ModuleId)
                    .Select(x => new
                    {
                        x.ModuleId,
                        x.ModuleName
                    })
                    .ToListAsync();

                var matchingEmployeeIds = employeeQuery
                    .Select(x => x.EmployeeId);

                var permissions = await _context.EmployeeModuleAccesses
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        matchingEmployeeIds.Contains(x.EmployeeId))
                    .Select(x => new
                    {
                        x.EmployeeModuleAccessId,
                        x.EmployeeId,
                        x.ModuleId,
                        x.CanAdd,
                        x.CanEdit,
                        x.CanView,
                        x.CanDelete,
                        x.CanDownload
                    })
                    .ToListAsync();

                var permissionLookup = permissions
                    .ToDictionary(x => (x.EmployeeId, x.ModuleId));

                var data = employees.Select(employee => new
                {
                    employee.EmployeeId,
                    employee.EmpCode,
                    employee.EmployeeName,

                    employee.CompanyId,
                    employee.CompanyName,

                    employee.RoleId,
                    employee.RoleName,

                    employee.BranchId,
                    employee.BranchName,

                    employee.DepartmentId,
                    employee.DepartmentName,

                    employee.CreatedBy,
                    employee.CreatedByName,
                    employee.CreatedOn,

                    employee.MappedUserName,
                    employee.IsActive,

                    Modules = modules.Select(module =>
                    {
                        permissionLookup.TryGetValue(
                            (employee.EmployeeId, module.ModuleId),
                            out var permission);

                        return new
                        {
                            module.ModuleId,
                            module.ModuleName,

                            EmployeeModuleAccessId =
                                permission?.EmployeeModuleAccessId,

                            CanAdd = permission?.CanAdd ?? false,
                            CanEdit = permission?.CanEdit ?? false,
                            CanView = permission?.CanView ?? false,
                            CanDelete = permission?.CanDelete ?? false,
                            CanDownload = permission?.CanDownload ?? false
                        };
                    }).ToList()
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = "Employee module access fetched successfully.",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch employee module access.");

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch employee module access."
                });
            }
        }















        public class SaveEmployeeModuleAccessRequest
    {
        public int CompanyId { get; set; }
        public int EmployeeId { get; set; }

        public string? Email { get; set; }

        // Required when no mapped password exists.
        // Null or empty on edit preserves the existing password.
        public string? Password { get; set; }

        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }

        public List<EmployeeModulePermissionRequest> Modules
        { get; set; } = new();
    }

    public class EmployeeModulePermissionRequest
    {
        public int ModuleId { get; set; }

        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanView { get; set; }
        public bool CanDelete { get; set; }
        public bool CanDownload { get; set; }
    }

        public class GetEmployeeModuleAccessRequest
        {
            public int CompanyId { get; set; }

            public List<int>? EmployeeIds { get; set; }

            public List<int>? BranchIds { get; set; }

            public List<int>? DepartmentIds { get; set; }
        }

        public class CheckEmployeeModuleAccessRequest
    {
        public int CompanyId { get; set; }
        public int EmployeeId { get; set; }
        public int ModuleId { get; set; }

        // Add, Edit, View, Delete, Download
        public string? Action { get; set; }
    }

}
}
