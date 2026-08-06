using Hireworthy.Hiring;
using Xunit;

namespace Hireworthy.Hiring.Tests;

/// <summary>
/// Employment-gap inference — the one piece of extraction that is arithmetic rather than reading.
/// </summary>
/// <remarks>
/// This is computed rather than asked of the model on purpose. A candidate gets asked about an
/// employment gap in an interview, so "is there a gap here?" has to be checkable, not plausible.
/// These cases are the check.
/// </remarks>
public sealed class CvExtractionTests
{
    private static EmploymentSpan Role(string employer, string from, string? to = null) => new()
    {
        Employer = employer,
        Title = "Engineer",
        StartedOn = DateOnly.Parse($"{from}-01"),
        EndedOn = to is null ? null : DateOnly.Parse($"{to}-01"),
    };

    [Fact]
    public void A_real_gap_between_two_roles_is_found()
    {
        // The seeded APP-1001 case: Northwind ends 2020-02, Kestrel starts 2021-01.
        var gaps = ExtractedProfile.InferGaps([
            Role("Northwind Retail", "2017-03", "2020-02"),
            Role("Kestrel Payments", "2021-01"),
        ]);

        var gap = Assert.Single(gaps);
        Assert.Contains("2020-02", gap);
        Assert.Contains("2021-01", gap);
        Assert.Contains("11 months", gap);
    }

    [Fact]
    public void Contiguous_roles_produce_no_gap()
    {
        var gaps = ExtractedProfile.InferGaps([
            Role("Vantage Logistics", "2019-06", "2021-12"),
            Role("Vantage Logistics", "2022-01"),
        ]);

        Assert.Empty(gaps);
    }

    [Fact]
    public void A_short_break_is_not_flagged()
    {
        // Two months between jobs is a notice period, not a gap worth asking about. Flagging it
        // would train a recruiter to ignore the flag, which is worse than not having one.
        var gaps = ExtractedProfile.InferGaps([
            Role("Halloway Systems", "2015-09", "2017-02"),
            Role("Northwind Retail", "2017-04"),
        ]);

        Assert.Empty(gaps);
    }

    [Fact]
    public void A_short_contract_nested_inside_a_longer_tenure_does_not_fabricate_a_gap()
    {
        // The bug this guards: comparing each role only to the PREVIOUS one. Sorted by start date,
        // the nested contract ends long before the outer role does, so a naive implementation
        // reports a gap the candidate never had.
        var gaps = ExtractedProfile.InferGaps([
            Role("Kestrel Payments", "2018-01", "2024-01"),
            Role("Side Contract", "2019-01", "2019-04"),
            Role("Next Employer", "2024-02"),
        ]);

        Assert.Empty(gaps);
    }

    [Fact]
    public void Nothing_after_a_current_role_can_be_a_gap()
    {
        var gaps = ExtractedProfile.InferGaps([
            Role("Kestrel Payments", "2021-01"),
        ]);

        Assert.Empty(gaps);
    }

    [Fact]
    public void Roles_out_of_order_on_the_cv_are_still_measured_correctly()
    {
        // CVs are written newest-first. The extraction preserves CV order in Ordinal, so the gap
        // calculation must sort for itself rather than trusting the order it was handed.
        var gaps = ExtractedProfile.InferGaps([
            Role("Kestrel Payments", "2021-01"),
            Role("Northwind Retail", "2017-03", "2020-02"),
        ]);

        var gap = Assert.Single(gaps);
        Assert.Contains("11 months", gap);
    }
}
