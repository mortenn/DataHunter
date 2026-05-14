using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using DataHunter.Annotations;
using DataHunter.Diagnostics;

namespace DataHunter.ViewModel
{
	public class FolderScanEntry : INotifyPropertyChanged
	{
		internal FolderScanEntry(string fullName)
		{
			FullName = fullName;
			Name = GetName(fullName);
			IsVirtual = GetIsVirtual(fullName);
			children = new List<FolderScanEntry>();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public string FullName { get; }

		public string Name { get; }

		public bool IsVirtual { get; }

		public bool AccessDenied { get; private set; }

		public bool ChildrenLoaded { get; private set; }

		public bool Scanned { get; private set; }

		public bool Canceled { get; private set; }

		public bool Scanning { get; private set; }

		public long FileBytes { get; private set; }

		public long TotalBytes { get; private set; }

		public List<FolderScanEntry> Children
		{
			get
			{
				lock (sync)
					return children.ToList();
			}
		}

		internal void Reset()
		{
			lock (sync)
			{
				AccessDenied = false;
				ChildrenLoaded = false;
				Scanned = false;
				Canceled = false;
				Scanning = false;
				FileBytes = 0;
				TotalBytes = 0;
				CountedInScan = false;
				FilesCountedInScan = false;
				children = new List<FolderScanEntry>();
			}

			OnPropertyChanged(nameof(AccessDenied));
			OnPropertyChanged(nameof(Children));
			OnPropertyChanged(nameof(ChildrenLoaded));
			OnPropertyChanged(nameof(FileBytes));
			OnPropertyChanged(nameof(TotalBytes));
			OnPropertyChanged(nameof(Scanned));
			OnPropertyChanged(nameof(Canceled));
			OnPropertyChanged(nameof(Scanning));
		}

		internal void SetAccessDenied()
		{
			lock (sync)
				AccessDenied = true;

			OnPropertyChanged(nameof(AccessDenied));
		}

		internal void SetChildren(List<FolderScanEntry> value)
		{
			lock (sync)
			{
				children = value;
				ChildrenLoaded = true;
			}

			OnPropertyChanged(nameof(Children));
			OnPropertyChanged(nameof(ChildrenLoaded));
		}

		internal void SetFileBytes(long value)
		{
			lock (sync)
			{
				FileBytes = value;
				TotalBytes = value;
			}

			OnPropertyChanged(nameof(FileBytes));
			OnPropertyChanged(nameof(TotalBytes));
		}

		internal void SetScanning(bool value)
		{
			lock (sync)
				Scanning = value;

			OnPropertyChanged(nameof(Scanning));
		}

		internal void SetScanned(bool value)
		{
			lock (sync)
			{
				Scanned = value;
				if (value)
					Canceled = false;
			}

			OnPropertyChanged(nameof(Scanned));
			OnPropertyChanged(nameof(Canceled));
		}

		internal void SetCanceled(bool value)
		{
			lock (sync)
			{
				Canceled = value;
				if (value)
					Scanned = false;
			}

			OnPropertyChanged(nameof(Canceled));
			OnPropertyChanged(nameof(Scanned));
		}

		internal void SetTotalBytes(long value)
		{
			lock (sync)
				TotalBytes = value;

			OnPropertyChanged(nameof(TotalBytes));
		}

		internal bool TryCountInScan()
		{
			lock (sync)
			{
				if (CountedInScan)
					return false;

				CountedInScan = true;
				return true;
			}
		}

		internal bool TryCountFilesInScan()
		{
			lock (sync)
			{
				if (FilesCountedInScan)
					return false;

				FilesCountedInScan = true;
				return true;
			}
		}

		internal object ScanSync { get; } = new object();

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private static bool GetIsVirtual(string fullName)
		{
			try
			{
				return File.GetAttributes(fullName).HasFlag(FileAttributes.ReparsePoint);
			}
			catch
			{
				return false;
			}
		}

		private static string GetName(string fullName)
		{
			var name = new DirectoryInfo(fullName).Name;
			return string.IsNullOrEmpty(name) ? fullName : name;
		}

		private readonly object sync = new object();
		private List<FolderScanEntry> children;
		private bool CountedInScan;
		private bool FilesCountedInScan;
	}

