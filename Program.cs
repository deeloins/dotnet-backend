using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AuthTodoApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException("Database connection string is not configured");

if (connectionString.StartsWith("postgres://"))
{
    // Convert Railway postgres URL to connection string
    var uri = new Uri(connectionString);
    var host = uri.Host;
    var dbPort = uri.Port;
    var username = uri.UserInfo.Split(':')[0];
    var password = uri.UserInfo.Split(':')[1];
    var database = uri.LocalPath.TrimStart('/');

    connectionString = $"Host={host};Port={dbPort};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}

// In Program.cs, replace the database config with:
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, o => o.EnableRetryOnFailure(
    maxRetryCount: 5,
    maxRetryDelay: TimeSpan.FromSeconds(30),
    errorCodesToAdd: null
    ));
    options.LogTo(Console.WriteLine, LogLevel.Information);
});

// Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT Configuration - Updated with better error handling
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? builder.Configuration["JWT:Key"]
    ?? throw new InvalidOperationException(
        "JWT Key is not configured. Set JWT_KEY environment variable in Railway.");

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? builder.Configuration["JWT:Issuer"]
    ?? "YesList";

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? builder.Configuration["JWT:Audience"]
    ?? jwtIssuer;

// Development fallback (remove in production)
if (builder.Environment.IsDevelopment() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_KEY")))
{
    jwtKey = "dev-key-" + Guid.NewGuid().ToString("N");
    Console.WriteLine("⚠️ WARNING: Using temporary JWT key for development!");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "Authentication failed");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://your-frontend.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod() // ← Critical for POST
            .AllowCredentials(); // ← If using cookies
    });
});

// Later in the pipeline:

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Health check with database verification
app.MapGet("/health", async (ApplicationDbContext dbContext) =>
{
    try
    {
        await dbContext.Database.CanConnectAsync();
        return Results.Ok("Healthy");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unhealthy: {ex.Message}");
    }
});

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");// ← Place this AFTER UseRouting()
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); app.Use(async (context, next) =>
{
    Console.WriteLine($"Incoming {context.Request.Method} to {context.Request.Path}");
    await next();
});

// Database migration with better error handling
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Applying database migrations...");
    await context.Database.MigrateAsync();
    logger.LogInformation("Migrations applied successfully");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to apply database migrations");

    if (!app.Environment.IsDevelopment())
    {
        // In production, exit if migrations fail
        Environment.Exit(1);
    }
}

// Configure port
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Logger.LogInformation("Starting web server on port {Port}", port);
app.Run($"http://0.0.0.0:{port}");