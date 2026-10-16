
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Helper;
using Recruitment.Model.Entity;
using System.Data;
using System.Net;
using System.Text.Json;


namespace IMS.Controllers
{
    [Route("api/EmployeeController")]
    [ApiController]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public EmployeeController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("CreateEmployee")]
        public IActionResult CreateEmployee([FromBody] CreateEmployeeRequest request)
        {
            try
            {
                if (request == null)
                    return Ok(new { Status = false, Message = "Invalid request" });

                if (string.IsNullOrWhiteSpace(request.FirstName))
                    return Ok(new { Status = false, Message = "FirstName is required" });

                if (request.CompanyId <= 0)
                    return Ok(new { Status = false, Message = "CompanyId is required" });

                if (request.BranchId <= 0)
                    return Ok(new { Status = false, Message = "BranchId is required" });

                if (string.IsNullOrWhiteSpace(request.Password))
                    return Ok(new { Status = false, Message = "Password is required" });

                if (string.IsNullOrWhiteSpace(request.Mobilenumber))
                    return Ok(new { Status = false, Message = "Mobilenumber is required" });

                if (string.IsNullOrWhiteSpace(request.Email))
                    return Ok(new { Status = false, Message = "Email is required" });

                // Validate EmpCode if provided
                if (!string.IsNullOrWhiteSpace(request.EmpCode))
                {
                    var normalizedEmpCode = request.EmpCode
                        .Trim()
                        .Replace(" ", "")
                        .ToLower();

                    var empCodeExists = _context.EmployeeMasters.Any(x =>
                        x.EmpCode != null && x.CompanyId == request.CompanyId&&
                        x.EmpCode
                            .Trim()
                            .Replace(" ", "")
                            .ToLower() == normalizedEmpCode &&
                        x.IsActive != false);

                    if (empCodeExists)
                    {
                        return Ok(new
                        {
                            Status = false,
                            Message = "Employee Code already exists"
                        });
                    }
                }
                if (string.IsNullOrWhiteSpace(request.EmpCode))
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "EmployeeCode is required"
                    });
                }

                var normalizedEmployeeCode =
                    NormalizeEmployeeCode(request.EmpCode);

                var existingEmployeeCodes = _context.EmployeeMasters
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive &&
                        x.EmpCode != null)
                    .Select(x => x.EmpCode)
                    .ToList();

                var duplicateEmployeeCode = existingEmployeeCodes
                    .Any(x =>
                        NormalizeEmployeeCode(x) == normalizedEmployeeCode);

                if (duplicateEmployeeCode)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message =
                            $"EmployeeCode '{request.EmpCode.Trim()}' already exists for this company"
                    });
                }

                if (!DateTime.TryParse(request.DateOfJoining, out DateTime employeeJoiningDate))
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Invalid DateOfJoining"
                    });
                }

                var company = _context.Companys
                    .FirstOrDefault(x => x.CompanyID == request.CompanyId);

                if (company == null)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Company not found"
                    });
                }

                var branch = _context.Branches
                    .FirstOrDefault(x =>
                        x.BranchId == request.BranchId &&
                        x.CompanyId == request.CompanyId &&
                        x.IsActive);

                if (branch == null)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Branch not found"
                    });
                }

                if (!company.CreatedOn.HasValue)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Company creation date not found"
                    });
                }

                if (!branch.CreatedOn.HasValue)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Branch creation date not found"
                    });
                }

                DateTime minimumAllowedDate =
                    company.CreatedOn.Value.Date > branch.CreatedOn.Value.Date
                        ? company.CreatedOn.Value.Date
                        : branch.CreatedOn.Value.Date;

                //if (employeeJoiningDate.Date < minimumAllowedDate)
                //{
                //    return Ok(new
                //    {
                //        Status = false,
                //        Message = $"Employee joining date cannot be before {minimumAllowedDate:dd-MM-yyyy}"
                //    });
                //}
                // Validate PAN if provided
                if (!string.IsNullOrWhiteSpace(request.Pan))
                {
                    // Basic PAN validation (10 characters, alphanumeric)
                    if (request.Pan.Length != 10)
                        return Ok(new { Status = false, Message = "PAN must be exactly 10 characters" });

                    var panExists = _context.EmployeeMasters
                        .Any(x => x.Pan == request.Pan && x.IsActive);

                    if (panExists)
                        return Ok(new { Status = false, Message = "PAN already exists" });
                }

                // Validate Aadhar if provided
                if (!string.IsNullOrWhiteSpace(request.Adhar))
                {
                    // Basic Aadhar validation (12 digits)
                    if (!System.Text.RegularExpressions.Regex.IsMatch(request.Adhar, @"^\d{12}$"))
                        return Ok(new { Status = false, Message = "Aadhar must be 12 digits" });

                    var adharExists = _context.EmployeeMasters
                        .Any(x => x.Adhar == request.Adhar && x.IsActive);

                    if (adharExists)
                        return Ok(new { Status = false, Message = "Aadhar already exists" });
                }

                // Validate Bank Account if provided
                if (!string.IsNullOrWhiteSpace(request.BankAccountNo))
                {
                    var bankAccountExists = _context.EmployeeMasters
                        .Any(x => x.BankAccountNo == request.BankAccountNo && x.IsActive);

                    if (bankAccountExists)
                        return Ok(new { Status = false, Message = "Bank Account Number already exists" });
                }

                // Validate IFSC if provided
                if (!string.IsNullOrWhiteSpace(request.IFSC))
                {
                    // Basic IFSC validation (11 characters, alphanumeric)
                    if (request.IFSC.Length != 11)
                        return Ok(new { Status = false, Message = "IFSC must be exactly 11 characters" });
                }

                DateTime? doj = null;

                if (!string.IsNullOrWhiteSpace(request.DateOfJoining))
                {
                    if (!DateTime.TryParseExact(
                            request.DateOfJoining,
                            "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out DateTime joiningDate))
                    {
                        return Ok(new
                        {
                            Status = false,
                            Message = "DateOfJoining must be in yyyy-MM-dd format"
                        });
                    }

                    doj = joiningDate;
                }

                var companyExists = _context.Companys
                    .Any(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Ok(new { Status = false, Message = "Company not found" });

                var branchExists = _context.Branches
                    .Any(x => x.BranchId == request.BranchId
                           && x.CompanyId == request.CompanyId
                           && x.IsActive);

                if (!branchExists)
                    return Ok(new { Status = false, Message = "Branch not found for this company" });

                var mobileExists = _context.EmployeeMasters
                    .Any(x => x.Mobilenumber == request.Mobilenumber);

                if (mobileExists)
                    return Ok(new { Status = false, Message = "Mobile number already exists" });

                var emailExists = _context.EmployeeMasters
                    .Any(x => x.Email == request.Email);

                if (emailExists)
                    return Ok(new { Status = false, Message = "Email already exists" });

                var staticEncyKey = _configuration["EncryptionSettings:StaticUniqueId"];

                var encryptedPassword = global::Recruitment.Helper.Helper.EncryptAPIData(
                    request.Password,
                    staticEncyKey
                );

                var employee = new Recruitment.Model.Entity.Employee
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    CompanyId = request.CompanyId,
                    BranchId = request.BranchId,
                    DepartmentId = request.DepartmentId,
                    Password = encryptedPassword,
                    Mobilenumber = request.Mobilenumber,
                    Email = request.Email,
                    Profile = request.Profile,
                    RoleId = request.RoleId,
                    City = request.City,
                    District = request.District,
                    State = request.State,
                    Location = request.Location,
                    DateOfJoining = doj,
                    EmergencyNumber = request.EmergencyNumber,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.Now,
                    IsActive = true,

                    // New fields
                    EmpCode = request.EmpCode,
                    Designation = request.Designation,
                    ReportingToManagerCode = request.ReportingToManagerCode,
                    Pan = request.Pan,
                    Adhar = request.Adhar,
                    BankName = request.BankName,
                    BankAccountNo = request.BankAccountNo,
                    IFSC = request.IFSC
                };

                _context.EmployeeMasters.Add(employee);
                _context.SaveChanges();

                return Ok(new
                {
                    Status = true,
                    Message = "Employee created successfully",
                    EmployeeId = employee.EmployeeId
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }


        public static string NormalizeEmployeeCode(string? employeeCode)
        {
            if (string.IsNullOrWhiteSpace(employeeCode))
                return string.Empty;

            return new string(
                employeeCode
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray());
        }
        [HttpPost]
        [Route("EditEmployee")]
        public IActionResult EditEmployee([FromBody] EditEmployeeRequest request)
        {
            try
            {
                if (request == null)
                    return Ok(new { Status = false, Message = "Invalid request" });

                if (request.EmployeeId <= 0)
                    return Ok(new { Status = false, Message = "EmployeeId is required" });

                if (string.IsNullOrWhiteSpace(request.FirstName))
                    return Ok(new { Status = false, Message = "FirstName is required" });

                if (request.BranchId <= 0)
                    return Ok(new { Status = false, Message = "BranchId is required" });

                if (request.DepartmentId <= 0)
                    return Ok(new { Status = false, Message = "DepartmentId is required" });

                if (string.IsNullOrWhiteSpace(request.Mobilenumber))
                    return Ok(new { Status = false, Message = "Mobilenumber is required" });

                if (string.IsNullOrWhiteSpace(request.Email))
                    return Ok(new { Status = false, Message = "Email is required" });

                var employee = _context.EmployeeMasters
                    .FirstOrDefault(x => x.EmployeeId == request.EmployeeId && x.IsActive);

                if (employee == null)
                    return Ok(new { Status = false, Message = "Employee not found" });

                // Validate EmpCode if provided and changed
                if (!string.IsNullOrWhiteSpace(request.EmpCode))
                {
                    if (request.EmpCode != employee.EmpCode)
                    {
                        var empCodeExists = _context.EmployeeMasters
                            .Any(x => x.EmpCode == request.EmpCode
                                   && x.EmployeeId != request.EmployeeId
                                   && x.IsActive);

                        if (empCodeExists)
                            return Ok(new { Status = false, Message = "Employee Code already exists" });
                    }
                }

                // Validate PAN if provided and changed
                if (!string.IsNullOrWhiteSpace(request.Pan))
                {
                    // Basic PAN validation (10 characters, alphanumeric)
                    if (request.Pan.Length != 10)
                        return Ok(new { Status = false, Message = "PAN must be exactly 10 characters" });

                    if (request.Pan != employee.Pan)
                    {
                        var panExists = _context.EmployeeMasters
                            .Any(x => x.Pan == request.Pan
                                   && x.EmployeeId != request.EmployeeId
                                   && x.IsActive);

                        if (panExists)
                            return Ok(new { Status = false, Message = "PAN already exists" });
                    }
                }

                // Validate Aadhar if provided and changed
                if (!string.IsNullOrWhiteSpace(request.Adhar))
                {
                    // Basic Aadhar validation (12 digits)
                    if (!System.Text.RegularExpressions.Regex.IsMatch(request.Adhar, @"^\d{12}$"))
                        return Ok(new { Status = false, Message = "Aadhar must be 12 digits" });

                    if (request.Adhar != employee.Adhar)
                    {
                        var adharExists = _context.EmployeeMasters
                            .Any(x => x.Adhar == request.Adhar
                                   && x.EmployeeId != request.EmployeeId
                                   && x.IsActive);

                        if (adharExists)
                            return Ok(new { Status = false, Message = "Aadhar already exists" });
                    }
                }

                // Validate Bank Account if provided and changed
                if (!string.IsNullOrWhiteSpace(request.BankAccountNo))
                {
                    if (request.BankAccountNo != employee.BankAccountNo)
                    {
                        var bankAccountExists = _context.EmployeeMasters
                            .Any(x => x.BankAccountNo == request.BankAccountNo
                                   && x.EmployeeId != request.EmployeeId
                                   && x.IsActive);

                        if (bankAccountExists)
                            return Ok(new { Status = false, Message = "Bank Account Number already exists" });
                    }
                }

                // Validate IFSC if provided
                if (!string.IsNullOrWhiteSpace(request.IFSC))
                {
                    // Basic IFSC validation (11 characters, alphanumeric)
                    if (request.IFSC.Length != 11)
                        return Ok(new { Status = false, Message = "IFSC must be exactly 11 characters" });
                }

                var branchExists = _context.Branches
                    .Any(x => x.BranchId == request.BranchId && x.IsActive == true);

                if (!branchExists)
                    return Ok(new { Status = false, Message = "Branch not found for this company" });

                var departmentExists = _context.Departments
                    .Any(x => x.DepartmentId == request.DepartmentId
                           && x.BranchId == request.BranchId
                           && x.IsActive);

                if (!departmentExists)
                    return Ok(new { Status = false, Message = "Department not found for this branch" });

                var mobileExists = _context.EmployeeMasters
                    .Any(x => x.Mobilenumber == request.Mobilenumber
                           && x.EmployeeId != request.EmployeeId);

                if (mobileExists)
                    return Ok(new { Status = false, Message = "Mobile number already exists" });

                var emailExists = _context.EmployeeMasters
                    .Any(x => x.Email == request.Email
                           && x.EmployeeId != request.EmployeeId);

                if (emailExists)
                    return Ok(new { Status = false, Message = "Email already exists" });

                DateTime? doj = null;

                if (!string.IsNullOrWhiteSpace(request.DateOfJoining))
                {
                    if (!DateTime.TryParseExact(
                            request.DateOfJoining,
                            "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out DateTime joiningDate))
                    {
                        return Ok(new
                        {
                            Status = false,
                            Message = "DateOfJoining must be in yyyy-MM-dd format"
                        });
                    }

                    doj = joiningDate;
                }

                employee.FirstName = request.FirstName;
                employee.LastName = request.LastName;
                employee.BranchId = request.BranchId;
                employee.DepartmentId = request.DepartmentId;
                employee.Mobilenumber = request.Mobilenumber;
                employee.Email = request.Email;
                employee.Profile = request.Profile;
                employee.City = request.City;
                employee.District = request.District;
                employee.State = request.State;
                employee.Location = request.Location;
                employee.DateOfJoining = doj;
                employee.EmergencyNumber = request.EmergencyNumber;
                employee.ModifiedBy = request.ModifiedBy;
                employee.ModifiedOn = DateTime.Now;

                // Update new fields if provided
                if (!string.IsNullOrWhiteSpace(request.EmpCode))
                    employee.EmpCode = request.EmpCode;

                if (!string.IsNullOrWhiteSpace(request.Designation))
                    employee.Designation = request.Designation;

                if (!string.IsNullOrWhiteSpace(request.ReportingToManagerCode))
                    employee.ReportingToManagerCode = request.ReportingToManagerCode;

                if (!string.IsNullOrWhiteSpace(request.Pan))
                    employee.Pan = request.Pan;

                if (!string.IsNullOrWhiteSpace(request.Adhar))
                    employee.Adhar = request.Adhar;

                if (!string.IsNullOrWhiteSpace(request.BankName))
                    employee.BankName = request.BankName;

                if (!string.IsNullOrWhiteSpace(request.BankAccountNo))
                    employee.BankAccountNo = request.BankAccountNo;

                if (!string.IsNullOrWhiteSpace(request.IFSC))
                    employee.IFSC = request.IFSC;

                if (!string.IsNullOrWhiteSpace(request.Password))
                {
                    var staticEncyKey = _configuration["EncryptionSettings:StaticUniqueId"];

                    employee.Password = global::Recruitment.Helper.Helper.EncryptAPIData(
                        request.Password,
                        staticEncyKey
                    );
                }

                _context.SaveChanges();

                return Ok(new
                {
                    Status = true,
                    Message = "Employee updated successfully"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("GetEmployeeList")]
        public async Task<IActionResult> GetEmployeeList(
       [FromBody] GetEmployeeListRequest? request)
        {
            if (request == null || request.CompanyId <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Valid CompanyId is required."
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

                bool allBranches =
                    request.BranchIds == null ||
                    request.BranchIds.Count == 0 ||
                    request.BranchIds.Contains(0);

                bool allDepartments =
                    request.DepartmentIds == null ||
                    request.DepartmentIds.Count == 0 ||
                    request.DepartmentIds.Contains(0);

                var selectedBranchIds = allBranches
                    ? Array.Empty<int>()
                    : request.BranchIds!.Distinct().ToArray();

                var selectedDepartmentIds = allDepartments
                    ? Array.Empty<int>()
                    : request.DepartmentIds!.Distinct().ToArray();

                if (!allBranches)
                {
                    var validBranchCount = await _context.Branches
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            selectedBranchIds.Contains(x.BranchId));

                    if (validBranchCount != selectedBranchIds.Length)
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
                            selectedDepartmentIds.Contains(x.DepartmentId));

                    if (validDepartmentCount != selectedDepartmentIds.Length)
                    {
                        return BadRequest(new
                        {
                            Status = false,
                            Message = "One or more departments do not belong to this company."
                        });
                    }
                }

                var query = _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId&&x.IsAdmin!=true&&x.IsActive!=false);

                if (!allBranches)
                {
                    query = query.Where(x =>
                        selectedBranchIds.Contains(x.BranchId));
                }

                if (!allDepartments)
                {
                    query = query.Where(x =>
                        selectedDepartmentIds.Any(id => id == x.DepartmentId));
                }

                var employees = await query
                    .OrderByDescending(x => x.EmployeeId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.EmpCode,
                        x.FirstName,
                        x.LastName,

                        EmployeeName =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim(),

                        x.CompanyId,
                        CompanyName = company.CompanyName,

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

                        x.RoleId,
                        RoleName = _context.Roles
                            .Where(r =>
                                r.RoleId == x.RoleId &&
                                r.CompanyId == x.CompanyId)
                            .Select(r => r.RoleName)
                            .FirstOrDefault(),
                        IsRoleMapped = _context.RoleTemplateAssignment.Any(assignment =>
assignment.CompanyId == x.CompanyId &&
assignment.EmployeeId == x.EmployeeId &&
assignment.IsActive == true &&
_context.Set<RoleTemplate>().Any(template =>
    template.CompanyId == assignment.CompanyId &&
    template.TemplateId == assignment.TemplateId &&
    template.IsActive == true)),
                        x.Designation,
                        x.ReportingToManagerCode,
                        x.Mobilenumber,
                        x.Email,
                        x.Profile,
                        x.Location,
                        x.City,
                        x.District,
                        x.State,
                        x.DateOfJoining,
                        x.IsActive,
                        x.CreatedOn,
                        x.CreatedBy
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = employees.Count > 0
                        ? "Employee list fetched successfully."
                        : "No employees found.",
                    Data = employees
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch employees for company {CompanyId}",
                    request.CompanyId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch employee list."
                });
            }
        }




        [HttpPost("GetEmployeeRoleList")]
        public async Task<IActionResult> GetEmployeeRoleList(
            [FromBody] GetEmployeeListRequest? request,
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

            if (request.BranchIds?.Any(x => x < 0) == true)
                return Fail(400, "Branch IDs cannot be negative.");

            if (request.DepartmentIds?.Any(x => x < 0) == true)
                return Fail(400, "Department IDs cannot be negative.");

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

                // Preserve your existing non-EP flow.
                if (!(company.EpId > 0))
                    return await GetEmployeeRoleListLocal(request);

                if (request.UserId <= 0)
                {
                    return Fail(
                        400,
                        "Valid Recruitment UserId is required for EP synchronization.");
                }

                var caller = await _context.EmployeeMasters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId);

                if (caller == null)
                    return Fail(404, "User not found for this company.");

                if (!caller.IsActive)
                    return Fail(403, "User is inactive.");

                if (!(caller.EPID > 0))
                    return Fail(409, "EPID is not configured for this employee.");

                var connectionString =
                    configuration.GetConnectionString("EPDatabase");

                var mappingsUrl =
                    configuration["EPIntegration:GetEmployeeRoleMappingsUrl"];

                var encryptionKey =
                    configuration["EncryptionSettings:StaticUniqueId"];

                if (string.IsNullOrWhiteSpace(connectionString))
                    return Fail(500, "EP database connection is not configured.");

                if (string.IsNullOrWhiteSpace(mappingsUrl))
                    return Fail(500, "EP employee mapping URL is not configured.");

                if (string.IsNullOrWhiteSpace(encryptionKey))
                    return Fail(500, "Encryption key is not configured.");

                int epUserId = caller.EPID.Value;
                int epOrgId;

                var epEmployees = new Dictionary<int, EPEmployeeDetails>();

                // Read EP database using its separate connection.
                await using (var epConnection = new SqlConnection(connectionString))
                {
                    await epConnection.OpenAsync();

                    using (var command = epConnection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT OrgID
FROM dbo.tblEmp_Registration
WHERE ID = @UserID
  AND IsActive = 1
  AND (IsDelete = 0 OR IsDelete IS NULL);";

                        command.Parameters.Add(
                            "@UserID",
                            SqlDbType.Int).Value = epUserId;

                        var value = await command.ExecuteScalarAsync();

                        if (value == null || value == DBNull.Value)
                        {
                            return Fail(
                                404,
                                "Active EP employee or organization not found.");
                        }

                        epOrgId = Convert.ToInt32(value);
                    }

                    if (epOrgId <= 0)
                        return Fail(409, "The EP employee has no valid organization.");

                    // Company.EPID is the EP organization reference used by role sync.
                    if (epOrgId != company.EpId)
                    {
                        return Fail(
                            409,
                            "The employee's EP organization does not match this company's EPID.");
                    }

                    using (var command = epConnection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT
    emp.ID,
    emp.FirstName,
    emp.LastName,
    emp.BranchId,
    branch.Name AS BranchName,
    emp.DeptId,
    department.Name AS DepartmentName
FROM dbo.tblEmp_Registration AS emp
LEFT JOIN dbo.tblBranchMaster AS branch
    ON branch.ID = emp.BranchId
LEFT JOIN dbo.tblDepartmentMaster AS department
    ON department.ID = emp.DeptId
WHERE emp.OrgID = @OrgID
  AND emp.IsActive = 1
  AND (emp.IsDelete = 0 OR emp.IsDelete IS NULL);";

                        command.Parameters.Add(
                            "@OrgID",
                            SqlDbType.Int).Value = epOrgId;

                        using var reader = await command.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var item = new EPEmployeeDetails
                            {
                                ID = Convert.ToInt32(reader["ID"]),

                                FirstName = reader["FirstName"] == DBNull.Value
                                    ? null
                                    : Convert.ToString(reader["FirstName"]),

                                LastName = reader["LastName"] == DBNull.Value
                                    ? null
                                    : Convert.ToString(reader["LastName"]),

                                BranchId = reader["BranchId"] == DBNull.Value
                                    ? 0
                                    : Convert.ToInt32(reader["BranchId"]),

                                BranchName = reader["BranchName"] == DBNull.Value
                                    ? null
                                    : Convert.ToString(reader["BranchName"]),

                                DeptId = reader["DeptId"] == DBNull.Value
                                    ? 0
                                    : Convert.ToInt32(reader["DeptId"]),

                                DepartmentName =
                                    reader["DepartmentName"] == DBNull.Value
                                        ? null
                                        : Convert.ToString(reader["DepartmentName"])
                            };

                            epEmployees.Add(item.ID, item);
                        }
                    }
                }

                var separator = mappingsUrl.Contains("?") ? "&" : "?";

                var url =
                    $"{mappingsUrl}{separator}OrgID={epOrgId}&UserID={epUserId}";

                var client = httpClientFactory.CreateClient("EP");

                using var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return Fail(
                        502,
                        "EP employee role-mapping API returned an error.");
                }

                var json = await response.Content.ReadAsStringAsync();

                var epResponse = JsonSerializer.Deserialize<EPMappingResponse>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (epResponse?.Status != true || epResponse.Data == null)
                {
                    return Fail(
                        502,
                        "EP employee role-mapping API returned an unsuccessful response.");
                }

                // Only active role mappings appear in this list.
                var mappings = epResponse.Data
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

                    if (mapping.UserId <= 0 || mapping.RoleId <= 0)
                        return Fail(502, "EP returned an invalid employee or role ID.");

                    if (!epEmployees.ContainsKey(mapping.UserId))
                    {
                        return Fail(
                            502,
                            "An EP mapping employee does not belong to the active organization.");
                    }
                }

                // EmployeeMasters has only one RoleId.
                // Do not silently overwrite one role with another.
                var multipleRoleEmployee = mappings
                    .GroupBy(x => x.UserId)
                    .FirstOrDefault(g =>
                        g.Select(x => x.RoleId).Distinct().Count() > 1);

                if (multipleRoleEmployee != null)
                {
                    return Fail(
                        409,
                        $"EP employee {multipleRoleEmployee.Key} has multiple active roles. " +
                        "A separate employee-role mapping table is required.");
                }

                var conflictingCredentials = mappings
                    .GroupBy(x => x.UserId)
                    .Any(g => g.Select(x => new
                    {
                        x.Email,
                        x.Password,
                        x.MobileNumber
                    }).Distinct().Count() > 1);

                if (conflictingCredentials)
                    return Fail(502, "EP returned conflicting employee login details.");

                mappings = mappings
                    .GroupBy(x => x.UserId)
                    .Select(g => g.First())
                    .ToList();

                // Capture active EP mappings before branch/department filters
                // and before skipping roles missing in Recruitment.
                var activeEpEmployeeIds = mappings
                    .Select(x => x.UserId)
                    .ToHashSet();
                bool allBranches =
                    request.BranchIds == null ||
                    request.BranchIds.Count == 0 ||
                    request.BranchIds.Contains(0);

                bool allDepartments =
                    request.DepartmentIds == null ||
                    request.DepartmentIds.Count == 0 ||
                    request.DepartmentIds.Contains(0);

                // Resolve EP references to Recruitment branch/department IDs.
                // Run the master synchronization before using this API.
                var localBranches = await _context.Branches
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId && x.EPBranchId > 0)
                    .Select(x => new { x.BranchId, x.EPBranchId, x.BranchName })
                    .ToListAsync();

                if (localBranches.GroupBy(x => x.EPBranchId).Any(g => g.Count() > 1))
                    return Fail(409, "Duplicate EP branch mappings exist in Recruitment.");

                var branchesByEpId = localBranches.ToDictionary(
                    x => Convert.ToInt32(x.EPBranchId));

                var localDepartments = await _context.Departments
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId && x.EPDepartmentId > 0)
                    .Select(x => new
                    {
                        x.DepartmentId,
                        x.EPDepartmentId,
                        x.BranchId,
                        x.DepartmentName
                    })
                    .ToListAsync();

                if (localDepartments.GroupBy(x => new { x.BranchId, x.EPDepartmentId })
                    .Any(g => g.Count() > 1))
                    return Fail(409, "Duplicate EP department mappings exist in Recruitment.");

                var departmentsByEpId = localDepartments.ToDictionary(
                    x => (x.BranchId, x.EPDepartmentId));

                // Request filters use Recruitment IDs.
                // activeEpEmployeeIds above remains unfiltered for deactivation.
                mappings = mappings.Where(mapping =>
                {
                    var epEmployee = epEmployees[mapping.UserId];
                    bool hasBranch = branchesByEpId.TryGetValue(
                        epEmployee.BranchId, out var localBranch);

                    if (!allBranches &&
                        (!hasBranch || !request.BranchIds!.Contains(localBranch!.BranchId)))
                        return false;

                    if (!allDepartments)
                    {
                        if (!hasBranch ||
                            !departmentsByEpId.TryGetValue(
                                (localBranch!.BranchId, epEmployee.DeptId),
                                out var localDepartment) ||
                            !request.DepartmentIds!.Contains(localDepartment.DepartmentId))
                            return false;
                    }

                    return true;
                }).ToList();

                var epRoleIds = mappings
                    .Select(x => x.RoleId)
                    .Distinct()
                    .ToList();

                var localRoles = await _context.Roles
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EPRoleId.HasValue &&
                        epRoleIds.Contains(x.EPRoleId.Value))
                    .ToListAsync();

                if (localRoles
                    .GroupBy(x => x.EPRoleId!.Value)
                    .Any(g => g.Count() > 1))
                {
                    return Fail(
                        409,
                        "Duplicate EP role mappings exist in Recruitment.");
                }

                var rolesByEpId = localRoles
                    .ToDictionary(x => x.EPRoleId!.Value);
                mappings = mappings
                    .Where(x => rolesByEpId.ContainsKey(x.RoleId))
                    .ToList();

                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var localEmployees = await _context.EmployeeMasters
                    .Where(x => x.CompanyId == request.CompanyId)
                    .ToListAsync();

                if (localEmployees
                    .Where(x => x.EPID > 0)
                    .GroupBy(x => x.EPID!.Value)
                    .Any(g => g.Count() > 1))
                {
                    return Fail(
                        409,
                        "Duplicate EP employee mappings exist in Recruitment.");
                }

                var employeesByEpId = localEmployees
                    .Where(x => x.EPID > 0)
                    .ToDictionary(x => x.EPID!.Value);

                var synchronizedEmployees = new Dictionary<int, Employee>();
                var now = DateTime.UtcNow;

                int addedCount = 0;
                int updatedCount = 0;
                int skippedCount = 0;

                foreach (var mapping in mappings)
                {
                    var epEmployee = epEmployees[mapping.UserId];
                    var localRole = rolesByEpId[mapping.RoleId];

                    employeesByEpId.TryGetValue(
                        mapping.UserId,
                        out var employee);

                    var mobile = mapping.MobileNumber?.Trim();
                    var roleMail = mapping.Email?.Trim();

                    // Link an existing employee only if the company/mobile match
                    // identifies exactly one employee.
                    if (employee == null && !string.IsNullOrWhiteSpace(mobile))
                    {
                        var mobileMatches = localEmployees
                            .Where(x =>
                                string.Equals(
                                    x.Mobilenumber?.Trim(),
                                    mobile,
                                    StringComparison.Ordinal))
                            .ToList();

                        if (mobileMatches.Count > 1)
                        {
                            return Fail(
                                409,
                                $"Multiple Recruitment employees match EP employee {mapping.UserId}'s mobile.");
                        }

                        employee = mobileMatches.FirstOrDefault();

                        if (employee?.EPID > 0 &&
                            employee.EPID != mapping.UserId)
                        {
                            return Fail(
                                409,
                                "The matching mobile belongs to a different EP employee.");
                        }
                    }

                    // Preserve the existing non-admin list behavior.
                    if (employee?.IsAdmin == true)
                        continue;

                    if (!branchesByEpId.TryGetValue(
                            epEmployee.BranchId, out var localBranch))
                    {
                        return Fail(409,
                            $"Recruitment branch mapping is missing for EP branch {epEmployee.BranchId}, employee {mapping.UserId}. Synchronize branches first.");
                    }

                    if (!departmentsByEpId.TryGetValue(
                            (localBranch.BranchId, epEmployee.DeptId),
                            out var localDepartment))
                    {
                        return Fail(409,
                            $"Recruitment department mapping is missing for EP department {epEmployee.DeptId} under EP branch {epEmployee.BranchId}, employee {mapping.UserId}. Synchronize departments first.");
                    }

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
                            $"Mapped email already belongs to another Recruitment employee: {roleMail}");
                    }

                    string? encryptedRolePassword = null;

                    if (!string.IsNullOrWhiteSpace(mapping.Password))
                    {
                        encryptedRolePassword =
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
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        };

                        _context.EmployeeMasters.Add(employee);
                        localEmployees.Add(employee);
                    }

                    bool changed =
                        employee!.EPID != mapping.UserId ||
                        employee.FirstName != epEmployee.FirstName ||
                        employee.LastName != epEmployee.LastName ||
                        employee.Mobilenumber != mobile ||
                        employee.RoleMail != roleMail ||
                        employee.RoleId != localRole.RoleId ||
                        employee.BranchId != localBranch.BranchId ||
                        employee.DepartmentId != localDepartment.DepartmentId ||
                        (encryptedRolePassword != null &&
                         employee.RolePW != encryptedRolePassword);

                    employee.EPID = mapping.UserId;
                    employee.FirstName = epEmployee.FirstName;
                    employee.LastName = epEmployee.LastName;
                    employee.Mobilenumber = mobile;

                    // EP Email is the mapped login email.
                    employee.RoleMail = roleMail;

                    // Save Recruitment's local RoleId.
                    employee.RoleId = localRole.RoleId;

                    // Store Recruitment IDs resolved from the EP references.
                    employee.BranchId = localBranch.BranchId;
                    employee.DepartmentId = localDepartment.DepartmentId;

                    if (encryptedRolePassword != null)
                        employee.RolePW = encryptedRolePassword;

                    // Preserve normal Password, personal Email and other local fields.
                    // Existing local IsActive is also preserved.
                    if (isNew)
                    {
                        addedCount++;
                    }
                    else if (changed)
                    {
                        employee.ModifiedBy = request.UserId;
                        employee.ModifiedOn = now;
                        updatedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }

                    employeesByEpId[mapping.UserId] = employee;
                    synchronizedEmployees[mapping.UserId] = employee;
                }

                // Deactivate imported non-admin employees missing from the
                // successful EP active role-mapping response.
                int deactivatedCount = 0;

                foreach (var savedEmployee in localEmployees.Where(x =>
                    x.EPID.HasValue &&
                    x.EPID.Value > 0 &&
                    x.IsAdmin != true &&
                    x.IsActive &&
                    !activeEpEmployeeIds.Contains(x.EPID.Value)))
                {
                    savedEmployee.IsActive = false;
                    savedEmployee.ModifiedBy = request.UserId;
                    savedEmployee.ModifiedOn = now;
                    deactivatedCount++;
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var auditIds = synchronizedEmployees.Values
                    .SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    })
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                var auditNames = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        auditIds.Contains(x.EmployeeId))
                    .Select(x => new
                    {
                        x.EmployeeId,
                        Name = ((x.FirstName ?? "") + " " +
                                (x.LastName ?? "")).Trim()
                    })
                    .ToDictionaryAsync(x => x.EmployeeId, x => x.Name);

                string? GetAuditName(int? id)
                {
                    return id.HasValue &&
                           auditNames.TryGetValue(id.Value, out var name)
                        ? name
                        : null;
                }

                var data = mappings
                    .Where(x =>
                        synchronizedEmployees.ContainsKey(x.UserId) &&
                        synchronizedEmployees[x.UserId].IsActive &&
        synchronizedEmployees[x.UserId].IsAdmin != true)
                    .Select(mapping =>
                    {
                        var employee = synchronizedEmployees[mapping.UserId];
                        var epEmployee = epEmployees[mapping.UserId];
                        var role = rolesByEpId[mapping.RoleId];

                        var fullName =
                            ((employee.FirstName ?? "") + " " +
                             (employee.LastName ?? "")).Trim();

                        return new
                        {
                            employee.EmployeeId,
                            employee.EPID,
                            mapping.MappingId,
                            employee.EmpCode,
                            employee.FirstName,
                            employee.LastName,

                            EmployeeName = $"{fullName} ({role.RoleName})",

                            employee.CompanyId,
                            CompanyName = company.CompanyName,

                            employee.BranchId,
                            BranchName = branchesByEpId[epEmployee.BranchId].BranchName,
                            employee.DepartmentId,
                            DepartmentName = departmentsByEpId[
                                (employee.BranchId, epEmployee.DeptId)].DepartmentName,

                            role.RoleId,
                            role.EPRoleId,
                            role.RoleName,
                            IsRoleMapped = true,

                            employee.Designation,
                            employee.ReportingToManagerCode,
                            employee.Mobilenumber,
                            employee.Email,
                            employee.RoleMail,
                            employee.Profile,
                            employee.Location,
                            employee.City,
                            employee.District,
                            employee.State,
                            employee.DateOfJoining,
                            employee.IsActive,

                            employee.CreatedBy,
                            CreatedByName = GetAuditName(employee.CreatedBy),
                            employee.CreatedOn,

                            employee.ModifiedBy,
                            ModifiedByName = GetAuditName(employee.ModifiedBy),
                            employee.ModifiedOn
                        };
                    })
                    .OrderByDescending(x => x.EmployeeId)
                    .ToList();

                return Ok(new
                {
                    Status = true,
                    StatusCode = 200,
                    Message = data.Count > 0
                        ? "EP role-mapped employees synchronized successfully."
                        : "No matching EP role-mapped employees found.",
                    IsEP = true,
                    AddedCount = addedCount,
                    UpdatedCount = updatedCount,
                    SkippedCount = skippedCount,
                    DeactivatedCount = deactivatedCount,
                    Data = data
                });
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Error(ex, "EP employee mapping request failed.");

                return Fail(502, "Unable to connect to the EP API.");
            }
            catch (TaskCanceledException ex)
            {
                Serilog.Log.Error(ex, "EP employee mapping request timed out.");

                return Fail(504, "The EP request timed out.");
            }
            catch (JsonException ex)
            {
                Serilog.Log.Error(ex, "Invalid EP employee mapping response.");

                return Fail(502, "EP returned an invalid response.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to synchronize EP employees for company {CompanyId}.",
                    request.CompanyId);

                return Fail(500, "Unable to fetch or synchronize employee roles.");
            }
        }


        //    [HttpPost("GetEmployeeRoleList1")]
        //    public async Task<IActionResult> GetEmployeeRoleList(
        //[FromBody] GetEmployeeListRequest? request)
        //    {
        //        if (request == null || request.CompanyId <= 0)
        //        {
        //            return BadRequest(new
        //            {
        //                Status = false,
        //                Message = "Valid CompanyId is required."
        //            });
        //        }

        //        if (request.BranchIds != null &&
        //            request.BranchIds.Any(id => id < 0))
        //        {
        //            return BadRequest(new
        //            {
        //                Status = false,
        //                Message = "Branch IDs cannot be negative."
        //            });
        //        }

        //        if (request.DepartmentIds != null &&
        //            request.DepartmentIds.Any(id => id < 0))
        //        {
        //            return BadRequest(new
        //            {
        //                Status = false,
        //                Message = "Department IDs cannot be negative."
        //            });
        //        }

        //        try
        //        {
        //            var company = await _context.Companys
        //                .AsNoTracking()
        //                .Where(x => x.CompanyID == request.CompanyId)
        //                .Select(x => new
        //                {
        //                    x.CompanyID,
        //                    x.CompanyName
        //                })
        //                .FirstOrDefaultAsync();

        //            if (company == null)
        //            {
        //                return NotFound(new
        //                {
        //                    Status = false,
        //                    Message = "Company not found."
        //                });
        //            }

        //            bool allBranches =
        //                request.BranchIds == null ||
        //                request.BranchIds.Count == 0 ||
        //                request.BranchIds.Contains(0);

        //            bool allDepartments =
        //                request.DepartmentIds == null ||
        //                request.DepartmentIds.Count == 0 ||
        //                request.DepartmentIds.Contains(0);

        //            var selectedBranchIds = allBranches
        //                ? Array.Empty<int>()
        //                : request.BranchIds!.Distinct().ToArray();

        //            var selectedDepartmentIds = allDepartments
        //                ? Array.Empty<int>()
        //                : request.DepartmentIds!.Distinct().ToArray();

        //            if (!allBranches)
        //            {
        //                var validBranchCount = await _context.Branches
        //                    .CountAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        selectedBranchIds.Contains(x.BranchId));

        //                if (validBranchCount != selectedBranchIds.Length)
        //                {
        //                    return BadRequest(new
        //                    {
        //                        Status = false,
        //                        Message = "One or more branches do not belong to this company."
        //                    });
        //                }
        //            }

        //            if (!allDepartments)
        //            {
        //                var validDepartmentCount = await _context.Departments
        //                    .CountAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        selectedDepartmentIds.Contains(x.DepartmentId));

        //                if (validDepartmentCount != selectedDepartmentIds.Length)
        //                {
        //                    return BadRequest(new
        //                    {
        //                        Status = false,
        //                        Message = "One or more departments do not belong to this company."
        //                    });
        //                }
        //            }

        //            var query = _context.EmployeeMasters
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.IsAdmin != true &&
        //                    x.IsActive != false);

        //            if (!allBranches)
        //            {
        //                query = query.Where(x =>
        //                    selectedBranchIds.Contains(x.BranchId));
        //            }

        //            if (!allDepartments)
        //            {
        //                query = query.Where(x =>
        //                    selectedDepartmentIds.Any(id => id == x.DepartmentId));
        //            }

        //            // Distinct employee-role pairs from active assignments
        //            // in active templates.
        //            var mappedRoles =
        //                (
        //                    from assignment in _context.RoleTemplateAssignment
        //                        .AsNoTracking()

        //                    join template in _context.Set<RoleTemplate>()
        //                        .AsNoTracking()
        //                        on new
        //                        {
        //                            assignment.CompanyId,
        //                            assignment.TemplateId
        //                        }
        //                        equals new
        //                        {
        //                            template.CompanyId,
        //                            template.TemplateId
        //                        }

        //                    join role in _context.Roles.AsNoTracking()
        //                        on new
        //                        {
        //                            assignment.CompanyId,
        //                            assignment.RoleId
        //                        }
        //                        equals new
        //                        {
        //                            role.CompanyId,
        //                            role.RoleId
        //                        }

        //                    where assignment.CompanyId == request.CompanyId
        //                          && assignment.IsActive == true
        //                          && template.IsActive == true

        //                    select new
        //                    {
        //                        assignment.CompanyId,
        //                        assignment.EmployeeId,
        //                        role.RoleId,
        //                        role.RoleName
        //                    }
        //                ).Distinct();

        //            var rows = await (
        //                from employee in query

        //                join mappedRole in mappedRoles
        //                    on new
        //                    {
        //                        employee.CompanyId,
        //                        employee.EmployeeId
        //                    }
        //                    equals new
        //                    {
        //                        mappedRole.CompanyId,
        //                        mappedRole.EmployeeId
        //                    }

        //                orderby employee.EmployeeId descending, mappedRole.RoleName

        //                select new
        //                {
        //                    employee.EmployeeId,
        //                    employee.EmpCode,
        //                    employee.FirstName,
        //                    employee.LastName,

        //                    employee.CompanyId,

        //                    employee.BranchId,
        //                    BranchName = _context.Branches
        //                        .Where(b =>
        //                            b.BranchId == employee.BranchId &&
        //                            b.CompanyId == employee.CompanyId)
        //                        .Select(b => b.BranchName)
        //                        .FirstOrDefault(),

        //                    employee.DepartmentId,
        //                    DepartmentName = _context.Departments
        //                        .Where(d =>
        //                            d.DepartmentId == employee.DepartmentId &&
        //                            d.CompanyId == employee.CompanyId &&
        //                            d.BranchId == employee.BranchId)
        //                        .Select(d => d.DepartmentName)
        //                        .FirstOrDefault(),

        //                    // Use the mapped role instead of EmployeeMasters.RoleId.
        //                    mappedRole.RoleId,
        //                    mappedRole.RoleName,

        //                    employee.Designation,
        //                    employee.ReportingToManagerCode,
        //                    employee.Mobilenumber,
        //                    employee.Email,
        //                    employee.Profile,
        //                    employee.Location,
        //                    employee.City,
        //                    employee.District,
        //                    employee.State,
        //                    employee.DateOfJoining,
        //                    employee.IsActive,

        //                    employee.CreatedOn,
        //                    employee.CreatedBy,
        //                    employee.ModifiedOn,
        //                    employee.ModifiedBy
        //                }
        //            ).ToListAsync();

        //            var auditIds = rows
        //                .SelectMany(x => new int?[]
        //                {
        //            x.CreatedBy,
        //            x.ModifiedBy
        //                })
        //                .Where(x => x.HasValue)
        //                .Select(x => x!.Value)
        //                .Distinct()
        //                .ToArray();

        //            // Include admin/inactive audit users so existing names still resolve.
        //            var employeeNames = await _context.EmployeeMasters
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    auditIds.Contains(x.EmployeeId))
        //                .Select(x => new
        //                {
        //                    x.EmployeeId,
        //                    Name =
        //                        ((x.FirstName ?? "") + " " +
        //                         (x.LastName ?? "")).Trim()
        //                })
        //                .ToDictionaryAsync(x => x.EmployeeId, x => x.Name);

        //            string? GetEmployeeName(int? employeeId)
        //            {
        //                return employeeId.HasValue &&
        //                       employeeNames.TryGetValue(
        //                           employeeId.Value, out var name)
        //                    ? name
        //                    : null;
        //            }

        //            var employees = rows.Select(x =>
        //            {
        //                var fullName =
        //                    ((x.FirstName ?? "") + " " +
        //                     (x.LastName ?? "")).Trim();

        //                return new
        //                {
        //                    x.EmployeeId,
        //                    x.EmpCode,
        //                    x.FirstName,
        //                    x.LastName,

        //                    EmployeeName = string.IsNullOrWhiteSpace(x.RoleName)
        //                        ? fullName
        //                        : $"{fullName} ({x.RoleName})",

        //                    x.CompanyId,
        //                    CompanyName = company.CompanyName,

        //                    x.BranchId,
        //                    x.BranchName,

        //                    x.DepartmentId,
        //                    x.DepartmentName,

        //                    x.RoleId,
        //                    x.RoleName,
        //                    IsRoleMapped = true,

        //                    x.Designation,
        //                    x.ReportingToManagerCode,
        //                    x.Mobilenumber,
        //                    x.Email,
        //                    x.Profile,
        //                    x.Location,
        //                    x.City,
        //                    x.District,
        //                    x.State,
        //                    x.DateOfJoining,
        //                    x.IsActive,

        //                    x.CreatedBy,
        //                    CreatedByName = GetEmployeeName(x.CreatedBy),
        //                    x.CreatedOn,

        //                    x.ModifiedBy,
        //                    ModifiedByName = GetEmployeeName(x.ModifiedBy),
        //                    x.ModifiedOn
        //                };
        //            }).ToList();

        //            return Ok(new
        //            {
        //                Status = true,
        //                Message = employees.Count > 0
        //                    ? "Role-mapped employee list fetched successfully."
        //                    : "No role-mapped employees found.",
        //                Data = employees
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            Serilog.Log.Error(
        //                ex,
        //                "Failed to fetch role-mapped employees for company {CompanyId}",
        //                request.CompanyId);

        //            return StatusCode(500, new
        //            {
        //                Status = false,
        //                Message = "Unable to fetch employee list."
        //            });
        //        }
        //    }
        //    // [HttpPost]
        //    // [Route("GetEmployeeList")]
        //    // public IActionResult GetEmployeeList([FromBody] GetEmployeeListRequest request)
        //    // {
        //    //     try
        //    //     {
        //    //         if (request == null)
        //    //             return Ok(new { Status = false, Message = "Invalid request" });

        //    //         if (request.CompanyId <= 0)
        //    //             return Ok(new { Status = false, Message = "CompanyId is required" });

        //    //         var companyExists = _context.Companys
        //    //             .Any(x => x.CompanyID == request.CompanyId);

        //    //         if (!companyExists)
        //    //             return Ok(new { Status = false, Message = "Company not found" });

        //    //         var branchIds = request.BranchList?
        //    //             .Where(x => x > 0)
        //    //             .ToList() ?? new List<int>();

        //    //         var deptIds = request.DeptList?
        //    //             .Where(x => x > 0)
        //    //             .ToList() ?? new List<int>();


        //    //         var RoleIds = request.RoleList?
        //    //             .Where(x => x > 0)
        //    //             .ToList() ?? new List<int>();

        //    //         var employeeBaseQuery = _context.EmployeeMasters
        //    //             .Where(x => x.IsActive && x.CompanyId == request.CompanyId)
        //    //             .AsQueryable();

        //    //         if (branchIds.Count > 0)
        //    //         {
        //    //             employeeBaseQuery = employeeBaseQuery
        //    //                 .Where(x => branchIds.Contains(x.BranchId));
        //    //         }

        //    //         if (deptIds.Count > 0)
        //    //         {
        //    //             employeeBaseQuery = employeeBaseQuery
        //    //                 .Where(x => deptIds.Contains(x.DepartmentId));
        //    //         }
        //    //         if (RoleIds.Count > 0)
        //    //         {
        //    //             employeeBaseQuery = employeeBaseQuery
        //    //                 .Where(x => RoleIds.Contains((int)x.RoleId));
        //    //         }
        //    //         var staticEncyKey = _configuration["EncryptionSettings:StaticUniqueId"];
        //    //         var employees = (
        //    //             from e in employeeBaseQuery

        //    //             join b in _context.Branches
        //    //                 on e.BranchId equals b.BranchId into branchJoin
        //    //             from b in branchJoin.DefaultIfEmpty()

        //    //             join d in _context.Departments
        //    //                 on e.DepartmentId equals d.DepartmentId into deptJoin
        //    //             from d in deptJoin.DefaultIfEmpty()

        //    //             join c in _context.Companys
        //    //                 on e.CompanyId equals c.CompanyID into companyJoin
        //    //             from c in companyJoin.DefaultIfEmpty()
        //    //             join r in _context.Rolemasters
        //    //on e.RoleId equals r.RoleId into roleJoin
        //    //             from r in roleJoin.DefaultIfEmpty()
        //    //             select new
        //    //             {
        //    //                 e.EmployeeId,
        //    //                 e.FirstName,
        //    //                 e.LastName,
        //    //                 e.IsAdmin,
        //    //                 EmployeeName = (e.FirstName ?? "") + " " + (e.LastName ?? ""),

        //    //                 e.CompanyId,
        //    //                 CompanyName = c != null ? c.CompanyCode : "",

        //    //                 e.BranchId,
        //    //                 BranchName = b != null ? b.BranchName : "",

        //    //                 e.DepartmentId,
        //    //                 DepartmentName = d != null ? d.DepartmentName : "",

        //    //                 e.Mobilenumber,
        //    //                 e.Email,
        //    //                 e.Profile,
        //    //                 e.RoleId,
        //    //                 Password = !string.IsNullOrWhiteSpace(e.Password)
        //    // ? Helper.Helper.DecryptAPIData(e.Password, staticEncyKey)
        //    // : "",
        //    //                 e.EmpCode,
        //    //                 e.City,
        //    //                 e.District,
        //    //                 e.State,
        //    //                 e.Location,
        //    //                 e.BankName,
        //    //                 e.Pan,
        //    //                 e.IFSC,
        //    //                 e.BankAccountNo,
        //    //                 e.Adhar,
        //    //                 e.ReportingToManagerCode,

        //    //                 e.DateOfJoining,
        //    //                 e.EmergencyNumber,

        //    //                 RoleName = r != null && !string.IsNullOrWhiteSpace(r.RoleName)
        //    //     ? r.RoleName
        //    //     : "Employee",



        //    //                 e.CreatedOn,
        //    //                 e.ModifiedOn,
        //    //                 e.CreatedBy,
        //    //                 e.ModifiedBy,
        //    //                 e.IsActive
        //    //             })
        //    //             .OrderByDescending(x => x.EmployeeId)
        //    //             .ToList();


        //    //         employees=employees.Where(a=>a.IsAdmin!=true).ToList();
        //    //         return Ok(new
        //    //         {
        //    //             Status = true,
        //    //             Message = "Employee list fetched successfully",
        //    //             Data = employees
        //    //         });
        //    //     }
        //    //     catch (Exception ex)
        //    //     {
        //    //         return Ok(new
        //    //         {
        //    //             Status = false,
        //    //             Message = ex.Message
        //    //         });
        //    //     }
        //    // }



        //        [HttpPost("GetEmployeeRoleList")]
        //        public async Task<IActionResult> GetEmployeeRoleList(
        //            [FromBody] GetEmployeeListRequest? request,
        //            [FromServices] IHttpClientFactory httpClientFactory,
        //            [FromServices] IConfiguration configuration)
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

        //            if (request.BranchIds?.Any(x => x < 0) == true)
        //                return Fail(400, "Branch IDs cannot be negative.");

        //            if (request.DepartmentIds?.Any(x => x < 0) == true)
        //                return Fail(400, "Department IDs cannot be negative.");

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

        //                // Preserve your existing non-EP flow.
        //                if (!(company.EpId > 0))
        //                    return await GetEmployeeRoleListLocal(request);

        //                if (request.UserId <= 0)
        //                {
        //                    return Fail(
        //                        400,
        //                        "Valid Recruitment UserId is required for EP synchronization.");
        //                }

        //                var caller = await _context.EmployeeMasters
        //                    .AsNoTracking()
        //                    .FirstOrDefaultAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EmployeeId == request.UserId);

        //                if (caller == null)
        //                    return Fail(404, "User not found for this company.");

        //                if (!caller.IsActive)
        //                    return Fail(403, "User is inactive.");

        //                if (!(caller.EPID > 0))
        //                    return Fail(409, "EPID is not configured for this employee.");

        //                var connectionString =
        //                    configuration.GetConnectionString("EPDatabase");

        //                var mappingsUrl =
        //                    configuration["EPIntegration:GetEmployeeRoleMappingsUrl"];

        //                var encryptionKey =
        //                    configuration["EncryptionSettings:StaticUniqueId"];

        //                if (string.IsNullOrWhiteSpace(connectionString))
        //                    return Fail(500, "EP database connection is not configured.");

        //                if (string.IsNullOrWhiteSpace(mappingsUrl))
        //                    return Fail(500, "EP employee mapping URL is not configured.");

        //                if (string.IsNullOrWhiteSpace(encryptionKey))
        //                    return Fail(500, "Encryption key is not configured.");

        //                int epUserId = caller.EPID.Value;
        //                int epOrgId;

        //                var epEmployees = new Dictionary<int, EPEmployeeDetails>();

        //                // Read EP database using its separate connection.
        //                await using (var epConnection = new SqlConnection(connectionString))
        //                {
        //                    await epConnection.OpenAsync();

        //                    using (var command = epConnection.CreateCommand())
        //                    {
        //                        command.CommandText = @"
        //SELECT OrgID
        //FROM dbo.tblEmp_Registration
        //WHERE ID = @UserID
        //  AND IsActive = 1
        //  AND (IsDelete = 0 OR IsDelete IS NULL);";

        //                        command.Parameters.Add(
        //                            "@UserID",
        //                            SqlDbType.Int).Value = epUserId;

        //                        var value = await command.ExecuteScalarAsync();

        //                        if (value == null || value == DBNull.Value)
        //                        {
        //                            return Fail(
        //                                404,
        //                                "Active EP employee or organization not found.");
        //                        }

        //                        epOrgId = Convert.ToInt32(value);
        //                    }

        //                    if (epOrgId <= 0)
        //                        return Fail(409, "The EP employee has no valid organization.");

        //                    // Company.EPID is the EP organization reference used by role sync.
        //                    if (epOrgId != company.EpId)
        //                    {
        //                        return Fail(
        //                            409,
        //                            "The employee's EP organization does not match this company's EPID.");
        //                    }

        //                    using (var command = epConnection.CreateCommand())
        //                    {
        //                        command.CommandText = @"
        //SELECT
        //    emp.ID,
        //    emp.FirstName,
        //    emp.LastName,
        //    emp.BranchId,
        //    branch.Name AS BranchName,
        //    emp.DeptId,
        //    department.Name AS DepartmentName
        //FROM dbo.tblEmp_Registration AS emp
        //LEFT JOIN dbo.tblBranchMaster AS branch
        //    ON branch.ID = emp.BranchId
        //LEFT JOIN dbo.tblDepartmentMaster AS department
        //    ON department.ID = emp.DeptId
        //WHERE emp.OrgID = @OrgID
        //  AND emp.IsActive = 1
        //  AND (emp.IsDelete = 0 OR emp.IsDelete IS NULL);";

        //                        command.Parameters.Add(
        //                            "@OrgID",
        //                            SqlDbType.Int).Value = epOrgId;

        //                        using var reader = await command.ExecuteReaderAsync();

        //                        while (await reader.ReadAsync())
        //                        {
        //                            var item = new EPEmployeeDetails
        //                            {
        //                                ID = Convert.ToInt32(reader["ID"]),

        //                                FirstName = reader["FirstName"] == DBNull.Value
        //                                    ? null
        //                                    : Convert.ToString(reader["FirstName"]),

        //                                LastName = reader["LastName"] == DBNull.Value
        //                                    ? null
        //                                    : Convert.ToString(reader["LastName"]),

        //                                BranchId = reader["BranchId"] == DBNull.Value
        //                                    ? 0
        //                                    : Convert.ToInt32(reader["BranchId"]),

        //                                BranchName = reader["BranchName"] == DBNull.Value
        //                                    ? null
        //                                    : Convert.ToString(reader["BranchName"]),

        //                                DeptId = reader["DeptId"] == DBNull.Value
        //                                    ? 0
        //                                    : Convert.ToInt32(reader["DeptId"]),

        //                                DepartmentName =
        //                                    reader["DepartmentName"] == DBNull.Value
        //                                        ? null
        //                                        : Convert.ToString(reader["DepartmentName"])
        //                            };

        //                            epEmployees.Add(item.ID, item);
        //                        }
        //                    }
        //                }

        //                var separator = mappingsUrl.Contains("?") ? "&" : "?";

        //                var url =
        //                    $"{mappingsUrl}{separator}OrgID={epOrgId}&UserID={epUserId}";

        //                var client = httpClientFactory.CreateClient("EP");

        //                using var response = await client.GetAsync(url);

        //                if (!response.IsSuccessStatusCode)
        //                {
        //                    return Fail(
        //                        502,
        //                        "EP employee role-mapping API returned an error.");
        //                }

        //                var json = await response.Content.ReadAsStringAsync();

        //                var epResponse = JsonSerializer.Deserialize<EPMappingResponse>(
        //                    json,
        //                    new JsonSerializerOptions
        //                    {
        //                        PropertyNameCaseInsensitive = true
        //                    });

        //                if (epResponse?.Status != true || epResponse.Data == null)
        //                {
        //                    return Fail(
        //                        502,
        //                        "EP employee role-mapping API returned an unsuccessful response.");
        //                }

        //                // Only active role mappings appear in this list.
        //                var mappings = epResponse.Data
        //                    .Where(x => x.Status)
        //                    .ToList();

        //                foreach (var mapping in mappings)
        //                {
        //                    if (mapping.UserId > 0 &&
        //                        mapping.ID > 0 &&
        //                        mapping.UserId != mapping.ID)
        //                    {
        //                        return Fail(502, "EP returned inconsistent employee IDs.");
        //                    }

        //                    if (mapping.UserId <= 0)
        //                        mapping.UserId = mapping.ID;

        //                    if (mapping.UserId <= 0 || mapping.RoleId <= 0)
        //                        return Fail(502, "EP returned an invalid employee or role ID.");

        //                    if (!epEmployees.ContainsKey(mapping.UserId))
        //                    {
        //                        return Fail(
        //                            502,
        //                            "An EP mapping employee does not belong to the active organization.");
        //                    }
        //                }

        //                // EmployeeMasters has only one RoleId.
        //                // Do not silently overwrite one role with another.
        //                var multipleRoleEmployee = mappings
        //                    .GroupBy(x => x.UserId)
        //                    .FirstOrDefault(g =>
        //                        g.Select(x => x.RoleId).Distinct().Count() > 1);

        //                if (multipleRoleEmployee != null)
        //                {
        //                    return Fail(
        //                        409,
        //                        $"EP employee {multipleRoleEmployee.Key} has multiple active roles. " +
        //                        "A separate employee-role mapping table is required.");
        //                }

        //                var conflictingCredentials = mappings
        //                    .GroupBy(x => x.UserId)
        //                    .Any(g => g.Select(x => new
        //                    {
        //                        x.Email,
        //                        x.Password,
        //                        x.MobileNumber
        //                    }).Distinct().Count() > 1);

        //                if (conflictingCredentials)
        //                    return Fail(502, "EP returned conflicting employee login details.");

        //                mappings = mappings
        //                    .GroupBy(x => x.UserId)
        //                    .Select(g => g.First())
        //                    .ToList();

        //                // Capture active EP mappings before branch/department filters
        //                // and before skipping roles missing in Recruitment.
        //                var activeEpEmployeeIds = mappings
        //                    .Select(x => x.UserId)
        //                    .ToHashSet();
        //                bool allBranches =
        //                    request.BranchIds == null ||
        //                    request.BranchIds.Count == 0 ||
        //                    request.BranchIds.Contains(0);

        //                bool allDepartments =
        //                    request.DepartmentIds == null ||
        //                    request.DepartmentIds.Count == 0 ||
        //                    request.DepartmentIds.Contains(0);

        //                // For EP companies, these filters are EP IDs.
        //                mappings = mappings.Where(mapping =>
        //                {
        //                    var employee = epEmployees[mapping.UserId];

        //                    return
        //                        (allBranches ||
        //                         request.BranchIds!.Contains(employee.BranchId)) &&
        //                        (allDepartments ||
        //                         request.DepartmentIds!.Contains(employee.DeptId));
        //                }).ToList();

        //                var epRoleIds = mappings
        //                    .Select(x => x.RoleId)
        //                    .Distinct()
        //                    .ToList();

        //                var localRoles = await _context.Roles
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EPRoleId.HasValue &&
        //                        epRoleIds.Contains(x.EPRoleId.Value))
        //                    .ToListAsync();

        //                if (localRoles
        //                    .GroupBy(x => x.EPRoleId!.Value)
        //                    .Any(g => g.Count() > 1))
        //                {
        //                    return Fail(
        //                        409,
        //                        "Duplicate EP role mappings exist in Recruitment.");
        //                }

        //                var rolesByEpId = localRoles
        //                    .ToDictionary(x => x.EPRoleId!.Value);
        //                mappings = mappings
        //                    .Where(x => rolesByEpId.ContainsKey(x.RoleId))
        //                    .ToList();

        //                await using var transaction =
        //                    await _context.Database.BeginTransactionAsync(
        //                        IsolationLevel.Serializable);

        //                var localEmployees = await _context.EmployeeMasters
        //                    .Where(x => x.CompanyId == request.CompanyId)
        //                    .ToListAsync();

        //                if (localEmployees
        //                    .Where(x => x.EPID > 0)
        //                    .GroupBy(x => x.EPID!.Value)
        //                    .Any(g => g.Count() > 1))
        //                {
        //                    return Fail(
        //                        409,
        //                        "Duplicate EP employee mappings exist in Recruitment.");
        //                }

        //                var employeesByEpId = localEmployees
        //                    .Where(x => x.EPID > 0)
        //                    .ToDictionary(x => x.EPID!.Value);

        //                var synchronizedEmployees = new Dictionary<int, Employee>();
        //                var now = DateTime.UtcNow;

        //                int addedCount = 0;
        //                int updatedCount = 0;
        //                int skippedCount = 0;

        //                foreach (var mapping in mappings)
        //                {
        //                    var epEmployee = epEmployees[mapping.UserId];
        //                    var localRole = rolesByEpId[mapping.RoleId];

        //                    employeesByEpId.TryGetValue(
        //                        mapping.UserId,
        //                        out var employee);

        //                    var mobile = mapping.MobileNumber?.Trim();
        //                    var roleMail = mapping.Email?.Trim();

        //                    // Link an existing employee only if the company/mobile match
        //                    // identifies exactly one employee.
        //                    if (employee == null && !string.IsNullOrWhiteSpace(mobile))
        //                    {
        //                        var mobileMatches = localEmployees
        //                            .Where(x =>
        //                                string.Equals(
        //                                    x.Mobilenumber?.Trim(),
        //                                    mobile,
        //                                    StringComparison.Ordinal))
        //                            .ToList();

        //                        if (mobileMatches.Count > 1)
        //                        {
        //                            return Fail(
        //                                409,
        //                                $"Multiple Recruitment employees match EP employee {mapping.UserId}'s mobile.");
        //                        }

        //                        employee = mobileMatches.FirstOrDefault();

        //                        if (employee?.EPID > 0 &&
        //                            employee.EPID != mapping.UserId)
        //                        {
        //                            return Fail(
        //                                409,
        //                                "The matching mobile belongs to a different EP employee.");
        //                        }
        //                    }

        //                    // Preserve the existing non-admin list behavior.
        //                    if (employee?.IsAdmin == true)
        //                        continue;

        //                    if (!string.IsNullOrWhiteSpace(roleMail) &&
        //                        localEmployees.Any(x =>
        //                            x != employee &&
        //                            string.Equals(
        //                                x.RoleMail?.Trim(),
        //                                roleMail,
        //                                StringComparison.OrdinalIgnoreCase)))
        //                    {
        //                        return Fail(
        //                            409,
        //                            $"Mapped email already belongs to another Recruitment employee: {roleMail}");
        //                    }

        //                    string? encryptedRolePassword = null;

        //                    if (!string.IsNullOrWhiteSpace(mapping.Password))
        //                    {
        //                        encryptedRolePassword =
        //                            global::Recruitment.Helper.Helper.EncryptAPIData(
        //                                mapping.Password,
        //                                encryptionKey);
        //                    }

        //                    bool isNew = employee == null;

        //                    if (isNew)
        //                    {
        //                        employee = new Employee
        //                        {
        //                            CompanyId = request.CompanyId,
        //                            IsActive = true,
        //                            IsAdmin = false,
        //                            CreatedBy = request.UserId,
        //                            CreatedOn = now
        //                        };

        //                        _context.EmployeeMasters.Add(employee);
        //                        localEmployees.Add(employee);
        //                    }

        //                    bool changed =
        //                        employee!.EPID != mapping.UserId ||
        //                        employee.FirstName != epEmployee.FirstName ||
        //                        employee.LastName != epEmployee.LastName ||
        //                        employee.Mobilenumber != mobile ||
        //                        employee.RoleMail != roleMail ||
        //                        employee.RoleId != localRole.RoleId ||
        //                        employee.BranchId != epEmployee.BranchId ||
        //                        employee.DepartmentId != epEmployee.DeptId ||
        //                        (encryptedRolePassword != null &&
        //                         employee.RolePW != encryptedRolePassword);

        //                    employee.EPID = mapping.UserId;
        //                    employee.FirstName = epEmployee.FirstName;
        //                    employee.LastName = epEmployee.LastName;
        //                    employee.Mobilenumber = mobile;

        //                    // EP Email is the mapped login email.
        //                    employee.RoleMail = roleMail;

        //                    // Save Recruitment's local RoleId.
        //                    employee.RoleId = localRole.RoleId;

        //                    // These are EP IDs, according to your requested storage model.
        //                    employee.BranchId = epEmployee.BranchId;
        //                    employee.DepartmentId = epEmployee.DeptId;

        //                    if (encryptedRolePassword != null)
        //                        employee.RolePW = encryptedRolePassword;

        //                    // Preserve normal Password, personal Email and other local fields.
        //                    // Existing local IsActive is also preserved.
        //                    if (isNew)
        //                    {
        //                        addedCount++;
        //                    }
        //                    else if (changed)
        //                    {
        //                        employee.ModifiedBy = request.UserId;
        //                        employee.ModifiedOn = now;
        //                        updatedCount++;
        //                    }
        //                    else
        //                    {
        //                        skippedCount++;
        //                    }

        //                    employeesByEpId[mapping.UserId] = employee;
        //                    synchronizedEmployees[mapping.UserId] = employee;
        //                }

        //                // Deactivate imported non-admin employees missing from the
        //                // successful EP active role-mapping response.
        //                int deactivatedCount = 0;

        //                foreach (var savedEmployee in localEmployees.Where(x =>
        //                    x.EPID.HasValue &&
        //                    x.EPID.Value > 0 &&
        //                    x.IsAdmin != true &&
        //                    x.IsActive &&
        //                    !activeEpEmployeeIds.Contains(x.EPID.Value)))
        //                {
        //                    savedEmployee.IsActive = false;
        //                    savedEmployee.ModifiedBy = request.UserId;
        //                    savedEmployee.ModifiedOn = now;
        //                    deactivatedCount++;
        //                }
        //                await _context.SaveChangesAsync();
        //                await transaction.CommitAsync();

        //                var auditIds = synchronizedEmployees.Values
        //                    .SelectMany(x => new int?[]
        //                    {
        //                x.CreatedBy,
        //                x.ModifiedBy
        //                    })
        //                    .Where(x => x.HasValue)
        //                    .Select(x => x!.Value)
        //                    .Distinct()
        //                    .ToList();

        //                var auditNames = await _context.EmployeeMasters
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        auditIds.Contains(x.EmployeeId))
        //                    .Select(x => new
        //                    {
        //                        x.EmployeeId,
        //                        Name = ((x.FirstName ?? "") + " " +
        //                                (x.LastName ?? "")).Trim()
        //                    })
        //                    .ToDictionaryAsync(x => x.EmployeeId, x => x.Name);

        //                string? GetAuditName(int? id)
        //                {
        //                    return id.HasValue &&
        //                           auditNames.TryGetValue(id.Value, out var name)
        //                        ? name
        //                        : null;
        //                }

        //                var data = mappings
        //                    .Where(x =>
        //                        synchronizedEmployees.ContainsKey(x.UserId) &&
        //                        synchronizedEmployees[x.UserId].IsActive &&
        //        synchronizedEmployees[x.UserId].IsAdmin != true)
        //                    .Select(mapping =>
        //                    {
        //                        var employee = synchronizedEmployees[mapping.UserId];
        //                        var epEmployee = epEmployees[mapping.UserId];
        //                        var role = rolesByEpId[mapping.RoleId];

        //                        var fullName =
        //                            ((employee.FirstName ?? "") + " " +
        //                             (employee.LastName ?? "")).Trim();

        //                        return new
        //                        {
        //                            employee.EmployeeId,
        //                            employee.EPID,
        //                            mapping.MappingId,
        //                            employee.EmpCode,
        //                            employee.FirstName,
        //                            employee.LastName,

        //                            EmployeeName = $"{fullName} ({role.RoleName})",

        //                            employee.CompanyId,
        //                            CompanyName = company.CompanyName,

        //                            employee.BranchId,
        //                            epEmployee.BranchName,
        //                            employee.DepartmentId,
        //                            epEmployee.DepartmentName,

        //                            role.RoleId,
        //                            role.EPRoleId,
        //                            role.RoleName,
        //                            IsRoleMapped = true,

        //                            employee.Designation,
        //                            employee.ReportingToManagerCode,
        //                            employee.Mobilenumber,
        //                            employee.Email,
        //                            employee.RoleMail,
        //                            employee.Profile,
        //                            employee.Location,
        //                            employee.City,
        //                            employee.District,
        //                            employee.State,
        //                            employee.DateOfJoining,
        //                            employee.IsActive,

        //                            employee.CreatedBy,
        //                            CreatedByName = GetAuditName(employee.CreatedBy),
        //                            employee.CreatedOn,

        //                            employee.ModifiedBy,
        //                            ModifiedByName = GetAuditName(employee.ModifiedBy),
        //                            employee.ModifiedOn
        //                        };
        //                    })
        //                    .OrderByDescending(x => x.EmployeeId)
        //                    .ToList();

        //                return Ok(new
        //                {
        //                    Status = true,
        //                    StatusCode = 200,
        //                    Message = data.Count > 0
        //                        ? "EP role-mapped employees synchronized successfully."
        //                        : "No matching EP role-mapped employees found.",
        //                    IsEP = true,
        //                    AddedCount = addedCount,
        //                    UpdatedCount = updatedCount,
        //                    SkippedCount = skippedCount,
        //                    DeactivatedCount = deactivatedCount,
        //                    Data = data
        //                });
        //            }
        //            catch (HttpRequestException ex)
        //            {
        //                Serilog.Log.Error(ex, "EP employee mapping request failed.");

        //                return Fail(502, "Unable to connect to the EP API.");
        //            }
        //            catch (TaskCanceledException ex)
        //            {
        //                Serilog.Log.Error(ex, "EP employee mapping request timed out.");

        //                return Fail(504, "The EP request timed out.");
        //            }
        //            catch (JsonException ex)
        //            {
        //                Serilog.Log.Error(ex, "Invalid EP employee mapping response.");

        //                return Fail(502, "EP returned an invalid response.");
        //            }
        //            catch (Exception ex)
        //            {
        //                Serilog.Log.Error(
        //                    ex,
        //                    "Failed to synchronize EP employees for company {CompanyId}.",
        //                    request.CompanyId);

        //                return Fail(500, "Unable to fetch or synchronize employee roles.");
        //            }
        //        }



        private async Task<IActionResult> GetEmployeeRoleListLocal(
    GetEmployeeListRequest? request)
        {
            if (request == null || request.CompanyId <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    StatusCode = 400,
                    Message = "Valid CompanyId is required."
                });
            }

            if (request.BranchIds != null &&
                request.BranchIds.Any(id => id < 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    StatusCode = 400,
                    Message = "Branch IDs cannot be negative."
                });
            }

            if (request.DepartmentIds != null &&
                request.DepartmentIds.Any(id => id < 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    StatusCode = 400,
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
                        StatusCode = 404,
                        Message = "Company not found."
                    });
                }

                bool allBranches =
                    request.BranchIds == null ||
                    request.BranchIds.Count == 0 ||
                    request.BranchIds.Contains(0);

                bool allDepartments =
                    request.DepartmentIds == null ||
                    request.DepartmentIds.Count == 0 ||
                    request.DepartmentIds.Contains(0);

                var selectedBranchIds = allBranches
                    ? Array.Empty<int>()
                    : request.BranchIds!.Distinct().ToArray();

                var selectedDepartmentIds = allDepartments
                    ? Array.Empty<int>()
                    : request.DepartmentIds!.Distinct().ToArray();

                if (!allBranches)
                {
                    var validBranchCount = await _context.Branches
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            selectedBranchIds.Contains(x.BranchId));

                    if (validBranchCount != selectedBranchIds.Length)
                    {
                        return BadRequest(new
                        {
                            Status = false,
                            StatusCode = 400,
                            Message =
                                "One or more branches do not belong to this company."
                        });
                    }
                }

                if (!allDepartments)
                {
                    var validDepartmentCount = await _context.Departments
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            selectedDepartmentIds.Contains(x.DepartmentId));

                    if (validDepartmentCount != selectedDepartmentIds.Length)
                    {
                        return BadRequest(new
                        {
                            Status = false,
                            StatusCode = 400,
                            Message =
                                "One or more departments do not belong to this company."
                        });
                    }
                }

                var query = _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsAdmin != true &&
                        x.IsActive != false);

                if (!allBranches)
                {
                    query = query.Where(x =>
                        selectedBranchIds.Contains(x.BranchId));
                }

                if (!allDepartments)
                {
                    query = query.Where(x =>
                        selectedDepartmentIds.Any(id =>
                            id == x.DepartmentId));
                }

                // Distinct employee-role pairs from active assignments
                // in active templates.
                var mappedRoles =
                    (
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
                              && assignment.IsActive == true
                              && template.IsActive == true

                        select new
                        {
                            assignment.CompanyId,
                            assignment.EmployeeId,
                            role.RoleId,
                            role.RoleName
                        }
                    ).Distinct();

                var rows = await (
                    from employee in query

                    join mappedRole in mappedRoles
                        on new
                        {
                            employee.CompanyId,
                            employee.EmployeeId
                        }
                        equals new
                        {
                            mappedRole.CompanyId,
                            mappedRole.EmployeeId
                        }

                    orderby employee.EmployeeId descending,
                            mappedRole.RoleName

                    select new
                    {
                        employee.EmployeeId,
                        employee.EmpCode,
                        employee.FirstName,
                        employee.LastName,

                        employee.CompanyId,

                        employee.BranchId,
                        BranchName = _context.Branches
                            .Where(b =>
                                b.BranchId == employee.BranchId &&
                                b.CompanyId == employee.CompanyId)
                            .Select(b => b.BranchName)
                            .FirstOrDefault(),

                        employee.DepartmentId,
                        DepartmentName = _context.Departments
                            .Where(d =>
                                d.DepartmentId == employee.DepartmentId &&
                                d.CompanyId == employee.CompanyId &&
                                d.BranchId == employee.BranchId)
                            .Select(d => d.DepartmentName)
                            .FirstOrDefault(),

                        // Use the assignment's role.
                        mappedRole.RoleId,
                        mappedRole.RoleName,

                        employee.Designation,
                        employee.ReportingToManagerCode,
                        employee.Mobilenumber,
                        employee.Email,
                        employee.Profile,
                        employee.Location,
                        employee.City,
                        employee.District,
                        employee.State,
                        employee.DateOfJoining,
                        employee.IsActive,

                        employee.CreatedOn,
                        employee.CreatedBy,
                        employee.ModifiedOn,
                        employee.ModifiedBy
                    }
                ).ToListAsync();

                var auditIds = rows
                    .SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    })
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToArray();

                // Include admin and inactive employees for audit names.
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

                string? GetEmployeeName(int? employeeId)
                {
                    return employeeId.HasValue &&
                           employeeNames.TryGetValue(
                               employeeId.Value,
                               out var name)
                        ? name
                        : null;
                }

                var employees = rows.Select(x =>
                {
                    var fullName =
                        ((x.FirstName ?? "") + " " +
                         (x.LastName ?? "")).Trim();

                    return new
                    {
                        x.EmployeeId,
                        x.EmpCode,
                        x.FirstName,
                        x.LastName,

                        EmployeeName = string.IsNullOrWhiteSpace(x.RoleName)
                            ? fullName
                            : $"{fullName} ({x.RoleName})",

                        x.CompanyId,
                        CompanyName = company.CompanyName,

                        x.BranchId,
                        x.BranchName,

                        x.DepartmentId,
                        x.DepartmentName,

                        x.RoleId,
                        x.RoleName,
                        IsRoleMapped = true,

                        x.Designation,
                        x.ReportingToManagerCode,
                        x.Mobilenumber,
                        x.Email,
                        x.Profile,
                        x.Location,
                        x.City,
                        x.District,
                        x.State,
                        x.DateOfJoining,
                        x.IsActive,

                        x.CreatedBy,
                        CreatedByName = GetEmployeeName(x.CreatedBy),
                        x.CreatedOn,

                        x.ModifiedBy,
                        ModifiedByName = GetEmployeeName(x.ModifiedBy),
                        x.ModifiedOn
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    StatusCode = 200,
                    Message = employees.Count > 0
                        ? "Role-mapped employee list fetched successfully."
                        : "No role-mapped employees found.",
                    Data = employees
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch role-mapped employees for company {CompanyId}",
                    request.CompanyId);

                return StatusCode(500, new
                {
                    Status = false,
                    StatusCode = 500,
                    Message = "Unable to fetch employee list."
                });
            }
        }
        public class CreateEmployeeRequest
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }

            public int CompanyId { get; set; }
            public int BranchId { get; set; }
            public int DepartmentId { get; set; }

            public string? Password { get; set; }
            public string? Mobilenumber { get; set; }
            public string? Email { get; set; }
            public string? Profile { get; set; }

            public int? CreatedBy { get; set; }
            public int? RoleId { get; set; }

            public string? City { get; set; }
            public string? District { get; set; }
            public string? State { get; set; }
            public string? Location { get; set; }

            public string? DateOfJoining { get; set; }
            public string? EmergencyNumber { get; set; }

            // New fields
            public string? EmpCode { get; set; }
            public string? Designation { get; set; }
            public string? ReportingToManagerCode { get; set; }
            public string? Pan { get; set; }
            public string? Adhar { get; set; }
            public string? BankName { get; set; }
            public string? BankAccountNo { get; set; }
            public string? IFSC { get; set; }
        }
        public class GetEmployeeListRequest
        {
            public int CompanyId { get; set; }

            // Recruitment EmployeeId of the caller.
            public int UserId { get; set; }

            public List<int>? BranchIds { get; set; }

            public List<int>? DepartmentIds { get; set; }
        }
        private sealed class EPMappingResponse
        {
            public bool Status { get; set; }
            public string? Message { get; set; }
            public List<EPMappingRow>? Data { get; set; }
        }

        private sealed class EPMappingRow
        {
            public int MappingId { get; set; }
            public int UserId { get; set; }
            public int ID { get; set; }

            public string? UserName { get; set; }
            public string? Email { get; set; }
            public string? MobileNumber { get; set; }
            public string? Password { get; set; }

            public int RoleId { get; set; }
            public string? RoleName { get; set; }
            public bool Status { get; set; }
        }

        private sealed class EPEmployeeDetails
        {
            public int ID { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }

            public int BranchId { get; set; }
            public string? BranchName { get; set; }

            public int DeptId { get; set; }
            public string? DepartmentName { get; set; }
        }


        public class EditEmployeeRequest
        {
            public int EmployeeId { get; set; }

            public string? FirstName { get; set; }
            public string? LastName { get; set; }

            public int BranchId { get; set; }
            public int DepartmentId { get; set; }

            public string? Password { get; set; }
            public string? Mobilenumber { get; set; }
            public string? Email { get; set; }
            public string? Profile { get; set; }

            public string? City { get; set; }
            public string? District { get; set; }
            public string? State { get; set; }
            public string? Location { get; set; }

            public string? DateOfJoining { get; set; }
            public string? EmergencyNumber { get; set; }

            public int? ModifiedBy { get; set; }

            // New fields
            public string? EmpCode { get; set; }
            public string? Designation { get; set; }
            public string? ReportingToManagerCode { get; set; }
            public string? Pan { get; set; }
            public string? Adhar { get; set; }
            public string? BankName { get; set; }
            public string? BankAccountNo { get; set; }
            public string? IFSC { get; set; }
        }

        public class DeleteEmployeeRequest
        {
            public int EmployeeId { get; set; }
            public int? DeletedBy { get; set; }
            public string? DeleteType { get; set; }
            public string? DeletedReason { get; set; }
        }


       
    }
}
