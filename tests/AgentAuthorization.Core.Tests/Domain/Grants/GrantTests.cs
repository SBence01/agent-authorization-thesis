using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Domain.Grants;

public sealed class GrantTests
{

    [Fact]
    public void Constructor_WhenExpirationIsBeforeValidFrom_ShouldThrow()
    {
        //Arrange
        var validFrom = DateTime.UtcNow;

        //Assert
        Assert.Throws<ArgumentException>(() => new Grant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ImmutableHashSet.Create(ResourceAction.Read),
            new ProjectScope(Guid.NewGuid()),
            validFrom,
            validFrom.AddMinutes(-1)));
    }

    [Fact]
    public void Constructor_WhenAllowedActionsIsEmpty_ShouldThrow()
    {
        //Arrange
        var validFrom = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new Grant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ImmutableHashSet<ResourceAction>.Empty,
            new ProjectScope(Guid.NewGuid()),
            validFrom,
            validFrom.AddHours(1)));
    }

    [Fact]
    public void Constructor_WhenExpirationEqualsValidFrom_ShouldThrow()
    {
        //Arrange
        var now = DateTimeOffset.UtcNow;

        //Assert
        Assert.Throws<ArgumentException>(() => new Grant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ImmutableHashSet.Create(ResourceAction.Read),
            new ProjectScope(Guid.NewGuid()),
            now,
            now));
    }

    [Fact]
    public void Constructor_WhenAllowedActionsIsNull_ShouldThrow()
    {
        //Arrange
        var now = DateTimeOffset.UtcNow;

        //Assert
        Assert.Throws<ArgumentException>(() => new Grant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null!,
            new ProjectScope(Guid.NewGuid()),
            now,
            now.AddHours(1)));
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateGrant()
    {
        //Arrange
        var grantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var actions = ImmutableHashSet.Create(ResourceAction.Read);
        var scope = new ProjectScope(Guid.NewGuid());

        var validFrom = DateTimeOffset.UtcNow;
        var expiresAt = validFrom.AddHours(1);

        //Act
        var grant = new Grant(grantId, agentId, workflowId, actions, scope, validFrom, expiresAt);

        //Assert
        Assert.Equal(grantId, grant.GrantId);
        Assert.Equal(agentId, grant.AgentId);
        Assert.Equal(workflowId, grant.WorkflowId);
        Assert.Equal(actions, grant.AllowedActions);
        Assert.Equal(scope, grant.Scope);
        Assert.Equal(validFrom, grant.ValidFrom);
        Assert.Equal(expiresAt, grant.ExpiresAt);
    }

    [Fact]
    public void Constructor_WhenGrantIdIsEmpty_ShouldThrow()
    {
        //Arrange
        var now = DateTimeOffset.UtcNow;

        //Assert
        Assert.Throws<ArgumentException>(() => new Grant(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ImmutableHashSet.Create(ResourceAction.Read),
            new ProjectScope(Guid.NewGuid()),
            now,
            now.AddHours(1)));
    }

    [Fact]
    public void Constructor_WhenAgentIdIsEmpty_ShouldThrow()
    {
        //Arrange
        var now = DateTimeOffset.UtcNow;

        //Assert
        Assert.Throws<ArgumentException>(() => new Grant(
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            ImmutableHashSet.Create(ResourceAction.Read),
            new ProjectScope(Guid.NewGuid()),
            now,
            now.AddHours(1)));
    }

    [Fact]
    public void Constructor_WhenWorkflowIdIsEmpty_ShouldThrow()
    {
        //Arrange
        var now = DateTimeOffset.UtcNow;

        //Assert
        Assert.Throws<ArgumentException>(() => new Grant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            ImmutableHashSet.Create(ResourceAction.Read),
            new ProjectScope(Guid.NewGuid()),
            now,
            now.AddHours(1)));
    }

    [Fact]
    public void Constructor_WhenScopeIsNull_ShouldThrow()
    {
        //Arrange
        var now = DateTimeOffset.UtcNow;

        //Assert
        Assert.Throws<ArgumentNullException>(() => new Grant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ImmutableHashSet.Create(ResourceAction.Read),
            null!,
            now,
            now.AddHours(1)));
    }

    [Fact]
    public void Constructor_WhenAllowedActionsContainsUndefinedValue_ShouldThrow()
    {
        //Arrange
        var now = DateTimeOffset.UtcNow;
        var actions = ImmutableHashSet.Create(ResourceAction.Read, (ResourceAction)999);

        Assert.Throws<ArgumentException>(() => new Grant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            actions,
            new ProjectScope(Guid.NewGuid()),
            now,
            now.AddHours(1)));
    }
}
