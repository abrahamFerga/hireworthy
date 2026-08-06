# Tick journal

The loop's memory. The conversation is not: compaction erases it, and the next tick is usually a
fresh session. One line per tick, appended, never rewritten.

```text
2026-08-06T02:26Z · deliver · preflight · — · Blocked · no RUNBOOK.md · next: /deliver:install-runbook
2026-08-06T02:30Z · install-runbook · — · — · Success · RUNBOOK.md + fixture + 3 evals + .http + run skill · L1 13/13 integration
2026-08-06T02:31Z · deliver · rule 4 · #10 · Blocked-cleared · preflight green, #10 selected · handoff deferred to next firing
```
