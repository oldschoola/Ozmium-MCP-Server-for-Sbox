# MCP Server Plugin Changelog

## [Unreleased]

### 2026-03-26 — Feature: Comprehensive Tool Handlers Refactoring & Expansion

**Problem:** The MCP server's tool handler classes were becoming monolithic and difficult to maintain. As new capabilities were requested for different engine subsystems, keeping them in a few centralized files restricted organization and scalability.

**Solution:** Modularized the MCP server by introducing dedicated handler classes for specific S&box engine components and subsystems. This expansion allows logical grouping of tools, easier maintenance, and paves the way for deeper integration with engine features like Physics, Audio, and Navigation.

**Files Added/Modified:**
- `Editor/AudioToolHandlers.cs` — Audio tools (create/configure sound point)
- `Editor/CameraToolHandlers.cs` — Camera tools (create/configure camera)
- `Editor/EffectToolHandlers.cs` — Effect & environment tools
- `Editor/GameToolHandlers.cs` — Game logic tools (spawn points, triggers, etc.)
- `Editor/LightingToolHandlers.cs` — Lighting tools (create/configure light, sky box)
- `Editor/MeshEditHandlers.cs` — Mesh editing tools
- `Editor/NavigationToolHandlers.cs` — Navigation tools (nav mesh agent, link, area)
- `Editor/PhysicsToolHandlers.cs` — Physics tools (add/configure collider, rigidbody)
- `Editor/RenderingToolHandlers.cs` — Rendering tools (text, line, sprite, trail, model renderer)
- `Editor/UtilityToolHandlers.cs` — Utility tools (asset dependencies, batch transform, etc.)
- `Editor/ToolDefinitions.cs` & `Editor/RpcDispatcher.cs` — Updated to register new modular schemas.

**Acknowledgments:**
- Huge thanks to **Oldschoola** for their continued contributions and input on this massive update.
### 2026-03-18 — Feature: Custom Logo Header & Colorized Logs

**Problem:** The MCP Server panel was a standard gray interface with a monolithic block of uncolored text, making it difficult to distinguish tool names from initialization events or errors. Additionally, standard audio `.wav` files were ignored by S&box's rigid asset pipeline.

**Solution:** 
- Injected the **Ozmium Logo** into the top of the GUI using Qt-native `background-image` layout rules for an aggressive, clean crop.
- Reconstructed the Activity Log into a dynamic `Layout.Column()` of distinct `Label` elements carrying customized CSS tracking:
  - **Red:** Errors
  - **Blue:** Active tool commands
  - **Green:** Boot sequences and initialization
  - **Gray:** Internal engine routing tasks
- Integrated a native Windows P/Invoke audio player (`winmm.dll`) to trigger `Startup sound.wav` universally to instantly bypass S&box's asset compilation pipeline requirements.

**Files Changed:**
- `Editor/McpServerWindow.cs` — Integrated the logo widget, restructured `_logCanvas`, mapped formatting.
- `Editor/SboxMcpServer.cs` — Integrated `PlaySystemSound` interop call.
### 2026-03-18 — Feature: Remote Component References via MCP

**Problem:** The MCP tool `set_component_property` did not support assigning `Component` or `GameObject` references. The underlying JSON value parser failed with an `Invalid cast` exception when given a generic GUID string.

**Solution:** Added explicit support in `ConvertJsonValue` to check for `targetType` assignability to `Component` and `GameObject`. If the incoming value is a GUID string or an object with an `id`/`Id` field, the MCP server now traverses the scene and resolves the reference dynamically. This allows tools to natively establish component linkages like `BoneMergeTarget` dynamically via MCP.

**Files Changed:**
- `Editor/OzmiumWriteHandlers.cs` — Enhanced `ConvertJsonValue()` to resolve references via `OzmiumSceneHelpers.WalkAll()` and updated `SchemaSetComponentProperty`.

### 2026-03-18 — Fix: Scene Resolution Priority

**Problem:** All scene query tools (`get_scene_summary`, `find_game_objects`, `get_scene_hierarchy`, etc.) returned a minimal 4-object runtime scene instead of the actual editor scene. This made all scene inspection and manipulation tools non-functional when the game wasn't playing.

