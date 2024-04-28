using System.Text.RegularExpressions;

namespace CronExpressionDescriptor
{
    /// <summary>
    /// Converts a Cron Expression into a human readable string
    /// </summary>
    public class ExpressionDescriptor
    {
        private readonly char[] _specialCharacters = new char[] { '/', '-', ',', '*' };
        private readonly string[] _24hourTimeFormatTwoLetterISOLanguageName = new string[] { "ru", "uk", "de", "it", "tr", "pl", "ro", "da", "sl" };

        private readonly string _expression;
        private readonly Options _options;
        private readonly bool _use24HourTimeFormat;
        private readonly CultureInfo _culture;
        private string[] _expressionParts;
        private bool _parsed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpressionDescriptor"/> class
        /// </summary>
        /// <param name="expression">The cron expression string</param>
        public ExpressionDescriptor(string expression)
            : this(expression, new Options()) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpressionDescriptor"/> class
        /// </summary>
        /// <param name="expression">The cron expression string</param>
        /// <param name="options">Options to control the output description</param>
        public ExpressionDescriptor(string expression, Options options)
        {
            _expression = expression;
            _options = options;
            _expressionParts = new string[7];
            _parsed = false;

            if (!string.IsNullOrEmpty(options.Locale))
            {
                _culture = new CultureInfo(options.Locale);
            }
            else
            {
                // If options.Locale not specified...
                _culture = new CultureInfo("en-US");
            }

            if (_options.Use24HourTimeFormat != null)
            {
                // 24HourTimeFormat specified in options so use it
                _use24HourTimeFormat = _options.Use24HourTimeFormat.Value;
            }
            else
            {
                // 24HourTimeFormat not specified, default based on m_24hourTimeFormatLocales
                _use24HourTimeFormat = _24hourTimeFormatTwoLetterISOLanguageName.Contains(_culture.TwoLetterISOLanguageName);
            }
        }

        /// <summary>
        /// Generates a human readable string for the Cron Expression
        /// </summary>
        /// <param name="type">Which part(s) of the expression to describe</param>
        /// <returns>The cron expression description</returns>
        public string GetDescription(DescriptionTypeEnum type)
        {
            string description = string.Empty;

            try
            {
                if (!_parsed)
                {
                    ExpressionParser parser = new ExpressionParser(_expression, _options);
                    _expressionParts = parser.Parse();
                    _parsed = true;
                }

                description = type switch
                {
                    DescriptionTypeEnum.FULL => GetFullDescription(),
                    DescriptionTypeEnum.TIMEOFDAY => GetTimeOfDayDescription(),
                    DescriptionTypeEnum.HOURS => GetHoursDescription(),
                    DescriptionTypeEnum.MINUTES => GetMinutesDescription(),
                    DescriptionTypeEnum.SECONDS => GetSecondsDescription(),
                    DescriptionTypeEnum.DAYOFMONTH => GetDayOfMonthDescription(),
                    DescriptionTypeEnum.MONTH => GetMonthDescription(),
                    DescriptionTypeEnum.DAYOFWEEK => GetDayOfWeekDescription(),
                    DescriptionTypeEnum.YEAR => GetYearDescription(),
                    _ => GetSecondsDescription(),
                };
            }
            catch (Exception ex)
            {
                if (!_options.ThrowExceptionOnParseError)
                {
                    description = ex.Message;
                }
                else
                {
                    throw;
                }
            }

            // Uppercase the first letter
            description = string.Concat(_culture.TextInfo.ToUpper(description[0]), description.Substring(1));

            return description;
        }

