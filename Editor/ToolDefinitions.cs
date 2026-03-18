using System.Collections.Generic;

namespace SboxMcpServer;

/// <summary>
/// Static MCP tool schema definitions returned by tools/list.
/// Add a new entry here whenever a new tool is implemented in ToolHandlers.
/// </summary>
internal static class ToolDefinitions
{
	internal static object[] All => new object[]
	{
		GetSceneSummary,
		GetSceneHierarchy,
		FindGameObjects,
		FindGameObjectsInRadius,
		GetGameObjectDetails,
		GetComponentProperties,
		GetPrefabInstances,
		ListConsoleCommands,
		RunConsoleCommand
	};

	// ── Individual tool schemas ────────────────────────────────────────────

	private static Dictionary<string, object> GetSceneHierarchy => new()
	{
		["name"] = "get_scene_hierarchy",
		["description"] =
			"Lists GameObjects in the active scene as an indented tree. " +
			"Use rootOnly=true first to get a short top-level overview before expanding. " +
			"Use rootId to walk only a specific subtree (e.g. just the Ship or just the Units container) " +
			"instead of dumping the entire scene. " +
			"Each entry shows name, ID, enabled state, tags, and component types. " +
			"AVOID calling this on large scenes without rootOnly=true or rootId — it can return thousands of lines. " +
			"For questions like 'are there any X objects?' use find_game_objects instead. " +
			"For a quick scene overview use get_scene_summary instead.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["rootOnly"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If true, only list root-level GameObjects (no children). Default false."
				},
				["includeDisabled"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If false, exclude disabled GameObjects. Default true (include all)."
				},
				["rootId"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "GUID of a GameObject. If provided, only that object's subtree is listed. Use this to inspect a specific container (e.g. the Ship or Units) without dumping the whole scene."
				}
			}
		}
	};

	private static Dictionary<string, object> FindGameObjects => new()
	{
		["name"] = "find_game_objects",
		["description"] =
			"Search and filter GameObjects in the active scene by name, tag, component type, path, " +
			"network root status, or prefab instance status. Includes disabled objects and those inside " +
			"disabled parents (unlike the old version). " +
			"Use this whenever the user asks whether something exists in the scene, " +
			"e.g. 'are there any crystals?', 'how many NPCs?', 'find all doors', 'is there a SpawnPoint?'. " +
			"Also use this to locate an object before calling get_game_object_details. " +
			"Filters are ANDed together. Results can be sorted by name, distance, or componentCount. " +
			"Returns each match's ID, scene path, tags, component types, world position, child count, " +
			"isPrefabInstance, prefabSource, isNetworkRoot, and networkMode. " +
			"Never guess whether an object exists — always call this tool to check.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["nameContains"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Case-insensitive substring to match against GameObject names."
				},
				["hasTag"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Only return objects that have this tag."
				},
				["hasComponent"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Only return objects that have a component whose type name contains this string (case-insensitive). E.g. 'Rigidbody', 'ResourceNode', 'ModelRenderer'."
				},
				["pathContains"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Only return objects whose full scene path contains this string. E.g. 'Units/' to find everything under the Units container."
				},
				["enabledOnly"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If true, only return enabled GameObjects. Default false (return all, including disabled)."
				},
				["isNetworkRoot"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If set, filter to objects that are (true) or are not (false) a network root."
				},
				["isPrefabInstance"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If set, filter to objects that are (true) or are not (false) prefab instances."
				},
				["maxResults"] = new Dictionary<string, object>
				{
					["type"]        = "integer",
					["description"] = "Maximum number of results to return. Default 50, max 500."
				},
				["sortBy"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Sort results by: 'name', 'distance' (requires sortOriginX/Y/Z), or 'componentCount'."
				},
				["sortOriginX"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "X coordinate of the origin for distance sorting."
				},
				["sortOriginY"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "Y coordinate of the origin for distance sorting."
				},
				["sortOriginZ"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "Z coordinate of the origin for distance sorting."
				}
			}
		}
	};

	private static Dictionary<string, object> FindGameObjectsInRadius => new()
	{
		["name"] = "find_game_objects_in_radius",
		["description"] =
			"Find all GameObjects within a given world-space radius of a point, sorted by distance. " +
			"Use this for spatial questions: 'what's near the player?', 'which resource nodes are close to my building?', " +
			"'what units are within attack range?'. " +
			"Optionally filter by tag or component type. Results include distanceFromOrigin. " +
			"Get the origin coordinates from a prior get_game_object_details call.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["x"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "World X coordinate of the search origin."
				},
				["y"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "World Y coordinate of the search origin."
				},
				["z"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "World Z coordinate of the search origin."
				},
				["radius"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "Search radius in world units. Default 1000."
				},
				["hasTag"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Only return objects with this tag."
				},
				["hasComponent"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Only return objects with a component whose type name contains this string."
				},
				["enabledOnly"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If true, only return enabled GameObjects. Default false."
				},
				["maxResults"] = new Dictionary<string, object>
				{
					["type"]        = "integer",
					["description"] = "Maximum number of results. Default 50, max 500."
				}
			},
			["required"] = new[] { "x", "y", "z" }
		}
	};

	private static Dictionary<string, object> GetGameObjectDetails => new()
	{
		["name"] = "get_game_object_details",
		["description"] =
			"Get full details for one specific GameObject by its GUID or exact name. " +
			"Use this when the user asks about a specific object's position, rotation, scale, " +
			"what components it has, whether it is enabled, who its parent is, or what its children are. " +
			"Prefer using the 'id' (GUID) from a prior find_game_objects call rather than guessing by name. " +
			"Set includeChildrenRecursive=true to get the full subtree in one call (useful for ships, buildings, etc.). " +
			"Returns world and local transform, all components with enabled state, tags, parent ref, children list, " +
			"network mode, prefab source, and isNetworkRoot.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["id"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "The GUID of the GameObject (preferred)."
				},
				["name"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Exact name of the GameObject. If multiple objects share the name, the first match is returned."
				},
				["includeChildrenRecursive"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If true, children are returned as full detail objects recursively instead of a shallow summary. Useful for inspecting a whole prefab tree. Default false."
				}
			}
		}
	};

	private static Dictionary<string, object> GetComponentProperties => new()
	{
		["name"] = "get_component_properties",
		["description"] =
			"Get the runtime property values of a specific component on a GameObject. " +
			"Use this when you need to know the actual data inside a component — not just that it exists. " +
			"Examples: 'what is the UnitHealth of this drone?', 'what resource type does this ResourceNode hold?', " +
			"'what are the PlayerResources values?', 'what is the Ship's current state?'. " +
			"Returns all readable public properties with their current values. " +
			"Use find_game_objects or get_game_object_details first to get the object's id. " +
			"You MUST provide either 'id' or 'name' in addition to 'componentType'.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["id"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "The GUID of the GameObject (preferred). Either 'id' or 'name' must be provided."
				},
				["name"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Exact name of the GameObject. Either 'id' or 'name' must be provided."
				},
				["componentType"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Case-insensitive substring of the component type name. E.g. 'UnitHealth', 'ResourceNode', 'Ship', 'PlayerResources'."
				}
			},
			["required"] = new[] { "componentType" }
		}
	};

	private static Dictionary<string, object> GetSceneSummary => new()
	{
		["name"] = "get_scene_summary",
		["description"] =
			"Returns a high-level overview of the active scene without listing every object. " +
			"Includes: total/root/enabled/disabled object counts, all unique tags in use, " +
			"a component-type frequency table, a prefab source breakdown (which prefabs have how many instances), " +
			"a network mode distribution, and a root object list. " +
			"Call this FIRST before any other scene tool to orient yourself. " +
			"Also use this when the user asks 'what tags are in use?', 'what types of objects are in the scene?', " +
			"'which prefabs are most used?', or 'give me an overview of the scene'.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"]       = "object",
			["properties"] = new Dictionary<string, object>()
		}
	};

	private static Dictionary<string, object> GetPrefabInstances => new()
	{
		["name"] = "get_prefab_instances",
		["description"] =
			"Find all instances of a specific prefab in the scene, or get a breakdown of all prefabs and their instance counts. " +
			"Use this when the user asks 'how many drones do I have?', 'are all my buildings using the same prefab?', " +
			"'what prefabs are in the scene?', or 'find all instances of constructor_drone.prefab'. " +
			"If prefabPath is omitted, returns a full breakdown of all prefab sources and counts. " +
			"prefabPath is matched as a case-insensitive substring of the full prefab path.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["prefabPath"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Substring of the prefab path to match. E.g. 'constructor_drone', 'astro_bus', 'starter_building'. Omit to get a full breakdown of all prefabs."
				},
				["enabledOnly"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If true, only count/return enabled instances. Default false."
				},
				["maxResults"] = new Dictionary<string, object>
				{
					["type"]        = "integer",
					["description"] = "Maximum number of instance results to return. Default 100, max 500."
				}
			}
		}
	};

	private static Dictionary<string, object> ListConsoleCommands => new()
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

	private static Dictionary<string, object> RunConsoleCommand => new()
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
