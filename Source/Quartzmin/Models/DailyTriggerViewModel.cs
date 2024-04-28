using System.ComponentModel.DataAnnotations;
using static Quartz.MisfireInstruction;

namespace Quartzmin.Models
{
    public class DailyTriggerViewModel : IHasValidation
    {
        public DaysOfWeekViewModel DaysOfWeek { get; set; } = new DaysOfWeekViewModel();

        [Required]
        public int? RepeatInterval { get; set; }

        [Required]
        public IntervalUnit RepeatUnit { get; set; }

        public int? RepeatCount { get; set; }

        public bool RepeatForever { get; set; }

        [Required]
        public TimeSpan? StartTime { get; set; }

        [Required]
        public TimeSpan? EndTime { get; set; }

        public string TimeZone { get; set; }

        public void Validate(ICollection<ValidationError> errors)
        {
            ModelValidator.ValidateObject(this, errors, nameof(TriggerViewModel.Trigger), nameof(TriggerPropertiesViewModel.Daily));

            if (RepeatForever == false && RepeatCount == null)
            {
                errors.Add(ValidationError.EmptyField("trigger[daily.repeatCount]"));
            }
        }

        public static DailyTriggerViewModel FromTrigger(IDailyTimeIntervalTrigger trigger)
        {
            var model = new DailyTriggerViewModel()
            {
                RepeatCount = trigger.RepeatCount,
                RepeatInterval = trigger.RepeatInterval,
                RepeatUnit = trigger.RepeatIntervalUnit,
                StartTime = trigger.StartTimeOfDay.ToTimeSpan(),
                EndTime = trigger.EndTimeOfDay.ToTimeSpan(),
                DaysOfWeek = DaysOfWeekViewModel.Create(trigger.DaysOfWeek),
                TimeZone = trigger.TimeZone.Id,
            };

            if (model.RepeatCount == -1)
            {
                model.RepeatCount = null;
                model.RepeatForever = true;
            }

            return model;
        }

        public void Apply(TriggerBuilder builder, TriggerPropertiesViewModel model)
        {
            builder.WithDailyTimeIntervalSchedule(x =>
            {
                if (!string.IsNullOrEmpty(TimeZone))
                {
                    x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(TimeZone));
                }

                if (!RepeatForever)
                {
                    x.WithRepeatCount(RepeatCount.Value);
                }

                x.WithInterval(RepeatInterval.Value, RepeatUnit);
                x.StartingDailyAt(StartTime.Value.ToTimeOfDay());
                x.EndingDailyAt(EndTime.Value.ToTimeOfDay());
                x.OnDaysOfTheWeek(DaysOfWeek.GetSelected().ToArray());

                switch (model.MisfireInstruction)
                {
                    case InstructionNotSet:
                        break;
                    case IgnoreMisfirePolicy:
                        x.WithMisfireHandlingInstructionIgnoreMisfires();
                        break;
                    case DailyTimeIntervalTrigger.DoNothing:
                        x.WithMisfireHandlingInstructionDoNothing();
                        break;
                    case DailyTimeIntervalTrigger.FireOnceNow:
                        x.WithMisfireHandlingInstructionFireAndProceed();
                        break;
                    default:
                        throw new ArgumentException("Invalid value: " + model.MisfireInstruction, nameof(model.MisfireInstruction));
                }
            });
        }
    }
}