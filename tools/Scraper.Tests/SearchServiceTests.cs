using System.Net;
using System.Text;
using System.Text.Json;
using ModernLNUElectronicsWebSite.Data;
using ModernLNUElectronicsWebSite.Search;
using Xunit;

namespace ModernLNUElectronicsWebSite.Scraper.Tests;

public class SearchServiceTests
{
    private static readonly SearchDoc[] Index =
    [
        Doc("staff:velhosh", SearchKind.Staff, "ВЕЛЬГОШ Сергій Романович", "Деканат · заступник декана"),
        Doc("staff:velhosh-a", SearchKind.Staff, "ВЕЛЬГОШ Андрій Сергійович", "Кафедра оптоелектроніки · аспірант"),
        Doc("course:dm", SearchKind.Course, "Дискретна математика", "Дисципліна · ВЕЛЬГОШ Сергій Романович"),
        Doc("course:alg", SearchKind.Course, "Алгоритми та структури даних", "Дисципліна · ВЕЛЬГОШ Сергій Романович"),
        Doc("news:zbory", SearchKind.News, "Збори трудового колективу", "01.06.2026", "виступив Вельгош"),
        Doc("page:contacts", SearchKind.Page, "Контакти", "Розділ дзеркала", "адреса телефон"),
    ];

    private static SearchDoc Doc(string id, SearchKind kind, string title, string subtitle, string text = "") =>
        new(id, kind, title, id.Replace(':', '/'), null, subtitle, text, null);

    private static async Task<SearchService> LoadedAsync()
    {
        var json = JsonSerializer.Serialize(Index, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("https://mirror.test/") };
        var service = new SearchService(http);

        await service.EnsureLoadedAsync();
        return service;
    }

    [Fact]
    public async Task FindsAcrossKinds()
    {
        var results = (await LoadedAsync()).Query("Вельгош");

        Assert.Equal(5, results.Total);
        Assert.Equal(5, results.Hits.Count);
    }

    [Fact]
    public async Task KeepsCountsStableWhileFiltering()
    {
        var service = await LoadedAsync();

        var all = service.Query("Вельгош");
        var courses = service.Query("Вельгош", SearchKind.Course);

        Assert.Equal(all.Counts, courses.Counts);
        Assert.Equal(all.Total, courses.Total);
        Assert.Equal(2, courses.Hits.Count);
        Assert.All(courses.Hits, h => Assert.Equal(SearchKind.Course, h.Doc.Kind));
    }

    [Fact]
    public async Task OrdersCountsBySize()
    {
        var results = (await LoadedAsync()).Query("Вельгош");

        Assert.Equal([SearchKind.Staff, SearchKind.Course, SearchKind.News], results.Counts.Select(c => c.Kind));
        Assert.Equal([2, 2, 1], results.Counts.Select(c => c.Count));
    }

    [Fact]
    public async Task LimitAppliesAfterFiltering()
    {
        var results = (await LoadedAsync()).Query("Вельгош", SearchKind.Course, limit: 1);

        Assert.Single(results.Hits);
        Assert.Equal(5, results.Total);
    }

    [Fact]
    public async Task RequiresEveryWordToMatch()
    {
        var service = await LoadedAsync();

        Assert.Equal(2, service.Query("Вельгош математика").Total + service.Query("Вельгош дискретна").Total);
        Assert.Equal(0, service.Query("Вельгош астрономія").Total);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankQueryFindsNothing(string? query) =>
        Assert.Empty((await LoadedAsync()).Query(query).Hits);

    [Fact]
    public async Task SurvivesMissingIndex()
    {
        var http = new HttpClient(new StubHandler(null)) { BaseAddress = new Uri("https://mirror.test/") };
        var service = new SearchService(http);

        await service.EnsureLoadedAsync();

        Assert.True(service.IsLoaded);
        Assert.Equal(0, service.DocumentCount);
        Assert.Empty(service.Query("Вельгош").Hits);
    }

    private sealed class StubHandler(string? json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                });
    }
}
