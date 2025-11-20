using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IndoorCO2MapAppV2.UIUtility
{
    internal class BooleanToTextConverter : IValueConverter
    {
        public string TrueText { get; set; } = "True";
        public string FalseText { get; set; } = "False";

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? TrueText : FalseText;
            return FalseText;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
