using System.Text.Json;
using PortfolioAssistant.Core.Calculations;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Tools;

public sealed class GetPricesTool(IPriceProvider prices) : ToolBase("""
    {
      "type": "object",
      "properties": {
        "assets": { "type": "array", "items": { "type": "string" }, "description": "סימולי נכסים, למשל BTC, ETH" }
      },
      "required": ["assets"]
    }
    """)
{
    public override string Name => "get_prices";
    public override string Description =>
        "מחזיר שערים נוכחיים בדולרים לרשימת נכסים דיגיטליים, עם מקור וזמן השער.";

    public override async Task<string> ExecuteAsync(JsonElement input, UserContext user, CancellationToken ct)
    {
        if (input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("assets", out var assetsElement)
            || assetsElement.ValueKind != JsonValueKind.Array)
            return Error("assets is required");

        var assets = assetsElement.EnumerateArray()
            .Where(a => a.ValueKind == JsonValueKind.String)
            .Select(a => a.GetString()!.Trim().ToUpperInvariant())
            .Where(a => a.Length > 0)
            .ToList();
        if (assets.Count == 0) return Error("assets is required");

        try
        {
            var snapshot = await prices.GetPricesAsync(assets, ct);
            return ToJson(new
            {
                prices = snapshot.Prices.Select(p => new { asset = p.Key, priceUsd = Fmt.Usd(p.Value) }),
                asOf = Fmt.DateTime(snapshot.AsOf),
                source = snapshot.Source
            });
        }
        catch (PriceUnavailableException ex)
        {
            return Error(ex.Message);
        }
    }
}
