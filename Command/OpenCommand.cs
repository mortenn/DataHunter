using System;
using System.Diagnostics;
using System.Windows.Input;

namespace DataHunter.Command
{
	public class OpenCommand : ICommand
	{
		public OpenCommand(string path)
		{
			this.path = path;
		}

		public bool CanExecute(object parameter)
		{
			return path != null;
		}

		public void Execute(object parameter)
		{
			Process.Start(path);
		}

		public event EventHandler CanExecuteChanged;

		protected virtual void OnCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}

		private readonly string path;
	}
}
