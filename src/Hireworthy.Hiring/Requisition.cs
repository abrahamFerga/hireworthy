using Plenipo.Core.Entities;
using Plenipo.Core.Multitenancy;

namespace Hireworthy.Hiring;

/// <summary>Where a requisition is in its own lifecycle — not where a candidate is.</summary>
public enum RequisitionStatus
{
    Draft = 0,
    Open = 1,
    Closed = 2,
}

/// <summary>
/// A role being hired for — the primary object of the module, and the container everything else
/// hangs from: applicants apply to it, a rubric measures against it, decisions are made within it.
/// </summary>
/// <remarks>
/// <para>
/// The requisition is also the <b>retrieval boundary</b>. Each one gets its own RAG collection so a
/// candidate's material for REQ-142 cannot surface while screening REQ-150 — that is a legal
/// boundary, not a tidiness preference.
/// </para>
/// <para>
/// <see cref="ITenantOwned"/> is not decoration. The module's <c>DbContext</c> declares a
/// <c>HasQueryFilter</c> per entity so no query can cross a hiring-organisation boundary; the
/// platform context does this by reflection, a module context does not.
/// </para>
/// </remarks>
public sealed class Requisition : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }

    /// <summary>Short human reference, e.g. "REQ-142".</summary>
    public required string Reference { get; set; }

    public required string Title { get; set; }

    /// <summary>
    /// The job description, verbatim. This is the <b>source text a rubric is derived from</b>, and
    /// the rubric's criteria must be traceable to it — so it is stored as written, never summarised.
    /// </summary>
    public string? JobDescription { get; set; }

    public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;

    public string? Location { get; set; }

    /// <summary>Display name of the hiring manager accountable for the req. PII.</summary>
    public string? HiringManager { get; set; }

    public ICollection<Rubric> Rubrics { get; set; } = [];
}
