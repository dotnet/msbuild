namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public interface IPlatformNativeStep
	{
		int Invoke();
		string DetermineOutputFile(NativeCompileSettings config);
		bool CheckPreReqs();
	}
}