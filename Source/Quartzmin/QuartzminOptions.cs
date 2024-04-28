﻿using Number = Quartzmin.TypeHandlers.NumberHandler.UnderlyingType;

namespace Quartzmin;

public class QuartzminOptions
{
    /// <summary>
    /// Gets or sets supports any value that is a viable as a img src attribute value: url, or base64
    /// src='data:image/jpeg;base64, LzlqLzRBQ...[end of base64 data]'
    /// Defaults to the quartzmin original logo
    /// </summary>
    public string Logo { get; set; } = "Content/Images/logo.png";

    public string ProductName { get; set; } = string.Empty;

    private string _virtualPathRoot = "/";
    public string VirtualPathRoot
    {
        get
        {
            var pathRoot = _virtualPathRoot;
            if (!pathRoot.EndsWith("/"))
            {
                pathRoot += "/";
            }

            return pathRoot;
        }
        set
        {
            _virtualPathRoot = value;
        }
    }

    public string WebAppName { get; set; }

    public IScheduler Scheduler { get; set; }

    /// <summary>
    /// Gets supported value types in job data map.
    /// </summary>
    public List<TypeHandlerBase> StandardTypes { get; } = new ();

    /// <summary>
    /// Gets or sets default type for new job data map item.
    /// </summary>
    public TypeHandlerBase DefaultSelectedType { get; set; }

    public string DefaultDateFormat
    {
        get => DateTimeSettings.DefaultDateFormat;
        set => DateTimeSettings.DefaultDateFormat = value;
    }

    public string DefaultTimeFormat
    {
        get => DateTimeSettings.DefaultTimeFormat;
        set => DateTimeSettings.DefaultTimeFormat = value;
    }

    public QuartzminOptions()
    {
        DefaultSelectedType = new StringHandler() { Name = "String" };

        // order of StandardTypes is important due to TypeHandlerBase.CanHandle evaluation
        StandardTypes.Add(new FileHandler() { Name = "File", DisplayName = "Binary Data" });
        StandardTypes.Add(new BooleanHandler() { Name = "Boolean" });
        StandardTypes.Add(new DateTimeHandler() { Name = "Date", DisplayName = "Date", IgnoreTimeComponent = true });
        StandardTypes.Add(new DateTimeHandler() { Name = "DateTime" });
        StandardTypes.Add(new DateTimeHandler() { Name = "DateTimeUtc", DisplayName = "DateTime (UTC)", IsUtc = true });
        StandardTypes.Add(new NumberHandler(Number.Decimal));
        StandardTypes.Add(new NumberHandler(Number.Double));
        StandardTypes.Add(new NumberHandler(Number.Float));
        StandardTypes.Add(new NumberHandler(Number.Integer));
        StandardTypes.Add(new NumberHandler(Number.Long));
        StandardTypes.Add(DefaultSelectedType); // String
        StandardTypes.Add(new StringHandler() { Name = "MultilineString", DisplayName = "String (Multiline)", IsMultiline = true });
    }

#if DEBUG
    public string SitePhysicalDirectory { get; set; }

    internal string ContentRootDirectory =>
        string.IsNullOrEmpty(SitePhysicalDirectory) ? null : Path.Combine(SitePhysicalDirectory, "Content");
    internal string ViewsRootDirectory =>
        string.IsNullOrEmpty(SitePhysicalDirectory) ? null : Path.Combine(SitePhysicalDirectory, "Views");
#else
        internal string ContentRootDirectory => null;
        internal string ViewsRootDirectory => null;
#endif
}