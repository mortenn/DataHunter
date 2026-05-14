using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DataHunter.Annotations;

namespace DataHunter.ViewModel
{
	public abstract class DataContainer : INotifyPropertyChanged
	{
		public bool AccessDenied => ScanEntry.AccessDenied;

		public event PropertyChangedEventHandler PropertyChanged;

		public abstract List<DataContainer> Path { get; }

		public abstract string FullName { get; }

		public abstract string Name { get; }

		public AppContext AppContext { get; set; }

		public bool Scanning => ScanEntry.Scanning;

		public bool Canceled => ScanEntry.Canceled;

		public bool HasActiveScan
		{
			get { return hasActiveScan; }
			set
			{
				if (hasActiveScan == value)
					return;

				hasActiveScan = value;
				OnPropertyChanged();
			}
		}

		public bool IsExpanded
		{
			get { return is_expanded; }
			set
			{
				is_expanded = value;
				OnPropertyChanged(nameof(IsExpanded));
			}
		}

		public bool IsSelected
		{
			get { return is_selected; }
			set
			{
				is_selected = value;
				OnPropertyChanged(nameof(IsSelected));
			}
		}

		public abstract bool IsVirtual { get; }

		public List<Folder> Folders
		{
			get
			{
				if (folders != null)
					return folders;

				var childFolders = Cache.GetOrLoadChildren(FullName).Select(GetOrCreateFolder).ToList();
				foreach (var folder in folderCache.Values.ToList())
				{
					if (
						!childFolders.Any(existing =>
							string.Equals(existing.FullName, folder.FullName, StringComparison.OrdinalIgnoreCase)
						)
					)
						childFolders.Add(folder);
				}
				folders = childFolders;
				return folders;
			}
		}

		public long? Bytes
		{
			get
			{
				if (ScanEntry.Canceled && !ScanEntry.Scanned)
					return null;

				if (ScanEntry.Scanned || ScanEntry.Scanning)
					return CurrentKnownTotalBytes;

				StartScan();
				return null;
			}
		}

		public long? Files
		{
			get
			{
				if (ScanEntry.Canceled && !ScanEntry.Scanned)
					return null;

				if (ScanEntry.Scanned || ScanEntry.Scanning)
					return ScanEntry.FileBytes;

				StartScan();
				return null;
			}
		}

		public long SortBytes => HasKnownSize ? CurrentKnownTotalBytes : -1;

		public long ChartBytes => ScanEntry.TotalBytes;

		public long OwnFileBytes => ScanEntry.FileBytes;

		public bool HasKnownSize =>
			ScanEntry.Scanned || ScanEntry.Scanning || ScanEntry.TotalBytes > 0 || ScanEntry.FileBytes > 0;

		public double DiskSharePercent
		{
			get
			{
				var totalSize = RootDriveTotalSize;
				if (totalSize <= 0)
					return 0;

				return 100 * (double)CurrentKnownTotalBytes / totalSize;
			}
		}

		public double ShareOfParent
		{
			get
			{
				if (!HasReliableShareOfParent)
					return 0;

				var parentBytes = Parent?.CurrentKnownChildBytes ?? 0;
				if (parentBytes <= 0)
					return 0;

				return Math.Min(1, Math.Max(0, (double)CurrentKnownTotalBytes / parentBytes));
			}
		}

		public void RefreshShareOfParent()
		{
			OnPropertyChanged(nameof(ShareOfParent));
		}

		public bool HasReliableShareOfParent =>
			Parent != null && Parent.IsScanned && Parent.LoadedChildCount <= ShareNotificationLimit;

		public void Rescan()
		{
			Cache.Reset(FullName);
			folders = null;
			StartScan();
			AppContext?.Refresh();
			OnPropertyChanged(nameof(AccessDenied));
			OnPropertyChanged(nameof(Bytes));
			OnPropertyChanged(nameof(Files));
			OnPropertyChanged(nameof(Folders));
			OnPropertyChanged(nameof(Scanning));
		}

		public void EnsureScanning()
		{
			StartScan();
		}

		public List<FolderScanEntry> LoadedChildEntries => Cache.GetLoadedChildren(FullName);

		public List<FolderScanEntry> LoadChildEntries() => Cache.GetOrLoadChildren(FullName);

		public int LoadedChildCount => Cache.GetLoadedChildren(FullName).Count;

		public bool IsScanned => ScanEntry.Scanned;

		public Folder GetOrCreateChildFolder(FolderScanEntry entry)
		{
			return GetOrCreateFolder(entry);
		}

		private void StartScan()
		{
			Cache
				.ScanAsync(FullName)
				.ContinueWith(
					task => DataHunter.App.ShowException(task.Exception.GetBaseException()),
					TaskContinuationOptions.OnlyOnFaulted
				);
		}

		protected abstract Folder CreateFolder(FolderScanEntry entry);

		public DataContainer Parent { get; }

		protected FolderScanCache Cache { get; }

		protected DataContainer(DataContainer parent, FolderScanCache cache)
		{
			Parent = parent;
			Cache = cache;
		}

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			if (handler == null)
				return;

			var args = new PropertyChangedEventArgs(propertyName);
			var dispatcher = Application.Current?.Dispatcher;
			if (dispatcher != null && !dispatcher.CheckAccess())
				dispatcher.BeginInvoke(new Action(() => handler(this, args)));
			else
				handler(this, args);
		}

