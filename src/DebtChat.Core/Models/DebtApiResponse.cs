using System.Text.Json.Serialization;

namespace DebtChat.Core.Models;

public sealed record DebtApiResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<DebtRecord> Data,
    [property: JsonPropertyName("meta")] DebtApiMeta Meta);

public sealed record DebtApiMeta(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("total-count")] int TotalCount,
    [property: JsonPropertyName("total-pages")] int TotalPages);

public sealed record DebtRecord(
    [property: JsonPropertyName("record_date")] string RecordDate,
    [property: JsonPropertyName("tot_pub_debt_out_amt")] string TotalPublicDebtOutstanding,
    [property: JsonPropertyName("debt_held_public_amt")] string DebtHeldByPublic,
    [property: JsonPropertyName("intragov_hold_amt")] string IntragovernmentalHoldings);
