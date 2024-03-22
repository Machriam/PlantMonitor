using PlantMonitorControl.Features.AppsettingsConfiguration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Starting PlantMonitor");
builder.Host.UseSerilog();

var options = builder.Configuration.GetRequiredSection(ConfigurationOptions.Configuration).Get<ConfigurationOptions>();
builder.Services.AddSingleton<IEnvironmentConfiguration>(new EnvironmentConfiguration(options));
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

app.Run();