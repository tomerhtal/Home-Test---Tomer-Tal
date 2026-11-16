using System.Text.Json;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Data;

/// <summary>
/// שערים מקובץ. בייצור: מקור השערים הרשמי של הפלטפורמה, עם cache קצר.
/// </summary>
public sealed class JsonPriceProvider : IPriceProvider
{
    private readonly PriceSnapshot _snapshot;
    private readonly TimeSpan? _maxAge;
    private readonly TimeProvider _clock;

    /// <param name="maxAge">
    /// שער ישן מהסף הזה נחסם. באב-טיפוס ברירת המחדל היא null (כבוי), כי קובץ השערים סטטי.
    /// </param>
    public JsonPriceProvider(string dataFolder, TimeSpan? maxAge = null, TimeProvider? clock = null)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<PriceFile>(
            File.ReadAllText(Path.Combine(dataFolder, "prices.json")), options)
            ?? throw new InvalidDataException("prices.json is empty");

        _snapshot = new PriceSnapshot(
            new Dictionary<string, decimal>(file.Prices, StringComparer.OrdinalIgnoreCase),
            file.AsOf,
            file.Source);
        _maxAge = maxAge;
        _clock = clock ?? TimeProvider.System;
    }

    public Task<PriceSnapshot> GetPricesAsync(IEnumerable<string> assets, CancellationToken ct)
    {
        if (_maxAge is { } maxAge && _clock.GetUtcNow() - _snapshot.AsOf > maxAge)
            throw new PriceUnavailableException($"Prices are stale (as of {_snapshot.AsOf:O}).");

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // שער חסר הוא שגיאה, לא 0: אחרת השווי היה שגוי בשקט.
            if (!_snapshot.Prices.TryGetValue(asset, out var price))
                throw new PriceUnavailableException($"No price for asset '{asset}'.");
            result[asset] = price;
        }

        return Task.FromResult(_snapshot with { Prices = result });
    }

    private sealed record PriceFile(DateTimeOffset AsOf, string Source, Dictionary<string, decimal> Prices);
}
