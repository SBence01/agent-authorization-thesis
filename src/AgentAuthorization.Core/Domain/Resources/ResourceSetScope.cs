using System.Collections.Immutable;

namespace AgentAuthorization.Core.Domain.Resources;

public sealed record ResourceSetScope(ImmutableHashSet<Guid> ResourceIds) : ResourceScope
{

}
