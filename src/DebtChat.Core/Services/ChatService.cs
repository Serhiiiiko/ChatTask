using DebtChat.Core.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DebtChat.Core.Services;

/// <summary>
/// Orchestrates the chat loop with in-memory conversation history.
/// Each instance holds a single conversation session; history is lost when the process exits.
/// </summary>
public sealed partial class ChatService(
    IChatClient chatClient,
    GetCurrentDateTool currentDateTool,
    GetUsDebtTool usDebtTool,
    ILogger<ChatService> logger)
{
    private const string SystemPrompt = """
        You are a U.S. Public Debt expert assistant. You answer questions about U.S. public debt
        using ONLY the Treasury "Debt to the Penny" dataset.

        You have exactly two tools:
        1. get_current_date — Returns today's date. Use this when you need to know the current date.
        2. get_us_debt — Fetches data from the Treasury "Debt to the Penny" API. This is the ONLY way to get debt data.

        Rules:
        - Base ALL answers on data from the get_us_debt tool. Never fabricate or estimate numbers.
        - If asked about something outside U.S. public debt, respond: "I don't have expertise in that area."
        - If data is not available for a requested period (before April 1993), say: "I don't have data for that period."
        - If the API returns no data for a valid date range, say so explicitly.
        - Be concise, factual, and precise. No filler or speculation.
        - You may perform calculations (growth, differences, trends, percentages) based on fetched data.
        - Format large dollar amounts with commas for readability (e.g., $36,177,073,901,949.70).
        - Always include the date(s) associated with the data you present.
        - When comparing periods, fetch data for both periods before answering.
        - For year-based queries, use the last available record of that year for the debt figure.
        """;

    private readonly List<ChatMessage> _history = [new(ChatRole.System, SystemPrompt)];

    private readonly ChatOptions _chatOptions = new()
    {
        Tools =
        [
            AIFunctionFactory.Create(currentDateTool.GetCurrentDate, name: "get_current_date"),
            AIFunctionFactory.Create(usDebtTool.GetUsDebtAsync, name: "get_us_debt"),
        ],
    };

    public async Task<string> SendMessageAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        LogUserMessageReceived(userMessage.Length);

        _history.Add(new ChatMessage(ChatRole.User, userMessage));

        var response = await chatClient.GetResponseAsync(_history, _chatOptions, cancellationToken);

        LogAssistantResponseReceived(response.Text?.Length ?? 0);

        _history.AddMessages(response);

        return response.Text ?? "I was unable to generate a response. Please try again.";
    }

    public async IAsyncEnumerable<string> SendMessageStreamingAsync(
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LogStreamingMessageReceived(userMessage.Length);

        _history.Add(new ChatMessage(ChatRole.User, userMessage));

        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in chatClient.GetStreamingResponseAsync(
            _history, _chatOptions, cancellationToken))
        {
            updates.Add(update);

            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }

        _history.AddMessages(updates);

        LogStreamingResponseCompleted();
    }

    public void Reset()
    {
        _history.Clear();
        _history.Add(new ChatMessage(ChatRole.System, SystemPrompt));
        LogChatHistoryReset();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User message received ({Length} chars)")]
    partial void LogUserMessageReceived(int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assistant response received ({Length} chars)")]
    partial void LogAssistantResponseReceived(int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "User message received for streaming ({Length} chars)")]
    partial void LogStreamingMessageReceived(int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "Streaming response completed")]
    partial void LogStreamingResponseCompleted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Chat history reset")]
    partial void LogChatHistoryReset();
}
