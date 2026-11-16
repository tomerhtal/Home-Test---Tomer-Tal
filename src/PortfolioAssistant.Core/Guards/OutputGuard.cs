using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Llm;

namespace PortfolioAssistant.Core.Guards;

public record OutputCheck(bool Ok, string? Problem)
{
    public static readonly OutputCheck Pass = new(true, null);
    public static OutputCheck Fail(string problem) => new(false, problem);
}

public interface IOutputGuard
{
    Task<OutputCheck> CheckAsync(string answer, IReadOnlyList<ToolOutput> toolOutputs, CancellationToken ct);
}

/// <summary>
/// בדיקת פלט לפני שהתשובה מוצגת:
/// 1. כל מספר בתשובה מופיע בתוצאות הכלים (המודל לא חישב, עיגל או המציא).
/// 2. כל מסמך שצוטט אכן נשלף, ותשובה מבוססת מסמכים מצטטת מקור.
/// 3. אין ביטויי המלצה (ייעוץ עקיף שהוסיף המודל לשאלה תמימה).
/// בייצור מוסיפים כאן מודל שופט לייעוץ עקיף ו-Bedrock Guardrails ל-PII ול-grounding.
/// </summary>
public sealed partial class OutputGuard(IDocumentStore documents) : IOutputGuard
{
    public Task<OutputCheck> CheckAsync(string answer, IReadOnlyList<ToolOutput> toolOutputs, CancellationToken ct) =>
        Task.FromResult(Check(answer, toolOutputs));

    public OutputCheck Check(string answer, IReadOnlyList<ToolOutput> toolOutputs)
    {
        // 1. מספרים
        var allowed = toolOutputs.SelectMany(o => ExtractNumbers(o.Json)).ToHashSet();
        var orphans = ExtractNumbers(answer).Where(n => !allowed.Contains(n)).Distinct().ToList();
        if (orphans.Count > 0)
            return OutputCheck.Fail(
                "numbers not returned by any tool: " + string.Join(", ", orphans.Select(n => n.ToString(CultureInfo.InvariantCulture))));

        // 2. מקורות
        var retrievedTitles = documents.All
            .Where(d => toolOutputs.Any(o => o.Json.Contains(d.Title, StringComparison.Ordinal)))
            .Select(d => d.Title)
            .ToHashSet();
        var citedTitles = documents.All.Where(d => answer.Contains(d.Title, StringComparison.Ordinal)).Select(d => d.Title).ToList();

        var invented = citedTitles.Where(t => !retrievedTitles.Contains(t)).ToList();
        if (invented.Count > 0)
            return OutputCheck.Fail("cited a document that was not retrieved: " + string.Join(", ", invented));

        var searchReturnedResults = toolOutputs
            .Where(o => o.ToolName == "search_documents")
            .Any(o => SearchResultTexts(o.Json).Any());
        if (searchReturnedResults && answer != Messages.NoInformation && citedTitles.Count == 0)
            return OutputCheck.Fail("document-based answer without a source");

        // 3. ייעוץ. מסירים קודם ציטוטים מילוליים ממסמכים (למשל מדיניות הייעוץ עצמה מזכירה "כדאי לי לקנות").
        var withoutQuotes = toolOutputs
            .Where(o => o.ToolName == "search_documents")
            .SelectMany(o => SearchResultTexts(o.Json))
            .Aggregate(answer, (text, quote) => text.Replace(quote, " ", StringComparison.Ordinal));
        if (AdvicePatterns.Any(p => p.IsMatch(withoutQuotes)))
            return OutputCheck.Fail("answer contains a recommendation");

        return OutputCheck.Pass;
    }

    internal static IEnumerable<decimal> ExtractNumbers(string text)
    {
        // מספרים שאינם כמויות: תאריכים, שעות ומזהי לקוח.
        var cleaned = NonQuantityPattern().Replace(text, " ");
        foreach (Match m in NumberPattern().Matches(cleaned))
        {
            var raw = m.Value.Replace(",", "");
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                yield return value;
        }
    }

    private static IEnumerable<string> SearchResultTexts(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results)) return [];
        return results.EnumerateArray()
            .Select(r => r.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "")
            .Where(t => t.Length > 0)
            .ToList();
    }

    private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    internal static readonly Regex[] AdvicePatterns =
    [
        new(@"כדאי\s+(\S+\s+){0,3}?(לקנות|למכור|להחזיק|להשקיע|לצאת|להגדיל|להקטין|לממש|לשקול|לאזן)", Opt),
        new(@"מומלץ", Opt),
        new(@"(אני|הייתי)\s+(ממליץ|ממליצה|מציע|מציעה)", Opt),
        new(@"should\s+(buy|sell|hold|consider)", Opt),
        new(@"recommend", Opt)
    ];

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}(T[\d:.]+Z?)?|\d{1,2}\.\d{1,2}\.\d{4}|\d{1,2}:\d{2}|(?<![A-Za-z])[A-Za-z]-\d+")]
    private static partial Regex NonQuantityPattern();

    [GeneratedRegex(@"(?<![\p{L}\d.])\d[\d,]*(\.\d+)?")]
    private static partial Regex NumberPattern();
}
