using System.Globalization;
using System.Text.RegularExpressions;
using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Data;

/// <summary>
/// חיפוש מילות מפתח על מסמכי Markdown עם front matter.
/// נימוק: ארבעה מסמכים קצרים לא מצדיקים embeddings ו-vector store. החיפוש צפוי ונבדק.
/// בייצור: מימוש מעל Amazon Bedrock Knowledge Bases מאחורי אותו ממשק.
/// </summary>
public sealed class KeywordDocumentStore : IDocumentStore
{
    private readonly List<IndexedDocument> _documents;

    public KeywordDocumentStore(string documentsFolder)
    {
        _documents = Directory.GetFiles(documentsFolder, "*.md")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => Index(ParseFile(File.ReadAllText(f), Path.GetFileName(f))))
            .ToList();
    }

    public IReadOnlyList<DocumentChunk> All => _documents.Select(d => d.Chunk).ToList();

    public DocumentChunk? GetById(string id) =>
        _documents.FirstOrDefault(d => d.Chunk.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Chunk;

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query, IReadOnlyCollection<string> audiences, int topK, CancellationToken ct)
    {
        var queryTokens = Tokenizer.Tokenize(query).ToHashSet();

        IReadOnlyList<SearchResult> results = _documents
            .Where(d => audiences.Contains(d.Chunk.Audience, StringComparer.OrdinalIgnoreCase)) // 1. סינון הרשאות
            .Select(d => new SearchResult(d.Chunk, Score(queryTokens, d)))                       // 2. דירוג
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Chunk.Id, StringComparer.Ordinal)
            .Take(topK)
            .ToList();

        return Task.FromResult(results);
    }

    // מילה שמופיעה בכותרת או בטקסט: נקודה. מילה שמופיעה במילות המפתח: נקודה נוספת.
    private static double Score(HashSet<string> queryTokens, IndexedDocument doc) =>
        queryTokens.Sum(t => (doc.ContentTokens.Contains(t) ? 1 : 0) + (doc.KeywordTokens.Contains(t) ? 1 : 0));

    private static IndexedDocument Index(DocumentChunk chunk) => new(
        chunk,
        Tokenizer.Tokenize(chunk.Title + " " + chunk.Text).ToHashSet(),
        Tokenizer.Tokenize(string.Join(' ', chunk.Keywords)).ToHashSet());

    internal static DocumentChunk ParseFile(string content, string fileName)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
            throw new InvalidDataException($"{fileName}: missing front matter");

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 1;
        for (; i < lines.Length && lines[i].Trim() != "---"; i++)
        {
            var sep = lines[i].IndexOf(':');
            if (sep <= 0) continue;
            meta[lines[i][..sep].Trim()] = lines[i][(sep + 1)..].Trim();
        }
        var text = string.Join('\n', lines.Skip(i + 1)).Trim();

        string Required(string key) => meta.TryGetValue(key, out var v) && v.Length > 0
            ? v : throw new InvalidDataException($"{fileName}: missing '{key}'");

        var rules = meta.Where(kv => kv.Key.StartsWith("rule.", StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(kv => kv.Key["rule.".Length..], kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var keywords = meta.TryGetValue("keywords", out var k)
            ? k.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();

        return new DocumentChunk(
            Required("id"), Required("title"), Required("audience"), Required("version"),
            DateOnly.ParseExact(Required("effectiveDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            text, keywords, rules);
    }

    private sealed record IndexedDocument(DocumentChunk Chunk, HashSet<string> ContentTokens, HashSet<string> KeywordTokens);
}

/// <summary>
/// פירוק טקסט למילים, עם טיפול בסיסי בעברית: הסרת תחילית אחת נפוצה (ו, ה, ב, ל, מ, ש, כ),
/// כך ש"בסקירת השוק" יתאים ל"סקירת שוק". אותו נרמול חל על השאלה ועל המסמכים.
/// </summary>
internal static class Tokenizer
{
    private static readonly HashSet<char> HebrewPrefixes = ['ו', 'ה', 'ב', 'ל', 'מ', 'ש', 'כ'];

    private static readonly HashSet<string> StopWords =
    [
        "מה", "של", "על", "את", "האם", "כמה", "זה", "זו", "יש", "לי", "אני", "הוא", "היא", "עם", "או", "גם", "כל",
        "the", "is", "what", "of", "an", "and", "to", "in"
    ];

    public static IEnumerable<string> Tokenize(string text)
    {
        foreach (var raw in Regex.Split(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+"))
        {
            if (raw.Length < 2 || StopWords.Contains(raw)) continue;
            yield return Normalize(raw);
        }
    }

    private static string Normalize(string token) =>
        token.Length > 3 && HebrewPrefixes.Contains(token[0]) ? token[1..] : token;
}
