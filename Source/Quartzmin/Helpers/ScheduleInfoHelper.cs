namespace Quartzmin.Helpers
{
    public class ScheduleInfoHelper
    {
        public async Task<object> GetScheduleInfoAsync(IScheduler scheduler)
        {
            var histStore = scheduler.Context.GetExecutionHistoryStore();
            var metadata = await scheduler.GetMetaData().ConfigureAwait(false);
            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).ConfigureAwait(false);
            var triggerKeys = await scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup()).ConfigureAwait(false);
            var currentlyExecutingJobs = await scheduler.GetCurrentlyExecutingJobs().ConfigureAwait(false);
            IEnumerable<object> pausedJobGroups = null;
            IEnumerable<object> pausedTriggerGroups = null;
            IEnumerable<ExecutionHistoryEntry> execHistory = null;

            try
            {
                pausedJobGroups = await GetGroupPauseStateAsync(await scheduler.GetJobGroupNames().ConfigureAwait(false), async x => await scheduler.IsJobGroupPaused(x).ConfigureAwait(false)).ConfigureAwait(false);
            }
            catch (NotImplementedException) { }

            try
            {
                pausedTriggerGroups = await GetGroupPauseStateAsync(await scheduler.GetTriggerGroupNames().ConfigureAwait(false), async x => await scheduler.IsTriggerGroupPaused(x).ConfigureAwait(false)).ConfigureAwait(false);
            }
            catch (NotImplementedException) { }

            int? failedJobs = null;
            int executedJobs = metadata.NumberOfJobsExecuted;

            if (histStore != null)
            {
                execHistory = await (histStore?.FilterLastAsync(10)).ConfigureAwait(false);
                executedJobs = await (histStore?.GetTotalJobsExecutedAsync()).ConfigureAwait(false);
                failedJobs = await (histStore?.GetTotalJobsFailedAsync()).ConfigureAwait(false);
            }

            var histogram = execHistory.ToHistogram(detailed: true) ?? Histogram.CreateEmpty();

            histogram.BarWidth = 14;

            return new
            {
                History = histogram,

                // MetaData = metadata,
                RunningSince = metadata.RunningSince != null ? metadata.RunningSince.Value.UtcDateTime.ToDefaultFormat() + " UTC" : "N / A",
                Environment.MachineName,
                Application = Environment.CommandLine,
                JobsCount = jobKeys.Count,
                TriggerCount = triggerKeys.Count,
                ExecutingJobs = currentlyExecutingJobs.Count,
                ExecutedJobs = executedJobs,
                FailedJobs = failedJobs?.ToString(CultureInfo.InvariantCulture) ?? "N / A",

                // JobGroups = pausedJobGroups,
                // TriggerGroups = pausedTriggerGroups,
                HistoryEnabled = histStore != null,
            };
        }

        private async Task<IEnumerable<object>> GetGroupPauseStateAsync(IEnumerable<string> groups, Func<string, Task<bool>> func)
        {
            var result = new List<object>();

            foreach (var name in groups.OrderBy(x => x, StringComparer.InvariantCultureIgnoreCase))
            {
                result.Add(new { Name = name, IsPaused = await func(name).ConfigureAwait(false) });
            }

            return result;
        }
    }
}