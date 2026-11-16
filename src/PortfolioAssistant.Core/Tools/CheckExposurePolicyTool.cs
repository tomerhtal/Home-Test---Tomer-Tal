using System.Text.Json;
using PortfolioAssistant.Core.Calculations;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Tools;

/// <summary>מחשב אחוזי חשיפה ומשווה לתקרה שבמדיניות החשיפה (הערך נקרא מהמטא-דאטה של המסמך).</summary>
public sealed class CheckExposurePolicyTool(IPortfolioRepository portfolios, IPriceProvider prices, IDocumentStore documents)
    : ToolBase(CustomerIdSchema)
{
    public override string Name => "check_exposure_policy";
    public override string Description =>
        "מחשב את אחוז החשיפה של כל נכס בתיק ובודק אם הוא חורג מהתקרה שבמדיניות החשיפה לפי פרופיל הסיכון. " +
        "מחזיר גם את מקור המדיניות.";

    public override async Task<string> ExecuteAsync(JsonElement input, UserContext user, CancellationToken ct)
    {
        var customerId = ReadString(input, "customerId");
        if (string.IsNullOrEmpty(customerId)) return Error("customerId is required");

        var portfolio = await portfolios.GetForUserAsync(customerId, user, ct);
        if (portfolio is null) return NotFound();

        var policy = documents.GetById(PolicyRules.ExposureDocumentId);
        if (policy is null) return Error("Exposure policy document is not available");

        PriceSnapshot snapshot;
        try { snapshot = await prices.GetPricesAsync(portfolio.Holdings.Select(h => h.Asset), ct); }
        catch (PriceUnavailableException ex) { return Error(ex.Message); }

        var valuation = PortfolioCalculator.Value(portfolio, snapshot);
        var check = PortfolioCalculator.Exposure(
            valuation,
            PolicyRules.ExposureLimit(policy, portfolio.RiskProfile),
            PolicyRules.Stablecoins(policy));

        return ToJson(new
        {
            found = true,
            customerId = portfolio.CustomerId,
            riskProfile = portfolio.RiskProfile,
            hasRule = check.HasRule,
            limit = check.LimitPercent is { } l ? Fmt.Percent(l) : null,
            assets = check.Assets.Select(a => new
            {
                asset = a.Asset,
                exposure = Fmt.Percent(a.ExposurePercent),
                exceedsLimit = a.ExceedsLimit
            }),
            excludedStablecoins = check.ExcludedStablecoins,
            holdingsAsOf = Fmt.Date(portfolio.LastUpdated),
            pricesAsOf = Fmt.DateTime(snapshot.AsOf),
            source = SourceOf(policy)
        });
    }
}
