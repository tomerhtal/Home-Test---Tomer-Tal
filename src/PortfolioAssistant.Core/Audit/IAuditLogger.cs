namespace PortfolioAssistant.Core.Audit;

/// <summary>רשומת ביקורת לכל בקשה, כולל בקשות שנחסמו.</summary>
public record AuditRecord(
    DateTimeOffset At,
    string UserId,
    string CustomerId,
    string Question,
    string Status,
    string? Detail,
    IReadOnlyList<string> ToolsUsed,
    string Answer,
    string? Model,
    int Rounds);

public interface IAuditLogger
{
    Task LogAsync(AuditRecord record, CancellationToken ct);
}

/// <summary>לבדיקות ול-eval: שומר את הרשומות בזיכרון.</summary>
public sealed class InMemoryAuditLogger : IAuditLogger
{
    private readonly List<AuditRecord> _records = [];
    public IReadOnlyList<AuditRecord> Records { get { lock (_records) return _records.ToList(); } }

    public Task LogAsync(AuditRecord record, CancellationToken ct)
    {
        lock (_records) _records.Add(record);
        return Task.CompletedTask;
    }
}
