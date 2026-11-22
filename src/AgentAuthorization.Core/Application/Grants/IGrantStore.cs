using AgentAuthorization.Core.Domain.Grants;

namespace AgentAuthorization.Core.Application.Grants;

public interface IGrantStore
{
    void AddGrant(Grant grant);
    Grant? GetById(Guid grantId);
    IReadOnlyCollection<Grant> GetByAgentId(Guid agentId);
    bool RevokeGrant(Guid grantId);
}
