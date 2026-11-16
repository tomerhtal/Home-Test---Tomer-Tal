namespace PortfolioAssistant.Core.Domain;

/// <summary>נכס אחד בתיק.</summary>
public record Holding(string Asset, decimal Quantity, decimal AvgCostUsd);

/// <summary>תיק לקוח כפי שהוא מגיע ממערכת המקור.</summary>
public record Portfolio(
    string CustomerId,
    string RiskProfile,
    DateTimeOffset LastUpdated,
    IReadOnlyList<Holding> Holdings);
