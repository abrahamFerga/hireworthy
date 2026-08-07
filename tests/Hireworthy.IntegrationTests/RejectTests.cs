using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Hireworthy.Hiring;
using Hireworthy.Hiring.Persistence;
using Xunit;

namespace Hireworthy.IntegrationTests;

/// <summary>
/// Rejecting a candidate — the decision this product is ultimately judged on.
/// </summary>
/// <remarks>
/// A rejection is the decision a candidate may challenge, the one a bias auditor samples, and the
/// one NYC LL144 and the EU AI Act are written about. Every test here is a refusal or an evidence
/// assertion, because those are what a green build cannot tell you.
/// </remarks>
[Collection("api")]
public sealed class RejectTests(IntegrationFixture fixture)
{
    /// <summary>An applicant that exists only for this test — see AdvanceTests for why.</summary>
    private static async Task<string> NewScreenedApplicantAsync(
        HiringTools tools, HiringDbContext db, Guid tenantId, string suffix)
    {
        var requisitionId = await db.Requisitions
            .Where(r => r.Reference == "REQ-142").Select(r => r.Id).SingleAsync();
        var reference = $"APP-R{suffix}";

        db.Applicants.Add(new Applicant
        {
            TenantId = tenantId,
            RequisitionId = requisitionId,
            Reference = reference,
            FullName = $"Reject Candidate {suffix}",
            Stage = ApplicantStage.Applied,
            Cv = new CvDocument
            {
                TenantId = tenantId,
                FileName = $"{suffix.ToLowerInvariant()}.pdf",
                ExtractedText = "Engineer at Example Ltd. Some Python. No production ownership stated.",
            },
        });
        await db.SaveChangesAsync();

        const string quote = "Some Python";
        var cv = await db.CvDocuments.Where(c => c.Applicant!.Reference == reference)
            .Select(c => c.ExtractedText).SingleAsync();
        var start = cv.IndexOf(quote, StringComparison.Ordinal);

        await tools.ScreenApplicantAsync(reference,
        [
            new ScoredCriterion(SeededRubric.PythonExperience, 1, quote, start, start + quote.Length, false,
                "Mentions Python but evidences no production ownership."),
            .. SeededRubric.UnresolvedExcept(SeededRubric.PythonExperience),
        ]);

        return reference;
    }

    private static (HiringTools Tools, HiringDbContext Db) Arrange(IServiceScope scope) =>
        (ActivatorUtilities.CreateInstance<HiringTools>(scope.ServiceProvider),
         scope.ServiceProvider.GetRequiredService<HiringDbContext>());

    [Fact]
    public async Task Rejecting_an_unscreened_candidate_throws_and_rejects_nobody()
    {
        // A rejection with no evidence behind it is the single worst artifact this system could
        // produce: an adverse employment decision nobody can account for.
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = Arrange(scope);

        var requisitionId = await db.Requisitions
            .Where(r => r.Reference == "REQ-142").Select(r => r.Id).SingleAsync();
        db.Applicants.Add(new Applicant
        {
            TenantId = tenantId,
            RequisitionId = requisitionId,
            Reference = "APP-RNOEVIDENCE",
            FullName = "Unscreened",
            Stage = ApplicantStage.Applied,
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.RejectCandidateAsync("APP-RNOEVIDENCE", "Not a fit"));

        Assert.Contains("no evidence", ex.Message);
        Assert.Contains("Nobody was rejected", ex.Message);

        db.ChangeTracker.Clear();
        var after = await db.Applicants.SingleAsync(a => a.Reference == "APP-RNOEVIDENCE");
        Assert.Equal(ApplicantStage.Applied, after.Stage);
        Assert.Empty(await db.Decisions.Where(d => d.Applicant!.Reference == "APP-RNOEVIDENCE").ToListAsync());
    }

    [Fact]
    public async Task A_missing_reason_throws()
    {
        // This is the only account of why a person was turned down. An empty one makes the audit
        // trail technically complete and practically useless to the person who needs it most.
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = Arrange(scope);
        var reference = await NewScreenedApplicantAsync(tools, db, tenantId, "NOREASON");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.RejectCandidateAsync(reference, "  "));
    }

