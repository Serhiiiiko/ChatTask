using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DebtChat.ServiceDefaults;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.ConfigureLogging();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    private static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("DebtChat.Core");
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource("DebtChat.Core")
                    .AddSource("Microsoft.Extensions.AI");
            });

        AddOpenTelemetryExporters(builder);

        return builder;
    }

    private static void AddOpenTelemetryExporters(IHostApplicationBuilder builder)
    {
        var useOtlp = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlp)
        {
            builder.Services.AddOpenTelemetry()
                .UseOtlpExporter();
        }
    }

    private static IHostApplicationBuilder ConfigureLogging(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddConsole();

        if (builder.Environment.IsDevelopment())
        {
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
        }
        else
        {
            builder.Logging.SetMinimumLevel(LogLevel.Information);
        }

        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.Logging.AddFilter("Polly", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Extensions.Http.Resilience", LogLevel.Warning);

        return builder;
    }
}