	public class FolderScanCounts
	{
		public FolderScanCounts(string root, int completed, int total)
		{
			Root = root;
			Completed = completed;
			Total = total;
		}

		public string Root { get; }

		public int Completed { get; }

		public int Total { get; }
	}

	public class FolderScanStarted
	{
		public FolderScanStarted(string root, string fullName)
		{
			Root = root;
			FullName = fullName;
		}

		public string Root { get; }

		public string FullName { get; }
	}

	public class FolderScanningChanged
	{
		public FolderScanningChanged(string root, bool scanning)
		{
			Root = root;
			Scanning = scanning;
		}

		public string Root { get; }

		public bool Scanning { get; }
	}

	public class FolderScanEntryChanged
	{
		public FolderScanEntryChanged(string root, string fullName)
		{
			Root = root;
			FullName = fullName;
		}

		public string Root { get; }

		public string FullName { get; }
	}

	public class FolderScanCache
	{
		public event EventHandler<FolderScanStarted> FolderScanStarted;

		public event EventHandler<FolderScanCounts> FolderScanCountsChanged;

		public event EventHandler<FolderScanningChanged> ScanningChanged;

		public event EventHandler<FolderScanEntryChanged> EntryChanged;

		public event EventHandler<int> ActiveWorkerCountChanged;

		public FolderScanEntry GetOrCreate(string fullName)
		{
			var path = Normalize(fullName);
			return entries.GetOrAdd(path, p => new FolderScanEntry(p));
		}

		public List<FolderScanEntry> GetOrLoadChildren(string fullName)
		{
			var entry = GetOrCreate(fullName);
			EnsureChildrenLoaded(entry);
			return entry.Children;
		}

		public List<FolderScanEntry> GetLoadedChildren(string fullName)
		{
			return GetOrCreate(fullName).Children;
		}

		public void Reset(string fullName)
		{
			ResumeScanning();
			Reset(GetOrCreate(fullName));
		}

		public Task<long> ScanAsync(string fullName)
		{
			return ScanAsync(GetOrCreate(fullName));
		}

		public void CancelAll()
		{
			lock (cancellationSync)
			{
				scanningSuspended = true;
				cancellation.Cancel();
			}
		}

		public void ResumeScanning()
		{
			lock (cancellationSync)
			{
				scanningSuspended = false;
				if (cancellation.IsCancellationRequested)
				{
					cancellation.Dispose();
					cancellation = new CancellationTokenSource();
				}
			}
		}

		private void EnsureChildrenLoaded(
			FolderScanEntry entry,
			CancellationToken cancellationToken = default(CancellationToken)
		)
		{
			if (entry.ChildrenLoaded)
				return;

			if (entry.IsVirtual)
			{
				entry.SetChildren(new List<FolderScanEntry>());
				return;
			}

			try
			{
				var children = new List<FolderScanEntry>();
				foreach (var child in Directory.EnumerateDirectories(entry.FullName))
				{
					cancellationToken.ThrowIfCancellationRequested();
					children.Add(GetOrCreate(child));
				}
				entry.SetChildren(children);
			}
			catch (DirectoryNotFoundException)
			{
				entry.SetAccessDenied();
				entry.SetChildren(new List<FolderScanEntry>());
			}
			catch (UnauthorizedAccessException)
			{
				entry.SetAccessDenied();
				entry.SetChildren(new List<FolderScanEntry>());
			}
			catch (IOException)
			{
				entry.SetAccessDenied();
				entry.SetChildren(new List<FolderScanEntry>());
			}
			catch (SecurityException)
			{
				entry.SetAccessDenied();
				entry.SetChildren(new List<FolderScanEntry>());
			}
		}

		private void Reset(FolderScanEntry entry)
		{
			var children = entry.Children;
			entry.Reset();
			foreach (var child in children)
				Reset(child);
		}

