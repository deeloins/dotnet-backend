using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context); // call the next middleware in the pipeline
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception caught in middleware");

            context.Response.StatusCode = 500; // Internal Server Error
            context.Response.ContentType = "application/json";

            var response = new { error = ex.Message };
            var json = System.Text.Json.JsonSerializer.Serialize(response);

            await context.Response.WriteAsync(json);
        }
    }
}
