using DebtChat.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DebtChat.Console;

/// <summary>
/// Background service that runs the interactive chat loop in the console.
/// Maintains conversation memory in RAM for the duration of the session.
/// </summary>
public sealed partial class ChatLoop(
    ChatService chatService,
    IHostApplicationLifetime lifetime,
    ILogger<ChatLoop> logger) : BackgroundService
{
    private const string WelcomeMessage = """

        ╔══════════════════════════════════════════════════════════════╗
        ║              U.S. Public Debt Chat Assistant                ║
        ║                                                            ║
        ║  Ask questions about U.S. public debt using the Treasury   ║
        ║  "Debt to the Penny" dataset (data from April 1993).       ║
        ║                                                            ║
        ║  Examples:                                                 ║
        ║    • What is the current U.S. debt?                        ║
        ║    • What was the debt in 2008?                            ║
        ║    • How much did the debt increase in 2024?               ║
        ║                                                            ║
        ║  Type 'exit' or 'quit' to end the session.                 ║
        ║  Type 'reset' to clear conversation history.               ║
        ╚══════════════════════════════════════════════════════════════╝

        """;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the host a moment to finish startup logging
        await Task.Delay(500, stoppingToken);

        System.Console.WriteLine(WelcomeMessage);

        while (!stoppingToken.IsCancellationRequested)
        {
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.Write("You: ");
            System.Console.ResetColor();

            var input = System.Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                System.Console.WriteLine("\nGoodbye!");
                lifetime.StopApplication();
                return;
            }

            if (input.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                chatService.Reset();
                System.Console.WriteLine("\nConversation history cleared.\n");
                continue;
            }

            try
            {
                System.Console.ForegroundColor = ConsoleColor.Green;
                System.Console.Write("\nAssistant: ");
                System.Console.ResetColor();

                await foreach (var chunk in chatService.SendMessageStreamingAsync(input, stoppingToken))
                {
                    System.Console.Write(chunk);
                }

                System.Console.WriteLine("\n");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogProcessingError(ex);
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine($"\nError: {ex.Message}\n");
                System.Console.ResetColor();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing message")]
    partial void LogProcessingError(Exception ex);
}
