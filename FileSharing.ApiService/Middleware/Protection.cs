namespace FileSharing.ApiService.Middleware;

public class Protection
{
    // TODO: Nginx
    // proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    // proxy_set_header X-Forwarded-Proto $scheme;
    
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public Protection(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_env.IsProduction())
        {
            // TODO: CRSF?
            if (!context.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden");
                return;
            }
        }

        await _next(context);
    }
}