using AgentAuthorization.Core.Application.Authorization;
using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Authorization;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using AgentAuthorization.Infrastructure.Grants;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Application.Authorization;

public sealed class AuthorizationServiceDelegationTests
{
    private static readonly Guid ParentId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid ChildId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid AlternativeId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid RecipientId = Guid.Parse("00000000-0000-0000-0000-000000000102");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    private static readonly Guid ProjectId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000401");

    private static readonly Guid OtherId = Guid.Parse("00000000-0000-0000-0000-000000000999");

    private static readonly DateTimeOffset Start = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CurrentTime = Start.AddMinutes(30);

    private readonly InMemoryGrantStore _store = new();
    private readonly AuthorizationEngine _engine;
    private readonly AuthorizationService _service;
    private readonly GrantDelegationService _delegationService;

    public AuthorizationServiceDelegationTests()
    {
        var catalog = new ResourceCatalog(
            ImmutableDictionary<Guid, Guid>.Empty
                .Add(ResourceId, ProjectId)
                .Add(OtherId, OtherId));

        var evaluator = new ResourceScopeEvaluator(catalog);
        var validator = new GrantDelegationValidator(evaluator);

        _engine = new AuthorizationEngine(evaluator);
        _service = new AuthorizationService(_store, _engine, validator);

        _delegationService = new GrantDelegationService(_store, validator);
    }

    [Fact]
    public void Authorize_WithValidDelegation_ShouldAllowRecipient()
    {
        //Arrange
        _store.AddGrant(CreateParent());

        var child = CreateChild();
        Assert.True(_delegationService.TryDelegateGrant(child, OwnerId, CurrentTime));

        //Act
        var decision = _service.Authorize(CreateRequest(), CurrentTime);

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
    }

    [Fact]
    public void Authorize_WithMissingParent_ShouldDeny()
    {
        //Arrange
        _store.AddGrant(CreateChild());

        //Act
        var decision = _service.Authorize(CreateRequest(), CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
    }

    [Fact]
    public void Authorize_AfterParentRevocation_ShouldDenyExistingChild()
    {
        //Arrange
        _store.AddGrant(CreateParent());
        var child = CreateChild();

        Assert.True(_delegationService.TryDelegateGrant(child, OwnerId, CurrentTime));

        Assert.True(_service.Authorize(CreateRequest(), CurrentTime).IsAllowed);

        Assert.True(_store.RevokeGrant(ParentId));

        //Act
        var decision = _service.Authorize(CreateRequest(), CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);

        Assert.False(child.IsRevoked);
        Assert.Same(child, _store.GetById(ChildId));
    }

    [Fact]
    public void Authorize_AfterChildRevocation_ShouldKeepParentUsable()
    {
        //Arrange
        var parent = CreateParent();
        _store.AddGrant(parent);

        Assert.True(_delegationService.TryDelegateGrant(CreateChild(), OwnerId, CurrentTime));

        Assert.True(_store.RevokeGrant(ChildId));

        var parentRequest = new AuthorizationRequest(
            OwnerId,
            ResourceAction.Read,
            ResourceId,
            WorkflowId);

        //Act
        var childDecision = _service.Authorize(CreateRequest(), CurrentTime);

        var parentDecision = _service.Authorize(parentRequest, CurrentTime);

        //Assert
        Assert.False(childDecision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, childDecision.Reason);

        Assert.True(parentDecision.IsAllowed);
        Assert.False(parent.IsRevoked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Authorize_WithInvalidDelegationAndValidAlternative_ShouldAllow(bool alternativeFirst)
    {
        //Arrange
        var parent = CreateParent();
        parent.Revoke();
        _store.AddGrant(parent);

        var child = CreateChild();

        var alternative = new Grant(
            AlternativeId,
            RecipientId,
            WorkflowId,
            ImmutableHashSet.Create(ResourceAction.Read),
            new ResourceSetScope(ImmutableHashSet.Create(ResourceId)),
            Start,
            Start.AddHours(1));

        if (alternativeFirst)
        {
            _store.AddGrant(alternative);
            _store.AddGrant(child);
        }
        else
        {
            _store.AddGrant(child);
            _store.AddGrant(alternative);
        }

        //Act
        var decision = _service.Authorize(CreateRequest(), CurrentTime);

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
    }

    [Theory]
    [InlineData("DelegationDisabled")]
    [InlineData("ParentIsDelegated")]
    [InlineData("DifferentWorkflow")]
    [InlineData("AdditionalAction")]
    [InlineData("AdditionalResource")]
    [InlineData("ChildStartsTooEarly")]
    [InlineData("ChildExpiresTooLate")]
    public void Authorize_WithInvalidStoredDelegation_ShouldDeny(string scenario)
    {
        //Arrange
        var parent = CreateParent();
        var child = CreateChild();

        switch (scenario)
        {
            case "DelegationDisabled":
                parent = CreateParent(canDelegate: false);
                break;

            case "ParentIsDelegated":
                parent = CreateParent(
                    canDelegate: false,
                    parentGrantId: OtherId);
                break;

            case "DifferentWorkflow":
                parent = CreateParent(workflowId: OtherId);
                break;

            case "AdditionalAction":
                child = CreateChild(
                    actions: ImmutableHashSet.Create(
                        ResourceAction.Read,
                        ResourceAction.Delete));
                break;

            case "AdditionalResource":
                child = CreateChild(
                    scope: new ResourceSetScope(ImmutableHashSet.Create(ResourceId, OtherId)));
                break;

            case "ChildStartsTooEarly":
                child = CreateChild(validFrom: Start.AddMinutes(-1));
                break;

            case "ChildExpiresTooLate":
                child = CreateChild(expiresAt: Start.AddMinutes(61));
                break;

            default:
                throw new ArgumentException("Unknown test scenario.", nameof(scenario));
        }

        _store.AddGrant(parent);
        _store.AddGrant(child);

        //Act
        var decision = _service.Authorize(CreateRequest(), CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
    }

    private static AuthorizationRequest CreateRequest()
    {
        return new AuthorizationRequest(
            RecipientId,
            ResourceAction.Read,
            ResourceId,
            WorkflowId);
    }

    private static Grant CreateParent(
        bool canDelegate = true,
        Guid? parentGrantId = null,
        Guid? workflowId = null)
    {
        return new Grant(
            ParentId,
            OwnerId,
            workflowId ?? WorkflowId,
            ImmutableHashSet.Create(
                ResourceAction.Read,
                ResourceAction.Write),
            new ProjectScope(ProjectId),
            Start,
            Start.AddHours(1),
            parentGrantId: parentGrantId,
            canDelegate: canDelegate);
    }

    private static Grant CreateChild(
        ImmutableHashSet<ResourceAction>? actions = null,
        ResourceScope? scope = null,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? expiresAt = null)
    {
        return new Grant(
            ChildId,
            RecipientId,
            WorkflowId,
            actions ?? ImmutableHashSet.Create(ResourceAction.Read),
            scope ?? new ResourceSetScope(
                ImmutableHashSet.Create(ResourceId)),
            validFrom ?? Start,
            expiresAt ?? Start.AddHours(1),
            parentGrantId: ParentId);
    }
}
