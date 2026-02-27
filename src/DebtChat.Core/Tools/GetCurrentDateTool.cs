using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace DebtChat.Core.Tools;

/// <summary>
/// MCP tool that returns the current date. Used by the LLM to determine "today"
/// when answering questions about the current U.S. debt.
/// </summary>
public sealed partial class GetCurrentDateTool(TimeProvider timeProvider, ILogger<GetCurrentDateTool> logger)
{
    [Description("Returns today's date in ISO 8601 format (yyyy-MM-dd). Use this when you need to know the current date.")]
    public string GetCurrentDate()
    {
        var today = timeProvider.GetUtcNow().Date;
        var formatted = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        LogCurrentDateInvoked(formatted);

        return formatted;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "GetCurrentDate tool invoked, returning: {Date}")]
    partial void LogCurrentDateInvoked(string date);
}
