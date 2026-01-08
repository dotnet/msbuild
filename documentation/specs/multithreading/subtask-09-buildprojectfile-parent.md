# Subtask 9: BuildProjectFile Implementation (Parent Side)

**Parent:** [taskhost-callbacks-implementation-plan.md](./taskhost-callbacks-implementation-plan.md)
**Phase:** 2
**Status:** ✅ Complete
**Dependencies:** Subtask 7 (BuildProjectFile Packets)

---

## Objective

Implement the parent-side handling in `TaskHostTask` to receive `TaskHostBuildRequest` packets, forward them to the real `IBuildEngine`, and send back `TaskHostBuildResponse` packets.

---

## Implementation Steps

### Step 1: Register Packet Handler in Constructor

**File:** `src/Build/Instance/TaskFactories/TaskHostTask.cs`

Add to constructor:

```csharp
(this as INodePacketFactory).RegisterPacketHandler(
    NodePacketType.TaskHostBuildRequest, 
    TaskHostBuildRequest.FactoryForDeserialization, 
    this);
```

### Step 2: Add Dispatch in PacketReceived Handler

In the `PacketReceived` method (or equivalent packet dispatch):

```csharp
case NodePacketType.TaskHostBuildRequest:
    HandleBuildRequest((TaskHostBuildRequest)packet);
    break;
```

### Step 3: Implement HandleBuildRequest Method

```csharp
/// <summary>
/// Handles BuildProjectFile* requests from the TaskHost.
/// Forwards the request to the real build engine and sends back the response.
/// </summary>
private void HandleBuildRequest(TaskHostBuildRequest request)
{
    bool result = false;
    IDictionary targetOutputs = null;
    IList<IDictionary<string, ITaskItem[]>> targetOutputsPerProject = null;
    
    try
    {
        switch (request.RequestType)
        {
            case TaskHostBuildRequest.BuildRequestType.SingleProject:
                result = HandleSingleProjectBuild(request, out targetOutputs);
                break;
                
            case TaskHostBuildRequest.BuildRequestType.SingleProjectWithToolsVersion:
                result = HandleSingleProjectBuildWithToolsVersion(request, out targetOutputs);
                break;
                
            case TaskHostBuildRequest.BuildRequestType.MultipleProjectsIBuildEngine2:
                result = HandleMultipleProjectsBuildIBuildEngine2(request, out targetOutputs);
                break;
                
            case TaskHostBuildRequest.BuildRequestType.MultipleProjectsIBuildEngine3:
                var engineResult = HandleMultipleProjectsBuildIBuildEngine3(request);
                result = engineResult.Result;
                targetOutputsPerProject = engineResult.TargetOutputsPerProject;
                break;
        }
    }
    catch (Exception ex)
    {
        // Log the exception but don't fail - return false to the TaskHost
        _taskLoggingContext.LogError(
            new BuildEventFileInfo(_taskLocation),
            "BuildProjectFileCallbackFailed",
            ex.Message);
        result = false;
    }
    
    // Send response
    TaskHostBuildResponse response;
    if (targetOutputsPerProject != null)
    {
        response = new TaskHostBuildResponse(request.RequestId, result, targetOutputsPerProject);
    }
    else
    {
        response = new TaskHostBuildResponse(request.RequestId, result, targetOutputs);
    }
    
    SendDataToTaskHost(response);
}
```

### Step 4: Implement Individual Build Request Handlers

