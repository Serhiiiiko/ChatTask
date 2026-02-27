using DebtChat.Core.Constants;
using DebtChat.Core.Services;
using DebtChat.Core.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DebtChat.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDebtChatCore(this IServiceCollection services)
    {
        // Treasury API HTTP client
        services.AddHttpClient<TreasuryApiClient>(client =>
        {
            client.BaseAddress = new Uri(TreasuryApi.BaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Tools
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<GetCurrentDateTool>();
        services.AddSingleton<GetUsDebtTool>();

        // Chat service (scoped per session — one per console run)
        services.AddSingleton<ChatService>();

        return services;
    }
}
