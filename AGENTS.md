# hireworthy

**Hireworthy** — evidence-first hiring on the **Plenipo** platform (.NET 10 + Aspire + React).
The assistant proposes; a named human decides; every decision carries its evidence.

Read [RUNBOOK.md](RUNBOOK.md) before running or testing anything. It is the source of truth for
how to run this repo and prove a change works.

## Build and test

```bash
dotnet build Hireworthy.slnx                                   # warnings are errors
dotnet test  tests/Hireworthy.Hiring.Tests                     # module guard, no Docker needed
dotnet test  tests/Hireworthy.IntegrationTests                 # boots the real host (needs Docker)
dotnet run   --project src/Hireworthy.AppHost                  # the whole stack
```

**`dotnet build` proves nothing.** A module that never loads compiles perfectly. Exercise the change
through a real request or the UI, then lock it in with a test that fails without the fix.

## Repository layout

```text
src/Hireworthy.AppHost/     Aspire orchestration. References the Host and NOTHING else.
src/Hireworthy.Host/        The product. ONE package reference. Program.cs is the seam list.
src/Hireworthy.Hiring/      The ONLY real code: IModule + manifest + tools + Persistence/.
tests/Hireworthy.Hiring.Tests/       Module guard: manifest integrity, tool parity, tenant filters.
tests/Hireworthy.IntegrationTests/   Testcontainers pgvector + the real host.
```

There is no `Domain`/`Application`/`Infrastructure` split and no `ServiceDefaults` project. That is
deliberate: the platform is the foundation, and those layers are files inside the one module.

## Product invariants — never weaken these to unblock yourself

1. **No biometric assessment, ever.** No facial geometry, no voiceprint, no analysis of appearance,
   accent, or tone of voice. Assessment is on the CV text and the interview transcript only. This is
   what keeps the product out of BIPA/CUBI/Washington biometric exposure entirely, and it is the
   disclosure candidates are shown.
2. **No claim to predict job performance.** The product evidences and cites; it does not forecast.
3. **Every state-changing tool is approval-gated in BOTH places** — the manifest `ToolDescriptor`
   and the `ModuleTool`. The runner unions the flags, so setting one and reviewing only that one
   hides a broken gate.
4. **Every `ITenantOwned` entity declares its own `HasQueryFilter`.** One employer seeing another's
   candidates is a data-protection incident. `ManifestGuardTests` fails the build without it.
5. **A rubric is frozen and versioned before anyone is scored against it.** Scores pin a version.

## Facts verified against source — do not contradict these

The Plenipo platform's own documentation is wrong in places, so the trust ranking is
**source > tests > `.http` catalog > platform docs > product docs**.

- The host API is `builder.AddPlenipoPlatform()` / `app.UsePlenipoPlatform()`.
  `BUILDING_A_PRODUCT.md` documents `AddPlenipo()` / `UsePlenipo()` — **those do not exist.**
- **Platform packages are not on nuget.org.** They are vendored into `.packages/` and pinned by
  `packageSourceMapping` in `nuget.config` — a dependency-confusion guard, not a formality.
- Postgres must be **`pgvector/pgvector`** — the RAG migration creates a vector column at startup.
- A module tool's permission is `tools.<module>.<tool>`, built with `Permissions.ForTool`, and it
  appears in **two** places. `GET /api/admin/security/catalog` shows the truth.

### Four ways this product compiles cleanly and is dead at runtime

Each was paid for once already, in a sibling product. Do not rediscover them.

1. **`app.UsePlenipoPlatform(); app.Run();` does not apply migrations.** Use
   `await app.RunPlenipoPlatformAsync()` — it is `UsePlenipoPlatform()` + `InitializePlenipoAsync()`
   + `RunAsync()`. Without it the app serves 500s forever and the job processor logs
   `42P01: relation "platform.background_jobs" does not exist`, which reads like a job bug and is not.
2. **`IModuleToolSource` must be registered SINGLETON.** The platform's `IToolRegistry` is a
   singleton that consumes it; a scoped registration fails DI validation at startup and takes six
   other platform services down with it. That is why `GetTools` takes the scoped `IServiceProvider`
   as a parameter.
3. **The AppHost must `WaitFor(postgres)`, not the two database resources.** A database resource's
   health check connects to that database by name, and only this API's own initializer creates them
   — waiting on the databases is a circular wait. Symptom: containers healthy, dashboard up, API
   silently absent.
4. **`IModule.MigrateAsync` is a defaulted interface member.** Leaving it unimplemented compiles,
   passes every manifest guard, and boots — then fails with
   `42P01: relation "hiring.requisitions" does not exist` on the first real request. Implement it,
   and call `MigrateAsync`, never `EnsureCreatedAsync` (the database already exists, so
   `EnsureCreatedAsync` returns false and creates no tables at all).

## Never edit the platform from this repo

If Plenipo is missing something, climb the escalation ladder (is it already there? does a product
seam cover it? can a local shim carry it?) and only then file a platform request. Apply the shim
first, tagged `TODO(plenipo#N)`, so this repo is never blocked.

Same rule for the `plenipo-agents` marketplace: if a skill is wrong or stale, record the correct
fact here so you are never blocked, then file one issue there.

## How work is judged here

Every claim of "done" is graded L1–L5 and you must say which level you are on: L1 deterministic
(an exit code decided it) · L2 rule/linter · L3 delayed field truth · L4 **model as judge — your
opinion** · L5 human checkpoint. **Never report an L4 conclusion with L1 confidence**, and **prove
the verifier** — a new check must be seen red before the fix and green after.

End work in exactly one named state: `Success`, `No-op`, `Blocked`, `Stalled`, `Exhausted`, or
`Approval-required`. **An error or an exhausted budget never counts as success.**
