using ApprovalTests.Reporters;
using ApprovalTests.Reporters.TestFrameworks;

[assembly: UseReporter(typeof(FrameworkAssertReporter))]
[assembly: ApprovalTests.Namers.UseApprovalSubdirectory("Approvals")]
