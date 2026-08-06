# Hireworthy — architecture

**Module:** `hiring` · **Platform pin:** `0.1.0-alpha.28` (vendored, `Directory.Build.props`)
**Level: L2** — rule checks over a document. Nothing here ran. The L1 proof arrives per issue in
`/deliver:work-next-issue`; epic 1's has already landed (see §13).

## 1. Delta statement

The Plenipo platform supplies auth, multi-tenancy, RBAC-before-the-model, the approval lane, the
append-only audit database, budgets, the job processor, chat over AG-UI and SignalR, the document
store with OCR, the RAG pipeline, connectors, notification channels and the admin console.
**Hireworthy adds one domain module**: requisitions, versioned evaluation rubrics, applicants, CV
extraction, citation-grounded screening, consented structured interviews, and the approval-gated
advance/reject decision — plus two things the platform has no opinion about and this domain
requires: **citation grounding enforced as a deterministic check**, and **adverse-impact
arithmetic**.

Everything in `plenipo-platform`'s *already decided* table is used as shipped and is not re-argued
here.

## 2. Module boundary

One module, `Hireworthy.Hiring`, id `hiring`. Screening and interviewing share the rubric, the
applicant and the decision record — foreign keys everywhere between them, one audience, one release
cadence. A second module would mean a second `DbContext` needing its own per-entity
`HasQueryFilter`, multiplying the highest-consequence mistake available here for no domain benefit.
(PLAN.md §3 answers OQ8; not re-litigated.)

## 3. Tool surface

| Tool | Description the **model** routes on | Permission | Approval | Risk tier | Why gated |
|---|---|---|---|---|---|
| `list_requisitions` | List requisitions with status and whether a rubric is approved | `tools.hiring.list_requisitions` | no | read | — |
| `get_requisition` | One requisition in detail, with its JD and approved rubric | `tools.hiring.get_requisition` | no | read | — |
| `propose_rubric` | Propose evaluation criteria derived from the job description | `tools.hiring.propose_rubric` | **yes** | propose | The rubric is the yardstick 180 people are measured against. A wrong one silently biases every downstream score |
| `parse_cv` | Extract employment history, skills, education from a CV | `tools.hiring.parse_cv` | no | write-reversible | Derived data, recomputable from the source document (ADR-0004) |
| `list_applicants` | Applicants on a requisition, filtered by stage or score | `tools.hiring.list_applicants` | no | read | — |
| `get_applicant` | One applicant: CV, profile, scores, stage | `tools.hiring.get_applicant` | no | read | — |
| `screen_applicants` | Score every applicant against the approved rubric, citing the CV span behind each criterion | `tools.hiring.screen_applicants` | no | propose | Writes **proposals only**; nothing advances. ADR-0004 |
| `advance_candidates` | Move named candidates to the next stage | `tools.hiring.advance_candidates` | **yes** | write-irreversible | A person is told they progressed. Un-telling them is not possible |
| `reject_candidate` | Reject a candidate with a recorded reason | `tools.hiring.reject_candidate` | **yes** | write-irreversible | **The single most consequential write in the product.** An autonomous rejection is an adverse employment decision made by a machine |
| `send_interview_invite` | Send a one-time interview link with the AI-use disclosure | `tools.hiring.send_interview_invite` | **yes** | **external-effect** | Leaves the system: a real person receives a real message, and it starts the IL AIVIA disclosure clock |
| `get_consent_status` | Which candidates have consented to AI analysis | `tools.hiring.get_consent_status` | no | read | — |
| `evaluate_transcript` | Score a finished interview transcript against the rubric, quoting answers | `tools.hiring.evaluate_transcript` | no | propose | Proposal only; the advance that follows is gated |
| `explain_score` | Why one candidate ranks above another, citing both CVs | `tools.hiring.explain_score` | no | read | — |
| `get_impact_report` | Selection rates by category against the four-fifths rule | `tools.hiring.get_impact_report` | no | read | — |
| `get_pipeline_report` | Time-to-fill and stage conversion for a requisition | `tools.hiring.get_pipeline_report` | no | read | — |

**Four gated writes.** `send_interview_invite` is the `external-effect` tier and therefore carries
the narrowest grant: recruiter and above only, never sourcer.

Every permission string lands in **two** places — the `ToolDescriptor` in `ModuleManifest.Tools` and
the `ModuleTool` from `IModuleToolSource` — built with `Permissions.ForTool(id, name)` in both.
`GET /api/admin/security/catalog` is the L2 check that reveals a mismatch; a tool present in one
place only is never callable and raises no error.

## 4. Permission model

Registered at the host with `AddPlenipoRole`. Runtime-editable per tenant; these are starting
baselines. Full strings in SPEC.md §6.

