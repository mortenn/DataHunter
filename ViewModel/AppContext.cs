using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DataHunter.Annotations;

namespace DataHunter.ViewModel
{
	public class AppContext : INotifyPropertyChanged
	{
		public List<Drive> Drives { get; }

		public event PropertyChangedEventHandler PropertyChanged;

		public AppContext()
		{
			Drives = DriveInfo.GetDrives()
				.Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
				.Select(d => new Drive(d)).ToList();
		}

		public DataContainer SelectedFolder
		{
			get
			{
				return selected_folder;
			}
			set
			{
				if(selected_folder != null)
					selected_folder.AppContext = null;
				selected_folder = value;
				if(selected_folder != null)
				{
					selected_folder.AppContext = this;
					selected_folder.IsExpanded = true;
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
