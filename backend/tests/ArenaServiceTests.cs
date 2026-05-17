using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NextStep.data;
using NextStep.Modules.Chatbot.DTOs;
using NextStep.Modules.Chatbot.Interfaces;
using NextStep.Modules.Chatbot.Models;
using NextStep.Modules.Chatbot.Services;
using NextStep.Modules.Identity.Models;
using Xunit;

namespace NextStep.Tests;

public class ArenaServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IAgentHttpClient> _mockAgentClient;
    private readonly ArenaService _service;

    public ArenaServiceTests()
    {
        // Use a unique name for each In-Memory Database to isolate tests
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _mockAgentClient = new Mock<IAgentHttpClient>();
        _service = new ArenaService(_mockAgentClient.Object, _db);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 1 — Questions Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQuestionsAsync_Should_CallAgentClient_And_ReturnQuestions()
    {
        // Arrange
        var request = new QuestionsRequest(Mode: "arena", UserId: "user-1", OfferId: null);
        var expectedResponse = new QuestionsResponse(
            Questions: new List<QuestionItemDto>
            {
                new("q1", "What is React?", "technical", "generated", false, "Explain virtual DOM")
            },
            SessionId: null
        );

        _mockAgentClient
            .Setup(c => c.PostQuestionsAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.GenerateQuestionsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Questions.Should().HaveCount(1);
        result.Questions[0].Question.Should().Be("What is React?");
        _mockAgentClient.Verify(c => c.PostQuestionsAsync(request), Times.Once);
    }

    [Fact]
    public async Task FreeChatAsync_Should_CallAgentClient_And_ReturnChatResponse()
    {
        // Arrange
        var request = new FreeChatRequest("Hello", "thread-1", new List<MessageTurnDto>());
        var expectedResponse = new FreeChatResponse("Hi there!", "thread-1");

        _mockAgentClient
            .Setup(c => c.PostFreeChatAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.FreeChatAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Response.Should().Be("Hi there!");
        _mockAgentClient.Verify(c => c.PostFreeChatAsync(request), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 2 — Interview Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartSessionAsync_Should_CallAgentClient_And_ReturnOpeningMessage()
    {
        // Arrange
        var request = new StartSessionRequest(null, "arena", "user-1");
        var expectedResponse = new StartSessionResponse("session-123", "Welcome to your interview!");

        _mockAgentClient
            .Setup(c => c.PostStartInterviewAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.StartSessionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be("session-123");
        result.OpeningMessage.Should().Be("Welcome to your interview!");
        _mockAgentClient.Verify(c => c.PostStartInterviewAsync(request), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_Should_CallAgentClient_And_ReturnAiResponse()
    {
        // Arrange
        var request = new SendMessageRequest("session-123", "Here is my answer", new List<MessageTurnDto>());
        var expectedResponse = new SendMessageResponse("Good job! Tell me more.", "session-123");

        _mockAgentClient
            .Setup(c => c.PostSendMessageAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.SendMessageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().Be("Good job! Tell me more.");
        _mockAgentClient.Verify(c => c.PostSendMessageAsync(request), Times.Once);
    }

    [Fact]
    public async Task EndSessionAsync_Should_CallAgentClient_And_ReturnEvaluation()
    {
        // Arrange
        var request = new EndSessionRequest("session-123", new List<MessageTurnDto>());
        var mockFeedback = new FeedbackDto(85, new(), new(), new(), new(), "", "", new());
        var expectedResponse = new EndSessionResponse("session-123", 85, mockFeedback);

        _mockAgentClient
            .Setup(c => c.PostEndInterviewAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.EndSessionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be("session-123");
        result.Score.Should().Be(85);
        _mockAgentClient.Verify(c => c.PostEndInterviewAsync(request), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 3 — Salary Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSalaryAsync_Should_CallAgentClient_And_ReturnSalaryScript()
    {
        // Arrange
        var request = new SalaryRequest("Software Engineer", "OCP", null);
        var expectedResponse = new SalaryResponse(
            RangeMin: 15000,
            RangeMax: 25000,
            Currency: "MAD",
            YourTarget: 22000,
            ConfidenceLevel: "high",
            MarketSources: new List<string> { "Glassdoor" },
            NegotiationScript: new List<NegotiationStepDto>()
        );

        _mockAgentClient
            .Setup(c => c.PostSalaryAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.GetSalaryAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RangeMin.Should().Be(15000);
        result.RangeMax.Should().Be(25000);
        _mockAgentClient.Verify(c => c.PostSalaryAsync(request), Times.Once);
    }

    [Fact]
    public async Task SalaryCoachAsync_Should_ConstructFreeChatRequest_And_ReturnCoachResponse()
    {
        // Arrange
        var mockContext = new SalaryContextDto(15000, 25000, "MAD", 22000);
        var request = new SalaryCoachRequest("How to negotiate?", "thread-123", mockContext, new List<MessageTurnDto>(), "arena", "user-123", null);
        
        var expectedAgentRequest = new FreeChatRequest(
            UserInput: "How to negotiate?",
            ThreadId: "thread-123",
            History: request.History,
            Mode: "arena",
            UserId: "user-123",
            OfferId: null,
            ArenaConfig: null
        );

        var mockAgentResponse = new FreeChatResponse("Anchor at 25k", "thread-123");

        _mockAgentClient
            .Setup(c => c.PostFreeChatAsync(expectedAgentRequest))
            .ReturnsAsync(mockAgentResponse);

        // Act
        var result = await _service.SalaryCoachAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("ok");
        result.Response.Should().Be("Anchor at 25k");
        _mockAgentClient.Verify(c => c.PostFreeChatAsync(expectedAgentRequest), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // History & Session Detail Database Queries Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSessionsAsync_Should_QueryDirectly_And_ReturnMappedSessions()
    {
        // Arrange
        var userGuid = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userGuid,
            KeycloakId = "keycloak-sub-123",
            Email = "john.doe@example.com"
        };
        await _db.Utilisateurs.AddAsync(user);

        var activeSession = new SessionCoaching
        {
            IdSession = Guid.NewGuid(),
            IdUtilisateur = userGuid,
            Mode = "arena",
            Status = "started",
            Language = "fr",
            DurationMinutes = 30,
            Domain = "Finance",
            Level = "senior",
            ScoreEntretien = 82,
            DateSession = DateTime.UtcNow
        };

        var pendingSession = new SessionCoaching
        {
            IdSession = Guid.NewGuid(),
            IdUtilisateur = userGuid,
            Mode = "offer",
            Status = "pending",
            Language = "en",
            DurationMinutes = 20,
            DateSession = DateTime.UtcNow
        };

        await _db.SessionCoachings.AddRangeAsync(activeSession, pendingSession);
        await _db.SaveChangesAsync();

        // Act (Test keycloak ID resolution)
        var result1 = await _service.GetSessionsAsync("keycloak-sub-123");

        // Act (Test direct GUID resolution)
        var result2 = await _service.GetSessionsAsync(userGuid.ToString());

        // Assert
        result1.Should().NotBeNull();
        result1.Should().HaveCount(1); // Excludes pending session
        result1[0].SessionId.Should().Be(activeSession.IdSession.ToString());
        result1[0].Mode.Should().Be("arena");
        result1[0].Status.Should().Be("started");
        result1[0].Domain.Should().Be("Finance");
        result1[0].Level.Should().Be("senior");
        result1[0].ScoreEntretien.Should().Be(82);

        result2.Should().NotBeNull();
        result2.Should().HaveCount(1);
        result2[0].SessionId.Should().Be(activeSession.IdSession.ToString());
    }

    [Fact]
    public async Task GetSessionDetailAsync_Should_DeserializeSnakeCaseFeedback_And_ReturnDetailDto()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        
        var feedbackData = new FeedbackDto(
            GlobalScore: 89,
            Dimensions: new List<DimensionScoreDto> { new("Clarity", 90, "Great structure") },
            QuestionEvaluations: new List<QuestionEvaluationDto> { new("Q1", "My Ans", 85, "Corrected") },
            Strengths: new List<string> { "Strong Python" },
            Improvements: new List<string> { "Add Kafka" },
            BestAnswer: "My STAR story",
            WorstAnswer: "None",
            CoachingTips: new List<string> { "Always anchor" }
        );

        // Serialize to match Python snake_case serialization exactly
        var options = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var feedbackJson = JsonSerializer.Serialize(feedbackData, options);

        var session = new SessionCoaching
        {
            IdSession = sessionId,
            IdUtilisateur = Guid.NewGuid(),
            Mode = "arena",
            Domain = "Data Science",
            Level = "mid",
            ScoreEntretien = 89,
            FeedbackJson = feedbackJson,
            DateSession = DateTime.UtcNow
        };

        await _db.SessionCoachings.AddAsync(session);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.GetSessionDetailAsync(sessionId.ToString());

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be(sessionId.ToString());
        result.Mode.Should().Be("arena");
        result.Domain.Should().Be("Data Science");
        result.Level.Should().Be("mid");
        result.GlobalScore.Should().Be(89);
        result.Strengths.Should().ContainSingle().Which.Should().Be("Strong Python");
        result.Improvements.Should().ContainSingle().Which.Should().Be("Add Kafka");
        result.Dimensions.Should().HaveCount(1);
        result.Dimensions[0].Name.Should().Be("Clarity");
        result.Dimensions[0].Score.Should().Be(90);
        result.QuestionEvaluations.Should().HaveCount(1);
        result.QuestionEvaluations[0].Question.Should().Be("Q1");
        result.BestAnswer.Should().Be("My STAR story");
    }

    [Fact]
    public async Task DeleteSessionAsync_Should_DeleteOwnedSession_And_ReturnTrue()
    {
        // Arrange
        var userGuid = Guid.NewGuid();
        var sessionGuid = Guid.NewGuid();

        var user = new UserEntity
        {
            Id = userGuid,
            KeycloakId = "keycloak-123",
            Email = "user@test.com"
        };

        var session = new SessionCoaching
        {
            IdSession = sessionGuid,
            IdUtilisateur = userGuid,
            Mode = "arena",
            Status = "started"
        };

        await _db.Utilisateurs.AddAsync(user);
        await _db.SessionCoachings.AddAsync(session);
        await _db.SaveChangesAsync();

        // Act (Unauthorized attempt with different user sub)
        var unauthResult = await _service.DeleteSessionAsync(sessionGuid.ToString(), "other-user");

        // Act (Authorized deletion)
        var authResult = await _service.DeleteSessionAsync(sessionGuid.ToString(), "keycloak-123");

        // Assert
        unauthResult.Should().BeFalse();
        authResult.Should().BeTrue();

        var dbSession = await _db.SessionCoachings.FindAsync(sessionGuid);
        dbSession.Should().BeNull(); // Verified deleted from Database
    }
}
