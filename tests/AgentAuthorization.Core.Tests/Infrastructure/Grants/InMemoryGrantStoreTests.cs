using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using AgentAuthorization.Infrastructure.Grants;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Infrastructure.Grants;

public sealed class InMemoryGrantStoreTests
{
    private static readonly Guid GrantA = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid GrantB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid GrantC = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly Guid AgentA = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid AgentB = Guid.Parse("00000000-0000-0000-0000-000000000102");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    private static readonly DateTimeOffset ValidFrom = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Add_WithValidGrant_ShouldPreserverGrantData()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        var grant = CreateGrant(GrantA, AgentA);

        //Act
        store.AddGrant(grant);
        var storedGrant = store.GetById(GrantA);

        //Assert
        Assert.NotNull(storedGrant);
        Assert.Equal(grant.GrantId, storedGrant.GrantId);
        Assert.Equal(grant.AgentId, storedGrant.AgentId);
        Assert.Equal(grant.WorkflowId, storedGrant.WorkflowId);
        Assert.True(grant.AllowedActions.SetEquals(storedGrant.AllowedActions));

        var storedScope = Assert.IsType<ResourceSetScope>(storedGrant.Scope);
        Assert.True(ImmutableHashSet.Create(ResourceId).SetEquals(storedScope.ResourceIds));

