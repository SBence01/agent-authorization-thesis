using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Authorization;

namespace AgentAuthorization.Core.Application.Authorization;

public sealed class AuthorizationService
{
    private readonly IGrantStore _grantStore;

    private readonly AuthorizationEngine _authorizationEngine;

    private readonly GrantDelegationValidator _delegationValidator;

    public AuthorizationService(IGrantStore grantStore, AuthorizationEngine authorizationEngine, GrantDelegationValidator delegationValidator)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(authorizationEngine);
        ArgumentNullException.ThrowIfNull(delegationValidator);

        _grantStore = grantStore;
        _authorizationEngine = authorizationEngine;
        _delegationValidator = delegationValidator;
    }

    public AuthorizationDecision Authorize(AuthorizationRequest request, DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(request);

        var grants = _grantStore.GetByAgentId(request.AgentId);

        //any independently sufficient grant may authorize the request
        //permissions from different grants are not combined
        foreach (var grant in grants)
        {
            var decision = _authorizationEngine.Authorize(grant, request, currentTime);

            //a rejected grant must not prevent another grant from authorizing request
            if (!decision.IsAllowed)
            {
                continue;
            }

            if (grant.ParentGrantId is null)
            {
                return decision;
            }

            //revalidate the parent on every delegated request
            //parent revocation or expiry blocks access without modifying the child grant
            var parent = _grantStore.GetById(grant.ParentGrantId.Value);
            if (parent is null)
            {
                continue;
            }

            //here the validator checks the stored parent-child relationship
            //passing the parent's owner ID does not authenticate a new delegation request
            if (!_delegationValidator.CanDelegate(parent, grant, parent.AgentId, currentTime))
            {
                continue;
            }

            return decision;
        }

        return AuthorizationDecision.Deny(AuthorizationReason.NoMatchingGrant);
    }
}
