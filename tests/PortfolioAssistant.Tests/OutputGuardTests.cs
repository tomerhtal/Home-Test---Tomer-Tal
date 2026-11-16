using PortfolioAssistant.Core.Guards;
using PortfolioAssistant.Core.Llm;

namespace PortfolioAssistant.Tests;

public class OutputGuardTests
{
    private readonly OutputGuard _guard = new(TestData.Documents());

    private static readonly ToolOutput ValueOutput = new("1", "calculate_portfolio_value",
        """{"found":true,"customerId":"C-1042","totalValueUsd":"104,510","holdingsAsOf":"30.09.2026","pricesAsOf":"10.11.2026 14:32 UTC"}""");

    private static readonly ToolOutput ExposureOutput = new("2", "check_exposure_policy",
        """{"found":true,"limit":"60%","assets":[{"asset":"BTC","exposure":"51.24%","exceedsLimit":false}],"source":{"id":"exposure-policy","title":"מדיניות חשיפה","version":"1"}}""");

    private static readonly ToolOutput AdvisorySearch = new("3", "search_documents",
        """{"results":[{"id":"advisory-policy","title":"מדיניות ייעוץ","version":"1","text":"העוזר אינו ממליץ על פעולות. לשאלות מסוג \"כדאי לי לקנות/למכור\" יש להפנות ליועץ מורשה.","score":3}]}""");

    [Fact]
    public void NumbersFromTools_Pass()
    {
        var result = _guard.Check("השווי של C-1042 הוא 104,510 דולר, נכון ל-30.09.2026 14:32.", [ValueOutput]);
        Assert.True(result.Ok, result.Problem);
    }

    [Fact]
    public void RoundedByModel_Fails()
    {
        // הכלי החזיר 51.24%; המודל עיגל ל-51.2%. גם עיגול הוא חישוב של המודל.
        var result = _guard.Check("לפי \"מדיניות חשיפה\" (גרסה 1), החשיפה ל-BTC היא 51.2%.", [ExposureOutput]);
        Assert.False(result.Ok);
        Assert.Contains("51.2", result.Problem);
    }

    [Fact]
    public void InventedNumber_Fails()
    {
        Assert.False(_guard.Check("השווי הוא 120,000 דולר.", [ValueOutput]).Ok);
    }

    [Fact]
    public void CitingDocumentThatWasNotRetrieved_Fails()
    {
        var result = _guard.Check("לפי \"דמי ניהול\", השווי הוא 104,510 דולר.", [ValueOutput]);
        Assert.False(result.Ok);
        Assert.Contains("not retrieved", result.Problem);
    }

    [Fact]
    public void DocumentAnswerWithoutSource_Fails()
    {
        var result = _guard.Check("העוזר אינו ממליץ על פעולות.", [AdvisorySearch]);
        Assert.False(result.Ok);
        Assert.Contains("without a source", result.Problem);
    }

    [Fact]
    public void RecommendationAddedByModel_Fails()
    {
        var answer = "לפי \"מדיניות חשיפה\" (גרסה 1), החשיפה ל-BTC היא 51.24%. כדאי לשקול להקטין את החשיפה.";
        var result = _guard.Check(answer, [ExposureOutput]);
        Assert.False(result.Ok);
        Assert.Contains("recommendation", result.Problem);
    }

    [Fact]
    public void QuotingTheAdvisoryPolicy_IsNotTreatedAsAdvice()
    {
        var answer = "לפי \"מדיניות ייעוץ\" (גרסה 1): העוזר אינו ממליץ על פעולות. לשאלות מסוג \"כדאי לי לקנות/למכור\" יש להפנות ליועץ מורשה.";
        var result = _guard.Check(answer, [AdvisorySearch]);
        Assert.True(result.Ok, result.Problem);
    }

    [Fact]
    public void ExtractNumbers_IgnoresDatesTimesAndCustomerIds()
    {
        var numbers = OutputGuard.ExtractNumbers("C-1042 ב-30.09.2026 14:32: 104,510 ו-51.24%").ToList();
        Assert.Equal(new[] { 104_510m, 51.24m }, numbers);
    }
}
