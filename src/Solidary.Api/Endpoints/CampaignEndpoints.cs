using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Solidary.Application.UseCases.Campaigns.Create;
using Solidary.Application.UseCases.Donations.Submit;

namespace Solidary.Api.Endpoints;

public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/campaigns");

        group.MapPost("/", async (CreateCampaignRequest request, ClaimsPrincipal user, ISender sender) =>
        {
            var command = new CreateCampaignCommand(
                request.Title,
                request.Description,
                request.StartDate,
                request.EndDate,
                request.FundingGoal,
                GetUserId(user));

            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/campaigns/{result.Value!.CampaignId}", result.Value)
                : Results.BadRequest(new { error = result.Error });
        }).RequireAuthorization("AdminOnly");

        group.MapPost("/{campaignId:guid}/donations", async (Guid campaignId, SubmitDonationRequest request, ClaimsPrincipal user, ISender sender) =>
        {
            var command = new SubmitDonationCommand(GetUserId(user), campaignId, request.Amount);

            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Accepted(value: result.Value)
                : Results.BadRequest(new { error = result.Error });
        }).RequireAuthorization();

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(subject!);
    }
}

public record CreateCampaignRequest(string Title, string Description, DateTime StartDate, DateTime EndDate, decimal FundingGoal);

public record SubmitDonationRequest(decimal Amount);
