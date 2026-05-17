using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NextStep.Modules.Chatbot.Controllers;
using NextStep.Modules.Chatbot.DTOs;
using NextStep.Modules.Chatbot.Interfaces;
using Xunit;

namespace NextStep.Tests;

public class ArenaControllerTests
{
    private readonly Mock<IArenaService> _mockArenaService;
    private readonly ArenaController _controller;

    public ArenaControllerTests()
    {
        _mockArenaService = new Mock<IArenaService>();
        _controller = new ArenaController(_mockArenaService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim("sub", "keycloak-user-123")
                    }))
                }
            }
        };
    }

    [Fact]
    public void HealthCheck_Should_ReturnOk()
    {
        // Act
        var result = _controller.HealthCheck();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GenerateQuestions_Should_SetUserId_And_ReturnOk()
    {
        // Arrange
        var request = new QuestionsRequest(Mode: null, UserId: null, OfferId: null);
        var expectedServiceRequest = request with { UserId = "keycloak-user-123", Mode = "arena" };
        var expectedResponse = new QuestionsResponse(new List<QuestionItemDto>(), null);

        _mockArenaService
            .Setup(s => s.GenerateQuestionsAsync(expectedServiceRequest))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GenerateQuestions(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
        _mockArenaService.Verify(s => s.GenerateQuestionsAsync(expectedServiceRequest), Times.Once);
    }

    [Fact]
    public async Task FreeChat_Should_SetUserId_And_ReturnOk()
    {
        // Arrange
        var request = new FreeChatRequest("Hello", "thread-1", new List<MessageTurnDto>());
        var expectedServiceRequest = request with { UserId = "keycloak-user-123", Mode = "arena" };
        var expectedResponse = new FreeChatResponse("Hi!", "thread-1");

        _mockArenaService
            .Setup(s => s.FreeChatAsync(expectedServiceRequest))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.FreeChat(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task StartSession_Should_SetUserIdAndMode_And_ReturnOk()
    {
        // Arrange
        var request = new StartSessionRequest(null, null, null);
        var expectedServiceRequest = request with { UserId = "keycloak-user-123", Mode = "arena" };
        var expectedResponse = new StartSessionResponse("session-1", "Hello");

        _mockArenaService
            .Setup(s => s.StartSessionAsync(expectedServiceRequest))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.StartSession(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task SendMessage_Should_SetUserIdAndMode_And_ReturnOk()
    {
        // Arrange
        var request = new SendMessageRequest("session-1", "User text", new List<MessageTurnDto>());
        var expectedServiceRequest = request with { UserId = "keycloak-user-123", Mode = "arena" };
        var expectedResponse = new SendMessageResponse("AI text", "session-1");

        _mockArenaService
            .Setup(s => s.SendMessageAsync(expectedServiceRequest))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SendMessage(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task SendMessage_Should_Return500_On_Exception()
    {
        // Arrange
        var request = new SendMessageRequest("session-1", "User text", new List<MessageTurnDto>());
        _mockArenaService
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>()))
            .ThrowsAsync(new Exception("Network failure"));

        // Act
        var result = await _controller.SendMessage(request);

        // Assert
        var errorResult = result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task EndSession_Should_ReturnOk()
    {
        // Arrange
        var request = new EndSessionRequest("session-1", new List<MessageTurnDto>());
        var expectedServiceRequest = request with { UserId = "keycloak-user-123", Mode = "arena" };
        var mockFeedback = new FeedbackDto(90, new(), new(), new(), new(), "", "", new());
        var expectedResponse = new EndSessionResponse("session-1", 90, mockFeedback);

        _mockArenaService
            .Setup(s => s.EndSessionAsync(expectedServiceRequest))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.EndSession(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task GetSalary_Should_ReturnOk()
    {
        // Arrange
        var request = new SalaryRequest("Dev", "Google", null);
        var expectedServiceRequest = request with { UserId = "keycloak-user-123", Mode = "arena" };
        var expectedResponse = new SalaryResponse(10000, 20000, "USD", 15000, "high", new List<string>(), new List<NegotiationStepDto>());

        _mockArenaService
            .Setup(s => s.GetSalaryAsync(expectedServiceRequest))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetSalary(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task SalaryCoach_Should_ReturnOk()
    {
        // Arrange
        var mockContext = new SalaryContextDto(10000, 20000, "USD", 15000);
        var request = new SalaryCoachRequest("negotiate", "thread-1", mockContext, new List<MessageTurnDto>());
        var expectedServiceRequest = request with { UserId = "keycloak-user-123", Mode = "arena" };
        var expectedResponse = new SalaryCoachResponse("ok", "Coach response");

        _mockArenaService
            .Setup(s => s.SalaryCoachAsync(expectedServiceRequest))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SalaryCoach(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task GetSessions_Should_ReturnSessions()
    {
        // Arrange
        var expectedResponse = new List<SessionSummaryDto>();
        _mockArenaService
            .Setup(s => s.GetSessionsAsync("keycloak-user-123"))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetSessions();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task GetSessions_Should_ReturnUnauthorized_When_UserIdIsEmpty()
    {
        // Arrange
        var emptyController = new ArenaController(_mockArenaService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()) // No claims
                }
            }
        };

        // Act
        var result = await emptyController.GetSessions();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetSessionDetail_Should_ReturnDetail()
    {
        // Arrange
        var expectedResponse = new SessionDetailDto("session-1", "arena", "Dev", "senior", 80, DateTime.UtcNow, new List<DimensionScoreDto>(), new List<string>(), new List<string>(), new List<string>(), new List<QuestionEvaluationDto>(), "best", "worst");
        _mockArenaService
            .Setup(s => s.GetSessionDetailAsync("session-1"))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetSessionDetail("session-1");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task GetSessionDetail_Should_ReturnBadRequest_When_SessionIdIsEmpty()
    {
        // Act
        var result = await _controller.GetSessionDetail("");

        // Assert
        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task DeleteSession_Should_ReturnOk_When_DeletedSuccessfully()
    {
        // Arrange
        _mockArenaService
            .Setup(s => s.DeleteSessionAsync("session-1", "keycloak-user-123"))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteSession("session-1");

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task DeleteSession_Should_ReturnNotFound_When_NotDeleted()
    {
        // Arrange
        _mockArenaService
            .Setup(s => s.DeleteSessionAsync("session-1", "keycloak-user-123"))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteSession("session-1");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyOffers_Should_ReturnOffers()
    {
        // Arrange
        var expectedResponse = new List<UserOfferSummaryDto>();
        _mockArenaService
            .Setup(s => s.GetUserOffersAsync("keycloak-user-123"))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetMyOffers();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedResponse);
    }
}
