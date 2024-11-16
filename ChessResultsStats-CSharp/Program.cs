using ChessResultsStats_CSharp;
using ChessResultsStats_CSharp.Data;
using ChessResultsStats_CSharp.Service;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);


// Service de base de donn�es SQL Server
builder.Services.AddDbContext<ChessGamesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ChessGamesDbConnection")));

// Health Check 
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("Database");

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)  // Charger la config depuis appsettings.json
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Services CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        builder =>
        {
            builder.WithOrigins("http://localhost:4200", "http://localhost:7222", "https://chessresultsstats.com")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});

builder.Services.AddSingleton(Log.Logger);
builder.Services.AddScoped<GamesService>();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors("AllowAngularApp");

// Active le point d'acc�s Health Check
app.MapHealthChecks("/health");

// Configure le pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();