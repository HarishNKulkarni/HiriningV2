
using MailChimp.Net.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;

namespace IMS.Controllers
{
    [Route("api/CompanyController")]
    [ApiController]
    public class CompanyController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IConfiguration _configuration;
        public CompanyController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this.dbContext = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("AddcompanyDetails")]
        public IActionResult AddcompanyDetails(Companyreg dto)
        {
            var mobileExists = dbContext.Companys
                .Any(x => x.Mobile == dto.Mobile);

            if (mobileExists)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Mobile number already exists."
                });
            }

            var emailExists = dbContext.Companys
                .Any(x => x.Email == dto.Email);

            if (emailExists)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Email already exists."
                });
            }

            var vehicleTypeIds = dto.VehicleTypeIds?
         .Where(x => x > 0)
         .Distinct()
         .ToList() ?? new List<int>();

            var brandIds = dto.BrandIds?
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();


            var validBrands = new List<dynamic>();



            var CompanyDetails = new Company()
            {
                CompanyCode = dto.CompanyCode,
                CompanyName = dto.CompanyName,
                Email = dto.Email,
                Mobile = dto.Mobile,
                Website = dto.Website,
                GSTNo = dto.GSTNo,
                PANNo = dto.PANNo,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                Pincode = dto.Pincode,
                Logo = dto.Logo,
                CompanyType = dto.CompanyType,
                IsActive = dto.IsActive,
                CreatedOn = dto.CreatedOn,
                Password = dto.Password,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                EpId=dto.EpId

            };

            dbContext.Companys.Add(CompanyDetails);
            dbContext.SaveChanges();

          
            var staticEncyKey = _configuration["EncryptionSettings:StaticUniqueId"];
            var encryptedPassword = global::Recruitment.Helper.Helper.EncryptAPIData(
                dto.Password,
                staticEncyKey
            );

            var adminEmployee = new Employee
            {
                FirstName = dto.FirstName ?? "Admin",
                LastName = dto.LastName ,
                CompanyId = CompanyDetails.CompanyID,
                BranchId = 0, // Set to 0 as per requirement
                DepartmentId = 0, // Set to 0 as per requirement
                Password = encryptedPassword,
                Mobilenumber = dto.Mobile,
                Email = dto.Email,
                Profile = null,
                RoleId = null,
                City = dto.City,
                District = null,
                State = dto.State,
                Location = null,
                DateOfJoining = DateTime.Now,
                EmergencyNumber = null,
                CreatedBy = CompanyDetails.CompanyID,
                CreatedOn = DateTime.Now,
                IsActive = true,
                IsAdmin = true, // Set IsAdmin = true
                EPID=dto.AdminId,
                // Optional fields
                EmpCode = $"ADMIN_{CompanyDetails.CompanyID}",
                Designation = "Company Admin",
                ReportingToManagerCode = null,
                Pan = null,
                Adhar = null,
                BankName = null,
                BankAccountNo = null,
                IFSC = null
            };

            dbContext.EmployeeMasters.Add(adminEmployee);
            dbContext.SaveChanges();


            return Ok(new
            {
                Status = true,
                Message = "Company added successfully.",
                Data = CompanyDetails
            });
        }

        [HttpPost]
        [Route("UpdateCompanyDetails")]
        public IActionResult UpdateCompanyDetails(Companyreg dto)
        {
            try
            {
                if (dto == null || dto.companyID <= 0)
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "CompanyID is required"
                    });
                }

                // =====================================================
                // CHECK IF COMPANY EXISTS
                // =====================================================

                var existingCompany = dbContext.Companys
                    .FirstOrDefault(x => x.CompanyID == dto.companyID);

                if (existingCompany == null)
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Company not found"
                    });
                }

                // =====================================================
                // CHECK DUPLICATE MOBILE (EXCLUDING CURRENT COMPANY)
                // =====================================================

                var mobileExists = dbContext.Companys
                    .Any(x => x.Mobile == dto.Mobile && x.CompanyID != dto.companyID);

                if (mobileExists)
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Mobile number already exists in another company."
                    });
                }

                // =====================================================
                // CHECK DUPLICATE EMAIL (EXCLUDING CURRENT COMPANY)
                // =====================================================

                var emailExists = dbContext.Companys
                    .Any(x => x.Email == dto.Email && x.CompanyID != dto.companyID);

                if (emailExists)
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Email already exists in another company."
                    });
                }

                // =====================================================
                // UPDATE COMPANY DETAILS
                // =====================================================

                existingCompany.CompanyCode = dto.CompanyCode;
                existingCompany.CompanyName = dto.CompanyName;
                existingCompany.Email = dto.Email;
                existingCompany.Mobile = dto.Mobile;
                existingCompany.Website = dto.Website;
                existingCompany.GSTNo = dto.GSTNo;
                existingCompany.PANNo = dto.PANNo;
                existingCompany.Address = dto.Address;
                existingCompany.City = dto.City;
                existingCompany.State = dto.State;
                existingCompany.Pincode = dto.Pincode;
                existingCompany.Logo = dto.Logo;
                existingCompany.CompanyType = dto.CompanyType;
                existingCompany.IsActive = dto.IsActive;
                existingCompany.FirstName = dto.FirstName;
                existingCompany.LastName = dto.LastName;
                existingCompany.EpId = dto.EpId;

                // Update password only if provided
                if (!string.IsNullOrWhiteSpace(dto.Password))
                {
                    var staticEncyKey = _configuration["EncryptionSettings:StaticUniqueId"];
                    existingCompany.Password = global::Recruitment.Helper.Helper.EncryptAPIData(dto.Password, staticEncyKey);
                }

                // Update timestamp
           

                dbContext.SaveChanges();

                // =====================================================
                // UPDATE VEHICLE TYPES (Replace existing)
                // =====================================================

               

            
             
                dbContext.SaveChanges();

                // =====================================================
                // UPDATE ADMIN EMPLOYEE
                // =====================================================

                var adminEmployee = dbContext.EmployeeMasters
                    .FirstOrDefault(x => x.CompanyId == dto.companyID && x.IsAdmin == true);

                if (adminEmployee != null)
                {
                    adminEmployee.FirstName = dto.FirstName ?? adminEmployee.FirstName;
                    adminEmployee.LastName = dto.LastName ?? adminEmployee.LastName;
                    adminEmployee.Mobilenumber = dto.Mobile ?? adminEmployee.Mobilenumber;
                    adminEmployee.Email = dto.Email ?? adminEmployee.Email;
                    adminEmployee.City = dto.City ?? adminEmployee.City;
                    adminEmployee.State = dto.State ?? adminEmployee.State;
                    adminEmployee.ModifiedOn = DateTime.Now;
                    adminEmployee.ModifiedBy = dto.companyID;

                    // Update password only if provided
                    if (!string.IsNullOrWhiteSpace(dto.Password))
                    {
                        var staticEncyKey = _configuration["EncryptionSettings:StaticUniqueId"];
                        adminEmployee.Password = global::Recruitment.Helper.Helper.EncryptAPIData(dto.Password, staticEncyKey);
                    }

                    dbContext.SaveChanges();
                }

                return Ok(new
                {
                    Status = true,
                    Message = "Company updated successfully.",
                    Data = new
                    {
                        existingCompany.CompanyID,
                        existingCompany.CompanyCode,
                        existingCompany.CompanyName,
                        existingCompany.Email,
                        existingCompany.Mobile,
                        existingCompany.Website,
                        existingCompany.GSTNo,
                        existingCompany.PANNo,
                        existingCompany.Address,
                        existingCompany.City,
                        existingCompany.State,
                        existingCompany.Pincode,
                        existingCompany.CompanyType,
                        existingCompany.IsActive,
                        existingCompany.Logo,
                        existingCompany.FirstName,
                        existingCompany.LastName,
                        existingCompany.EpId
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                });
            }
        }


        [HttpGet]
        [Route("GetCompanywiseDetails")]
        public IActionResult GetCompanywiseDetails(int id)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}/IMS/";

            var companyDetails = dbContext.Companys
                .Where(x => x.CompanyID == id)
                .Select(x => new
                {
                    x.CompanyID,
                    x.CompanyCode,
                    x.CompanyName,
                    x.FirstName,
                    x.LastName,
                    x.Email,
                    x.Mobile,
                    x.Website,
                    x.GSTNo,
                    x.PANNo,
                    x.Address,
                    x.City,
                    x.State,
                    x.Pincode,
                    x.CompanyType,
                    x.IsActive,
                    x.CreatedOn,
                    x.EpId,
                    Logo = string.IsNullOrWhiteSpace(x.Logo),
                    //Logo = string.IsNullOrWhiteSpace(x.Logo)
                    //    ? null
                    //    : x.Logo.StartsWith("http")
                    //        ? x.Logo
                    //        : baseUrl + x.Logo,

                
                })
                .FirstOrDefault();

            if (companyDetails == null)
            {
                return NotFound(new
                {
                    Status = false,
                    Message = "Company not found"
                });
            }



            return Ok(new
            {
                Status = true,
                Message = "Company details fetched successfully",
                Data = companyDetails
            });
        }

        public class LoginRequest
        {
            public string? Mobile { get; set; }

            public string? Password { get; set; }
        }

        //[HttpPost("Login")]
        //public IActionResult Login([FromBody] LoginRequest? dto)
        //{
        //    IActionResult Fail(int code, string message)
        //    {
        //        return StatusCode(code, new
        //        {
        //            Status = false,
        //            StatusCode = code,
        //            Message = message
        //        });
        //    }

        //    try
        //    {
        //        if (dto == null)
        //            return Fail(400, "Invalid request.");

        //        if (string.IsNullOrWhiteSpace(dto.Mobile))
        //        {
        //            return Fail(
        //                400,
        //                "Mobile number or role email is required.");
        //        }

        //        if (string.IsNullOrWhiteSpace(dto.Password))
        //            return Fail(400, "Password is required.");

        //        var staticEncyKey =
        //            _configuration["EncryptionSettings:StaticUniqueId"];

        //        if (string.IsNullOrWhiteSpace(staticEncyKey))
        //        {
        //            return Fail(
        //                500,
        //                "Encryption key is not configured.");
        //        }

        //        // Same encryption used when saving Password and RolePW.
        //        var encryptedPassword =
        //            global::Recruitment.Helper.Helper.EncryptAPIData(
        //                dto.Password,
        //                staticEncyKey);

        //        var loginValue = dto.Mobile.Trim();

        //        bool isRoleLogin = loginValue.Contains("@");

        //        var employeeQuery = dbContext.EmployeeMasters
        //            .AsNoTracking()
        //            .Where(x => x.IsActive == true);

        //        var employee = isRoleLogin
        //            ? employeeQuery.FirstOrDefault(x =>
        //                x.RoleMail != null &&
        //                EF.Functions.Collate(
        //                    x.RoleMail.Trim(),
        //                    "SQL_Latin1_General_CP1_CI_AS") == loginValue &&
        //                x.RolePW == encryptedPassword)

        //            : employeeQuery.FirstOrDefault(x =>
        //                x.Mobilenumber == loginValue &&
        //                x.Password == encryptedPassword);

        //        if (employee == null)
        //        {
        //            return Fail(
        //                401,
        //                isRoleLogin
        //                    ? "Invalid Role Email or Password."
        //                    : "Invalid Mobile Number or Password.");
        //        }

        //        // Use the authenticated employee's CompanyId.
        //        var company = dbContext.Companys
        //            .AsNoTracking()
        //            .Where(x => x.CompanyID == employee.CompanyId)
        //            .Select(x => new
        //            {
        //                x.CompanyName,
        //                x.EpId
        //            })
        //            .FirstOrDefault();

        //        var companyName = company?.CompanyName ?? "";

        //        // Null, zero or negative EPID returns false.
        //        // This flag does not block login.
        //        bool isEP = company != null && company.EpId > 0;

        //        bool isAdmin = employee.IsAdmin == true;

        //        // Admin access is determined by IsAdmin.
        //        string roleName = isAdmin
        //            ? "Admin"
        //            : dbContext.Roles
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.CompanyId == employee.CompanyId &&
        //                    x.RoleId == employee.RoleId)
        //                .Select(x => x.RoleName)
        //                .FirstOrDefault() ?? "Employee";

        //        string? userName = isRoleLogin
        //            ? employee.RoleMail
        //            : employee.Mobilenumber;

        //        return Ok(new
        //        {
        //            Status = true,
        //            StatusCode = 200,
        //            Message = "Login Successful.",

        //            Data = new
        //            {
        //                EmployeeId = employee.EmployeeId,
        //                EmployeeCode = employee.EmpCode,

        //                FirstName = employee.FirstName,
        //                LastName = employee.LastName,

        //                EmployeeName =
        //                    ((employee.FirstName ?? "") + " " +
        //                     (employee.LastName ?? "")).Trim(),

        //                Email = employee.Email,
        //                Mobile = employee.Mobilenumber,

        //                CompanyID = employee.CompanyId,
        //                CompanyName = companyName,
        //                IsEP = isEP,

        //                BranchId = employee.BranchId,
        //                DepartmentId = employee.DepartmentId,

        //                IsAdmin = isAdmin,

        //                RoleId = employee.RoleId,
        //                RoleName = roleName,

        //                UserName = userName
        //            }
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Serilog.Log.Error(ex, "Employee login failed.");

        //        return Fail(
        //            500,
        //            "Unable to process login.");
        //    }
        //}


        [HttpPost("Login")]
        public IActionResult Login([FromBody] LoginRequest? dto)
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

            try
            {
                if (dto == null)
                    return Fail(400, "Invalid request.");

                if (string.IsNullOrWhiteSpace(dto.Mobile))
                {
                    return Fail(
                        400,
                        "Mobile number or role email is required.");
                }

                if (string.IsNullOrWhiteSpace(dto.Password))
                    return Fail(400, "Password is required.");

                var staticEncyKey =
                    _configuration["EncryptionSettings:StaticUniqueId"];

                if (string.IsNullOrWhiteSpace(staticEncyKey))
                {
                    return Fail(
                        500,
                        "Encryption key is not configured.");
                }

                // Same encryption used when saving Password and RolePW.
                var encryptedPassword =
                    global::Recruitment.Helper.Helper.EncryptAPIData(
                        dto.Password,
                        staticEncyKey);

                var loginValue = dto.Mobile.Trim();

                bool isRoleLogin = loginValue.Contains("@");

                var employeeQuery = dbContext.EmployeeMasters
                    .AsNoTracking()
                    .Where(x => x.IsActive == true);

                var employee = isRoleLogin
                    ? employeeQuery.FirstOrDefault(x =>
                        x.RoleMail != null &&
                        EF.Functions.Collate(
                            x.RoleMail.Trim(),
                            "SQL_Latin1_General_CP1_CI_AS") == loginValue &&
                        x.RolePW == encryptedPassword)

                    : employeeQuery.FirstOrDefault(x =>
                        x.Mobilenumber == loginValue &&
                        x.Password == encryptedPassword);

                if (employee == null)
                {
                    return Fail(
                        401,
                        isRoleLogin
                            ? "Invalid Role Email or Password."
                            : "Invalid Mobile Number or Password.");
                }

                var company = dbContext.Companys
                    .AsNoTracking()
                    .Where(x => x.CompanyID == employee.CompanyId)
                    .Select(x => new
                    {
                        x.CompanyName,
                        x.EpId
                    })
                    .FirstOrDefault();

                var companyName = company?.CompanyName ?? "";

                bool isEP = company != null && company.EpId > 0;

                bool isAdmin = employee.IsAdmin == true;

                string roleName = isAdmin
                    ? "Admin"
                    : dbContext.Roles
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == employee.CompanyId &&
                            x.RoleId == employee.RoleId)
                        .Select(x => x.RoleName)
                        .FirstOrDefault() ?? "Employee";

                string? userName = isRoleLogin
                    ? employee.RoleMail
                    : employee.Mobilenumber;

                // Only the requested modules, in the requested display order.
                var allowedModuleNames = isAdmin
                    ? new[]
                    {
                "Dashboard",
                "Role Assignment",
                "Job Description",
                "JD Statistics",
                "Candidate"
                    }
                    : new[]
                    {
                "Dashboard",
                "Job Description",
                "JD Statistics",
                "Candidate"
                    };

                // Fetch actual IDs and names from the Module table.
                var moduleRows = dbContext
                    .Set<global::Recruitment.Model.Entity.Module>()
                    .AsNoTracking()
                    .Where(x => allowedModuleNames.Contains(
                        EF.Functions.Collate(
                            x.ModuleName.Trim(),
                            "SQL_Latin1_General_CP1_CI_AS")))
                    .Select(x => new
                    {
                        x.ModuleId,
                        x.ModuleName
                    })
                    .ToList();
                var modules = moduleRows
                    .OrderBy(x => Array.FindIndex(
                        allowedModuleNames,
                        name => string.Equals(
                            name,
                            x.ModuleName.Trim(),
                            StringComparison.OrdinalIgnoreCase)))
                    .ThenBy(x => x.ModuleId)
                    .Select(x => new
                    {
                        x.ModuleId,
                        x.ModuleName,
                        CanAdd = true,
                        CanEdit = true,
                        CanView = true,
                        CanDelete = true,
                        CanDownload = true
                    })
                    .ToList();

                return Ok(new
                {
                    Status = true,
                    StatusCode = 200,
                    Message = "Login Successful.",

                    Data = new
                    {
                        EmployeeId = employee.EmployeeId,
                        EmployeeCode = employee.EmpCode,

                        FirstName = employee.FirstName,
                        LastName = employee.LastName,

                        EmployeeName =
                            ((employee.FirstName ?? "") + " " +
                             (employee.LastName ?? "")).Trim(),

                        Email = employee.Email,
                        Mobile = employee.Mobilenumber,

                        CompanyID = employee.CompanyId,
                        CompanyName = companyName,
                        IsEP = isEP,

                        BranchId = employee.BranchId,
                        DepartmentId = employee.DepartmentId,

                        IsAdmin = isAdmin,

                        RoleId = employee.RoleId,
                        RoleName = roleName,

                        UserName = userName,

                        Modules = modules
                    }
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Employee login failed.");

                return Fail(
                    500,
                    "Unable to process login.");
            }
        }
        public static string GenerateOTP()
        {
            byte[] bytes = new byte[4];
            RandomNumberGenerator.Fill(bytes);

            int otp = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 900000 + 100000;

            return otp.ToString();
        }

        [HttpPost]
        [Route("SendRegOtp")]
        public IActionResult SendRegOtp([FromBody] SendOtpRequest request)
        {
            try
            {
                if (request == null)
                    return Ok(new { Status = false, Message = "Invalid request" });

                if (string.IsNullOrWhiteSpace(request.Email))
                    return Ok(new { Status = false, Message = "Email is required" });

                string otp = GenerateOTP();

                string fromMailID = _configuration["MailSettings:FromMailID"];
                string mailPassword = _configuration["MailSettings:MailPassword"];
                string mailServerHOST = _configuration["MailSettings:MailServerHOST"];
                string sendingPort = _configuration["MailSettings:SendingPort"];

                string subject = "OTP Verification Code";

                string body = $@"
            <p>Team {request.Name},</p>

            <p>Your One-Time Password (OTP) for verification is:</p>

            <h2 style='letter-spacing:4px;color:#2c3e50;'>{otp}</h2>

            <p>Please enter this OTP to continue the verification process.</p>

            <p>This OTP is confidential. Please do not share it with anyone.</p>

            <br/>
            <p>Regards,<br/>IMS Team</p>
        ";

                MailMessage mail = new MailMessage();
                SmtpClient smtpServer = new SmtpClient(mailServerHOST);

                mail.From = new MailAddress(fromMailID, "Hiring Team");
                mail.To.Add(request.Email);
                mail.Subject = subject;
                mail.Body = body;
                mail.IsBodyHtml = true;

                smtpServer.Port = Convert.ToInt32(sendingPort);
                smtpServer.Credentials = new NetworkCredential(fromMailID, mailPassword);
                smtpServer.EnableSsl = true;

                smtpServer.Send(mail);

                return Ok(new
                {
                    Status = true,
                    Message = "OTP sent successfully",
                    OTP = otp
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

        [HttpPost]
        [Route("CheckDuplicateMobileNumber")]
        public IActionResult CheckDuplicateMobileNumber([FromBody] CheckMobileNumberRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Request data is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.MobileNumber))
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Mobile number is required"
                    });
                }

                string mobileNumber = request.MobileNumber.Trim();

                bool exists = dbContext.Companys.Any(x =>
                                    x.Mobile == mobileNumber)
                              ||
                              dbContext.EmployeeMasters.Any(x =>
                                    x.Mobilenumber == mobileNumber);

                if (exists)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "Mobile number already exists"
                    });
                }

                return Ok(new
                {
                    Status = false,
                    Message = "Mobile number not exists"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                });
            }
        }
        public class CheckMobileNumberRequest
        {
            public string? MobileNumber { get; set; }
        }

        [HttpPost]
        [Route("CheckDuplicateEmail")]
        public IActionResult CheckDuplicateEmail([FromBody] CheckEmailRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Request data is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Email is required"
                    });
                }

                string email = request.Email.Trim().ToLower();

                bool exists = dbContext.Companys.Any(x =>
                                    x.Email != null &&
                                    x.Email.ToLower() == email)
                              ||
                              dbContext.EmployeeMasters.Any(x =>
                                    x.Email != null &&
                                    x.Email.ToLower() == email);

                if (exists)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "Email already exists"
                    });
                }

                return Ok(new
                {
                    Status = false,
                    Message = "Email not exists"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                });
            }
        }


        public class CheckEmailRequest
        {
            public string? Email { get; set; }
        }


        public class SendOtpRequest
        {
            public string? Email { get; set; }
            public string? Name { get; set; }
        }


        ///=]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
        /// ///=]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
        ///  ///=]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
        ///   ///=]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
        ///    ///=]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
        ///    


        public class Companyreg
        {
            public string? CompanyCode { get; set; }
            public int? EpId { get; set; }
            public int? AdminId { get; set; }
            public int companyID { get; set; }
            public string? CompanyName { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Email { get; set; }

            public string? Mobile { get; set; }

            public string? Website { get; set; }

            public string? GSTNo { get; set; }

            public string? PANNo { get; set; }

            public string? Address { get; set; }

            public string? City { get; set; }

            public string? State { get; set; }

            public string? Pincode { get; set; }

            public string? Logo { get; set; }

            public string? CompanyType { get; set; }

            public bool? IsActive { get; set; }
            public string? Password { get; set; }
            public DateTime? CreatedOn { get; set; }
            public List<int>? VehicleTypeIds { get; set; }
            public List<int>? BrandIds { get; set; }
        }
    }
}
