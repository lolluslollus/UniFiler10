using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using System.Collections;
using System.Globalization;

namespace UniFiler10.Converters
{
	public class NotNullToTrue : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return value != null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}
	public class NotNullToVisible : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return value == null ? Visibility.Collapsed : Visibility.Visible;
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
	{// LOLLO TODO checl if you shouldn't rather bind to list.Count. This will only fire when the list is replaced, not when its count changed.
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
	public class IntZeroToVisible : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null) return Visibility.Visible;
			int iint = 1;
			int.TryParse(value.ToString(), out iint);
			if (iint > 0) return Visibility.Collapsed;
			return Visibility.Visible;
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
			if (!(value is bool)) return false;
			return !((bool)value);
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			if (!(value is bool)) return false;
			return !((bool)value);
		}
	}

	public class BooleanToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is bool)) return Visibility.Collapsed;
			bool boo = (bool)value;
			if (boo) return Visibility.Visible;
			return Visibility.Collapsed;
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
			if (!(value is bool)) return Visibility.Visible;
			bool boo = (bool)value;
			if (boo) return Visibility.Collapsed;
			return Visibility.Visible;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class StringEmptyToCollapsedConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null || string.IsNullOrEmpty(value.ToString())) return Visibility.Collapsed;
			else return Visibility.Visible;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}

	public class FalseToFlashyConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is bool)) return Application.Current.Resources["FlashyForeground"];
			bool boo = (bool)value;
			if (boo) return Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
			return Application.Current.Resources["FlashyForeground"];
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way bonding, it should never come here");
		}
	}

	public class ParameterMatchesToTrue : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			return !string.IsNullOrEmpty(parameter?.ToString()) && value.ToString().Equals(parameter.ToString());
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
			//if (!(value is bool) || string.IsNullOrEmpty(parameter.ToString())) return null;
			//return (bool)value ? parameter : null;
		}
	}

	public class TextEmptyToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is string)) return Visibility.Visible;
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
			if (!(value is string)) return Visibility.Collapsed;
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
				output = string.Format(CultureInfo.CurrentUICulture, format, new[] { value });
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
			if (parameter != null)
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