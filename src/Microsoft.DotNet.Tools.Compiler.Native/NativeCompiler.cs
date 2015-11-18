using System;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
	public class NativeCompiler 
	{
		public static NativeCompiler Create(NativeCompileSettings config)
		{
			var invoker = new ILCompilerInvoker(config);
			var intCompiler = IntermediateCompiler.Create(config);
			
			var nc = new NativeCompiler() 
			{
				invoker = invoker, 
				intermediateCompiler = intCompiler
			};
			
			return nc;
		}

        private ILCompilerInvoker invoker;
        private IntermediateCompiler intermediateCompiler;

		public bool CompileToNative(NativeCompileSettings config)
		{	
			int result = invoker.Invoke();
            if(result != 0)
            {
                return false;
            }

            result = intermediateCompiler.Invoke();
            if (result != 0)
            {
                return false;
            }

            return true;
		}
	}
}