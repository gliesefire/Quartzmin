namespace Quartzmin.Models
{
    public class TriggerViewModel : IHasValidation
    {
        public TriggerPropertiesViewModel Trigger { get; set; }
        public JobDataMapModel DataMap { get; set; }

        public void Validate(ICollection<ValidationError> errors) => ModelValidator.ValidateObject(this, errors);
    }
}