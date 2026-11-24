namespace AgentAuthorization.Core.Application.Auditing;

public interface IAuthorizationAuditSink
{
    void Write(AuthorizationAuditEntry entry);
}
