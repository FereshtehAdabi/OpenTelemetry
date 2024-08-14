using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace opentelemetry2
{
    public static class Startup
    {
        public static readonly ActivitySource MyActivitySource = new("opentelemetry2.Startup.*");

        public static WebApplication InitializeApp(String[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            ConfigureServices(builder);
            var app = builder.Build();
            Configure(app);
            return app;
        }

        private static void ConfigureServices(WebApplicationBuilder builder)//, IWebHostEnvironment webHostEnvironment)
        {           
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            builder.Services.AddHttpClient("opentelemetry3").ConfigureHttpClient(c => c.BaseAddress = new Uri("http://localhost:5094"));

            // Note: Switch between Zipkin/OTLP/Console by setting UseTracingExporter in appsettings.json.
            var tracingExporter = builder.Configuration.GetValue("UseTracingExporter", defaultValue: "console")!.ToLowerInvariant();

            // Note: Switch between Prometheus/OTLP/Console by setting UseMetricsExporter in appsettings.json.
            var metricsExporter = builder.Configuration.GetValue("UseMetricsExporter", defaultValue: "console")!.ToLowerInvariant();

            // Note: Switch between Console/OTLP by setting UseLogExporter in appsettings.json.
            var logExporter = builder.Configuration.GetValue("UseLogExporter", defaultValue: "console")!.ToLowerInvariant();

            // Note: Switch between Explicit/Exponential by setting HistogramAggregation in appsettings.json
            var histogramAggregation = builder.Configuration.GetValue("HistogramAggregation", defaultValue: "explicit")!.ToLowerInvariant();

            // Create a service to expose ActivitySource, and Metric Instruments
            // for manual instrumentation
            builder.Services.AddSingleton<Instrumentation>();

            // Clear default logging providers used by WebApplication host.
            builder.Logging.ClearProviders();

            builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService(
                serviceName: "opentelemetry2",
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                serviceInstanceId: Environment.MachineName))
            .WithTracing(b =>
            {
                // Tracing
                // Ensure the TracerProvider subscribes to any custom ActivitySources.
                b.AddSource(Instrumentation.ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation();

                // Use IConfiguration binding for AspNetCore instrumentation options.to record exceptions
                builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));

                switch (tracingExporter)
                {
                    case "jaeger":
                        b.AddJaegerExporter();
                        b.ConfigureServices(services =>
                        {
                            services.Configure<JaegerExporterOptions>(builder.Configuration.GetSection("jaeger"));
                        });
                        break;

                    case "otlp":
                        b.AddOtlpExporter(otlpOptions =>
                        {
                            // Use IConfiguration directly for Otlp exporter endpoint option.
                            otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
                        });
                        break;

                    default:
                        b.AddConsoleExporter();
                        break;
                }
            });
            #region metric & logging
            //.WithMetrics(b =>
            //{
            //    // Metrics

            //    // Ensure the MeterProvider subscribes to any custom Meters.
            //    b
            //        .AddMeter(Instrumentation.MeterName)
            //        .SetExemplarFilter(ExemplarFilterType.TraceBased)
            //        .AddRuntimeInstrumentation()
            //        .AddHttpClientInstrumentation()
            //        .AddAspNetCoreInstrumentation();

            //    switch (histogramAggregation)
            //    {
            //        case "exponential":
            //            b.AddView(instrument =>
            //            {
            //                return instrument.GetType().GetGenericTypeDefinition() == typeof(Histogram<>)
            //                    ? new Base2ExponentialBucketHistogramConfiguration()
            //                    : null;
            //            });
            //            break;
            //        default:
            //            // Explicit bounds histogram is the default.
            //            // No additional configuration necessary.
            //            break;
            //    }

            //    switch (metricsExporter)
            //    {
            //        case "prometheus":
            //            b.AddPrometheusExporter();
            //            break;
            //        case "otlp":
            //            b.AddOtlpExporter(otlpOptions =>
            //            {
            //                // Use IConfiguration directly for Otlp exporter endpoint option.
            //                otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
            //            });
            //            break;
            //        default:
            //            b.AddConsoleExporter();
            //            break;
            //    }
            //})
            //.WithLogging(b =>
            //{
            //    // Note: See appsettings.json Logging:OpenTelemetry section for configuration.

            //    switch (logExporter)
            //    {
            //        case "otlp":
            //            b.AddOtlpExporter(otlpOptions =>
            //            {
            //                // Use IConfiguration directly for Otlp exporter endpoint option.
            //                otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
            //            });
            //            break;
            //        default:
            //            b.AddConsoleExporter();
            //            break;
            //    }
            //});

            //builder.Services.AddControllers();

            //builder.Services.AddEndpointsApiExplorer();

            //builder.Services.AddSwaggerGen();
            #endregion
        }

        private static void Configure(WebApplication app)
        {
            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/weatherforecast", async context =>
                {
                    // Adding tags for debugging & tracing
                    Activity.Current.AddTag("Trace_ID", Activity.Current.TraceId.ToString());
                    Activity.Current.AddTag("Span_ID", Activity.Current.SpanId.ToString());
                    Activity.Current.AddTag("Parent_Span_ID", Activity.Current.ParentSpanId.ToString());
                    context.Response.Headers.Add("Request-Id", Activity.Current.TraceId.ToString());

                    using var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("opentelemetry3");
                    var content = client.GetStringAsync("/weatherforecast");
                    await context.Response.WriteAsync("hello from project opentelemetry2, test\n");
                    await context.Response.WriteAsync(await content);
                });
            });
        }
    }
}
