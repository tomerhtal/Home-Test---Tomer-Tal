using System.Text.Json;
using PortfolioAssistant.Core.Calculations;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Tools;

/// <summary>שולף תיק, שולף שערים ומחשב שווי, בסבב אחד. המודל מקבל תוצאה מוכנה ולא מחשב.</summary>
public sealed class CalculatePortfolioValueTool(IPortfolioRepository portfolios, IPriceProvider prices)
    : ToolBase(CustomerIdSchema)
{
    public override string Name => "calculate_portfolio_value";
    public override string Description =>
        "מחשב את השווי הנוכחי בדולרים של כל נכס בתיק ואת סך שווי התיק. השתמש בכל שאלה על שווי תיק.";

    public override async Task<string> ExecuteAsync(JsonElement input, UserContext user, CancellationToken ct)
    {
        var customerId = ReadString(input, "customerId");
        if (string.IsNullOrEmpty(customerId)) return Error("customerId is required");

        var portfolio = await portfolios.GetForUserAsync(customerId, user, ct);
        if (portfolio is null) return NotFound();

        PriceSnapshot snapshot;
        try { snapshot = await prices.GetPricesAsync(portfolio.Holdings.Select(h => h.Asset), ct); }
        catch (PriceUnavailableException ex) { return Error(ex.Message); }

        var valuation = PortfolioCalculator.Value(portfolio, snapshot);
        return ToJson(new
        {
            found = true,
            customerId = portfolio.CustomerId,
            holdings = valuation.Assets.Select(a => new
            {
                asset = a.Asset,
                quantity = Fmt.Quantity(a.Quantity),
                priceUsd = Fmt.Usd(a.PriceUsd),
                valueUsd = Fmt.Usd(a.ValueUsd)
            }),
            totalValueUsd = Fmt.Usd(valuation.TotalUsd),
            holdingsAsOf = Fmt.Date(portfolio.LastUpdated),
            pricesAsOf = Fmt.DateTime(snapshot.AsOf)
        });
    }
}
