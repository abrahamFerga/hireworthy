using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Hireworthy.Hiring;
using Hireworthy.Hiring.Persistence;
using Plenipo.Application.Approvals;
using Plenipo.Core.Platform;
using Xunit;

namespace Hireworthy.IntegrationTests;

/// <summary>
/// Advancing a candidate — the first write in this product with a consequence that cannot be undone.
/// </summary>
/// <remarks>
/// Every assertion here is about a <b>refusal</b>. The happy path is easy and a green build already
/// half-implies it; what a green build says nothing about is whether the gate fires, whether an
/// unevidenced advance is impossible, and whether a failure throws instead of quietly reporting
/// success to a hiring manager.
/// </remarks>
[Collection("api")]
public sealed class AdvanceTests(IntegrationFixture fixture)
{
    private static async Task<(HiringTools Tools, HiringDbContext Db)> ArrangeAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();
        var tools = ActivatorUtilities.CreateInstance<HiringTools>(scope.ServiceProvider);
        return (tools, db);
    }

    private static async Task ScreenAsync(HiringTools tools, HiringDbContext db, string reference)
    {
        var cv = await db.CvDocuments
            .Where(c => c.Applicant!.Reference == reference)
            .Select(c => c.ExtractedText)
            .SingleAsync();

        var quote = cv[..Math.Min(40, cv.Length)];
        await tools.ScreenApplicantAsync(reference,
        [
            new ScoredCriterion("Production Python experience", 4, quote, 0, quote.Length, false, null),
        ]);
    }

    /// <summary>
    /// Creates an applicant that exists only for this test.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT reusing a seeded applicant. Tests in this collection share one database, and
    /// xUnit gives no ordering guarantee — so a test asserting "this candidate has never been
    /// screened" would pass or fail depending on whether a sibling test screened them first. That is
    /// a flake that shows up weeks later in CI and gets blamed on the runner.
    /// </remarks>
    private static async Task<string> NewUnscreenedApplicantAsync(HiringDbContext db, Guid tenantId, string suffix)
    {
        var requisitionId = await db.Requisitions.Where(r => r.Reference == "REQ-142").Select(r => r.Id).SingleAsync();
        var reference = $"APP-T{suffix}";

        db.Applicants.Add(new Applicant
        {
            TenantId = tenantId,
            RequisitionId = requisitionId,
            Reference = reference,
            FullName = $"Test Candidate {suffix}",
            Stage = ApplicantStage.Applied,
            Cv = new CvDocument
            {
                TenantId = tenantId,
                FileName = $"{suffix.ToLowerInvariant()}.pdf",
                ExtractedText = "Senior Engineer, Example Ltd. Built Python services in production.",
            },
        });
        await db.SaveChangesAsync();

        return reference;
    }

    [Fact]
    public async Task Advancing_an_unscreened_candidate_throws_and_moves_nobody()
    {
        // A decision with no evidence is the thing this whole product argues against.
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = await ArrangeAsync(scope);
        var reference = await NewUnscreenedApplicantAsync(db, tenantId, "UNSCREENED");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.AdvanceCandidatesAsync([reference], "Looks strong"));

        Assert.Contains("no evidence", ex.Message);
        Assert.Contains("Nothing was advanced", ex.Message);

        db.ChangeTracker.Clear();
        var after = await db.Applicants.SingleAsync(a => a.Reference == reference);
        Assert.Equal(ApplicantStage.Applied, after.Stage);
        Assert.Empty(await db.Decisions.Where(d => d.Applicant!.Reference == reference).ToListAsync());
    }

    [Fact]
    public async Task An_unknown_reference_throws_before_anything_is_written()
    {
        // Atomicity: a partial advance would tell some people they progressed and leave the rest in
        // a state nobody chose.
        //
        // Its own applicant, for the same reason as the helper above: this test asserts a candidate
        // was NOT moved, and a sibling test advances APP-1001. Sharing one would make the result
        // depend on xUnit's ordering, which is exactly the flake this suite must not have.
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = await ArrangeAsync(scope);
        var reference = await NewUnscreenedApplicantAsync(db, tenantId, "ATOMIC");
        await ScreenAsync(tools, db, reference);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.AdvanceCandidatesAsync([reference, "APP-9999"], "Shortlisted"));

        Assert.Contains("APP-9999", ex.Message);
        Assert.Contains("Nothing was advanced", ex.Message);

        db.ChangeTracker.Clear();
        var untouched = await db.Applicants.SingleAsync(a => a.Reference == reference);
        Assert.Equal(ApplicantStage.Applied, untouched.Stage);
    }

    [Fact]
    public async Task A_missing_reason_throws()
    {
        // The reason is what a rejected candidate, an auditor or a tribunal later reads. An empty
        // one would make the audit trail technically complete and practically useless.
        var (scope, _, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, _) = await ArrangeAsync(scope);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.AdvanceCandidatesAsync(["APP-1001"], "   "));
    }

    [Fact]
    public async Task A_successful_advance_records_the_decision_and_the_evidence_it_rests_on()
    {
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = await ArrangeAsync(scope);
        await ScreenAsync(tools, db, "APP-1001");

        var screening = await db.ScreeningResults
            .SingleAsync(r => r.Applicant!.Reference == "APP-1001" && r.Status == ScreeningStatus.Proposed);

        var result = await tools.AdvanceCandidatesAsync(["APP-1001"], "Strongest Python evidence in the pile");

        Assert.Contains("Advanced 1 candidate", result);

        db.ChangeTracker.Clear();
        var decision = await db.Decisions.SingleAsync(d => d.Applicant!.Reference == "APP-1001");

        Assert.Equal(DecisionKind.Advance, decision.Kind);
        Assert.Equal(tenantId, decision.TenantId);
        Assert.Equal(ApplicantStage.Applied, decision.FromStage);
        Assert.Equal(ApplicantStage.Screening, decision.ToStage);
        Assert.Equal("Strongest Python evidence in the pile", decision.Reason);

        // The point of the entity: the decision points at the screening that justified it.
        Assert.Equal(screening.Id, decision.ScreeningResultId);

        var applicant = await db.Applicants.SingleAsync(a => a.Reference == "APP-1001");
        Assert.Equal(ApplicantStage.Screening, applicant.Stage);
    }

    [Fact]
    public async Task A_candidate_at_a_terminal_stage_cannot_be_advanced()
    {
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = await ArrangeAsync(scope);
        var reference = await NewUnscreenedApplicantAsync(db, tenantId, "TERMINAL");

        // Screen them FIRST, so the evidence check passes and terminality is genuinely the thing
        // being tested. Accepting "either message" would have made this test prove nothing about
        // terminal stages at all.
        await ScreenAsync(tools, db, reference);

        var applicant = await db.Applicants.SingleAsync(a => a.Reference == reference);
        applicant.Stage = ApplicantStage.Hired;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.AdvanceCandidatesAsync([reference], "Again"));

        Assert.Contains("final stage", ex.Message);
        Assert.Contains("Hired", ex.Message);
    }

    [Fact]
    public async Task The_advance_turn_parks_on_the_approval_gate()
    {
        // Through AdminClient so it goes through the real RBAC and approval pipeline.
        // AuthorizedScopeAsync bypasses both and could never prove this.
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(client, "hiring", "Advance APP-1001 to the next stage");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.Contains("advance_candidates", turn.ToolCalls);
        Assert.True(turn.RequiredApproval,
            $"advance_candidates did not park on the gate. CUSTOM events were [{string.Join(", ", turn.CustomEvents)}].");
    }

    [Fact]
    public async Task The_reply_does_not_claim_anyone_advanced_while_the_write_is_parked()
    {
        // Telling a hiring manager that twenty people were advanced when they were not is the
        // failure this product exists to prevent.
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(client, "hiring", "Advance APP-1001 to the next stage");

        foreach (var claim in new[] { "has been advanced", "have been advanced", "is now at", "successfully advanced" })
        {
            Assert.False(turn.Text.Contains(claim, StringComparison.OrdinalIgnoreCase),
                $"The reply claims \"{claim}\" while the write is parked. Reply was: {turn.Text}");
        }
    }

    [Fact]
    public async Task A_sourcer_cannot_reach_the_advance_tool_at_all()
    {
        // RBAC before the model. A sourcer screens and proposes; advancing is not theirs, and if
        // this ever passes the approval lane has become ceremony.
        using var client = fixture.AdminClient(roles: "hiring-sourcer", subject: "it-sourcer-adv");
        var turn = await AguiStream.PostAsync(client, "hiring", "Advance APP-1001 to the next stage");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.DoesNotContain("advance_candidates", turn.ToolCalls);
    }

    [Fact]
    public async Task A_recruiter_cannot_advance_either()
    {
        // SPEC.md §3: a recruiter may reject and schedule; advancing is the hiring manager's call.
        // The recruiter's grant is an enumerated allowlist precisely so this stays true as the
        // module gains tools.
        using var client = fixture.AdminClient(roles: "hiring-recruiter", subject: "it-recruiter-adv");
        var turn = await AguiStream.PostAsync(client, "hiring", "Advance APP-1001 to the next stage");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.DoesNotContain("advance_candidates", turn.ToolCalls);
    }

    [Fact]
    public async Task A_recruiter_cannot_approve_the_advance_they_are_forbidden_to_propose()
    {
        // The sibling above proves the PROPOSE path is closed: the runner never puts
        // advance_candidates in a recruiter's tool list. This proves the OTHER path to the same
        // write, and it is a different gate — approving is authorized on
        // Permissions.ManageApprovals ALONE (ApprovalEndpoints), and ApprovalExecutor re-invokes
        // the tool without ever reading tool.Permission. So a recruiter who may not advance
        // anyone can advance them by approving someone else's parked call.
        //
        // Issue #51. The platform request is plenipo#145; the guard this asserts is a shim.
        //
        // The pending approval is RECORDED DIRECTLY rather than driven through a model turn. The
        // defect is "an approved call re-executes without re-checking the approver", which needs
        // no model to reproduce — and a Mock-provider turn would make the reproduction depend on
        // what the mock chose to call rather than on the gate under test.
        var (scope, tenantId, userId) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = await ArrangeAsync(scope);

        // Its own applicant, for the same reason as the helper above: this asserts a candidate was
        // NOT moved, and a sibling test advances APP-1001.
        var reference = await NewUnscreenedApplicantAsync(db, tenantId, "APPROVE51");
        await ScreenAsync(tools, db, reference);

        var approvals = scope.ServiceProvider.GetRequiredService<IApprovalStore>();
        var approvalId = Guid.NewGuid();
        await approvals.RecordPendingAsync(new PendingApproval
        {
            Id = approvalId,
            TenantId = tenantId,
            UserId = userId,
            UserDisplay = "the talent lead, who may make this call",
            // No FK on this column (PendingApprovalConfiguration indexes it and nothing more), so a
            // synthetic conversation keeps the arrangement to the one thing being tested.
            ConversationId = Guid.NewGuid(),
            ModuleId = HiringModule.Id,
            ToolName = "advance_candidates",
            ArgumentsJson = JsonSerializer.Serialize(new
            {
                references = new[] { reference },
                reason = "Approved by someone whose tier is denied this decision",
            }),
        });

        // The recruiter holds Permissions.ManageApprovals and NOT
        // tools.hiring.advance_candidates — the exact asymmetry SPEC.md §3 describes.
        using var recruiter = fixture.AdminClient(roles: "hiring-recruiter", subject: "it-recruiter-51");
        var response = await recruiter.PostAsync($"/api/chat/approvals/{approvalId}/approve", content: null);
        var body = await response.Content.ReadAsStringAsync();

        db.ChangeTracker.Clear();
        var applicant = await db.Applicants.SingleAsync(a => a.Reference == reference);

        // The consequence, asserted before the status code: a real person's stage is the thing that
        // must not have moved. A refusal that still advanced them would be a passing test and a
        // shipped defect.
        Assert.Equal(ApplicantStage.Applied, applicant.Stage);
        Assert.Empty(await db.Decisions.Where(d => d.Applicant!.Reference == reference).ToListAsync());
        Assert.False(response.IsSuccessStatusCode,
            $"The approve succeeded, so a recruiter just made a decision their tier is denied. Body: {body}");

        // 422, not 403, and the difference is the whole reason plenipo#145 exists: the shim refuses
        // inside the tool, so the platform records "approved, but the tool threw" for what is
        // actually "this person may not approve this". Asserted rather than described, so the day
        // the platform gates it properly this test fails and is rewritten deliberately.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("advance_candidates", body, StringComparison.Ordinal);
        Assert.Contains("Nothing was written", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_talent_lead_can_approve_the_advance_their_tier_is_granted()
    {
        // The POSITIVE CONTROL for the guard the sibling above proves. Without this test the suite
        // observes RequirePermissionToWrite returning false exactly once and never returning true,
        // so "the guard refuses a recruiter" and "the guard refuses EVERY approver on this path"
        // are indistinguishable — and the second one breaks the product's primary workflow with
        // every check still green.
        //
        // It has to be hiring-talent-lead specifically, and NOT system_admin or
        // AuthorizedScopeAsync's client: both hold "*", which PermissionMatcher short-circuits
        // before it ever walks the dotted hierarchy, so either would re-test the wildcard the rest
        // of this suite already leans on and prove nothing about this guard.
        //
        // What it actually settles, and could not be settled by reading: hiring-talent-lead is
        // granted "tools.hiring.*" in Program.cs and NEVER the literal
        // "tools.hiring.advance_candidates" that RequirePermissionToWrite builds. Whether
        // ICurrentUser.HasPermission expands a prefix wildcard was asserted nowhere in this repo.
        // If it does not, this test fails and the shim is refusing everyone.
        var (scope, tenantId, userId) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var (tools, db) = await ArrangeAsync(scope);

        // Its own applicant: this one is asserted to have MOVED, so sharing a reference with a
        // test that asserts nobody moved would make both depend on xUnit's ordering.
        var reference = await NewUnscreenedApplicantAsync(db, tenantId, "ALLOW51");
        await ScreenAsync(tools, db, reference);

        var approvals = scope.ServiceProvider.GetRequiredService<IApprovalStore>();
        var approvalId = Guid.NewGuid();
        await approvals.RecordPendingAsync(new PendingApproval
        {
            Id = approvalId,
            TenantId = tenantId,
            UserId = userId,
            UserDisplay = "the talent lead, who may make this call",
            ConversationId = Guid.NewGuid(),
            ModuleId = HiringModule.Id,
            ToolName = "advance_candidates",
            ArgumentsJson = JsonSerializer.Serialize(new
            {
                references = new[] { reference },
                reason = "Strong evidence against every criterion in the approved rubric",
            }),
        });

        using var lead = fixture.AdminClient(roles: "hiring-talent-lead", subject: "it-talent-lead-51");
        var response = await lead.PostAsync($"/api/chat/approvals/{approvalId}/approve", content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode,
            $"A talent lead holding tools.hiring.* could not approve advance_candidates, so the "
          + $"#51 shim is refusing legitimate approvers and no candidate can be advanced at all. "
          + $"Status {(int)response.StatusCode}. Body: {body}");

        db.ChangeTracker.Clear();

        // The consequence, asserted as a real person actually moving rather than as a 200. A
        // success status with no stage change would be the approval lane become ceremony.
        var applicant = await db.Applicants.SingleAsync(a => a.Reference == reference);
        Assert.Equal(ApplicantStage.Screening, applicant.Stage);

        var decision = Assert.Single(
            await db.Decisions.Where(d => d.Applicant!.Reference == reference).ToListAsync());
        Assert.Equal(DecisionKind.Advance, decision.Kind);
        Assert.Equal(ApplicantStage.Applied, decision.FromStage);
        Assert.Equal(ApplicantStage.Screening, decision.ToStage);
    }
}
