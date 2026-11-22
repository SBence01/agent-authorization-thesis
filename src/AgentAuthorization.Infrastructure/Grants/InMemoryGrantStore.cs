using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Grants;

namespace AgentAuthorization.Infrastructure.Grants
{
    public class InMemoryGrantStore : IGrantStore
    {
        private readonly Dictionary<Guid, Grant> _grants = new();

        public void AddGrant(Grant grant)
        {
            ArgumentNullException.ThrowIfNull(grant, nameof(grant));

            if (!_grants.TryAdd(grant.GrantId, grant))
            {
                throw new InvalidOperationException($"Grant with ID {grant.GrantId} already exists.");
            }
        }

        public Grant? GetById(Guid grantId)
        {
            if (grantId == Guid.Empty)
            {
                return null;
            }

            return _grants.GetValueOrDefault(grantId);
        }

        public IReadOnlyCollection<Grant> GetByAgentId(Guid agentId)
        {
            if (agentId == Guid.Empty)
            {
                return Array.Empty<Grant>();
            }

            return _grants.Values.Where(g => g.AgentId == agentId).ToList();
        }

        public bool RevokeGrant(Guid grantId)
        {
            if (grantId == Guid.Empty)
            {
                return false;
            }

            if (!_grants.TryGetValue(grantId, out var grant))
            {
                return false;
            }

            if (!grant.IsRevoked)
            {
                grant.Revoke();
            }

            return true;
        }
    }
}
