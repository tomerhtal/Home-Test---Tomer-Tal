using System.Text.Json;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Tools;

/// <summary>
/// כלי שהמודל יכול לבקש להפעיל. המודל רק מבקש; הקוד מריץ.
/// ההרשאות מגיעות מ-UserContext, ולא מארגומנט שהמודל ממלא.
/// </summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonElement InputSchema { get; }
    Task<string> ExecuteAsync(JsonElement input, UserContext user, CancellationToken ct);
}
