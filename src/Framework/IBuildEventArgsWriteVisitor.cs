namespace Microsoft.Build.Framework
{
    public interface IBuildEventArgsWriteVisitor
    {
        void Visit(BuildEventArgs e);
        void Visit(BuildMessageEventArgs e);
        void Visit(CriticalBuildMessageEventArgs e);
        void Visit(TaskCommandLineEventArgs e);
        void Visit(ProjectImportedEventArgs e);
        void Visit(TargetSkippedEventArgs e);
        void Visit(PropertyReassignmentEventArgs e);
        void Visit(UninitializedPropertyReadEventArgs e);
        void Visit(EnvironmentVariableReadEventArgs e);
        void Visit(PropertyInitialValueSetEventArgs e);
        void Visit(TaskStartedEventArgs e);
        void Visit(TaskFinishedEventArgs e);
        void Visit(TargetStartedEventArgs e);
        void Visit(TargetFinishedEventArgs e);
        void Visit(BuildErrorEventArgs e);
        void Visit(BuildWarningEventArgs e);
        void Visit(ProjectStartedEventArgs e);
        void Visit(ProjectFinishedEventArgs e);
        void Visit(BuildStartedEventArgs e);
        void Visit(BuildFinishedEventArgs e);
        void Visit(ProjectEvaluationStartedEventArgs e);
        void Visit(ProjectEvaluationFinishedEventArgs e);
    }
}
