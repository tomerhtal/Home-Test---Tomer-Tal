using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Data;

public interface IPriceProvider
{
    /// <summary>מחזיר שערים לנכסים המבוקשים. נכס בלי שער, או שער ישן מדי, זורקים PriceUnavailableException.</summary>
    Task<PriceSnapshot> GetPricesAsync(IEnumerable<string> assets, CancellationToken ct);
}

public sealed class PriceUnavailableException(string message) : Exception(message);
