using Hireworthy.Hiring;
using Hireworthy.Hiring.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Plenipo.Application.Authorization;
using Plenipo.Core.Identity;
using Plenipo.Core.Multitenancy;
using Plenipo.Modules.Sdk;
using Xunit;

namespace Hireworthy.Hiring.Tests;

/// <summary>
/// The module guard. These tests exist because every failure they catch is silent at runtime:
/// a tool registered in one place and not the other is simply never callable, a permission string
/// that disagrees between the two 403s even for <c>system_admin</c>, and a tenant-owned entity
/// without a query filter leaks across hiring organisations without erroring once — which in this
/// product means one employer seeing another employer's candidates.
/// </summary>
public sealed class ManifestGuardTests
{
    private static readonly HiringModule Module = new();

    private sealed class FixedTenantContext : ITenantContext
    {
        private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
        public Guid? TenantId => Tenant;
        public bool HasTenant => true;
        public Guid RequireTenantId() => Tenant;
    }

    /// <summary>
    /// Grants nothing, on purpose. This file inspects tool DECLARATIONS and never invokes one, so
    /// a stub that answered "yes" to every permission would quietly start passing the day one of
    /// these tests did call a tool. TODO(plenipo#145) — delete alongside the shim it exists for.
    /// </summary>
    private sealed class GrantsNothingCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public string? Subject => null;
        public string? DisplayName => null;
        public Guid? TenantId => null;
        public bool IsAuthenticated => false;
        public IReadOnlySet<string> Permissions => new HashSet<string>(StringComparer.Ordinal);
        public bool HasPermission(string permission) => false;
    }

    [Fact]
    public void Manifest_has_a_stable_non_empty_id()
    {
        Assert.False(string.IsNullOrWhiteSpace(Module.Manifest.Id));
        Assert.Equal("hiring", Module.Manifest.Id);
        Assert.Equal(HiringModule.Id, Module.Manifest.Id);
    }

    [Fact]
    public void Tab_ids_and_routes_are_unique()
    {
        var tabs = Module.Manifest.Tabs;

        Assert.Equal(tabs.Select(t => t.Id).Distinct().Count(), tabs.Count);
        Assert.Equal(tabs.Select(t => t.Route).Distinct().Count(), tabs.Count);
        Assert.All(tabs, t => Assert.False(string.IsNullOrWhiteSpace(t.Route)));
    }

    [Fact]
    public void Tabs_that_expose_data_declare_a_permission()
    {
        // A tab with a DataEndpoint returns rows; without a permission it would be readable by
        // anyone who can reach the shell. Here those rows are requisitions and, later, candidates.
        foreach (var tab in Module.Manifest.Tabs.Where(t => !string.IsNullOrWhiteSpace(t.DataEndpoint)))
        {
            Assert.False(string.IsNullOrWhiteSpace(tab.Permission));
        }
    }

    [Fact]
    public void Tool_names_are_unique_and_permissions_use_the_platform_helper()
    {
        var tools = Module.Manifest.Tools;

        Assert.Equal(tools.Select(t => t.Name).Distinct().Count(), tools.Count);

        foreach (var tool in tools)
        {
            Assert.Equal(Permissions.ForTool(HiringModule.Id, tool.Name), tool.Permission);
        }
    }

    [Fact]
    public void Every_descriptor_has_a_matching_executable_tool_with_the_same_permission()
    {
        var executable = BuildToolSourceTools();

        foreach (var descriptor in Module.Manifest.Tools)
        {
            var match = executable.SingleOrDefault(t => t.Name == descriptor.Name);

            Assert.True(match is not null,
                $"Tool '{descriptor.Name}' is in the manifest but has no ModuleTool — it is silently never callable.");
            Assert.Equal(descriptor.Permission, match!.Permission);
        }
    }

    [Fact]
    public void Every_executable_tool_is_declared_in_the_manifest()
    {
        var declared = Module.Manifest.Tools.Select(t => t.Name).ToHashSet();

        foreach (var tool in BuildToolSourceTools())
        {
            Assert.True(declared.Contains(tool.Name),
                $"Tool '{tool.Name}' is executable but absent from the manifest — it will not be offered to the model.");
        }
    }

    [Fact]
    public void Writes_are_approval_gated_in_both_places()
    {
        // The runner unions the two RequiresApproval flags, so setting one and reviewing only that
        // one hides a broken gate. Both must be true for every write.
        //
        // As epics 3+ land, advance_candidates and reject_candidate join this list. A write that
        // reaches this module ungated would let the assistant reject a job applicant with no
        // accountable human — the single failure this product exists to prevent.
        string[] writes = ["propose_rubric", "advance_candidates", "reject_candidate"];

        var executable = BuildToolSourceTools();

        foreach (var name in writes)
        {
            var descriptor = Module.Manifest.Tools.Single(t => t.Name == name);
            var tool = executable.Single(t => t.Name == name);

            Assert.True(descriptor.RequiresApproval, $"Manifest descriptor for '{name}' is not approval-gated.");
            Assert.True(tool.RequiresApproval, $"ModuleTool for '{name}' is not approval-gated.");
        }
    }

    [Fact]
    public void Every_approval_gated_tool_is_named_in_the_agent_instructions()
    {
        // Registration and instruction are separate edits, and forgetting the second one is silent:
        // the tool still works, the gate still fires, every other guard stays green — the model just
        // has no guidance about the most consequential thing it can do, including the instruction
        // never to report the write as done before a human approves it.
        //
        // This test exists because exactly that happened: advance_candidates was registered in #12
        // and its instruction edit silently failed to apply, and nothing caught it.
        var instructions = Module.Manifest.AgentInstructions ?? string.Empty;

        foreach (var tool in Module.Manifest.Tools.Where(t => t.RequiresApproval))
        {
            Assert.True(instructions.Contains(tool.Name, StringComparison.Ordinal),
                $"Approval-gated tool '{tool.Name}' is not mentioned in AgentInstructions. "
              + "A gated write the model has no instruction about is the one most likely to be "
              + "misreported to a user as already done.");
        }
    }

    [Fact]
    public void Every_tenant_owned_entity_declares_a_query_filter()
    {
        // The single highest-consequence mistake available in this codebase. PlatformDbContext
        // applies filters by reflection; a module context does not, so an entity added without a
        // filter is a silent cross-tenant leak. This test is what makes that unrepresentable.
        using var db = BuildDbContext();

        var tenantOwned = db.Model.GetEntityTypes()
            .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType))
            .ToList();

        Assert.NotEmpty(tenantOwned);

        foreach (var entity in tenantOwned)
        {
            Assert.True(entity.GetDeclaredQueryFilters().Count > 0,
                $"Entity '{entity.ClrType.Name}' is ITenantOwned but has no HasQueryFilter — cross-tenant leak.");
        }
    }

    [Fact]
    public void Agent_instructions_forbid_forecasting_and_protected_characteristics()
    {
        // Not decoration. The product's central claim is that it evidences and cites rather than
        // predicting job performance — the claim the category has been sued and criticised over —
        // and the module invariant is that assessment never touches appearance or voice. Both live
        // only in the manifest's AgentInstructions, so a well-meaning edit could delete them
        // silently. This test is what makes that edit fail the build.
        var instructions = Module.Manifest.AgentInstructions ?? string.Empty;

        Assert.Contains("never", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("performance", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("appearance", instructions, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ModuleTool> BuildToolSourceTools()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext, FixedTenantContext>();
        services.AddSingleton<ICurrentUser, GrantsNothingCurrentUser>();
        services.AddDbContext<HiringDbContext>(o => o.UseNpgsql("Host=localhost;Database=guard"));
        services.AddScoped<HiringTools>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        return new HiringToolSource().GetTools(scope.ServiceProvider);
    }

    private static HiringDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<HiringDbContext>()
            .UseNpgsql("Host=localhost;Database=guard")
            .Options;

        return new HiringDbContext(options, new FixedTenantContext());
    }
}
