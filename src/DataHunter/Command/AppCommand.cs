using System;
using System.ComponentModel;
using System.Windows.Input;
using AppContext = DataHunter.ViewModel.AppContext;

namespace DataHunter.Command
{
	public abstract class AppCommand : ICommand
	{
		protected AppCommand(AppContext context)
		{
			Context = context;
			Context.PropertyChanged += ContextOnPropertyChanged;
			AttachSelectedFolder();
		}

		private void ContextOnPropertyChanged(object sender, PropertyChangedEventArgs args)
		{
			if (args.PropertyName == nameof(AppContext.SelectedFolder))
			{
				AttachSelectedFolder();
				OnCanExecuteChanged();
			}
		}

		private void AttachSelectedFolder()
		{
			if (selectedFolder != null)
				selectedFolder.PropertyChanged -= SelectedFolderOnPropertyChanged;

			selectedFolder = Context.SelectedFolder;
			if (selectedFolder != null)
				selectedFolder.PropertyChanged += SelectedFolderOnPropertyChanged;
		}

		private void SelectedFolderOnPropertyChanged(object sender, PropertyChangedEventArgs args)
		{
			if (AffectsCanExecute(args.PropertyName))
				OnCanExecuteChanged();
		}

		protected virtual bool AffectsCanExecute(string propertyName)
		{
			return false;
		}

		public abstract bool CanExecute(object parameter);

		public abstract void Execute(object parameter);

		public event EventHandler CanExecuteChanged;

		protected AppContext Context;

		private DataHunter.ViewModel.DataContainer selectedFolder;

		protected virtual void OnCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
