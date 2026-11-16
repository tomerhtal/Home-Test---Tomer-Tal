using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Amazon.Runtime.Documents;
using PortfolioAssistant.Core.Llm;

namespace PortfolioAssistant.Bedrock;

/// <summary>
/// "המתורגמן" בין הטפסים הפנימיים (LlmModels) לבין ה-Converse API של Amazon Bedrock.
/// ה-Converse API אחיד לכל המודלים שתומכים בכלים, ולכן מודל ראשי ומודל גיבוי הם אותו מחלקה עם modelId שונה.
/// הערה: המתאם נכתב לפי ה-API של Bedrock ולא נבדק מול השירות (אין גישה ל-AWS בסביבת הפיתוח). ראו README.
/// </summary>
public sealed class BedrockLlmClient(IAmazonBedrockRuntime client, string modelId, int maxTokens = 1024) : ILlmClient
{
    public string Name => modelId;

    public async Task<LlmResponse> SendAsync(
        string systemPrompt, IReadOnlyList<ToolDefinition> tools, IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        var request = new ConverseRequest
        {
            ModelId = modelId,
            System = [new SystemContentBlock { Text = systemPrompt }],
            Messages = messages.Select(ToBedrock).ToList(),
            ToolConfig = new ToolConfiguration
            {
                Tools = tools.Select(t => new Tool
                {
                    ToolSpec = new ToolSpecification
                    {
                        Name = t.Name,
                        Description = t.Description,
                        InputSchema = new ToolInputSchema { Json = DocumentJson.FromJson(t.InputSchema) }
                    }
                }).ToList()
            },
            InferenceConfig = new InferenceConfiguration { MaxTokens = maxTokens, Temperature = 0 }
        };

        ConverseResponse response;
        try
        {
            response = await client.ConverseAsync(request, ct);
        }
        catch (AmazonServiceException ex)
        {
            // עומס, timeout או שגיאת שרת: ה-Orchestrator יעבור למודל הגיבוי.
            throw new LlmUnavailableException($"Bedrock call failed for model {modelId}: {ex.ErrorCode}", ex);
        }

        var content = response.Output?.Message?.Content ?? [];

        if (response.StopReason?.Value == "tool_use")
        {
            var calls = content
                .Where(c => c.ToolUse is not null)
                .Select(c => new ToolCall(c.ToolUse.ToolUseId, c.ToolUse.Name, DocumentJson.ToJson(c.ToolUse.Input)))
                .ToList();
            return new LlmResponse(calls, null);
        }

        return LlmResponse.FromText(string.Concat(content.Where(c => c.Text is not null).Select(c => c.Text)));
    }

    // טופס פנימי ← טופס של Bedrock
    private static Message ToBedrock(ChatMessage message) => message switch
    {
        UserMessage u => new Message
        {
            Role = ConversationRole.User,
            Content = [new ContentBlock { Text = u.Text }]
        },
        AssistantText a => new Message
        {
            Role = ConversationRole.Assistant,
            Content = [new ContentBlock { Text = a.Text }]
        },
        AssistantToolRequest r => new Message
        {
            Role = ConversationRole.Assistant,
            Content = r.Calls.Select(c => new ContentBlock
            {
                ToolUse = new ToolUseBlock { ToolUseId = c.Id, Name = c.Name, Input = DocumentJson.FromJson(c.Input) }
            }).ToList()
        },
        ToolResultsMessage t => new Message
        {
            Role = ConversationRole.User,   // ב-Converse, תוצאות כלים נשלחות בהודעת user
            Content = t.Results.Select(r => new ContentBlock
            {
                ToolResult = new ToolResultBlock
                {
                    ToolUseId = r.ToolCallId,
                    Content = [new ToolResultContentBlock { Text = r.Json }]
                }
            }).ToList()
        },
        _ => throw new NotSupportedException(message.GetType().Name)
    };
}

/// <summary>המרה בין JsonElement לבין Document של ה-AWS SDK.</summary>
internal static class DocumentJson
{
    public static Document FromJson(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => new Document(e.EnumerateObject().ToDictionary(p => p.Name, p => FromJson(p.Value))),
        JsonValueKind.Array => new Document(e.EnumerateArray().Select(FromJson).ToList()),
        JsonValueKind.String => new Document(e.GetString()),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? new Document(l) : new Document(e.GetDouble()),
        JsonValueKind.True => new Document(true),
        JsonValueKind.False => new Document(false),
        _ => new Document()
    };

    public static JsonElement ToJson(Document d)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, d);
        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }

    private static void Write(Utf8JsonWriter w, Document d)
    {
        if (d.IsDictionary())
        {
            w.WriteStartObject();
            foreach (var (key, value) in d.AsDictionary()) { w.WritePropertyName(key); Write(w, value); }
            w.WriteEndObject();
        }
        else if (d.IsList()) { w.WriteStartArray(); foreach (var item in d.AsList()) Write(w, item); w.WriteEndArray(); }
        else if (d.IsString()) w.WriteStringValue(d.AsString());
        else if (d.IsBool()) w.WriteBooleanValue(d.AsBool());
        else if (d.IsInt()) w.WriteNumberValue(d.AsInt());
        else if (d.IsLong()) w.WriteNumberValue(d.AsLong());
        else if (d.IsDouble()) w.WriteNumberValue(d.AsDouble());
        else w.WriteNullValue();
    }
}
