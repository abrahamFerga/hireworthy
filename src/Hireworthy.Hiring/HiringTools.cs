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

/// <summary>One criterion's score for one applicant, with the CV span that evidences it.</summary>
/// <param name="CriterionName">The rubric criterion being scored, exactly as the rubric names it.</param>
/// <param name="Points">0–5. Use 0 with <paramref name="Unresolved"/> when the CV does not say.</param>
/// <param name="CitationText">The span of the CV that evidences the score, quoted VERBATIM.</param>
/// <param name="CitationStart">Inclusive character offset of that span in the CV text.</param>
/// <param name="CitationEnd">Exclusive character offset of that span in the CV text.</param>
/// <param name="Unresolved">True when the CV does not evidence this criterion. Flag it; never guess.</param>
/// <param name="Note">Why the quoted span supports the score.</param>
public sealed record ScoredCriterion(
    string CriterionName,
    int Points,
    string? CitationText,
    int CitationStart,
    int CitationEnd,
    bool Unresolved,
    string? Note);

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

    /// <summary>
    /// Scores one applicant against the requisition's approved rubric. <b>Not approval-gated</b>
    /// (ADR-0004) — it writes a proposal and moves nobody's stage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every citation is verified against the CV before anything is written.</b> If one does not
    /// occur verbatim at the offsets given, nothing persists and the tool says which criterion and
    /// what the CV actually says there. That is ADR-0005, and enforcing it here rather than in a
    /// later check is the point: a fabricated quote that reached a recruiter's screen has already
    /// done its damage.
    /// </para>
    /// <para>
    /// It refuses to score against a rubric that is not <see cref="RubricStatus.Approved"/>. Scoring
    /// 180 people against criteria a human never accepted is precisely what the approval gate on
    /// <c>propose_rubric</c> exists to prevent, and it would be trivially undone by letting this
    /// tool read a proposed rubric.
    /// </para>
    /// </remarks>
    [Description("Score one applicant against their requisition's approved rubric. Every criterion must quote the span of the CV that evidences it, verbatim, or be marked unresolved.")]
    public async Task<string> ScreenApplicantAsync(
        [Description("The applicant's reference, e.g. 'APP-1001'.")] string reference,
        [Description("One entry per rubric criterion. Quote the CV verbatim; do not paraphrase.")] ScoredCriterion[] scores,
        CancellationToken cancellationToken = default)
    {
        if (scores is null || scores.Length == 0)
        {
            return "No scores were supplied. Score every criterion on the approved rubric, or mark it unresolved.";
        }

        var applicant = await db.Applicants
            .Include(a => a.Cv)
            .Include(a => a.Requisition)
            .FirstOrDefaultAsync(a => a.Reference == reference, cancellationToken);

        if (applicant is null)
        {
            return $"No applicant found with reference \"{reference}\".";
        }

        if (applicant.Cv is null)
        {
            return $"{applicant.Reference} has no CV on file, so nothing can be cited. Upload one first.";
        }

        var rubric = await db.Rubrics
            .Include(r => r.Criteria)
            .Where(r => r.RequisitionId == applicant.RequisitionId && r.Status == RubricStatus.Approved)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (rubric is null)
        {
            return $"{applicant.Requisition?.Reference ?? "That requisition"} has no APPROVED rubric. "
                 + "Propose one with propose_rubric and have it approved before screening anybody — "
                 + "nobody may be measured against criteria a human has not accepted.";
        }

        var criteriaByName = rubric.Criteria.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var source = applicant.Cv.ExtractedText;
        var rows = new List<CriterionScore>();
        var tenantId = tenantContext.RequireTenantId();

        foreach (var scored in scores)
        {
            if (!criteriaByName.TryGetValue(scored.CriterionName, out var criterion))
            {
                return $"\"{scored.CriterionName}\" is not a criterion on approved rubric v{rubric.Version}. "
                     + $"Its criteria are: {string.Join(", ", rubric.Criteria.Select(c => c.Name))}.";
            }

            if (scored.Points is < 0 or > CriterionScore.MaxPoints)
            {
                return $"Criterion \"{scored.CriterionName}\" scored {scored.Points}; scores are 0–{CriterionScore.MaxPoints}.";
            }

            // The guarantee. An unresolved criterion is the ONLY way to score without evidence, and
            // it costs the candidate the points — so there is no incentive to use it to dodge the check.
            if (!scored.Unresolved)
            {
                var verdict = CitationGrounding.Verify(
                    source, scored.CitationText, scored.CitationStart, scored.CitationEnd);

                if (verdict is not GroundingVerdict.Ok)
                {
                    return CitationGrounding.Explain(
                        verdict, scored.CriterionName, source, scored.CitationStart, scored.CitationEnd);
                }
            }

            rows.Add(new CriterionScore
            {
                TenantId = tenantId,
                RubricCriterionId = criterion.Id,
                CriterionName = criterion.Name,
                Points = scored.Unresolved ? 0 : scored.Points,
                Unresolved = scored.Unresolved,
                CitationText = scored.Unresolved ? null : scored.CitationText,
                CitationStart = scored.Unresolved ? 0 : scored.CitationStart,
                CitationEnd = scored.Unresolved ? 0 : scored.CitationEnd,
                Note = scored.Note,
            });
        }

        var weights = rubric.Criteria.ToDictionary(c => c.Id, c => c.Weight);
        var (total, max, unresolved) = ScreeningResult.ComputeTotal(rows, weights);

        // Re-screening supersedes rather than accumulating: an applicant has one current score
        // against one rubric version, and two live results would make "their score" ambiguous at
        // exactly the moment somebody is deciding their application.
        var previous = await db.ScreeningResults
            .Where(r => r.ApplicantId == applicant.Id && r.Status == ScreeningStatus.Proposed)
            .ToListAsync(cancellationToken);

        foreach (var stale in previous)
        {
            stale.Status = ScreeningStatus.Superseded;
        }

        db.ScreeningResults.Add(new ScreeningResult
        {
            TenantId = tenantId,
            ApplicantId = applicant.Id,
            RubricId = rubric.Id,
            RubricVersion = rubric.Version,
            Status = ScreeningStatus.Proposed,
            ScreenedAt = DateTimeOffset.UtcNow,
            TotalScore = total,
            MaxScore = max,
            UnresolvedCount = unresolved,
            Scores = rows,
        });

        await db.SaveChangesAsync(cancellationToken);

        var unresolvedText = unresolved == 0
            ? "every criterion evidenced"
            : $"{unresolved} criterion(s) the CV does not evidence";

        return $"Scored {applicant.Reference} ({applicant.FullName}) against rubric v{rubric.Version}: "
             + $"{total}/{max}, {unresolvedText}. Every score cites the CV verbatim. "
             + "This is a proposal — no candidate advances or is rejected without a human approving it.";
    }

    /// <summary>
    /// Advances named candidates to the next stage. <b>Approval-gated</b>, and the most
    /// consequential write in the product.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Everything here THROWS rather than returning guidance</b>, and that is the opposite of
    /// <c>screen_applicant</c> and <c>parse_cv</c>. Those are ungated, so the model is still in a
    /// retry loop and a described problem is useful. This one runs only after a human has already
    /// approved it — there is no retry loop left, and a returned string would resolve the approval
    /// as <c>Executed</c> with <c>error: null</c>. Telling a hiring manager that twenty people were
    /// advanced when they were not is precisely the failure this product exists to prevent.
    /// </para>
    /// <para>
    /// <b>It refuses to advance a candidate nobody screened.</b> A decision with no evidence is the
    /// thing the whole product argues against, so <see cref="Decision.ScreeningResultId"/> is
    /// populated from the live screening or the call fails. It is also <b>atomic</b>: every
    /// candidate is validated before any is written, because a partial advance would tell some
    /// people they progressed and leave the rest in a state nobody chose.
    /// </para>
    /// </remarks>
    [Description("Advance named candidates to the next stage. Requires human approval, and every candidate must already have been screened.")]
    public async Task<string> AdvanceCandidatesAsync(
        [Description("The applicant references to advance, e.g. ['APP-1001','APP-1002'].")] string[] references,
        [Description("Why these candidates should advance. Recorded with the decision, permanently.")] string reason,
        CancellationToken cancellationToken = default)
    {
        if (references is null || references.Length == 0)
        {
            throw new ArgumentException("Name at least one candidate to advance.", nameof(references));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A reason is required. It is recorded with the decision and is what a rejected "
              + "candidate, an auditor or a tribunal later reads.", nameof(reason));
        }

        var wanted = references.Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var applicants = await db.Applicants
            .Where(a => wanted.Contains(a.Reference))
            .ToListAsync(cancellationToken);

        // Validate EVERY candidate before writing ANY of them.
        var missing = wanted.Where(w => !applicants.Any(a => a.Reference == w)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"No applicant found with reference(s): {string.Join(", ", missing)}. Nothing was advanced.");
        }

        var screenings = await db.ScreeningResults
            .Where(r => applicants.Select(a => a.Id).Contains(r.ApplicantId)
                     && r.Status == ScreeningStatus.Proposed)
            .ToListAsync(cancellationToken);

        var unscreened = applicants
            .Where(a => screenings.All(s => s.ApplicantId != a.Id))
            .Select(a => a.Reference)
            .ToList();

        if (unscreened.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot advance {string.Join(", ", unscreened)} — nobody has screened them, so there "
              + "is no evidence behind the decision. Screen them first with screen_applicant. "
              + "Nothing was advanced.");
        }

        var terminal = applicants
            .Where(a => NextStage(a.Stage) is null)
            .Select(a => $"{a.Reference} ({a.Stage})")
            .ToList();

        if (terminal.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot advance {string.Join(", ", terminal)} — already at a final stage. Nothing was advanced.");
        }

        var tenantId = tenantContext.RequireTenantId();
        var now = DateTimeOffset.UtcNow;
        var moved = new List<string>();

        foreach (var applicant in applicants)
        {
            var from = applicant.Stage;
            var to = NextStage(from)!.Value;
            var evidence = screenings.First(s => s.ApplicantId == applicant.Id);

            applicant.Stage = to;

            db.Decisions.Add(new Decision
            {
                TenantId = tenantId,
                ApplicantId = applicant.Id,
                Kind = DecisionKind.Advance,
                FromStage = from,
                ToStage = to,
                Reason = reason,
                ScreeningResultId = evidence.Id,
                DecidedAt = now,
            });

            moved.Add($"{applicant.Reference} ({from} → {to})");
        }

        await db.SaveChangesAsync(cancellationToken);

        return $"Advanced {applicants.Count} candidate(s): {string.Join("; ", moved)}. "
             + $"Reason: {reason}. Each decision records the screening it rests on.";
    }

    /// <summary>
    /// Rejects one candidate. <b>Approval-gated</b>, and the decision this product is ultimately
    /// judged on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Singular where <c>advance_candidates</c> is plural, and that asymmetry is deliberate.</b>
    /// Advancing a shortlist is one judgement about a group. A rejection is a judgement about a
    /// person, it is the decision they may challenge, and it is the one a bias auditor samples. One
    /// approval per rejection means a human looked at that individual — batching would make the gate
    /// a formality at exactly the point it matters most.
    /// </para>
    /// <para>
    /// It THROWS on every failure, for the same reason <c>advance_candidates</c> does: it runs only
    /// after a human approved, so there is no retry loop, and a returned string would resolve the
    /// approval as <c>Executed</c> with <c>error: null</c>. And it refuses to reject a candidate
    /// nobody screened — a rejection with no evidence behind it is the single worst artifact this
    /// system could produce.
    /// </para>
    /// </remarks>
    [Description("Reject one candidate, with a recorded reason. Requires human approval, and the candidate must already have been screened.")]
    public async Task<string> RejectCandidateAsync(
        [Description("The applicant's reference, e.g. 'APP-1001'.")] string reference,
        [Description("Why this candidate is being rejected, in terms of the rubric. Recorded permanently and readable by an auditor.")] string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Name the candidate to reject.", nameof(reference));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A reason is required. This is the sentence a rejected candidate, a bias auditor or "
              + "a tribunal reads, and it is the only account of why this person was turned down.",
                nameof(reason));
        }

        var applicant = await db.Applicants
            .FirstOrDefaultAsync(a => a.Reference == reference.Trim(), cancellationToken);

        if (applicant is null)
        {
            throw new InvalidOperationException(
                $"No applicant found with reference \"{reference}\". Nobody was rejected.");
        }

        if (applicant.Stage == ApplicantStage.Rejected)
        {
            throw new InvalidOperationException(
                $"{applicant.Reference} has already been rejected. Nobody was rejected again.");
        }

        if (applicant.Stage == ApplicantStage.Hired)
        {
            throw new InvalidOperationException(
                $"{applicant.Reference} has been hired and cannot be rejected. Nobody was rejected.");
        }

        var evidence = await db.ScreeningResults
            .FirstOrDefaultAsync(
                r => r.ApplicantId == applicant.Id && r.Status == ScreeningStatus.Proposed,
                cancellationToken);

        if (evidence is null)
        {
            throw new InvalidOperationException(
                $"Cannot reject {applicant.Reference} — nobody has screened them, so there is no "
              + "evidence behind the decision. Screen them first with screen_applicant. "
              + "Nobody was rejected.");
        }

        var from = applicant.Stage;
        applicant.Stage = ApplicantStage.Rejected;

        db.Decisions.Add(new Decision
        {
            TenantId = tenantContext.RequireTenantId(),
            ApplicantId = applicant.Id,
            Kind = DecisionKind.Reject,
            FromStage = from,
            ToStage = ApplicantStage.Rejected,
            Reason = reason,
            ScreeningResultId = evidence.Id,
            DecidedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);

        return $"Rejected {applicant.Reference} ({applicant.FullName}), previously at {from}. "
             + $"Reason: {reason}. The decision records the screening it rests on.";
    }

    /// <summary>The next stage, or null when there is nowhere further to go.</summary>
    private static ApplicantStage? NextStage(ApplicantStage stage) => stage switch
    {
        ApplicantStage.Applied => ApplicantStage.Screening,
        ApplicantStage.Screening => ApplicantStage.Interview,
        ApplicantStage.Interview => ApplicantStage.Offer,
        ApplicantStage.Offer => ApplicantStage.Hired,
        // Hired and Rejected are terminal. Rejected is deliberately NOT reversible by advancing:
        // un-rejecting somebody is a new decision a human makes explicitly, not a side effect.
        _ => null,
    };

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
