using Xunit;

namespace Hireworthy.IntegrationTests;

/// <summary>
/// The approval gate, exercised through the real AG-UI pipeline.
/// </summary>
/// <remarks>
/// This is the product's central claim under test: the assistant may propose the yardstick every
/// applicant is measured against, and a human decides. These assertions go through
/// <c>AdminClient</c> and never <c>AuthorizedScopeAsync</c> — a scope-based call bypasses RBAC and
/// the approval gate entirely, so it would pass while the gate was broken.
/// </remarks>
[Collection("api")]
public sealed class ProposeRubricTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task Proposing_a_rubric_parks_on_the_approval_gate()
    {
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(
            client, "hiring", "Propose a rubric for REQ-142 from its job description");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.Contains("propose_rubric", turn.ToolCalls);
        Assert.True(turn.RequiredApproval,
            $"propose_rubric did not park on the gate. CUSTOM events were [{string.Join(", ", turn.CustomEvents)}].");
    }

    [Fact]
    public async Task The_reply_does_not_claim_the_rubric_is_usable_before_approval()
    {
        // The failure this guards is not a crash — it is the assistant telling a recruiter the
        // yardstick is in place when it is parked. Screening 180 people against criteria nobody
        // approved is exactly what this product exists to prevent.
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(
            client, "hiring", "Propose a rubric for REQ-142 from its job description");

        foreach (var claim in new[] { "is approved", "now approved", "ready to screen", "you can now screen" })
        {
            Assert.False(turn.Text.Contains(claim, StringComparison.OrdinalIgnoreCase),
                $"The reply claims \"{claim}\" while the write is parked. Reply was: {turn.Text}");
        }
    }

    [Fact]
    public async Task A_sourcer_cannot_reach_the_write_tool_at_all()
    {
        // RBAC before the model: tools are filtered by permission BEFORE the request is built, so
        // an unpermitted tool is never even offered. `hiring-sourcer` may screen and propose a
        // shortlist and may approve nothing — if this ever passes, the approval lane is ceremony.
        using var client = fixture.AdminClient(roles: "hiring-sourcer", subject: "it-sourcer");
        var turn = await AguiStream.PostAsync(
            client, "hiring", "Propose a rubric for REQ-142 from its job description");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.DoesNotContain("propose_rubric", turn.ToolCalls);
        Assert.False(turn.RequiredApproval);
    }

    [Fact]
    public async Task A_read_turn_does_not_park_on_the_gate()
    {
        // The dual of the gate test: if reads parked too, the gate would be noise and people would
        // learn to click through it.
        using var client = fixture.AdminClient();
        var turn = await AguiStream.PostAsync(client, "hiring", "List the requisitions");

        Assert.False(turn.Failed, $"RUN_ERROR: {turn.Error}");
        Assert.Contains("list_requisitions", turn.ToolCalls);
        Assert.False(turn.RequiredApproval);
    }
}
