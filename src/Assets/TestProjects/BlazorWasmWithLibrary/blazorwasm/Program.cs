using System;

namespace standalone
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GC.KeepAlive(typeof(System.Text.Json.JsonSerializer));
        }
    }
}
