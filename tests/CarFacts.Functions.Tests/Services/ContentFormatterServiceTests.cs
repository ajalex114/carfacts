using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using CarFacts.Functions.Tests.Helpers;
using FluentAssertions;

namespace CarFacts.Functions.Tests.Services;

public class ContentFormatterServiceTests
{
    private readonly ContentFormatterService _sut = new();

    private CarFactsResponse _response = null!;
    private List<UploadedMedia> _media = null!;
    private string _todayDate = "March 21";

    public ContentFormatterServiceTests()
    {
        _response = TestDataBuilder.CreateValidResponse();
        _media = TestDataBuilder.CreateUploadedMedia();
    }

    [Fact]
    public void FormatPostHtml_ContainsGeoSummaryComment()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("<!-- GEO Summary for AI Search Engines:");
        html.Should().Contain(_response.GeoSummary.Replace("&", "&amp;"));
    }

    [Fact]
    public void FormatPostHtml_ContainsSchemaArticleMarkup()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("itemtype=\"https://schema.org/Article\"");
        html.Should().Contain("itemprop=\"headline\"");
        html.Should().Contain("itemprop=\"datePublished\"");
    }

    [Fact]
    public void FormatPostHtml_ContainsTableOfContents()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("Quick Navigation");
        // Meaningful anchors: first fact is Ford Model 48 (1935), last is Tesla Model X (2015)
        html.Should().Contain("href=\"#ford-model-48-1935\"");
        html.Should().Contain("href=\"#tesla-model-x-2015\"");
    }

    [Fact]
    public void FormatPostHtml_ContainsAllFiveFacts()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        // Meaningful anchor IDs derived from car model + year
        html.Should().Contain("id=\"ford-model-48-1935\"");
        html.Should().Contain("id=\"chevrolet-bel-air-1957\"");
        html.Should().Contain("id=\"bmw-3-0-csl-1972\"");
        html.Should().Contain("id=\"ferrari-f40-1988\"");
        html.Should().Contain("id=\"tesla-model-x-2015\"");
    }

    [Fact]
    public void FormatPostHtml_ContainsFactTitlesAsH2()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        foreach (var fact in _response.Facts)
        {
            html.Should().Contain($"🏆 {System.Web.HttpUtility.HtmlEncode(fact.CatchyTitle)}");
        }
    }

    [Fact]
    public void FormatPostHtml_ContainsImageTags()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        foreach (var media in _media)
        {
            html.Should().Contain($"src=\"{media.SourceUrl}\"");
            html.Should().Contain($"wp-image-{media.MediaId}");
        }
    }

    [Fact]
    public void FormatPostHtml_ContainsNewsArticleSchema()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("itemtype=\"https://schema.org/NewsArticle\"");
    }

    [Fact]
    public void FormatPostHtml_ContainsImpactSection()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("💡 The Big Deal:");
    }

    [Fact]
    public void FormatPostHtml_ContainsWrappingUp()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("🎯 Wrapping Up");
    }

    [Fact]
    public void FormatPostHtml_ContainsFaqSection()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("itemtype=\"https://schema.org/FAQPage\"");
        html.Should().Contain("What significant automotive events happened on March 21?");
    }

    [Fact]
    public void FormatPostHtml_HtmlEncodesSpecialCharacters()
    {
        _response.MainTitle = "Cars & Speed: <Fast> \"Furious\"";
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("Cars &amp; Speed: &lt;Fast&gt; &quot;Furious&quot;");
        html.Should().NotContain("content=\"Cars & Speed: <Fast>");
    }

    [Fact]
    public void FormatPostHtml_HandlesEmptyMedia()
    {
        var html = _sut.FormatPostHtml(_response, [], _todayDate);

        html.Should().NotContain("<img ");
        html.Should().NotContain("<figure");
        html.Should().Contain("id=\"ford-model-48-1935\""); // facts still present
    }

    [Fact]
    public void FormatPostHtml_ContainsKeywordsInMeta()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("itemprop=\"keywords\"");
        foreach (var keyword in _response.Keywords)
        {
            html.Should().Contain(keyword);
        }
    }

    [Fact]
    public void FormatPostHtml_ContainsDateReference()
    {
        var html = _sut.FormatPostHtml(_response, _media, _todayDate);

        html.Should().Contain("March 21");
        html.Should().Contain("On this day in automotive history");
    }
}
