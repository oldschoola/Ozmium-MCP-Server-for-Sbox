using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Terrain creation and editing MCP tools.
/// Uses Terrain component, TerrainStorage, and CompactTerrainMaterial APIs.
/// </summary>
internal static class TerrainToolHandlers
{
	private static readonly JsonSerializerOptions _json = new()
	{
		PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	// ── create ──────────────────────────────────────────────────────────────

	private static object Create( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		int resolution = OzmiumSceneHelpers.Get( args, "resolution", 512 );
		float terrainSize = OzmiumSceneHelpers.Get( args, "terrainSize", 20000f );
		float terrainHeight = OzmiumSceneHelpers.Get( args, "terrainHeight", 10000f );
		string name = OzmiumSceneHelpers.Get( args, "name", "Terrain" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var storage = new TerrainStorage();
			storage.SetResolution( resolution );
			storage.TerrainSize = terrainSize;
			storage.TerrainHeight = terrainHeight;

			var terrain = go.Components.Create<Terrain>();
			terrain.Storage = storage;

			go.Tags.Add( "terrain" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message      = $"Created terrain '{name}' (resolution={resolution}, size={terrainSize}, height={terrainHeight}).",
				id           = go.Id.ToString(),
				name         = go.Name,
				position     = OzmiumSceneHelpers.V3( go.WorldPosition ),
				resolution,
				terrainSize,
				terrainHeight
			}, _json ) );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── get_info ────────────────────────────────────────────────────────────

	private static Terrain FindTerrain( Scene scene, JsonElement args )
	{
		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		GameObject go = null;
		if ( !string.IsNullOrEmpty( id ) && Guid.TryParse( id, out var guid ) )
			go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
		if ( go == null && !string.IsNullOrEmpty( name ) )
			go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g =>
				string.Equals( g.Name, name, StringComparison.OrdinalIgnoreCase ) );

		if ( go == null )
		{
			// Fall back to first terrain in scene
			go = OzmiumSceneHelpers.WalkAll( scene, true )
				.FirstOrDefault( g => g.Components.Get<Terrain>() != null );
		}

		return go?.Components.Get<Terrain>();
	}

