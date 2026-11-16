using System.Text.Encodings.Web;
using System.Text.Json;
using PortfolioAssistant.Core.Domain;
using PortfolioAssistant.Core.Guards;

namespace PortfolioAssistant.Core.Tools;

public abstract class ToolBase : ITool
{
    protected const string CustomerIdSchema = """
        {
          "type": "object",
          "properties": {
            "customerId": { "type": "string", "description": "מזהה הלקוח, למשל C-1042" }
          },
          "required": ["customerId"]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // עברית קריאה בתוצאות
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected ToolBase(string inputSchemaJson)
    {
        using var doc = JsonDocument.Parse(inputSchemaJson);
        InputSchema = doc.RootElement.Clone();
    }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public JsonElement InputSchema { get; }

    public abstract Task<string> ExecuteAsync(JsonElement input, UserContext user, CancellationToken ct);

    protected static string ToJson(object value) => JsonSerializer.Serialize(value, JsonOptions);

    protected static string NotFound() => ToJson(new { found = false, message = Messages.CustomerNotAvailable });

    protected static string Error(string message) => ToJson(new { error = message });

    protected static string? ReadString(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object
        && input.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

    protected static object SourceOf(DocumentChunk doc) => new { id = doc.Id, title = doc.Title, version = doc.Version };
}
