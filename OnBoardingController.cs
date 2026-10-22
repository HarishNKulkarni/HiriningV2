using MailChimp.Net.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Recruitment.Data;
using Recruitment.Model.Entity;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;

namespace Recruitment.Controllers
{
    [Route("api/OnBoardingController")]
    [ApiController]
    public class OnBoardingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public OnBoardingController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            this._context = dbContext;
            _configuration = configuration;
            _configuration = configuration;
        }





      


        [HttpPost("SaveOnboardingTeam")]
        public async Task<IActionResult> SaveOnboardingTeam(
    [FromBody] SaveOnboardingTeamRequest? request)
        {
            IActionResult Fail(int statusCode, string message)
            {
                return StatusCode(statusCode, new
                {
                    Status = false,
                    Message = message
                });
            }

            if (request == null)
                return Fail(400, "Invalid request.");

            if (request.CompanyId <= 0 || request.UserId <= 0)
                return Fail(400, "Valid CompanyId and UserId are required.");

            if (request.OnboardingTeamId < 0)
                return Fail(400, "Invalid OnboardingTeamId.");

            var teamName = request.TeamName?.Trim();

            if (string.IsNullOrWhiteSpace(teamName))
                return Fail(400, "Team name is required.");

            if (teamName.Length > 150)
                return Fail(400, "Team name cannot exceed 150 characters.");

            if (request.CompanySkillMasterId.GetValueOrDefault() < 0)
                return Fail(400, "Invalid CompanySkillMasterId.");

            int? skillId = request.CompanySkillMasterId.GetValueOrDefault() == 0
                ? null
                : request.CompanySkillMasterId;

            var suppliedTemplateIds = request.TemplateIds ?? new List<int>();

            if (suppliedTemplateIds.Any(id => id < 0))
                return Fail(400, "TemplateIds cannot contain negative IDs.");

            // Avoid accidentally granting all-template access from a mixed list.
            if (suppliedTemplateIds.Contains(0) &&
                suppliedTemplateIds.Any(id => id > 0))
            {
                return Fail(
                    400,
                    "Send selected TemplateIds, or [0] for all templates. Do not mix them.");
            }

            if (suppliedTemplateIds.Distinct().Count() != suppliedTemplateIds.Count)
                return Fail(400, "Duplicate TemplateIds are not allowed.");

            var templateIds = suppliedTemplateIds
                .Where(id => id > 0)
                .ToList();

            if (request.Members == null || request.Members.Count == 0)
                return Fail(400, "At least one team member is required.");

            foreach (var member in request.Members)
            {
                if (member == null)
                    return Fail(400, "Team member cannot be null.");

                if (member.EmployeeId <= 0)
                    return Fail(400, "Every member must have a valid EmployeeId.");

                if (member.RoleId.GetValueOrDefault() < 0)
                    return Fail(400, "Invalid member RoleId.");

                if (member.RoleId == 0)
                    member.RoleId = null;
            }

            if (request.Members
                .GroupBy(x => x.EmployeeId)
                .Any(group => group.Count() > 1))
            {
                return Fail(400, "An employee cannot repeat within the same team.");
            }

            if (request.IsActive && !request.Members.Any(x => x.IsActive))
            {
                return Fail(
                    400,
                    "An active team must have at least one active member.");
            }

            bool isNew = request.OnboardingTeamId == 0;

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var companyExists = await _context.Companys
                    .AsNoTracking()
                    .AnyAsync(x => x.CompanyID == request.CompanyId);

                if (!companyExists)
                    return Fail(404, "Company not found.");

                var admin = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsActive,
                        x.IsAdmin
                    })
                    .FirstOrDefaultAsync();

                if (admin == null)
                    return Fail(404, "User not found for this company.");

                if (admin.IsActive != true)
                    return Fail(403, "User is inactive.");

                if (admin.IsAdmin != true)
                {
                    return Fail(
                        403,
                        "Only an admin can create or update onboarding teams.");
                }

                OnboardingTeam team;

                if (isNew)
                {
                    team = new OnboardingTeam
                    {
                        CompanyId = request.CompanyId
                    };
                }
                else
                {
                    var existingTeam = await _context.OnboardingTeams
                        .FirstOrDefaultAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.OnboardingTeamId == request.OnboardingTeamId);

                    if (existingTeam == null)
                        return Fail(404, "Onboarding team not found for this company.");

                    team = existingTeam;
                }

                // Case-insensitive uniqueness within the company.
                var duplicateName = await _context.OnboardingTeams
                    .AnyAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingTeamId != request.OnboardingTeamId &&
                        EF.Functions.Collate(
                            x.TeamName.Trim(),
                            "SQL_Latin1_General_CP1_CI_AS") == teamName);

                if (duplicateName)
                {
                    return Fail(
                        409,
                        "An onboarding team with this name already exists.");
                }

                // Validate the optional skill only when supplied.
                if (skillId.HasValue)
                {
                    var validSkill = await (
                        from skill in _context.CompanySkillMasters.AsNoTracking()

                        join category in _context.CompanySkillMappings.AsNoTracking()
                            on new
                            {
                                skill.CompanyId,
                                skill.SkillMappingId
                            }
                            equals new
                            {
                                category.CompanyId,
                                category.SkillMappingId
                            }

                        where skill.CompanyId == request.CompanyId
                              && skill.CompanySkillMasterId == skillId.Value
                              && skill.IsActive == true
                              && category.IsActive == true

                        select skill.CompanySkillMasterId
                    ).AnyAsync();

                    if (!validSkill)
                    {
                        return Fail(
                            400,
                            "Select an active company skill from an active category.");
                    }
                }

                // Empty selection means all templates; no mapping rows are inserted.
                if (templateIds.Count > 0)
                {
                    var validTemplateCount = await _context.RoleTemplates
                        .AsNoTracking()
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            templateIds.Contains(x.TemplateId) &&
                            x.IsActive == true);

                    if (validTemplateCount != templateIds.Count)
                    {
                        return Fail(
                            400,
                            "Select active templates belonging to this company.");
                    }
                }

                var employeeIds = request.Members
                    .Select(x => x.EmployeeId)
                    .ToList();

                var employees = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        employeeIds.Contains(x.EmployeeId))
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsActive
                    })
                    .ToDictionaryAsync(x => x.EmployeeId);

                if (employees.Count != employeeIds.Count)
                {
                    return Fail(
                        400,
                        "One or more employees do not belong to this company.");
                }

                foreach (var member in request.Members)
                {
                    if (member.IsActive &&
                        employees[member.EmployeeId].IsActive != true)
                    {
                        return Fail(
                            400,
                            $"Employee {member.EmployeeId} is inactive and cannot be an active team member.");
                    }
                }

                var roleIds = request.Members
                    .Where(x => x.RoleId.HasValue)
                    .Select(x => x.RoleId!.Value)
                    .Distinct()
                    .ToList();

                if (roleIds.Count > 0)
                {
                    var roles = await _context.Roles
                        .AsNoTracking()
                        .Where(x =>
                            x.CompanyId == request.CompanyId &&
                            roleIds.Contains(x.RoleId))
                        .Select(x => new
                        {
                            x.RoleId,
                            x.IsActive
                        })
                        .ToDictionaryAsync(x => x.RoleId);

                    if (roles.Count != roleIds.Count)
                    {
                        return Fail(
                            400,
                            "One or more roles do not belong to this company.");
                    }

                    foreach (var member in request.Members)
                    {
                        if (member.IsActive &&
                            member.RoleId.HasValue &&
                            roles[member.RoleId.Value].IsActive != true)
                        {
                            return Fail(
                                400,
                                $"Role {member.RoleId.Value} is inactive.");
                        }
                    }
                }

                var now = DateTime.UtcNow;

                team.TeamName = teamName;
                team.CompanySkillMasterId = skillId;
                team.IsActive = request.IsActive;

                if (isNew)
                {
                    team.CreatedBy = admin.EmployeeId;
                    team.CreatedOn = now;

                    _context.OnboardingTeams.Add(team);
                }
                else
                {
                    team.ModifiedBy = admin.EmployeeId;
                    team.ModifiedOn = now;
                }

                // Generate the team ID before inserting related rows.
                await _context.SaveChangesAsync();

                var existingMembers = await _context.OnboardingTeamMembers
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingTeamId == team.OnboardingTeamId)
                    .ToListAsync();

                var existingMembersByEmployee = existingMembers
                    .ToDictionary(x => x.EmployeeId);

                var submittedEmployeeIds = employeeIds.ToHashSet();

                // Full replacement semantics:
                // omitted members are deactivated, not deleted.
                foreach (var existingMember in existingMembers)
                {
                    if (!submittedEmployeeIds.Contains(existingMember.EmployeeId) &&
                        existingMember.IsActive)
                    {
                        existingMember.IsActive = false;
                        existingMember.ModifiedBy = admin.EmployeeId;
                        existingMember.ModifiedOn = now;
                    }
                }

                foreach (var member in request.Members)
                {
                    if (existingMembersByEmployee.TryGetValue(
                        member.EmployeeId, out var existingMember))
                    {
                        existingMember.RoleId = member.RoleId;
                        existingMember.IsActive = member.IsActive;
                        existingMember.ModifiedBy = admin.EmployeeId;
                        existingMember.ModifiedOn = now;
                    }
                    else
                    {
                        _context.OnboardingTeamMembers.Add(
                            new OnboardingTeamMembers
                            {
                                CompanyId = request.CompanyId,
                                OnboardingTeamId = team.OnboardingTeamId,
                                EmployeeId = member.EmployeeId,
                                RoleId = member.RoleId,
                                IsActive = member.IsActive,
                                CreatedBy = admin.EmployeeId,
                                CreatedOn = now
                            });
                    }
                }

                var existingTemplates = await _context.OnboardingTeamTemplates
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingTeamId == team.OnboardingTeamId)
                    .ToListAsync();

                var selectedTemplateIds = templateIds.ToHashSet();

                // Remove only mappings no longer selected.
                var removedTemplates = existingTemplates
                    .Where(x => !selectedTemplateIds.Contains(x.TemplateId))
                    .ToList();

                _context.OnboardingTeamTemplates.RemoveRange(removedTemplates);

                var existingTemplateIds = existingTemplates
                    .Select(x => x.TemplateId)
                    .ToHashSet();

                foreach (var templateId in templateIds)
                {
                    if (!existingTemplateIds.Contains(templateId))
                    {
                        _context.OnboardingTeamTemplates.Add(
                            new OnboardingTeamTemplate
                            {
                                CompanyId = request.CompanyId,
                                OnboardingTeamId = team.OnboardingTeamId,
                                TemplateId = templateId,
                                CreatedBy = admin.EmployeeId,
                                CreatedOn = now
                            });
                    }
                }

                await _context.SaveChangesAsync();

                var savedMembers = await _context.OnboardingTeamMembers
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.OnboardingTeamId == team.OnboardingTeamId)
                    .OrderBy(x => x.EmployeeId)
                    .Select(x => new
                    {
                        x.OnboardingTeamMemberId,
                        x.EmployeeId,
                        x.RoleId,
                        x.IsActive,
                        x.CreatedOn,
                        x.CreatedBy,
                        x.ModifiedOn,
                        x.ModifiedBy
                    })
                    .ToListAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = isNew
                        ? "Onboarding team created successfully."
                        : "Onboarding team updated successfully.",

                    Data = new
                    {
                        team.OnboardingTeamId,
                        team.CompanyId,
                        team.TeamName,
                        team.CompanySkillMasterId,
                        team.IsActive,

                        AllTemplates = templateIds.Count == 0,
                        TemplateIds = templateIds.OrderBy(x => x).ToList(),

                        ActiveMemberCount = savedMembers.Count(x => x.IsActive),
                        Members = savedMembers,

                        team.CreatedOn,
                        team.CreatedBy,
                        team.ModifiedOn,
                        team.ModifiedBy
                    }
                });
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                Serilog.Log.Warning(
                    ex,
                    "Duplicate onboarding team or mapping for CompanyId {CompanyId}.",
                    request.CompanyId);

                return Fail(
                    409,
                    "A duplicate team name, member, or template mapping already exists.");
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      sql.Number == 547)
            {
                Serilog.Log.Warning(
                    ex,
                    "Invalid onboarding relationship for CompanyId {CompanyId}.",
                    request.CompanyId);

                return Fail(
                    409,
                    "A related record is invalid or has changed. Refresh and try again.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to save onboarding team {TeamId} for CompanyId {CompanyId}.",
                    request.OnboardingTeamId,
                    request.CompanyId);

                return Fail(500, "Unable to save onboarding team.");
            }
        }


        [HttpPost("GetOnboardingTeamList")]
        public async Task<IActionResult> GetOnboardingTeamList(
    [FromBody] GetOnboardingTeamListRequest? request)
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
                return Fail(400, "Valid CompanyId and UserId are required.");
            }

            try
            {
                var user = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsAdmin,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "User not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "User is inactive.");

                bool isAdmin = user.IsAdmin == true;

                var teamsQuery = _context.OnboardingTeams
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId);

                if (!isAdmin)
                {
                    teamsQuery = teamsQuery.Where(team =>
                        team.IsActive &&
                        _context.OnboardingTeamMembers.Any(member =>
                            member.CompanyId == team.CompanyId &&
                            member.OnboardingTeamId == team.OnboardingTeamId &&
                            member.EmployeeId == request.UserId &&
                            member.IsActive));
                }

                var teams = await teamsQuery
                    .OrderByDescending(x => x.OnboardingTeamId)
                    .ToListAsync();

                var teamIds = teams
                    .Select(x => x.OnboardingTeamId)
                    .ToArray();

                var membersQuery = _context.OnboardingTeamMembers
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        teamIds.Contains(x.OnboardingTeamId));

                // Admin can see inactive memberships for management.
                if (!isAdmin)
                    membersQuery = membersQuery.Where(x => x.IsActive);

                var members = await membersQuery
                    .OrderBy(x => x.EmployeeId)
                    .ToListAsync();

                // Keep all saved template restrictions, even if a template
                // later becomes inactive. Do not interpret that as "all templates".
                var templateMappings = await _context.OnboardingTeamTemplates
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        teamIds.Contains(x.OnboardingTeamId))
                    .ToListAsync();

                var templateIds = templateMappings
                    .Select(x => x.TemplateId)
                    .Distinct()
                    .ToArray();

                var templates = await _context.RoleTemplates
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        templateIds.Contains(x.TemplateId))
                    .Select(x => new
                    {
                        x.TemplateId,
                        x.TemplateName,
                        x.IsActive
                    })
                    .ToDictionaryAsync(x => x.TemplateId);

                var skillIds = teams
                    .Where(x => x.CompanySkillMasterId.HasValue)
                    .Select(x => x.CompanySkillMasterId!.Value)
                    .Distinct()
                    .ToArray();

                var skills = await _context.CompanySkillMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        skillIds.Contains(x.CompanySkillMasterId))
                    .Select(x => new
                    {
                        x.CompanySkillMasterId,
                        x.SkillName,
                        x.IsActive,
                        x.IsDisable
                    })
                    .ToDictionaryAsync(x => x.CompanySkillMasterId);

                var roleIds = members
                    .Where(x => x.RoleId.HasValue)
                    .Select(x => x.RoleId!.Value)
                    .Distinct()
                    .ToArray();

                var roles = await _context.Roles
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        roleIds.Contains(x.RoleId))
                    .Select(x => new
                    {
                        x.RoleId,
                        x.RoleName,
                        x.IsActive
                    })
                    .ToDictionaryAsync(x => x.RoleId);

                var employeeIds = members
                    .Select(x => (int?)x.EmployeeId)
                    .Concat(teams.SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    }))
                    .Concat(members.SelectMany(x => new int?[]
                    {
                x.CreatedBy,
                x.ModifiedBy
                    }))
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToArray();

                var employees = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        employeeIds.Contains(x.EmployeeId))
                    .Select(x => new
                    {
                        x.EmployeeId,
                        EmployeeName =
                            ((x.FirstName ?? "") + " " +
                             (x.LastName ?? "")).Trim(),
                        x.IsActive
                    })
                    .ToDictionaryAsync(x => x.EmployeeId);

                string? GetEmployeeName(int? employeeId)
                {
                    return employeeId.HasValue &&
                           employees.TryGetValue(employeeId.Value, out var employee)
                        ? employee.EmployeeName
                        : null;
                }

                var membersByTeam = members.ToLookup(
                    x => x.OnboardingTeamId);

                var templatesByTeam = templateMappings.ToLookup(
                    x => x.OnboardingTeamId);

                var data = teams.Select(team =>
                {
                    var teamMembers = membersByTeam[team.OnboardingTeamId]
                        .ToList();

                    var teamTemplates = templatesByTeam[team.OnboardingTeamId]
                        .OrderBy(x => x.TemplateId)
                        .ToList();

                    var skill = team.CompanySkillMasterId.HasValue &&
                                skills.TryGetValue(
                                    team.CompanySkillMasterId.Value,
                                    out var matchedSkill)
                        ? matchedSkill
                        : null;

                    return new
                    {
                        team.OnboardingTeamId,
                        team.CompanyId,
                        team.TeamName,

                        team.CompanySkillMasterId,
                        SkillName = skill?.SkillName,
                        SkillIsActive = skill?.IsActive,
                        SkillIsDisable = skill?.IsDisable,

                        team.IsActive,

                        AllTemplates = teamTemplates.Count == 0,

                        TemplateIds = teamTemplates
                            .Select(x => x.TemplateId)
                            .ToList(),

                        Templates = teamTemplates.Select(mapping =>
                        {
                            templates.TryGetValue(
                                mapping.TemplateId, out var template);

                            return new
                            {
                                mapping.OnboardingTeamTemplateId,
                                mapping.TemplateId,
                                TemplateName = template?.TemplateName,
                                IsActive = template?.IsActive,
                                mapping.CreatedOn,
                                mapping.CreatedBy
                            };
                        }).ToList(),

                        MemberCount = teamMembers.Count,

                        // Membership flag only.
                        ActiveMemberCount = teamMembers.Count(x => x.IsActive),

                        Members = teamMembers.Select(member =>
                        {
                            employees.TryGetValue(
                                member.EmployeeId, out var employee);

                            var role = member.RoleId.HasValue &&
                                       roles.TryGetValue(
                                           member.RoleId.Value, out var matchedRole)
                                ? matchedRole
                                : null;

                            return new
                            {
                                member.OnboardingTeamMemberId,
                                member.EmployeeId,
                                EmployeeName = employee?.EmployeeName,
                                EmployeeIsActive = employee?.IsActive,

                                member.RoleId,
                                RoleName = role?.RoleName,
                                RoleIsActive = role?.IsActive,

                                member.IsActive,

                                member.CreatedOn,
                                member.CreatedBy,
                                CreatedByName = GetEmployeeName(member.CreatedBy),

                                member.ModifiedOn,
                                member.ModifiedBy,
                                ModifiedByName = GetEmployeeName(member.ModifiedBy)
                            };
                        }).ToList(),

                        team.CreatedOn,
                        team.CreatedBy,
                        CreatedByName = GetEmployeeName(team.CreatedBy),

                        team.ModifiedOn,
                        team.ModifiedBy,
                        ModifiedByName = GetEmployeeName(team.ModifiedBy)
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Onboarding teams fetched successfully."
                        : "No onboarding teams found.",

                    request.CompanyId,
                    request.UserId,
                    IsAdmin = isAdmin,

                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch onboarding teams for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return Fail(500, "Unable to fetch onboarding teams.");
            }
        }








// Place this DTO outside your controller.
public class GetOnboardingCandidateListRequest
    {
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public List<int>? BranchIds { get; set; }
        public List<int>? DepartmentIds { get; set; }
}

// Place the following method INSIDE your existing controller.
// Uses your existing _context and entity DbSets.
[HttpPost("GetOnboardingCandidateList")]
        public async Task<IActionResult> GetOnboardingCandidateList(
    [FromBody] GetOnboardingCandidateListRequest? request)
        {
            IActionResult Fail(int code, string message) =>
                StatusCode(code, new { Status = false, Message = message });

            if (request == null || request.CompanyId <= 0 || request.UserId <= 0)
                return Fail(400, "Valid CompanyId and UserId are required.");

            if (request.BranchIds?.Any(x => x < 0) == true ||
                request.DepartmentIds?.Any(x => x < 0) == true)
                return Fail(400, "Filter IDs cannot be negative.");

            static int[] FilterIds(List<int>? ids) =>
                ids == null || ids.Count == 0 || ids.Contains(0)
                    ? Array.Empty<int>()
                    : ids.Distinct().ToArray();

            static bool IsDecision(string? action) =>
                action == "Approve" || action == "Reject" || action == "Return";

            try
            {
                var user = await _context.EmployeeMasters.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                x.EmployeeId == request.UserId)
                    .Select(x => new { x.EmployeeId, x.IsAdmin, x.IsActive })
                    .FirstOrDefaultAsync();

                if (user == null) return Fail(404, "User not found for this company.");
                if (user.IsActive != true) return Fail(403, "User is inactive.");

                bool isAdmin = user.IsAdmin == true;
                bool allTemplates = isAdmin;
                var allowedTemplateIds = new HashSet<int>();

                if (!isAdmin)
                {
                    var teamIds = await (
                        from member in _context.OnboardingTeamMembers.AsNoTracking()
                        join team in _context.OnboardingTeams.AsNoTracking()
                            on new { member.CompanyId, member.OnboardingTeamId }
                            equals new { team.CompanyId, team.OnboardingTeamId }
                        where member.CompanyId == request.CompanyId &&
                              member.EmployeeId == request.UserId &&
                              member.IsActive && team.IsActive
                        select team.OnboardingTeamId
                    ).Distinct().ToListAsync();

                    if (teamIds.Count == 0)
                        return Fail(403, "An active onboarding team assignment is required.");

                    // Read every mapping, including references to inactive templates.
                    // Missing/invalid targets must never broaden access to all templates.
                    var scopes = await _context.OnboardingTeamTemplates.AsNoTracking()
                        .Where(x => x.CompanyId == request.CompanyId &&
                                    teamIds.Contains(x.OnboardingTeamId))
                        .Select(x => new { x.OnboardingTeamId, x.TemplateId })
                        .ToListAsync();

                    var restrictedTeams = scopes.Select(x => x.OnboardingTeamId).ToHashSet();
                    allTemplates = teamIds.Any(id => !restrictedTeams.Contains(id));
                    allowedTemplateIds = scopes.Select(x => x.TemplateId).ToHashSet();
                }

                var branchIds = FilterIds(request.BranchIds);
                var departmentIds = FilterIds(request.DepartmentIds);
                var allowedIds = allowedTemplateIds.ToArray();

                var query =
                    from application in _context.CandidateApplications.AsNoTracking()
                    join candidate in _context.Candidates.AsNoTracking()
                        on new { application.CompanyId, application.CandidateId }
                        equals new { candidate.CompanyId, candidate.CandidateId }
                    join job in _context.JobDescriptions.AsNoTracking()
                        on new { application.CompanyId, application.JobDescriptionId }
                        equals new { job.CompanyId, job.JobDescriptionId }
                    join template in _context.RoleTemplates.AsNoTracking()
                        on new { job.CompanyId, TemplateId = (int?)job.TemplateId }
                        equals new { template.CompanyId, TemplateId = (int?)template.TemplateId }
                    where application.CompanyId == request.CompanyId &&
                          application.IsActive == true &&
                          candidate.IsActive == true &&
                          template.IsActive == true
                    select new
                    {
                        Application = application,
                        Candidate = candidate,
                        job.JobTitle,
                        template.TemplateId,
                        template.TemplateName
                    };

                if (!allTemplates)
                    query = query.Where(x => allowedIds.Contains(x.TemplateId));

                if (branchIds.Length > 0)
                    query = query.Where(x => x.Application.BranchId.HasValue &&
                        branchIds.Contains(x.Application.BranchId.Value));

                if (departmentIds.Length > 0)
                    query = query.Where(x => x.Application.DepartmentId.HasValue &&
                        departmentIds.Contains(x.Application.DepartmentId.Value));

                var rows = await query
                    .OrderByDescending(x => x.Application.CandidateApplicationId)
                    .ToListAsync();

                var applicationIds = rows.Select(x => x.Application.CandidateApplicationId)
                    .Distinct().ToArray();
                var templateIds = rows.Select(x => x.TemplateId).Distinct().ToArray();

                var assignments = await _context.RoleTemplateAssignment.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                templateIds.Contains(x.TemplateId) &&
                                x.IsActive == true &&
                                x.CanApprove == true)
                    .Select(x => new
                    {
                        x.AssignmentId,
                        x.TemplateId,
                        x.EmployeeId,
                        x.HierarchyLevel
                    })
                    .ToListAsync();

                var assignmentIds = assignments.Select(x => x.AssignmentId).ToArray();

                var decisions = await _context.CandidateSkillAssessmentHistories
                    .AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                applicationIds.Contains(x.CandidateApplicationId) &&
                                x.AssignmentId.HasValue &&
                                assignmentIds.Contains(x.AssignmentId.Value) &&
                                (x.Action == "Approve" ||
                                 x.Action == "Reject" ||
                                 x.Action == "Return"))
                    .ToListAsync();

                // Group per application, job and assignment. Author must be the assigned
                // employee; an admin action under someone else's assignment does not
                // stand in for that employee's approval.
                var decisionsByAssignment = decisions.ToLookup(x => (
                    x.CandidateApplicationId, x.JobDescriptionId, x.AssignmentId!.Value));

                var assignmentsByTemplate = assignments.ToLookup(x => x.TemplateId);

                var eligibleRows = rows.Where(row =>
                {
                    var required = assignmentsByTemplate[row.TemplateId].ToList();
                    if (required.Count == 0) return false;

                    return required.All(assignment =>
                    {
                        var latest = decisionsByAssignment[(
                            row.Application.CandidateApplicationId,
                            row.Application.JobDescriptionId,
                            assignment.AssignmentId)]
                            .Where(x => x.CreatedBy == assignment.EmployeeId &&
                                        IsDecision(x.Action))
                            .OrderByDescending(x => x.CreatedOn)
                            .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
                            .FirstOrDefault();

                        return latest?.Action == "Approve";
                    });
                }).ToList();

                var eligibleApplicationIds = eligibleRows
                    .Select(x => x.Application.CandidateApplicationId).ToArray();

                // Includes completed/closed onboarding records for dashboard outcomes.
                var onboardings = await _context.CandidateOnboardings.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                eligibleApplicationIds.Contains(x.CandidateApplicationId))
                    .ToListAsync();

                var onboardingByApplication = onboardings.ToDictionary(
                    x => x.CandidateApplicationId);

                var onboardingIds = onboardings.Select(x => x.CandidateOnboardingId).ToArray();

                var documents = await _context.CandidateOnboardingDocuments.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                onboardingIds.Contains(x.CandidateOnboardingId) &&
                                x.IsActive)
                    .ToListAsync();

                var policies = await _context.CandidateOnboardingPolicies.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                onboardingIds.Contains(x.CandidateOnboardingId) &&
                                x.IsActive)
                    .ToListAsync();

                var offers = await _context.CandidateOnboardingOffers.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                onboardingIds.Contains(x.CandidateOnboardingId) &&
                                x.IsCurrent)
                    .ToListAsync();

                var docsByOnboarding = documents.ToLookup(x => x.CandidateOnboardingId);
                var policiesByOnboarding = policies.ToLookup(x => x.CandidateOnboardingId);
                // A unique filtered index should enforce one current offer.
                var offersByOnboarding = offers.ToLookup(x => x.CandidateOnboardingId);

                var resultBranchIds = eligibleRows
                    .Where(x => x.Application.BranchId.HasValue)
                    .Select(x => x.Application.BranchId!.Value).Distinct().ToArray();
                var resultDepartmentIds = eligibleRows
                    .Where(x => x.Application.DepartmentId.HasValue)
                    .Select(x => x.Application.DepartmentId!.Value).Distinct().ToArray();

                var branches = await _context.Branches.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                resultBranchIds.Contains(x.BranchId))
                    .ToDictionaryAsync(x => x.BranchId, x => x.BranchName);

                var departments = await _context.Departments.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                                resultDepartmentIds.Contains(x.DepartmentId))
                    .ToDictionaryAsync(x => x.DepartmentId, x => x.DepartmentName);

                var data = eligibleRows.Select(row =>
                {
                    var application = row.Application;
                    var candidate = row.Candidate;
                    onboardingByApplication.TryGetValue(
                        application.CandidateApplicationId, out var onboarding);

                    int onboardingId = onboarding?.CandidateOnboardingId ?? 0;
                    var docs = docsByOnboarding[onboardingId].ToList();
                    var candidatePolicies = policiesByOnboarding[onboardingId].ToList();
                    var currentOffer = offersByOnboarding[onboardingId]
                        .OrderByDescending(x => x.VersionNumber)
                        .ThenByDescending(x => x.CandidateOnboardingOfferId)
                        .FirstOrDefault();

                    int submittedCount = docs.Count(x =>
                        x.Status == "Submitted" || x.Status == "Verified");
                    int verifiedCount = docs.Count(x => x.Status == "Verified");
                    int mandatoryCount = docs.Count(x => x.IsMandatory);

                    // No document requests must not imply verification is complete.
                    // If only optional requests exist, require all of them verified
                    // for this dashboard's "Verified Candidates" classification.
                    bool documentsVerified = docs.Count > 0 &&
                        (mandatoryCount > 0
                            ? docs.Where(x => x.IsMandatory).All(x => x.Status == "Verified")
                            : docs.All(x => x.Status == "Verified"));

                    string policyStatus = candidatePolicies.Count == 0
                        ? "NotSent"
                        : candidatePolicies.Any(x => x.IsAcknowledged == false)
                            ? "Declined"
                            : candidatePolicies.All(x => x.IsAcknowledged == true)
                                ? "Acknowledged"
                                : candidatePolicies.Any(x => x.SharedOn.HasValue)
                                    ? "AwaitingAcknowledgment"
                                    : "NotSent";

                    bool joined = onboarding?.FinalOutcome == "Joined";
                    bool closed = joined ||
                        onboarding?.FinalOutcome == "Rejected" ||
                        onboarding?.FinalOutcome == "OfferDeclined";

                    bool activeOnboarding = onboarding == null || onboarding.IsActive;

                    string? branchName = null;
                    string? departmentName = null;
                    if (application.BranchId.HasValue)
                        branches.TryGetValue(application.BranchId.Value, out branchName);
                    if (application.DepartmentId.HasValue)
                        departments.TryGetValue(application.DepartmentId.Value, out departmentName);

                    return new
                    {
                        application.CompanyId,
                        candidate.CandidateId,
                        application.CandidateApplicationId,
                        application.JobDescriptionId,
                        row.JobTitle,
                        row.TemplateId,
                        row.TemplateName,

                        CandidateName = string.Join(" ", new[]
                        {
                    candidate.FirstName, candidate.MiddleName, candidate.LastName
                }.Where(x => !string.IsNullOrWhiteSpace(x))),
                        candidate.Email,
                        candidate.MobileNumber,

                        application.BranchId,
                        BranchName = branchName,
                        application.DepartmentId,
                        DepartmentName = departmentName,

                        CandidateOnboardingId = (int?)onboarding?.CandidateOnboardingId,
                        OnboardingStatus = onboarding?.Status ?? "ReadyForOnboarding",
                        OnboardingIsActive = activeOnboarding,
                        AllHierarchyApproved = true,
                        RequiredApprovalCount = assignmentsByTemplate[row.TemplateId].Count(),

                        // Eligibility/action flag, separate from completed joining.
                        IsOnBoarding = activeOnboarding && !closed,
                        IsJoined = joined,
                        IsPending = activeOnboarding && !closed,
                        IsVerified = documentsVerified,

                        DocumentStatus = submittedCount > 0
                            ? "Submitted" : "RequestDocuments",
                        DocumentStatusText = submittedCount > 0
                            ? "Submitted" : "Request Documents",
                        RequestedDocumentCount = docs.Count,
                        SubmittedDocumentCount = submittedCount,
                        VerifiedDocumentCount = verifiedCount,
                        MandatoryDocumentCount = mandatoryCount,
                        ResubmissionRequiredCount = docs.Count(x =>
                            x.Status == "ResubmissionRequired"),

                        CompanyPolicyStatus = policyStatus,
                        PolicyCount = candidatePolicies.Count,
                        AcknowledgedPolicyCount = candidatePolicies.Count(x =>
                            x.IsAcknowledged == true),

                        OfferStatus = currentOffer?.Status ?? "NotIssued",
                        OfferResponse = currentOffer?.CandidateResponse,
                        CandidateOnboardingOfferId =
                            (int?)currentOffer?.CandidateOnboardingOfferId,

                        OnboardedStatus = joined ? "Joined"
                            : onboarding?.FinalOutcome ?? "NotYet",

                        ExpectedJoiningDate = onboarding?.ExpectedJoiningDate
                            ?? currentOffer?.ExpectedJoiningDate,
                        ActualJoiningDate = onboarding?.ActualJoiningDate,
                        FinalOutcome = onboarding?.FinalOutcome,
                        OutcomeRemarks = onboarding?.OutcomeRemarks,
                        application.AppliedOn
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Onboarding candidates fetched successfully."
                        : "No fully approved candidates found within your scope.",
                    request.CompanyId,
                    request.UserId,
                    IsAdmin = isAdmin,
                    AllTemplates = allTemplates,
                    Summary = new
                    {
                        TotalCandidate = data.Count,
                        VerifiedCandidate = data.Count(x => x.IsVerified),
                        PendingCandidate = data.Count(x => x.IsPending),
                        JoinedCandidate = data.Count(x => x.IsJoined)
                    },
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex,
                    "Failed to fetch onboarding candidates for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId, request.UserId);
                return Fail(500, "Unable to fetch onboarding candidates.");
            }
        }







      

// DTOs go outside your controller.


//// Place INSIDE your existing controller.
//[HttpPost("SaveOnboardingDocument")]
//        public async Task<IActionResult> SaveOnboardingDocument([FromBody] SaveOnboardingDocumentRequest? request)
//        {
//            IActionResult Fail(int code, string message) =>
//                StatusCode(code, new { Status = false, Message = message });

//            if (request == null || request.CompanyId <= 0 || request.UserId <= 0 ||
//                request.CandidateId <= 0 || request.JobDescriptionId <= 0 ||
//                request.CandidateApplicationId < 0)
//                return Fail(400, "Provide valid company, user, candidate and job IDs.");

//            if (request.Documents == null || request.Documents.Count == 0)
//                return Fail(400, "At least one document request is required.");

//            foreach (var item in request.Documents)
//            {
//                if (item == null)
//                    return Fail(400, "Document request cannot be null.");
//                if (item.CandidateOnboardingDocumentId < 0)
//                    return Fail(400, "Invalid document ID.");
//                if (item.AllocatedDays <= 0 || item.AllocatedDays > 365)
//                    return Fail(400, "AllocatedDays must be between 1 and 365.");

//                item.DocumentName = item.DocumentName?.Trim();
//                item.Instructions = item.Instructions?.Trim();

//                if (string.IsNullOrWhiteSpace(item.DocumentName))
//                    return Fail(400, "DocumentName is required.");
//                if (item.DocumentName?.Length > 200)
//                    return Fail(400, "DocumentName cannot exceed 200 characters.");
//                if (item.CandidateOnboardingDocumentId > 0 &&
//                    (item.RowVersion == null || item.RowVersion.Length != 8))
//                    return Fail(400, "Send the current RowVersion when editing a document request.");
//            }

//            if (request.Documents.Where(x => x.CandidateOnboardingDocumentId > 0)
//                .GroupBy(x => x.CandidateOnboardingDocumentId).Any(g => g.Count() > 1))
//                return Fail(400, "A document request ID cannot repeat.");

//            try
//            {
//                await using var transaction = await _context.Database
//                    .BeginTransactionAsync(IsolationLevel.Serializable);

//                var user = await _context.EmployeeMasters.AsNoTracking()
//                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
//                        x.EmployeeId == request.UserId);
//                if (user == null) return Fail(404, "Employee not found for this company.");
//                if (user.IsActive != true) return Fail(403, "Employee is inactive.");

//                var candidatesExist = await _context.Candidates.AsNoTracking()
//                    .AnyAsync(x => x.CompanyId == request.CompanyId &&
//                        x.CandidateId == request.CandidateId && x.IsActive == true);
//                if (!candidatesExist) return Fail(404, "Active candidate not found.");

//                var job = await _context.JobDescriptions.AsNoTracking()
//                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
//                        x.JobDescriptionId == request.JobDescriptionId);
//                if (job == null) return Fail(404, "Job not found for this company.");

//                int? templateId = job.TemplateId;
//                if (!templateId.HasValue || !await _context.RoleTemplates.AsNoTracking()
//                    .AnyAsync(x => x.CompanyId == request.CompanyId &&
//                        x.TemplateId == templateId.Value && x.IsActive == true))
//                    return Fail(409, "The job must have an active recruitment template.");

//                var applicationsQuery = _context.CandidateApplications.Where(x =>
//                    x.CompanyId == request.CompanyId &&
//                    x.CandidateId == request.CandidateId &&
//                    x.JobDescriptionId == request.JobDescriptionId);
//                if (request.CandidateApplicationId > 0)
//                    applicationsQuery = applicationsQuery.Where(x =>
//                        x.CandidateApplicationId == request.CandidateApplicationId);
//                var applications = await applicationsQuery.Take(2).ToListAsync();
//                if (applications.Count == 0) return Fail(404, "Candidate application not found.");
//                if (applications.Count > 1)
//                    return Fail(409, "Multiple applications exist. Send CandidateApplicationId.");
//                var application = applications[0];
//                if (application.IsActive != true)
//                    return Fail(409, "Candidate application is inactive.");

//                if (user.IsAdmin != true)
//                {
//                    var teamIds = await (
//                        from member in _context.OnboardingTeamMembers.AsNoTracking()
//                        join team in _context.OnboardingTeams.AsNoTracking()
//                            on new { member.CompanyId, member.OnboardingTeamId }
//                            equals new { team.CompanyId, team.OnboardingTeamId }
//                        where member.CompanyId == request.CompanyId &&
//                            member.EmployeeId == request.UserId &&
//                            member.IsActive && team.IsActive
//                        select team.OnboardingTeamId
//                    ).Distinct().ToListAsync();
//                    var scopes = await _context.OnboardingTeamTemplates.AsNoTracking()
//                        .Where(x => x.CompanyId == request.CompanyId &&
//                            teamIds.Contains(x.OnboardingTeamId)).ToListAsync();
//                    bool allowed = teamIds.Any(id =>
//                        !scopes.Any(x => x.OnboardingTeamId == id) ||
//                        scopes.Any(x => x.OnboardingTeamId == id &&
//                            x.TemplateId == templateId.Value));
//                    if (!allowed)
//                        return Fail(403, "You are not assigned to onboarding for this template.");
//                }

//                // Same eligibility rule as GetOnboardingCandidateList.
//                var assignments = await _context.RoleTemplateAssignment.AsNoTracking()
//                    .Where(x => x.CompanyId == request.CompanyId &&
//                        x.TemplateId == templateId.Value && x.IsActive == true &&
//                        x.CanApprove == true).ToListAsync();
//                if (assignments.Count == 0)
//                    return Fail(409, "The template has no active approval assignments.");

//                var assignmentIds = assignments.Select(x => x.AssignmentId).ToArray();
//                var decisions = await _context.CandidateSkillAssessmentHistories.AsNoTracking()
//                    .Where(x => x.CompanyId == request.CompanyId &&
//                        x.CandidateApplicationId == application.CandidateApplicationId &&
//                        x.JobDescriptionId == request.JobDescriptionId &&
//                        x.AssignmentId.HasValue && assignmentIds.Contains(x.AssignmentId.Value) &&
//                        (x.Action == "Approve" || x.Action == "Reject" || x.Action == "Return"))
//                    .ToListAsync();
//                if (assignments.Any(a =>
//                    decisions.Where(x => x.AssignmentId == a.AssignmentId &&
//                            x.CreatedBy == a.EmployeeId)
//                        .OrderByDescending(x => x.CreatedOn)
//                        .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
//                        .FirstOrDefault()?.Action != "Approve"))
//                    return Fail(409, "All required hierarchy approvals must be completed.");

//                var onboarding = await _context.CandidateOnboardings
//                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
//                        x.CandidateApplicationId == application.CandidateApplicationId);

//                if (onboarding != null &&
//                    (!onboarding.IsActive || !string.IsNullOrWhiteSpace(onboarding.FinalOutcome)))
//                    return Fail(409, "This onboarding case is inactive or already closed.");

//                // Do not reopen later completed stages by silently adding new requirements.
//                if (onboarding != null &&
//                    onboarding.Status != "ReadyForOnboarding" &&
//                    onboarding.Status != "DocumentsPending")
//                    return Fail(409, "Document requests can be changed only before advancing beyond the document stage.");

//                var existing = onboarding == null
//                    ? new List<CandidateOnboardingDocuments>()
//                    : await _context.CandidateOnboardingDocuments
//                        .Where(x => x.CompanyId == request.CompanyId &&
//                            x.CandidateOnboardingId == onboarding.CandidateOnboardingId)
//                        .ToListAsync();
//                var existingById = existing.ToDictionary(x => x.CandidateOnboardingDocumentId);

//                foreach (var item in request.Documents)
//                {
//                    CandidateOnboardingDocuments? saved = null;
//                    if (item.CandidateOnboardingDocumentId > 0)
//                    {
//                        if (!existingById.TryGetValue(item.CandidateOnboardingDocumentId, out saved))
//                            return Fail(404, "Document request not found for this onboarding case.");
//                        if (!saved.IsActive || saved.Status != "Requested")
//                            return Fail(409, "Only active, unsubmitted document requests can be edited here.");
//                        if (!saved.RowVersion.SequenceEqual(item.RowVersion!))
//                            return Fail(409, "Document request changed. Refresh and try again.");
//                    }


//                    if (string.IsNullOrWhiteSpace(item.DocumentName) || item.DocumentName.Length > 200)
//                        return Fail(400, "Every document must have a valid name up to 200 characters.");
//                }

//                // Validate the final combined selection. Omitted rows remain unchanged.
//                var editedIds = request.Documents.Where(x => x.CandidateOnboardingDocumentId > 0)
//                    .Select(x => x.CandidateOnboardingDocumentId).ToHashSet();
//                var finalNames = existing.Where(x => x.IsActive && !editedIds.Contains(x.CandidateOnboardingDocumentId))
//                    .Select(x => x.DocumentName.Trim())
//                    .Concat(request.Documents.Select(x => x.DocumentName!));
//                if (finalNames.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
//                    return Fail(409, "An active request for this document name already exists.");

//                var now = DateTime.UtcNow;
//                bool isNewOnboarding = onboarding == null;
//                if (onboarding == null)
//                {
//                    onboarding = new CandidateOnboardings
//                    {
//                        CompanyId = request.CompanyId,
//                        CandidateApplicationId = application.CandidateApplicationId,
//                        TemplateIdAtStart = templateId,
//                        Status = "ReadyForOnboarding",
//                        IsActive = true,
//                        CreatedBy = request.UserId,
//                        CreatedOn = now
//                    };
//                    _context.CandidateOnboardings.Add(onboarding);
//                    await _context.SaveChangesAsync();
//                }

//                string Snapshot(CandidateOnboardingDocuments document) =>
//                    JsonSerializer.Serialize(new
//                    {
//                        document.CandidateOnboardingDocumentId,
//                        document.OnboardingDocumentMasterId,
//                        document.DocumentName,
//                        document.IsMandatory,
//                        document.AllocatedDays,
//                        document.RequestedOn,
//                        document.DueOn,
//                        document.Instructions,
//                        document.Status,
//                        document.CurrentSubmissionNumber,
//                        document.ReceivedFromCandidate,
//                        document.IsActive
//                    });

//                var changes = new List<(CandidateOnboardingDocuments Document,
//                    string? Before, string? OldStatus, string Action)>();

//                foreach (var item in request.Documents)
//                {
//                    if (item.CandidateOnboardingDocumentId == 0)
//                    {
//                        var document = new CandidateOnboardingDocuments
//                        {
//                            CompanyId = request.CompanyId,
//                            CandidateOnboardingId = onboarding.CandidateOnboardingId,
//                            OnboardingDocumentMasterId = null,
//                            DocumentName = item.DocumentName!,
//                            IsMandatory = item.IsMandatory,
//                            AllocatedDays = item.AllocatedDays,
//                            RequestedOn = now,
//                            RequestedBy = request.UserId,
//                            DueOn = now.AddDays(item.AllocatedDays),
//                            Instructions = item.Instructions,
//                            Status = "Requested",
//                            CurrentSubmissionNumber = 0,
//                            ReceivedFromCandidate = false,
//                            IsActive = true,
//                            CreatedBy = request.UserId,
//                            CreatedOn = now
//                        };
//                        _context.CandidateOnboardingDocuments.Add(document);
//                        changes.Add((document, null, null, "DocumentRequested"));
//                    }
//                    else
//                    {
//                        var document = existingById[item.CandidateOnboardingDocumentId];
//                        bool daysChanged = document.AllocatedDays != item.AllocatedDays;
//                        bool changed = daysChanged || document.DocumentName != item.DocumentName ||
//                            document.IsMandatory != item.IsMandatory ||
//                            document.Instructions != item.Instructions;
//                        if (!changed) continue;

//                        string before = Snapshot(document);
//                        document.DocumentName = item.DocumentName!;
//                        document.IsMandatory = item.IsMandatory;
//                        document.Instructions = item.Instructions;
//                        if (daysChanged)
//                        {
//                            // Days are TOTAL calendar days from original request time.
//                            // Saving an unrelated edit never resets the due date.
//                            document.AllocatedDays = item.AllocatedDays;
//                            document.DueOn = document.RequestedOn.AddDays(item.AllocatedDays);
//                        }
//                        document.ModifiedBy = request.UserId;
//                        document.ModifiedOn = now;
//                        changes.Add((document, before, document.Status, "DocumentRequestUpdated"));
//                    }
//                }

//                if (changes.Count > 0)
//                {
//                    string oldStatus = onboarding.Status;
//                    onboarding.Status = "DocumentsPending";
//                    if (!isNewOnboarding)
//                    {
//                        onboarding.ModifiedBy = request.UserId;
//                        onboarding.ModifiedOn = now;
//                    }
//                    await _context.SaveChangesAsync();

//                    foreach (var change in changes)
//                    {
//                        _context.CandidateOnboardingHistories.Add(new CandidateOnboardingHistories
//                        {
//                            CompanyId = request.CompanyId,
//                            CandidateOnboardingId = onboarding.CandidateOnboardingId,
//                            CandidateOnboardingDocumentId = change.Document.CandidateOnboardingDocumentId,
//                            Action = change.Action,
//                            PreviousStatus = change.OldStatus,
//                            NewStatus = change.Document.Status,
//                            PreviousDetailsJson = change.Before,
//                            DetailsJson = Snapshot(change.Document),
//                            IsRemarkAdded = false,
//                            CreatedBy = request.UserId,
//                            CreatedOn = now
//                        });
//                    }
//                    if (oldStatus != onboarding.Status)
//                    {
//                        _context.CandidateOnboardingHistories.Add(new CandidateOnboardingHistories
//                        {
//                            CompanyId = request.CompanyId,
//                            CandidateOnboardingId = onboarding.CandidateOnboardingId,
//                            Action = "DocumentStageStarted",
//                            PreviousStatus = oldStatus,
//                            NewStatus = onboarding.Status,
//                            DetailsJson = JsonSerializer.Serialize(new { onboarding.Status }),
//                            CreatedBy = request.UserId,
//                            CreatedOn = now
//                        });
//                    }
//                    await _context.SaveChangesAsync();
//                }

//                var savedDocuments = await _context.CandidateOnboardingDocuments.AsNoTracking()
//                    .Where(x => x.CompanyId == request.CompanyId &&
//                        x.CandidateOnboardingId == onboarding.CandidateOnboardingId && x.IsActive)
//                    .OrderBy(x => x.CandidateOnboardingDocumentId)
//                    .Select(x => new
//                    {
//                        x.CandidateOnboardingDocumentId,
//                        x.OnboardingDocumentMasterId,
//                        x.DocumentName,
//                        x.IsMandatory,
//                        x.AllocatedDays,
//                        x.RequestedOn,
//                        x.RequestedBy,
//                        x.DueOn,
//                        x.Instructions,
//                        x.Status,
//                        x.CurrentSubmissionNumber,
//                        x.ReceivedFromCandidate,
//                        x.CreatedBy,
//                        x.CreatedOn,
//                        x.ModifiedBy,
//                        x.ModifiedOn,
//                        x.RowVersion
//                    }).ToListAsync();

//                var history = await _context.CandidateOnboardingHistories.AsNoTracking()
//                    .Where(x => x.CompanyId == request.CompanyId &&
//                        x.CandidateOnboardingId == onboarding.CandidateOnboardingId &&
//                        (x.Action == "DocumentRequested" || x.Action == "DocumentRequestUpdated" ||
//                         x.Action == "DocumentStageStarted"))
//                    .OrderByDescending(x => x.CreatedOn)
//                    .ThenByDescending(x => x.CandidateOnboardingHistoryId)
//                    .Select(x => new
//                    {
//                        x.CandidateOnboardingHistoryId,
//                        x.CandidateOnboardingDocumentId,
//                        x.Action,
//                        x.PreviousStatus,
//                        x.NewStatus,
//                        x.PreviousDetailsJson,
//                        x.DetailsJson,
//                        x.CreatedBy,
//                        x.CreatedOn
//                    }).ToListAsync();

//                await transaction.CommitAsync();
//                return Ok(new
//                {
//                    Status = true,
//                    Message = changes.Count == 0
//                        ? "No document request changes were found."
//                        : "Document requests saved successfully.",
//                    Data = new
//                    {
//                        onboarding.CandidateOnboardingId,
//                        request.CompanyId,
//                        request.CandidateId,
//                        request.JobDescriptionId,
//                        application.CandidateApplicationId,
//                        onboarding.Status,
//                        onboarding.RowVersion,
//                        ChangedDocumentCount = changes.Count,
//                        Documents = savedDocuments,
//                        History = history
//                    }
//                });
//            }
//            catch (DbUpdateConcurrencyException)
//            {
//                return Fail(409, "The record changed. Refresh the latest data and try again.");
//            }
//            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
//                (sql.Number == 2601 || sql.Number == 2627))
//            {
//                return Fail(409, "A duplicate onboarding record or document request already exists.");
//            }
//            catch (Exception ex)
//            {
//                Serilog.Log.Error(ex,
//                    "Failed to save onboarding document requests for CompanyId {CompanyId}, CandidateId {CandidateId}, JobId {JobId}.",
//                    request.CompanyId, request.CandidateId, request.JobDescriptionId);
//                return Fail(500, "Unable to save onboarding document requests.");
//            }
//        }




        public class GetOnboardingDocumentRequestsRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }

            public int CandidateId { get; set; }
            public int JobDescriptionId { get; set; }

            public int CandidateApplicationId { get; set; }
        }



        [HttpPost("GetOnboardingDocumentRequests")]
        public async Task<IActionResult> GetOnboardingDocumentRequests(
    [FromBody] GetOnboardingDocumentRequestsRequest? request)
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
                request.UserId <= 0 ||
                request.CandidateId <= 0 ||
                request.JobDescriptionId <= 0 ||
                request.CandidateApplicationId < 0)
            {
                return Fail(
                    400,
                    "Provide valid CompanyId, UserId, CandidateId and JobDescriptionId.");
            }

            try
            {
                var user = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsAdmin,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                bool isAdmin = user.IsAdmin == true;

                var candidate = await _context.Candidates
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId)
                    .Select(x => new
                    {
                        x.CandidateId,
                        x.FirstName,
                        x.MiddleName,
                        x.LastName
                    })
                    .FirstOrDefaultAsync();

                if (candidate == null)
                    return Fail(404, "Candidate not found for this company.");

                var job = await _context.JobDescriptions
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.JobDescriptionId == request.JobDescriptionId)
                    .Select(x => new
                    {
                        x.JobDescriptionId,
                        x.JobTitle,
                        TemplateId = (int?)x.TemplateId
                    })
                    .FirstOrDefaultAsync();

                if (job == null)
                    return Fail(404, "Job not found for this company.");

                var applicationsQuery = _context.CandidateApplications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId &&
                        x.JobDescriptionId == request.JobDescriptionId);

                if (request.CandidateApplicationId > 0)
                {
                    applicationsQuery = applicationsQuery.Where(x =>
                        x.CandidateApplicationId ==
                            request.CandidateApplicationId);
                }

                var applications = await applicationsQuery
                    .Select(x => new
                    {
                        x.CandidateApplicationId
                    })
                    .Take(2)
                    .ToListAsync();

                if (applications.Count == 0)
                    return Fail(404, "Candidate application not found.");

                if (applications.Count > 1)
                {
                    return Fail(
                        409,
                        "Multiple applications exist. Send CandidateApplicationId.");
                }

                int applicationId = applications[0].CandidateApplicationId;

                if (!isAdmin)
                {
                    var teamIds = await (
                        from member in _context.OnboardingTeamMembers
                            .AsNoTracking()

                        join team in _context.OnboardingTeams
                            .AsNoTracking()
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

                        where member.CompanyId == request.CompanyId
                              && member.EmployeeId == request.UserId
                              && member.IsActive
                              && team.IsActive

                        select team.OnboardingTeamId
                    )
                    .Distinct()
                    .ToListAsync();

                    var templateMappings = await _context.OnboardingTeamTemplates
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

                    bool hasAccess = teamIds.Any(teamId =>
                        // No mappings means all company templates.
                        !templateMappings.Any(x =>
                            x.OnboardingTeamId == teamId)
                        ||
                        (
                            job.TemplateId.HasValue &&
                            templateMappings.Any(x =>
                                x.OnboardingTeamId == teamId &&
                                x.TemplateId == job.TemplateId.Value)
                        ));

                    if (!hasAccess)
                    {
                        return Fail(
                            403,
                            "You are not assigned to onboarding for this candidate's template.");
                    }
                }

                var onboarding = await _context.CandidateOnboardings
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateApplicationId == applicationId)
                    .Select(x => new
                    {
                        x.CandidateOnboardingId,
                        x.Status,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                string candidateName = string.Join(
                    " ",
                    new[]
                    {
                candidate.FirstName,
                candidate.MiddleName,
                candidate.LastName
                    }.Where(x => !string.IsNullOrWhiteSpace(x)));

                if (onboarding == null)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "No document requests found.",

                        request.CompanyId,
                        request.CandidateId,
                        CandidateName = candidateName,
                        request.JobDescriptionId,
                        job.JobTitle,
                        CandidateApplicationId = applicationId,

                        CandidateOnboardingId = (int?)null,
                        OnboardingStatus = (string?)null,
                        OnboardingIsActive = (bool?)null,

                        TotalCount = 0,
                        RequestedCount = 0,
                        SubmittedCount = 0,
                        VerifiedCount = 0,
                        ResubmissionRequiredCount = 0,
                        OverdueCount = 0,

                        Data = Array.Empty<object>()
                    });
                }

                var documents = await _context.CandidateOnboardingDocuments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateOnboardingId ==
                            onboarding.CandidateOnboardingId &&
                        x.IsActive)
                    .OrderBy(x => x.CandidateOnboardingDocumentId)
                    .ToListAsync();

                var documentIds = documents
                    .Select(x => x.CandidateOnboardingDocumentId)
                    .ToArray();

                var histories = await _context.CandidateOnboardingHistories
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateOnboardingId ==
                            onboarding.CandidateOnboardingId &&
                        x.CandidateOnboardingDocumentId.HasValue &&
                        documentIds.Contains(
                            x.CandidateOnboardingDocumentId.Value))
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x =>
                        x.CandidateOnboardingHistoryId)
                    .ToListAsync();

                var employeeIds = documents
                    .SelectMany(x => new int?[]
                    {
                x.RequestedBy,
                x.ReceivedBy,
                x.SubmittedBy,
                x.VerifiedBy,
                x.CreatedBy,
                x.ModifiedBy
                    })
                    .Concat(histories.Select(x => (int?)x.CreatedBy))
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

                string? GetEmployeeName(int? employeeId)
                {
                    return employeeId.HasValue &&
                           employeeNames.TryGetValue(
                               employeeId.Value, out var name)
                        ? name
                        : null;
                }

                var historyLookup = histories.ToLookup(x =>
                    x.CandidateOnboardingDocumentId!.Value);

                var now = DateTime.UtcNow;

                var data = documents.Select(document =>
                {
                    var documentHistory =
                        historyLookup[document.CandidateOnboardingDocumentId]
                            .ToList();

                    // Indicates an outstanding submission deadline.
                    // It does not automatically reject the candidate.
                    bool isOverdue =
                        document.DueOn < now &&
                        (
                            document.Status == "Requested" ||
                            document.Status == "ResubmissionRequired"
                        );

                    return new
                    {
                        document.CandidateOnboardingDocumentId,
                        document.CandidateOnboardingId,
                        document.CompanyId,

                        request.CandidateId,
                        request.JobDescriptionId,
                        CandidateApplicationId = applicationId,

                        document.OnboardingDocumentMasterId,
                        document.DocumentName,
                        document.IsMandatory,
                        document.AllocatedDays,
                        document.Instructions,

                        document.Status,
                        document.CurrentSubmissionNumber,

                        document.RequestedOn,
                        document.RequestedBy,
                        RequestedByName =
                            GetEmployeeName(document.RequestedBy),

                        document.DueOn,
                        IsOverdue = isOverdue,

                        document.ReceivedFromCandidate,
                        document.ReceivedOn,
                        document.ReceivedBy,
                        ReceivedByName =
                            GetEmployeeName(document.ReceivedBy),

                        document.SubmittedOn,
                        document.SubmittedBy,
                        SubmittedByName =
                            GetEmployeeName(document.SubmittedBy),

                        document.VerifiedOn,
                        document.VerifiedBy,
                        VerifiedByName =
                            GetEmployeeName(document.VerifiedBy),

                        document.ReviewRemarks,
                        document.DetailsJson,
                        document.IsActive,

                        document.CreatedOn,
                        document.CreatedBy,
                        CreatedByName =
                            GetEmployeeName(document.CreatedBy),

                        document.ModifiedOn,
                        document.ModifiedBy,
                        ModifiedByName =
                            GetEmployeeName(document.ModifiedBy),

                        // Send this value back when editing this document.
                        document.RowVersion,

                        HistoryCount = documentHistory.Count,

                        History = documentHistory.Select(history => new
                        {
                            history.CandidateOnboardingHistoryId,
                            history.CandidateOnboardingDocumentId,
                            history.Action,
                            history.PreviousStatus,
                            history.NewStatus,
                            history.PreviousDetailsJson,
                            history.DetailsJson,
                            history.Remarks,
                            history.IsRemarkAdded,

                            history.CreatedBy,
                            CreatedByName =
                                GetEmployeeName(history.CreatedBy),
                            history.CreatedOn
                        }).ToList()
                    };
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Document requests fetched successfully."
                        : "No document requests found.",

                    request.CompanyId,
                    request.CandidateId,
                    CandidateName = candidateName,
                    request.JobDescriptionId,
                    job.JobTitle,
                    CandidateApplicationId = applicationId,

                    onboarding.CandidateOnboardingId,
                    OnboardingStatus = onboarding.Status,
                    OnboardingIsActive = onboarding.IsActive,

                    TotalCount = data.Count,

                    // Counts represent current document statuses.
                    RequestedCount = data.Count(x =>
                        x.Status == "Requested"),

                    SubmittedCount = data.Count(x =>
                        x.Status == "Submitted"),

                    VerifiedCount = data.Count(x =>
                        x.Status == "Verified"),

                    ResubmissionRequiredCount = data.Count(x =>
                        x.Status == "ResubmissionRequired"),

                    OverdueCount = data.Count(x => x.IsOverdue),

                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch onboarding document requests for CompanyId {CompanyId}, CandidateId {CandidateId}, JobId {JobId}.",
                    request.CompanyId,
                    request.CandidateId,
                    request.JobDescriptionId);

                return Fail(500, "Unable to fetch document requests.");
            }
        }



        // Copy this complete method INSIDE your controller. Keep your existing route.
        [HttpPost("SaveOnboardingDocument")]
        public async Task<IActionResult> SaveOnboardingDocument(
    [FromBody] SaveOnboardingDocumentRequest? request)
        {
            IActionResult Fail(int code, string message) =>
                StatusCode(code, new { Status = false, Message = message });

            if (request == null || request.CompanyId <= 0 || request.UserId <= 0 ||
                request.CandidateId <= 0 || request.JobDescriptionId <= 0 ||
                request.CandidateApplicationId < 0)
                return Fail(400, "Provide valid company, user, candidate and job IDs.");

            if (request.Documents == null || request.Documents.Count == 0)
                return Fail(400, "At least one document request is required.");

            foreach (var item in request.Documents)
            {
                if (item == null)
                    return Fail(400, "Document request cannot be null.");
                if (item.CandidateOnboardingDocumentId < 0)
                    return Fail(400, "Invalid document ID.");
                //if (item.AllocatedDays.HasValue &&
                //    (item.AllocatedDays.Value <= 0 || item.AllocatedDays.Value > 365))
                //    return Fail(400, "AllocatedDays must be between 1 and 365.");

                item.DocumentName = item.DocumentName?.Trim();
                item.Instructions = item.Instructions?.Trim();
                item.File = string.IsNullOrWhiteSpace(item.File) ? null : item.File.Trim();
                if (item.File?.Length > 1000)
                    return Fail(400, "File reference cannot exceed 1000 characters.");

                if (string.IsNullOrWhiteSpace(item.DocumentName))
                    return Fail(400, "DocumentName is required.");
                if (item.DocumentName?.Length > 200)
                    return Fail(400, "DocumentName cannot exceed 200 characters.");
                
            }

            if (request.Documents.Where(x => x.CandidateOnboardingDocumentId > 0)
                .GroupBy(x => x.CandidateOnboardingDocumentId).Any(g => g.Count() > 1))
                return Fail(400, "A document request ID cannot repeat.");

            try
            {
                await using var transaction = await _context.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable);

                var user = await _context.EmployeeMasters.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId);
                if (user == null) return Fail(404, "Employee not found for this company.");
                if (user.IsActive != true) return Fail(403, "Employee is inactive.");

                var candidatesExist = await _context.Candidates.AsNoTracking()
                    .AnyAsync(x => x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId && x.IsActive == true);
                if (!candidatesExist) return Fail(404, "Active candidate not found.");

                var job = await _context.JobDescriptions.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
                        x.JobDescriptionId == request.JobDescriptionId);
                if (job == null) return Fail(404, "Job not found for this company.");

                int? templateId = job.TemplateId;
                if (!templateId.HasValue || !await _context.RoleTemplates.AsNoTracking()
                    .AnyAsync(x => x.CompanyId == request.CompanyId &&
                        x.TemplateId == templateId.Value && x.IsActive == true))
                    return Fail(409, "The job must have an active recruitment template.");

                var applicationsQuery = _context.CandidateApplications.Where(x =>
                    x.CompanyId == request.CompanyId &&
                    x.CandidateId == request.CandidateId &&
                    x.JobDescriptionId == request.JobDescriptionId);
                if (request.CandidateApplicationId > 0)
                    applicationsQuery = applicationsQuery.Where(x =>
                        x.CandidateApplicationId == request.CandidateApplicationId);
                var applications = await applicationsQuery.Take(2).ToListAsync();
                if (applications.Count == 0) return Fail(404, "Candidate application not found.");
                if (applications.Count > 1)
                    return Fail(409, "Multiple applications exist. Send CandidateApplicationId.");
                var application = applications[0];
                if (application.IsActive != true)
                    return Fail(409, "Candidate application is inactive.");

                if (user.IsAdmin != true)
                {
                    var teamIds = await (
                        from member in _context.OnboardingTeamMembers.AsNoTracking()
                        join team in _context.OnboardingTeams.AsNoTracking()
                            on new { member.CompanyId, member.OnboardingTeamId }
                            equals new { team.CompanyId, team.OnboardingTeamId }
                        where member.CompanyId == request.CompanyId &&
                            member.EmployeeId == request.UserId &&
                            member.IsActive && team.IsActive
                        select team.OnboardingTeamId
                    ).Distinct().ToListAsync();
                    var scopes = await _context.OnboardingTeamTemplates.AsNoTracking()
                        .Where(x => x.CompanyId == request.CompanyId &&
                            teamIds.Contains(x.OnboardingTeamId)).ToListAsync();
                    bool allowed = teamIds.Any(id =>
                        !scopes.Any(x => x.OnboardingTeamId == id) ||
                        scopes.Any(x => x.OnboardingTeamId == id &&
                            x.TemplateId == templateId.Value));
                    if (!allowed)
                        return Fail(403, "You are not assigned to onboarding for this template.");
                }

                // Eligible when the final approval hierarchy OR an admin approves.
                // Lower hierarchy Reject/Return decisions do not block this rule.
                var assignments = await _context.RoleTemplateAssignment.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                        x.TemplateId == templateId.Value &&
                        x.IsActive == true &&
                        x.CanApprove == true)
                    .ToListAsync();

                // Include unassigned decisions so direct admin approval is visible.
                var decisions = await _context.CandidateSkillAssessmentHistories
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateApplicationId == application.CandidateApplicationId &&
                        x.JobDescriptionId == request.JobDescriptionId &&
                        (x.Action == "Approve" ||
                         x.Action == "Reject" ||
                         x.Action == "Return"))
                    .ToListAsync();

                var adminEmployeeIds = (await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsAdmin == true)
                    .Select(x => (int?)x.EmployeeId)
                    .ToListAsync())
                    .ToHashSet();

                // Latest decision across admin authors, not any old approval.
                var latestAdminDecision = decisions
                    .Where(x => adminEmployeeIds.Contains(x.CreatedBy))
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
                    .FirstOrDefault();

                bool adminApproved = latestAdminDecision?.Action == "Approve";
                bool lastHierarchyApproved = false;

                if (assignments.Count > 0)
                {
                    int lastHierarchyLevel = assignments.Max(x => x.HierarchyLevel);

                    var lastHierarchyAssignments = assignments
                        .Where(x => x.HierarchyLevel == lastHierarchyLevel)
                        .ToList();

                    // If several assignments share the last level,
                    // each final-level assignee must have approved.
                    lastHierarchyApproved = lastHierarchyAssignments.All(assignment =>
                    {
                        var latestDecision = decisions
                            .Where(x =>
                                x.AssignmentId == assignment.AssignmentId &&
                                x.CreatedBy == assignment.EmployeeId)
                            .OrderByDescending(x => x.CreatedOn)
                            .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
                            .FirstOrDefault();

                        return latestDecision?.Action == "Approve";
                    });
                }

                if (!adminApproved && !lastHierarchyApproved)
                {
                    return Fail(
                        409,
                        "Approval from the last hierarchy or an admin is required.");
                }

                var onboarding = await _context.CandidateOnboardings
                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
                        x.CandidateApplicationId == application.CandidateApplicationId);

                if (onboarding != null &&
                    (!onboarding.IsActive || !string.IsNullOrWhiteSpace(onboarding.FinalOutcome)))
                    return Fail(409, "This onboarding case is inactive or already closed.");

                // Do not reopen later completed stages by silently adding new requirements.
                if (onboarding != null &&
                    onboarding.Status != "ReadyForOnboarding" &&
                    onboarding.Status != "DocumentsPending")
                    return Fail(409, "Document requests can be changed only before advancing beyond the document stage.");

                var existing = onboarding == null
                    ? new List<CandidateOnboardingDocuments>()
                    : await _context.CandidateOnboardingDocuments
                        .Where(x => x.CompanyId == request.CompanyId &&
                            x.CandidateOnboardingId == onboarding.CandidateOnboardingId)
                        .ToListAsync();
                var existingById = existing.ToDictionary(x => x.CandidateOnboardingDocumentId);

                foreach (var item in request.Documents)
                {
                    CandidateOnboardingDocuments? saved = null;
                    if (item.CandidateOnboardingDocumentId > 0)
                    {
                        if (!existingById.TryGetValue(item.CandidateOnboardingDocumentId, out saved))
                            return Fail(404, "Document request not found for this onboarding case.");
                        if (!saved.IsActive || (saved.Status != "Requested" &&
                            saved.Status != "Submitted" && saved.Status != "ResubmissionRequired"))
                            return Fail(409, "Verified or inactive documents cannot be edited here.");
                        if (!string.Equals(saved.DocumentName, item.DocumentName, StringComparison.Ordinal) &&
                            (saved.CurrentSubmissionNumber > 0 || !string.IsNullOrEmpty(saved.FileStorageKey)))
                            return Fail(409, "A document with submissions cannot be renamed. Add a new request instead.");
                      
                    }


                    if (string.IsNullOrWhiteSpace(item.DocumentName) || item.DocumentName.Length > 200)
                        return Fail(400, "Every document must have a valid name up to 200 characters.");
                }

                foreach (var item in request.Documents)
                {
                    var current = item.CandidateOnboardingDocumentId > 0
                        ? existingById[item.CandidateOnboardingDocumentId] : null;
                    item.IsMandatory ??= current?.IsMandatory ?? true;
                    item.AllocatedDays ??= current?.AllocatedDays ?? 5;
                    // Omitted instructions on edit preserve existing instructions.
                    if (current != null && item.Instructions == null)
                        item.Instructions = current.Instructions;
                }

                // Validate the final combined selection. Omitted rows remain unchanged.
                var editedIds = request.Documents.Where(x => x.CandidateOnboardingDocumentId > 0)
                    .Select(x => x.CandidateOnboardingDocumentId).ToHashSet();
                var finalNames = existing.Where(x => x.IsActive && !editedIds.Contains(x.CandidateOnboardingDocumentId))
                    .Select(x => x.DocumentName.Trim())
                    .Concat(request.Documents.Select(x => x.DocumentName!));
                if (finalNames.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                    return Fail(409, "An active request for this document name already exists.");


                // A new upload does not need a pre-existing CandidateDocuments row.
                // This implementation supports files already saved under local /Content/.
                var environment = HttpContext.RequestServices
                    .GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                var registeredInThisCall = new HashSet<string>(StringComparer.Ordinal);

                // Infer the actual CandidateDocuments entity type from your DbSet.
                static T NewEntity<T>(DbSet<T> set) where T : class, new() => new T();

                foreach (var item in request.Documents.Where(x => x.File != null))
                {
                    string suppliedPath = item.File!;
                    // Accept /Content/... or Content/...; reject URLs and traversal.
                    string relativePath = suppliedPath.TrimStart('/');
                    var segments = relativePath.Split('/');
                    if (!relativePath.StartsWith("Content/", StringComparison.OrdinalIgnoreCase) ||
                        suppliedPath.StartsWith("//", StringComparison.Ordinal) ||
                        relativePath.IndexOfAny(new[] { '\\', ':', '?', '#', '%', '\0' }) >= 0 ||
                        segments.Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".." ||
                            part != part.Trim() || part.EndsWith(".", StringComparison.Ordinal)))
                        return Fail(400, "File must be a local /Content/... storage path without traversal.");

                    string storageKey = "/" + relativePath;
                    item.File = storageKey;
                    if (!registeredInThisCall.Add(storageKey)) continue;

                    // Include both historical path formats when checking registrations.
                    var registrations = await _context.CandidateDocuments
                        .Where(x => x.StorageKey == storageKey || x.StorageKey == relativePath)
                        .ToListAsync();
                    if (registrations.Any(x => x.CompanyId != request.CompanyId ||
                        x.CandidateApplicationId != application.CandidateApplicationId))
                        return Fail(409, "This file is already registered to another company or candidate application.");

                    var registered = registrations.FirstOrDefault(x => x.IsActive == true);
                    if (registered != null) continue;
                    if (registrations.Count > 0)
                        return Fail(409, "This file registration is inactive. Upload a new file or restore it separately.");

                    System.IO.FileInfo? physicalFile = null;
                    var roots = new[] { environment.WebRootPath, environment.ContentRootPath }
                        .Where(root => !string.IsNullOrWhiteSpace(root)).Distinct();
                    foreach (var root in roots)
                    {
                        string contentRoot = System.IO.Path.GetFullPath(
                            System.IO.Path.Combine(root!, "Content"));
                        string prefix = contentRoot.TrimEnd(
                            System.IO.Path.DirectorySeparatorChar,
                            System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
                        string physicalPath = System.IO.Path.GetFullPath(
                            System.IO.Path.Combine(root!, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                        if (!physicalPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var fileInfo = new System.IO.FileInfo(physicalPath);
                        if (!fileInfo.Exists) continue;
                        // Do not follow symbolic links/junctions outside the configured storage.
                        bool linked = (fileInfo.Attributes & System.IO.FileAttributes.ReparsePoint) != 0;
                        for (var parent = fileInfo.Directory; parent != null; parent = parent.Parent)
                        {
                            if ((parent.Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
                            { linked = true; break; }
                        }
                        if (linked) return Fail(400, "Linked file paths are not supported for this upload.");
                        physicalFile = fileInfo;
                        break;
                    }
                    //if (physicalFile == null)
                    //    return Fail(400, "Uploaded file was not found in this application's Content storage.");
                    //if (physicalFile.Length == 0)
                    //    return Fail(400, "Uploaded file is empty.");
                    //if (physicalFile.Length > int.MaxValue)
                    //    return Fail(400, "Uploaded file is too large.");

                    //if (!contentTypes.TryGetContentType(physicalFile.Name, out var contentType))
                        //contentType = "application/octet-stream";

                    var upload = NewEntity(_context.CandidateDocuments);
                    upload.CompanyId = request.CompanyId;
                    upload.CandidateApplicationId = application.CandidateApplicationId;
                    upload.DocumentType = item.DocumentName!;
                    // Original browser filename is not in the request. Use stored filename.
                    upload.OriginalFileName = item.DocumentName;
                    upload.StorageKey = storageKey;
                 //   upload.ContentType = contentType;
                   // upload.FileSizeBytes = (int)physicalFile.Length;
                    upload.IsActive = true;
                    upload.CreatedBy = request.UserId;
                    upload.CreatedOn = DateTime.UtcNow;
                    _context.CandidateDocuments.Add(upload);
                }

                // Find or create company masters. Existing defaults are never overwritten.
                var masterByName = new Dictionary<string, OnboardingDocumentMasters>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var item in request.Documents)
                {
                    string name = item.DocumentName!;
                    if (masterByName.ContainsKey(name)) continue;
                    var master = await _context.OnboardingDocumentMasters
                        .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
                            EF.Functions.Collate(x.DocumentName.Trim(),
                                "SQL_Latin1_General_CP1_CI_AS") == name);
                    if (master != null && !master.IsActive)
                        return Fail(409, $"Document type '{name}' is inactive. Reactivate it before use.");
                    if (master == null)
                    {
                        master = new OnboardingDocumentMasters
                        {
                            CompanyId = request.CompanyId,
                            DocumentName = name,
                            DefaultIsMandatory = item.IsMandatory!.Value,
                            DefaultAllocatedDays = item.AllocatedDays!.Value,
                            IsActive = true,
                            CreatedBy = request.UserId,
                            CreatedOn = DateTime.UtcNow
                        };
                        _context.OnboardingDocumentMasters.Add(master);
                    }
                    masterByName.Add(name, master);
                }
                await _context.SaveChangesAsync();

                var now = DateTime.UtcNow;
                bool isNewOnboarding = onboarding == null;
                if (onboarding == null)
                {
                    onboarding = new CandidateOnboardings
                    {
                        CompanyId = request.CompanyId,
                        CandidateApplicationId = application.CandidateApplicationId,
                        TemplateIdAtStart = templateId,
                        Status = "ReadyForOnboarding",
                        IsActive = true,
                        CreatedBy = request.UserId,
                        CreatedOn = now
                    };
                    _context.CandidateOnboardings.Add(onboarding);
                    await _context.SaveChangesAsync();
                }

                string Snapshot(CandidateOnboardingDocuments document) =>
                    JsonSerializer.Serialize(new
                    {
                        document.CandidateOnboardingDocumentId,
                        document.OnboardingDocumentMasterId,
                        document.DocumentName,
                        document.IsMandatory,
                        document.AllocatedDays,
                        document.RequestedOn,
                        document.DueOn,
                        document.Instructions,
                        document.Status,
                        File = document.FileStorageKey,
                        document.SubmittedBy,
                        document.SubmittedOn,
                        document.ReceivedBy,
                        document.ReceivedOn,
                        document.CurrentSubmissionNumber,
                        document.ReceivedFromCandidate,
                        document.IsActive
                    });

                var changes = new List<(CandidateOnboardingDocuments Document,
                    string? Before, string? OldStatus, string Action)>();

                foreach (var item in request.Documents)
                {
                    int masterId = masterByName[item.DocumentName!].OnboardingDocumentMasterId;
                    if (item.CandidateOnboardingDocumentId == 0)
                    {
                        var document = new CandidateOnboardingDocuments
                        {
                            CompanyId = request.CompanyId,
                            CandidateOnboardingId = onboarding.CandidateOnboardingId,
                            OnboardingDocumentMasterId = masterId,
                            DocumentName = item.DocumentName!,
                            IsMandatory = item.IsMandatory!.Value,
                            AllocatedDays = item.AllocatedDays!.Value,
                            RequestedOn = now,
                            RequestedBy = request.UserId,
                            DueOn = now.AddDays(item.AllocatedDays!.Value),
                            Instructions = item.Instructions,
                            FileStorageKey = item.File,
                            Status = item.File == null ? "Requested" : "Submitted",
                            CurrentSubmissionNumber = item.File == null ? 0 : 1,
                            ReceivedFromCandidate = item.File != null,
                            ReceivedBy = item.File == null ? (int?)null : request.UserId,
                            ReceivedOn = item.File == null ? (DateTime?)null : now,
                            SubmittedBy = item.File == null ? (int?)null : request.UserId,
                            SubmittedOn = item.File == null ? (DateTime?)null : now,
                            IsActive = true,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        };
                        _context.CandidateOnboardingDocuments.Add(document);
                        changes.Add((document, null, null, item.File == null
                            ? "DocumentRequested" : "DocumentSubmitted"));
                    }
                    else
                    {
                        var document = existingById[item.CandidateOnboardingDocumentId];
                        bool daysChanged = document.AllocatedDays != item.AllocatedDays;
                        bool submitFile = item.File != null &&
                            (document.FileStorageKey != item.File ||
                             document.Status == "Requested" || document.Status == "ResubmissionRequired");
                        bool changed = submitFile || document.OnboardingDocumentMasterId != masterId || daysChanged || document.DocumentName != item.DocumentName ||
                            document.IsMandatory != item.IsMandatory ||
                            document.Instructions != item.Instructions;
                        if (!changed) continue;

                        string before = Snapshot(document);
                        string previousDocumentStatus = document.Status;
                        document.OnboardingDocumentMasterId = masterId;
                        if (submitFile)
                        {
                            document.FileStorageKey = item.File;
                            document.Status = "Submitted";
                            document.CurrentSubmissionNumber += 1;
                            document.ReceivedFromCandidate = true;
                            document.ReceivedBy = request.UserId;
                            document.ReceivedOn = now;
                            document.SubmittedBy = request.UserId;
                            document.SubmittedOn = now;
                            document.VerifiedBy = null;
                            document.VerifiedOn = null;
                        }
                        document.DocumentName = item.DocumentName!;
                        document.IsMandatory = item.IsMandatory!.Value;
                        document.Instructions = item.Instructions;
                        if (daysChanged)
                        {
                            // Days are TOTAL calendar days from original request time.
                            // Saving an unrelated edit never resets the due date.
                            document.AllocatedDays = item.AllocatedDays!.Value;
                            document.DueOn = document.RequestedOn.AddDays(item.AllocatedDays!.Value);
                        }
                        document.ModifiedBy = request.UserId;
                        document.ModifiedOn = now;
                        changes.Add((document, before, previousDocumentStatus,
                            submitFile ? "DocumentSubmitted" : "DocumentRequestUpdated"));
                    }
                }

                if (changes.Count > 0)
                {
                    string oldStatus = onboarding.Status;
                    onboarding.Status = "DocumentsPending";
                    if (!isNewOnboarding)
                    {
                        onboarding.ModifiedBy = request.UserId;
                        onboarding.ModifiedOn = now;
                    }
                    await _context.SaveChangesAsync();

                    foreach (var change in changes)
                    {
                        _context.CandidateOnboardingHistories.Add(new CandidateOnboardingHistories
                        {
                            CompanyId = request.CompanyId,
                            CandidateOnboardingId = onboarding.CandidateOnboardingId,
                            CandidateOnboardingDocumentId = change.Document.CandidateOnboardingDocumentId,
                            Action = change.Action,
                            PreviousStatus = change.OldStatus,
                            NewStatus = change.Document.Status,
                            PreviousDetailsJson = change.Before,
                            DetailsJson = Snapshot(change.Document),
                            IsRemarkAdded = false,
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        });
                    }
                    if (oldStatus != onboarding.Status)
                    {
                        _context.CandidateOnboardingHistories.Add(new CandidateOnboardingHistories
                        {
                            CompanyId = request.CompanyId,
                            CandidateOnboardingId = onboarding.CandidateOnboardingId,
                            Action = "DocumentStageStarted",
                            PreviousStatus = oldStatus,
                            NewStatus = onboarding.Status,
                            DetailsJson = JsonSerializer.Serialize(new { onboarding.Status }),
                            CreatedBy = request.UserId,
                            CreatedOn = now
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                var savedDocuments = await _context.CandidateOnboardingDocuments.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                        x.CandidateOnboardingId == onboarding.CandidateOnboardingId && x.IsActive)
                    .OrderBy(x => x.CandidateOnboardingDocumentId)
                    .Select(x => new
                    {
                        x.CandidateOnboardingDocumentId,
                        x.OnboardingDocumentMasterId,
                        x.DocumentName,
                        x.IsMandatory,
                        x.AllocatedDays,
                        x.RequestedOn,
                        x.RequestedBy,
                        x.DueOn,
                        x.Instructions,
                        x.Status,
                        File = x.FileStorageKey,
                        x.SubmittedBy,
                        x.SubmittedOn,
                        x.CurrentSubmissionNumber,
                        x.ReceivedFromCandidate,
                        x.CreatedBy,
                        x.CreatedOn,
                        x.ModifiedBy,
                        x.ModifiedOn,
                        x.RowVersion
                    }).ToListAsync();

                var history = await _context.CandidateOnboardingHistories.AsNoTracking()
                    .Where(x => x.CompanyId == request.CompanyId &&
                        x.CandidateOnboardingId == onboarding.CandidateOnboardingId &&
                        (x.Action == "DocumentRequested" || x.Action == "DocumentRequestUpdated" ||
                         x.Action == "DocumentSubmitted" ||
                         x.Action == "DocumentStageStarted"))
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x => x.CandidateOnboardingHistoryId)
                    .Select(x => new
                    {
                        x.CandidateOnboardingHistoryId,
                        x.CandidateOnboardingDocumentId,
                        x.Action,
                        x.PreviousStatus,
                        x.NewStatus,
                        x.PreviousDetailsJson,
                        x.DetailsJson,
                        x.CreatedBy,
                        x.CreatedOn
                    }).ToListAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = changes.Count == 0
                        ? "No document request changes were found."
                        : "Document requests saved successfully.",
                    Data = new
                    {
                        onboarding.CandidateOnboardingId,
                        request.CompanyId,
                        request.CandidateId,
                        request.JobDescriptionId,
                        application.CandidateApplicationId,
                        onboarding.Status,
                        onboarding.RowVersion,
                        ChangedDocumentCount = changes.Count,
                        Documents = savedDocuments,
                        History = history
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail(409, "The record changed. Refresh the latest data and try again.");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
                (sql.Number == 2601 || sql.Number == 2627))
            {
                return Fail(409, "A duplicate onboarding record or document request already exists.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex,
                    "Failed to save onboarding document requests for CompanyId {CompanyId}, CandidateId {CandidateId}, JobId {JobId}.",
                    request.CompanyId, request.CandidateId, request.JobDescriptionId);
                return Fail(500, "Unable to save onboarding document requests.");
            }
        }


        // Replace your existing DTOs with these; do not add duplicate classes.
        public class SaveOnboardingDocumentRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }
            public int CandidateId { get; set; }
            public int JobDescriptionId { get; set; }
            public int CandidateApplicationId { get; set; }
            public List<SaveOnboardingDocumentItem>? Documents { get; set; }
        }
        public class SaveOnboardingDocumentItem
        {
            public int CandidateOnboardingDocumentId { get; set; }
            public string? DocumentName { get; set; }
            public bool? IsMandatory { get; set; }
            public int? AllocatedDays { get; set; }
            public string? Instructions { get; set; }
            public string? File { get; set; }
           // public byte[]? RowVersion { get; set; }
        }






        // DTOs go outside your controller.
        //public class SaveOnboardingDocumentRequest
        //    {
        //        public int CompanyId { get; set; }
        //        public int UserId { get; set; }
        //        public int CandidateId { get; set; }
        //        public int JobDescriptionId { get; set; }
        //        // Optional unless multiple applications exist for this candidate/job.
        //        public int CandidateApplicationId { get; set; }
        //        public List<OnboardingDocumentRequestItem>? Documents { get; set; }
        //    }
        //    public class OnboardingDocumentRequestItem
        //    {
        //        // 0 = create, positive = edit this exact request.
        //        public int CandidateOnboardingDocumentId { get; set; }
        //        // Supplied by the frontend; no document-master lookup.
        //        public string? DocumentName { get; set; }
        //        public bool IsMandatory { get; set; } = true;
        //        public int AllocatedDays { get; set; } = 5;
        //        public string? Instructions { get; set; }
        //        // Existing CandidateDocuments.StorageKey; null/empty preserves the current file.
        //        public string? File { get; set; }
        //        // Required for edits. Send the base64 RowVersion returned by GET/save.
        //        public byte[]? RowVersion { get; set; }
        //}

        //// Place INSIDE your existing controller.
        //[HttpPost("SaveOnboardingDocument")]
        //        public async Task<IActionResult> SaveOnboardingDocument(
        //    [FromBody] SaveOnboardingDocumentRequest? request)
        //        {
        //            IActionResult Fail(int code, string message) =>
        //                StatusCode(code, new { Status = false, Message = message });

        //            if (request == null || request.CompanyId <= 0 || request.UserId <= 0 ||
        //                request.CandidateId <= 0 || request.JobDescriptionId <= 0 ||
        //                request.CandidateApplicationId < 0)
        //                return Fail(400, "Provide valid company, user, candidate and job IDs.");

        //            if (request.Documents == null || request.Documents.Count == 0)
        //                return Fail(400, "At least one document request is required.");

        //            foreach (var item in request.Documents)
        //            {
        //                if (item == null)
        //                    return Fail(400, "Document request cannot be null.");
        //                if (item.CandidateOnboardingDocumentId < 0)
        //                    return Fail(400, "Invalid document ID.");
        //                if (item.AllocatedDays <= 0 || item.AllocatedDays > 365)
        //                    return Fail(400, "AllocatedDays must be between 1 and 365.");

        //                item.DocumentName = item.DocumentName?.Trim();
        //                item.Instructions = item.Instructions?.Trim();
        //                item.File = string.IsNullOrWhiteSpace(item.File) ? null : item.File.Trim();
        //                if (item.File?.Length > 1000)
        //                    return Fail(400, "File reference cannot exceed 1000 characters.");

        //                if (string.IsNullOrWhiteSpace(item.DocumentName))
        //                    return Fail(400, "DocumentName is required.");
        //                if (item.DocumentName?.Length > 200)
        //                    return Fail(400, "DocumentName cannot exceed 200 characters.");
        //                if (item.CandidateOnboardingDocumentId > 0 &&
        //                    (item.RowVersion == null || item.RowVersion.Length != 8))
        //                    return Fail(400, "Send the current RowVersion when editing a document request.");
        //            }

        //            if (request.Documents.Where(x => x.CandidateOnboardingDocumentId > 0)
        //                .GroupBy(x => x.CandidateOnboardingDocumentId).Any(g => g.Count() > 1))
        //                return Fail(400, "A document request ID cannot repeat.");

        //            try
        //            {
        //                await using var transaction = await _context.Database
        //                    .BeginTransactionAsync(IsolationLevel.Serializable);

        //                var user = await _context.EmployeeMasters.AsNoTracking()
        //                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
        //                        x.EmployeeId == request.UserId);
        //                if (user == null) return Fail(404, "Employee not found for this company.");
        //                if (user.IsActive != true) return Fail(403, "Employee is inactive.");

        //                var candidatesExist = await _context.Candidates.AsNoTracking()
        //                    .AnyAsync(x => x.CompanyId == request.CompanyId &&
        //                        x.CandidateId == request.CandidateId && x.IsActive == true);
        //                if (!candidatesExist) return Fail(404, "Active candidate not found.");

        //                var job = await _context.JobDescriptions.AsNoTracking()
        //                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
        //                        x.JobDescriptionId == request.JobDescriptionId);
        //                if (job == null) return Fail(404, "Job not found for this company.");

        //                int? templateId = job.TemplateId;
        //                if (!templateId.HasValue || !await _context.RoleTemplates.AsNoTracking()
        //                    .AnyAsync(x => x.CompanyId == request.CompanyId &&
        //                        x.TemplateId == templateId.Value && x.IsActive == true))
        //                    return Fail(409, "The job must have an active recruitment template.");

        //                var applicationsQuery = _context.CandidateApplications.Where(x =>
        //                    x.CompanyId == request.CompanyId &&
        //                    x.CandidateId == request.CandidateId &&
        //                    x.JobDescriptionId == request.JobDescriptionId);
        //                if (request.CandidateApplicationId > 0)
        //                    applicationsQuery = applicationsQuery.Where(x =>
        //                        x.CandidateApplicationId == request.CandidateApplicationId);
        //                var applications = await applicationsQuery.Take(2).ToListAsync();
        //                if (applications.Count == 0) return Fail(404, "Candidate application not found.");
        //                if (applications.Count > 1)
        //                    return Fail(409, "Multiple applications exist. Send CandidateApplicationId.");
        //                var application = applications[0];
        //                if (application.IsActive != true)
        //                    return Fail(409, "Candidate application is inactive.");

        //                if (user.IsAdmin != true)
        //                {
        //                    var teamIds = await (
        //                        from member in _context.OnboardingTeamMembers.AsNoTracking()
        //                        join team in _context.OnboardingTeams.AsNoTracking()
        //                            on new { member.CompanyId, member.OnboardingTeamId }
        //                            equals new { team.CompanyId, team.OnboardingTeamId }
        //                        where member.CompanyId == request.CompanyId &&
        //                            member.EmployeeId == request.UserId &&
        //                            member.IsActive && team.IsActive
        //                        select team.OnboardingTeamId
        //                    ).Distinct().ToListAsync();
        //                    var scopes = await _context.OnboardingTeamTemplates.AsNoTracking()
        //                        .Where(x => x.CompanyId == request.CompanyId &&
        //                            teamIds.Contains(x.OnboardingTeamId)).ToListAsync();
        //                    bool allowed = teamIds.Any(id =>
        //                        !scopes.Any(x => x.OnboardingTeamId == id) ||
        //                        scopes.Any(x => x.OnboardingTeamId == id &&
        //                            x.TemplateId == templateId.Value));
        //                    if (!allowed)
        //                        return Fail(403, "You are not assigned to onboarding for this template.");
        //                }

        //                // Eligible when the final approval hierarchy OR an admin approves.
        //                // Lower hierarchy Reject/Return decisions do not block this rule.
        //                var assignments = await _context.RoleTemplateAssignment.AsNoTracking()
        //                    .Where(x => x.CompanyId == request.CompanyId &&
        //                        x.TemplateId == templateId.Value &&
        //                        x.IsActive == true &&
        //                        x.CanApprove == true)
        //                    .ToListAsync();

        //                // Include unassigned decisions so direct admin approval is visible.
        //                var decisions = await _context.CandidateSkillAssessmentHistories
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.CandidateApplicationId == application.CandidateApplicationId &&
        //                        x.JobDescriptionId == request.JobDescriptionId &&
        //                        (x.Action == "Approve" ||
        //                         x.Action == "Reject" ||
        //                         x.Action == "Return"))
        //                    .ToListAsync();

        //                var adminEmployeeIds = (await _context.EmployeeMasters
        //                    .AsNoTracking()
        //                    .Where(x =>
        //                        x.CompanyId == request.CompanyId &&
        //                        x.IsAdmin == true)
        //                    .Select(x => (int?)x.EmployeeId)
        //                    .ToListAsync())
        //                    .ToHashSet();

        //                // Latest decision across admin authors, not any old approval.
        //                var latestAdminDecision = decisions
        //                    .Where(x => adminEmployeeIds.Contains(x.CreatedBy))
        //                    .OrderByDescending(x => x.CreatedOn)
        //                    .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
        //                    .FirstOrDefault();

        //                bool adminApproved = latestAdminDecision?.Action == "Approve";
        //                bool lastHierarchyApproved = false;

        //                if (assignments.Count > 0)
        //                {
        //                    int lastHierarchyLevel = assignments.Max(x => x.HierarchyLevel);

        //                    var lastHierarchyAssignments = assignments
        //                        .Where(x => x.HierarchyLevel == lastHierarchyLevel)
        //                        .ToList();

        //                    // If several assignments share the last level,
        //                    // each final-level assignee must have approved.
        //                    lastHierarchyApproved = lastHierarchyAssignments.All(assignment =>
        //                    {
        //                        var latestDecision = decisions
        //                            .Where(x =>
        //                                x.AssignmentId == assignment.AssignmentId &&
        //                                x.CreatedBy == assignment.EmployeeId)
        //                            .OrderByDescending(x => x.CreatedOn)
        //                            .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
        //                            .FirstOrDefault();

        //                        return latestDecision?.Action == "Approve";
        //                    });
        //                }

        //                if (!adminApproved && !lastHierarchyApproved)
        //                {
        //                    return Fail(
        //                        409,
        //                        "Approval from the last hierarchy or an admin is required.");
        //                }

        //                var onboarding = await _context.CandidateOnboardings
        //                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
        //                        x.CandidateApplicationId == application.CandidateApplicationId);

        //                if (onboarding != null &&
        //                    (!onboarding.IsActive || !string.IsNullOrWhiteSpace(onboarding.FinalOutcome)))
        //                    return Fail(409, "This onboarding case is inactive or already closed.");

        //                // Do not reopen later completed stages by silently adding new requirements.
        //                if (onboarding != null &&
        //                    onboarding.Status != "ReadyForOnboarding" &&
        //                    onboarding.Status != "DocumentsPending")
        //                    return Fail(409, "Document requests can be changed only before advancing beyond the document stage.");

        //                var existing = onboarding == null
        //                    ? new List<CandidateOnboardingDocuments>()
        //                    : await _context.CandidateOnboardingDocuments
        //                        .Where(x => x.CompanyId == request.CompanyId &&
        //                            x.CandidateOnboardingId == onboarding.CandidateOnboardingId)
        //                        .ToListAsync();
        //                var existingById = existing.ToDictionary(x => x.CandidateOnboardingDocumentId);

        //                foreach (var item in request.Documents)
        //                {
        //                    CandidateOnboardingDocuments? saved = null;
        //                    if (item.CandidateOnboardingDocumentId > 0)
        //                    {
        //                        if (!existingById.TryGetValue(item.CandidateOnboardingDocumentId, out saved))
        //                            return Fail(404, "Document request not found for this onboarding case.");
        //                        if (!saved.IsActive || (saved.Status != "Requested" &&
        //                            saved.Status != "Submitted" && saved.Status != "ResubmissionRequired"))
        //                            return Fail(409, "Verified or inactive documents cannot be edited here.");
        //                        if (!string.Equals(saved.DocumentName, item.DocumentName, StringComparison.Ordinal) &&
        //                            (saved.CurrentSubmissionNumber > 0 || !string.IsNullOrEmpty(saved.FileStorageKey)))
        //                            return Fail(409, "A document with submissions cannot be renamed. Add a new request instead.");
        //                        if (!saved.RowVersion.SequenceEqual(item.RowVersion!))
        //                            return Fail(409, "Document request changed. Refresh and try again.");
        //                    }


        //                    if (string.IsNullOrWhiteSpace(item.DocumentName) || item.DocumentName.Length > 200)
        //                        return Fail(400, "Every document must have a valid name up to 200 characters.");
        //                }

        //                // Validate the final combined selection. Omitted rows remain unchanged.
        //                var editedIds = request.Documents.Where(x => x.CandidateOnboardingDocumentId > 0)
        //                    .Select(x => x.CandidateOnboardingDocumentId).ToHashSet();
        //                var finalNames = existing.Where(x => x.IsActive && !editedIds.Contains(x.CandidateOnboardingDocumentId))
        //                    .Select(x => x.DocumentName.Trim())
        //                    .Concat(request.Documents.Select(x => x.DocumentName!));
        //                if (finalNames.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
        //                    return Fail(409, "An active request for this document name already exists.");


        //                // File references must already be registered by the upload API for
        //                // this company and exact candidate application. No URL is fetched here.
        //                var suppliedFiles = request.Documents.Where(x => x.File != null)
        //                    .Select(x => x.File!).Distinct().ToArray();
        //                var registeredFiles = await _context.CandidateDocuments.AsNoTracking()
        //                    .Where(x => x.CompanyId == request.CompanyId &&
        //                        x.CandidateApplicationId == application.CandidateApplicationId &&
        //                        x.IsActive == true && suppliedFiles.Contains(x.StorageKey))
        //                    .Select(x => x.StorageKey).ToListAsync();
        //                if (suppliedFiles.Any(file => !registeredFiles.Contains(file)))
        //                    return Fail(400, "A file is not registered as an active upload for this candidate application.");

        //                // Find or create company masters. Existing defaults are never overwritten.
        //                var masterByName = new Dictionary<string, OnboardingDocumentMasters>(
        //                    StringComparer.OrdinalIgnoreCase);
        //                foreach (var item in request.Documents)
        //                {
        //                    string name = item.DocumentName!;
        //                    if (masterByName.ContainsKey(name)) continue;
        //                    var master = await _context.OnboardingDocumentMasters
        //                        .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
        //                            EF.Functions.Collate(x.DocumentName.Trim(),
        //                                "SQL_Latin1_General_CP1_CI_AS") == name);
        //                    if (master != null && !master.IsActive)
        //                        return Fail(409, $"Document type '{name}' is inactive. Reactivate it before use.");
        //                    if (master == null)
        //                    {
        //                        master = new OnboardingDocumentMasters
        //                        {
        //                            CompanyId = request.CompanyId,
        //                            DocumentName = name,
        //                            DefaultIsMandatory = item.IsMandatory,
        //                            DefaultAllocatedDays = item.AllocatedDays,
        //                            IsActive = true,
        //                            CreatedBy = request.UserId,
        //                            CreatedOn = DateTime.UtcNow
        //                        };
        //                        _context.OnboardingDocumentMasters.Add(master);
        //                    }
        //                    masterByName.Add(name, master);
        //                }
        //                await _context.SaveChangesAsync();

        //                var now = DateTime.UtcNow;
        //                bool isNewOnboarding = onboarding == null;
        //                if (onboarding == null)
        //                {
        //                    onboarding = new CandidateOnboardings
        //                    {
        //                        CompanyId = request.CompanyId,
        //                        CandidateApplicationId = application.CandidateApplicationId,
        //                        TemplateIdAtStart = templateId,
        //                        Status = "ReadyForOnboarding",
        //                        IsActive = true,
        //                        CreatedBy = request.UserId,
        //                        CreatedOn = now
        //                    };
        //                    _context.CandidateOnboardings.Add(onboarding);
        //                    await _context.SaveChangesAsync();
        //                }

        //                string Snapshot(CandidateOnboardingDocuments document) =>
        //                    JsonSerializer.Serialize(new
        //                    {
        //                        document.CandidateOnboardingDocumentId,
        //                        document.OnboardingDocumentMasterId,
        //                        document.DocumentName,
        //                        document.IsMandatory,
        //                        document.AllocatedDays,
        //                        document.RequestedOn,
        //                        document.DueOn,
        //                        document.Instructions,
        //                        document.Status,
        //                        File = document.FileStorageKey,
        //                        document.SubmittedBy,
        //                        document.SubmittedOn,
        //                        document.ReceivedBy,
        //                        document.ReceivedOn,
        //                        document.CurrentSubmissionNumber,
        //                        document.ReceivedFromCandidate,
        //                        document.IsActive
        //                    });

        //                var changes = new List<(CandidateOnboardingDocuments Document,
        //                    string? Before, string? OldStatus, string Action)>();

        //                foreach (var item in request.Documents)
        //                {
        //                    int masterId = masterByName[item.DocumentName!].OnboardingDocumentMasterId;
        //                    if (item.CandidateOnboardingDocumentId == 0)
        //                    {
        //                        var document = new CandidateOnboardingDocuments
        //                        {
        //                            CompanyId = request.CompanyId,
        //                            CandidateOnboardingId = onboarding.CandidateOnboardingId,
        //                            OnboardingDocumentMasterId = masterId,
        //                            DocumentName = item.DocumentName!,
        //                            IsMandatory = item.IsMandatory,
        //                            AllocatedDays = item.AllocatedDays,
        //                            RequestedOn = now,
        //                            RequestedBy = request.UserId,
        //                            DueOn = now.AddDays(item.AllocatedDays),
        //                            Instructions = item.Instructions,
        //                            FileStorageKey = item.File,
        //                            Status = item.File == null ? "Requested" : "Submitted",
        //                            CurrentSubmissionNumber = item.File == null ? 0 : 1,
        //                            ReceivedFromCandidate = item.File != null,
        //                            ReceivedBy = item.File == null ? (int?)null : request.UserId,
        //                            ReceivedOn = item.File == null ? (DateTime?)null : now,
        //                            SubmittedBy = item.File == null ? (int?)null : request.UserId,
        //                            SubmittedOn = item.File == null ? (DateTime?)null : now,
        //                            IsActive = true,
        //                            CreatedBy = request.UserId,
        //                            CreatedOn = now
        //                        };
        //                        _context.CandidateOnboardingDocuments.Add(document);
        //                        changes.Add((document, null, null, item.File == null
        //                            ? "DocumentRequested" : "DocumentSubmitted"));
        //                    }
        //                    else
        //                    {
        //                        var document = existingById[item.CandidateOnboardingDocumentId];
        //                        bool daysChanged = document.AllocatedDays != item.AllocatedDays;
        //                        bool submitFile = item.File != null &&
        //                            (document.FileStorageKey != item.File ||
        //                             document.Status == "Requested" || document.Status == "ResubmissionRequired");
        //                        bool changed = submitFile || document.OnboardingDocumentMasterId != masterId || daysChanged || document.DocumentName != item.DocumentName ||
        //                            document.IsMandatory != item.IsMandatory ||
        //                            document.Instructions != item.Instructions;
        //                        if (!changed) continue;

        //                        string before = Snapshot(document);
        //                        string previousDocumentStatus = document.Status;
        //                        document.OnboardingDocumentMasterId = masterId;
        //                        if (submitFile)
        //                        {
        //                            document.FileStorageKey = item.File;
        //                            document.Status = "Submitted";
        //                            document.CurrentSubmissionNumber += 1;
        //                            document.ReceivedFromCandidate = true;
        //                            document.ReceivedBy = request.UserId;
        //                            document.ReceivedOn = now;
        //                            document.SubmittedBy = request.UserId;
        //                            document.SubmittedOn = now;
        //                            document.VerifiedBy = null;
        //                            document.VerifiedOn = null;
        //                        }
        //                        document.DocumentName = item.DocumentName!;
        //                        document.IsMandatory = item.IsMandatory;
        //                        document.Instructions = item.Instructions;
        //                        if (daysChanged)
        //                        {
        //                            // Days are TOTAL calendar days from original request time.
        //                            // Saving an unrelated edit never resets the due date.
        //                            document.AllocatedDays = item.AllocatedDays;
        //                            document.DueOn = document.RequestedOn.AddDays(item.AllocatedDays);
        //                        }
        //                        document.ModifiedBy = request.UserId;
        //                        document.ModifiedOn = now;
        //                        changes.Add((document, before, previousDocumentStatus,
        //                            submitFile ? "DocumentSubmitted" : "DocumentRequestUpdated"));
        //                    }
        //                }

        //                if (changes.Count > 0)
        //                {
        //                    string oldStatus = onboarding.Status;
        //                    onboarding.Status = "DocumentsPending";
        //                    if (!isNewOnboarding)
        //                    {
        //                        onboarding.ModifiedBy = request.UserId;
        //                        onboarding.ModifiedOn = now;
        //                    }
        //                    await _context.SaveChangesAsync();

        //                    foreach (var change in changes)
        //                    {
        //                        _context.CandidateOnboardingHistories.Add(new CandidateOnboardingHistories
        //                        {
        //                            CompanyId = request.CompanyId,
        //                            CandidateOnboardingId = onboarding.CandidateOnboardingId,
        //                            CandidateOnboardingDocumentId = change.Document.CandidateOnboardingDocumentId,
        //                            Action = change.Action,
        //                            PreviousStatus = change.OldStatus,
        //                            NewStatus = change.Document.Status,
        //                            PreviousDetailsJson = change.Before,
        //                            DetailsJson = Snapshot(change.Document),
        //                            IsRemarkAdded = false,
        //                            CreatedBy = request.UserId,
        //                            CreatedOn = now
        //                        });
        //                    }
        //                    if (oldStatus != onboarding.Status)
        //                    {
        //                        _context.CandidateOnboardingHistories.Add(new CandidateOnboardingHistories
        //                        {
        //                            CompanyId = request.CompanyId,
        //                            CandidateOnboardingId = onboarding.CandidateOnboardingId,
        //                            Action = "DocumentStageStarted",
        //                            PreviousStatus = oldStatus,
        //                            NewStatus = onboarding.Status,
        //                            DetailsJson = JsonSerializer.Serialize(new { onboarding.Status }),
        //                            CreatedBy = request.UserId,
        //                            CreatedOn = now
        //                        });
        //                    }
        //                    await _context.SaveChangesAsync();
        //                }

        //                var savedDocuments = await _context.CandidateOnboardingDocuments.AsNoTracking()
        //                    .Where(x => x.CompanyId == request.CompanyId &&
        //                        x.CandidateOnboardingId == onboarding.CandidateOnboardingId && x.IsActive)
        //                    .OrderBy(x => x.CandidateOnboardingDocumentId)
        //                    .Select(x => new
        //                    {
        //                        x.CandidateOnboardingDocumentId,
        //                        x.OnboardingDocumentMasterId,
        //                        x.DocumentName,
        //                        x.IsMandatory,
        //                        x.AllocatedDays,
        //                        x.RequestedOn,
        //                        x.RequestedBy,
        //                        x.DueOn,
        //                        x.Instructions,
        //                        x.Status,
        //                        File = x.FileStorageKey,
        //                        x.SubmittedBy,
        //                        x.SubmittedOn,
        //                        x.CurrentSubmissionNumber,
        //                        x.ReceivedFromCandidate,
        //                        x.CreatedBy,
        //                        x.CreatedOn,
        //                        x.ModifiedBy,
        //                        x.ModifiedOn,
        //                        x.RowVersion
        //                    }).ToListAsync();

        //                var history = await _context.CandidateOnboardingHistories.AsNoTracking()
        //                    .Where(x => x.CompanyId == request.CompanyId &&
        //                        x.CandidateOnboardingId == onboarding.CandidateOnboardingId &&
        //                        (x.Action == "DocumentRequested" || x.Action == "DocumentRequestUpdated" ||
        //                         x.Action == "DocumentSubmitted" ||
        //                         x.Action == "DocumentStageStarted"))
        //                    .OrderByDescending(x => x.CreatedOn)
        //                    .ThenByDescending(x => x.CandidateOnboardingHistoryId)
        //                    .Select(x => new
        //                    {
        //                        x.CandidateOnboardingHistoryId,
        //                        x.CandidateOnboardingDocumentId,
        //                        x.Action,
        //                        x.PreviousStatus,
        //                        x.NewStatus,
        //                        x.PreviousDetailsJson,
        //                        x.DetailsJson,
        //                        x.CreatedBy,
        //                        x.CreatedOn
        //                    }).ToListAsync();

        //                await transaction.CommitAsync();

        //                return Ok(new
        //                {
        //                    Status = true,
        //                    Message = changes.Count == 0
        //                        ? "No document request changes were found."
        //                        : "Document requests saved successfully.",
        //                    Data = new
        //                    {
        //                        onboarding.CandidateOnboardingId,
        //                        request.CompanyId,
        //                        request.CandidateId,
        //                        request.JobDescriptionId,
        //                        application.CandidateApplicationId,
        //                        onboarding.Status,
        //                        onboarding.RowVersion,
        //                        ChangedDocumentCount = changes.Count,
        //                        Documents = savedDocuments,
        //                        History = history
        //                    }
        //                });
        //            }
        //            catch (DbUpdateConcurrencyException)
        //            {
        //                return Fail(409, "The record changed. Refresh the latest data and try again.");
        //            }
        //            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
        //                (sql.Number == 2601 || sql.Number == 2627))
        //            {
        //                return Fail(409, "A duplicate onboarding record or document request already exists.");
        //            }
        //            catch (Exception ex)
        //            {
        //                Serilog.Log.Error(ex,
        //                    "Failed to save onboarding document requests for CompanyId {CompanyId}, CandidateId {CandidateId}, JobId {JobId}.",
        //                    request.CompanyId, request.CandidateId, request.JobDescriptionId);
        //                return Fail(500, "Unable to save onboarding document requests.");
        //            }
        //        }

        //    public async Task<IActionResult> SaveOnboardingDocument(
        //[FromBody] SaveOnboardingDocumentRequest? request)
        //    {
        //        IActionResult Fail(int code, string message) =>
        //            StatusCode(code, new { Status = false, Message = message });

        //        if (request == null || request.CompanyId <= 0 || request.UserId <= 0 ||
        //            request.CandidateId <= 0 || request.JobDescriptionId <= 0 ||
        //            request.CandidateApplicationId < 0)
        //            return Fail(400, "Provide valid company, user, candidate and job IDs.");

        //        if (request.Documents == null || request.Documents.Count == 0)
        //            return Fail(400, "At least one document request is required.");

        //        foreach (var item in request.Documents)
        //        {
        //            if (item == null)
        //                return Fail(400, "Document request cannot be null.");
        //            if (item.CandidateOnboardingDocumentId < 0)
        //                return Fail(400, "Invalid document ID.");
        //            if (item.AllocatedDays <= 0 || item.AllocatedDays > 365)
        //                return Fail(400, "AllocatedDays must be between 1 and 365.");

        //            item.DocumentName = item.DocumentName?.Trim();
        //            item.Instructions = item.Instructions?.Trim();
        //            item.File = string.IsNullOrWhiteSpace(item.File) ? null : item.File.Trim();
        //            if (item.File?.Length > 1000)
        //                return Fail(400, "File reference cannot exceed 1000 characters.");

        //            if (string.IsNullOrWhiteSpace(item.DocumentName))
        //                return Fail(400, "DocumentName is required.");
        //            if (item.DocumentName?.Length > 200)
        //                return Fail(400, "DocumentName cannot exceed 200 characters.");
        //            if (item.CandidateOnboardingDocumentId > 0 &&
        //                (item.RowVersion == null || item.RowVersion.Length != 8))
        //                return Fail(400, "Send the current RowVersion when editing a document request.");
        //        }

        //        if (request.Documents.Where(x => x.CandidateOnboardingDocumentId > 0)
        //            .GroupBy(x => x.CandidateOnboardingDocumentId).Any(g => g.Count() > 1))
        //            return Fail(400, "A document request ID cannot repeat.");

        //        try
        //        {
        //            await using var transaction = await _context.Database
        //                .BeginTransactionAsync(IsolationLevel.Serializable);

        //            var user = await _context.EmployeeMasters.AsNoTracking()
        //                .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
        //                    x.EmployeeId == request.UserId);
        //            if (user == null) return Fail(404, "Employee not found for this company.");
        //            if (user.IsActive != true) return Fail(403, "Employee is inactive.");

        //            var candidatesExist = await _context.Candidates.AsNoTracking()
        //                .AnyAsync(x => x.CompanyId == request.CompanyId &&
        //                    x.CandidateId == request.CandidateId && x.IsActive == true);
        //            if (!candidatesExist) return Fail(404, "Active candidate not found.");

        //            var job = await _context.JobDescriptions.AsNoTracking()
        //                .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
        //                    x.JobDescriptionId == request.JobDescriptionId);
        //            if (job == null) return Fail(404, "Job not found for this company.");

        //            int? templateId = job.TemplateId;
        //            if (!templateId.HasValue || !await _context.RoleTemplates.AsNoTracking()
        //                .AnyAsync(x => x.CompanyId == request.CompanyId &&
        //                    x.TemplateId == templateId.Value && x.IsActive == true))
        //                return Fail(409, "The job must have an active recruitment template.");

        //            var applicationsQuery = _context.CandidateApplications.Where(x =>
        //                x.CompanyId == request.CompanyId &&
        //                x.CandidateId == request.CandidateId &&
        //                x.JobDescriptionId == request.JobDescriptionId);
        //            if (request.CandidateApplicationId > 0)
        //                applicationsQuery = applicationsQuery.Where(x =>
        //                    x.CandidateApplicationId == request.CandidateApplicationId);
        //            var applications = await applicationsQuery.Take(2).ToListAsync();
        //            if (applications.Count == 0) return Fail(404, "Candidate application not found.");
        //            if (applications.Count > 1)
        //                return Fail(409, "Multiple applications exist. Send CandidateApplicationId.");
        //            var application = applications[0];
        //            if (application.IsActive != true)
        //                return Fail(409, "Candidate application is inactive.");

        //            if (user.IsAdmin != true)
        //            {
        //                var teamIds = await (
        //                    from member in _context.OnboardingTeamMembers.AsNoTracking()
        //                    join team in _context.OnboardingTeams.AsNoTracking()
        //                        on new { member.CompanyId, member.OnboardingTeamId }
        //                        equals new { team.CompanyId, team.OnboardingTeamId }
        //                    where member.CompanyId == request.CompanyId &&
        //                        member.EmployeeId == request.UserId &&
        //                        member.IsActive && team.IsActive
        //                    select team.OnboardingTeamId
        //                ).Distinct().ToListAsync();
        //                var scopes = await _context.OnboardingTeamTemplates.AsNoTracking()
        //                    .Where(x => x.CompanyId == request.CompanyId &&
        //                        teamIds.Contains(x.OnboardingTeamId)).ToListAsync();
        //                bool allowed = teamIds.Any(id =>
        //                    !scopes.Any(x => x.OnboardingTeamId == id) ||
        //                    scopes.Any(x => x.OnboardingTeamId == id &&
        //                        x.TemplateId == templateId.Value));
        //                if (!allowed)
        //                    return Fail(403, "You are not assigned to onboarding for this template.");
        //            }

        //            // Same eligibility rule as GetOnboardingCandidateList.
        //            var assignments = await _context.RoleTemplateAssignment.AsNoTracking()
        //                .Where(x => x.CompanyId == request.CompanyId &&
        //                    x.TemplateId == templateId.Value && x.IsActive == true &&
        //                    x.CanApprove == true).ToListAsync();
        //            if (assignments.Count == 0)
        //                return Fail(409, "The template has no active approval assignments.");

        //            var assignmentIds = assignments.Select(x => x.AssignmentId).ToArray();
        //            var decisions = await _context.CandidateSkillAssessmentHistories.AsNoTracking()
        //                .Where(x => x.CompanyId == request.CompanyId &&
        //                    x.CandidateApplicationId == application.CandidateApplicationId &&
        //                    x.JobDescriptionId == request.JobDescriptionId &&
        //                    x.AssignmentId.HasValue && assignmentIds.Contains(x.AssignmentId.Value) &&
        //                    (x.Action == "Approve" || x.Action == "Reject" || x.Action == "Return"))
        //                .ToListAsync();
        //            if (assignments.Any(a =>
        //                decisions.Where(x => x.AssignmentId == a.AssignmentId &&
        //                        x.CreatedBy == a.EmployeeId)
        //                    .OrderByDescending(x => x.CreatedOn)
        //                    .ThenByDescending(x => x.CandidateSkillAssessmentHistoryId)
        //                    .FirstOrDefault()?.Action != "Approve"))
        //                return Fail(409, "All required hierarchy approvals must be completed.");

        //            var onboarding = await _context.CandidateOnboardings
        //                .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
        //                    x.CandidateApplicationId == application.CandidateApplicationId);

        //            if (onboarding != null &&
        //                (!onboarding.IsActive || !string.IsNullOrWhiteSpace(onboarding.FinalOutcome)))
        //                return Fail(409, "This onboarding case is inactive or already closed.");

        //            // Do not reopen later completed stages by silently adding new requirements.
        //            if (onboarding != null &&
        //                onboarding.Status != "ReadyForOnboarding" &&
        //                onboarding.Status != "DocumentsPending")
        //                return Fail(409, "Document requests can be changed only before advancing beyond the document stage.");

        //            var existing = onboarding == null
        //                ? new List<CandidateOnboardingDocuments>()
        //                : await _context.CandidateOnboardingDocuments
        //                    .Where(x => x.CompanyId == request.CompanyId &&
        //                        x.CandidateOnboardingId == onboarding.CandidateOnboardingId)
        //                    .ToListAsync();
        //            var existingById = existing.ToDictionary(x => x.CandidateOnboardingDocumentId);

        //            foreach (var item in request.Documents)
        //            {
        //                CandidateOnboardingDocuments? saved = null;
        //                if (item.CandidateOnboardingDocumentId > 0)
        //                {
        //                    if (!existingById.TryGetValue(item.CandidateOnboardingDocumentId, out saved))
        //                        return Fail(404, "Document request not found for this onboarding case.");
        //                    if (!saved.IsActive || (saved.Status != "Requested" &&
        //                        saved.Status != "Submitted" && saved.Status != "ResubmissionRequired"))
        //                        return Fail(409, "Verified or inactive documents cannot be edited here.");
        //                    if (!string.Equals(saved.DocumentName, item.DocumentName, StringComparison.Ordinal) &&
        //                        (saved.CurrentSubmissionNumber > 0 || !string.IsNullOrEmpty(saved.FileStorageKey)))
        //                        return Fail(409, "A document with submissions cannot be renamed. Add a new request instead.");
        //                    if (!saved.RowVersion.SequenceEqual(item.RowVersion!))
        //                        return Fail(409, "Document request changed. Refresh and try again.");
        //                }


        //                if (string.IsNullOrWhiteSpace(item.DocumentName) || item.DocumentName.Length > 200)
        //                    return Fail(400, "Every document must have a valid name up to 200 characters.");
        //            }

        //            // Validate the final combined selection. Omitted rows remain unchanged.
        //            var editedIds = request.Documents.Where(x => x.CandidateOnboardingDocumentId > 0)
        //                .Select(x => x.CandidateOnboardingDocumentId).ToHashSet();
        //            var finalNames = existing.Where(x => x.IsActive && !editedIds.Contains(x.CandidateOnboardingDocumentId))
        //                .Select(x => x.DocumentName.Trim())
        //                .Concat(request.Documents.Select(x => x.DocumentName!));
        //            if (finalNames.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
        //                return Fail(409, "An active request for this document name already exists.");


        //            // File references must already be registered by the upload API for
        //            // this company and exact candidate application. No URL is fetched here.
        //            var suppliedFiles = request.Documents.Where(x => x.File != null)
        //                .Select(x => x.File!).Distinct().ToArray();
        //            var registeredFiles = await _context.CandidateDocuments.AsNoTracking()
        //                .Where(x => x.CompanyId == request.CompanyId &&
        //                    x.CandidateApplicationId == application.CandidateApplicationId &&
        //                    x.IsActive == true && suppliedFiles.Contains(x.StorageKey))
        //                .Select(x => x.StorageKey).ToListAsync();
        //            if (suppliedFiles.Any(file => !registeredFiles.Contains(file)))
        //                return Fail(400, "A file is not registered as an active upload for this candidate application.");

        //            // Find or create company masters. Existing defaults are never overwritten.
        //            var masterByName = new Dictionary<string, OnboardingDocumentMasters>(
        //                StringComparer.OrdinalIgnoreCase);
        //            foreach (var item in request.Documents)
        //            {
        //                string name = item.DocumentName!;
        //                if (masterByName.ContainsKey(name)) continue;
        //                var master = await _context.OnboardingDocumentMasters
        //                    .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId &&
        //                        EF.Functions.Collate(x.DocumentName.Trim(),
        //                            "SQL_Latin1_General_CP1_CI_AS") == name);
        //                if (master != null && !master.IsActive)
        //                    return Fail(409, $"Document type '{name}' is inactive. Reactivate it before use.");
        //                if (master == null)
        //                {
        //                    master = new OnboardingDocumentMasters
        //                    {
        //                        CompanyId = request.CompanyId,
        //                        DocumentName = name,
        //                        DefaultIsMandatory = item.IsMandatory,
        //                        DefaultAllocatedDays = item.AllocatedDays,
        //                        IsActive = true,
        //                        CreatedBy = request.UserId,
        //                        CreatedOn = DateTime.UtcNow
        //                    };
        //                    _context.OnboardingDocumentMasters.Add(master);
        //                }
        //                masterByName.Add(name, master);
        //            }
        //            await _context.SaveChangesAsync();

        //            var now = DateTime.UtcNow;
        //            bool isNewOnboarding = onboarding == null;
        //            if (onboarding == null)
        //            {
        //                onboarding = new CandidateOnboardings
        //                {
        //                    CompanyId = request.CompanyId,
        //                    CandidateApplicationId = application.CandidateApplicationId,
        //                    TemplateIdAtStart = templateId,
        //                    Status = "ReadyForOnboarding",
        //                    IsActive = true,
        //                    CreatedBy = request.UserId,
        //                    CreatedOn = now
        //                };
        //                _context.CandidateOnboardings.Add(onboarding);
        //                await _context.SaveChangesAsync();
        //            }

        //            string Snapshot(CandidateOnboardingDocuments document) =>
        //                JsonSerializer.Serialize(new
        //                {
        //                    document.CandidateOnboardingDocumentId,
        //                    document.OnboardingDocumentMasterId,
        //                    document.DocumentName,
        //                    document.IsMandatory,
        //                    document.AllocatedDays,
        //                    document.RequestedOn,
        //                    document.DueOn,
        //                    document.Instructions,
        //                    document.Status,
        //                    File = document.FileStorageKey,
        //                    document.SubmittedBy,
        //                    document.SubmittedOn,
        //                    document.ReceivedBy,
        //                    document.ReceivedOn,
        //                    document.CurrentSubmissionNumber,
        //                    document.ReceivedFromCandidate,
        //                    document.IsActive
        //                });

        //            var changes = new List<(CandidateOnboardingDocuments Document,
        //                string? Before, string? OldStatus, string Action)>();

        //            foreach (var item in request.Documents)
        //            {
        //                int masterId = masterByName[item.DocumentName!].OnboardingDocumentMasterId;
        //                if (item.CandidateOnboardingDocumentId == 0)
        //                {
        //                    var document = new CandidateOnboardingDocuments
        //                    {
        //                        CompanyId = request.CompanyId,
        //                        CandidateOnboardingId = onboarding.CandidateOnboardingId,
        //                        OnboardingDocumentMasterId = masterId,
        //                        DocumentName = item.DocumentName!,
        //                        IsMandatory = item.IsMandatory,
        //                        AllocatedDays = item.AllocatedDays,
        //                        RequestedOn = now,
        //                        RequestedBy = request.UserId,
        //                        DueOn = now.AddDays(item.AllocatedDays),
        //                        Instructions = item.Instructions,
        //                        FileStorageKey = item.File,
        //                        Status = item.File == null ? "Requested" : "Submitted",
        //                        CurrentSubmissionNumber = item.File == null ? 0 : 1,
        //                        ReceivedFromCandidate = item.File != null,
        //                        ReceivedBy = item.File == null ? (int?)null : request.UserId,
        //                        ReceivedOn = item.File == null ? (DateTime?)null : now,
        //                        SubmittedBy = item.File == null ? (int?)null : request.UserId,
        //                        SubmittedOn = item.File == null ? (DateTime?)null : now,
        //                        IsActive = true,
        //                        CreatedBy = request.UserId,
        //                        CreatedOn = now
        //                    };
        //                    _context.CandidateOnboardingDocuments.Add(document);
        //                    changes.Add((document, null, null, item.File == null
        //                        ? "DocumentRequested" : "DocumentSubmitted"));
        //                }
        //                else
        //                {
        //                    var document = existingById[item.CandidateOnboardingDocumentId];
        //                    bool daysChanged = document.AllocatedDays != item.AllocatedDays;
        //                    bool submitFile = item.File != null &&
        //                        (document.FileStorageKey != item.File ||
        //                         document.Status == "Requested" || document.Status == "ResubmissionRequired");
        //                    bool changed = submitFile || document.OnboardingDocumentMasterId != masterId || daysChanged || document.DocumentName != item.DocumentName ||
        //                        document.IsMandatory != item.IsMandatory ||
        //                        document.Instructions != item.Instructions;
        //                    if (!changed) continue;

        //                    string before = Snapshot(document);
        //                    string previousDocumentStatus = document.Status;
        //                    document.OnboardingDocumentMasterId = masterId;
        //                    if (submitFile)
        //                    {
        //                        document.FileStorageKey = item.File;
        //                        document.Status = "Submitted";
        //                        document.CurrentSubmissionNumber += 1;
        //                        document.ReceivedFromCandidate = true;
        //                        document.ReceivedBy = request.UserId;
        //                        document.ReceivedOn = now;
        //                        document.SubmittedBy = request.UserId;
        //                        document.SubmittedOn = now;
        //                        document.VerifiedBy = null;
        //                        document.VerifiedOn = null;
        //                    }
        //                    document.DocumentName = item.DocumentName!;
        //                    document.IsMandatory = item.IsMandatory;
        //                    document.Instructions = item.Instructions;
        //                    if (daysChanged)
        //                    {
        //                        // Days are TOTAL calendar days from original request time.
        //                        // Saving an unrelated edit never resets the due date.
        //                        document.AllocatedDays = item.AllocatedDays;
        //                        document.DueOn = document.RequestedOn.AddDays(item.AllocatedDays);
        //                    }
        //                    document.ModifiedBy = request.UserId;
        //                    document.ModifiedOn = now;
        //                    changes.Add((document, before, previousDocumentStatus,
        //                        submitFile ? "DocumentSubmitted" : "DocumentRequestUpdated"));
        //                }
        //            }

        //            if (changes.Count > 0)
        //            {
        //                string oldStatus = onboarding.Status;
        //                onboarding.Status = "DocumentsPending";
        //                if (!isNewOnboarding)
        //                {
        //                    onboarding.ModifiedBy = request.UserId;
        //                    onboarding.ModifiedOn = now;
        //                }
        //                await _context.SaveChangesAsync();

        //                foreach (var change in changes)
        //                {
        //                    _context.CandidateOnboardingHistories.Add(new CandidateOnboardingHistories
        //                    {
        //                        CompanyId = request.CompanyId,
        //                        CandidateOnboardingId = onboarding.CandidateOnboardingId,
        //                        CandidateOnboardingDocumentId = change.Document.CandidateOnboardingDocumentId,
        //                        Action = change.Action,
        //                        PreviousStatus = change.OldStatus,
        //                        NewStatus = change.Document.Status,
        //                        PreviousDetailsJson = change.Before,
        //                        DetailsJson = Snapshot(change.Document),
        //                        IsRemarkAdded = false,
        //                        CreatedBy = request.UserId,
        //                        CreatedOn = now
        //                    });
        //                }
        //                if (oldStatus != onboarding.Status)
        //                {
        //                    _context.CandidateOnboardingHistories.Add(new CandidateOnboardingHistories
        //                    {
        //                        CompanyId = request.CompanyId,
        //                        CandidateOnboardingId = onboarding.CandidateOnboardingId,
        //                        Action = "DocumentStageStarted",
        //                        PreviousStatus = oldStatus,
        //                        NewStatus = onboarding.Status,
        //                        DetailsJson = JsonSerializer.Serialize(new { onboarding.Status }),
        //                        CreatedBy = request.UserId,
        //                        CreatedOn = now
        //                    });
        //                }
        //                await _context.SaveChangesAsync();
        //            }

        //            var savedDocuments = await _context.CandidateOnboardingDocuments.AsNoTracking()
        //                .Where(x => x.CompanyId == request.CompanyId &&
        //                    x.CandidateOnboardingId == onboarding.CandidateOnboardingId && x.IsActive)
        //                .OrderBy(x => x.CandidateOnboardingDocumentId)
        //                .Select(x => new
        //                {
        //                    x.CandidateOnboardingDocumentId,
        //                    x.OnboardingDocumentMasterId,
        //                    x.DocumentName,
        //                    x.IsMandatory,
        //                    x.AllocatedDays,
        //                    x.RequestedOn,
        //                    x.RequestedBy,
        //                    x.DueOn,
        //                    x.Instructions,
        //                    x.Status,
        //                    File = x.FileStorageKey,
        //                    x.SubmittedBy,
        //                    x.SubmittedOn,
        //                    x.CurrentSubmissionNumber,
        //                    x.ReceivedFromCandidate,
        //                    x.CreatedBy,
        //                    x.CreatedOn,
        //                    x.ModifiedBy,
        //                    x.ModifiedOn,
        //                    x.RowVersion
        //                }).ToListAsync();

        //            var history = await _context.CandidateOnboardingHistories.AsNoTracking()
        //                .Where(x => x.CompanyId == request.CompanyId &&
        //                    x.CandidateOnboardingId == onboarding.CandidateOnboardingId &&
        //                    (x.Action == "DocumentRequested" || x.Action == "DocumentRequestUpdated" ||
        //                     x.Action == "DocumentSubmitted" ||
        //                     x.Action == "DocumentStageStarted"))
        //                .OrderByDescending(x => x.CreatedOn)
        //                .ThenByDescending(x => x.CandidateOnboardingHistoryId)
        //                .Select(x => new
        //                {
        //                    x.CandidateOnboardingHistoryId,
        //                    x.CandidateOnboardingDocumentId,
        //                    x.Action,
        //                    x.PreviousStatus,
        //                    x.NewStatus,
        //                    x.PreviousDetailsJson,
        //                    x.DetailsJson,
        //                    x.CreatedBy,
        //                    x.CreatedOn
        //                }).ToListAsync();

        //            await transaction.CommitAsync();

        //            return Ok(new
        //            {
        //                Status = true,
        //                Message = changes.Count == 0
        //                    ? "No document request changes were found."
        //                    : "Document requests saved successfully.",
        //                Data = new
        //                {
        //                    onboarding.CandidateOnboardingId,
        //                    request.CompanyId,
        //                    request.CandidateId,
        //                    request.JobDescriptionId,
        //                    application.CandidateApplicationId,
        //                    onboarding.Status,
        //                    onboarding.RowVersion,
        //                    ChangedDocumentCount = changes.Count,
        //                    Documents = savedDocuments,
        //                    History = history
        //                }
        //            });
        //        }
        //        catch (DbUpdateConcurrencyException)
        //        {
        //            return Fail(409, "The record changed. Refresh the latest data and try again.");
        //        }
        //        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
        //            (sql.Number == 2601 || sql.Number == 2627))
        //        {
        //            return Fail(409, "A duplicate onboarding record or document request already exists.");
        //        }
        //        catch (Exception ex)
        //        {
        //            Serilog.Log.Error(ex,
        //                "Failed to save onboarding document requests for CompanyId {CompanyId}, CandidateId {CandidateId}, JobId {JobId}.",
        //                request.CompanyId, request.CandidateId, request.JobDescriptionId);
        //            return Fail(500, "Unable to save onboarding document requests.");
        //        }
        //    }

        [HttpPost("GetCompanyOnboardingDocuments")]
        public async Task<IActionResult> GetCompanyOnboardingDocuments(
    [FromBody] GetCompanyOnboardingDocumentsRequest? request)
        {
            IActionResult Fail(int code, string message) =>
                StatusCode(code, new
                {
                    Status = false,
                    Message = message
                });

            if (request == null ||
                request.CompanyId <= 0 ||
                request.UserId <= 0)
            {
                return Fail(400, "Valid CompanyId and UserId are required.");
            }

            try
            {
                var user = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.IsActive,
                        x.IsAdmin
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                if (user.IsAdmin != true)
                {
                    bool hasTeamAccess = await (
                        from member in _context.OnboardingTeamMembers
                            .AsNoTracking()

                        join team in _context.OnboardingTeams
                            .AsNoTracking()
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

                        where member.CompanyId == request.CompanyId
                              && member.EmployeeId == request.UserId
                              && member.IsActive
                              && team.IsActive

                        select member.OnboardingTeamMemberId
                    ).AnyAsync();

                    if (!hasTeamAccess)
                    {
                        return Fail(
                            403,
                            "An active onboarding team assignment is required.");
                    }
                }

                var data = await _context.OnboardingDocumentMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.IsActive)
                    .OrderBy(x => x.DocumentName)
                    .Select(x => new
                    {
                        x.OnboardingDocumentMasterId,
                        x.CompanyId,
                        x.DocumentName,
                        x.DefaultIsMandatory,
                        x.DefaultAllocatedDays,
                        x.IsActive,
                        x.CreatedBy,
                        x.CreatedOn,
                        x.ModifiedBy,
                        x.ModifiedOn
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Company documents fetched successfully."
                        : "No company documents found.",

                    request.CompanyId,
                    TotalCount = data.Count,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch company onboarding documents for CompanyId {CompanyId}, UserId {UserId}.",
                    request.CompanyId,
                    request.UserId);

                return Fail(500, "Unable to fetch company documents.");
            }
        }
        [HttpPost("GetCandidateOnboardingDocuments")]
        public async Task<IActionResult> GetCandidateOnboardingDocuments(
    [FromBody] GetCandidateOnboardingDocumentsRequest? request)
        {
            IActionResult Fail(int code, string message) =>
                StatusCode(code, new
                {
                    Status = false,
                    Message = message
                });

            if (request == null ||
                request.CompanyId <= 0 ||
                request.UserId <= 0 ||
                request.CandidateId <= 0 ||
                request.JobDescriptionId <= 0 ||
                request.CandidateApplicationId < 0)
            {
                return Fail(
                    400,
                    "Provide valid company, user, candidate and job IDs.");
            }

            try
            {
                var user = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.IsActive,
                        x.IsAdmin
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                var candidate = await _context.Candidates
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId)
                    .Select(x => new
                    {
                        x.FirstName,
                        x.MiddleName,
                        x.LastName
                    })
                    .FirstOrDefaultAsync();

                if (candidate == null)
                    return Fail(404, "Candidate not found for this company.");

                var job = await _context.JobDescriptions
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.JobDescriptionId == request.JobDescriptionId)
                    .Select(x => new
                    {
                        x.JobTitle,
                        TemplateId = (int?)x.TemplateId
                    })
                    .FirstOrDefaultAsync();

                if (job == null)
                    return Fail(404, "Job not found for this company.");

                var applicationQuery = _context.CandidateApplications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateId == request.CandidateId &&
                        x.JobDescriptionId == request.JobDescriptionId);

                if (request.CandidateApplicationId > 0)
                {
                    applicationQuery = applicationQuery.Where(x =>
                        x.CandidateApplicationId ==
                            request.CandidateApplicationId);
                }

                var applications = await applicationQuery
                    .Select(x => x.CandidateApplicationId)
                    .Take(2)
                    .ToListAsync();

                if (applications.Count == 0)
                    return Fail(404, "Candidate application not found.");

                if (applications.Count > 1)
                {
                    return Fail(
                        409,
                        "Multiple applications exist. Send CandidateApplicationId.");
                }

                int applicationId = applications[0];

                if (user.IsAdmin != true)
                {
                    var teamIds = await (
                        from member in _context.OnboardingTeamMembers
                            .AsNoTracking()

                        join team in _context.OnboardingTeams
                            .AsNoTracking()
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

                        where member.CompanyId == request.CompanyId
                              && member.EmployeeId == request.UserId
                              && member.IsActive
                              && team.IsActive

                        select team.OnboardingTeamId
                    )
                    .Distinct()
                    .ToListAsync();

                    var mappings = await _context.OnboardingTeamTemplates
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

                    bool hasAccess = teamIds.Any(teamId =>
                        // No template mappings means all company templates.
                        !mappings.Any(x =>
                            x.OnboardingTeamId == teamId)
                        ||
                        (
                            job.TemplateId.HasValue &&
                            mappings.Any(x =>
                                x.OnboardingTeamId == teamId &&
                                x.TemplateId == job.TemplateId.Value)
                        ));

                    if (!hasAccess)
                    {
                        return Fail(
                            403,
                            "You are not assigned to onboarding for this candidate's template.");
                    }
                }

                var onboarding = await _context.CandidateOnboardings
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateApplicationId == applicationId)
                    .Select(x => new
                    {
                        x.CandidateOnboardingId,
                        x.Status,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                string candidateName = string.Join(
                    " ",
                    new[]
                    {
                candidate.FirstName,
                candidate.MiddleName,
                candidate.LastName
                    }.Where(x => !string.IsNullOrWhiteSpace(x)));

                if (onboarding == null)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "No candidate document requests found.",

                        request.CompanyId,
                        request.CandidateId,
                        CandidateName = candidateName,
                        request.JobDescriptionId,
                        job.JobTitle,
                        CandidateApplicationId = applicationId,

                        CandidateOnboardingId = (int?)null,
                        OnboardingStatus = (string?)null,
                        OnboardingIsActive = (bool?)null,

                        TotalCount = 0,
                        RequestedCount = 0,
                        SubmittedCount = 0,
                        VerifiedCount = 0,
                        ResubmissionRequiredCount = 0,
                        OverdueCount = 0,

                        Data = Array.Empty<object>()
                    });
                }

                var documents = await _context.CandidateOnboardingDocuments
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateOnboardingId ==
                            onboarding.CandidateOnboardingId &&
                        x.IsActive)
                    .OrderBy(x => x.CandidateOnboardingDocumentId)
                    .ToListAsync();

                var documentIds = documents
                    .Select(x => x.CandidateOnboardingDocumentId)
                    .ToArray();

                var histories = await _context.CandidateOnboardingHistories
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateOnboardingId ==
                            onboarding.CandidateOnboardingId &&
                        x.CandidateOnboardingDocumentId.HasValue &&
                        documentIds.Contains(
                            x.CandidateOnboardingDocumentId.Value))
                    .OrderByDescending(x => x.CreatedOn)
                    .ThenByDescending(x => x.CandidateOnboardingHistoryId)
                    .ToListAsync();

                var employeeIds = documents
                    .SelectMany(x => new int?[]
                    {
                x.RequestedBy,
                x.ReceivedBy,
                x.SubmittedBy,
                x.VerifiedBy,
                x.CreatedBy,
                x.ModifiedBy
                    })
                    .Concat(histories.Select(x => (int?)x.CreatedBy))
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
                        Name = ((x.FirstName ?? "") + " " +
                                (x.LastName ?? "")).Trim()
                    })
                    .ToDictionaryAsync(x => x.EmployeeId, x => x.Name);

                string? GetEmployeeName(int? employeeId)
                {
                    return employeeId.HasValue &&
                           employeeNames.TryGetValue(
                               employeeId.Value, out var name)
                        ? name
                        : null;
                }

                var historiesByDocument = histories.ToLookup(x =>
                    x.CandidateOnboardingDocumentId!.Value);

                var now = DateTime.UtcNow;

                var data = documents.Select(document => new
                {
                    document.CandidateOnboardingDocumentId,
                    document.CandidateOnboardingId,
                    document.OnboardingDocumentMasterId,
                    document.CompanyId,

                    request.CandidateId,
                    request.JobDescriptionId,
                    CandidateApplicationId = applicationId,

                    document.DocumentName,
                    document.IsMandatory,
                    document.AllocatedDays,
                    document.Instructions,

                    // Existing stored file reference; not a generated download URL.
                    File = document.FileStorageKey,

                    document.Status,
                    document.CurrentSubmissionNumber,
                    document.IsActive,

                    document.RequestedOn,
                    document.RequestedBy,
                    RequestedByName =
                        GetEmployeeName(document.RequestedBy),

                    document.DueOn,

                    IsOverdue =
                        document.DueOn < now &&
                        (
                            document.Status == "Requested" ||
                            document.Status == "ResubmissionRequired"
                        ),

                    document.ReceivedFromCandidate,
                    document.ReceivedOn,
                    document.ReceivedBy,
                    ReceivedByName =
                        GetEmployeeName(document.ReceivedBy),

                    document.SubmittedOn,
                    document.SubmittedBy,
                    SubmittedByName =
                        GetEmployeeName(document.SubmittedBy),

                    document.VerifiedOn,
                    document.VerifiedBy,
                    VerifiedByName =
                        GetEmployeeName(document.VerifiedBy),

                    document.ReviewRemarks,
                    document.DetailsJson,

                    document.CreatedOn,
                    document.CreatedBy,
                    CreatedByName =
                        GetEmployeeName(document.CreatedBy),

                    document.ModifiedOn,
                    document.ModifiedBy,
                    ModifiedByName =
                        GetEmployeeName(document.ModifiedBy),

                    // Use this document's RowVersion in the edit request.
                    document.RowVersion,

                    History = historiesByDocument[
                        document.CandidateOnboardingDocumentId]
                        .Select(history => new
                        {
                            history.CandidateOnboardingHistoryId,
                            history.CandidateOnboardingDocumentId,
                            history.Action,
                            history.PreviousStatus,
                            history.NewStatus,
                            history.PreviousDetailsJson,
                            history.DetailsJson,
                            history.Remarks,
                            history.IsRemarkAdded,
                            history.CreatedBy,
                            CreatedByName =
                                GetEmployeeName(history.CreatedBy),
                            history.CreatedOn
                        })
                        .ToList()
                }).ToList();

                return Ok(new
                {
                    Status = true,
                    Message = data.Count > 0
                        ? "Candidate documents fetched successfully."
                        : "No candidate document requests found.",

                    request.CompanyId,
                    request.CandidateId,
                    CandidateName = candidateName,
                    request.JobDescriptionId,
                    job.JobTitle,
                    CandidateApplicationId = applicationId,

                    onboarding.CandidateOnboardingId,
                    OnboardingStatus = onboarding.Status,
                    OnboardingIsActive = onboarding.IsActive,

                    TotalCount = data.Count,
                    RequestedCount = data.Count(x => x.Status == "Requested"),
                    SubmittedCount = data.Count(x => x.Status == "Submitted"),
                    VerifiedCount = data.Count(x => x.Status == "Verified"),
                    ResubmissionRequiredCount = data.Count(x =>
                        x.Status == "ResubmissionRequired"),
                    OverdueCount = data.Count(x => x.IsOverdue),

                    Data = data
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to fetch candidate onboarding documents for CompanyId {CompanyId}, CandidateId {CandidateId}, JobDescriptionId {JobDescriptionId}.",
                    request.CompanyId,
                    request.CandidateId,
                    request.JobDescriptionId);

                return Fail(500, "Unable to fetch candidate documents.");
            }
        }




        [HttpPost("DeleteCandidateOnboardingDocument")]
        public async Task<IActionResult> DeleteCandidateOnboardingDocument(
    [FromBody] DeleteCandidateOnboardingDocumentRequest? request)
        {
            IActionResult Fail(int code, string message) =>
                StatusCode(code, new
                {
                    Status = false,
                    Message = message
                });

            if (request == null ||
                request.CompanyId <= 0 ||
                request.UserId <= 0 ||
                request.CandidateId <= 0 ||
                request.JobDescriptionId <= 0 ||
                request.CandidateApplicationId <= 0 ||
                request.CandidateOnboardingDocumentId <= 0)
            {
                return Fail(400, "Provide valid reference IDs.");
            }

            var remarks = request.Remarks?.Trim();

            if (string.IsNullOrWhiteSpace(remarks))
                return Fail(400, "Enter a reason for removing this document.");

            if (request.RowVersion == null || request.RowVersion.Length != 8)
                return Fail(400, "Send the document's current RowVersion.");

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var user = await _context.EmployeeMasters
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.EmployeeId == request.UserId)
                    .Select(x => new
                    {
                        x.EmployeeId,
                        x.IsAdmin,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Fail(404, "Employee not found for this company.");

                if (user.IsActive != true)
                    return Fail(403, "Employee is inactive.");

                var application = await _context.CandidateApplications
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateApplicationId ==
                            request.CandidateApplicationId &&
                        x.CandidateId == request.CandidateId &&
                        x.JobDescriptionId == request.JobDescriptionId)
                    .Select(x => new
                    {
                        x.CandidateApplicationId,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (application == null)
                    return Fail(404, "Matching candidate application not found.");

                var job = await _context.JobDescriptions
                    .AsNoTracking()
                    .Where(x =>
                        x.CompanyId == request.CompanyId &&
                        x.JobDescriptionId == request.JobDescriptionId)
                    .Select(x => new
                    {
                        TemplateId = (int?)x.TemplateId
                    })
                    .FirstOrDefaultAsync();

                if (job == null)
                    return Fail(404, "Job not found for this company.");

                if (user.IsAdmin != true)
                {
                    var teamIds = await (
                        from member in _context.OnboardingTeamMembers
                            .AsNoTracking()

                        join team in _context.OnboardingTeams
                            .AsNoTracking()
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

                        where member.CompanyId == request.CompanyId
                              && member.EmployeeId == request.UserId
                              && member.IsActive
                              && team.IsActive

                        select team.OnboardingTeamId
                    )
                    .Distinct()
                    .ToListAsync();

                    var mappings = await _context.OnboardingTeamTemplates
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

                    bool hasAccess = teamIds.Any(teamId =>
                        !mappings.Any(x =>
                            x.OnboardingTeamId == teamId)
                        ||
                        (
                            job.TemplateId.HasValue &&
                            mappings.Any(x =>
                                x.OnboardingTeamId == teamId &&
                                x.TemplateId == job.TemplateId.Value)
                        ));

                    if (!hasAccess)
                    {
                        return Fail(
                            403,
                            "You are not assigned to onboarding for this candidate's template.");
                    }
                }

                var onboarding = await _context.CandidateOnboardings
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateApplicationId ==
                            request.CandidateApplicationId);

                if (onboarding == null)
                    return Fail(404, "Onboarding record not found.");

                var document = await _context.CandidateOnboardingDocuments
                    .FirstOrDefaultAsync(x =>
                        x.CompanyId == request.CompanyId &&
                        x.CandidateOnboardingId ==
                            onboarding.CandidateOnboardingId &&
                        x.CandidateOnboardingDocumentId ==
                            request.CandidateOnboardingDocumentId);

                if (document == null)
                    return Fail(404, "Document request not found for this candidate.");

                // Safe repeated deletion: do not create duplicate history.
                if (!document.IsActive)
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "Document request is already removed.",
                        Data = new
                        {
                            document.CandidateOnboardingDocumentId,
                            document.IsActive,
                            document.RowVersion
                        }
                    });
                }

                if (application.IsActive != true)
                    return Fail(409, "Candidate application is inactive.");

                if (!onboarding.IsActive ||
                    !string.IsNullOrWhiteSpace(onboarding.FinalOutcome))
                {
                    return Fail(
                        409,
                        "Documents cannot be removed from an inactive or closed onboarding case.");
                }

                // Keep document changes within the existing document-stage rules.
                if (onboarding.Status != "ReadyForOnboarding" &&
                    onboarding.Status != "DocumentsPending")
                {
                    return Fail(
                        409,
                        "Documents cannot be removed after advancing beyond the document stage.");
                }

                if (document.Status == "Verified")
                {
                    return Fail(
                        409,
                        "A verified document cannot be removed through this API.");
                }

                if (!document.RowVersion.SequenceEqual(request.RowVersion))
                {
                    return Fail(
                        409,
                        "The document changed. Refresh the latest data and try again.");
                }

                string Snapshot() =>
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        document.CandidateOnboardingDocumentId,
                        document.CandidateOnboardingId,
                        document.OnboardingDocumentMasterId,
                        document.DocumentName,
                        document.IsMandatory,
                        document.AllocatedDays,
                        document.RequestedOn,
                        document.DueOn,
                        document.Instructions,
                        File = document.FileStorageKey,
                        document.Status,
                        document.CurrentSubmissionNumber,
                        document.ReceivedFromCandidate,
                        document.IsActive,
                        document.ModifiedBy,
                        document.ModifiedOn
                    });

                var before = Snapshot();
                var now = DateTime.UtcNow;

                // Preserve document status and file references.
                // IsActive controls whether it remains an applicable request.
                document.IsActive = false;
                document.ModifiedBy = request.UserId;
                document.ModifiedOn = now;

                onboarding.ModifiedBy = request.UserId;
                onboarding.ModifiedOn = now;

                var history = new CandidateOnboardingHistories
                {
                    CompanyId = request.CompanyId,
                    CandidateOnboardingId = onboarding.CandidateOnboardingId,
                    CandidateOnboardingDocumentId =
                        document.CandidateOnboardingDocumentId,

                    Action = "DocumentRemoved",
                    PreviousStatus = document.Status,
                    NewStatus = document.Status,

                    PreviousDetailsJson = before,
                    DetailsJson = Snapshot(),

                    Remarks = remarks,
                    IsRemarkAdded = true,

                    CreatedBy = request.UserId,
                    CreatedOn = now
                };

                _context.CandidateOnboardingHistories.Add(history);

                await _context.SaveChangesAsync();

                var remainingDocumentCount =
                    await _context.CandidateOnboardingDocuments
                        .CountAsync(x =>
                            x.CompanyId == request.CompanyId &&
                            x.CandidateOnboardingId ==
                                onboarding.CandidateOnboardingId &&
                            x.IsActive);

                await transaction.CommitAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Document request removed successfully.",

                    Data = new
                    {
                        request.CompanyId,
                        request.CandidateId,
                        request.JobDescriptionId,
                        request.CandidateApplicationId,

                        document.CandidateOnboardingId,
                        document.CandidateOnboardingDocumentId,
                        document.DocumentName,
                        document.Status,
                        document.IsActive,

                        File = document.FileStorageKey,

                        document.ModifiedBy,
                        document.ModifiedOn,
                        document.RowVersion,

                        history.CandidateOnboardingHistoryId,
                        RemainingDocumentCount = remainingDocumentCount
                    }
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail(
                    409,
                    "The record changed. Refresh the latest data and try again.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Failed to remove onboarding document {DocumentId} for CompanyId {CompanyId}, ApplicationId {ApplicationId}.",
                    request.CandidateOnboardingDocumentId,
                    request.CompanyId,
                    request.CandidateApplicationId);

                return Fail(500, "Unable to remove document request.");
            }
        }

        public class DeleteCandidateOnboardingDocumentRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }

            public int CandidateId { get; set; }
            public int JobDescriptionId { get; set; }

            public int CandidateApplicationId { get; set; }
            public int CandidateOnboardingDocumentId { get; set; }

            public string? Remarks { get; set; }

            public byte[]? RowVersion { get; set; }
        }



        public class GetCompanyOnboardingDocumentsRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }
        }

        public class GetCandidateOnboardingDocumentsRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }

            public int CandidateId { get; set; }
            public int JobDescriptionId { get; set; }

            // Optional unless multiple applications exist for this candidate/job.
            public int CandidateApplicationId { get; set; }
        }
        public class GetOnboardingTeamListRequest
        {
            public int CompanyId { get; set; }
            public int UserId { get; set; }
        }
        public class SaveOnboardingTeamRequest
        {
            public int OnboardingTeamId { get; set; }

            public int CompanyId { get; set; }
            public int UserId { get; set; }

            [Required]
            [MaxLength(150)]
            public string TeamName { get; set; } = string.Empty;

            public int? CompanySkillMasterId { get; set; }

            // Null, empty, or [0] means all company templates.
            public List<int>? TemplateIds { get; set; }

            public List<SaveOnboardingTeamMemberRequest>? Members { get; set; }

            public bool IsActive { get; set; } = true;
        }

        public class SaveOnboardingTeamMemberRequest
        {
            public int EmployeeId { get; set; }

            // Optional. 0 is treated as null.
            public int? RoleId { get; set; }

            public bool IsActive { get; set; } = true;
        }






       
    }
}
