using System;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
	public class NativeCompiler 
	{
		public static NativeCompiler Create(Config config)
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

		public bool CompileToNative(Config config)
		{	
			int result = invoker.Invoke(config);
            if(result != 0)
            {
                return false;
            }

            result = intermediateCompiler.Invoke(config);
            if (result != 0)
            {
                return false;
            }

            return true;
		}
	}
}