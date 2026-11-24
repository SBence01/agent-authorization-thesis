using AgentAuthorization.Core.Application.Authorization;
using AgentAuthorization.Core.Application.Grants;
using AgentAuthorization.Core.Domain.Grants;
using AgentAuthorization.Core.Domain.Resources;
using AgentAuthorization.Demo;
using AgentAuthorization.Infrastructure.Auditing;
using AgentAuthorization.Infrastructure.Grants;
using System.Collections.Immutable;
using System.Text.Json;

var agentIds = new[]
{
    Guid.Parse("00000000-0000-0000-0000-000000000001"),
    Guid.Parse("00000000-0000-0000-0000-000000000002")
};

var workflowId = Guid.Parse("00000000-0000-0000-0000-000000000101");

var projectIds = new[]
{
    Guid.Parse("00000000-0000-0000-0000-000000000201"),
    Guid.Parse("00000000-0000-0000-0000-000000000202")
};

var resourceIds = new[]
{
    Guid.Parse("00000000-0000-0000-0000-000000000301"),
    Guid.Parse("00000000-0000-0000-0000-000000000302"),
    Guid.Parse("00000000-0000-0000-0000-000000000303")
};

var actions = new[]
{
    ResourceAction.Read,
    ResourceAction.Write,
    ResourceAction.Delete
};

var now = DateTimeOffset.UtcNow;
var store = new InMemoryGrantStore();
var grants = new List<Grant>();
var agents = Array.Empty<DemoAgent>();
var logPath = "";

DemoResourceOperationExecutor executor = null!;
GrantDelegationValidator validator = null!;
GrantDelegationService delegation = null!;

Reset();

while (true)
{
    ShowPage("AGENT AUTHORIZATION DEMO");

    Console.WriteLine("  1. Execute an operation");
    Console.WriteLine("  2. Delegate permissions");
    Console.WriteLine("  3. Revoke a grant");
    Console.WriteLine();
    Console.WriteLine("  4. View current state");
    Console.WriteLine("  5. View audit log");
    Console.WriteLine();
    Console.WriteLine("  6. Advance simulation time");
    Console.WriteLine("  7. Reset demo");
    Console.WriteLine();
    Console.WriteLine("  0. Exit");
    Console.WriteLine();

    var choice = Ask("Select an option: ", 7);

    if (choice == 0)
        break;

    try
    {
        switch (choice)
        {
            case 1:
                ExecuteOperation();
                break;

            case 2:
                DelegateGrant();
                break;

            case 3:
                RevokeGrant();
                break;

            case 4:
                ShowState();
                break;

            case 5:
                ShowAudit();
                break;

            case 6:
                ShowPage("ADVANCE SIMULATION TIME");
                Console.WriteLine("Time advances only when you change it here.");
                Console.WriteLine();

                var minutes = Ask("Minutes to advance (1-1440, 0 = cancel): ", 1440);

                if (minutes > 0)
                {
                    now = now.AddMinutes(minutes);
                    Console.WriteLine($"\nNew time: {now:yyyy-MM-dd HH:mm:ss} UTC");
                }
                break;

            case 7:
                ShowPage("RESET DEMO");
                Console.WriteLine("Resources, grants and simulation time will reset.");
                Console.WriteLine("A new audit file will be used. Previous files are kept.");
                Console.WriteLine();

                if (Ask("1 = reset, 0 = cancel: ", 1) == 1)
                {
                    Reset();
                    Console.WriteLine("\nDemo reset. Agent A has the initial grant.");
                }
                break;
        }
    }
    catch (Exception ex) when (
        ex is IOException or UnauthorizedAccessException or
        JsonException or ArgumentException or
        InvalidOperationException or KeyNotFoundException)
    {
        Console.WriteLine($"\nERROR: {ex.Message}");
    }

    if (!Console.IsInputRedirected)
    {
        Console.Write("\nPress Enter to return to the menu...");
        Console.ReadLine();
    }
}

