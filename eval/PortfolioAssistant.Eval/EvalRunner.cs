using PortfolioAssistant.Core.Domain;
using PortfolioAssistant.Core.Orchestration;

namespace PortfolioAssistant.Eval;

/// <summary>
/// מריץ כל מקרה דרך AssistantService (בדיוק הצינור של ה-API) ובודק את התוצאה מול הצפוי.
/// הבדיקות על שדות מובנים (סטטוס, כלים, מקורות) ועל טקסט שחייב או אסור שיופיע.
/// </summary>
public sealed class EvalRunner(AssistantService assistant)
{
    public async Task<EvalResult> RunAsync(EvalCase c, CancellationToken ct)
    {
        var user = new UserContext(c.UserId, UserContext.PortfolioManagerRole);
        var response = await assistant.AskAsync(c.CustomerId, c.Question, user, ct);
        var failures = new List<string>();

        if (response.Status != c.ExpectedStatus)
            failures.Add($"status: expected {c.ExpectedStatus}, got {response.Status}");

        foreach (var text in c.MustContain ?? [])
            if (!response.Text.Contains(text, StringComparison.Ordinal))
                failures.Add($"missing text: \"{text}\"");

        foreach (var text in c.MustNotContain ?? [])
            if (response.Text.Contains(text, StringComparison.Ordinal))
                failures.Add($"forbidden text present: \"{text}\"");

        foreach (var source in c.ExpectedSources ?? [])
            if (!response.Sources.Any(s => s.Id == source))
                failures.Add($"missing source: {source}");

        if (c.ExpectedTools is { } tools)
        {
            var expected = tools.Order().ToList();
            var actual = response.ToolsUsed.Distinct().Order().ToList();
            if (!expected.SequenceEqual(actual))
                failures.Add($"tools: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
        }

        return new EvalResult(c, response, failures);
    }
}
