using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Tests.Domain.Resources;

public sealed class ResourceCatalogTests
{
    private static readonly Guid ResourceA = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid ResourceB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly Guid ResourceC = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly Guid ProjectA = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid ProjectB = Guid.Parse("00000000-0000-0000-0000-000000000102");

    [Fact]
    public void GetProjectId_WithKnownResources_ShouldReturnTheCorrectProjectId()
    {
        //Arrange
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty
            .Add(ResourceA, ProjectA)
            .Add(ResourceB, ProjectB);

        var catalog = new ResourceCatalog(resourceProjects);

        //Act
        var projectIdA = catalog.GetProjectId(ResourceA);
        var projectIdB = catalog.GetProjectId(ResourceB);

        //Assert
        Assert.Equal(ProjectA, projectIdA);
        Assert.Equal(ProjectB, projectIdB);
    }

    [Fact]
    public void GetProjectId_WithResourcesInSmaeProject_ShouldReturnTheSameProjectId()
    {
        //Arrange
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty
            .Add(ResourceA, ProjectA)
            .Add(ResourceB, ProjectA);

        var catalog = new ResourceCatalog(resourceProjects);

        //Act
        var projectIdA = catalog.GetProjectId(ResourceA);
        var projectIdB = catalog.GetProjectId(ResourceB);

        //Assert
        Assert.Equal(ProjectA, projectIdA);
        Assert.Equal(ProjectA, projectIdB);
    }

    [Fact]
    public void GetProjectId_WithUnknownResource_ShouldReturnNull()
    {
        //Arrange
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty
            .Add(ResourceA, ProjectA)
            .Add(ResourceB, ProjectB);

        var catalog = new ResourceCatalog(resourceProjects);

        //Act
        var projectIdC = catalog.GetProjectId(ResourceC);

        //Assert
        Assert.Null(projectIdC);
    }

    [Fact]
    public void GetProjectId_WithEmptyResourceId_ShouldReturnNull()
    {
        //Arrange
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty.Add(ResourceA, ProjectA);
        var catalog = new ResourceCatalog(resourceProjects);

        //Act
        var projectIdEmpty = catalog.GetProjectId(Guid.Empty);

        //Assert
        Assert.Null(projectIdEmpty);
    }

    [Fact]
    public void Constructor_WithEmptyMapping_ShouldCreateAnEmptyCatalog()
    {
        //Arrange
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty;

        //Act
        var catalog = new ResourceCatalog(resourceProjects);

        //Assert
        Assert.NotNull(catalog);
    }

    [Fact]
    public void Constructor_WithNullMapping_ShouldThrowArgumentNullException()
    {
        //Arrange
        ImmutableDictionary<Guid, Guid> resourceProjects = null!;

        //Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ResourceCatalog(resourceProjects));
    }

    [Fact]
    public void Constructor_WithEmptyResourceIdAmongValidEntires_ShouldThrow()
    {
        //Arrange
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty
            .Add(ResourceA, ProjectA)
            .Add(Guid.Empty, ProjectB);

        //Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new ResourceCatalog(resourceProjects));
    }

    [Fact]
    public void Constructor_WithEmptyProjectIdAmongValidEntires_ShouldThrow()
    {
        //Arrange
        var resourceProjects = ImmutableDictionary<Guid, Guid>.Empty
            .Add(ResourceA, ProjectA)
            .Add(ResourceB, Guid.Empty);

        //Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new ResourceCatalog(resourceProjects));
    }
}
