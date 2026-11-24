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

public sealed class PolicyEnforcementPointAuditTests
{
    private static readonly Guid GrantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid AgentId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    private static readonly DateTimeOffset CurrentTime = new(2026, 11, 22, 10, 30, 0, TimeSpan.Zero);

    private readonly List<string> _events = new();
    private readonly InMemoryGrantStore _store = new();
    private readonly RecordingAuditSink _auditSink;
    private readonly RecordingExecutor _executor;
    private readonly AuthorizationService _authorizationService;
    private readonly PolicyEnforcementPoint _pep;

    public PolicyEnforcementPointAuditTests()
    {
        var evaluator = new ResourceScopeEvaluator(new ResourceCatalog(ImmutableDictionary<Guid, Guid>.Empty));

        _authorizationService = new AuthorizationService(
            _store,
            new AuthorizationEngine(evaluator),
            new GrantDelegationValidator(evaluator));

        _auditSink = new RecordingAuditSink(_events);
        _executor = new RecordingExecutor(_events);

        _pep = new PolicyEnforcementPoint(
            _authorizationService,
            _executor,
            _auditSink);
    }

    [Fact]
    public void Constructor_WithNullAuditSink_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyEnforcementPoint(
                _authorizationService,
                _executor,
                null!));
    }

    [Fact]
    public void Execute_WithAllowedRequest_ShouldAuditBeforeExecution()
    {
        //Arrange
        AddGrant();
        var request = CreateRequest();

        //Act
        var decision = _pep.Execute(request, CurrentTime);

        //Assert
        var entry = Assert.Single(_auditSink.Entries);

        Assert.Equal(CurrentTime, entry.Timestamp);
        Assert.Same(request, entry.Request);
        Assert.Equal(decision, entry.Decision);
        Assert.True(entry.Decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, entry.Decision.Reason);

        Assert.Equal(new[] { "audit", "execute" }, _events);
    }

    [Fact]
    public void Execute_WithDeniedRequest_ShouldAuditWithoutExecution()
    {
        //Arrange
        var request = CreateRequest();

        //Act
        var decision = _pep.Execute(request, CurrentTime);

        //Assert
        var entry = Assert.Single(_auditSink.Entries);

        Assert.Equal(CurrentTime, entry.Timestamp);
        Assert.Same(request, entry.Request);
        Assert.Equal(decision, entry.Decision);
        Assert.False(entry.Decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, entry.Decision.Reason);

        Assert.Equal(new[] { "audit" }, _events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execute_WhenAuditFails_ShouldThrowWithoutExecution(bool hasMatchingGrant)
    {
        //Arrange
        if (hasMatchingGrant)
        {
            AddGrant();
        }

        var expectedException = new IOException("Simulated audit failure.");

        _auditSink.Failure = expectedException;

        //Act
        var actualException = Assert.Throws<IOException>(() => _pep.Execute(CreateRequest(), CurrentTime));

        //Assert
        Assert.Same(expectedException, actualException);
        Assert.Empty(_auditSink.Entries);
        Assert.Equal(new[] { "audit" }, _events);
    }

    [Fact]
    public void Execute_WhenExecutorFails_ShouldKeepAuthorizationAudit()
    {
        //Arrange
        AddGrant();

        var expectedException = new InvalidOperationException("Simulated execution failure.");

        _executor.Failure = expectedException;

        //Act
        var actualException = Assert.Throws<InvalidOperationException>(() => _pep.Execute(CreateRequest(), CurrentTime));

        //Assert
        Assert.Same(expectedException, actualException);

        var entry = Assert.Single(_auditSink.Entries);
        Assert.True(entry.Decision.IsAllowed);

        Assert.Equal(new[] { "audit", "execute" }, _events);
    }

    [Fact]
    public void Execute_RepeatedRequest_ShouldAuditEveryCall()
    {
        //Arrange
        AddGrant();
        var request = CreateRequest();

        //Act
        _pep.Execute(request, CurrentTime);
        _pep.Execute(request, CurrentTime.AddMinutes(1));

        //Assert
        Assert.Equal(2, _auditSink.Entries.Count);
        Assert.Equal(CurrentTime, _auditSink.Entries[0].Timestamp);
        Assert.Equal(CurrentTime.AddMinutes(1), _auditSink.Entries[1].Timestamp);

        Assert.Equal(new[] { "audit", "execute", "audit", "execute" }, _events);
    }

    [Fact]
    public void Execute_WithNullRequest_ShouldNotAuditOrExecute()
    {
        //Act
        Assert.Throws<ArgumentNullException>(() => _pep.Execute(null!, CurrentTime));

        //Assert
        Assert.Empty(_auditSink.Entries);
        Assert.Empty(_events);
    }

    private void AddGrant()
    {
        _store.AddGrant(new Grant(
            GrantId,
            AgentId,
            WorkflowId,
            ImmutableHashSet.Create(ResourceAction.Read),
            new ResourceSetScope(
                ImmutableHashSet.Create(ResourceId)),
            CurrentTime.AddHours(-1),
            CurrentTime.AddHours(1)));
    }

    private static AuthorizationRequest CreateRequest()
    {
        return new AuthorizationRequest(
            AgentId,
            ResourceAction.Read,
            ResourceId,
            WorkflowId);
    }

    private sealed class RecordingAuditSink : IAuthorizationAuditSink
    {
        private readonly List<string> _events;

        public List<AuthorizationAuditEntry> Entries { get; } = new();
        public Exception? Failure { get; set; }

        public RecordingAuditSink(List<string> events)
        {
            _events = events;
        }

        public void Write(AuthorizationAuditEntry entry)
        {
            _events.Add("audit");

            if (Failure is not null)
            {
                throw Failure;
            }

            Entries.Add(entry);
        }
    }

    private sealed class RecordingExecutor : IResourceOperationExecutor
    {
        private readonly List<string> _events;

        public Exception? Failure { get; set; }

        public RecordingExecutor(List<string> events)
        {
            _events = events;
        }

        public void Execute(AuthorizationRequest request)
        {
            _events.Add("execute");

            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }
}
