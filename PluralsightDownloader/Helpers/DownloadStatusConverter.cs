namespace PluralsightDownloader.Helpers
{
    using System;
    using System.Globalization;
    using System.Windows.Data;
    using System.Windows.Media;

    public class DownloadStatusConverter : IValueConverter
    {
        private readonly SolidColorBrush green = new SolidColorBrush(Color.FromArgb(255, 140, 191, 38));

        private readonly SolidColorBrush red = new SolidColorBrush(Color.FromArgb(255, 229, 20, 0));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var downloaded = (bool)value;
            return downloaded ? this.green : this.red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
