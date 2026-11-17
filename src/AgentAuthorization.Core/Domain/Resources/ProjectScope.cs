namespace AgentAuthorization.Core.Domain.Resources;

public sealed record ProjectScope : ResourceScope
{
    public Guid ProjectId { get; }

    public ProjectScope(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project ID cannot be empty.", nameof(projectId));
        }

        ProjectId = projectId;
    }
}
