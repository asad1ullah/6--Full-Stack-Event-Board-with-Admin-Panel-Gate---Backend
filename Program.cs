using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using EventBoard.Api.Repositories;
using EventBoard.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {token}"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient("OpenWeather", client =>
{
    var baseUrl = builder.Configuration["OpenWeather:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5/";
    client.BaseAddress = new Uri(baseUrl);
});

// Allow React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowReact");

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, app.Configuration["Uploads:Path"] ?? "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/weatherforecast", async (
    string city,
    string? country,
    IHttpClientFactory httpClientFactory,
    IConfiguration config) =>
{
    var apiKey = config["OpenWeather:ApiKey"];

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem(
            "OpenWeather API key is not configured.",
            statusCode: 500);
    }

    var client = httpClientFactory.CreateClient("OpenWeather");

    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    var locationQuery = string.IsNullOrWhiteSpace(country)
        ? city
        : $"{city},{country}";

    var query =
        $"forecast?q={Uri.EscapeDataString(locationQuery)}&appid={apiKey}&units=metric";

    OpenWeatherForecastResponse? response;

    try
    {
        response = await client.GetFromJsonAsync<OpenWeatherForecastResponse>(
            query,
            jsonOptions);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(
            $"Could not fetch weather for {locationQuery}: {ex.Message}",
            statusCode: 502);
    }

    if (response?.List is null || response.List.Count == 0)
    {
        return Results.NotFound($"No forecast found for {locationQuery}.");
    }

    var current = response.List[0];
    var currentWeather = new
    {
        Temperature = (int)Math.Round(current.Main.Temp),
        Description = current.Weather.FirstOrDefault()?.Description
    };

    var forecast = response.List
        .Where(item => item.DtTxt.EndsWith("12:00:00"))
        .Take(5)
        .Select(item => new WeatherForecast(
            DateOnly.Parse(item.DtTxt.Split(' ')[0]),
            (int)Math.Round(item.Main.Temp),
            item.Weather.FirstOrDefault()?.Description))
        .ToArray();

    return Results.Ok(new { Current = currentWeather, Forecast = forecast });
})
.WithName("GetWeatherForecast")
.WithOpenApi();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await DataSeeder.SeedAsync(context, app.Configuration);
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program { }
