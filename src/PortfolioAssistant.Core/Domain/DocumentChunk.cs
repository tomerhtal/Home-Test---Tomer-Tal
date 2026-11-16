namespace PortfolioAssistant.Core.Domain;

/// <summary>קטע ממסמך ידע, עם המטא-דאטה שלו (לסינון, לציטוט ולחוקים מובנים).</summary>
public record DocumentChunk(
    string Id,
    string Title,
    string Audience,
    string Version,
    DateOnly EffectiveDate,
    string Text,
    IReadOnlyList<string> Keywords,
    IReadOnlyDictionary<string, string> Rules);

/// <summary>תוצאת חיפוש: קטע וציון רלוונטיות.</summary>
public record SearchResult(DocumentChunk Chunk, double Score);
