using System.ComponentModel.DataAnnotations;

namespace Quartzmin.Models
{
    public class TriggerPropertiesViewModel : IHasValidation
    {
        public string DateFormat { get; } = DateTimeSettings.DefaultDateFormat;
        public string TimeFormat { get; } = DateTimeSettings.DefaultTimeFormat;
        public string DateTimeFormat { get => DateFormat + " " + TimeFormat; }

        [SkipValidation]
        public SimpleTriggerViewModel Simple { get; set; } = new SimpleTriggerViewModel();
        [SkipValidation]
        public DailyTriggerViewModel Daily { get; set; } = new DailyTriggerViewModel();
        [SkipValidation]
        public CronTriggerViewModel Cron { get; set; } = new CronTriggerViewModel();
        [SkipValidation]
        public CalendarTriggerViewModel Calendar { get; set; } = new CalendarTriggerViewModel();

        public bool IsNew { get; set; }

        public bool IsCopy { get; set; }

        public TriggerType Type { get; set; }

        [Required]
        public string Job { get; set; }

        public IEnumerable<string> JobList { get; set; }

        [Required]
        public string TriggerName { get; set; }

        [Required]
        public string TriggerGroup { get; set; }

        public string OldTriggerName { get; set; }

        public string OldTriggerGroup { get; set; }

        public IEnumerable<string> TriggerGroupList { get; set; }

        public string Description { get; set; }

        public string StartTimeUtc { get; set; }
        public string EndTimeUtc { get; set; }

        public DateTime? GetStartTimeUtc() => ParseDateTime(StartTimeUtc);

        public DateTime? GetEndTimeUtc() => ParseDateTime(EndTimeUtc);

        public Dictionary<string, string> TimeZoneList { get => TimeZoneInfo.GetSystemTimeZones().ToDictionary(); }

        private DateTime? ParseDateTime(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result) == false)
            {
                return null;
            }

            return result;
        }

        public string CalendarName { get; set; }

        public IEnumerable<string> CalendarNameList { get; set; }

        [Required]
        public int? Priority { get; set; }

        public IEnumerable<string> PriorityList => Enumerable.Range(1, 10).Select(x => x.ToString());

        public int PriorityOrDefault => Priority ?? 5;

        [Required]
        public int? MisfireInstruction { get; set; }

        public void Validate(ICollection<ValidationError> errors)
        {
            ModelValidator.ValidateObject(this, errors, nameof(TriggerViewModel.Trigger));

            if (Type == TriggerType.Unknown)
            {
                errors.Add(ValidationError.EmptyField("trigger[type]"));
            }

            if (Type == TriggerType.Daily)
            {
                Daily.Validate(errors);
            }

            if (Type == TriggerType.Calendar)
            {
                Calendar.Validate(errors);
            }

            if (Type == TriggerType.Cron)
            {
                Cron.Validate(errors);
            }

            if (Type == TriggerType.Simple)
            {
                Simple.Validate(errors);
            }
        }

        public string MisfireInstructionsJson => _misfireInstructionsJson;

        private static readonly string _misfireInstructionsJson = CreateMisfireInstructionsJson();

        private static string CreateMisfireInstructionsJson()
        {
            var standardMisfireInstructions = new Dictionary<int, string>()
            {
                [0] = "Smart Policy",
                [1] = "Fire Once Now",
                [2] = "Do Nothing",
            };

            var validMisfireInstructions = new Dictionary<string, Dictionary<int, string>>()
            {
                ["cron"] = standardMisfireInstructions,
                ["calendar"] = standardMisfireInstructions,
                ["daily"] = standardMisfireInstructions,
                ["simple"] = new Dictionary<int, string>()
                {
                    [0] = "Smart Policy",
                    [1] = "Fire Now",
                    [2] = "Reschedule Now With Existing Repeat Count",
                    [3] = "Reschedule Now With Remaining Repeat Count",
                    [4] = "Reschedule Next With Remaining Count",
                    [5] = "Reschedule Next With Existing Count",
                },
            };

            return JsonConvert.SerializeObject(validMisfireInstructions, Formatting.None);
        }

        public static async Task<TriggerPropertiesViewModel> CreateAsync(IScheduler scheduler)
        {
            var model = new TriggerPropertiesViewModel()
            {
                TriggerGroupList = (await scheduler.GetTriggerGroupNames().ConfigureAwait(false)).GroupArray(),
                TriggerGroup = SchedulerConstants.DefaultGroup,
                JobList = (await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).ConfigureAwait(false)).Select(x => x.ToString()).ToArray(),
                CalendarNameList = await scheduler.GetCalendarNames().ConfigureAwait(false),
            };

            model.Cron.TimeZone = TimeZoneInfo.Local.Id;

            model.Simple.RepeatInterval = 1;
            model.Simple.RepeatUnit = IntervalUnit.Minute;
            model.Simple.RepeatForever = true;

            model.Daily.DaysOfWeek.AllOn();
            model.Daily.RepeatInterval = 1;
            model.Daily.RepeatUnit = IntervalUnit.Minute;
            model.Daily.RepeatForever = true;
            model.Daily.TimeZone = TimeZoneInfo.Local.Id;

            model.Calendar.RepeatInterval = 1;
            model.Calendar.RepeatUnit = IntervalUnit.Minute;
            model.Calendar.TimeZone = TimeZoneInfo.Local.Id;

            return model;
        }
    }
}