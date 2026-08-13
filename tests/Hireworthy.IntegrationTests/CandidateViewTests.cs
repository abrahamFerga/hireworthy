using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Hireworthy.Hiring;
using Hireworthy.Hiring.Persistence;
using Xunit;

namespace Hireworthy.IntegrationTests;

/// <summary>
/// The Candidate tab's data endpoint — where the citation guarantee becomes something a recruiter
/// can see rather than something a test asserts.
/// </summary>
[Collection("api")]
public sealed class CandidateViewTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task The_candidate_tab_is_declared_and_is_custom_react()
    {
        // A custom-React tab carries NO DataEndpoint — that absence is what tells the shell to look
        // for a registered component instead of rendering a generic table. If a DataEndpoint ever
        // appears here the tab silently reverts to server-driven and the highlighting vanishes.
        using var client = fixture.AdminClient();
        var modules = await client.GetFromJsonAsync<JsonElement>("/api/platform/modules");

        var hiring = modules.EnumerateArray().Single(m => m.GetProperty("id").GetString() == "hiring");
        var candidate = hiring.GetProperty("tabs").EnumerateArray()
            .Single(t => t.GetProperty("id").GetString() == "candidate");

        Assert.Equal("/hiring/candidates/:applicantReference", candidate.GetProperty("route").GetString());
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("dataEndpoint").ValueKind);

        // The tab ID is the key the React side registers against (defineModule). Renaming it
        // unmounts the component with no error anywhere, so it is asserted explicitly.
        Assert.Equal("candidate", candidate.GetProperty("id").GetString());
    }

    [Fact]
    public async Task An_unknown_candidate_is_404_not_an_empty_shell()
    {
        using var client = fixture.AdminClient();
        var response = await client.GetAsync("/api/hiring/candidates/APP-NOPE");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_endpoint_is_permission_gated()
    {
        // It returns a candidate's CV — the most personal payload in the product.
        using var client = fixture.AdminClient(roles: "hiring-compliance", subject: "it-compliance-cv");
        var response = await client.GetAsync("/api/hiring/candidates/APP-1001");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_unscreened_candidate_returns_the_whole_cv_with_nothing_highlighted()
    {
        // The honest empty state: no screening means no citations, so no highlights. It must still
        // return the CV in full rather than an error or a blank.
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();

        var requisitionId = await db.Requisitions.Where(r => r.Reference == "REQ-142").Select(r => r.Id).SingleAsync();
        const string cv = "Engineer at Example Ltd. Wrote Python services.";
        db.Applicants.Add(new Applicant
        {
            TenantId = tenantId,
            RequisitionId = requisitionId,
            Reference = "APP-CVPLAIN",
            FullName = "Plain Candidate",
            Stage = ApplicantStage.Applied,
            Cv = new CvDocument { TenantId = tenantId, FileName = "plain.pdf", ExtractedText = cv },
        });
        await db.SaveChangesAsync();

        using var client = fixture.AdminClient();
        var body = await client.GetFromJsonAsync<JsonElement>("/api/hiring/candidates/APP-CVPLAIN");

        Assert.Equal(JsonValueKind.Null, body.GetProperty("screening").ValueKind);

        var segments = body.GetProperty("cvSegments").EnumerateArray().ToList();
        Assert.Equal(cv, string.Concat(segments.Select(s => s.GetProperty("text").GetString())));
        Assert.All(segments, s => Assert.Empty(s.GetProperty("criteria").EnumerateArray()));
    }

    [Fact]
    public async Task A_screened_candidate_returns_segments_that_rebuild_the_cv_and_mark_the_cited_span()
    {
        // The whole point of the tab, end to end through the real host: score a candidate with a
        // real citation, then read the endpoint and confirm the highlight lands on exactly the
        // quoted words — and that the candidate's CV comes back byte-for-byte.
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();
        var tools = ActivatorUtilities.CreateInstance<HiringTools>(scope.ServiceProvider);

        var requisitionId = await db.Requisitions.Where(r => r.Reference == "REQ-142").Select(r => r.Id).SingleAsync();
        const string cv = "Staff Engineer. Rewrote the reconciliation pipeline in Python. On call.";
        const string quote = "Rewrote the reconciliation pipeline in Python";

        db.Applicants.Add(new Applicant
        {
            TenantId = tenantId,
            RequisitionId = requisitionId,
            Reference = "APP-CVCITED",
            FullName = "Cited Candidate",
            Stage = ApplicantStage.Applied,
            Cv = new CvDocument { TenantId = tenantId, FileName = "cited.pdf", ExtractedText = cv },
        });
        await db.SaveChangesAsync();

        var start = cv.IndexOf(quote, StringComparison.Ordinal);
        await tools.ScreenApplicantAsync("APP-CVCITED",
        [
            new ScoredCriterion(SeededRubric.PythonExperience, 5, quote, start, start + quote.Length, false, "Owned it."),
            .. SeededRubric.UnresolvedExcept(SeededRubric.PythonExperience),
        ]);

        using var client = fixture.AdminClient();
        var body = await client.GetFromJsonAsync<JsonElement>("/api/hiring/candidates/APP-CVCITED");

        Assert.Equal("Cited Candidate", body.GetProperty("fullName").GetString());
        Assert.Equal(1, body.GetProperty("screening").GetProperty("rubricVersion").GetInt32());

        var segments = body.GetProperty("cvSegments").EnumerateArray().ToList();

        // The invariant: the candidate's own words come back exactly.
        Assert.Equal(cv, string.Concat(segments.Select(s => s.GetProperty("text").GetString())));

        // And the highlight is on precisely the cited span — not a word more or less.
        var highlighted = segments
            .Where(s => s.GetProperty("criteria").EnumerateArray().Any())
            .ToList();
        var one = Assert.Single(highlighted);
        Assert.Equal(quote, one.GetProperty("text").GetString());
        Assert.Equal("Production Python experience", one.GetProperty("criteria").EnumerateArray().Single().GetString());

        // The score panel carries the quotation as a quotation. Selected by criterion rather than
        // by being the only row: a valid screening now covers the whole rubric (issue #44), so the
        // other four criteria are present and unresolved. The citation and unresolved assertions
        // below are unchanged; the implicit "exactly one score row" that the bare Single() used to
        // carry is NOT — that constraint now lives in ScreeningTests, which asserts Scores.Count.
        var score = body.GetProperty("scores").EnumerateArray()
            .Single(s => s.GetProperty("criterion").GetString() == SeededRubric.PythonExperience);
        Assert.Equal(quote, score.GetProperty("citationText").GetString());
        Assert.False(score.GetProperty("unresolved").GetBoolean());
    }
}