		private void ScanEntryOnPropertyChanged(object sender, PropertyChangedEventArgs args)
		{
			switch (args.PropertyName)
			{
				case nameof(FolderScanEntry.AccessDenied):
					OnPropertyChanged(nameof(AccessDenied));
					break;
				case nameof(FolderScanEntry.Children):
					folders = null;
					OnPropertyChanged(nameof(Folders));
					AppContext?.Refresh();
					break;
				case nameof(FolderScanEntry.FileBytes):
					OnPropertyChanged(nameof(Files));
					OnPropertyChanged(nameof(HasKnownSize));
					break;
				case nameof(FolderScanEntry.Scanning):
					OnPropertyChanged(nameof(Scanning));
					OnPropertyChanged(nameof(HasKnownSize));
					break;
				case nameof(FolderScanEntry.Canceled):
					OnPropertyChanged(nameof(Canceled));
					OnPropertyChanged(nameof(Bytes));
					OnPropertyChanged(nameof(Files));
					OnPropertyChanged(nameof(HasKnownSize));
					OnPropertyChanged(nameof(ShareOfParent));
					break;
				case nameof(FolderScanEntry.Scanned):
					OnPropertyChanged(nameof(IsScanned));
					OnPropertyChanged(nameof(Canceled));
					OnPropertyChanged(nameof(Bytes));
					OnPropertyChanged(nameof(Files));
					OnPropertyChanged(nameof(HasKnownSize));
					OnPropertyChanged(nameof(ShareOfParent));
					NotifyChildSharesChanged();
					break;
				case nameof(FolderScanEntry.TotalBytes):
					ScheduleSizePropertiesChanged();
					break;
			}
		}

		private void FireSizePropertiesChanged()
		{
			Interlocked.Exchange(ref sizeNotificationScheduled, 0);
			OnPropertyChanged(nameof(Bytes));
			OnPropertyChanged(nameof(SortBytes));
			OnPropertyChanged(nameof(ChartBytes));
			OnPropertyChanged(nameof(OwnFileBytes));
			OnPropertyChanged(nameof(HasKnownSize));
			OnPropertyChanged(nameof(DiskSharePercent));
			OnPropertyChanged(nameof(ShareOfParent));
			OnPropertyChanged(nameof(HasReliableShareOfParent));
			NotifyChildSharesChanged();
			AppContext?.Refresh();
		}

		private void ChildFolderOnPropertyChanged(object sender, PropertyChangedEventArgs args)
		{
			if (args.PropertyName == nameof(Bytes) || args.PropertyName == nameof(SortBytes))
				ScheduleSizePropertiesChanged();
		}

		private void NotifyChildSharesChanged()
		{
			if (folders == null)
				return;

			if (folders.Count > ShareNotificationLimit)
				return;

			foreach (var folder in folders)
			{
				folder.OnPropertyChanged(nameof(HasReliableShareOfParent));
				folder.OnPropertyChanged(nameof(ShareOfParent));
			}
		}

		private void ScheduleSizePropertiesChanged()
		{
			if (Interlocked.Exchange(ref sizeNotificationScheduled, 1) == 1)
				return;

			var dispatcher = Application.Current?.Dispatcher;
			if (dispatcher == null)
			{
				FireSizePropertiesChanged();
				return;
			}

			dispatcher.BeginInvoke(
				new Action(() =>
				{
					if (sizeNotificationTimer == null)
					{
						sizeNotificationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
						sizeNotificationTimer.Tick += (sender, args) =>
						{
							sizeNotificationTimer.Stop();
							FireSizePropertiesChanged();
						};
					}

					sizeNotificationTimer.Start();
				})
			);
		}

		private long CurrentKnownChildBytes
		{
			get { return ScanEntry.Children.Where(f => !f.IsVirtual).Sum(f => f.TotalBytes); }
		}

		private long CurrentKnownTotalBytes =>
			Math.Max(ScanEntry.TotalBytes, ScanEntry.FileBytes + CurrentKnownChildBytes);

		private Folder GetOrCreateFolder(FolderScanEntry entry)
		{
			Folder folder;
			if (folderCache.TryGetValue(entry.FullName, out folder))
				return folder;

			folder = CreateFolder(entry);
			folder.PropertyChanged += ChildFolderOnPropertyChanged;
			folderCache[entry.FullName] = folder;
			return folder;
		}

		private long RootDriveTotalSize
		{
			get
			{
				var current = this;
				while (current.Parent != null)
					current = current.Parent;

				var drive = current as Drive;
				return drive?.Info.TotalSize ?? 0;
			}
		}

		private FolderScanEntry ScanEntry
		{
			get
			{
				var value = Cache.GetOrCreate(FullName);
				if (scanEntry != value)
				{
					if (scanEntry != null)
						scanEntry.PropertyChanged -= ScanEntryOnPropertyChanged;

					scanEntry = value;
					scanEntry.PropertyChanged += ScanEntryOnPropertyChanged;
				}

				return scanEntry;
			}
		}

		private List<Folder> folders;
		private readonly Dictionary<string, Folder> folderCache = new Dictionary<string, Folder>(
			StringComparer.OrdinalIgnoreCase
		);
		private FolderScanEntry scanEntry;
		private DispatcherTimer sizeNotificationTimer;
		private int sizeNotificationScheduled;
		private bool is_expanded;
		private bool is_selected;
		private bool hasActiveScan;
		private const int ShareNotificationLimit = 500;
	}
}
