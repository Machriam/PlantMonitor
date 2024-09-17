using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.Dashboard;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Server.Features.ImageStitching;
using Plantmonitor.Server.Features.RestApiFilter;
using Plantmonitor.Server.Features.TemperatureMonitor;
using Plantmonitor.Shared.Features.ImageStreaming;
using Plantmonitor.Shared.Features.MeasureTemperature;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .CreateLogger();

Log.Information("Starting Gatewayserver");
builder.Host.UseSerilog();

var configurationStorage = new ConfigurationStorage(builder.Configuration);
var environmentConfiguration = new EnvironmentConfiguration(builder.Configuration, new ConfigurationStorage(builder.Configuration));
builder.Services.AddSingleton<IEnvironmentConfiguration>(environmentConfiguration);
builder.Services.AddSingleton<IConfigurationStorage>(configurationStorage);
builder.Services.AddSingleton<IDeviceConnectionEventBus, DeviceConnectionEventBus>();

builder.Services.AddTransient<IDeviceConnectionTester, DeviceConnectionTester>();
builder.Services.AddTransient<IDatabaseUpgrader, DatabaseUpgrader>();
builder.Services.AddTransient<IDeviceApiFactory, DeviceApiFactory>();
builder.Services.AddTransient<ITemperatureMeasurementWorker, TemperatureMeasurementWorker>();
builder.Services.AddTransient<IPictureDiskStreamer, PictureDiskStreamer>();
builder.Services.AddTransient<IDeviceRestarter, DeviceRestarter>();
builder.Services.AddTransient<IVirtualImageWorker, VirtualImageWorker>();
builder.Services.AddTransient<IPhotoTourSummaryWorker, PhotoTourSummaryWorker>();
builder.Services.AddTransient<IPhotoStitcher, PhotoStitcher>();
builder.Services.AddTransient<IImageCropper, ImageCropper>();

builder.Services.AddHostedService<DeviceConnectionWorker>();
builder.Services.AddHostedService(s => (TemperatureMeasurementWorker)s.GetRequiredService<ITemperatureMeasurementWorker>());
builder.Services.AddHostedService<AutomaticPhotoTourWorker>();
builder.Services.AddHostedService<DeviceTemperatureWatcherWorker>();
builder.Services.AddHostedService<VirtualImageWorker>();
builder.Services.AddHostedService<PhotoTourSummaryWorker>();

var dataSourceBuilder = new NpgsqlDataSourceBuilder(environmentConfiguration.DatabaseConnection());
var dataSource = dataSourceBuilder.Configure().Build();
builder.Services.AddDbContext<IDataContext, DataContext>(
    options => options.UseNpgsql(dataSource, npg => npg.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.Converters.Add(new DateTimeConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR().AddMessagePackProtocol();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod()));
}
builder.Services.AddOpenApiDocument(options =>
{
    options.DocumentProcessors.Add(new AddAdditionalTypeProcessor<StreamingMetaData>());
    options.DocumentProcessors.Add(new AddAdditionalTypeProcessor<TemperatureStreamData>());

    options.PostProcess = document =>
    {
        document.Info = new NSwag.OpenApiInfo()
        {
            Version = "v1",
            Title = "Gateway Server API",
            Description = "Server to manage sensors and the corresponding Raspberry Pis"
        };
    };
});
builder.Services.AddMvc(options =>
{
    options.Filters.Add<ModelAttributeErrorFilter>();
    options.Filters.Add<ApiExceptionFilter>();
});

var app = builder.Build();
app.Services.GetRequiredService<IConfigurationStorage>().InitializeConfiguration();
CreateOrUpdateDatabase();
var webHost = app.Services.GetRequiredService<IWebHostEnvironment>();
if (Path.Exists(webHost.DownloadFolderPath())) Directory.Delete(webHost.DownloadFolderPath(), true);
Directory.CreateDirectory(webHost.DownloadFolderPath());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

app.Use(async (context, next) =>
{
    await next();
    var path = context.Request.Path.Value;

    if (context.Response.StatusCode != StatusCodes.Status304NotModified &&
        path?.StartsWith("/api") == false && path?.StartsWith("/hub") == false && path?.StartsWith(IWebHostEnvironmentExtensions.DownloadFolder) == false)
    {
        context.Request.Path = "/index.html";
        await next();
    }
});

app.UseStaticFiles();
app.UseDefaultFiles();

app.UseAuthorization();

app.MapControllers();
app.MapHub<PictureStreamingHub>("/hub/video");
app.MapHub<TemperatureStreamingHub>("/hub/temperatures");

app.Services.GetRequiredService<IConfigurationStorage>().InitializeConfiguration();
app.Run();

void CreateOrUpdateDatabase()
{
    using var scope = app.Services.CreateScope();
    var schemas = new List<string>();
    var databaseUpgrader = scope.ServiceProvider.GetRequiredService<IDatabaseUpgrader>();
    using var connection = dataSource.OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT schema_name FROM information_schema.schemata;";
    using var reader = command.ExecuteReader(System.Data.CommandBehavior.Default);
    while (reader.Read()) schemas.Add(reader.GetString(0));
    connection.Close();
    if (!schemas.Contains("plantmonitor"))
    {
        var lastPatch = 0;
        foreach (var patch in databaseUpgrader.GetPatchesToApply())
        {
            connection.Open();
            using var patchCommand = connection.CreateCommand();
            patchCommand.CommandText = patch.Sql;
            patchCommand.ExecuteNonQuery();
            connection.Close();
            lastPatch = patch.Number;
        }
        using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        dataContext.ConfigurationData.First(cd => cd.Key == Enum.GetName(ConfigurationDatumKeys.PatchNumber)).Value = lastPatch.ToString();
        dataContext.SaveChanges();
    }
    else
    {
        using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var patchNumberText = dataContext.ConfigurationData.First(cd => cd.Key == Enum.GetName(ConfigurationDatumKeys.PatchNumber)).Value;
        var patchNumber = int.Parse(patchNumberText);
        foreach (var patch in databaseUpgrader.GetPatchesToApply(patchNumber)) dataContext.Database.ExecuteSqlRaw(patch.Sql);
    }
}
