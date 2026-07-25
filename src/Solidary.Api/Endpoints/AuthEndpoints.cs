using MediatR;
using Solidary.Application.UseCases.Auth.Login;
using Solidary.Application.UseCases.Auth.Register;

namespace Solidary.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (RegisterDonorCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/users/{result.Value!.UserId}", result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        group.MapPost("/login", async (LoginCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        });

        return app;
    }
}
