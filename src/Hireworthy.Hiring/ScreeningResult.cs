using Plenipo.Core.Entities;
using Plenipo.Core.Multitenancy;

namespace Hireworthy.Hiring;

/// <summary>Whether a screening result has been acted on.</summary>
public enum ScreeningStatus
{
    /// <summary>The assistant's proposal. Changes nothing until a human advances or rejects.</summary>
    Proposed = 0,

    /// <summary>Superseded by a later screening of the same applicant.</summary>
    Superseded = 1,
}

/// <summary>
/// One applicant measured against one version of one rubric.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rubric version is pinned, and that is the whole point.</b> A score is only meaningful
/// alongside the yardstick it was measured with — if the job description is edited mid-pile and the
/// rubric supersedes, every earlier score still says which criteria it was measured against. Without
/// that, "why was #47 rejected and #48 advanced?" has no answer, and the pile is not comparable.
/// </para>
/// <para>
/// <see cref="ScreeningStatus.Proposed"/> is the only status a screening ever gets from the
/// assistant. Nothing here moves a candidate's stage — only <c>advance_candidates</c> and
/// <c>reject_candidate</c> do, and both are approval-gated (ADR-0004).
/// </para>
/// </remarks>
public sealed class ScreeningResult : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ApplicantId { get; set; }

    public Applicant? Applicant { get; set; }

    public Guid RubricId { get; set; }

    public Rubric? Rubric { get; set; }

    /// <summary>Denormalised from the rubric so the pinning survives even if the rubric row moves.</summary>
    public int RubricVersion { get; set; }

    public ScreeningStatus Status { get; set; } = ScreeningStatus.Proposed;

    public DateTimeOffset ScreenedAt { get; set; }

    /// <summary>Weighted sum of the criterion scores.</summary>
    public int TotalScore { get; set; }

    /// <summary>The weighted maximum, so a total is comparable across differently-sized rubrics.</summary>
    public int MaxScore { get; set; }

    /// <summary>How many criteria the CV simply did not evidence.</summary>
    public int UnresolvedCount { get; set; }

    public ICollection<CriterionScore> Scores { get; set; } = [];

    /// <summary>
    /// The weighted total and maximum for a set of criterion scores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure and deterministic: the same criterion scores always produce the same total. That is what
    /// makes "re-running the same input produces the same score" checkable at all — the model's
    /// judgement varies, but everything downstream of it must not.
    /// </para>
    /// <para>
    /// <b>An unresolved criterion scores 0 but still counts toward the maximum.</b> That is a
    /// deliberate and arguable choice: it means "the CV does not evidence this", and the burden of
    /// evidence is on the application. Excluding it from the maximum instead would quietly reward a
    /// vague CV by shrinking the yardstick. The recruiter sees <see cref="UnresolvedCount"/> next to
    /// the total precisely so this is visible rather than baked in silently.
    /// </para>
    /// <para>
    /// <b>The maximum belongs to the rubric, not to the supplied score set</b>
    /// (<c>weightByCriterionId</c> is the rubric's full weight table). An <i>omitted</i> criterion
    /// reached the same outcome ADR-0004 was written to prevent, more cheaply than marking it
    /// unresolved: scoring one of five criteria used to read 25/25 rather than 25/90, so a weaker
    /// applicant outranked a stronger one scored honestly. A criterion the rubric does not know is
    /// the one thing still counted from the supplied set, at the default weight of 1, so it cannot
    /// be free either — <c>screen_applicant</c> refuses both shapes before they reach here, and this
    /// keeps the arithmetic honest for any other caller.
    /// </para>
    /// </remarks>
    public static (int Total, int Max, int Unresolved) ComputeTotal(
        IEnumerable<CriterionScore> scores,
        IReadOnlyDictionary<Guid, int> weightByCriterionId)
    {
        var total = 0;
        var unresolved = 0;

        var max = weightByCriterionId.Values.Sum(weight => CriterionScore.MaxPoints * weight);

        foreach (var score in scores)
        {
            var onTheRubric = weightByCriterionId.TryGetValue(score.RubricCriterionId, out var w);
            var weight = onTheRubric ? w : 1;

            if (!onTheRubric)
            {
                max += CriterionScore.MaxPoints * weight;
            }

            if (score.Unresolved)
            {
                unresolved++;
                continue;
            }

            total += score.Points * weight;
        }

        return (total, max, unresolved);
    }
}

/// <summary>
/// One criterion's score for one applicant, and the words on the CV that justify it.
/// </summary>
/// <remarks>
/// <see cref="CitationText"/> plus the offsets are the evidence. They are verified against
/// <see cref="CvDocument.ExtractedText"/> before this row is ever written, so a citation that is
/// stored is a citation that was real at the moment of storage — see <see cref="CitationGrounding"/>
/// and ADR-0005.
/// </remarks>
public sealed class CriterionScore : EntityBase, ITenantOwned
{
    /// <summary>Scores run 0–5, matching the weight scale so the arithmetic stays legible.</summary>
    public const int MaxPoints = 5;

    public Guid TenantId { get; set; }

    public Guid ScreeningResultId { get; set; }

    public ScreeningResult? ScreeningResult { get; set; }

    public Guid RubricCriterionId { get; set; }

    /// <summary>Denormalised so a scorecard reads without joining a superseded rubric.</summary>
    public required string CriterionName { get; set; }

    /// <summary>0–5. Zero when <see cref="Unresolved"/>.</summary>
    public int Points { get; set; }

    /// <summary>True when the CV does not evidence this criterion either way. Flagged, never guessed.</summary>
    public bool Unresolved { get; set; }

    /// <summary>The span of the CV that evidences the score, verbatim. Null only when unresolved.</summary>
    public string? CitationText { get; set; }

    /// <summary>Inclusive start offset into <see cref="CvDocument.ExtractedText"/>.</summary>
    public int CitationStart { get; set; }

    /// <summary>Exclusive end offset into <see cref="CvDocument.ExtractedText"/>.</summary>
    public int CitationEnd { get; set; }

    /// <summary>Why the citation supports this score. Reasoning, not evidence — the citation is the evidence.</summary>
    public string? Note { get; set; }
}
