using System.Text;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Shared utilities used by all tool handler classes.
/// </summary>
internal static class ToolHandlerBase
{
	/// <summary>Wraps a plain text string in the MCP content envelope.</summary>
	internal static object TextResult( string text ) => new
	{
		content = new object[] { new { type = "text", text } }
	};

	/// <summary>Appends a single indented hierarchy line to a StringBuilder.</summary>
	internal static void AppendHierarchyLine( StringBuilder sb, GameObject go, int depth, bool showChildCount )
	{
		var indent   = new string( ' ', depth * 2 );
		var comps    = SceneQueryHelpers.GetComponentNames( go );
		var tags     = SceneQueryHelpers.GetTags( go );
		var compStr  = comps.Count > 0 ? $" [{string.Join( ", ", comps )}]" : "";
		var tagStr   = tags.Count  > 0 ? $" #{string.Join( " #", tags )}" : "";
		var disStr   = go.Enabled ? "" : " (disabled)";
		var childStr = showChildCount ? $"  children:{go.Children.Count}" : "";
		sb.AppendLine( $"{indent}- {go.Name} (ID: {go.Id}){disStr}{tagStr}{compStr}{childStr}" );
	}
}
