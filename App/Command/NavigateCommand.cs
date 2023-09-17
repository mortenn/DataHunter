using DataHunter.ViewModel;

namespace DataHunter.Command
{
	internal sealed class NavigateCommand : AppCommand
	{
		public NavigateCommand(AppContext context) : base(context)
		{
		}

		public override bool CanExecute(object parameter)
		{
			return Context.SelectedFolder?.Parent != null;
		}

		public override void Execute(object parameter)
		{
			Context.SelectedFolder = Context.SelectedFolder.Parent;
		}
	}
}