        /// <summary>
        /// Generates the FULL description
        /// </summary>
        /// <returns>The FULL description</returns>
        protected string GetFullDescription()
        {
            string description;

            try
            {
                string timeSegment = GetTimeOfDayDescription();
                string dayOfMonthDesc = GetDayOfMonthDescription();
                string monthDesc = GetMonthDescription();
                string dayOfWeekDesc = GetDayOfWeekDescription();
                string yearDesc = GetYearDescription();

                description = string.Format("{0}{1}{2}{3}{4}",
                       timeSegment,
                       dayOfMonthDesc,
                       dayOfWeekDesc,
                       monthDesc,
                       yearDesc);

                description = TransformVerbosity(description, _options.Verbose);
            }
            catch (Exception ex)
            {
                description = GetString("AnErrorOccuredWhenGeneratingTheExpressionD");
                if (_options.ThrowExceptionOnParseError)
                {
                    throw new FormatException(description, ex);
                }
            }

            return description;
        }

        /// <summary>
        /// Generates a description for only the TIMEOFDAY portion of the expression
        /// </summary>
        /// <returns>The TIMEOFDAY description</returns>
        protected string GetTimeOfDayDescription()
        {
            string secondsExpression = _expressionParts[0];
            string minuteExpression = _expressionParts[1];
            string hourExpression = _expressionParts[2];

            StringBuilder description = new StringBuilder();

            // handle special cases first
            if (minuteExpression.IndexOfAny(_specialCharacters) == -1
                && hourExpression.IndexOfAny(_specialCharacters) == -1
                && secondsExpression.IndexOfAny(_specialCharacters) == -1)
            {
                // specific time of day (i.e. 10 14)
                description.Append(GetString("AtSpace")).Append(FormatTime(hourExpression, minuteExpression, secondsExpression));
            }
            else if (secondsExpression == string.Empty && minuteExpression.Contains('-')
                && !minuteExpression.Contains(',')
                && hourExpression.IndexOfAny(_specialCharacters) == -1)
            {
                // minute range in single hour (i.e. 0-10 11)
                string[] minuteParts = minuteExpression.Split('-');
                description.Append(string.Format(GetString("EveryMinuteBetweenX0AndX1"),
                    FormatTime(hourExpression, minuteParts[0]),
                    FormatTime(hourExpression, minuteParts[1])));
            }
            else if (secondsExpression == string.Empty && hourExpression.Contains(',')
                && hourExpression.IndexOf('-') == -1
                && minuteExpression.IndexOfAny(_specialCharacters) == -1)
            {
                // hours list with single minute (o.e. 30 6,14,16)
                string[] hourParts = hourExpression.Split(',');
                description.Append(GetString("At"));
                for (int i = 0; i < hourParts.Length; i++)
                {
                    description.Append(" ").Append(FormatTime(hourParts[i], minuteExpression));

                    if (i < (hourParts.Length - 2))
                    {
                        description.Append(",");
                    }

                    if (i == hourParts.Length - 2)
                    {
                        description.Append(GetString("SpaceAnd"));
                    }
                }
            }
            else
            {
                // default time description
                string secondsDescription = GetSecondsDescription();
                string minutesDescription = GetMinutesDescription();
                string hoursDescription = GetHoursDescription();

                description.Append(secondsDescription);

                if (description.Length > 0)
                {
                    description.Append(", ");
                }

                description.Append(minutesDescription);

                if (description.Length > 0)
                {
                    description.Append(", ");
                }

                description.Append(hoursDescription);
            }

            return description.ToString();
        }

        /// <summary>
        /// Generates a description for only the SECONDS portion of the expression
        /// </summary>
        /// <returns>The SECONDS description</returns>
        protected string GetSecondsDescription()
        {
            string description = GetSegmentDescription(
               _expressionParts[0],
               GetString("EverySecond"),
               s => s,
               s => string.Format(GetString("EveryX0Seconds"), s),
               s => GetString("SecondsX0ThroughX1PastTheMinute"),
               s =>
               {
                   int i = 0;
                   if (int.TryParse(s, out i))
                   {
                       return s == "0"
                        ? string.Empty
                        : (i < 20)
                            ? GetString("AtX0SecondsPastTheMinute")
                            : GetString("AtX0SecondsPastTheMinuteGt20") ?? GetString("AtX0SecondsPastTheMinute");
                   }
                   else
                   {
                       return GetString("AtX0SecondsPastTheMinute");
                   }
               },
               s => GetString("ComaMinX0ThroughMinX1") ?? GetString("ComaX0ThroughX1"));

            return description;
        }

