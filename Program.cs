using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway supplies PORT at runtime
var railwayPort = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(railwayPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");
}

// =========================
// CONTROLLERS
// =========================

builder.Services.AddControllers();

// =========================
// DATABASE
// =========================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is missing."
    );
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    )
);

// =========================
// SERVICES
// =========================

builder.Services.AddScoped<JwtService>();
builder.Services.AddHttpClient<EmailService>();

// =========================
// JWT
// =========================

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT Key is missing."
    );
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)
                )
        };
});

// =========================
// CORS
// =========================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "https://sync-up-jade-one.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// =========================
// FORWARDED HEADERS
// =========================

app.UseForwardedHeaders(
    new ForwardedHeadersOptions
    {
        ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto
    }
);

// =========================
// HTTPS
// =========================

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// =========================
// MIDDLEWARE
// =========================

app.UseCors("AllowAngularApp");

app.UseAuthentication();
app.UseAuthorization();

// =========================
// ENDPOINTS
// =========================

app.MapControllers();

app.MapGet(
    "/health",
    () => Results.Ok(
        new
        {
            status = "ok"
        }
    )
);

app.Run();