using System.ComponentModel.DataAnnotations;
using static Quartz.MisfireInstruction;

namespace Quartzmin.Models
{
    public class CalendarTriggerViewModel : IHasValidation
    {
        [Required]
        public int? RepeatInterval { get; set; } // nullable to validate missing value

        [Required]
        public IntervalUnit RepeatUnit { get; set; }

        public string TimeZone { get; set; }

        public bool PreserveHourAcrossDst { get; set; }

        public bool SkipDayIfHourDoesNotExist { get; set; }

        public void Validate(ICollection<ValidationError> errors)
        {
            ModelValidator.ValidateObject(this, errors, nameof(TriggerViewModel.Trigger), nameof(TriggerPropertiesViewModel.Calendar));
        }

        public static CalendarTriggerViewModel FromTrigger(ICalendarIntervalTrigger trigger)
        {
            return new CalendarTriggerViewModel()
            {
                RepeatInterval = trigger.RepeatInterval,
                RepeatUnit = trigger.RepeatIntervalUnit,
                PreserveHourAcrossDst = trigger.PreserveHourOfDayAcrossDaylightSavings,
                SkipDayIfHourDoesNotExist = trigger.SkipDayIfHourDoesNotExist,
                TimeZone = trigger.TimeZone.Id,
            };
        }

        public void Apply(TriggerBuilder builder, TriggerPropertiesViewModel model)
        {
            builder.WithCalendarIntervalSchedule(x =>
            {
                if (!string.IsNullOrEmpty(TimeZone))
                {
                    x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(TimeZone));
                }

                x.WithInterval(RepeatInterval.Value, RepeatUnit);
                x.PreserveHourOfDayAcrossDaylightSavings(PreserveHourAcrossDst);
                x.SkipDayIfHourDoesNotExist(SkipDayIfHourDoesNotExist);

                switch (model.MisfireInstruction)
                {
                    case InstructionNotSet:
                        break;
                    case IgnoreMisfirePolicy:
                        x.WithMisfireHandlingInstructionIgnoreMisfires();
                        break;
                    case CalendarIntervalTrigger.DoNothing:
                        x.WithMisfireHandlingInstructionDoNothing();
                        break;
                    case CalendarIntervalTrigger.FireOnceNow:
                        x.WithMisfireHandlingInstructionFireAndProceed();
                        break;
                    default:
                        throw new ArgumentException("Invalid value: " + model.MisfireInstruction, nameof(model.MisfireInstruction));
                }
            });
        }
    }
}