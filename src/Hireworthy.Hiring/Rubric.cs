using Plenipo.Core.Entities;
using Plenipo.Core.Multitenancy;

namespace Hireworthy.Hiring;

/// <summary>Whether a rubric may yet be used to score anybody.</summary>
public enum RubricStatus
{
    /// <summary>Proposed by the assistant, awaiting a human. <b>Nobody may be scored against it.</b></summary>
    Proposed = 0,

    /// <summary>Approved by a recruiter. This is the yardstick screening uses.</summary>
    Approved = 1,

    /// <summary>Superseded by a later version. Retained because earlier scores pin it.</summary>
    Superseded = 2,
}

/// <summary>
/// The frozen yardstick every applicant to one requisition is measured against.
/// </summary>
/// <remarks>
/// <para>
/// <b>This entity is why the product works.</b> Scoring each CV in isolation lets the standard
/// drift down a pile of 180 — and then "why was #47 rejected and #48 advanced?" has no answer.
/// Extracting the criteria once, freezing them, and pinning every score to a specific
/// <see cref="Version"/> is what makes the scores comparable and the decision defensible.
/// </para>
/// <para>
/// Versioned rather than mutable: if a job description is edited mid-pile, earlier scores were
/// measured against a different standard, and silently overwriting the rubric would erase that
/// fact. A new version supersedes; the old one stays for the scores that reference it.
/// </para>
/// </remarks>
public sealed class Rubric : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid RequisitionId { get; set; }

    public Requisition? Requisition { get; set; }

    /// <summary>1-based, per requisition. Scores pin this, so it may never be reused.</summary>
    public int Version { get; set; } = 1;

    public RubricStatus Status { get; set; } = RubricStatus.Proposed;

    /// <summary>Why the assistant proposed these criteria. Recorded with the approval.</summary>
    public string? Rationale { get; set; }

    /// <summary>Display name of whoever approved it, once approved. PII.</summary>
    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public ICollection<RubricCriterion> Criteria { get; set; } = [];
}

/// <summary>
/// One thing a candidate is measured on, traceable to the job description it came from.
/// </summary>
public sealed class RubricCriterion : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid RubricId { get; set; }

    public Rubric? Rubric { get; set; }

    /// <summary>Short label, e.g. "Production Python experience".</summary>
    public required string Name { get; set; }

    /// <summary>
    /// What "meets" looks like, concretely — e.g. "3+ years shipping Python services in
    /// production". Vague requirements are what make two recruiters disagree, so this is the field
    /// that carries the structure the interview literature says raises inter-rater reliability.
    /// </summary>
    public required string Requirement { get; set; }

    /// <summary>Relative weight, 1–5. Not a percentage — the total need not be 100.</summary>
    public int Weight { get; set; } = 3;

    public int Ordinal { get; set; }
}
