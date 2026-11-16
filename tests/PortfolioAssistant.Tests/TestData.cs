using PortfolioAssistant.Core;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Tests;

internal static class TestData
{
    public static readonly string Folder = AssistantBuilder.FindDataFolder();
    public static readonly UserContext Pm317 = new("pm-317", UserContext.PortfolioManagerRole);
    public static readonly UserContext Pm402 = new("pm-402", UserContext.PortfolioManagerRole);

    public static JsonPortfolioRepository Portfolios() => new(Folder);
    public static JsonPriceProvider Prices() => new(Folder);
    public static KeywordDocumentStore Documents() => new(Path.Combine(Folder, "documents"));

    public static Portfolio SamplePortfolio() =>
        Portfolios().GetForUserAsync("C-1042", Pm317, CancellationToken.None).Result!;

    public static PriceSnapshot SamplePrices() =>
        new(new Dictionary<string, decimal> { ["BTC"] = 63000m, ["ETH"] = 2900m, ["USDC"] = 1m },
            DateTimeOffset.Parse("2026-11-10T14:32:00Z"), "test");
}
