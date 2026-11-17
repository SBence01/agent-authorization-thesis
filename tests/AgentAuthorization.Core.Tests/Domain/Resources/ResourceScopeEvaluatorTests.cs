using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Domain.Resources;

public sealed class ResourceScopeEvaluatorTests
{
    public static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static readonly Guid OtherResourceId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private readonly ResourceScopeEvaluator _evaluator = new();

    [Fact]
    public void ContainsResource_WhenResourceIsInSet_ShouldReturnTrue()
    {
        //Arrange
        var resourceSetScope = new ResourceSetScope(ImmutableHashSet.Create(ResourceId, OtherResourceId));

        //Assert
        Assert.True(_evaluator.ContainsResource(resourceSetScope, ResourceId));
        Assert.True(_evaluator.ContainsResource(resourceSetScope, OtherResourceId));
    }

    [Fact]
    public void ContainsResource_WhenResourceIsNotInSet_ShouldReturnFalse()
    {
        //Arrange
        var resourceSetScope = new ResourceSetScope(ImmutableHashSet.Create(ResourceId));

        //Act
        var result = _evaluator.ContainsResource(resourceSetScope, OtherResourceId);

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsResource_WhenResourceIdIsEmpty_ShouldReturnFalse()
    {
        //Arrange
        var resourceSetScope = new ResourceSetScope(ImmutableHashSet.Create(ResourceId));

        //Act
        var result = _evaluator.ContainsResource(resourceSetScope, Guid.Empty);

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsResource_WithUnsupportedProjectScope_ShouldReturnFalse()
    {
        //Arrange
        var projectId = Guid.Parse("00000000-0000-0000-0000-000000000010");

        //Act
        var scope = new ProjectScope(projectId);
        var result = _evaluator.ContainsResource(scope, ResourceId);

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsResource_WithUnknownScope_ShouldReturnFalse()
    {
        //Arrange
        var scope = new UnknownScope();

        //Act
        var result = _evaluator.ContainsResource(scope, ResourceId);

        Assert.False(result);
    }

    private sealed record UnknownScope : ResourceScope;
}
