# Subtask 7: BuildProjectFile Packets & Serialization

**Parent:** [taskhost-callbacks-implementation-plan.md](./taskhost-callbacks-implementation-plan.md)
**Phase:** 2
**Status:** ✅ Complete
**Dependencies:** Subtask 5 (Task Context Management)

---

## Objective

Create the packet types for `BuildProjectFile*` callbacks, including proper serialization of `ITaskItem[]` outputs. This is the most complex serialization in the callback system.

---

## Background

### Methods to Support

| Method | Interface | Parameters |
|--------|-----------|------------|
| `BuildProjectFile(4 params)` | IBuildEngine | projectFileName, targetNames[], globalProperties, targetOutputs |
| `BuildProjectFile(5 params)` | IBuildEngine2 | + toolsVersion |
| `BuildProjectFilesInParallel(7 params)` | IBuildEngine2 | projectFileNames[], targetNames[], globalProperties[], targetOutputsPerProject[], toolsVersion[], useResultsCache, unloadProjectsOnCompletion |
| `BuildProjectFilesInParallel(6 params)` | IBuildEngine3 | Returns `BuildEngineResult` with `IList<IDictionary<string, ITaskItem[]>>` |

---

## Implementation Summary

### Files Modified/Created

| File | Changes |
|------|---------|
| `src/Shared/TaskHostBuildRequest.cs` | **NEW** - Request packet for BuildProjectFile* callbacks |
| `src/Shared/TaskHostBuildResponse.cs` | **NEW** - Response packet with ITaskItem[] serialization |
| `src/Build/Microsoft.Build.csproj` | Added compilation includes for new packets |
| `src/MSBuild/MSBuild.csproj` | Added compilation includes for new packets |
| `src/Build/Instance/TaskFactories/TaskHostTask.cs` | Registered packet handler |
| `src/MSBuild/OutOfProcTaskHostNode.cs` | Registered packet handler and response routing |
| `src/Build.UnitTests/BackEnd/TaskHostBuildPacket_Tests.cs` | **NEW** - 13 unit tests |

---

## Implementation Details

### TaskHostBuildRequest

Uses factory methods for each IBuildEngine variant:
- `CreateBuildEngine1Request()` - IBuildEngine.BuildProjectFile (4 params)
- `CreateBuildEngine2SingleRequest()` - IBuildEngine2.BuildProjectFile (5 params)
- `CreateBuildEngine2ParallelRequest()` - IBuildEngine2.BuildProjectFilesInParallel (7 params)
- `CreateBuildEngine3ParallelRequest()` - IBuildEngine3.BuildProjectFilesInParallel (6 params)

The `BuildRequestVariant` enum identifies which variant was used for proper serialization.

### TaskHostBuildResponse

Supports two output formats:
- Single project: `Dictionary<string, ITaskItem[]>` (target name → outputs)
- Multiple projects: `List<Dictionary<string, ITaskItem[]>>` (one per project)

Uses `TaskParameter` class for `ITaskItem[]` serialization, which handles metadata preservation.

### Packet Registration

**Parent side (TaskHostTask.cs):**
```csharp
(this as INodePacketFactory).RegisterPacketHandler(
    NodePacketType.TaskHostBuildRequest,
    TaskHostBuildRequest.FactoryForDeserialization,
    this);
```

**TaskHost side (OutOfProcTaskHostNode.cs):**
```csharp
thisINodePacketFactory.RegisterPacketHandler(
    NodePacketType.TaskHostBuildResponse,
    TaskHostBuildResponse.FactoryForDeserialization,
    this);
```

---

## Testing

### Unit Tests

**File:** `src/Build.UnitTests/BackEnd/TaskHostBuildPacket_Tests.cs`

| Test | Purpose |
|------|---------|
| `TaskHostBuildRequest_BuildEngine1_RoundTrip` | Serializes IBuildEngine.BuildProjectFile |
| `TaskHostBuildRequest_BuildEngine2Single_RoundTrip` | Serializes IBuildEngine2.BuildProjectFile |
| `TaskHostBuildRequest_BuildEngine2Parallel_RoundTrip` | Serializes IBuildEngine2.BuildProjectFilesInParallel |
| `TaskHostBuildRequest_BuildEngine3Parallel_RoundTrip` | Serializes IBuildEngine3.BuildProjectFilesInParallel |
| `TaskHostBuildRequest_NullGlobalProperties_RoundTrip` | Handles null global properties |
| `TaskHostBuildRequest_ImplementsITaskHostCallbackPacket` | Interface compliance |
| `TaskHostBuildResponse_SingleProject_Success_RoundTrip` | Serializes success with ITaskItem[] outputs |
| `TaskHostBuildResponse_SingleProject_Failure_RoundTrip` | Serializes failure |
| `TaskHostBuildResponse_TaskItemWithMetadata_RoundTrip` | Preserves custom metadata |
| `TaskHostBuildResponse_MultipleTargets_RoundTrip` | Multiple targets with outputs |
| `TaskHostBuildResponse_BuildEngine3_MultipleProjects_RoundTrip` | IBuildEngine3 format |
| `TaskHostBuildResponse_EmptyTargetOutputs_RoundTrip` | Empty but non-null outputs |
| `TaskHostBuildResponse_ImplementsITaskHostCallbackPacket` | Interface compliance |

### Test Results

All 44 tests pass on both .NET 10.0 and .NET Framework 4.7.2:
- 31 tests from previous subtasks
- 13 new TaskHostBuildPacket tests

---

## Design Decisions

### Why Factory Methods Instead of Constructors?

The `TaskHostBuildRequest` class supports 4 different IBuildEngine variants with different parameters. Factory methods make the intent clear and prevent parameter mismatches.

### Why TaskParameter for ITaskItem[] Serialization?

`TaskParameter` already handles the complex serialization of `ITaskItem[]` with metadata preservation. Reusing it avoids duplicating serialization logic and ensures consistency with existing task parameter serialization.

### Why Convert IDictionary to Dictionary<string,string>?

`IDictionary` is weakly typed (object keys/values). Converting to `Dictionary<string,string>` ensures type safety and proper serialization. The conversion uses `ToString()` which matches MSBuild's property handling.

### Why Store ITaskItem[] Directly in Response?

Unlike the request (which converts `IDictionary` to `Dictionary<string,string>`), the response stores `ITaskItem[]` directly because:
1. `ITaskItem` metadata must be preserved exactly
2. `TaskParameter` handles the serialization
3. Callers expect `ITaskItem[]` from `targetOutputs`

---

## Verification Checklist

- [x] `TaskHostBuildRequest` serializes all 4 method variants
- [x] `TaskHostBuildResponse` serializes single project outputs
- [x] `TaskHostBuildResponse` serializes multiple project outputs
- [x] `ITaskItem` arrays with metadata round-trip correctly
- [x] Empty/null outputs handled correctly
- [x] `IDictionary` → `Dictionary<string,string>` conversion works
- [x] Packet handlers registered on both sides
- [x] Response routing added to HandlePacket
- [x] Default case in switch throws (defensive coding)
- [x] All tests pass

---

## Notes

- `TaskParameter` class from `src/Shared/TaskParameter.cs` handles the complex `ITaskItem` serialization
- Metadata on task items is preserved through serialization via `TaskParameterTaskItem`
- Global properties use `StringComparer.OrdinalIgnoreCase` for consistency
- Packet types were already reserved (0x20, 0x21) in `NodePacketType`
- This subtask creates infrastructure only; actual BuildProjectFile callback implementation is in subtask 8
