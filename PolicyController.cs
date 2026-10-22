using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;

namespace Recruitment.Controllers
{
    [Route("api/PolicyController")]
    [ApiController]
    public class PolicyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public PolicyController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }

        [HttpPost("SaveCompanyPolicy")]
        public async Task<IActionResult> SaveCompanyPolicy(
     [FromBody] SaveCompanyPolicyRequest? request)
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

            if (request.CompanyId <= 0 || request.UserId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId and UserId are required.");
            }

            if (request.Policies == null || request.Policies.Count == 0)
                return Fail(400, "At least one policy is required.");

            foreach (var item in request.Policies)
            {
                if (item == null)
                    return Fail(400, "Policy cannot be null.");

                if (item.PolicyId < 0)
                    return Fail(400, "Invalid PolicyId.");

                item.PolicyName = item.PolicyName?.Trim();
                item.PolicyDescription = item.PolicyDescription?.Trim();

                item.FilePath = string.IsNullOrWhiteSpace(item.FilePath)
                    ? null
                    : item.FilePath.Trim();

                if (string.IsNullOrWhiteSpace(item.PolicyName))
                    return Fail(400, "PolicyName is required.");

                if (item.PolicyName.Length > 200)
                {
                    return Fail(
                        400,
                        "PolicyName cannot exceed 200 characters.");
                }

                if (item.FilePath?.Length > 1000)
                {
                    return Fail(
                        400,
                        "FilePath cannot exceed 1000 characters.");
                }
            }

            if (request.Policies
                .Where(x => x.PolicyId > 0)
                .GroupBy(x => x.PolicyId)
                .Any(group => group.Count() > 1))
            {
                return Fail(
                    400,
                    "The same PolicyId cannot appear multiple times.");
            }

            if (request.Policies
                .GroupBy(
                    x => x.PolicyName!,
                    StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                return Fail(
                    409,
                    "Duplicate policy names are not allowed in the request.");
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

                var updateIds = request.Policies
                    .Where(x => x.PolicyId > 0)
                    .Select(x => x.PolicyId)
                    .ToList();

                var existingPolicies = await _context.CompanyPolicies
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        updateIds.Contains(x.PolicyId))
                    .ToDictionaryAsync(x => x.PolicyId);

                var missingIds = updateIds
                    .Where(id => !existingPolicies.ContainsKey(id))
                    .ToList();

                if (missingIds.Count > 0)
                {
                    return Fail(
                        404,
                        "Policies not found for this company: " +
                        string.Join(", ", missingIds));
                }

                // Preserve the existing company-wise duplicate-name validation.
                foreach (var item in request.Policies)
                {
                    var duplicateName = await _context.CompanyPolicies
                        .AnyAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.PolicyId != item.PolicyId &&
                            EF.Functions.Collate(
                                x.PolicyName.Trim(),
                                "SQL_Latin1_General_CP1_CI_AS") ==
                            item.PolicyName);

                    if (duplicateName)
                    {
                        return Fail(
                            409,
                            $"Policy name '{item.PolicyName}' already exists for this company.");
                    }
                }

                var now = DateTime.UtcNow;

                var savedPolicies = new List<CompanyPolicy>();

                int createdCount = 0;
                int updatedCount = 0;

                foreach (var item in request.Policies)
                {
                    CompanyPolicy policy;

                    if (item.PolicyId == 0)
                    {
                        policy = new CompanyPolicy
                        {
                            CompanyId = request.CompanyId,
                            CreatedBy = request.UserId,
                            CreatedDate = now
                        };

                        _context.CompanyPolicies.Add(policy);
                        createdCount++;
                    }
                    else
                    {
                        policy = existingPolicies[item.PolicyId];

                        // Preserve original creation details.
                        policy.ModifiedBy = request.UserId;
                        policy.ModifiedDate = now;

                        updatedCount++;
                    }

                    policy.PolicyName = item.PolicyName!;
                    policy.PolicyDescription = item.PolicyDescription;

                    // Save the supplied link without checking physical storage.
                    policy.FilePath = item.FilePath;

                    savedPolicies.Add(policy);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Company policies saved successfully.",
                    TotalCount = savedPolicies.Count,
                    CreatedCount = createdCount,
                    UpdatedCount = updatedCount,

                    Data = savedPolicies.Select(policy => new
                    {
                        policy.PolicyId,
                        policy.PolicyName,
                        policy.PolicyDescription,
                        policy.FilePath,
                        policy.CompanyId,
                        policy.CreatedBy,
                        policy.CreatedDate,
                        policy.ModifiedDate,
                        policy.ModifiedBy
                    }).ToList()
                });
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                return Fail(
                    409,
                    "A duplicate company policy record was detected.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save company policies for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return Fail(500, "Unable to save company policies.");
            }
        }
        [HttpPost("GetCompanyPolicyList")]
        public async Task<IActionResult> GetCompanyPolicyList(
    [FromBody] GetCompanyPolicyListRequest? request)
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
                var companyExists = await _context.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var user = await _context.EmployeeMasters
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

                var data = await _context.CompanyPolicies
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId)
                    .OrderByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.PolicyId)
                    .Select(x => new
                    {
                        x.PolicyId,
                        x.PolicyName,
                        x.PolicyDescription,
                        x.FilePath,
                        x.CompanyId,
                        x.CreatedBy,
                        x.CreatedDate,
                        x.ModifiedDate,
                        x.ModifiedBy
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Company policies fetched successfully."
                        : "No company policies found.",
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch company policies for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return Fail(500, "Unable to fetch company policies.");
            }
        }
        public class SaveCompanyPolicyRequest
        {
            public int CompanyId { get; set; }

            public int UserId { get; set; }

            public List<SaveCompanyPolicyItem>? Policies { get; set; }
        }

        public class SaveCompanyPolicyItem
        {
            public int PolicyId { get; set; }

            public string? PolicyName { get; set; }

            public string? PolicyDescription { get; set; }

            public string? FilePath { get; set; }
        }

        public class GetCompanyPolicyListRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }
        }
    }
}
