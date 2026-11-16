namespace PortfolioAssistant.Core.Tools;

/// <summary>רשימת הכלים. ה-Orchestrator שולח מכאן את ההגדרות למודל, ומריץ רק כלים שרשומים כאן.</summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools) =>
        _tools = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);

    public IReadOnlyCollection<ITool> All => _tools.Values;

    public bool TryGet(string name, out ITool tool) => _tools.TryGetValue(name, out tool!);
}
