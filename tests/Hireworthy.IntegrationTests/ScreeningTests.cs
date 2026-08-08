using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Hireworthy.Hiring;
using Hireworthy.Hiring.Persistence;
using Xunit;

namespace Hireworthy.IntegrationTests;

/// <summary>
/// Cited screening against a real host and a real Postgres.
/// </summary>
/// <remarks>
/// The assertions that matter here are the refusals: a fabricated citation must not persist, and a
/// rubric nobody approved must not be usable. Both are things a green build says nothing about.
/// </remarks>
[Collection("api")]
public sealed class ScreeningTests(IntegrationFixture fixture)
{
    private static async Task<(HiringTools Tools, HiringDbContext Db, string Cv)> ArrangeAsync(
        IntegrationFixture fixture, IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();
        var tools = ActivatorUtilities.CreateInstance<HiringTools>(scope.ServiceProvider);
        var cv = await db.CvDocuments
            .Where(c => c.Applicant!.Reference == "APP-1001")
            .Select(c => c.ExtractedText)
            .SingleAsync();
        return (tools, db, cv);
    }

    /// <summary>Builds a genuinely grounded score by locating the span in the real CV.</summary>
    private static ScoredCriterion Grounded(string cv, string criterion, int points, string quote)
    {
        var start = cv.IndexOf(quote, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Fixture error: \"{quote}\" is not in the seeded CV.");
        return new ScoredCriterion(criterion, points, quote, start, start + quote.Length, false, null);
    }

    [Fact]
    public async Task The_seeded_rubric_is_approved_so_screening_has_a_yardstick()
    {
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();

        var rubric = await db.Rubrics.Include(r => r.Criteria)
            .SingleAsync(r => r.Requisition!.Reference == "REQ-142");

        Assert.Equal(RubricStatus.Approved, rubric.Status);
        Assert.Equal(5, rubric.Criteria.Count);
    }

    [Fact]
    public async Task A_grounded_screening_persists_with_its_citations()
    {
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db, cv) = await ArrangeAsync(fixture, scope);

        var result = await tools.ScreenApplicantAsync("APP-1001",
        [
            Grounded(cv, "Production Python experience", 5, "Rewrote the reconciliation pipeline in Python"),
            Grounded(cv, "On-call ownership", 5, "On call one week in four."),
            new ScoredCriterion("Written communication", 0, null, 0, 0, true, "CV does not mention design docs"),
            Grounded(cv, "Mentoring", 4, "Mentored two juniors through their first on-call rotations."),
            Grounded(cv, "Payments or ledger domain", 5, "Own the settlement service."),
        ]);

        Assert.Contains("against rubric v1", result);

        var screening = await db.ScreeningResults
            .Include(r => r.Scores)
            .SingleAsync(r => r.Applicant!.Reference == "APP-1001" && r.Status == ScreeningStatus.Proposed);

        Assert.Equal(tenantId, screening.TenantId);
        Assert.Equal(1, screening.RubricVersion);
        Assert.Equal(5, screening.Scores.Count);
        Assert.Equal(1, screening.UnresolvedCount);

        // 5x5 + 5x4 + (unresolved, 0) + 4x3 + 5x2 = 67, out of 5x(5+4+4+3+2) = 90.
        Assert.Equal(67, screening.TotalScore);
        Assert.Equal(90, screening.MaxScore);

