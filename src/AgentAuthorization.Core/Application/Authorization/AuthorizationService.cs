using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Authorization;

namespace AgentAuthorization.Core.Application.Authorization;

public sealed class AuthorizationService
{
    private readonly IGrantStore _grantStore;

    private readonly AuthorizationEngine _authorizationEngine;
    public AuthorizationService(IGrantStore grantStore, AuthorizationEngine authorizationEngine)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(authorizationEngine);

        _grantStore = grantStore;
        _authorizationEngine = authorizationEngine;
    }

    public AuthorizationDecision Authorize(AuthorizationRequest request, DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(request);

        var grants = _grantStore.GetByAgentId(request.AgentId);

        foreach (var grant in grants)
        {
            var decision = _authorizationEngine.Authorize(grant, request, currentTime);

            if (decision.IsAllowed)
            {
                return decision;
            }
        }

        return AuthorizationDecision.Deny(AuthorizationReason.NoMatchingGrant);
    }
}
