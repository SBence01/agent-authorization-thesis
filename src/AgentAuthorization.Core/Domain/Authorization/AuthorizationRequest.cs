public sealed record AuthorizationRequest(
    Guid AgentId,
    string Action,
    Guid ResourceId,
    Guid WorkflowId
);