namespace Quartzmin.Models
{
    public class JobViewModel : IHasValidation
    {
        public JobPropertiesViewModel Job { get; set; }
        public JobDataMapModel DataMap { get; set; }

        public void Validate(ICollection<ValidationError> errors) => ModelValidator.ValidateObject(this, errors);
    }
}
