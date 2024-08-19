using Plantmonitor.Server.Features.AppConfiguration;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using PlantMonitorControl.Features.HealthChecking;
using PlantMonitorControl.Features.ImageTaking;
using PlantMonitorControl.Features.MeasureTemperature;
using PlantMonitorControl.Features.MotorMovement;
using PlantMonitorControl.Features.SwitchOutlets;
using Serilog;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(ConfigurationOptions.LogFileLocation, Serilog.Events.LogEventLevel.Information, fileSizeLimitBytes: 1024 * 1024, shared: true,
    flushToDiskInterval: TimeSpan.FromSeconds(10d), rollOnFileSizeLimit: true, retainedFileCountLimit: 20)
    .CreateLogger();

Log.Information("Starting PlantMonitor");
builder.Host.UseSerilog();
builder.Configuration
    .SetBasePath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? builder.Environment.ContentRootPath : "/srv/dist/")
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

var options = builder.Configuration.GetRequiredSection(ConfigurationOptions.Configuration).Get<ConfigurationOptions>();
builder.Services.AddSingleton<IEnvironmentConfiguration>(new EnvironmentConfiguration(options));
builder.Services.AddTransient<IHealthSettingsEditor, HealthSettingsEditor>();
builder.Services.AddTransient<IFileStreamingReader, FileStreamingReader>();
builder.Services.AddTransient<IExposureSettingsEditor, ExposureSettingsEditor>();
builder.Services.AddSingleton<IMotorPositionCalculator, MotorPositionCalculator>();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddKeyedTransient<ICameraInterop, DevelopVisCameraInterop>(ICameraInterop.VisCamera);
    builder.Services.AddKeyedTransient<ICameraInterop, DevelopIrCameraInterop>(ICameraInterop.IrCamera);
    builder.Services.AddTransient<IGpioInteropFactory, GpioInteropFactoryDevelop>();
    builder.Services.AddTransient<IClick2TempInterop, DevelopClick2TempInterop>();
    builder.Services.AddTransient<IOutletSwitcher, OutletSwitcherDevelop>();
}
else
{
    builder.Services.AddTransient<IOutletSwitcher, OutletSwitcher433MHz>();
    builder.Services.AddKeyedTransient<ICameraInterop, RaspberryCameraInterop>(ICameraInterop.VisCamera);
    builder.Services.AddKeyedTransient<ICameraInterop, FlirLeptonCameraInterop>(ICameraInterop.IrCamera);
    builder.Services.AddTransient<IGpioInteropFactory, GpioInteropFactory>();
    builder.Services.AddTransient<IClick2TempInterop, Click2TempInterop>();
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
builder.Services.AddMvc(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
});

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
app.MapHub<TemperatureHub>("/hub/temperatures");

app.Run();
