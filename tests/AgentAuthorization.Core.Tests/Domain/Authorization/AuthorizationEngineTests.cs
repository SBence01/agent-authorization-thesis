using AgentAuthorization.Core.Application.Authorization;
using AgentAuthorization.Core.Domain.Authorization;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Domain.Authorization;

public sealed class AuthorizationEngineTests
{
    private static readonly Guid GrantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid AgentId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private static readonly Guid OtherId = Guid.Parse("00000000-0000-0000-0000-000000000005");

    private static readonly Guid ProjectId = Guid.Parse("00000000-0000-0000-0000-000000000006");

    private static readonly Guid OtherProjectId = Guid.Parse("00000000-0000-0000-0000-000000000007");

    private static readonly DateTimeOffset ValidFrom = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExpiresAt = ValidFrom.AddHours(1);

    private static readonly DateTimeOffset CurrentTime = ValidFrom.AddMinutes(30);

    private readonly AuthorizationEngine _engine;

    public AuthorizationEngineTests()
    {
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty
            .Add(ResourceId, ProjectId)
            .Add(OtherId, OtherProjectId);

        var catalog = new ResourceCatalog(resourceProjects);

        var scopeEvaluator = new ResourceScopeEvaluator(catalog);

        _engine = new AuthorizationEngine(scopeEvaluator);
    }

    [Fact]
    public void Authorize_WhenAllConstaintsMatch_ShouldAllow()
    {
        //Arrange
        var grant = CreateGrant();
        var request = CreateRequest();

        //Act
        var decision = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenAgentDoesNotMatch_ShouldDeny()
    {
        //Arrange
        var grant = CreateGrant();
        var request = CreateRequest() with { AgentId = OtherId };

        //Act
        var decision = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.AgentMismatch, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenWorkflowDoesNotMatch_ShouldDeny()
    {
        //Arrange
        var grant = CreateGrant();
        var request = CreateRequest() with { WorkflowId = OtherId };

        //Act
        var decision = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.WorkflowMismatch, decision.Reason);
    }

    [Theory]
    [InlineData(ResourceAction.Write)]
    [InlineData(ResourceAction.Delete)]
    [InlineData((ResourceAction)999)]
    public void Authorize_WhenResourceActionIsNotAllowed_ShouldDeny(ResourceAction action)
    {
        //Arrange
        var grant = CreateGrant();
        var request = CreateRequest() with { Action = action };

        //Act
        var decision = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.ActionNotAllowed, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenResourceIsOutOfScope_ShouldDeny()
    {
        //Arrange
        var grant = CreateGrant();
        var request = CreateRequest() with { ResourceId = OtherId };

        //Act
        var decision = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.ResourceOutOfScope, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenResourceIdIsEmpty_ShouldDeny()
    {
        //Arrange
        var grant = CreateGrant();
        var request = CreateRequest() with { ResourceId = Guid.Empty };

        //Act
        var decision = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.ResourceOutOfScope, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenResourceBelongsToGrantedProject_ShouldAllow()
    {
        //Arrange
        var grant = CreateGrant(new ProjectScope(ProjectId));
        var request = CreateRequest();

        //Act
        var decision = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
    }

    [Fact]
    public void Authorize_WhenResourceBelongsToOtherProject_ShouldDeny()
    {
        //Arrange
        var grant = CreateGrant(new ProjectScope(ProjectId));
        var request = CreateRequest() with { ResourceId = OtherId };

        //Act
        var decision = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.ResourceOutOfScope, decision.Reason);
    }

    [Fact]
    public void Authorize_WithProjectScopeAndUnknownResource_ShouldeDeny()
    {
        //Arrange
        var unknownResourceId = Guid.Parse("00000000-0000-0000-0000-000000000008");

        var grant = CreateGrant(new ProjectScope(ProjectId));
        var request = CreateRequest() with { ResourceId = unknownResourceId };

        //Act
        var decision = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.ResourceOutOfScope, decision.Reason);
    }

    [Theory]
    [InlineData(-1L, false, AuthorizationReason.GrantNotYetValid)]
    [InlineData(0L, true, AuthorizationReason.Allowed)]
    [InlineData(1L, true, AuthorizationReason.Allowed)]
    [InlineData(TimeSpan.TicksPerHour - 1, true, AuthorizationReason.Allowed)]
    [InlineData(TimeSpan.TicksPerHour, false, AuthorizationReason.GrantExpired)]
    [InlineData(TimeSpan.TicksPerHour + 1, false, AuthorizationReason.GrantExpired)]
    public void Authorize_AtValidityBoundary_ShouldReturnExpectedDecision(long ticksFromValidFrom, bool expectedAllowed, AuthorizationReason expectedReason)
    {
        //Arrange
        var grant = CreateGrant();
        var request = CreateRequest();
        var currentTime = ValidFrom.AddTicks(ticksFromValidFrom);

        //Act
        var decision = _engine.Authorize(grant, request, currentTime);

        //Assert
        Assert.Equal(expectedAllowed, decision.IsAllowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Fact]
    public void Authorize_AfterGrantIsRevoked_ShouldDenyWithGrantRevoked()
    {
        //Arrange
        var grant = CreateGrant();
        var request = CreateRequest();

        var beforeRevocation = _engine.Authorize(grant, request, CurrentTime);

        //Act
        grant.Revoke();

        var afterRevocation = _engine.Authorize(grant, request, CurrentTime);

        //Assert
        Assert.True(beforeRevocation.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, beforeRevocation.Reason);

        Assert.False(afterRevocation.IsAllowed);
        Assert.Equal(AuthorizationReason.GrantRevoked, afterRevocation.Reason);
    }

    private static Grant CreateGrant(ResourceScope? scope = null)
    {
        return new Grant(
            GrantId,
            AgentId,
            WorkflowId,
            ImmutableHashSet.Create(ResourceAction.Read),
            scope ?? new ResourceSetScope(ImmutableHashSet.Create(ResourceId)),
            ValidFrom,
            ExpiresAt);
    }

    private static AuthorizationRequest CreateRequest()
    {
        return new AuthorizationRequest(AgentId, ResourceAction.Read, ResourceId, WorkflowId);
    }
}
