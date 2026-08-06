# Hireworthy — decision record

Each entry passed all three ADR tests: a real alternative existed, the choice is **not** implied by
a platform invariant, and someone reading the code in six months would ask *"why is it like this?"*

Newest last. See the bottom for what was **considered and is not an ADR**.

---

## ADR-0001 — Never analyse a candidate's face or voice

**Status:** Accepted · 2026-08-05

**Context.** The category does this. Video-interview vendors have scored facial expressions, eye
contact and vocal prosody, and the user's original request was for a generated avatar conducting
interviews — which invites analysing the candidate's video too. There is a real alternative and it
is the market default.

Two things argue against it. **Legally**, facial geometry and voiceprints are biometric identifiers
under Illinois BIPA (and Texas CUBI, Washington HB 1493): written informed consent before
collection, $1,000 negligent / $5,000 intentional **per violation**, a private right of action, and
**the employer is liable alongside the vendor**. Click-through terms do not satisfy it.
**Empirically**, HireVue dropped facial analysis in January 2021 after EPIC's FTC complaint called
it *"biased, unprovable, and not replicable."*

**Decision.** Hireworthy **never** analyses a candidate's face or voice. Assessment runs on the CV
text and the interview transcript, against the approved rubric. The generated avatar, when it ships,
is on the **interviewer** side only. No facial template is built, no voiceprint extracted, no
prosody or expression used as a signal. This is a product invariant, not a configuration flag.

**Consequences.** Removes essentially the entire biometric-privacy surface rather than managing it.
Makes the IL AIVIA-required *"what characteristics it evaluates"* disclosure a sentence a candidate
will actually accept — *"what you said, not how you looked."* Forecloses "engagement" and
"enthusiasm" scoring some buyers will ask for; the answer is no, with this ADR as the reason.
Residual risk, stated: speech-to-text is not voiceprinting, but retained audio still needs a short
enforced deletion window — see ADR-0008.

---

## ADR-0002 — Never claim to predict job performance

**Status:** Accepted · 2026-08-05

**Context.** The obvious product claim is *"we predict who will succeed."* It is also the claim that
cannot be checked: ground truth arrives 12–24 months later, is confounded by manager and team, and
you never observe the counterfactual for rejected candidates. A prior scouting pass rejected this
whole vertical partly on that basis — *"'was this a good candidate' has no checkable answer."*

**Decision.** Hireworthy claims its screening is **evidenced, cited, consistent and human-approved**.
It does not forecast job performance, and the module instructions forbid the model from doing so.

**Consequences.** The verifier becomes real and mostly deterministic: citation containment (L1),
re-run consistency (L1), four-fifths ratios (L1), a labelled extraction set (L2), and the approval
gate emitting a labelled datapoint on every decision (L3). Weaker marketing, defensible product.
The industrial-organisational literature's validity coefficients belong to *structured interviewing
as a method* and are **not** inherited by this implementation — citing them as if they were would be
the same overreach observers criticised in the HireVue audit marketing.

---

## ADR-0003 — The rubric is a versioned entity, frozen before anyone is scored

**Status:** Accepted · 2026-08-05

**Context.** The obvious implementation scores each CV against the job description directly. The
alternative is to extract criteria once, freeze them, and pin every score to that version.

**Decision.** `Rubric` is an entity with a `Version`, unique per requisition and never reused.
`propose_rubric` is **approval-gated**; no applicant may be scored against a rubric in `Proposed`.
Editing a job description mid-pile produces a **new version** that supersedes rather than mutating.

**Consequences.** This is why scores across 180 applicants are comparable and why *"why was #47
rejected and #48 advanced?"* has an answer. Costs an extra approval step per requisition and a
supersession path. Without it the yardstick drifts down the pile and the whole defensibility claim
collapses — which is the difference between this product and a per-CV prompt.

---

## ADR-0004 — `screen_applicants` and `parse_cv` write without an approval gate

**Status:** Accepted · 2026-08-05 · **Deviation from a stated rule — recorded deliberately**

