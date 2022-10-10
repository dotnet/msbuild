namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
	public class NullCurrentSessionIdFixture
	{
        public NullCurrentSessionIdFixture()
        {
            // We need to set this to guarantee that the telemetry logging
            // information will not be added to the msbuild generated parameters
            // when testing the translation between CLI params and msbuild params.
            // This is now needed because before we set SKIP FIRST RUN in the CLI
            // build scripts, but now we don't and we don't want to rely on scripts
            // to make our build/tests work.
            Telemetry.Telemetry.DisableForTests();
        }
    }
}