	private static object GetInfo( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		var s = terrain.Storage;
		var materials = s.Materials.Select( ( m, i ) => new
		{
			index = i,
			name = m.ResourceName ?? $"Material {i}",
			surface = m.Surface?.ResourceName
		} ).ToList();

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message       = $"Terrain info for '{terrain.GameObject.Name}'.",
			id            = terrain.GameObject.Id.ToString(),
			name          = terrain.GameObject.Name,
			position      = OzmiumSceneHelpers.V3( terrain.WorldPosition ),
			resolution    = s.Resolution,
			terrainSize   = s.TerrainSize,
			terrainHeight = s.TerrainHeight,
			materialCount = s.Materials.Count,
			materials
		}, _json ) );
	}

	// ── get_height ──────────────────────────────────────────────────────────

	private static object GetHeight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		var s = terrain.Storage;
		var localPos = terrain.WorldTransform.PointToLocal( new Vector3( wx, 0, wz ) );

		// Convert local position to heightmap UV
		var uv = new Vector2( localPos.x / s.TerrainSize, localPos.y / s.TerrainSize );
		if ( uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1 )
			return OzmiumSceneHelpers.Txt( $"Position ({wx}, {wz}) is outside terrain bounds." );

		var hx = (int)(uv.x * s.Resolution);
		var hy = (int)(uv.y * s.Resolution);
		hx = Math.Clamp( hx, 0, s.Resolution - 1 );
		hy = Math.Clamp( hy, 0, s.Resolution - 1 );

		var index = hy * s.Resolution + hx;
		var rawHeight = s.HeightMap[index];
		var heightScale = s.TerrainHeight / ushort.MaxValue;
		var worldHeight = rawHeight * heightScale;

		// Convert height to world space
		var heightWorldPos = terrain.WorldTransform.PointToWorld( new Vector3( localPos.x, worldHeight, localPos.y ) );

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message     = $"Height at ({wx}, {wz}).",
			queryXZ     = new { x = wx, z = wz },
			heightmapUV = new { x = hx, y = hy },
			rawHeight,
			worldHeight = MathF.Round( heightWorldPos.y, 2 ),
			worldPosition = new { x = MathF.Round( heightWorldPos.x, 2 ), y = MathF.Round( heightWorldPos.y, 2 ), z = MathF.Round( heightWorldPos.z, 2 ) }
		}, _json ) );
	}

	// ── set_height ──────────────────────────────────────────────────────────

	private static void ModifyHeightRegion( TerrainStorage storage, Vector2 worldCenter, float worldRadius,
		float terrainSizeScale, Func<ushort, float, ushort> modifier )
	{
		var resolution = storage.Resolution;
		var halfRes = terrainSizeScale / 2f;

		// World to heightmap conversion
		int cx = (int)((worldCenter.x / storage.TerrainSize) * resolution);
		int cy = (int)((worldCenter.y / storage.TerrainSize) * resolution);
		int r = Math.Max( 1, (int)((worldRadius / storage.TerrainSize) * resolution) );

		int x0 = Math.Max( 0, cx - r );
		int y0 = Math.Max( 0, cy - r );
		int x1 = Math.Min( resolution - 1, cx + r );
		int y1 = Math.Min( resolution - 1, cy + r );

		for ( int y = y0; y <= y1; y++ )
		{
			for ( int x = x0; x <= x1; x++ )
			{
				float dx = x - cx;
				float dy = y - cy;
				float dist = MathF.Sqrt( dx * dx + dy * dy );
				if ( dist > r ) continue;

				float falloff = 1.0f - (dist / r);
				falloff = falloff * falloff * (3.0f - 2.0f * falloff); // smoothstep

				int idx = y * resolution + x;
				storage.HeightMap[idx] = modifier( storage.HeightMap[idx], falloff );
			}
		}
	}

	private static object SetHeight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 100f );
		float height = OzmiumSceneHelpers.Get( args, "height", 500f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			var heightScale = s.TerrainHeight / ushort.MaxValue;
			ushort targetRaw = (ushort)Math.Clamp( height / heightScale, 0, ushort.MaxValue );

			ModifyHeightRegion( s, new Vector2( wx, wz ), radius, s.TerrainSize / s.Resolution,
				( current, falloff ) =>
				{
					float newHeight = current + (targetRaw - current) * falloff;
					return (ushort)Math.Clamp( newHeight, 0, ushort.MaxValue );
				} );

			// Sync to GPU and rebuild collider
			terrain.SyncGPUTexture();
			var region = new RectInt( 0, 0, s.Resolution, s.Resolution );
			terrain.SyncCPUTexture( Terrain.SyncFlags.Height, region );

			return OzmiumSceneHelpers.Txt( $"Set height to {height} in a {radius}-unit radius around ({wx}, {wz})." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── flatten ─────────────────────────────────────────────────────────────

	private static object Flatten( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 100f );
		float height = OzmiumSceneHelpers.Get( args, "height", 0f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			var heightScale = s.TerrainHeight / ushort.MaxValue;
			ushort targetRaw = (ushort)Math.Clamp( height / heightScale, 0, ushort.MaxValue );

			ModifyHeightRegion( s, new Vector2( wx, wz ), radius, s.TerrainSize / s.Resolution,
				( current, falloff ) =>
				{
					float newHeight = current + (targetRaw - current) * falloff;
					return (ushort)Math.Clamp( newHeight, 0, ushort.MaxValue );
				} );

			terrain.SyncGPUTexture();
			var region = new RectInt( 0, 0, s.Resolution, s.Resolution );
			terrain.SyncCPUTexture( Terrain.SyncFlags.Height, region );

			return OzmiumSceneHelpers.Txt( $"Flattened terrain to height {height} in a {radius}-unit radius around ({wx}, {wz})." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── paint_material ──────────────────────────────────────────────────────

	private static object PaintMaterial( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 100f );
		byte baseTextureId = (byte)OzmiumSceneHelpers.Get( args, "baseTextureId", 0 );
		byte overlayTextureId = (byte)OzmiumSceneHelpers.Get( args, "overlayTextureId", 0 );
		byte blendFactor = (byte)OzmiumSceneHelpers.Get( args, "blendFactor", 255 );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			var resolution = s.Resolution;
			var newMat = new CompactTerrainMaterial( baseTextureId, overlayTextureId, blendFactor );

			int cx = (int)((wx / s.TerrainSize) * resolution);
			int cy = (int)((wz / s.TerrainSize) * resolution);
			int r = Math.Max( 1, (int)((radius / s.TerrainSize) * resolution) );

			int x0 = Math.Max( 0, cx - r );
			int y0 = Math.Max( 0, cy - r );
			int x1 = Math.Min( resolution - 1, cx + r );
			int y1 = Math.Min( resolution - 1, cy + r );

			int painted = 0;
			for ( int y = y0; y <= y1; y++ )
			{
				for ( int x = x0; x <= x1; x++ )
				{
					float dx = x - cx;
					float dy = y - cy;
					float dist = MathF.Sqrt( dx * dx + dy * dy );
					if ( dist > r ) continue;

					float falloff = 1.0f - (dist / r);
					falloff = falloff * falloff * (3.0f - 2.0f * falloff);

					int idx = y * resolution + x;
					var existing = new CompactTerrainMaterial( s.ControlMap[idx] );

					// Blend the blend factor
					byte newBlend = (byte)(existing.BlendFactor + (blendFactor - existing.BlendFactor) * falloff);
					var blended = new CompactTerrainMaterial( baseTextureId, overlayTextureId, newBlend );
					s.ControlMap[idx] = blended.Packed;
					painted++;
				}
			}

			terrain.SyncGPUTexture();
			var region = new RectInt( 0, 0, resolution, resolution );
			terrain.SyncCPUTexture( Terrain.SyncFlags.Control, region );

			return OzmiumSceneHelpers.Txt( $"Painted material (base={baseTextureId}, overlay={overlayTextureId}, blend={blendFactor}) on {painted} texels in {radius}-unit radius around ({wx}, {wz})." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── get_material_at ────────────────────────────────────────────────────

	private static object GetMaterialAt( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );

		var info = terrain.GetMaterialAtWorldPosition( new Vector3( wx, 0, wz ) );
		if ( info == null )
			return OzmiumSceneHelpers.Txt( $"Position ({wx}, {wz}) is outside terrain bounds." );

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message           = $"Material at ({wx}, {wz}).",
			baseTextureId     = info.Value.BaseTextureId,
			overlayTextureId  = info.Value.OverlayTextureId,
			blendFactor       = MathF.Round( info.Value.BlendFactor, 3 ),
			isHole            = info.Value.IsHole,
			baseMaterial      = info.Value.BaseMaterial?.ResourceName,
			overlayMaterial   = info.Value.OverlayMaterial?.ResourceName,
			dominantMaterial  = info.Value.GetDominantMaterial()?.ResourceName
		}, _json ) );
	}

	// ── manage_terrain (Omnibus) ───────────────────────────────────────────

	internal static object ManageTerrain( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create"          => Create( args ),
			"get_info"        => GetInfo( args ),
			"get_height"      => GetHeight( args ),
			"set_height"      => SetHeight( args ),
			"flatten"         => Flatten( args ),
			"paint_material"  => PaintMaterial( args ),
			"get_material_at" => GetMaterialAt( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: create, get_info, get_height, set_height, flatten, paint_material, get_material_at" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageTerrain
	{
		get
		{
			var props = new Dictionary<string, object>();
			props["operation"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "create", "get_info", "get_height", "set_height", "flatten", "paint_material", "get_material_at" } };
			props["id"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of the terrain GameObject (optional, defaults to first terrain in scene)." };
			props["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of the terrain GO." };
			props["x"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position (create) or XZ center for height/paint operations." };
			props["y"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position (create only)." };
			props["z"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position (create) or Z for height/paint operations." };
			props["resolution"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Heightmap resolution (create, default 512)." };
			props["terrainSize"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World width/length of terrain (create, default 20000)." };
			props["terrainHeight"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max world height of terrain (create, default 10000)." };
			props["radius"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Brush radius for set_height/flatten/paint_material." };
			props["height"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Target height for set_height/flatten." };
			props["baseTextureId"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Base texture ID 0-31 for paint_material." };
			props["overlayTextureId"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Overlay texture ID 0-31 for paint_material." };
			props["blendFactor"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Blend factor 0-255 for paint_material." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object> { ["name"] = "manage_terrain", ["description"] = "Create, query, sculpt, and paint terrain. Create terrain with resolution/size/height, sculpt heightmaps, paint splatmap materials.", ["inputSchema"] = schema };
		}
	}
}
