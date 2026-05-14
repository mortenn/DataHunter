using System;
using System.Windows;
using System.Windows.Input;
using DataHunter.Diagnostics;
using DataHunter.View;
using DataHunter.ViewModel;
using AppContext = DataHunter.ViewModel.AppContext;

namespace DataHunter
{
	public partial class MainWindow
	{
		public MainWindow()
		{
			InitializeComponent();
			Loaded += (sender, args) => ((App)Application.Current).ThemeManager.Apply(this);
		}

		private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			Select(e.NewValue);
		}

		private void NavigateAction(object sender, RoutedEventArgs e)
		{
			Select(((FrameworkElement)sender).DataContext);
		}

		private void LandingShortcut_OnClick(object sender, RoutedEventArgs e)
		{
			try
			{
				var shortcut = ((FrameworkElement)sender).DataContext as AppContext.LandingShortcut;
				if (shortcut == null)
					return;

				FlightRecorder.Log($"Shortcut click: {shortcut.Title} -> {shortcut.FullName}");
				((AppContext)DataContext).SelectPath(shortcut.FullName);
			}
			catch (Exception exception)
			{
				FlightRecorder.Log("Shortcut click exception");
				FlightRecorder.Log(exception);
				App.ShowException(exception);
			}
		}

		private void ThemeToggle_OnClick(object sender, RoutedEventArgs e)
		{
			var context = (AppContext)DataContext;
			context.ToggleThemeMode();
		}

		private void CancelScan_OnClick(object sender, RoutedEventArgs e)
		{
			var context = (AppContext)DataContext;
			context.CancelScanning();
		}

		private void LoadLargeFolderTable_OnClick(object sender, RoutedEventArgs e)
		{
			var context = (AppContext)DataContext;
			context.LoadLargeFolderTable();
		}

		private void PieChart_OnSliceClicked(object sender, PieSliceClickedEventArgs e)
		{
			Select(e.Slice.Source);
		}

		private void Window_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			var context = (AppContext)DataContext;
			switch (e.ChangedButton)
			{
				case MouseButton.XButton1:
					context.NavigateBack();
					e.Handled = true;
					break;
				case MouseButton.XButton2:
					context.NavigateForward();
					e.Handled = true;
					break;
			}
		}

		private void Select(object sender)
		{
			if (sender is DataContainer folder)
				((AppContext)DataContext).SelectedFolder = folder;
			else if (sender is MyPc)
				((AppContext)DataContext).ShowStartPage();
		}
	}
}
