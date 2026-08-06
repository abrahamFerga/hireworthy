# Hireworthy — product specification

**Module id:** `hiring` · **Industry:** `talent-acquisition` · **Written:** 2026-08-05

Input: [`research/talent-acquisition.md`](research/talent-acquisition.md) and the go/no-go brief.
**Evidence level L4** — this is a judgement until a human accepts it (L5). Internal consistency is
not verification.

## 1. Framing

> **Hireworthy lets a recruiter screen and interview a pile of applicants from the raw CVs and the
> job description, with the hiring manager approving before any candidate is advanced or rejected.**

**The approval-worthy write:** advancing or rejecting a candidate. An AI *proposing* a shortlist is
valuable; an AI *rejecting* a person autonomously is legally radioactive under NYC LL144 and the EU
AI Act's human-oversight obligation, and ethically wrong regardless. The gate is not a safety net
bolted on — it is the feature the buyer's counsel signs off on.

**The claim this product must never make:** that it predicts job performance. That claim is not
checkable on a useful timescale and it is what the category has been burned for. Hireworthy claims
its screening is *evidenced, cited, consistent, and human-approved* — all of which are checkable,
three of them deterministically (§8).

## 2. Jobs to be done

| # | Job | Observable outcome |
|---|---|---|
| J1 | When a req closes for applications, I want every applicant scored against the same rubric, so I can defend why each was advanced or rejected | A shortlist where every criterion cites a verbatim span from that candidate's CV |
| J2 | When I read a JD, I want the evaluation criteria extracted and agreed **before** anyone is scored, so the yardstick doesn't drift down the pile | A versioned rubric approved by a human, attached to the req |
| J3 | When a candidate reaches screening, I want a structured interview conducted consistently, so I get comparable evidence instead of 20 differently-run conversations | A transcript scored against the same rubric, with quotes |
| J4 | When a hiring manager questions a ranking, I want to show the evidence behind it, so the conversation is about the candidate and not about the tool | A per-candidate evidence chain: rubric, citation, score, approver, timestamp |
| J5 | When counsel asks whether our screening has adverse impact, I want the selection rates by category, so I can answer with arithmetic instead of assurance | A four-fifths report per requisition, exportable |
| J6 | When a candidate asks what the AI evaluated, I want to have told them before it ran, so consent is real and the process is lawful in Illinois | A recorded consent event with the disclosure text shown, timestamped before any AI analysis |

## 3. Personas and authority tiers

| Persona | May read | May draft / propose | May commit / approve |
|---|---|---|---|
| **Sourcer** | Reqs, applicants, CVs, scores | Screen a pile; propose a shortlist | **Nothing** |
| **Recruiter** | Same | Propose a rubric; invite to interview | Reject a candidate; approve a rubric |
| **Hiring Manager** | **Own requisitions only** | — | Advance a candidate |
| **Talent Lead** | All | All | All of the above, plus publish a req |
| **Compliance Officer** | **Impact reports and audit only — no candidate records** | — | Nothing |

**Merges applied.** *Interview Coordinator* was folded into **Recruiter** — it differed only in
being unable to reject, which the permission set already expresses without a separate tier.
*Interviewer* was **not** merged: a panellist must not read others' scorecards before submitting
their own, and that read-restriction changes an outcome, so it is a genuine tier. It is out of scope
for v1 (single AI interviewer, no human panel) and returns with panel interviews.

**Compliance Officer reads the audit but never a candidate record** — an unusual shape, and the one
that makes the tier real rather than decorative.

## 4. Capabilities

### 4.1 Must-have

