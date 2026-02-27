using System.Net;
using System.Text.Json;
using DebtChat.Core.Models;
using DebtChat.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DebtChat.Tests.Unit.Services;

public class TreasuryApiClientTests
{
    [Test]
    public async Task GetDebtDataAsync_SuccessfulResponse_ReturnsDeserializedData()
    {
        var expected = new DebtApiResponse(
            Data: [new DebtRecord("2024-12-31", "36177073901949.70", "28908004857451.48", "7269069044498.22")],
            Meta: new DebtApiMeta(Count: 1, TotalCount: 1, TotalPages: 1));
        var handler = new FakeHttpMessageHandler(expected);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        var result = await client.GetDebtDataAsync(filter: "record_date:eq:2024-12-31");

        result.Data.Count.ShouldBe(1);
        result.Data[0].RecordDate.ShouldBe("2024-12-31");
        result.Data[0].TotalPublicDebtOutstanding.ShouldBe("36177073901949.70");
        result.Data[0].DebtHeldByPublic.ShouldBe("28908004857451.48");
        result.Data[0].IntragovernmentalHoldings.ShouldBe("7269069044498.22");
        result.Meta.Count.ShouldBe(1);
        result.Meta.TotalCount.ShouldBe(1);
        result.Meta.TotalPages.ShouldBe(1);
    }

    [Test]
    public async Task GetDebtDataAsync_DefaultSort_IsDescendingRecordDate()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [], Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await client.GetDebtDataAsync();

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldContain("sort=-record_date");
    }

    [Test]
    public async Task GetDebtDataAsync_DefaultPageSize_Is100()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [], Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await client.GetDebtDataAsync();

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldContain("page[size]=100");
    }

    [Test]
    public async Task GetDebtDataAsync_DefaultPageNumber_IsOne()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [], Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await client.GetDebtDataAsync();

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldContain("page[number]=1");
    }

    [Test]
    public async Task GetDebtDataAsync_WithFilter_IncludesFilterInQuery()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [], Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await client.GetDebtDataAsync(filter: "record_date:gte:2024-01-01,record_date:lte:2024-12-31");

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldContain("filter=record_date");
    }

    [Test]
    public async Task GetDebtDataAsync_NullFilter_ExcludesFilterFromQuery()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [], Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await client.GetDebtDataAsync(filter: null);

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldNotContain("filter=");
    }

    [Test]
    public async Task GetDebtDataAsync_CustomSort_OverridesDefault()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [], Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await client.GetDebtDataAsync(sort: "record_date");

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldContain("sort=record_date");
        handler.LastRequestUri.Query.ShouldNotContain("sort=-record_date");
    }

    [Test]
    public async Task GetDebtDataAsync_CustomPagination_OverridesDefaults()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [], Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await client.GetDebtDataAsync(pageNumber: 3, pageSize: 50);

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldContain("page[number]=3");
        handler.LastRequestUri.Query.ShouldContain("page[size]=50");
    }

    [Test]
    public async Task GetDebtDataAsync_IncludesDefaultFieldsSelection()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [], Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await client.GetDebtDataAsync();

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.Query.ShouldContain("fields=record_date");
        handler.LastRequestUri.Query.ShouldContain("tot_pub_debt_out_amt");
        handler.LastRequestUri.Query.ShouldContain("debt_held_public_amt");
        handler.LastRequestUri.Query.ShouldContain("intragov_hold_amt");
    }

    [Test]
    public async Task GetDebtDataAsync_UsesCorrectEndpoint()
    {
        var handler = new FakeHttpMessageHandler(new DebtApiResponse(
            Data: [], Meta: new DebtApiMeta(Count: 0, TotalCount: 0, TotalPages: 0)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await client.GetDebtDataAsync();

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri.AbsolutePath.ShouldBe("/services/api/fiscal_service/v2/accounting/od/debt_to_penny");
    }

    [Test]
    public async Task GetDebtDataAsync_HttpError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        await Should.ThrowAsync<HttpRequestException>(
            () => client.GetDebtDataAsync());
    }

    [Test]
    public async Task GetDebtDataAsync_MultipleRecords_ReturnsAll()
    {
        var expected = new DebtApiResponse(
            Data:
            [
                new DebtRecord("2024-12-31", "36177073901949.70", "28908004857451.48", "7269069044498.22"),
                new DebtRecord("2024-12-30", "36100000000000.00", "28800000000000.00", "7300000000000.00"),
                new DebtRecord("2024-12-29", "36050000000000.00", "28750000000000.00", "7300000000000.00"),
            ],
            Meta: new DebtApiMeta(Count: 3, TotalCount: 3, TotalPages: 1));
        var handler = new FakeHttpMessageHandler(expected);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var client = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        var result = await client.GetDebtDataAsync();

        result.Data.Count.ShouldBe(3);
        result.Meta.Count.ShouldBe(3);
    }

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
