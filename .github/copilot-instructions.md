# Copilot instructions — Hireworthy

**The source of truth is [`AGENTS.md`](../AGENTS.md); read it.** This file exists because
github.com Chat is the one surface that reads *only* this file — so it carries the minimum needed
to be useful standalone, and **deliberately does not duplicate `AGENTS.md`**. Do not "sync" the two:
no tool defines which wins, so a contradiction resolves nondeterministically.

## What this is

Hireworthy is an evidence-first hiring system on the **Plenipo** platform (.NET 10 + Aspire). The
assistant proposes; a named human decides; every decision carries the evidence behind it. All the
real code is one domain module, `src/Hireworthy.Hiring` — the host is a ~20-line seam list and the
platform supplies auth, tenancy, RBAC, approvals, audit, jobs, chat, documents, OCR and RAG.

## Verify a change

```bash
dotnet build Hireworthy.slnx                    # warnings are errors
dotnet test tests/Hireworthy.Hiring.Tests       # manifest, permissions, tenant filters
dotnet test tests/Hireworthy.IntegrationTests   # real host on a real Postgres (needs Docker)
```

**`dotnet build` proves nothing.** A module that never loads compiles perfectly. The full run/prove
contract, including the boot-killers that compile cleanly and are dead at runtime, is in
[`RUNBOOK.md`](../RUNBOOK.md).

## The rules that catch most mistakes here

1. **Never analyse a candidate's face or voice.** Assessment is on CV text and interview transcript
   only — no facial geometry, voiceprint, appearance, accent or tone of voice. This is a product
   invariant with a legal basis, not a preference.
2. **Never claim to predict job performance.** The product evidences and cites; it does not forecast.
3. **Every state-changing tool is approval-gated in BOTH places** — the `ToolDescriptor` in the
   manifest and the `ModuleTool` in the tool source. The runner unions the flags, so setting one and
   reviewing only that one hides a broken gate.
4. **Every `ITenantOwned` entity declares its own `HasQueryFilter`.** `PlatformDbContext` applies
   filters by reflection; a module context does **not**. A missing filter is a silent cross-tenant
   leak — here that means one employer seeing another employer's candidates.

## Reviewing a pull request

A PR must carry a `Closes #<n>`, a **## Runtime evidence** section showing the change exercised
through a real request, and a **## Regression test** section stating the test was seen **red before
the fix and green after**. `.github/scripts/pr-gates.mjs` enforces this as a required check — if a
diff touches tenant filters, approval flags, permission grants or CI itself, it needs the
`human-approved` label.

**These instructions are advisory.** Anything that must be enforced lives in CI or a gate script.
