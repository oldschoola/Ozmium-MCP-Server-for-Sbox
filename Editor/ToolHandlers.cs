// This file is intentionally minimal. All tool logic has been split into:
//   SceneToolHandlers.cs   — scene-inspection tools
//   ConsoleToolHandlers.cs — console tools
//   ToolHandlerBase.cs     — shared utilities (TextResult, AppendHierarchyLine)
//
// RpcDispatcher.cs routes incoming tool calls to the appropriate handler.
