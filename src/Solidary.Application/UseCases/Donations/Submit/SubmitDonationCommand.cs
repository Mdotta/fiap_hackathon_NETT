using MediatR;
using Solidary.Application.Common;

namespace Solidary.Application.UseCases.Donations.Submit;

public record SubmitDonationCommand(Guid DonorId, Guid CampaignId, decimal Amount)
    : IRequest<Result<SubmitDonationResponse>>;

public record SubmitDonationResponse(Guid DonationId, string Status);
