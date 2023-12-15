// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests;

public class OpenTelemetryBuilderTests
{
    internal sealed class SimpleInstrument : Instrument
    {
        public SimpleInstrument(Meter meter, string name, string unit, string description) : base(meter, name, unit, description)
        {
        }
    }

    class CustomExporter : BaseExporter<Metric>
    {
        public CustomExporter() : base()
        {
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }

    [Fact]
    public void TestCustomMetrics()
    {
        HostBuilder builder = new HostBuilder();
        builder.ConfigureServices((s) =>
        {
            

            var otelBuilder = s.AddOpenTelemetry();
            otelBuilder.WithLogging(configure =>
            {
            });

            otelBuilder.WithMetrics(configure =>
            {
                //configure.AddMeter("System.Runtime");
                configure.AddReader(new BaseExportingMetricReader(new CustomExporter()));
            });
        });



        var host = builder.Build();
        var listener = host.Services.GetRequiredService<IMetricsListener>();
        var instrument = new SimpleInstrument(new Meter("System.Runtime"), "Test", "MiB", "Test");


        var f = host.Services.GetRequiredService<ILoggerFactory>();
        var logger = f.CreateLogger("test");
        logger.LogInformation("test");
        //bool result = listener.InstrumentPublished(instrument, out _);
        //listener.GetMeasurementHandlers().IntHandler(instrument, 5, null, null);

    }

    [Fact]
    public void ConfigureResourceTest()
    {
        var services = new ServiceCollection();

        services
            .AddOpenTelemetry()
            .ConfigureResource(r => r.Clear().AddResource(
                new Resource(new Dictionary<string, object> { ["key1"] = "value1" })))
            .WithLogging(logging => logging.ConfigureResource(r => r.AddResource(
                new Resource(new Dictionary<string, object> { ["l_key1"] = "l_value1" }))))
            .WithMetrics(metrics => metrics.ConfigureResource(r => r.AddResource(
                new Resource(new Dictionary<string, object> { ["m_key1"] = "m_value1" }))))
            .WithTracing(tracing => tracing.ConfigureResource(r => r.AddResource(
                new Resource(new Dictionary<string, object> { ["t_key1"] = "t_value1" }))));

        using var sp = services.BuildServiceProvider();

        var tracerProvider = sp.GetRequiredService<TracerProvider>() as TracerProviderSdk;
        var meterProvider = sp.GetRequiredService<MeterProvider>() as MeterProviderSdk;
        var loggerProvider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.NotNull(meterProvider);
        Assert.NotNull(loggerProvider);

        Assert.Equal(2, tracerProvider.Resource.Attributes.Count());
        Assert.Contains(
            tracerProvider.Resource.Attributes,
            kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Contains(
            tracerProvider.Resource.Attributes,
            kvp => kvp.Key == "t_key1" && (string)kvp.Value == "t_value1");

        Assert.Equal(2, meterProvider.Resource.Attributes.Count());
        Assert.Contains(
            meterProvider.Resource.Attributes,
            kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Contains(
            meterProvider.Resource.Attributes,
            kvp => kvp.Key == "m_key1" && (string)kvp.Value == "m_value1");

        Assert.Equal(2, loggerProvider.Resource.Attributes.Count());
        Assert.Contains(
            loggerProvider.Resource.Attributes,
            kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Contains(
            loggerProvider.Resource.Attributes,
            kvp => kvp.Key == "l_key1" && (string)kvp.Value == "l_value1");
    }

    [Fact]
    public void ConfigureResourceServiceProviderTest()
    {
        var services = new ServiceCollection();

        services.AddSingleton<TestResourceDetector>();

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddDetector(sp => sp.GetRequiredService<TestResourceDetector>()))
            .WithLogging()
            .WithMetrics()
            .WithTracing();

        using var sp = services.BuildServiceProvider();

        var tracerProvider = sp.GetRequiredService<TracerProvider>() as TracerProviderSdk;
        var meterProvider = sp.GetRequiredService<MeterProvider>() as MeterProviderSdk;
        var loggerProvider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.NotNull(meterProvider);
        Assert.NotNull(loggerProvider);

        Assert.Single(tracerProvider.Resource.Attributes, kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Single(meterProvider.Resource.Attributes, kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Single(loggerProvider.Resource.Attributes, kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
    }

    private sealed class TestResourceDetector : IResourceDetector
    {
        public Resource Detect() => ResourceBuilder.CreateEmpty().AddAttributes(
            new Dictionary<string, object>
            {
                ["key1"] = "value1",
            }).Build();
    }
}
