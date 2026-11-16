using PortfolioAssistant.Core.Calculations;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Tests;

public class PortfolioCalculatorTests
{
    private static readonly HashSet<string> Stablecoins = ["USDC"];

    [Fact]
    public void Value_SamplePortfolio_Is104510()
    {
        var valuation = PortfolioCalculator.Value(TestData.SamplePortfolio(), TestData.SamplePrices());

        Assert.Equal(104_510m, valuation.TotalUsd);
        Assert.Equal(53_550m, valuation.Assets.Single(a => a.Asset == "BTC").ValueUsd);
        Assert.Equal(35_960m, valuation.Assets.Single(a => a.Asset == "ETH").ValueUsd);
        Assert.Equal(15_000m, valuation.Assets.Single(a => a.Asset == "USDC").ValueUsd);
    }

    [Fact]
    public void Exposure_SamplePortfolio_BtcIs51_24AndWithinLimit()
    {
        var valuation = PortfolioCalculator.Value(TestData.SamplePortfolio(), TestData.SamplePrices());
        var check = PortfolioCalculator.Exposure(valuation, 0.60m, Stablecoins);

        var btc = check.Assets.Single(a => a.Asset == "BTC");
        var eth = check.Assets.Single(a => a.Asset == "ETH");
        Assert.Equal(51.24m, btc.ExposurePercent);
        Assert.Equal(34.41m, eth.ExposurePercent);
        Assert.False(btc.ExceedsLimit);
        Assert.False(eth.ExceedsLimit);
        Assert.Equal(60m, check.LimitPercent);
    }

    [Fact]
    public void Exposure_StablecoinsAreExcluded()
    {
        var valuation = PortfolioCalculator.Value(TestData.SamplePortfolio(), TestData.SamplePrices());
        var check = PortfolioCalculator.Exposure(valuation, 0.60m, Stablecoins);

        Assert.DoesNotContain(check.Assets, a => a.Asset == "USDC");
        Assert.Contains("USDC", check.ExcludedStablecoins);
    }

    [Fact]
    public void Exposure_ExactlyAtLimit_DoesNotExceed_AboveLimit_Exceeds()
    {
        // "לא יעלה על 60%": בדיוק 60% מותר.
        var atLimit = Valuation(("BTC", 60m), ("USDC", 40m));
        var above = Valuation(("BTC", 60.01m), ("USDC", 39.99m));

        Assert.False(PortfolioCalculator.Exposure(atLimit, 0.60m, Stablecoins).Assets.Single().ExceedsLimit);
        Assert.True(PortfolioCalculator.Exposure(above, 0.60m, Stablecoins).Assets.Single().ExceedsLimit);
    }

    [Fact]
    public void Exposure_NoRuleForRiskProfile_ReportsNoRule()
    {
        var valuation = PortfolioCalculator.Value(TestData.SamplePortfolio(), TestData.SamplePrices());
        var check = PortfolioCalculator.Exposure(valuation, limitFraction: null, Stablecoins);

        Assert.False(check.HasRule);
        Assert.Null(check.LimitPercent);
        Assert.All(check.Assets, a => Assert.False(a.ExceedsLimit));
    }

    [Fact]
    public void QuarterlyFee_SamplePortfolio_Is209_02()
    {
        var fee = PortfolioCalculator.PeriodFee(104_510m, annualRate: 0.008m, collectionsPerYear: 4);

        Assert.Equal(209.02m, fee.FeeUsd);
        Assert.Equal(0.2m, fee.PeriodRatePercent);
        Assert.Equal(0.8m, fee.AnnualRatePercent);
    }

    [Fact]
    public void Round2_UsesAwayFromZero()
    {
        Assert.Equal(0.13m, PortfolioCalculator.Round2(0.125m));
        Assert.Equal(51.24m, PortfolioCalculator.Round2(51.2391m));
    }

    [Fact]
    public void Value_MissingPrice_Throws()
    {
        var prices = TestData.SamplePrices() with { Prices = new Dictionary<string, decimal> { ["BTC"] = 63000m } };
        Assert.Throws<KeyNotFoundException>(() => PortfolioCalculator.Value(TestData.SamplePortfolio(), prices));
    }

    private static PortfolioValuation Valuation(params (string Asset, decimal Value)[] assets) =>
        new(assets.Select(a => new AssetValue(a.Asset, a.Value, 1m, a.Value)).ToList(), assets.Sum(a => a.Value));
}
