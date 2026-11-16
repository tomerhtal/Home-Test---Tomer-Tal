using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Data;

public interface IDocumentStore
{
    /// <summary>כל המסמכים (לבדיקת מקורות ולחוקים המובנים).</summary>
    IReadOnlyList<DocumentChunk> All { get; }

    /// <summary>מסמך לפי מזהה, בלי סינון קהל (לשימוש פנימי של הכלים בחוקים המובנים).</summary>
    DocumentChunk? GetById(string id);

    /// <summary>חיפוש. הסינון לפי audience קורה לפני הדירוג, כך שקטע אסור לא חוזר בכלל.</summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query, IReadOnlyCollection<string> audiences, int topK, CancellationToken ct);
}
