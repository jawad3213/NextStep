using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Modules.Applications.Domain;
using NextStep.Modules.Applications.Infrastructure.Persistence;
using NextStep.Shared.Events;
using Xunit;

namespace NextStep.Tests;

/// <summary>
/// Every background run is identified by the run id stored inside the offer payload, which is
/// how a client still waiting on a run tells a fresh result from the one it already had.
/// The analysis writes that marker; a later CV generation re-saves the whole payload from the
/// agent answer, which does not carry it. If the marker were dropped there, a generation run
/// would quietly remove the evidence a waiting client depends on.
/// </summary>
public class OfferServiceRunIdTests : IDisposable
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _offerId = Guid.NewGuid();
    private readonly ApplicationsDbContext _db;
    private readonly OfferService _service;

    public OfferServiceRunIdTests()
    {
        _db = new ApplicationsDbContext(new DbContextOptionsBuilder<ApplicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        _db.OffresEmploi.Add(new OffreEmploi
        {
            Id = _offerId,
            UtilisateurId = _userId,
            TexteBrut = "PFE",
            DateCreation = DateTime.UtcNow
        });
        _db.SaveChanges();

        _service = new OfferService(
            Mock.Of<NextStep.Modules.Applications.Infrastructure.Repositories.IOfferRepository>(),
            _db,
            Mock.Of<NextStep.Modules.Applications.Contracts.ICvContentSanitizer>(),
            Mock.Of<IEventPublisher>(),
            NullLogger<OfferService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private Task SaveAsync(string json) =>
        _service.SavePipelineResultAsync(_offerId, JsonDocument.Parse(json), _userId, CancellationToken.None);

    private string? StoredRunId()
    {
        var root = JsonDocument.Parse(_db.OffresEmploi.Single(o => o.Id == _offerId).AnalyseJson!).RootElement;

        return root.TryGetProperty("analysis_run_id", out var runId) ? runId.GetString() : null;
    }

    [Fact]
    public async Task Analysis_Run_Stores_The_Marker()
    {
        await SaveAsync("""{"analysis_run_id":"run-42","analyzed_offer":{"titre":"PFE"}}""");

        StoredRunId().Should().Be("run-42");
    }

    [Fact]
    public async Task Generation_Keeps_The_Run_That_Produced_The_Analysis()
    {
        await SaveAsync("""{"analysis_run_id":"run-42","analyzed_offer":{"titre":"PFE"}}""");

        // A generation re-saves the whole payload from the agent, which has no idea which
        // analysis run it is based on.
        await SaveAsync("""{"analyzed_offer":{"titre":"PFE"},"cv_data":{"content":"CV"}}""");

        StoredRunId().Should().Be("run-42");
    }

    [Fact]
    public async Task Payload_With_Its_Own_Marker_Wins()
    {
        await SaveAsync("""{"analysis_run_id":"run-42","analyzed_offer":{"titre":"PFE"}}""");

        await SaveAsync("""{"analysis_run_id":"run-99","analyzed_offer":{"titre":"PFE"}}""");

        StoredRunId().Should().Be("run-99");
    }

    [Fact]
    public async Task Generation_On_An_Offer_Without_A_Marker_Adds_None()
    {
        await SaveAsync("""{"analyzed_offer":{"titre":"PFE"},"cv_data":{"content":"CV"}}""");

        StoredRunId().Should().BeNull();
    }
}
