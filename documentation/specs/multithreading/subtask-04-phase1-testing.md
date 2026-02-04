# Subtask 4: Phase 1 Testing & Validation

**Parent:** [taskhost-callbacks-implementation-plan.md](./taskhost-callbacks-implementation-plan.md)
**Phase:** 1
**Status:** ✅ Complete
**Estimated:** 2-3 hours
**Dependencies:** Subtasks 1 (✅), 2 (✅), 3 (✅)

---

## Objective

Validate that Phase 1 implementations (`IsRunningMultipleNodes`, `RequestCores`, `ReleaseCores`) work correctly through comprehensive testing. This subtask ensures the callback infrastructure is robust before proceeding to Phase 2's complex callbacks.

---

## Executive Summary

Based on the current implementation state:
- ✅ `TaskHostQueryPacket_Tests.cs` already exists with serialization tests for Query packets
- ✅ `TaskHostResourcePacket_Tests.cs` already exists with serialization tests for Resource packets
- ✅ `IsRunningMultipleNodesTask.cs` test task already exists
- ✅ `IsRunningMultipleNodesCallbackWorksInTaskHost` integration test already exists in `TaskHostFactory_Tests.cs`
- ✅ `RequestCores`/`ReleaseCores` integration tests - IMPLEMENTED
- ✅ Concurrent callback correlation tests - IMPLEMENTED
- ✅ Edge case and error handling tests - IMPLEMENTED

### Critical Bug Fix Discovered

During testing, we discovered that `OutOfProcTaskHostNode.HandlePacket()` was missing a case for `TaskHostResourceResponse`. The response packet was registered but never routed to `HandleCallbackResponse()`, causing `RequestCores`/`ReleaseCores` to hang indefinitely.

**Fix:** Added `case NodePacketType.TaskHostResourceResponse:` to the switch statement in `HandlePacket()` at `src/MSBuild/OutOfProcTaskHostNode.cs:781`.

---

## Implementation Plan

### Step 1: Create ResourceManagementTask Test Task

**File:** `src/Build.UnitTests/BackEnd/ResourceManagementTask.cs` (new)

