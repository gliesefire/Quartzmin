namespace Quartzmin.Controllers
{
    public class TriggersController : PageControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> IndexAsync()
        {
            var keys = (await Scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup()).ConfigureAwait(false)).OrderBy(x => x.ToString());
            var list = new List<TriggerListItem>();

            foreach (var key in keys)
            {
                var t = await GetTriggerAsync(key).ConfigureAwait(false);
                var state = await Scheduler.GetTriggerState(key).ConfigureAwait(false);

                list.Add(new TriggerListItem()
                {
                    Type = t.GetTriggerType(),
                    TriggerName = t.Key.Name,
                    TriggerGroup = t.Key.Group,
                    IsPaused = state == TriggerState.Paused,
                    JobKey = t.JobKey.ToString(),
                    JobGroup = t.JobKey.Group,
                    JobName = t.JobKey.Name,
                    ScheduleDescription = t.GetScheduleDescription(),
                    History = Histogram.Empty,
                    StartTime = t.StartTimeUtc.UtcDateTime.ToDefaultFormat(),
                    EndTime = t.FinalFireTimeUtc?.UtcDateTime.ToDefaultFormat(),
                    LastFireTime = t.GetPreviousFireTimeUtc()?.UtcDateTime.ToDefaultFormat(),
                    NextFireTime = t.GetNextFireTimeUtc()?.UtcDateTime.ToDefaultFormat(),
                    ClrType = t.GetType().Name,
                    Description = t.Description,
                });
            }

            ViewBag.Groups = (await Scheduler.GetTriggerGroupNames().ConfigureAwait(false)).GroupArray();

            list = list.OrderBy(x => x.JobKey).ToList();
            string prevKey = null;
            foreach (var item in list)
            {
                if (item.JobKey != prevKey)
                {
                    item.JobHeaderSeparator = true;
                    prevKey = item.JobKey;
                }
            }

            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> NewAsync()
        {
            var model = await TriggerPropertiesViewModel.CreateAsync(Scheduler).ConfigureAwait(false);
            var jobDataMap = new JobDataMapModel() { Template = JobDataMapItemTemplate };

            model.IsNew = true;

            model.Type = TriggerType.Cron;
            model.Priority = 5;

            return View("Edit", new TriggerViewModel() { Trigger = model, DataMap = jobDataMap });
        }

        [HttpGet]
        public async Task<IActionResult> EditAsync(string name, string group, bool clone = false)
        {
            if (!EnsureValidKey(name, group))
            {
                return BadRequest();
            }

            var key = new TriggerKey(name, group);
            var trigger = await GetTriggerAsync(key).ConfigureAwait(false);

            var jobDataMap = new JobDataMapModel() { Template = JobDataMapItemTemplate };

            var model = await TriggerPropertiesViewModel.CreateAsync(Scheduler).ConfigureAwait(false);

            model.IsNew = clone;
            model.IsCopy = clone;
            model.Type = trigger.GetTriggerType();
            model.Job = trigger.JobKey.ToString();
            model.TriggerName = trigger.Key.Name;
            model.TriggerGroup = trigger.Key.Group;
            model.OldTriggerName = trigger.Key.Name;
            model.OldTriggerGroup = trigger.Key.Group;

            if (clone)
            {
                model.TriggerName += " - Copy";
            }

            // don't show start time in the past because rescheduling cause triggering missfire policies
            model.StartTimeUtc = trigger.StartTimeUtc > DateTimeOffset.UtcNow ? trigger.StartTimeUtc.UtcDateTime.ToDefaultFormat() : null;

            model.EndTimeUtc = trigger.EndTimeUtc?.UtcDateTime.ToDefaultFormat();

            model.CalendarName = trigger.CalendarName;
            model.Description = trigger.Description;
            model.Priority = trigger.Priority;

            model.MisfireInstruction = trigger.MisfireInstruction;

            switch (model.Type)
            {
                case TriggerType.Cron:
                    model.Cron = CronTriggerViewModel.FromTrigger((ICronTrigger)trigger);
                    break;
                case TriggerType.Simple:
                    model.Simple = SimpleTriggerViewModel.FromTrigger((ISimpleTrigger)trigger);
                    break;
                case TriggerType.Daily:
                    model.Daily = DailyTriggerViewModel.FromTrigger((IDailyTimeIntervalTrigger)trigger);
                    break;
                case TriggerType.Calendar:
                    model.Calendar = CalendarTriggerViewModel.FromTrigger((ICalendarIntervalTrigger)trigger);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported trigger type: " + trigger.GetType().AssemblyQualifiedName);
            }

            jobDataMap.Items.AddRange(trigger.GetJobDataMapModel(Services));

            return View("Edit", new TriggerViewModel() { Trigger = model, DataMap = jobDataMap });
        }

        [HttpPost, JsonErrorResponse]
        public async Task<IActionResult> SaveAsync([FromForm] TriggerViewModel model)
        {
            var triggerModel = model.Trigger;
            var jobDataMap = (await Request.GetJobDataMapFormAsync().ConfigureAwait(false)).GetModel(Services);

            var result = new ValidationResult();

            model.Validate(result.Errors);
            ModelValidator.Validate(jobDataMap, result.Errors);

            if (result.Success)
            {
                var builder = TriggerBuilder.Create()
                    .WithIdentity(new TriggerKey(triggerModel.TriggerName, triggerModel.TriggerGroup))
                    .ForJob(jobKey: triggerModel.Job)
                    .UsingJobData(jobDataMap.GetQuartzJobDataMap())
                    .WithDescription(triggerModel.Description)
                    .WithPriority(triggerModel.PriorityOrDefault);

                builder.StartAt(triggerModel.GetStartTimeUtc() ?? DateTime.UtcNow);
                builder.EndAt(triggerModel.GetEndTimeUtc());

                if (!string.IsNullOrEmpty(triggerModel.CalendarName))
                {
                    builder.ModifiedByCalendar(triggerModel.CalendarName);
                }

                if (triggerModel.Type == TriggerType.Cron)
                {
                    triggerModel.Cron.Apply(builder, triggerModel);
                }

                if (triggerModel.Type == TriggerType.Simple)
                {
                    triggerModel.Simple.Apply(builder, triggerModel);
                }

                if (triggerModel.Type == TriggerType.Daily)
                {
                    triggerModel.Daily.Apply(builder, triggerModel);
                }

                if (triggerModel.Type == TriggerType.Calendar)
                {
                    triggerModel.Calendar.Apply(builder, triggerModel);
                }

                var trigger = builder.Build();

                if (triggerModel.IsNew)
                {
                    await Scheduler.ScheduleJob(trigger).ConfigureAwait(false);
                }
                else
                {
                    await Scheduler.RescheduleJob(new TriggerKey(triggerModel.OldTriggerName, triggerModel.OldTriggerGroup), trigger).ConfigureAwait(false);
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

            var key = model.ToTriggerKey();

            if (!await Scheduler.UnscheduleJob(key).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Cannot unschedule job " + key);
            }

            return NoContent();
        }

        [HttpPost, JsonErrorResponse]
        public async Task<IActionResult> ResumeAsync([FromBody] KeyModel model)
        {
            if (!EnsureValidKey(model))
            {
                return BadRequest();
            }

            await Scheduler.ResumeTrigger(model.ToTriggerKey()).ConfigureAwait(false);
            return NoContent();
        }

        [HttpPost, JsonErrorResponse]
        public async Task<IActionResult> PauseAsync([FromBody] KeyModel model)
        {
            if (!EnsureValidKey(model))
            {
                return BadRequest();
            }

            await Scheduler.PauseTrigger(model.ToTriggerKey()).ConfigureAwait(false);
            return NoContent();
        }

        [HttpPost, JsonErrorResponse]
        public async Task<IActionResult> PauseJobAsync([FromBody] KeyModel model)
        {
            if (!EnsureValidKey(model))
            {
                return BadRequest();
            }

            await Scheduler.PauseJob(model.ToJobKey()).ConfigureAwait(false);
            return NoContent();
        }

        [HttpPost, JsonErrorResponse]
        public async Task<IActionResult> ResumeJobAsync([FromBody] KeyModel model)
        {
            if (!EnsureValidKey(model))
            {
                return BadRequest();
            }

            await Scheduler.ResumeJob(model.ToJobKey()).ConfigureAwait(false);
            return NoContent();
        }

        [HttpPost, JsonErrorResponse]
        public IActionResult Cron()
        {
            var cron = Request.ReadAsString()?.Trim();
            if (string.IsNullOrEmpty(cron))
            {
                return Json(new { Description = string.Empty, Next = new object[0] });
            }

            string desc = "Invalid format.";

            try
            {
                desc = CronExpressionDescriptor.ExpressionDescriptor.GetDescription(cron);
            }
            catch
            { }

            List<string> nextDates = new List<string>();

            try
            {
                var qce = new CronExpression(cron);
                DateTime dt = DateTime.Now;
                for (int i = 0; i < 10; i++)
                {
                    var next = qce.GetNextValidTimeAfter(dt);
                    if (next == null)
                    {
                        break;
                    }

                    nextDates.Add(next.Value.LocalDateTime.ToDefaultFormat());
                    dt = next.Value.LocalDateTime;
                }
            }
            catch
            { }

            return Json(new { Description = desc, Next = nextDates });
        }

        private async Task<ITrigger> GetTriggerAsync(TriggerKey key)
        {
            var trigger = await Scheduler.GetTrigger(key).ConfigureAwait(false);

            if (trigger == null)
            {
                throw new InvalidOperationException("Trigger " + key + " not found.");
            }

            return trigger;
        }

        [HttpGet, JsonErrorResponse]
        public async Task<IActionResult> AdditionalDataAsync()
        {
            var keys = await Scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup()).ConfigureAwait(false);
            var history = await Scheduler.Context.GetExecutionHistoryStore().FilterLastOfEveryTriggerAsync(10).ConfigureAwait(false);
            var historyByTrigger = history.ToLookup(x => x.Trigger);

            var list = new List<object>();
            foreach (var key in keys)
            {
                list.Add(new
                {
                    TriggerName = key.Name,
                    TriggerGroup = key.Group,
                    History = historyByTrigger.TryGet(key.ToString()).ToHistogram(),
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
}
