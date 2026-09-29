using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NextStep.Modules.Profile.Application.Services;
using NextStep.Modules.Profile.Domain;
using NextStep.Modules.Profile.Infrastructure.Persistence;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;
using Xunit;

namespace NextStep.Tests;

/// <summary>
/// The user's language preference: defaults to English, persists server-side (not just in
/// the browser), and is stamped onto the resume-generation request sent to the agents so
/// the AI-written summary comes back in the right language regardless of what the frontend
/// happened to send.
/// </summary>
public class ProfileServiceLanguagePreferenceTests : IDisposable
{
    private readonly ProfileDbContext _db;
    private readonly ProfileService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public ProfileServiceLanguagePreferenceTests()
    {
        _db = new ProfileDbContext(new DbContextOptionsBuilder<ProfileDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Utilisateurs.Add(new UserEntity { Id = _userId, KeycloakId = "kc", Email = "u@test.com" });
        _db.SaveChanges();
        _service = new ProfileService(_db, Mock.Of<IUserService>());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task New_users_default_to_english()
    {
        (await _service.GetLanguagePreferenceAsync(_userId)).Should().Be("en");
        (await _service.GetFullProfileAsync(_userId)).PreferredLanguage.Should().Be("en");
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("FR")]
    [InlineData(" en ")]
    public async Task Supported_languages_are_persisted_case_and_whitespace_insensitively(string input)
    {
        await _service.UpdateLanguagePreferenceAsync(_userId, input);

        var stored = await _service.GetLanguagePreferenceAsync(_userId);
        stored.Should().Be(input.Trim().ToLowerInvariant());
    }

    [Theory]
    [InlineData("de")]
    [InlineData("")]
    [InlineData("english")]
    public async Task Unsupported_languages_are_refused(string input)
    {
        var act = () => _service.UpdateLanguagePreferenceAsync(_userId, input);

        await act.Should().ThrowAsync<BadRequestException>();
        (await _service.GetLanguagePreferenceAsync(_userId)).Should().Be("en");
    }
}

public class ResumeImportServiceLanguageTests
{
    private readonly Mock<IAgentHttpClient> _agents = new();
    private readonly Mock<IProfileService> _profileService = new();
    private readonly ResumeImportService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public ResumeImportServiceLanguageTests()
    {
        _service = new ResumeImportService(_agents.Object, _profileService.Object);
    }

    [Fact]
    public async Task The_users_stored_language_preference_is_stamped_onto_the_request()
    {
        _profileService.Setup(p => p.GetLanguagePreferenceAsync(_userId)).ReturnsAsync("fr");
        JsonObject? sentPayload = null;
        _agents.Setup(a => a.PostRawAsync(It.IsAny<string>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .Callback<string, JsonObject, CancellationToken>((_, payload, _) => sentPayload = payload)
            .ReturnsAsync(JsonDocument.Parse("""{"resume":"Some summary."}"""));

        await _service.GenerateResumeAsync(_userId, new { personal = new { jobTitle = "Dev" } });

        sentPayload.Should().NotBeNull();
        sentPayload!["language"]!.GetValue<string>().Should().Be("fr");
        sentPayload["personal"].Should().NotBeNull();
    }

    [Fact]
    public async Task Upstream_failure_degrades_to_an_explicit_error_instead_of_throwing()
    {
        _profileService.Setup(p => p.GetLanguagePreferenceAsync(_userId)).ReturnsAsync("en");
        _agents.Setup(a => a.PostRawAsync(It.IsAny<string>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var result = await _service.GenerateResumeAsync(_userId, new { });

        result.Errors.Should().NotBeEmpty();
    }
}
