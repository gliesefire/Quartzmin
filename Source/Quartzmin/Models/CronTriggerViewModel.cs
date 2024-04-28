using System.ComponentModel.DataAnnotations;
using static Quartz.MisfireInstruction;

namespace Quartzmin.Models
{
    public class CronTriggerViewModel : IHasValidation
    {
        [Required]
        public string Expression { get; set; }
        public string TimeZone { get; set; }

        public void Validate(ICollection<ValidationError> errors)
        {
            ModelValidator.ValidateObject(this, errors, nameof(TriggerViewModel.Trigger), nameof(TriggerPropertiesViewModel.Cron));
        }

        public static CronTriggerViewModel FromTrigger(ICronTrigger trigger)
        {
            return new CronTriggerViewModel()
            {
                Expression = trigger.CronExpressionString,
                TimeZone = trigger.TimeZone.Id,
            };
        }

        public void Apply(TriggerBuilder builder, TriggerPropertiesViewModel model)
        {
            builder.WithCronSchedule(Expression, x =>
            {
                 if (!string.IsNullOrEmpty(TimeZone))
                {
                    x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(TimeZone));
                }

                 switch (model.MisfireInstruction)
                 {
                     case InstructionNotSet:
                         break;
                     case IgnoreMisfirePolicy:
                         x.WithMisfireHandlingInstructionIgnoreMisfires();
                         break;
                     case CronTrigger.DoNothing:
                         x.WithMisfireHandlingInstructionDoNothing();
                         break;
                     case CronTrigger.FireOnceNow:
                         x.WithMisfireHandlingInstructionFireAndProceed();
                         break;
                     default:
                         throw new ArgumentException("Invalid value: " + model.MisfireInstruction, nameof(model.MisfireInstruction));
                 }
            });
        }
    }
}