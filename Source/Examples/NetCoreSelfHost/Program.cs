using Quartzmin;
using System.Threading;

namespace NetCoreSelfHost
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var scheduler = DemoScheduler.CreateAsync().Result;
            scheduler.Start();

            while (!scheduler.IsShutdown)
            {
                Thread.Sleep(500);
            }
        }
    }
}