```csharp
/// <summary>
/// Handles IBuildEngine.BuildProjectFile (4 params).
/// </summary>
private bool HandleSingleProjectBuild(
    TaskHostBuildRequest request, 
    out IDictionary targetOutputs)
{
    targetOutputs = new Hashtable();
    
    return _buildEngine.BuildProjectFile(
        request.ProjectFileName,
        request.TargetNames,
        ConvertToIDictionary(request.GlobalProperties),
        targetOutputs);
}

/// <summary>
/// Handles IBuildEngine2.BuildProjectFile (5 params).
/// </summary>
private bool HandleSingleProjectBuildWithToolsVersion(
    TaskHostBuildRequest request, 
    out IDictionary targetOutputs)
{
    targetOutputs = new Hashtable();
    
    if (_buildEngine is IBuildEngine2 engine2)
    {
        return engine2.BuildProjectFile(
            request.ProjectFileName,
            request.TargetNames,
            ConvertToIDictionary(request.GlobalProperties),
            targetOutputs,
            request.ToolsVersion);
    }
    
    // Fallback to 4-param version if engine doesn't support IBuildEngine2
    return _buildEngine.BuildProjectFile(
        request.ProjectFileName,
        request.TargetNames,
        ConvertToIDictionary(request.GlobalProperties),
        targetOutputs);
}

/// <summary>
/// Handles IBuildEngine2.BuildProjectFilesInParallel (7 params).
/// </summary>
private bool HandleMultipleProjectsBuildIBuildEngine2(
    TaskHostBuildRequest request, 
    out IDictionary targetOutputs)
{
    // This overload doesn't return per-project outputs, just overall result
    targetOutputs = null;
    
    if (_buildEngine is IBuildEngine2 engine2)
    {
        var outputsPerProject = new IDictionary[request.ProjectFileNames.Length];
        for (int i = 0; i < outputsPerProject.Length; i++)
        {
            outputsPerProject[i] = new Hashtable();
        }
        
        return engine2.BuildProjectFilesInParallel(
            request.ProjectFileNames,
            request.TargetNames,
            ConvertToIDictionaryArray(request.GlobalPropertiesArray),
            outputsPerProject,
            request.ToolsVersions,
            request.UseResultsCache,
            request.UnloadProjectsOnCompletion);
    }
    
    // Fallback: build sequentially
    return BuildProjectsSequentially(request);
}

/// <summary>
/// Handles IBuildEngine3.BuildProjectFilesInParallel (6 params).
/// </summary>
private BuildEngineResult HandleMultipleProjectsBuildIBuildEngine3(
    TaskHostBuildRequest request)
{
    if (_buildEngine is IBuildEngine3 engine3)
    {
        return engine3.BuildProjectFilesInParallel(
            request.ProjectFileNames,
            request.TargetNames,
            ConvertToIDictionaryArray(request.GlobalPropertiesArray),
            ConvertToIListArray(request.RemoveGlobalProperties),
            request.ToolsVersions,
            request.ReturnTargetOutputs);
    }
    
    // Fallback: build sequentially and construct result
    bool result = BuildProjectsSequentially(request);
    return new BuildEngineResult(result, null);
}
```

### Step 5: Add Helper Conversion Methods

```csharp
/// <summary>
/// Converts Dictionary&lt;string, string&gt; to IDictionary (Hashtable).
/// </summary>
private static IDictionary ConvertToIDictionary(Dictionary<string, string> source)
{
    if (source == null) return null;
    
    var result = new Hashtable(StringComparer.OrdinalIgnoreCase);
    foreach (var kvp in source)
    {
        result[kvp.Key] = kvp.Value;
    }
    return result;
}

/// <summary>
/// Converts array of Dictionary&lt;string, string&gt; to array of IDictionary.
/// </summary>
private static IDictionary[] ConvertToIDictionaryArray(Dictionary<string, string>[] source)
{
    if (source == null) return null;
    
    var result = new IDictionary[source.Length];
    for (int i = 0; i < source.Length; i++)
    {
        result[i] = ConvertToIDictionary(source[i]);
    }
    return result;
}

/// <summary>
/// Converts array of List&lt;string&gt; to array of IList&lt;string&gt;.
/// </summary>
private static IList<string>[] ConvertToIListArray(List<string>[] source)
{
    if (source == null) return null;
    
    var result = new IList<string>[source.Length];
    for (int i = 0; i < source.Length; i++)
    {
        result[i] = source[i];
    }
    return result;
}

/// <summary>
/// Fallback method to build projects sequentially when parallel build isn't supported.
/// </summary>
private bool BuildProjectsSequentially(TaskHostBuildRequest request)
{
    bool overallResult = true;
    
    for (int i = 0; i < request.ProjectFileNames.Length; i++)
    {
        IDictionary globalProps = request.GlobalPropertiesArray != null && i < request.GlobalPropertiesArray.Length
            ? ConvertToIDictionary(request.GlobalPropertiesArray[i])
            : null;
        
        string toolsVersion = request.ToolsVersions != null && i < request.ToolsVersions.Length
            ? request.ToolsVersions[i]
            : null;
        
        bool projectResult;
        if (!string.IsNullOrEmpty(toolsVersion) && _buildEngine is IBuildEngine2 engine2)
        {
            projectResult = engine2.BuildProjectFile(
                request.ProjectFileNames[i],
                request.TargetNames,
                globalProps,
                new Hashtable(),
                toolsVersion);
        }
        else
        {
            projectResult = _buildEngine.BuildProjectFile(
                request.ProjectFileNames[i],
                request.TargetNames,
                globalProps,
                new Hashtable());
        }
        
        if (!projectResult)
        {
            overallResult = false;
            // Continue building remaining projects (matches parallel behavior)
        }
    }
    
    return overallResult;
}
```

