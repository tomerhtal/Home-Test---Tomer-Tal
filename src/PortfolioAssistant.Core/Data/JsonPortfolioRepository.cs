using System.Text.Json;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Data;

/// <summary>
/// מימוש מקבצי JSON. בייצור: שאילתה ל-DB עם JOIN על טבלת השיוך, או API של מערכת התיקים.
/// </summary>
public sealed class JsonPortfolioRepository : IPortfolioRepository
{
    private readonly Dictionary<string, Portfolio> _portfolios;
    private readonly Dictionary<string, HashSet<string>> _assignments;

    public JsonPortfolioRepository(string dataFolder)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var portfolios = JsonSerializer.Deserialize<List<Portfolio>>(
            File.ReadAllText(Path.Combine(dataFolder, "portfolios.json")), options)
            ?? throw new InvalidDataException("portfolios.json is empty");
        _portfolios = portfolios.ToDictionary(p => p.CustomerId, StringComparer.OrdinalIgnoreCase);

        var assignments = JsonSerializer.Deserialize<List<Assignment>>(
            File.ReadAllText(Path.Combine(dataFolder, "assignments.json")), options)
            ?? throw new InvalidDataException("assignments.json is empty");
        _assignments = assignments.ToDictionary(
            a => a.ManagerId,
            a => a.Customers.ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<Portfolio?> GetForUserAsync(string customerId, UserContext user, CancellationToken ct)
    {
        // שלב 1: הרשאה. רק אם המשתמש משויך ללקוח ממשיכים.
        var isAssigned = _assignments.TryGetValue(user.UserId, out var customers)
                         && customers.Contains(customerId);

        // שלב 2: שליפת התיק.
        Portfolio? result = null;
        if (isAssigned)
            _portfolios.TryGetValue(customerId, out result);

        return Task.FromResult(result);
    }

    private sealed record Assignment(string ManagerId, List<string> Customers);
}
