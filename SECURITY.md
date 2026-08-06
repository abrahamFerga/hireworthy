# Security

## Reporting a vulnerability

Open a [security advisory](https://github.com/abrahamFerga/hireworthy/security/advisories/new).
Please do not open a public issue for a vulnerability.

## What is especially sensitive here

This product holds **job applicants' personal data** and makes **employment decisions**. Two classes
of defect matter more here than they would elsewhere:

1. **Cross-tenant leakage.** One employer seeing another employer's candidates is a data-protection
   incident, not a bug. Every `ITenantOwned` entity in the module's `DbContext` declares its own
   `HasQueryFilter`; `ManifestGuardTests.Every_tenant_owned_entity_declares_a_query_filter` fails
   the build if a new entity is added without one. **Do not weaken that test to unblock yourself.**
2. **An ungated write.** `advance_candidates`, `reject_candidate` and `propose_rubric` are
   approval-gated in **both** the manifest descriptor and the `ModuleTool`. A write that reached
   this module ungated would let the assistant reject a job applicant with no accountable human.

## Invariants that must never be weakened

- RBAC before the model — tools are filtered by permission before the model request is built.
- Approval-first writes — every state change parks for a human.
- Tenant isolation by construction.
- Write-only secrets — no credential is ever echoed back.
- Append-only audit — here the audit trail is a *deliverable*, not just a safety net: it is what a
  bias auditor and an employment tribunal ask for.
- **No biometric assessment.** No facial geometry, no voiceprint, no analysis of appearance, accent
  or tone of voice, at any point, for any purpose.

## Regulatory note

Hireworthy is built to *support* NYC Local Law 144, the Illinois AI Video Interview Act and the EU
AI Act's high-risk obligations for recruitment. **It does not deliver compliance with any of them.**
An independent bias audit, a published audit summary, a conformity assessment and an EU technical
file are acts performed by people and third parties, not features. See `SPEC.md` §7.
