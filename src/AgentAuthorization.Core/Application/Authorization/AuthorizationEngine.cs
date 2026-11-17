using AgentAuthorization.Core.Domain.Authorization;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;

namespace AgentAuthorization.Core.Application.Authorization;

public class AuthorizationEngine
{
    private readonly ResourceScopeEvaluator _resourceScopeEvaluator;

    public AuthorizationEngine(ResourceScopeEvaluator resourceScopeEvaluator)
    {
        ArgumentNullException.ThrowIfNull(resourceScopeEvaluator);
        
        _resourceScopeEvaluator = resourceScopeEvaluator;
    }

    public AuthorizationDecision Authorize(Grant grant, AuthorizationRequest request, DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(request);
        
        if (grant.AgentId != request.AgentId)
        {
            return AuthorizationDecision.Deny(AuthorizationReason.AgentMismatch);
        }

        if (grant.WorkflowId != request.WorkflowId)
        {
            return AuthorizationDecision.Deny(AuthorizationReason.WorkflowMismatch);
        }

        if (!grant.AllowedActions.Contains(request.Action))
        {
            return AuthorizationDecision.Deny(AuthorizationReason.ActionNotAllowed);
        }

        if (!_resourceScopeEvaluator.ContainsResource(grant.Scope, request.ResourceId))
        {
            return AuthorizationDecision.Deny(AuthorizationReason.ResourceOutOfScope);
        }

        if (currentTime < grant.ValidFrom)
        {
            return AuthorizationDecision.Deny(AuthorizationReason.GrantNotYetValid);
        }

        if (currentTime >= grant.ExpiresAt)
        {
            return AuthorizationDecision.Deny(AuthorizationReason.GrantExpired);
        }

        return AuthorizationDecision.Allow();
    }
}
