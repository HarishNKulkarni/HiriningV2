
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.Globalization;
using System.Text.RegularExpressions;
using static IMS.Controllers.EmployeeController;

namespace IMS.Controllers
{
    [Route("api/BranchController")]
    [ApiController]
    public class BranchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
   
        public BranchController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("CreateBranch")]
        public IActionResult CreateBranch([FromBody] CreateBranchRequest request)
        {
            try
            {
                if (request == null)
                    return Ok(new { Status = false, Message = "Invalid request" });

                if (request.CompanyId <= 0)
                    return Ok(new { Status = false, Message = "CompanyId is required" });

                if (string.IsNullOrWhiteSpace(request.BranchName))
                    return Ok(new { Status = false, Message = "BranchName is required" });

                if (string.IsNullOrWhiteSpace(request.BranchContactNumber))
                    return Ok(new { Status = false, Message = "Branch ContactNumber is required" });

                var companyExists = _context.Companys
                    .Any(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Ok(new { Status = false, Message = "Company not found" });



                request.BranchName = request.BranchName.Trim();

                // Ignore uppercase, lowercase and spaces
                string normalizedBranchName = request.BranchName
                    .Replace(" ", "")
                    .ToLower();

                var branchNameExists = _context.Branches.Any(x =>
                    x.CompanyId == request.CompanyId &&
                    x.IsActive == true &&
                    x.BranchName != null &&
                    x.BranchName.Trim()
                        .Replace(" ", "")
                        .ToLower() == normalizedBranchName
                );

                if (branchNameExists)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "BranchName Already Exists For This Company"
                    });
                }








                if (request.BranchContactNumber!=null)
                {
                    var contactExists = _context.Branches
                    .Any(x => x.BranchContactNumber == request.BranchContactNumber&&x.IsActive!=false);

                    if (contactExists)
                        return Ok(new { Status = false, Message = "Branch Contact Number Already Exists" });
                }
                var activeDivision = "";




                var branch = new Branch
                {
                    BranchName = request.BranchName,
                    Location = request.Location,
                    City = request.City,
                    District = request.District,
                    State = request.State,
                    CompanyId = request.CompanyId,
                    BranchCode = request.BranchCode,
                    BranchContactNumber = request.BranchContactNumber,
                    CreatedOn = DateTime.Now,
                    CreatedBy = request.CreatedBy,
                    IsActive = true,
                   
                   
                   
                };

                _context.Branches.Add(branch);
                _context.SaveChanges();



            

         

                    return Ok(new
                {
                    Status = true,
                    Message = "Branch created successfully",
                    BranchId = branch.BranchId
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
        [Route("CheckDuplicateBranchCode")]
        public IActionResult CheckDuplicateBranchCode([FromBody] CheckBranchCodeRequest request)
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

                if (request.CompanyId <= 0)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "CompanyId is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.BranchCode))
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "BranchCode is required"
                    });
                }

                string branchCode = request.BranchCode
                    .Replace(" ", "")
                    .Trim()
                    .ToLower();

                bool exists = _context.Branches.Any(x =>
                    x.CompanyId == request.CompanyId &&
                    x.BranchCode != null &&
                    x.BranchCode.Replace(" ", "").Trim().ToLower() == branchCode &&
                    x.IsActive);

                if (exists)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "Branch code already exists"
                    });
                }

                return Ok(new
                {
                    Status = false,
                    Message = "Branch code not exists"
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


        [HttpGet]
        [Route("GetBranchList")]
        public IActionResult GetBranchList([FromQuery] int companyId)
        {
            if (companyId <= 0)
            {
                return Ok(new
                {
                    Status = false,
                    Message = "CompanyId is required"
                });
            }

            var companyExists = _context.Companys
                .Any(x => x.CompanyID == companyId);

            if (!companyExists)
            {
                return Ok(new
                {
                    Status = false,
                    Message = "Company not found"
                });
            }

            var branches = _context.Branches
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.IsActive == true)
                .OrderByDescending(x => x.BranchId)
                .Select(x => new
                {
                    x.BranchId,
                    x.BranchName,
                    x.BranchCode,
                    x.BranchContactNumber,
                    x.CompanyId,
                    x.Location,
                    x.City,
                    x.District,
                    x.State,
                    x.IsActive,
                    x.CreatedOn,
                    x.CreatedBy
                })
                .ToList();

            return Ok(new
            {
                Status = true,
                Message = branches.Count > 0
                    ? "Branch list fetched successfully"
                    : "No active branches found",
                Data = branches
            });
        }

        public class CheckBranchCodeRequest
        {
            public int CompanyId { get; set; }
            public string? BranchCode { get; set; }
        }

        [HttpPost]
        [Route("UpdateBranch")]
        public IActionResult UpdateBranch([FromBody] UpdateBranchRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Invalid request"
                    });
                }

                if (request.BranchId <= 0)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "BranchId is required"
                    });
                }

                if (request.CompanyId <= 0)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "CompanyId is required"
                    });
                }

                var companyExists = _context.Companys
                    .Any(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Company not found"
                    });
                }

                var branch = _context.Branches
                    .FirstOrDefault(x => x.BranchId == request.BranchId);

                if (branch == null)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "Branch not found"
                    });
                }

                var duplicateContact = _context.Branches
                    .Any(x => x.BranchContactNumber == request.BranchContactNumber&&x.CompanyId==request.CompanyId
                           && x.BranchId != request.BranchId);

                if (duplicateContact)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "BranchContactNumber already exists"
                    });
                }
                var activeDivision = "";

                if (request.ActiveDivisions != null && request.ActiveDivisions.Count > 0)
                {
                    activeDivision = string.Join(",", request.ActiveDivisions);
                }

                branch.BranchName = request.BranchName;
                branch.Location = request.Location;
                branch.City = request.City;
                branch.District = request.District;
                branch.State = request.State;
                branch.CompanyId = request.CompanyId;
                branch.BranchCode = request.BranchCode;
                branch.BranchContactNumber = request.BranchContactNumber;
                branch.ModifiedOn = DateTime.Now;
                branch.ModifiedBy = request.ModifiedBy;

                _context.SaveChanges();

                return Ok(new
                {
                    Status = true,
                    Message = "Branch updated successfully"
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


    










        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///        
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///
        public class BulkUploadBranchRequest
        {
            public IFormFile? File { get; set; }

            public int CompanyId { get; set; }

            public int CreatedBy { get; set; }
        }
        public class CreateBranchRequest
        {
            public string? BranchName { get; set; }
            public string? Location { get; set; }
            public string? City { get; set; }
            public string? District { get; set; }
            public string? State { get; set; }
            public int CompanyId { get; set; }
           
            public string? BranchCode { get; set; }
            public string? BranchContactNumber { get; set; }
            public int? CreatedBy { get; set; }
            public string? Region { get; set; }

           
        }


        public class UpdateBranchRequest
        {
            public int BranchId { get; set; }

            public string? BranchName { get; set; }
            public string? Location { get; set; }
            public string? City { get; set; }
            public string? District { get; set; }
            public string? State { get; set; }

            public int CompanyId { get; set; }

            public string? BranchCode { get; set; }
            public string? BranchContactNumber { get; set; }

            public int? ModifiedBy { get; set; }
            public string? Region { get; set; }
            public List<string>? ActiveDivisions { get; set; }
        }

        public class DeleteBranchRequest
        {
            public int BranchId { get; set; }
        }
        public class GetBranchRequest
        {
            public int CompanyId { get; set; }
            public int? UserId { get; set; }
        }
    }
}