    [Fact]
    public async Task A_rejection_records_the_decision_and_the_evidence_it_rests_on()
    {
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = Arrange(scope);
        var reference = await NewScreenedApplicantAsync(tools, db, tenantId, "OK");

        var screening = await db.ScreeningResults
            .SingleAsync(r => r.Applicant!.Reference == reference && r.Status == ScreeningStatus.Proposed);

        var result = await tools.RejectCandidateAsync(
            reference, "Scored 1/5 on production Python; the rubric weights it highest.");

        Assert.Contains("Rejected", result);

        db.ChangeTracker.Clear();
        var decision = await db.Decisions.SingleAsync(d => d.Applicant!.Reference == reference);

        Assert.Equal(DecisionKind.Reject, decision.Kind);
        Assert.Equal(tenantId, decision.TenantId);
        Assert.Equal(ApplicantStage.Rejected, decision.ToStage);
        Assert.Contains("production Python", decision.Reason);

        // The evidence chain: this rejection points at the screening that justified it, which
        // points at cited spans of the candidate's own CV.
        Assert.Equal(screening.Id, decision.ScreeningResultId);

        var applicant = await db.Applicants.SingleAsync(a => a.Reference == reference);
        Assert.Equal(ApplicantStage.Rejected, applicant.Stage);
    }

    [Fact]
    public async Task Rejecting_twice_throws()
    {
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = Arrange(scope);
        var reference = await NewScreenedApplicantAsync(tools, db, tenantId, "TWICE");

        await tools.RejectCandidateAsync(reference, "Below the bar on the weighted criteria.");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.RejectCandidateAsync(reference, "Again"));

        Assert.Contains("already been rejected", ex.Message);

        db.ChangeTracker.Clear();
        Assert.Single(await db.Decisions.Where(d => d.Applicant!.Reference == reference).ToListAsync());
    }

    [Fact]
    public async Task A_hired_candidate_cannot_be_rejected()
    {
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = Arrange(scope);
        var reference = await NewScreenedApplicantAsync(tools, db, tenantId, "HIRED");

        var applicant = await db.Applicants.SingleAsync(a => a.Reference == reference);
        applicant.Stage = ApplicantStage.Hired;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.RejectCandidateAsync(reference, "Changed our minds"));

        Assert.Contains("has been hired", ex.Message);
    }

    [Fact]
    public async Task The_reject_turn_parks_on_the_approval_gate()
    {
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(client, "hiring", "Reject APP-1002");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.Contains("reject_candidate", turn.ToolCalls);
        Assert.True(turn.RequiredApproval,
            $"reject_candidate did not park on the gate. CUSTOM events were [{string.Join(", ", turn.CustomEvents)}].");
    }

    [Fact]
    public async Task The_reply_does_not_claim_anyone_was_rejected_while_the_write_is_parked()
    {
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(client, "hiring", "Reject APP-1002");

        foreach (var claim in new[] { "has been rejected", "was rejected", "successfully rejected" })
        {
            Assert.False(turn.Text.Contains(claim, StringComparison.OrdinalIgnoreCase),
                $"The reply claims \"{claim}\" while the write is parked. Reply was: {turn.Text}");
        }
    }

    [Fact]
    public async Task A_recruiter_CAN_reach_reject_but_still_not_advance()
    {
        // The asymmetry SPEC.md §3 describes, asserted in one place so it cannot drift: a recruiter
        // may reject and schedule; advancing is the hiring manager's call. If both ever became
        // reachable, the tier would stop meaning anything.
        using var client = fixture.AdminClient(roles: "hiring-recruiter", subject: "it-recruiter-rej");

        var reject = await AguiStream.PostAsync(client, "hiring", "Reject APP-1002");
        Assert.False(reject.Failed, $"RUN_ERROR: {reject.Error}");
        Assert.Contains("reject_candidate", reject.ToolCalls);
        Assert.True(reject.RequiredApproval);

        var advance = await AguiStream.PostAsync(client, "hiring", "Advance APP-1001 to the next stage");
        Assert.False(advance.Failed, $"RUN_ERROR: {advance.Error}");
        Assert.DoesNotContain("advance_candidates", advance.ToolCalls);
    }

    [Fact]
    public async Task A_sourcer_can_reach_neither()
    {
        using var client = fixture.AdminClient(roles: "hiring-sourcer", subject: "it-sourcer-rej");
        var turn = await AguiStream.PostAsync(client, "hiring", "Reject APP-1002");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.DoesNotContain("reject_candidate", turn.ToolCalls);
        Assert.DoesNotContain("advance_candidates", turn.ToolCalls);
    }
}
