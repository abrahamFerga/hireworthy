---
description: 'Authoring rules for the Hireworthy domain module — tool registration, tenant isolation, the approval gate, and the seams that must not be rebuilt.'
applyTo: 'src/**/*.cs'
---

# Writing module code

Depth is in [`../../AGENTS.md`](../../AGENTS.md) and
[`../../DECISIONS.md`](../../DECISIONS.md). These are the rules that apply to every file under
`src/`.

## Registering a tool takes two edits, not one

A tool needs a `ToolDescriptor` in `HiringModule.Manifest.Tools` **and** a `ModuleTool` in
`HiringToolSource.GetTools`, carrying the **same** permission string — always built with
`Permissions.ForTool(ModuleId, name)`, never hand-written.

Miss either and the tool is silently never callable. Mismatch the strings and it 403s even for
`system_admin`. Neither raises an error anywhere. `GET /api/admin/security/catalog` shows the truth.

## Writes are approval-gated, in both places

`RequiresApproval = true` on the descriptor **and** the `ModuleTool`. A gated write must **throw**
rather than return a string when it cannot complete: a returned string tells the platform the tool
ran to completion, so it resolves the approval as `Executed` with `error: null`. Telling someone a
candidate was advanced when they were not is the failure this product exists to prevent.

Read tools do the opposite — return guidance rather than throwing, so the model can retry.

## Tenant isolation is per entity

Every `ITenantOwned` entity needs its own `entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId)`
in `HiringDbContext.OnModelCreating`. `PlatformDbContext` does this by reflection; a module context
does not. `ManifestGuardTests` fails the build if you add an entity without one — do not weaken that
test to unblock yourself.

The module context derives from **`ModuleDbContext`**, never `DbContext`, or `CreatedAt`/`UpdatedAt`
persist as `default`.

## Do not rebuild what the platform ships

Permission checks, audit trails, "are you sure?" confirmations, tenant filtering in queries, chat
endpoints, job schedulers, file stores, OCR, vector stores, role editors, token budgets, secret
stores, OAuth dances. Each has a seam. Rebuilding one is the most expensive mistake available here.

## Product invariants

Never analyse a candidate's face or voice. Never claim to predict job performance. Both live in the
manifest's `AgentInstructions` and are guarded by a test — a change to prompt-shaped assets also
needs a golden eval case.
