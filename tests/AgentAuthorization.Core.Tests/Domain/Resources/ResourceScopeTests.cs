using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Domain.Resources;

public sealed class ResourceScopeTests
{
    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid OtherResourceId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void ProjectScope_WithValidProjectId_ShouldPreserveProjectId()
    {
        //Arrange
        var projectId = Guid.Parse("00000000-0000-0000-0000-000000000010");

        //Act
        var projectScope = new ProjectScope(projectId);

        //Assert
        Assert.Equal(projectId, projectScope.ProjectId);
    }

    [Fact]
    public void ProjectScope_WithEmptyProjectId_ShouldThrow()
    {
        //Assert
        Assert.Throws<ArgumentException>(() => new ProjectScope(Guid.Empty));
    }

    [Fact]
    public void ResourceSetScope_WithValidResourceIds_ShouldPreserveResourceIds()
    {
        //Arrange
        var resourceIds = ImmutableHashSet.Create(ResourceId, OtherResourceId);

        //Act
        var resourceSetScope = new ResourceSetScope(resourceIds);

        //Assert
        Assert.True(resourceIds.SetEquals(resourceSetScope.ResourceIds));
    }

    [Fact]
    public void ResourceSetScope_WithNullResourceIds_ShouldThrow()
    {
        //Assert
        Assert.Throws<ArgumentNullException>(() => new ResourceSetScope(null!));
    }

    [Fact]
    public void ResourceSetScope_WithEmptyResourceIds_ShouldThrow()
    {
        //Assert
        Assert.Throws<ArgumentException>(() => new ResourceSetScope(ImmutableHashSet<Guid>.Empty));
    }

    [Fact]
    public void ResourceSetScope_WithEmptyIdAmongValidIds_ShouldThrow()
    {
        //Arrange
        var resourceIds = ImmutableHashSet.Create(ResourceId, Guid.Empty);

        //Assert
        Assert.Throws<ArgumentException>(() => new ResourceSetScope(resourceIds));
    }
}
