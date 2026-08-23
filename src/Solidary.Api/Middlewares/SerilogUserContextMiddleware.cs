using Serilog.Context;

namespace Solidary.Api.Middlewares;

public class SerilogUserContextMiddleware
{
    private readonly RequestDelegate _next;

    public SerilogUserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var username = context.User.Identity?.IsAuthenticated == true 
            ? context.User.Identity.Name 
            : "Anonymous";
        
        using (LogContext.PushProperty("User", username))
        {
            await _next(context);
        }
    }
}