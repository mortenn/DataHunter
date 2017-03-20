using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DataHunter.Annotations;
using DataHunter.Command;

namespace DataHunter.ViewModel
{
	public class AppContext : INotifyPropertyChanged
	{
		public AppContext()
		{
			NavigateCommand = new NavigateCommand(this);
			OpenCommand = new OpenCommand(this);
			RefreshCommand = new RefreshCommand(this);

			Drives = DriveInfo.GetDrives()
				.Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
				.Select(d => new Drive(d)).ToList();
		}

		public List<Drive> Drives { get; }

		public event PropertyChangedEventHandler PropertyChanged;

		public ICommand NavigateCommand { get; }

		public ICommand OpenCommand { get; }

		public ICommand RefreshCommand { get; }

		public DataContainer SelectedFolder
		{
			get
			{
				return selected_folder;
			}
			set
			{
				if(selected_folder != null && selected_folder == value)
					return;

				if(selected_folder != null)
				{
					selected_folder.AppContext = null;
					selected_folder.IsSelected = false;
					if(value != null)
					{
						var parent = value;
						while(parent != null && parent != selected_folder)
							parent = parent.Parent;
						if(parent == null && selected_folder != null)
							selected_folder.IsExpanded = false;
					}
				}
				selected_folder = value;
				if(selected_folder != null)
				{
					selected_folder.AppContext = this;
					selected_folder.IsExpanded = true;
					selected_folder.IsSelected = true;
				}
				OnPropertyChanged(nameof(SelectedFolder));
				OnPropertyChanged(nameof(Content));
			}
		}

		public List<Folder> Content
		{
			get
			{
				return SelectedFolder?.Folders.OrderByDescending(f => f.Bytes).ToList();
			}
		}

		public void Refresh()
		{
			OnPropertyChanged(nameof(Content));
		}

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private DataContainer selected_folder;
	}
}
