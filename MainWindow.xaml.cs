using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DataHunter.ViewModel;

namespace DataHunter
{
	public partial class MainWindow
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			((AppContext)DataContext).SelectedFolder = e.NewValue as DataContainer;
		}

		private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
		{
			((AppContext) DataContext).SelectedFolder = ((Button) sender).DataContext as DataContainer;
		}

		private void EventSetter_OnHandler(object sender, MouseButtonEventArgs e)
		{
			((AppContext) DataContext).SelectedFolder = ((DataGridRow) sender).DataContext as DataContainer;
		}
	}
}
