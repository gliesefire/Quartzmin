using Microsoft.AspNetCore.SignalR;

namespace Quartzmin.Hubs
{
    public class QuartzHub : Hub
    {
        protected Services Services => (Services)Context.GetHttpContext()?.Items[typeof(Services)];

        public async Task GetScheduleInfoAsync()
        {
            var scheduleInfo = await new ScheduleInfoHelper().GetScheduleInfoAsync(Services.Scheduler).ConfigureAwait(false);

            await Clients.All.SendAsync("Update", scheduleInfo).ConfigureAwait(false);
        }

        public async Task UpdateHistoryAsync()
        {
            // TODO: read from partial view file
            // Handlebars.Compile()
        }
    }
}