namespace Quartzmin.Controllers;

public class SchedulerController : PageControllerBase
{
    [HttpGet]
    public async Task<IActionResult> IndexAsync()
    {
        var histStore = Scheduler.Context.GetExecutionHistoryStore();
        var metadata = await Scheduler.GetMetaData().ConfigureAwait(false);
        var jobKeys = await Scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).ConfigureAwait(false);
        var triggerKeys = await Scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup()).ConfigureAwait(false);
        var currentlyExecutingJobs = await Scheduler.GetCurrentlyExecutingJobs().ConfigureAwait(false);
        IEnumerable<object> pausedJobGroups = null;
        IEnumerable<object> pausedTriggerGroups = null;
        IEnumerable<ExecutionHistoryEntry> execHistory = null;

        try
        {
            pausedJobGroups = await GetGroupPauseStateAsync(await Scheduler.GetJobGroupNames().ConfigureAwait(false),
                async x => await Scheduler.IsJobGroupPaused(x).ConfigureAwait(false)).ConfigureAwait(false);
        }
        catch (NotImplementedException)
        {
        }

        try
        {
            pausedTriggerGroups = await GetGroupPauseStateAsync(await Scheduler.GetTriggerGroupNames().ConfigureAwait(false),
                async x => await Scheduler.IsTriggerGroupPaused(x).ConfigureAwait(false)).ConfigureAwait(false);
        }
        catch (NotImplementedException)
        {
        }

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

        return View(new
        {
            History = histogram,
            MetaData = metadata,
            RunningSince = metadata.RunningSince != null ? metadata.RunningSince.Value.UtcDateTime.ToDefaultFormat() + " UTC" : "N / A",
            Environment.MachineName,
            Application = Environment.CommandLine,
            JobsCount = jobKeys.Count,
            TriggerCount = triggerKeys.Count,
            ExecutingJobs = currentlyExecutingJobs.Count,
            ExecutedJobs = executedJobs,
            FailedJobs = failedJobs?.ToString(CultureInfo.InvariantCulture) ?? "N / A",
            JobGroups = pausedJobGroups,
            TriggerGroups = pausedTriggerGroups,
            HistoryEnabled = histStore != null,
        });
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

    public class ActionArgs
    {
        public string Action { get; set; }
        public string Name { get; set; }
        public string Groups { get; set; } // trigger-groups | job-groups
    }

    [HttpPost, JsonErrorResponse]
    public async Task ActionAsync([FromBody] ActionArgs args)
    {
        switch (args.Action.ToLower())
        {
            case "shutdown":
                await Scheduler.Shutdown().ConfigureAwait(false);
                break;
            case "standby":
                await Scheduler.Standby().ConfigureAwait(false);
                break;
            case "start":
                await Scheduler.Start().ConfigureAwait(false);
                break;
            case "pause":
                if (string.IsNullOrEmpty(args.Name))
                {
                    await Scheduler.PauseAll().ConfigureAwait(false);
                }
                else
                {
                    if (args.Groups == "trigger-groups")
                    {
                        await Scheduler.PauseTriggers(GroupMatcher<TriggerKey>.GroupEquals(args.Name)).ConfigureAwait(false);
                    }
                    else if (args.Groups == "job-groups")
                    {
                        await Scheduler.PauseJobs(GroupMatcher<JobKey>.GroupEquals(args.Name)).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid groups: " + args.Groups);
                    }
                }

                break;
            case "resume":
                if (string.IsNullOrEmpty(args.Name))
                {
                    await Scheduler.ResumeAll().ConfigureAwait(false);
                }
                else
                {
                    if (args.Groups == "trigger-groups")
                    {
                        await Scheduler.ResumeTriggers(GroupMatcher<TriggerKey>.GroupEquals(args.Name)).ConfigureAwait(false);
                    }
                    else if (args.Groups == "job-groups")
                    {
                        await Scheduler.ResumeJobs(GroupMatcher<JobKey>.GroupEquals(args.Name)).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid groups: " + args.Groups);
                    }
                }

                break;
            default:
                throw new InvalidOperationException("Invalid action: " + args.Action);
        }
    }
}