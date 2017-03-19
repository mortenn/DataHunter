using System;
using System.Windows.Input;
using DataHunter.ViewModel;

namespace DataHunter.Command
{
	public class RefreshCommand : ICommand
	{
		public RefreshCommand(DataContainer container)
		{
			this.container = container;
		}

		public bool CanExecute(object parameter)
		{
			return container != null && !container.AccessDenied && !container.Scanning;
		}

		public void Execute(object parameter)
		{
			container.Rescan();
		}

		public event EventHandler CanExecuteChanged;

		protected virtual void OnCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}

		private readonly DataContainer container;
	}
}