        /// <summary>
        /// Generates a description for only the MINUTE portion of the expression
        /// </summary>
        /// <returns>The MINUTE description</returns>
        protected string GetMinutesDescription()
        {
            string description = GetSegmentDescription(
                expression: _expressionParts[1],
                allDescription: GetString("EveryMinute"),
                getSingleItemDescription: s => s,
                getIntervalDescriptionFormat: s => string.Format(GetString("EveryX0Minutes"), s),
                getBetweenDescriptionFormat: s => GetString("MinutesX0ThroughX1PastTheHour"),
                getDescriptionFormat: s =>
                {
                    int i = 0;
                    if (int.TryParse(s, out i))
                    {
                        return s == "0"
                          ? string.Empty
                          : (int.Parse(s) < 20)
                              ? GetString("AtX0MinutesPastTheHour")
                              : GetString("AtX0MinutesPastTheHourGt20") ?? GetString("AtX0MinutesPastTheHour");
                    }
                    else
                    {
                        return GetString("AtX0MinutesPastTheHour");
                    }
                },
                getRangeFormat: s => GetString("ComaMinX0ThroughMinX1") ?? GetString("ComaX0ThroughX1"));

            return description;
        }

        /// <summary>
        /// Generates a description for only the HOUR portion of the expression
        /// </summary>
        /// <returns>The HOUR description</returns>
        protected string GetHoursDescription()
        {
            string expression = _expressionParts[2];
            string description = GetSegmentDescription(expression,
                   GetString("EveryHour"),
                   s => FormatTime(s, "0"),
                   s => string.Format(GetString("EveryX0Hours"), s),
                   s => GetString("BetweenX0AndX1"),
                   s => GetString("AtX0"),
                   s => GetString("ComaMinX0ThroughMinX1") ?? GetString("ComaX0ThroughX1"));

            return description;
        }

        /// <summary>
        /// Generates a description for only the DAYOFWEEK portion of the expression
        /// </summary>
        /// <returns>The DAYOFWEEK description</returns>
        protected string GetDayOfWeekDescription()
        {
            string description = null;

            if (_expressionParts[5] == "*")
            {
                // DOW is specified as * so we will not generate a description and defer to DOM part.
                // Otherwise, we could get a contradiction like "on day 1 of the month, every day"
                // or a dupe description like "every day, every day".
                description = string.Empty;
            }
            else
            {
                description = GetSegmentDescription(
                    _expressionParts[5],
                    GetString("ComaEveryDay"),
                    s =>
                    {
                        string exp = s.Contains('#')
                            ? s.Remove(s.IndexOf("#"))
                            : s.Contains('L')
                                ? s.Replace("L", string.Empty)
                                : s;

                        return _culture.DateTimeFormat.GetDayName((DayOfWeek)Convert.ToInt32(exp));
                    },
                    s => string.Format(GetString("ComaEveryX0DaysOfTheWeek"), s),
                    s => GetString("ComaX0ThroughX1"),
                    s =>
                    {
                        string format = null;
                        if (s.Contains('#'))
                        {
                            string dayOfWeekOfMonthNumber = s.Substring(s.IndexOf("#") + 1);
                            string dayOfWeekOfMonthDescription = null;
                            switch (dayOfWeekOfMonthNumber)
                            {
                                case "1":
                                    dayOfWeekOfMonthDescription = GetString("First");
                                    break;
                                case "2":
                                    dayOfWeekOfMonthDescription = GetString("Second");
                                    break;
                                case "3":
                                    dayOfWeekOfMonthDescription = GetString("Third");
                                    break;
                                case "4":
                                    dayOfWeekOfMonthDescription = GetString("Fourth");
                                    break;
                                case "5":
                                    dayOfWeekOfMonthDescription = GetString("Fifth");
                                    break;
                            }

                            format = string.Concat(GetString("ComaOnThe"),
                                dayOfWeekOfMonthDescription, GetString("SpaceX0OfTheMonth"));
                        }
                        else if (s.Contains('L'))
                        {
                            format = GetString("ComaOnTheLastX0OfTheMonth");
                        }
                        else
                        {
                            format = GetString("ComaOnlyOnX0");
                        }

                        return format;
                    },
                    s => GetString("ComaX0ThroughX1"));
            }

            return description;
        }

