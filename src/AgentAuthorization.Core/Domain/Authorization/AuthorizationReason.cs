using System.ComponentModel;

namespace AgentAuthorization.Core.Domain.Authorization;

public enum AuthorizationReason
{
    [Description("The operation was authorized.")]
    Allowed,
    [Description("No matching grant found for the requested authorization.")]
    NoMatchingGrant,
    [Description("The agent does not match the grant.")]
    AgentMismatch,
    [Description("The workflow does not match the grant.")]
    WorkflowMismatch,
    [Description("The requested action is not allowed by the grant.")]
    ActionNotAllowed,
    [Description("The requested resource is out of the grant's scope.")]
    ResourceOutOfScope,
    [Description("The grant is not yet valid.")]
    GrantNotYetValid,
    [Description("The grant has expired.")]
    GrantExpired,
    [Description("The grant has been revoked.")]
    GrantRevoked
}