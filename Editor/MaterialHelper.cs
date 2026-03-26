using System.Linq;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Helper utilities for material loading and application operations on PolygonMesh.
/// Provides centralized methods for material handling in mesh editing tools.
/// </summary>
internal static class MaterialHelper
{
	/// <summary>
	/// Default material path to use when no material is specified or loading fails.
	/// </summary>
	private const string DefaultMaterialPath = "materials/dev/reflectivity_30.vmat";

	/// <summary>
	/// Loads a Material asset from the given path.
	/// Returns null if the path is invalid or loading fails.
	/// </summary>
	/// <param name="materialPath">Path to the material asset (e.g., "materials/dev/dev_01.vmat")</param>
	/// <returns>The loaded Material, or null if loading fails</returns>
	internal static Material LoadMaterial( string materialPath )
	{
		if ( string.IsNullOrEmpty( materialPath ) )
			return null;

		try
		{
			var material = Material.Load( materialPath );
			return material;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Loads a Material asset from the given path, falling back to a default material if loading fails.
	/// </summary>
	/// <param name="materialPath">Path to the material asset (e.g., "materials/dev/dev_01.vmat")</param>
	/// <returns>The loaded Material, or a default material if loading fails</returns>
	internal static Material LoadMaterialOrDefault( string materialPath )
	{
		var material = LoadMaterial( materialPath );
		if ( material != null )
			return material;

		return Material.Load( DefaultMaterialPath );
	}

	/// <summary>
	/// Applies a material to a specific face in a PolygonMesh.
	/// </summary>
	/// <param name="mesh">The PolygonMesh to modify</param>
	/// <param name="faceIndex">Index of the face (0-based)</param>
	/// <param name="material">Material to apply</param>
	/// <returns>True if successful, false otherwise</returns>
	internal static bool ApplyMaterialToFace( PolygonMesh mesh, int faceIndex, Material material )
	{
		if ( mesh == null || material == null )
			return false;

		try
		{
			var faceHandle = mesh.FaceHandleFromIndex( faceIndex );
			if ( !faceHandle.IsValid )
				return false;

			mesh.SetFaceMaterial( faceHandle, material );
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Applies a material to a specific face in a PolygonMesh by material path.
	/// </summary>
	/// <param name="mesh">The PolygonMesh to modify</param>
	/// <param name="faceIndex">Index of the face (0-based)</param>
	/// <param name="materialPath">Path to the material asset</param>
	/// <returns>True if successful, false otherwise</returns>
	internal static bool ApplyMaterialToFace( PolygonMesh mesh, int faceIndex, string materialPath )
	{
		var material = LoadMaterial( materialPath );
		if ( material == null )
			return false;

		return ApplyMaterialToFace( mesh, faceIndex, material );
	}

	/// <summary>
	/// Aligns the texture coordinates of a face to the world grid.
	/// This ensures textures align properly across faces.
	/// </summary>
	/// <param name="mesh">The PolygonMesh to modify</param>
	/// <param name="faceIndex">Index of the face (0-based)</param>
	/// <returns>True if successful, false otherwise</returns>
	internal static bool TextureAlignToGrid( PolygonMesh mesh, int faceIndex )
	{
		if ( mesh == null )
			return false;

		try
		{
			var faceHandle = mesh.FaceHandleFromIndex( faceIndex );
			if ( !faceHandle.IsValid )
				return false;

			mesh.TextureAlignToGrid( mesh.Transform, faceHandle );
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Sets texture parameters for a specific face (UV axes and scale).
	/// </summary>
	/// <param name="mesh">The PolygonMesh to modify</param>
	/// <param name="faceIndex">Index of the face (0-based)</param>
	/// <param name="vAxisU">U axis direction vector</param>
	/// <param name="vAxisV">V axis direction vector</param>
	/// <param name="scale">Texture scale</param>
	/// <returns>True if successful, false otherwise</returns>
	internal static bool SetTextureParameters( PolygonMesh mesh, int faceIndex,
		Vector3 vAxisU, Vector3 vAxisV, Vector2 scale )
	{
		if ( mesh == null )
			return false;

		try
		{
			var faceHandle = mesh.FaceHandleFromIndex( faceIndex );
			if ( !faceHandle.IsValid )
				return false;

			mesh.SetFaceTextureParameters( faceHandle, vAxisU, vAxisV, scale );
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Gets the current material assigned to a face.
	/// </summary>
	/// <param name="mesh">The PolygonMesh to query</param>
	/// <param name="faceIndex">Index of the face (0-based)</param>
	/// <returns>The Material assigned to the face, or null if invalid</returns>
	internal static Material GetFaceMaterial( PolygonMesh mesh, int faceIndex )
	{
		if ( mesh == null )
			return null;

		try
		{
			var faceHandle = mesh.FaceHandleFromIndex( faceIndex );
			if ( !faceHandle.IsValid )
				return null;

			return mesh.GetFaceMaterial( faceHandle );
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Gets the number of faces in the mesh.
	/// </summary>
	internal static int GetFaceCount( PolygonMesh mesh )
	{
		return mesh?.FaceHandles?.Count() ?? 0;
	}

	/// <summary>
	/// Gets the number of vertices in the mesh.
	/// </summary>
	internal static int GetVertexCount( PolygonMesh mesh )
	{
		return mesh?.VertexHandles?.Count() ?? 0;
	}

	/// <summary>
	/// Gets the number of edges in the mesh.
	/// </summary>
	internal static int GetEdgeCount( PolygonMesh mesh )
	{
		return mesh?.HalfEdgeHandles?.Count() ?? 0;
	}
}
