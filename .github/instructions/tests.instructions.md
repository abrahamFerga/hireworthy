---
description: 'Rules for Hireworthy test code — which client proves a security property, what a golden eval may assert, and why a test never seen red asserts nothing.'
applyTo: 'tests/**/*.cs'
---

# Writing tests

The ladder and the commands are in [`../../RUNBOOK.md`](../../RUNBOOK.md). These rules apply to
every file under `tests/`.

## A test never seen red asserts nothing

Before claiming a regression test works, **break the thing it guards and watch it fail**, then
restore and watch it pass. A check that has only ever been green may be asserting nothing at all.
State that you did this; it is the difference between an L1 claim and an opinion.

## Use the right client, or the test proves the opposite of what you think

- **`fixture.AdminClient(...)`** goes through the real pipeline. It is the **only** way to prove
  RBAC, the approval gate, or the AG-UI protocol. Pass a narrower role to assert a 403.
- **`fixture.AuthorizedScopeAsync()`** deliberately bypasses RBAC and the approval gate — it is how
  a tool runs *after* the platform has done its part. A security-shaped assertion written against it
  will pass while the gate is broken.

If the test says anything about permissions, gating, or roles, it goes through `AdminClient`.

## Golden evals prove the contract, not the answer

The assistant runs on Plenipo's **Mock** provider, which selects a tool by matching name tokens in
the message rather than by reasoning. Cases in `Evals/cases/*.json` may assert:

- the right tool is reachable, and an unpermitted one is never offered;
- a write parks on the gate;
- the reply does not claim a parked write happened.

They may **not** assert answer quality. A case only a real model could satisfy is a flake, and a
flaky harness teaches the next agent to delete the whole thing.

## Keep the fixture honest

`pgvector/pgvector:pg17` — never stock `postgres`, and keep the major in sync with the AppHost. A
product that runs on pg17 and tests on pg16 is testing something it does not ship.
