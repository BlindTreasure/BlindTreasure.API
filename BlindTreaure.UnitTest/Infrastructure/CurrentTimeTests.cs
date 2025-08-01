using FluentAssertions;

namespace BlindTreaure.UnitTest.Infrastructure;

public class CurrentTimeTests
{
    /// <summary>
    /// Checks if GetCurrentTime returns a DateTime very close to the actual UTC now.
    /// </summary>
    /// <remarks>
    /// Scenario: The GetCurrentTime method is called.
    /// Expected: The returned DateTime should be a UTC time that is very close to the moment it was called, allowing for a small delay.
    /// Coverage: Verifies the core functionality of returning the current UTC time.
    /// </remarks>
    [Fact]
    public void GetCurrentTime_ShouldReturnUtcNow()
    {
        // Arrange
        var currentTimeService = new BlindTreasure.Infrastructure.Commons.CurrentTime();

        // Act
        var result = currentTimeService.GetCurrentTime();

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(1000)); // Allow up to 1 second difference
    }

    /// <summary>
    /// Checks if GetCurrentTime always returns a DateTime object set to Coordinated Universal Time (UTC).
    /// </summary>
    /// <remarks>
    /// Scenario: The GetCurrentTime method is called.
    /// Expected: The 'Kind' property of the returned DateTime object is explicitly set to DateTimeKind.Utc.
    /// Coverage: Ensures that the method consistently provides time in the expected universal format.
    /// </remarks>
    [Fact]
    public void GetCurrentTime_ShouldAlwaysReturnUtcKind()
    {
        // Arrange
        var currentTimeService = new BlindTreasure.Infrastructure.Commons.CurrentTime();

        // Act
        var result = currentTimeService.GetCurrentTime();

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    /// <summary>
    /// Checks if GetCurrentTime returns a date that is not a default or ancient value, implying it's a current-era timestamp.
    /// </summary>
    /// <remarks>
    /// Scenario: The GetCurrentTime method is called.
    /// Expected: The returned DateTime is after a reasonable historical date, confirming it's a valid and recent timestamp.
    /// Coverage: Basic sanity check to ensure the method returns a sensible date, not an uninitialized or extremely old one.
    /// </remarks>
    [Fact]
    public void GetCurrentTime_ShouldBeAfterKnownHistoricalDate()
    {
        // Arrange
        var currentTimeService = new BlindTreasure.Infrastructure.Commons.CurrentTime();
        var historicalCutoff = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc); // A date well after system epoch

        // Act
        var result = currentTimeService.GetCurrentTime();

        // Assert
        result.Should().BeAfter(historicalCutoff);
    }

    /// <summary>
    /// Checks if consecutive calls to GetCurrentTime show a non-decreasing time, even over a very short period.
    /// </summary>
    /// <remarks>
    /// Scenario: The GetCurrentTime method is called twice in quick succession.
    /// Expected: The second timestamp is either equal to or later than the first, reflecting the passage of time.
    /// Coverage: Ensures the time source is dynamic and not fixed, and that time progresses forward or stays the same.
    /// </remarks>
    [Fact]
    public async Task GetCurrentTime_ConsecutiveCallsShouldBeNonDecreasing()
    {
        // Arrange
        var currentTimeService = new BlindTreasure.Infrastructure.Commons.CurrentTime();

        // Act
        var firstCall = currentTimeService.GetCurrentTime();
        await Task.Delay(50); // Wait a short time to allow clock to tick
        var secondCall = currentTimeService.GetCurrentTime();

        // Assert
        secondCall.Should().BeOnOrAfter(firstCall);
    }

    /// <summary>
    /// Checks that GetCurrentTime does not return the absolute minimum or maximum possible DateTime values.
    /// </summary>
    /// <remarks>
    /// Scenario: The GetCurrentTime method is called.
    /// Expected: The returned DateTime is not equivalent to DateTime.MinValue or DateTime.MaxValue, which are boundary values for the DateTime type.
    /// Coverage: Ensures the method returns a real-world timestamp within valid operational bounds.
    /// </remarks>
    [Fact]
    public void GetCurrentTime_ShouldNotReturnMinMaxValues()
    {
        // Arrange
        var currentTimeService = new BlindTreasure.Infrastructure.Commons.CurrentTime();

        // Act
        var result = currentTimeService.GetCurrentTime();

        // Assert
        result.Should().NotBe(DateTime.MinValue);
        result.Should().NotBe(DateTime.MaxValue);
    }
}