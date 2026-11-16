using System.Text.Json;
using PortfolioAssistant.Core.Calculations;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Tools;

/// <summary>
/// חיפוש במסמכי הידע. המודל קובע מה לחפש (query); הקוד קובע מה מותר לראות (audience) וכמה תוצאות (topK).
/// </summary>
public sealed class SearchDocumentsTool(IDocumentStore documents) : ToolBase("""
    {
      "type": "object",
      "properties": {
        "query": { "type": "string", "description": "מה לחפש, בניסוח קצר. למשל: תנודתיות ETH סקירת שוק" }
      },
      "required": ["query"]
    }
    """)
{
    public const int TopK = 3;

    public override string Name => "search_documents";
    public override string Description =>
        "מחפש במסמכי הידע הפנימיים: מדיניות חשיפה, דמי ניהול, מדיניות ייעוץ וסקירות שוק. " +
        "מחזיר קטעים עם שם המסמך, גרסה ותאריך. השתמש בכל שאלה על מדיניות, עמלות או מחקר.";

    public override async Task<string> ExecuteAsync(JsonElement input, UserContext user, CancellationToken ct)
    {
        var query = ReadString(input, "query");
        if (string.IsNullOrEmpty(query)) return Error("query is required");

        var results = await documents.SearchAsync(query, AllowedAudiences(user), TopK, ct);
        return ToJson(new
        {
            results = results.Select(r => new
            {
                id = r.Chunk.Id,
                title = r.Chunk.Title,
                version = r.Chunk.Version,
                effectiveDate = Fmt.Date(r.Chunk.EffectiveDate),
                text = r.Chunk.Text,
                score = r.Score
            })
        });
    }

    /// <summary>מיפוי תפקיד לקהלים מותרים. תפקיד לא מוכר לא רואה כלום.</summary>
    public static IReadOnlyCollection<string> AllowedAudiences(UserContext user) => user.Role switch
    {
        UserContext.PortfolioManagerRole => ["internal", "client"],
        UserContext.ClientRole => ["client"],
        _ => []
    };
}
