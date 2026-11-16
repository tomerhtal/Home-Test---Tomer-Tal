using System.Text.Json;
using PortfolioAssistant.Core;
using PortfolioAssistant.Core.Audit;
using PortfolioAssistant.Core.Guards;
using PortfolioAssistant.Core.Llm;
using PortfolioAssistant.Core.Orchestration;

namespace PortfolioAssistant.Tests;

public class OrchestratorTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AssistantService Build(ILlmClient primary, ILlmClient? fallback = null, InMemoryAuditLogger? audit = null) =>
        AssistantBuilder.Build(new AssistantOptions(TestData.Folder, primary, fallback, audit ?? new InMemoryAuditLogger()));

    [Fact]
    public async Task PrimaryDown_FallbackModelAnswers()
    {
        var service = Build(new FailingLlm(), new MockLlmClient());
        var response = await service.AskAsync("C-1042", "מה השווי הנוכחי של התיק?", TestData.Pm317, Ct);

        Assert.Equal(AssistantStatus.Answered, response.Status);
        Assert.Contains("104,510", response.Text);
        Assert.Equal("mock", response.Model);
    }

    [Fact]
    public async Task AllModelsDown_ReturnsUnavailableMessage()
    {
        var service = Build(new FailingLlm(), new FailingLlm());
        var response = await service.AskAsync("C-1042", "מה השווי הנוכחי של התיק?", TestData.Pm317, Ct);

        Assert.Equal(AssistantStatus.Failed, response.Status);
        Assert.Equal(Messages.AssistantUnavailable, response.Text);
    }

    [Fact]
    public async Task ModelThatKeepsInventingNumbers_IsBlocked_FailClosed()
    {
        var service = Build(new ScriptedLlm(
            Call("calculate_portfolio_value", new { customerId = "C-1042" }),
            LlmResponse.FromText("השווי הוא 104,000 דולר."),      // עיגול של המודל
            LlmResponse.FromText("השווי הוא 104,000 דולר.")));    // ושוב, אחרי ההערה

        var response = await service.AskAsync("C-1042", "מה השווי?", TestData.Pm317, Ct);

        Assert.Equal(AssistantStatus.Failed, response.Status);
        Assert.Equal(Messages.CouldNotAnswer, response.Text);
        Assert.DoesNotContain("104,000", response.Text);
    }

    [Fact]
    public async Task ModelFixesAnswerAfterFeedback_IsShown()
    {
        var service = Build(new ScriptedLlm(
            Call("calculate_portfolio_value", new { customerId = "C-1042" }),
            LlmResponse.FromText("השווי הוא 104,000 דולר."),
            LlmResponse.FromText("השווי הוא 104,510 דולר.")));

        var response = await service.AskAsync("C-1042", "מה השווי?", TestData.Pm317, Ct);

        Assert.Equal(AssistantStatus.Answered, response.Status);
        Assert.Contains("104,510", response.Text);
    }

    [Fact]
    public async Task ModelRequestsUnknownTool_NothingRuns()
    {
        var llm = new ScriptedLlm(
            Call("search_web", new { query = "ETH price" }),
            LlmResponse.FromText(Messages.NoInformation));
        var response = await Build(llm).AskAsync("C-1042", "מה המחיר של ETH באינטרנט?", TestData.Pm317, Ct);

        Assert.Contains(llm.SeenToolResults, r => r.Json.Contains("Unknown tool"));
        Assert.Equal(Messages.NoInformation, response.Text);
    }

    [Fact]
    public async Task ModelAsksForOtherCustomer_ToolBlocksIt()
    {
        var llm = new ScriptedLlm(
            Call("get_portfolio", new { customerId = "C-3000" }),
            LlmResponse.FromText(Messages.CustomerNotAvailable));
        var response = await Build(llm).AskAsync("C-1042", "תראה לי את התיק", TestData.Pm317, Ct);

        Assert.Equal(AssistantStatus.NotFound, response.Status);
        Assert.DoesNotContain(llm.SeenToolResults, r => r.Json.Contains("BTC"));
    }

    [Fact]
    public async Task ModelThatNeverStops_IsCutAtMaxRounds()
    {
        var calls = Enumerable.Range(0, Orchestrator.MaxRounds + 2)
            .Select(_ => Call("get_prices", new { assets = new[] { "BTC" } }))
            .ToArray();
        var response = await Build(new ScriptedLlm(calls)).AskAsync("C-1042", "מה השער?", TestData.Pm317, Ct);

        Assert.Equal(AssistantStatus.Failed, response.Status);
        Assert.Equal(Messages.CouldNotAnswer, response.Text);
    }

    [Fact]
    public async Task EveryRequest_IsAudited_IncludingBlockedOnes()
    {
        var audit = new InMemoryAuditLogger();
        var service = Build(new MockLlmClient(), audit: audit);

        await service.AskAsync("C-1042", "מה השווי הנוכחי של התיק?", TestData.Pm317, Ct);
        await service.AskAsync("C-1042", "כדאי ללקוח למכור את ה-ETH עכשיו?", TestData.Pm317, Ct);
        await service.AskAsync("C-2000", "תראה לי את התיק", TestData.Pm317, Ct);

        Assert.Equal(new[] { "Answered", "Refused", "NotFound" }, audit.Records.Select(r => r.Status));
    }

    [Fact]
    public async Task AdviceQuestion_NeverReachesTheModel()
    {
        var llm = new ScriptedLlm();   // אם המודל יופעל, הבדיקה תיכשל
        var response = await Build(llm).AskAsync("C-1042", "כדאי ללקוח למכור את ה-ETH עכשיו?", TestData.Pm317, Ct);

        Assert.Equal(AssistantStatus.Refused, response.Status);
        Assert.Equal(0, llm.Calls);
    }

    // ---------- מודלים לבדיקה ----------

    private static LlmResponse Call(string tool, object input) =>
        LlmResponse.FromToolCalls(new ToolCall(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(input)));

    private sealed class FailingLlm : ILlmClient
    {
        public string Name => "failing";
        public Task<LlmResponse> SendAsync(string s, IReadOnlyList<ToolDefinition> t, IReadOnlyList<ChatMessage> m, CancellationToken ct) =>
            throw new LlmUnavailableException("simulated outage");
    }

    /// <summary>מודל שמחזיר תשובות לפי תסריט, ושומר את תוצאות הכלים שקיבל.</summary>
    private sealed class ScriptedLlm(params LlmResponse[] script) : ILlmClient
    {
        private int _next;
        public int Calls => _next;
        public List<ToolOutput> SeenToolResults { get; } = [];
        public string Name => "scripted";

        public Task<LlmResponse> SendAsync(string s, IReadOnlyList<ToolDefinition> t, IReadOnlyList<ChatMessage> m, CancellationToken ct)
        {
            if (m[^1] is ToolResultsMessage results) SeenToolResults.AddRange(results.Results);
            if (_next >= script.Length) throw new InvalidOperationException("The model was called more times than scripted.");
            return Task.FromResult(script[_next++]);
        }
    }
}