		private Task<long> ScanAsync(FolderScanEntry entry)
		{
			if (entry.Scanned)
				return Task.FromResult(entry.TotalBytes);

			var cancellationToken = CurrentCancellationToken;
			if (cancellationToken.IsCancellationRequested || scanningSuspended)
				return Task.FromCanceled<long>(cancellationToken);

			Lazy<Task<long>> activeScan;
			if (scans.TryGetValue(entry.FullName, out activeScan))
				return activeScan.Value;

			var session = new ScanSession(cancellationToken);
			var scan = new Lazy<Task<long>>(
				() => Task.Run(() => ScanEntry(entry, session, 0), cancellationToken),
				LazyThreadSafetyMode.ExecutionAndPublication
			);

			if (scans.TryAdd(entry.FullName, scan))
			{
				OnActiveWorkerCountChanged();
				return scan.Value;
			}

			return scans.TryGetValue(entry.FullName, out activeScan) ? activeScan.Value : ScanAsync(entry);
		}

		private long ScanEntry(FolderScanEntry entry, ScanSession session, int depth)
		{
			var started = false;
			try
			{
				lock (entry.ScanSync)
				{
					session.CancellationToken.ThrowIfCancellationRequested();
					if (entry.Scanned)
						return entry.TotalBytes;
					if (depth > MaxScanDepth)
					{
						FlightRecorder.Log($"Maximum scan depth reached at {entry.FullName}");
						entry.SetAccessDenied();
						return entry.TotalBytes;
					}
					if (!session.TryEnter(entry.FullName))
					{
						FlightRecorder.Log($"Skipping scan cycle at {entry.FullName}");
						return entry.TotalBytes;
					}

					started = true;
					OnScanStarted(entry);
					entry.SetCanceled(false);
					entry.SetScanning(true);
					var total = FindFileBytes(entry, session.CancellationToken);
					entry.SetFileBytes(total);
					OnEntryChanged(entry);
					CountCompleted(entry);

					EnsureChildrenLoaded(entry, session.CancellationToken);
					OnEntryChanged(entry);
					if (!entry.IsVirtual && !entry.AccessDenied)
					{
						foreach (var child in entry.Children)
						{
							session.CancellationToken.ThrowIfCancellationRequested();
							if (child.IsVirtual)
								continue;

							total += ScanChild(child, session, depth + 1);
							entry.SetTotalBytes(total);
							OnEntryChanged(entry);
						}
					}

					session.CancellationToken.ThrowIfCancellationRequested();
					entry.SetTotalBytes(total);
					entry.SetScanned(true);
					OnEntryChanged(entry);
					return total;
				}
			}
			catch (OperationCanceledException)
			{
				entry.SetCanceled(true);
				OnEntryChanged(entry);
				throw;
			}
			finally
			{
				if (started)
				{
					entry.SetScanning(false);
					OnScanFinished(entry);
				}
				Lazy<Task<long>> removed;
				if (scans.TryRemove(entry.FullName, out removed))
					OnActiveWorkerCountChanged();
			}
		}

		private void OnScanFinished(FolderScanEntry entry)
		{
			var root = GetRoot(entry.FullName);
			var counts = GetCounts(root);
			if (Interlocked.Decrement(ref counts.ActiveScans) == 0)
				ScanningChanged?.Invoke(this, new FolderScanningChanged(root, false));
		}

		private void CountCompleted(FolderScanEntry entry)
		{
			if (!entry.TryCountFilesInScan())
				return;

			var root = GetRoot(entry.FullName);
			var counts = GetCounts(root);
			var completed = Interlocked.Increment(ref counts.CompletedFolders);
			FolderScanCountsChanged?.Invoke(
				this,
				new FolderScanCounts(root, completed, Volatile.Read(ref counts.TotalFolders))
			);
		}

		private void CountDiscovered(FolderScanEntry entry)
		{
			if (!entry.TryCountInScan())
				return;

			var root = GetRoot(entry.FullName);
			var counts = GetCounts(root);
			var total = Interlocked.Increment(ref counts.TotalFolders);
			FolderScanCountsChanged?.Invoke(
				this,
				new FolderScanCounts(root, Volatile.Read(ref counts.CompletedFolders), total)
			);
		}

