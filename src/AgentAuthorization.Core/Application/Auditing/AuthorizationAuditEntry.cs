using AgentAuthorization.Core.Domain.Authorization;

namespace AgentAuthorization.Core.Application.Auditing;

//captures an authorization decision at the supplied evaluation time
//it does not represent the outcome of resource execution
public sealed record AuthorizationAuditEntry(
    DateTimeOffset Timestamp,
    AuthorizationRequest Request,
    AuthorizationDecision Decision
);
