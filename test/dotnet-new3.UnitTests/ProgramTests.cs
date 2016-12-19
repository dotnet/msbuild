using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3.UnitTests
{
    public class ProgramTests
    {
        private static string[] CommandToArgArrayHelper(string command)
        {
            return command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        [Fact]
        public void TestTest()
        {
            string command = "";
        }
    }
}
