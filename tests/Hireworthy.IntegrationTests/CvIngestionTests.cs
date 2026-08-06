using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Hireworthy.Hiring;
using Hireworthy.Hiring.Persistence;
using Xunit;

namespace Hireworthy.IntegrationTests;

/// <summary>
/// CV intake and extraction, exercised through the real host.
/// </summary>
[Collection("api")]
public sealed class CvIngestionTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task The_seeded_applicants_carry_cv_text_with_inconsistent_dates()
    {
        // The seed is the extraction task's input, so it has to actually contain the problem:
        // dates written three different ways. A pre-normalised seed would make parse_cv look like
        // it works when it had nothing to reason over.
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();

        var maya = await db.Applicants.Include(a => a.Cv)
            .SingleAsync(a => a.Reference == "APP-1001");

        Assert.Equal("Maya Osei", maya.FullName);
        Assert.NotNull(maya.Cv);
        Assert.Contains("Jan 2021 to present", maya.Cv!.ExtractedText);
        Assert.Contains("03/2017 – 02/2020", maya.Cv.ExtractedText);
        Assert.Contains("September 2015 until February 2017", maya.Cv.ExtractedText);
    }

    [Fact]
    public async Task Reading_an_applicant_returns_the_cv_text_the_model_has_to_extract_from()
    {
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(client, "hiring", "Show me applicant APP-1001");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.Contains("get_applicant", turn.ToolCalls);
        Assert.False(turn.RequiredApproval);
    }

    [Fact]
    public async Task Parsing_a_cv_runs_end_to_end_and_does_not_park_on_the_gate()
    {
        // ADR-0004: derived, recomputable, moves nobody's stage — so it is deliberately ungated.
        // Asserted through AdminClient so it goes through the real RBAC and approval pipeline;
        // AuthorizedScopeAsync bypasses both and could never prove this.
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(client, "hiring", "Parse the CV for APP-1001");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.Contains("parse_cv", turn.ToolCalls);
        Assert.False(turn.RequiredApproval,
            "parse_cv parked on the approval gate. It writes derived data and must not (ADR-0004).");
    }

    [Fact]
    public async Task Extraction_persists_the_roles_and_computes_the_gap()
    {
        // The full path: tool -> entities -> computed gaps, against a real Postgres.
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();
        var tools = ActivatorUtilities.CreateInstance<HiringTools>(scope.ServiceProvider);

        var result = await tools.ParseCvAsync(
            "APP-1001",
            [
                new ExtractedRole("Kestrel Payments", "Staff Engineer", "2021-01", null),
                new ExtractedRole("Northwind Retail", "Backend Engineer", "2017-03", "2020-02"),
                new ExtractedRole("Halloway Systems", "Junior Developer", "2015-09", "2017-02"),
            ],
            ["Python", "PostgreSQL", "Go"],
            ["BSc Computer Science, University of Manchester, 2015"],
            unresolved: null);

        Assert.Contains("3 role(s)", result);

        var profile = await db.ExtractedProfiles
            .Include(p => p.Employment)
            .SingleAsync(p => p.Applicant!.Reference == "APP-1001");

        Assert.Equal(3, profile.Employment.Count);
        Assert.Equal(tenantId, profile.TenantId);
        Assert.Contains("Python", profile.Skills);

        // The 11-month gap between Northwind and Kestrel — and only that one. The 1-month break
        // between Halloway and Northwind is a notice period, not a gap worth asking about.
        var gap = Assert.Single(profile.EmploymentGaps);
        Assert.Contains("11 months", gap);
    }

    [Fact]
    public async Task Re_parsing_replaces_the_profile_rather_than_accumulating_roles()
    {
        // A profile is a statement about one CV. Two overlapping extractions would double every
        // role and quietly corrupt the evidence a decision later rests on.
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();
        var tools = ActivatorUtilities.CreateInstance<HiringTools>(scope.ServiceProvider);

        ExtractedRole[] roles = [new("Vantage Logistics", "Senior Engineer", "2022-01", null)];

        await tools.ParseCvAsync("APP-1002", roles, ["Python"], [], null);
        await tools.ParseCvAsync("APP-1002", roles, ["Python", "SQL"], [], null);

        var profiles = await db.ExtractedProfiles
            .Where(p => p.Applicant!.Reference == "APP-1002")
            .Include(p => p.Employment)
            .ToListAsync();

        var profile = Assert.Single(profiles);
        Assert.Single(profile.Employment);
        Assert.Equal(2, profile.Skills.Count);
    }

    [Fact]
    public async Task A_date_it_cannot_read_is_reported_rather_than_guessed()
    {
        // parse_cv is ungated, so the model is still in a retry loop and can correct itself —
        // guidance, not an exception. The opposite of propose_rubric, which is gated and must
        // throw because a human has already approved by the time it runs.
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var tools = ActivatorUtilities.CreateInstance<HiringTools>(scope.ServiceProvider);

        var result = await tools.ParseCvAsync(
            "APP-1001",
            [new ExtractedRole("Somewhere", "Engineer", "five years ago", null)],
            [], [], null);

        Assert.Contains("Could not read the start date", result);
        Assert.Contains("yyyy-MM", result);
    }
}
