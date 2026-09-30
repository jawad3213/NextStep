using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NextStep.Modules.Applications.Api;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.ErrorHandling;
using Xunit;

namespace NextStep.Tests;

/// <summary>
/// The three-agent analysis is started as fire-and-forget work so the browser never holds
/// an idle connection for minutes. That changes what the endpoint must guarantee up front:
/// ownership has to be checked before acknowledging, otherwise a foreign offer is accepted
/// with 202 and the caller only finds out through a timeout.
/// </summary>
public class OfferControllerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OfferId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IOfferService> _offerService = new();
    private readonly Mock<IOfferAnalysisService> _analysisService = new();
    private readonly Mock<IProfileApi> _profile = new();

    private OfferController CreateController() => new(
        _offerService.Object,
        _analysisService.Object,
        Mock.Of<ISkillGapService>(),
        _profile.Object,
        NullLogger<OfferController>.Instance)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "keycloak-user-123") }))
            }
        }
    };

    private void ResolveUser()
    {
        _profile.Setup(p => p.EnsureUserIdAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(UserId);
    }

    [Fact]
    public async Task AnalyzeSync_Should_AcceptAndStartTheWork_ForAnOwnedOffer()
    {
        ResolveUser();
        _offerService.Setup(s => s.OfferBelongsToUserAsync(UserId, OfferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.AnalyzeSync(OfferId, new ResumePipelineDto { TemplateId = 1 });

        result.Should().BeOfType<AcceptedResult>();
        _analysisService.Verify(
            s => s.StartAnalysisInBackground(UserId, OfferId), Times.Once);
    }

    [Fact]
    public async Task AnalyzeSync_Should_NotFound_AndStartNothing_ForAnUnknownOrForeignOffer()
    {
        ResolveUser();
        _offerService.Setup(s => s.OfferBelongsToUserAsync(UserId, OfferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CreateController();

        var result = await controller.AnalyzeSync(OfferId, new ResumePipelineDto { TemplateId = 1 });

        result.Should().BeOfType<NotFoundObjectResult>();
        _analysisService.Verify(
            s => s.StartAnalysisInBackground(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeSync_Should_NotRunTheWork_ForAnotherUsersOffer()
    {
        var intruderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        ResolveUser();
        _offerService.Setup(s => s.OfferBelongsToUserAsync(intruderId, OfferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CreateController();

        var result = await controller.AnalyzeSync(OfferId, new ResumePipelineDto { TemplateId = 1 });

        result.Should().BeOfType<NotFoundObjectResult>();
        _analysisService.Verify(
            s => s.StartAnalysisInBackground(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    /// <summary>
    /// The analysis is stored only when the agents are done, so a client polling for the
    /// result receives an empty 200. Returning 404 here would surface as an error toast on
    /// every poll while a perfectly normal background run is in progress.
    /// </summary>
    [Fact]
    public async Task GetAnalysis_Should_ReturnOkWithNoBody_WhileTheAnalysisIsStillRunning()
    {
        ResolveUser();
        _offerService.Setup(s => s.GetAnalysisAsync(UserId, OfferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OfferAnalysisDto?)null);
        var controller = CreateController();

        var result = await controller.GetAnalysis(OfferId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetAnalysis_Should_ReturnTheStoredAnalysis_WhenItIsReady()
    {
        ResolveUser();
        var analysis = new OfferAnalysisDto { OfferId = OfferId, Titre = "PFE IA", ScoreMatching = 72 };
        _offerService.Setup(s => s.GetAnalysisAsync(UserId, OfferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);
        var controller = CreateController();

        var result = await controller.GetAnalysis(OfferId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(analysis);
    }

    /// <summary>An unknown or foreign offer is still an error, not an empty result.</summary>
    [Fact]
    public async Task GetAnalysis_Should_PropagateNotFound_ForAnUnknownOrForeignOffer()
    {
        ResolveUser();
        _offerService.Setup(s => s.GetAnalysisAsync(UserId, OfferId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Analysis not found."));
        var controller = CreateController();

        var act = () => controller.GetAnalysis(OfferId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
