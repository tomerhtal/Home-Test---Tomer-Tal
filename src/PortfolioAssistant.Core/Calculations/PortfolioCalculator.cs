using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Calculations;

public record AssetValue(string Asset, decimal Quantity, decimal PriceUsd, decimal ValueUsd);
public record PortfolioValuation(IReadOnlyList<AssetValue> Assets, decimal TotalUsd);

public record AssetExposure(string Asset, decimal ExposurePercent, bool ExceedsLimit);
public record ExposureCheck(bool HasRule, decimal? LimitPercent, IReadOnlyList<AssetExposure> Assets, IReadOnlyList<string> ExcludedStablecoins);

public record FeeCalculation(decimal AnnualRatePercent, decimal PeriodRatePercent, int CollectionsPerYear, decimal BasisUsd, decimal FeeUsd);

/// <summary>
/// כל החישובים המספריים במערכת. פונקציות טהורות: בלי קבצים, בלי רשת ובלי מודל.
/// החישוב נעשה בדיוק מלא (decimal), ועיגול לשתי ספרות קורה רק בסוף.
/// </summary>
public static class PortfolioCalculator
{
    public static PortfolioValuation Value(Portfolio portfolio, PriceSnapshot prices)
    {
        var assets = portfolio.Holdings
            .Select(h =>
            {
                var price = prices.Prices[h.Asset];
                return new AssetValue(h.Asset, h.Quantity, price, h.Quantity * price);
            })
            .ToList();

        return new PortfolioValuation(assets, assets.Sum(a => a.ValueUsd));
    }

    /// <param name="limitFraction">התקרה כשבר (0.60), או null אם אין כלל לפרופיל הסיכון.</param>
    public static ExposureCheck Exposure(PortfolioValuation valuation, decimal? limitFraction, ISet<string> stablecoins)
    {
        var excluded = valuation.Assets.Where(a => stablecoins.Contains(a.Asset)).Select(a => a.Asset).ToList();

        if (valuation.TotalUsd == 0)
            return new ExposureCheck(limitFraction is not null, limitFraction * 100, [], excluded);

        var assets = valuation.Assets
            .Where(a => !stablecoins.Contains(a.Asset))
            .Select(a =>
            {
                var share = a.ValueUsd / valuation.TotalUsd;
                // "לא יעלה על": בדיוק בתקרה מותר, מעליה חריגה. ההשוואה על הערך המדויק, לא המעוגל.
                var exceeds = limitFraction is { } limit && share > limit;
                return new AssetExposure(a.Asset, Round2(share * 100), exceeds);
            })
            .ToList();

        return new ExposureCheck(limitFraction is not null, limitFraction * 100, assets, excluded);
    }

    public static FeeCalculation PeriodFee(decimal basisUsd, decimal annualRate, int collectionsPerYear)
    {
        if (collectionsPerYear <= 0) throw new ArgumentOutOfRangeException(nameof(collectionsPerYear));
        var periodRate = annualRate / collectionsPerYear;
        return new FeeCalculation(annualRate * 100, periodRate * 100, collectionsPerYear, basisUsd, Round2(basisUsd * periodRate));
    }

    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
