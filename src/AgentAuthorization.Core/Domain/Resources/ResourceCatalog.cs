using System.Collections.Immutable;

namespace AgentAuthorization.Core.Domain.Resources;

public sealed class ResourceCatalog
{
    private readonly ImmutableDictionary<Guid, Guid> _resourceProjects;

    public ResourceCatalog(ImmutableDictionary<Guid, Guid> resourceProjects)
    {
        ArgumentNullException.ThrowIfNull(resourceProjects, nameof(resourceProjects));

        foreach (var (resourceId, projectId) in resourceProjects)
        {
            if (resourceId == Guid.Empty)
            {
                throw new ArgumentException("Resource ID cannot be empty.", nameof(resourceProjects));
            }
            if (projectId == Guid.Empty)
            {
                throw new ArgumentException("Project ID cannot be empty.", nameof(resourceProjects));
            }
        }

        _resourceProjects = resourceProjects;
    }

    public Guid? GetProjectId(Guid resourceId)
    {
        if (_resourceProjects.TryGetValue(resourceId, out var projectId))
        {
            return projectId;
        }
        return null;
    }

}
