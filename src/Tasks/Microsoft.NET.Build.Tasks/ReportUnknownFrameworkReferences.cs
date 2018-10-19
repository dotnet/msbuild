namespace Microsoft.NET.Build.Tasks
{
    public class ReportUnknownFrameworkReferences : TaskBase
    {
        public string[] UnresolvedFrameworkReferences { get; set; }

        protected override void ExecuteCore()
        {
            if (UnresolvedFrameworkReferences != null)
            {
                foreach (var unresolvedFrameworkReference in UnresolvedFrameworkReferences)
                {
                    Log.LogError(Strings.UnknownFrameworkReference, unresolvedFrameworkReference);
                }
            }
        }
    }
}
