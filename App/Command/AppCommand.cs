using System;
using System.ComponentModel;
using System.Windows.Input;
using AppContext = DataHunter.ViewModel.AppContext;

namespace DataHunter.Command
{
	internal abstract class AppCommand : ICommand
	{
		protected AppCommand(AppContext context)
		{
			Context = context;
			Context.PropertyChanged += ContextOnPropertyChanged;
		}

		private void ContextOnPropertyChanged(object sender, PropertyChangedEventArgs args)
		{
			if(args.PropertyName == nameof(AppContext.SelectedFolder))
				OnCanExecuteChanged();
		}

		public abstract bool CanExecute(object parameter);

		public abstract void Execute(object parameter);

		public event EventHandler CanExecuteChanged;

		protected AppContext Context;

		protected virtual void OnCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
