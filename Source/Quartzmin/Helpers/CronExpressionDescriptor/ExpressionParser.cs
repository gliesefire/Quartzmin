﻿using System.Text.RegularExpressions;

namespace CronExpressionDescriptor
{
    /// <summary>
    /// Cron Expression Parser
    /// </summary>
    public class ExpressionParser
    {
        /* Cron reference

          ┌───────────── minute (0 - 59)
          │ ┌───────────── hour (0 - 23)
          │ │ ┌───────────── day of month (1 - 31)
          │ │ │ ┌───────────── month (1 - 12)
          │ │ │ │ ┌───────────── day of week (0 - 6) (Sunday to Saturday; 7 is also Sunday on some systems)
          │ │ │ │ │
          │ │ │ │ │
          │ │ │ │ │
          * * * * *  command to execute

         */

        private readonly string _expression;
        private readonly Options _options;
        private readonly CultureInfo _en_culture;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpressionParser"/> class
        /// </summary>
        /// <param name="expression">The cron expression string</param>
        /// <param name="options">Parsing options</param>
        public ExpressionParser(string expression, Options options)
        {
            _expression = expression;
            _options = options;
            _en_culture = new CultureInfo("en-US"); // Default to English
        }

        /// <summary>
        /// Parses the cron expression string
        /// </summary>
        /// <returns>A 7 part string array, one part for each component of the cron expression (seconds, minutes, etc.)</returns>
        public string[] Parse()
        {
            // Initialize all elements of parsed array to empty strings
            string[] parsed = new string[7].Select(el => string.Empty).ToArray();

            if (string.IsNullOrEmpty(_expression))
            {
                throw new MissingFieldException("Field 'expression' not found.");
            }
            else
            {
                string[] expressionPartsTemp = _expression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (expressionPartsTemp.Length < 5)
                {
                    throw new FormatException(string.Format("Error: Expression only has {0} parts.  At least 5 part are required.", expressionPartsTemp.Length));
                }
                else if (expressionPartsTemp.Length == 5)
                {
                    // 5 part cron so shift array past seconds element
                    Array.Copy(expressionPartsTemp, 0, parsed, 1, 5);
                }
                else if (expressionPartsTemp.Length == 6)
                {
                    // If last element ends with 4 digits, a year element has been supplied and no seconds element
                    Regex yearRegex = new Regex("\\d{4}$");
                    if (yearRegex.IsMatch(expressionPartsTemp[5]))
                    {
                        Array.Copy(expressionPartsTemp, 0, parsed, 1, 6);
                    }
                    else
                    {
                        Array.Copy(expressionPartsTemp, 0, parsed, 0, 6);
                    }
                }
                else if (expressionPartsTemp.Length == 7)
                {
                    parsed = expressionPartsTemp;
                }
                else
                {
                    throw new FormatException(string.Format("Error: Expression has too many parts ({0}).  Expression must not have more than 7 parts.", expressionPartsTemp.Length));
                }
            }

            NormalizeExpression(parsed);

            return parsed;
        }

        /// <summary>
        /// Converts cron expression components into consistent, predictable formats.
        /// </summary>
        /// <param name="expressionParts">A 7 part string array, one part for each component of the cron expression</param>
        private void NormalizeExpression(string[] expressionParts)
        {
            // Convert ? to * only for DOM and DOW
            expressionParts[3] = expressionParts[3].Replace("?", "*");
            expressionParts[5] = expressionParts[5].Replace("?", "*");

            // Convert 0/, 1/ to */
            if (expressionParts[0].StartsWith("0/"))
            {
                // Seconds
                expressionParts[0] = expressionParts[0].Replace("0/", "*/");
            }

            if (expressionParts[1].StartsWith("0/"))
            {
                // Minutes
                expressionParts[1] = expressionParts[1].Replace("0/", "*/");
            }

            if (expressionParts[2].StartsWith("0/"))
            {
                // Hours
                expressionParts[2] = expressionParts[2].Replace("0/", "*/");
            }

            if (expressionParts[3].StartsWith("1/"))
            {
                // DOM
                expressionParts[3] = expressionParts[3].Replace("1/", "*/");
            }

            if (expressionParts[4].StartsWith("1/"))
            {
                // Month
                expressionParts[4] = expressionParts[4].Replace("1/", "*/");
            }

            if (expressionParts[5].StartsWith("1/"))
            {
                // DOW
                expressionParts[5] = expressionParts[5].Replace("1/", "*/");
            }

            if (expressionParts[6].StartsWith("1/"))
            {
                // Years
                expressionParts[6] = expressionParts[6].Replace("1/", "*/");
            }

            // Handle DayOfWeekStartIndexZero option where SUN=1 rather than SUN=0
            if (!_options.DayOfWeekStartIndexZero)
            {
                expressionParts[5] = DecreaseDaysOfWeek(expressionParts[5]);
            }

            // Convert DOM '?' to '*'
            if (expressionParts[3] == "?")
            {
                expressionParts[3] = "*";
            }

            // Convert SUN-SAT format to 0-6 format
            for (int i = 0; i <= 6; i++)
            {
                DayOfWeek currentDay = (DayOfWeek)i;
                string currentDayOfWeekDescription = currentDay.ToString().Substring(0, 3).ToUpperInvariant();
                expressionParts[5] = Regex.Replace(expressionParts[5], currentDayOfWeekDescription, i.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            // Convert JAN-DEC format to 1-12 format
            for (int i = 1; i <= 12; i++)
            {
                DateTime currentMonth = new DateTime(DateTime.Now.Year, i, 1);
                string currentMonthDescription = currentMonth.ToString("MMM", _en_culture).ToUpperInvariant();
                expressionParts[4] = Regex.Replace(expressionParts[4], currentMonthDescription, i.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            // Convert 0 second to (empty)
            if (expressionParts[0] == "0")
            {
                expressionParts[0] = string.Empty;
            }

            // Loop through all parts and apply global normalization
            for (int i = 0; i < expressionParts.Length; i++)
            {
                // convert all '*/1' to '*'
                if (expressionParts[i] == "*/1")
                {
                    expressionParts[i] = "*";
                }

                /* Convert Month,DOW,Year step values with a starting value (i.e. not '*') to between expressions.
                   This allows us to reuse the between expression handling for step values.

                   For Example:
                    - month part '3/2' will be converted to '3-12/2' (every 2 months between March and December)
                    - DOW part '3/2' will be converted to '3-6/2' (every 2 days between Tuesday and Saturday)
                */

                if (expressionParts[i].Contains('/')
                    && expressionParts[i].IndexOfAny(new char[] { '*', '-', ',' }) == -1)
                {
                    var stepRangeThrough = i switch
                    {
                        4 => "12",
                        5 => "6",
                        6 => "9999",
                        _ => null,
                    };

                    if (stepRangeThrough != null)
                    {
                        string[] parts = expressionParts[i].Split('/');
                        expressionParts[i] = string.Format("{0}-{1}/{2}", parts[0], stepRangeThrough, parts[1]);
                    }
                }
            }
        }

        private static string DecreaseDaysOfWeek(string dayOfWeekExpressionPart)
        {
            var dowChars = dayOfWeekExpressionPart.ToCharArray();
            for (int i = 0; i < dowChars.Length; i++)
            {
                if ((i == 0 || (dowChars[i - 1] != '#' && dowChars[i - 1] != '/'))
                    && int.TryParse(dowChars[i].ToString(), out var charNumeric))
                {
                    dowChars[i] = (charNumeric - 1).ToString()[0];
                }
            }

            return new string(dowChars);
        }
    }
}
