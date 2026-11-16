using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PortfolioAssistant.Core.Llm;

/// <summary>
/// מודל מדומה, דטרמיניסטי, לריצה בלי גישה ל-API (כפי שהמבחן מתיר).
/// הוא מחליף רק את "המוח": את ההחלטה איזה כלי לבקש (לפי מילות מפתח) ואת הניסוח (לפי תבניות).
/// הלולאה, הכלים, החישובים, ההרשאות, החיפוש והבדיקות רצים באמת.
/// </summary>
public sealed partial class MockLlmClient : ILlmClient
{
    public string Name => "mock";

    public Task<LlmResponse> SendAsync(
        string systemPrompt, IReadOnlyList<ToolDefinition> tools, IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        var question = messages.OfType<UserMessage>().First().Text;
        var toolResults = messages.OfType<ToolResultsMessage>().SelectMany(m => m.Results).ToList();

        // מצב 2: כבר יש תוצאות כלים. מנסחים תשובה מהן.
        if (toolResults.Count > 0)
            return Task.FromResult(LlmResponse.FromText(Compose(question, toolResults[^1])));

        // מצב 1: אין עדיין תוצאות. מחליטים איזה כלי לבקש.
        var customerId = ExtractCustomerId(question) ?? ExtractCustomerId(systemPrompt) ?? "";
        var available = tools.Select(t => t.Name).ToHashSet();
        var (toolName, input) = DecideTool(question, customerId);

        if (!available.Contains(toolName))
            return Task.FromResult(LlmResponse.FromText("לא נמצא כלי מתאים לשאלה."));

        var call = new ToolCall($"mock-{Guid.NewGuid():N}"[..13], toolName, JsonSerializer.SerializeToElement(input));
        return Task.FromResult(LlmResponse.FromToolCalls(call));
    }

    // ---------- החלטה איזה כלי להפעיל ----------

    private static (string Tool, object Input) DecideTool(string question, string customerId)
    {
        var q = question.ToLowerInvariant();
        var assets = MentionedAssets(question);

        bool Has(params string[] words) => words.Any(w => q.Contains(w));

        if (Has("דמי ניהול", "דמי הניהול", "עמלה", "עמלת"))
            return Has("ישלם", "לשלם", "רבעון")
                ? ("calculate_management_fee", new { customerId })
                : ("search_documents", new { query = question });

        if (Has("חשיפ", "חורג"))
            return ("check_exposure_policy", new { customerId });

        if (Has("שווי"))
            return ("calculate_portfolio_value", new { customerId });

        if (Has("שער", "מחיר") && !Has("קנייה ממוצע") && assets.Count > 0 && !Has("סקיר"))
            return ("get_prices", new { assets });

        if (Has("תיק", "אחזק") || (Has("כמה") && assets.Count > 0))
            return ("get_portfolio", new { customerId });

        return ("search_documents", new { query = question });
    }

    // ---------- ניסוח תשובה מתוצאת כלי ----------

    private static string Compose(string question, ToolOutput output)
    {
        using var doc = JsonDocument.Parse(output.Json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out _))
            return Guards.Messages.DataUnavailable;
        if (root.TryGetProperty("found", out var found) && !found.GetBoolean())
            return root.GetProperty("message").GetString()!;

