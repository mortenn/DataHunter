using System.Diagnostics;
using DataHunter.ViewModel;

namespace DataHunter.Command
{
	public class OpenCommand : AppCommand
	{
		public OpenCommand(AppContext context) : base(context)
		{
		}

		public override bool CanExecute(object parameter)
		{
			return Context.SelectedFolder?.FullName != null;
		}

		public override void Execute(object parameter)
		{
			Process.Start(Context.SelectedFolder.FullName);
		}
	}
}