		private void OnScanStarted(FolderScanEntry entry)
		{
			var root = GetRoot(entry.FullName);
			var counts = GetCounts(root);
			if (Interlocked.Increment(ref counts.ActiveScans) == 1)
				ScanningChanged?.Invoke(this, new FolderScanningChanged(root, true));

			CountDiscovered(entry);
			FolderScanStarted?.Invoke(this, new FolderScanStarted(root, entry.FullName));
		}

		private void OnEntryChanged(FolderScanEntry entry)
		{
			var root = GetRoot(entry.FullName);
			EntryChanged?.Invoke(this, new FolderScanEntryChanged(root, entry.FullName));
		}

		private void OnActiveWorkerCountChanged()
		{
			ActiveWorkerCountChanged?.Invoke(this, scans.Count);
		}

		private long ScanChild(FolderScanEntry child, ScanSession session, int depth)
		{
			Lazy<Task<long>> activeScan;
			if (scans.TryGetValue(child.FullName, out activeScan))
				return activeScan.Value.GetAwaiter().GetResult();

			return ScanEntry(child, session, depth);
		}

		private static long FindFileBytes(FolderScanEntry entry, CancellationToken cancellationToken)
		{
			if (entry.IsVirtual || entry.AccessDenied)
				return 0;

			try
			{
				long total = 0;
				foreach (var file in Directory.EnumerateFiles(entry.FullName))
				{
					cancellationToken.ThrowIfCancellationRequested();
					try
					{
						total += new FileInfo(file).Length;
					}
					catch (FileNotFoundException) { }
					catch (UnauthorizedAccessException) { }
					catch (IOException) { }
					catch (SecurityException) { }
				}

				return total;
			}
			catch (DirectoryNotFoundException)
			{
				entry.SetAccessDenied();
				return 0;
			}
			catch (UnauthorizedAccessException)
			{
				entry.SetAccessDenied();
				return 0;
			}
			catch (IOException)
			{
				entry.SetAccessDenied();
				return 0;
			}
			catch (SecurityException)
			{
				entry.SetAccessDenied();
				return 0;
			}
		}

		private static string Normalize(string fullName)
		{
			return Path.GetFullPath(fullName);
		}

		private DriveScanCounts GetCounts(string root)
		{
			return scanCounts.GetOrAdd(root, r => new DriveScanCounts());
		}

		private static string GetRoot(string fullName)
		{
			return Path.GetPathRoot(fullName) ?? fullName;
		}

		private CancellationToken CurrentCancellationToken
		{
			get
			{
				if (!cancellation.IsCancellationRequested)
					return cancellation.Token;

				lock (cancellationSync)
				{
					if (cancellation.IsCancellationRequested && !scanningSuspended && scans.IsEmpty)
					{
						cancellation.Dispose();
						cancellation = new CancellationTokenSource();
					}

					return cancellation.Token;
				}
			}
		}

		private class DriveScanCounts
		{
			public int ActiveScans;

			public int CompletedFolders;

			public int TotalFolders;
		}

		private class ScanSession
		{
			public ScanSession(CancellationToken cancellationToken)
			{
				CancellationToken = cancellationToken;
			}

			public CancellationToken CancellationToken { get; }

			public bool TryEnter(string fullName)
			{
				return visited.Add(Normalize(fullName));
			}

			private readonly HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}

		private readonly ConcurrentDictionary<string, FolderScanEntry> entries = new ConcurrentDictionary<
			string,
			FolderScanEntry
		>(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, Lazy<Task<long>>> scans = new ConcurrentDictionary<
			string,
			Lazy<Task<long>>
		>(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, DriveScanCounts> scanCounts = new ConcurrentDictionary<
			string,
			DriveScanCounts
		>(StringComparer.OrdinalIgnoreCase);
		private readonly object cancellationSync = new object();
		private CancellationTokenSource cancellation = new CancellationTokenSource();
		private volatile bool scanningSuspended;
		private const int MaxScanDepth = 256;
	}
}
