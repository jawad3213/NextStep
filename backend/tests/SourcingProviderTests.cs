using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NextStep.Modules.Identity.Services;
using NextStep.Modules.Sourcing.Controllers;
using NextStep.Modules.Sourcing.DTOs;
using NextStep.Modules.Sourcing.Services;

namespace NextStep.Tests;

public class SourcingProviderTests
{
    [Fact]
    public void All_Should_Contain_Only_Supported_Job_Providers()
    {
        SourcingProviders.All.Should().Equal("linkedin", "indeed", "glassdoor");
        SourcingProviders.All.Should().NotContain("instagram");
    }

    [Fact]
    public void NormalizeRequestedProviders_Should_Normalize_And_Dedupe_Supported_Providers()
    {
        var result = SourcingProviders.NormalizeRequestedProviders(
            [" LinkedIn ", "indeed", "LINKEDIN", "glassdoor"]);

        result.Should().Equal("linkedin", "indeed", "glassdoor");
    }

    [Fact]
    public void NormalizeRequestedProviders_Should_Reject_Instagram()
    {
        var act = () => SourcingProviders.NormalizeRequestedProviders(["instagram"]);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*instagram*Allowed providers: linkedin, indeed, glassdoor*");
    }

    [Fact]
    public void ContractFilter_Should_Match_Only_Requested_Internships()
    {
        SourcingContractFilters.Matches(
                [NormalizedContractTypes.Internship],
                NormalizedContractTypes.Internship,
                "Full-time",
                "Project Lead H/F",
                "Stage de fin d'etudes")
            .Should()
            .BeTrue();

        SourcingContractFilters.Matches(
                [NormalizedContractTypes.Internship],
                NormalizedContractTypes.FullTime,
                "Full-time",
                "Project Lead H/F",
                "Permanent role")
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ContractNormalizer_Should_Not_Treat_Generic_Contract_Text_As_Freelance()
    {
        var normalized = SourcingContractFilters.NormalizeContractType(
            normalizedContractType: null,
            employmentType: null,
            title: "Software Engineer",
            description: "The contract includes standard benefits and internal mobility.");

        normalized.Should().Be(NormalizedContractTypes.Other);
    }

    [Fact]
    public async Task Search_Should_Return_BadRequest_When_Service_Rejects_Unsupported_Provider()
    {
        var service = new Mock<ISourcedOfferService>();
        service
            .Setup(x => x.SearchAsync(
                It.IsAny<Guid>(),
                It.IsAny<SourcedOfferSearchRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException(
                "Unsupported sourcing provider 'instagram'. Allowed providers: linkedin, indeed, glassdoor."));

        var userService = new Mock<IUserService>();
        var controller = new SourcedOffersController(service.Object, userService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        var result = await controller.Search(
            new SourcedOfferSearchRequest { Providers = ["instagram"] },
            CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
