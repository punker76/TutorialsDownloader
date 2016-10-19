namespace PluralsightDownloader.Helpers
{
    using System;
    using System.Globalization;
    using System.Windows.Data;
    using System.Windows.Media;

    public class DownloadStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var downloaded = (bool)value;
            return downloaded ? "CheckboxMarkedCircleOutline" : "CheckboxBlankCircleOutline";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
