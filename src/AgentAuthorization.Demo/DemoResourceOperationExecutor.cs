using AgentAuthorization.Core.Application.Resources;
using AgentAuthorization.Core.Domain.Authorization;
using AgentAuthorization.Core.Domain.Resources;
using System.Collections.Immutable;

namespace AgentAuthorization.Demo;

internal sealed class DemoResourceOperationExecutor : IResourceOperationExecutor
{
    private readonly Dictionary<Guid, int> _versions;

    public int ExecutionCount { get; private set; }

    public DemoResourceOperationExecutor(ImmutableDictionary<Guid, int> initialVersions)
    {
        ArgumentNullException.ThrowIfNull(initialVersions);

        _versions = new Dictionary<Guid, int>(initialVersions);
    }

    public void Execute(AuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resourceId = request.ResourceId;

        switch (request.Action)
        {
            case ResourceAction.Read:
                Console.WriteLine($"Read: {resourceId}, version {GetExistingVersion(resourceId)}");
                break;

            case ResourceAction.Write:
                var newVersion = GetExistingVersion(resourceId) + 1;
                _versions[resourceId] = newVersion;
                Console.WriteLine($"Write: {resourceId}, new version {newVersion}");
                break;

            case ResourceAction.Delete:
                GetExistingVersion(resourceId);
                _versions.Remove(resourceId);
                Console.WriteLine($"Delete: {resourceId} removed");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Action, "Unknown resource action.");
        }

        ExecutionCount++;
    }

    public int? GetVersion(Guid resourceId)
    {
        return _versions.TryGetValue(resourceId, out var version) ? version : null;
    }

    private int GetExistingVersion(Guid resourceId)
    {
        if (!_versions.TryGetValue(resourceId, out var version))
        {
            throw new KeyNotFoundException($"Resource '{resourceId}' does not exist.");
        }

        return version;
    }
}
