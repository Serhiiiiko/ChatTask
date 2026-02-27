using System.Net.Http.Json;
using DebtChat.Core.Constants;
using DebtChat.Core.Models;
using Microsoft.Extensions.Logging;

namespace DebtChat.Core.Services;

public sealed partial class TreasuryApiClient(HttpClient httpClient, ILogger<TreasuryApiClient> logger)
{
    public async Task<DebtApiResponse> GetDebtDataAsync(
        string? filter = null,
        string? sort = null,
        int? pageNumber = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        List<string> queryParams =
        [
            $"fields={TreasuryApi.Fields.DefaultSelection}",
        ];

        if (!string.IsNullOrWhiteSpace(filter))
        {
            queryParams.Add($"filter={filter}");
        }

        queryParams.Add($"sort={sort ?? $"-{TreasuryApi.Fields.RecordDate}"}");

        queryParams.Add($"page[number]={pageNumber ?? TreasuryApi.Defaults.DefaultPageNumber}");
        queryParams.Add($"page[size]={pageSize ?? TreasuryApi.Defaults.PageSize}");

        var queryString = string.Join("&", queryParams);
        var requestUri = $"{TreasuryApi.DebtToPennyEndpoint}?{queryString}";

        LogRequestingTreasuryApi(requestUri);

        var response = await httpClient.GetFromJsonAsync<DebtApiResponse>(
            requestUri, cancellationToken);

        if (response is null)
        {
            LogNullResponse(requestUri);
            throw new InvalidOperationException("Treasury API returned an empty response.");
        }

        LogTreasuryApiResponse(response.Meta.Count, response.Meta.TotalCount);

        return response;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Requesting Treasury API: {RequestUri}")]
    partial void LogRequestingTreasuryApi(string requestUri);

    [LoggerMessage(Level = LogLevel.Error, Message = "Treasury API returned null response for: {RequestUri}")]
    partial void LogNullResponse(string requestUri);

    [LoggerMessage(Level = LogLevel.Information, Message = "Treasury API returned {Count} records (total: {TotalCount})")]
    partial void LogTreasuryApiResponse(int count, int totalCount);
}