**Context.** SPEC.md's own rule is that every state-changing tool is approval-gated, with no
low-risk-write exception. Both these tools write rows. Gating `screen_applicants` would mean a human
approving 180 score records before reading any of them, which makes the gate ceremony — and a gate
people click through is worse than no gate, because it launders the decision.

`networthy` set the fleet precedent with exactly one ungated write behind its own ADR-0005.

**Decision.** `parse_cv` and `screen_applicants` write **proposals only** and are ungated. Neither
changes a candidate's stage, and nothing they write has any effect until `advance_candidates` or
`reject_candidate` — both gated — commits a decision. `ScreeningResult.Status` is `proposed` and
there is no code path that reads a proposal as a decision.

**Consequences.** The gate stays meaningful because it sits at the one place a person's outcome
changes. The load-bearing constraint this creates: **no future tool may read a `proposed` result as
authoritative**, and any tool that would must be gated instead. `hiring-sourcer` deliberately holds
`screen_applicants` and can approve nothing, which is what proves the lane is real.

---

## ADR-0005 — Citation grounding is a deterministic, build-breaking check

**Status:** Accepted · 2026-08-05

**Context.** The platform's RAG pipeline returns citations, but nothing forces a *score* to point at
a span that exists. The alternative is to trust the model and spot-check — which is what "AI resume
screening" generally does.

**Decision.** Every `CriterionScore` stores `citationText` plus start/end offsets into
`CvDocument.extractedText`. A test asserts, for every score in the golden set, that the cited span
occurs **verbatim** at those offsets. It runs in CI and breaks the build. Target: 100%.

**Consequences.** The product's central guarantee becomes an exit code rather than an opinion — L1,
not L4. A model that paraphrases a CV instead of quoting it fails CI rather than shipping a
plausible fabrication into an employment decision. Costs a golden set that must be maintained as
extraction changes, and constrains prompt work: the model must quote, not summarise.

---

## ADR-0006 — "Own requisitions only" is a module query filter, not a permission string

**Status:** Accepted · 2026-08-05

**Context.** A hiring manager may advance candidates on **their own** requisitions and not on other
people's. The platform's RBAC is permission-string based and has no row-level concept. Options were
a permission convention (`tools.hiring.advance_candidates:own`), a platform request, or a filter in
the module.

**Decision.** A module-level filter on `Requisition.OwnerUserId`, applied **in addition to** the
tenant query filter, for roles holding `hiring-manager` and not `hiring-talent-lead`. No new
authorization concept is invented; the permission string stays flat.

**Consequences.** Keeps the platform's model intact and avoids a parallel scheme nobody else
understands. The cost is that the restriction lives in module code rather than in the admin console,
so it is invisible where an operator would look for it — recorded here for that reason. Revisit as a
platform request if a second product needs row-scoped grants.

---

## ADR-0007 — The candidate reaches the system anonymously, by signed one-time token

**Status:** Accepted · 2026-08-05

**Context.** A job applicant is an **outsider**: no account, no tenant membership, no role. They must
see the AI-use disclosure and answer interview questions. Every platform surface assumes an
authenticated tenant user, and the admin console is explicitly not extensible.

**Decision.** A module endpoint `/api/hiring/interview/{token}` mapped **without**
`RequireAuthorization`. The token is signed, single-use, scoped to exactly one `InterviewSession`,
and expires (`hiring.invite-expiry`, hourly). It resolves tenant and applicant server-side; the
candidate never selects a tenant, and no other module data is reachable through it.

**Consequences.** This is the **one** unauthenticated write path in the product, and therefore the
highest-risk surface in the repo — a token-scoping bug is a cross-tenant incident, not a bug. It
must be reviewed as such: never widen the token's scope, never accept a tenant id from the request,
and treat any change to this endpoint as spine-touching regardless of autonomy level.

---

## ADR-0008 — IL AIVIA deletion removes content and keeps a decision tombstone

**Status:** Accepted · 2026-08-05

