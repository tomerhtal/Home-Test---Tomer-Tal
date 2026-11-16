namespace PortfolioAssistant.Core.Domain;

/// <summary>שערים ברגע מסוים, עם המקור והזמן שלהם.</summary>
public record PriceSnapshot(
    IReadOnlyDictionary<string, decimal> Prices,
    DateTimeOffset AsOf,
    string Source);
