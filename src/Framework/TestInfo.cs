namespace Microsoft.Build.Framework
{
    //  This is a central place to keep track of whether tests are running or not.  Test startup code
    //  will set this to true.  It is consumed in BuildEnvironmentHelper.  However, since that class
    //  is compiled into each project separately, it's not possible for the test startup code to
    //  interact directly with the BuildEnvironmentHelper class - hence this central location.

    //  This class is accessed via reflection, because adding the InternalsVisibleTo attributes which
    //  would be required to access it statically causes errors due to other shared internal classes
    //  which are compiled into multiple projects.
    internal static class TestInfo
    {
        public static bool s_runningTests = false;
    }
}
