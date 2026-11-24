namespace AgentAuthorization.Core.Domain.Resources
{
    public sealed class ResourceScopeEvaluator
    {
        private readonly ResourceCatalog _resourceCatalog;

        public ResourceScopeEvaluator(ResourceCatalog resourceCatalog)
        {
            ArgumentNullException.ThrowIfNull(resourceCatalog, nameof(resourceCatalog));
            _resourceCatalog = resourceCatalog;
        }

        public bool ContainsResource(ResourceScope scope, Guid resourceId)
        {
            if (resourceId == Guid.Empty)
            {
                return false;
            }

            return scope switch
            {
                ResourceSetScope resourceSetScope => resourceSetScope.ResourceIds.Contains(resourceId),

                ProjectScope projectScope => _resourceCatalog.GetProjectId(resourceId) == projectScope.ProjectId,

                _ => false
            };
        }

        public bool IsSubsetOf(ResourceScope childScope, ResourceScope parentScope)
        {
            return (childScope, parentScope) switch
            {
                (ResourceSetScope child, ResourceSetScope parent)
                    => child.ResourceIds.IsSubsetOf(parent.ResourceIds),

                (ResourceSetScope child, ProjectScope parent)
                    => child.ResourceIds.All(id => _resourceCatalog.GetProjectId(id) == parent.ProjectId),

                (ProjectScope child, ProjectScope parent)
                    => child.ProjectId == parent.ProjectId,

                _ => false
            };
        }
    }
}
