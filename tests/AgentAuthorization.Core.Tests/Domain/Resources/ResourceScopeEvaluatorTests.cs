using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Domain.Resources;

public sealed class ResourceScopeEvaluatorTests
{
    private static readonly Guid ResourceA = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid ResourceB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid ResourceC = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly Guid UnknownResource = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private static readonly Guid ProjectA = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid ProjectB = Guid.Parse("00000000-0000-0000-0000-000000000102");

    private readonly ResourceScopeEvaluator _evaluator;

    public ResourceScopeEvaluatorTests()
    {
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty
            .Add(ResourceA, ProjectA)
            .Add(ResourceB, ProjectA)
            .Add(ResourceC, ProjectB);

        var catalog = new ResourceCatalog(resourceProjects);

        _evaluator = new ResourceScopeEvaluator(catalog);

    }

    [Fact]
    public void Constructor_WithNullCatalog_ShouldThrow()
    {
        //Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ResourceScopeEvaluator(null!));
    }

    [Fact]
    public void ContainsResource_WhenResourceIsInSet_ShouldReturnTrue()
    {
        //Arrange
        var resourceSetScope = new ResourceSetScope(ImmutableHashSet.Create(ResourceA, ResourceC));

        //Act & Assert
        Assert.True(_evaluator.ContainsResource(resourceSetScope, ResourceA));
        Assert.True(_evaluator.ContainsResource(resourceSetScope, ResourceC));
    }

    [Fact]
    public void ContainsResource_WhenResourceIsNotInSet_ShouldReturnFalse()
    {
        //Arrange
        var resourceSetScope = new ResourceSetScope(ImmutableHashSet.Create(ResourceA));
        //Act & Assert
        Assert.False(_evaluator.ContainsResource(resourceSetScope, ResourceB));
    }

    [Fact]
    public void ContainsResource_WhenResourceIsInProject_ShouldReturnTrue()
    {
        //Arrange
        var projectScope = new ProjectScope(ProjectA);
        //Act & Assert
        Assert.True(_evaluator.ContainsResource(projectScope, ResourceA));
        Assert.True(_evaluator.ContainsResource(projectScope, ResourceB));
    }

    [Fact]
    public void ContainsResource_WithResourceSetAndEmptyResourceId_ShouldReturnFalse()
    {
        //Arrange
        var resourceSetScope = new ResourceSetScope(ImmutableHashSet.Create(ResourceA));

        //Act & Assert
        Assert.False(_evaluator.ContainsResource(resourceSetScope, Guid.Empty));
    }

    [Fact]
    public void ContainsResource_WhenResourcesBelongToProject_ShouldReturnTrue()
    {
        //Arrange
        var projectScope = new ProjectScope(ProjectA);

        //Act & Assert
        Assert.True(_evaluator.ContainsResource(projectScope, ResourceA));
        Assert.True(_evaluator.ContainsResource(projectScope, ResourceB));
    }

    [Fact]
    public void ContainsResource_WhenResourceBelongsToOtherProject_ShouldReturnFalse()
    {
        //Arrange
        var scope = new ProjectScope(ProjectA);

        //Act
        var result = _evaluator.ContainsResource(scope, ResourceC);

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsResource_WhenResourceBelongsToSecondProject_ShouldReturnTrue()
    {
        //Arrange
        var scope = new ProjectScope(ProjectB);
        //Act
        var result = _evaluator.ContainsResource(scope, ResourceC);
        //Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsResource_WhenResourceIsUnknown_ShouldReturnFalse()
    {
        //Arrange
        var scope = new ProjectScope(ProjectA);

        //Act
        var result = _evaluator.ContainsResource(scope, UnknownResource);

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsResource_WithProjectIdAsUnregisteredResource_ShouldReturnFalse()
    {
        //Arrange
        var scope = new ProjectScope(ProjectA);

        //Act
        var result = _evaluator.ContainsResource(scope, ProjectA);

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsResource_WithEmptyResourceId_ShouldReturnFalse()
    {
        //Arrange
        var scope = new ProjectScope(ProjectA);

        //Act
        var result = _evaluator.ContainsResource(scope, Guid.Empty);

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsResource_WithUnknownScope_ShouldReturnFalse()
    {
        //Arrange
        var scope = new UnknownScope();

        //Act
        var result = _evaluator.ContainsResource(scope, ResourceA);

        //Assert
        Assert.False(result);
    }

    private sealed record UnknownScope : ResourceScope;
}
