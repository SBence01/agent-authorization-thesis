using AgentAuthorization.Core.Application.Auditing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentAuthorization.Infrastructure.Auditing;

public sealed class JsonLinesAuthorizationAuditSink : IAuthorizationAuditSink
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonLinesAuthorizationAuditSink(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = filePath;
    }

    public void Write(AuthorizationAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var json = JsonSerializer.Serialize(entry, _serializerOptions);

        File.AppendAllText(_filePath, json + Environment.NewLine);
    }
}
