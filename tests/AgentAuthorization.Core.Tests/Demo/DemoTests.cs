using AgentAuthorization.Core.Application.Auditing;
using AgentAuthorization.Core.Application.Authorization;
using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Authorization;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using AgentAuthorization.Demo;
using AgentAuthorization.Infrastructure.Grants;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Demo;

public sealed class DemoTests
{
    private static readonly Guid GrantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid AgentId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    private static readonly Guid OtherId = Guid.Parse("00000000-0000-0000-0000-000000000999");

    private static readonly DateTimeOffset Start = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Executor_WithNullInitialVersions_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new DemoResourceOperationExecutor(null!));
    }

    [Fact]
    public void Executor_WithNullRequest_ShouldNotChangeState()
    {
        //Arrange
        var executor = CreateExecutor();

        //Act
        Assert.Throws<ArgumentNullException>(() => executor.Execute(null!));

        //Assert
        Assert.Equal(4, executor.GetVersion(ResourceId));
        Assert.Equal(0, executor.ExecutionCount);
    }

    [Fact]
    public void Executor_ReadWriteDelete_ShouldApplyExpectedStateChanges()
    {
        //Arrange
        var executor = CreateExecutor();

        //Act: read
        executor.Execute(CreateRequest(ResourceAction.Read));

        //Assert
        Assert.Equal(4, executor.GetVersion(ResourceId));
        Assert.Equal(1, executor.ExecutionCount);

        //Act: write
        executor.Execute(CreateRequest(ResourceAction.Write));

        //Assert
        Assert.Equal(5, executor.GetVersion(ResourceId));
        Assert.Equal(2, executor.ExecutionCount);

        //Act: delete
        executor.Execute(CreateRequest(ResourceAction.Delete));

        //Assert
        Assert.Null(executor.GetVersion(ResourceId));
        Assert.Equal(3, executor.ExecutionCount);
    }

    [Theory]
    [InlineData(ResourceAction.Read)]
    [InlineData(ResourceAction.Write)]
    [InlineData(ResourceAction.Delete)]
    public void Executor_WithMissingResource_ShouldNotChangeState(ResourceAction action)
    {
        //Arrange
        var executor = CreateExecutor();
        var request = new AuthorizationRequest(AgentId, action, OtherId, WorkflowId);

        //Act
        Assert.Throws<KeyNotFoundException>(() => executor.Execute(request));

        //Assert
        Assert.Null(executor.GetVersion(OtherId));
        Assert.Equal(4, executor.GetVersion(ResourceId));
        Assert.Equal(0, executor.ExecutionCount);
    }

    [Fact]
    public void Executor_WithUndefinedAction_ShouldNotChangeState()
    {
        //Arrange
        var executor = CreateExecutor();
        var request = CreateRequest((ResourceAction)999);

        //Act
        Assert.Throws<ArgumentOutOfRangeException>(() => executor.Execute(request));

        //Assert
        Assert.Equal(4, executor.GetVersion(ResourceId));
        Assert.Equal(0, executor.ExecutionCount);
    }

    [Fact]
    public void Agent_ShouldUseConfiguredIdentityWorkflowAndClock()
    {
        //Arrange
        var executor = CreateExecutor();
        var (pep, audit) = CreatePep(executor);
        var now = Start.AddMinutes(15);
        var clockCalls = 0;

        var agent = new DemoAgent(
            AgentId,
            WorkflowId,
            pep,
            () =>
            {
                clockCalls++;
                return now;
            });

        //Act
        var decision = agent.Execute(ResourceAction.Write, ResourceId);

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(5, executor.GetVersion(ResourceId));
        Assert.Equal(1, executor.ExecutionCount);
        Assert.Equal(1, clockCalls);

        var entry = Assert.Single(audit.Entries);

        Assert.Equal(now, entry.Timestamp);
        Assert.Equal(AgentId, entry.Request.AgentId);
        Assert.Equal(WorkflowId, entry.Request.WorkflowId);
        Assert.Equal(ResourceId, entry.Request.ResourceId);
        Assert.Equal(ResourceAction.Write, entry.Request.Action);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Agent_WithDifferentIdentityOrWorkflow_ShouldNotExecute(bool changeAgent)
    {
        //Arrange
        var executor = CreateExecutor();
        var (pep, audit) = CreatePep(executor);

        var agent = new DemoAgent(
            changeAgent ? OtherId : AgentId,
            changeAgent ? WorkflowId : OtherId,
            pep,
            () => Start);

        //Act
        var decision = agent.Execute(ResourceAction.Write, ResourceId);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(4, executor.GetVersion(ResourceId));
        Assert.Equal(0, executor.ExecutionCount);
        Assert.False(Assert.Single(audit.Entries).Decision.IsAllowed);
    }

    [Fact]
    public void Agent_ShouldReadClockAgainForEachRequest()
    {
        //Arrange
        var executor = CreateExecutor();
        var (pep, audit) = CreatePep(executor);
        var now = Start;

        var agent = new DemoAgent(
            AgentId,
            WorkflowId,
            pep,
            () => now);

        //Act
        var firstDecision = agent.Execute(ResourceAction.Write, ResourceId);

        now = Start.AddHours(1);

        var secondDecision = agent.Execute(ResourceAction.Write, ResourceId);

        //Assert
        Assert.True(firstDecision.IsAllowed);
        Assert.False(secondDecision.IsAllowed);

        Assert.Equal(5, executor.GetVersion(ResourceId));
        Assert.Equal(1, executor.ExecutionCount);

        Assert.Equal(2, audit.Entries.Count);
        Assert.Equal(Start, audit.Entries[0].Timestamp);
        Assert.Equal(now, audit.Entries[1].Timestamp);
    }

    private static DemoResourceOperationExecutor CreateExecutor()
    {
        return new DemoResourceOperationExecutor(ImmutableDictionary<Guid, int>.Empty.Add(ResourceId, 4));
    }

    private static AuthorizationRequest CreateRequest(ResourceAction action)
    {
        return new AuthorizationRequest(AgentId, action, ResourceId, WorkflowId);
    }

    private static (PolicyEnforcementPoint Pep,RecordingAuditSink Audit) CreatePep(DemoResourceOperationExecutor executor)
    {
        var store = new InMemoryGrantStore();

        store.AddGrant(new Grant(
            GrantId,
            AgentId,
            WorkflowId,
            ImmutableHashSet.Create(
                ResourceAction.Read,
                ResourceAction.Write),
            new ResourceSetScope(
                ImmutableHashSet.Create(ResourceId)),
            Start,
            Start.AddHours(1)));

        var evaluator = new ResourceScopeEvaluator(
            new ResourceCatalog(ImmutableDictionary<Guid, Guid>.Empty));

        var service = new AuthorizationService(
            store,
            new AuthorizationEngine(evaluator),
            new GrantDelegationValidator(evaluator));

        var audit = new RecordingAuditSink();

        return (new PolicyEnforcementPoint(service, executor, audit), audit);
    }

    private sealed class RecordingAuditSink : IAuthorizationAuditSink
    {
        public List<AuthorizationAuditEntry> Entries { get; } = new();

        public void Write(AuthorizationAuditEntry entry)
        {
            Entries.Add(entry);
        }
    }
}
