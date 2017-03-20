using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DataHunter.Annotations;
using DataHunter.Command;

namespace DataHunter.ViewModel
{
	public abstract class DataContainer : INotifyPropertyChanged
	{
		public bool AccessDenied { get; protected set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public abstract List<DataContainer> Path { get; }

		public abstract string FullName { get; }

		public AppContext AppContext { get; set; }

		public bool Scanning { get; set; }

		public bool IsExpanded
		{
			get
			{
				return is_expanded;
			}
			set
			{
				is_expanded = value;
				OnPropertyChanged(nameof(IsExpanded));
			}
		}

		public bool IsSelected
		{
			get
			{
				return is_selected;
			}
			set
			{
				is_selected = value;
				OnPropertyChanged(nameof(IsSelected));
			}
		}

		public List<Folder> Folders
		{
			get
			{
				if(folders != null)
					return folders;

				try
				{
					folders = Scan();
				}
				catch(DirectoryNotFoundException)
				{
					AccessDenied = true;
					folders = new List<Folder>();
				}
				catch(UnauthorizedAccessException)
				{
					AccessDenied = true;
					folders = new List<Folder>();
				}
				catch(Exception e)
				{
					MessageBox.Show(e.Message + e.StackTrace);
					folders = new List<Folder>();
				}
				return folders;
			}
		}

		public long? Bytes
		{
			get
			{
				if(scanned || Scanning)
					return total_bytes;

				Rescan();
				return null;
			}
		}

		public long? Files
		{
			get
			{
				if(!scanned)
					bytes = FindBytes();
				return bytes;
			}
		}

		public void Rescan()
		{
			scanned = false;
			total_bytes = 0;
			bytes = 0;
			Scanning = true;
			folders = null;
			Task.Factory.StartNew(GetSize);
			AppContext?.Refresh();
			OnPropertyChanged(nameof(Bytes));
			OnPropertyChanged(nameof(Files));
			OnPropertyChanged(nameof(Folders));
		}

		protected abstract List<Folder> Scan();

		protected abstract long FindBytes();

		public DataContainer Parent { get; }

		protected DataContainer(DataContainer parent)
		{
			Parent = parent;
		}

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private async Task<long> GetSize()
		{
			OnPropertyChanged(nameof(Scanning));
			if(!AccessDenied)
			{
				if(bytes == 0)
				{
					bytes = FindBytes();
					SizeChanged();
				}
				var sub = Folders.Select(f => f.GetSize());
				total_bytes = bytes;
				foreach(var child in sub)
				{
					total_bytes += await child;
				}
			}
			scanned = true;
			Scanning = false;
			SizeChanged();
			OnPropertyChanged(nameof(Scanning));
			return total_bytes;
		}

		private void SizeChanged()
		{
			total_bytes = Folders.Sum(f => f.Bytes ?? 0) + bytes;
			OnPropertyChanged(nameof(Bytes));
			Parent?.SizeChanged();
			AppContext?.Refresh();
		}

		private List<Folder> folders;
		private long bytes;
		private long total_bytes;
		private bool scanned;
		private bool is_expanded;
		private bool is_selected;
	}
}