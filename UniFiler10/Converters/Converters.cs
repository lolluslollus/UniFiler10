using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using UniFiler10.Data.Metadata;
using Utilz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using System.Collections;
using System.Globalization;

namespace UniFiler10.Converters
{
    public class IsNotNullToTrue : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return false;
            return true;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never come here");
        }
    }
    public class IsNotNullToVisible : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return Visibility.Collapsed;
            return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never come here");
        }

    }
    public class IListNotEmptyToTrue : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return false;
            if ((value as IList)?.Count > 0) return true;
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never come here");
        }
    }
    public class IListNotEmptyToVisible : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return Visibility.Visible;
            if ((value as IList)?.Count > 0) return Visibility.Visible;
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never come here");
        }
    }

    public class TrueToFalseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is bool)) return false;
            return !((bool)value);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is bool)) return false;
            return !((bool)value);
        }

    }

	public class BooleanToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is bool)) return Visibility.Collapsed;
            bool boo = (bool)value;
            if (boo) return Visibility.Visible;
            else return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }

    public class BooleanToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is bool)) return Visibility.Visible;
            bool boo = (bool)value;
            if (boo) return Visibility.Collapsed;
            else return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }
    public class TextEmptyToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is string)) return Visibility.Visible;
            string txt = value.ToString();
            if (string.IsNullOrWhiteSpace(txt)) return Visibility.Visible;
            else return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }
    public class TextEmptyToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || !(value is string)) return Visibility.Collapsed;
            string txt = value.ToString();
            if (string.IsNullOrWhiteSpace(txt)) return Visibility.Collapsed;
            else return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way bonding, it should never come here");
        }
    }
	public class StringFormatterConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			string format = parameter.ToString();
			string output = string.Empty;
			try
			{
				output = string.Format(CultureInfo.CurrentUICulture, format, new object[1] { value });
			}
			catch (FormatException)
			{
				output = value.ToString();
			}
			return output;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class ReduceWidthForText : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null) return 0.0;

			double par = 0.0;
			if (parameter!=null)
				double.TryParse(
					parameter.ToString(),
					NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowThousands | NumberStyles.AllowTrailingWhite, 
					CultureInfo.InvariantCulture,
					out par);

			double val = 0.0;
			double.TryParse(
				value.ToString(),
				NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowThousands | NumberStyles.AllowTrailingWhite,
				CultureInfo.InvariantCulture,
				out val);

			return Math.Max(0.0, val - par);
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}

}
