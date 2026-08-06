using System.ComponentModel;
using Hireworthy.Hiring.Persistence;
using Microsoft.EntityFrameworkCore;
using Plenipo.Core.Multitenancy;

namespace Hireworthy.Hiring;

/// <summary>One criterion the assistant proposes for a rubric.</summary>
/// <param name="Name">Short label, e.g. "Production Python experience".</param>
/// <param name="Requirement">What "meets" looks like, concretely and checkably against a CV.</param>
/// <param name="Weight">Relative importance, 1–5.</param>
public sealed record ProposedCriterion(string Name, string Requirement, int Weight);

/// <summary>One role the assistant read off a CV.</summary>
/// <param name="Employer">The organisation, as written on the CV.</param>
/// <param name="Title">The job title, as written on the CV.</param>
/// <param name="StartedOn">First month, as <c>yyyy-MM</c>. CVs do not state days.</param>
/// <param name="EndedOn">Last month as <c>yyyy-MM</c>, or null/empty if this is the current role.</param>
public sealed record ExtractedRole(string Employer, string Title, string StartedOn, string? EndedOn);

/// <summary>
/// The hiring module's agent tools for epic 1 — requisitions and the approved rubric.
/// </summary>
/// <remarks>
/// <c>propose_rubric</c> writes, so it is approval-gated in <b>both</b> the manifest descriptor and
/// the <c>ModuleTool</c>. Its return string is deliberately worded as a proposal, never as an
/// approved rubric: nobody may be scored against criteria a human has not accepted, and the
/// platform parks the call before this method's effect is committed.
/// </remarks>
public sealed class HiringTools(HiringDbContext db, ITenantContext tenantContext)
{
    [Description("List the organisation's requisitions with their status and whether a rubric has been approved.")]
    public async Task<string> ListRequisitionsAsync(CancellationToken cancellationToken = default)
    {
        var requisitions = await db.Requisitions
            .OrderBy(r => r.Reference)
            .Select(r => new
            {
                r.Reference,
                r.Title,
                r.Status,
                ApprovedRubrics = r.Rubrics.Count(x => x.Status == RubricStatus.Approved),
                PendingRubrics = r.Rubrics.Count(x => x.Status == RubricStatus.Proposed),
            })
            .Take(200)
            .ToListAsync(cancellationToken);

        if (requisitions.Count == 0)
        {
            return "There are no requisitions yet.";
        }

        var lines = requisitions.Select(r =>
        {
            var rubric = r.ApprovedRubrics > 0 ? "rubric approved"
                : r.PendingRubrics > 0 ? "rubric awaiting approval"
                : "no rubric yet";
            return $"{r.Reference} — {r.Title} [{r.Status}, {rubric}]";
        });

        return $"{requisitions.Count} requisition(s): {string.Join("; ", lines)}.";
    }

    [Description("Get one requisition in detail by its reference, e.g. 'REQ-142', including its job description and approved rubric.")]
    public async Task<string> GetRequisitionAsync(
        [Description("The requisition's reference, e.g. 'REQ-142'.")] string reference,
        CancellationToken cancellationToken = default)
    {
        var requisition = await db.Requisitions
            .Include(r => r.Rubrics)
            .ThenInclude(x => x.Criteria)
            .FirstOrDefaultAsync(r => r.Reference == reference, cancellationToken);

        if (requisition is null)
        {
            return $"No requisition found with reference \"{reference}\".";
        }

        var manager = string.IsNullOrWhiteSpace(requisition.HiringManager)
            ? "unassigned"
            : requisition.HiringManager;

        var rubric = requisition.Rubrics
            .Where(r => r.Status == RubricStatus.Approved)
            .OrderByDescending(r => r.Version)
            .FirstOrDefault();

        var rubricText = rubric is null
            ? "No approved rubric yet — propose one before screening anybody."
            : $"Approved rubric v{rubric.Version}: "
              + string.Join("; ", rubric.Criteria
                  .OrderBy(c => c.Ordinal)
                  .Select(c => $"{c.Name} (weight {c.Weight}) — {c.Requirement}"));

        return $"{requisition.Reference} — {requisition.Title}. Status: {requisition.Status}. "
             + $"Hiring manager: {manager}. Location: {requisition.Location ?? "unspecified"}. "
             + $"{rubricText} "
             + $"Job description: {requisition.JobDescription ?? "none recorded."}";
    }

