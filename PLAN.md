# Hireworthy — build plan

**Module id:** `hiring` · **Module project:** `Hireworthy.Hiring` · **From:** [`SPEC.md`](SPEC.md)

**There is no Foundations epic.** The backbone is `AddPlenipoPlatform()` — one method call. Epic 1
is the thinnest *true* hiring capability that proves the module loads, a read tool answers a real
domain question, a write tool parks on the approval gate, and a tab renders it.

## 1. Epics in build order

### Epic 1 — Requisitions and the approved rubric

**Delivers:** *List / open requisitions* · *Propose the rubric from the JD*

A recruiter opens req #142, the agent reads the job description and proposes the evaluation criteria
every applicant will be measured against, and **the recruiter approves those criteria before anyone
is scored**. Freezing the yardstick first is what makes 180 scores comparable — so this is not a
bootstrap slice, it is the hinge the whole product turns on.

The four proofs:

1. `GET /api/platform/modules` lists `{"id":"hiring"}`
2. `list_requisitions` / `get_requisition` answer over real requisition data
3. `propose_rubric` parks: the AG-UI turn emits `CUSTOM(approval_required)` and **the reply does not
   claim the rubric was saved**
4. The Requisitions tab renders the req and its pending rubric

*Dependencies:* none.

### Epic 2 — CV intake and structured extraction

**Delivers:** *Ingest a CV and extract structured facts*

Upload a CV (PDF, often two-column, sometimes a scan), extract employment spans, skills and
education. Inconsistent date formats become resolved spans; gaps become inferable. The platform
supplies the file store, text extraction and OCR — **the extraction schema and the inference are
ours.**

*Depends on:* E1 (an applicant belongs to a requisition).

### Epic 3 — Cited screening and the shortlist decision

**Delivers:** *Score a pile against the approved rubric, with citations* · *Advance candidates* ·
*Reject a candidate*

**The killer workflow completes here.** Every applicant scored against the frozen rubric, each
criterion carrying a verbatim span from that candidate's CV, unresolvable ones flagged rather than
guessed. Then the approval-gated decisions.

The citation-containment check (SPEC §8) becomes a build-breaking test in this epic — seen red
before it is seen green.

*Depends on:* E1 (rubric), E2 (extracted CVs).

### Epic 4 — Candidate view and pipeline board

**Delivers:** *Open a candidate with CV, score and stage in one view* · *Pipeline board*

The two screens the research found are category table stakes and **chat-hostile** — dragging 40
candidates between stages and comparing CVs side by side are spatial tasks. Tabs, not chat.

*Depends on:* E3 (there must be scores and stages to render).

### Epic 5 — Consent, disclosure and the candidate surface

**Delivers:** *Consent and disclosure as a blocking gate* · *Invite a candidate to interview*

A candidate is an **outsider holding a one-time link** who must see what the AI evaluates and
consent before any analysis runs. Legally required in Illinois; near-universally skipped by the
market. Scheduled ahead of the interview because **it gates it** — and because OQ4 is the riskiest
unknown in the plan, so it goes early.

*Depends on:* E1. **Blocked on OQ4** (unauthenticated access pattern) — must be answered by the
design loop first.

### Epic 6 — The adaptive structured interview

**Delivers:** *Adaptive structured interview*

The interviewer agent probes what the candidate actually said; a **separate** evaluator agent scores
the transcript against the same rubric, quote-bound. Turn-based over the existing AG-UI surface
(OQ5) — no media transport, no avatar, no face or voice analysis.

*Depends on:* E1 (rubric), E5 (consent gate must block this), E3 (the rubric-scoring machinery).

### Epic 7 — Evidence and oversight

**Delivers:** *Explain a ranking with citations* · *Adverse-impact monitoring* ·
*Time-to-fill / pipeline conversion*

"Why is #47 below #48?" answered from the rubric and the cited spans. Four-fifths selection-rate
report per requisition. Pipeline conversion for the buyer's own metric.

*Depends on:* E3 (decisions to analyse), E6 (interview outcomes).

**Not an epic:** *open source* and *self-hostable* are structural facts of the repo and its licence,
not work. Stated here so nobody manufactures an epic for them.

## 2. Delivered by the platform — struck, not built

