using Hireworthy.Hiring.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plenipo.Application.Authorization;
using Plenipo.Core.Multitenancy;
using Plenipo.Modules.Sdk;

namespace Hireworthy.Hiring;

/// <summary>
/// Hireworthy's hiring module — evidence-first screening and interviewing.
/// </summary>
/// <remarks>
/// Epic 1 — requisitions and the approved rubric — is the walking skeleton: the module loads, a
/// read tool answers a real domain question, a write tool parks on the approval gate, and a tab
/// renders the requisitions. CV intake, cited screening, the shortlist decision, the candidate
/// consent surface, the adaptive interview and the impact report are epics 2–7 and are
/// deliberately absent. See PLAN.md.
/// </remarks>
public sealed class HiringModule : IModule
{
    /// <summary>Stable module id — used in the AG-UI route, permission strings and the manifest.</summary>
    public const string Id = "hiring";

    /// <summary>Permission required to view the module's tabs.</summary>
    public const string ViewHiring = "hiring.view";

    /// <summary>Permission required to administer requisitions.</summary>
    public const string ManageHiring = "hiring.manage";

    public ModuleManifest Manifest { get; } = new()
    {
        Id = Id,
        DisplayName = "Hiring",
        Version = "0.1.0",
        Description =
            "Requisitions, evaluation rubrics and evidenced screening — with a named human "
          + "approving before any candidate is advanced or rejected.",
        Icon = "users",
        AgentInstructions =
            "You are a careful hiring assistant. Use list_requisitions and get_requisition to read "
          + "the requisition and its job description before answering. Use propose_rubric to "
          + "propose the criteria applicants will be measured against; derive every criterion from "
          + "the job description text and make each one checkable against a CV. That proposal "
          + "REQUIRES a human approval, so never state that a rubric is in place, approved, or "
          + "usable for screening before the approval has been granted — say you have proposed it "
          + "and it is awaiting review. "
          + "Use get_applicant to read a candidate's CV, then parse_cv to record the employment "
          + "history, skills and education you read from it. Give dates as yyyy-MM and leave the end "
          + "date empty for a current role; the system works out the employment gaps itself, so do "
          + "not assert gaps yourself. Flag anything you cannot resolve rather than guessing it. "
          + "Use screen_applicant to score a candidate against the requisition's APPROVED rubric. "
          + "Every criterion must quote the span of the CV that evidences it, word for word, with the "
          + "exact character offsets — a paraphrase is rejected, and so is a quotation that is not "
          + "really there. If the CV does not evidence a criterion, mark it unresolved; never score "
          + "it from memory or from what the candidate probably meant. "
          + "Use advance_candidates to move candidates forward, and reject_candidate to turn one "
          + "down. BOTH require a human approval, so never tell anyone a candidate has been "
          + "advanced or rejected before that approval is granted — say you have proposed it and it "
          + "is awaiting review. Neither works on a candidate nobody has screened: there would be "
          + "no evidence behind the decision. reject_candidate takes one candidate at a time on "
          + "purpose, because a rejection is a judgement about a person and the decision they may "
          + "challenge; give the reason in terms of the approved rubric. "
          + "Never predict how well a candidate would perform in the job: this system evidences and "
          + "cites, it does not forecast job performance. "
          + "Never draw an inference from a candidate's name, photograph, age, gender, nationality, "
          + "or any proxy for them, and never assess appearance, accent, or tone of voice — "
          + "assessment is on what a candidate wrote or said, measured against the approved rubric.",
        SuggestedPrompts =
        [
            "Which requisitions still need a rubric?",
            "Show me REQ-142",
            "Propose a rubric for REQ-142 from its job description",
            "Show me applicant APP-1001",
            "Parse the CV for APP-1001",
            "Screen applicant APP-1001 against the approved rubric",
            "Advance APP-1001 to the next stage",
            "Reject APP-1002",
        ],
        Roles = ["hiring-sourcer", "hiring-recruiter", "hiring-talent-lead", "hiring-compliance"],
        Tools =
        [
            new ToolDescriptor
            {
                Name = "list_requisitions",
                Description =
                    "List the organisation's requisitions with their status and whether a rubric has been approved.",
                Permission = Permissions.ForTool(Id, "list_requisitions"),
            },
            new ToolDescriptor
            {
                Name = "get_requisition",
                Description =
                    "Get one requisition in detail by its reference, e.g. 'REQ-142', including its job description and approved rubric.",
                Permission = Permissions.ForTool(Id, "get_requisition"),
            },
            new ToolDescriptor
            {
                Name = "get_applicant",
                Description =
                    "Get one applicant by reference, e.g. 'APP-1001', including their CV text and anything already extracted from it.",
                Permission = Permissions.ForTool(Id, "get_applicant"),
            },
            new ToolDescriptor
            {
                Name = "parse_cv",
                Description =
                    "Record the employment history, skills and education read from an applicant's CV. "
                  + "Dates are yyyy-MM; leave the end date empty for a current role.",
                Permission = Permissions.ForTool(Id, "parse_cv"),
                // Deliberately ungated — ADR-0004. Derived, recomputable, and it moves no stage.
            },
            new ToolDescriptor
            {
                Name = "screen_applicant",
                Description =
                    "Score one applicant against their requisition's approved rubric. Every criterion must "
                  + "quote the span of the CV that evidences it, verbatim, or be marked unresolved.",
                Permission = Permissions.ForTool(Id, "screen_applicant"),
                // Ungated — ADR-0004. A proposal; nothing advances until a human approves.
            },
            new ToolDescriptor
            {
                Name = "advance_candidates",
                Description =
                    "Advance named candidates to the next stage. Requires human approval, and every "
                  + "candidate must already have been screened.",
                Permission = Permissions.ForTool(Id, "advance_candidates"),
                RequiresApproval = true,
            },
            new ToolDescriptor
            {
                Name = "reject_candidate",
                Description =
                    "Reject one candidate, with a recorded reason. Requires human approval, and the "
                  + "candidate must already have been screened.",
                Permission = Permissions.ForTool(Id, "reject_candidate"),
                RequiresApproval = true,
            },
            new ToolDescriptor
            {
                Name = "propose_rubric",
                Description =
                    "Propose the evaluation criteria for a requisition, derived from its job description. "
                  + "The rubric is not usable for screening until a human approves it.",
                Permission = Permissions.ForTool(Id, "propose_rubric"),
                // The gate is the union of this flag and the ModuleTool's — set BOTH and keep them
                // in sync, or a review of one will mislead you about whether the write is gated.
                RequiresApproval = true,
            },
        ],
        Tabs =
        [
            new TabDescriptor
            {
                Id = "chat",
                Label = "Chat",
                Route = "/hiring/chat",
                Icon = "message-circle",
                Order = 0,
            },
            new TabDescriptor
            {
                Id = "requisitions",
                Label = "Requisitions",
                Route = "/hiring/requisitions",
                Icon = "briefcase",
                Order = 1,
                Permission = ViewHiring,
                DataEndpoint = "/api/hiring/requisitions",
                Columns =
                [
                    new("reference", "Ref"),
                    new("title", "Role"),
                    new("status", "Status"),
                    new("rubric", "Rubric"),
                    new("hiringManager", "Hiring manager"),
                ],
            },
        ],
    };

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HiringDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("plenipo-platform"),
                npgsql => npgsql.MigrationsHistoryTable(
                    HiringDbContext.MigrationsHistoryTable,
                    HiringDbContext.Schema)));

        services.AddScoped<HiringTools>();

        // IModuleToolSource MUST be a singleton: the platform's IToolRegistry is a singleton and
        // consumes every registered source, so a scoped registration fails DI validation at
        // startup with "Cannot consume scoped service IModuleToolSource from singleton
        // IToolRegistry" — and takes six other platform services down with it.
        // This is why IModuleToolSource.GetTools receives the scoped IServiceProvider as a
        // PARAMETER instead of injecting it: the source is a singleton that resolves the scoped
        // HiringTools (and its DbContext) per call.
        services.AddSingleton<IModuleToolSource, HiringToolSource>();
    }

    /// <summary>
    /// Creates the module's own schema. <b>The platform cannot do this for you.</b>
    /// </summary>
    /// <remarks>
    /// <c>InitializePlenipoAsync</c> migrates the platform and audit databases and then calls each
    /// module — it migrates *itself*, it cannot invent a module's DDL. Because
    /// <see cref="IModule.MigrateAsync"/> is a defaulted interface member, leaving it unimplemented
    /// compiles, passes every manifest guard, and boots; the only symptom is
    /// <c>42P01: relation "hiring.requisitions" does not exist</c> on the first real request.
    /// </remarks>
    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // A scope of our own: this runs at startup, outside any request, and the context is scoped.
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HiringDbContext>();

        // MigrateAsync, never EnsureCreatedAsync. The platform's initializer has already created
        // this database, so EnsureCreatedAsync would find it present, return false, and create no
        // tables at all — the silent version of exactly the bug this method fixes.
        await db.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds a starter set of requisitions into the scope's tenant — in Development only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The absence of a tenant is the production safety gate, and it is the platform's, not
    /// ours.</b> <c>SeedPlenipoModulesAsync</c> calls this method unconditionally, but it first
    /// calls <c>EstablishDevTenantContextAsync</c> on this very scope — and *that* is
    /// <c>IsDevelopment</c>-gated. So outside Development no tenant is ever established and this
    /// method returns having done nothing.
    /// </para>
    /// <para>
    /// Do not "improve" this by opening a new scope and looking the dev tenant up by slug. That
    /// discards the context the platform just prepared, re-implements a seam that has already run,
    /// and — because nothing reserves the slug <c>dev</c> — would write fabricated requisitions into
    /// any production tenant an operator happened to name that way. Fabricated hiring records are a
    /// worse failure here than in most domains: a requisition implies a real role and real
    /// applicants. A real tenant starts empty, which is the correct empty state.
    /// </para>
    /// <para>
    /// Idempotent across sequential boots: it does nothing once the tenant has any requisition. It
    /// is NOT safe against two hosts seeding the same fresh database concurrently — the unique index
    /// on (TenantId, Reference) is what makes that race fail loudly instead of duplicating.
    /// </para>
    /// </remarks>
    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // The platform's scope, used as handed over. No tenant means production — nothing to seed,
        // and that is success, not an error.
        var tenant = services.GetRequiredService<ITenantContext>();

        if (!tenant.HasTenant)
        {
            return;
        }

        var db = services.GetRequiredService<HiringDbContext>();
        var tenantId = tenant.RequireTenantId();

        // Each kind is guarded SEPARATELY, and that is not fussiness. A single
        // `if (Requisitions.Any()) return;` short-circuits the whole method on a dev volume that
        // already has requisitions from an earlier version — so a developer who has run the product
        // before gets the new tables and none of the new data, and the new tool looks broken for
        // them and fine for everyone else. Testcontainers starts fresh every run, so no test can
        // catch that; only booting against an existing volume does.
        //
        // No IgnoreQueryFilters: the tenant is established, so these counts are scoped to exactly
        // the tenant being seeded — which is the check we want.
        if (!await db.Requisitions.AnyAsync(cancellationToken))
        {
            db.Requisitions.AddRange(StarterRequisitions(tenantId));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Applicants.AnyAsync(cancellationToken))
        {
            var backend = await db.Requisitions
                .FirstOrDefaultAsync(r => r.Reference == "REQ-142", cancellationToken);

            if (backend is not null)
            {
                db.Applicants.AddRange(StarterApplicants(tenantId, backend.Id));
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (!await db.Rubrics.AnyAsync(cancellationToken))
        {
            var backend = await db.Requisitions
                .FirstOrDefaultAsync(r => r.Reference == "REQ-142", cancellationToken);

            if (backend is not null)
            {
                db.Rubrics.Add(StarterRubric(tenantId, backend.Id));
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// An already-approved rubric on REQ-142, so screening is demonstrable on a fresh clone.
    /// </summary>
    /// <remarks>
    /// <b>Seeding this as Approved does not weaken the approval gate</b>, and the distinction is
    /// worth stating because it looks like it might. The gate lives on <c>propose_rubric</c>, which
    /// is what the assistant can reach; this is dev fixture data standing in for "a recruiter
    /// already approved these criteria last week", exactly as the seeded requisitions stand in for
    /// reqs somebody already opened. Nothing in the module can move a rubric to Approved — that
    /// still requires a human clearing the parked call.
    /// <para>
    /// The criteria are derived from REQ-142's own job description text, and each is checkable
    /// against a CV, because a criterion that cannot be evidenced is one the screener can only
    /// guess at.
    /// </para>
    /// </remarks>
    private static Rubric StarterRubric(Guid tenantId, Guid requisitionId) => new()
    {
        TenantId = tenantId,
        RequisitionId = requisitionId,
        Version = 1,
        Status = RubricStatus.Approved,
        ApprovedBy = "Priya Raman",
        ApprovedAt = new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
        Rationale =
            "Derived from REQ-142. The job description leads on shipping and operating production "
          + "Python, on-call ownership, and written communication across time zones; payments domain "
          + "knowledge is explicitly 'a strong plus but not required', so it is weighted low.",
        Criteria =
        [
            new RubricCriterion
            {
                TenantId = tenantId,
                Name = "Production Python experience",
                Requirement = "Has shipped and operated Python services in production for several years.",
                Weight = 5,
                Ordinal = 0,
            },
            new RubricCriterion
            {
                TenantId = tenantId,
                Name = "On-call ownership",
                Requirement = "Has been on call for systems they built, not only for systems they inherited.",
                Weight = 4,
                Ordinal = 1,
            },
            new RubricCriterion
            {
                TenantId = tenantId,
                Name = "Written communication",
                Requirement = "Evidence of design documents or written decision-making, not just verbal collaboration.",
                Weight = 4,
                Ordinal = 2,
            },
            new RubricCriterion
            {
                TenantId = tenantId,
                Name = "Mentoring",
                Requirement = "Has mentored junior engineers; the role carries two.",
                Weight = 3,
                Ordinal = 3,
            },
            new RubricCriterion
            {
                TenantId = tenantId,
                Name = "Payments or ledger domain",
                Requirement = "Payment rails, ledger design or financial reconciliation. A plus, not a requirement.",
                Weight = 2,
                Ordinal = 4,
            },
        ],
    };

    /// <summary>
    /// Two applicants on REQ-142, each with a CV as text.
    /// </summary>
    /// <remarks>
    /// The CVs state their dates <b>inconsistently on purpose</b> — "Jan 2019", "2019 – present",
    /// "03/2020" — because resolving that inconsistency is the extraction task. A pre-normalised
    /// seed would make <c>parse_cv</c> look like it works when it had nothing to reason over.
    /// APP-1001 carries a real ten-month employment gap; APP-1002 does not, so the gap inference
    /// has both a positive and a negative case on a fresh clone.
    /// </remarks>
    private static IEnumerable<Applicant> StarterApplicants(Guid tenantId, Guid requisitionId) =>
    [
        new()
        {
            TenantId = tenantId,
            RequisitionId = requisitionId,
            Reference = "APP-1001",
            FullName = "Maya Osei",
            Email = "maya.osei@example.com",
            Stage = ApplicantStage.Applied,
            Source = "Job board",
            Cv = new CvDocument
            {
                TenantId = tenantId,
                FileName = "maya-osei-cv.pdf",
                OcrUsed = false,
                ExtractedText =
                    "MAYA OSEI — Backend Engineer\n"
                  + "maya.osei@example.com\n\n"
                  + "EXPERIENCE\n"
                  + "Staff Engineer, Kestrel Payments — Jan 2021 to present\n"
                  + "  Own the settlement service. Rewrote the reconciliation pipeline in Python; "
                  + "cut end-of-day breaks from ~40 a night to under 5. On call one week in four.\n"
                  + "  Mentored two juniors through their first on-call rotations.\n\n"
                  + "Backend Engineer, Northwind Retail — 03/2017 – 02/2020\n"
                  + "  Built and ran order-management services in Python and some Go. Took the "
                  + "checkout service through two Black Fridays.\n\n"
                  + "Junior Developer, Halloway Systems — September 2015 until February 2017\n"
                  + "  Internal tooling, mostly Python.\n\n"
                  + "EDUCATION\n"
                  + "BSc Computer Science, University of Manchester, 2015\n\n"
                  + "SKILLS\n"
                  + "Python, PostgreSQL, Go, Kubernetes, design documents, incident response",
            },
        },
        new()
        {
            TenantId = tenantId,
            RequisitionId = requisitionId,
            Reference = "APP-1002",
            FullName = "Daniel Brandt",
            Email = "d.brandt@example.com",
            Stage = ApplicantStage.Applied,
            Source = "Referral",
            Cv = new CvDocument
            {
                TenantId = tenantId,
                FileName = "daniel-brandt-cv.pdf",
                OcrUsed = true,
                ExtractedText =
                    "Daniel Brandt\n"
                  + "d.brandt@example.com\n\n"
                  + "Work history\n"
                  + "Senior Software Engineer at Vantage Logistics, 2022 - Current\n"
                  + "  Python services for freight tracking. Wrote the team's testing guidelines.\n"
                  + "Software Engineer, Vantage Logistics, June 2019 to December 2021\n"
                  + "  Same team, promoted internally.\n"
                  + "Analyst, Meridian Consulting, 2018 — 2019\n"
                  + "  Data analysis in Python and SQL. Not an engineering role.\n\n"
                  + "Education: MSc Data Science, TU Delft, 2018. BSc Mathematics, TU Delft, 2016.\n"
                  + "Skills: Python, SQL, Django, AWS, pandas",
            },
        },
    ];

    /// <summary>
    /// Three requisitions with real job-description text, in mixed states.
    /// </summary>
    /// <remarks>
    /// The job descriptions are deliberately full prose rather than bullet lists: proposing a rubric
    /// from them is the epic-1 reasoning task, and a pre-structured list would make the tool look
    /// like it works when it had nothing to extract. States vary so the manifest's own suggested
    /// prompts — "Which requisitions still need a rubric?" and "Show me REQ-142" — both return a
    /// real answer on a fresh clone.
    /// </remarks>
    private static IEnumerable<Requisition> StarterRequisitions(Guid tenantId) =>
    [
        new()
        {
            TenantId = tenantId,
            Reference = "REQ-142",
            Title = "Senior Backend Engineer",
            Status = RequisitionStatus.Open,
            Location = "Remote (EU)",
            HiringManager = "Priya Raman",
            JobDescription =
                "We are looking for a senior backend engineer to join the payments platform team. "
              + "You will own services that move real money, so we care a great deal about people "
              + "who write tests before they write fixes. Most of our stack is Python and Postgres, "
              + "with some Go at the edges; you should have shipped and operated production Python "
              + "services for several years and be comfortable being on call for what you build. "
              + "We work asynchronously across four time zones, so clear written communication "
              + "matters more here than it does in most teams — design documents are how decisions "
              + "get made. Experience with payment rails, ledger design, or financial reconciliation "
              + "is a strong plus but not required; we have hired people who learned it here. You "
              + "will mentor two junior engineers.",
        },
        new()
        {
            TenantId = tenantId,
            Reference = "REQ-150",
            Title = "Compliance Analyst",
            Status = RequisitionStatus.Open,
            Location = "London, hybrid",
            HiringManager = "Tom Ashworth",
            JobDescription =
                "Our compliance function is growing and we need an analyst who can turn regulation "
              + "into something engineers can act on. Day to day you will read incoming regulatory "
              + "updates, work out what actually applies to us, and translate that into control "
              + "requirements with the teams who will implement them. You will maintain the evidence "
              + "pack we hand auditors each year. We would expect a couple of years in a compliance, "
              + "audit, or risk role, ideally somewhere regulated — financial services, healthcare, "
              + "or similar. Familiarity with ISO 27001 or SOC 2 would help. Above all we need "
              + "someone who is comfortable saying 'I don't know yet, I will find out' and then "
              + "finding out.",
        },
        new()
        {
            TenantId = tenantId,
            Reference = "REQ-155",
            Title = "Technical Recruiter",
            Status = RequisitionStatus.Draft,
            Location = "Remote (EU)",
            HiringManager = "Priya Raman",
            JobDescription =
                "We are hiring a recruiter to own technical hiring end to end. You will partner with "
              + "engineering managers to work out what a role actually needs, run structured "
              + "interview loops, and keep candidates informed at every stage — we treat a slow "
              + "rejection as a failure. You should have run technical requisitions before and be "
              + "able to hold your own in a conversation about what a backend engineer does. "
              + "Experience designing structured interview scorecards is exactly what we want.",
        },
    ];

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Backs the Requisitions tab's server-driven table: the platform renders the JSON array as
        // a grid using the tab's Columns, so the register needs no custom UI.
        var group = endpoints.MapGroup("/api/hiring").WithTags("Hiring").RequireAuthorization();

        group.MapGet("/requisitions", async (HiringDbContext db, CancellationToken ct) =>
            {
                var rows = await db.Requisitions
                    .OrderBy(r => r.Reference)
                    .Select(r => new
                    {
                        reference = r.Reference,
                        title = r.Title,
                        status = r.Status.ToString(),
                        rubric =
                            r.Rubrics.Any(x => x.Status == RubricStatus.Approved) ? "Approved"
                            : r.Rubrics.Any(x => x.Status == RubricStatus.Proposed) ? "Awaiting approval"
                            : "None",
                        hiringManager = r.HiringManager,
                    })
                    .ToListAsync(ct);

                return Results.Ok(rows);
            })
            .RequireAuthorization(PermissionRequirement.PolicyName(ViewHiring))
            .WithName("Hiring_ListRequisitions");
    }
}
