using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Modules.Applications.Domain;
using NextStep.Modules.Applications.Infrastructure.Persistence;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;
using Xunit;

namespace NextStep.Tests;

/// <summary>The shared-secret check of the /internal/agents endpoints.</summary>
public class InternalApiKeyAttributeTests
{
    private static AuthorizationFilterContext Context(string? configuredKey, string? sentKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AGENTS_API_KEY"] = configuredKey })
            .Build();
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<IConfiguration>(configuration).BuildServiceProvider(),
        };
        if (sentKey is not null) http.Request.Headers[AgentApiKeyHandler.HeaderName] = sentKey;
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }

    [Fact]
    public void Right_key_is_let_through()
    {
        var context = Context("s3cret", "s3cret");
        new InternalApiKeyAttribute().OnAuthorization(context);
        context.Result.Should().BeNull();
    }

    [Theory]
    [InlineData("s3cret", null)]
    [InlineData("s3cret", "wrong")]
    [InlineData("s3cret", "")]
    [InlineData("", "")]      // no key configured: everything is refused
    [InlineData(null, "anything")]
    public void Missing_or_wrong_key_is_refused(string? configured, string? sent)
    {
        var context = Context(configured, sent);
        new InternalApiKeyAttribute().OnAuthorization(context);
        context.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}

/// <summary>What SN Copilot may read and change through the Applications module.</summary>
public class AgentApplicationsServiceTests : IDisposable
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ApplicationsDbContext _db;
    private readonly Mock<ICandidatureService> _candidatures = new();
    private readonly AgentApplicationsService _service;

    public AgentApplicationsServiceTests()
    {
        _db = new ApplicationsDbContext(new DbContextOptionsBuilder<ApplicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _service = new AgentApplicationsService(_candidatures.Object, _db);
    }

    public void Dispose() => _db.Dispose();

    private static CandidatureDto Candidature(Guid? offerId = null, string? notes = null, DateTime? sentAt = null) => new()
    {
        IdCandidature = Guid.NewGuid(),
        IdOffre = offerId,
        Statut = "ENVOYE",
        Notes = notes,
        ApplicationDate = sentAt ?? DateTime.UtcNow,
    };

    [Fact]
    public async Task List_resolves_company_and_role_from_the_offer_analysis_or_the_notes()
    {
        var offer = new OffreEmploi
        {
            UtilisateurId = _userId,
            TexteBrut = "...",
            AnalyseJson = """{"analyzed_offer":{"titre":".NET Developer","entreprise":"Acme"}}""",
        };
        _db.OffresEmploi.Add(offer);
        await _db.SaveChangesAsync();
        _candidatures.Setup(c => c.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync([
            Candidature(offer.Id, sentAt: DateTime.UtcNow),
            Candidature(notes: "Globex\nData Engineer\nvia LinkedIn", sentAt: DateTime.UtcNow.AddDays(-1)),
            Candidature(sentAt: DateTime.UtcNow.AddDays(-2)),
        ]);

        var list = await _service.ListAsync(_userId, 50);

        list.Select(c => (c.Entreprise, c.Role)).Should().Equal(
            ("Acme", ".NET Developer"),
            ("Globex", "Data Engineer"),
            ("Company not specified", "Position not specified"));
    }

    [Fact]
    public async Task Unknown_status_is_refused_before_anything_is_changed()
    {
        var act = () => _service.UpdateStatusAsync(_userId, Guid.NewGuid(), new AgentStatusUpdateDto { NouveauStatut = "WHATEVER" });

        await act.Should().ThrowAsync<BadRequestException>();
        _candidatures.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Another_users_application_cannot_be_changed()
    {
        var id = Guid.NewGuid();
        _candidatures.Setup(c => c.GetOwnedAsync(_userId, id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("Access denied."));

        var act = () => _service.UpdateStatusAsync(_userId, id, new AgentStatusUpdateDto { NouveauStatut = "REFUSE" });

        await act.Should().ThrowAsync<ForbiddenException>();
        _candidatures.Verify(c => c.UpdateStatutAsync(It.IsAny<Guid>(), It.IsAny<UpdateStatutDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Status_change_is_recorded_as_made_by_the_copilot()
    {
        var application = Candidature();
        _candidatures.Setup(c => c.GetOwnedAsync(_userId, application.IdCandidature, It.IsAny<CancellationToken>())).ReturnsAsync(application);
        _candidatures.Setup(c => c.UpdateStatutAsync(application.IdCandidature, It.IsAny<UpdateStatutDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(application);
        _candidatures.Setup(c => c.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync([application]);

        await _service.UpdateStatusAsync(_userId, application.IdCandidature, new AgentStatusUpdateDto { NouveauStatut = "entretien_propose" });

        _candidatures.Verify(c => c.UpdateStatutAsync(
            application.IdCandidature,
            It.Is<UpdateStatutDto>(d => d.NouveauStatut == "ENTRETIEN_PROPOSE"),
            AgentApplicationsService.AgentSource,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Created_application_keeps_company_and_role_in_the_notes()
    {
        CreateCandidatureDto? sent = null;
        _candidatures.Setup(c => c.CreateAsync(_userId, It.IsAny<CreateCandidatureDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CreateCandidatureDto, string, CancellationToken>((_, dto, _, _) => sent = dto)
            .ReturnsAsync(Candidature());

        var created = await _service.CreateAsync(_userId, new AgentCreateCandidatureDto { Entreprise = " Acme ", Poste = "Dev", Channel = "linkedin" });

        sent!.Notes.Should().Be("Acme\nDev");
        sent.Channel.Should().Be("LINKEDIN");
        sent.AppliedManually.Should().BeTrue();
        created.Entreprise.Should().Be("Acme");
        _candidatures.Verify(c => c.CreateAsync(_userId, It.IsAny<CreateCandidatureDto>(), AgentApplicationsService.AgentSource, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Offer_analysis_is_only_returned_to_its_owner()
    {
        var offer = new OffreEmploi { UtilisateurId = _userId, TexteBrut = "...", AnalyseJson = """{"analyzed_offer":{}}""" };
        _db.OffresEmploi.Add(offer);
        await _db.SaveChangesAsync();

        (await _service.GetOfferAnalysisJsonAsync(_userId, offer.Id)).Should().NotBeNull();
        (await _service.GetOfferAnalysisJsonAsync(Guid.NewGuid(), offer.Id)).Should().BeNull();
    }
}
