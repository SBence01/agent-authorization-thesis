using AgentAuthorization.Core.Application.Auditing;
using AgentAuthorization.Core.Application.Authorization;
using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Application.Resources;
using AgentAuthorization.Core.Domain.Authorization;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using AgentAuthorization.Infrastructure.Auditing;
using AgentAuthorization.Infrastructure.Grants;
using System.Collections.Immutable;
using System.Text.Json;

namespace AgentAuthorization.Core.Tests.Infrastructure.Auditing;

public sealed class JsonlinesAuthorizationAuditSinkTests : IDisposable
{
    private static readonly Guid GrantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid AgentId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid WorkflowId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    private static readonly Guid ResourceId = Guid.Parse("00000000-0000-0000-0000-000000000301");

    private static readonly DateTimeOffset Timestamp = new(2026, 11, 22, 10, 30, 0, TimeSpan.Zero);

    private readonly string _directory;
    private readonly string _filePath;

    public JsonlinesAuthorizationAuditSinkTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AgentAuthorization.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_directory);

        _filePath = Path.Combine(_directory, "authorization.jsonl");
    }

    [Fact]
    public void Constructor_WithNullPath_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonLinesAuthorizationAuditSink(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_WithBlankPath_ShouldThrow(string filePath)
    {
        Assert.Throws<ArgumentException>(() => new JsonLinesAuthorizationAuditSink(filePath));
    }

    [Fact]
    public void Write_WithNullEntry_ShouldThrowWithoutCreatingFile()
    {
        //Arrange
        var sink = new JsonLinesAuthorizationAuditSink(_filePath);

        //Act
        Assert.Throws<ArgumentNullException>(() => sink.Write(null!));

        //Assert
        Assert.False(File.Exists(_filePath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Write_ShouldPersistCompleteEntryAsSingleJsonLine(bool allowed)
    {
        //Arrange
        var sink = new JsonLinesAuthorizationAuditSink(_filePath);
        var entry = CreateEntry(allowed, Timestamp);

        Assert.False(File.Exists(_filePath));

        //Act
        sink.Write(entry);

        //Assert
        var line = Assert.Single(File.ReadAllLines(_filePath));

        AssertJsonEntry(line, entry);
        Assert.EndsWith(Environment.NewLine, File.ReadAllText(_filePath));
    }

    [Fact]
    public void Write_WithNewSinkInstance_ShouldAppendAndPreserveEarlierEntry()
    {
        //Arrange
        var firstEntry = CreateEntry(true, Timestamp);
        var secondEntry = CreateEntry(false, Timestamp.AddMinutes(1));

        var firstSink = new JsonLinesAuthorizationAuditSink(_filePath);
        firstSink.Write(firstEntry);

        var originalContent = File.ReadAllText(_filePath);

        //Act
        var secondSink = new JsonLinesAuthorizationAuditSink(_filePath);
        secondSink.Write(secondEntry);

        //Assert
        var lines = File.ReadAllLines(_filePath);

        Assert.Equal(2, lines.Length);
        AssertJsonEntry(lines[0], firstEntry);
        AssertJsonEntry(lines[1], secondEntry);

        Assert.StartsWith(originalContent, File.ReadAllText(_filePath));
    }

    [Fact]
    public void Write_WithMissingDirectory_ShouldPropagateFailure()
    {
        //Arrange
        var missingDirectory = Path.Combine(_directory, "missing");
        var filePath = Path.Combine(missingDirectory, "authorization.jsonl");

        var sink = new JsonLinesAuthorizationAuditSink(filePath);

        //Act
        Assert.Throws<DirectoryNotFoundException>(() => sink.Write(CreateEntry(true, Timestamp)));

        //Assert
        Assert.False(Directory.Exists(missingDirectory));
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void Pep_WhenFileAuditFails_ShouldNotExecuteAllowedOperation()
    {
        //Arrange
        var store = new InMemoryGrantStore();

        store.AddGrant(new Grant(
            GrantId,
            AgentId,
            WorkflowId,
            ImmutableHashSet.Create(ResourceAction.Read),
            new ResourceSetScope(
                ImmutableHashSet.Create(ResourceId)),
            Timestamp.AddHours(-1),
            Timestamp.AddHours(1)));

        var evaluator = new ResourceScopeEvaluator(new ResourceCatalog(ImmutableDictionary<Guid, Guid>.Empty));

        var authorizationService = new AuthorizationService(
            store,
            new AuthorizationEngine(evaluator),
            new GrantDelegationValidator(evaluator));

        var filePath = Path.Combine(
            _directory,
            "missing",
            "authorization.jsonl");

        var sink = new JsonLinesAuthorizationAuditSink(filePath);
        var executor = new CountingExecutor();

        var pep = new PolicyEnforcementPoint(
            authorizationService,
            executor,
            sink);

        var request = CreateEntry(true, Timestamp).Request;

        Assert.True(authorizationService.Authorize(request, Timestamp).IsAllowed);

        //Act
        Assert.Throws<DirectoryNotFoundException>(() => pep.Execute(request, Timestamp));

        //Assert
        Assert.Equal(0, executor.CallCount);
        Assert.False(File.Exists(filePath));
    }

    private static AuthorizationAuditEntry CreateEntry(bool allowed, DateTimeOffset timestamp)
    {
        var request = new AuthorizationRequest(
            AgentId,
            ResourceAction.Read,
            ResourceId,
            WorkflowId);

        var decision = allowed
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny(
                AuthorizationReason.NoMatchingGrant);

        return new AuthorizationAuditEntry(
            timestamp,
            request,
            decision);
    }

    private static void AssertJsonEntry(string json, AuthorizationAuditEntry expected)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(expected.Timestamp, root.GetProperty("Timestamp").GetDateTimeOffset());

        var request = root.GetProperty("Request");

        Assert.Equal(expected.Request.AgentId, request.GetProperty("AgentId").GetGuid());

        Assert.Equal(expected.Request.WorkflowId, request.GetProperty("WorkflowId").GetGuid());

        Assert.Equal(expected.Request.ResourceId, request.GetProperty("ResourceId").GetGuid());

        Assert.Equal(expected.Request.Action.ToString(), request.GetProperty("Action").GetString());

        var decision = root.GetProperty("Decision");

        Assert.Equal(expected.Decision.IsAllowed, decision.GetProperty("IsAllowed").GetBoolean());

        Assert.Equal(expected.Decision.Reason.ToString(), decision.GetProperty("Reason").GetString());
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private sealed class CountingExecutor : IResourceOperationExecutor
    {
        public int CallCount { get; private set; }

        public void Execute(AuthorizationRequest request)
        {
            CallCount++;
        }
    }
}
