using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;

namespace Recruitment.Controllers
{
    [Route("api/CityController")]
    [ApiController]
    public class CityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        public CityController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }



        [HttpPost("SaveCity")]
        public async Task<IActionResult> SaveCity(
    [FromBody] SaveCityRequest? request)
        {
            if (request == null || request.CompanyId <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Valid CompanyId is required."
                });
            }

            var cityName = request.CityName?.Trim();

            if (string.IsNullOrWhiteSpace(cityName))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "City name is required."
                });
            }

            if (cityName.Length > 150)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "City name cannot exceed 150 characters."
                });
            }

            try
            {
                var companyExists = await _context.Companys
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                {
                    return NotFound(new
                    {
                        Status = false,
                        Message = "Company not found."
                    });
                }

                var existingCity = await _context.Cities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        EF.Functions.Collate(
                            x.CityName.Trim(),
                            "SQL_Latin1_General_CP1_CI_AS") == cityName);

                if (existingCity != null)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "City already exists.",
                        Data = new
                        {
                            existingCity.CityId,
                            existingCity.CompanyId,
                            existingCity.CityName,
                            existingCity.IsActive
                        }
                    });
                }

                var city = new City
                {
                    CompanyId = request.CompanyId,
                    CityName = cityName,
                    IsActive = true,
                    CreatedOn = DateTime.UtcNow
                };

                _context.Cities.Add(city);

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                    when (ex.InnerException is SqlException sql &&
                          (sql.Number == 2601 || sql.Number == 2627))
                {
                    // Another request may have inserted this city first.
                    _context.Entry(city).State = EntityState.Detached;

                    var savedCity = await _context.Cities
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            EF.Functions.Collate(
                                x.CityName.Trim(),
                                "SQL_Latin1_General_CP1_CI_AS") == cityName);

                    if (savedCity == null)
                        throw;

                    return Ok(new
                    {
                        Status = true,
                        Message = "City already exists.",
                        Data = new
                        {
                            savedCity.CityId,
                            savedCity.CompanyId,
                            savedCity.CityName,
                            savedCity.IsActive
                        }
                    });
                }

                return Ok(new
                {
                    Status = true,
                    Message = "City saved successfully.",
                    Data = new
                    {
                        city.CityId,
                        city.CompanyId,
                        city.CityName,
                        city.IsActive
                    }
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save city for company {CompanyId}",
                    request.CompanyId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to save city."
                });
            }
        }
        [HttpPost("GetCityList")]
        public async Task<IActionResult> GetCityList(
    [FromBody] GetCityListRequest? request)
        {
            if (request == null || request.CompanyId <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Valid CompanyId is required."
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

                var cities = await _context.Cities
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId)
                    .OrderByDescending(x => x.CityId)
                    .Select(x => new
                    {
                        x.CityId,
                        x.CityName,
                        x.CompanyId,
                        CompanyName = company.CompanyName,
                        x.IsActive,
                        x.CreatedOn,
                        x.CreatedBy,
                        x.ModifiedOn,
                        x.ModifiedBy
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = cities.Count > 0
                        ? "City list fetched successfully."
                        : "No cities found.",
                    Data = cities
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch cities for company {CompanyId}",
                    request.CompanyId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch city list."
                });
            }
        }
        public class SaveCityRequest
        {
            public int CompanyId { get; set; }
            public string? CityName { get; set; }
        }
        public class GetCityListRequest
        {
            public int CompanyId { get; set; }
        }
    }
}
