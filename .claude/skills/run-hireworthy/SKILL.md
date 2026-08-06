---
name: run-hireworthy
description: Run, observe and prove a change in the Hireworthy product — the AppHost command, dev-auth headers, the keyless Mock provider, the AG-UI event contract, and the five-rung test ladder. USE FOR: booting the stack, checking the hiring module actually loaded, exercising the approval gate, deciding what evidence a change needs. DO NOT USE FOR: what the product is or should be (SPEC.md, PLAN.md), the module boundary and ADRs (ARCH.md, DECISIONS.md), or platform-wide contracts (/harness:plenipo-runbook).
---

# Run Hireworthy

The full contract is [`RUNBOOK.md`](../../../RUNBOOK.md). This skill is the index that makes it
findable — read the runbook before running anything non-trivial.

## Boot it

```bash
dotnet run --project src/Hireworthy.AppHost
```

No API keys — Mock provider, Mock embedder, dev auth. Postgres is `pgvector/pgvector:pg17` on the
**pinned** host port **15434**.

## Is it up?

```bash
curl -s http://localhost:5000/alive
curl -s http://localhost:5000/api/platform/modules \
  -H 'X-Dev-Subject: dev-user' -H 'X-Dev-Tenant: dev' -H 'X-Dev-Roles: system_admin'
```

The second must list `hiring`. **A module that never loads compiles perfectly**, so the build says
nothing about this.

If the dashboard is up but the API is silently absent, the startup exception is in
`%TEMP%/aspire-dcp*/`*`_err`, not on stdout.

## The ladder

```bash
dotnet build Hireworthy.slnx                    # 1 — compiles, warnings are errors
dotnet test tests/Hireworthy.Hiring.Tests       # 2 — manifest, permissions, tenant filters
dotnet test tests/Hireworthy.IntegrationTests   # 3+4 — real host, real Postgres, evals
```

Rung 5 is a real request or the UI. `hireworthy.http` has one request per endpoint.

## Before you claim done

- Say which rung your evidence is on. "It looks right" is **L4**.
- A regression test must be **seen red before the fix and green after**.
- End in one named state: `Success`, `No-op`, `Blocked`, `Stalled`, `Exhausted`, `Approval-required`.

## Never weaken these to unblock yourself

No biometric assessment · no claim to predict job performance · every state-changing tool gated in
**both** registration sites · every `ITenantOwned` entity declares its own `HasQueryFilter`.