| Role | Tier |
|---|---|
| `hiring-sourcer` | reads + `screen_applicants`. **Approves nothing** — deliberately excluded from `chat.approvals.manage` so it cannot clear its own gate |
| `hiring-recruiter` | + rubric proposal, invites, transcript evaluation, rejection; may approve rubrics and rejections |
| `hiring-manager` | advance, **own requisitions only** (ADR-0006) |
| `hiring-talent-lead` | `tools.hiring.*` + `Permissions.ManageApprovals` |
| `hiring-compliance` | `get_impact_report` only. **No candidate-record read at all** — the person who audits decisions must not be able to influence them |

Enumerated allowlists, never wildcards, for every role below talent-lead: a wildcard would silently
hand that role every future write tool the module gains.

## 5. Tab surface

| id | Route | Permission | Surface | Reason |
|---|---|---|---|---|
| `chat` | `/hiring/chat` | — | server-driven | The platform's |
| `requisitions` | `/hiring/requisitions` | `tools.hiring.list_requisitions` | **server-driven** | A table with columns; the declarative surface expresses it exactly |
| `candidate` | `/hiring/candidates/:applicantId` | `tools.hiring.get_applicant` | **custom React** | The CV must render with **the cited spans highlighted in place**, next to the score that cites them. That is the product's central claim made visible, and no declarative table can express a span-anchored overlay on a document |
| `pipeline` | `/hiring/pipeline` | `tools.hiring.list_applicants` | **custom React** | Drag-and-drop across stage columns. Bulk spatial manipulation — the one interaction the research found is genuinely chat-hostile |
| `compliance` *(admin)* | `/hiring/compliance` | `tools.hiring.get_impact_report` | server-driven | Ratios in a table. **Admin tabs must declare a permission or startup throws** |

Routes checked against the fleet's existing modules (`legal`, `finance`, `compliance`) — no
collision. Two custom tabs is a real frontend cost, accepted for two named interactions and no
others.

### The custom-React seam, confirmed against source

Previously this section said "custom React" without naming the seam, which is not a shape a build
loop can act on. Pinned now, read from the platform checkout and the one product that has built
against it — **not from documentation**:

| Fact | Value | Where it was read |
|---|---|---|
| Package | **`@plenipo/ui`** | `plenipo/frontend/plenipo-ui/package.json` → `name` |
| Version to pin | **`0.1.0-alpha.28`** — the *same* number as the .NET pin | `networthy-ui/package.json`; matches this repo's `Directory.Build.props` |
| Registration | `defineModule("hiring", { tabs: { candidate: …, pipeline: … } })` | `plenipo-ui/src/index.ts` exports `defineModule`, `createModuleUiRegistry`, `resolveTabComponent` from `./lib/moduleUi` |
| Mounting | `<PlenipoApp moduleUi={[hiring]} branding={{ name: "Hireworthy" }} />` | `networthy-ui/src/App.tsx` |
| Project layout | `frontend/hireworthy-ui/` | mirrors `networthy/frontend/networthy-ui/` |
| AppHost wiring | `builder.AddViteApp("hireworthy-ui", dir).WithPnpm()` | `Networthy.AppHost/AppHost.cs:80` |
| Toolchain | React `^18.3.1`, Vite `^6.4.3`, Tailwind `^3.4.14` | `networthy-ui/package.json` — Tailwind **v3, not v4** |

**The tab id is the key.** `defineModule` maps tab ids to components, so the manifest's
`TabDescriptor.Id` (`candidate`, `pipeline`) is what the React side registers against — not the
route. Renaming a tab id silently unmounts its component.

### The install path — asked, then answered

**How `@plenipo/ui` resolves on a bare clone.** The .NET packages are vendored into `.packages/`
precisely because they are not on nuget.org. `@plenipo/ui` is equally not on public npm — and
`networthy` has **no `.npmrc` and no vendored tarball** anywhere in its worktree, so how its
`npm install` ever succeeds is not visible from its repo.

That is not a detail. Platform invariant: *keyless by default — a product's CI needs no external
accounts.* If installing `@plenipo/ui` requires an authenticated registry, then adding a frontend
**breaks `dotnet test` and CI on a bare clone**, which is the one thing this product's whole
verification story rests on.

**Answered, empirically:** `@plenipo/ui` is published to the **public** npm registry. A probe
install into an empty directory, with `_authToken=` deliberately blank, added 129 packages and
exited 0 with `0.1.0-alpha.28` on disk. The absence of an `.npmrc` in `networthy` was not a missing
piece — it is the evidence that none is needed.

