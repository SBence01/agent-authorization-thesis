using AgentAuthorization.Core.Application.Authorization;
using AgentAuthorization.Core.Domain.Authorization;
using AgentAuthorization.Core.Domain.Resources;

namespace AgentAuthorization.Demo;

internal sealed class DemoAgent
{
    private readonly Guid _agentId;
    private readonly Guid _workflowId;
    private readonly PolicyEnforcementPoint _pep;
    private readonly Func<DateTimeOffset> _clock;

    public DemoAgent(Guid agentId, Guid workflowId, PolicyEnforcementPoint pep, Func<DateTimeOffset> clock)
    {
        if (agentId == Guid.Empty || workflowId == Guid.Empty)
        {
            throw new ArgumentException("Agent ID or workflow ID cannot be empty");
        }

        ArgumentNullException.ThrowIfNull(pep);
        ArgumentNullException.ThrowIfNull(clock);

        _agentId = agentId;
        _workflowId = workflowId;
        _pep = pep;
        _clock = clock;
    }

    public AuthorizationDecision Execute(ResourceAction action, Guid resourceId)
    {
        var request = new AuthorizationRequest(_agentId, action, resourceId, _workflowId);

        var currentTime = _clock();

        return _pep.Execute(request, currentTime);
    }
}
