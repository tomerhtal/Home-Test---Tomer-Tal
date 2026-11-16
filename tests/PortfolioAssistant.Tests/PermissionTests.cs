using System.Text.Json;
using PortfolioAssistant.Core.Domain;
using PortfolioAssistant.Core.Guards;
using PortfolioAssistant.Core.Tools;

namespace PortfolioAssistant.Tests;

public class PermissionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task AssignedManager_GetsPortfolio()
    {
        var portfolio = await TestData.Portfolios().GetForUserAsync("C-1042", TestData.Pm317, Ct);
        Assert.NotNull(portfolio);
        Assert.Equal(3, portfolio.Holdings.Count);
    }

    [Theory]
    [InlineData("C-2000")]   // לא קיים
    [InlineData("C-3000")]   // קיים, משויך ל-pm-402
    public async Task UnknownOrUnassignedCustomer_ReturnsNull(string customerId)
    {
        Assert.Null(await TestData.Portfolios().GetForUserAsync(customerId, TestData.Pm317, Ct));
    }

    [Fact]
    public async Task UnknownUser_GetsNothing()
    {
        var stranger = new UserContext("pm-999", UserContext.PortfolioManagerRole);
        Assert.Null(await TestData.Portfolios().GetForUserAsync("C-1042", stranger, Ct));
    }

    [Theory]
    [InlineData("C-2000")]
    [InlineData("C-3000")]
    public async Task GetPortfolioTool_ForbiddenCustomer_ReturnsSameMessageAndNoData(string customerId)
    {
        var tool = new GetPortfolioTool(TestData.Portfolios());
        var json = await tool.ExecuteAsync(JsonSerializer.SerializeToElement(new { customerId }), TestData.Pm317, Ct);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal(Messages.CustomerNotAvailable, doc.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("BTC", json);
    }

    [Fact]
    public async Task SearchDocuments_ClientRole_DoesNotSeeInternalDocuments()
    {
        var tool = new SearchDocumentsTool(TestData.Documents());
        var client = new UserContext("client-1", UserContext.ClientRole);

        var json = await tool.ExecuteAsync(
            JsonSerializer.SerializeToElement(new { query = "סקירת שוק ETH" }), client, Ct);

        using var doc = JsonDocument.Parse(json);
        Assert.Empty(doc.RootElement.GetProperty("results").EnumerateArray());
    }

    [Fact]
    public async Task SearchDocuments_UnknownRole_SeesNothing()
    {
        var tool = new SearchDocumentsTool(TestData.Documents());
        var unknown = new UserContext("x", "SomethingElse");

        var json = await tool.ExecuteAsync(JsonSerializer.SerializeToElement(new { query = "דמי ניהול" }), unknown, Ct);

        using var doc = JsonDocument.Parse(json);
        Assert.Empty(doc.RootElement.GetProperty("results").EnumerateArray());
    }
}
