using PortfolioAssistant.Core.Orchestration;

namespace PortfolioAssistant.Eval;

/// <summary>מקרה בדיקה: שאלה, הקשר ותוצאה צפויה.</summary>
public record EvalCase(
    string Id,
    string Category,
    bool Critical,
    string Question,
    string CustomerId,
    string UserId,
    AssistantStatus ExpectedStatus,
    string? Note = null,
    string[]? MustContain = null,
    string[]? MustNotContain = null,
    string[]? ExpectedSources = null,
    string[]? ExpectedTools = null);

public record EvalResult(EvalCase Case, AssistantResponse Response, IReadOnlyList<string> Failures)
{
    public bool Passed => Failures.Count == 0;
}
