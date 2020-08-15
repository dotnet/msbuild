namespace Microsoft.Build.Framework
{
    public interface IBuildEventArgsDispatchVisitor
    {
        void Visit(BuildEventArgs buildEventArgs);
        void Visit(BuildMessageEventArgs buildEventArgs);
        void Visit(TaskStartedEventArgs buildEventArgs);
        void Visit(TaskFinishedEventArgs buildEventArgs);
        void Visit(TargetStartedEventArgs buildEventArgs);
        void Visit(TargetFinishedEventArgs buildEventArgs);
        void Visit(ProjectStartedEventArgs buildEventArgs);
        void Visit(ProjectFinishedEventArgs buildEventArgs);
        void Visit(BuildStartedEventArgs buildEventArgs);
        void Visit(BuildFinishedEventArgs buildEventArgs);
        void Visit(CustomBuildEventArgs buildEventArgs);
        void Visit(BuildStatusEventArgs buildEventArgs);
        void Visit(BuildWarningEventArgs buildEventArgs);
        void Visit(BuildErrorEventArgs buildEventArgs);
    }
}
