using PortfolioAssistant.Core.Domain;
using PortfolioAssistant.Core.Orchestration;

namespace PortfolioAssistant.Api;

public record AskRequest(string? CustomerId, string? Question);

public static class AskEndpoint
{
    public static void MapAskEndpoint(this WebApplication app)
    {
        // נקודת הכניסה היחידה: customerId ושאלה בשפה חופשית.
        app.MapPost("/api/ask", async (AskRequest request, HttpRequest http, AssistantService assistant, CancellationToken ct) =>
        {
            // באב-טיפוס המשתמש מגיע מהכותרת X-User-Id. בייצור: אימות token של ה-SSO הארגוני.
            var userId = http.Headers["X-User-Id"].ToString().Trim();
            if (userId.Length == 0)
                return Results.Problem("Missing X-User-Id header.", statusCode: StatusCodes.Status401Unauthorized);

            if (string.IsNullOrWhiteSpace(request.CustomerId) || string.IsNullOrWhiteSpace(request.Question))
                return Results.Problem("customerId and question are required.", statusCode: StatusCodes.Status400BadRequest);

            var user = new UserContext(userId, UserContext.PortfolioManagerRole);
            var response = await assistant.AskAsync(request.CustomerId, request.Question, user, ct);
            return Results.Ok(response);
        });
    }
}
