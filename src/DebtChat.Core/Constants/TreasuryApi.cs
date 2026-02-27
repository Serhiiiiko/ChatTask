namespace DebtChat.Core.Constants;

/// <summary>
/// Constants for the U.S. Treasury Fiscal Data API — Debt to the Penny endpoint.
/// </summary>
public static class TreasuryApi
{
    public const string BaseUrl = "https://api.fiscaldata.treasury.gov";

    public const string DebtToPennyEndpoint = "/services/api/fiscal_service/v2/accounting/od/debt_to_penny";

    public static class Fields
    {
        public const string RecordDate = "record_date";
        public const string TotalPublicDebtOutstanding = "tot_pub_debt_out_amt";
        public const string DebtHeldByPublic = "debt_held_public_amt";
        public const string IntragovernmentalHoldings = "intragov_hold_amt";

        public const string DefaultSelection =
            $"{RecordDate},{TotalPublicDebtOutstanding},{DebtHeldByPublic},{IntragovernmentalHoldings}";
    }

    public static class Defaults
    {
        public const int PageSize = 100;
        public const int DefaultPageNumber = 1;
    }
}