**Context.** 820 ILCS 42 requires deleting a candidate's interview material within **30 days** of
request, including backups, and instructing downstream recipients to do the same. The platform's
audit database is **append-only by design**. These are in direct tension, and both are real
obligations — a hiring decision must remain provable to a regulator or a tribunal.

**Decision.** The daily `hiring.retention-sweep` deletes **content** — `InterviewTurn` text, stored
audio, `CvDocument.extractedText` — and retains a **tombstone**: the `Decision` row with its kind,
approver, timestamp, rubric version, and a hash of the evidence that existed. The append-only audit
entry is never rewritten; it records that a deletion occurred and when.

**Consequences.** Satisfies the deletion duty for the personal data while preserving the
accountability chain the product exists to produce. A tombstoned decision can be shown to have been
made by a named human against a named rubric, but its underlying evidence can no longer be
re-examined — an honest trade, and the candidate's own request caused it. **The retention window
itself is a policy decision a human must sign off**, not an engineering default.

---

## ADR-0009 — v1's interview runs over the turn-based AG-UI surface; no media transport

**Status:** Accepted · 2026-08-05

**Context.** The user's original request was a generated video stream with voice. The platform's
chat transports are turn-based, inbound channels beyond the supported set are *deliberately not
extensible*, and a live spoken interview needs a streaming media transport that does not exist here.
The alternative was to block epic 6 until one exists, or build a product-owned transport.

**Decision.** v1 conducts the structured interview turn-based over the existing AG-UI surface —
a real interview with real adaptive follow-up probing, provable end to end. The rendered avatar
ships later **behind a connector seam** to a vendor that hosts the session and returns a transcript.

**Consequences.** Epic 6 is buildable now instead of blocked on the largest unknown in the project,
and the reasoning — the interviewer/evaluator split, the adaptive probes — ships first, which is the
part that is actually hard. It also matches the evidence: the single most-rejected interview format
in the candidate-experience data is *pre-recorded, AI-scored, with no live interviewer*, so shipping
the avatar first would have built the exact artifact candidates walk away from. The avatar carries
no product claim (ADR-0001 constrains it to the interviewer side).

---

## ADR-0010 — The AppHost propagates its environment to the API explicitly

**Status:** Accepted · 2026-08-05

**Context.** Aspire launches the API with `--no-launch-profile`, so the API never reads its own
`launchSettings.json` and inherits only what the AppHost passes. With nothing passed it runs as
**Production**, and `AddPlenipoPlatform()` throws *"Plenipo authentication is not configured"* before
serving a request. The symptom is silent from every place you would look: containers healthy,
dashboard up, *"Distributed application started"* printed, and the API simply absent — the exception
lands in a DCP per-resource log under `%TEMP%/aspire-dcp*/`. This cost a real debugging cycle during
scaffolding, and it was initially misdiagnosed as a Postgres password failure.

**Decision.** `AppHost.cs` passes `.WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)`
to the API resource, and the AppHost's own `launchSettings.json` sets `Development`. Reading the
AppHost's environment rather than hardcoding `"Development"` is deliberate.

**Consequences.** `dotnet run --project src/Hireworthy.AppHost` works on a bare clone. Hardcoding
`"Development"` would have been shorter and would have silently forced dev-auth in a real
deployment — the reason for the indirection. Recorded in `AGENTS.md` alongside the four inherited
boot-killers.

---

## Considered, and deliberately **not** an ADR

These are the platform, restated. Recorded here so the next agent does not re-litigate them:

- ASP.NET Core, EF Core 10, Npgsql, Postgres, React/Vite/Tailwind — the platform's stack.
- Multi-tenancy, `ITenantOwned`, global query filters — a platform invariant.
- RBAC before the model; tools filtered before the request is built — a platform invariant.
- Approval-gated writes as a *mechanism* — a platform invariant. **Which** tools are gated is a
  decision, and it lives in ARCH.md §3, not here.
- The append-only audit database — the platform's.
- OpenTelemetry, the Aspire dashboard, the job processor, `ISecretVault` — the platform's.
- **One module** — decided in PLAN.md §3 with its reasoning (OQ8). Not re-argued.
