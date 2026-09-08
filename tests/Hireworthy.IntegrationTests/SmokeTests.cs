using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Hireworthy.IntegrationTests;

/// <summary>
/// Rung 3: the host actually boots and the module is actually loaded.
/// </summary>
/// <remarks>
/// Every assertion here failed at some point during scaffolding while <c>dotnet build</c> stayed
/// green — a module that never loads compiles perfectly, and a host that throws at startup compiles
/// perfectly too. These are the cheapest checks that would have caught it.
/// </remarks>
[Collection("api")]
public sealed class SmokeTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task Alive_returns_200_without_calling_the_model()
    {
        using var client = fixture.AdminClient();
        var response = await client.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_hiring_module_is_loaded_and_declares_both_tabs()
    {
        using var client = fixture.AdminClient();
        var modules = await client.GetFromJsonAsync<JsonElement>("/api/platform/modules");

        var hiring = modules.EnumerateArray()
            .SingleOrDefault(m => m.GetProperty("id").GetString() == "hiring");

        Assert.True(hiring.ValueKind is not JsonValueKind.Undefined,
            "The hiring module did not load. A module that never loads still compiles — this is the check that catches it.");

        var routes = hiring.GetProperty("tabs").EnumerateArray()
            .Select(t => t.GetProperty("route").GetString())
            .ToList();

        Assert.Contains("/hiring/chat", routes);
        Assert.Contains("/hiring/requisitions", routes);
    }

    [Fact]
    public async Task The_seeded_requisitions_are_readable_through_the_tab_endpoint()
    {
        using var client = fixture.AdminClient();
        var rows = await client.GetFromJsonAsync<JsonElement>("/api/hiring/requisitions");

        var references = rows.EnumerateArray()
            .Select(r => r.GetProperty("reference").GetString())
            .ToList();

        Assert.Contains("REQ-142", references);

        // Per-requisition rather than a blanket assertion, and deliberately tightened rather than
        // relaxed when the seed gained an approved rubric: REQ-142 has one so screening is
        // demonstrable on a fresh clone, and the other two do NOT — which is the state that proves
        // "no approved rubric" is still the default and nobody is scored by accident.
        string RubricOf(string reference) => rows.EnumerateArray()
            .Single(r => r.GetProperty("reference").GetString() == reference)
            .GetProperty("rubric").GetString()!;

        Assert.Equal("Approved", RubricOf("REQ-142"));
        Assert.Equal("None", RubricOf("REQ-150"));
        Assert.Equal("None", RubricOf("REQ-155"));
    }

    [Fact]
    public async Task The_requisitions_endpoint_is_permission_gated()
    {
        // A tab endpoint returns rows; without the permission it would be readable by anyone who
        // can reach the shell. `hiring-compliance` deliberately holds no candidate-record read.
        using var client = fixture.AdminClient(roles: "hiring-compliance", subject: "it-compliance");
        var response = await client.GetAsync("/api/hiring/requisitions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_security_catalog_agrees_with_the_manifest_on_every_tool()
    {
        // GET /api/admin/security/catalog is the platform's own view of both registration sites.
        // A tool declared in the manifest but not the tool source is silently never callable, and
        // a permission string that disagrees 403s even for system_admin.
        using var client = fixture.AdminClient();
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/admin/security/catalog");

        var hiring = catalog.GetProperty("modules").EnumerateArray()
            .Single(m => m.GetProperty("id").GetString() == "hiring");

        var tools = hiring.GetProperty("tools").EnumerateArray()
            .ToDictionary(t => t.GetProperty("permission").GetString()!, t => t);

        Assert.Equal(9, tools.Count);
        Assert.True(tools.ContainsKey("tools.hiring.list_requisitions"));
        Assert.True(tools.ContainsKey("tools.hiring.get_requisition"));
        Assert.True(tools.ContainsKey("tools.hiring.list_applicants"));
        Assert.True(tools.ContainsKey("tools.hiring.get_applicant"));
        Assert.True(tools.ContainsKey("tools.hiring.parse_cv"));
        Assert.True(tools.ContainsKey("tools.hiring.screen_applicant"));
        Assert.True(tools.ContainsKey("tools.hiring.advance_candidates"));
        Assert.True(tools.ContainsKey("tools.hiring.reject_candidate"));

        // The gate, asserted where the platform reports it rather than where we declared it.
        Assert.True(tools["tools.hiring.propose_rubric"].GetProperty("requiresApproval").GetBoolean(),
            "propose_rubric is not approval-gated. Nobody may be scored against criteria a human has not accepted.");
        Assert.False(tools["tools.hiring.list_requisitions"].GetProperty("requiresApproval").GetBoolean());

        // parse_cv is ungated by argued exception (ADR-0004), not by oversight: it writes derived
        // data recomputable from the CV and moves nobody's stage. If a future change makes it
        // consequential, this assertion is the one that must be revisited first.
        Assert.False(tools["tools.hiring.parse_cv"].GetProperty("requiresApproval").GetBoolean());
        Assert.False(tools["tools.hiring.screen_applicant"].GetProperty("requiresApproval").GetBoolean());

        // The most consequential write in the product. A real person is told they progressed and it
        // cannot be un-told, so this flag is the one that must never quietly become false.
        Assert.True(tools["tools.hiring.advance_candidates"].GetProperty("requiresApproval").GetBoolean(),
            "advance_candidates is not approval-gated. No candidate may be advanced without an accountable human.");

        // The decision this product is ultimately judged on.
        Assert.True(tools["tools.hiring.reject_candidate"].GetProperty("requiresApproval").GetBoolean(),
            "reject_candidate is not approval-gated. An autonomous rejection is an adverse employment decision made by a machine.");

        // Everything the module does is audited: in this product the audit trail is a deliverable
        // a bias auditor asks for, not merely a safety net.
        Assert.All(tools.Values, t => Assert.True(t.GetProperty("audited").GetBoolean()));
    }
}
