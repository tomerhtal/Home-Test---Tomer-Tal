using PortfolioAssistant.Core.Audit;
using PortfolioAssistant.Core.Data;
using PortfolioAssistant.Core.Domain;
using PortfolioAssistant.Core.Guards;

namespace PortfolioAssistant.Core.Orchestration;

/// <summary>
/// הצינור של כל בקשה: הרשאה ← בדיקת קלט ← הלולאה מול המודל ← לוג.
/// עוצר בשלב הראשון שאומר "לא", ורושם בלוג בכל מקרה.
/// </summary>
public sealed class AssistantService(
    IPortfolioRepository portfolios,
    IInputGuard inputGuard,
    Orchestrator orchestrator,
    IAuditLogger audit)
{
    public async Task<AssistantResponse> AskAsync(string customerId, string question, UserContext user, CancellationToken ct)
    {
        customerId = customerId.Trim();
        question = question.Trim();

        // שלב 1: האם המשתמש רשאי לראות את הלקוח? (לפני המודל: דטרמיניסטי וחינמי)
        if (await portfolios.GetForUserAsync(customerId, user, ct) is null)
            return await FinishAsync(user, customerId, question,
                new(Messages.CustomerNotAvailable, AssistantStatus.NotFound, [], [], "customer_not_available", null, 0), ct);

        // שלב 2: האם מותר לענות על השאלה? (ייעוץ ו-injection נחסמים בלי להפעיל את המודל)
        var input = await inputGuard.CheckAsync(question, ct);
        if (!input.Allowed)
            return await FinishAsync(user, customerId, question,
                new(input.Message!, AssistantStatus.Refused, [], [], input.Reason, null, 0), ct);

        // שלב 3: הלולאה מול המודל (כולל בדיקת הפלט).
        var result = await orchestrator.RunAsync(question, customerId, user, ct);
        return await FinishAsync(user, customerId, question, result, ct);
    }

    // שלב 4: לוג והחזרה.
    private async Task<AssistantResponse> FinishAsync(
        UserContext user, string customerId, string question, OrchestratorResult result, CancellationToken ct)
    {
        await audit.LogAsync(new AuditRecord(
            DateTimeOffset.UtcNow, user.UserId, customerId, question, result.Status.ToString(), result.Detail,
            result.ToolsUsed, result.Text, result.Model, result.Rounds), ct);

        return new AssistantResponse(result.Text, result.Status, result.Sources, result.ToolsUsed, result.Model);
    }
}
