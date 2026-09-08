using Plenipo.Testing;
using Xunit;

namespace Hireworthy.IntegrationTests;

/// <summary>
/// The real Hireworthy host on a throwaway Postgres: platform + module migrations run, the dev
/// tenant and seeded requisitions land, the job processor and hosted services start. Everything is
/// real except the AI provider (Mock) — the same keyless posture the Plenipo platform's own suite
/// uses, and what lets this run on a bare clone with no API key.
/// </summary>
/// <remarks>
/// The container, the factory, the dev-auth clients and the tenant helpers now come from
/// <see cref="PlenipoHostFixture{TProgram}"/> in the Plenipo.Testing kit (fleet testing contract,
/// docs/TESTING_CONTRACT.md), so this file is only what the kit cannot know: which module, which
/// tools, which roles. Everything the product's own tests call — <c>AdminClient</c>,
/// <c>AuthorizedScopeAsync</c>, <c>Factory</c> — keeps the same shape it had when this class owned
/// the container itself.
/// </remarks>
public sealed class IntegrationFixture : PlenipoHostFixture<Program>
{
    /// <summary>
    /// What the platform's invariant packs need to know about this product. Every value is read
    /// from the hiring manifest (src/Hireworthy.Hiring/HiringModule.cs) and the role baselines
    /// (src/Hireworthy.Host/Program.cs), never invented:
    /// <list type="bullet">
    /// <item><c>list_requisitions</c> — a read, ungated, audited.</item>
    /// <item><c>advance_candidates</c> — the load-bearing write, <c>RequiresApproval = true</c>.
    /// It is the tool hireworthy#51 was about.</item>
    /// <item><c>hiring-talent-lead</c> — the accountable owner: chat, <c>tools.hiring.*</c> AND
    /// <c>chat.approvals.manage</c>, so it can both ask for an advance and decide one.</item>
    /// <item><c>hiring-sourcer</c> — the narrow tier, deliberately holding neither
    /// <c>tools.hiring.advance_candidates</c> nor <c>chat.approvals.manage</c> (ADR-0004). That
    /// exclusion is what S01, S04a and the red-team pack lean on.</item>
    /// </list>
    /// </summary>
    public override ProductContract Contract { get; } = new(
        ModuleId: "hiring",
        ReadTool: "list_requisitions",
        WriteTool: "advance_candidates",
        ApproverRole: "hiring-talent-lead",
        NarrowRole: "hiring-sourcer",
        ReadEndpoints: ["/api/hiring/requisitions"]);

    /// <summary>
    /// pgvector, and pg17 to match the AppHost (src/Hireworthy.AppHost/AppHost.cs) — a product that
    /// runs on pg17 and tests on the kit's pg16 default is testing something it does not ship.
    /// </summary>
    protected override string PostgresImage => "pgvector/pgvector:pg17";
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<IntegrationFixture>;
