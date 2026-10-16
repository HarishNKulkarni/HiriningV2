
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;

namespace IMS.Controllers
{
    [Route("api/DepartmentController")]
    [ApiController]
    public class DepartmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        public DepartmentController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }
        [HttpPost("save")]
        public async Task<IActionResult> SaveDepartment(
      [FromBody] SaveDepartmentRequest request)
        {
            if (request.DepartmentId < 0)
            {
                return BadRequest(new
                {
                    StatusCode = 400,
                    message = "Invalid DepartmentId."
                });
            }

            var name = request.DepartmentName?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new
                {
                    StatusCode = 400,
                    message = "Department name is required."
                });
            }

            if (name.Length > 150)
            {
                return BadRequest(new
                {
                    StatusCode = 400,
                    message = "Department name cannot exceed 150 characters."
                });
            }

            if (request.BranchId <= 0 || request.CompanyId <= 0)
            {
                return BadRequest(new
                {
                    StatusCode = 400,
                    message = "Valid BranchId and CompanyId are required."
                });
            }

            bool isNew = request.DepartmentId == 0;

            var userId = isNew ? request.CreatedBy : request.ModifiedBy;

            if (userId.HasValue && userId.Value <= 0)
            {
                return BadRequest(new
                {
                    StatusCode = 400,
                    message = "Invalid creator or modifier user ID."
                });
            }

            Department department;

            if (isNew)
            {
                department = new Department();
            }
            else
            {
                var existing = await _context.Departments
                    .FirstOrDefaultAsync(
                        d => d.DepartmentId == request.DepartmentId);

                if (existing == null)
                {
                    return NotFound(new
                    {
                        StatusCode = 404,
                        message = "Department not found."
                    });
                }

                department = existing;
            }

            // Uses the case-insensitive database column configuration.
            var duplicate = await _context.Departments.AnyAsync(
                d => d.BranchId == request.BranchId
                     && d.DepartmentName == name
                     && d.DepartmentId != request.DepartmentId);

            if (duplicate)
            {
                return Conflict(new
                {
                    StatusCode = 409,
                    message = "Department name already exists in this branch."
                });
            }

            department.DepartmentName = name;
            department.BranchId = request.BranchId;
            department.CompanyId = request.CompanyId;
            department.Location = request.Location?.Trim();
            department.City = request.City?.Trim();
            department.District = request.District?.Trim();
            department.State = request.State?.Trim();
            department.IsActive = request.IsActive;

            if (isNew)
            {
                department.CreatedOn = DateTime.UtcNow;
                department.CreatedBy = request.CreatedBy;
                department.ModifiedOn = null;
                department.ModifiedBy = null;

                _context.Departments.Add(department);
            }
            else
            {
                department.ModifiedOn = DateTime.UtcNow;
                department.ModifiedBy = request.ModifiedBy;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql
                      && (sql.Number == 2601 || sql.Number == 2627))
            {
                return Conflict(new
                {
                    StatusCode = 409,
                    message = "A department with these unique details already exists."
                });
            }

            return StatusCode(isNew ? 201 : 200, new
            {
                StatusCode = isNew ? 201 : 200,
                message = isNew
                    ? "Department created successfully."
                    : "Department updated successfully.",
                data = department
            });
        }
        [HttpPost("GetDepartmentList")]
        public async Task<IActionResult> GetDepartmentList(
       [FromBody] GetDepartmentListRequest? request)
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

                var selectedBranchIds = allBranches
                    ? Array.Empty<int>()
                    : request.BranchIds!.Distinct().ToArray();

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

                var query = _context.Departments
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId);

                if (!allBranches)
                {
                    query = query.Where(x =>
                        selectedBranchIds.Contains(x.BranchId));
                }

                var departments = await (
                    from department in query
                    join branch in _context.Branches.AsNoTracking()
                        on new
                        {
                            department.CompanyId,
                            department.BranchId
                        }
                        equals new
                        {
                            branch.CompanyId,
                            branch.BranchId
                        }
                    orderby department.DepartmentId descending
                    select new
                    {
                        department.DepartmentId,
                        department.DepartmentName,
                        department.CompanyId,
                        CompanyName = company.CompanyName,
                        department.BranchId,
                        branch.BranchName,
                        department.Location,
                        department.City,
                        department.District,
                        department.State,
                        department.IsActive,
                        department.CreatedOn,
                        department.CreatedBy,
                        department.ModifiedOn,
                        department.ModifiedBy
                    }
                ).ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = departments.Count > 0
                        ? "Department list fetched successfully."
                        : "No departments found.",
                    Data = departments
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch departments for company {CompanyId}",
                    request.CompanyId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch department list."
                });
            }
        }

        [HttpDelete("delete/{id:int}")]
        public async Task<IActionResult> DeleteDepartment(
    int id,
    CancellationToken cancellationToken)
        {
            if (id <= 0)
                return BadRequest(new { message = "Invalid DepartmentId." });

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            var department = await _context.Departments
                .FirstOrDefaultAsync(
                    d => d.DepartmentId == id,
                    cancellationToken);

            if (department == null)
                return NotFound(new { message = "Department not found." });

            var hasActiveEmployees = await _context.EmployeeMasters.AnyAsync(
                e => e.DepartmentId == id && e.IsActive == true,
                cancellationToken);

            if (hasActiveEmployees)
                return Conflict(new
                {
                    message =
                        "Cannot delete this department because active employees are assigned to it."
                });

            _context.Departments.Remove(department);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql && sql.Number == 547)
            {
                await transaction.RollbackAsync(cancellationToken);

                return Conflict(new
                {
                    message =
                        "Cannot delete this department because related records still reference it."
                });
            }

            return Ok(new { message = "Department deleted successfully." });
        }










        public class SaveDepartmentRequest
        {
            public int DepartmentId { get; set; }
            public string? DepartmentName { get; set; }
            public int BranchId { get; set; }
            public int CompanyId { get; set; }
            public string? Location { get; set; }
            public string? City { get; set; }
            public string? District { get; set; }
            public string? State { get; set; }
            public int? CreatedBy { get; set; }
            public int? ModifiedBy { get; set; }
            public bool IsActive { get; set; } = true;
        }


        public class GetDepartmentListRequest
        {
            public int CompanyId { get; set; }

            public List<int>? BranchIds { get; set; }
        }
    }
}
