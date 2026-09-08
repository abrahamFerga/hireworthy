using Hireworthy.Hiring;
using Hireworthy.Hiring.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    /// The applicant reserved for the platform's conformance kit, and for nothing else.
    /// </summary>
    /// <remarks>
    /// The kit's S03 releases the parked <c>advance_candidates</c> and requires it to EXECUTE, so
    /// the reference it names must be a candidate the tool will actually advance:
    /// <c>advance_candidates</c> refuses anyone nobody has screened ("Cannot advance … nobody has
    /// screened them, so there is no evidence behind the decision"). It is deliberately NOT a
    /// seeded applicant: <c>AdvanceTests</c> assert that <c>APP-1001</c> is still <c>Applied</c>
    /// after a refused turn, and tests in this collection share one database with no ordering
    /// guarantee — a kit run that moved APP-1001 would make those pass or fail by luck.
    /// </remarks>
    public const string KitApplicantReference = "APP-KIT01";

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
    /// <item><c>WritePrompt</c> — the Mock provider synthesises a required array parameter as
    /// <c>[]</c>, and <c>advance_candidates</c> refuses an empty list, so the kit could never
    /// release anything (hireworthy#66, plenipo#209). Since plenipo#210 a JSON object in the turn
    /// is taken verbatim for the declared parameters, so the prompt names the reserved applicant
    /// itself rather than hoping the Mock invents one.</item>
    /// </list>
    /// </summary>
    public override ProductContract Contract { get; } = new(
        ModuleId: "hiring",
        ReadTool: "list_requisitions",
        WriteTool: "advance_candidates",
        ApproverRole: "hiring-talent-lead",
        NarrowRole: "hiring-sourcer",
        ReadEndpoints: ["/api/hiring/requisitions"],
        WritePrompt: $"Please advance candidates for me, using a tool. {{\"references\":[\"{KitApplicantReference}\"]}}");

    /// <summary>
    /// pgvector, and pg17 to match the AppHost (src/Hireworthy.AppHost/AppHost.cs) — a product that
    /// runs on pg17 and tests on the kit's pg16 default is testing something it does not ship.
    /// </summary>
    protected override string PostgresImage => "pgvector/pgvector:pg17";

    /// <summary>
    /// Puts <see cref="KitApplicantReference"/> on file and screens them, so the write the kit
    /// releases has the evidence the product requires behind it.
    /// </summary>
    /// <remarks>
    /// Screening goes through the product's own tool — <see cref="HiringTools.ScreenApplicantAsync"/>,
    /// the same call <c>AdvanceTests</c> make — rather than writing a <c>ScreeningResult</c> row
    /// directly, so this arrangement stays honest if the evidence rule ever changes. It runs in an
    /// authorized scope (tenant, user and permissions populated) because it is arranging the world,
    /// not asserting anything about RBAC; the kit's own turns go through the real HTTP pipeline as
    /// the approver role, which is where the gate is proved.
    /// </remarks>
    protected override async Task OnHostStartedAsync()
    {
        var (scope, tenantId, _) = await AuthorizedScopeAsync();
        using (scope)
        {
            var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();
            if (await db.Applicants.AnyAsync(a => a.Reference == KitApplicantReference))
            {
                return;
            }

            // REQ-142 is the seeded requisition with an APPROVED rubric; screening refuses to score
            // against anything else (HiringTools.ScreenApplicantAsync).
            var requisitionId = await db.Requisitions
                .Where(r => r.Reference == "REQ-142")
                .Select(r => r.Id)
                .SingleAsync();

            const string cvText = "Senior Engineer, Example Ltd. Built Python services in production.";

            db.Applicants.Add(new Applicant
            {
                TenantId = tenantId,
                RequisitionId = requisitionId,
                Reference = KitApplicantReference,
                FullName = "Conformance Kit Candidate",
                Stage = ApplicantStage.Applied,
                Cv = new CvDocument
                {
                    TenantId = tenantId,
                    FileName = "kit.pdf",
                    ExtractedText = cvText,
                },
            });
            await db.SaveChangesAsync();

            var tools = ActivatorUtilities.CreateInstance<HiringTools>(scope.ServiceProvider);
            var quote = cvText[..40];
            var outcome = await tools.ScreenApplicantAsync(KitApplicantReference,
            [
                new ScoredCriterion("Production Python experience", 4, quote, 0, quote.Length, false, null),
            ]);

            // The tool reports refusals as text rather than throwing, and a silent refusal here
            // would surface as an unexplained S03 failure much later.
            if (!await db.ScreeningResults.AnyAsync(r =>
                    r.Applicant!.Reference == KitApplicantReference && r.Status == ScreeningStatus.Proposed))
            {
                throw new InvalidOperationException(
                    $"Could not screen {KitApplicantReference} for the conformance kit: {outcome}");
            }
        }
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<IntegrationFixture>;
