namespace PluralsightDownloader.Helpers
{
    using System;
    using System.Globalization;
    using System.Windows.Data;
    public class TimeDisplayConverter : IValueConverter
    {
        public static string TimeToReadbleFormat(int time)
        {
            if (time == 0)
            {
                return string.Empty;
            }

            var seconds = (int)time;
            var min = seconds / 60;
            var sec = seconds % 60;

            if (min < 60)
            {
                return string.Format("{0}m {1}s", min, sec);
            }
            var hour = min / 60;
            min = min % 60;
            return string.Format("{0}h {1}m {2}s", hour, min, sec);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return TimeToReadbleFormat(System.Convert.ToInt32(value));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
