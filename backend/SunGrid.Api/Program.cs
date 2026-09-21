// File name: Program.cs
// Project name: SunGrid
// Purpose of the file: Application entry point configuring dependency injection, middleware pipeline, JWT auth, Swagger, and database seeding.
// Author placeholder: SunGrid Development Team

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SunGrid.Api.Data;
using SunGrid.Api.Middleware;
using SunGrid.Api.Services;
using SunGrid.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

// Configure Settings Sections
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<SeedAdminSettings>(builder.Configuration.GetSection("SeedAdminSettings"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("CorsSettings"));

// Register Singleton Database Context
builder.Services.AddSingleton<MongoDbContext>();

// Register Application Services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IStationService, StationService>();
builder.Services.AddScoped<IBookingSlotService, BookingSlotService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IQrTransactionService, QrTransactionService>();
builder.Services.AddScoped<DataSeeder>();

// Configure JWT Authentication
var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettingsSection["SecretKey"] ?? "SunGrid_Default_Development_Secret_Key_2026_Minimum_32_Bytes!";
var issuer = jwtSettingsSection["Issuer"] ?? "SunGridApi";
var audience = jwtSettingsSection["Audience"] ?? "SunGridClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure Controllers
builder.Services.AddControllers();

// Configure CORS Policy
var corsOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("SunGridCorsPolicy", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure Swagger / OpenAPI with Bearer Authorization
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SunGrid API - Smart Solar Microgrid Trading System",
        Version = "v1",
        Description = "RESTful Web API for SunGrid microgrid user management and authentication."
    });

    // Add JWT Bearer Security Definition to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT Bearer token format: Bearer {your_token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed initial Backoffice admin on application startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedInitialAdminAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the initial Backoffice account.");
    }
}

// Global Exception Handler Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// Enable Swagger UI in Development and Production
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SunGrid API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("SunGridCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
