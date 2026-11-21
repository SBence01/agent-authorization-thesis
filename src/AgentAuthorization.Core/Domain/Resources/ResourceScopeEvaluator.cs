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
    }
}
