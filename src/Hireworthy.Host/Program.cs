using Hireworthy.Hiring;
using Plenipo.Application.Authorization;
using Plenipo.AspNetCore.Hosting;
using Plenipo.AspNetCore.Modules;

// Hireworthy — a thin product host on the Plenipo platform. Everything that would normally be
// "the backbone" (auth, tenancy, RBAC, approvals, audit, jobs, chat transports, documents, OCR,
// RAG, admin console) arrives with AddPlenipoPlatform(). This file is the seam list; all the real
// code lives in Hireworthy.Hiring.
//
// NOTE: the host API is AddPlenipoPlatform() / UsePlenipoPlatform(). The platform's own
// BUILDING_A_PRODUCT.md documents AddPlenipo() / UsePlenipo() — those do not exist.

var builder = WebApplication.CreateBuilder(args);

builder.AddPlenipoPlatform();
builder.AddPlenipoModule<HiringModule>();

// ── Role baselines ────────────────────────────────────────────────────────────────────────────
// These are the SHIPPED starting points; they are runtime-editable per tenant in the admin
// console. Roles narrow what RBAC allows — nothing here grants beyond it. See SPEC.md §6.

// THE LOAD-BEARING ROLE. A sourcer may screen a pile and propose a shortlist, and may approve
// NOTHING — deliberately excluded from chat.approvals.manage so they cannot clear their own gate.
// An enumerated allowlist, never a wildcard: a wildcard would silently hand the sourcer every
// future write tool the module gains, including advance_candidates and reject_candidate.
// If this exclusion is ever lost, the approval lane becomes ceremony and the product's central
// claim — that no candidate is advanced or rejected without an accountable human — becomes false.
builder.Services.AddPlenipoRole("hiring-sourcer",
[
    "chat.use", "chat.conversations.view", "files.read",
    "tools.documents.read_document", "tools.documents.list_documents",
    HiringModule.ViewHiring,
    "tools.hiring.list_requisitions",
    "tools.hiring.get_requisition",
    // Screening work: read a candidate's CV and record what it says. Both are reads or derived
    // writes that move nobody's stage — the sourcer still approves nothing (ADR-0004).
    "tools.hiring.get_applicant",
    "tools.hiring.parse_cv",
    // Screening is the sourcer's core job. It writes a PROPOSAL with cited evidence and moves
    // nobody's stage — the sourcer still approves nothing.
    "tools.hiring.screen_applicant",
]);

// Recruiters run the req: they may propose the rubric that every applicant is measured against,
// and they may approve it. They may not advance a candidate — that is the hiring manager's call.
builder.Services.AddPlenipoRole("hiring-recruiter",
[
    "chat.use", "chat.conversations.view", "files.read", "files.upload",
    "tools.documents.read_document", "tools.documents.list_documents",
    HiringModule.ViewHiring,
    "tools.hiring.list_requisitions",
    "tools.hiring.get_requisition",
    "tools.hiring.get_applicant",
    "tools.hiring.parse_cv",
    "tools.hiring.screen_applicant",
    "tools.hiring.propose_rubric",
    // SPEC.md §3: a recruiter MAY reject and schedule; advancing is the hiring manager's call.
    // That asymmetry is the whole point of the tier, so reject is granted here and advance is not.
    "tools.hiring.reject_candidate",
    Permissions.ManageApprovals,
]);

// The accountable owner for the whole hiring surface.
builder.Services.AddPlenipoRole("hiring-talent-lead",
[
    "chat.use", "chat.conversations.view", "files.read", "files.upload",
    "tools.documents.read_document", "tools.documents.list_documents",
    HiringModule.ViewHiring,
    HiringModule.ManageHiring,
    "tools.hiring.*",
    Permissions.ManageApprovals,
]);

// Compliance reads the impact report and the audit trail and NOTHING else — deliberately not
// tools.hiring.*, and with no candidate-record read at all. An unusual shape, and the one that
// makes the tier real rather than decorative: the person who audits the decisions must not be
// able to influence them.
builder.Services.AddPlenipoRole("hiring-compliance",
[
    "chat.use", "chat.conversations.view",
]);

var app = builder.Build();

// RunPlenipoPlatformAsync = UsePlenipoPlatform() + InitializePlenipoAsync() + RunAsync().
//
// Do NOT reduce this to `app.UsePlenipoPlatform(); app.Run();`. UsePlenipoPlatform only configures
// the pipeline; InitializePlenipoAsync is what applies the platform and audit migrations, then each
// module's migrations and seed data. Without it the app starts and serves 500s forever, because
// every request hits tables that were never created — the visible symptom is
// `42P01: relation "platform.background_jobs" does not exist` from the job processor, which looks
// like a job bug and is not one.
await app.RunPlenipoPlatformAsync();

// Required so WebApplicationFactory<Program> can host the app from the integration tests.
public partial class Program;
