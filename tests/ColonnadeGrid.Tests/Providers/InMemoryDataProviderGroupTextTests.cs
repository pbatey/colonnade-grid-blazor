using System.Globalization;
using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;

namespace ColonnadeGrid.Tests.Providers;

public class InMemoryDataProviderGroupTextTests
{
    private sealed record Measurement(double Value);

    [Fact]
    public async Task GroupDisplayText_FollowsTheCurrentCulture_WhileTheKeyStaysInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var provider = new InMemoryDataProvider<Measurement>([new Measurement(1.5)]);

            var response = await provider.GetDataAsync(new DataRequest(0, int.MaxValue, null, [], nameof(Measurement.Value)));
            var group = Assert.Single(response.Groups!);
            Assert.Equal("1.5", group.Key);
            Assert.Equal("1,5", group.DisplayText);

            var listed = await provider.GetGroupsAsync(new GroupListRequest(null, [], nameof(Measurement.Value), 0, 10));
            Assert.Equal("1,5", Assert.Single(listed.Groups).DisplayText);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