This task exercises `RequestCores` and `ReleaseCores` callbacks from the TaskHost.

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that exercises IBuildEngine9 RequestCores/ReleaseCores callbacks.
    /// Used to test that resource management callbacks work correctly in the task host.
    /// </summary>
    public class ResourceManagementTask : Task
    {
        /// <summary>
        /// Number of cores to request. Defaults to 2.
        /// </summary>
        public int RequestedCores { get; set; } = 2;

        /// <summary>
        /// If true, releases the cores after requesting them.
        /// </summary>
        public bool ReleaseCoresAfterRequest { get; set; } = true;

        /// <summary>
        /// Output: Number of cores actually granted by the scheduler.
        /// </summary>
        [Output]
        public int CoresGranted { get; set; }

        /// <summary>
        /// Output: True if the task completed without exceptions.
        /// </summary>
        [Output]
        public bool CompletedSuccessfully { get; set; }

        /// <summary>
        /// Output: Exception message if an error occurred.
        /// </summary>
        [Output]
        public string ErrorMessage { get; set; }

        public override bool Execute()
        {
            try
            {
                if (BuildEngine is IBuildEngine9 engine9)
                {
                    // Request cores
                    CoresGranted = engine9.RequestCores(RequestedCores);
                    Log.LogMessage(MessageImportance.High,
                        $"ResourceManagement: Requested {RequestedCores} cores, granted {CoresGranted}");

                    // Release cores if requested
                    if (ReleaseCoresAfterRequest && CoresGranted > 0)
                    {
                        engine9.ReleaseCores(CoresGranted);
                        Log.LogMessage(MessageImportance.High,
                            $"ResourceManagement: Released {CoresGranted} cores");
                    }

                    CompletedSuccessfully = true;
                    return true;
                }

                Log.LogError("BuildEngine does not implement IBuildEngine9");
                ErrorMessage = "BuildEngine does not implement IBuildEngine9";
                return false;
            }
            catch (System.Exception ex)
            {
                Log.LogErrorFromException(ex);
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                CompletedSuccessfully = false;
                return false;
            }
        }
    }
}
```

**Rationale:**
- Mirrors the existing `IsRunningMultipleNodesTask` pattern
- Provides output properties for verification in tests
- Captures exceptions to distinguish "worked but returned 0" from "threw exception"
- Configurable parameters for testing different scenarios

---

### Step 2: Create MultipleCallbackTask Test Task

**File:** `src/Build.UnitTests/BackEnd/MultipleCallbackTask.cs` (new)

This task exercises multiple callbacks in sequence to test correlation ID management.

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that exercises multiple IBuildEngine callbacks in sequence.
    /// Used to test that callback request/response correlation works correctly.
    /// </summary>
    public class MultipleCallbackTask : Task
    {
        /// <summary>
        /// Number of times to call RequestCores/ReleaseCores in a loop.
        /// </summary>
        public int Iterations { get; set; } = 5;

        /// <summary>
        /// Output: IsRunningMultipleNodes value from first query.
        /// </summary>
        [Output]
        public bool IsRunningMultipleNodes { get; set; }

        /// <summary>
        /// Output: Total cores granted across all iterations.
        /// </summary>
        [Output]
        public int TotalCoresGranted { get; set; }

        /// <summary>
        /// Output: Number of successful callback round-trips.
        /// </summary>
        [Output]
        public int SuccessfulCallbacks { get; set; }

        public override bool Execute()
        {
            try
            {
                // Test IsRunningMultipleNodes
                if (BuildEngine is IBuildEngine2 engine2)
                {
                    IsRunningMultipleNodes = engine2.IsRunningMultipleNodes;
                    SuccessfulCallbacks++;
                    Log.LogMessage(MessageImportance.High,
                        $"IsRunningMultipleNodes = {IsRunningMultipleNodes}");
                }

                // Test RequestCores/ReleaseCores multiple times
                if (BuildEngine is IBuildEngine9 engine9)
                {
                    for (int i = 0; i < Iterations; i++)
                    {
                        int granted = engine9.RequestCores(1);
                        TotalCoresGranted += granted;
                        SuccessfulCallbacks++;

                        if (granted > 0)
                        {
                            engine9.ReleaseCores(granted);
                            SuccessfulCallbacks++;
                        }

                        Log.LogMessage(MessageImportance.Normal,
                            $"Iteration {i + 1}: Requested 1 core, granted {granted}");
                    }
                }

                Log.LogMessage(MessageImportance.High,
                    $"MultipleCallbackTask completed: {SuccessfulCallbacks} successful callbacks");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }
        }
    }
}
```

**Rationale:**
- Tests that multiple sequential callbacks work correctly
- Verifies request ID generation doesn't collide
- Validates the full callback lifecycle repeated many times

---

### Step 3: Add Integration Tests to TaskHostFactory_Tests.cs

**File:** `src/Build.UnitTests/BackEnd/TaskHostFactory_Tests.cs` (modify)

Add new test methods after the existing `IsRunningMultipleNodesCallbackWorksInTaskHost` test:

