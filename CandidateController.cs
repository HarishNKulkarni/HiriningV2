using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.ComponentModel.DataAnnotations;

namespace Recruitment.Controllers
{
    [Route("api/CandidateController")]
    [ApiController]
    public class CandidateController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public CandidateController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }









        
        [HttpPost("SaveCandidates")]
        public async Task<IActionResult> SaveCandidates(
    [FromBody] SaveCandidatesRequest? request)
        {
            IActionResult Fail(int statusCode, string message)
            {
                return StatusCode(statusCode, new
                {
                    Status = false,
                    Message = message
                });
            }

            // 0 means not selected for these optional references.
            int? OptionalId(int? value)
            {
                return value == 0 ? null : value;
            }

            string NormalizeCurrency(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return "INR";

                var currency = value.Trim();

                // INR(₹) or INR (₹) becomes INR.
                var bracketIndex = currency.IndexOf('(');

                if (bracketIndex >= 0)
                {
                    currency = currency.Substring(0, bracketIndex).Trim();
                }

                return string.IsNullOrWhiteSpace(currency)
                    ? "INR"
                    : currency.ToUpperInvariant();
            }

            if (request == null ||
                request.Candidates == null ||
                request.Candidates.Count == 0 ||
                request.Candidates.Any(x => x == null))
            {
                return Fail(400, "Please provide candidate details.");
            }

            if (request.JobDescriptionId <= 0)
            {
                return Fail(400, "Valid JobDescriptionId is required.");
            }

            var submittedMobileNumbers = new HashSet<string>();

            foreach (var row in request.Candidates)
            {
                if (string.IsNullOrWhiteSpace(row.MobileNumber))
                {
                    return Fail(400, "Candidate mobile number is required.");
                }

                row.MobileNumber = row.MobileNumber.Trim();

                if (!submittedMobileNumbers.Add(row.MobileNumber))
                {
                    return Fail(
                        409,
                        $"Mobile number {row.MobileNumber} appears more than once for this JD.");
                }
            }

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var job = await _context.JobDescriptions
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.JobDescriptionId == request.JobDescriptionId)
                    .Select(x => new
                    {
                        x.JobDescriptionId,
                        x.JobTitle
                    })
                    .FirstOrDefaultAsync();

                if (job == null)
                {
                    return Fail(
                        404,
                        "Job description not found for this company.");
                }

                var savedApplicationIds = new List<int>();

                foreach (var row in request.Candidates)
                {
                    var now = DateTime.UtcNow;

                    bool isNewCandidate = row.CandidateId == 0;
                    bool isNewApplication = row.CandidateApplicationId == 0;

                    Candidate candidate;

                    if (isNewCandidate)
                    {
                        if (!isNewApplication)
                        {
                            return Fail(
                                404,
                                "Candidate application not found for the supplied candidate.");
                        }

                        candidate = new Candidate
                        {
                            CompanyId = request.CompanyId,
                            CreatedOn = now,
                            CreatedBy = request.UserId
                        };
                    }
                    else
                    {
                        var existingCandidate = await _context.Candidates
                            .FirstOrDefaultAsync(x =>
                                x.CompanyId == request.CompanyId &&
                                x.CandidateId == row.CandidateId);

                        if (existingCandidate == null)
                        {
                            return Fail(
                                404,
                                $"Candidate {row.CandidateId} not found for this company.");
                        }

                        candidate = existingCandidate;
                    }

                    CandidateApplication application;

                    if (isNewApplication)
                    {
                        application = new CandidateApplication
                        {
                            CompanyId = request.CompanyId,
                            JobDescriptionId = request.JobDescriptionId,
                            AppliedPosition = job.JobTitle,
                            Stage = "Screening",
                            Status = "Pending",
                            AppliedOn = now,
                            CreatedOn = now,
                            CreatedBy = request.UserId
                        };
                    }
                    else
                    {
                        var existingApplication =
                            await _context.CandidateApplications
                                .FirstOrDefaultAsync(x =>
                                    x.CompanyId == request.CompanyId &&
                                    x.CandidateApplicationId ==
                                        row.CandidateApplicationId &&
                                    x.CandidateId == row.CandidateId &&
                                    x.JobDescriptionId ==
                                        request.JobDescriptionId);

                        if (existingApplication == null)
                        {
                            return Fail(
                                404,
                                $"Application {row.CandidateApplicationId} not found for this candidate and JD.");
                        }

                        application = existingApplication;
                    }

                    // Same company + JD + mobile is not allowed.
                    // Excludes the application currently being edited.
                    // Includes inactive applications.
                    bool duplicateMobile = await (
                        from existingApplication in
                            _context.CandidateApplications
                        join existingCandidate in _context.Candidates
                            on new
                            {
                                existingApplication.CompanyId,
                                existingApplication.CandidateId
                            }
                            equals new
                            {
                                existingCandidate.CompanyId,
                                existingCandidate.CandidateId
                            }
                        where
                            existingApplication.CompanyId ==
                                request.CompanyId &&
                            existingApplication.JobDescriptionId ==
                                request.JobDescriptionId &&
                            existingApplication.CandidateApplicationId !=
                                row.CandidateApplicationId &&
                            existingCandidate.MobileNumber.Trim() ==
                                row.MobileNumber
                        select existingApplication.CandidateApplicationId
                    ).AnyAsync();

                    if (duplicateMobile)
                    {
                        return Fail(
                            409,
                            $"Mobile number {row.MobileNumber} is already registered for this JD.");
                    }

                    // Changing a candidate's mobile affects their other applications.
                    if (!isNewCandidate &&
                        candidate.MobileNumber != row.MobileNumber)
                    {
                        var candidateJobIds = _context.CandidateApplications
                            .Where(x =>
                                x.CompanyId == request.CompanyId &&
                                x.CandidateId == candidate.CandidateId)
                            .Select(x => x.JobDescriptionId);

                        bool duplicateOnAnotherJob = await (
                            from otherApplication in
                                _context.CandidateApplications
                            join otherCandidate in _context.Candidates
                                on new
                                {
                                    otherApplication.CompanyId,
                                    otherApplication.CandidateId
                                }
                                equals new
                                {
                                    otherCandidate.CompanyId,
                                    otherCandidate.CandidateId
                                }
                            where
                                otherApplication.CompanyId ==
                                    request.CompanyId &&
                                otherCandidate.CandidateId !=
                                    candidate.CandidateId &&
                                candidateJobIds.Contains(
                                    otherApplication.JobDescriptionId) &&
                                otherCandidate.MobileNumber.Trim() ==
                                    row.MobileNumber
                            select otherApplication.CandidateApplicationId
                        ).AnyAsync();

                        if (duplicateOnAnotherJob)
                        {
                            return Fail(
                                409,
                                "This mobile number is already registered for another JD belonging to this candidate.");
                        }
                    }

                    // Personal information.
                    candidate.FirstName =
                        row.FirstName?.Trim() ?? string.Empty;

                    candidate.MiddleName = row.MiddleName?.Trim();
                    candidate.LastName = row.LastName?.Trim();

                    candidate.Email = row.Email?.Trim();
                    candidate.AlternateEmail = row.AlternateEmail?.Trim();

                    candidate.MobileNumber = row.MobileNumber!;

                    candidate.AlternateMobileNumber =
                        row.AlternateMobileNumber?.Trim();

                    candidate.Gender = row.Gender?.Trim();
                    candidate.DateOfBirth = row.DateOfBirth;

                    candidate.CurrentAddress = row.CurrentAddress?.Trim();
                    candidate.PermanentAddress = row.PermanentAddress?.Trim();
                    candidate.CurrentCity = row.CurrentCity?.Trim();
                    candidate.State = row.State?.Trim();
                    candidate.Country = row.Country?.Trim();
                    candidate.Pincode = row.Pincode?.Trim();

                    // Professional information.
                    candidate.CurrentCompany = row.CurrentCompany?.Trim();

                    candidate.CurrentDesignation =
                        row.CurrentDesignation?.Trim();

                    candidate.EmploymentStatus =
                        row.EmploymentStatus?.Trim();

                    candidate.TotalExperienceYears =
                        row.TotalExperienceYears;

                    candidate.RelevantExperienceYears =
                        row.RelevantExperienceYears;

                    candidate.NoticePeriodDays = row.NoticePeriodDays;

                    candidate.PreviousCompany = row.PreviousCompany?.Trim();
                    candidate.Industry = row.Industry?.Trim();
                    candidate.FunctionalArea = row.FunctionalArea?.Trim();

                    candidate.HighestQualification =
                        row.HighestQualification?.Trim();

                    candidate.Specialization = row.Specialization?.Trim();
                    candidate.University = row.University?.Trim();
                    candidate.PassingYear = row.PassingYear;

                    candidate.IsActive = row.IsActive;

                    // Resume is stored in Candidate, as requested.
                    // Null/blank preserves the existing resume on edit.
                    if (!string.IsNullOrWhiteSpace(row.ResumePath))
                    {
                        candidate.ResumePath = row.ResumePath.Trim();
                    }

                    if (isNewCandidate)
                    {
                        _context.Candidates.Add(candidate);
                    }
                    else
                    {
                        candidate.ModifiedOn = now;
                        candidate.ModifiedBy = request.UserId;
                    }

                    // Generate CandidateId for a new candidate.
                    await _context.SaveChangesAsync();

                    // Application details.
                    application.CandidateId = candidate.CandidateId;

                    // Requires nullable entity properties and database columns.
                    application.BranchId = OptionalId(row.BranchId);
                    application.DepartmentId = OptionalId(row.DepartmentId);

                    // INR(₹) -> INR; null/blank -> INR.
                    application.Currency = NormalizeCurrency(row.Currency);

                    application.CurrentCTC = row.CurrentCTC;
                    application.CurrentFixedCTC = row.CurrentFixedCTC;
                    application.CurrentVariableCTC = row.CurrentVariableCTC;

                    application.ExpectedCTC = row.ExpectedCTC;
                    application.ExpectedFixedCTC = row.ExpectedFixedCTC;
                    application.ExpectedVariableCTC = row.ExpectedVariableCTC;

                    application.CurrentMonthlySalary =
                        row.CurrentMonthlySalary;

                    application.ExpectedMonthlySalary =
                        row.ExpectedMonthlySalary;

                    application.OfferedCTC = row.OfferedCTC;
                    application.IsNegotiable = row.IsNegotiable;

                    application.Source = row.Source?.Trim();
                    application.ReferredBy = row.ReferredBy?.Trim();

                    application.RecruiterEmployeeId =
                        OptionalId(row.RecruiterEmployeeId);

                    application.HiringManagerEmployeeId =
                        OptionalId(row.HiringManagerEmployeeId);

                    application.RecruitmentCityId =
                        OptionalId(row.RecruitmentCityId);

                    application.Cadre = row.Cadre?.Trim();

                    application.InterviewRequired = row.InterviewRequired;

                    application.PreferredInterviewDate =
                        row.PreferredInterviewDate;

                    application.PreferredLocation =
                        row.PreferredLocation?.Trim();

                    application.WillingToRelocate =
                        row.WillingToRelocate;

                    application.CandidateAvailability =
                        row.CandidateAvailability?.Trim();

                    application.Remarks = row.Remarks?.Trim();

                    // Your pasted code did not save VerificationRemarks.
                    // Enable only if the entity property and DB column exist:
                    // application.VerificationRemarks =
                    //     row.VerificationRemarks?.Trim();

                    application.IsActive = row.IsActive;

                    // Existing Stage and Status remain unchanged on edit.
                    if (isNewApplication)
                    {
                        _context.CandidateApplications.Add(application);
                    }
                    else
                    {
                        application.ModifiedOn = now;
                        application.ModifiedBy = request.UserId;
                    }

                    // Generate CandidateApplicationId for a new application.
                    await _context.SaveChangesAsync();

                    // Save document references.
                    // Null/empty list leaves existing documents unchanged.
                    if (row.Documents != null)
                    {
                        foreach (var documentRequest in row.Documents)
                        {
                            CandidateDocument document;

                            if (documentRequest.CandidateDocumentId == 0)
                            {
                                document = new CandidateDocument
                                {
                                    CompanyId = request.CompanyId,
                                    CandidateApplicationId =
                                        application.CandidateApplicationId,
                                    CreatedOn = now,
                                    CreatedBy = request.UserId
                                };

                                _context.CandidateDocuments.Add(document);
                            }
                            else
                            {
                                var existingDocument =
                                    await _context.CandidateDocuments
                                        .FirstOrDefaultAsync(x =>
                                            x.CompanyId == request.CompanyId &&
                                            x.CandidateApplicationId ==
                                                application.CandidateApplicationId &&
                                            x.CandidateDocumentId ==
                                                documentRequest.CandidateDocumentId);

                                if (existingDocument == null)
                                {
                                    return Fail(
                                        404,
                                        $"Document {documentRequest.CandidateDocumentId} not found for this candidate application.");
                                }

                                document = existingDocument;
                                document.ModifiedOn = now;
                                document.ModifiedBy = request.UserId;
                            }

                            document.DocumentType =
                                documentRequest.DocumentType?.Trim()
                                ?? string.Empty;

                            document.OriginalFileName =
                                documentRequest.OriginalFileName?.Trim()
                                ?? string.Empty;

                            document.StorageKey =
                                documentRequest.FilePath?.Trim()
                                ?? string.Empty;

                            document.ContentType =
                                documentRequest.ContentType?.Trim();

                            document.FileSizeBytes =
                                documentRequest.FileSizeBytes ?? 0;
                        }
                    }

                    await _context.SaveChangesAsync();

                    savedApplicationIds.Add(
                        application.CandidateApplicationId);
                }

                var savedApplications = await (
                    from application in
                        _context.CandidateApplications.AsNoTracking()
                    join candidate in _context.Candidates.AsNoTracking()
                        on new
                        {
                            application.CompanyId,
                            application.CandidateId
                        }
                        equals new
                        {
                            candidate.CompanyId,
                            candidate.CandidateId
                        }
                    where
                        application.CompanyId == request.CompanyId &&
                        savedApplicationIds.Contains(
                            application.CandidateApplicationId)
                    orderby application.CandidateApplicationId descending
                    select new
                    {
                        candidate.CandidateId,

                        CandidateName =
                            ((candidate.FirstName ?? "") + " " +
                             (candidate.MiddleName ?? "") + " " +
                             (candidate.LastName ?? "")).Trim(),

                        candidate.MobileNumber,
                        candidate.ResumePath,

                        application.CandidateApplicationId,
                        application.CompanyId,

                        CompanyName = _context.Companys
                            .Where(x =>
                                x.CompanyID == application.CompanyId)
                            .Select(x => x.CompanyName)
                            .FirstOrDefault(),

                        application.JobDescriptionId,
                        JobTitle = job.JobTitle,
                        application.AppliedPosition,

                        application.BranchId,

                        BranchName = _context.Branches
                            .Where(x =>
                                x.CompanyId == application.CompanyId &&
                                x.BranchId == application.BranchId)
                            .Select(x => x.BranchName)
                            .FirstOrDefault(),

                        application.DepartmentId,

                        DepartmentName = _context.Departments
                            .Where(x =>
                                x.CompanyId == application.CompanyId &&
                                x.DepartmentId == application.DepartmentId)
                            .Select(x => x.DepartmentName)
                            .FirstOrDefault(),

                        application.Stage,
                        application.Status,
                        application.IsActive,

                        application.CreatedOn,
                        application.CreatedBy,

                        CreatedByName = _context.EmployeeMasters
                            .Where(x =>
                                x.CompanyId == application.CompanyId &&
                                x.EmployeeId == application.CreatedBy)
                            .Select(x =>
                                ((x.FirstName ?? "") + " " +
                                 (x.LastName ?? "")).Trim())
                            .FirstOrDefault(),

                        application.ModifiedOn,
                        application.ModifiedBy,

                        ModifiedByName = _context.EmployeeMasters
                            .Where(x =>
                                x.CompanyId == application.CompanyId &&
                                x.EmployeeId == application.ModifiedBy)
                            .Select(x =>
                                ((x.FirstName ?? "") + " " +
                                 (x.LastName ?? "")).Trim())
                            .FirstOrDefault()
                    }
                ).ToListAsync();

                var savedDocuments = await _context.CandidateDocuments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        savedApplicationIds.Contains(
                            x.CandidateApplicationId))
                    .OrderBy(x => x.CandidateDocumentId)
                    .Select(x => new
                    {
                        x.CandidateDocumentId,
                        x.CandidateApplicationId,
                        x.DocumentType,
                        x.OriginalFileName,
                        FilePath = x.StorageKey,
                        x.ContentType,
                        x.FileSizeBytes,
                        x.IsActive
                    })
                    .ToListAsync();

                var documentsByApplication = savedDocuments
                    .ToLookup(x => x.CandidateApplicationId);

                var data = savedApplications.Select(x => new
                {
                    x.CandidateId,
                    x.CandidateName,
                    x.MobileNumber,
                    x.ResumePath,
                    x.CandidateApplicationId,
                    x.CompanyId,
                    x.CompanyName,
                    x.JobDescriptionId,
                    x.JobTitle,
                    x.AppliedPosition,
                    x.BranchId,
                    x.BranchName,
                    x.DepartmentId,
                    x.DepartmentName,
                    x.Stage,
                    x.Status,
                    x.IsActive,
                    x.CreatedOn,
                    x.CreatedBy,
                    x.CreatedByName,
                    x.ModifiedOn,
                    x.ModifiedBy,
                    x.ModifiedByName,

                    Documents = documentsByApplication[
                        x.CandidateApplicationId].ToList()
                }).ToList();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Candidates saved successfully.",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save candidates for CompanyId {CompanyId}, JobDescriptionId {JobDescriptionId}.",
                    request.CompanyId,
                    request.JobDescriptionId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to save candidates."
                });
            }
        }



        [HttpPost("GetCandidateList")]
        public async Task<IActionResult> GetCandidateList(
[FromBody] GetCandidateListRequest? request)
        {
            if (request == null || request.CompanyId <= 0 || request.User <= 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Valid CompanyId and User are required."
                });
            }

            if (request.JobDescriptionIds != null && request.JobDescriptionIds.Any(id => id < 0))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "JobDescriptionIds cannot contain negative IDs."
                });
            }

            // Existing filter behavior:
            // null, [], or any list containing 0 means all.
            static int[] GetFilterIds(List<int>? values)
            {
                if (values == null ||
                    values.Count == 0 ||
                    values.Contains(0))
                {
                    return Array.Empty<int>();
                }

                return values.Distinct().ToArray();
            }

            try
            {
                // Determine access from the employee record, not the request.
                var accessUser = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.User)
                    .Select(x => new { x.EmployeeId, x.IsAdmin, x.IsActive })
                    .FirstOrDefaultAsync();

                if (accessUser == null)
                {
                    return NotFound(new
                    {
                        Status = false,
                        Message = "User not found for this company."
                    });
                }

                if (accessUser.IsActive != true)
                {
                    return StatusCode(403, new
                    {
                        Status = false,
                        Message = "User is inactive."
                    });
                }

                var branchIds = GetFilterIds(request.BranchIds);
                var departmentIds = GetFilterIds(request.DepartmentIds);
                var jobIds = GetFilterIds(request.JobDescriptionIds);

                var query =
                    from application in _context.CandidateApplications
                        .AsNoTracking()

                    join candidate in _context.Candidates.AsNoTracking()
                        on new
                        {
                            application.CompanyId,
                            application.CandidateId
                        }
                        equals new
                        {
                            candidate.CompanyId,
                            candidate.CandidateId
                        }

                    where application.CompanyId == request.CompanyId

                    select new
                    {
                        Application = application,
                        Candidate = candidate
                    };

                if (branchIds.Length > 0)
                {
                    query = query.Where(x =>
                        x.Application.BranchId.HasValue &&
                        branchIds.Contains(x.Application.BranchId.Value));
                }

                if (departmentIds.Length > 0)
                {
                    query = query.Where(x =>
                        x.Application.DepartmentId.HasValue &&
                        departmentIds.Contains(
                            x.Application.DepartmentId.Value));
                }

                // null, [], or a list containing 0 means all jobs.
                if (jobIds.Length > 0)
                {
                    query = query.Where(x =>
                        jobIds.Contains(x.Application.JobDescriptionId));
                }

                // Admins retain the existing company-wide list.
                // Non-admins see only jobs mapped to their assigned templates.
                if (accessUser.IsAdmin != true)
                {
                    var allowedJobIds =
                        from job in _context.JobDescriptions.AsNoTracking()
                        join assignment in _context.RoleTemplateAssignment.AsNoTracking()
                            on new { job.CompanyId, TemplateId = (int?)job.TemplateId }
                            equals new { assignment.CompanyId, TemplateId = (int?)assignment.TemplateId }
                        join template in _context.RoleTemplates.AsNoTracking()
                            on new { assignment.CompanyId, assignment.TemplateId }
                            equals new { template.CompanyId, template.TemplateId }
                        where job.CompanyId == request.CompanyId &&
                              assignment.EmployeeId == accessUser.EmployeeId &&
                              assignment.IsActive == true &&
                              template.IsActive == true
                        select job.JobDescriptionId;

                    query = query.Where(x =>
                        allowedJobIds.Contains(x.Application.JobDescriptionId));
                }

                var rows = await query
                    .OrderByDescending(x =>
                        x.Application.CandidateApplicationId)
                    .ToListAsync();

                if (rows.Count == 0)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "No candidates found.",
                        TotalCount = 0,
                        Data = Array.Empty<object>()
                    });
                }

                var resultApplicationIds = rows
                    .Select(x => x.Application.CandidateApplicationId)
                    .Distinct()
                    .ToArray();

                var resultJobIds = rows
                    .Select(x => x.Application.JobDescriptionId)
                    .Distinct()
                    .ToArray();

                var resultBranchIds = rows
                    .Where(x => x.Application.BranchId.HasValue)
                    .Select(x => x.Application.BranchId!.Value)
                    .Distinct()
                    .ToArray();

                var resultDepartmentIds = rows
                    .Where(x => x.Application.DepartmentId.HasValue)
                    .Select(x => x.Application.DepartmentId!.Value)
                    .Distinct()
                    .ToArray();

                var resultCityIds = rows
                    .Where(x => x.Application.RecruitmentCityId.HasValue)
                    .Select(x => x.Application.RecruitmentCityId!.Value)
                    .Distinct()
                    .ToArray();

                var companyName = await _context.Companys
                    .AsNoTracking()
                    .Where(x => x.CompanyID == request.CompanyId)
                    .Select(x => x.CompanyName)
                    .FirstOrDefaultAsync();

                // TemplateId is additionally loaded for the approval path.
                var jobRows = await _context.JobDescriptions
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultJobIds.Contains(x.JobDescriptionId))
                    .Select(x => new
                    {
                        x.JobDescriptionId,
                        x.JobTitle,
                        TemplateId = (int?)x.TemplateId
                    })
                    .ToListAsync();

                var jobs = jobRows.ToDictionary(
                    x => x.JobDescriptionId);

                var branches = await _context.Branches
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultBranchIds.Contains(x.BranchId))
                    .Select(x => new
                    {
                        x.BranchId,
                        x.BranchName
                    })
                    .ToDictionaryAsync(
                        x => x.BranchId,
                        x => x.BranchName);

                var departments = await _context.Departments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultDepartmentIds.Contains(x.DepartmentId))
                    .Select(x => new
                    {
                        x.DepartmentId,
                        x.DepartmentName
                    })
                    .ToDictionaryAsync(
                        x => x.DepartmentId,
                        x => x.DepartmentName);

                var cities = await _context.Cities
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultCityIds.Contains(x.CityId))
                    .Select(x => new
                    {
                        x.CityId,
                        x.CityName
                    })
                    .ToDictionaryAsync(
                        x => x.CityId,
                        x => x.CityName);

                var documents = await _context.CandidateDocuments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultApplicationIds.Contains(
                            x.CandidateApplicationId))
                    .OrderByDescending(x => x.CandidateDocumentId)
                    .ToListAsync();

                // -------------------------------------------------------
                // Additional data for approval path and current-user tasks.
                // Existing candidate filtering is unchanged.
                // -------------------------------------------------------

                var templateIds = jobRows
                    .Where(x =>
                        x.TemplateId.HasValue &&
                        x.TemplateId.Value > 0)
                    .Select(x => x.TemplateId!.Value)
                    .Distinct()
                    .ToArray();

                // Include all saved history:
                // saves, remarks, approvals, rejections and returns.
                // Do not exclude IsRemarkAdded records.
                var histories = await _context
                    .CandidateSkillAssessmentHistories
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        resultApplicationIds.Contains(
                            x.CandidateApplicationId) &&
                        resultJobIds.Contains(x.JobDescriptionId))
                    .OrderBy(x => x.CreatedOn)
                    .ThenBy(x => x.CandidateSkillAssessmentHistoryId)
                    .ToListAsync();

                var historyAssignmentIds = histories
                    .Where(x => x.AssignmentId.HasValue)
                    .Select(x => x.AssignmentId!.Value)
                    .Distinct()
                    .ToArray();

                // Include inactive assignments so old history still resolves.
                // Also load historical assignments even if a JD's template changed.
                var assignmentRows = await _context.RoleTemplateAssignment
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        (
                            templateIds.Contains(x.TemplateId) ||
                            historyAssignmentIds.Contains(x.AssignmentId)
                        ))
                    .Select(x => new
                    {
                        x.AssignmentId,
                        x.TemplateId,
                        x.EmployeeId,
                        x.RoleId,
                        x.HierarchyLevel,
                        x.Stage,
                        x.ReportingToAssignmentId,
                        x.CanApprove,
                        x.CanReject,
                        x.CanReturn,
                        x.IsActive
                    })
                    .ToListAsync();

                // Configured permissions for the requested employee only.
                // Keep historical ApprovalPath permissions unchanged.
                var permissionUserActive = request.User > 0 &&
                    await _context.EmployeeMasters.AsNoTracking().AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.User &&
                        x.IsActive == true);

                var activePermissionTemplateIds = (await _context.RoleTemplates
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        templateIds.Contains(x.TemplateId) &&
                        x.IsActive == true)
                    .Select(x => x.TemplateId)
                    .ToListAsync()).ToHashSet();

                var assignmentsById = assignmentRows.ToDictionary(
                    x => x.AssignmentId);

                var roleIds = assignmentRows
                    .Select(x => x.RoleId)
                    .Distinct()
                    .ToArray();

                var roleNames = await _context.Roles
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        roleIds.Contains(x.RoleId))
                    .Select(x => new
                    {
                        x.RoleId,
                        x.RoleName
                    })
                    .ToDictionaryAsync(
                        x => x.RoleId,
                        x => x.RoleName);

                var historySkillIds = histories
                    .Where(x => x.CompanySkillMasterId > 0)
                    .Select(x => x.CompanySkillMasterId)
                    .Distinct()
                    .ToArray();

                // Include inactive skills to retain names for existing history.
                var skillRows = await _context.CompanySkillMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        historySkillIds.Contains(
                            x.CompanySkillMasterId))
                    .Select(x => new
                    {
                        x.CompanySkillMasterId,
                        x.SkillName
                    })
                    .ToListAsync();

                var skillNames = skillRows.ToDictionary(
                    x => x.CompanySkillMasterId,
                    x => x.SkillName);

                // User = 0 intentionally returns no personal tasks.
                // Reading this API does not mark notifications read or actioned.
                var pendingNotifications = await _context
                    .RecruitmentNotifications
                    .AsNoTracking()
                    .Where(x =>
                        request.User > 0 &&
                        x.CompanyId == request.CompanyId &&
                        x.ToUserId == request.User &&
                        resultApplicationIds.Contains(
                            x.CandidateApplicationId) &&
                        resultJobIds.Contains(x.JobDescriptionId) &&
                        x.IsActionRequest == true &&
                        x.ActionedByHistoryId == null)
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x => x.NotificationId)
                    .ToListAsync();

                // Includes existing audit users and approval-path participants.
                var employeeIds = rows
                    .SelectMany(x => new int?[]
                    {
            x.Application.RecruiterEmployeeId,
            x.Application.HiringManagerEmployeeId,
            x.Application.CreatedBy,
            x.Application.ModifiedBy,
            x.Candidate.CreatedBy,
            x.Candidate.ModifiedBy
                    })
                    .Concat(documents.SelectMany(x => new int?[]
                    {
            x.CreatedBy,
            x.ModifiedBy
                    }))
                    .Concat(histories.SelectMany(x => new int?[]
                    {
            x.CreatedBy,
            x.ModifiedBy
                    }))
                    .Concat(assignmentRows.Select(x =>
                        (int?)x.EmployeeId))
                    .Concat(pendingNotifications.SelectMany(x => new int?[]
                    {
            x.FromUserId,
            x.ToUserId
                    }))
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToArray();

                var employeeNames = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        employeeIds.Contains(x.EmployeeId))
                    .Select(x => new
                    {
                        x.EmployeeId,

                        EmployeeName =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim()
                    })
                    .ToDictionaryAsync(
                        x => x.EmployeeId,
                        x => x.EmployeeName);

                var adminEmployeeIds = (await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                        employeeIds.Contains(x.EmployeeId) && x.IsAdmin == true)
                    .Select(x => (int?)x.EmployeeId)
                    .ToListAsync()).ToHashSet();

                string? GetEmployeeName(int? employeeId)
                {
                    if (employeeId.HasValue &&
                        employeeNames.TryGetValue(
                            employeeId.Value, out var name))
                    {
                        return name;
                    }

                    return null;
                }

                string? GetRoleName(int? roleId)
                {
                    if (roleId.HasValue &&
                        roleNames.TryGetValue(roleId.Value, out var name))
                    {
                        return name;
                    }

                    return null;
                }

                string GetSkillName(int skillId)
                {
                    if (skillId == 0)
                        return "Default Remark";

                    return skillNames.TryGetValue(skillId, out var name) &&
                           !string.IsNullOrWhiteSpace(name)
                        ? name
                        : $"Skill {skillId}";
                }

                static bool IsDecision(string? action)
                {
                    return action == "Approve" ||
                           action == "Reject" ||
                           action == "Return";
                }

                var documentsByApplication = documents.ToLookup(
                    x => x.CandidateApplicationId);

                // Scope history to both application and JD.
                var historiesByApplication = histories.ToLookup(x => (
                    x.CandidateApplicationId,
                    x.JobDescriptionId
                ));

                var notificationsByApplication =
                    pendingNotifications.ToLookup(x => (
                        x.CandidateApplicationId,
                        x.JobDescriptionId
                    ));

                var data = rows.Select(row =>
                {
                    var candidate = row.Candidate;
                    var application = row.Application;

                    jobs.TryGetValue(
                        application.JobDescriptionId,
                        out var job);

                    string? branchName = null;

                    if (application.BranchId.HasValue)
                    {
                        branches.TryGetValue(
                            application.BranchId.Value,
                            out branchName);
                    }

                    string? departmentName = null;

                    if (application.DepartmentId.HasValue)
                    {
                        departments.TryGetValue(
                            application.DepartmentId.Value,
                            out departmentName);
                    }

                    string? recruitmentCityName = null;

                    if (application.RecruitmentCityId.HasValue)
                    {
                        cities.TryGetValue(
                            application.RecruitmentCityId.Value,
                            out recruitmentCityName);
                    }

                    // Permissions come from this employee's assignments in
                    // this job's current active template, not another role.
                    var myPermissions = assignmentRows
                        .Where(x =>
                            permissionUserActive &&
                            job != null &&
                            x.TemplateId == job.TemplateId &&
                            activePermissionTemplateIds.Contains(x.TemplateId) &&
                            x.EmployeeId == request.User &&
                            x.IsActive == true)
                        .OrderBy(x => x.HierarchyLevel)
                        .ThenBy(x => x.AssignmentId)
                        .Select(x => new
                        {
                            x.AssignmentId,
                            x.TemplateId,
                            x.EmployeeId,
                            x.RoleId,
                            RoleName = GetRoleName(x.RoleId),
                            x.HierarchyLevel,
                            x.Stage,
                            CanApprove = x.CanApprove == true,
                            CanReject = x.CanReject == true,
                            CanReturn = x.CanReturn == true
                        })
                        .ToList();

                    var applicationHistory = historiesByApplication[(
                        application.CandidateApplicationId,
                        application.JobDescriptionId
                    )].ToList();

                    // Current user's decision only. Another hierarchy's approval
                    // must not set this user's candidate status to Approved.
                    var ownStatusAssignmentIds = myPermissions
                        .Select(x => x.AssignmentId)
                        .ToHashSet();

                    var ownLatestDecision = applicationHistory
                        .Where(x =>
                            x.CreatedBy == request.User &&
                            x.AssignmentId.HasValue &&
                            ownStatusAssignmentIds.Contains(x.AssignmentId.Value) &&
                            IsDecision(x.Action))
                        .OrderByDescending(x => x.CreatedOn)
                        .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
                        .FirstOrDefault();

                    string ownRoleStatus = ownLatestDecision?.Action switch
                    {
                        "Approve" => "Approved",
                        "Reject" => "Rejected",
                        "Return" => "Returned",
                        _ => "Pending"
                    };

                    var applicationNotifications =
                        notificationsByApplication[(
                            application.CandidateApplicationId,
                            application.JobDescriptionId
                        )].ToList();

                    var applicationHistoryAssignmentIds =
                        applicationHistory
                            .Where(x => x.AssignmentId.HasValue)
                            .Select(x => x.AssignmentId!.Value)
                            .ToHashSet();

                    // Show current active hierarchy assignments, including those
                    // without history, plus assignments with historical activity.
                    var pathAssignments = assignmentRows
                        .Where(x =>
                            (
                                job != null &&
                                x.TemplateId == job.TemplateId &&
                                x.IsActive == true
                            ) ||
                            applicationHistoryAssignmentIds.Contains(
                                x.AssignmentId))
                        .OrderBy(x => x.HierarchyLevel)
                        .ThenBy(x => x.AssignmentId)
                        .ToList();

                    // Resume is shown only on the first active assignment
                    // in the job's current template, not historical templates.
                    int? resumeAssignmentId = pathAssignments
                        .Where(x => job != null &&
                            x.TemplateId == job.TemplateId && x.IsActive == true)
                        .OrderBy(x => x.HierarchyLevel)
                        .ThenBy(x => x.AssignmentId)
                        .Select(x => (int?)x.AssignmentId)
                        .FirstOrDefault();

                    bool isResume =
                        permissionUserActive &&
                        job != null &&
                        job.TemplateId.HasValue &&
                        activePermissionTemplateIds.Contains(job.TemplateId.Value) &&
                        resumeAssignmentId.HasValue &&
                        assignmentRows.Any(x =>
                            x.AssignmentId == resumeAssignmentId.Value &&
                            x.EmployeeId == request.User);
                    object? resumeDetails = null;
                    string? resumeDetailsSource = null;
                    int? resumeHistoryId = null;

                    if (isResume)
                    {
                        var resumeHistory = applicationHistory
                            .Where(x =>
                                x.AssignmentId == resumeAssignmentId &&
                                x.CreatedBy == request.User &&
                                x.IsResume == true)
                            .OrderByDescending(x => x.CreatedOn)
                            .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
                            .FirstOrDefault();

                        if (!string.IsNullOrWhiteSpace(resumeHistory?.DetailsJson))
                        {
                            try
                            {
                                using var document = System.Text.Json.JsonDocument.Parse(
                                    resumeHistory!.DetailsJson);
                                var root = document.RootElement;
                                if (root.ValueKind == System.Text.Json.JsonValueKind.Object &&
                                    root.EnumerateObject().Any())
                                {
                                    // Keep the saved detail fields and add authoritative
                                    // identifiers from the history record, not its JSON.
                                    var detailsWithIds = new Dictionary<string, object?>(
                                        StringComparer.OrdinalIgnoreCase);

                                    foreach (var property in root.EnumerateObject())
                                    {
                                        if (string.Equals(property.Name, "CandidateSkillAssessmentId", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(property.Name, "CompanyId", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(property.Name, "JobDescriptionId", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(property.Name, "CandidateApplicationId", StringComparison.OrdinalIgnoreCase))
                                        {
                                            continue;
                                        }

                                        detailsWithIds[property.Name] = property.Value.Clone();
                                    }

                                    detailsWithIds["candidateSkillAssessmentId"] = resumeHistory.CandidateSkillAssessmentId;
                                    detailsWithIds["companyId"] = resumeHistory.CompanyId;
                                    detailsWithIds["jobDescriptionId"] = resumeHistory.JobDescriptionId;
                                    detailsWithIds["candidateApplicationId"] = resumeHistory.CandidateApplicationId;
                                    resumeDetails = detailsWithIds;
                                    resumeDetailsSource = "History";
                                    resumeHistoryId = resumeHistory.CandidateSkillAssessmentHistoryId;
                                }
                            }
                            catch (System.Text.Json.JsonException)
                            {
                                // An invalid historical JSON value must not
                                // prevent the candidate list from loading.
                            }
                        }

                        if (resumeDetails == null)
                        {
                            resumeDetailsSource = "Candidate";
                            resumeDetails = new
                            {
                                // Null when no saved resume record exists.
                                CandidateSkillAssessmentId = (int?)resumeHistory?.CandidateSkillAssessmentId,
                                application.CompanyId,
                                application.JobDescriptionId,
                                application.CandidateApplicationId,
                                candidate.CandidateId,
                                candidate.FirstName,
                                candidate.MiddleName,
                                candidate.LastName,
                                candidate.Email,
                                candidate.AlternateEmail,
                                candidate.MobileNumber,
                                candidate.AlternateMobileNumber,
                                candidate.Gender,
                                candidate.DateOfBirth,
                                candidate.CurrentAddress,
                                candidate.PermanentAddress,
                                candidate.CurrentCity,
                                candidate.State,
                                candidate.Country,
                                candidate.Pincode,
                                candidate.CurrentCompany,
                                candidate.CurrentDesignation,
                                candidate.EmploymentStatus,
                                candidate.TotalExperienceYears,
                                candidate.RelevantExperienceYears,
                                candidate.NoticePeriodDays,
                                candidate.PreviousCompany,
                                candidate.Industry,
                                candidate.FunctionalArea,
                                candidate.HighestQualification,
                                candidate.Specialization,
                                candidate.University,
                                candidate.PassingYear,
                                candidate.ResumePath
                            };
                        }
                    }

                    var approvalPath = pathAssignments.Select(assignment =>
                    {
                        var assignmentHistory = applicationHistory
                            .Where(x =>
                                x.AssignmentId == assignment.AssignmentId)
                            .OrderBy(x => x.CreatedOn)
                            .ThenBy(x =>
                                x.CandidateSkillAssessmentHistoryId)
                            .ToList();

                        var latestDecision = assignmentHistory
                            .Where(x => IsDecision(x.Action))
                            .OrderByDescending(x => x.CreatedOn)
                            .ThenByDescending(x =>
                                x.CandidateSkillAssessmentHistoryId)
                            .FirstOrDefault();

                        // Latest recorded decision, not completion of every skill.
                        string hierarchyStatus = latestDecision?.Action switch
                        {
                            "Approve" => "Approved",
                            "Reject" => "Rejected",
                            "Return" => "Returned",
                            _ => "Pending"
                        };

                        int? reportingEmployeeId = null;
                        string? reportingEmployeeName = null;
                        string? reportingRoleName = null;

                        if (assignment.ReportingToAssignmentId.HasValue &&
                            assignmentsById.TryGetValue(
                                assignment.ReportingToAssignmentId.Value,
                                out var reportingAssignment))
                        {
                            reportingEmployeeId =
                                reportingAssignment.EmployeeId;

                            reportingEmployeeName = GetEmployeeName(
                                reportingAssignment.EmployeeId);

                            reportingRoleName = GetRoleName(
                                reportingAssignment.RoleId);
                        }

                        return new
                        {
                            assignment.TemplateId,
                            assignment.AssignmentId,
                            assignment.HierarchyLevel,

                            HierarchyLabel =
                                $"H{assignment.HierarchyLevel}",

                            assignment.RoleId,
                            RoleName = GetRoleName(assignment.RoleId),

                            assignment.EmployeeId,
                            EmployeeName = GetEmployeeName(
                                assignment.EmployeeId),

                            assignment.Stage,
                            assignment.IsActive,
                            assignment.CanApprove,
                            assignment.CanReject,
                            assignment.CanReturn,

                            assignment.ReportingToAssignmentId,
                            ReportingToEmployeeId = reportingEmployeeId,
                            ReportingToEmployeeName = reportingEmployeeName,
                            ReportingToRoleName = reportingRoleName,

                            IsCurrentTemplate =
                                job != null &&
                                assignment.TemplateId == job.TemplateId,


                            HasActivity = assignmentHistory.Count > 0,
                            Status = hierarchyStatus,

                            // This is the last recorded decision, not an
                            // inferred completion status for all assigned skills.
                            LatestDecision = latestDecision?.Action,
                            LatestDecisionStatus = latestDecision?.NewStatus,
                            LatestDecisionOn = latestDecision?.CreatedOn,
                            LatestDecisionBy = latestDecision?.CreatedBy,
                            LatestDecisionByName = GetEmployeeName(
                                latestDecision?.CreatedBy),

                            HistoryCount = assignmentHistory.Count,

                            DecisionCount = assignmentHistory.Count(x =>
                                IsDecision(x.Action)),

                            RemarksCount = assignmentHistory.Count(x =>
                                x.IsRemarkAdded),

                            History = assignmentHistory.Select(history => new
                            {
                                HistoryId =
                                    history.CandidateSkillAssessmentHistoryId,

                                history.CandidateSkillAssessmentHistoryId,
                                history.CandidateSkillAssessmentId,
                                history.CandidateApplicationId,
                                history.JobDescriptionId,
                                history.AssignmentId,

                                history.CompanySkillMasterId,
                                SkillId = history.CompanySkillMasterId,

                                SkillName = GetSkillName(
                                    history.CompanySkillMasterId),

                                IsDefaultRemark =
                                    history.CompanySkillMasterId == 0,

                                // Default-remark activity can be displayed
                                // under the assignment's configured stage.
                                DisplayTitle =
                                    history.CompanySkillMasterId == 0
                                        ? string.IsNullOrWhiteSpace(
                                            assignment.Stage)
                                            ? "Default Remark"
                                            : $"Stage: {assignment.Stage}"
                                        : GetSkillName(
                                            history.CompanySkillMasterId),

                                Stage = assignment.Stage,

                                history.Action,
                                history.PreviousStatus,
                                history.NewStatus,
                                Status = history.NewStatus,
                                history.Score,
                                history.Remarks,
                                history.IsRemarkAdded,

                                // Use the actual event author.
                                EmployeeId = history.CreatedBy,
                                EmployeeName = GetEmployeeName(
                                    history.CreatedBy),

                                history.CreatedBy,
                                CreatedByName = GetEmployeeName(
                                    history.CreatedBy),
                                history.CreatedOn,

                                history.ModifiedBy,
                                ModifiedByName = GetEmployeeName(
                                    history.ModifiedBy),
                                history.ModifiedOn
                            }).ToList()
                        };
                    }).ToList();

                    // Append admins who actually added remarks for this application/job.
                    // Known historical assignments from other templates are excluded.
                    var adminHistory = applicationHistory
                        .Where(x =>
                            adminEmployeeIds.Contains(x.CreatedBy) &&
                            job != null &&
                            (!x.AssignmentId.HasValue ||
                             x.AssignmentId.Value == 0 ||
                             (assignmentsById.TryGetValue(
                                 x.AssignmentId.Value, out var linkedAssignment) &&
                              linkedAssignment.TemplateId == job.TemplateId)))
                        .ToList();

                    var adminPathEntries = adminHistory
                        .GroupBy(x => x.CreatedBy)
                        .Where(group => group.Any(x => x.IsRemarkAdded &&
                            !string.IsNullOrWhiteSpace(x.Remarks)))
                        .OrderBy(group => group.Key)
                        .Select(group =>
                        {
                            var adminRemarks = group
                                .Where(x => x.IsRemarkAdded &&
                                    !string.IsNullOrWhiteSpace(x.Remarks))
                                .OrderBy(x => x.CreatedOn)
                                .ThenBy(x => x.CandidateSkillAssessmentHistoryId)
                                .ToList();

                            var latestAdminDecision = group
                                .Where(x => IsDecision(x.Action))
                                .OrderByDescending(x => x.CreatedOn)
                                .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
                                .FirstOrDefault();

                            return new
                            {
                                TemplateId = job?.TemplateId,
                                AssignmentId = (int?)null,
                                HierarchyLevel = (int?)null,
                                HierarchyLabel = "Admin",
                                RoleId = (int?)null,
                                RoleName = "Admin",
                                EmployeeId = group.Key,
                                EmployeeName = GetEmployeeName(group.Key),
                                Stage = "Admin",
                                IsAdmin = true,
                                // Admin cards are activity entries, not role assignments.
                                CanApprove = (bool?)null,
                                CanReject = (bool?)null,
                                CanReturn = (bool?)null,
                                ReportingToAssignmentId = (int?)null,
                                ReportingToEmployeeId = (int?)null,
                                ReportingToEmployeeName = (string?)null,
                                ReportingToRoleName = (string?)null,
                                IsCurrentTemplate = true,
                                HasActivity = true,
                                Status = latestAdminDecision?.Action switch
                                {
                                    "Approve" => "Approved",
                                    "Reject" => "Rejected",
                                    "Return" => "Returned",
                                    _ => "Pending"
                                },
                                LatestDecision = latestAdminDecision?.Action,
                                LatestDecisionStatus = latestAdminDecision?.NewStatus,
                                LatestDecisionOn = latestAdminDecision?.CreatedOn,
                                LatestDecisionBy = latestAdminDecision?.CreatedBy,
                                LatestDecisionByName =
                                    GetEmployeeName(latestAdminDecision?.CreatedBy),
                                HistoryCount = adminRemarks.Count,
                                DecisionCount = adminRemarks.Count(x => IsDecision(x.Action)),
                                RemarksCount = adminRemarks.Count,
                                History = adminRemarks.Select(history => new
                                {
                                    HistoryId = history.CandidateSkillAssessmentHistoryId,
                                    history.CandidateSkillAssessmentHistoryId,
                                    history.CandidateSkillAssessmentId,
                                    history.CandidateApplicationId,
                                    history.JobDescriptionId,
                                    history.AssignmentId,
                                    history.CompanySkillMasterId,
                                    SkillId = history.CompanySkillMasterId,
                                    SkillName = GetSkillName(history.CompanySkillMasterId),
                                    IsDefaultRemark = history.CompanySkillMasterId == 0,
                                    DisplayTitle = history.CompanySkillMasterId == 0
                                        ? "Admin Remark"
                                        : GetSkillName(history.CompanySkillMasterId),
                                    Stage = "Admin",
                                    history.Action,
                                    history.PreviousStatus,
                                    history.NewStatus,
                                    Status = history.NewStatus,
                                    history.Score,
                                    history.Remarks,
                                    history.IsRemarkAdded,
                                    EmployeeId = history.CreatedBy,
                                    EmployeeName = GetEmployeeName(history.CreatedBy),
                                    history.CreatedBy,
                                    CreatedByName = GetEmployeeName(history.CreatedBy),
                                    history.CreatedOn,
                                    history.ModifiedBy,
                                    ModifiedByName = GetEmployeeName(history.ModifiedBy),
                                    history.ModifiedOn
                                }).ToList()
                            };
                        }).ToList();

                    var approvalPathWithAdmins = approvalPath.Cast<object>()
                        .Concat(adminPathEntries.Cast<object>())
                        .ToList();

                    // Viewer-specific flag: own approval as admin or at last hierarchy.
                    int? lastHierarchyLevel = assignmentRows
                        .Where(x => job != null &&
                            x.TemplateId == job.TemplateId &&
                            activePermissionTemplateIds.Contains(x.TemplateId) &&
                            x.IsActive == true)
                        .Select(x => (int?)x.HierarchyLevel)
                        .Max();

                    var myLastAssignmentIds = myPermissions
                        .Where(x => x.HierarchyLevel == lastHierarchyLevel)
                        .Select(x => x.AssignmentId)
                        .ToHashSet();

                    var onboardingDecision = accessUser.IsAdmin == true
                        ? adminHistory
                            .Where(x => x.CreatedBy == request.User &&
                                IsDecision(x.Action))
                            .OrderByDescending(x => x.CreatedOn)
                            .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
                            .FirstOrDefault()
                        : applicationHistory
                            .Where(x => x.CreatedBy == request.User &&
                                x.AssignmentId.HasValue &&
                                myLastAssignmentIds.Contains(x.AssignmentId.Value) &&
                                IsDecision(x.Action))
                            .OrderByDescending(x => x.CreatedOn)
                            .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
                            .FirstOrDefault();

                    bool isOnBoarding = onboardingDecision?.Action == "Approve";

                    // Preserve visibility of admin/unlinked history without
                    // inventing an assignment or hierarchy for its author.
                    var unassignedHistory = applicationHistory
                        .Where(x =>
                            !x.AssignmentId.HasValue ||
                            !assignmentsById.ContainsKey(
                                x.AssignmentId.Value))
                        .Select(history => new
                        {
                            HistoryId =
                                history.CandidateSkillAssessmentHistoryId,

                            history.CandidateSkillAssessmentHistoryId,
                            history.CandidateSkillAssessmentId,
                            history.CandidateApplicationId,
                            history.JobDescriptionId,
                            history.AssignmentId,

                            history.CompanySkillMasterId,
                            SkillId = history.CompanySkillMasterId,
                            SkillName = GetSkillName(
                                history.CompanySkillMasterId),

                            IsDefaultRemark =
                                history.CompanySkillMasterId == 0,

                            history.Action,
                            history.PreviousStatus,
                            history.NewStatus,
                            Status = history.NewStatus,
                            history.Score,
                            history.Remarks,
                            history.IsRemarkAdded,

                            EmployeeId = history.CreatedBy,
                            EmployeeName = GetEmployeeName(
                                history.CreatedBy),

                            history.CreatedBy,
                            CreatedByName = GetEmployeeName(
                                history.CreatedBy),
                            history.CreatedOn,

                            history.ModifiedBy,
                            ModifiedByName = GetEmployeeName(
                                history.ModifiedBy),
                            history.ModifiedOn
                        })
                        .ToList();

                    // Tasks come from saved, unconsumed action requests.
                    // This does not infer or create a first-stage task.
                    var yourTask = applicationNotifications.Select(notification =>
                    {
                        int? taskRoleId = null;
                        int? taskHierarchyLevel = null;
                        string? taskStage = null;

                        if (notification.ToAssignmentId.HasValue &&
                            assignmentsById.TryGetValue(
                                notification.ToAssignmentId.Value,
                                out var taskAssignment))
                        {
                            taskRoleId = taskAssignment.RoleId;
                            taskHierarchyLevel = taskAssignment.HierarchyLevel;
                            taskStage = taskAssignment.Stage;
                        }

                        return new
                        {
                            notification.NotificationId,
                            notification.TemplateId,

                            AssignmentId = notification.ToAssignmentId,
                            RoleId = taskRoleId,
                            RoleName = GetRoleName(taskRoleId),
                            HierarchyLevel = taskHierarchyLevel,
                            Stage = taskStage,

                            SkillId = notification.CompanySkillMasterId,
                            SkillName = GetSkillName(
                                notification.CompanySkillMasterId),

                            IsDefaultRemark =
                                notification.CompanySkillMasterId == 0,

                            TaskStatus = "Pending",
                            Source = "Notification",
                            notification.IsActionRequest,

                            notification.Title,
                            notification.Message,

                            notification.FromUserId,
                            FromUserName = GetEmployeeName(
                                notification.FromUserId),

                            notification.ToUserId,
                            ToUserName = GetEmployeeName(
                                notification.ToUserId),

                            notification.ResumeAfterAssignmentId,
                            notification.IsRead,
                            notification.CreatedOn
                        };
                    }).ToList();

                    return new
                    {
                        application.CompanyId,
                        CompanyName = companyName,

                        candidate.CandidateId,
                        application.CandidateApplicationId,

                        application.JobDescriptionId,
                        JobTitle = job?.JobTitle,
                        application.AppliedPosition,

                        application.BranchId,
                        BranchName = branchName,

                        application.DepartmentId,
                        DepartmentName = departmentName,

                        // Personal information
                        candidate.FirstName,
                        candidate.MiddleName,
                        candidate.LastName,

                        CandidateName = string.Join(
                            " ",
                            new[]
                            {
                    candidate.FirstName,
                    candidate.MiddleName,
                    candidate.LastName
                            }.Where(x => !string.IsNullOrWhiteSpace(x))),

                        candidate.Email,
                        candidate.AlternateEmail,
                        candidate.MobileNumber,
                        candidate.AlternateMobileNumber,
                        candidate.Gender,
                        candidate.DateOfBirth,

                        candidate.CurrentAddress,
                        candidate.PermanentAddress,
                        candidate.CurrentCity,
                        candidate.State,
                        candidate.Country,
                        candidate.Pincode,

                        // Professional information
                        candidate.CurrentCompany,
                        candidate.CurrentDesignation,
                        candidate.EmploymentStatus,
                        candidate.TotalExperienceYears,
                        candidate.RelevantExperienceYears,
                        candidate.NoticePeriodDays,
                        candidate.PreviousCompany,
                        candidate.Industry,
                        candidate.FunctionalArea,
                        candidate.HighestQualification,
                        candidate.Specialization,
                        candidate.University,
                        candidate.PassingYear,
                        candidate.ResumePath,

                        // Compensation
                        application.Currency,
                        application.CurrentCTC,
                        application.CurrentFixedCTC,
                        application.CurrentVariableCTC,
                        application.ExpectedCTC,
                        application.ExpectedFixedCTC,
                        application.ExpectedVariableCTC,
                        application.CurrentMonthlySalary,
                        application.ExpectedMonthlySalary,
                        application.OfferedCTC,
                        application.IsNegotiable,

                        // Recruitment information
                        application.Source,
                        application.ReferredBy,

                        application.RecruiterEmployeeId,
                        RecruiterEmployeeName = GetEmployeeName(
                            application.RecruiterEmployeeId),

                        application.HiringManagerEmployeeId,
                        HiringManagerEmployeeName = GetEmployeeName(
                            application.HiringManagerEmployeeId),

                        application.RecruitmentCityId,
                        RecruitmentCityName = recruitmentCityName,

                        application.Cadre,
                        application.InterviewRequired,
                        application.PreferredInterviewDate,
                        application.PreferredLocation,
                        application.WillingToRelocate,
                        application.CandidateAvailability,
                        application.Remarks,

                        application.Stage,
                        // Preserve the frontend's existing ApplicationStatus field.
                        // Admin sees the stored status; role users see their own decision.
                        ApplicationStatus = accessUser.IsAdmin == true
                            ? application.Status
                            : ownRoleStatus,
                        application.AppliedOn,
                        application.IsActive,

                        CandidateIsActive = candidate.IsActive,

                        // Application audit
                        application.CreatedOn,
                        application.CreatedBy,
                        CreatedByName = GetEmployeeName(
                            application.CreatedBy),

                        application.ModifiedOn,
                        application.ModifiedBy,
                        ModifiedByName = GetEmployeeName(
                            application.ModifiedBy),

                        // Candidate master audit
                        CandidateCreatedOn = candidate.CreatedOn,
                        CandidateCreatedBy = candidate.CreatedBy,
                        CandidateCreatedByName = GetEmployeeName(
                            candidate.CreatedBy),

                        CandidateModifiedOn = candidate.ModifiedOn,
                        CandidateModifiedBy = candidate.ModifiedBy,
                        CandidateModifiedByName = GetEmployeeName(
                            candidate.ModifiedBy),

                        // Existing document response retained.
                        Documents = documentsByApplication[
                            application.CandidateApplicationId]
                            .Select(document => new
                            {
                                document.CandidateDocumentId,
                                document.CandidateApplicationId,
                                document.DocumentType,
                                document.OriginalFileName,

                                FilePath = document.StorageKey,

                                document.ContentType,
                                document.FileSizeBytes,
                                document.IsActive,

                                document.CreatedOn,
                                document.CreatedBy,

                                CreatedByName = GetEmployeeName(
                                    document.CreatedBy),

                                document.ModifiedOn,
                                document.ModifiedBy,

                                ModifiedByName = GetEmployeeName(
                                    document.ModifiedBy)
                            })
                            .ToList(),

                        // Additional response fields for your screen.
                        TemplateId = job?.TemplateId,

                        ApprovalHistoryCount = applicationHistory.Count,

                        ApprovalActionCount = applicationHistory.Count(x =>
                            IsDecision(x.Action)),

                        IsResume = isResume,
                        ResumeDetails = resumeDetails,
                        ResumeDetailsSource = resumeDetailsSource,
                        ResumeHistoryId = resumeHistoryId,

                        MyPermissions = myPermissions,
                        ApprovalPath = approvalPathWithAdmins,
                        IsOnBoarding = isOnBoarding,
                        UnassignedApprovalHistory = unassignedHistory,

                        HasPendingActionRequest = yourTask.Count > 0,
                        YourTask = yourTask
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = "Candidate list fetched successfully.",
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch candidates for CompanyId {CompanyId}.",
                    request.CompanyId);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Unable to fetch candidate list."
                });
            }
        }



        //        [HttpPost("GetCandidateList")]
        //        public async Task<IActionResult> GetCandidateList(
        //[FromBody] GetCandidateListRequest? request)
        //        {
        //            if (request == null || request.CompanyId <= 0 || request.User <= 0)
        //            {
        //                return BadRequest(new
        //                {
        //                    Status = false,
        //                    Message = "Valid CompanyId and User are required."
        //                });
        //            }

        //            if (request.JobDescriptionIds != null && request.JobDescriptionIds.Any(id => id < 0))
        //            {
        //                return BadRequest(new
        //                {
        //                    Status = false,
        //                    Message = "JobDescriptionIds cannot contain negative IDs."
        //                });
        //            }

        //            // Existing filter behavior:
        //            // null, [], or any list containing 0 means all.
        //            static int[] GetFilterIds(List<int>? values)
        //            {
        //                if (values == null ||
        //                    values.Count == 0 ||
        //                    values.Contains(0))
        //                {
        //                    return Array.Empty<int>();
        //                }

        //                return values.Distinct().ToArray();
        //            }

        //            try
        //            {
        //                // Determine access from the employee record, not the request.
        //                var accessUser = await _context.EmployeeMasters
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EmployeeId == request.User)
        //                    .Select(x => new { x.EmployeeId, x.IsAdmin, x.IsActive })
        //                    .FirstOrDefaultAsync();

        //                if (accessUser == null)
        //                {
        //                    return NotFound(new
        //                    {
        //                        Status = false,
        //                        Message = "User not found for this company."
        //                    });
        //                }

        //                if (accessUser.IsActive != true)
        //                {
        //                    return StatusCode(403, new
        //                    {
        //                        Status = false,
        //                        Message = "User is inactive."
        //                    });
        //                }

        //                var branchIds = GetFilterIds(request.BranchIds);
        //                var departmentIds = GetFilterIds(request.DepartmentIds);
        //                var jobIds = GetFilterIds(request.JobDescriptionIds);

        //                var query =
        //                    from application in _context.CandidateApplications
        //                        .AsNoTracking()

        //                    join candidate in _context.Candidates.AsNoTracking()
        //                        on new
        //                        {
        //                            application.CompanyId,
        //                            application.CandidateId
        //                        }
        //                        equals new
        //                        {
        //                            candidate.CompanyId,
        //                            candidate.CandidateId
        //                        }

        //                    where application.CompanyId == request.CompanyId

        //                    select new
        //                    {
        //                        Application = application,
        //                        Candidate = candidate
        //                    };

        //                if (branchIds.Length > 0)
        //                {
        //                    query = query.Where(x =>
        //                        x.Application.BranchId.HasValue &&
        //                        branchIds.Contains(x.Application.BranchId.Value));
        //                }

        //                if (departmentIds.Length > 0)
        //                {
        //                    query = query.Where(x =>
        //                        x.Application.DepartmentId.HasValue &&
        //                        departmentIds.Contains(
        //                            x.Application.DepartmentId.Value));
        //                }

        //                // null, [], or a list containing 0 means all jobs.
        //                if (jobIds.Length > 0)
        //                {
        //                    query = query.Where(x =>
        //                        jobIds.Contains(x.Application.JobDescriptionId));
        //                }

        //                // Admins retain the existing company-wide list.
        //                // Non-admins see only jobs mapped to their assigned templates.
        //                if (accessUser.IsAdmin != true)
        //                {
        //                    var allowedJobIds =
        //                        from job in _context.JobDescriptions.AsNoTracking()
        //                        join assignment in _context.RoleTemplateAssignment.AsNoTracking()
        //                            on new { job.CompanyId, TemplateId = (int?)job.TemplateId }
        //                            equals new { assignment.CompanyId, TemplateId = (int?)assignment.TemplateId }
        //                        join template in _context.RoleTemplates.AsNoTracking()
        //                            on new { assignment.CompanyId, assignment.TemplateId }
        //                            equals new { template.CompanyId, template.TemplateId }
        //                        where job.CompanyId == request.CompanyId &&
        //                              assignment.EmployeeId == accessUser.EmployeeId &&
        //                              assignment.IsActive == true &&
        //                              template.IsActive == true
        //                        select job.JobDescriptionId;

        //                    query = query.Where(x =>
        //                        allowedJobIds.Contains(x.Application.JobDescriptionId));
        //                }

        //                var rows = await query
        //                    .OrderByDescending(x =>
        //                        x.Application.CandidateApplicationId)
        //                    .ToListAsync();

        //                if (rows.Count == 0)
        //                {
        //                    return Ok(new
        //                    {
        //                        Status = true,
        //                        Message = "No candidates found.",
        //                        TotalCount = 0,
        //                        Data = Array.Empty<object>()
        //                    });
        //                }

        //                var resultApplicationIds = rows
        //                    .Select(x => x.Application.CandidateApplicationId)
        //                    .Distinct()
        //                    .ToArray();

        //                var resultJobIds = rows
        //                    .Select(x => x.Application.JobDescriptionId)
        //                    .Distinct()
        //                    .ToArray();

        //                var resultBranchIds = rows
        //                    .Where(x => x.Application.BranchId.HasValue)
        //                    .Select(x => x.Application.BranchId!.Value)
        //                    .Distinct()
        //                    .ToArray();

        //                var resultDepartmentIds = rows
        //                    .Where(x => x.Application.DepartmentId.HasValue)
        //                    .Select(x => x.Application.DepartmentId!.Value)
        //                    .Distinct()
        //                    .ToArray();

        //                var resultCityIds = rows
        //                    .Where(x => x.Application.RecruitmentCityId.HasValue)
        //                    .Select(x => x.Application.RecruitmentCityId!.Value)
        //                    .Distinct()
        //                    .ToArray();

        //                var companyName = await _context.Companys
        //                    .AsNoTracking()
        //                    .Where(x => x.CompanyID == request.CompanyId)
        //                    .Select(x => x.CompanyName)
        //                    .FirstOrDefaultAsync();

        //                // TemplateId is additionally loaded for the approval path.
        //                var jobRows = await _context.JobDescriptions
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        resultJobIds.Contains(x.JobDescriptionId))
        //                    .Select(x => new
        //                    {
        //                        x.JobDescriptionId,
        //                        x.JobTitle,
        //                        TemplateId = (int?)x.TemplateId
        //                    })
        //                    .ToListAsync();

        //                var jobs = jobRows.ToDictionary(
        //                    x => x.JobDescriptionId);

        //                var branches = await _context.Branches
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        resultBranchIds.Contains(x.BranchId))
        //                    .Select(x => new
        //                    {
        //                        x.BranchId,
        //                        x.BranchName
        //                    })
        //                    .ToDictionaryAsync(
        //                        x => x.BranchId,
        //                        x => x.BranchName);

        //                var departments = await _context.Departments
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        resultDepartmentIds.Contains(x.DepartmentId))
        //                    .Select(x => new
        //                    {
        //                        x.DepartmentId,
        //                        x.DepartmentName
        //                    })
        //                    .ToDictionaryAsync(
        //                        x => x.DepartmentId,
        //                        x => x.DepartmentName);

        //                var cities = await _context.Cities
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        resultCityIds.Contains(x.CityId))
        //                    .Select(x => new
        //                    {
        //                        x.CityId,
        //                        x.CityName
        //                    })
        //                    .ToDictionaryAsync(
        //                        x => x.CityId,
        //                        x => x.CityName);

        //                var documents = await _context.CandidateDocuments
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        resultApplicationIds.Contains(
        //                            x.CandidateApplicationId))
        //                    .OrderByDescending(x => x.CandidateDocumentId)
        //                    .ToListAsync();

        //                // -------------------------------------------------------
        //                // Additional data for approval path and current-user tasks.
        //                // Existing candidate filtering is unchanged.
        //                // -------------------------------------------------------

        //                var templateIds = jobRows
        //                    .Where(x =>
        //                        x.TemplateId.HasValue &&
        //                        x.TemplateId.Value > 0)
        //                    .Select(x => x.TemplateId!.Value)
        //                    .Distinct()
        //                    .ToArray();

        //                // Include all saved history:
        //                // saves, remarks, approvals, rejections and returns.
        //                // Do not exclude IsRemarkAdded records.
        //                var histories = await _context
        //                    .CandidateSkillAssessmentHistories
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        resultApplicationIds.Contains(
        //                            x.CandidateApplicationId) &&
        //                        resultJobIds.Contains(x.JobDescriptionId))
        //                    .OrderBy(x => x.CreatedOn)
        //                    .ThenBy(x => x.CandidateSkillAssessmentHistoryId)
        //                    .ToListAsync();

        //                var historyAssignmentIds = histories
        //                    .Where(x => x.AssignmentId.HasValue)
        //                    .Select(x => x.AssignmentId!.Value)
        //                    .Distinct()
        //                    .ToArray();

        //                // Include inactive assignments so old history still resolves.
        //                // Also load historical assignments even if a JD's template changed.
        //                var assignmentRows = await _context.RoleTemplateAssignment
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        (
        //                            templateIds.Contains(x.TemplateId) ||
        //                            historyAssignmentIds.Contains(x.AssignmentId)
        //                        ))
        //                    .Select(x => new
        //                    {
        //                        x.AssignmentId,
        //                        x.TemplateId,
        //                        x.EmployeeId,
        //                        x.RoleId,
        //                        x.HierarchyLevel,
        //                        x.Stage,
        //                        x.ReportingToAssignmentId,
        //                        x.CanApprove,
        //                        x.CanReject,
        //                        x.CanReturn,
        //                        x.IsActive
        //                    })
        //                    .ToListAsync();

        //                // Configured permissions for the requested employee only.
        //                // Keep historical ApprovalPath permissions unchanged.
        //                var permissionUserActive = request.User > 0 &&
        //                    await _context.EmployeeMasters.AsNoTracking().AnyAsync(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.EmployeeId == request.User &&
        //                        x.IsActive == true);

        //                var activePermissionTemplateIds = (await _context.RoleTemplates
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        templateIds.Contains(x.TemplateId) &&
        //                        x.IsActive == true)
        //                    .Select(x => x.TemplateId)
        //                    .ToListAsync()).ToHashSet();

        //                var assignmentsById = assignmentRows.ToDictionary(
        //                    x => x.AssignmentId);

        //                var roleIds = assignmentRows
        //                    .Select(x => x.RoleId)
        //                    .Distinct()
        //                    .ToArray();

        //                var roleNames = await _context.Roles
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        roleIds.Contains(x.RoleId))
        //                    .Select(x => new
        //                    {
        //                        x.RoleId,
        //                        x.RoleName
        //                    })
        //                    .ToDictionaryAsync(
        //                        x => x.RoleId,
        //                        x => x.RoleName);

        //                var historySkillIds = histories
        //                    .Where(x => x.CompanySkillMasterId > 0)
        //                    .Select(x => x.CompanySkillMasterId)
        //                    .Distinct()
        //                    .ToArray();

        //                // Include inactive skills to retain names for existing history.
        //                var skillRows = await _context.CompanySkillMasters
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        historySkillIds.Contains(
        //                            x.CompanySkillMasterId))
        //                    .Select(x => new
        //                    {
        //                        x.CompanySkillMasterId,
        //                        x.SkillName
        //                    })
        //                    .ToListAsync();

        //                var skillNames = skillRows.ToDictionary(
        //                    x => x.CompanySkillMasterId,
        //                    x => x.SkillName);

        //                // User = 0 intentionally returns no personal tasks.
        //                // Reading this API does not mark notifications read or actioned.
        //                var pendingNotifications = await _context
        //                    .RecruitmentNotifications
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        request.User > 0 &&
        //                        x.CompanyId == request.CompanyId &&
        //                        x.ToUserId == request.User &&
        //                        resultApplicationIds.Contains(
        //                            x.CandidateApplicationId) &&
        //                        resultJobIds.Contains(x.JobDescriptionId) &&
        //                        x.IsActionRequest == true &&
        //                        x.ActionedByHistoryId == null)
        //                    .OrderByDescending(x => x.CreatedOn)
        //                    .ThenByDescending(x => x.NotificationId)
        //                    .ToListAsync();

        //                // Includes existing audit users and approval-path participants.
        //                var employeeIds = rows
        //                    .SelectMany(x => new int?[]
        //                    {
        //            x.Application.RecruiterEmployeeId,
        //            x.Application.HiringManagerEmployeeId,
        //            x.Application.CreatedBy,
        //            x.Application.ModifiedBy,
        //            x.Candidate.CreatedBy,
        //            x.Candidate.ModifiedBy
        //                    })
        //                    .Concat(documents.SelectMany(x => new int?[]
        //                    {
        //            x.CreatedBy,
        //            x.ModifiedBy
        //                    }))
        //                    .Concat(histories.SelectMany(x => new int?[]
        //                    {
        //            x.CreatedBy,
        //            x.ModifiedBy
        //                    }))
        //                    .Concat(assignmentRows.Select(x =>
        //                        (int?)x.EmployeeId))
        //                    .Concat(pendingNotifications.SelectMany(x => new int?[]
        //                    {
        //            x.FromUserId,
        //            x.ToUserId
        //                    }))
        //                    .Where(x => x.HasValue)
        //                    .Select(x => x!.Value)
        //                    .Distinct()
        //                    .ToArray();

        //                var employeeNames = await _context.EmployeeMasters
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        employeeIds.Contains(x.EmployeeId))
        //                    .Select(x => new
        //                    {
        //                        x.EmployeeId,

        //                        EmployeeName =
        //                            ((x.FirstName ?? "") + " " +
        //                             (x.LastName ?? "")).Trim()
        //                    })
        //                    .ToDictionaryAsync(
        //                        x => x.EmployeeId,
        //                        x => x.EmployeeName);

        //                string? GetEmployeeName(int? employeeId)
        //                {
        //                    if (employeeId.HasValue &&
        //                        employeeNames.TryGetValue(
        //                            employeeId.Value, out var name))
        //                    {
        //                        return name;
        //                    }

        //                    return null;
        //                }

        //                string? GetRoleName(int? roleId)
        //                {
        //                    if (roleId.HasValue &&
        //                        roleNames.TryGetValue(roleId.Value, out var name))
        //                    {
        //                        return name;
        //                    }

        //                    return null;
        //                }

        //                string GetSkillName(int skillId)
        //                {
        //                    if (skillId == 0)
        //                        return "Default Remark";

        //                    return skillNames.TryGetValue(skillId, out var name) &&
        //                           !string.IsNullOrWhiteSpace(name)
        //                        ? name
        //                        : $"Skill {skillId}";
        //                }

        //                static bool IsDecision(string? action)
        //                {
        //                    return action == "Approve" ||
        //                           action == "Reject" ||
        //                           action == "Return";
        //                }

        //                var documentsByApplication = documents.ToLookup(
        //                    x => x.CandidateApplicationId);

        //                // Scope history to both application and JD.
        //                var historiesByApplication = histories.ToLookup(x => (
        //                    x.CandidateApplicationId,
        //                    x.JobDescriptionId
        //                ));

        //                var notificationsByApplication =
        //                    pendingNotifications.ToLookup(x => (
        //                        x.CandidateApplicationId,
        //                        x.JobDescriptionId
        //                    ));

        //                var data = rows.Select(row =>
        //                {
        //                    var candidate = row.Candidate;
        //                    var application = row.Application;

        //                    jobs.TryGetValue(
        //                        application.JobDescriptionId,
        //                        out var job);

        //                    string? branchName = null;

        //                    if (application.BranchId.HasValue)
        //                    {
        //                        branches.TryGetValue(
        //                            application.BranchId.Value,
        //                            out branchName);
        //                    }

        //                    string? departmentName = null;

        //                    if (application.DepartmentId.HasValue)
        //                    {
        //                        departments.TryGetValue(
        //                            application.DepartmentId.Value,
        //                            out departmentName);
        //                    }

        //                    string? recruitmentCityName = null;

        //                    if (application.RecruitmentCityId.HasValue)
        //                    {
        //                        cities.TryGetValue(
        //                            application.RecruitmentCityId.Value,
        //                            out recruitmentCityName);
        //                    }

        //                    // Permissions come from this employee's assignments in
        //                    // this job's current active template, not another role.
        //                    var myPermissions = assignmentRows
        //                        .Where(x =>
        //                            permissionUserActive &&
        //                            job != null &&
        //                            x.TemplateId == job.TemplateId &&
        //                            activePermissionTemplateIds.Contains(x.TemplateId) &&
        //                            x.EmployeeId == request.User &&
        //                            x.IsActive == true)
        //                        .OrderBy(x => x.HierarchyLevel)
        //                        .ThenBy(x => x.AssignmentId)
        //                        .Select(x => new
        //                        {
        //                            x.AssignmentId,
        //                            x.TemplateId,
        //                            x.EmployeeId,
        //                            x.RoleId,
        //                            RoleName = GetRoleName(x.RoleId),
        //                            x.HierarchyLevel,
        //                            x.Stage,
        //                            CanApprove = x.CanApprove == true,
        //                            CanReject = x.CanReject == true,
        //                            CanReturn = x.CanReturn == true
        //                        })
        //                        .ToList();

        //                    var applicationHistory = historiesByApplication[(
        //                        application.CandidateApplicationId,
        //                        application.JobDescriptionId
        //                    )].ToList();

        //                    // Current user's decision only. Another hierarchy's approval
        //                    // must not set this user's candidate status to Approved.
        //                    var ownStatusAssignmentIds = myPermissions
        //                        .Select(x => x.AssignmentId)
        //                        .ToHashSet();

        //                    var ownLatestDecision = applicationHistory
        //                        .Where(x =>
        //                            x.CreatedBy == request.User &&
        //                            x.AssignmentId.HasValue &&
        //                            ownStatusAssignmentIds.Contains(x.AssignmentId.Value) &&
        //                            IsDecision(x.Action))
        //                        .OrderByDescending(x => x.CreatedOn)
        //                        .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
        //                        .FirstOrDefault();

        //                    string ownRoleStatus = ownLatestDecision?.Action switch
        //                    {
        //                        "Approve" => "Approved",
        //                        "Reject" => "Rejected",
        //                        "Return" => "Returned",
        //                        _ => "Pending"
        //                    };

        //                    var applicationNotifications =
        //                        notificationsByApplication[(
        //                            application.CandidateApplicationId,
        //                            application.JobDescriptionId
        //                        )].ToList();

        //                    var applicationHistoryAssignmentIds =
        //                        applicationHistory
        //                            .Where(x => x.AssignmentId.HasValue)
        //                            .Select(x => x.AssignmentId!.Value)
        //                            .ToHashSet();

        //                    // Show current active hierarchy assignments, including those
        //                    // without history, plus assignments with historical activity.
        //                    var pathAssignments = assignmentRows
        //                        .Where(x =>
        //                            (
        //                                job != null &&
        //                                x.TemplateId == job.TemplateId &&
        //                                x.IsActive == true
        //                            ) ||
        //                            applicationHistoryAssignmentIds.Contains(
        //                                x.AssignmentId))
        //                        .OrderBy(x => x.HierarchyLevel)
        //                        .ThenBy(x => x.AssignmentId)
        //                        .ToList();

        //                    // Resume is shown only on the first active assignment
        //                    // in the job's current template, not historical templates.
        //                    int? resumeAssignmentId = pathAssignments
        //                        .Where(x => job != null &&
        //                            x.TemplateId == job.TemplateId && x.IsActive == true)
        //                        .OrderBy(x => x.HierarchyLevel)
        //                        .ThenBy(x => x.AssignmentId)
        //                        .Select(x => (int?)x.AssignmentId)
        //                        .FirstOrDefault();

        //                    bool isResume =
        //                        permissionUserActive &&
        //                        job != null &&
        //                        job.TemplateId.HasValue &&
        //                        activePermissionTemplateIds.Contains(job.TemplateId.Value) &&
        //                        resumeAssignmentId.HasValue &&
        //                        assignmentRows.Any(x =>
        //                            x.AssignmentId == resumeAssignmentId.Value &&
        //                            x.EmployeeId == request.User);
        //                    object? resumeDetails = null;
        //                    string? resumeDetailsSource = null;
        //                    int? resumeHistoryId = null;

        //                    if (isResume)
        //                    {
        //                        var resumeHistory = applicationHistory
        //                            .Where(x =>
        //                                x.AssignmentId == resumeAssignmentId &&
        //                                x.CreatedBy == request.User &&
        //                                x.IsResume == true)
        //                            .OrderByDescending(x => x.CreatedOn)
        //                            .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
        //                            .FirstOrDefault();

        //                        if (!string.IsNullOrWhiteSpace(resumeHistory?.DetailsJson))
        //                        {
        //                            try
        //                            {
        //                                using var document = System.Text.Json.JsonDocument.Parse(
        //                                    resumeHistory!.DetailsJson);
        //                                var root = document.RootElement;
        //                                if (root.ValueKind == System.Text.Json.JsonValueKind.Object &&
        //                                    root.EnumerateObject().Any())
        //                                {
        //                                    // Keep the saved detail fields and add authoritative
        //                                    // identifiers from the history record, not its JSON.
        //                                    var detailsWithIds = new Dictionary<string, object?>(
        //                                        StringComparer.OrdinalIgnoreCase);

        //                                    foreach (var property in root.EnumerateObject())
        //                                    {
        //                                        if (string.Equals(property.Name, "CandidateSkillAssessmentId", StringComparison.OrdinalIgnoreCase) ||
        //                                            string.Equals(property.Name, "CompanyId", StringComparison.OrdinalIgnoreCase) ||
        //                                            string.Equals(property.Name, "JobDescriptionId", StringComparison.OrdinalIgnoreCase) ||
        //                                            string.Equals(property.Name, "CandidateApplicationId", StringComparison.OrdinalIgnoreCase))
        //                                        {
        //                                            continue;
        //                                        }

        //                                        detailsWithIds[property.Name] = property.Value.Clone();
        //                                    }

        //                                    detailsWithIds["candidateSkillAssessmentId"] = resumeHistory.CandidateSkillAssessmentId;
        //                                    detailsWithIds["companyId"] = resumeHistory.CompanyId;
        //                                    detailsWithIds["jobDescriptionId"] = resumeHistory.JobDescriptionId;
        //                                    detailsWithIds["candidateApplicationId"] = resumeHistory.CandidateApplicationId;
        //                                    resumeDetails = detailsWithIds;
        //                                    resumeDetailsSource = "History";
        //                                    resumeHistoryId = resumeHistory.CandidateSkillAssessmentHistoryId;
        //                                }
        //                            }
        //                            catch (System.Text.Json.JsonException)
        //                            {
        //                                // An invalid historical JSON value must not
        //                                // prevent the candidate list from loading.
        //                            }
        //                        }

        //                        if (resumeDetails == null)
        //                        {
        //                            resumeDetailsSource = "Candidate";
        //                            resumeDetails = new
        //                            {
        //                                // Null when no saved resume record exists.
        //                                CandidateSkillAssessmentId = (int?)resumeHistory?.CandidateSkillAssessmentId,
        //                                application.CompanyId,
        //                                application.JobDescriptionId,
        //                                application.CandidateApplicationId,
        //                                candidate.CandidateId,
        //                                candidate.FirstName,
        //                                candidate.MiddleName,
        //                                candidate.LastName,
        //                                candidate.Email,
        //                                candidate.AlternateEmail,
        //                                candidate.MobileNumber,
        //                                candidate.AlternateMobileNumber,
        //                                candidate.Gender,
        //                                candidate.DateOfBirth,
        //                                candidate.CurrentAddress,
        //                                candidate.PermanentAddress,
        //                                candidate.CurrentCity,
        //                                candidate.State,
        //                                candidate.Country,
        //                                candidate.Pincode,
        //                                candidate.CurrentCompany,
        //                                candidate.CurrentDesignation,
        //                                candidate.EmploymentStatus,
        //                                candidate.TotalExperienceYears,
        //                                candidate.RelevantExperienceYears,
        //                                candidate.NoticePeriodDays,
        //                                candidate.PreviousCompany,
        //                                candidate.Industry,
        //                                candidate.FunctionalArea,
        //                                candidate.HighestQualification,
        //                                candidate.Specialization,
        //                                candidate.University,
        //                                candidate.PassingYear,
        //                                candidate.ResumePath
        //                            };
        //                        }
        //                    }

        //                    var approvalPath = pathAssignments.Select(assignment =>
        //                    {
        //                        var assignmentHistory = applicationHistory
        //                            .Where(x =>
        //                                x.AssignmentId == assignment.AssignmentId)
        //                            .OrderBy(x => x.CreatedOn)
        //                            .ThenBy(x =>
        //                                x.CandidateSkillAssessmentHistoryId)
        //                            .ToList();

        //                        var latestDecision = assignmentHistory
        //                            .Where(x => IsDecision(x.Action))
        //                            .OrderByDescending(x => x.CreatedOn)
        //                            .ThenByDescending(x =>
        //                                x.CandidateSkillAssessmentHistoryId)
        //                            .FirstOrDefault();

        //                        // Latest recorded decision, not completion of every skill.
        //                        string hierarchyStatus = latestDecision?.Action switch
        //                        {
        //                            "Approve" => "Approved",
        //                            "Reject" => "Rejected",
        //                            "Return" => "Returned",
        //                            _ => "Pending"
        //                        };

        //                        int? reportingEmployeeId = null;
        //                        string? reportingEmployeeName = null;
        //                        string? reportingRoleName = null;

        //                        if (assignment.ReportingToAssignmentId.HasValue &&
        //                            assignmentsById.TryGetValue(
        //                                assignment.ReportingToAssignmentId.Value,
        //                                out var reportingAssignment))
        //                        {
        //                            reportingEmployeeId =
        //                                reportingAssignment.EmployeeId;

        //                            reportingEmployeeName = GetEmployeeName(
        //                                reportingAssignment.EmployeeId);

        //                            reportingRoleName = GetRoleName(
        //                                reportingAssignment.RoleId);
        //                        }

        //                        return new
        //                        {
        //                            assignment.TemplateId,
        //                            assignment.AssignmentId,
        //                            assignment.HierarchyLevel,

        //                            HierarchyLabel =
        //                                $"H{assignment.HierarchyLevel}",

        //                            assignment.RoleId,
        //                            RoleName = GetRoleName(assignment.RoleId),

        //                            assignment.EmployeeId,
        //                            EmployeeName = GetEmployeeName(
        //                                assignment.EmployeeId),

        //                            assignment.Stage,
        //                            assignment.IsActive,
        //                            assignment.CanApprove,
        //                            assignment.CanReject,
        //                            assignment.CanReturn,

        //                            assignment.ReportingToAssignmentId,
        //                            ReportingToEmployeeId = reportingEmployeeId,
        //                            ReportingToEmployeeName = reportingEmployeeName,
        //                            ReportingToRoleName = reportingRoleName,

        //                            IsCurrentTemplate =
        //                                job != null &&
        //                                assignment.TemplateId == job.TemplateId,


        //                            HasActivity = assignmentHistory.Count > 0,
        //                            Status = hierarchyStatus,

        //                            // This is the last recorded decision, not an
        //                            // inferred completion status for all assigned skills.
        //                            LatestDecision = latestDecision?.Action,
        //                            LatestDecisionStatus = latestDecision?.NewStatus,
        //                            LatestDecisionOn = latestDecision?.CreatedOn,
        //                            LatestDecisionBy = latestDecision?.CreatedBy,
        //                            LatestDecisionByName = GetEmployeeName(
        //                                latestDecision?.CreatedBy),

        //                            HistoryCount = assignmentHistory.Count,

        //                            DecisionCount = assignmentHistory.Count(x =>
        //                                IsDecision(x.Action)),

        //                            RemarksCount = assignmentHistory.Count(x =>
        //                                x.IsRemarkAdded),

        //                            History = assignmentHistory.Select(history => new
        //                            {
        //                                HistoryId =
        //                                    history.CandidateSkillAssessmentHistoryId,

        //                                history.CandidateSkillAssessmentHistoryId,
        //                                history.CandidateSkillAssessmentId,
        //                                history.CandidateApplicationId,
        //                                history.JobDescriptionId,
        //                                history.AssignmentId,

        //                                history.CompanySkillMasterId,
        //                                SkillId = history.CompanySkillMasterId,

        //                                SkillName = GetSkillName(
        //                                    history.CompanySkillMasterId),

        //                                IsDefaultRemark =
        //                                    history.CompanySkillMasterId == 0,

        //                                // Default-remark activity can be displayed
        //                                // under the assignment's configured stage.
        //                                DisplayTitle =
        //                                    history.CompanySkillMasterId == 0
        //                                        ? string.IsNullOrWhiteSpace(
        //                                            assignment.Stage)
        //                                            ? "Default Remark"
        //                                            : $"Stage: {assignment.Stage}"
        //                                        : GetSkillName(
        //                                            history.CompanySkillMasterId),

        //                                Stage = assignment.Stage,

        //                                history.Action,
        //                                history.PreviousStatus,
        //                                history.NewStatus,
        //                                Status = history.NewStatus,
        //                                history.Score,
        //                                history.Remarks,
        //                                history.IsRemarkAdded,

        //                                // Use the actual event author.
        //                                EmployeeId = history.CreatedBy,
        //                                EmployeeName = GetEmployeeName(
        //                                    history.CreatedBy),

        //                                history.CreatedBy,
        //                                CreatedByName = GetEmployeeName(
        //                                    history.CreatedBy),
        //                                history.CreatedOn,

        //                                history.ModifiedBy,
        //                                ModifiedByName = GetEmployeeName(
        //                                    history.ModifiedBy),
        //                                history.ModifiedOn
        //                            }).ToList()
        //                        };
        //                    }).ToList();

        //                    // Preserve visibility of admin/unlinked history without
        //                    // inventing an assignment or hierarchy for its author.
        //                    var unassignedHistory = applicationHistory
        //                        .Where(x =>
        //                            !x.AssignmentId.HasValue ||
        //                            !assignmentsById.ContainsKey(
        //                                x.AssignmentId.Value))
        //                        .Select(history => new
        //                        {
        //                            HistoryId =
        //                                history.CandidateSkillAssessmentHistoryId,

        //                            history.CandidateSkillAssessmentHistoryId,
        //                            history.CandidateSkillAssessmentId,
        //                            history.CandidateApplicationId,
        //                            history.JobDescriptionId,
        //                            history.AssignmentId,

        //                            history.CompanySkillMasterId,
        //                            SkillId = history.CompanySkillMasterId,
        //                            SkillName = GetSkillName(
        //                                history.CompanySkillMasterId),

        //                            IsDefaultRemark =
        //                                history.CompanySkillMasterId == 0,

        //                            history.Action,
        //                            history.PreviousStatus,
        //                            history.NewStatus,
        //                            Status = history.NewStatus,
        //                            history.Score,
        //                            history.Remarks,
        //                            history.IsRemarkAdded,

        //                            EmployeeId = history.CreatedBy,
        //                            EmployeeName = GetEmployeeName(
        //                                history.CreatedBy),

        //                            history.CreatedBy,
        //                            CreatedByName = GetEmployeeName(
        //                                history.CreatedBy),
        //                            history.CreatedOn,

        //                            history.ModifiedBy,
        //                            ModifiedByName = GetEmployeeName(
        //                                history.ModifiedBy),
        //                            history.ModifiedOn
        //                        })
        //                        .ToList();

        //                    // Tasks come from saved, unconsumed action requests.
        //                    // This does not infer or create a first-stage task.
        //                    var yourTask = applicationNotifications.Select(notification =>
        //                    {
        //                        int? taskRoleId = null;
        //                        int? taskHierarchyLevel = null;
        //                        string? taskStage = null;

        //                        if (notification.ToAssignmentId.HasValue &&
        //                            assignmentsById.TryGetValue(
        //                                notification.ToAssignmentId.Value,
        //                                out var taskAssignment))
        //                        {
        //                            taskRoleId = taskAssignment.RoleId;
        //                            taskHierarchyLevel = taskAssignment.HierarchyLevel;
        //                            taskStage = taskAssignment.Stage;
        //                        }

        //                        return new
        //                        {
        //                            notification.NotificationId,
        //                            notification.TemplateId,

        //                            AssignmentId = notification.ToAssignmentId,
        //                            RoleId = taskRoleId,
        //                            RoleName = GetRoleName(taskRoleId),
        //                            HierarchyLevel = taskHierarchyLevel,
        //                            Stage = taskStage,

        //                            SkillId = notification.CompanySkillMasterId,
        //                            SkillName = GetSkillName(
        //                                notification.CompanySkillMasterId),

        //                            IsDefaultRemark =
        //                                notification.CompanySkillMasterId == 0,

        //                            TaskStatus = "Pending",
        //                            Source = "Notification",
        //                            notification.IsActionRequest,

        //                            notification.Title,
        //                            notification.Message,

        //                            notification.FromUserId,
        //                            FromUserName = GetEmployeeName(
        //                                notification.FromUserId),

        //                            notification.ToUserId,
        //                            ToUserName = GetEmployeeName(
        //                                notification.ToUserId),

        //                            notification.ResumeAfterAssignmentId,
        //                            notification.IsRead,
        //                            notification.CreatedOn
        //                        };
        //                    }).ToList();

        //                    return new
        //                    {
        //                        application.CompanyId,
        //                        CompanyName = companyName,

        //                        candidate.CandidateId,
        //                        application.CandidateApplicationId,

        //                        application.JobDescriptionId,
        //                        JobTitle = job?.JobTitle,
        //                        application.AppliedPosition,

        //                        application.BranchId,
        //                        BranchName = branchName,

        //                        application.DepartmentId,
        //                        DepartmentName = departmentName,

        //                        // Personal information
        //                        candidate.FirstName,
        //                        candidate.MiddleName,
        //                        candidate.LastName,

        //                        CandidateName = string.Join(
        //                            " ",
        //                            new[]
        //                            {
        //                    candidate.FirstName,
        //                    candidate.MiddleName,
        //                    candidate.LastName
        //                            }.Where(x => !string.IsNullOrWhiteSpace(x))),

        //                        candidate.Email,
        //                        candidate.AlternateEmail,
        //                        candidate.MobileNumber,
        //                        candidate.AlternateMobileNumber,
        //                        candidate.Gender,
        //                        candidate.DateOfBirth,

        //                        candidate.CurrentAddress,
        //                        candidate.PermanentAddress,
        //                        candidate.CurrentCity,
        //                        candidate.State,
        //                        candidate.Country,
        //                        candidate.Pincode,

        //                        // Professional information
        //                        candidate.CurrentCompany,
        //                        candidate.CurrentDesignation,
        //                        candidate.EmploymentStatus,
        //                        candidate.TotalExperienceYears,
        //                        candidate.RelevantExperienceYears,
        //                        candidate.NoticePeriodDays,
        //                        candidate.PreviousCompany,
        //                        candidate.Industry,
        //                        candidate.FunctionalArea,
        //                        candidate.HighestQualification,
        //                        candidate.Specialization,
        //                        candidate.University,
        //                        candidate.PassingYear,
        //                        candidate.ResumePath,

        //                        // Compensation
        //                        application.Currency,
        //                        application.CurrentCTC,
        //                        application.CurrentFixedCTC,
        //                        application.CurrentVariableCTC,
        //                        application.ExpectedCTC,
        //                        application.ExpectedFixedCTC,
        //                        application.ExpectedVariableCTC,
        //                        application.CurrentMonthlySalary,
        //                        application.ExpectedMonthlySalary,
        //                        application.OfferedCTC,
        //                        application.IsNegotiable,

        //                        // Recruitment information
        //                        application.Source,
        //                        application.ReferredBy,

        //                        application.RecruiterEmployeeId,
        //                        RecruiterEmployeeName = GetEmployeeName(
        //                            application.RecruiterEmployeeId),

        //                        application.HiringManagerEmployeeId,
        //                        HiringManagerEmployeeName = GetEmployeeName(
        //                            application.HiringManagerEmployeeId),

        //                        application.RecruitmentCityId,
        //                        RecruitmentCityName = recruitmentCityName,

        //                        application.Cadre,
        //                        application.InterviewRequired,
        //                        application.PreferredInterviewDate,
        //                        application.PreferredLocation,
        //                        application.WillingToRelocate,
        //                        application.CandidateAvailability,
        //                        application.Remarks,

        //                        application.Stage,
        //                        // Preserve the frontend's existing ApplicationStatus field.
        //                        // Admin sees the stored status; role users see their own decision.
        //                        ApplicationStatus = accessUser.IsAdmin == true
        //                            ? application.Status
        //                            : ownRoleStatus,
        //                        application.AppliedOn,
        //                        application.IsActive,

        //                        CandidateIsActive = candidate.IsActive,

        //                        // Application audit
        //                        application.CreatedOn,
        //                        application.CreatedBy,
        //                        CreatedByName = GetEmployeeName(
        //                            application.CreatedBy),

        //                        application.ModifiedOn,
        //                        application.ModifiedBy,
        //                        ModifiedByName = GetEmployeeName(
        //                            application.ModifiedBy),

        //                        // Candidate master audit
        //                        CandidateCreatedOn = candidate.CreatedOn,
        //                        CandidateCreatedBy = candidate.CreatedBy,
        //                        CandidateCreatedByName = GetEmployeeName(
        //                            candidate.CreatedBy),

        //                        CandidateModifiedOn = candidate.ModifiedOn,
        //                        CandidateModifiedBy = candidate.ModifiedBy,
        //                        CandidateModifiedByName = GetEmployeeName(
        //                            candidate.ModifiedBy),

        //                        // Existing document response retained.
        //                        Documents = documentsByApplication[
        //                            application.CandidateApplicationId]
        //                            .Select(document => new
        //                            {
        //                                document.CandidateDocumentId,
        //                                document.CandidateApplicationId,
        //                                document.DocumentType,
        //                                document.OriginalFileName,

        //                                FilePath = document.StorageKey,

        //                                document.ContentType,
        //                                document.FileSizeBytes,
        //                                document.IsActive,

        //                                document.CreatedOn,
        //                                document.CreatedBy,

        //                                CreatedByName = GetEmployeeName(
        //                                    document.CreatedBy),

        //                                document.ModifiedOn,
        //                                document.ModifiedBy,

        //                                ModifiedByName = GetEmployeeName(
        //                                    document.ModifiedBy)
        //                            })
        //                            .ToList(),

        //                        // Additional response fields for your screen.
        //                        TemplateId = job?.TemplateId,

        //                        ApprovalHistoryCount = applicationHistory.Count,

        //                        ApprovalActionCount = applicationHistory.Count(x =>
        //                            IsDecision(x.Action)),

        //                        IsResume = isResume,
        //                        ResumeDetails = resumeDetails,
        //                        ResumeDetailsSource = resumeDetailsSource,
        //                        ResumeHistoryId = resumeHistoryId,

        //                        MyPermissions = myPermissions,
        //                        ApprovalPath = approvalPath,
        //                        UnassignedApprovalHistory = unassignedHistory,

        //                        HasPendingActionRequest = yourTask.Count > 0,
        //                        YourTask = yourTask
        //                    };
        //                }).ToList();

        //                return Ok(new
        //                {
        //                    Status = true,
        //                    Message = "Candidate list fetched successfully.",
        //                    TotalCount = data.Count,
        //                    Data = data
        //                });
        //            }
        //            catch (Exception ex)
        //            {
        //                Serilog.Log.Error(
        //                    ex,
        //                    "Failed to fetch candidates for CompanyId {CompanyId}.",
        //                    request.CompanyId);

        //                return StatusCode(500, new
        //                {
        //                    Status = false,
        //                    Message = "Unable to fetch candidate list."
        //                });
        //            }
        //        }







        public class GetCandidateListRequest
        {
            public int CompanyId { get; set; }
            public int User { get; set; }
            public List<int>? JobDescriptionIds { get; set; }

            public List<int>? BranchIds { get; set; }

            public List<int>? DepartmentIds { get; set; }
        }


        public class SaveCandidatesRequest
    {
        public int CompanyId { get; set; }

        public int JobDescriptionId { get; set; }

        public int? UserId { get; set; }

        public List<SaveCandidateRequest> Candidates { get; set; } = new();
    }

    public class SaveCandidateRequest
    {
        public int CandidateId { get; set; }

        public int CandidateApplicationId { get; set; }

        // Job placement
        public int BranchId { get; set; }

        public int DepartmentId { get; set; }

        // Personal information
        public string? FirstName { get; set; }
        public string? ResumePath { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }

        public string? Email { get; set; }
        public string? AlternateEmail { get; set; }

        public string? MobileNumber { get; set; }
        public string? AlternateMobileNumber { get; set; }

        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public string? CurrentAddress { get; set; }
        public string? PermanentAddress { get; set; }

        public string? CurrentCity { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }

        // Professional information
        public string? CurrentCompany { get; set; }
        public string? CurrentDesignation { get; set; }
        public string? EmploymentStatus { get; set; }

        public decimal? TotalExperienceYears { get; set; }
        public decimal? RelevantExperienceYears { get; set; }

        public int? NoticePeriodDays { get; set; }

        public string? PreviousCompany { get; set; }
        public string? Industry { get; set; }
        public string? FunctionalArea { get; set; }

        public string? HighestQualification { get; set; }
        public string? Specialization { get; set; }
        public string? University { get; set; }
        public int? PassingYear { get; set; }

        // Compensation: annual amounts, not LPA
        public string? Currency { get; set; } = "INR";

        public decimal? CurrentCTC { get; set; }
        public decimal? CurrentFixedCTC { get; set; }
        public decimal? CurrentVariableCTC { get; set; }

        public decimal? ExpectedCTC { get; set; }
        public decimal? ExpectedFixedCTC { get; set; }
        public decimal? ExpectedVariableCTC { get; set; }

        public decimal? CurrentMonthlySalary { get; set; }
        public decimal? ExpectedMonthlySalary { get; set; }

        public decimal? OfferedCTC { get; set; }
        public bool? IsNegotiable { get; set; }

        // Recruitment information
        public string? Source { get; set; }
        public string? ReferredBy { get; set; }

        public int? RecruiterEmployeeId { get; set; }
        public int? HiringManagerEmployeeId { get; set; }

        public int? RecruitmentCityId { get; set; }
        public string? Cadre { get; set; }

        public bool? InterviewRequired { get; set; }
        public DateTime? PreferredInterviewDate { get; set; }

        public string? PreferredLocation { get; set; }
        public bool? WillingToRelocate { get; set; }

        public string? CandidateAvailability { get; set; }

        public string? Remarks { get; set; }
        public string? VerificationRemarks { get; set; }

        public bool IsActive { get; set; } = true;
         
            public List<SaveCandidateDocumentRequest>? Documents { get; set; }
    }

    public class SaveCandidateDocumentRequest
    {
        public int CandidateDocumentId { get; set; }

        public string? DocumentType { get; set; }

        public string? OriginalFileName { get; set; }

        // Example: /Content/CandidateDocuments/ydhsbjkns.jpg
        public string? FilePath { get; set; }

        public string? ContentType { get; set; }

        public long? FileSizeBytes { get; set; }
    }
}
}












