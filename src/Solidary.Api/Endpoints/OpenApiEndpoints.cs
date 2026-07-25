using Swashbuckle.AspNetCore.SwaggerUI;

namespace Solidary.Api.Endpoints;

public static class OpenApiEndpoints
{
    // Exposed unconditionally (not gated to Development) so Swagger is reachable in the
    // compose/K8s demo environment too — the hackathon grading script demos auth via Swagger/Postman.
    public static WebApplication MapOpenApiEndpoints(this WebApplication app)
    {
        app.MapOpenApi();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Solidary API v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
