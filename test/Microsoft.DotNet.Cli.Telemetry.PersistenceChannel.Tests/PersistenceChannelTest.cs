using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using TaskEx = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Cli.Telemetry.PersistenceChannel.Tests
{
    public class PersistenceChannelTest
    {
        [Fact]
        public void TestPersistenceChannelConstructorAndDisposeOnDeadlock()
        {
            List<TaskEx> taskList = new List<TaskEx>();
            for (int i = 0; i < 500; i++)
            {
                TaskEx task = new TaskEx(Action);
                task.Start();
                taskList.Add(task);
            }

            bool completed = TaskEx.WaitAll(taskList.ToArray(), 50000);
            completed.Should().BeTrue("tasks did not finish. Potential deadlock problem.");
        }

        private void Action()
        {
            PersistenceChannel channel = new PersistenceChannel();
            channel.Dispose();
        }
    }
}
