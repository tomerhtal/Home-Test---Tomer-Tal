using PortfolioAssistant.Core.Data;

namespace PortfolioAssistant.Tests;

public class PriceProviderTests
{
    [Fact]
    public async Task ReturnsRequestedPricesWithTimestampAndSource()
    {
        var snapshot = await TestData.Prices().GetPricesAsync(["BTC", "ETH"], CancellationToken.None);

        Assert.Equal(63000m, snapshot.Prices["BTC"]);
        Assert.Equal(2900m, snapshot.Prices["ETH"]);
        Assert.Equal(2, snapshot.Prices.Count);
        Assert.Equal("platform-pricing", snapshot.Source);
    }

    [Fact]
    public async Task MissingAsset_Throws_InsteadOfReturningZero()
    {
        await Assert.ThrowsAsync<PriceUnavailableException>(
            () => TestData.Prices().GetPricesAsync(["DOGE"], CancellationToken.None));
    }

    [Fact]
    public async Task StalePrices_Throw()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-11-10T15:00:00Z"));   // 28 דקות אחרי השער
        var provider = new JsonPriceProvider(TestData.Folder, maxAge: TimeSpan.FromMinutes(5), clock);

        await Assert.ThrowsAsync<PriceUnavailableException>(
            () => provider.GetPricesAsync(["BTC"], CancellationToken.None));
    }

    [Fact]
    public async Task FreshPrices_Pass()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-11-10T14:34:00Z"));
        var provider = new JsonPriceProvider(TestData.Folder, maxAge: TimeSpan.FromMinutes(5), clock);

        var snapshot = await provider.GetPricesAsync(["BTC"], CancellationToken.None);
        Assert.Equal(63000m, snapshot.Prices["BTC"]);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
