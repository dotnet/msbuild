# Subtask 8: BuildProjectFile Implementation (TaskHost Side)

**Parent:** [taskhost-callbacks-implementation-plan.md](./taskhost-callbacks-implementation-plan.md)
**Phase:** 2
**Status:** ✅ Complete
**Dependencies:** Subtasks 5 (Task Context), 7 (BuildProjectFile Packets)

---

## Objective

Implement the `BuildProjectFile*` methods in `OutOfProcTaskHostNode` to forward build requests to the parent process and block until results are returned.

---

## Current Behavior

**File:** `src/MSBuild/OutOfProcTaskHostNode.cs` (lines 363-405)

```csharp
public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
{
    LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
    return false;
}

public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
{
    LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
    return false;
}

public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
{
    LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
    return false;
}

public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs)
{
    LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
    return new BuildEngineResult(false, null);
}
```

---

## Implementation Steps

### Step 1: Implement IBuildEngine.BuildProjectFile (4 params)

**File:** `src/MSBuild/OutOfProcTaskHostNode.cs`

```csharp
/// <summary>
/// Implementation of IBuildEngine.BuildProjectFile.
/// Forwards the request to the parent process and blocks until the result is returned.
/// </summary>
public bool BuildProjectFile(
    string projectFileName, 
    string[] targetNames, 
    IDictionary globalProperties, 
    IDictionary targetOutputs)
{
    var request = new TaskHostBuildRequest(projectFileName, targetNames, globalProperties);
    var response = SendRequestAndWaitForResponse<TaskHostBuildResponse>(request);
    
    // Copy outputs back to the caller's dictionary
    if (targetOutputs != null && response.OverallResult)
    {
        CopyTargetOutputs(response.GetTargetOutputs(), targetOutputs);
    }
    
    return response.OverallResult;
}
```

### Step 2: Implement IBuildEngine2.BuildProjectFile (5 params)

```csharp
/// <summary>
/// Implementation of IBuildEngine2.BuildProjectFile.
/// Forwards the request to the parent process and blocks until the result is returned.
/// </summary>
public bool BuildProjectFile(
    string projectFileName, 
    string[] targetNames, 
    IDictionary globalProperties, 
    IDictionary targetOutputs, 
    string toolsVersion)
{
    var request = new TaskHostBuildRequest(
        projectFileName, 
        targetNames, 
        globalProperties, 
        toolsVersion);
    var response = SendRequestAndWaitForResponse<TaskHostBuildResponse>(request);
    
    // Copy outputs back to the caller's dictionary
    if (targetOutputs != null && response.OverallResult)
    {
        CopyTargetOutputs(response.GetTargetOutputs(), targetOutputs);
    }
    
    return response.OverallResult;
}
```

### Step 3: Implement IBuildEngine2.BuildProjectFilesInParallel (7 params)

```csharp
/// <summary>
/// Implementation of IBuildEngine2.BuildProjectFilesInParallel.
/// Forwards the request to the parent process and blocks until results are returned.
/// </summary>
public bool BuildProjectFilesInParallel(
    string[] projectFileNames, 
    string[] targetNames, 
    IDictionary[] globalProperties, 
    IDictionary[] targetOutputsPerProject, 
    string[] toolsVersion, 
    bool useResultsCache, 
    bool unloadProjectsOnCompletion)
{
    var request = new TaskHostBuildRequest(
        projectFileNames,
        targetNames,
        globalProperties,
        toolsVersion,
        useResultsCache,
        unloadProjectsOnCompletion);
    var response = SendRequestAndWaitForResponse<TaskHostBuildResponse>(request);
    
    // Copy outputs back to caller's dictionaries
    if (targetOutputsPerProject != null && response.OverallResult)
    {
        var outputs = response.GetTargetOutputsPerProject();
        if (outputs != null)
        {
            for (int i = 0; i < Math.Min(targetOutputsPerProject.Length, outputs.Count); i++)
            {
                if (targetOutputsPerProject[i] != null && outputs[i] != null)
                {
                    CopyTargetOutputs(outputs[i], targetOutputsPerProject[i]);
                }
            }
        }
    }
    
    return response.OverallResult;
}
```

### Step 4: Implement IBuildEngine3.BuildProjectFilesInParallel (6 params)