| Capability the spec implies | Seam that supplies it |
|---|---|
| Sign-in, SSO, invites, user management | Platform auth + admin console |
| Roles, permissions, role editing | Dotted permissions, runtime-editable baselines at `/admin` |
| Audit trail of every decision | Append-only audit database |
| The approval gate mechanism itself | `RequiresApproval = true` |
| Tenant separation | `ITenantOwned` + global query filters |
| File upload, PDF text extraction, **OCR** | Tenant-scoped file store + platform document tools |
| Semantic retrieval over CVs with citations | Opt-in RAG pipeline, per-collection gating |
| Chat window, streaming, history | `/api/chat/stream`, `/api/agui/hiring`, `/hubs/agent` |
| Token budgets, cost dashboards | Per-tenant usage tracking |
| Job scheduling | Manifest-declared recurring jobs, platform processor |
| Email / SMS / WhatsApp delivery | Notification channels |

## 3. Module list

| Project | Bounded context | Capabilities |
|---|---|---|
| `Hireworthy.Hiring` | Requisitions, rubrics, applicants, screening, interviews, decisions | **All of them** |

**Exactly one module.** Screening and interviewing share the rubric, the applicant and the decision
record — foreign keys everywhere between them, one audience, one release cadence. None of the three
split conditions holds. Answering **OQ8** deliberately rather than inheriting the default: a second
module would add a second `DbContext` needing its own per-entity `HasQueryFilter`, multiplying the
highest-consequence mistake in this codebase for no domain benefit.

**This plan therefore does not trigger `Approval-required` on structural grounds** — one module, no
connectors, no platform change.

## 4. Entity sketch

Conceptual only — the schema is the design loop's. **All are `ITenantOwned`.**

| Entity | Fields that matter | PII |
|---|---|---|
| `Requisition` | title, jobDescriptionText, ownerUserId, status, location | — |
| `Rubric` | requisitionId, **version**, status (proposed/approved), approvedBy, approvedAt | — |
| `RubricCriterion` | rubricId, name, requirement, weight, ordinal | — |
| `Applicant` | requisitionId, name, email, phone, stage, source | **PII** |
| `CvDocument` | applicantId, fileId, extractedText, ocrUsed | **PII** |
| `ExtractedProfile` | applicantId, employmentSpans, skills, education, inferredGaps | **PII** |
| `ScreeningResult` | applicantId, rubricId **(version-pinned)**, totalScore, status=proposed | — |
| `CriterionScore` | screeningResultId, criterionId, score, **citationText, citationStart, citationEnd**, unresolved | **PII** (quotes a CV) |
| `ConsentRecord` | applicantId, disclosureVersion, disclosureText, consentedAt | **PII** |
| `InterviewSession` | applicantId, rubricId, inviteToken, expiresAt, consentRecordId, status | **PII** |
| `InterviewTurn` | sessionId, role (interviewer/candidate), text, ordinal | **PII** |
| `TranscriptEvaluation` | sessionId, rubricId, totalScore, status=proposed | — |
| `Decision` | applicantId, kind (advance/reject), reason, evidenceRef, approvedBy, approvedAt | — |
| `DemographicResponse` | applicantId, selfReportedCategories | **PII — sensitive** |

`CriterionScore.citationText` + offsets are the product's central guarantee: the containment check
in E3 proves the quoted span exists verbatim in `CvDocument.extractedText`.

## 5. Tool inventory

