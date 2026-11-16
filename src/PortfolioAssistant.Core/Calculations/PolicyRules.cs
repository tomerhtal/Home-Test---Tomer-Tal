using System.Globalization;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Calculations;

/// <summary>
/// קריאת החוקים המובנים מהמטא-דאטה של מסמכי המדיניות (שדות rule.*).
/// הטקסט נועד לבני אדם ולמודל; הערכים המובנים נועדו לקוד. שניהם באותו קובץ ובאותה גרסה.
/// </summary>
public static class PolicyRules
{
    public const string ExposureDocumentId = "exposure-policy";
    public const string FeesDocumentId = "management-fees";

    public static decimal? ExposureLimit(DocumentChunk exposurePolicy, string riskProfile) =>
        exposurePolicy.Rules.TryGetValue($"exposureLimit.{riskProfile}", out var value)
            ? decimal.Parse(value, CultureInfo.InvariantCulture)
            : null;

    public static ISet<string> Stablecoins(DocumentChunk exposurePolicy) =>
        exposurePolicy.Rules.TryGetValue("stablecoins", out var value)
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static decimal AnnualFeeRate(DocumentChunk fees) =>
        decimal.Parse(Required(fees, "annualFeeRate"), CultureInfo.InvariantCulture);

    public static int CollectionsPerYear(DocumentChunk fees) =>
        int.Parse(Required(fees, "collectionsPerYear"), CultureInfo.InvariantCulture);

    private static string Required(DocumentChunk doc, string key) =>
        doc.Rules.TryGetValue(key, out var value)
            ? value
            : throw new InvalidDataException($"Document '{doc.Id}' is missing rule '{key}'.");
}
