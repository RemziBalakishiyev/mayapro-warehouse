using MayaPro.WarehouseApi.Api.Extensions;
using MayaPro.WarehouseApi.Api.Middleware;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog: console + rolling file ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/warehouse-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

// --- CORS: frontend origin from configuration ---
const string FrontendCors = "Frontend";
string frontendOrigin = builder.Configuration["Cors:FrontendOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(options => options.AddPolicy(FrontendCors, policy => policy
    .WithOrigins(frontendOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// --- JWT bearer authentication, role policies and ICurrentUser ---
builder.Services.AddJwtAuthentication(builder.Configuration);

// --- Global exception handling ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- Swagger (JWT security scheme ready; UI only exposed in Development) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MayaPro Warehouse API",
        Version = "v1"
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer token. Nümunə: \"Bearer {token}\"",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [jwtScheme] = Array.Empty<string>() });
});

// --- Cross-module transaction infrastructure: one shared connection + unit of work per scope ---
builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// The IActivityLogger implementation is supplied by the Activity module (DbActivityLogger).

// --- Modules: discover and register every IModule ---
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();

// --- Middleware pipeline ---
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(FrontendCors);
app.UseAuthentication();
app.UseAuthorization();

// --- Health check ---
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .WithTags("Health");

// --- Module endpoints ---
app.MapModuleEndpoints();

// --- Apply each module's migrations on startup ---
await app.Services.MigrateModulesAsync();

app.Run();

/// <summary>Exposed so the integration test project can drive the host via WebApplicationFactory.</summary>
public partial class Program;