```csharp
/// <summary>
/// Implementation of IBuildEngine3.BuildProjectFilesInParallel.
/// Forwards the request to the parent process and blocks until results are returned.
/// Returns a BuildEngineResult with target outputs.
/// </summary>
public BuildEngineResult BuildProjectFilesInParallel(
    string[] projectFileNames, 
    string[] targetNames, 
    IDictionary[] globalProperties, 
    IList<string>[] removeGlobalProperties, 
    string[] toolsVersion, 
    bool returnTargetOutputs)
{
    var request = new TaskHostBuildRequest(
        projectFileNames,
        targetNames,
        globalProperties,
        removeGlobalProperties,
        toolsVersion,
        returnTargetOutputs);
    var response = SendRequestAndWaitForResponse<TaskHostBuildResponse>(request);
    
    IList<IDictionary<string, ITaskItem[]>> targetOutputsPerProject = null;
    
    if (returnTargetOutputs && response.OverallResult)
    {
        targetOutputsPerProject = response.GetTargetOutputsPerProject();
    }
    
    return new BuildEngineResult(response.OverallResult, targetOutputsPerProject);
}
```

### Step 5: Add Helper Method for Copying Outputs

```csharp
/// <summary>
/// Copies target outputs from source dictionary to destination dictionary.
/// </summary>
private static void CopyTargetOutputs(IDictionary source, IDictionary destination)
{
    if (source == null || destination == null)
    {
        return;
    }
    
    foreach (DictionaryEntry entry in source)
    {
        destination[entry.Key] = entry.Value;
    }
}

/// <summary>
/// Copies target outputs from strongly-typed source to IDictionary destination.
/// </summary>
private static void CopyTargetOutputs(
    IDictionary<string, ITaskItem[]> source, 
    IDictionary destination)
{
    if (source == null || destination == null)
    {
        return;
    }
    
    foreach (var kvp in source)
    {
        destination[kvp.Key] = kvp.Value;
    }
}
```

### Step 6: Register Response Packet Handler

In the constructor or initialization:

```csharp
_packetFactory.RegisterPacketHandler(
    NodePacketType.TaskHostBuildResponse, 
    TaskHostBuildResponse.FactoryForDeserialization, 
    this);
```

In `HandlePacket`:

```csharp
case NodePacketType.TaskHostBuildResponse:
    HandleResponsePacket(packet);
    break;
```

### Step 7: Update SendRequestAndWaitForResponse for Build Requests

The `SendRequestAndWaitForResponse` method from subtask 6 already handles environment save/restore, but we may need to update the task state:

```csharp
private TResponse SendRequestAndWaitForResponse<TResponse>(INodePacket request) 
    where TResponse : class, INodePacket
{
    var context = GetCurrentTaskContext();
    
    // ...existing code...
    
    // For build requests, mark as blocked (not yielded)
    if (request is TaskHostBuildRequest)
    {
        context.State = TaskExecutionState.BlockedOnCallback;
    }
    
    // ...rest of implementation...
}
```

---

## Error Handling

### Timeout Handling

If the parent doesn't respond within the timeout:

```csharp
catch (InvalidOperationException ex) when (ex.Message.Contains("Timeout"))
{
    // Log error but let the calling code decide what to do
    LogErrorFromResource("BuildProjectFileCallbackTimeout", projectFileName);
    throw;
}
```

### Parent Crash Handling

If the connection is lost during the callback:

```csharp
if (_nodeEndpoint.LinkStatus != LinkStatus.Active)
{
    LogErrorFromResource("BuildProjectFileCallbackConnectionLost", projectFileName);
    throw new InvalidOperationException(
        ResourceUtilities.GetResourceString("TaskHostCallbackConnectionLost"));
}
```

### Build Failure Handling

Build failures are not errors - they're expected results:

```csharp
// This is fine - just return false
return response.OverallResult;
```

---

## Testing

### Unit Tests

```csharp
[Fact]
public void BuildProjectFile_SingleProject_SendsCorrectRequest()
{
    // Mock the endpoint to capture the sent packet
    var sentPackets = new List<INodePacket>();
    var mockEndpoint = CreateMockEndpoint(sentPackets);
    
    var node = CreateTestNode(mockEndpoint);
    
    // Set up mock response
    SetupMockResponse<TaskHostBuildResponse>(
        node, 
        new TaskHostBuildResponse(1, true, new Hashtable()));
    
    var result = node.BuildProjectFile(
        "test.csproj", 
        new[] { "Build" }, 
        null, 
        null);
    
    result.ShouldBeTrue();
    sentPackets.Count.ShouldBe(1);
    var request = sentPackets[0] as TaskHostBuildRequest;
    request.ShouldNotBeNull();
    request.ProjectFileName.ShouldBe("test.csproj");
    request.TargetNames.ShouldBe(new[] { "Build" });
}

[Fact]
public void BuildProjectFile_WithOutputs_CopiesOutputsToProvidedDictionary()
{
    var node = CreateTestNode();
    
    var responseOutputs = new Hashtable
    {
        { "Build", new ITaskItem[] { new TaskItem("output.dll") } }
    };
    SetupMockResponse<TaskHostBuildResponse>(
        node, 
        new TaskHostBuildResponse(1, true, responseOutputs));
    
    var callerOutputs = new Hashtable();
    var result = node.BuildProjectFile(
        "test.csproj",
        new[] { "Build" },
        null,
        callerOutputs);
    
    result.ShouldBeTrue();
    callerOutputs.ShouldContainKey("Build");
    var items = (ITaskItem[])callerOutputs["Build"];
    items[0].ItemSpec.ShouldBe("output.dll");
}

[Fact]
public void BuildProjectFilesInParallel_IBuildEngine3_ReturnsCorrectResult()
{
    var node = CreateTestNode();
    
    var responseOutputs = new List<IDictionary<string, ITaskItem[]>>
    {
        new Dictionary<string, ITaskItem[]>
        {
            { "Build", new ITaskItem[] { new TaskItem("proj1.dll") } }
        }
    };
    SetupMockResponse<TaskHostBuildResponse>(
        node, 
        new TaskHostBuildResponse(1, true, responseOutputs));
    
    var result = node.BuildProjectFilesInParallel(
        new[] { "proj1.csproj" },
        new[] { "Build" },
        null,
        null,
        null,
        returnTargetOutputs: true);
    
    result.Result.ShouldBeTrue();
    result.TargetOutputsPerProject.ShouldNotBeNull();
    result.TargetOutputsPerProject.Count.ShouldBe(1);
}
```

