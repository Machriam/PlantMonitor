using Plantmonitor.ImageWorker;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .CreateLogger();

Log.Information("Starting Gatewayserver");
builder.Host.UseSerilog();

builder.Services.AddHostedService<VirtualImageWorker>();
builder.Services.AddHostedService<PhotoTourSummaryWorker>();

var host = builder.Build();
host.Run();
