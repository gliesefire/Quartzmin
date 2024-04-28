namespace Quartzmin.Models
{
    public class DaysOfWeekViewModel
    {
        public bool Monday { get; set; }
        public bool Tuesday { get; set; }
        public bool Wednesday { get; set; }
        public bool Thursday { get; set; }
        public bool Friday { get; set; }
        public bool Saturday { get; set; }
        public bool Sunday { get; set; }

        public void AllOn()
        {
            Monday = true;
            Tuesday = true;
            Wednesday = true;
            Thursday = true;
            Friday = true;
            Saturday = true;
            Sunday = true;
        }

        public static DaysOfWeekViewModel Create(IEnumerable<DayOfWeek> list)
        {
            var model = new DaysOfWeekViewModel();
            foreach (var item in list)
            {
                if (item == DayOfWeek.Sunday)
                {
                    model.Sunday = true;
                }

                if (item == DayOfWeek.Monday)
                {
                    model.Monday = true;
                }

                if (item == DayOfWeek.Tuesday)
                {
                    model.Tuesday = true;
                }

                if (item == DayOfWeek.Wednesday)
                {
                    model.Wednesday = true;
                }

                if (item == DayOfWeek.Thursday)
                {
                    model.Thursday = true;
                }

                if (item == DayOfWeek.Friday)
                {
                    model.Friday = true;
                }

                if (item == DayOfWeek.Saturday)
                {
                    model.Saturday = true;
                }
            }

            return model;
        }

        public IEnumerable<DayOfWeek> GetSelected()
        {
            if (Monday)
            {
                yield return DayOfWeek.Monday;
            }

            if (Tuesday)
            {
                yield return DayOfWeek.Tuesday;
            }

            if (Wednesday)
            {
                yield return DayOfWeek.Wednesday;
            }

            if (Thursday)
            {
                yield return DayOfWeek.Thursday;
            }

            if (Friday)
            {
                yield return DayOfWeek.Friday;
            }

            if (Saturday)
            {
                yield return DayOfWeek.Saturday;
            }

            if (Sunday)
            {
                yield return DayOfWeek.Sunday;
            }
        }

        public bool AreOnlyWeekendEnabled => !Monday && !Tuesday && !Wednesday && !Thursday && !Friday && Saturday && Sunday;
        public bool AreOnlyWeekdaysEnabled => Monday && Tuesday && Wednesday && Thursday && Friday && !Saturday && !Sunday;
    }
}