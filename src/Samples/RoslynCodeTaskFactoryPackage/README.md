# RoslynCodeTaskFactory Package Sample

This sample project demonstrates the use of RoslynCodeTaskFactory and answers the question about telemetry classification.

## Purpose

This sample was created to answer the question: **When using the RoslynCodeTaskFactory NuGet package (version 2.0.7), is it counted in telemetry as RoslynCodeTaskFactory or as CustomTaskFactory?**

## Answer

**External RoslynCodeTaskFactory packages are counted as CustomTaskFactory in MSBuild telemetry, NOT as RoslynCodeTaskFactory.**

## Explanation

MSBuild's telemetry system classifies task factories based on their .NET type name using `GetType().FullName`. The classification logic is in `src/Build/BackEnd/Components/Logging/ProjectTelemetry.cs`:

```csharp
switch (taskFactoryTypeName)
{
    case "Microsoft.Build.BackEnd.AssemblyTaskFactory":
        _assemblyTaskFactoryTasksExecutedCount++;
        break;
    case "Microsoft.Build.BackEnd.IntrinsicTaskFactory":
        _intrinsicTaskFactoryTasksExecutedCount++;
        break;
    case "Microsoft.Build.Tasks.CodeTaskFactory":
        _codeTaskFactoryTasksExecutedCount++;
        break;
    case "Microsoft.Build.Tasks.RoslynCodeTaskFactory":  // Built-in only
        _roslynCodeTaskFactoryTasksExecutedCount++;
        break;
    case "Microsoft.Build.Tasks.XamlTaskFactory":
        _xamlTaskFactoryTasksExecutedCount++;
        break;
    default:
        _customTaskFactoryTasksExecutedCount++;  // External packages fall here
        break;
}
```

### Key Points:

1. **Built-in RoslynCodeTaskFactory**: The MSBuild-included RoslynCodeTaskFactory has the type name `Microsoft.Build.Tasks.RoslynCodeTaskFactory` and is counted as "RoslynCodeTaskFactory" in telemetry.

2. **External RoslynCodeTaskFactory Package**: The NuGet package `RoslynCodeTaskFactory` v2.0.7 implements the same functionality but with a different namespace/type name. Since it doesn't match the hardcoded constant `"Microsoft.Build.Tasks.RoslynCodeTaskFactory"`, it falls through to the `default` case and is counted as "CustomTaskFactory".

3. **Telemetry Impact**: This means that usage statistics for external RoslynCodeTaskFactory packages are:
   - **NOT** included in `RoslynCodeTaskFactoryTasksExecutedCount`
   - **ARE** included in `CustomTaskFactoryTasksExecutedCount`

## Sample Usage

This project demonstrates:

1. **Built-in RoslynCodeTaskFactory** usage (counted as RoslynCodeTaskFactory)
2. How an external package would be used (counted as CustomTaskFactory)

### Built-in Example

```xml
<UsingTask
  TaskName="BuiltInRoslynTask"
  TaskFactory="RoslynCodeTaskFactory"
  AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll" >
  <ParameterGroup>
    <Message ParameterType="System.String" Required="true" />
    <Result ParameterType="System.String" Output="true" />
  </ParameterGroup>
  <Task>
    <Code Type="Fragment" Language="cs">
      <![CDATA[
      string processedMessage = "Built-in RoslynCodeTaskFactory processed: " + Message;
      Log.LogMessage(MessageImportance.High, processedMessage);
      Result = processedMessage;
      ]]>
    </Code>
  </Task>
</UsingTask>
```

### External Package Example (Conceptual)

If using the external package, the syntax would be similar:

```xml
<PackageReference Include="RoslynCodeTaskFactory" Version="2.0.7" />

<UsingTask
  TaskName="ExternalRoslynTask"
  TaskFactory="RoslynCodeTaskFactory" 
  AssemblyFile="$(PkgRoslynCodeTaskFactory)\build\netstandard2.0\RoslynCodeTaskFactory.dll" >
  <!-- Same task definition as above -->
</UsingTask>
```

## Testing

To build and test this sample:

```bash
# From MSBuild repository root
source artifacts/sdk-build-env.sh
dotnet build src/Samples/RoslynCodeTaskFactoryPackage/RoslynCodeTaskFactoryPackage.csproj -v normal
```

## Conclusion

**Answer: External RoslynCodeTaskFactory packages are counted as CustomTaskFactory in MSBuild telemetry.**

This is because MSBuild's telemetry system only recognizes the exact type name of the built-in task factories. Any external implementation, even if functionally identical and using the same TaskFactory name, will be classified as a custom task factory.