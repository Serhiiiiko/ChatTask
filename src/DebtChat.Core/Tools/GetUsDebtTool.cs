using System.ComponentModel;
using System.Text.Json;
using DebtChat.Core.Services;
using Microsoft.Extensions.Logging;

namespace DebtChat.Core.Tools;

/// <summary>
/// MCP tool that fetches U.S. public debt data from the Treasury "Debt to the Penny" API.
/// This is the ONLY allowed way to retrieve debt data.
/// </summary>
public sealed partial class GetUsDebtTool(TreasuryApiClient treasuryApiClient, ILogger<GetUsDebtTool> logger)
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Description("""
        Fetches U.S. public debt data from the Treasury "Debt to the Penny" API.
        This is the ONLY source for debt data. The dataset contains daily records starting from 1993-04-01.

        Fields returned: record_date, tot_pub_debt_out_amt (total public debt outstanding),
        debt_held_public_amt (debt held by the public), intragov_hold_amt (intragovernmental holdings).

        Filter syntax: field:operator:value (comma-separated for multiple).
        Operators: eq, gt, gte, lt, lte, in.
        Example filters:
        - "record_date:gte:2024-01-01,record_date:lte:2024-12-31" (date range)
        - "record_date:eq:2024-06-15" (exact date)
        - "record_calendar_year:eq:2008" (entire year)

        Sort: prefix with - for descending. Default: -record_date (newest first).
        """)]
    public async Task<string> GetUsDebtAsync(
        [Description("Filter expression. Example: 'record_date:gte:2024-01-01,record_date:lte:2024-12-31'. Use record_calendar_year for year queries.")]
        string? filter = null,
        [Description("Sort field with optional - prefix for descending. Default: '-record_date'.")]
        string? sort = null,
        [Description("Page number (1-based). Default: 1.")]
        int? pageNumber = null,
        [Description("Number of records per page (1-10000). Default: 100.")]
        int? pageSize = null)
    {
        LogToolInvoked(filter, sort, pageNumber, pageSize);

        try
        {
            var response = await treasuryApiClient.GetDebtDataAsync(
                filter: filter,
                sort: sort,
                pageNumber: pageNumber,
                pageSize: pageSize);

            var result = new
            {
                records = response.Data,
                pagination = new
                {
                    returned = response.Meta.Count,
                    totalRecords = response.Meta.TotalCount,
                    totalPages = response.Meta.TotalPages,
                    currentPage = pageNumber ?? 1,
                },
            };

            return JsonSerializer.Serialize(result, _serializerOptions);
        }
        catch (HttpRequestException ex)
        {
            LogFetchError(ex);
            return JsonSerializer.Serialize(new { error = "Failed to fetch data from Treasury API. The service may be temporarily unavailable." });
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "GetUsDebt tool invoked with filter={Filter}, sort={Sort}, page={Page}, size={Size}")]
    partial void LogToolInvoked(string? filter, string? sort, int? page, int? size);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch debt data from Treasury API")]
    partial void LogFetchError(Exception ex);
}