| Tool | Does | Permission | Approval | Epic |
|---|---|---|---|---|
| `list_requisitions` | List open requisitions with stage counts | `tools.hiring.list_requisitions` | no | 1 |
| `get_requisition` | Read one requisition, its JD and its approved rubric | `tools.hiring.get_requisition` | no | 1 |
| `propose_rubric` | Read a job description and propose weighted evaluation criteria for it | `tools.hiring.propose_rubric` | **yes** | 1 |
| `parse_cv` | Extract employment history, skills and education from an uploaded CV | `tools.hiring.parse_cv` | no | 2 |
| `list_applicants` | List applicants on a requisition, filtered by stage or score | `tools.hiring.list_applicants` | no | 2 |
| `get_applicant` | Read one applicant: CV, extracted profile, scores and stage | `tools.hiring.get_applicant` | no | 2 |
| `screen_applicants` | Score every applicant on a requisition against the approved rubric, citing the CV span behind each criterion | `tools.hiring.screen_applicants` | no — **see OQ2** | 3 |
| `advance_candidates` | Move named candidates to the next stage | `tools.hiring.advance_candidates` | **yes** | 3 |
| `reject_candidate` | Reject a candidate with a recorded reason | `tools.hiring.reject_candidate` | **yes** | 3 |
| `send_interview_invite` | Send a candidate a one-time interview link with the AI-use disclosure | `tools.hiring.send_interview_invite` | **yes** | 5 |
| `get_consent_status` | Report which candidates have consented to AI analysis | `tools.hiring.get_consent_status` | no | 5 |
| `evaluate_transcript` | Score a finished interview transcript against the rubric, quoting the answers | `tools.hiring.evaluate_transcript` | no — proposal only | 6 |
| `explain_score` | Explain why one candidate ranks above another, citing both CVs | `tools.hiring.explain_score` | no | 7 |
| `get_impact_report` | Report selection rates by category against the four-fifths rule | `tools.hiring.get_impact_report` | no | 7 |
| `get_pipeline_report` | Report time-to-fill and stage conversion for a requisition | `tools.hiring.get_pipeline_report` | no | 7 |

**Four approval-gated writes.** Each permission string must appear **twice** at build time — the
manifest `ToolDescriptor` and the `ModuleTool` — and `GET /api/admin/security/catalog` is where a
mismatch surfaces.

## 6. Tab inventory

| id | Route | Permission | Epic |
|---|---|---|---|
| `requisitions` | `/hiring/requisitions` | `tools.hiring.list_requisitions` | 1 |
| `pipeline` | `/hiring/pipeline` | `tools.hiring.list_applicants` | 4 |
| `candidate` | `/hiring/candidates/:applicantId` | `tools.hiring.get_applicant` | 4 |
| `compliance` *(admin)* | `/hiring/compliance` | `tools.hiring.get_impact_report` | 7 |

Routes are unique across all modules. **The admin tab declares a permission** — startup validation
throws otherwise.

## 7. Permission model

| Role | Grants |
|---|---|
| `hiring-sourcer` | `chat.use`, `chat.conversations.view`, `files.read`, `tools.documents.read_document`, `tools.documents.list_documents`, `tools.hiring.list_requisitions`, `tools.hiring.get_requisition`, `tools.hiring.list_applicants`, `tools.hiring.get_applicant`, `tools.hiring.parse_cv`, `tools.hiring.screen_applicants`, `tools.hiring.explain_score` |
| `hiring-recruiter` | *sourcer* + `files.upload`, `tools.hiring.propose_rubric`, `tools.hiring.send_interview_invite`, `tools.hiring.get_consent_status`, `tools.hiring.evaluate_transcript`, `tools.hiring.reject_candidate`, `tools.hiring.get_pipeline_report` |
| `hiring-manager` | `chat.use`, `chat.conversations.view`, `files.read`, `tools.hiring.list_requisitions`, `tools.hiring.get_requisition`, `tools.hiring.list_applicants`, `tools.hiring.get_applicant`, `tools.hiring.explain_score`, `tools.hiring.advance_candidates` — **scoped to owned requisitions, see OQ1** |
| `hiring-talent-lead` | `chat.use`, `chat.conversations.view`, `files.upload`, `files.read`, `tools.documents.*`, `tools.hiring.*` |
| `hiring-compliance` | `chat.use`, `tools.hiring.get_impact_report` — **deliberately not `tools.hiring.*`, and no candidate-record read** |

`hiring-sourcer` calls a state-changing tool (`screen_applicants`) and can approve nothing.
`system_admin` is not a product role and always resolves to `*`.

## 8. Connector surface

**None in v1.** CVs arrive by upload. Ashby/Lever (public APIs, no partner gate) and Greenhouse
(partner approval required, v1/v2 deprecated after 2026-08-31) are v2, as separate
`Hireworthy.Connectors.<Vendor>` projects. The avatar vendor is also a connector, and also not v1.

## 9. Background jobs

