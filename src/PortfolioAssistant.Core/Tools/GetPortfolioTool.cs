using System.Text.Json;
using PortfolioAssistant.Core.Calculations;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Tools;

public sealed class GetPortfolioTool(IPortfolioRepository portfolios) : ToolBase(CustomerIdSchema)
{
    public override string Name => "get_portfolio";
    public override string Description =>
        "מחזיר את תיק הלקוח: נכסים, כמויות, מחיר קנייה ממוצע, פרופיל סיכון ותאריך עדכון. " +
        "השתמש בשאלות על אחזקות או כמויות. לשווי, לחשיפה או לדמי ניהול יש כלים ייעודיים.";

    public override async Task<string> ExecuteAsync(JsonElement input, UserContext user, CancellationToken ct)
    {
        var customerId = ReadString(input, "customerId");
        if (string.IsNullOrEmpty(customerId)) return Error("customerId is required");

        var portfolio = await portfolios.GetForUserAsync(customerId, user, ct);
        if (portfolio is null) return NotFound();

        return ToJson(new
        {
            found = true,
            customerId = portfolio.CustomerId,
            riskProfile = portfolio.RiskProfile,
            holdingsAsOf = Fmt.Date(portfolio.LastUpdated),
            holdings = portfolio.Holdings.Select(h => new
            {
                asset = h.Asset,
                quantity = Fmt.Quantity(h.Quantity),
                avgCostUsd = Fmt.Usd(h.AvgCostUsd)
            })
        });
    }
}
