using Microsoft.EntityFrameworkCore;
using Npgsql;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.ImageWorker;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .CreateLogger();

Log.Information("Starting Gatewayserver");
builder.Host.UseSerilog();

var configuration = new ImageWorkerConfiguration(builder.Configuration);
builder.Services.AddTransient<IPhotoStitcher, PhotoStitcher>();
builder.Services.AddTransient<IImageCropper, ImageCropper>();
builder.Services.AddSingleton<IImageWorkerConfiguration>(configuration);
builder.Services.AddHostedService<VirtualImageWorker>();
builder.Services.AddHostedService<PhotoTourSummaryWorker>();
var dataSourceBuilder = new NpgsqlDataSourceBuilder(configuration.DatabaseConnection());
var dataSource = dataSourceBuilder.Configure().Build();
builder.Services.AddDbContext<IDataContext, DataContext>(
    options => options.UseNpgsql(dataSource, npg => npg.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

var host = builder.Build();
host.Run();
