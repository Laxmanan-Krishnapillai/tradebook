using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Options;
using Tradebook.ServiceDefaults;

namespace Tradebook.UnitTests;

public sealed class NpgsqlObservabilityTests
{
    [Fact]
    public async Task MetricsAreExportedWithAStableNonSecretPoolName()
    {
        const string password = "not-for-telemetry";
        var exporter = new RecordingMetricExporter();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
        builder.ConfigureOpenTelemetry();
        builder
            .Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddReader(new BaseExportingMetricReader(exporter)));

        using var services = builder.Services.BuildServiceProvider();
        using var meterProvider = services.GetRequiredService<MeterProvider>();
        await using var connections = new NpgsqlConnectionFactory(
            Options.Create(
                new DatabaseOptions
                {
                    ConnectionString =
                        $"Host=localhost;Database=tradebook;Username=test;Password={password}",
                }
            )
        );

        Assert.True(meterProvider.ForceFlush());
        Assert.Contains("Npgsql", exporter.MeterNames);
        Assert.Contains(NpgsqlConnectionFactory.DataSourceName, exporter.PoolNames);
        Assert.DoesNotContain(
            exporter.PoolNames,
            name => name.Contains(password, StringComparison.Ordinal)
        );
    }

    private sealed class RecordingMetricExporter : BaseExporter<Metric>
    {
        public HashSet<string> MeterNames { get; } = new(StringComparer.Ordinal);

        public HashSet<string> PoolNames { get; } = new(StringComparer.Ordinal);

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                MeterNames.Add(metric.MeterName);
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    foreach (var tag in point.Tags)
                    {
                        if (
                            string.Equals(
                                tag.Key,
                                "db.client.connection.pool.name",
                                StringComparison.Ordinal
                            ) && tag.Value is string poolName
                        )
                        {
                            PoolNames.Add(poolName);
                        }
                    }
                }
            }

            return ExportResult.Success;
        }
    }
}
