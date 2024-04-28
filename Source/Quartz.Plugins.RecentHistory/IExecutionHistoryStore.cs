using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quartz.Plugins.RecentHistory
{
    public interface IExecutionHistoryStore
    {
        string SchedulerName { get; set; }

        Task<ExecutionHistoryEntry> GetAsync(string fireInstanceId);
        Task SaveAsync(ExecutionHistoryEntry entry);
        Task PurgeAsync();

        Task<IEnumerable<ExecutionHistoryEntry>> FilterLastOfEveryJobAsync(int limitPerJob);
        Task<IEnumerable<ExecutionHistoryEntry>> FilterLastOfEveryTriggerAsync(int limitPerTrigger);
        Task<IEnumerable<ExecutionHistoryEntry>> FilterLastAsync(int limit);

        Task<int> GetTotalJobsExecutedAsync();
        Task<int> GetTotalJobsFailedAsync();

        Task IncrementTotalJobsExecutedAsync();
        Task IncrementTotalJobsFailedAsync();
    }
}
