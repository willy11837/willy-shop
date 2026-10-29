using System.Diagnostics;
using System.Security.Claims;

namespace WillyShop.Middleware;

public sealed class RequestLoggingScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingScopeMiddleware> _logger;

    public RequestLoggingScopeMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var activity = Activity.Current;
        var scopeProperties = new Dictionary<string, object?>
        {
            ["TraceId"] = activity?.TraceId.ToString() ?? context.TraceIdentifier,
            ["SpanId"] = activity?.SpanId.ToString()
        };

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");
            if (!string.IsNullOrWhiteSpace(userId))
                scopeProperties["UserId"] = userId;
        }

        using (_logger.BeginScope(scopeProperties))
            await _next(context);
    }
}