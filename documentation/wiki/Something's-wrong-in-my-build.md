📝 This is incomplete, put here in the spirit of "something is better than nothing".

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

## "Unable to locate the .NET Core SDK"
When building via Visual Studio or the command line, you may encounter an error resembling:
```
error : Unable to locate the .NET Core SDK. Check that it is installed and that the version specified in global.json (if any) matches the installed version.
error MSB4236: The SDK 'Microsoft.NET.Sdk' specified could not be found.
```
It's possible that this is because MSBuild is using a .NET Core SDK preview. Users on release versions of Visual Studio must opt into using these previews.

First, verify the `global.json` file in your repository is using a preview version of .NET Core SDK such as `"3.0.100-preview5-011568"`. Next, open Visual Studio and navigate to Tools -> Options -> Preview Features and check the `Use previews of the .NET Core SDK` box.

![](https://user-images.githubusercontent.com/3347530/59614580-a795c900-90e6-11e9-8981-0fdbd08d42bd.png)

## Tools
Many debugging tools listed [here](https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/MSBuild-Resources.md#tools).

[MSBuildStructuredLog](https://github.com/KirillOsenkov/MSBuildStructuredLog) can be used to get a clearer idea of what's going on in your build. MSBuildStructuredLog is graphical interface over MSBuild binary logs that visualizes a structured representation of executed targets, tasks, properties, and item values. It can be easier to look though than the diagnostic log.

![](https://raw.githubusercontent.com/KirillOsenkov/MSBuildStructuredLog/master/docs/Screenshot1.png)