**Keyless CI holds, and #14/#15 are unblocked.** Note the asymmetry worth remembering: the .NET
packages are **not** on nuget.org and must be vendored into `.packages/`; the npm package **is**
public and must not be. Assuming the two halves distribute the same way is the mistake this
paragraph exists to prevent.

## 6. Data model

`HiringDbContext` derives from **`ModuleDbContext`**, not `DbContext` (else `CreatedAt`/`UpdatedAt`
persist as `default`). Schema `hiring`, its own migrations-history table in that schema.

| Entity | Owns | `ITenantOwned` | Query filter declared | Notes |
|---|---|---|---|---|
| `Requisition` | reference, title, JD text, status, hiring manager | yes | **yes — shipped** | Unique `(TenantId, Reference)`. The RAG collection boundary |
| `Rubric` | version, status, rationale, approver | yes | **yes — shipped** | Unique `(RequisitionId, Version)`; versions never reused |
| `RubricCriterion` | name, requirement, weight, ordinal | yes | **yes — shipped** | |
| `Applicant` | name, email, phone, stage | yes | required | **PII** |
| `CvDocument` | fileId, extracted text, ocrUsed | yes | required | **PII**. Source of truth for citation containment |
| `ExtractedProfile` | employment spans, skills, education, inferred gaps | yes | required | **PII** |
| `ScreeningResult` | rubric **version-pinned**, total, status | yes | required | |
| `CriterionScore` | score, **citationText + start/end offsets**, unresolved flag | yes | required | **PII** (quotes a CV). The product's central guarantee |
| `ConsentRecord` | disclosure version + text, consentedAt | yes | required | **PII**. Blocks analysis until present |
| `InterviewSession` | invite token, expiry, consent ref, status | yes | required | **PII** |
| `InterviewTurn` | role, text, ordinal | yes | required | **PII**. Subject to the retention sweep |
| `TranscriptEvaluation` | rubric-pinned, total, status | yes | required | |
| `Decision` | kind, reason, evidence ref, approver, approvedAt | yes | required | Survives content deletion as a tombstone (ADR-0008) |
| `DemographicResponse` | self-reported categories | yes | required | **PII — sensitive**. Four-fifths input only |

**`PlatformDbContext` applies tenant filters by reflection; a module context does not.** Every row
above must declare its own. `ManifestGuardTests.Every_tenant_owned_entity_declares_a_query_filter`
fails the build otherwise — proven red before green during scaffolding.

## 7. Host seams

| Seam | Used | Reason |
|---|---|---|
| `AddPlenipoPlatform` | **yes** | The product |
| `AddPlenipoModule<HiringModule>` | **yes** | The one domain module |
| `AddPlenipoRole` | **yes** ×5 | Five authority tiers |
| `AddPlenipoConnector<>` | **no** | v1 takes CVs by upload. Ashby/Lever (public APIs) are v2; Greenhouse needs partner approval and deprecates v1/v2 after 2026-08-31 — a queue someone else controls, so v1 must not depend on it |
| `AddPlenipoProduct` | **not yet** | No paid tiers in v1; entitlements are a v2 decision. Deliberate, not forgotten |
| `AddPlenipoTenantProvisionedHook<>` | **no** | A new employer starts with an empty board. There is nothing honest to pre-create — a fabricated requisition implies a real role and real applicants |
| `AddPlenipoNotificationChannel<>` | **not yet, and this is the one to revisit** | Candidate communication is genuinely messaging-first, and the fleet uses this seam nowhere. `send_interview_invite` is the natural first consumer, in epic 5 |
| `AddPlenipoPlatformTools<>` | **no** | No product-wide tools above the module |

## 8. Connectors and RAG

**No connectors in v1.** Retrieval collection granularity is **per requisition** — a permission
boundary, not a performance knob: a candidate's material for REQ-142 must not surface while
screening REQ-150 without a fresh lawful basis.

## 9. Plans and entitlements

None in v1 (§7). When added, the server-side plan is authoritative; entitlements are never derived
from checkout metadata.

## 10. Background jobs

| Job | `Kind` (globally unique) | Trigger | Cadence | Tenancy |
|---|---|---|---|---|
| Bulk screening | `hiring.screen-batch` | reactive, queued by `screen_applicants` | on demand | Tenant id set explicitly from the queueing request |
| Retention sweep | `hiring.retention-sweep` | scheduled | daily | **Iterates tenants explicitly** (ADR-0008) |
| Invite expiry | `hiring.invite-expiry` | scheduled | hourly | Set explicitly per session |

## 11. Agent surface