### Step 6: Add SendDataToTaskHost Helper

```csharp
/// <summary>
/// Sends a packet to the connected TaskHost.
/// </summary>
private void SendDataToTaskHost(INodePacket packet)
{
    // Use the existing task host provider to send data
    _taskHostProvider.SendData(_requiredContext, packet);
}
```

---

## Error Handling

### Exception in Build

```csharp
catch (Exception ex)
{
    // Don't crash the parent - log and return failure
    _taskLoggingContext.LogError(
        new BuildEventFileInfo(_taskLocation),
        "BuildProjectFileCallbackFailed",
        ex.Message);
    result = false;
}
```

### Missing Build Engine Interface

If the build engine doesn't implement the required interface, fall back to simpler versions:

```csharp
if (_buildEngine is IBuildEngine3 engine3)
{
    // Use IBuildEngine3 method
}
else if (_buildEngine is IBuildEngine2 engine2)
{
    // Fall back to IBuildEngine2
}
else
{
    // Fall back to IBuildEngine
}
```

---

## Testing

### Unit Tests

```csharp
[Fact]
public void HandleBuildRequest_SingleProject_ForwardsToEngine()
{
    var mockEngine = new Mock<IBuildEngine>();
    mockEngine.Setup(e => e.BuildProjectFile(
        It.IsAny<string>(),
        It.IsAny<string[]>(),
        It.IsAny<IDictionary>(),
        It.IsAny<IDictionary>()))
        .Returns(true);
    
    var task = CreateTestTaskHostTask(mockEngine.Object);
    
    var request = new TaskHostBuildRequest(
        "test.csproj",
        new[] { "Build" },
        null);
    request.RequestId = 100;
    
    var capturedResponse = CaptureResponse<TaskHostBuildResponse>(task);
    
    task.HandleBuildRequest(request);
    
    capturedResponse.RequestId.ShouldBe(100);
    capturedResponse.OverallResult.ShouldBeTrue();
    
    mockEngine.Verify(e => e.BuildProjectFile(
        "test.csproj",
        new[] { "Build" },
        It.IsAny<IDictionary>(),
        It.IsAny<IDictionary>()), 
        Times.Once);
}

[Fact]
public void HandleBuildRequest_WithToolsVersion_UsesIBuildEngine2()
{
    var mockEngine = new Mock<IBuildEngine2>();
    mockEngine.Setup(e => e.BuildProjectFile(
        It.IsAny<string>(),
        It.IsAny<string[]>(),
        It.IsAny<IDictionary>(),
        It.IsAny<IDictionary>(),
        It.IsAny<string>()))
        .Returns(true);
    
    var task = CreateTestTaskHostTask(mockEngine.Object);
    
    var request = new TaskHostBuildRequest(
        "test.csproj",
        new[] { "Build" },
        null,
        "16.0");
    request.RequestId = 200;
    
    task.HandleBuildRequest(request);
    
    mockEngine.Verify(e => e.BuildProjectFile(
        "test.csproj",
        new[] { "Build" },
        It.IsAny<IDictionary>(),
        It.IsAny<IDictionary>(),
        "16.0"), 
        Times.Once);
}

[Fact]
public void HandleBuildRequest_Exception_ReturnsFailureResponse()
{
    var mockEngine = new Mock<IBuildEngine>();
    mockEngine.Setup(e => e.BuildProjectFile(
        It.IsAny<string>(),
        It.IsAny<string[]>(),
        It.IsAny<IDictionary>(),
        It.IsAny<IDictionary>()))
        .Throws(new InvalidOperationException("Test exception"));
    
    var task = CreateTestTaskHostTask(mockEngine.Object);
    
    var request = new TaskHostBuildRequest(
        "test.csproj",
        new[] { "Build" },
        null);
    request.RequestId = 300;
    
    var capturedResponse = CaptureResponse<TaskHostBuildResponse>(task);
    
    // Should not throw
    task.HandleBuildRequest(request);
    
    capturedResponse.RequestId.ShouldBe(300);
    capturedResponse.OverallResult.ShouldBeFalse();
}
```

