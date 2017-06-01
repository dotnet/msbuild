namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    public class LaunchSettingsApplyResult
    {
        public LaunchSettingsApplyResult(bool success, string failureReason, string runAfterLaunch = null)
        {
            Success = success;
            FailureReason = failureReason;
            RunAfterLaunch = runAfterLaunch;
        }

        public bool Success { get; }

        public string FailureReason { get; }

        public string RunAfterLaunch { get; }
    }
}
