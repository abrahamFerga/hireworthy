using Hireworthy.Hiring;

namespace Hireworthy.IntegrationTests;

/// <summary>
/// The criteria of the seeded approved rubric v1 on REQ-142, and a way to cover the rest of it.
/// </summary>
/// <remarks>
/// <para>
/// <c>screen_applicant</c> refuses a score set that omits a criterion (issue #44), because an
/// omitted criterion shrank the maximum and inflated the candidate. Tests that need a screening as a
/// <b>precondition</b> — advance, reject, the candidate view — must therefore supply the whole
/// rubric rather than the one criterion they care about.
/// </para>
/// <para>
/// <see cref="UnresolvedExcept"/> marks the remainder unresolved, which is what is actually true of
/// the throwaway CVs those tests seed: a two-line CV genuinely does not evidence mentoring or ledger
/// work. Nothing here relaxes an assertion — every assertion still lives in the calling test, and an
/// unresolved criterion costs the candidate its points exactly as ADR-0013 intends.
/// </para>
/// </remarks>
public static class SeededRubric
{
    public const string PythonExperience = "Production Python experience";

    public static readonly string[] CriterionNames =
    [
        PythonExperience,
        "On-call ownership",
        "Written communication",
        "Mentoring",
        "Payments or ledger domain",
    ];

    /// <summary>Every seeded criterion except the named ones, marked unresolved and uncited.</summary>
    public static IEnumerable<ScoredCriterion> UnresolvedExcept(params string[] scored) =>
        CriterionNames
            .Where(name => !scored.Contains(name))
            .Select(name => new ScoredCriterion(name, 0, null, 0, 0, true, "Not evidenced by this CV."));
}
