# Adding Custom Events to MSBuild

MSBuild has many built-in event types, but often you may need to add a new custom one. 

## Steps for Binary Logger Compatibility
This guide outlines the steps to ensure the BinaryLogger can work with your new event.

### Changes in MSBuild

1. **Add the `NewEventArgs` to `src/Framework` folder**

2. **Update [BinaryLogRecordKind.cs](https://github.com/dotnet/msbuild/blob/main/src/Build/Logging/BinaryLogger/BinaryLogRecordKind.cs)**
   - Add the new event to the enum

3. **Modify [BinaryLogger.cs](https://github.com/dotnet/msbuild/blob/main/src/Build/Logging/BinaryLogger/BinaryLogger.cs)**
   - Update `FileFormatVersion`

4. **Update [BuildEventArgsReader.cs](https://github.com/dotnet/msbuild/blob/main/src/Build/Logging/BinaryLogger/BuildEventArgsReader.cs)**
   - Add a new case in the `HandleError` switch
   - Implement the method (imitate other `ReadEventArgs` methods)

5. **Modify [BuildEventArgsWriter.cs](https://github.com/dotnet/msbuild/blob/main/src/Build/Logging/BinaryLogger/BuildEventArgsWriter.cs)**
   - Add a new case in `WriteCore`
   - Document the change above the method

6. **Update [LogMessagePacketBase.cs](https://github.com/dotnet/msbuild/blob/main/src/Shared/LogMessagePacketBase.cs)**
   - Add to `LoggingEventType`
   - Add case in `GetBuildEventArgFromId` and `GetLoggingEventId`

7. **Create a new test file**
   - Add `Framework.UnitTests/NewEventArgs_Tests.cs`
   - Use [BuildSubmissionStartedEventArgs_Tests.cs](https://github.com/dotnet/msbuild/blob/main/src/Framework.UnitTests/BuildSubmissionStartedEventArgs_Tests.cs) as a reference

8. **Update [NodePackets_Tests.cs](https://github.com/dotnet/msbuild/blob/main/src/Build.UnitTests/BackEnd/NodePackets_Tests.cs)**
   - Add relevant test cases

### Changes in [MSBuildStructuredLog](https://github.com/KirillOsenkov/MSBuildStructuredLog)

1. **Update [BinaryLogRecordKind.cs](https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/master/src/StructuredLogger/BinaryLogger/BinaryLogRecordKind.cs)**
   - Add the new event to the enum

2. **Modify [BinaryLogger.cs](https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/master/src/StructuredLogger/BinaryLogger/BinaryLogger.cs)**
   - Increment version
   - Document the change

3. **Create `src/StructuredLogger/BinaryLogger/XXXEventArgs.cs`**
   - Implement the class for the new event (copy from MSBuild)

4. **Update [BuildEventArgsReader.cs](https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/master/src/StructuredLogger/BinaryLogger/BuildEventArgsReader.cs)**
   - Add a new case in `ReadBuildEventArgs`

5. **Modify [BuildEventArgsWriter.cs](https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/master/src/StructuredLogger/BinaryLogger/BuildEventArgsWriter.cs)**
   - Add a new case in `WriteCore`

### Example Pull Requests adding adding and serializing events

Here are some example PRs that demonstrate adding binary log support:

1. [Add Buildcheck events support + BuildSubmissionStarted](https://github.com/KirillOsenkov/MSBuildStructuredLog/pull/797) (MSBuildStructuredLog)
   - Corresponding [MSBuild PR](https://github.com/dotnet/msbuild/pull/10424)

2. [Add Binary Logger Support for BuildCanceled](https://github.com/dotnet/msbuild/pull/10755) (MSBuild)
   - Corresponding [MSBuildStructuredLog PR](https://github.com/KirillOsenkov/MSBuildStructuredLog/pull/824)
