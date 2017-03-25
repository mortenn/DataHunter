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
			Select(e.NewValue as DataContainer);
		}

		private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
		{
			Select(((DataGridRow)sender).DataContext);
		}

		private void DataGrid_OnDoubleClick(object sender, MouseButtonEventArgs e)
		{
			Select(((DataGridRow)sender).DataContext);
		}

		private void Select(object sender)
		{
			if(sender is DataContainer)
				((AppContext)DataContext).SelectedFolder = (DataContainer)sender;
		}
	}
}
