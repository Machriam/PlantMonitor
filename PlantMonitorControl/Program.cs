using PlantMonitorControl.Features.AppsettingsConfiguration;
using PlantMonitorControl.Features.HealthChecking;
using PlantMonitorControl.Features.MotorMovement;
using Serilog;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);
var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "server.logs");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(logFile, Serilog.Events.LogEventLevel.Information, fileSizeLimitBytes: 1024 * 1024 * 8,
                  rollOnFileSizeLimit: true, retainedFileCountLimit: 4)
    .CreateLogger();

Log.Information("Starting PlantMonitor");
builder.Host.UseSerilog();
builder.Configuration
    .SetBasePath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? builder.Environment.ContentRootPath : "/srv/dist/")
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

var options = builder.Configuration.GetRequiredSection(ConfigurationOptions.Configuration).Get<ConfigurationOptions>();
builder.Services.AddSingleton<IEnvironmentConfiguration>(new EnvironmentConfiguration(options));
builder.Services.AddSingleton<IMotorPositionCalculator, MotorPositionCalculator>();
builder.Services.AddTransient<IHealthSettingsEditor, HealthSettingsEditor>();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddKeyedTransient<ICameraInterop, DevelopCameraInterop>(ICameraInterop.VisCamera);
}
else
{
    builder.Services.AddKeyedTransient<ICameraInterop, RaspberryCameraInterop>(ICameraInterop.VisCamera);
}
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
});
if (builder.Environment.IsProduction())
{
    builder.WebHost.UseKestrel(options =>
    {
        options.ListenAnyIP(443, listenOptions => listenOptions.UseHttps("/srv/certs/plantmonitor.pfx"));
    });
}

builder.Services.AddOpenApiDocument(options =>
{
    options.PostProcess = document =>
    {
        document.Info = new NSwag.OpenApiInfo()
        {
            Version = "v1",
            Title = "PlantMonitor Control API",
            Description = "Take Pictures from the plants and move the arm"
        };
    };
});

builder.Services.AddControllers();
builder.Services.AddSignalR().AddMessagePackProtocol();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthorization();

app.MapControllers();
app.MapHub<StreamingHub>("/hub/video");

app.Run();
