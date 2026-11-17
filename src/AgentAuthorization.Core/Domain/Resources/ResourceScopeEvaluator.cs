namespace AgentAuthorization.Core.Domain.Resources
{
    public sealed class ResourceScopeEvaluator
    {
        public bool ContainsResource(ResourceScope scope, Guid resourceId)
        {
            if (resourceId == Guid.Empty)
            {
                return false;
            }

            return scope switch
            {
                ResourceSetScope resourceSetScope => resourceSetScope.ResourceIds.Contains(resourceId),

                _ => false
            };
        }
    }
}
