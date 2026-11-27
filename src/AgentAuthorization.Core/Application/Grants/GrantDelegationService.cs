using AgentAuthorization.Core.Domain.Grants;

namespace AgentAuthorization.Core.Application.Grants;

public sealed class GrantDelegationService
{
    private readonly IGrantStore _grantStore;
    private readonly GrantDelegationValidator _validator;

    public GrantDelegationService(IGrantStore grantStore, GrantDelegationValidator validator)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(validator);

        _grantStore = grantStore;
        _validator = validator;
    }

    public bool TryDelegateGrant(Grant child, Guid delegatingAgentId, DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.ParentGrantId == null)
        {
            return false;
        }

        if (_grantStore.GetById(child.GrantId) is not null)
        {
            return false;
        }

        //Validate against the stored parent rather than a parent supplied by the caller
        var parent = _grantStore.GetById(child.ParentGrantId.Value);
        if (parent is null)
        {
            return false;
        }

        if (!_validator.CanDelegate(parent, child, delegatingAgentId, currentTime))
        {
            return false;
        }

        //Persist the child only after all delegation checks have succeeded
        _grantStore.AddGrant(child);
        return true;
    }
}
