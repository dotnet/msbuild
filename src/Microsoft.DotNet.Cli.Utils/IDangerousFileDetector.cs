namespace Microsoft.DotNet.Cli.Utils
{
    internal interface IDangerousFileDetector
    {
        bool IsDangerous(string filePath);
    }
}