        Assert.Equal(grant.ValidFrom, storedGrant.ValidFrom);
        Assert.Equal(grant.ExpiresAt, storedGrant.ExpiresAt);
    }

    [Fact]
    public void Add_WithNullGrant_ShouldThrow()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();

        //Act
        Action act = () => store.AddGrant(null!);

        //Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Add_WithDuplicateGrantId_ShouldThrowAndPreserveOriginalGrant()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();

        var originalGrant = CreateGrant(GrantA, AgentA);
        var conflictingGrant = CreateGrant(GrantA, AgentB);

        store.AddGrant(originalGrant);

        //Act
        Action act = () => store.AddGrant(conflictingGrant);

        //Assert
        Assert.Throws<InvalidOperationException>(act);

        var storedGrant = store.GetById(GrantA);

        Assert.NotNull(storedGrant);
        Assert.Equal(AgentA, storedGrant.AgentId);
        Assert.Single(store.GetByAgentId(AgentA));
        Assert.Empty(store.GetByAgentId(AgentB));
    }

    [Fact]
    public void GetById_WithMultipleGrants_ShouldReturnCorrectGrant()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();

        var grantA = CreateGrant(GrantA, AgentA);
        var grantB = CreateGrant(GrantB, AgentB);

        store.AddGrant(grantA);
        store.AddGrant(grantB);

        //Act
        var storedGrant = store.GetById(GrantB);

        //Assert
        Assert.NotNull(storedGrant);
        Assert.Equal(GrantB, storedGrant.GrantId);
        Assert.Equal(AgentB, storedGrant.AgentId);
    }

    [Fact]
    public void GetById_WithUnknownGrantId_ShouldReturnNull()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        store.AddGrant(CreateGrant(GrantA, AgentA));

        //Act
        var result = store.GetById(GrantB);

        //Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetById_WithEmptyGrantId_ShouldReturnNull()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        store.AddGrant(CreateGrant(GrantA, AgentA));

        //Act
        var result = store.GetById(Guid.Empty);

        //Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetByAgentId_ShouldReturnOnlyRequestedAgentsGrants()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();

        var grantA = CreateGrant(GrantA, AgentA);
        var grantB = CreateGrant(GrantB, AgentA);
        var grantC = CreateGrant(GrantC, AgentB);
        store.AddGrant(grantA);
        store.AddGrant(grantB);
        store.AddGrant(grantC);

        //Act
        var agentAGrants = store.GetByAgentId(AgentA);
        //Assert
        Assert.Equal(2, agentAGrants.Count);
        Assert.All(agentAGrants, g => Assert.Equal(AgentA, g.AgentId));
        Assert.Contains(agentAGrants, g => g.GrantId == GrantA);
        Assert.Contains(agentAGrants, g => g.GrantId == GrantB);
    }

    [Fact]
    public void GetByAgentId_WithUnknownAgent_ShouldReturnEmptyCollection()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        store.AddGrant(CreateGrant(GrantA, AgentA));

        //Act
        var result = store.GetByAgentId(AgentB);

        //Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetByAgentId_WithEmptyAgentId_ShouldReturnEmptyCollection()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        store.AddGrant(CreateGrant(GrantA, AgentA));

        //Act
        var result = store.GetByAgentId(Guid.Empty);

        //Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetByAgentId_WhenStoreIsEmpty_ShouldReturnEmptyCollection()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();

        //Act
        var result = store.GetByAgentId(AgentA);

        //Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetByAgentId_ShouldReturnCollectionSnapshot()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        store.AddGrant(CreateGrant(GrantA, AgentA));

        var previousSnapshot = store.GetByAgentId(AgentA);

        //Act
        store.AddGrant(CreateGrant(GrantB, AgentA));

        //Assert
        var previousGrant = Assert.Single(previousSnapshot);
        Assert.Equal(GrantA, previousGrant.GrantId);

        var currentSnapshot = store.GetByAgentId(AgentA);
        Assert.Equal(2, currentSnapshot.Count);
    }

    [Fact]
    public void SeparateStores_ShouldKeepTheirDataIndependent()
    {
        //Arrange
        IGrantStore firstStore = new InMemoryGrantStore();
        IGrantStore secondStore = new InMemoryGrantStore();

        //Act
        firstStore.AddGrant(CreateGrant(GrantA, AgentA));

        //Assert
        Assert.NotNull(firstStore.GetById(GrantA));
        Assert.Null(secondStore.GetById(GrantA));
        Assert.Empty(secondStore.GetByAgentId(AgentA));
    }

    [Fact]
    public void RevokeGrant_WithExistingGrant_ShouldRevokeStoredGrant()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        store.AddGrant(CreateGrant(GrantA, AgentA));

        //Act
        var result = store.RevokeGrant(GrantA);

        //Assert
        Assert.True(result);

        var storedGrant = store.GetById(GrantA);
        Assert.NotNull(storedGrant);
        Assert.True(storedGrant.IsRevoked);

        var agentsGrant = Assert.Single(store.GetByAgentId(AgentA));
        Assert.Equal(GrantA, agentsGrant.GrantId);
        Assert.True(agentsGrant.IsRevoked);
    }

    [Fact]
    public void RevokeGrant_WithUnknownGrantId_ShouldReturnFalse()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        var existingGrant = CreateGrant(GrantA, AgentA);
        store.AddGrant(existingGrant);

        //Act
        var result = store.RevokeGrant(GrantB);

        //Assert
        Assert.False(result);
        Assert.False(existingGrant.IsRevoked);
    }

    [Fact]
    public void RevokeGrant_WithEmptyGrantId_ShouldReturnFalse()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        var existingGrant = CreateGrant(GrantA, AgentA);
        store.AddGrant(existingGrant);

        //Act
        var result = store.RevokeGrant(Guid.Empty);

        //Assert
        Assert.False(result);
        Assert.False(existingGrant.IsRevoked);
    }

    [Fact]
    public void RevokeGrant_WhenAlreadyRevoked_ShouldReturnTrue()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();
        store.AddGrant(CreateGrant(GrantA, AgentA));
        store.RevokeGrant(GrantA);

        //Act
        var result = store.RevokeGrant(GrantA);

        //Assert
        Assert.True(result);

        var storedGrant = store.GetById(GrantA);
        Assert.NotNull(storedGrant);
        Assert.True(storedGrant.IsRevoked);
    }

    [Fact]
    public void RevokeGrant_ShouldLeaveOtherGrantsActive()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();

        var firstGrant = CreateGrant(GrantA, AgentA);
        var secondGrant = CreateGrant(GrantB, AgentA);

        store.AddGrant(firstGrant);
        store.AddGrant(secondGrant);

        //Act
        store.RevokeGrant(GrantA);

        //Assert
        Assert.True(firstGrant.IsRevoked);
        Assert.False(secondGrant.IsRevoked);
    }

    [Fact]
    public void AddGrant_WithRevokedGrantId_ShouldRejectReplacement()
    {
        //Arrange
        IGrantStore store = new InMemoryGrantStore();

        store.AddGrant(CreateGrant(GrantA, AgentA));
        store.RevokeGrant(GrantA);

        var replacementGrant = CreateGrant(GrantA, AgentA);

        //Act
        Action act = () => store.AddGrant(replacementGrant);

        //Assert
        Assert.Throws<InvalidOperationException>(act);

        var storedGrant = store.GetById(GrantA);
        Assert.NotNull(storedGrant);
        Assert.True(storedGrant.IsRevoked);
    }

    private static Grant CreateGrant(Guid grantId, Guid agentId)
    {
        return new Grant(
            grantId,
            agentId,
            WorkflowId,
            ImmutableHashSet.Create(ResourceAction.Read),
            new ResourceSetScope(ImmutableHashSet.Create(ResourceId)),
            ValidFrom,
            ValidFrom.AddHours(1)
        );
    }
}
