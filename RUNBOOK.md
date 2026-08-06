# Hireworthy — runbook

**How to run this, and how to prove a change works.** This file is the source of truth for both.
`dotnet build` proves the code is well formed and nothing else.

## Run it

```bash
dotnet run --project src/Hireworthy.AppHost
```

No API keys. The assistant uses Plenipo's **Mock** provider and the RAG pipeline uses the
deterministic **Mock** embedder, so a bare clone works. `aspire run` gives the same stack with the
telemetry dashboard.

| Thing | Value |
|---|---|
| API | `http://localhost:5000` (Aspire assigns; the Postgres port is the pinned one) |
| Postgres host port | **15434**, pinned — not 15432 (Networthy) or 15433 (Auditworthy) |
| Postgres image | **`pgvector/pgvector:pg17`** — never stock `postgres` |
| Data volume | `hireworthy-pg-data` |
| Dev password | `hireworthy-dev-only`, fixed on purpose |
| Databases | `plenipo-platform` and the separate append-only `plenipo-audit` |

**Never unpin the Postgres host port to resolve a conflict.** Two AppHosts mounting one data volume
destroy the cluster; the bind failure is the guard working.

## Dev auth

Every authenticated request needs three headers. Development only.

```bash
curl -s http://localhost:5000/api/platform/modules \
  -H 'X-Dev-Subject: dev-user' -H 'X-Dev-Tenant: dev' -H 'X-Dev-Roles: system_admin'
```

Roles worth using: `hiring-sourcer` (proposes, approves nothing), `hiring-recruiter`,
`hiring-manager`, `hiring-talent-lead`, `hiring-compliance` (impact report only).

## Is it actually up?

`GET /alive` returns 200 and never calls the model — it is the safe readiness poll.

**A green build is not a running app.** The module loading is the thing to check:

```bash
curl -s http://localhost:5000/api/platform/modules -H 'X-Dev-Subject: dev-user' \
  -H 'X-Dev-Tenant: dev' -H 'X-Dev-Roles: system_admin' | grep hiring
```

If the API is silently absent while the dashboard is up, read
`%TEMP%/aspire-dcp*/`*`_err` — the host's startup exception lands there, not on the AppHost's
stdout. See ADR-0010.

## The test ladder

| Rung | Command | What it catches | Level |
|---|---|---|---|
| 1 | `dotnet build Hireworthy.slnx` | it compiles, warnings are errors | L1 |
| 2 | `dotnet test tests/Hireworthy.Hiring.Tests` | manifest integrity, tool/permission parity, **per-entity tenant filters** | L1 |
| 3 | `dotnet test tests/Hireworthy.IntegrationTests` | the real host on a real Postgres: migrations, seeding, RBAC, the approval gate, the AG-UI protocol | L1+L3 |
| 4 | the `Evals/cases/*.json` inside rung 3 | prompt-shaped regressions — instructions and tool descriptions | L3 |
| 5 | a real request or the UI | that the feature does what the issue asked | L3 |

Rungs 2 and 3 need no secrets. Rung 3 needs Docker.

**Rung 4's limit, stated up front:** the Mock provider selects a tool by matching name tokens, not by
reasoning. The eval cases prove the contract *around* the model — the right tool is reachable, an
unpermitted one is never offered, a write parks, and the reply does not claim a parked write
happened. They prove nothing about answer quality. Do not add a case only a real model could pass.

## Proving a change

1. Reproduce through the narrowest surface that shows it.
2. Diagnose from telemetry before source.
3. Fix one variable per turn.
4. **Lock it in with a test seen red before the fix and green after.** A test never seen red may be
   asserting nothing.

Then say which rung your evidence is on. "I read it and it looks right" is **L4** — say so rather
than implying something ran.

## Product invariants you may not weaken to unblock yourself

- **No biometric assessment.** No facial geometry, no voiceprint, no appearance, accent or tone of
  voice, at any point. Transcript and CV text only. (ADR-0001)
- **No claim to predict job performance.** (ADR-0002)
- **Every state-changing tool is approval-gated in both places** — the manifest descriptor and the
  `ModuleTool`. The runner unions the flags.
- **Every `ITenantOwned` entity declares its own `HasQueryFilter`.** One employer seeing another's
  candidates is a data-protection incident. `ManifestGuardTests` fails the build without it.

## Four ways this compiles cleanly and is dead at runtime

Recorded in `AGENTS.md` in full. In short: use `RunPlenipoPlatformAsync()` not
`UsePlenipoPlatform()`+`Run()`; register `IModuleToolSource` **singleton**; `WaitFor(postgres)` not
the database resources; and implement `IModule.MigrateAsync` (it is a defaulted member, so omitting
it compiles and boots, then 42P01s on the first request). Plus ADR-0010: the AppHost must propagate
`ASPNETCORE_ENVIRONMENT` explicitly.

## Resetting

Dev data is throwaway. If Postgres will not open its volume, or the major changed:

```bash
docker volume rm hireworthy-pg-data
```

## Request catalog

[`hireworthy.http`](hireworthy.http) — one request per endpoint, with the dev-auth headers.
