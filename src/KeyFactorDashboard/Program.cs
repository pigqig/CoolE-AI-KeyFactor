using System.Reflection;
using System.Text;
using KeyFactorDashboard.Auth;
using KeyFactorDashboard.Data;
using KeyFactorDashboard.Middleware;
using KeyFactorDashboard.Options;
using KeyFactorDashboard.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PythonOptions>(builder.Configuration.GetSection(PythonOptions.SectionName));
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<KeyFactorDashboard.Options.CorsOptions>(builder.Configuration.GetSection(KeyFactorDashboard.Options.CorsOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TokenIssuer>();
builder.Services.AddScoped<AuditSink>();

var authOpts = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authOpts.Issuer,
            ValidAudience = authOpts.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOpts.SigningKey.PadRight(32)))
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PlantRoles.CanMutate, p => p.RequireRole(PlantRoles.Engineer, PlantRoles.Admin));
    options.AddPolicy(PlantRoles.CanApprove, p => p.RequireRole(PlantRoles.Qa, PlantRoles.Admin));
    options.AddPolicy(PlantRoles.AdminOnly, p => p.RequireRole(PlantRoles.Admin));
});

builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

var python = builder.Configuration.GetSection(PythonOptions.SectionName).Get<PythonOptions>() ?? new PythonOptions();
builder.Services.AddHttpClient<IPythonApiClient, PythonApiClient>(client =>
{
    client.BaseAddress = new Uri(python.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddScoped<AnalysisLedger>();
builder.Services.AddHostedService<PythonProcessHostedService>();

var dbOpts = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
var conn = builder.Configuration.GetConnectionString("Default") ?? "Data Source=App_Data/keyfactor.db";
if (!string.Equals(dbOpts.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    conn = SqlitePath.Resolve(conn, builder.Environment.ContentRootPath);
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.Equals(dbOpts.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(conn);
    }
    else
    {
        options.UseSqlite(conn);
    }
});

var cors = builder.Configuration.GetSection(KeyFactorDashboard.Options.CorsOptions.SectionName).Get<KeyFactorDashboard.Options.CorsOptions>()
           ?? new KeyFactorDashboard.Options.CorsOptions();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (cors.Origins is { Length: > 0 })
        {
            policy.WithOrigins(cors.Origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Key Factor Dashboard API",
        Version = "v1",
        Description = "Public ASP.NET Core surface for remanufacturing key-factor analysis. "
                      + "External systems POST CSV or JSON datasets, train a model, then read importances, "
                      + "dependence, what-if, and scored rows. The Vue UI uses the same /api/v1 routes. "
                      + "Sklearn work is proxied to a local Python service."
    });

    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml))
    {
        options.IncludeXmlComments(xml, includeControllerXmlComments: true);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "廠內登入後：Authorization: Bearer {token}",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "機台進件可改用 X-Api-Key（視為 Engineer）。與 JWT 擇一。",
        Type = SecuritySchemeType.ApiKey,
        Name = ApiKeyMiddleware.HeaderName,
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
var apiOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiOptions>>().Value;
var enableSwagger = apiOptions.EnableSwagger || app.Environment.IsDevelopment();

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthorization();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Key Factor Dashboard v1");
        options.DocumentTitle = "Key Factor Dashboard API";
    });
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (string.Equals(dbOpts.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }

    PlantSeed.Ensure(db);
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Expose the implicit Program type to WebApplicationFactory.</summary>
public partial class Program;
