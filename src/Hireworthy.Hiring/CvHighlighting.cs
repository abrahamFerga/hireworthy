namespace Hireworthy.Hiring;

/// <summary>One run of CV text, either plain or evidencing a criterion.</summary>
/// <param name="Text">The literal text of this run.</param>
/// <param name="Criteria">
/// Which rubric criteria cite this run. Empty means plain text. More than one means two criteria
/// were evidenced by overlapping spans, which is normal — one sentence can prove two things.
/// </param>
public sealed record CvSegment(string Text, IReadOnlyList<string> Criteria)
{
    public bool Highlighted => Criteria.Count > 0;
}

/// <summary>
/// Turns a CV and its citations into a flat run of segments a UI can render without doing any
/// reasoning of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is deliberately server-side.</b> Putting it in the browser would move the product's
/// central guarantee into a place where it is expensive to test, easy to get subtly wrong on
/// overlapping ranges, and impossible to assert from the existing test ladder. The frontend
/// receives segments and paints them; it decides nothing.
/// </para>
/// <para>
/// It also <b>re-verifies grounding at read time</b>. A citation is checked when it is written
/// (ADR-0005), but a stored offset could drift if anything ever rewrote the CV text. A span whose
/// text no longer matches is dropped from the highlighting rather than rendered in the wrong place —
/// showing a recruiter a highlight over the wrong words is worse than showing none, because it
/// looks like evidence.
/// </para>
/// </remarks>
public static class CvHighlighting
{
    /// <summary>
    /// Splits <paramref name="cvText"/> into ordered segments, marking every span cited by a score.
    /// </summary>
    /// <remarks>
    /// Overlaps are handled by sweeping character positions rather than by emitting one segment per
    /// citation: two criteria citing overlapping ranges produce three segments (A only, both, B
    /// only), which is what lets the UI show a doubly-evidenced sentence without nesting elements.
    /// </remarks>
    public static IReadOnlyList<CvSegment> Segment(string cvText, IEnumerable<CriterionScore> scores)
    {
        if (string.IsNullOrEmpty(cvText))
        {
            return [];
        }

        // Only spans that still verify. An unresolved criterion has no citation by construction.
        var spans = scores
            .Where(s => !s.Unresolved)
            .Where(s => CitationGrounding.Verify(cvText, s.CitationText, s.CitationStart, s.CitationEnd)
                        is GroundingVerdict.Ok)
            .Select(s => (s.CitationStart, s.CitationEnd, s.CriterionName))
            .ToList();

        if (spans.Count == 0)
        {
            return [new CvSegment(cvText, [])];
        }

        // Every offset where the set of covering criteria can change. Sweeping boundaries rather
        // than iterating citations is what makes overlap fall out for free.
        var boundaries = new SortedSet<int> { 0, cvText.Length };
        foreach (var (start, end, _) in spans)
        {
            boundaries.Add(start);
            boundaries.Add(end);
        }

        var cuts = boundaries.ToList();
        var segments = new List<CvSegment>();

        for (var i = 0; i < cuts.Count - 1; i++)
        {
            var from = cuts[i];
            var to = cuts[i + 1];

            if (to <= from) continue;

            var covering = spans
                .Where(s => s.CitationStart <= from && s.CitationEnd >= to)
                .Select(s => s.CriterionName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            var text = cvText[from..to];

            // Merge with the previous run when the covering set is identical, so plain prose does
            // not arrive as a hundred one-character segments.
            if (segments.Count > 0 && segments[^1].Criteria.SequenceEqual(covering, StringComparer.Ordinal))
            {
                segments[^1] = segments[^1] with { Text = segments[^1].Text + text };
                continue;
            }

            segments.Add(new CvSegment(text, covering));
        }

        return segments;
    }
}