| Capability | Seam | Approval-gated? | Permission | Job |
|---|---|---|---|---|
| List / open requisitions | **Module tool** + Tab | no (read) | `tools.hiring.list_requisitions` | J1 |
| Open a candidate with CV, score and stage in one view | **Tab** (+ tool below) | no (read) | `tools.hiring.get_applicant` | J1, J4 |
| Ingest a CV and extract structured facts | **Module tool** | no (read-shaped; writes an extraction row) | `tools.hiring.parse_cv` | J1 |
| **Propose the rubric from the JD** | **Module tool** | **yes** | `tools.hiring.propose_rubric` | J2 |
| **Score a pile against the approved rubric, with citations** | **Module tool** | no — writes *proposals* only (see OQ2) | `tools.hiring.screen_applicants` | J1 |
| **Advance candidates** | **Module tool** | **yes** | `tools.hiring.advance_candidates` | J1 |
| **Reject a candidate** | **Module tool** | **yes** | `tools.hiring.reject_candidate` | J1 |
| Pipeline board (stages, drag between them) | **Tab** | n/a — human-driven | `tools.hiring.list_applicants` | J1 |
| Time-to-fill / pipeline conversion | **Module tool** + Tab | no (read) | `tools.hiring.get_pipeline_report` | J5 |

### 4.2 Differentiator

| Capability | Seam | Approval-gated? | Permission | Job |
|---|---|---|---|---|
| **Adaptive structured interview** — follow-up probes generated from what the candidate just said, scored by a *separate* evaluator context | **Module endpoint** (candidate surface) + **Module tool** `evaluate_transcript` | no on evaluate (proposal); **yes** on the advance that follows | `tools.hiring.evaluate_transcript` | J3 |
| **Explain a ranking with citations** — "why is #47 below #48?" answered from the rubric and the cited spans | **Module tool** | no (read) | `tools.hiring.explain_score` | J4 |
| **Consent and disclosure as a blocking gate** — no AI analysis runs before a recorded, timestamped consent | **Module endpoint** (candidate) + **Background job** | n/a — the candidate's own act | `tools.hiring.get_consent_status` | J6 |
| **Adverse-impact monitoring** — selection rates by category against the four-fifths rule | **Module tool** + admin **Tab** | no (read) | `tools.hiring.get_impact_report` | J5 |
| **Invite a candidate to interview** (outbound; starts the disclosure clock) | **Module tool** | **yes** | `tools.hiring.send_interview_invite` | J3, J6 |

The reasoning work is the first row: two agents that must disagree — an interviewer whose job is to
*elicit* and an evaluator whose job is to be *unconvinced*. That is the differentiator the buyer is
being sold, and per the research it is what no vendor in the field expresses as a data model.

### 4.3 Out-of-scope for v1

| Excluded | Reason | Reopening trigger |
|---|---|---|
| Rendered video avatar | Platform has no realtime media transport (research §7 gap 1); and it is a commodity vendor call that must carry no product claim | A connector to a session-hosting vendor exists **and** v1's turn-based interview is proven |
| Any ATS connector | Greenhouse needs partner approval and deprecates v1/v2 after 2026-08-31 — a queue someone else controls | A design partner on Ashby or Lever (public APIs, no partner gate) |
| Calendar scheduling | Somebody else's system of record | Interview volume makes manual scheduling the bottleneck |
| Sourcing / outbound / CRM | A different product (Gem, HireEZ); not the killer workflow | Never for v1 |
| Offer management, onboarding, HRIS sync | Downstream of the decision this product exists to make | A customer asks and the decision loop is proven |
| Published LL144 bias audit | **Business class** — an act by an independent third party, not software | Never — v1 produces the data; a human commissions the audit |
| Human interview panels + scorecard independence | Adds a real RBAC tier; v1 has one AI interviewer | Multi-interviewer demand |
| Multi-language interviewing | No evidence gathered; deferred honestly rather than guessed | A non-English design partner |

## 5. Platform-provided — cut, not to return as feature requests

