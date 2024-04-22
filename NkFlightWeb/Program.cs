using CommonCore.Enum;
using CommonCore;
using Serilog;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using IGeekFan.AspNetCore.Knife4jUI;
using CommonCore.Dependency;
using NkFlightWeb.Db;
using NkFlightWeb.Workers;

var configuration = new ConfigurationBuilder()
.SetBasePath(Directory.GetCurrentDirectory())
.AddJsonFile("Serilog.json")
.AddJsonFile($"Serilog.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
.Build();
var logger = new LoggerConfiguration()
   .ReadFrom.Configuration(configuration)
   .CreateLogger();
Log.Logger = logger;
var builder = WebApplication.CreateBuilder(args);
Log.Information("Starting LionAirl WebApi");
// Add services to the container.
builder.Services.AddControllers()
    .AddApiResult();
builder.Services.AddHostedService<GetTokenWorker>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});
builder.Services.AddAutoIoc(typeof(IScopedDependency), LifeCycle.Scoped)
       .AddAutoIoc(typeof(ISingletonDependency), LifeCycle.Singleton)
       .AddAutoIoc(typeof(ITransientDependency), LifeCycle.Transient)
       .AddMapper();

builder.Services.AddHyTripEntityFramework<HeyTripDbContext>(options =>
{
    options.UseMySql(builder.Configuration.GetConnectionString("heytripDb"), ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("heytripDb")));
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.UseKnife4UI();
}
else
{
    app.UseSwagger();
    //app.UseSwaggerUI();
    app.UseKnife4UI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();