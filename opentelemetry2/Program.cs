using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using opentelemetry2;
using System.Diagnostics;

var app = Startup.InitializeApp(args);
// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.Run();