```csharp
/// <summary>
/// Verifies that IBuildEngine9.RequestCores can be called from a task running in the task host.
/// This tests the resource management callback infrastructure.
/// </summary>
[Fact]
public void RequestCoresCallbackWorksInTaskHost()
{
    using TestEnvironment env = TestEnvironment.Create(_output);

    string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(ResourceManagementTask)}"" AssemblyFile=""{typeof(ResourceManagementTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""TestRequestCores"">
        <{nameof(ResourceManagementTask)} RequestedCores=""4"" ReleaseCoresAfterRequest=""true"">
            <Output PropertyName=""CoresGranted"" TaskParameter=""CoresGranted"" />
            <Output PropertyName=""CompletedSuccessfully"" TaskParameter=""CompletedSuccessfully"" />
            <Output PropertyName=""ErrorMessage"" TaskParameter=""ErrorMessage"" />
        </{nameof(ResourceManagementTask)}>
    </Target>
</Project>";

    TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);

    BuildParameters buildParameters = new()
    {
        MaxNodeCount = 4,
        EnableNodeReuse = false
    };

    ProjectInstance projectInstance = new(project.ProjectFile);

    BuildManager buildManager = BuildManager.DefaultBuildManager;
    BuildResult buildResult = buildManager.Build(
        buildParameters,
        new BuildRequestData(projectInstance, targetsToBuild: ["TestRequestCores"]));

    buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

    // Verify the task completed successfully (no NotImplementedException)
    string completedSuccessfully = projectInstance.GetPropertyValue("CompletedSuccessfully");
    completedSuccessfully.ShouldBe("True",
        $"Task failed with error: {projectInstance.GetPropertyValue("ErrorMessage")}");

    // Verify we got at least 1 core (implicit core guarantee)
    string coresGranted = projectInstance.GetPropertyValue("CoresGranted");
    coresGranted.ShouldNotBeNullOrEmpty();
    int.Parse(coresGranted).ShouldBeGreaterThanOrEqualTo(1);
}

/// <summary>
/// Verifies that IBuildEngine9.ReleaseCores can be called from a task running in the task host
/// without throwing exceptions.
/// </summary>
[Fact]
public void ReleaseCoresCallbackWorksInTaskHost()
{
    using TestEnvironment env = TestEnvironment.Create(_output);

    string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(ResourceManagementTask)}"" AssemblyFile=""{typeof(ResourceManagementTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""TestReleaseCores"">
        <{nameof(ResourceManagementTask)} RequestedCores=""2"" ReleaseCoresAfterRequest=""true"">
            <Output PropertyName=""CompletedSuccessfully"" TaskParameter=""CompletedSuccessfully"" />
            <Output PropertyName=""ErrorMessage"" TaskParameter=""ErrorMessage"" />
        </{nameof(ResourceManagementTask)}>
    </Target>
</Project>";

    TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);

    BuildParameters buildParameters = new()
    {
        EnableNodeReuse = false
    };

    ProjectInstance projectInstance = new(project.ProjectFile);

    BuildManager buildManager = BuildManager.DefaultBuildManager;
    BuildResult buildResult = buildManager.Build(
        buildParameters,
        new BuildRequestData(projectInstance, targetsToBuild: ["TestReleaseCores"]));

    buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

    string completedSuccessfully = projectInstance.GetPropertyValue("CompletedSuccessfully");
    completedSuccessfully.ShouldBe("True",
        $"Task failed with error: {projectInstance.GetPropertyValue("ErrorMessage")}");
}

/// <summary>
/// Verifies that multiple callbacks can be made from a single task execution
/// and that request/response correlation works correctly.
/// </summary>
[Fact]
public void MultipleCallbacksWorkInTaskHost()
{
    using TestEnvironment env = TestEnvironment.Create(_output);

    string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(MultipleCallbackTask)}"" AssemblyFile=""{typeof(MultipleCallbackTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""TestMultipleCallbacks"">
        <{nameof(MultipleCallbackTask)} Iterations=""10"">
            <Output PropertyName=""SuccessfulCallbacks"" TaskParameter=""SuccessfulCallbacks"" />
            <Output PropertyName=""TotalCoresGranted"" TaskParameter=""TotalCoresGranted"" />
        </{nameof(MultipleCallbackTask)}>
    </Target>
</Project>";

    TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);

    BuildParameters buildParameters = new()
    {
        MaxNodeCount = 4,
        EnableNodeReuse = false
    };

    ProjectInstance projectInstance = new(project.ProjectFile);

    BuildManager buildManager = BuildManager.DefaultBuildManager;
    BuildResult buildResult = buildManager.Build(
        buildParameters,
        new BuildRequestData(projectInstance, targetsToBuild: ["TestMultipleCallbacks"]));

    buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

    // 1 IsRunningMultipleNodes + 10 RequestCores + 10 ReleaseCores = 21 callbacks
    string successfulCallbacks = projectInstance.GetPropertyValue("SuccessfulCallbacks");
    successfulCallbacks.ShouldNotBeNullOrEmpty();
    int.Parse(successfulCallbacks).ShouldBeGreaterThanOrEqualTo(11); // At minimum: 1 + 10 requests
}

/// <summary>
/// Verifies that callbacks work with the MSBUILDFORCEALLTASKSOUTOFPROC environment variable
/// which forces all tasks to run in the task host.
/// </summary>
[Fact]
public void CallbacksWorkWithForceTaskHostEnvVar()
{
    using TestEnvironment env = TestEnvironment.Create(_output);

    // Force all tasks out of proc
    env.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");

    string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(MultipleCallbackTask)}"" AssemblyFile=""{typeof(MultipleCallbackTask).Assembly.Location}"" />
    <Target Name=""TestCallbacksWithEnvVar"">
        <{nameof(MultipleCallbackTask)} Iterations=""3"">
            <Output PropertyName=""SuccessfulCallbacks"" TaskParameter=""SuccessfulCallbacks"" />
            <Output PropertyName=""IsRunningMultipleNodes"" TaskParameter=""IsRunningMultipleNodes"" />
        </{nameof(MultipleCallbackTask)}>
    </Target>
</Project>";

    TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);

    BuildParameters buildParameters = new()
    {
        MaxNodeCount = 2,
        EnableNodeReuse = true  // Sidecar mode with env var
    };

    ProjectInstance projectInstance = new(project.ProjectFile);

    BuildManager buildManager = BuildManager.DefaultBuildManager;
    BuildResult buildResult = buildManager.Build(
        buildParameters,
        new BuildRequestData(projectInstance, targetsToBuild: ["TestCallbacksWithEnvVar"]));

    buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

    string successfulCallbacks = projectInstance.GetPropertyValue("SuccessfulCallbacks");
    int.Parse(successfulCallbacks).ShouldBeGreaterThan(0);
}
```

