using System.Collections.Generic;

namespace SboxMcpServer;

/// <summary>
/// MCP tool schema definitions for console-related tools.
/// </summary>
internal static class ConsoleToolDefinitions
{
	internal static Dictionary<string, object> ListConsoleCommands => new()
	{
		["name"] = "list_console_commands",
		["description"] =
			"List all [ConVar]-attributed console variables registered in the game, with their current values, " +
			"help text, flags, and declaring type. " +
			"Use this before run_console_command to discover valid command names. " +
			"Use the filter parameter to narrow results, e.g. filter='mcp' or filter='gravity'.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["filter"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Case-insensitive substring to filter command names. Omit to list all."
				}
			}
		}
	};

	internal static Dictionary<string, object> RunConsoleCommand => new()
	{
		["name"]        = "run_console_command",
		["description"] =
			"Runs a console command in the S&box editor. " +
			"Use list_console_commands first to discover valid command names. " +
			"Errors are returned as text rather than thrown, so the tool always returns a result.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["command"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "The console command to run."
				}
			},
			["required"] = new[] { "command" }
		}
	};
}