### Integration Tests

```csharp
[Fact]
public void BuildProjectFile_EndToEnd_BuildsNestedProject()
{
    // Full integration test with actual TaskHost
    // 1. Create a task that calls BuildProjectFile
    // 2. The target project should build successfully
    // 3. Verify outputs are returned correctly
}
```

---

## Verification Checklist

- [x] Packet handler registered for `TaskHostBuildRequest` (done in subtask 7)
- [x] `HandleBuildRequest` dispatches to correct handler based on `Variant`
- [x] Single project build (BuildEngine1) forwards correctly to `IBuildEngine`
- [x] Single project with tools version (BuildEngine2Single) uses `IBuildEngine2`
- [x] Multiple projects with `IBuildEngine2` signature (BuildEngine2Parallel) works
- [x] Multiple projects with `IBuildEngine3` signature (BuildEngine3Parallel) returns `BuildEngineResult`
- [x] Target outputs are included in response
- [x] Exceptions don't crash the parent - return failure response
- [x] Interface fallback chain works correctly (BuildEngine2Single falls back to BuildEngine1)
- [x] Handler stack correctly routes packets in nested task scenarios
- [x] Integration test `BuildProjectFileCallbackWorksInTaskHost` passes
- [ ] Full build `.\build.cmd` passes (integration testing)

---

## Notes

- The parent must handle requests promptly as the TaskHost thread is blocked
- Output dictionaries are created fresh in the parent - we don't try to modify TaskHost's dictionaries in place
- The `_taskHostProvider.SendData` method already exists for sending other packets
- Exceptions are caught and result in `result=false` being returned - no logging needed since build engine logs errors

---

## Implementation Notes (Added During Development)

### Handler Stack in NodeProviderOutOfProcTaskHost

The parent side uses a handler stack to support nested task scenarios:

1. **`_nodeIdToPacketHandlerStack`** - Changed from `Dictionary<int, INodePacketHandler>` to `Dictionary<int, Stack<INodePacketHandler>>`
2. **`_nodeIdToPacketFactory`** - Now stores a single factory (`NodeProviderOutOfProcTaskHost` itself) per node instead of per-task factories

When `BuildProjectFile` is called:
1. Task1's handler is on the stack (depth=1)
2. Parent schedules Task2 to the same TaskHost
3. Task2's handler is pushed onto the stack (depth=2)
4. Packets are routed to the handler at the top of the stack
5. When Task2 completes, its handler is popped
6. Task1's handler is now at the top again

### Thread Safety

Added locking with `_activeNodes` to synchronize `PacketReceived` and `DisconnectFromHost`:
- `PacketReceived` acquires lock, peeks stack, calls handler **inside the lock**
- `DisconnectFromHost` acquires lock, pops handler from stack

This prevents a race condition where a packet could be routed to a handler that was just disconnected.

### Late-Arriving Packets

After disconnect, packets may still arrive (e.g., log messages, completion packets). These are now safely ignored rather than causing errors:

```csharp
case NodePacketType.LogMessage:
case NodePacketType.TaskHostTaskComplete:
case NodePacketType.TaskHostQueryRequest:
case NodePacketType.TaskHostResourceRequest:
case NodePacketType.TaskHostBuildRequest:
    // Late-arriving packets from already-completed tasks - safe to ignore
    break;
```

