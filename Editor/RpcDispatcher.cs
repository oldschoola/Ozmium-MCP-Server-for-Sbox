using System;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Handles JSON-RPC method dispatch for the MCP server.
/// Translates incoming method names into tool handler calls and sends
/// the result back over the SSE connection via McpServer.SendSseEvent.
/// </summary>
internal static class RpcDispatcher
{
	/// <summary>
	/// Parses and dispatches a single JSON-RPC request, then sends the response
	/// as an SSE event on the given session.
	/// </summary>
	internal static async Task ProcessRpcRequest(
		McpSession session,
		object id,
		string method,
		string rawBody,
		JsonSerializerOptions jsonOptions,
		Action<string> logInfo,
		Action<string> logError )
	{
		object result = null;
		object error  = null;

		using var doc = JsonDocument.Parse( rawBody );
		var root      = doc.RootElement;

		try
		{
			if ( method == "initialize" )
			{
				result = new
				{
					protocolVersion = "2024-11-05",
					capabilities    = new { tools = new { listChanged = true } },
					serverInfo      = new { name = "SboxMcpServer", version = "1.2.0" }
				};
			}
			else if ( method == "tools/list" )
			{
				result = new { tools = ToolDefinitions.All };
			}
			else if ( method == "tools/call" )
			{
				var args     = root.TryGetProperty( "params", out var p ) && p.TryGetProperty( "arguments", out var a ) ? a : default;
				var toolName = root.GetProperty( "params" ).GetProperty( "name" ).GetString();

				// run_console_command is dispatched through its own method so that
				// its try/catch can intercept exceptions thrown by ConsoleSystem.Run
				// on the main thread (nested catches in async methods don't reliably
				// catch these in s&box's sandbox environment).
				if ( toolName == "run_console_command" )
				{
					result = RunConsoleCommandSafe( args );
				}
				else
				{
					// Scene API calls must run on the main thread.
					logInfo?.Invoke( $"Waiting for GameTask.MainThread() to execute tool {toolName}..." );
					await GameTask.MainThread();
					logInfo?.Invoke( $"Resumed on MainThread for tool {toolName}." );

					result = toolName switch
					{
						// ── Read tools ───────────────────────────────────────────────────────
						"get_scene_summary"           => SceneToolHandlers.GetSceneSummary( jsonOptions ),
						"get_scene_hierarchy"         => SceneToolHandlers.GetSceneHierarchy( args ),
						"find_game_objects"           => SceneToolHandlers.FindGameObjects( args, jsonOptions ),
						"find_game_objects_in_radius" => SceneToolHandlers.FindGameObjectsInRadius( args, jsonOptions ),
						"get_game_object_details"     => SceneToolHandlers.GetGameObjectDetails( args, jsonOptions ),
						"get_component_properties"    => SceneToolHandlers.GetComponentProperties( args, jsonOptions ),
						"get_prefab_instances"        => SceneToolHandlers.GetPrefabInstances( args, jsonOptions ),
						// ── Asset + console ──────────────────────────────────────────────────
						"browse_assets"               => AssetToolHandlers.BrowseAssets( args, jsonOptions ),
						"get_editor_context"          => AssetToolHandlers.GetEditorContext( jsonOptions ),
						"list_console_commands"       => ConsoleToolHandlers.ListConsoleCommands( args, jsonOptions ),
						// ── Write tools ──────────────────────────────────────────────────────
						"create_game_object"          => OzmiumWriteHandlers.CreateGameObject( args ),
						"add_component"               => OzmiumWriteHandlers.AddComponent( args ),
						"remove_component"            => OzmiumWriteHandlers.RemoveComponent( args ),
						"set_component_property"      => OzmiumWriteHandlers.SetComponentProperty( args ),
						"destroy_game_object"         => OzmiumWriteHandlers.DestroyGameObject( args ),
						"reparent_game_object"        => OzmiumWriteHandlers.ReparentGameObject( args ),
						"set_game_object_tags"        => OzmiumWriteHandlers.SetGameObjectTags( args ),
						"instantiate_prefab"          => OzmiumWriteHandlers.InstantiatePrefab( args ),
						"save_scene"                  => OzmiumWriteHandlers.SaveScene(),
						"undo"                        => OzmiumWriteHandlers.Undo(),
						"redo"                        => OzmiumWriteHandlers.Redo(),
						// ── Batch transform & object management ────────────────────────────
						"set_game_object_transform"   => OzmiumWriteHandlers.SetGameObjectTransform( args ),
						"duplicate_game_object"      => OzmiumWriteHandlers.DuplicateGameObject( args ),
						"set_game_object_enabled"    => OzmiumWriteHandlers.SetGameObjectEnabled( args ),
						"set_game_object_name"       => OzmiumWriteHandlers.SetGameObjectName( args ),
						"set_component_enabled"      => OzmiumWriteHandlers.SetComponentEnabled( args ),
						// ── Extended asset tools ─────────────────────────────────────────────
						"get_model_info"              => OzmiumAssetHandlers.GetModelInfo( args ),
						"get_material_properties"     => OzmiumAssetHandlers.GetMaterialProperties( args ),
						"get_prefab_structure"        => OzmiumAssetHandlers.GetPrefabStructure( args ),
						"reload_asset"                => OzmiumAssetHandlers.ReloadAsset( args ),
						"get_component_types"         => OzmiumAssetHandlers.GetComponentTypes( args ),
						"search_assets"               => OzmiumAssetHandlers.SearchAssets( args ),
						"get_scene_statistics"        => OzmiumAssetHandlers.GetSceneStatistics(),
						// ── Editor control ───────────────────────────────────────────────────
						"select_game_object"          => OzmiumEditorHandlers.SelectGameObject( args ),
						"open_asset"                  => OzmiumEditorHandlers.OpenAsset( args ),
						"get_play_state"              => OzmiumEditorHandlers.GetPlayState(),
						"start_play_mode"             => OzmiumEditorHandlers.StartPlayMode(),
						"stop_play_mode"              => OzmiumEditorHandlers.StopPlayMode(),
						"get_editor_log"              => OzmiumEditorHandlers.GetEditorLog( args ),
						// ── Selection tools ──────────────────────────────────────────────────
						"get_selected_objects"        => OzmiumEditorHandlers.GetSelectedObjects(),
						"set_selected_objects"        => OzmiumEditorHandlers.SetSelectedObjects( args ),
						"clear_selection"             => OzmiumEditorHandlers.ClearSelection(),
						// ── Mesh editing tools ───────────────────────────────────────────────
						"create_block"                => MeshEditHandlers.CreateBlock( args ),
						"set_face_material"           => MeshEditHandlers.SetFaceMaterial( args ),
						"set_texture_parameters"      => MeshEditHandlers.SetTextureParameters( args ),
						"set_vertex_position"        => MeshEditHandlers.SetVertexPosition( args ),
						"set_vertex_color"           => MeshEditHandlers.SetVertexColor( args ),
						"set_vertex_blend"           => MeshEditHandlers.SetVertexBlend( args ),
						"get_mesh_info"               => MeshEditHandlers.GetMeshInfo( args ),
						// ── Lighting tools ───────────────────────────────────────────────────
						"create_light"               => LightingToolHandlers.CreateLight( args ),
						"configure_light"            => LightingToolHandlers.ConfigureLight( args ),
						"create_sky_box"             => LightingToolHandlers.CreateSkyBox( args ),
						"set_sky_box"                => LightingToolHandlers.SetSkyBox( args ),
						"create_ambient_light"       => LightingToolHandlers.CreateAmbientLight( args ),
						"create_indirect_light_volume" => LightingToolHandlers.CreateIndirectLightVolume( args ),
						// ── Physics & collider tools ────────────────────────────────────────
						"add_collider"               => PhysicsToolHandlers.AddCollider( args ),
						"configure_collider"         => PhysicsToolHandlers.ConfigureCollider( args ),
						"add_rigidbody"              => PhysicsToolHandlers.AddRigidbody( args ),
						"create_character_controller" => PhysicsToolHandlers.CreateCharacterController( args ),
						"add_plane_collider"         => PhysicsToolHandlers.AddPlaneCollider( args ),
						"add_hull_collider"          => PhysicsToolHandlers.AddHullCollider( args ),
						"create_model_physics"       => PhysicsToolHandlers.CreateModelPhysics( args ),
						// ── Audio tools ─────────────────────────────────────────────────────
						"create_sound_point"         => AudioToolHandlers.CreateSoundPoint( args ),
						"configure_sound"            => AudioToolHandlers.ConfigureSound( args ),
						"create_soundscape_trigger"  => AudioToolHandlers.CreateSoundscapeTrigger( args ),
						"create_sound_box"           => AudioToolHandlers.CreateSoundBox( args ),
						"create_dsp_volume"          => AudioToolHandlers.CreateDspVolume( args ),
						"create_audio_listener"      => AudioToolHandlers.CreateAudioListener( args ),
						// ── Camera tools ────────────────────────────────────────────────────
						"create_camera"              => CameraToolHandlers.CreateCamera( args ),
						"configure_camera"           => CameraToolHandlers.ConfigureCamera( args ),
						// ── Effect & environment tools ──────────────────────────────────────
						"create_particle_effect"     => EffectToolHandlers.CreateParticleEffect( args ),
						"configure_particle_effect"  => EffectToolHandlers.ConfigureParticleEffect( args ),
						"create_fog_volume"          => EffectToolHandlers.CreateFogVolume( args ),
						"configure_post_processing"  => EffectToolHandlers.ConfigurePostProcessing( args ),
						"create_environment_light"   => EffectToolHandlers.CreateEnvironmentLight( args ),
						// ── Utility tools ───────────────────────────────────────────────────
						"get_asset_dependencies"     => UtilityToolHandlers.GetAssetDependencies( args ),
						"batch_transform"            => UtilityToolHandlers.BatchTransform( args ),
						"copy_component"             => UtilityToolHandlers.CopyComponent( args ),
						"get_object_bounds"          => UtilityToolHandlers.GetObjectBounds( args ),
						// ── Navigation tools ──────────────────────────────────────────────────
						"create_nav_mesh_agent"      => NavigationToolHandlers.CreateNavMeshAgent( args ),
						"create_nav_mesh_link"       => NavigationToolHandlers.CreateNavMeshLink( args ),
						"create_nav_mesh_area"       => NavigationToolHandlers.CreateNavMeshArea( args ),
						// ── Rendering tools ──────────────────────────────────────────────────
						"create_render_entity"       => RenderingToolHandlers.CreateRenderEntity( args ),
						// ── Game tools ──────────────────────────────────────────────────────
						"create_game_entity"         => GameToolHandlers.CreateGameEntity( args ),
						// ── Effect & physics extension tools ────────────────────────────────
						"create_beam_effect"         => EffectToolHandlers.CreateBeamEffect( args ),
						"create_verlet_rope"         => EffectToolHandlers.CreateVerletRope( args ),
						"create_joint"               => EffectToolHandlers.CreateJoint( args ),
						"create_clutter"             => EffectToolHandlers.CreateClutter( args ),
						"create_radius_damage"       => EffectToolHandlers.CreateRadiusDamage( args ),
						// ── Editor & scene extension tools ──────────────────────────────────
						"frame_selection"            => OzmiumEditorHandlers.FrameSelection( args ),
						"save_scene_as"              => OzmiumEditorHandlers.SaveSceneAs( args ),
						"get_scene_unsaved"          => OzmiumEditorHandlers.GetSceneUnsaved(),
						"break_from_prefab"          => OzmiumEditorHandlers.BreakFromPrefab( args ),
						"update_from_prefab"         => OzmiumEditorHandlers.UpdateFromPrefab( args ),
						_                             => throw new InvalidOperationException( $"Tool '{toolName}' not found" )
					};
				}

				logInfo( $"Tool: {toolName}" );
			}
			else
			{
				error = new { code = -32601, message = $"Method '{method}' not found" };
			}
		}
		catch ( ArgumentException ex )
		{
			// Invalid parameters (e.g. missing required arg)
			error = new { code = -32602, message = ex.Message };
		}
		catch ( Exception ex )
		{
			logError( $"ProcessRpcRequest catch: method={method} ex={ex.Message}" );

			// For run_console_command, convert engine exceptions into a friendly text result.
			// Parse rawBody fresh since root/doc may be in an uncertain state after the fault.
			if ( method == "tools/call" )
			{
				string toolNameCatch = null;
				string cmdStrCatch   = "?";
				try
				{
					var bodyDoc  = JsonDocument.Parse( rawBody );
					var paramsEl = bodyDoc.RootElement.GetProperty( "params" );
					toolNameCatch = paramsEl.GetProperty( "name" ).GetString();
					if ( paramsEl.TryGetProperty( "arguments", out var argsEl ) &&
						argsEl.TryGetProperty( "command", out var cmdEl ) )
						cmdStrCatch = cmdEl.GetString() ?? "?";
				}
				catch ( Exception parseEx )
				{
					logError( $"ProcessRpcRequest catch parse error: {parseEx.Message}" );
				}

				logError( $"ProcessRpcRequest catch: toolName={toolNameCatch}" );

				if ( toolNameCatch == "run_console_command" )
				{
					result = ToolHandlerBase.TextResult( $"Command failed: {cmdStrCatch}\nError: {ex.Message}" );
					error  = null;
				}
				else
				{
					error = new { code = -32603, message = $"Internal error: {ex.Message}" };
				}
			}
			else
			{
				error = new { code = -32603, message = $"Internal error: {ex.Message}" };
			}
		}

		var response = new { jsonrpc = "2.0", id, result, error };
		var json     = JsonSerializer.Serialize( response, jsonOptions );
		await McpServer.SendSseEvent( session, "message", json );
	}

	/// <summary>
	/// Runs run_console_command in a plain try/catch (no async context) so that
	/// exceptions from ConsoleSystem are catchable.
	/// </summary>
	private static object RunConsoleCommandSafe( JsonElement args )
	{
		var cmdStr = args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty( "command", out var cp )
			? cp.GetString()
			: "";
		try
		{
			return ConsoleToolHandlers.RunConsoleCommand( args );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.TextResult( $"Command failed: {cmdStr}\nError: {ex.Message}" );
		}
	}
}