    /// <summary>
    /// Proposes the evaluation criteria for a requisition. Approval-gated: the platform parks this
    /// call and a human approves before the write below is reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This method THROWS where the read tools above return guidance, and the difference is
    /// deliberate.</b> A returned string is, to the platform, a tool that ran to completion — it
    /// resolves the approval as <c>Executed</c> and reports <c>error: null</c>. That is correct
    /// behaviour on the platform's part and it cannot know a string means "I did nothing".
    /// </para>
    /// <para>
    /// For an ungated read, describing the problem is right: the model can retry with better
    /// arguments. For a gated write there is no retry loop — a human has already approved by the
    /// time this executes — so a write that did not happen must never resolve as one that did.
    /// Telling a recruiter that the yardstick 180 people will be measured against is in place when
    /// it is not is exactly the failure this product exists to prevent.
    /// </para>
    /// </remarks>
    [Description("Propose the evaluation criteria for a requisition, derived from its job description. The rubric is not usable for screening until a human approves it.")]
    public async Task<string> ProposeRubricAsync(
        [Description("The requisition's reference, e.g. 'REQ-142'.")] string reference,
        [Description("The criteria to measure every applicant against. Each must be checkable against a CV.")] ProposedCriterion[] criteria,
        [Description("Why these criteria follow from the job description. Recorded with the approval.")] string rationale,
        CancellationToken cancellationToken = default)
    {
        if (criteria is null || criteria.Length == 0)
        {
            throw new ArgumentException(
                "A rubric needs at least one criterion.", nameof(criteria));
        }

        var invalid = criteria.FirstOrDefault(c => c.Weight is < 1 or > 5);
        if (invalid is not null)
        {
            throw new ArgumentException(
                $"Criterion \"{invalid.Name}\" has weight {invalid.Weight}; weights are 1–5.",
                nameof(criteria));
        }

        var requisition = await db.Requisitions
            .Include(r => r.Rubrics)
            .FirstOrDefaultAsync(r => r.Reference == reference, cancellationToken);

        if (requisition is null)
        {
            throw new InvalidOperationException($"No requisition found with reference \"{reference}\".");
        }

        var tenantId = tenantContext.RequireTenantId();

        // A new version rather than a mutation: earlier scores pin the version they were measured
        // against, so overwriting would silently re-point history at different criteria.
        var nextVersion = requisition.Rubrics.Count == 0
            ? 1
            : requisition.Rubrics.Max(r => r.Version) + 1;

        var rubric = new Rubric
        {
            TenantId = tenantId,
            RequisitionId = requisition.Id,
            Version = nextVersion,
            Status = RubricStatus.Proposed,
            Rationale = rationale,
            Criteria = [.. criteria.Select((c, i) => new RubricCriterion
            {
                TenantId = tenantId,
                Name = c.Name,
                Requirement = c.Requirement,
                Weight = c.Weight,
                Ordinal = i,
            })],
        };

        db.Rubrics.Add(rubric);
        await db.SaveChangesAsync(cancellationToken);

        return $"Proposed rubric v{nextVersion} for {requisition.Reference} with {criteria.Length} "
             + $"criteria: {string.Join("; ", criteria.Select(c => $"{c.Name} (weight {c.Weight})"))}. "
             + $"Rationale: {rationale}";
    }

    [Description("Get one applicant by reference, e.g. 'APP-1001', including their CV text and anything already extracted from it.")]
    public async Task<string> GetApplicantAsync(
        [Description("The applicant's reference, e.g. 'APP-1001'.")] string reference,
        CancellationToken cancellationToken = default)
    {
        var applicant = await db.Applicants
            .Include(a => a.Requisition)
            .Include(a => a.Cv)
            .Include(a => a.Profile)
            .ThenInclude(p => p!.Employment)
            .FirstOrDefaultAsync(a => a.Reference == reference, cancellationToken);

        if (applicant is null)
        {
            return $"No applicant found with reference \"{reference}\".";
        }

        var profile = applicant.Profile is null
            ? "Nothing extracted from this CV yet — use parse_cv."
            : $"Extracted: "
              + $"{string.Join("; ", applicant.Profile.Employment.OrderBy(e => e.Ordinal).Select(FormatRole))}. "
              + $"Skills: {Join(applicant.Profile.Skills)}. "
              + $"Education: {Join(applicant.Profile.Education)}. "
              + $"Employment gaps: {Join(applicant.Profile.EmploymentGaps)}. "
              + $"Unresolved: {applicant.Profile.Unresolved ?? "none"}.";

        return $"{applicant.Reference} — {applicant.FullName}, applying to "
             + $"{applicant.Requisition?.Reference ?? "an unknown requisition"}. "
             + $"Stage: {applicant.Stage}. {profile} "
             + $"CV text: {applicant.Cv?.ExtractedText ?? "no CV on file."}";
    }

