using Plenipo.Core.Entities;
using Plenipo.Core.Multitenancy;

namespace Hireworthy.Hiring;

/// <summary>
/// One role on a CV, with its dates resolved to a comparable form.
/// </summary>
/// <remarks>
/// Resolving the dates is the point. A CV states them three different ways — "2019–present",
/// "Jan 2019 - Current", "5 yrs" — and none of those can be compared to another candidate's until
/// something turns them into a span. Day precision is deliberately not modelled: CVs give
/// month-and-year, so storing a day would invent precision the source does not have.
/// </remarks>
public sealed class EmploymentSpan : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ExtractedProfileId { get; set; }

    public ExtractedProfile? Profile { get; set; }

    public required string Employer { get; set; }

    public required string Title { get; set; }

    /// <summary>First month of the role. Day is always 1 — CVs do not state days.</summary>
    public DateOnly StartedOn { get; set; }

    /// <summary>Last month of the role, or null when the candidate is still there.</summary>
    public DateOnly? EndedOn { get; set; }

    public bool IsCurrent => EndedOn is null;

    public int Ordinal { get; set; }
}

/// <summary>
/// What the assistant read off one CV: the roles, the skills, the education, and the gaps
/// between roles that a recruiter would ask about.
/// </summary>
/// <remarks>
/// Derived data, recomputable from <see cref="CvDocument.ExtractedText"/> at any time — which is
/// exactly why <c>parse_cv</c> is not approval-gated (ADR-0004). It changes nothing about a
/// candidate's outcome; only <c>advance_candidates</c> and <c>reject_candidate</c> do, and both are
/// gated.
/// </remarks>
public sealed class ExtractedProfile : EntityBase, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ApplicantId { get; set; }

    public Applicant? Applicant { get; set; }

    public DateTimeOffset ExtractedAt { get; set; }

    /// <summary>Skills as written on the CV, not normalised to a taxonomy.</summary>
    public List<string> Skills { get; set; } = [];

    /// <summary>Education lines as written, e.g. "BSc Computer Science, Manchester, 2018".</summary>
    public List<string> Education { get; set; } = [];

    /// <summary>
    /// Gaps between roles, in the recruiter's own words — computed, never asserted by the model.
    /// </summary>
    /// <remarks>
    /// Stored rather than computed on read so a reviewer sees the same gaps the recruiter saw at
    /// decision time, even if the extraction is later re-run.
    /// </remarks>
    public List<string> EmploymentGaps { get; set; } = [];

    /// <summary>Anything the assistant could not resolve — flagged rather than guessed.</summary>
    public string? Unresolved { get; set; }

    public ICollection<EmploymentSpan> Employment { get; set; } = [];

    /// <summary>
    /// Finds the gaps between employment spans.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Computed here, deliberately, rather than asked of the model.</b> "Was there a gap between
    /// these two jobs?" is arithmetic on two dates — a model asked to do it will sometimes be
    /// wrong, and an employment gap is something a candidate gets asked about in an interview. This
    /// method is the reason it is checkable rather than plausible.
    /// </para>
    /// <para>
    /// <paramref name="minimumMonths"/> defaults to 3: shorter breaks are ordinary notice periods,
    /// garden leave, or a holiday between jobs, and flagging them would train recruiters to ignore
    /// the flag. Overlapping roles produce no gap.
    /// </para>
    /// </remarks>
    public static List<string> InferGaps(IEnumerable<EmploymentSpan> spans, int minimumMonths = 3)
    {
        var ordered = spans
            .OrderBy(s => s.StartedOn)
            .ThenBy(s => s.EndedOn ?? DateOnly.MaxValue)
            .ToList();

        var gaps = new List<string>();

        // `covered` is the furthest month any role so far reached, not merely the previous role's
        // end. Without it, a short contract nested inside a longer tenure would fabricate a gap
        // that never existed.
        DateOnly? covered = null;

        foreach (var span in ordered)
        {
            if (covered is not null && span.StartedOn > covered)
            {
                var months = MonthsBetween(covered.Value, span.StartedOn);
                if (months >= minimumMonths)
                {
                    gaps.Add($"{covered:yyyy-MM} to {span.StartedOn:yyyy-MM} ({months} months)");
                }
            }

            // A current role covers everything from here on, so nothing after it can be a gap.
            if (span.EndedOn is null)
            {
                return gaps;
            }

            covered = covered is null || span.EndedOn > covered ? span.EndedOn : covered;
        }

        return gaps;
    }

    private static int MonthsBetween(DateOnly from, DateOnly to) =>
        ((to.Year - from.Year) * 12) + to.Month - from.Month;
}