        return output.ToolName switch
        {
            "calculate_portfolio_value" => ComposeValue(root),
            "check_exposure_policy" => ComposeExposure(question, root),
            "calculate_management_fee" => ComposeFee(root),
            "get_portfolio" => ComposePortfolio(question, root),
            "get_prices" => ComposePrices(root),
            "search_documents" => ComposeSearch(root),
            _ => Guards.Messages.CouldNotAnswer
        };
    }

    private static string ComposeValue(JsonElement r)
    {
        var parts = r.GetProperty("holdings").EnumerateArray()
            .Select(h => $"{S(h, "asset")} {S(h, "valueUsd")} דולר");
        return $"השווי הנוכחי של התיק של {S(r, "customerId")} הוא {S(r, "totalValueUsd")} דולר " +
               $"({string.Join(", ", parts)}). " +
               $"האחזקות נכונות ל-{S(r, "holdingsAsOf")} והשערים נכונים ל-{S(r, "pricesAsOf")}.";
    }

    private static string ComposeExposure(string question, JsonElement r)
    {
        var source = r.GetProperty("source");
        var cite = Cite(source);
        var profile = RiskProfileName(S(r, "riskProfile"));

        if (!r.GetProperty("hasRule").GetBoolean())
            return $"{cite}, לא מוגדרת תקרת חשיפה לפרופיל סיכון {profile}.";

        var asked = MentionedAssets(question);
        var assets = r.GetProperty("assets").EnumerateArray()
            .Where(a => asked.Count == 0 || asked.Contains(S(a, "asset")))
            .ToList();

        var sb = new StringBuilder();
        sb.Append($"{cite}, בפרופיל סיכון {profile} נכס בודד שאינו מטבע יציב לא יעלה על {S(r, "limit")} משווי התיק. ");
        foreach (var a in assets)
        {
            var verdict = a.GetProperty("exceedsLimit").GetBoolean() ? "חורגת מהמדיניות" : "אינה חורגת מהמדיניות";
            sb.Append($"החשיפה ל-{S(a, "asset")} היא {S(a, "exposure")}, ולכן היא {verdict}. ");
        }
        sb.Append($"החישוב לפי אחזקות נכון ל-{S(r, "holdingsAsOf")} ושערים נכון ל-{S(r, "pricesAsOf")}.");
        return sb.ToString();
    }

    private static string ComposeFee(JsonElement r) =>
        $"דמי הניהול לרבעון הקרוב הם {S(r, "quarterlyFeeUsd")} דולר: {S(r, "quarterlyRate")} לרבעון " +
        $"({S(r, "annualRate")} בשנה, שנגבים {S(r, "collectionsPerYear")} פעמים בשנה) " +
        $"על שווי התיק הנוכחי, {S(r, "basisValueUsd")} דולר. {Cite(r.GetProperty("source"))}.";

    private static string ComposePortfolio(string question, JsonElement r)
    {
        var asked = MentionedAssets(question);
        var holdings = r.GetProperty("holdings").EnumerateArray()
            .Where(h => asked.Count == 0 || asked.Contains(S(h, "asset")))
            .ToList();

        if (holdings.Count == 0)
            return $"בתיק של {S(r, "customerId")} אין אחזקה בנכס שנשאל עליו (נכון ל-{S(r, "holdingsAsOf")}).";

        var parts = holdings.Select(h => $"{S(h, "quantity")} {S(h, "asset")} (מחיר קנייה ממוצע {S(h, "avgCostUsd")} דולר)");
        return $"בתיק של {S(r, "customerId")} יש {string.Join(", ", parts)}. האחזקות נכונות ל-{S(r, "holdingsAsOf")}.";
    }

    private static string ComposePrices(JsonElement r)
    {
        var parts = r.GetProperty("prices").EnumerateArray().Select(p => $"{S(p, "asset")}: {S(p, "priceUsd")} דולר");
        return $"השערים הנוכחיים: {string.Join(", ", parts)} (מקור: {S(r, "source")}, נכון ל-{S(r, "asOf")}).";
    }

    private static string ComposeSearch(JsonElement r)
    {
        var results = r.GetProperty("results").EnumerateArray().ToList();
        if (results.Count == 0) return Guards.Messages.NoInformation;

        // רק התוצאות עם הציון הגבוה ביותר: כמו מודל שבוחר את הקטע הרלוונטי.
        var top = results.Max(x => x.GetProperty("score").GetDouble());
        var best = results.Where(x => x.GetProperty("score").GetDouble() >= top);

        return string.Join(" ", best.Select(x =>
            $"{Cite(x)} מתאריך {S(x, "effectiveDate")}: {S(x, "text")}"));
    }

    // ---------- עזרים ----------

    private static string Cite(JsonElement source) => $"לפי \"{S(source, "title")}\" (גרסה {S(source, "version")})";

    private static string S(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString()! : v.ToString() : "";

    private static string RiskProfileName(string profile) => profile switch
    {
        "low" => "נמוך",
        "medium" => "בינוני",
        "high" => "גבוה",
        _ => profile
    };

    private static HashSet<string> MentionedAssets(string text)
    {
        var assets = AssetPattern().Matches(text).Select(m => m.Value.ToUpperInvariant()).ToHashSet();
        if (text.Contains("ביטקוין")) assets.Add("BTC");
        if (text.Contains("את'ריום") || text.Contains("אתריום")) assets.Add("ETH");
        return assets;
    }

    private static string? ExtractCustomerId(string text)
    {
        var m = CustomerIdPattern().Match(text);
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }

    [GeneratedRegex(@"(?<![A-Za-z])(BTC|ETH|USDC)(?![A-Za-z])", RegexOptions.IgnoreCase)]
    private static partial Regex AssetPattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9])C-\d+", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerIdPattern();
}
