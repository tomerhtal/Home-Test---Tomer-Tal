using System.Text.Json.Serialization;

namespace PortfolioAssistant.Core.Orchestration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssistantStatus
{
    /// <summary>נענה כרגיל.</summary>
    Answered,
    /// <summary>סורב: בקשת ייעוץ, ניסיון תמרון או שאלה לא תקינה.</summary>
    Refused,
    /// <summary>הלקוח לא קיים או שהמשתמש לא משויך אליו (אותה תשובה בשני המקרים).</summary>
    NotFound,
    /// <summary>משהו לא עבד: התשובה נכשלה בבדיקות, נתונים לא זמינים או המודל לא זמין.</summary>
    Failed
}

public record SourceRef(string Id, string Title, string Version);

/// <summary>מה שחוזר למסך. ה-eval בודק את השדות האלה ישירות.</summary>
public record AssistantResponse(
    string Text,
    AssistantStatus Status,
    IReadOnlyList<SourceRef> Sources,
    IReadOnlyList<string> ToolsUsed,
    string? Model);
