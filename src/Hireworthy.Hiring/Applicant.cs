using Plenipo.Core.Entities;
using Plenipo.Core.Multitenancy;

namespace Hireworthy.Hiring;

/// <summary>Where a candidate is in the pipeline for one requisition.</summary>
public enum ApplicantStage
{
    Applied = 0,
    Screening = 1,
    Interview = 2,
    Offer = 3,
    Hired = 4,
    Rejected = 5,
}

/// <summary>
/// One person's application to one requisition.
/// </summary>
/// <remarks>
/// <para>
/// An applicant belongs to exactly one requisition on purpose. The same person applying to two
/// roles is two applicants, because the rubric, the evidence and the decision are per requisition —
/// carrying a score across would measure someone against a yardstick nobody approved for that role.
/// </para>
/// <para>
/// Almost every field here is <b>personal data</b>, and this is the entity that makes the tenant
/// boundary a legal one rather than a tidiness preference: one employer seeing another employer's
/// applicants is a data-protection incident.
/// </para>
/// </remarks>
public sealed class Applicant : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid RequisitionId { get; set; }

    public Requisition? Requisition { get; set; }

    /// <summary>Short human reference, e.g. "APP-1001". Unique within the organisation.</summary>
    public required string Reference { get; set; }

    /// <summary>PII.</summary>
    public required string FullName { get; set; }

    /// <summary>PII.</summary>
    public string? Email { get; set; }

    /// <summary>PII.</summary>
    public string? Phone { get; set; }

    public ApplicantStage Stage { get; set; } = ApplicantStage.Applied;

    /// <summary>Where the application came from — job board, referral, direct.</summary>
    public string? Source { get; set; }

    public CvDocument? Cv { get; set; }

    public ExtractedProfile? Profile { get; set; }
}
