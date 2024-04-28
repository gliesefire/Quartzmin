namespace Quartzmin.Helpers
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class SkipValidationAttribute : Attribute
    {
    }
}