        /// <summary>
        /// Generates a description for only the MONTH portion of the expression
        /// </summary>
        /// <returns>The MONTH description</returns>
        protected string GetMonthDescription()
        {
            string description = GetSegmentDescription(
                _expressionParts[4],
                string.Empty,
                s => new DateTime(DateTime.Now.Year, Convert.ToInt32(s), 1).ToString("MMMM", _culture),
                s => string.Format(GetString("ComaEveryX0Months"), s),
                s => GetString("ComaMonthX0ThroughMonthX1") ?? GetString("ComaX0ThroughX1"),
                s => GetString("ComaOnlyInX0"),
                s => GetString("ComaMonthX0ThroughMonthX1") ?? GetString("ComaX0ThroughX1"));

            return description;
        }

        /// <summary>
        /// Generates a description for only the DAYOFMONTH portion of the expression
        /// </summary>
        /// <returns>The DAYOFMONTH description</returns>
        protected string GetDayOfMonthDescription()
        {
            string description = null;
            string expression = _expressionParts[3];

            switch (expression)
            {
                case "L":
                    description = GetString("ComaOnTheLastDayOfTheMonth");
                    break;
                case "WL":
                case "LW":
                    description = GetString("ComaOnTheLastWeekdayOfTheMonth");
                    break;
                default:
                    Regex weekDayNumberMatches = new Regex("(\\d{1,2}W)|(W\\d{1,2})");
                    if (weekDayNumberMatches.IsMatch(expression))
                    {
                        Match m = weekDayNumberMatches.Match(expression);
                        int dayNumber = int.Parse(m.Value.Replace("W", string.Empty));

                        string dayString = dayNumber == 1 ? GetString("FirstWeekday") :
                            string.Format(GetString("WeekdayNearestDayX0"), dayNumber);
                        description = string.Format(GetString("ComaOnTheX0OfTheMonth"), dayString);

                        break;
                    }
                    else
                    {
                        // Handle "last day offset" (i.e. L-5:  "5 days before the last day of the month")
                        Regex lastDayOffSetMatches = new Regex("L-(\\d{1,2})");
                        if (lastDayOffSetMatches.IsMatch(expression))
                        {
                            Match m = lastDayOffSetMatches.Match(expression);
                            string offSetDays = m.Groups[1].Value;
                            description = string.Format(GetString("CommaDaysBeforeTheLastDayOfTheMonth"), offSetDays);
                            break;
                        }
                        else
                        {
                            description = GetSegmentDescription(expression,
                                GetString("ComaEveryDay"),
                                s => s,
                                s => s == "1" ? GetString("ComaEveryDay") : GetString("ComaEveryX0Days"),
                                s => GetString("ComaBetweenDayX0AndX1OfTheMonth"),
                                s => GetString("ComaOnDayX0OfTheMonth"),
                                s => GetString("ComaX0ThroughX1"));
                            break;
                        }
                    }
            }

            return description;
        }

        /// <summary>
        /// Generates a description for only the YEAR portion of the expression
        /// </summary>
        /// <returns>The YEAR description</returns>
        private string GetYearDescription()
        {
            string description = GetSegmentDescription(_expressionParts[6],
                string.Empty,
                s => Regex.IsMatch(s, @"^\d+$") ?
                new DateTime(Convert.ToInt32(s), 1, 1).ToString("yyyy") : s,
                s => string.Format(GetString("ComaEveryX0Years"), s),
                s => GetString("ComaYearX0ThroughYearX1") ?? GetString("ComaX0ThroughX1"),
                s => GetString("ComaOnlyInX0"),
                s => GetString("ComaYearX0ThroughYearX1") ?? GetString("ComaX0ThroughX1"));

            return description;
        }

