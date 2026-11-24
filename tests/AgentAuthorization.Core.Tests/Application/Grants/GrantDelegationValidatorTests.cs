using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Application.Grants;

public sealed class GrantDelegationValidatorTests
{
    private static readonly Guid ParentId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ChildId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid RecipientId = Guid.Parse("00000000-0000-0000-0000-000000000201");
    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000301");
    private static readonly Guid ProjectId = Guid.Parse("00000000-0000-0000-0000-000000000401");
    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000501");
    private static readonly Guid OtherResourceId = Guid.Parse("00000000-0000-0000-0000-000000000502");
    private static readonly Guid OtherId = Guid.Parse("00000000-0000-0000-0000-000000000601");
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly GrantDelegationValidator _validator;

    public GrantDelegationValidatorTests()
    {
        var catalog = new ResourceCatalog(
            ImmutableDictionary<Guid, Guid>.Empty
            .Add(ResourceId, ProjectId)
            .Add(OtherResourceId, OtherId));

        _validator = new GrantDelegationValidator(new ResourceScopeEvaluator(catalog));
    }

    [Fact]
    public void Constructor_WithNullEvaluator_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new GrantDelegationValidator(null!));
    }

    [Fact]
    public void CanDelegate_WithNullParent_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => _validator.CanDelegate(null!, CreateChild(), OwnerId, Start));
    }

    [Fact]
    public void CanDelegate_WithNullChild_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => _validator.CanDelegate(CreateParent(), null!, OwnerId, Start));
    }

    [Fact]
    public void CanDelegate_WithNarrowerChild_ShouldReturnTrue()
    {
        //Arrange
        var parent = CreateParent();
        var child = CreateChild(validFrom: Start.AddMinutes(10), expiresAt: Start.AddMinutes(40));

        //Act
        var result = _validator.CanDelegate(parent, child, OwnerId, Start.AddMinutes(20));

        //Assert
        Assert.True(result);
    }

    [Fact]
    public void CanDelegate_WithEqualPermissionsAndTimeRange_ShouldReturnTrue()
    {
        //Arrange
        var parent = CreateParent();
        var child = CreateChild(actions: parent.AllowedActions, scope: new ProjectScope(ProjectId));

        //Act
        var result = _validator.CanDelegate(parent, child, OwnerId, Start);

        //Assert
        Assert.True(result);
    }

    [Fact]
    public void CanDelegate_WithFutureCHildStart_ShouldReturnTrue()
    {
        //Arrange
        var parent = CreateParent();
        var child = CreateChild(validFrom: Start.AddMinutes(30));

        //Act
        var result = _validator.CanDelegate(parent, child, OwnerId, Start);

        //Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("WrongOwner")]
    [InlineData("EmptyOwner")]
    [InlineData("DelegationDisabled")]
    [InlineData("ParentIsDelegated")]
    [InlineData("WrongParentLink")]
    [InlineData("MissingParentLink")]
    [InlineData("DifferentWorkflow")]
    [InlineData("AdditionalAction")]
    [InlineData("ResourceOutsideScope")]
    [InlineData("ChildStartsTooEarly")]
    [InlineData("ChildExpiresTooLate")]
    [InlineData("ParentRevoked")]
    [InlineData("ChildRevoked")]
    public void CanDelegate_WithInvalidDelegation_ShouldReturnFalse(string scenario)
    {
        //Arrange
        var parent = CreateParent();
        var child = CreateChild();
        var delegatingAgentId = OwnerId;

        switch (scenario)
        {
            case "WrongOwner":
                delegatingAgentId = OtherId;
                break;

            case "EmptyOwner":
                delegatingAgentId = Guid.Empty;
                break;

            case "DelegationDisabled":
                parent = CreateParent(canDelegate: false);
                break;

            case "ParentIsDelegated":
                parent = CreateParent(canDelegate: false, parentGrantId: OtherId);
                break;

            case "WrongParentLink":
                child = CreateChild(parentGrantId: OtherId);
                break;

            case "MissingParentLink":
                child = CreateChild(withParent: false);
                break;

            case "DifferentWorkflow":
                child = CreateChild(workflowId: OtherId);
                break;

            case "AdditionalAction":
                child = CreateChild(
                    actions: ImmutableHashSet.Create(
                        ResourceAction.Read,
                        ResourceAction.Delete));
                break;

            case "ResourceOutsideScope":
                child = CreateChild(
                    scope: new ResourceSetScope(
                        ImmutableHashSet.Create(
                            ResourceId,
                            OtherResourceId)));
                break;

            case "ChildStartsTooEarly":
                child = CreateChild(validFrom: Start.AddMinutes(-1));
                break;

            case "ChildExpiresTooLate":
                child = CreateChild(expiresAt: Start.AddMinutes(61));
                break;

            case "ParentRevoked":
                parent.Revoke();
                break;

            case "ChildRevoked":
                child.Revoke();
                break;

            default:
                throw new ArgumentException("Unknown test scenario.", nameof(scenario));
        }

        //Act
        var result = _validator.CanDelegate(parent, child, delegatingAgentId, Start);

        //Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(59, true)]
    [InlineData(60, false)]
    [InlineData(61, false)]
    public void CanDelegate_ShouldRespectParentValidity(int minutesAfterStart, bool expected)
    {
        //Arrange
        var parent = CreateParent();
        var child = CreateChild();
        var currentTime = Start.AddMinutes(minutesAfterStart);

        //Act
        var result = _validator.CanDelegate(parent, child, OwnerId, currentTime);

        //Assert
        Assert.Equal(expected, result);
    }

    private static Grant CreateParent(bool canDelegate = true, Guid? parentGrantId = null)
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
            parentGrantId: parentGrantId,
            canDelegate: canDelegate);
    }

    private static Grant CreateChild(
       ImmutableHashSet<ResourceAction>? actions = null,
       ResourceScope? scope = null,
       Guid? workflowId = null,
       DateTimeOffset? validFrom = null,
       DateTimeOffset? expiresAt = null,
       Guid? parentGrantId = null,
       bool withParent = true)
    {
        return new Grant(
            ChildId,
            RecipientId,
            workflowId ?? WorkflowId,
            actions ?? ImmutableHashSet.Create(ResourceAction.Read),
            scope ?? new ResourceSetScope(ImmutableHashSet.Create(ResourceId)),
            validFrom ?? Start,
            expiresAt ?? Start.AddHours(1),
            parentGrantId: withParent
                ? parentGrantId ?? ParentId
                : null,
            canDelegate: false);
    }
}