    /// <summary>
    /// Records what the assistant read off a CV. <b>Not approval-gated</b>, per ADR-0004.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This writes derived data that is recomputable from <c>CvDocument.ExtractedText</c> at any
    /// time, and it changes nothing about a candidate's outcome — no stage moves, no decision is
    /// recorded. Only <c>advance_candidates</c> and <c>reject_candidate</c> do that, and both are
    /// gated. Gating extraction as well would mean a human approving 180 extraction records before
    /// reading any of them, which makes the gate ceremony, and a gate people click through is worse
    /// than no gate because it launders the decision.
    /// </para>
    /// <para>
    /// It <b>returns guidance rather than throwing</b> on bad input, which is the opposite of
    /// <see cref="ProposeRubricAsync"/> and deliberately so: this call is ungated, so the model is
    /// still in a retry loop and can correct its own arguments. A gated write has no retry loop —
    /// a human has already approved by the time it executes — so there a failure must throw.
    /// </para>
    /// <para>
    /// The employment gaps are <b>computed</b> from the parsed dates, never taken from the model.
    /// "Was there a gap between these two jobs?" is arithmetic, and a candidate gets asked about
    /// gaps in an interview — so it must be checkable rather than plausible.
    /// </para>
    /// </remarks>
    [Description("Record the employment history, skills and education read from an applicant's CV. Dates are yyyy-MM; leave the end date empty for a current role.")]
    public async Task<string> ParseCvAsync(
        [Description("The applicant's reference, e.g. 'APP-1001'.")] string reference,
        [Description("Every role on the CV, in the order they appear.")] ExtractedRole[] employment,
        [Description("Skills as written on the CV. Do not normalise them to a taxonomy.")] string[] skills,
        [Description("Education lines as written, e.g. 'BSc Computer Science, Manchester, 2018'.")] string[] education,
        [Description("Anything you could not resolve from the CV. Flag it rather than guessing.")] string? unresolved = null,
        CancellationToken cancellationToken = default)
    {
        var applicant = await db.Applicants
            .Include(a => a.Profile)
            .FirstOrDefaultAsync(a => a.Reference == reference, cancellationToken);

        if (applicant is null)
        {
            return $"No applicant found with reference \"{reference}\".";
        }

        var spans = new List<EmploymentSpan>();
        var tenantId = tenantContext.RequireTenantId();

        for (var i = 0; i < (employment?.Length ?? 0); i++)
        {
            var role = employment![i];

            if (!TryParseMonth(role.StartedOn, out var startedOn))
            {
                return $"Could not read the start date \"{role.StartedOn}\" for {role.Employer}. "
                     + "Use yyyy-MM, e.g. 2019-01.";
            }

            DateOnly? endedOn = null;
            if (!string.IsNullOrWhiteSpace(role.EndedOn))
            {
                if (!TryParseMonth(role.EndedOn, out var parsedEnd))
                {
                    return $"Could not read the end date \"{role.EndedOn}\" for {role.Employer}. "
                         + "Use yyyy-MM, or leave it empty if this is the current role.";
                }

                if (parsedEnd < startedOn)
                {
                    return $"The role at {role.Employer} ends ({role.EndedOn}) before it starts "
                         + $"({role.StartedOn}). Re-read those dates.";
                }

                endedOn = parsedEnd;
            }

            spans.Add(new EmploymentSpan
            {
                TenantId = tenantId,
                Employer = role.Employer,
                Title = role.Title,
                StartedOn = startedOn,
                EndedOn = endedOn,
                Ordinal = i,
            });
        }

        // Re-extraction replaces rather than accumulates: a profile is a statement about one CV,
        // and two overlapping extractions would double every role.
        if (applicant.Profile is not null)
        {
            db.ExtractedProfiles.Remove(applicant.Profile);
            await db.SaveChangesAsync(cancellationToken);
        }

        var gaps = ExtractedProfile.InferGaps(spans);

        var profile = new ExtractedProfile
        {
            TenantId = tenantId,
            ApplicantId = applicant.Id,
            ExtractedAt = DateTimeOffset.UtcNow,
            Skills = [.. skills ?? []],
            Education = [.. education ?? []],
            EmploymentGaps = gaps,
            Unresolved = unresolved,
            Employment = spans,
        };

        db.ExtractedProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);

        var gapText = gaps.Count == 0 ? "no employment gaps over 3 months" : $"gaps: {Join(gaps)}";

        return $"Extracted {spans.Count} role(s), {profile.Skills.Count} skill(s) and "
             + $"{profile.Education.Count} education entr(y/ies) for {applicant.Reference} "
             + $"({applicant.FullName}); {gapText}."
             + (unresolved is null ? "" : $" Unresolved: {unresolved}");
    }

    private static string FormatRole(EmploymentSpan e) =>
        $"{e.Title} at {e.Employer} ({e.StartedOn:yyyy-MM} to {(e.EndedOn is null ? "present" : e.EndedOn.Value.ToString("yyyy-MM"))})";

    private static string Join(IReadOnlyCollection<string> values) =>
        values.Count == 0 ? "none" : string.Join(", ", values);

    /// <summary>Parses <c>yyyy-MM</c>. Day is forced to 1 — a CV never states one.</summary>
    private static bool TryParseMonth(string? value, out DateOnly month)
    {
        month = default;

        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Trim().Split('-');
        if (parts.Length < 2) return false;

        if (!int.TryParse(parts[0], out var year) || year is < 1900 or > 2200) return false;
        if (!int.TryParse(parts[1], out var m) || m is < 1 or > 12) return false;

        month = new DateOnly(year, m, 1);
        return true;
    }
}