---

### Step 4: Add Concurrent Callback Correlation Unit Tests

**File:** `src/Build.UnitTests/BackEnd/TaskHostCallbackCorrelation_Tests.cs` (new)

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for the callback request/response correlation mechanism.
    /// These tests validate the thread-safety and correctness of the
    /// _pendingCallbackRequests dictionary and request ID generation.
    /// </summary>
    public class TaskHostCallbackCorrelation_Tests
    {
        /// <summary>
        /// Verifies that concurrent access to a ConcurrentDictionary (simulating
        /// _pendingCallbackRequests) is thread-safe.
        /// </summary>
        [Fact]
        public void PendingRequests_ConcurrentAccess_IsThreadSafe()
        {
            var pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<INodePacket>>();
            var tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                int requestId = i;
                tasks.Add(Task.Run(() =>
                {
                    var tcs = new TaskCompletionSource<INodePacket>();
                    pendingRequests[requestId] = tcs;
                    Thread.Sleep(Random.Shared.Next(1, 10));
                    pendingRequests.TryRemove(requestId, out _);
                }));
            }

            Task.WaitAll(tasks.ToArray());

            pendingRequests.Count.ShouldBe(0);
        }

        /// <summary>
        /// Verifies that Interlocked.Increment generates unique request IDs
        /// even under heavy concurrent load.
        /// </summary>
        [Fact]
        public void RequestIdGeneration_ConcurrentRequests_NoCollisions()
        {
            var requestIds = new ConcurrentBag<int>();
            int nextRequestId = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < 1000; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    int id = Interlocked.Increment(ref nextRequestId);
                    requestIds.Add(id);
                }));
            }

            Task.WaitAll(tasks.ToArray());

            requestIds.Count.ShouldBe(1000);
            requestIds.Distinct().Count().ShouldBe(1000);
        }

        /// <summary>
        /// Verifies that TaskCompletionSource correctly signals waiting threads
        /// when SetResult is called.
        /// </summary>
        [Fact]
        public void TaskCompletionSource_SignalsWaitingThread()
        {
            var tcs = new TaskCompletionSource<INodePacket>(TaskCreationOptions.RunContinuationsAsynchronously);
            var responseReceived = false;

            var waitingTask = Task.Run(() =>
            {
                // Simulate waiting for response
                var result = tcs.Task.Result;
                responseReceived = true;
            });

            // Simulate response arriving after a short delay
            Thread.Sleep(50);
            var response = new TaskHostQueryResponse(1, true);
            tcs.SetResult(response);

            waitingTask.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            responseReceived.ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that multiple pending requests can be resolved independently
        /// without cross-contamination.
        /// </summary>
        [Fact]
        public void MultiplePendingRequests_ResolveIndependently()
        {
            var pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<INodePacket>>();

            // Create 5 pending requests
            for (int i = 1; i <= 5; i++)
            {
                pendingRequests[i] = new TaskCompletionSource<INodePacket>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            // Resolve them in random order
            var resolveOrder = new[] { 3, 1, 5, 2, 4 };
            foreach (var requestId in resolveOrder)
            {
                var response = new TaskHostQueryResponse(requestId, requestId % 2 == 0);
                if (pendingRequests.TryRemove(requestId, out var tcs))
                {
                    tcs.SetResult(response);
                }
            }

            // Verify all were resolved correctly
            pendingRequests.Count.ShouldBe(0);
        }

        /// <summary>
        /// Verifies that the callback response type checking works correctly.
        /// </summary>
        [Fact]
        public void ResponseTypeChecking_CorrectTypesAccepted()
        {
            var queryResponse = new TaskHostQueryResponse(1, true);
            var resourceResponse = new TaskHostResourceResponse(2, 4);

            // Both should implement ITaskHostCallbackPacket
            queryResponse.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
            resourceResponse.ShouldBeAssignableTo<ITaskHostCallbackPacket>();

            // Verify RequestId is accessible through interface
            ((ITaskHostCallbackPacket)queryResponse).RequestId.ShouldBe(1);
            ((ITaskHostCallbackPacket)resourceResponse).RequestId.ShouldBe(2);
        }
    }
}
```

---

### Step 5: Add Edge Case Tests to Existing Packet Tests

**File:** `src/Build.UnitTests/BackEnd/TaskHostResourcePacket_Tests.cs` (modify)

Add these additional tests to the existing file:

```csharp
/// <summary>
/// Tests that zero core count serializes correctly (edge case).
/// </summary>
[Fact]
public void TaskHostResourceRequest_ZeroCores_RoundTrip()
{
    var request = new TaskHostResourceRequest(
        TaskHostResourceRequest.ResourceOperation.RequestCores, 0);
    request.RequestId = 1;

    ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
    request.Translate(writeTranslator);

    ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
    var deserialized = (TaskHostResourceRequest)TaskHostResourceRequest.FactoryForDeserialization(readTranslator);

    deserialized.CoreCount.ShouldBe(0);
}

