using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DataHunter.Annotations;
using DataHunter.Command;
using DataHunter.Diagnostics;
using DataHunter.View;

namespace DataHunter.ViewModel
{
	public class AppContext : INotifyPropertyChanged
	{
		public AppContext()
		{
			NavigateCommand = new NavigateCommand(this);
			OpenCommand = new OpenCommand(this);
			RefreshCommand = new RefreshCommand(this);
			((App)Application.Current).ThemeManager.ThemeChanged += ThemeManagerOnThemeChanged;
			contentRefreshTimer = new DispatcherTimer { Interval = NormalRefreshInterval };
			contentRefreshTimer.Tick += ContentRefreshTimerOnTick;
			scanStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
			scanStatusTimer.Tick += ScanStatusTimerOnTick;
			scanElapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			scanElapsedTimer.Tick += ScanElapsedTimerOnTick;
			pieRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
			pieRefreshTimer.Tick += PieRefreshTimerOnTick;
			scanCache.FolderScanCountsChanged += ScanCacheOnFolderScanCountsChanged;
			scanCache.ActiveWorkerCountChanged += ScanCacheOnActiveWorkerCountChanged;
			scanCache.EntryChanged += ScanCacheOnEntryChanged;
			scanCache.FolderScanStarted += ScanCacheOnFolderScanStarted;
			scanCache.ScanningChanged += ScanCacheOnScanningChanged;

			Drives = DriveInfo
				.GetDrives()
				.Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
				.Select(d => new Drive(d, scanCache))
				.ToList();
			MyPcRoot = new MyPc(Drives);
			TreeRoots = new List<MyPc> { MyPcRoot };
			LandingShortcuts = BuildLandingShortcuts();
		}

		public List<Drive> Drives { get; }

		public MyPc MyPcRoot { get; }

		public List<MyPc> TreeRoots { get; }

		public List<LandingShortcut> LandingShortcuts { get; }

		public event PropertyChangedEventHandler PropertyChanged;

		public ICommand NavigateCommand { get; }

		public ICommand OpenCommand { get; }

		public ICommand RefreshCommand { get; }

		public bool CanNavigateBack => backHistory.Count > 0;

		public bool CanNavigateForward => forwardHistory.Count > 0;

		public string ScanStatusText =>
			selected_folder == null ? "Select a folder to scan"
			: scanCanceled ? "Scan canceled"
			: scanInProgress ? "Scanning"
			: "Scan complete";

		public bool IsScanInProgress => scanInProgress;

		public string ScanWorkerCountText => $"Workers {activeScanWorkers}";

		public string ScanElapsedText => selected_folder == null ? null : FormatElapsed(scanElapsed);

		public string ScannedFolderCountText =>
			selected_folder == null ? null : $"Scanned {completedScanFolders}/{totalScanFolders} folders";

		public string SelectedThemeMode
		{
			get { return selectedThemeMode; }
			set
			{
				if (selectedThemeMode == value || string.IsNullOrEmpty(value))
					return;

				selectedThemeMode = value;
				((App)Application.Current).ThemeManager.Mode = (AppThemeMode)
					Enum.Parse(typeof(AppThemeMode), selectedThemeMode);
				OnPropertyChanged(nameof(SelectedThemeMode));
				OnPropertyChanged(nameof(IsThemeToggleDark));
			}
		}

		public bool IsThemeToggleDark => !((App)Application.Current).ThemeManager.IsEffectiveLightTheme;

		public void ToggleThemeMode()
		{
			if (SelectedThemeMode == "System")
				SelectedThemeMode = ((App)Application.Current).ThemeManager.IsSystemLightTheme ? "Dark" : "Light";
			else
				SelectedThemeMode = "System";
		}

		public void CancelScanning()
		{
			scanCanceled = true;
			scanCache.CancelAll();
			OnPropertyChanged(nameof(ScanStatusText));
		}

