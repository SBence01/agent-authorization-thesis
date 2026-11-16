using AgentAuthorization.Core.Domain.Resources;

namespace AgentAuthorization.Core.Domain.Authorization;

public sealed record AuthorizationRequest(
    Guid AgentId,
    ResourceAction Action,
    Guid ResourceId,
    Guid WorkflowId
);