        /// <summary>
        /// Generates the segment description
        /// <remarks>
        /// Range expressions used the 'ComaX0ThroughX1' resource
        /// However Romanian language has different idioms for
        /// 1. 'from number to number' (minutes, seconds, hours, days) => ComaMinX0ThroughMinX1 optional resource
        /// 2. 'from month to month' ComaMonthX0ThroughMonthX1 optional resource
        /// 3. 'from year to year' => ComaYearX0ThroughYearX1 optional resource
        /// therefore <paramref name="getRangeFormat"/> was introduced
        /// </remarks>
        /// </summary>
        protected string GetSegmentDescription(string expression,
            string allDescription,
            Func<string, string> getSingleItemDescription,
            Func<string, string> getIntervalDescriptionFormat,
            Func<string, string> getBetweenDescriptionFormat,
            Func<string, string> getDescriptionFormat,
            Func<string, string> getRangeFormat)
        {
            string description = null;

            if (string.IsNullOrEmpty(expression))
            {
                description = string.Empty;
            }
            else if (expression == "*")
            {
                description = allDescription;
            }
            else if (expression.IndexOfAny(new char[] { '/', '-', ',' }) == -1)
            {
                description = string.Format(getDescriptionFormat(expression), getSingleItemDescription(expression));
            }
            else if (expression.Contains('/'))
            {
                string[] segments = expression.Split('/');
                description = string.Format(getIntervalDescriptionFormat(segments[1]), getSingleItemDescription(segments[1]));

                // interval contains 'between' piece (i.e. 2-59/3 )
                if (segments[0].Contains('-'))
                {
                    string betweenSegmentDescription = GenerateBetweenSegmentDescription(segments[0], getBetweenDescriptionFormat, getSingleItemDescription);

                    if (!betweenSegmentDescription.StartsWith(", "))
                    {
                        description += ", ";
                    }

                    description += betweenSegmentDescription;
                }
                else if (segments[0].IndexOfAny(new char[] { '*', ',' }) == -1)
                {
                    string rangeItemDescription = string.Format(getDescriptionFormat(segments[0]), getSingleItemDescription(segments[0]));

                    // remove any leading comma
                    rangeItemDescription = rangeItemDescription.Replace(", ", string.Empty);

                    description += string.Format(GetString("CommaStartingX0"), rangeItemDescription);
                }
            }
            else if (expression.Contains(','))
            {
                string[] segments = expression.Split(',');

                string descriptionContent = string.Empty;
                for (int i = 0; i < segments.Length; i++)
                {
                    if (i > 0 && segments.Length > 2)
                    {
                        descriptionContent += ",";

                        if (i < segments.Length - 1)
                        {
                            descriptionContent += " ";
                        }
                    }

                    if (i > 0 && segments.Length > 1 && (i == segments.Length - 1 || segments.Length == 2))
                    {
                        descriptionContent += GetString("SpaceAndSpace");
                    }

                    if (segments[i].Contains('-'))
                    {
                        string betweenSegmentDescription = GenerateBetweenSegmentDescription(segments[i], getRangeFormat, getSingleItemDescription);

                        // remove any leading comma
                        betweenSegmentDescription = betweenSegmentDescription.Replace(", ", string.Empty);

                        descriptionContent += betweenSegmentDescription;
                    }
                    else
                    {
                        descriptionContent += getSingleItemDescription(segments[i]);
                    }
                }

                description = string.Format(getDescriptionFormat(expression), descriptionContent);
            }
            else if (expression.Contains('-'))
            {
                description = GenerateBetweenSegmentDescription(expression, getBetweenDescriptionFormat, getSingleItemDescription);
            }

            return description;
        }

