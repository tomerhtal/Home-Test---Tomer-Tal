using PortfolioAssistant.Core.Calculations;
using PortfolioAssistant.Core.Data;

namespace PortfolioAssistant.Tests;

public class DocumentStoreTests
{
    private static readonly string[] Internal = ["internal"];
    private readonly KeywordDocumentStore _store = TestData.Documents();

    [Fact]
    public void LoadsAllFourDocumentsWithMetadata()
    {
        Assert.Equal(4, _store.All.Count);
        Assert.All(_store.All, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Title));
            Assert.False(string.IsNullOrWhiteSpace(d.Version));
            Assert.False(string.IsNullOrWhiteSpace(d.Text));
        });
    }

    [Theory]
    [InlineData("מה נאמר בסקירת השוק האחרונה על ETH?", "market-review-2026-09")]
    [InlineData("מה אומרת מדיניות הייעוץ?", "advisory-policy")]
    [InlineData("מה שיעור דמי הניהול השנתי?", "management-fees")]
    [InlineData("מה התקרה במדיניות החשיפה?", "exposure-policy")]
    public async Task Search_FindsTheRightDocumentFirst(string query, string expectedId)
    {
        var results = await _store.SearchAsync(query, Internal, topK: 3, CancellationToken.None);
        Assert.NotEmpty(results);
        Assert.Equal(expectedId, results[0].Chunk.Id);
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmpty()
    {
        var results = await _store.SearchAsync("מי פיתח את רשת אתריום?", Internal, 3, CancellationToken.None);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_FiltersByAudienceBeforeRanking()
    {
        var results = await _store.SearchAsync("סקירת שוק ETH", ["client"], 3, CancellationToken.None);
        Assert.Empty(results);
    }

    // רשת ביטחון: הערך המובנה במטא-דאטה חייב להתאים לטקסט שבני אדם קוראים.
    [Fact]
    public void ExposureLimit_InMetadata_MatchesDocumentText()
    {
        var doc = _store.GetById(PolicyRules.ExposureDocumentId)!;
        var limit = PolicyRules.ExposureLimit(doc, "medium")!.Value;
        Assert.Contains(Fmt.Percent(limit * 100), doc.Text);
    }

    [Fact]
    public void AnnualFeeRate_InMetadata_MatchesDocumentText()
    {
        var doc = _store.GetById(PolicyRules.FeesDocumentId)!;
        Assert.Contains(Fmt.Percent(PolicyRules.AnnualFeeRate(doc) * 100), doc.Text);
        Assert.Equal(4, PolicyRules.CollectionsPerYear(doc));
        Assert.Contains("רבעונית", doc.Text);
    }
}
