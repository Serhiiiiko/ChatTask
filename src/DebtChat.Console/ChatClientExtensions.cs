using Anthropic;
using DebtChat.Core.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DebtChat.Console;

public static class ChatClientExtensions
{
    public static IServiceCollection AddLlmChatClient(this IServiceCollection services)
    {
        services.AddOptions<LlmOptions>()
            .BindConfiguration(LlmOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddChatClient(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;

            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                ?? throw new InvalidOperationException(
                    "ANTHROPIC_API_KEY environment variable is not set. " +
                    "Set it with: [System.Environment]::SetEnvironmentVariable(\"ANTHROPIC_API_KEY\", \"your-key\", \"User\") " +
                    "then restart your terminal.");

            var client = new AnthropicClient { ApiKey = apiKey };
            return client.AsIChatClient(options.Model);
        })
        .UseFunctionInvocation()
        .UseOpenTelemetry(configure: t => t.EnableSensitiveData = true)
        .UseLogging();

        return services;
    }
}
