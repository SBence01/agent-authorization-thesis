using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Core.Domain.Grants
{
    public class Grant
    {
        public Guid GrantId { get; }
        public Guid AgentId { get; }
        public Guid WorkflowId { get; }
        public ImmutableHashSet<ResourceAction> AllowedActions { get; }
        public ResourceScope Scope { get; }
        public DateTimeOffset ValidFrom { get; }

        public DateTimeOffset ExpiresAt { get; }

        public bool IsRevoked { get; private set; }

        public Grant(Guid grantId, Guid agentId, Guid workflowId, ImmutableHashSet<ResourceAction> allowedActions, ResourceScope scope, DateTimeOffset validFrom, DateTimeOffset expiresAt)
        {
            if (grantId == Guid.Empty)
            {
                throw new ArgumentException("Grant ID cannot be empty.", nameof(grantId));
            }

            if (agentId == Guid.Empty)
            {
                throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));
            }

            if (workflowId == Guid.Empty)
            {
                throw new ArgumentException("Workflow ID cannot be empty.", nameof(workflowId));
            }

            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope), "Scope cannot be null.");
            }

            if (allowedActions is null || allowedActions.Count == 0)
            {
                throw new ArgumentException("Allowed actions cannot be null or empty.", nameof(allowedActions));
            }

            if (allowedActions.Any(action => !Enum.IsDefined(action)))
            {
                throw new ArgumentException("Allowed actions must contain only defined resource actions.", nameof(allowedActions));
            }

            if (expiresAt <= validFrom)
            {
                throw new ArgumentException("Expiration date cannot be before or equal to valid from date.");
            }

            GrantId = grantId;
            AgentId = agentId;
            WorkflowId = workflowId;
            AllowedActions = allowedActions;
            Scope = scope;
            ValidFrom = validFrom;
            ExpiresAt = expiresAt;
            IsRevoked = false;
        }

        public void Revoke()
        {
            IsRevoked = true;
        }
    }
}
