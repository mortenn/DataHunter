using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace DataHunter.View
{
	public class FormatKbSizeConverter : IValueConverter
	{
		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
		private static extern long StrFormatByteSizeW(
			long qdw,
			[MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszBuf,
			int cchBuf
		);

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return string.Empty;

			var number = System.Convert.ToInt64(value);
			if (number == 1)
				return "1 B";
			if (number < 1024)
				return $"{number}  B";

			var sb = new StringBuilder(32);
			StrFormatByteSizeW(number, sb, sb.Capacity);
			return sb.ToString().Replace("KB", "kB");
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return DependencyProperty.UnsetValue;
		}
	}
}