		public void SelectPath(string path)
		{
			FlightRecorder.Log($"SelectPath start: {path}");
			var folder = CreateContainerForPath(path);
			if (folder == null)
			{
				FlightRecorder.Log($"SelectPath ignored missing path: {path}");
				return;
			}

			expandTreeAncestors = true;
			try
			{
				SelectedFolder = folder;
				FlightRecorder.Log($"SelectPath selected: {folder.FullName}");
			}
			finally
			{
				expandTreeAncestors = false;
			}
		}

		public void ShowStartPage()
		{
			MyPcRoot.IsSelected = true;
			SelectedFolder = null;
		}

		private void ThemeManagerOnThemeChanged(object sender, EventArgs e)
		{
			OnPropertyChanged(nameof(IsThemeToggleDark));
		}

		public string CurrentScanPath
		{
			get { return currentScanPath; }
		}

		public DataContainer SelectedFolder
		{
			get { return selected_folder; }
			set
			{
				if (selected_folder != null && selected_folder == value)
					return;

				if (!navigatingHistory)
				{
					if (selected_folder != null)
						backHistory.Push(selected_folder);
					forwardHistory.Clear();
				}

				if (selected_folder != null)
				{
					DetachContentHandlers();
					selected_folder.AppContext = null;
					selected_folder.IsSelected = false;
					if (value != null)
					{
						var parent = value;
						while (parent != null && parent != selected_folder)
							parent = parent.Parent;
						if (parent == null && selected_folder != null)
							selected_folder.IsExpanded = false;
					}
				}
				if (value != null)
				{
					scanCanceled = false;
					scanCache.ResumeScanning();
				}

				MyPcRoot.IsSelected = value == null;
				selected_folder = value;
				if (selected_folder != null)
				{
					MyPcRoot.IsExpanded = true;
					selected_folder.AppContext = this;
					selected_folder.IsExpanded = selected_folder.Parent == null;
					selected_folder.IsSelected = true;
					if (expandTreeAncestors)
						ExpandTreeAncestors(selected_folder);
				}
				OnPropertyChanged(nameof(SelectedFolder));
				UpdateContent();
				selectedScanRoot = GetRoot(selected_folder?.FullName);
				ApplySelectedScanStatus();
				OnPropertyChanged(nameof(Content));
				OnPropertyChanged(nameof(CanNavigateBack));
				OnPropertyChanged(nameof(CanNavigateForward));
				OnPropertyChanged(nameof(ScanStatusText));
				OnPropertyChanged(nameof(IsScanInProgress));
				OnPropertyChanged(nameof(ScanElapsedText));
				OnPropertyChanged(nameof(ScannedFolderCountText));
			}
		}

		public void NavigateBack()
		{
			if (!CanNavigateBack)
				return;

			forwardHistory.Push(selected_folder);
			NavigateHistory(backHistory.Pop());
		}

		public void NavigateForward()
		{
			if (!CanNavigateForward)
				return;

			backHistory.Push(selected_folder);
			NavigateHistory(forwardHistory.Pop());
		}

		public ICollectionView Content
		{
			get { return content; }
		}

		public bool IsContentLoadDeferred =>
			selected_folder != null
			&& deferredContentChildCount > LargeFolderTableWarningThreshold
			&& !IsContentLoadDismissedForSelectedFolder;

		public string ContentLoadWarningText =>
			deferredContentChildCount <= 0
				? null
				: $"This folder contains {deferredContentChildCount:N0} child folders. Loading the table can be slow.";

		public string DetailTitle => PieSource == null ? "Details" : PieSource.Name;

		public string DetailPath => PieSource?.FullName;

		public List<PieSlice> PieSlices => pieSlices;

		public bool IsDetailPaneExpanded
		{
			get { return isDetailPaneExpanded; }
			set
			{
				if (isDetailPaneExpanded == value)
					return;

				isDetailPaneExpanded = value;
				OnPropertyChanged(nameof(IsDetailPaneExpanded));
			}
		}

