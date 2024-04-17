using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Server.Features.RestApiFilter;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Starting Gatewayserver");
builder.Host.UseSerilog();

builder.Services.AddTransient<IEnvironmentConfiguration, EnvironmentConfiguration>();
builder.Services.AddTransient<IDeviceConnectionTester, DeviceConnectionTester>();
builder.Services.AddTransient<IConfigurationStorage, ConfigurationStorage>();
builder.Services.AddTransient<IDeviceApiFactory, DeviceApiFactory>();
builder.Services.AddSingleton<IDeviceConnectionEventBus, DeviceConnectionEventBus>();
builder.Services.AddHostedService<DeviceConnectionWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR().AddMessagePackProtocol();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
    });
}
builder.Services.AddOpenApiDocument(options =>
{
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
});

var app = builder.Build();
app.Services.GetRequiredService<IConfigurationStorage>().InitializeConfiguration();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();
app.MapHub<PictureStreamingHub>("/hub/video");

app.Run();