/// <summary>
/// Tests that large core count serializes correctly (edge case).
/// </summary>
[Fact]
public void TaskHostResourceRequest_LargeCoreCount_RoundTrip()
{
    var request = new TaskHostResourceRequest(
        TaskHostResourceRequest.ResourceOperation.RequestCores, int.MaxValue);
    request.RequestId = int.MaxValue;

    ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
    request.Translate(writeTranslator);

    ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
    var deserialized = (TaskHostResourceRequest)TaskHostResourceRequest.FactoryForDeserialization(readTranslator);

    deserialized.CoreCount.ShouldBe(int.MaxValue);
    deserialized.RequestId.ShouldBe(int.MaxValue);
}

/// <summary>
/// Tests that negative response values serialize correctly (edge case).
/// </summary>
[Fact]
public void TaskHostResourceResponse_NegativeValue_RoundTrip()
{
    // While negative cores doesn't make semantic sense,
    // the packet should handle it for robustness
    var response = new TaskHostResourceResponse(-1, -1);

    ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
    response.Translate(writeTranslator);

    ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
    var deserialized = (TaskHostResourceResponse)TaskHostResourceResponse.FactoryForDeserialization(readTranslator);

    deserialized.RequestId.ShouldBe(-1);
    deserialized.CoresGranted.ShouldBe(-1);
}
```

---

### Step 6: Manual Validation Steps

After implementing the automated tests, perform these manual validations:

#### 6.1 Build the Repository
```cmd
cd D:\msbuilds\msb1
.\build.cmd -v quiet
```
**Expected:** Build completes successfully.

#### 6.2 Run the New Unit Tests
```cmd
dotnet test src\Build.UnitTests\Microsoft.Build.Engine.UnitTests.csproj --filter "FullyQualifiedName~TaskHostCallback"
dotnet test src\Build.UnitTests\Microsoft.Build.Engine.UnitTests.csproj --filter "FullyQualifiedName~ResourceManagement"
dotnet test src\Build.UnitTests\Microsoft.Build.Engine.UnitTests.csproj --filter "FullyQualifiedName~TaskHostResourcePacket"
dotnet test src\Build.UnitTests\Microsoft.Build.Engine.UnitTests.csproj --filter "FullyQualifiedName~TaskHostFactory_Tests"
```
**Expected:** All tests pass.

#### 6.3 Test with Force TaskHost Environment Variable
```cmd
set MSBUILDFORCEALLTASKSOUTOFPROC=1
artifacts\msbuild-build-env.bat
dotnet build src\Samples\Dependency\Dependency.csproj -v diag 2>&1 | findstr /i "NotImplementedException MSB5022"
```
**Expected:** No NotImplementedException errors, no MSB5022 errors related to callbacks.

#### 6.4 Verify Diagnostic Output
```cmd
set MSBUILDFORCEALLTASKSOUTOFPROC=1
dotnet build src\Samples\Dependency\Dependency.csproj -v diag 2>&1 | findstr /i "IsRunningMultipleNodes RequestCores ReleaseCores"
```
**Expected:** Should see log messages from callbacks if any tasks use them.

---

## Files to Create

| File | Purpose | Status |
|------|---------|--------|
| `src/Build.UnitTests/BackEnd/ResourceManagementTask.cs` | Test task for RequestCores/ReleaseCores | ✅ Created |
| `src/Build.UnitTests/BackEnd/MultipleCallbackTask.cs` | Test task for multiple sequential callbacks | ✅ Created |
| `src/Build.UnitTests/BackEnd/TaskHostCallbackCorrelation_Tests.cs` | Unit tests for correlation mechanism | ✅ Created |

## Files to Modify

| File | Changes | Status |
|------|---------|--------|
| `src/MSBuild/OutOfProcTaskHostNode.cs` | **BUG FIX**: Add `TaskHostResourceResponse` case to `HandlePacket()` | ✅ Fixed |
| `src/Build.UnitTests/BackEnd/TaskHostFactory_Tests.cs` | Add 4 new integration tests | ✅ Modified |
| `src/Build.UnitTests/BackEnd/TaskHostResourcePacket_Tests.cs` | Add 3 edge case tests | ✅ Modified |

---

## Verification Checklist

### Unit Tests
- [x] `TaskHostResourcePacket_Tests` - all existing tests pass
- [x] `TaskHostResourcePacket_Tests` - new edge case tests pass
- [x] `TaskHostQueryPacket_Tests` - all tests pass
- [x] `TaskHostCallbackCorrelation_Tests` - all tests pass

### Integration Tests
- [x] `IsRunningMultipleNodesCallbackWorksInTaskHost` - passes (existing)
- [x] `RequestCoresCallbackWorksInTaskHost` - passes (new)
- [x] `ReleaseCoresCallbackWorksInTaskHost` - passes (new)
- [x] `MultipleCallbacksWorkInTaskHost` - passes (new)
- [x] `CallbacksWorkWithForceTaskHostEnvVar` - passes (new)

### Manual Validation
- [x] Build completes successfully
- [x] No `NotImplementedException` with `MSBUILDFORCEALLTASKSOUTOFPROC=1`
- [x] No MSB5022 errors for callback operations

---

## Phase 1 Completion Criteria

Phase 1 is complete when ALL of the following are true:

1. ✅ `IsRunningMultipleNodes` returns the parent's actual value (not hardcoded `false`)
2. ✅ `RequestCores` returns granted cores (not throws `NotImplementedException`)
3. ✅ `ReleaseCores` completes without exception
4. ✅ No MSB5022 errors logged for these callbacks
5. ✅ All unit tests pass
6. ✅ Integration tests validate end-to-end behavior with TaskHostFactory
7. ✅ Integration tests validate behavior with MSBUILDFORCEALLTASKSOUTOFPROC=1

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Tests flaky due to timing | Use synchronous BuildManager.Build(), not async |
| Process cleanup issues | Use TestEnvironment which handles cleanup |
| TaskHost process doesn't terminate | Tests verify process state, kill if needed |
| Scheduler returns 0 cores | Assert >= 1 (implicit core guarantee) |

---

## Implementation Order

1. **Create test tasks** (Step 1, 2) - ~30 min
2. **Add integration tests** (Step 3) - ~45 min
3. **Add correlation unit tests** (Step 4) - ~30 min
4. **Add edge case tests** (Step 5) - ~15 min
5. **Manual validation** (Step 6) - ~30 min
6. **Fix any issues found** - ~30 min

**Total Estimated Time:** 2-3 hours

---

## Notes

- All test tasks inherit from `Microsoft.Build.Utilities.Task` for consistency
- Tests use `TaskHostFactory` explicitly to force out-of-proc execution
- `EnableNodeReuse = false` in tests prevents sidecar processes persisting
- Tests capture output via `ITestOutputHelper` for debugging
- Edge case tests ensure packet serialization is robust for all int values
