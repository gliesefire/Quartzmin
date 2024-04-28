namespace Quartzmin.Controllers;

public class JobsController : PageControllerBase
{
    [HttpGet]
    public async Task<IActionResult> IndexAsync()
    {
        var keys = (await Scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).ConfigureAwait(false)).OrderBy(x => x.ToString());
        var list = new List<JobListItem>();
        var knownTypes = new List<string>();

        foreach (var key in keys)
        {
            var detail = await GetJobDetailAsync(key).ConfigureAwait(false);
            var item = new JobListItem()
            {
                Concurrent = !detail.ConcurrentExecutionDisallowed,
                Persist = detail.PersistJobDataAfterExecution,
                Recovery = detail.RequestsRecovery,
                JobName = key.Name,
                Group = key.Group,
                Type = detail.JobType.FullName,
                History = Histogram.Empty,
                Description = detail.Description
            };
            knownTypes.Add(detail.JobType.RemoveAssemblyDetails());
            list.Add(item);
        }

        Services.Cache.UpdateJobTypes(knownTypes);

        ViewBag.Groups = (await Scheduler.GetJobGroupNames().ConfigureAwait(false)).GroupArray();

        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> NewAsync()
    {
        var job = new JobPropertiesViewModel() { IsNew = true };
        var jobDataMap = new JobDataMapModel() { Template = JobDataMapItemTemplate };

        job.GroupList = (await Scheduler.GetJobGroupNames().ConfigureAwait(false)).GroupArray();
        job.Group = SchedulerConstants.DefaultGroup;
        job.TypeList = Services.Cache.JobTypes;

        return View("Edit", new JobViewModel { Job = job, DataMap = jobDataMap });
    }

    [HttpGet]
    public async Task<IActionResult> TriggerAsync(string name, string group)
    {
        if (!EnsureValidKey(name, group))
        {
            return BadRequest();
        }

        var jobKey = JobKey.Create(name, group);
        var job = await GetJobDetailAsync(jobKey).ConfigureAwait(false);
        var jobDataMap = new JobDataMapModel() { Template = JobDataMapItemTemplate };

        ViewBag.JobName = name;
        ViewBag.Group = group;

        jobDataMap.Items.AddRange(job.GetJobDataMapModel(Services));

        return View(jobDataMap);
    }

