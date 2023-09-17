using System.Collections.Concurrent;

namespace DataHunter.Data;

internal sealed class DirectoryWalker
{
	private readonly DriveInfo root;
	private readonly DirectoryRepository directories;

	public DirectoryWalker(DirectoryRepository directories, DriveInfo drive)
	{
		this.directories = directories;
		root = drive;
	}

	public void Initialize()
	{
		var results = new ConcurrentBag<TreeNode>();
		Parallel.ForEach(
			root.RootDirectory.EnumerateDirectories(),
			dir => results.Add(ScanSubTree(null, 1, dir))
		);
		var rootNode = new TreeNode
		{
			Parent = null,
			Path = root.RootDirectory.FullName,
			Left = 0,
			Right = long.MaxValue,
			TotalFileSize = root.RootDirectory.EnumerateFiles().Sum(f => f.Length)
		};
		directories.SeedDirectories(new[] { Convert(rootNode, 0) });
		long offset = 1;
		foreach (var subTree in results.OrderBy(n => n.Directory))
		{
			var directoryMetadatas = Flatten(subTree, offset).ToArray();
			directories.SeedDirectories(directoryMetadatas);
			offset = directoryMetadatas.Last().Right + 1;
		}
		rootNode.Right = offset;
	}

	private IEnumerable<DirectoryMetadata> Flatten(TreeNode rootNode, long offset)
	{
		var stack = new Stack<TreeNode>();
		stack.Push(rootNode);

		while (stack.Count > 0)
		{
			var currentNode = stack.Pop();
			yield return Convert(currentNode, offset);

			// Push children on stack in reverse to preserve order
			for(int i = currentNode.Children.Count - 1; i >= 0; i--)
			{
				stack.Push(currentNode.Children[i]);
			}
		}
	}

	private DirectoryMetadata Convert(TreeNode node, long offset)
		=> new(root.Name, Path.GetDirectoryName(node.Path) ?? string.Empty, Path.GetFileName(node.Path), node.Left + offset, node.Right + offset, node.TotalFileSize);

	private TreeNode ScanSubTree(TreeNode? parent, long offset, DirectoryInfo directory)
	{
		var me = new TreeNode
		{
			Parent = parent,
			Path = directory.FullName,
			Left = offset
		};
		IEnumerable<DirectoryInfo> subDirectories;
		try
		{
			subDirectories = directory.EnumerateDirectories();
		}
		catch (UnauthorizedAccessException)
		{
			me.AccessDenied = true;
			me.Right = offset + 1;
			return me;
		}
		try
		{
			me.TotalFileSize = directory.EnumerateFiles().Sum(f => f.Length);
		}
		catch (UnauthorizedAccessException)
		{
			me.AccessDenied = true;
		}
		long childOffset = offset + 1;
		foreach (var subDirectory in subDirectories)
		{
			var newChild = ScanSubTree(me, childOffset, subDirectory);
			childOffset = newChild.Right + 1;
			me.Children.Add(newChild);
		}
		me.Right = childOffset;
		return me;
	}
}
