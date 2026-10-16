using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;

namespace Recruitment.Controllers
{
    [Route("api/DeviceTokenController")]
    [ApiController]
    public class DeviceTokenController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public DeviceTokenController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }




        [HttpPost("SaveEmployeeDeviceToken")]
        public async Task<IActionResult> SaveEmployeeDeviceToken([FromBody] SaveEmployeeDeviceTokenRequest? request)
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

            if (request == null ||
                request.CompanyId <= 0 ||
                request.EmployeeId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId and EmployeeId are required.");
            }

            var deviceId = request.DeviceId?.Trim();
            var deviceToken = request.DeviceToken?.Trim();

            if (string.IsNullOrWhiteSpace(deviceId) ||
                deviceId.Length > 200)
            {
                return Fail(
                    400,
                    "A valid DeviceId of up to 200 characters is required.");
            }

            if (request.IsLoggedIn &&
                (string.IsNullOrWhiteSpace(deviceToken) ||
                 deviceToken.Length > 2048))
            {
                return Fail(
                    400,
                    "A valid DeviceToken of up to 2048 characters is required.");
            }

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

                if (company.EpId > 0)
                {
                    return Fail(
                        409,
                        "This company uses device tokens stored in EP.");
                }

                var employee = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.EmployeeId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (employee == null)
                    return Fail(404, "Employee not found for this company.");

                // Allow logout even if an employee has become inactive.
                if (request.IsLoggedIn && employee.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var existing = await _context.EmployeeDeviceTokens
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.DeviceId == deviceId);

                var now = DateTime.UtcNow;

                if (!request.IsLoggedIn)
                {
                    // Do not let a delayed logout for another employee
                    // disable the current user's token on a shared device.
                    if (existing != null &&
                        existing.EmployeeId == request.EmployeeId)
                    {
                        existing.IsLoggedIn = false;
                        existing.ModifiedOn = now;

                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        Status = true,
                        StatusCode = 200,
                        Message = "Device logout recorded successfully."
                    });
                }

                // Disable duplicate registrations of this token on other
                // device records within this company.
                var duplicateTokens = await _context.EmployeeDeviceTokens
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.DeviceId != deviceId &&
                        x.DeviceToken == deviceToken &&
                        x.IsLoggedIn)
                    .ToListAsync();

                foreach (var duplicate in duplicateTokens)
                {
                    duplicate.IsLoggedIn = false;
                    duplicate.ModifiedOn = now;
                }

                if (existing == null)
                {
                    existing = new EmployeeDeviceToken
                    {
                        CompanyId = request.CompanyId,
                        EmployeeId = request.EmployeeId,
                        DeviceId = deviceId!,
                        DeviceToken = deviceToken!,
                        IsLoggedIn = true,
                        CreatedOn = now
                    };

                    _context.EmployeeDeviceTokens.Add(existing);
                }
                else
                {
                    // A shared device now belongs to the employee logging in.
                    existing.EmployeeId = request.EmployeeId;
                    existing.DeviceToken = deviceToken!;
                    existing.IsLoggedIn = true;
                    existing.ModifiedOn = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    StatusCode = 200,
                    Message = "Employee device token saved successfully.",
                    Data = new
                    {
                        existing.EmployeeDeviceTokenId,
                        existing.CompanyId,
                        existing.EmployeeId,
                        existing.DeviceId,
                        existing.IsLoggedIn
                    }
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save device token for CompanyId {CompanyId}, EmployeeId {EmployeeId}.",
                    request.CompanyId,
                    request.EmployeeId);

                return Fail(500, "Unable to save employee device token.");
            }
        }












        public class SaveEmployeeDeviceTokenRequest
        {
            public int CompanyId { get; set; }

            public int EmployeeId { get; set; }

            public string? DeviceId { get; set; }

            public string? DeviceToken { get; set; }

            public bool IsLoggedIn { get; set; } = true;
        }
    }
}
