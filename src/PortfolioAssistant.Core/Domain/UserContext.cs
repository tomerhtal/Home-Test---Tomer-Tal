namespace PortfolioAssistant.Core.Domain;

/// <summary>
/// מי המשתמש המחובר. בייצור נבנה מה-token של ה-SSO; באב-טיפוס מהכותרת X-User-Id.
/// המודל לעולם לא רואה ולא משנה את האובייקט הזה.
/// </summary>
public record UserContext(string UserId, string Role)
{
    public const string PortfolioManagerRole = "PortfolioManager";
    public const string ClientRole = "Client";
}
