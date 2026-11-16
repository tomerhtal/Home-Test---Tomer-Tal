namespace PortfolioAssistant.Core.Llm;

/// <summary>
/// "החוזה" של כל מודל במערכת, אמיתי או מדומה: מקבל את כל השיחה ומחזיר בקשה לכלים או טקסט סופי.
/// </summary>
public interface ILlmClient
{
    string Name { get; }

    Task<LlmResponse> SendAsync(
        string systemPrompt,
        IReadOnlyList<ToolDefinition> tools,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken ct);
}