void Reset()
{
    now = DateTimeOffset.UtcNow;
    store = new InMemoryGrantStore();

    var catalog = new ResourceCatalog(
        ImmutableDictionary<Guid, Guid>.Empty
            .Add(resourceIds[0], projectIds[0])
            .Add(resourceIds[1], projectIds[0])
            .Add(resourceIds[2], projectIds[1]));

    var evaluator = new ResourceScopeEvaluator(catalog);

    validator = new GrantDelegationValidator(evaluator);
    delegation = new GrantDelegationService(store, validator);

    var authorization = new AuthorizationService(
        store,
        new AuthorizationEngine(evaluator),
        validator);

    executor = new DemoResourceOperationExecutor(resourceIds.ToImmutableDictionary(id => id, _ => 0));

    var directory = Path.Combine(AppContext.BaseDirectory, "logs");
    Directory.CreateDirectory(directory);

    logPath = Path.Combine(directory, $"audit-{Guid.NewGuid():N}.jsonl");

    var pep = new PolicyEnforcementPoint(
        authorization,
        executor,
        new JsonLinesAuthorizationAuditSink(logPath));

    agents = agentIds
        .Select(id => new DemoAgent(id, workflowId, pep, () => now))
        .ToArray();

    var root = new Grant(
        grantId: Guid.NewGuid(),
        agentId: agentIds[0],
        workflowId: workflowId,
        allowedActions: ImmutableHashSet.Create(
            ResourceAction.Read,
            ResourceAction.Write),
        scope: new ProjectScope(projectIds[0]),
        validFrom: now,
        expiresAt: now.AddHours(1),
        canDelegate: true);

    store.AddGrant(root);
    grants = new List<Grant> { root };
}