| Job | `Kind` | Trigger | Cadence | Reads / writes | Tenant |
|---|---|---|---|---|---|
| Bulk screening | `hiring.screen-batch` | Reactive — queued by `screen_applicants` | On demand | Reads CVs + rubric; writes `ScreeningResult`, `CriterionScore` | Set explicitly from the queueing request |
| Retention sweep | `hiring.retention-sweep` | Scheduled | Daily | Deletes interview content past its window; writes a decision tombstone | **Iterates tenants explicitly — see OQ3** |
| Invite expiry | `hiring.invite-expiry` | Scheduled | Hourly | Expires unused interview tokens | Set explicitly per session |

`Kind` values are globally unique — startup-validated. No scheduler is planned; the platform runs
these.

## 10. Coverage

One row per SPEC must-have, verbatim from SPEC §4.1, plus the §4.2 differentiators.

| SPEC capability | Epic |
|---|---|
| List / open requisitions | 1 |
| Open a candidate with CV, score and stage in one view | 4 |
| Ingest a CV and extract structured facts | 2 |
| Propose the rubric from the JD | 1 |
| Score a pile against the approved rubric, with citations | 3 |
| Advance candidates | 3 |
| Reject a candidate | 3 |
| Pipeline board (stages, drag between them) | 4 |
| Time-to-fill / pipeline conversion | 7 |
| Adaptive structured interview | 6 |
| Explain a ranking with citations | 7 |
| Consent and disclosure as a blocking gate | 5 |
| Adverse-impact monitoring | 7 |
| Invite a candidate to interview (outbound; starts the disclosure clock) | 5 |

**14 capabilities, 14 rows, 0 unplaced, 0 duplicated.** Diff run mechanically in §12.

## 11. Open questions for the design loop

| # | Decision | Options | Decider |
|---|---|---|---|
| **OQ1** | How is "hiring manager sees own reqs only" enforced? | (a) module query filter on `Requisition.ownerUserId`; (b) a permission convention; (c) unsupported → platform request | **Design loop** — ADR. Blocks E1 |
| **OQ2** | Is `screen_applicants` an ungated write? | (a) ungated, proposals commit nothing — networthy's ADR-0005 precedent; (b) gate it and accept the friction of approving 180 scores | **Design loop** — ADR. Blocks E3 |
| **OQ3** | IL AIVIA 30-day deletion vs append-only audit | (a) delete content, retain a decision tombstone; (b) retention window per tenant; (c) platform request | **Design loop** — ADR, plus a **human** on the policy. Blocks E5 |
| **OQ4** | The unauthenticated candidate surface | (a) module endpoint + signed one-time token; (b) a platform seam that does not exist → platform request | **Design loop** — ADR. **Riskiest unknown; blocks E5 and E6** |
| **OQ5** | Interview over turn-based AG-UI, or wait for a media transport? | (a) AG-UI now (recommended); (b) block on a transport | **Design loop**. Blocks E6 |
| **OQ6** | Rubric versioning | (a) versioned entity, scores pin a version (recommended); (b) immutable-on-approve | **Design loop**. Blocks E1 |
| **OQ7** | Demographics for four-fifths monitoring | (a) self-reported optional EEO fields, report coverage %; (b) omit and report only what exists | **Design loop** + **human** — legally sensitive. Blocks E7 |
| **OQ8** | One module or two? | **Answered in §3: one.** | Recorded, not open |

**"Is this ours or a missing platform primitive?"** is live for OQ1, OQ3 and OQ4. Each names a
platform request as an explicit option so it is decided rather than discovered mid-build.

## 12. Exit condition

**L2 structural check plus L5 human acceptance.** No compiler ran on this.

| Gate | Result |
|---|---|
| **Coverage** — SPEC capability set vs §10 first column | **exit 0**, 14/14, 0 unplaced, 0 extra, 0 duplicated. Script seen **red** first (injected a fake SPEC capability → exit 1) and it also caught a **real** defect on its first real run: one §10 row had been shortened from the SPEC's verbatim wording. That is why this row cites an exit code and not a reading |
| **Skeleton** — epic 1 names a domain capability and lists the four proofs | **pass** — rubric approval; would not read identically for any other industry |
| **No platform epic** — none describes auth, tenancy, audit, approvals-as-mechanism, a scheduler, a chat panel or a connector registry | **pass** — all struck into §2 |

**Terminal state: `Success`** on the structural gates. Human acceptance (L5) is pending and is what
actually closes this loop.
