using Hireworthy.Hiring;
using Xunit;

namespace Hireworthy.Hiring.Tests;

/// <summary>
/// Turning a CV plus its citations into renderable segments.
/// </summary>
/// <remarks>
/// This is the product's central guarantee becoming visible. A highlight over the wrong words is
/// worse than no highlight at all, because it *looks* like evidence — so these cases are mostly
/// about the ways a naive implementation puts the marker in the wrong place.
/// </remarks>
public sealed class CvHighlightingTests
{
    private const string Cv = "Rewrote the pipeline in Python. On call one week in four.";

    private static CriterionScore Score(string criterion, string quote, bool unresolved = false)
    {
        var start = unresolved ? 0 : Cv.IndexOf(quote, StringComparison.Ordinal);
        if (!unresolved) Assert.True(start >= 0, $"Fixture error: \"{quote}\" not in the CV.");

        return new CriterionScore
        {
            CriterionName = criterion,
            Points = unresolved ? 0 : 4,
            Unresolved = unresolved,
            CitationText = unresolved ? null : quote,
            CitationStart = unresolved ? 0 : start,
            CitationEnd = unresolved ? 0 : start + quote.Length,
        };
    }

    private static string Rebuilt(IReadOnlyList<CvSegment> segs) => string.Concat(segs.Select(s => s.Text));

    [Fact]
    public void The_segments_always_reconstruct_the_cv_exactly()
    {
        // The invariant that matters most: whatever the highlighting does, the candidate's own
        // words must come back byte-for-byte. A renderer that drops or duplicates a character is
        // showing a document the candidate did not write.
        var segs = CvHighlighting.Segment(Cv, [
            Score("Python", "pipeline in Python"),
            Score("On-call", "On call one week in four."),
        ]);

        Assert.Equal(Cv, Rebuilt(segs));
    }

    [Fact]
    public void A_cited_span_is_marked_and_the_rest_is_not()
    {
        var segs = CvHighlighting.Segment(Cv, [Score("Python", "pipeline in Python")]);

        var highlighted = segs.Where(s => s.Highlighted).ToList();
        var one = Assert.Single(highlighted);
        Assert.Equal("pipeline in Python", one.Text);
        Assert.Equal("Python", Assert.Single(one.Criteria));
        Assert.Equal(Cv, Rebuilt(segs));
    }

    [Fact]
    public void Overlapping_citations_produce_a_shared_segment_rather_than_a_duplicate()
    {
        // Two criteria evidenced by overlapping text is normal — one sentence can prove two things.
        // A naive "emit one segment per citation" implementation duplicates the overlap, which
        // renders the candidate's words twice.
        var segs = CvHighlighting.Segment(Cv, [
            Score("Rewriting", "Rewrote the pipeline"),
            Score("Python", "the pipeline in Python"),
        ]);

        Assert.Equal(Cv, Rebuilt(segs));

        var shared = segs.Where(s => s.Criteria.Count > 1).ToList();
        var both = Assert.Single(shared);
        Assert.Equal("the pipeline", both.Text);
        Assert.Contains("Rewriting", both.Criteria);
        Assert.Contains("Python", both.Criteria);
    }

    [Fact]
    public void Citations_supplied_out_of_order_still_segment_correctly()
    {
        // Scores arrive in rubric order, not document order.
        var segs = CvHighlighting.Segment(Cv, [
            Score("On-call", "On call one week in four."),
            Score("Python", "Rewrote the pipeline"),
        ]);

        Assert.Equal(Cv, Rebuilt(segs));
        Assert.StartsWith("Rewrote the pipeline", segs.First(s => s.Highlighted).Text);
    }

    [Fact]
    public void An_unresolved_criterion_highlights_nothing()
    {
        // It has no citation by construction, and inventing one would be fabricating evidence.
        var segs = CvHighlighting.Segment(Cv, [Score("Mentoring", "", unresolved: true)]);

        Assert.Equal(Cv, Rebuilt(segs));
        Assert.DoesNotContain(segs, s => s.Highlighted);
    }

    [Fact]
    public void A_citation_whose_text_no_longer_matches_is_dropped_not_misplaced()
    {
        // The failure this guards: a stored offset that has drifted. Rendering the highlight anyway
        // puts a marker over unrelated words and it looks exactly like evidence. Dropping it shows
        // the recruiter nothing, which is the honest failure.
        var drifted = new CriterionScore
        {
            CriterionName = "Python",
            Points = 5,
            CitationText = "pipeline in Python",
            CitationStart = 0,
            CitationEnd = 18,
        };

        var segs = CvHighlighting.Segment(Cv, [drifted]);

        Assert.Equal(Cv, Rebuilt(segs));
        Assert.DoesNotContain(segs, s => s.Highlighted);
    }

    [Fact]
    public void Plain_prose_is_not_shattered_into_per_character_segments()
    {
        // Boundary sweeping without merging emits one segment per cut, which would hand the browser
        // hundreds of elements for an ordinary CV.
        var segs = CvHighlighting.Segment(Cv, [Score("Python", "Python")]);

        Assert.Equal(3, segs.Count);
        Assert.Equal(Cv, Rebuilt(segs));
    }

    [Fact]
    public void No_citations_yields_one_plain_segment()
    {
        var segs = CvHighlighting.Segment(Cv, []);
        var only = Assert.Single(segs);
        Assert.Equal(Cv, only.Text);
        Assert.False(only.Highlighted);
    }

    [Fact]
    public void An_empty_cv_yields_no_segments_rather_than_throwing()
    {
        Assert.Empty(CvHighlighting.Segment("", []));
    }
}
