using DataHunter.ViewModel;

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
	}
}
