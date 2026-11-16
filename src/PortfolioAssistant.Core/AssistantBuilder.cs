using PortfolioAssistant.Core.Audit;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Guards;
using PortfolioAssistant.Core.Llm;
using PortfolioAssistant.Core.Orchestration;
using PortfolioAssistant.Core.Tools;

namespace PortfolioAssistant.Core;

public record AssistantOptions(
    string DataFolder,
    ILlmClient Primary,
    ILlmClient? Fallback,
    IAuditLogger Audit,
    TimeSpan? MaxPriceAge = null);

/// <summary>
/// הרכבת כל הרכיבים במקום אחד, כדי שה-API, ה-eval והבדיקות ישתמשו בדיוק באותה מערכת.
/// </summary>
public static class AssistantBuilder
{
    public static AssistantService Build(AssistantOptions options)
    {
        var portfolios = new JsonPortfolioRepository(options.DataFolder);
        var prices = new JsonPriceProvider(options.DataFolder, options.MaxPriceAge);
        var documents = new KeywordDocumentStore(Path.Combine(options.DataFolder, "documents"));

        var tools = new ToolRegistry(
        [
            new GetPortfolioTool(portfolios),
            new GetPricesTool(prices),
            new CalculatePortfolioValueTool(portfolios, prices),
            new CheckExposurePolicyTool(portfolios, prices, documents),
            new CalculateManagementFeeTool(portfolios, prices, documents),
            new SearchDocumentsTool(documents)
        ]);

        var orchestrator = new Orchestrator(options.Primary, options.Fallback, tools, new OutputGuard(documents), documents);
        return new AssistantService(portfolios, new InputGuard(), orchestrator, options.Audit);
    }

    /// <summary>מאתר את תיקיית data: ליד הקבצים הבינאריים, או בתיקיות שמעל (בהרצה מתוך הקוד).</summary>
    public static string FindDataFolder(string? start = null)
    {
        var dir = new DirectoryInfo(start ?? AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "portfolios.json"))) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find the 'data' folder.");
    }
}
