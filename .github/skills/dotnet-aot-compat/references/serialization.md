# Serialization and AOT

Use this reference only when the actual warnings or requested contract involve serialization. Do not replace an unrelated serialization layer merely because AOT analysis was requested.

## Establish the wire contract

Identify the serializer, concrete and polymorphic runtime types, options, converters, accessibility, and serialized data consumers. Inspect existing tests/data and context declarations before changing calls.

Source generation can handle accessible types from other assemblies or packages; source ownership is not the constraint. Conversely, merely adding a type to a context does not prove that its constructors, accessors, converters, polymorphism, or output semantics are supported.

The repository's [TasksDetailsTelemetry](../../../../src/Build/TelemetryInfra/TasksDetailsTelemetry.cs) is an actual source-generated JSON example. Its context in the Build assembly includes `TaskDetailInfo`, defined in [Framework TelemetryDataUtils](../../../../src/Framework/Telemetry/TelemetryDataUtils.cs).

## System.Text.Json pattern

A context is a partial class deriving from `JsonSerializerContext`, annotated with `JsonSerializable` for its roots and optionally `JsonSourceGenerationOptions`. Use `JsonSerializable`, not `[JsonSerializerContext]`: the latter names the base class, not an attribute.

Illustrative C# declarations:

```csharp
using System.Text.Json.Serialization;

internal sealed record Payload(string Name, int Count);

[JsonSerializable(typeof(Payload))]
internal partial class PayloadJsonContext : JsonSerializerContext
{
}
```

For a `Payload payload` and `string json`, typed call-site fragments are:

```csharp
string serialized = JsonSerializer.Serialize(
    payload, PayloadJsonContext.Default.Payload);
Payload? deserialized = JsonSerializer.Deserialize(
    json, PayloadJsonContext.Default.Payload);
```

These fragments also require `System.Text.Json`. See the [official context and overload examples](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation).

When replacing existing calls:

1. Collect the actual root types and runtime polymorphic types. Do not infer the full graph from a generic argument or variable's declared `object` type alone.
2. Reuse an existing context if it owns the same contract; a single project-wide context is not mandatory.
3. Preserve naming, null handling, enum representation, reference handling, custom converters, and other options. Replacing a custom-options call with `Context.Default` can change behavior.
4. Use the matching `JsonTypeInfo<T>` or context overload for string, reader, writer, stream, or async usage.
5. Check changed output/round trips and unsupported graph members as well as analyzer diagnostics. One successful compilation does not guarantee no remaining serialization problem.

Source generation modes have different capabilities. In particular, a serialization-optimized path is not a general substitute for deserialization metadata. Check the selected serializer version and mode rather than applying one mechanical transformation to every call.

`JsonSerializerOptions.GetConverter(Type)` and unconfigured reflection-based serialization are not substitutes for supplying an AOT-safe type contract. If a project has a similarly named generic extension, inspect that extension rather than assuming a universal `GetConverter<T>()` API.

## External models and converters

If a dependency exposes its own generated/model serialization contract, that may be the appropriate path. Verify its actual version's API and the required wire options.

For example, current [Azure ResponseError](https://learn.microsoft.com/en-us/dotnet/api/azure.responseerror) exposes `IJsonModel<ResponseError>`. When that interface and the required constructor are available in the selected dependency, a call can go through the model interface rather than through reflection-based `JsonSerializer`.

Conditional call-site example, with the appropriate `Azure` and `System.ClientModel.Primitives` imports and a verified `modelOptions` value:

```csharp
((IJsonModel<ResponseError>)error).Write(writer, modelOptions);
ResponseError parsed =
    ((IJsonModel<ResponseError>)new ResponseError()).Create(ref reader, modelOptions);
```

This is an optional dependency-specific technique, not a reason to add Azure dependencies to MSBuild or a promise about older model versions. Do not prohibit ordinary source generation just because a type is external.

If necessary, a custom `JsonConverter<T>` can implement the wire format using reader/writer operations. Preserve null handling, unknown properties, nested models, and reader positioning; register it through the supported options/context mechanism. A hand-written converter is a behavioral implementation, not a warning-suppression shortcut.

## Other serialization layers

Reflection-heavy serializers can require architectural work for Native AOT. Changing serializer families can alter formats, extension behavior, and compatibility. Do not automatically migrate `Newtonsoft.Json`, XML serialization, or a public serialization contract in a scoped annotation task.

If a path is fundamentally unsupported, describe the relevant trimming/dynamic-code incompatibility to callers rather than pretending an annotation implements it. Distinguish that documented limitation from completing an AOT migration.