        /// <summary>
        /// Generates the between segment description
        /// </summary>
        /// <returns>The between segment description</returns>
        protected string GenerateBetweenSegmentDescription(string betweenExpression, Func<string, string> getBetweenDescriptionFormat, Func<string, string> getSingleItemDescription)
        {
            string description = string.Empty;
            string[] betweenSegments = betweenExpression.Split('-');
            string betweenSegment1Description = getSingleItemDescription(betweenSegments[0]);
            string betweenSegment2Description = getSingleItemDescription(betweenSegments[1]);
            betweenSegment2Description = betweenSegment2Description.Replace(":00", ":59");
            var betweenDescriptionFormat = getBetweenDescriptionFormat(betweenExpression);
            description += string.Format(betweenDescriptionFormat, betweenSegment1Description, betweenSegment2Description);

            return description;
        }

        /// <summary>
        /// Given time parts, will construct a formatted time description
        /// </summary>
        /// <param name="hourExpression">Hours part</param>
        /// <param name="minuteExpression">Minutes part</param>
        /// <returns>Formatted time description</returns>
        protected string FormatTime(string hourExpression, string minuteExpression)
        {
            return FormatTime(hourExpression, minuteExpression, string.Empty);
        }

        /// <summary>
        /// Given time parts, will construct a formatted time description
        /// </summary>
        /// <param name="hourExpression">Hours part</param>
        /// <param name="minuteExpression">Minutes part</param>
        /// <param name="secondExpression">Seconds part</param>
        /// <returns>Formatted time description</returns>
        protected string FormatTime(string hourExpression, string minuteExpression, string secondExpression)
        {
            int hour = Convert.ToInt32(hourExpression);

            string period = string.Empty;
            if (!_use24HourTimeFormat)
            {
                period = GetString((hour >= 12) ? "PMPeriod" : "AMPeriod");
                if (period.Length > 0)
                {
                    // add preceding space
                    period = string.Concat(" ", period);
                }

                if (hour > 12)
                {
                    hour -= 12;
                }

                if (hour == 0)
                {
                    hour = 12;
                }
            }

            string minute = Convert.ToInt32(minuteExpression).ToString();
            string second = string.Empty;
            if (!string.IsNullOrEmpty(secondExpression))
            {
                second = string.Concat(":", Convert.ToInt32(secondExpression).ToString().PadLeft(2, '0'));
            }

            return string.Format("{0}:{1}{2}{3}",
                hour.ToString().PadLeft(2, '0'), minute.PadLeft(2, '0'), second, period);
        }

        /// <summary>
        /// Transforms the verbosity of the expression description by stripping verbosity from original description
        /// </summary>
        /// <param name="description">The description to transform</param>
        /// <param name="useVerboseFormat">If true, will leave description as it, if false, will strip verbose parts</param>
        /// <returns>The transformed description with proper verbosity</returns>
        protected string TransformVerbosity(string description, bool useVerboseFormat)
        {
            if (!useVerboseFormat)
            {
                description = description.Replace(GetString("ComaEveryMinute"), string.Empty);
                description = description.Replace(GetString("ComaEveryHour"), string.Empty);
                description = description.Replace(GetString("ComaEveryDay"), string.Empty);
            }

            return description;
        }

        /// <summary>
        /// Gets a localized string resource
        /// refactored because Resources.ResourceManager.GetString was way too long
        /// </summary>
        /// <param name="resourceName">name of the resource</param>
        /// <returns>translated resource</returns>
        protected string GetString(string resourceName)
        {
            return Resources.GetString(resourceName);
        }

        #region Static

        /// <summary>
        /// Generates a human readable string for the Cron Expression
        /// </summary>
        /// <param name="expression">The cron expression string</param>
        /// <returns>The cron expression description</returns>
        public static string GetDescription(string expression)
        {
            return GetDescription(expression, new Options());
        }

        /// <summary>
        /// Generates a human readable string for the Cron Expression
        /// </summary>
        /// <param name="expression">The cron expression string</param>
        /// <param name="options">Options to control the output description</param>
        /// <returns>The cron expression description</returns>
        public static string GetDescription(string expression, Options options)
        {
            ExpressionDescriptor descripter = new ExpressionDescriptor(expression, options);
            return descripter.GetDescription(DescriptionTypeEnum.FULL);
        }
        #endregion
    }
}