| Cut | Provided by |
|---|---|
| Sign-in, SSO, invites, user management | Platform auth + admin console at `/admin` |
| Roles, permissions, a role editor | Dotted permissions + runtime-editable baselines |
| Audit log / "who changed what" | Append-only audit database — already records every tool call |
| "Are you sure?" confirmation for AI actions | `RequiresApproval = true` |
| Tenant / organization separation | `ITenantOwned` + global query filters |
| File upload, PDF text extraction, **OCR** | Tenant-scoped file store + platform document tools |
| Semantic search over CVs with citations | Opt-in RAG pipeline, per-collection gating |
| Chat window, streaming, message history | `/api/chat/stream`, `/api/agui/{moduleId}`, `/hubs/agent` |
| Token budgets and cost dashboards | Per-tenant usage tracking |
| Third-party OAuth / webhooks | Connector SDK |
| Email / SMS / WhatsApp delivery | Notification channels |
| Job scheduler | Manifest-declared recurring jobs |

**None of these is a capability of Hireworthy.** The résumé *parsing* is ours; the file store, the
OCR and the retrieval underneath it are not.

## 6. RBAC model

Shipped baselines registered at the host with `AddPlenipoRole`. Runtime-editable per tenant — the
spec fixes the *starting* baseline only.

```text
hiring-sourcer      chat.use, chat.conversations.view, files.read,
                    tools.documents.read_document, tools.documents.list_documents,
                    tools.hiring.list_requisitions, tools.hiring.get_requisition,
                    tools.hiring.list_applicants, tools.hiring.get_applicant,
                    tools.hiring.parse_cv, tools.hiring.screen_applicants,
                    tools.hiring.explain_score

hiring-recruiter    <sourcer> + files.upload,
                    tools.hiring.propose_rubric, tools.hiring.send_interview_invite,
                    tools.hiring.evaluate_transcript, tools.hiring.reject_candidate,
                    tools.hiring.get_pipeline_report, tools.hiring.get_consent_status

hiring-manager      chat.use, chat.conversations.view, files.read,
                    tools.hiring.list_requisitions, tools.hiring.get_requisition,
                    tools.hiring.list_applicants, tools.hiring.get_applicant,
                    tools.hiring.explain_score, tools.hiring.advance_candidates
                    — scoped to requisitions they own (see OQ1)

hiring-talent-lead  chat.use, chat.conversations.view, files.upload, files.read,
                    tools.documents.*, tools.hiring.*

hiring-compliance   chat.use, tools.hiring.get_impact_report
                    — deliberately NOT tools.hiring.* and no candidate-record read
```

### May call / may approve

| Role | May call a state-changing capability | May approve one |
|---|---|---|
| **hiring-sourcer** | **yes** — `screen_applicants` | **no** |
| **hiring-recruiter** | yes | yes — rubrics, rejections |
| **hiring-manager** | yes — `advance_candidates` | yes — advances, own reqs |
| **hiring-talent-lead** | yes | yes — all |
| **hiring-compliance** | no | no |

**`hiring-sourcer` proposes and can approve nothing** — the approval lane is load-bearing, not
ceremony. `system_admin` is not respecified; it always resolves to `*`.

## 7. Regulatory constraints

| Regime | Obligation | Platform | Seam |
|---|---|---|---|
| **NYC LL144** | Selection-rate data by sex, race/ethnicity and intersectional category | **supports** — append-only outcomes | `tools.hiring.get_impact_report`, admin tab |
| **NYC LL144** | Independent annual bias audit + published summary | **does not deliver** — a third-party act | — |
| **NYC LL144** | ≥10 business days' candidate notice | supports (audit timestamps) | Module endpoint + job |
| **IL AIVIA** | Notice + explanation of what the AI evaluates, **before** the interview | supports | Candidate module endpoint |
| **IL AIVIA** | **Consent before AI analysis** | supports | Blocking gate on `evaluate_transcript` |
| **IL AIVIA** | Delete within 30 days of request, including backups | **does not deliver** — and conflicts with append-only audit | Background job + OQ3 |
| **EU AI Act Annex III** (from **2027-12-02**) | Human oversight of a high-risk employment system | **supports — the approval gate is the mechanism** | `RequiresApproval = true` |
| **EU AI Act** | Record-keeping, technical documentation | supports record-keeping; **does not deliver** the technical file or conformity assessment | Audit DB |
| **BIPA / CUBI / WA** | Written consent before collecting facial geometry or voiceprints | **avoided by design** | See invariant below |
| **Title VII / EEOC** | Disparate-impact exposure | supports monitoring; **does not deliver** a validation study | `get_impact_report` |