**Root Cause:** `ResolveScene()` in both `OzmiumSceneHelpers.cs` and `SceneToolHandlers.cs` checked `Game.ActiveScene` first. Since `Game.ActiveScene` is always non-null (returns a minimal runtime "Scene"), the `SceneEditorSession` fallback was never reached.

**Fix:** Reordered `ResolveScene()` to prioritize `SceneEditorSession.Active` (the editor scene the user is working with) and only fall back to `Game.ActiveScene` as a last resort.

**Files Changed:**
- `Editor/OzmiumSceneHelpers.cs` — `ResolveScene()`
- `Editor/SceneToolHandlers.cs` — `ResolveScene()`

---

### 2026-03-18 — Feature: `(MCP IGNORE)` Name Marker

**Problem:** Large map objects (e.g. Aerowalk with 900+ child objects) cause MCP scene queries to time out because they walk every object in the scene.

**Solution:** Two approaches to mark objects as ignored by MCP:
1. **Name marker**: Add `(MCP IGNORE)` anywhere in the GameObject's name (e.g. "Aerowalk (MCP IGNORE)")
2. **Tag**: Add the `mcp_ignore` tag to the GameObject

Both approaches skip the object and its entire subtree during all MCP scene traversals.

Also fixed `get_editor_context` which was calling `WalkAll().Count()` to count objects per session — replaced with `scene.Children.Count` to avoid walking the entire tree.

**Files Changed:**
- `Editor/AssetToolHandlers.cs` — replaced `WalkAll().Count()` with `Children.Count` in `get_editor_context`
- `Editor/OzmiumSceneHelpers.cs` — `IgnoreMarker` + `IgnoreTag` constants, `WalkSubtree()` filter
- `Editor/SceneQueryHelpers.cs` — `IgnoreMarker` + `IgnoreTag` constants, `WalkSubtree()` filter
- `Editor/SceneToolHandlers.cs` — inline hierarchy walk functions
- `Editor/OzmiumReadHandlers.cs` — `Walk()` method and rootOnly loop

---

### 2026-03-18 — Feature: Auto-Skip Large Subtrees

**Problem:** Even without manual tagging, large map objects (e.g. Aerowalk with 900+ children) cause MCP queries to hang.

**Solution:** Objects with more than 25 direct children are automatically treated as "too large to walk." The parent object is still returned in results (so users know it exists with its `childCount`), but its children are not recursively walked. Users can still explicitly query a specific object by ID/name to get its full details.

**Files Changed:**
- `Editor/OzmiumSceneHelpers.cs` — `MaxAutoWalkChildren` constant + check in `WalkSubtree()`
- `Editor/SceneQueryHelpers.cs` — `MaxAutoWalkChildren` constant + check in `WalkSubtree()`

---

### 2026-03-18 — Fix: GameTask.MainThread Deadlocks

**Problem:** All MCP tools abruptly began hanging indefinitely. The MCP connection would stay open but never return a result because the `.NET` background thread pool was silently dropping the engine's main thread continuation. S&box automatically deleted duplicate/conflicting `mcpserver.dll` plugins during cleanup, exposing the underlying threading issue.

**Root Cause:** Incoming JSON-RPC requests were being dispatched via a raw `.NET` `Task.Run()`, which lacks S&box's internal task scheduling context. Calling `await GameTask.MainThread()` from this detached context caused the game engine to lose track of the continuation, permanently hanging the tool executing.

**Fix:** Replaced the raw `Task.Run()` call with S&box's native `GameTask.RunInThreadAsync()`. This ensures the engine tracks the task context, allowing safe dispatching back to the main thread for editor/scene manipulation.

**Files Changed:**
- `Editor/SboxMcpServer.cs` — Swapped `Task.Run()` -> `GameTask.RunInThreadAsync()` inside `HandleMessage()`
- `sbox_mcp.sbproj` — Erased `CsProjName` override to halt generation of duplicate `.csproj` clones.