    [HttpPost, ActionName("Trigger"), JsonErrorResponse]
    public async Task<IActionResult> PostTriggerAsync(string name, string group)
    {
        if (!EnsureValidKey(name, group))
        {
            return BadRequest();
        }

        var jobDataMap = (await Request.GetJobDataMapFormAsync().ConfigureAwait(false)).GetModel(Services);

        var result = new ValidationResult();

        ModelValidator.Validate(jobDataMap, result.Errors);

        if (result.Success)
        {
            await Scheduler.TriggerJob(JobKey.Create(name, group), jobDataMap.GetQuartzJobDataMap()).ConfigureAwait(false);
        }

        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> EditAsync(string name, string group, bool clone = false)
    {
        if (!EnsureValidKey(name, group))
        {
            return BadRequest();
        }

        var jobKey = JobKey.Create(name, group);
        var job = await GetJobDetailAsync(jobKey).ConfigureAwait(false);

        var jobModel = new JobPropertiesViewModel() { };
        var jobDataMap = new JobDataMapModel() { Template = JobDataMapItemTemplate };

        jobModel.IsNew = clone;
        jobModel.IsCopy = clone;
        jobModel.JobName = name;
        jobModel.Group = group;
        jobModel.GroupList = (await Scheduler.GetJobGroupNames().ConfigureAwait(false)).GroupArray();

        jobModel.Type = job.JobType.RemoveAssemblyDetails();
        jobModel.TypeList = Services.Cache.JobTypes;

        jobModel.Description = job.Description;
        jobModel.Recovery = job.RequestsRecovery;

        if (clone)
        {
            jobModel.JobName += " - Copy";
        }

        jobDataMap.Items.AddRange(job.GetJobDataMapModel(Services));

        return View("Edit", new JobViewModel() { Job = jobModel, DataMap = jobDataMap });
    }

    private async Task<IJobDetail> GetJobDetailAsync(JobKey key)
    {
        var job = await Scheduler.GetJobDetail(key).ConfigureAwait(false);

        if (job == null)
        {
            throw new InvalidOperationException("Job " + key + " not found.");
        }

        return job;
    }

    [HttpPost, JsonErrorResponse]
    public async Task<IActionResult> SaveAsync([FromForm] JobViewModel model, bool trigger)
    {
        var jobModel = model.Job;
        var jobDataMap = (await Request.GetJobDataMapFormAsync().ConfigureAwait(false)).GetModel(Services);

        var result = new ValidationResult();

        model.Validate(result.Errors);
        ModelValidator.Validate(jobDataMap, result.Errors);

        if (result.Success)
        {
            IJobDetail BuildJob(JobBuilder builder)
            {
                return builder
                    .OfType(Type.GetType(jobModel.Type, true))
                    .WithIdentity(jobModel.JobName, jobModel.Group)
                    .WithDescription(jobModel.Description)
                    .SetJobData(jobDataMap.GetQuartzJobDataMap())
                    .RequestRecovery(jobModel.Recovery)
                    .Build();
            }

            if (jobModel.IsNew)
            {
                await Scheduler.AddJob(BuildJob(JobBuilder.Create().StoreDurably()), replace: false).ConfigureAwait(false);
            }
            else
            {
                var oldJob = await GetJobDetailAsync(JobKey.Create(jobModel.OldJobName, jobModel.OldGroup)).ConfigureAwait(false);
                await Scheduler.UpdateJobAsync(oldJob.Key, BuildJob(oldJob.GetJobBuilder())).ConfigureAwait(false);
            }

            if (trigger)
            {
                await Scheduler.TriggerJob(JobKey.Create(jobModel.JobName, jobModel.Group)).ConfigureAwait(false);
            }
        }

        return Json(result);
    }

    [HttpPost, JsonErrorResponse]
    public async Task<IActionResult> DeleteAsync([FromBody] KeyModel model)
    {
        if (!EnsureValidKey(model))
        {
            return BadRequest();
        }

        var key = model.ToJobKey();

        if (!await Scheduler.DeleteJob(key).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Cannot delete job " + key);
        }

        return NoContent();
    }

    [HttpGet, JsonErrorResponse]
    public async Task<IActionResult> AdditionalDataAsync()
    {
        var keys = await Scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).ConfigureAwait(false);
        var history = await Scheduler.Context.GetExecutionHistoryStore().FilterLastOfEveryJobAsync(10).ConfigureAwait(false);
        var historyByJob = history.ToLookup(x => x.Job);

        var list = new List<object>();
        foreach (var key in keys)
        {
            var triggers = await Scheduler.GetTriggersOfJob(key).ConfigureAwait(false);

            var nextFires = triggers.Select(x => x.GetNextFireTimeUtc()?.UtcDateTime).ToArray();

            list.Add(new
            {
                JobName = key.Name, key.Group,
                History = historyByJob.TryGet(key.ToString()).ToHistogram(),
                NextFireTime = nextFires.Where(x => x != null).OrderBy(x => x).FirstOrDefault()?.ToDefaultFormat(),
            });
        }

        return View(list);
    }

    [HttpGet]
    public Task<IActionResult> DuplicateAsync(string name, string group)
    {
        return EditAsync(name, group, clone: true);
    }

    private bool EnsureValidKey(string name, string group) => !(string.IsNullOrEmpty(name) || string.IsNullOrEmpty(group));
    private bool EnsureValidKey(KeyModel model) => EnsureValidKey(model.Name, model.Group);
}