		public PieSlice HighlightedPieSlice
		{
			get { return highlightedPieSlice; }
			set
			{
				if (highlightedPieSlice == value)
					return;

				highlightedPieSlice = value;
				OnPropertyChanged(nameof(HighlightedPieSlice));
			}
		}

		public Folder SelectedContentFolder
		{
			get { return selectedContentFolder; }
			set
			{
				if (suppressContentSelection && value != null)
					return;

				if (selectedContentFolder == value)
					return;

				selectedContentFolder = value;
				highlightedPieSlice = null;
				OnPropertyChanged(nameof(SelectedContentFolder));
				OnPropertyChanged(nameof(HighlightedPieSlice));
				OnPropertyChanged(nameof(DetailTitle));
				OnPropertyChanged(nameof(DetailPath));
				RefreshPieSlices();
			}
		}

		public void Refresh()
		{
			var dispatcher = Application.Current?.Dispatcher;
			if (dispatcher != null && !dispatcher.CheckAccess())
			{
				dispatcher.BeginInvoke(new Action(Refresh));
				return;
			}

			if (selected_folder != null && !ReferenceEquals(selected_folder.Folders, contentFolders))
			{
				UpdateContent();
				OnPropertyChanged(nameof(Content));
				RefreshPieSlices();
				return;
			}

			ScheduleContentRefresh();
		}

		public void LoadLargeFolderTable()
		{
			if (selected_folder == null)
				return;

			contentLoadDismissedPath = selected_folder.FullName;
			RebuildContentView();
			OnPropertyChanged(nameof(IsContentLoadDeferred));
			OnPropertyChanged(nameof(ContentLoadWarningText));
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

		private void NavigateHistory(DataContainer folder)
		{
			navigatingHistory = true;
			try
			{
				SelectedFolder = folder;
			}
			finally
			{
				navigatingHistory = false;
			}
		}

		private List<LandingShortcut> BuildLandingShortcuts()
		{
			var shortcuts = new[]
			{
				new LandingShortcut(
					"My profile",
					Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
					"Your user profile and app data"
				),
				new LandingShortcut("Windows temp folder", Path.GetTempPath(), "Temporary files for the current user"),
				new LandingShortcut(
					"Program Files",
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
					"64-bit installed applications"
				),
				new LandingShortcut(
					"Program Files (x86)",
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
					"32-bit installed applications"
				),
				new LandingShortcut(
					"Windows",
					Environment.GetFolderPath(Environment.SpecialFolder.Windows),
					"Windows system files"
				),
				new LandingShortcut(
					"Downloads",
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
					"Downloaded files"
				),
			};

			return shortcuts
				.Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.FullName) && Directory.Exists(shortcut.FullName))
				.GroupBy(shortcut => Path.GetFullPath(shortcut.FullName), StringComparer.OrdinalIgnoreCase)
				.Select(group => group.First())
				.ToList();
		}

		private DataContainer CreateContainerForPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return null;

			var fullName = Path.GetFullPath(path);
			FlightRecorder.Log($"CreateContainerForPath fullName: {fullName}");
			var root = Path.GetPathRoot(fullName);
			var drive = Drives.FirstOrDefault(d => string.Equals(d.FullName, root, StringComparison.OrdinalIgnoreCase));
			if (drive == null)
				return null;

			if (string.Equals(Path.GetFullPath(drive.FullName), fullName, StringComparison.OrdinalIgnoreCase))
				return drive;

			var stack = new Stack<string>();
			var current = new DirectoryInfo(fullName);
			while (
				current != null
				&& !string.Equals(
					Path.GetFullPath(current.FullName),
					Path.GetFullPath(drive.FullName),
					StringComparison.OrdinalIgnoreCase
				)
			)
			{
				stack.Push(current.FullName);
				current = current.Parent;
			}