### Integration Tests

```csharp
[Fact]
public void BuildProjectFile_InTaskHost_SuccessfullyBuildsProject()
{
    // End-to-end test with actual TaskHost process
    // 1. Create a task that calls BuildProjectFile on a sample project
    // 2. Run with MSBUILDFORCEALLTASKSOUTOFPROC=1
    // 3. Verify the nested build succeeds and outputs are available
}

[Fact]
public void BuildProjectFile_RecursiveBuild_Succeeds()
{
    // Test that ProjectA can build ProjectB from TaskHost
    // where ProjectB also runs tasks in TaskHost
}
```

---

## Verification Checklist

- [x] `BuildProjectFile(4 params)` forwards to parent and returns result
- [x] `BuildProjectFile(5 params)` forwards to parent and returns result
- [x] `BuildProjectFilesInParallel(7 params)` forwards to parent and returns result
- [x] `BuildProjectFilesInParallel(6 params)` returns correct `BuildEngineResult`
- [x] Target outputs are correctly copied back to caller's dictionaries
- [x] `ITaskItem` metadata is preserved through the round-trip (via TaskParameter in subtask 7)
- [x] Connection loss is properly detected (via SendCallbackRequestAndWaitForResponse)
- [x] No MSB5022 error logged (removed stub error logging)
- [x] Integration test `BuildProjectFileCallbackWorksInTaskHost` passes
- [ ] Full build `.\build.cmd` passes (integration testing)

---

## Notes

- The blocking wait in `SendCallbackRequestAndWaitForResponse` allows the parent to schedule a new task to the same TaskHost while this one waits
- Output dictionaries provided by the caller are modified in-place (this is the existing IBuildEngine contract)
- The `BuildEngineResult` wrapper for IBuildEngine3 is a value type, so we create a new one from the response
- Error handling should not swallow exceptions - let them propagate to the task so it can decide what to do
- Environment save/restore is only needed for Yield/Reacquire (subtask 10), not for BuildProjectFile callbacks

---

## Implementation Notes (Added During Development)

### Handler Stack for Nested Tasks

When a task in the TaskHost calls `BuildProjectFile`, the parent may send another task to the same TaskHost (to preserve static state sharing). This creates a nested task scenario:

1. **Task1** calls `BuildProjectFile` → blocks waiting for response
2. Parent sends **TaskHostConfiguration** for **Task2** (e.g., a `Message` task triggered by the nested build)
3. **Task2** executes in the same TaskHost process
4. **Task2** completes → sends `TaskHostTaskComplete`
5. **Task1** receives its `TaskHostBuildResponse` and continues

### Key Fix: Packet Factory Routing

A critical bug was discovered and fixed in `NodeProviderOutOfProcTaskHost`:

**Problem:** Each `TaskHostTask` instance registers its own `NodePacketFactory` in `_nodeIdToPacketFactory`. When Task2 connected, it overwrote Task1's factory. When Task2 disconnected, packets were still being routed through Task2's factory (which routes directly to Task2's handler), even though Task1 was now the active handler.

**Solution:** Changed to use a single factory (`NodeProviderOutOfProcTaskHost` itself) for all tasks on a node. All packets now route through `NodeProviderOutOfProcTaskHost.PacketReceived`, which uses the handler stack to dispatch to the correct `TaskHostTask`.

### TaskId Correlation

Added `TaskId` field to `TaskHostTaskComplete` packet to enable proper correlation in nested scenarios. Each task gets a unique ID, and the completion handler verifies the TaskId matches before processing.

