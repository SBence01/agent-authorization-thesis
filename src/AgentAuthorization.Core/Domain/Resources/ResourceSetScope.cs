using System.Collections.Immutable;

namespace AgentAuthorization.Core.Domain.Resources;

public sealed record ResourceSetScope : ResourceScope
{
    public ImmutableHashSet<Guid> ResourceIds { get; }

    public ResourceSetScope(ImmutableHashSet<Guid> resourceIds)
    {
        ArgumentNullException.ThrowIfNull(resourceIds);

        if (resourceIds.Count == 0)
        {
            throw new ArgumentException("Resource IDs cannot be empty.", nameof(resourceIds));
        }

        if (resourceIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Resource IDs cannot contain empty GUIDs.", nameof(resourceIds));
        }

        ResourceIds = resourceIds;
    }
}
