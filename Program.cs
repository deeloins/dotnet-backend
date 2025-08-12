using DotNetEnv;
using AuthTodoApp.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    Env.Load();
}

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("DATABASE_URL not found in environment variables.");
}

if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
{
    var databaseUri = new Uri(connectionString);
    var userInfo = databaseUri.UserInfo.Split(':');

    connectionString =
        $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.AbsolutePath.TrimStart('/')};" +
        $"Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // change to your frontend URL
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\r\n\r\nExample: \"Bearer abc123\"",
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
if (string.IsNullOrEmpty(jwtKey))
    throw new Exception("JWT_KEY not configured");

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "YesList";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "YesUsers";
var jwtDuration = int.TryParse(Environment.GetEnvironmentVariable("JWT_DURATION"), out var duration) ? duration : 60;

// Configure Authentication with JWT Bearer
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
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("Authentication failed: " + context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("Token validated successfully.");
            return Task.CompletedTask;
        }
    };
});

Console.WriteLine($"JWT_KEY: {(string.IsNullOrEmpty(jwtKey) ? "NULL or EMPTY" : "SET")}");
Console.WriteLine($"JWT_ISSUER: {jwtIssuer}");
Console.WriteLine($"JWT_AUDIENCE: {jwtAudience}");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
