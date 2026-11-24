using AgentAuthorization.Core.Application.Auditing;
using AgentAuthorization.Core.Application.Authorization;
using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Application.Resources;
using AgentAuthorization.Core.Domain.Authorization;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using AgentAuthorization.Infrastructure.Grants;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Application.Authorization;

public sealed class PolicyEnforcementPointTests
{
    private static readonly Guid GrantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid ParentId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid AgentId =  Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid ParentAgentId = Guid.Parse("00000000-0000-0000-0000-000000000102");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    private static readonly Guid ProjectId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000401");

    private static readonly DateTimeOffset Start = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CurrentTime = Start.AddMinutes(30);

    private readonly InMemoryGrantStore _store = new();
    private readonly RecordingExecutor _executor = new();
    private readonly AuthorizationService _authorizationService;
    private readonly PolicyEnforcementPoint _pep;
    private readonly RecordingAuditSink _auditSink = new();

    public PolicyEnforcementPointTests()
    {
        var catalog = new ResourceCatalog(
            ImmutableDictionary<Guid, Guid>.Empty
                .Add(ResourceId, ProjectId));

        var evaluator = new ResourceScopeEvaluator(catalog);
        var engine = new AuthorizationEngine(evaluator);
        var validator = new GrantDelegationValidator(evaluator);

        _authorizationService = new AuthorizationService(_store, engine, validator);

        _pep = new PolicyEnforcementPoint(_authorizationService, _executor, _auditSink);
    }

    [Fact]
    public void Constructor_WithNullAuthorizationService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new PolicyEnforcementPoint(null!, _executor, _auditSink));
    }

    [Fact]
    public void Constructor_WithNullExecutor_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new PolicyEnforcementPoint(_authorizationService, null!, _auditSink));
    }

    [Fact]
    public void Execute_WithNullRequest_ShouldThrowWithoutExecution()
    {
        //Act
        Assert.Throws<ArgumentNullException>(() => _pep.Execute(null!, CurrentTime));

        //Assert
        Assert.Empty(_executor.Requests);
    }

    [Theory]
    [InlineData(ResourceAction.Read)]
    [InlineData(ResourceAction.Write)]
    [InlineData(ResourceAction.Delete)]
    public void Execute_WithAllowedRequest_ShouldExecuteExactRequestOnce(ResourceAction action)
    {
        //Arrange
        _store.AddGrant(CreateGrant(GrantId, AgentId, action: action));

        var request = CreateRequest(action);

        //Act
        var decision = _pep.Execute(request, CurrentTime);

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
        Assert.Same(request, Assert.Single(_executor.Requests));
    }

    [Fact]
    public void Execute_WithoutGrant_ShouldDenyWithoutExecution()
    {
        //Act
        var decision = _pep.Execute(CreateRequest(), CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
        Assert.Empty(_executor.Requests);
    }

    [Fact]
    public void Execute_WithDisallowedAction_ShouldDenyWithoutExecution()
    {
        //Arrange
        _store.AddGrant(CreateGrant(GrantId, AgentId));
        var request = CreateRequest(ResourceAction.Delete);

        //Act
        var decision = _pep.Execute(request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Empty(_executor.Requests);
    }

    [Fact]
    public void Execute_AfterGrantRevocation_ShouldNotExecuteAgain()
    {
        //Arrange
        _store.AddGrant(CreateGrant(GrantId, AgentId));
        var request = CreateRequest();

        Assert.True(_pep.Execute(request, CurrentTime).IsAllowed);
        Assert.Single(_executor.Requests);

        Assert.True(_store.RevokeGrant(GrantId));

        //Act
        var decision = _pep.Execute(request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Single(_executor.Requests);
    }

    [Fact]
    public void Execute_AfterParentRevocation_ShouldNotExecuteAgain()
    {
        //Arrange
        _store.AddGrant(CreateGrant(
            ParentId,
            ParentAgentId,
            canDelegate: true));

        _store.AddGrant(CreateGrant(
            GrantId,
            AgentId,
            parentGrantId: ParentId));

        var request = CreateRequest();

        Assert.True(_pep.Execute(request, CurrentTime).IsAllowed);
        Assert.Single(_executor.Requests);

        Assert.True(_store.RevokeGrant(ParentId));

        //Act
        var decision = _pep.Execute(request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Single(_executor.Requests);
    }

    [Fact]
    public void Execute_AtExpiration_ShouldDenyWithoutExecution()
    {
        //Arrange
        _store.AddGrant(CreateGrant(GrantId, AgentId));

        // Act
        var decision = _pep.Execute(CreateRequest(), Start.AddHours(1));

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Empty(_executor.Requests);
    }

    [Fact]
    public void Execute_WhenExecutorThrows_ShouldPropagateException()
    {
        //Arrange
        _store.AddGrant(CreateGrant(GrantId, AgentId));

        var expectedException = new InvalidOperationException("Simulated execution failure.");

        _executor.Failure = expectedException;
        var request = CreateRequest();

        //Act
        var actualException = Assert.Throws<InvalidOperationException>(() => _pep.Execute(request, CurrentTime));

        //Assert
        Assert.Same(expectedException, actualException);
        Assert.Same(request, Assert.Single(_executor.Requests));
    }

    private static AuthorizationRequest CreateRequest(ResourceAction action = ResourceAction.Read)
    {
        return new AuthorizationRequest(
            AgentId,
            action,
            ResourceId,
            WorkflowId);
    }

    private static Grant CreateGrant(
        Guid grantId,
        Guid agentId,
        ResourceAction action = ResourceAction.Read,
        Guid? parentGrantId = null,
        bool canDelegate = false)
    {
        return new Grant(
            grantId,
            agentId,
            WorkflowId,
            ImmutableHashSet.Create(action),
            new ResourceSetScope(
                ImmutableHashSet.Create(ResourceId)),
            Start,
            Start.AddHours(1),
            parentGrantId: parentGrantId,
            canDelegate: canDelegate);
    }

    private sealed class RecordingExecutor : IResourceOperationExecutor
    {
        public List<AuthorizationRequest> Requests { get; } = new();

        public Exception? Failure { get; set; }

        public void Execute(AuthorizationRequest request)
        {
            Requests.Add(request);

            if (Failure is not null)
            {
                throw Failure;
            }
        }
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
