using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.NET.TestFramework
{
    public class TestCommandLine
    {
        public static List<string> HandleCommandLine(string[] args)
        {
            //  No additional command line options for these tests beyond what xunit supports
            return args.ToList();
        }
    }
}
