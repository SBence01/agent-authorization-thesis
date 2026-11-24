using AgentAuthorization.Core.Domain.Authorization;

namespace AgentAuthorization.Core.Application.Resources;

public interface IResourceOperationExecutor
{
    void Execute(AuthorizationRequest request);
}
