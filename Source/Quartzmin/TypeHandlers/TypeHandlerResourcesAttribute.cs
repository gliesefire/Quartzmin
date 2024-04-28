namespace Quartzmin.TypeHandlers
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class TypeHandlerResourcesAttribute : Attribute
    {
        public string Template { get; set; }
        public string Script { get; set; }

        public static TypeHandlerResourcesAttribute GetResolved(Type type)
        {
            var attr = type.GetCustomAttribute<TypeHandlerResourcesAttribute>(inherit: true)
                ?? throw new ArgumentException(type.FullName + " missing attribute " + nameof(TypeHandlerResourcesAttribute));
            attr.Resolve();
            return attr;
        }

        protected virtual void Resolve() { }
    }
}
