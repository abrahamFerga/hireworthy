using Plenipo.Testing.Conformance;
using Plenipo.Testing.Evals;
using Xunit;

namespace Hireworthy.IntegrationTests;

// The fleet testing contract (Plenipo docs/TESTING_CONTRACT.md): the platform publishes its
// invariants as executable tests and this product runs them against its OWN host on every pull
// request. Five lines, no product code — S01–S15 (the security spine), R1–R4 (the agent
// guardrails), manifest integrity, tenant isolation, and the golden-eval runner over
// Evals/cases/*.json. Upgrading PlenipoVersion upgrades the invariants, which is the point: a
// platform release that would break Hireworthy fails here rather than in production.
//
// S04 in particular is the acceptance test for hireworthy#51 / plenipo#145 — an approver who holds
// chat.approvals.manage but not the parked tool's own permission cannot release it. The hiring
// spelling of that reproduction lives in AdvanceTests.

// KNOWN RED, AND DELIBERATELY NOT WORKED AROUND: PlenipoSpine.S03. It releases a parked write and
// requires it to EXECUTE, but the arguments were synthesized by the Mock provider, and
// MockChatClient.SynthesizeArguments gives every required ARRAY parameter `Array.Empty<object?>()`
// (DefaultForSchema: "array" => Array.Empty<object?>()). `advance_candidates` takes
// `string[] references`, so the parked call is always
//
//   {"reason": "Please advance candidates for me, using a tool.", "references": []}
//
// and releasing it is 422 "Name at least one candidate to advance. (Parameter 'references')".
// ProductContract's only steering seam is WritePrompt — text — and quoted spans fill string
// parameters only, so no product-side prompt can populate a collection. S03 is therefore
// unsatisfiable for ANY product whose approval-gated write takes a collection, and the fix belongs
// in the kit (an argument seam on ProductContract, or a Mock that fills a required array with one
// synthesized element), not here. Naming a different, string-only write tool in the contract would
// make this green while quietly moving S01/S03/S04 off the tool this product exists to gate — a
// worked-around invariant, not a satisfied one. Filed as a platform request; see the PR body.
[Collection("api")]
public sealed class PlenipoSpine(IntegrationFixture fixture) : PlenipoSpineConformance<Program>(fixture);

[Collection("api")]
public sealed class PlenipoManifest(IntegrationFixture fixture) : PlenipoManifestConformance<Program>(fixture);

[Collection("api")]
public sealed class PlenipoTenancy(IntegrationFixture fixture) : PlenipoTenancyConformance<Program>(fixture);

[Collection("api")]
public sealed class PlenipoRedTeam(IntegrationFixture fixture) : PlenipoRedTeamConformance<Program>(fixture);

// Rung 4 of the ladder. The cases are the product's own (Evals/cases/*.json); the runner is the
// platform's, so a change to the AG-UI protocol or the gate reaches every product at once.
//
// LIMIT, stated where the harness lives: the assistant runs on Plenipo's Mock provider, which picks
// a tool by matching name tokens in the message rather than by reasoning. These cases prove the
// contract AROUND the model — the right tool is reachable, an unpermitted tool is never offered, a
// write parks on the gate, the reply does not claim a parked write happened. They prove nothing
// about answer quality. Do not add a case only a real model could satisfy; it will be a flake that
// teaches the next agent to delete this harness.
[Collection("api")]
public sealed class HiringGoldenEvals(IntegrationFixture fixture) : PlenipoGoldenEvals<Program>(fixture);
