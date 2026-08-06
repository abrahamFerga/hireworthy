using Plenipo.Core.Entities;
using Plenipo.Core.Multitenancy;

namespace Hireworthy.Hiring;

/// <summary>What was decided about a candidate.</summary>
public enum DecisionKind
{
    Advance = 0,
    Reject = 1,
}

/// <summary>
/// The record that a named human moved a named candidate, and the evidence they moved them on.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the artifact the whole product exists to produce.</b> When a rejected applicant asks
/// why, when a bias auditor arrives, or when an employment tribunal asks how the decision was made,
/// this row plus the <see cref="Evidence"/> it points at is the answer: which rubric version, which
/// cited spans of which CV, which person approved it, and when.
/// </para>
/// <para>
/// It is written only after the platform's approval gate has been cleared, so its existence is
/// itself evidence that a human decided. Nothing in the module writes one otherwise.
/// </para>
/// <para>
/// It survives content deletion as a tombstone (ADR-0008): if a candidate exercises their Illinois
/// AIVIA right to have interview material deleted, the CV text and transcript go and this row
/// stays, so the accountability chain outlives the personal data it was drawn from.
/// </para>
/// </remarks>
public sealed class Decision : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ApplicantId { get; set; }

    public Applicant? Applicant { get; set; }

    public DecisionKind Kind { get; set; }

    public ApplicantStage FromStage { get; set; }

    public ApplicantStage ToStage { get; set; }

    /// <summary>Why, in the decider's words. Recorded with the approval.</summary>
    public required string Reason { get; set; }

    /// <summary>
    /// The screening this decision rests on. <b>Required in practice</b> — the tool refuses to
    /// decide about a candidate nobody screened — but nullable in the schema so a future decision
    /// made on interview evidence instead can point somewhere else without a migration.
    /// </summary>
    public Guid? ScreeningResultId { get; set; }

    public ScreeningResult? Evidence { get; set; }

    /// <summary>Display name of the human who approved it. PII.</summary>
    public string? ApprovedBy { get; set; }

    public DateTimeOffset DecidedAt { get; set; }
}
