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
/// The Pipeline board's data endpoint, and the guard that the board cannot move anybody.
/// </summary>
/// <remarks>
/// <para>
/// The board is a <b>read</b> surface. Dragging a card is a proposal a human then makes through
/// <c>advance_candidates</c> or <c>reject_candidate</c>, both approval-gated; the drop itself writes
/// nothing. <see cref="The_board_offers_no_route_that_moves_a_candidate_between_stages"/> is the
/// test that keeps it that way, and it is not defensive tidiness — see its own comment for the
/// privilege escalation a stage-writing endpoint would open here.
/// </para>
/// </remarks>
[Collection("api")]
public sealed class PipelineTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task The_pipeline_tab_is_declared_and_is_custom_react()
    {
        // Same contract as the Candidate tab: no DataEndpoint is what tells the shell to look for a
        // registered React component instead of rendering a generic table. A board is columns of
        // draggable cards; no declarative table can express that.
        using var client = fixture.AdminClient();
        var modules = await client.GetFromJsonAsync<JsonElement>("/api/platform/modules");

        var hiring = modules.EnumerateArray().Single(m => m.GetProperty("id").GetString() == "hiring");
        var pipeline = hiring.GetProperty("tabs").EnumerateArray()
            .Single(t => t.GetProperty("id").GetString() == "pipeline");

        Assert.Equal("/hiring/pipeline", pipeline.GetProperty("route").GetString());
        Assert.Equal(JsonValueKind.Null, pipeline.GetProperty("dataEndpoint").ValueKind);

        // The tab ID is the key the React side registers against (defineModule). Renaming it
        // unmounts the component with no error anywhere.
        Assert.Equal("pipeline", pipeline.GetProperty("id").GetString());
    }

    [Fact]
    public async Task The_board_groups_applicants_into_stage_columns()
    {
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();

        var requisitionId = await db.Requisitions
            .Where(r => r.Reference == "REQ-142").Select(r => r.Id).SingleAsync();

        db.Applicants.AddRange(
            new Applicant
            {
                TenantId = tenantId,
                RequisitionId = requisitionId,
                Reference = "APP-BOARD1",
                FullName = "Board Applied",
                Stage = ApplicantStage.Applied,
            },
            new Applicant
            {
                TenantId = tenantId,
                RequisitionId = requisitionId,
                Reference = "APP-BOARD2",
                FullName = "Board Interview",
                Stage = ApplicantStage.Interview,
            });
        await db.SaveChangesAsync();

        using var client = fixture.AdminClient();
        var body = await client.GetFromJsonAsync<JsonElement>("/api/hiring/pipeline?requisition=REQ-142");

        Assert.Equal("REQ-142", body.GetProperty("requisition").GetString());

        var columns = body.GetProperty("columns").EnumerateArray().ToList();

        // Every stage gets a column, including the empty ones — an absent column reads as "no such
        // stage" rather than "nobody is here yet", and a board with holes in it is a board that
        // cannot be dragged onto.
        Assert.Equal(
            Enum.GetNames<ApplicantStage>(),
            columns.Select(c => c.GetProperty("stage").GetString()).ToArray());

        var applied = columns.Single(c => c.GetProperty("stage").GetString() == "Applied");
        var interview = columns.Single(c => c.GetProperty("stage").GetString() == "Interview");

        Assert.Contains("APP-BOARD1",
            applied.GetProperty("candidates").EnumerateArray()
                .Select(c => c.GetProperty("reference").GetString()));
        Assert.Contains("APP-BOARD2",
            interview.GetProperty("candidates").EnumerateArray()
                .Select(c => c.GetProperty("reference").GetString()));

        // A card carries the name a recruiter recognises, not just a reference.
        var card = applied.GetProperty("candidates").EnumerateArray()
            .Single(c => c.GetProperty("reference").GetString() == "APP-BOARD1");
        Assert.Equal("Board Applied", card.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task The_board_is_permission_gated()
    {
        // It lists candidates by name — personal data, and exactly what hiring-compliance is
        // deliberately denied so the person auditing decisions cannot influence them.
        using var client = fixture.AdminClient(roles: "hiring-compliance", subject: "it-compliance-board");
        var response = await client.GetAsync("/api/hiring/pipeline?requisition=REQ-142");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_requisition_is_404_not_an_empty_board()
    {
        using var client = fixture.AdminClient();
        var response = await client.GetAsync("/api/hiring/pipeline?requisition=REQ-NOPE");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_board_offers_no_route_that_moves_a_candidate_between_stages()
    {
        // THE GUARD, and the reason the drop is a proposal rather than a write.
        //
        // A stage-writing endpoint here would not merely "skip the approval gate" — it would open a
        // privilege escalation. The platform's ApprovalExecutor re-invokes an approved tool WITHOUT
        // re-checking that tool's permission, and hiring-recruiter deliberately holds
        // Permissions.ManageApprovals while NOT holding tools.hiring.advance_candidates (Program.cs
        // — "advancing is the hiring manager's call"). So any board route that either wrote a stage
        // directly, or queued a PendingApproval for advance_candidates, would hand the recruiter the
        // one decision the role model withholds from them.
        //
        // This test is written from the recruiter's own token for that reason.
        var (scope, tenantId, _) = await fixture.AuthorizedScopeAsync();
        using var _s = scope;
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();

        var requisitionId = await db.Requisitions
            .Where(r => r.Reference == "REQ-142").Select(r => r.Id).SingleAsync();

        db.Applicants.Add(new Applicant
        {
            TenantId = tenantId,
            RequisitionId = requisitionId,
            Reference = "APP-NOMOVE",
            FullName = "Immovable Candidate",
            Stage = ApplicantStage.Applied,
        });
        await db.SaveChangesAsync();

        using var client = fixture.AdminClient(roles: "hiring-recruiter", subject: "it-recruiter-board");

        // Every shape a board drop would plausibly reach for.
        var attempts = new (HttpMethod Method, string Url)[]
        {
            (HttpMethod.Post, "/api/hiring/pipeline"),
            (HttpMethod.Post, "/api/hiring/pipeline/moves"),
            (HttpMethod.Put, "/api/hiring/pipeline/APP-NOMOVE"),
            (HttpMethod.Patch, "/api/hiring/pipeline/APP-NOMOVE"),
            (HttpMethod.Put, "/api/hiring/candidates/APP-NOMOVE/stage"),
            (HttpMethod.Post, "/api/hiring/candidates/APP-NOMOVE/stage"),
            (HttpMethod.Patch, "/api/hiring/candidates/APP-NOMOVE"),
        };

        foreach (var (method, url) in attempts)
        {
            using var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(new { stage = "Interview", toStage = "Interview" }),
            };
            using var response = await client.SendAsync(request);

            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"{method} {url} answered {(int)response.StatusCode} — the board must expose no "
              + "route that changes a candidate's stage. Advancing and rejecting go through the "
              + "approval-gated tools, whose permissions the role model actually enforces.");
        }

        // And the candidate did not move.
        db.ChangeTracker.Clear();
        var stage = await db.Applicants
            .Where(a => a.Reference == "APP-NOMOVE").Select(a => a.Stage).SingleAsync();
        Assert.Equal(ApplicantStage.Applied, stage);
    }

    [Fact]
    public async Task list_applicants_is_registered_and_is_not_approval_gated()
    {
        // It reads. A read tool marked RequiresApproval would park every board refresh on a human,
        // which is how an approval queue stops being read and the gate stops meaning anything.
        using var client = fixture.AdminClient();
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/admin/security/catalog");

        var permissions = catalog.GetRawText();
        Assert.Contains("tools.hiring.list_applicants", permissions, StringComparison.Ordinal);
    }
}
