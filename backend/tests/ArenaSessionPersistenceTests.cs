using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Coaching.Application.Dtos;
using NextStep.Modules.Coaching.Application.Services;
using NextStep.Modules.Coaching.Domain;
using NextStep.Modules.Coaching.Infrastructure.Agents;
using NextStep.Modules.Coaching.Infrastructure.Persistence;
using NextStep.Modules.Profile.Contracts;
using Xunit;

namespace NextStep.Tests;

/// <summary>
/// The Coaching module owns sessions and questions: the agents only compute, the backend
/// saves (and reuses) around each agent call.
/// </summary>
public class ArenaSessionPersistenceTests : IDisposable
{
    private const string KeycloakId = "kc-user";
    private readonly Guid _userId = Guid.NewGuid();
    private readonly CoachingDbContext _db;
    private readonly Mock<IAgentHttpClient> _agents = new();
    private readonly Mock<IApplicationsApi> _applications = new();
    private readonly Mock<IProfileApi> _profile = new();
    private readonly ArenaService _service;

    private static readonly ArenaConfigDto Arena = new("Backend", "junior", 20, "fr", ["APIs"]);

    public ArenaSessionPersistenceTests()
    {
        _db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _profile.Setup(p => p.FindUserIdAsync(KeycloakId, It.IsAny<CancellationToken>())).ReturnsAsync(_userId);
        _service = new ArenaService(_agents.Object, _db, _applications.Object, _profile.Object, NullLogger<ArenaService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private void AgentsGenerate(params string[] questions) =>
        _agents.Setup(a => a.PostQuestionsAsync(It.IsAny<QuestionsRequest>()))
            .ReturnsAsync(new QuestionsResponse(
                questions.Select((q, i) => new QuestionItemDto($"g{i}", q, "technical", "generated", false, "tip")).ToList(),
                null));

    [Fact]
    public async Task Generated_questions_are_saved_with_a_pending_session()
    {
        AgentsGenerate("Q1", "Q2");

        var result = await _service.GenerateQuestionsAsync(new QuestionsRequest("arena", KeycloakId, null, Arena));

        var session = await _db.SessionCoachings.SingleAsync();
        session.Should().BeEquivalentTo(new { IdUtilisateur = _userId, Mode = "arena", Status = "pending", Domain = "Backend", Language = "fr" });
        result.SessionId.Should().Be(session.IdSession.ToString());
        var saved = await _db.QuestionEntrainements.OrderBy(q => q.Ordre).ToListAsync();
        saved.Select(q => q.TexteQuestion).Should().Equal("Q1", "Q2");
        result.Questions.Select(q => q.Id).Should().Equal(saved.Select(q => q.IdQuestion.ToString()));
    }

    [Fact]
    public async Task Same_arena_configuration_reuses_the_questions()
    {
        AgentsGenerate("Q1");
        var request = new QuestionsRequest("arena", KeycloakId, null, Arena);

        var first = await _service.GenerateQuestionsAsync(request);
        var second = await _service.GenerateQuestionsAsync(request);

        _agents.Verify(a => a.PostQuestionsAsync(It.IsAny<QuestionsRequest>()), Times.Once);
        second.Questions.Should().BeEquivalentTo(first.Questions);
    }

    [Fact]
    public async Task Offer_questions_are_reused_for_the_same_application()
    {
        var offerId = Guid.NewGuid();
        var candidatureId = Guid.NewGuid();
        _applications.Setup(a => a.FindApplicationForOfferAsync(_userId, offerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationSnapshot(candidatureId, _userId, offerId, "ENVOYE", "EN_ATTENTE", false, null, null, null, null, null, null));
        AgentsGenerate("Offer question");
        var request = new QuestionsRequest("offer", KeycloakId, offerId.ToString());

        await _service.GenerateQuestionsAsync(request);
        var again = await _service.GenerateQuestionsAsync(request);

        _agents.Verify(a => a.PostQuestionsAsync(It.IsAny<QuestionsRequest>()), Times.Once);
        again.Questions.Single().Question.Should().Be("Offer question");
        (await _db.SessionCoachings.SingleAsync()).IdCandidature.Should().Be(candidatureId);
    }

    [Fact]
    public async Task Interview_session_is_created_then_completed_with_the_evaluation()
    {
        _agents.Setup(a => a.PostStartInterviewAsync(It.IsAny<StartSessionRequest>()))
            .ReturnsAsync((StartSessionRequest r) => new StartSessionResponse(r.SessionId!, "Bonjour"));
        var start = await _service.StartSessionAsync(new StartSessionRequest(null, "arena", KeycloakId, ArenaConfig: Arena));

        var session = await _db.SessionCoachings.SingleAsync();
        session.IdSession.ToString().Should().Be(start.SessionId);
        session.Status.Should().Be("started");

        var feedback = new FeedbackDto(78, [new DimensionScoreDto("Clarity", 80, "Good")], [], ["STAR"], ["Metrics"], "B", "W", ["Tip"]);
        _agents.Setup(a => a.PostEndInterviewAsync(It.IsAny<EndSessionRequest>()))
            .ReturnsAsync(new EndSessionResponse(start.SessionId, 78, feedback));
        await _service.EndSessionAsync(new EndSessionRequest(start.SessionId, [], "arena", KeycloakId));

        var completed = await _db.SessionCoachings.AsNoTracking().SingleAsync();
        completed.Status.Should().Be("completed");
        completed.ScoreEntretien.Should().Be(78);
        completed.CompletedAt.Should().NotBeNull();
        completed.FeedbackJson.Should().Contain("\"global_score\":78");

        var detail = await _service.GetSessionDetailAsync(start.SessionId, KeycloakId);
        detail.Dimensions.Single().Name.Should().Be("Clarity");
        detail.GlobalScore.Should().Be(78);
    }

    [Fact]
    public async Task Offers_list_comes_from_the_applications_module()
    {
        var offerId = Guid.NewGuid();
        _applications.Setup(a => a.ListAppliedOfferIdsAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync([offerId]);
        _applications.Setup(a => a.GetOfferSummaryAsync(_userId, offerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfferSummary(offerId, ".NET Developer", "Acme", null,
                ["C#", "SQL", "Docker", "Azure", "Git", "Scrum", "Kafka"], [],
                Location: "Casablanca", ContractType: "CDI", YearsExperience: 2, MatchingScore: 74));

        var offer = (await _service.GetUserOffersAsync(KeycloakId)).Single();

        offer.Should().BeEquivalentTo(new
        {
            OfferId = offerId.ToString(), JobTitle = ".NET Developer", Company = "Acme",
            Location = "Casablanca", ContractType = "CDI", MatchingScore = (int?)74, YearsExperience = (int?)2,
        });
        offer.RequiredSkills.Should().HaveCount(6);
    }

    [Fact]
    public async Task Another_users_session_is_never_updated()
    {
        var foreign = new SessionCoaching { IdUtilisateur = Guid.NewGuid(), Status = "started" };
        _db.SessionCoachings.Add(foreign);
        await _db.SaveChangesAsync();
        _agents.Setup(a => a.PostEndInterviewAsync(It.IsAny<EndSessionRequest>()))
            .ReturnsAsync(new EndSessionResponse(foreign.IdSession.ToString(), 90, new FeedbackDto(90, [], [], [], [], "", "", [])));

        await _service.EndSessionAsync(new EndSessionRequest(foreign.IdSession.ToString(), [], "arena", KeycloakId));

        (await _db.SessionCoachings.AsNoTracking().SingleAsync()).Status.Should().Be("started");
    }
}
