using DebtChat.Console;
using DebtChat.Core.Extensions;
using DebtChat.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.AddServiceDefaults();
builder.Services.AddDebtChatCore();
builder.Services.AddLlmChatClient();
builder.Services.AddHostedService<ChatLoop>();

var host = builder.Build();
await host.RunAsync();
