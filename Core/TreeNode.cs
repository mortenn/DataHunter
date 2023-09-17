using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DataHunter.Tests")]
[assembly: InternalsVisibleTo("DataHunter")]

namespace DataHunter.Data;

internal sealed class TreeNode
{
	public TreeNode? Parent { get; internal set; }
	public List<TreeNode> Children { get; internal set; } = new List<TreeNode>();
	public string Directory => System.IO.Path.GetFileName(Path);
	public required string Path { get; internal set; }
	public long Left { get; internal set; }
	public long Right { get; internal set; }
	public bool AccessDenied { get; internal set; }
	public long TotalFileSize { get; internal set; }
}