using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PortfolioAssistant.Core.Audit;

/// <summary>
/// לוג ביקורת לקובץ JSON Lines (שורה לכל בקשה). מספרים שנראים כמו ת"ז או כרטיס אשראי ממוסכים.
/// בייצור: CloudWatch עם הרשאות גישה מוגבלות ותקופת שמירה לפי הרגולציה.
/// </summary>
public sealed partial class FileAuditLogger : IAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _folder;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileAuditLogger(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
    }

    public async Task LogAsync(AuditRecord record, CancellationToken ct)
    {
        var masked = record with { Question = Mask(record.Question), Answer = Mask(record.Answer) };
        var line = JsonSerializer.Serialize(masked, JsonOptions) + Environment.NewLine;
        var path = Path.Combine(_folder, $"audit-{record.At:yyyyMMdd}.jsonl");

        await _lock.WaitAsync(ct);
        try { await File.AppendAllTextAsync(path, line, ct); }
        finally { _lock.Release(); }
    }

    internal static string Mask(string text) => SensitiveNumberPattern().Replace(text, "[masked]");

    // 9 ספרות (ת"ז) או 13-19 ספרות (כרטיס), עם רווחים או מקפים אופציונליים בכרטיס.
    [GeneratedRegex(@"(?<!\d)(\d{9}|(?:\d[ -]?){12,18}\d)(?!\d)")]
    private static partial Regex SensitiveNumberPattern();
}
