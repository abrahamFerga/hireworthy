# Industry research — talent acquisition

**Slug:** `talent-acquisition` · **Researched:** 2026-08-05 · **For:** Hireworthy

> **Evidence level: L4 — model as judge.** This is synthesis of secondary sources and vendor
> marketing, read via search. No amount of citation upgrades it. The only L3 signal in this loop is
> a real buyer. Where a cell says `unknown`, that is a finding, not a hole to fill later.

Companion documents: the go/no-go argument, kill criteria and buyer/pain-in-units live in
[`../opportunities/talent-acquisition.md`](../opportunities/talent-acquisition.md) and are **not
repeated here**. This file covers the field, the matrix, the UX patterns, the compliance
classification, the platform mapping, and the three-way scope split.

## 1. Sources

| # | Source | Read |
|---|---|---|
| S1 | [Greenhouse — 63% of job seekers have faced an AI interview](https://www.greenhouse.com/newsroom/63-of-job-seekers-have-faced-an-ai-interview-most-havent-had-a-good-one-yet) | 2026-08-05 |
| S2 | [People Management — third of candidates drop out of AI-led interviews](https://www.peoplemanagement.co.uk/article/1956592/third-candidates-drop-hiring-process-ai-led-interviews-survey-finds) | 2026-08-05 |
| S3 | [Metaintro — 38% of job seekers walk out of AI interviews](https://www.metaintro.com/blog/ai-interviews-candidate-dropout-2026) | 2026-08-05 |
| S4 | [Leon Consulting — ATS pricing comparison 2026](https://leonstaff.com/blogs/ats-pricing-comparison-2026/) | 2026-08-05 |
| S5 | [Leon Consulting — Greenhouse ATS pricing](https://leonstaff.com/blogs/greenhouse-ats-pricing/) | 2026-08-05 |
| S6 | [Pin — Ashby pricing 2026](https://www.pin.com/blog/ashby-pricing/) | 2026-08-05 |
| S7 | [Metaview — best AI hiring tools 2026](https://www.metaview.ai/resources/blog/ai-hiring-tools) | 2026-08-05 |
| S8 | [Recruiting Tech Reviews — HireVue vs Paradox](https://recruitingtechreviews.com/articles/hirevue-vs-paradox) | 2026-08-05 |
| S9 | [Greenhouse Harvest API developer docs](https://developers.greenhouse.io/harvest.html) | 2026-08-05 |
| S10 | [Cavuno — ATS platforms with public job posting APIs](https://cavuno.com/blog/ats-platforms-public-job-posting-apis) | 2026-08-05 |
| S11 | [NYC DCWP — AEDT FAQ (regulator)](https://www.nyc.gov/assets/dca/downloads/pdf/about/DCWP-AEDT-FAQ.pdf) | 2026-08-05 |
| S12 | [ILGA — 820 ILCS 42, AI Video Interview Act (regulator)](https://www.ilga.gov/Legislation/ILCS/Articles?ActID=4015&ChapterID=68&Print=True) | 2026-08-05 |
| S13 | [EU AI Act — Annex III](https://artificialintelligenceact.eu/annex/3/) · [AI Act Service Desk](https://ai-act-service-desk.ec.europa.eu/en/ai-act/annex-3) | 2026-08-05 |
| S14 | [DLA Piper — draft guidelines on high-risk AI in employment](https://knowledge.dlapiper.com/dlapiperknowledge/globalemploymentlatestdevelopments/2026/eu-commission-publishes-draft-guidelines-on-high-risk-ai-in-employment) | 2026-08-05 |
| S15 | [ABA — Voiceprints, AI and BIPA](https://www.americanbar.org/groups/litigation/resources/newsletters/class-actions-derivative-suits/voiceprints-ai-bipa-new-trends-biometric-privacy-litigation/) | 2026-08-05 |
| S16 | [Fortune — HireVue drops facial monitoring](https://fortune.com/2021/01/19/hirevue-drops-facial-monitoring-amid-a-i-algorithm-audit/) | 2026-08-05 |
| S17 | [Fast Company — auditors struggling to hold AI companies accountable](https://www.fastcompany.com/90597594/ai-algorithm-auditing-hirevue) | 2026-08-05 |
| S18 | [McDaniel et al. — validity of employment interviews, meta-analysis](https://home.ubalt.edu/tmitch/645/articles/McDanieletal1994CriterionValidityInterviewsMeta.pdf) | 2026-08-05 |
| S19 | [ClarityHire — predictive validity of hiring methods](https://clarity-hire.com/blog/predictive-validity-hiring-methods-research) | 2026-08-05 |
| S20 | [Gem — what is an applicant tracking system](https://www.gem.com/blog/applicant-tracking-system) · [best ATS 2026](https://www.gem.com/blog/best-applicant-tracking-system) | 2026-08-05 |
| S21 | [Bullhorn — ATS guide](https://www.bullhorn.com/blog/applicant-tracking-systems-guide/) | 2026-08-05 |
| S22 | [Pin — cost-per-hire benchmarks 2026](https://www.pin.com/blog/cost-per-hire-benchmarks/) | 2026-08-05 |
| S23 | [Pin — structured interviews guide](https://www.pin.com/blog/structured-interviews-guide/) | 2026-08-05 |

## 2. The field

| Vendor | Class | Who buys it | Packaging | Src |
|---|---|---|---|---|
| **Greenhouse** | Incumbent (system of record) | Head of Talent, 50–5,000 emp | Tiered by employee count: ~$6.5K/yr SMB (<50), $15–40K mid-market, $70K+ enterprise | S4, S5 |
| **Ashby** | Incumbent | Talent leaders at scaling startups | $400/mo Foundations (≤100 emp); ~$14.7K SMB avg; $30–70K at 100–300 emp; $120K+ large | S6 |
| **Lever** | Incumbent | Mid-market TA | From ~$4K/yr; **CRM, advanced analytics and EU hosting priced separately** | S4 |
| **Workday** | Incumbent (HRIS-anchored) | Enterprise HR | Enterprise contract, quote-only | S4 |
| **Bullhorn** | Incumbent (staffing-agency ATS) | Staffing agencies | Quote-only | S21 |
| **HireVue** | AI-native challenger | Enterprise TA, high-volume | Quote-only; reported $25K–$250K+/yr band | S7, S8 |
| **Paradox (Olivia)** | AI-native challenger | Hourly/high-volume hiring | Quote-only | S7, S8 |
| **Eightfold AI** | AI-native challenger | Enterprise talent intelligence | Quote-only | S7 |
| **Metaview / BrightHire** | AI-native challenger (interview intelligence) | TA ops | Quote-only | S7 |
| **Sapia / HeyMilo / ConverzAI** | AI-native challenger (chat/voice screening) | Volume recruiting, staffing | Quote-only | S7 |
| **Email + spreadsheet + shared drive** | **Adjacent horizontal — the real incumbent below ~50 employees** | Founders, office managers, first HR hire | Free | — |

**Packaging finding.** Every AI-native vendor in this table is **quote-only**; only the ATS
incumbents publish anything (S4–S6). Hidden pricing across an entire product class is itself a
finding about the buyer: this is a sales-led, procurement-mediated market, which is exactly the
opening for an open-source, self-hostable entrant with a published cost of zero.

**Second packaging finding.** Pricing is anchored to **employee count**, not to hiring volume (S4,
S5, S6) — so a 40-person company running 200 reqs a year pays SMB rates. That mismatch is where
per-requisition value accrues invisibly today.

## 3. Capability matrix

Rows are in the **industry's** vocabulary. `unknown` used freely and deliberately — a marketing page
is not a feature list.

| Capability | Greenhouse | Ashby | Lever | HireVue | Paradox | Metaview | Spreadsheet |
|---|---|---|---|---|---|---|---|
| Requisition + pipeline stages | yes (S20) | yes (S20) | yes (S20) | no | no | no | partial |
| Résumé parsing to structured fields | yes (S20) | yes (S20) | yes (S20) | unknown | unknown | no | no |
| Keyword/boolean candidate search | yes (S20) | yes (S20) | yes (S20) | no | no | no | no |
| **Structured interview scorecards** | yes (S20, S23) | yes (S20) | yes (S20) | yes (S8) | no | yes (S7) | no |
| **AI match score vs a job description** | partial (S20) | partial (S20) | unknown | yes (S8) | unknown | no | no |
| **Citation to the CV span behind a score** | unknown | unknown | unknown | unknown | unknown | unknown | no |
| AI-conducted interview (async video) | no | no | no | **yes (S8)** | no | no | no |
| AI-conducted interview (conversational/voice) | no | no | no | unknown | **yes (S8)** | no | no |
| Conversational scheduling / candidate engagement | partial | partial | partial | unknown | **yes (S8)** | no | no |
| Interview recording + auto notes | unknown | unknown | unknown | yes (S8) | no | **yes (S7)** | no |
| **Propose → human-approve → commit as a data model** | **no** | **no** | **no** | **no** | **no** | **no** | no |
| **Append-only decision audit as an exportable deliverable** | unknown | unknown | unknown | unknown | unknown | unknown | no |
| Adverse-impact / four-fifths monitoring | unknown | unknown | unknown | partial (S16, S17) | unknown | unknown | no |
| Candidate AI-use disclosure + consent capture | unknown | unknown | unknown | unknown | unknown | unknown | no |
| Time-to-fill / cost-per-hire analytics | yes (S20) | yes (S20) | partial — priced separately (S4) | unknown | unknown | unknown | no |
| Public API for job postings | unknown | **yes, no partner approval (S10)** | **yes, no partner approval (S10)** | n/a | n/a | n/a | no |
| Full data API | **yes, partner approval required (S9)** | unknown | unknown | unknown | unknown | unknown | no |
| Self-hostable / open source | no | no | no | no | no | no | n/a |

**The two rows that matter.** *Propose → approve → commit as a data model* is `no` across the entire
field, and *citation to the CV span* is `unknown` everywhere — nobody advertises it, which for a
feature this demonstrable suggests nobody has it. Those two rows are the product.

**An honest caveat on the `unknown` block.** Six of these rows are `unknown` for every vendor. That
is a real limit of desk research against quote-only vendors, not a competitive finding. Treating
`unknown` as `no` would be the single easiest way to make this matrix lie.

## 4. UX patterns

What every serious product in the category has, because users transfer expectations between them
(S20, S21):

- **Primary object: the requisition** (the "req" or "job"). Everything hangs off it.
- **Secondary object: the candidate/application**, which exists *within* a req.
- **The Kanban pipeline** — drag-and-drop stage columns (sourced → screen → interview → offer →
  hired) is the category's signature screen (S20).
- **List → detail → timeline.** The candidate profile shows parsed résumé, match score, every
  interview note, and current stage **in one view** (S20).
- **The document viewer** — the résumé, read inline, not downloaded.
- **The scorecard form** — per interview, per competency, submitted by the interviewer (S20, S23).
- **The one report people actually export:** time-to-fill and pipeline conversion by stage (S20).

**Chat-hostile parts, stated honestly:**

| Pattern | Why chat is the wrong surface |
|---|---|
| **Dragging 40 candidates between stages** | Bulk spatial manipulation. Chat makes this slower, not faster |
| **Side-by-side CV comparison** | Comparison is visual and simultaneous; a linear transcript destroys it |
| **The scorecard form itself** | A short structured form the interviewer fills in 90 seconds. Chat adds turns to a solved interaction |
| **Skimming a pipeline for "who's stuck"** | An at-a-glance density judgement over a board |

**Implication for Hireworthy:** the chat surface is right for *"screen this pile against the JD"*
and *"why did you rank #47 below #48?"* — the reasoning questions. It is wrong for pipeline
manipulation and scorecard entry, which need **tabs**. A design that pushes everything through chat
would be worse than the incumbents at the incumbents' own core loop.

## 5. Compliance constraints

Sourced from the regulator where possible (S11, S12, S13), and classified.

| Obligation | Who is bound | Requirement | Class |
|---|---|---|---|
| **NYC LL144** — annual bias audit | Employers/agencies using an AEDT for NYC roles | Independent third-party audit, **published summary**, before use and annually; up to $1,500/violation/day (S11) | **Business** (the auditor + publication) + **Product** (produce the selection-rate data) |
| **NYC LL144** — candidate notice | Same | ≥10 business days' notice before AEDT use (S11) | **Product** (notice record + timestamp) |
| **IL AIVIA** — notice & explanation | Employers using AI to analyse video interviews in IL | Notify before the interview; **explain how the AI works and what characteristics it evaluates** (S12) | **Product** (disclosure surface) + **Business** (the wording) |
| **IL AIVIA** — consent | Same | **Consent before AI analysis**; may not analyse a non-consenting applicant (S12) | **Product** (consent gate blocking the pipeline) |
| **IL AIVIA** — sharing limit | Same | No sharing except with those whose expertise/technology is necessary to evaluate (S12) | **Platform** (RBAC + tenant isolation) |
| **IL AIVIA** — deletion | Same | Delete within **30 days** of request, **including backups**, and instruct downstream recipients (S12) | **Product** (retention job) — **and in direct tension with append-only audit; see §7** |
| **EU AI Act Annex III** — high-risk | Providers *and* deployers of recruitment/selection AI | Risk management, data governance, technical documentation, record-keeping, transparency, **human oversight**, accuracy/robustness. Deployer fines to €15M or 3% turnover. **Obligations postponed to 2027-12-02** (S13, S14) | **Platform** (human oversight = approval gate; record-keeping = audit) + **Business** (conformity assessment, technical file, registration) |
| **BIPA / CUBI / WA** | Anyone collecting facial geometry or voiceprints | Written informed consent **before** collection; $1,000 negligent / $5,000 intentional per violation; **employer liable alongside vendor**; click-through ToS insufficient (S15) | **Avoided by product design** — see §7 invariant |
| **Title VII / EEOC** | Employers | Disparate-impact exposure; four-fifths rule as the screening heuristic | **Product** (impact monitoring) + **Business** (validation study, legal defence) |

**Anything in the Business column makes this run `Approval-required`,** and three obligations land
there. The platform's audit and RBAC **support** LL144 and the EU AI Act; they do not deliver
compliance with either, and writing otherwise would be the most damaging sentence in this file.

## 6. Platform mapping — table stakes one-to-one

| Platform capability | Table stake it covers | Verdict | What still has to be built |
|---|---|---|---|
| Multi-tenancy (`ITenantOwned` + query filters) | The hiring org boundary; a candidate in tenant A invisible to tenant B | **delivered** | `HasQueryFilter` **per entity** in the module `DbContext` — the platform does *not* do this for module contexts |
| RBAC before the model | Sourcer/recruiter/hiring-manager/talent-lead tiers (§ brief §8) | **delivered** | The permission strings, the `AddPlenipoRole` baselines, and a **row-level scope** for "own reqs only" |
| Approvals on every write | Maker–checker on advance/reject — **and the EU AI Act's human-oversight obligation** (S13) | **delivered** | Deciding which tools are writes; `RequiresApproval = true` on `advance_candidates` / `reject_candidate` |
| Append-only audit | "Prove who rejected this candidate, on what evidence" — LL144's and the AI Act's record-keeping | **delivered** | The **export format** a regulator or auditor actually accepts, and reconciling it with IL AIVIA deletion (§7) |
| Documents + OCR | The CV, often a scan or a 2-column PDF | **delivered** | The extraction schema for CVs and JDs |
| Scoped RAG with citations | Per-requisition retrieval, not one global candidate index | **delivered** | The collection boundary (per req), the ingestion trigger, and **citation *enforcement*** (§7) |
| Chat-first (SignalR + AG-UI) | "Screen this pile"; "why is #47 below #48?" | **delivered** | Tools it routes to + suggested prompts. **Not** the pipeline board or scorecard form (§4) |
| Jobs (manifest-declared, platform-run) | Bulk screening of 180 CVs; the 30-day deletion sweep | **delivered** | The job bodies; `Kind` must be globally unique |
| Connectors (per-tenant OAuth) | Ashby/Lever/Greenhouse; the avatar vendor | **delivered** | One connector each — **none in v1** |
| Budgets / token accounting | Cost control on a 2,000-CV month | **delivered** | Nothing |
| Admin console | User and role administration | **delivered** | Nothing — it is a fixed surface, not extensible |
| **Realtime bidirectional audio/video** | The live interview itself | **not delivered** | → §7 |
| **Unauthenticated candidate surface** | The candidate is an outsider with a one-time link | **not delivered** | → §7 |
| **Citation grounding as an invariant** | Every score points at a real span | **not delivered** | → §7 |
| **Adverse-impact computation** | Four-fifths monitoring | **not delivered** | → §7 |

## 7. Gaps — what the platform does not give you here

**Non-empty by obligation, and these are the honest ones.**

1. **Realtime bidirectional audio/video — the largest unknown in the whole project.**
   The platform ships chat over SignalR and AG-UI at `/api/agui/{moduleId}`, which is **turn-based**.
   A live spoken interview needs a streaming media transport that does not exist here, and
   *"inbound channels beyond the supported set"* is listed as **deliberately not extensible**.
   *What it would take:* a product-owned realtime transport, or a connector to a vendor that hosts
   the session and returns a transcript.
   *Can v1 ship without it?* **Yes** — v1 runs the interview as turn-based text/voice over the
   existing chat surface. That is a real interview and it is testable.
   *Does a connector remove the need?* **Largely yes**, which is the much better outcome.

2. **An unauthenticated candidate surface.** The platform assumes an authenticated tenant user, and
   the admin console is a fixed surface. A candidate is an *outsider* holding a one-time link, who
   must see a consent disclosure and answer questions without an account. **Worth an ADR** — the
   access pattern is genuinely new, and getting it wrong is a tenant-isolation incident.

3. **Citation grounding.** RAG returns citations; nothing forces a *score* to point at a span that
   really exists. This must be built and enforced as a hard check (deterministic string containment
   against the source text), and it is the product's central quality guarantee.

4. **Adverse-impact / four-fifths computation.** Arithmetic over outcomes by category. Entirely the
   module's, and it feeds the LL144 audit data (S11).

5. **Consent capture and 30-day deletion versus append-only audit.** IL AIVIA requires deletion of
   interview material within 30 days on request, including backups (S12); the platform's audit is
   append-only by design. **These are in direct tension and the resolution is design work** — most
   likely: delete the *content*, retain the *decision record* with a tombstone. That is an ADR, not
   a coding task.

6. **A validated selection instrument.** The I/O literature's validity coefficients (S18, S19)
   belong to *structured interviewing as a method*; they are not inherited by any particular
   implementation. Claiming Hireworthy's scores are "validated" without a study would be the same
   overreach observers criticised in HireVue's audit marketing (S17).

Gaps 1 and 2 are the two that could change the architecture. Everything else is module work.

### The invariant that removes the worst compliance exposure

> **Never analyse the candidate's face or voice.** Assessment runs on the **transcript only**. The
> generated avatar is on the *interviewer* side. No facial template, no voiceprint, no prosody or
> expression as a signal.

This removes almost all BIPA/CUBI/WA surface (S15), avoids what HireVue publicly retreated from in
2021 after EPIC called its facial analysis *"biased, unprovable, and not replicable"* (S16), and
makes the IL AIVIA-required "what characteristics it evaluates" disclosure (S12) a sentence a
candidate will accept. **Residual risk, stated:** speech-to-text is not voiceprinting, but audio
retention still needs a short enforced deletion window — a policy decision for a human.

## 8. Must-have / differentiator / out-of-scope

**Must-have** — present in a majority of the leaders **and** required by the killer workflow:

| Capability | Majority evidence | Needed by the workflow |
|---|---|---|
| Requisition + pipeline stages | Greenhouse, Ashby, Lever, Workday, Bullhorn (S20, S21) | The req is the container for everything |
| Résumé parsing to structured fields | Greenhouse, Ashby, Lever (S20) | Screening input |
| Structured interview scorecards | Greenhouse, Ashby, Lever, HireVue, Metaview (S20, S23) | The rubric *is* the scorecard |
| Candidate profile: résumé + score + notes + stage in one view | Greenhouse, Ashby, Lever (S20) | Where the recruiter approves |
| Time-to-fill / pipeline conversion reporting | Greenhouse, Ashby (S20) | The buyer's metric |

**Differentiator** — what the platform makes cheap that the field does badly:

| Differentiator | Who does it badly, and the evidence |
|---|---|
| **Propose → approve → commit as a data model** | `no` for **every** vendor in §3. Their stage transition is a field write, so the audit log cannot distinguish a human decision from an automated one — which is precisely what LL144 and the AI Act turn on (S11, S13) |
| **A citation behind every score** | `unknown` for every vendor (§3); nobody advertises a demonstrable feature |
| **Consent + disclosure as a first-class gate** | **82% of candidates were not told AI was used; 70% never told it would evaluate them** (S1) — a legally-required step (S12) that the market near-universally skips |
| **Audit trail as an exportable deliverable** | `unknown` everywhere; and the independent-audit industry itself has been criticised as immature (S17) |
| **Adverse-impact monitoring from day one** | `unknown`/`partial`; HireVue's was contested (S16, S17) |
| **Open-source and self-hostable** | `no` for the entire field (§3), in a market where every AI-native vendor hides its price |

**Out-of-scope for v1**, each with its reason:

| Excluded | Reason |
|---|---|
| Rendered video avatar | Gap 1 (no realtime transport) + K12 in the brief: it is a commodity vendor call and must carry no product claim. Ships behind a connector seam later |
| Any ATS connector | Greenhouse needs **partner approval** and its v1/v2 API is deprecated after **2026-08-31** (S9) — a schedule risk someone else controls. Ashby/Lever public APIs (S10) are v2 |
| Calendar scheduling | Somebody else's system of record → connector, later |
| Sourcing / outbound / CRM | A different product (Gem, HireEZ). Not the killer workflow |
| Offer management, onboarding, HRIS sync | Downstream of the decision this product exists to make; Workday's system of record |
| The published LL144 bias audit | **Business class** (§5) — an act by an independent third party. v1 produces the *data*; it cannot produce the audit |
| Multi-language interviewing | Real demand, no evidence gathered. Deferred honestly rather than guessed |
| Pixel-precise CV annotation | Chat-hostile (§4) and not needed by the workflow |

## 9. Open questions for the spec

1. **Does v1's interview run over the existing AG-UI turn-based surface, or wait for a transport?**
   (Gap 1.) Recommendation to test in the spec: yes, turn-based, and prove it end to end before any
   media work.
2. **How does an unauthenticated candidate reach the system safely?** (Gap 2.) One-time signed link,
   scoped to a single application, expiring — but the tenant-isolation implications need an ADR.
3. **How is IL AIVIA deletion reconciled with append-only audit?** (Gap 5.) Content deletion plus a
   decision tombstone is the likely answer; it needs to be decided, not assumed.
4. **Is the rubric versioned per requisition?** If a JD is edited mid-pile, every prior score used a
   different yardstick. Almost certainly yes, which makes the rubric an entity with a version.
5. **Where do candidate demographics come from for four-fifths monitoring** — self-reported EEO
   fields, and what happens when they are absent (which is common and lawful)?
6. **One module or two?** Screening and interviewing share the rubric and the candidate. Default per
   the plan loop is exactly one; the spec should state why one is right rather than inherit it.
7. **Does a rejected candidate get to see their evidence?** A differentiator with real support cost,
   and a GDPR Art. 22 question in the EU.

---

**Terminal state: `Approval-required`** — three obligations land in the **Business** column (§5): the
LL144 independent auditor and published summary, the EU AI Act conformity assessment and technical
file, and the audio-retention policy. Those are human decisions, not engineering ones. Level **L4**.