### Product invariant — not a setting

> **Hireworthy never analyses the candidate's face or voice.** Assessment runs on the **transcript
> only**, against the approved rubric. The generated avatar is on the *interviewer* side. No facial
> template, no voiceprint, no prosody or expression as a signal.

**Four obligations land in "does not deliver" → this spec ends `Approval-required`.**

## 8. Success metrics

| Metric | Instrument | Target | By |
|---|---|---|---|
| **Citation validity** — every score's cited span exists verbatim in the source | Deterministic containment check in CI over the golden set | **100%**, build-breaking | Epic 1 |
| **Is the agent right** — recruiter accepts the proposed shortlist | Approval accept/reject rate on the approval lane | ≥70% accepted unmodified | 30 days post-pilot |
| Scoring consistency — same CV, same score on re-run | Re-run diff in CI | 100% identical | Epic 1 |
| Agent actually used | `GET /api/admin/audit/tool-calls` | `screen_applicants` called on ≥80% of reqs | 30 days |
| Extraction accuracy | Golden set of labelled CVs | ≥90% fields correct | Epic 2 |
| Cost per tenant | `GET /api/admin/usage?days=30` | < $2 per 100 CVs screened | 30 days |
| Time-to-decision | Approval-lane latency | Median < 24h | 30 days |
| Adverse impact | `get_impact_report` | All ratios ≥ 0.80, or flagged | Every req |

## 9. Open questions for the shape loop

| # | Question |
|---|---|
| **OQ1** | **"Hiring manager sees own reqs only" is row-level, not role-level.** The platform's RBAC is permission-string based. Is this a query filter in the module, a permission convention, or genuinely unsupported? Answer before epic 1. |
| **OQ2** | **`screen_applicants` writes proposal rows without an approval gate.** The spec's own rule says every state-changing tool is gated. The argument for an exception: it commits nothing — no stage changes until `advance_candidates`. **networthy set the precedent with exactly one ungated write behind ADR-0005.** Needs an ADR here, not a default. |
| **OQ3** | **IL AIVIA 30-day deletion versus append-only audit.** Likely resolution: delete content, retain a decision tombstone. Must be decided, not assumed. |
| **OQ4** | **The unauthenticated candidate surface.** A candidate is an outsider with a one-time link who must see a disclosure and answer questions without an account. The platform assumes an authenticated tenant user. Needs an ADR — getting it wrong is a tenant-isolation incident. |
| **OQ5** | **Does v1's interview run over the turn-based AG-UI surface?** Recommendation: yes, and prove it end to end before any media work. |
| **OQ6** | **Rubric versioning.** If a JD is edited mid-pile, earlier scores used a different yardstick. Almost certainly a versioned entity — confirm. |
| **OQ7** | **Demographics for four-fifths monitoring** — self-reported EEO fields, and the behaviour when absent (common and lawful). |
| **OQ8** | **One module or two?** Screening and interviewing share the rubric and the candidate; default is one. State why rather than inherit it. |

## Exit check — L2 rule check on this document

| # | Rule | Result |
|---|---|---|
| 1 | Every must-have names exactly one primary seam; none is *Platform-provided* | **pass** — 9/9 |
| 2 | Every state-changing capability is marked approval-gated | **pass with one declared exception** — `screen_applicants`, raised as **OQ2** rather than defaulted |
| 3 | Every freebie from the research matrix appears under Platform-provided | **pass** — §5, 12 rows |
| 4 | Permission strings dotted lowercase, `tools.<module>.<tool>`; no parallel auth concept | **pass** — verified against `networthy` source, not documentation |
| 5 | At least one role may call a state-changing capability and may not approve it | **pass** — `hiring-sourcer` |

**Terminal state: `Approval-required`** (§7 — four obligations the platform supports but does not
deliver). Level **L2** for the exit check above; **L4** for the document as a whole.
