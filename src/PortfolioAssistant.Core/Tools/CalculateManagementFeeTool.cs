using System.Text.Json;
using PortfolioAssistant.Core.Calculations;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Tools;

/// <summary>
/// דמי ניהול לרבעון הקרוב: שיעור שנתי חלקי מספר הגביות בשנה, על השווי הנוכחי (הנחה מתועדת ב-README).
/// </summary>
public sealed class CalculateManagementFeeTool(IPortfolioRepository portfolios, IPriceProvider prices, IDocumentStore documents)
    : ToolBase(CustomerIdSchema)
{
    public override string Name => "calculate_management_fee";
    public override string Description =>
        "מחשב את דמי הניהול שהלקוח ישלם ברבעון הקרוב, לפי מסמך דמי הניהול ושווי התיק הנוכחי. מחזיר גם את המקור.";

    public override async Task<string> ExecuteAsync(JsonElement input, UserContext user, CancellationToken ct)
    {
        var customerId = ReadString(input, "customerId");
        if (string.IsNullOrEmpty(customerId)) return Error("customerId is required");

        var portfolio = await portfolios.GetForUserAsync(customerId, user, ct);
        if (portfolio is null) return NotFound();

        var feesDoc = documents.GetById(PolicyRules.FeesDocumentId);
        if (feesDoc is null) return Error("Management fees document is not available");

        PriceSnapshot snapshot;
        try { snapshot = await prices.GetPricesAsync(portfolio.Holdings.Select(h => h.Asset), ct); }
        catch (PriceUnavailableException ex) { return Error(ex.Message); }

        var valuation = PortfolioCalculator.Value(portfolio, snapshot);
        var fee = PortfolioCalculator.PeriodFee(
            valuation.TotalUsd,
            PolicyRules.AnnualFeeRate(feesDoc),
            PolicyRules.CollectionsPerYear(feesDoc));

        return ToJson(new
        {
            found = true,
            customerId = portfolio.CustomerId,
            annualRate = Fmt.Percent(fee.AnnualRatePercent),
            quarterlyRate = Fmt.Percent(fee.PeriodRatePercent),
            collectionsPerYear = fee.CollectionsPerYear.ToString(),
            basisValueUsd = Fmt.Usd(fee.BasisUsd),
            quarterlyFeeUsd = Fmt.Usd(fee.FeeUsd),
            pricesAsOf = Fmt.DateTime(snapshot.AsOf),
            source = SourceOf(feesDoc)
        });
    }
}
