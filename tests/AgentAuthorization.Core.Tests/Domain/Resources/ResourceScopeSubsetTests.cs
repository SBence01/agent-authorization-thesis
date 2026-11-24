using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Domain.Resources;

public sealed class ResourceScopeSubsetTests
{
    private static readonly Guid ProjectA = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid ProjectB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid ResourceA = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid ResourceB = Guid.Parse("00000000-0000-0000-0000-000000000102");

    private static readonly Guid ResourceC = Guid.Parse("00000000-0000-0000-0000-000000000103");

    private static readonly Guid UnknownResource = Guid.Parse("00000000-0000-0000-0000-000000000999");

    private readonly ResourceScopeEvaluator _evaluator;

    public ResourceScopeSubsetTests()
    {
        var projects = ImmutableDictionary<Guid, Guid>.Empty
            .Add(ResourceA, ProjectA)
            .Add(ResourceB, ProjectA)
            .Add(ResourceC, ProjectB);

        _evaluator = new ResourceScopeEvaluator(new ResourceCatalog(projects));
    }

    public static IEnumerable<object[]> ScopeCases()
    {
        yield return new object[]
        {
            "Equal resource sets",
            Set(ResourceA, ResourceB),
            Set(ResourceA, ResourceB),
            true
        };

        yield return new object[]
        {
            "Child contains fewer resources",
            Set(ResourceA),
            Set(ResourceA, ResourceB),
            true
        };

        yield return new object[]
        {
            "Child contains an additional resource",
            Set(ResourceA, ResourceB),
            Set(ResourceA),
            false
        };

        yield return new object[]
        {
            "Resource sets do not overlap",
            Set(ResourceC),
            Set(ResourceA, ResourceB),
            false
        };

        yield return new object[]
        {
            "Resource set membership does not require catalog registration",
            Set(UnknownResource),
            Set(ResourceA, UnknownResource),
            true
        };

        yield return new object[]
        {
            "All child resources belong to the parent project",
            Set(ResourceA, ResourceB),
            new ProjectScope(ProjectA),
            true
        };

        yield return new object[]
        {
            "One child resource belongs to another project",
            Set(ResourceA, ResourceC),
            new ProjectScope(ProjectA),
            false
        };

        yield return new object[]
        {
            "One child resource is unknown",
            Set(ResourceA, UnknownResource),
            new ProjectScope(ProjectA),
            false
        };

        yield return new object[]
        {
            "Child resource belongs to another project",
            Set(ResourceC),
            new ProjectScope(ProjectA),
            false
        };

        yield return new object[]
        {
            "Same project",
            new ProjectScope(ProjectA),
            new ProjectScope(ProjectA),
            true
        };

        yield return new object[]
        {
            "Different projects",
            new ProjectScope(ProjectB),
            new ProjectScope(ProjectA),
            false
        };

        yield return new object[]
        {
            "Project scope cannot be derived from a resource set",
            new ProjectScope(ProjectA),
            Set(ResourceA, ResourceB),
            false
        };
    }

    [Theory]
    [MemberData(nameof(ScopeCases))]
    public void IsSubsetOf_ShouldFollowScopeRules(
        string scenario,
        ResourceScope childScope,
        ResourceScope parentScope,
        bool expected)
    {
        //Act
        var actual = _evaluator.IsSubsetOf(childScope, parentScope);

        //Assert
        Assert.True(expected == actual, $"{scenario}: expected {expected}, received {actual}");
    }

    [Fact]
    public void IsSubsetOf_WithNullChild_ShouldReturnFalse()
    {
        //Act
        var result = _evaluator.IsSubsetOf(null!, new ProjectScope(ProjectA));

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSubsetOf_WithUnknownChildScope_ShouldReturnFalse()
    {
        //Act
        var result = _evaluator.IsSubsetOf(new UnknownScope(), new ProjectScope(ProjectA));

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSubsetOf_WithUnknownParentScope_ShouldReturnFalse()
    {
        //Act
        var result = _evaluator.IsSubsetOf(Set(ResourceA), new UnknownScope());

        //Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSubsetOf_WithNullParent_ShouldReturnFalse()
    {
        //Arrange
        var childScope = new ProjectScope(ProjectA);

        //Act
        var result = _evaluator.IsSubsetOf(childScope, null!);

        //Assert
        Assert.False(result);
    }

    private static ResourceSetScope Set(params Guid[] resourceIds)
    {
        return new ResourceSetScope(resourceIds.ToImmutableHashSet());
    }

    private sealed record UnknownScope : ResourceScope;
}
