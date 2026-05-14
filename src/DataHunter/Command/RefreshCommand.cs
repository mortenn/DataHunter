using DataHunter.ViewModel;
using AppContext = DataHunter.ViewModel.AppContext;

namespace DataHunter.Command
{
	public class RefreshCommand : AppCommand
	{
		public RefreshCommand(AppContext context) : base(context)
		{
		}

		public override bool CanExecute(object parameter)
		{
			return Context.SelectedFolder != null && !Context.SelectedFolder.AccessDenied && !Context.SelectedFolder.Scanning;
		}

		public override void Execute(object parameter)
		{
			Context.SelectedFolder.Rescan();
		}

		protected override bool AffectsCanExecute(string propertyName)
		{
			return propertyName == nameof(DataContainer.AccessDenied) || propertyName == nameof(DataContainer.Scanning);
		}
	}
}
