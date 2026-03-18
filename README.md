# Ozmium MCP Server for S&box

Connect AI coding assistants to the S&box editor using the [Model Context Protocol](https://modelcontextprotocol.io/). While you're building your game, your AI assistant can see inside the editor in real time — querying your scene, inspecting GameObjects, reading component property values, and running console commands — without any copy-pasting.

---

## Features

- SSE-based MCP server running on `localhost:8098`
- **9 tools** for intelligent scene querying (see below)
- Disabled objects and disabled subtrees are fully visible to all query tools
- Built-in Editor panel with live server status, session count, and an activity log
- Localhost-only — nothing leaves your machine

---

## Tools

### `get_scene_summary`
Returns a high-level overview of the active scene: total/root/enabled/disabled object counts, all unique tags in use, a component-type frequency breakdown, a **prefab source breakdown** (which prefabs have how many instances), a **network mode distribution**, and a root object list. **Start here** to orient yourself before drilling into specifics.

### `find_game_objects`
Search and filter GameObjects by any combination of:
- `nameContains` — case-insensitive name substring
- `hasTag` — objects that carry a specific tag
- `hasComponent` — objects with a component whose type name contains the given string
- `pathContains` — objects whose full scene path contains the string (e.g. `"Units/"`)
- `enabledOnly` — skip disabled objects (default: false — disabled objects are included)
- `isNetworkRoot` — filter to network roots or non-roots
- `isPrefabInstance` — filter to prefab instances or non-instances
- `maxResults` — cap results (default 50, max 500)
- `sortBy` — sort by `"name"`, `"distance"` (requires `sortOriginX/Y/Z`), or `"componentCount"`

Returns a flat list with ID, scene path, tags, component types, world position, child count, isPrefabInstance, prefabSource, isNetworkRoot, and networkMode.

> **Note:** This tool now uses a manual recursive walk and correctly finds objects inside disabled parent subtrees, unlike the previous version which used `GetAllObjects()`.

### `find_game_objects_in_radius`
Find all GameObjects within a world-space radius of a point, sorted by distance. Useful for spatial questions: *"what's near the player?"*, *"which resource nodes are close to my building?"*, *"what units are within attack range?"*. Supports `hasTag`, `hasComponent`, and `enabledOnly` filters. Results include `distanceFromOrigin`.

### `get_game_object_details`
Get full details for a single GameObject by `id` (GUID, preferred) or `name`. Returns:
- World **and** local transform (position, rotation, scale)
- All components with their enabled state
- Tags, parent reference, children summary
- Network mode, prefab source, isNetworkRoot

Set `includeChildrenRecursive=true` to get the full subtree in one call — useful for inspecting a whole ship, building, or unit prefab tree.

### `get_component_properties`
Get the **runtime property values** of a specific component on a GameObject. This is the tool to use when you need to know what's *inside* a component, not just that it exists. Examples:
- *"What is the UnitHealth of this drone?"*
- *"What resource type does this ResourceNode hold?"*
- *"What are the PlayerResources values?"*
- *"What is the Ship's current state?"*

Returns all readable public properties with their current values. Requires `componentType` (case-insensitive substring match) plus either `id` or `name`.

### `get_scene_hierarchy`
Lists the scene as an indented tree. Supports:
- `rootOnly=true` — top-level only (much shorter output)
- `includeDisabled=false` — skip disabled objects
- `rootId` — walk only a specific subtree by GUID (e.g. just the Ship or just the Units container)

For large scenes, prefer `find_game_objects` or `get_scene_summary`.

### `get_prefab_instances`
Find all instances of a specific prefab, or get a full breakdown of all prefabs and their instance counts. Use this when the user asks *"how many drones do I have?"*, *"what prefabs are in the scene?"*, or *"find all instances of constructor_drone.prefab"*. `prefabPath` is matched as a case-insensitive substring. Omit it to get the full breakdown.

### `list_console_commands`
List all `[ConVar]`-attributed console variables registered in the game, with their current values, help text, flags, and declaring type. Use this **before** `run_console_command` to discover valid command names. Supports a `filter` parameter to narrow results.

### `run_console_command`
Executes a console command. Errors are returned as text rather than thrown as exceptions, so the tool always returns a result. Use `list_console_commands` first to find valid command names.

---

## Setup

1. **Install the plugin** — add it via the S&box Library Manager and let it compile.
2. **Open the MCP panel** — in the S&box editor go to **Editor → MCP → Open MCP Panel**.
3. **Start the server** — click **Start MCP Server**. The status indicator turns green.
4. **Connect your AI assistant** — add this to your MCP config (e.g. `mcp_config.json` for Claude Desktop):

```json
{
  "mcpServers": {
    "sbox": {
      "url": "http://localhost:8098/sse",
      "type": "sse"
    }
  }
}
```

5. **Done.** Your AI assistant can now call all nine tools directly.

---

## Requirements

- S&box Editor (latest)
- An MCP-compatible AI client (Claude Desktop, Cursor, etc.)

---

## Code Structure

| File | Responsibility |
|---|---|
| `SboxMcpServer.cs` | HTTP/SSE transport, JSON-RPC dispatch |
| `McpSession.cs` | Session state (SSE connection + lifecycle) |
| `ToolDefinitions.cs` | MCP tool schemas returned by `tools/list` |
| `ToolHandlers.cs` | Tool call logic (one method per tool) |
| `SceneQueryHelpers.cs` | Pure scene-data helpers (path, tags, components, object builders, `WalkAll`/`WalkSubtree`) |
| `McpServerWindow.cs` | Editor UI panel |

To add a new tool: add its schema to `ToolDefinitions.cs`, implement its handler in `ToolHandlers.cs`, and add a case to the switch in `SboxMcpServer.cs`.

### Key design notes

- **`WalkAll` / `WalkSubtree`** in `SceneQueryHelpers` replace `scene.GetAllObjects(true)` everywhere. The s&box API's `GetAllObjects` does not traverse into disabled parent subtrees; the manual walk does.
- **`get_component_properties`** uses standard .NET reflection (`GetProperties`) to read public instance properties at runtime. It handles `Vector3`, `Enum`, primitives, and strings with graceful fallback for unreadable properties.
- **`list_console_commands`** enumerates `[ConVar]`-attributed static properties across all loaded assemblies via `AppDomain.CurrentDomain.GetAssemblies()`, since `ConsoleSystem` has no enumeration API.
- **`run_console_command`** wraps `ConsoleSystem.Run` in a try/catch and returns errors as text, preventing unhandled exceptions from propagating as MCP -32603 errors.
