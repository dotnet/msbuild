📝 This is horrifically incomplete, put here in the spirit of "something is better than nothing".

To debug a tricky problem with your build, try these tools in order:

1. Diagnostic-level logs show you project state changes and inputs to all tasks
1. The MSBuild preprocessor `msbuild /pp:out.xml` shows you the full logic of the project in a single file (so you can easily grep around for "what uses this property?" and "where is this target defined?")
1. Add a target to debug project state of specific interest at specific times:
```xml
<Target Name="PrintfDebugger" BeforeTargets="Something">
  <Message Importance="High" Text="PropOfInterest: $(PropOfInterest)" />
  <Message Importance="High" Text="ItemOfInterest: @(ItemOfInterest)" />
</Target>
```