Module instructions carry two product invariants as text the model reads every turn: **never
predict job performance**, and **never infer from name, photo, age, gender, nationality, appearance,
accent or tone of voice**. `ManifestGuardTests.Agent_instructions_forbid_forecasting_and_protected_characteristics`
fails the build if either is deleted — the instructions are prompt-shaped assets, so a change to
them also requires a golden eval case (rung 4).

## 12. C4 — component view of the module

```text
┌─ Hireworthy.Hiring (module id: hiring) ─────────────────────────────────────┐
│                                                                             │
│  HiringModule ── manifest ──> tools[15] · tabs[5] · roles[5] · jobs[3]       │
│       │                                                                     │
│       ├── HiringToolSource (SINGLETON) ──resolves per call──> HiringTools    │
│       │        │                                    (scoped, holds DbContext)│
│       │        └── ModuleTool × 15, permission == manifest descriptor        │
│       │                                                                     │
│       ├── MapEndpoints ──> /api/hiring/requisitions   (Requisitions tab)     │
│       │                    /api/hiring/candidates/*   (Candidate tab)        │
│       │                    /api/hiring/interview/{token}  (CANDIDATE, anon)  │
│       │                                                                     │
│       └── HiringDbContext : ModuleDbContext  schema "hiring"                 │
│                └── 14 entities, HasQueryFilter declared PER ENTITY           │
└──────────────────────────┬──────────────────────────────────────────────────┘
                           │ crosses into the platform
   ┌───────────────────────┴────────────────────────────────────────────────┐
   │ approval lane · append-only audit · RBAC-before-model · tenant filters │
   │ file store + OCR · RAG (per-requisition) · AG-UI + SignalR · jobs      │
   └────────────────────────────────────────────────────────────────────────┘
```

The context and container views are the platform's and identical for every product; redrawing them
would add pages and no information.

## 13. Epic → seam map — the exit condition

| Epic | Delivered by | Shaped? |
|---|---|---|
| **1** Requisitions and the approved rubric | tools `list_requisitions`, `get_requisition`, `propose_rubric` (gated) · tab `requisitions` · entities `Requisition`, `Rubric`, `RubricCriterion` | **yes — and already L1-proven at runtime** |
| **2** CV intake and structured extraction | tool `parse_cv` · entities `CvDocument`, `ExtractedProfile`, `Applicant` · platform file store + OCR | yes |
| **3** Cited screening and the shortlist decision | tools `screen_applicants`, `advance_candidates` (gated), `reject_candidate` (gated) · entities `ScreeningResult`, `CriterionScore`, `Decision` · job `hiring.screen-batch` · ADR-0005 | yes |
| **4** Candidate view and pipeline board | tabs `candidate`, `pipeline` (**both custom React**, §5) · tools `get_applicant`, `list_applicants` | yes |
| **5** Consent, disclosure and the candidate surface | module endpoint `/api/hiring/interview/{token}` (anonymous, ADR-0007) · tool `send_interview_invite` (gated, external-effect) · entity `ConsentRecord` · job `hiring.invite-expiry` | yes |
| **6** The adaptive structured interview | tool `evaluate_transcript` · entities `InterviewSession`, `InterviewTurn`, `TranscriptEvaluation` · AG-UI turn-based (ADR-0009) | yes |
| **7** Evidence and oversight | tools `explain_score`, `get_impact_report`, `get_pipeline_report` · admin tab `compliance` · entity `DemographicResponse` · job `hiring.retention-sweep` | **partly — OQ7 open** |

**7/7 epics map to a named seam. No empty cells.** Epic 7 carries one unresolved open question that
does not block its other two capabilities.

## 14. Open questions

| # | Question | Why it is still open |
|---|---|---|
| ~~**OQ9**~~ | **RESOLVED 2026-08-06 — the package IS public.** `@plenipo/ui` is on registry.npmjs.org (6 versions, `0.1.0-alpha.28` is `latest`), and `npm install @plenipo/ui@0.1.0-alpha.28` succeeds **with an empty auth token, exit 0**. So there is no `.npmrc`, no token, and keyless CI is preserved — which is exactly why `networthy` has no `.npmrc`: it never needed one. The npm half is **not** vendored like `.packages/`, and does not need to be. #14 and #15 are unblocked. |
| **OQ7** | Where do demographics for four-fifths monitoring come from, and what happens when absent (common and lawful)? | **Legally sensitive and not an engineering call.** Options: optional self-reported EEO fields with a reported coverage %, or omit and report only what exists. **A human decides.** Blocks `get_impact_report`, not the rest of epic 7 |
| — | Does a rejected candidate get to see their evidence? | A differentiator with real support cost, and a GDPR Art. 22 question in the EU. Not v1; recorded so it is not rediscovered |

Every other open question from PLAN.md §11 is now an ADR. **OQ7 is `Approval-required`.**
