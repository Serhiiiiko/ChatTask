using System.Net;
using System.Text.Json;
using DebtChat.Core.Models;
using DebtChat.Core.Services;
using DebtChat.Core.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DebtChat.Tests.Unit.Tools;

public class GetUsDebtToolTests
{
    [Test]
    public async Task GetUsDebtAsync_ReturnsJsonWithRecords()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [new DebtRecord("2024-12-31", "36177073901949.70", "28908004857451.48", "7269069044498.22")],
            Meta: new DebtApiMeta(Count: 1, TotalCount: 1, TotalPages: 1)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);
        var tool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        var result = await tool.GetUsDebtAsync(filter: "record_date:eq:2024-12-31");

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("36177073901949.70");
        result.ShouldContain("pagination");
    }

    [Test]
    public async Task GetUsDebtAsync_ReturnsErrorJson_OnHttpFailure()
    {
        var handler = new FakeHttpMessageHandler(statusCode: HttpStatusCode.InternalServerError);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);
        var tool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        var result = await tool.GetUsDebtAsync(filter: "record_date:eq:2024-12-31");

        result.ShouldContain("error");
    }

    [Test]
    public async Task GetUsDebtAsync_PassesFilterToApiClient()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [],
            Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);
        var tool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        await tool.GetUsDebtAsync(filter: "record_calendar_year:eq:2008", pageSize: 5);

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldContain("record_calendar_year");
        handler.LastRequestUri.Query.ShouldContain("2008");
    }

    [Test]
    public async Task GetUsDebtAsync_MultipleRecords_ReturnsAllRecords()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data:
            [
                new DebtRecord("2024-12-31", "36177073901949.70", "28908004857451.48", "7269069044498.22"),
                new DebtRecord("2024-12-30", "36100000000000.00", "28800000000000.00", "7300000000000.00"),
            ],
            Meta: new DebtApiMeta(Count: 2, TotalCount: 2, TotalPages: 1)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);
        var tool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        var result = await tool.GetUsDebtAsync();

        result.ShouldContain("2024-12-31");
        result.ShouldContain("2024-12-30");
        result.ShouldContain("36177073901949.70");
        result.ShouldContain("36100000000000.00");
    }

    [Test]
    public async Task GetUsDebtAsync_PaginationMetadata_SerializedCorrectly()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [new DebtRecord("2024-01-01", "34000000000000.00", "27000000000000.00", "7000000000000.00")],
            Meta: new DebtApiMeta(Count: 1, TotalCount: 500, TotalPages: 5)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);
        var tool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        var result = await tool.GetUsDebtAsync(pageNumber: 3, pageSize: 100);

        using var doc = JsonDocument.Parse(result);
        var pagination = doc.RootElement.GetProperty("pagination");
        pagination.GetProperty("returned").GetInt32().ShouldBe(1);
        pagination.GetProperty("total_records").GetInt32().ShouldBe(500);
        pagination.GetProperty("total_pages").GetInt32().ShouldBe(5);
        pagination.GetProperty("current_page").GetInt32().ShouldBe(3);
    }

    [Test]
    public async Task GetUsDebtAsync_SortParameter_PassedToApiClient()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [],
            Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);
        var tool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        await tool.GetUsDebtAsync(sort: "record_date");

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldContain("sort=record_date");
    }

    [Test]
    public async Task GetUsDebtAsync_NullFilter_DoesNotIncludeFilterInQuery()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [],
            Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);
        var tool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        await tool.GetUsDebtAsync(filter: null);

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldNotContain("filter=");
    }

    [Test]
    public async Task GetUsDebtAsync_ResponseUsesSnakeCaseNaming()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [new DebtRecord("2024-06-15", "35000000000000.00", "28000000000000.00", "7000000000000.00")],
            Meta: new DebtApiMeta(Count: 1, TotalCount: 1, TotalPages: 1)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);
        var tool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        var result = await tool.GetUsDebtAsync();

        // Verify snake_case naming in the output JSON
        result.ShouldContain("total_records");
        result.ShouldContain("total_pages");
        result.ShouldContain("current_page");
    }

    [Test]
    public async Task GetUsDebtAsync_DefaultPageNumber_IsOne()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [],
            Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);
        var tool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        var result = await tool.GetUsDebtAsync();

        using var doc = JsonDocument.Parse(result);
        var pagination = doc.RootElement.GetProperty("pagination");
        pagination.GetProperty("current_page").GetInt32().ShouldBe(1);
    }

    /// <summary>
    /// Fake HTTP handler for unit testing without real network calls.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly DebtApiResponse? _response;
        private readonly HttpStatusCode _statusCode;

        public Uri? LastRequestUri { get; private set; }

        public FakeHttpMessageHandler(DebtApiResponse response)
        {
            _response = response;
            _statusCode = HttpStatusCode.OK;
        }

        public FakeHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            if (_response is null)
            {
                return Task.FromResult(new HttpResponseMessage(_statusCode));
            }

            var json = JsonSerializer.Serialize(_response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
            });

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
