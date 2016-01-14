using System;

namespace ConsoleApplication
{
    public class L11
    {
        public static string Value()
        {
            return "L11 " + L12.Value() + L21.Value();
        }
    }
}
