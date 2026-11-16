using System.Text.Json;

namespace PortfolioAssistant.Core.Llm;

// "הטפסים" הפנימיים בין ה-Orchestrator למודל. לא תלויים באף ספק מודל.

public record ToolDefinition(string Name, string Description, JsonElement InputSchema);

/// <summary>המודל מבקש להריץ כלי.</summary>
public record ToolCall(string Id, string Name, JsonElement Input);

/// <summary>מה שכלי החזיר (JSON כמחרוזת).</summary>
public record ToolOutput(string ToolCallId, string ToolName, string Json);

public abstract record ChatMessage;
public record UserMessage(string Text) : ChatMessage;
public record AssistantToolRequest(IReadOnlyList<ToolCall> Calls) : ChatMessage;
public record ToolResultsMessage(IReadOnlyList<ToolOutput> Results) : ChatMessage;
public record AssistantText(string Text) : ChatMessage;

/// <summary>תשובת המודל: או בקשה לכלים, או טקסט סופי.</summary>
public record LlmResponse(IReadOnlyList<ToolCall> ToolCalls, string? Text)
{
    public bool WantsTools => ToolCalls.Count > 0;
    public static LlmResponse FromText(string text) => new([], text);
    public static LlmResponse FromToolCalls(params ToolCall[] calls) => new(calls, null);
}

/// <summary>המודל לא זמין (timeout, עומס, שגיאת שרת). מצדיק מעבר למודל גיבוי.</summary>
public sealed class LlmUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
