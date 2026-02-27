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

            var client = new AnthropicClient();
            return client.AsIChatClient(options.Model);
        })
        .UseFunctionInvocation()
        .UseOpenTelemetry(configure: t => t.EnableSensitiveData = true)
        .UseLogging();

        return services;
    }
}
