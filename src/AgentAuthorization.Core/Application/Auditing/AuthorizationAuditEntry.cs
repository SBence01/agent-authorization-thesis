using AgentAuthorization.Core.Domain.Authorization;

namespace AgentAuthorization.Core.Application.Auditing;

public sealed record AuthorizationAuditEntry(
    DateTimeOffset Timestamp,
    AuthorizationRequest Request,
    AuthorizationDecision Decision
);
