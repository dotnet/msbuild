using System;
using System.Threading.Tasks;

namespace PortableTask
{
    public class Sleep : Microsoft.Build.Utilities.Task
    {
        public double Seconds { get; set; }

        public override bool Execute()
        {
            Task.Delay(TimeSpan.FromSeconds(Seconds)).Wait();
            return true;
        }
    }
}
