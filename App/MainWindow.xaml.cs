using System.Windows;
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

		private void NavigateAction(object sender, RoutedEventArgs e)
		{
			Select(((FrameworkElement)sender).DataContext);
		}

		private void Select(object sender)
		{
			if(sender is DataContainer folder)
				((AppContext)DataContext).SelectedFolder = folder;
		}
	}
}
