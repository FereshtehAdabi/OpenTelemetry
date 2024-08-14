
using opentelemetry3;
using System.Diagnostics;

var app = Startup.InitializeApp(args);
// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.Run();
