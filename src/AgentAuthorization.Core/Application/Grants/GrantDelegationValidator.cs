using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;

namespace AgentAuthorization.Core.Application.Grants;

public sealed class GrantDelegationValidator
{
    private readonly ResourceScopeEvaluator _evaluator;

    public GrantDelegationValidator(ResourceScopeEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        _evaluator = evaluator;
    }

    public bool CanDelegate(Grant parent, Grant child, Guid delegatingAgentId, DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (parent.AgentId != delegatingAgentId)
        {
            return false;
        }

        if (!parent.CanDelegate)
        {
            return false;
        }

        if (parent.ParentGrantId != null)
        {
            return false;
        }

        if (child.ParentGrantId != parent.GrantId)
        {
            return false;
        }

        if (parent.WorkflowId != child.WorkflowId)
        {
            return false;
        }

        if (!child.AllowedActions.IsSubsetOf(parent.AllowedActions))
        {
            return false;
        }

        if (child.ValidFrom < parent.ValidFrom)
        {
            return false;
        }

        if (child.ExpiresAt > parent.ExpiresAt)
        {
            return false;
        }

        if (parent.IsRevoked)
        {
            return false;
        }

        if (!_evaluator.IsSubsetOf(child.Scope, parent.Scope))
        {
            return false;
        }    

        if (currentTime < parent.ValidFrom || currentTime >= parent.ExpiresAt)
        {
            return false;
        }

        if (child.IsRevoked)
        {
            return false;
        }

        return true;
    }
}
