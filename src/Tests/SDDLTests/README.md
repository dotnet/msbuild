# Security Descriptor Tests for Windows

The console application contains a number of tests that need to be executed manually. The tests are used to
verify the behavior of creating files and directories under `ProgramData`, for example, caching
workload related MSIs. 

## Building and Running Tests

The test can be built either from within Visual Studio or on the commandline,
but should be executed from a command prompt. To run the test, execute
`artifacts\sdk-build.env.bat` followed by `dotnet artifacts\SDDLTests\SDDLTests.dll`.

The test will attempt to clean up the test assets from previous runs before
recreating them.

## Test Expectations

The test will verify the security descriptor for 3 objects.
- The root directory
of the cache in `ProgramData\SDDLTest`. This is the equivalent of `ProgramData\dotnet` used by
workload related commands.

- A sub folder, `ProgramData\SDDLTest\a`. This is equivalent to creating
a versioned sub-folder for a workload package inside the cache.

- A file inside the subfolder that was relocated from the user's temporary
directory. This is equivalent of downloading a workload package and then moving
its contents to the workload package cache.

The primary owner, group and DACL for each of the aforementioned
objects are verified.

## Scenarios

Once compiled, the executable can be used to verify a number of scenarios.

1. Launch the test from a non-elevated command prompt. The application will
elevate before moving the file under `ProgramData`. This is similar to a user
executing a `dotnet workload` command that downloads, elevates and caches a
workload related MSI.

1. Launch the test from an administator prompt. The test will not elevate. The
test asset file will be created under the user's temporary directory before being
moved to the test directory under `ProgramData`. The test is intended to
ensure consistent behavior when running in a different context.

1. Launch the test from a command prompt using Windows Sandbox. While the default account,
`WDAGUtilityAccount`, runs as administrator, Windows Sandbox is not domain joined. The goal here is
to very code changes that impact descriptors referencing non-existing SIDs.

1. Launch the command running as `nt authority\System` (the local SYSTEM account).
Although workloads do not currently support offering automatic updates, the Windows Update
service usually runs under the local SYSTEM account. Since workload packages are
first downloaded to the user's temporary directory, the test is intended to ensure
consistent results across different accounts. The [psexec](https://learn.microsoft.com/en-us/sysinternals/downloads/psexec)
tools can be used to launch a command prompt using the SYSTEM account by running
`psexec.exe -sid cmd` from an elevated command prompt.

## Previous Errors

Below are two examples of actual scenarios where the permissions were
too restrictive or where attempts to fix an issue created problems. The test
is intended to help validate issues such and find potential regressions.

1. In .NET 6, the workload packages ended up with the an ACE associated with the user's SID
that provided full access while the World SID (Everyone) was not missing. This resulted
in errors when a machine was shared between multiple users. Once the package was cached,
subquent attempts to read or execute the package by another user failed.

1. To fix the issue, the `MsiPackageCache` was modified to explicitly provide
read and execute permissions to the Domain User SID. However, since the SID
does not exist on Windows Sandbox, testing workload commands would result in an exception.

## Additional Documentation & Tools

1. [Security Descriptor Definition Language](https://learn.microsoft.com/en-us/windows/win32/secauthz/security-descriptor-definition-language)
1. [Descriptor Control Flags](https://learn.microsoft.com/en-us/windows/win32/secauthz/security-descriptor-control)
1. [ACE Ordering Rules](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-adts/dbfdc00c-1e4b-4165-939b-974e8ea23733)
1. `icacls` is a commandline tool that can be used to create files and folders with specific descriptors.
1. The `get-acl <path> | format-list` cmdlet in Powershell can be used to examine
descriptors.
