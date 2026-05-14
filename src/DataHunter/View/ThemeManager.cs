using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace DataHunter.View
{
	public enum AppThemeMode
	{
		System,
		Light,
		Dark,
	}

	public class ThemeManager : IDisposable
	{
		public ThemeManager(Application application)
		{
			this.application = application;
			SystemEvents.UserPreferenceChanged += SystemEventsOnUserPreferenceChanged;
		}

		public event EventHandler ThemeChanged;

		public void Apply()
		{
			Apply(ResolveLightTheme());
		}

		public void Apply(Window window)
		{
			ApplyTitleBar(window, !ResolveLightTheme());
		}

		public bool IsEffectiveLightTheme => ResolveLightTheme();

		public bool IsSystemLightTheme => IsLightTheme();

		public AppThemeMode Mode
		{
			get { return mode; }
			set
			{
				if (mode == value)
					return;

				mode = value;
				Apply();
			}
		}

		public void Dispose()
		{
			SystemEvents.UserPreferenceChanged -= SystemEventsOnUserPreferenceChanged;
		}

		private void Apply(bool isLightTheme)
		{
			var dictionaries = application.Resources.MergedDictionaries;
			var oldTheme = dictionaries.FirstOrDefault(IsModernThemeDictionary);
			if (oldTheme != null)
				dictionaries.Remove(oldTheme);

			dictionaries.Add(
				new ResourceDictionary
				{
					Source = new Uri(
						isLightTheme ? "Themes/ModernLight.xaml" : "Themes/ModernDark.xaml",
						UriKind.Relative
					),
				}
			);

			foreach (Window window in application.Windows)
				ApplyTitleBar(window, !isLightTheme);

			ThemeChanged?.Invoke(this, EventArgs.Empty);
		}

		private static void ApplyTitleBar(Window window, bool useDarkMode)
		{
			if (window == null)
				return;

			var handle = new WindowInteropHelper(window).Handle;
			if (handle == IntPtr.Zero)
				return;

			var value = useDarkMode ? 1 : 0;
			DwmSetWindowAttribute(handle, DwmWindowAttributeUseImmersiveDarkMode, ref value, sizeof(int));
		}

		private static bool IsLightTheme()
		{
			try
			{
				using (
					var key = Registry.CurrentUser.OpenSubKey(
						@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
					)
				)
				{
					return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) != 0;
				}
			}
			catch
			{
				return true;
			}
		}

		private static bool IsModernThemeDictionary(ResourceDictionary dictionary)
		{
			var source = dictionary.Source?.OriginalString;
			return source != null
				&& (
					source.EndsWith("ModernLight.xaml", StringComparison.OrdinalIgnoreCase)
					|| source.EndsWith("ModernDark.xaml", StringComparison.OrdinalIgnoreCase)
				);
		}

		private void SystemEventsOnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			if (mode != AppThemeMode.System)
				return;

			if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color)
				return;

			application.Dispatcher.BeginInvoke(new Action(Apply));
		}

		private bool ResolveLightTheme()
		{
			switch (mode)
			{
				case AppThemeMode.Light:
					return true;
				case AppThemeMode.Dark:
					return false;
				default:
					return IsLightTheme();
			}
		}

		private const int DwmWindowAttributeUseImmersiveDarkMode = 20;

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(
			IntPtr hwnd,
			int attribute,
			ref int attributeValue,
			int attributeSize
		);

		private readonly Application application;
		private AppThemeMode mode = AppThemeMode.System;
	}
}
