using Hireworthy.Hiring;
using Xunit;

namespace Hireworthy.Hiring.Tests;

/// <summary>
/// The product's central guarantee: a score's citation must be a verbatim span of the CV.
/// </summary>
/// <remarks>
/// If these tests are ever weakened to make something pass, the product no longer does the one
/// thing it claims to do. A paraphrase must fail. A quotation that is not really at those offsets
/// must fail. "Close enough" is the failure mode this exists to make impossible.
/// </remarks>
public sealed class CitationGroundingTests
{
    private const string Cv =
        "Staff Engineer, Kestrel Payments — Jan 2021 to present\n"
      + "  Rewrote the reconciliation pipeline in Python; cut end-of-day breaks from ~40 a night to under 5.";

    private static (int Start, int End) SpanOf(string text)
    {
        var start = Cv.IndexOf(text, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Test fixture is wrong: \"{text}\" is not in the CV.");
        return (start, start + text.Length);
    }

    [Fact]
    public void A_verbatim_span_is_grounded()
    {
        var (start, end) = SpanOf("Rewrote the reconciliation pipeline in Python");

        Assert.Equal(
            GroundingVerdict.Ok,
            CitationGrounding.Verify(Cv, "Rewrote the reconciliation pipeline in Python", start, end));
    }

    [Fact]
    public void A_paraphrase_is_rejected_even_though_it_is_true()
    {
        // The candidate DID rewrite a pipeline in Python. The paraphrase is accurate and still
        // fails, because the product's claim is "we quote the CV", not "we summarise it fairly".
        // This is the single most important assertion in the repo.
        var (start, end) = SpanOf("Rewrote the reconciliation pipeline in Python");

        Assert.Equal(
            GroundingVerdict.TextDoesNotMatch,
            CitationGrounding.Verify(Cv, "rebuilt the reconciliation pipeline using Python", start, end));
    }

    [Fact]
    public void A_real_quote_at_the_wrong_offsets_is_rejected()
    {
        // The words appear in the CV, but not there. Accepting this would let a citation point at
        // an unrelated part of the document and still look grounded on screen.
        var (start, _) = SpanOf("Staff Engineer");

        Assert.Equal(
            GroundingVerdict.TextDoesNotMatch,
            CitationGrounding.Verify(Cv, "Rewrote the reconciliation pipeline in Python", start, start + 44));
    }

    [Fact]
    public void A_fabricated_quote_is_rejected()
    {
        var (start, end) = SpanOf("Kestrel Payments");

        Assert.Equal(
            GroundingVerdict.TextDoesNotMatch,
            CitationGrounding.Verify(Cv, "Led a team of twelve engineers", start, end));
    }

    [Fact]
    public void Offsets_past_the_end_are_rejected_rather_than_throwing()
    {
        // A model that invents offsets must get a usable error, not an unhandled exception that
        // surfaces as a 500 and tells the recruiter nothing.
        Assert.Equal(
            GroundingVerdict.OutOfRange,
            CitationGrounding.Verify(Cv, "anything", Cv.Length - 2, Cv.Length + 50));

        Assert.Equal(GroundingVerdict.OutOfRange, CitationGrounding.Verify(Cv, "anything", -1, 5));
        Assert.Equal(GroundingVerdict.OutOfRange, CitationGrounding.Verify(Cv, "anything", 10, 10));
    }

    [Fact]
    public void A_missing_citation_is_its_own_verdict()
    {
        // Distinct from TextDoesNotMatch so the message can tell the model to mark the criterion
        // unresolved rather than to fix its quotation.
        Assert.Equal(GroundingVerdict.Missing, CitationGrounding.Verify(Cv, null, 0, 10));
        Assert.Equal(GroundingVerdict.Missing, CitationGrounding.Verify(Cv, "   ", 0, 10));
    }

    [Fact]
    public void Surrounding_whitespace_does_not_defeat_a_real_quotation()
    {
        // Line wrapping in an extracted PDF is an artefact of the extractor, not of what the
        // candidate wrote — so leading/trailing whitespace is trimmed. Interior text is not.
        var (start, end) = SpanOf("  Rewrote the reconciliation pipeline in Python");

        Assert.Equal(
            GroundingVerdict.Ok,
            CitationGrounding.Verify(Cv, "Rewrote the reconciliation pipeline in Python", start, end));
    }

    [Fact]
    public void The_explanation_shows_what_the_cv_actually_says_there()
    {
        // A message a recruiter or an agent can act on beats a boolean.
        var (start, end) = SpanOf("Staff Engineer");
        var message = CitationGrounding.Explain(
            GroundingVerdict.TextDoesNotMatch, "Production Python experience", Cv, start, end);

        Assert.Contains("Production Python experience", message);
        Assert.Contains("Staff Engineer", message);
        Assert.Contains("verbatim", message, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// The scoring arithmetic. Deterministic by construction — the model's judgement varies, everything
/// downstream of it must not.
/// </summary>
public sealed class ScreeningTotalTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private static CriterionScore Score(Guid criterionId, int points, bool unresolved = false) => new()
    {
        RubricCriterionId = criterionId,
        CriterionName = "c",
        Points = points,
        Unresolved = unresolved,
    };

    [Fact]
    public void The_total_is_weighted_by_each_criterion()
    {
        // 4 points x weight 5, plus 2 points x weight 1 = 22, out of (5x5 + 5x1) = 30.
        var (total, max, unresolved) = ScreeningResult.ComputeTotal(
            [Score(A, 4), Score(B, 2)],
            new Dictionary<Guid, int> { [A] = 5, [B] = 1 });

        Assert.Equal(22, total);
        Assert.Equal(30, max);
        Assert.Equal(0, unresolved);
    }

    [Fact]
    public void An_unresolved_criterion_scores_zero_but_still_counts_toward_the_maximum()
    {
        // The arguable choice, asserted so it cannot drift silently: the burden of evidence is on
        // the application. Excluding it from the maximum would quietly reward a vague CV.
        var (total, max, unresolved) = ScreeningResult.ComputeTotal(
            [Score(A, 5), Score(B, 0, unresolved: true)],
            new Dictionary<Guid, int> { [A] = 2, [B] = 3 });

        Assert.Equal(10, total);
        Assert.Equal(25, max);
        Assert.Equal(1, unresolved);
    }

    [Fact]
    public void The_same_input_always_produces_the_same_total()
    {
        // Acceptance criterion: "re-running the same input produces the same score".
        var scores = new[] { Score(A, 3), Score(B, 1) };
        var weights = new Dictionary<Guid, int> { [A] = 4, [B] = 2 };

        var first = ScreeningResult.ComputeTotal(scores, weights);
        var second = ScreeningResult.ComputeTotal(scores, weights);

        Assert.Equal(first, second);
    }

    [Fact]
    public void An_unknown_criterion_weight_defaults_to_one_rather_than_throwing()
    {
        var (total, max, _) = ScreeningResult.ComputeTotal(
            [Score(A, 3)], new Dictionary<Guid, int>());

        Assert.Equal(3, total);
        Assert.Equal(5, max);
    }
}
