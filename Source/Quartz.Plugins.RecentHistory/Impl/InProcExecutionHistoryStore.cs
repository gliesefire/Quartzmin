using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quartz.Plugins.RecentHistory.Impl
{
    [Serializable]
    public class InProcExecutionHistoryStore : IExecutionHistoryStore
    {
        public string SchedulerName { get; set; }

        private readonly Dictionary<string, ExecutionHistoryEntry> _data = new Dictionary<string, ExecutionHistoryEntry>();
        private DateTime _nextPurgeTime = DateTime.UtcNow;
        private int _updatesFromLastPurge;
        private int _totalJobsExecuted = 0;
        private int _totalJobsFailed = 0;

        public Task<ExecutionHistoryEntry> GetAsync(string fireInstanceId)
        {
            lock (_data)
            {
                if (_data.TryGetValue(fireInstanceId, out var entry))
                {
                    return Task.FromResult(entry);
                }
                else
                {
                    return Task.FromResult<ExecutionHistoryEntry>(null);
                }
            }
        }

        public async Task PurgeAsync()
        {
            var ids = new HashSet<string>((await FilterLastOfEveryTriggerAsync(10).ConfigureAwait(false)).Select(x => x.FireInstanceId));

            lock (_data)
            {
                foreach (var key in _data.Keys.ToArray())
                {
                    if (!ids.Contains(key))
                    {
                        _data.Remove(key);
                    }
                }
            }
        }

        public async Task SaveAsync(ExecutionHistoryEntry entry)
        {
            _updatesFromLastPurge++;

            if (_updatesFromLastPurge >= 10 || _nextPurgeTime < DateTime.UtcNow)
            {
                _nextPurgeTime = DateTime.UtcNow.AddMinutes(1);
                _updatesFromLastPurge = 0;
                await PurgeAsync().ConfigureAwait(false);
            }

            lock (_data)
            {
                _data[entry.FireInstanceId] = entry;
            }
        }

        public Task<IEnumerable<ExecutionHistoryEntry>> FilterLastOfEveryJobAsync(int limitPerJob)
        {
            lock (_data)
            {
                IEnumerable<ExecutionHistoryEntry> result = _data.Values
                    .Where(x => x.SchedulerName == SchedulerName)
                    .GroupBy(x => x.Job)
                    .Select(x => x.OrderByDescending(y => y.ActualFireTimeUtc).Take(limitPerJob).Reverse())
                    .SelectMany(x => x).ToArray();
                return Task.FromResult(result);
            }
        }

        public Task<IEnumerable<ExecutionHistoryEntry>> FilterLastOfEveryTriggerAsync(int limitPerTrigger)
        {
            lock (_data)
            {
                IEnumerable<ExecutionHistoryEntry> result = _data.Values
                    .Where(x => x.SchedulerName == SchedulerName)
                    .GroupBy(x => x.Trigger)
                    .Select(x => x.OrderByDescending(y => y.ActualFireTimeUtc).Take(limitPerTrigger).Reverse())
                    .SelectMany(x => x).ToArray();
                return Task.FromResult(result);
            }
        }

        public Task<IEnumerable<ExecutionHistoryEntry>> FilterLastAsync(int limit)
        {
            lock (_data)
            {
                IEnumerable<ExecutionHistoryEntry> result = _data.Values
                    .Where(x => x.SchedulerName == SchedulerName)
                    .OrderByDescending(y => y.ActualFireTimeUtc).Take(limit).Reverse().ToArray();
                return Task.FromResult(result);
            }
        }

        public Task<int> GetTotalJobsExecutedAsync()
        {
            return Task.FromResult(_totalJobsExecuted);
        }

        public Task<int> GetTotalJobsFailedAsync()
        {
            return Task.FromResult(_totalJobsFailed);
        }

        public Task IncrementTotalJobsExecutedAsync()
        {
            Interlocked.Increment(ref _totalJobsExecuted);
            return Task.FromResult(0);
        }

        public Task IncrementTotalJobsFailedAsync()
        {
            Interlocked.Increment(ref _totalJobsFailed);
            return Task.FromResult(0);
        }
    }
}
