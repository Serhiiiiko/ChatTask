using DebtChat.Core.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace DebtChat.Tests.Unit.Tools;

public class GetCurrentDateToolTests
{
    [Test]
    public void GetCurrentDate_ReturnsFormattedDate()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 6, 15, 14, 30, 0, TimeSpan.Zero));
        var tool = new GetCurrentDateTool(fakeTime, NullLogger<GetCurrentDateTool>.Instance);

        var result = tool.GetCurrentDate();

        result.ShouldBe("2025-06-15");
    }

    [Test]
    public void GetCurrentDate_ReturnsUtcDate_NotLocal()
    {
        // Simulate a time where UTC date differs from potential local dates
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 30, 0, TimeSpan.Zero));
        var tool = new GetCurrentDateTool(fakeTime, NullLogger<GetCurrentDateTool>.Instance);

        var result = tool.GetCurrentDate();

        result.ShouldBe("2025-01-01");
    }

    [Test]
    public void GetCurrentDate_ReturnsIso8601Format()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero));
        var tool = new GetCurrentDateTool(fakeTime, NullLogger<GetCurrentDateTool>.Instance);

        var result = tool.GetCurrentDate();

        result.ShouldMatch(@"^\d{4}-\d{2}-\d{2}$");
    }

    [Test]
    public void GetCurrentDate_LeapYear_ReturnsCorrectDate()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.Zero));
        var tool = new GetCurrentDateTool(fakeTime, NullLogger<GetCurrentDateTool>.Instance);

        var result = tool.GetCurrentDate();

        result.ShouldBe("2024-02-29");
    }

    [Test]
    public void GetCurrentDate_YearBoundary_ReturnsUtcDate()
    {
        // Dec 31st 23:59 UTC should still be Dec 31 (not Jan 1)
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero));
        var tool = new GetCurrentDateTool(fakeTime, NullLogger<GetCurrentDateTool>.Instance);

        var result = tool.GetCurrentDate();

        result.ShouldBe("2025-12-31");
    }

    [Test]
    public void GetCurrentDate_NewYearDay_ReturnsJanuaryFirst()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var tool = new GetCurrentDateTool(fakeTime, NullLogger<GetCurrentDateTool>.Instance);

        var result = tool.GetCurrentDate();

        result.ShouldBe("2026-01-01");
    }

    [Test]
    public void GetCurrentDate_OffsetTimeProvider_UsesUtcDate()
    {
        // Time is Jan 1 at midnight UTC, but with a +5 offset it would be Jan 1 05:00
        // The tool should use UTC date (Jan 1), not the offset date
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var tool = new GetCurrentDateTool(fakeTime, NullLogger<GetCurrentDateTool>.Instance);

        var result = tool.GetCurrentDate();

        result.ShouldBe("2025-01-01");
    }
}
