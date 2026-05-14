using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DataHunter.Diagnostics;
using DataHunter.View;

namespace DataHunter
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public ThemeManager ThemeManager => themeManager;

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			FlightRecorder.Log("Application startup");
			DispatcherUnhandledException += AppOnDispatcherUnhandledException;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
			TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
			themeManager = new ThemeManager(this);
			themeManager.Apply();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			FlightRecorder.Log($"Application exit code {e.ApplicationExitCode}");
			themeManager?.Dispose();
			base.OnExit(e);
		}

		public static void ShowException(Exception exception)
		{
			var dispatcher = Current?.Dispatcher;
			if(dispatcher == null || dispatcher.CheckAccess())
				ShowExceptionDialog(exception);
			else
				dispatcher.BeginInvoke(new Action(() => ShowExceptionDialog(exception)));
		}

		private static void AppOnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			FlightRecorder.Log("Dispatcher unhandled exception");
			FlightRecorder.Log(e.Exception);
			ShowExceptionDialog(e.Exception);
			e.Handled = true;
		}

		private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			var exception = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString());
			FlightRecorder.Log("AppDomain unhandled exception");
			FlightRecorder.Log(exception);
			ShowException(exception);
		}

		private static void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			FlightRecorder.Log("TaskScheduler unobserved exception");
			FlightRecorder.Log(e.Exception.GetBaseException());
			ShowException(e.Exception.GetBaseException());
			e.SetObserved();
		}

		private static void ShowExceptionDialog(Exception exception)
		{
			if(showingException)
				return;

			try
			{
				showingException = true;
				var dialog = new ErrorDialog(exception)
				{
					Owner = Current?.MainWindow
				};
				dialog.ShowDialog();
			}
			catch(Exception dialogException)
			{
				FlightRecorder.Log("Error dialog failed");
				FlightRecorder.Log(dialogException);
				try
				{
					MessageBox.Show(exception.ToString(), "DataHunter error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				catch
				{
				}
			}
			finally
			{
				showingException = false;
			}
		}

		private static bool showingException;
		private ThemeManager themeManager;
	}
}
