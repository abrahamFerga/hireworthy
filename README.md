# Hireworthy

**The free, open-source AI-first hiring system that has to show its work.** Requisitions,
evaluation rubrics, evidenced CV screening and structured interviews — where the assistant
*proposes* and a named human *decides*, and every decision carries the evidence behind it.

Built on the [Plenipo](https://github.com/abrahamFerga/Plenipo) platform (.NET 10 + Aspire + React).

## What it does

A recruiter opens a requisition with 180 applicants and says *"screen this pile against the job
description."* Hireworthy extracts the evaluation rubric **once** from the JD, a human approves it,
and then every applicant is scored against that same frozen yardstick — with each criterion citing
a verbatim span from that candidate's own CV. The recruiter approves the shortlist. Nobody is
advanced or rejected without a named human deciding it.

## What it will not do

These are product invariants, not settings:

- **It never analyses a candidate's face or voice.** Assessment runs on the transcript and the CV
  text only. No facial geometry, no voiceprint, no prosody or expression as a signal. This removes
  essentially all biometric-privacy exposure, and it avoids the pseudoscience the category
  publicly retreated from.
- **It never predicts job performance.** That claim is not checkable on a useful timescale. What it
  claims instead is that screening is evidenced, cited, consistent and human-approved — all of
  which are checkable, three of them deterministically.
- **It never decides.** Advancing and rejecting are approval-gated writes. The assistant proposes.
- **It never scores a candidate who has not consented**, and it tells them what is evaluated first.

## Status

**Epic 1 — requisitions and the approved rubric.** The module loads, `list_requisitions` and
`get_requisition` read real data, `propose_rubric` parks on the approval gate, and the Requisitions
tab renders. CV intake, cited screening, the shortlist decision, the consent surface, the adaptive
interview and the adverse-impact report are epics 2–7. See [PLAN.md](PLAN.md).

## Run it

```bash
dotnet run --project src/Hireworthy.AppHost
```

No API keys required — the assistant uses Plenipo's Mock provider and the RAG pipeline uses the
deterministic Mock embedder. See [RUNBOOK.md](RUNBOOK.md) for the full contract, dev-auth headers
and the test ladder.

## Documents

| File | What it holds |
|---|---|
| [SPEC.md](SPEC.md) | What the product is: jobs, personas, capabilities, RBAC, regulatory constraints |
| [PLAN.md](PLAN.md) | Epics in build order, tool inventory, permissions, open questions |
| [ARCH.md](ARCH.md) | The shape: module boundary, data model, the decisions and their reasons |
| [RUNBOOK.md](RUNBOOK.md) | How to run it and prove a change works |
| [research/](research/) | The industry research the spec was written from |

## Licence

MIT. See [LICENSE](LICENSE).