			DataContainer parent = drive;
			while (stack.Count > 0)
			{
				var entry = scanCache.GetOrCreate(stack.Pop());
				parent = parent.GetOrCreateChildFolder(entry);
			}

			return parent;
		}

		private static void ExpandTreeAncestors(DataContainer folder)
		{
			var current = folder.Parent;
			while (current != null)
			{
				current.IsExpanded = true;
				current = current.Parent;
			}
		}

		private void AttachContentHandlers()
		{
			if (contentFolders == null)
				return;

			if (contentFolders.Count > LargeFolderEventHandlerLimit)
				return;

			foreach (var folder in contentFolders)
				folder.PropertyChanged += ContentFolderOnPropertyChanged;
		}

		private void ContentFolderOnPropertyChanged(object sender, PropertyChangedEventArgs args)
		{
			if (
				args.PropertyName == nameof(DataContainer.Bytes)
				|| args.PropertyName == nameof(DataContainer.SortBytes)
			)
			{
				RefreshContentShares();
				ScheduleContentRefresh();
				SchedulePieRefresh();
			}
			else if (args.PropertyName == nameof(DataContainer.ShareOfParent))
				ScheduleContentRefresh();
		}

		private void RefreshContentShares()
		{
			if (contentFolders == null)
				return;

			if (contentFolders.Count > ShareRefreshLimit)
				return;

			foreach (var folder in contentFolders)
				folder.RefreshShareOfParent();
		}

		private void ContentRefreshTimerOnTick(object sender, EventArgs e)
		{
			contentRefreshTimer.Stop();
			RebuildContentView();
		}

		private void PieRefreshTimerOnTick(object sender, EventArgs e)
		{
			pieRefreshTimer.Stop();
			RefreshPieSlices();
		}

		private void DetachContentHandlers()
		{
			if (contentFolders == null)
				return;

			if (contentFolders.Count > LargeFolderEventHandlerLimit)
				return;

			foreach (var folder in contentFolders)
				folder.PropertyChanged -= ContentFolderOnPropertyChanged;
		}

		private void ScheduleContentRefresh()
		{
			if (content == null)
				return;

			if (!contentRefreshTimer.IsEnabled)
				contentRefreshTimer.Start();
		}

		private void SchedulePieRefresh()
		{
			if (!pieRefreshTimer.IsEnabled)
				pieRefreshTimer.Start();
		}

		private void ScanCacheOnFolderScanStarted(object sender, FolderScanStarted scan)
		{
			RunOnUiThread(() =>
			{
				if (!IsSelectedScanRoot(scan.Root) || !IsInSelectedFolder(scan.FullName))
					return;

				var status = GetScanStatus(scan.Root);
				status.PendingPath = scan.FullName;
				if (!scanStatusTimer.IsEnabled)
					scanStatusTimer.Start();
			});
		}

		private void ScanCacheOnFolderScanCountsChanged(object sender, FolderScanCounts counts)
		{
			RunOnUiThread(() =>
			{
				var status = GetScanStatus(counts.Root);
				status.PendingCompleted = counts.Completed;
				status.PendingTotal = counts.Total;
				status.Completed = counts.Completed;
				status.Total = counts.Total;
				if (IsSelectedScanRoot(counts.Root) && !scanStatusTimer.IsEnabled)
					scanStatusTimer.Start();
			});
		}

		private void ScanCacheOnActiveWorkerCountChanged(object sender, int count)
		{
			RunOnUiThread(() =>
			{
				activeScanWorkers = count;
				OnPropertyChanged(nameof(ScanWorkerCountText));
			});
		}

		private void ScanCacheOnEntryChanged(object sender, FolderScanEntryChanged change)
		{
			if (Interlocked.Exchange(ref entryChangeRefreshScheduled, 1) == 1)
				return;

			var dispatcher = Application.Current?.Dispatcher;
			if (dispatcher != null)
				dispatcher.BeginInvoke(new Action(ProcessPendingEntryChanges));
			else
				ProcessPendingEntryChanges();
		}

		private void ProcessPendingEntryChanges()
		{
			Interlocked.Exchange(ref entryChangeRefreshScheduled, 0);
			if (selected_folder == null)
				return;

			ScheduleContentRefresh();
			SchedulePieRefresh();
		}

		private void ScanCacheOnScanningChanged(object sender, FolderScanningChanged scan)
		{
			RunOnUiThread(() =>
			{
				var status = GetScanStatus(scan.Root);
				status.InProgress = scan.Scanning;
				UpdateRootScanActivity(scan.Root, scan.Scanning);
				if (status.InProgress)
				{
					status.Stopwatch.Restart();
					status.Elapsed = TimeSpan.Zero;
				}
				else
				{
					status.Stopwatch.Stop();
					status.Elapsed = status.Stopwatch.Elapsed;
					status.CurrentPath = null;
					status.PendingPath = null;
				}

				if (!IsSelectedScanRoot(scan.Root))
					return;

				ApplySelectedScanStatus();
				if (status.InProgress)
					scanElapsedTimer.Start();
				else
					scanElapsedTimer.Stop();

				if (!status.InProgress)
				{
					currentScanPath = status.CurrentPath;
					OnPropertyChanged(nameof(CurrentScanPath));
					OnPropertyChanged(nameof(ScannedFolderCountText));
				}
			});
		}

		private void ScanStatusTimerOnTick(object sender, EventArgs e)
		{
			scanStatusTimer.Stop();
			var status = GetScanStatus(selectedScanRoot);
			status.Completed = status.PendingCompleted;
			status.Total = status.PendingTotal;
			if (status.PendingPath != null)
				status.CurrentPath = status.PendingPath;
			status.PendingPath = null;
			completedScanFolders = status.Completed;
			totalScanFolders = status.Total;
			currentScanPath = status.CurrentPath;
			OnPropertyChanged(nameof(ScannedFolderCountText));
			OnPropertyChanged(nameof(CurrentScanPath));

			if (!scanInProgress)
				OnPropertyChanged(nameof(ScanStatusText));
		}

		private void ScanElapsedTimerOnTick(object sender, EventArgs e)
		{
			var status = GetScanStatus(selectedScanRoot);
			if (status.InProgress)
				status.Elapsed = status.Stopwatch.Elapsed;
			scanElapsed = status.Elapsed;
			OnPropertyChanged(nameof(ScanElapsedText));
		}

		private void UpdateContent()
		{
			suppressContentSelection = true;
			DetachContentHandlers();
			selectedContentFolder = null;
			highlightedPieSlice = null;
			OnPropertyChanged(nameof(SelectedContentFolder));
			OnPropertyChanged(nameof(HighlightedPieSlice));
			if (selected_folder == null)
			{
				content = null;
				contentFolders = null;
				displayContentFolders = null;
				deferredContentChildCount = 0;
				OnPropertyChanged(nameof(IsContentLoadDeferred));
				OnPropertyChanged(nameof(ContentLoadWarningText));
				OnPropertyChanged(nameof(DetailTitle));
				OnPropertyChanged(nameof(DetailPath));
				RefreshPieSlices();
				Application.Current.Dispatcher.BeginInvoke(new Action(() => suppressContentSelection = false));
				return;
			}

			var childEntries = selected_folder.LoadChildEntries();
			if (childEntries.Count > LargeFolderTableWarningThreshold && !IsContentLoadDismissedForSelectedFolder)
			{
				content = null;
				contentFolders = null;
				displayContentFolders = null;
				OnPropertyChanged(nameof(Content));
				deferredContentChildCount = childEntries.Count;
			}
			else
			{
				deferredContentChildCount = 0;
				contentFolders = selected_folder.Folders;
				contentRefreshTimer.Interval =
					contentFolders.Count > LargeFolderRefreshThreshold
						? LargeFolderRefreshInterval
						: NormalRefreshInterval;
				AttachContentHandlers();
				RebuildContentView();
			}
			OnPropertyChanged(nameof(IsContentLoadDeferred));
			OnPropertyChanged(nameof(ContentLoadWarningText));
			OnPropertyChanged(nameof(DetailTitle));
			OnPropertyChanged(nameof(DetailPath));
			RefreshPieSlices();
			Application.Current.Dispatcher.BeginInvoke(new Action(() => suppressContentSelection = false));
		}

		private void RebuildContentView()
		{
			if (contentFolders == null)
			{
				content = null;
				OnPropertyChanged(nameof(Content));
				return;
			}

			displayContentFolders = contentFolders.OrderBy(folder => folder, FolderSizeComparer.Instance).ToList();
			content = CollectionViewSource.GetDefaultView(displayContentFolders);
			OnPropertyChanged(nameof(Content));
		}

		private bool IsContentLoadDismissedForSelectedFolder
		{
			get
			{
				return selected_folder != null
					&& string.Equals(
						contentLoadDismissedPath,
						selected_folder.FullName,
						StringComparison.OrdinalIgnoreCase
					);
			}
		}

		private static void RunOnUiThread(Action action)
		{
			var dispatcher = Application.Current?.Dispatcher;
			if (dispatcher != null && !dispatcher.CheckAccess())
				dispatcher.BeginInvoke(action);
			else
				action();
		}

		private void ApplySelectedScanStatus()
		{
			var status = GetScanStatus(selectedScanRoot);
			scanInProgress = status.InProgress;
			if (status.InProgress)
				scanCanceled = false;
			scanElapsed = status.InProgress ? status.Stopwatch.Elapsed : status.Elapsed;
			completedScanFolders = status.Completed;
			totalScanFolders = status.Total;
			currentScanPath = status.PendingPath ?? status.CurrentPath;

			if (scanInProgress)
				scanElapsedTimer.Start();
			else
				scanElapsedTimer.Stop();

			OnPropertyChanged(nameof(ScanStatusText));
			OnPropertyChanged(nameof(IsScanInProgress));
			OnPropertyChanged(nameof(ScanElapsedText));
			OnPropertyChanged(nameof(ScannedFolderCountText));
			OnPropertyChanged(nameof(CurrentScanPath));
		}

		private DriveScanStatus GetScanStatus(string root)
		{
			root = root ?? string.Empty;
			DriveScanStatus status;
			if (!scanStatuses.TryGetValue(root, out status))
			{
				status = new DriveScanStatus();
				scanStatuses[root] = status;
			}

			return status;
		}

		private static string GetRoot(string fullName)
		{
			return fullName == null ? string.Empty : Path.GetPathRoot(fullName) ?? fullName;
		}

		private bool IsSelectedScanRoot(string root)
		{
			return string.Equals(
				root ?? string.Empty,
				selectedScanRoot ?? string.Empty,
				StringComparison.OrdinalIgnoreCase
			);
		}

		private void UpdateRootScanActivity(string root, bool active)
		{
			var drive = Drives.FirstOrDefault(d => string.Equals(d.FullName, root, StringComparison.OrdinalIgnoreCase));
			if (drive != null)
				drive.HasActiveScan = active;
		}

		private bool IsInSelectedFolder(string fullName)
		{
			var selectedFullName = selected_folder?.FullName;
			if (string.IsNullOrEmpty(selectedFullName) || string.IsNullOrEmpty(fullName))
				return false;

			selectedFullName =
				Path.GetFullPath(selectedFullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				+ Path.DirectorySeparatorChar;
			fullName =
				Path.GetFullPath(fullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				+ Path.DirectorySeparatorChar;
			return fullName.StartsWith(selectedFullName, StringComparison.OrdinalIgnoreCase);
		}

		private List<PieSlice> BuildPieSlices(DataContainer source)
		{
			if (source == null)
				return new List<PieSlice>();

			source.EnsureScanning();
			var candidates = new List<PieSliceCandidate>();
			if (source.OwnFileBytes > 0)
				candidates.Add(new PieSliceCandidate("Files", source.OwnFileBytes, null));

			foreach (var entry in source.LoadedChildEntries.Where(f => !f.IsVirtual))
			{
				if (entry.TotalBytes <= 0)
					continue;

				candidates.Add(new PieSliceCandidate(entry.Name, entry.TotalBytes, entry));
			}

			var ordered = candidates.OrderByDescending(slice => slice.Bytes).ThenBy(slice => slice.Name).ToList();
			var visible = ordered.Take(MaxPieSlices).ToList();
			var otherBytes = ordered.Skip(MaxPieSlices).Sum(slice => slice.Bytes);
			var slices = new List<PieSlice>();
			for (var i = 0; i < visible.Count; i++)
			{
				var slice = visible[i];
				slices.Add(
					new PieSlice(
						slice.Name,
						slice.Bytes,
						GetPieBrush(i, Math.Max(visible.Count, 1)),
						slice.Source == null ? null : source.GetOrCreateChildFolder(slice.Source)
					)
				);
			}

			if (otherBytes > 0)
				slices.Add(new PieSlice("Other", otherBytes, otherPieBrush));

			return slices;
		}

		private void RefreshPieSlices()
		{
			var previousHighlight = highlightedPieSlice;
			pieSlices = BuildPieSlices(PieSource);
			var nextHighlight = FindMatchingSlice(pieSlices, previousHighlight);
			var highlightChanged = !ReferenceEquals(highlightedPieSlice, nextHighlight);
			if (!ReferenceEquals(highlightedPieSlice, nextHighlight))
				highlightedPieSlice = nextHighlight;

			if (highlightChanged)
				OnPropertyChanged(nameof(HighlightedPieSlice));
			OnPropertyChanged(nameof(PieSlices));
		}

		private static PieSlice FindMatchingSlice(IEnumerable<PieSlice> slices, PieSlice previousHighlight)
		{
			if (previousHighlight == null)
				return null;

			return slices.FirstOrDefault(slice => IsSameSlice(slice, previousHighlight));
		}

		private static bool IsSameSlice(PieSlice left, PieSlice right)
		{
			if (left == null || right == null)
				return false;

			if (left.Source != null || right.Source != null)
				return ReferenceEquals(left.Source, right.Source);

			return string.Equals(left.Name, right.Name, StringComparison.Ordinal);
		}

		private static Brush GetPieBrush(int index, int count)
		{
			if (count <= 1)
				return CreatePieBrush(pieStartColor);

			var amount = (double)index / (count - 1);
			var color = Color.FromRgb(
				(byte)(pieStartColor.R + (pieEndColor.R - pieStartColor.R) * amount),
				(byte)(pieStartColor.G + (pieEndColor.G - pieStartColor.G) * amount),
				(byte)(pieStartColor.B + (pieEndColor.B - pieStartColor.B) * amount)
			);
			return CreatePieBrush(color);
		}

		private static Brush CreatePieBrush(Color color)
		{
			var brush = new SolidColorBrush(color);
			if (brush.CanFreeze)
				brush.Freeze();
			return brush;
		}

		private static string FormatElapsed(TimeSpan elapsed)
		{
			if (elapsed.TotalHours >= 1)
				return elapsed.ToString(@"h\:mm\:ss");

			return elapsed.ToString(@"m\:ss");
		}

		private class FolderSizeComparer : IComparer, IComparer<Folder>
		{
			public static readonly FolderSizeComparer Instance = new FolderSizeComparer();

			public int Compare(object x, object y)
			{
				return Compare(x as Folder, y as Folder);
			}

			public int Compare(Folder left, Folder right)
			{
				if (left == null || right == null)
					return 0;

				var knownComparison = right.HasKnownSize.CompareTo(left.HasKnownSize);
				if (knownComparison != 0)
					return knownComparison;

				var sizeComparison = right.SortBytes.CompareTo(left.SortBytes);
				if (sizeComparison != 0)
					return sizeComparison;

				return StringComparer.CurrentCultureIgnoreCase.Compare(left.Info.Name, right.Info.Name);
			}
		}

		private class DriveScanStatus
		{
			public readonly Stopwatch Stopwatch = new Stopwatch();

			public bool InProgress;

			public int Completed;

			public int PendingCompleted;

			public int PendingTotal;

			public int Total;

			public string CurrentPath;

			public string PendingPath;

			public TimeSpan Elapsed;
		}

		public class LandingShortcut
		{
			public LandingShortcut(string title, string fullName, string description)
			{
				Title = title;
				FullName = fullName;
				Description = description;
			}

			public string Title { get; }

			public string FullName { get; }

			public string Description { get; }
		}

		private struct PieSliceCandidate
		{
			public PieSliceCandidate(string name, long bytes, FolderScanEntry source)
			{
				Name = name;
				Bytes = bytes;
				Source = source;
			}

			public string Name { get; }

			public long Bytes { get; }

			public FolderScanEntry Source { get; }
		}

		private DataContainer PieSource => selectedContentFolder ?? selected_folder;

		private const int MaxPieSlices = 10;
		private const int AutoExpandChildLimit = 500;
		private const int LargeFolderEventHandlerLimit = 5000;
		private const int LargeFolderRefreshThreshold = 5000;
		private const int LargeFolderTableWarningThreshold = 5000;
		private const int ShareRefreshLimit = 500;
		private static readonly TimeSpan NormalRefreshInterval = TimeSpan.FromMilliseconds(120);
		private static readonly TimeSpan LargeFolderRefreshInterval = TimeSpan.FromMilliseconds(1000);
		private static readonly Color pieStartColor = Color.FromRgb(0xEF, 0x6A, 0x5B);
		private static readonly Color pieEndColor = Color.FromRgb(0x72, 0xCF, 0x7E);
		private static readonly Brush otherPieBrush = CreatePieBrush(Color.FromRgb(0x8D, 0x8D, 0x8D));

		private readonly FolderScanCache scanCache = new FolderScanCache();
		private readonly Stack<DataContainer> backHistory = new Stack<DataContainer>();
		private readonly Stack<DataContainer> forwardHistory = new Stack<DataContainer>();
		private readonly Dictionary<string, DriveScanStatus> scanStatuses = new Dictionary<string, DriveScanStatus>(
			StringComparer.OrdinalIgnoreCase
		);
		private readonly DispatcherTimer contentRefreshTimer;
		private readonly DispatcherTimer pieRefreshTimer;
		private readonly DispatcherTimer scanElapsedTimer;
		private readonly DispatcherTimer scanStatusTimer;
		private ICollectionView content;
		private List<Folder> contentFolders;
		private List<Folder> displayContentFolders;
		private List<PieSlice> pieSlices = new List<PieSlice>();
		private PieSlice highlightedPieSlice;
		private Folder selectedContentFolder;
		private DataContainer selected_folder;
		private string currentScanPath;
		private string contentLoadDismissedPath;
		private string selectedScanRoot = string.Empty;
		private string selectedThemeMode = "System";
		private TimeSpan scanElapsed;
		private int completedScanFolders;
		private int activeScanWorkers;
		private int deferredContentChildCount;
		private int entryChangeRefreshScheduled;
		private int totalScanFolders;
		private bool isDetailPaneExpanded = true;
		private bool scanCanceled;
		private bool expandTreeAncestors;
		private bool suppressContentSelection;
		private bool scanInProgress;
		private bool navigatingHistory;
	}
}