        // Every stored citation is still verbatim in the CV — the guarantee, re-checked after a
        // round trip through Postgres rather than only at the moment of writing.
        foreach (var score in screening.Scores.Where(s => !s.Unresolved))
        {
            Assert.Equal(
                GroundingVerdict.Ok,
                CitationGrounding.Verify(cv, score.CitationText, score.CitationStart, score.CitationEnd));
        }
    }

    [Fact]
    public async Task A_fabricated_citation_is_refused_and_nothing_is_written()
    {
        // The failure this product exists to prevent: a plausible quotation that is not in the CV.
        // It must not reach a recruiter's screen, so it must not be persisted at all.
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db, cv) = await ArrangeAsync(fixture, scope);

        var before = await db.ScreeningResults.CountAsync(r => r.Applicant!.Reference == "APP-1002");

        var result = await tools.ScreenApplicantAsync("APP-1002",
        [
            new ScoredCriterion("Production Python experience", 5,
                "Led a team of twelve engineers building payment rails", 0, 52, false, null),
        ]);

        Assert.Contains("verbatim", result, StringComparison.OrdinalIgnoreCase);

        var after = await db.ScreeningResults.CountAsync(r => r.Applicant!.Reference == "APP-1002");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task A_paraphrase_is_refused_even_though_it_is_accurate()
    {
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, _, cv) = await ArrangeAsync(fixture, scope);

        var quote = "Rewrote the reconciliation pipeline in Python";
        var start = cv.IndexOf(quote, StringComparison.Ordinal);

        var result = await tools.ScreenApplicantAsync("APP-1001",
        [
            new ScoredCriterion("Production Python experience", 5,
                "rebuilt the reconciliation pipeline using Python", start, start + quote.Length, false, null),
        ]);

        Assert.Contains("do not paraphrase", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_criterion_that_is_not_on_the_rubric_is_refused()
    {
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, _, cv) = await ArrangeAsync(fixture, scope);

        var result = await tools.ScreenApplicantAsync("APP-1001",
        [
            Grounded(cv, "Charisma", 5, "Own the settlement service."),
        ]);

        Assert.Contains("not a criterion on approved rubric", result);
    }

    [Fact]
    public async Task A_partial_score_set_is_refused_and_nothing_is_written()
    {
        // Issue #44. Supplying only the criterion the CV evidences well used to shrink the
        // yardstick: APP-1002 read 25/25 — "every criterion evidenced" — while four criteria were
        // never looked at, outranking a genuinely stronger candidate scored honestly at 80/90.
        // Omission must cost what an unresolved mark costs, so it is refused outright rather than
        // scored as unresolved on the model's behalf: "the CV does not evidence this" is a
        // judgement, and this product may not assert one nobody made.
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db, _) = await ArrangeAsync(fixture, scope);
        var cv = await db.CvDocuments
            .Where(c => c.Applicant!.Reference == "APP-1002")
            .Select(c => c.ExtractedText)
            .SingleAsync();

        var before = await db.ScreeningResults.CountAsync(r => r.Applicant!.Reference == "APP-1002");

        var result = await tools.ScreenApplicantAsync("APP-1002",
        [
            Grounded(cv, "Production Python experience", 5, "Python services for freight tracking"),
        ]);

        Assert.Contains("Missing:", result);
        Assert.Contains("On-call ownership", result);
        Assert.DoesNotContain("every criterion evidenced", result);

        var after = await db.ScreeningResults.CountAsync(r => r.Applicant!.Reference == "APP-1002");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task A_repeated_criterion_is_refused_and_nothing_is_written()
    {
        // Issue #45, and the case a completeness check alone does not see: every criterion IS
        // covered, so nothing is "Missing:" — one of them is simply supplied twice. Left unchecked
        // this is worse than the omission it sits next to, because the maximum now belongs to the
        // rubric while the total is still summed per row: an honest 60/90 with the highest-weighted
        // criterion repeated reads 85/90 (94.4%) and overtakes the honest 80/90 candidate from
        // issue #44's own table. Nothing on the Candidate tab shows it — CvHighlighting.Segment
        // de-duplicates the covering names, so the citation is not doubled on screen.
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db, _) = await ArrangeAsync(fixture, scope);
        var cv = await db.CvDocuments
            .Where(c => c.Applicant!.Reference == "APP-1002")
            .Select(c => c.ExtractedText)
            .SingleAsync();

        var before = await db.ScreeningResults.CountAsync(r => r.Applicant!.Reference == "APP-1002");

        var python = Grounded(cv, SeededRubric.PythonExperience, 5, "Python services for freight tracking");

        var result = await tools.ScreenApplicantAsync("APP-1002",
        [
            python,
            .. SeededRubric.UnresolvedExcept(SeededRubric.PythonExperience),
            python,
        ]);

        Assert.Contains("Scored more than once:", result);
        Assert.Contains(SeededRubric.PythonExperience, result);
        Assert.DoesNotContain("Missing:", result);
        Assert.DoesNotContain("every criterion evidenced", result);

        var after = await db.ScreeningResults.CountAsync(r => r.Applicant!.Reference == "APP-1002");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Re_screening_supersedes_rather_than_leaving_two_live_scores()
    {
        // Two live results would make "their score" ambiguous at exactly the moment somebody is
        // deciding an application.
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db, cv) = await ArrangeAsync(fixture, scope);

        const string mentoring = "Mentored two juniors through their first on-call rotations.";

        await tools.ScreenApplicantAsync("APP-1001",
            [Grounded(cv, "Mentoring", 3, mentoring), .. SeededRubric.UnresolvedExcept("Mentoring")]);
        await tools.ScreenApplicantAsync("APP-1001",
            [Grounded(cv, "Mentoring", 5, mentoring), .. SeededRubric.UnresolvedExcept("Mentoring")]);

        var live = await db.ScreeningResults
            .Where(r => r.Applicant!.Reference == "APP-1001" && r.Status == ScreeningStatus.Proposed)
            .ToListAsync();

        Assert.Single(live);
    }

    [Fact]
    public async Task Screening_runs_end_to_end_through_agui_and_does_not_park_on_the_gate()
    {
        // Through AdminClient so it goes through the real RBAC and approval pipeline.
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(
            client, "hiring", "Screen applicant APP-1001 against the approved rubric");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.Contains("screen_applicant", turn.ToolCalls);
        Assert.False(turn.RequiredApproval,
            "screen_applicant parked on the approval gate. It writes a proposal and must not (ADR-0004).");
    }
}
