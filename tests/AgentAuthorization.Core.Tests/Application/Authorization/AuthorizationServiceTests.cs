using AgentAuthorization.Core.Application.Authorization;
using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Authorization;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using AgentAuthorization.Infrastructure.Grants;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Application.Authorization;

public sealed class AuthorizationServiceTests
{
    private static readonly Guid GrantA = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid GrantB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid AgentId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid OtherAgentId = Guid.Parse("00000000-0000-0000-0000-000000000102");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    private static readonly Guid OtherWorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000202");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    private static readonly Guid OtherResourceId = Guid.Parse("00000000-0000-0000-0000-000000000302");

    private static readonly Guid ProjectId = Guid.Parse("00000000-0000-0000-0000-000000000401");

    private static readonly DateTimeOffset ValidFrom = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CurrentTime = ValidFrom.AddMinutes(30);

    private readonly InMemoryGrantStore _grantStore = new();
    private readonly AuthorizationEngine _engine;
    private readonly AuthorizationService _service;
    private readonly GrantDelegationValidator _validator;

    public AuthorizationServiceTests()
    {
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty.Add(ResourceId, ProjectId);

        var catalog = new ResourceCatalog(resourceProjects);
        var evaluator = new ResourceScopeEvaluator(catalog);

        _engine = new AuthorizationEngine(evaluator);
        _validator = new GrantDelegationValidator(evaluator);
        _service = new AuthorizationService(_grantStore, _engine, _validator);
    }

