using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.Data;
using System.Text.Json;

namespace Recruitment.Controllers
{
    [Route("api/CandidateOfferLetterController")]
    [ApiController]
    public class CandidateOfferLetterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public CandidateOfferLetterController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }



        private IActionResult Fail(int code, string message)
        {
            return StatusCode(code, new
            {
                Status = false,
                Message = message
            });
        }

        // Shared validation for all six endpoints.
        private async Task<IActionResult?> ValidateAccess(
            AppointmentReferenceRequest? request)
        {
            if (request == null ||
                request.CompanyId <= 0 ||
                request.UserId <= 0 ||
                request.CandidateId <= 0 ||
                request.JobDescriptionId <= 0 ||
                request.CandidateApplicationId <= 0)
            {
                return Fail(
                    400,
                    "Valid CompanyId, UserId, CandidateId, " +
                    "JobDescriptionId and CandidateApplicationId are required.");
            }

            var user = await _context.EmployeeMasters
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == request.CompanyId &&
                    x.EmployeeId == request.UserId)
                .Select(x => new
                {
                    x.IsAdmin,
                    x.IsActive
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return Fail(404, "Employee not found for this company.");

            if (user.IsActive != true)
                return Fail(403, "Employee is inactive.");

            var companyExists = await _context.Companys
                .AsNoTracking()
                .AnyAsync(x => x.CompanyID == request.CompanyId);

            if (!companyExists)
                return Fail(404, "Company not found.");

            var candidateExists = await _context.Candidates
                .AsNoTracking()
                .AnyAsync(x =>
                    x.CompanyId == request.CompanyId &&
                    x.CandidateId == request.CandidateId);

            if (!candidateExists)
                return Fail(404, "Candidate not found for this company.");

            var applicationExists = await _context.CandidateApplications
                .AsNoTracking()
                .AnyAsync(x =>
                    x.CompanyId == request.CompanyId &&
                    x.CandidateApplicationId ==
                        request.CandidateApplicationId &&
                    x.CandidateId == request.CandidateId &&
                    x.JobDescriptionId == request.JobDescriptionId);

            if (!applicationExists)
            {
                return Fail(
                    404,
                    "Application not found for this candidate and job.");
            }

            var job = await _context.JobDescriptions
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == request.CompanyId &&
                    x.JobDescriptionId == request.JobDescriptionId)
                .Select(x => new
                {
                    x.TemplateId
                })
                .FirstOrDefaultAsync();

            if (job == null)
                return Fail(404, "Job not found for this company.");

            if (user.IsAdmin == true)
                return null;

            var teamIds = await (
                from member in _context.OnboardingTeamMembers.AsNoTracking()
                join team in _context.OnboardingTeams.AsNoTracking()
                    on new
                    {
                        member.CompanyId,
                        member.OnboardingTeamId
                    }
                    equals new
                    {
                        team.CompanyId,
                        team.OnboardingTeamId
                    }
                where member.CompanyId == request.CompanyId &&
                      member.EmployeeId == request.UserId &&
                      member.IsActive == true &&
                      team.IsActive == true
                select team.OnboardingTeamId
            ).Distinct().ToListAsync();

            if (teamIds.Count == 0)
            {
                return Fail(
                    403,
                    "You are not assigned to an active onboarding team.");
            }

            var scopes = await _context.OnboardingTeamTemplates
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == request.CompanyId &&
                    teamIds.Contains(x.OnboardingTeamId))
                .Select(x => new
                {
                    x.OnboardingTeamId,
                    x.TemplateId
                })
                .ToListAsync();

            bool allowed = teamIds.Any(teamId =>
                !scopes.Any(x => x.OnboardingTeamId == teamId) ||
                scopes.Any(x =>
                    x.OnboardingTeamId == teamId &&
                    x.TemplateId == job.TemplateId));

            if (!allowed)
            {
                return Fail(
                    403,
                    "Your onboarding team does not have access to this template.");
            }

            return null;
        }

        private IQueryable<CandidateAppointmentLetter> FindRecord(
            AppointmentReferenceRequest request)
        {
            return _context.CandidateAppointmentLetters.Where(x =>
                x.CompanyId == request.CompanyId &&
                x.CandidateId == request.CandidateId &&
                x.JobDescriptionId == request.JobDescriptionId &&
                x.CandidateApplicationId ==
                    request.CandidateApplicationId);
        }

        // Shared transaction/error handling.
        private async Task<IActionResult> Execute(
            AppointmentReferenceRequest? request,
            bool write,
            Func<Task<IActionResult>> action)
        {
            try
            {
                if (write)
                {
                    await using var transaction =
                        await _context.Database.BeginTransactionAsync(
                            IsolationLevel.Serializable);

                    var failure = await ValidateAccess(request);

                    if (failure != null)
                        return failure;

                    var result = await action();

                    if (result is OkObjectResult)
                        await transaction.CommitAsync();

                    return result;
                }

                var accessFailure = await ValidateAccess(request);

                if (accessFailure != null)
                    return accessFailure;

                return await action();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail(
                    409,
                    "The record changed. Refresh and try again.");
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                return Fail(
                    409,
                    "An appointment record already exists for this application. Refresh and try again.");
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      sql.Number == 547)
            {
                return Fail(
                    409,
                    "A related record is unavailable or a database constraint was violated.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Appointment API failed for CompanyId {CompanyId}, " +
                    "CandidateApplicationId {CandidateApplicationId}.",
                    request?.CompanyId,
                    request?.CandidateApplicationId);

                return Fail(
                    500,
                    "Unable to process the appointment request.");
            }
        }

        private static object LetterData(
            CandidateAppointmentLetter row)
        {
            return new
            {
                row.CandidateAppointmentLetterId,
                row.CompanyId,
                row.CandidateId,
                row.JobDescriptionId,
                row.CandidateApplicationId,
                row.HasAppointmentLetter,
                row.OfferLetterFilePath,
                row.ExpectedJoiningDate,

                DetailsJson = string.IsNullOrWhiteSpace(row.DetailsJson)
                    ? (JsonElement?)null
                    : JsonSerializer.Deserialize<JsonElement>(
                        row.DetailsJson),

                row.IsIssued,
                row.IssuedBy,
                row.IssuedOn,
                row.CreatedBy,
                row.CreatedOn,
                row.ModifiedBy,
                row.ModifiedOn
            };
        }

        private static object DecisionData(
            CandidateAppointmentLetter row)
        {
            return new
            {
                row.CandidateAppointmentLetterId,
                row.CompanyId,
                row.CandidateId,
                row.JobDescriptionId,
                row.CandidateApplicationId,
                row.ExpectedJoiningDate,
                row.HasDecision,
                row.ActualJoiningDate,
                row.FinalDecision,
                row.Remarks,

                CreatedBy = row.DecisionCreatedBy,
                CreatedOn = row.DecisionCreatedOn,
                ModifiedBy = row.DecisionModifiedBy,
                ModifiedOn = row.DecisionModifiedOn
            };
        }

        // 1. ADD / UPDATE APPOINTMENT LETTER
        [HttpPost("SaveAppointmentLetter")]
        public Task<IActionResult> SaveAppointmentLetter(
            [FromBody] SaveAppointmentLetterRequest? request)
        {
            return Execute(request, true, async () =>
            {
                var dto = request!;

                var filePath =
                    string.IsNullOrWhiteSpace(dto.OfferLetterFilePath)
                        ? null
                        : dto.OfferLetterFilePath.Trim();

                if (filePath?.Length > 1000)
                {
                    return Fail(
                        400,
                        "OfferLetterFilePath cannot exceed 1000 characters.");
                }

                if (dto.DetailsJson.HasValue &&
                    dto.DetailsJson.Value.ValueKind != JsonValueKind.Object &&
                    dto.DetailsJson.Value.ValueKind != JsonValueKind.Null)
                {
                    return Fail(
                        400,
                        "DetailsJson must be a JSON object or null.");
                }

                if (dto.IsIssued &&
                    (!dto.ExpectedJoiningDate.HasValue ||
                     string.IsNullOrWhiteSpace(filePath)))
                {
                    return Fail(
                        400,
                        "ExpectedJoiningDate and OfferLetterFilePath are required to issue the letter.");
                }

                var row = await FindRecord(dto).FirstOrDefaultAsync();

                bool isNewLetter =
                    row == null || !row.HasAppointmentLetter;

                var now = DateTime.UtcNow;

                if (row == null)
                {
                    row = new CandidateAppointmentLetter
                    {
                        CompanyId = dto.CompanyId,
                        CandidateId = dto.CandidateId,
                        JobDescriptionId = dto.JobDescriptionId,
                        CandidateApplicationId =
                            dto.CandidateApplicationId,
                        CreatedBy = dto.UserId,
                        CreatedOn = now
                    };

                    _context.CandidateAppointmentLetters.Add(row);
                }
                else
                {
                    if (row.HasDecision)
                    {
                        return Fail(
                            409,
                            "Remove the final decision before changing the appointment letter.");
                    }

                    row.ModifiedBy = dto.UserId;
                    row.ModifiedOn = now;
                }

                bool wasIssued = row.IsIssued;

                row.HasAppointmentLetter = true;
                row.OfferLetterFilePath = filePath;
                row.ExpectedJoiningDate =
                    dto.ExpectedJoiningDate?.Date;

                row.DetailsJson =
                    dto.DetailsJson.HasValue &&
                    dto.DetailsJson.Value.ValueKind == JsonValueKind.Object
                        ? dto.DetailsJson.Value.GetRawText()
                        : null;

                row.IsIssued = dto.IsIssued;

                if (dto.IsIssued && !wasIssued)
                {
                    row.IssuedBy = dto.UserId;
                    row.IssuedOn = now;
                }
                else if (!dto.IsIssued)
                {
                    row.IssuedBy = null;
                    row.IssuedOn = null;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Status = true,
                    Message = isNewLetter
                        ? "Appointment letter created successfully."
                        : "Appointment letter updated successfully.",
                    Data = LetterData(row)
                });
            });
        }

        // 2. GET APPOINTMENT LETTER
        [HttpPost("GetAppointmentLetter")]
        public Task<IActionResult> GetAppointmentLetter(
            [FromBody] AppointmentReferenceRequest? request)
        {
            return Execute(request, false, async () =>
            {
                var row = await FindRecord(request!)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (row == null || !row.HasAppointmentLetter)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "No appointment letter found.",
                        Data = (object?)null
                    });
                }

                return Ok(new
                {
                    Status = true,
                    Message = "Appointment letter fetched successfully.",
                    Data = LetterData(row)
                });
            });
        }

        // 3. DELETE APPOINTMENT LETTER SECTION
        [HttpPost("DeleteAppointmentLetter")]
        public Task<IActionResult> DeleteAppointmentLetter(
            [FromBody] AppointmentReferenceRequest? request)
        {
            return Execute(request, true, async () =>
            {
                var dto = request!;
                var row = await FindRecord(dto).FirstOrDefaultAsync();

                if (row == null || !row.HasAppointmentLetter)
                    return Fail(404, "Appointment letter not found.");

                if (row.HasDecision)
                {
                    return Fail(
                        409,
                        "Delete the candidate appointment decision first.");
                }

                row.HasAppointmentLetter = false;
                row.OfferLetterFilePath = null;
                row.ExpectedJoiningDate = null;
                row.DetailsJson = null;
                row.IsIssued = false;
                row.IssuedBy = null;
                row.IssuedOn = null;
                row.ModifiedBy = dto.UserId;
                row.ModifiedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Appointment letter deleted successfully.",
                    row.CandidateAppointmentLetterId
                });
            });
        }

        // 4. ADD / UPDATE FINAL DECISION
        [HttpPost("SaveCandidateAppointmentDecision")]
        public Task<IActionResult> SaveCandidateAppointmentDecision(
     [FromBody] SaveCandidateAppointmentDecisionRequest? request)
        {
            return Execute(request, true, async () =>
            {
                var dto = request!;

                string? decision =
                    dto.FinalDecision?.Trim().ToLowerInvariant() switch
                    {
                        "joined" or "candidate joined"
                            => "Joined",

                        "offerdeclined" or "offer declined by candidate"
                            => "OfferDeclined",

                        "rejected" or "candidate rejected by company"
                            => "Rejected",

                        _ => null
                    };

                if (decision == null)
                {
                    return Fail(
                        400,
                        "FinalDecision must be Candidate Joined, " +
                        "Offer Declined by Candidate, or Candidate Rejected by Company.");
                }

                var remarks = string.IsNullOrWhiteSpace(dto.Remarks)
                    ? null
                    : dto.Remarks.Trim();

                if (remarks?.Length > 4000)
                {
                    return Fail(
                        400,
                        "Remarks cannot exceed 4000 characters.");
                }

                if (decision == "Joined" &&
                    !dto.ActualJoiningDate.HasValue)
                {
                    return Fail(
                        400,
                        "ActualJoiningDate is required when the candidate has joined.");
                }

                if (decision != "Joined" &&
                    string.IsNullOrWhiteSpace(remarks))
                {
                    return Fail(
                        400,
                        "Remarks are required when the offer is declined or the candidate is rejected.");
                }

                // A joining date applies only when the candidate has joined.
                DateTime? actualJoiningDate = decision == "Joined"
                    ? dto.ActualJoiningDate?.Date
                    : null;

                var row = await FindRecord(dto).FirstOrDefaultAsync();

                var now = DateTime.UtcNow;

                // Decision can be saved independently of the appointment letter.
                if (row == null)
                {
                    row = new CandidateAppointmentLetter
                    {
                        CompanyId = dto.CompanyId,
                        CandidateId = dto.CandidateId,
                        JobDescriptionId = dto.JobDescriptionId,
                        CandidateApplicationId = dto.CandidateApplicationId,

                        HasAppointmentLetter = false,
                        IsIssued = false,

                        HasDecision = false,
                        FinalDecision = "Pending",

                        CreatedBy = dto.UserId,
                        CreatedOn = now
                    };

                    _context.CandidateAppointmentLetters.Add(row);
                }

                bool isNewDecision = !row.HasDecision;

                // Update only the decision fields; preserve letter details.
                row.HasDecision = true;
                row.FinalDecision = decision;
                row.ActualJoiningDate = actualJoiningDate;
                row.Remarks = remarks;

                if (isNewDecision)
                {
                    row.DecisionCreatedBy = dto.UserId;
                    row.DecisionCreatedOn = now;
                    row.DecisionModifiedBy = null;
                    row.DecisionModifiedOn = null;
                }
                else
                {
                    row.DecisionModifiedBy = dto.UserId;
                    row.DecisionModifiedOn = now;
                }

                row.ModifiedBy = dto.UserId;
                row.ModifiedOn = now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Status = true,
                    Message = isNewDecision
                        ? "Candidate appointment decision created successfully."
                        : "Candidate appointment decision updated successfully.",
                    Data = DecisionData(row)
                });
            });
        }

        // 5. GET FINAL DECISION
        [HttpPost("GetCandidateAppointmentDecision")]
        public Task<IActionResult> GetCandidateAppointmentDecision(
            [FromBody] AppointmentReferenceRequest? request)
        {
            return Execute(request, false, async () =>
            {
                var row = await FindRecord(request!)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (row == null || !row.HasDecision)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "No candidate appointment decision found.",
                        FinalDecision = "Pending",
                        ExpectedJoiningDate = row?.ExpectedJoiningDate,
                        Data = (object?)null
                    });
                }

                return Ok(new
                {
                    Status = true,
                    Message = "Candidate appointment decision fetched successfully.",
                    Data = DecisionData(row)
                });
            });
        }

        // 6. DELETE ONLY THE FINAL DECISION
        [HttpPost("DeleteCandidateAppointmentDecision")]
        public Task<IActionResult> DeleteCandidateAppointmentDecision(
            [FromBody] AppointmentReferenceRequest? request)
        {
            return Execute(request, true, async () =>
            {
                var dto = request!;
                var row = await FindRecord(dto).FirstOrDefaultAsync();

                if (row == null || !row.HasDecision)
                    return Fail(404, "Candidate appointment decision not found.");

                row.HasDecision = false;
                row.FinalDecision = "Pending";
                row.ActualJoiningDate = null;
                row.Remarks = null;

                row.DecisionCreatedBy = null;
                row.DecisionCreatedOn = null;
                row.DecisionModifiedBy = null;
                row.DecisionModifiedOn = null;

                row.ModifiedBy = dto.UserId;
                row.ModifiedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Candidate appointment decision deleted successfully.",
                    row.CandidateAppointmentLetterId,
                    row.FinalDecision
                });
            });
        }




public class AppointmentReferenceRequest
    {
        public int CompanyId { get; set; }

        public int UserId { get; set; }

        public int CandidateId { get; set; }

        public int JobDescriptionId { get; set; }

        public int CandidateApplicationId { get; set; }
    }

    public class SaveAppointmentLetterRequest : AppointmentReferenceRequest
    {
        public string? OfferLetterFilePath { get; set; }

        public DateTime? ExpectedJoiningDate { get; set; }

        // Send a JSON object, not an escaped JSON string.
        public JsonElement? DetailsJson { get; set; }

        public bool IsIssued { get; set; }
    }

    public class SaveCandidateAppointmentDecisionRequest
        : AppointmentReferenceRequest
    {
        public DateTime? ActualJoiningDate { get; set; }

        // Joined / NotJoined / Rejected
        public string? FinalDecision { get; set; }

        public string? Remarks { get; set; }
    }
}
}

    
