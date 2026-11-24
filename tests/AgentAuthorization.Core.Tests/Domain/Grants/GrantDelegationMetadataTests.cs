using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Domain.Grants;

public sealed class GrantDelegationMetadataTests
{
    private static readonly Guid GrantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid ParentGrantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid AgentId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    private static readonly DateTimeOffset ValidFrom = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void constructor_WithoutDelegationParameters_ShouldCreateNonDelegatableRoot()
    {
        //Act
        var grant = new Grant(
            GrantId,
            AgentId,
            WorkflowId,
            ImmutableHashSet.Create(ResourceAction.Read),
            new ResourceSetScope(ImmutableHashSet.Create(ResourceId)),
            ValidFrom,
            ValidFrom.AddHours(1));

        //Assert
        Assert.Null(grant.ParentGrantId);
        Assert.False(grant.CanDelegate);
    }

    [Fact]
    public void Constructor_WithDelegationEnabledForRoot_ShouldAllowDelegation()
    {
        //Act
        var grant = CreateGrant(canDelegate: true);

        //Assert
        Assert.Null(grant.ParentGrantId);
        Assert.True(grant.CanDelegate);
    }

    [Fact]
    public void Constructor_WithParent_ShouldCreateNonDelegatableChild()
    {
        //Act
        var grant = CreateGrant(parentGrantId: ParentGrantId);

        //Assert
        Assert.Equal(ParentGrantId, grant.ParentGrantId);
        Assert.False(grant.CanDelegate);
        Assert.False(grant.IsRevoked);
    }

    [Fact]
    public void Constructor_WithEmptyParentId_ShouldThrow()
    {
        //Act
        var exception = Assert.Throws<ArgumentException>(() => CreateGrant(parentGrantId: Guid.Empty));

        //Assert
        Assert.Equal("parentGrantId", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithOwnIdAsParent_ShouldThrow()
    {
        //Act
        var exception = Assert.Throws<ArgumentException>(() => CreateGrant(parentGrantId: GrantId));

        //Assert
        Assert.Equal("parentGrantId", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithDelegationEnabledForChild_ShouldThrow()
    {
        //Act
        var exception = Assert.Throws<ArgumentException>(() => CreateGrant(parentGrantId: ParentGrantId, canDelegate: true));

        //Assert
        Assert.Equal("canDelegate", exception.ParamName);
    }

    private static Grant CreateGrant(Guid? parentGrantId = null, bool canDelegate = false)
    {
        return new Grant(
            GrantId,
            AgentId,
            WorkflowId,
            ImmutableHashSet.Create(ResourceAction.Read),
            new ResourceSetScope(ImmutableHashSet.Create(ResourceId)),
            ValidFrom,
            ValidFrom.AddHours(1),
            parentGrantId: parentGrantId,
            canDelegate: canDelegate);
    }
}
