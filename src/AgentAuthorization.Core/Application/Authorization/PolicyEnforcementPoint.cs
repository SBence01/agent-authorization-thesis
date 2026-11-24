using AgentAuthorization.Core.Application.Auditing;
using AgentAuthorization.Core.Application.Resources;
using AgentAuthorization.Core.Domain.Authorization;

namespace AgentAuthorization.Core.Application.Authorization;

public sealed class PolicyEnforcementPoint
{
    private readonly AuthorizationService _authorizationService;
    private readonly IResourceOperationExecutor _executor;
    private readonly IAuthorizationAuditSink _auditSink;

    public PolicyEnforcementPoint(AuthorizationService authorizationService, IResourceOperationExecutor executor, IAuthorizationAuditSink auditSink)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(auditSink);

        _authorizationService = authorizationService;
        _executor = executor;
        _auditSink = auditSink;
    }

    public AuthorizationDecision Execute(AuthorizationRequest request, DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = _authorizationService.Authorize(request, currentTime);

        var entry = new AuthorizationAuditEntry(currentTime, request, result);
        _auditSink.Write(entry);

        if (!result.IsAllowed)
        {
            return result;
        }

        _executor.Execute(request);

        return result;
    }
}
