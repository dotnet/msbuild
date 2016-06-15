dotnet-test communication protocol
===================================

## Introduction
Anytime you pass a port to dotnet test, the command will run in design time. That means that dotnet test will connect to that port
using TCP and will then exchange an established set of messages with whatever else is connected to that port. When this happens, the runner
also receives a new port that dotnet test will use to communicate with it. The reason why the runner also uses TCP to
communicate with dotnet test is because in design mode, it is not sufficient to just output results to the console. The 
command needs to send the adapter structure messages containing the results of the test execution.

### Communication protocol at design time.

1. Because during design time, dotnet test connects to a port when it starts up, the adapter needs to be listening on 
that port otherwise dotnet test will fail. We did it like this so that the adapter could reserve all the ports it needs 
by binding and listening to them before dotnet test ran and tried to get ports for the runner.
2. Once dotnet test starts, it sends a TestSession.Connected message to the adapter indicating that it is ready to receive messages.
3. It is possible to send an optional 
[version check](https://github.com/dotnet/cli/blob/rel/1.0.0/src/Microsoft.Extensions.Testing.Abstractions/Messages/ProtocolVersionMessage.cs) 
message with the adapter version of the protocol in it. Dotnet test will send back the version of the protocol that it supports.

All messages have the format described here: 
[Message.cs](https://github.com/dotnet/cli/blob/rel/1.0.0/src/Microsoft.Extensions.Testing.Abstractions/Messages/Message.cs). 
The payload formats for each message is described in links to the classes used to serialize/deseralize the information in the description of the protocol.

#### Test Execution
![alt tag](../../../images/DotnetTestExecuteTests.png)

1. After the optional version check, the adapter sends a TestExecution.GetTestRunnerProcessStartInfo, with the 
[tests](https://github.com/dotnet/cli/blob/rel/1.0.0/src/Microsoft.Extensions.Testing.Abstractions/Messages/RunTestsMessage.cs) it wants to execute inside of it. Dotnet test sends back a FileName and Arguments inside a [TestStartInfo](https://github.com/dotnet/cli/blob/rel/1.0.0/src/dotnet/commands/dotnet-test/TestStartInfo.cs) payload that the adapter can use to start the runner. In the past, we would send the list of tests to run as part of that argument, but we were actually going over the command line size limit for some test projects.
  1. As part of the arguments, we send a port that the runner should connect to and for executing tests, a --wait-command flag, that indicates that the runner should connect to the port and wait for commands, instead of going ahead and executing the tests.
2. At this point, the adapter can launch the runner (and attach to it for debugging if it chooses to).
3. Once the runner starts, it sends dotnet test a TestRunner.WaitCommand message that indicates it is ready to receive commands, at which point dotnet test sends a TestRunner.Execute with the list of [tests](https://github.com/dotnet/cli/blob/rel/1.0.0/src/Microsoft.Extensions.Testing.Abstractions/Messages/RunTestsMessage.cs) to run. This bypasses the command line size limit described above.
4. The runner then sends dotnet test (and it passes forward to the adapter) a TestExecution.TestStarted for each tests as they start with the [test](https://github.com/dotnet/cli/blob/rel/1.0.0/src/Microsoft.Extensions.Testing.Abstractions/Test.cs) information inside of it.
5. The runner also sends dotnet test (and it forwards to the adapter) a TestExecution.TestResult for each test with the [individual result](https://github.com/dotnet/cli/blob/rel/1.0.0/src/Microsoft.Extensions.Testing.Abstractions/TestResult.cs) of the test.
6. After all tests finish, the runner sends a TestRunner.Completed message to dotnet test, which dotnet test sends as TestExecution.Completed to the adapter.
7. Once the adapter is done, it sends dotnet test a TestSession.Terminate which will cause dotnet test to shutdown.

#### Test discovery
![alt tag](../../../images/DotnetTestDiscoverTests.png)

1. After the optional version check, the adapter sends a TestDiscovery.Start message. Because in this case, the adapter does not need to attach to the process, dotnet test will start the runner itself. Also, since there is no long list of arguments to be passed to the runner, no --wait-command flag is needed to be passed to the runner. dotnet test only passes a --list argument to the runner, which means the runner should not run the tests, just list them.
2. The runner then sends dotnet test (and it passes forward to the adapter) a TestDiscovery.TestFound for each [test](https://github.com/dotnet/cli/blob/rel/1.0.0/src/Microsoft.Extensions.Testing.Abstractions/Test.cs) found.
3. After all tests are discovered, the runner sends a TestRunner.Completed message to dotnet test, which dotnet test sends as TestDiscovery.Completed to the adapter.
4. Once the adapter is done, it sends dotnet test a TestSession.Terminate which will cause dotnet test to shutdown.