void ShowPage(string title)
{
    if (!Console.IsOutputRedirected)
        Console.Clear();

    Console.WriteLine(title);
    Console.WriteLine();
    Console.WriteLine($"Simulation time: {now:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine();
}

void ExecuteOperation()
{
    ShowPage("EXECUTE AN OPERATION");

    var agent = Ask("Agent: 1 = A, 2 = B, 0 = cancel: ", 2);
    if (agent == 0) return;

    Console.WriteLine();

    var action = Ask("Action: 1 = Read, 2 = Write, 3 = Delete, 0 = cancel: ", 3);
    if (action == 0) return;

    Console.WriteLine("  Resources: R1 and R2 belong to P1; R3 belongs to P2.");
    Console.WriteLine();

    var resource = Ask("Resource: 1 = R1, 2 = R2, 3 = R3, 0 = cancel: ", 3);
    if (resource == 0) return;

    var id = resourceIds[resource - 1];
    var versionBefore = Version(id);
    var executionsBefore = executor.ExecutionCount;

    Console.WriteLine();
    Console.WriteLine($"Request: Agent {AgentName(agentIds[agent - 1])} / {actions[action - 1]} / R{resource}");
    Console.WriteLine();

    try
    {
        var decision = agents[agent - 1].Execute(actions[action - 1], id);

        Console.WriteLine();
        Console.WriteLine($"Decision:  {(decision.IsAllowed ? "ALLOW" : "DENY")}");
        Console.WriteLine($"Reason:    {decision.Reason}");
        Console.WriteLine($"Execution: {(decision.IsAllowed ? "Completed" : "Blocked")}");
    }
    finally
    {
        Console.WriteLine();
        Console.WriteLine($"R{resource} version:       {versionBefore} -> {Version(id)}");
        Console.WriteLine($"Successful executions: {executionsBefore} -> {executor.ExecutionCount}");
    }
}

void DelegateGrant()
{
    ShowPage("DELEGATE PERMISSIONS");

    ShowGrants();

    var owner = Ask("Delegating agent: 1 = A, 2 = B, 0 = cancel: ", 2);
    if (owner == 0) return;

    var source = Ask($"Source grant number (1-{grants.Count}, 0 = cancel): ", grants.Count);
    if (source == 0) return;

    var parent = grants[source - 1];

    Console.WriteLine();
    Console.WriteLine($"Source:       G{source}, owned by Agent {AgentName(parent.AgentId)}");
    Console.WriteLine($"Actions:      {ActionNames(parent)}");
    Console.WriteLine($"Scope:        {ScopeName(parent.Scope)}");
    Console.WriteLine($"Status:       {Status(parent)}");
    Console.WriteLine($"Can delegate: {(parent.CanDelegate ? "Yes" : "No")}");
    Console.WriteLine($"Expires:      {parent.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine();

    var recipient = Ask("Recipient: 1 = A, 2 = B, 0 = cancel: ", 2);
    if (recipient == 0) return;

    Console.WriteLine();

    var selectedActions = AskMany("Actions: 1 = Read, 2 = Write, 3 = Delete", 3);
    if (selectedActions.Length == 0) return;

    Console.WriteLine();

    var scopeType = Ask("Scope: 1 = resources, 2 = project, 0 = cancel: ", 2);
    if (scopeType == 0) return;

    ResourceScope scope;

    if (scopeType == 1)
    {
        var selectedResources = AskMany("Resources: 1 = R1, 2 = R2, 3 = R3", 3);
        if (selectedResources.Length == 0) return;

        scope = new ResourceSetScope(
            selectedResources
                .Select(i => resourceIds[i - 1])
                .ToImmutableHashSet());
    }
    else
    {
        var project = Ask("Project: 1 = P1, 2 = P2, 0 = cancel: ", 2);
        if (project == 0) return;

        scope = new ProjectScope(projectIds[project - 1]);
    }

    Console.WriteLine();

    var minutes = Ask("Validity from now, in minutes (1-1440, 0 = cancel): ", 1440);
    if (minutes == 0) return;

    var child = new Grant(
        grantId: Guid.NewGuid(),
        agentId: agentIds[recipient - 1],
        workflowId: workflowId,
        allowedActions: selectedActions
            .Select(i => actions[i - 1])
            .ToImmutableHashSet(),
        scope: scope,
        validFrom: now,
        expiresAt: now.AddMinutes(minutes),
        parentGrantId: parent.GrantId);

    Console.WriteLine();

    if (delegation.TryDelegateGrant(child, agentIds[owner - 1], now))
    {
        grants.Add(child);

        Console.WriteLine($"ACCEPTED: G{grants.Count} created for Agent {AgentName(child.AgentId)}.");
        Console.WriteLine($"Permissions: {ActionNames(child)} / {ScopeName(child.Scope)}");
        Console.WriteLine($"Expires: {child.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");
    }
    else
    {
        Console.WriteLine("REJECTED: no grant was created.");
        Console.WriteLine("The request must satisfy the source grant's delegation rules.");
    }
}

void RevokeGrant()
{
    ShowPage("REVOKE A GRANT");
    ShowGrants();

    Console.WriteLine("Revoking a parent also blocks access through its child grants.");
    Console.WriteLine();

    var number = Ask($"Grant number (1-{grants.Count}, 0 = cancel): ", grants.Count);
    if (number == 0) return;

    Console.WriteLine();

    if (store.RevokeGrant(grants[number - 1].GrantId))
        Console.WriteLine($"G{number} is revoked.");
    else
        Console.WriteLine("Revocation failed: grant not found.");
}

void ShowState()
{
    ShowPage("CURRENT STATE");

    Console.WriteLine("Resources");
    Console.WriteLine();

    for (var i = 0; i < resourceIds.Length; i++)
    {
        var project = i < 2 ? "P1" : "P2";
        Console.WriteLine($"  R{i + 1}   Project {project}   Version: {Version(resourceIds[i])}");
    }

    Console.WriteLine();
    Console.WriteLine($"Successful executions: {executor.ExecutionCount}");
    Console.WriteLine();
    Console.WriteLine("Grants");
    Console.WriteLine();

    ShowGrants();

    Console.WriteLine("ACTIVE describes grant validity; each request is checked separately.");
    Console.WriteLine("Agent selection and revocation are demo controller actions.");
}

void ShowGrants()
{
    for (var i = 0; i < grants.Count; i++)
    {
        var grant = grants[i];
        var parentIndex = grants.FindIndex(g => g.GrantId == grant.ParentGrantId);
        var parentName = grant.ParentGrantId is null
            ? "none"
            : parentIndex >= 0 ? $"G{parentIndex + 1}" : "missing";

        Console.WriteLine($"  G{i + 1} | Agent {AgentName(grant.AgentId)} | {Status(grant)}");
        Console.WriteLine($"       {ActionNames(grant)} / {ScopeName(grant.Scope)}");
        Console.WriteLine($"       Valid: {grant.ValidFrom:MM-dd HH:mm:ss} - {grant.ExpiresAt:MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"       Parent: {parentName} | Can delegate: {(grant.CanDelegate ? "Yes" : "No")}");
        Console.WriteLine();
    }
}

void ShowAudit()
{
    ShowPage("AUTHORIZATION AUDIT");

    Console.WriteLine("Operation decisions only. ALLOW does not confirm execution success.");
    Console.WriteLine();

    if (!File.Exists(logPath))
    {
        Console.WriteLine("No entries yet.");
        return;
    }

    var count = 0;

    foreach (var line in File.ReadLines(logPath))
    {
        using var document = JsonDocument.Parse(line);

        var root = document.RootElement;
        var request = root.GetProperty("Request");
        var decision = root.GetProperty("Decision");
        var timestamp = root.GetProperty("Timestamp").GetDateTimeOffset();
        var verdict = decision.GetProperty("IsAllowed").GetBoolean() ? "ALLOW" : "DENY";

        Console.WriteLine(
            $"  {++count}. {timestamp:MM-dd HH:mm:ss} | " +
            $"Agent {AgentName(request.GetProperty("AgentId").GetGuid())} | " +
            $"{request.GetProperty("Action").GetString()} " +
            $"{ResourceName(request.GetProperty("ResourceId").GetGuid())}");

        Console.WriteLine($"     {verdict} | {decision.GetProperty("Reason").GetString()}");
        Console.WriteLine();
    }

    Console.WriteLine($"File: {logPath}");
}

string Status(Grant grant)
{
    if (grant.IsRevoked) return "REVOKED";
    if (now < grant.ValidFrom) return "NOT YET VALID";
    if (now >= grant.ExpiresAt) return "EXPIRED";

    if (grant.ParentGrantId is Guid parentId)
    {
        var parent = store.GetById(parentId);

        if (parent is null || !validator.CanDelegate(parent, grant, parent.AgentId, now))
        {
            return "PARENT BLOCKED";
        }
    }

    return "ACTIVE";
}

string AgentName(Guid id) => id == agentIds[0] ? "A" : id == agentIds[1] ? "B" : "?";

string ResourceName(Guid id) => $"R{Array.IndexOf(resourceIds, id) + 1}";

string Version(Guid id) => executor.GetVersion(id)?.ToString() ?? "missing";

string ActionNames(Grant grant) => string.Join(", ", grant.AllowedActions.OrderBy(action => action));

string ScopeName(ResourceScope scope) => scope switch
{
    ProjectScope project => $"Project P{Array.IndexOf(projectIds, project.ProjectId) + 1}",

    ResourceSetScope set => string.Join(", ", set.ResourceIds.OrderBy(id => id).Select(ResourceName)),

    _ => "Unknown"
};

static int Ask(string prompt, int maximum)
{
    while (true)
    {
        Console.Write(prompt);

        var input = Console.ReadLine();
        if (input is null) return 0;

        if (int.TryParse(input, out var value) && value >= 0 && value <= maximum)
            return value;

        Console.WriteLine($"Enter a number from 0 to {maximum}.");
    }
}

static int[] AskMany(string description, int maximum)
{
    Console.WriteLine(description);
    Console.WriteLine("Separate numbers with commas. Enter 0 to cancel.");

    while (true)
    {
        Console.Write("Selection: ");

        var input = Console.ReadLine();

        if (input is null || input.Trim() == "0")
            return Array.Empty<int>();

        var values = input.Split(',')
            .Select(part => int.TryParse(part.Trim(), out var value) ? value : -1)
            .ToArray();

        if (values.All(value => value >= 1 && value <= maximum))
            return values.Distinct().OrderBy(value => value).ToArray();

        Console.WriteLine($"Use numbers from 1 to {maximum}, separated by commas.");
    }
}