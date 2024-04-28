using System.ComponentModel.DataAnnotations;
using Quartz.Impl.Triggers;
using static Quartz.MisfireInstruction;

namespace Quartzmin.Models
{
    public class SimpleTriggerViewModel : IHasValidation
    {
        [Required]
        public int? RepeatInterval { get; set; }

        [Required]
        public IntervalUnit RepeatUnit { get; set; }

        public int? RepeatCount { get; set; }

        public bool RepeatForever { get; set; }

        public void Validate(ICollection<ValidationError> errors)
        {
            ModelValidator.ValidateObject(this, errors, nameof(TriggerViewModel.Trigger), nameof(TriggerPropertiesViewModel.Simple));

            if (RepeatForever == false && RepeatCount == null)
            {
                errors.Add(ValidationError.EmptyField("trigger[simple.repeatCount]"));
            }
        }

        public static SimpleTriggerViewModel FromTrigger(ISimpleTrigger trigger)
        {
            var model = new SimpleTriggerViewModel()
            {
                RepeatCount = trigger.RepeatCount,
                RepeatForever = trigger.RepeatCount == SimpleTriggerImpl.RepeatIndefinitely,
                RepeatInterval = (int)trigger.RepeatInterval.TotalMilliseconds,
                RepeatUnit = IntervalUnit.Millisecond,
            };

            if (model.RepeatCount == -1)
            {
                model.RepeatCount = null;
            }

            if (trigger.RepeatInterval.Milliseconds == 0 && model.RepeatInterval > 0)
            {
                model.RepeatInterval = (int)trigger.RepeatInterval.TotalSeconds;
                model.RepeatUnit = IntervalUnit.Second;
                if (trigger.RepeatInterval.Seconds == 0)
                {
                    model.RepeatInterval = (int)trigger.RepeatInterval.TotalMinutes;
                    model.RepeatUnit = IntervalUnit.Minute;
                    if (trigger.RepeatInterval.Minutes == 0)
                    {
                        model.RepeatInterval = (int)trigger.RepeatInterval.TotalHours;
                        model.RepeatUnit = IntervalUnit.Hour;
                        if (trigger.RepeatInterval.Hours == 0)
                        {
                            model.RepeatInterval = (int)trigger.RepeatInterval.TotalDays;
                            model.RepeatUnit = IntervalUnit.Day;
                        }
                    }
                }
            }

            return model;
        }

        private TimeSpan GetRepeatIntervalTimeSpan()
        {
            switch (RepeatUnit)
            {
                case IntervalUnit.Millisecond:
                    return TimeSpan.FromMilliseconds(RepeatInterval.Value);
                case IntervalUnit.Second:
                    return TimeSpan.FromSeconds(RepeatInterval.Value);
                case IntervalUnit.Minute:
                    return TimeSpan.FromMinutes(RepeatInterval.Value);
                case IntervalUnit.Hour:
                    return TimeSpan.FromHours(RepeatInterval.Value);
                case IntervalUnit.Day:
                    return TimeSpan.FromDays(RepeatInterval.Value);
                default:
                    throw new ArgumentException("Invalid value: " + RepeatUnit, nameof(RepeatUnit));
            }
        }

        public void Apply(TriggerBuilder builder, TriggerPropertiesViewModel model)
        {
            builder.WithSimpleSchedule(x =>
            {
                x.WithInterval(GetRepeatIntervalTimeSpan());

                if (RepeatForever)
                {
                    x.RepeatForever();
                }
                else
                {
                    x.WithRepeatCount(RepeatCount.Value);
                }

                switch (model.MisfireInstruction)
                {
                    case InstructionNotSet:
                        break;
                    case IgnoreMisfirePolicy:
                        x.WithMisfireHandlingInstructionIgnoreMisfires();
                        break;
                    case SimpleTrigger.FireNow:
                        x.WithMisfireHandlingInstructionFireNow();
                        break;
                    case SimpleTrigger.RescheduleNowWithExistingRepeatCount:
                        x.WithMisfireHandlingInstructionNowWithExistingCount();
                        break;
                    case SimpleTrigger.RescheduleNowWithRemainingRepeatCount:
                        x.WithMisfireHandlingInstructionNowWithRemainingCount();
                        break;
                    case SimpleTrigger.RescheduleNextWithRemainingCount:
                        x.WithMisfireHandlingInstructionNextWithExistingCount();
                        break;
                    default:
                        throw new ArgumentException("Invalid value: " + model.MisfireInstruction, nameof(model.MisfireInstruction));
                }
            });
        }
    }
}