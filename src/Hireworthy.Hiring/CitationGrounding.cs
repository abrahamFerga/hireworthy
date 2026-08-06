namespace Hireworthy.Hiring;

/// <summary>Why a citation was rejected, or <see cref="Ok"/> if it was not.</summary>
public enum GroundingVerdict
{
    Ok = 0,

    /// <summary>The offsets fall outside the source text.</summary>
    OutOfRange = 1,

    /// <summary>The offsets are inside the text but do not contain the quoted words.</summary>
    TextDoesNotMatch = 2,

    /// <summary>A citation was required and none was given.</summary>
    Missing = 3,
}

/// <summary>
/// The product's central guarantee, expressed as arithmetic rather than trust.
/// </summary>
/// <remarks>
/// <para>
/// Every score a criterion carries must quote a span that occurs <b>verbatim</b> in the candidate's
/// own CV, at the offsets given. That is a string comparison — deterministic, cheap, and impossible
/// to be nearly right about. It is what separates this product from a model that produces a
/// plausible-sounding justification: a paraphrase fails here, and so does a fabrication.
/// </para>
/// <para>
/// <b>This is enforced at write time, not merely tested.</b> A screening result whose citation does
/// not verify is never persisted, so a fabricated quote cannot reach a recruiter's screen and cannot
/// end up in the evidence chain behind a rejection. Detecting it later would be too late — by then
/// somebody has read it and acted on it. See ADR-0005.
/// </para>
/// </remarks>
public static class CitationGrounding
{
    /// <summary>
    /// Checks that <paramref name="citation"/> is exactly what <paramref name="source"/> contains
    /// between the given offsets.
    /// </summary>
    /// <remarks>
    /// <paramref name="end"/> is exclusive, so <c>source[start..end]</c> is the cited span and
    /// <c>end - start</c> is its length. Comparison is ordinal and case-sensitive on purpose: a CV
    /// says what it says, and a citation that had to be case-folded to match is not a quotation.
    /// Surrounding whitespace is trimmed from the comparison only because line wrapping in an
    /// extracted PDF is an artefact of the extractor, not of what the candidate wrote.
    /// </remarks>
    public static GroundingVerdict Verify(string source, string? citation, int start, int end)
    {
        if (string.IsNullOrWhiteSpace(citation))
        {
            return GroundingVerdict.Missing;
        }

        if (start < 0 || end <= start || end > source.Length)
        {
            return GroundingVerdict.OutOfRange;
        }

        var span = source[start..end];

        return string.Equals(span.Trim(), citation.Trim(), StringComparison.Ordinal)
            ? GroundingVerdict.Ok
            : GroundingVerdict.TextDoesNotMatch;
    }

    /// <summary>A message worth reading, naming the criterion and what was actually at the offsets.</summary>
    public static string Explain(GroundingVerdict verdict, string criterion, string source, int start, int end) =>
        verdict switch
        {
            GroundingVerdict.Missing =>
                $"Criterion \"{criterion}\" has no citation. Quote the span of the CV that evidences it, "
              + "or mark the criterion unresolved — do not score it from memory.",

            GroundingVerdict.OutOfRange =>
                $"Criterion \"{criterion}\" cites characters {start}–{end}, which is outside the CV "
              + $"(length {source.Length}). Re-read the offsets.",

            GroundingVerdict.TextDoesNotMatch =>
                $"Criterion \"{criterion}\" quotes text that is not at characters {start}–{end}. "
              + $"What the CV actually says there is: \"{Excerpt(source, start, end)}\". "
              + "Quote the CV verbatim; do not paraphrase it.",

            _ => $"Criterion \"{criterion}\" is grounded.",
        };

    private static string Excerpt(string source, int start, int end)
    {
        if (start < 0 || start >= source.Length) return "(nothing — the offset is past the end)";

        var safeEnd = Math.Min(end <= start ? start + 40 : end, source.Length);
        var text = source[start..safeEnd];

        return text.Length > 120 ? text[..120] + "…" : text;
    }
}