    [Fact]
    public void Constructor_WithNullGrantStore_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthorizationService(null!, _engine, _validator));
    }

    [Fact]
    public void Constructor_WithNullAuthorizationEngine_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthorizationService(_grantStore, null!, _validator));
    }

    [Fact]
    public void Constructor_WithNullDelegationValidator_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthorizationService(_grantStore, _engine, null!));
    }

    [Fact]
    public void Authorize_WithNullRequest_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Authorize(null!, CurrentTime));
    }

    [Fact]
    public void Authorize_WhenStoreIsEmpty_ShouldDeny()
    {
        //Arrange
        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
    }

    [Fact]
    public void Authorize_WithMatchingGrant_ShouldAllow()
    {
        //Arrange
        _grantStore.AddGrant(CreateGrant(GrantA));
        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
    }

    [Fact]
    public void Authorize_WithMatchingProjectGrant_ShouldAllow()
    {
        //Arrange
        var grant = CreateGrant(GrantA, scope: new ProjectScope(ProjectId));

        _grantStore.AddGrant(grant);
        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenGrantBelongsToOtherAgent_ShouldDeny()
    {
        //Arrange
        _grantStore.AddGrant(CreateGrant(GrantA, agentId: OtherAgentId));

        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenGrantBelongsToOtherWorkflow_ShouldDeny()
    {
        //Arrange
        _grantStore.AddGrant(CreateGrant(GrantA, workflowId: OtherWorkflowId));

        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Authorize_WithMatchingAndNonMatchingGrants_ShouldAllow(bool addMatchingGrantFirst)
    {
        //Arrange
        var matchingGrant = CreateGrant(GrantA);
        var nonMatchingGrant = CreateGrant(GrantB, action: ResourceAction.Write);

        if (addMatchingGrantFirst)
        {
            _grantStore.AddGrant(matchingGrant);
            _grantStore.AddGrant(nonMatchingGrant);
        }
        else
        {
            _grantStore.AddGrant(nonMatchingGrant);
            _grantStore.AddGrant(matchingGrant);
        }

        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenActionAndResourceAreSplitAcrossGrants_ShouldDeny()
    {
        //Arrange
        var readOtherResource = CreateGrant(
            GrantA, action: ResourceAction.Read, scope: new ResourceSetScope(ImmutableHashSet.Create(OtherResourceId)));

        var writeRequestedResource = CreateGrant(GrantB, action: ResourceAction.Write);

        _grantStore.AddGrant(readOtherResource);
        _grantStore.AddGrant(writeRequestedResource);

        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenActionAndWorkflowAreSplitAcrossGrants_ShouldDeny()
    {
        //Arrange
        var readInOtherWorkflow = CreateGrant(GrantA, workflowId: OtherWorkflowId);

        var writeInRequestedWorkflow = CreateGrant(GrantB, action: ResourceAction.Write);

        _grantStore.AddGrant(readInOtherWorkflow);
        _grantStore.AddGrant(writeInRequestedWorkflow);

        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(TimeSpan.TicksPerHour)]
    [InlineData(TimeSpan.TicksPerHour + 1)]
    public void Authorize_WhenGrantIsOutsideValidityWindow_ShouldDeny(long ticksFromValidFrom)
    {
        //Arrange
        _grantStore.AddGrant(CreateGrant(GrantA));

        var request = CreateRequest();
        var currentTime = ValidFrom.AddTicks(ticksFromValidFrom);

        //Act
        var decision = _service.Authorize(request, currentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
    }

    [Fact]
    public void Authorize_AfterMatchingGrantIsAdded_ShouldUseUpdatedStore()
    {
        //Arrange
        var request = CreateRequest();

        var beforeAddingGrant = _service.Authorize(request, CurrentTime);

        //Act
        _grantStore.AddGrant(CreateGrant(GrantA));

        var afterAddingGrant = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.False(beforeAddingGrant.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, beforeAddingGrant.Reason);
        Assert.True(afterAddingGrant.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, afterAddingGrant.Reason);
    }

    [Fact]
    public void Authorize_AfterOnlyMatchingGrantIsRevoked_ShouldDeny()
    {
        //Arrange
        _grantStore.AddGrant(CreateGrant(GrantA));
        var request = CreateRequest();

        var beforeRevocation = _service.Authorize(request, CurrentTime);

        //Act
        var revoked = _grantStore.RevokeGrant(GrantA);

        var afterRevocation = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.True(revoked);
        Assert.True(beforeRevocation.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, beforeRevocation.Reason);

        Assert.False(afterRevocation.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, afterRevocation.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Authorize_WhenAnotherMatchingGrantIsActive_ShouldAllow(bool addRevokedGrantFirst)
    {
        //Arrange
        var grantToRevoke = CreateGrant(GrantA);
        var activeGrant = CreateGrant(GrantB);

        if (addRevokedGrantFirst)
        {
            _grantStore.AddGrant(grantToRevoke);
            _grantStore.AddGrant(activeGrant);
        }
        else
        {
            _grantStore.AddGrant(activeGrant);
            _grantStore.AddGrant(grantToRevoke);
        }

        _grantStore.RevokeGrant(GrantA);
        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.True(grantToRevoke.IsRevoked);
        Assert.False(activeGrant.IsRevoked);
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenAllMatchingGrantsAreRevoked_ShouldDeny()
    {
        //Arrange
        _grantStore.AddGrant(CreateGrant(GrantA));
        _grantStore.AddGrant(CreateGrant(GrantB));

        _grantStore.RevokeGrant(GrantA);
        _grantStore.RevokeGrant(GrantB);

        var request = CreateRequest();

        //Act
        var decision = _service.Authorize(request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.NoMatchingGrant, decision.Reason);
    }

    private static Grant CreateGrant(
        Guid grantId,
        ResourceAction action = ResourceAction.Read,
        ResourceScope? scope = null,
        Guid? agentId = null,
        Guid? workflowId = null)
    {
        return new Grant(
            grantId,
            agentId ?? AgentId,
            workflowId ?? WorkflowId,
            ImmutableHashSet.Create(action),
            scope ?? new ResourceSetScope(ImmutableHashSet.Create(ResourceId)),
            ValidFrom,
            ValidFrom.AddHours(1));
    }

    private static AuthorizationRequest CreateRequest()
    {
        return new AuthorizationRequest(
            AgentId,
            ResourceAction.Read,
            ResourceId,
            WorkflowId);
    }
}
