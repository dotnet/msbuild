📝 This is horrifically incomplete, put here in the spirit of "something is better than nothing".

## Debugging Issues With a Build

To debug a tricky problem with your build, try these tools in order:

1. Diagnostic-level logs show you project state changes and inputs to all tasks
2. The MSBuild preprocessor `msbuild /pp:out.xml` shows you the full logic of the project in a single file (so you can easily grep around for "what uses this property?" and "where is this target defined?")
3. Add a target to debug project state of specific interest at specific times:
```xml
<Target Name="PrintfDebugger" BeforeTargets="Something">
  <Message Importance="High" Text="PropOfInterest: $(PropOfInterest)" />
  <Message Importance="High" Text="ItemOfInterest: @(ItemOfInterest)" />
</Target>
```

## Tools
Many debugging tools listed [here](https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/MSBuild-Resources.md#tools).

[MSBuildStructuredLog](https://github.com/KirillOsenkov/MSBuildStructuredLog) can be used to get a clearer idea of what's going on in your build. MSBuildStructuredLog is a logger for MSBuild that records and visualizes a structured representation of executed targets, tasks, properties, and item values. It can be easier to look though than the diagnostic log.

![](https://raw.githubusercontent.com/KirillOsenkov/MSBuildStructuredLog/master/docs/Screenshot1.png)
