using Plenipo.Core.Entities;
using Plenipo.Core.Multitenancy;

namespace Hireworthy.Hiring;

/// <summary>
/// The candidate's CV as text, and the record of how that text was obtained.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="ExtractedText"/> is the source of truth for citation grounding.</b> Every score a
/// criterion later carries must quote a span that occurs verbatim in this field — that check is
/// deterministic and build-breaking (ADR-0005), and it is the product's central guarantee. So this
/// text is stored exactly as extracted and is never normalised, re-wrapped or summarised: doing so
/// would silently invalidate every offset stored against it.
/// </para>
/// <para>
/// The platform supplies the file store and the OCR. This entity is only the extracted text plus
/// the provenance a reviewer needs — a scanned two-column PDF that went through OCR deserves less
/// confidence than a text PDF, and <see cref="OcrUsed"/> is what makes that visible.
/// </para>
/// </remarks>
public sealed class CvDocument : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ApplicantId { get; set; }

    public Applicant? Applicant { get; set; }

    public required string FileName { get; set; }

    /// <summary>The CV, verbatim. PII. Never normalise this — citation offsets point into it.</summary>
    public required string ExtractedText { get; set; }

    /// <summary>True when the text came from OCR rather than an embedded text layer.</summary>
    public bool OcrUsed { get; set; }
}
