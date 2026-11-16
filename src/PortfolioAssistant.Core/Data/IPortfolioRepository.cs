using PortfolioAssistant.Core.Domain;

namespace PortfolioAssistant.Core.Data;

public interface IPortfolioRepository
{
    /// <summary>
    /// מחזיר את התיק רק אם המשתמש משויך ללקוח. לקוח שלא קיים ולקוח שאינו משויך
    /// מחזירים את אותו null, כדי שאי אפשר יהיה לנחש אילו לקוחות קיימים.
    /// </summary>
    Task<Portfolio?> GetForUserAsync(string customerId, UserContext user, CancellationToken ct);
}
