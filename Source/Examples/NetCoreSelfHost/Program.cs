using Quartzmin;
using System.Threading;
using System.Threading.Tasks;

namespace NetCoreSelfHost
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var scheduler = await DemoScheduler.CreateAsync();
            await scheduler.Start();

            while (!scheduler.IsShutdown)
            {
                Thread.Sleep(500);
            }
        }
    }
}
