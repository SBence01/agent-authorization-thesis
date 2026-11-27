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

            //Explicit resource membership does not require catalog registration
            //Project membership must be established through the catalog
            return scope switch
            {
                ResourceSetScope resourceSetScope => resourceSetScope.ResourceIds.Contains(resourceId),

                ProjectScope projectScope => _resourceCatalog.GetProjectId(resourceId) == projectScope.ProjectId,

                //Null and unsupported scopes do not authorize access
                _ => false
            };
        }

        //Checks whether the child scope stays within the parent's resource boundaries.
        //A project scope cannot be derived from an explicit resourse set even
        //if that set contains every currently registered resource in the project.
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
