using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Jobs;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Shared.Http;
using NextStep.Shared.Realtime;
using Xunit;

namespace NextStep.Tests;

/// <summary>
/// The analysis and the CV generation used to be started with <c>Task.Run</c> straight from the
/// HTTP entry point. The process was then the only owner of a run lasting several minutes: a
/// restart, a crash or a scale-down discarded the work silently, with no retry and no trace, and
/// the browser kept polling a result that would never arrive. Hangfire now owns these runs.
///
/// These tests pin the durable hand-off: the request path must only enqueue, and the job must
/// still notify the client and surface failures instead of reporting a successful empty run.
/// </summary>
public class OfferBackgroundJobsTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OfferId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IOfferService> _offerService = new();
    private readonly Mock<IAgentHttpClient> _agents = new();
    private readonly RecordingJobClient _backgroundJobs = new();

    private OfferAnalysisService CreateService() => new(
        _offerService.Object,
        _agents.Object,
        _backgroundJobs,
        NullLogger<OfferAnalysisService>.Instance);

    private Job SingleEnqueuedJob() => _backgroundJobs.Enqueued.Should().ContainSingle().Subject;

    private static (RecordingClientProxy Proxy, IHubContext<PipelineHub> Hub) CreateHub()
    {
        var proxy = new RecordingClientProxy();

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy);

        var hub = new Mock<IHubContext<PipelineHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        return (proxy, hub.Object);
    }

    private static object PayloadSentTo(RecordingClientProxy proxy, string method) =>
        proxy.Sent.Single(s => s.Method == method).Payload!;

    /// <summary>SignalR payloads are anonymous types, so they can only be read reflectively.</summary>
    private static object? Field(object payload, string name) =>
        payload.GetType().GetProperty(name)!.GetValue(payload);

    // ─── The request path must only enqueue, never run the work inline ───

    [Fact]
    public void StartAnalysisInBackground_Enqueues_DurableJob_With_Offer_And_User()
    {
        CreateService().StartAnalysisInBackground(UserId, OfferId);

        var job = SingleEnqueuedJob();
        job.Type.Should().Be<OfferAnalysisJob>();
        job.Method.Name.Should().Be(nameof(OfferAnalysisJob.ExecuteAsync));
        job.Args.Should().Equal(UserId, OfferId);
    }

    [Fact]
    public void StartGenerationInBackground_Enqueues_DurableJob_With_Template()
    {
        CreateService().StartGenerationInBackground(UserId, OfferId, 7);

        var job = SingleEnqueuedJob();
        job.Type.Should().Be<OfferGenerationJob>();
        job.Method.Name.Should().Be(nameof(OfferGenerationJob.ExecuteAsync));
        job.Args.Should().Equal(OfferId, UserId.ToString(), 7);
    }

    [Fact]
    public void Enqueued_Job_Carries_No_CancellationToken()
    {
        // Hangfire serializes every argument of the job, so a CancellationToken would be stored
        // as a plain value instead of the shutdown token it looks like. The long-running work
        // relies on its own timeout rather than pretending to be cancellable.
        var service = CreateService();

        service.StartAnalysisInBackground(UserId, OfferId);
        service.StartGenerationInBackground(UserId, OfferId, 7);

        _backgroundJobs.Enqueued.Should().HaveCount(2);
        _backgroundJobs.Enqueued.Should().OnlyContain(j =>
            j.Args.All(a => !typeof(CancellationToken).IsInstanceOfType(a))
            && j.Method.GetParameters().All(p => p.ParameterType != typeof(CancellationToken)));
    }

    [Fact]
    public void Start_In_Background_Does_Not_Run_The_Analysis_Inline()
    {
        // A Task.Run implementation would touch the agent client on this thread; enqueueing must
        // only hand the work over.
        CreateService().StartAnalysisInBackground(UserId, OfferId);

        _agents.VerifyNoOtherCalls();
        _offerService.VerifyNoOtherCalls();
    }

    // ─── The job keeps the client contract it had as a Task.Run ───

    [Fact]
    public async Task AnalysisJob_Notifies_Progress_Then_Completion()
    {
        var analysis = new Mock<IOfferAnalysisService>();
        analysis.Setup(a => a.AnalyzeAsync(UserId, OfferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfferAnalysisDto { OfferId = OfferId, ScoreMatching = 65, RunId = "run-1" });

        var (proxy, hub) = CreateHub();
        var job = new OfferAnalysisJob(analysis.Object, hub, NullLogger<OfferAnalysisJob>.Instance);

        await job.ExecuteAsync(UserId, OfferId);

        analysis.Verify(a => a.AnalyzeAsync(UserId, OfferId, It.IsAny<CancellationToken>()), Times.Once);

        var progress = PayloadSentTo(proxy, "PipelineProgress");
        var completed = PayloadSentTo(proxy, "PipelineCompleted");

        Field(completed, "status").Should().Be("completed");
        Field(completed, "result").Should().BeOfType<OfferAnalysisDto>().Which.RunId.Should().Be("run-1");

        Field(progress, "step").Should().Be("analysis");
        Field(progress, "progressPercent").Should().Be(100);
    }

    [Fact]
    public async Task AnalysisJob_Notifies_Client_And_Rethrows_So_Hangfire_Records_The_Failure()
    {
        var analysis = new Mock<IOfferAnalysisService>();
        analysis.Setup(a => a.AnalyzeAsync(UserId, OfferId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("agent 500"));

        var (proxy, hub) = CreateHub();
        var job = new OfferAnalysisJob(analysis.Object, hub, NullLogger<OfferAnalysisJob>.Instance);

        var act = () => job.ExecuteAsync(UserId, OfferId);

        // Swallowing the error would make the dashboard report a successful run that produced
        // nothing, and the browser would poll until it times out.
        await act.Should().ThrowAsync<InvalidOperationException>();

        Field(PayloadSentTo(proxy, "PipelineCompleted"), "status").Should().Be("error");
    }

    [Fact]
    public async Task AnalysisJob_Keeps_The_Original_Failure_When_Notifying_Itself_Fails()
    {
        var analysis = new Mock<IOfferAnalysisService>();
        analysis.Setup(a => a.AnalyzeAsync(UserId, OfferId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("agent 500"));

        var (proxy, hub) = CreateHub();
        proxy.Fail = true; // no connected client

        var job = new OfferAnalysisJob(analysis.Object, hub, NullLogger<OfferAnalysisJob>.Instance);

        var act = () => job.ExecuteAsync(UserId, OfferId);

        // The original analysis failure must survive a broken notification channel.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("agent 500");
    }

    [Fact]
    public async Task GenerationJob_Delegates_To_The_Runner()
    {
        var runner = new Mock<IPipelineRunnerService>();
        runner.Setup(r => r.StartGenerationAsync(OfferId, UserId.ToString(), 7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await new OfferGenerationJob(runner.Object).ExecuteAsync(OfferId, UserId.ToString(), 7);

        runner.Verify(
            r => r.StartGenerationAsync(OfferId, UserId.ToString(), 7, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// <c>IClientProxy.SendAsync(method, arg, CancellationToken)</c> is a SignalR extension
    /// method, which Moq cannot stub, so the sends are recorded by implementing the interface.
    /// Every overload funnels into the same recorder, so the assertions do not depend on which
    /// overload the extension happens to call.
    /// </summary>
    private sealed class RecordingClientProxy : IClientProxy
    {
        public List<(string Method, object? Payload)> Sent { get; } = new();

        public bool Fail { get; set; }

        public Task SendCoreAsync(string method, object?[] args) => Record(method, args.FirstOrDefault());

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken)
            => Record(method, args.FirstOrDefault());

        public Task SendAsync(string method, object? arg1) => Record(method, arg1);

        public Task SendAsync(string method, object? arg1, object? arg2) => Record(method, arg1);

        public Task SendAsync(string method, object? arg1, object? arg2, object? arg3) => Record(method, arg1);

        public Task SendAsync(string method, object?[] args) => Record(method, args.FirstOrDefault());

        private Task Record(string method, object? payload)
        {
            if (Fail)
                throw new InvalidOperationException("no connected client");

            Sent.Add((method, payload));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Moq refuses to set up Hangfire's generic <c>Enqueue&lt;T&gt;</c> overloads, and the async
    /// overload is only an extension method that ends up calling <see cref="Create"/>. Recording
    /// there means the assertions read the very job Hangfire persists. The unused entry points
    /// fail loudly so an accidental change of path cannot pass unnoticed.
    /// </summary>
    private sealed class RecordingJobClient : IBackgroundJobClient
    {
        public List<Job> Enqueued { get; } = new();

        public string Create(Job job, IState state)
        {
            Enqueued.Add(job);
            return "recorded-job";
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => throw new NotSupportedException();

        public string Enqueue(Expression<Action> methodCall) => throw new NotSupportedException();

        public string Enqueue<T>(Expression<Action<T>> methodCall) => throw new NotSupportedException();

        public string Enqueue(string jobId, Expression<Action> methodCall) => throw new NotSupportedException();

        public string Enqueue<T>(string jobId, Expression<Action<T>> methodCall) => throw new NotSupportedException();
    }
}
