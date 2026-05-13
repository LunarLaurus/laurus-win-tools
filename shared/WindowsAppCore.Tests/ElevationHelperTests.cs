using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class ElevationHelperTests
{
    [WindowsFact]
    public void IsElevated_ReturnsSameValueOnRepeatedCalls()
    {
        ElevationHelper.IsElevated().Should().Be(ElevationHelper.IsElevated());
    }
}
