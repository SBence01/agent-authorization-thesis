using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using AgentAuthorization.Infrastructure.Grants;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Application.Grants;

public sealed class GrantDelegationServiceTests
{
    private static readonly Guid ParentId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid ChildId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid SecondChildId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid RecipientId = Guid.Parse("00000000-0000-0000-0000-000000000102");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    private static readonly Guid ProjectId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000401");

    private static readonly DateTimeOffset Start = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryGrantStore _store = new();
    private readonly GrantDelegationValidator _validator;
    private readonly GrantDelegationService _service;

    public GrantDelegationServiceTests()
    {
        var catalog = new ResourceCatalog(ImmutableDictionary<Guid, Guid>.Empty.Add(ResourceId, ProjectId));

        _validator = new GrantDelegationValidator(new ResourceScopeEvaluator(catalog));

        _service = new GrantDelegationService(_store, _validator);
    }

    [Fact]
    public void Constructor_WithNullStore_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new GrantDelegationService(null!, _validator));
    }

    [Fact]
    public void Constructor_WithNullValidator_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new GrantDelegationService(_store, null!));
    }

    [Fact]
    public void TryDelegateGrant_WithNullChild_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => _service.TryDelegateGrant(null!, OwnerId, Start));
    }

    [Fact]
    public void TryDelegateGrant_WithValidChild_ShouldStoreChild()
    {
        //Arrange
        var parent = CreateParent();
        var child = CreateChild();
        _store.AddGrant(parent);

        //Act
        var result = _service.TryDelegateGrant(child, OwnerId, Start);

        //Assert
        Assert.True(result);
        Assert.Same(child, _store.GetById(ChildId));
        Assert.Same(child, Assert.Single( _store.GetByAgentId(RecipientId)));
        Assert.Same(parent, _store.GetById(ParentId));
        Assert.False(parent.IsRevoked);
    }

    [Fact]
    public void TryDelegateGrant_WithoutParentLink_ShouldNotStoreChild()
    {
        //Arrange
        _store.AddGrant(CreateParent());
        var child = CreateChild(withParent: false);

        //Act
        var result = _service.TryDelegateGrant(child, OwnerId, Start);

        //Assert
        Assert.False(result);
        Assert.Null(_store.GetById(ChildId));
        Assert.Empty(_store.GetByAgentId(RecipientId));
    }

    [Fact]
    public void TryDelegateGrant_WithMissingParent_ShouldNotStoreChild()
    {
        //Arrange
        var child = CreateChild();

        //Act
        var result = _service.TryDelegateGrant(child, OwnerId, Start);

        //Assert
        Assert.False(result);
        Assert.Null(_store.GetById(ChildId));
        Assert.Empty(_store.GetByAgentId(RecipientId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryDelegateGrant_WithInvalidOwner_ShouldNotStoreChild(bool useEmptyId)
    {
        //Arrange
        _store.AddGrant(CreateParent());
        var child = CreateChild();
        var callerId = useEmptyId ? Guid.Empty : RecipientId;

        //Act
        var result = _service.TryDelegateGrant(child, callerId, Start);

        //Assert
        Assert.False(result);
        Assert.Null(_store.GetById(ChildId));
        Assert.Empty(_store.GetByAgentId(RecipientId));
    }

    [Fact]
    public void TryDelegateGrant_WithAdditionalAction_ShouldNotStoreChild()
    {
        //Arrange
        _store.AddGrant(CreateParent());
        var child = CreateChild(action: ResourceAction.Delete);

        //Act
        var result = _service.TryDelegateGrant(child, OwnerId, Start);

        //Assert
        Assert.False(result);
        Assert.Null(_store.GetById(ChildId));
        Assert.Empty(_store.GetByAgentId(RecipientId));
    }

    [Fact]
    public void TryDelegateGrant_AfterParentRevocation_ShouldRejectNewChild()
    {
        //Arrange
        _store.AddGrant(CreateParent());

        var firstChild = CreateChild();
        Assert.True( _service.TryDelegateGrant(firstChild, OwnerId, Start));

        Assert.True(_store.RevokeGrant(ParentId));
        var secondChild = CreateChild(childId: SecondChildId);

        //Act
        var result = _service.TryDelegateGrant(secondChild, OwnerId, Start);

        //Assert
        Assert.False(result);
        Assert.Null(_store.GetById(SecondChildId));
        Assert.Same(firstChild, Assert.Single(_store.GetByAgentId(RecipientId)));
    }

    [Fact]
    public void TryDelegateGrant_WithFutureChildStart_ShouldStoreChild()
    {
        //Arrange
        _store.AddGrant(CreateParent());
        var child = CreateChild(validFrom: Start.AddMinutes(30));

        //Act
        var result = _service.TryDelegateGrant(child, OwnerId, Start);

        //Assert
        Assert.True(result);
        Assert.Same(child, _store.GetById(ChildId));
        Assert.Equal(Start.AddMinutes(30), child.ValidFrom);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryDelegateGrant_WithExistingId_ShouldPreserveExistingGrant(bool revokeExisting)
    {
        //Arrange
        _store.AddGrant(CreateParent());

        var existing = CreateChild(action: ResourceAction.Write);
        _store.AddGrant(existing);

        if (revokeExisting)
        {
            Assert.True(_store.RevokeGrant(ChildId));
        }

        var replacement = CreateChild(action: ResourceAction.Read);

        //Act
        var result = _service.TryDelegateGrant(replacement, OwnerId, Start);

        //Assert
        Assert.False(result);
        Assert.Same(existing, _store.GetById(ChildId));
        Assert.Same(existing, Assert.Single(_store.GetByAgentId(RecipientId)));
        Assert.Equal(revokeExisting, existing.IsRevoked);
        Assert.Contains(ResourceAction.Write, existing.AllowedActions);
        Assert.DoesNotContain(ResourceAction.Read, existing.AllowedActions);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(60, false)]
    public void TryDelegateGrant_ShouldUseSuppliedTime(int minutesAfterStart, bool expected)
    {
        //Arrange
        _store.AddGrant(CreateParent());
        var child = CreateChild();

        //Act
        var result = _service.TryDelegateGrant(
            child,
            OwnerId,
            Start.AddMinutes(minutesAfterStart));

        //Assert
        Assert.Equal(expected, result);

        if (expected)
        {
            Assert.Same(child, _store.GetById(ChildId));
        }
        else
        {
            Assert.Null(_store.GetById(ChildId));
            Assert.Empty(_store.GetByAgentId(RecipientId));
        }
    }

    private static Grant CreateParent()
    {
        return new Grant(
            ParentId,
            OwnerId,
            WorkflowId,
            ImmutableHashSet.Create(
                ResourceAction.Read,
                ResourceAction.Write),
            new ProjectScope(ProjectId),
            Start,
            Start.AddHours(1),
            canDelegate: true);
    }

    private static Grant CreateChild(
        Guid? childId = null,
        ResourceAction action = ResourceAction.Read,
        DateTimeOffset? validFrom = null,
        bool withParent = true)
    {
        return new Grant(
            childId ?? ChildId,
            RecipientId,
            WorkflowId,
            ImmutableHashSet.Create(action),
            new ResourceSetScope(
                ImmutableHashSet.Create(ResourceId)),
            validFrom ?? Start,
            Start.AddHours(1),
            parentGrantId: withParent ? ParentId : null);
    }
}
