using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Omnibus handler for compilation management operations:
/// compile_project, get_compile_errors, wait_for_compile.
/// </summary>
internal static class CompilationToolHandlers
{
	// ── compile_project ─────────────────────────────────────────────────────

	private static object CompileProject( JsonElement args )
	{
		try
		{
			var projects = Project.All;
			if ( projects == null || projects.Count == 0 )
				return OzmiumSceneHelpers.Txt( "No projects loaded." );

			// Find the target project — default to current/active
			string projectName = OzmiumSceneHelpers.Get( args, "project", (string)null );

			Project target = null;
			if ( !string.IsNullOrEmpty( projectName ) )
			{
				target = projects.FirstOrDefault( p =>
					(p.Config?.Title ?? "").IndexOf( projectName, StringComparison.OrdinalIgnoreCase ) >= 0 ||
					(p.GetRootPath() ?? "").IndexOf( projectName, StringComparison.OrdinalIgnoreCase ) >= 0 );

				if ( target == null )
					return OzmiumSceneHelpers.Txt( $"Project '{projectName}' not found. Use get_compile_errors with no filter to see all projects." );
			}
			else
			{
				target = Project.Current ?? projects.FirstOrDefault( p => p.Active );
				if ( target == null )
					return OzmiumSceneHelpers.Txt( "No active project found." );
			}

			// Trigger compile via Updated (public API that compiles if needed)
			var task = EditorUtility.Projects.Updated( target );
			// We can't truly await here (handler must return synchronously from main thread),
			// so we check the current compiler state and report it.

			bool isBuilding = target.HasCompiler && target.Compiler?.IsBuilding == true;
			bool needsBuild = target.HasCompiler && target.Compiler?.NeedsBuild == true;
			bool lastSuccess = target.HasCompiler && target.Compiler?.BuildSuccess == true;

			// Collect current diagnostics
			var diagnostics = GetDiagnosticsForProject( target );
			int errorCount = diagnostics.Count( d => d.severity == "Error" );
			int warningCount = diagnostics.Count( d => d.severity == "Warning" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = isBuilding
					? $"Compile triggered for '{target.Config?.Title ?? target.GetRootPath()}'. Build in progress..."
					: needsBuild
						? $"Compile queued for '{target.Config?.Title ?? target.GetRootPath()}'."
						: $"Project '{target.Config?.Title ?? target.GetRootPath()}' is up to date.",
				project      = target.Config?.Title ?? "Unknown",
				rootPath     = target.GetRootPath(),
				isBuilding,
				needsBuild,
				lastBuildSuccess = lastSuccess,
				errorCount,
				warningCount
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_compile_errors ──────────────────────────────────────────────────

	private static object GetCompileErrors( JsonElement args )
	{
		try
		{
			string projectFilter = OzmiumSceneHelpers.Get( args, "project", (string)null );
			string severityFilter = OzmiumSceneHelpers.Get( args, "severity", "Warning" );
			int maxResults = OzmiumSceneHelpers.Get( args, "maxResults", 50 );

			var projects = Project.All;
			if ( projects == null || projects.Count == 0 )
				return OzmiumSceneHelpers.Txt( "No projects loaded." );

			var allDiagnostics = new List<Dictionary<string, object>>();
			var projectSummaries = new List<object>();

			foreach ( var project in projects )
			{
				bool matchesFilter = string.IsNullOrEmpty( projectFilter ) ||
					(project.Config?.Title ?? "").IndexOf( projectFilter, StringComparison.OrdinalIgnoreCase ) >= 0 ||
					(project.GetRootPath() ?? "").IndexOf( projectFilter, StringComparison.OrdinalIgnoreCase ) >= 0;

				if ( !matchesFilter ) continue;

				var diagnostics = GetDiagnosticsForProject( project );
				int errors = diagnostics.Count( d => d.severity == "Error" );
				int warnings = diagnostics.Count( d => d.severity == "Warning" );
				int infos = diagnostics.Count( d => d.severity == "Info" );

				projectSummaries.Add( new
				{
					project    = project.Config?.Title ?? "Unknown",
					rootPath   = project.GetRootPath(),
					isBuilding = project.HasCompiler && project.Compiler?.IsBuilding == true,
					buildSuccess = project.HasCompiler && project.Compiler?.BuildSuccess == true,
					errors,
					warnings,
					infos
				} );

				// Filter by severity
				var filtered = diagnostics.Where( d =>
				{
					return severityFilter.ToLowerInvariant() switch
					{
						"error" => d.severity == "Error",
						"warning" => d.severity == "Error" || d.severity == "Warning",
						"info" => d.severity == "Error" || d.severity == "Warning" || d.severity == "Info",
						_ => d.severity == "Error" || d.severity == "Warning"
					};
				} );

				foreach ( var diag in filtered )
				{
					allDiagnostics.Add( new Dictionary<string, object>
					{
						["project"]  = project.Config?.Title ?? "Unknown",
						["severity"] = diag.severity,
						["id"]       = diag.id,
						["message"]  = diag.message,
						["file"]     = diag.file,
						["line"]     = diag.line,
						["column"]   = diag.column
					} );
				}
			}

			// Sort: errors first, then warnings, then info
			allDiagnostics.Sort( ( a, b ) =>
			{
				int SeverityRank( string s ) => s switch { "Error" => 0, "Warning" => 1, _ => 2 };
				return SeverityRank( a["severity"] as string ?? "" )
					.CompareTo( SeverityRank( b["severity"] as string ?? "" ) );
			} );

			if ( allDiagnostics.Count > maxResults )
				allDiagnostics = allDiagnostics.Take( maxResults ).ToList();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				summary = $"{allDiagnostics.Count} diagnostic(s) at severity >= {severityFilter}.",
				projects = projectSummaries,
				diagnostics = allDiagnostics
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── wait_for_compile ────────────────────────────────────────────────────

	private static object WaitForCompile( JsonElement args )
	{
		try
		{
			var projects = Project.All;
			if ( projects == null || projects.Count == 0 )
				return OzmiumSceneHelpers.Txt( "No projects loaded." );

			// Check current build state across all projects
			bool anyBuilding = projects.Any( p => p.HasCompiler && p.Compiler?.IsBuilding == true );
			bool anyNeedsBuild = projects.Any( p => p.HasCompiler && p.Compiler?.NeedsBuild == true );

			var results = new List<object>();
			int totalErrors = 0;
			int totalWarnings = 0;

			foreach ( var project in projects )
			{
				if ( !project.HasCompiler ) continue;

				var diagnostics = GetDiagnosticsForProject( project );
				int errors = diagnostics.Count( d => d.severity == "Error" );
				int warnings = diagnostics.Count( d => d.severity == "Warning" );
				totalErrors += errors;
				totalWarnings += warnings;

				results.Add( new
				{
					project      = project.Config?.Title ?? "Unknown",
					isBuilding   = project.Compiler?.IsBuilding == true,
					needsBuild   = project.Compiler?.NeedsBuild == true,
					buildSuccess = project.Compiler?.BuildSuccess == true,
					errors,
					warnings
				} );
			}

			string status;
			if ( anyBuilding )
				status = "Building — one or more projects are still compiling. Call again to check.";
			else if ( anyNeedsBuild )
				status = "Queued — compiles are pending. Call again to check.";
			else if ( totalErrors > 0 )
				status = $"Complete with errors — {totalErrors} error(s), {totalWarnings} warning(s).";
			else
				status = $"Complete — all builds succeeded. {totalWarnings} warning(s).";

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				status,
				anyBuilding,
				anyNeedsBuild,
				allSucceeded = !anyBuilding && !anyNeedsBuild && totalErrors == 0,
				totalErrors,
				totalWarnings,
				projects = results
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private record struct DiagnosticInfo( string severity, string id, string message, string file, int line, int column );

	private static List<DiagnosticInfo> GetDiagnosticsForProject( Project project )
	{
		var results = new List<DiagnosticInfo>();

		try
		{
			// Try to get diagnostics from the project's static API
			var allDiags = Project.GetCompileDiagnostics();
			if ( allDiags != null )
			{
				foreach ( var d in allDiags )
				{
					// Skip hidden diagnostics
					if ( d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden )
						continue;

					string filePath = "";
					int line = 0;
					int column = 0;

					if ( d.Location?.SourceTree != null )
					{
						filePath = d.Location.SourceTree.FilePath ?? "";
						var lineSpan = d.Location.GetLineSpan();
						line = lineSpan.StartLinePosition.Line + 1;
						column = lineSpan.StartLinePosition.Character + 1;
					}

					// If filtering by project, check if the file belongs to this project
					if ( !string.IsNullOrEmpty( project.GetRootPath() ) && !string.IsNullOrEmpty( filePath ) )
					{
						if ( !filePath.StartsWith( project.GetRootPath(), StringComparison.OrdinalIgnoreCase ) )
							continue;
					}

					results.Add( new DiagnosticInfo(
						d.Severity.ToString(),
						d.Id,
						d.GetMessage(),
						filePath,
						line,
						column
					) );
				}
			}

			// Also check compiler-level diagnostics if available
			if ( project.HasCompiler && project.Compiler?.Diagnostics != null )
			{
				foreach ( var d in project.Compiler.Diagnostics )
				{
					if ( d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden )
						continue;

					// Avoid duplicates from the global list
					string filePath = d.Location?.SourceTree?.FilePath ?? "";
					int line = 0;
					int column = 0;

					if ( d.Location?.SourceTree != null )
					{
						var lineSpan = d.Location.GetLineSpan();
						line = lineSpan.StartLinePosition.Line + 1;
						column = lineSpan.StartLinePosition.Character + 1;
					}

					var info = new DiagnosticInfo( d.Severity.ToString(), d.Id, d.GetMessage(), filePath, line, column );
					if ( !results.Contains( info ) )
						results.Add( info );
				}
			}
		}
		catch
		{
			// Compiler may not be accessible — return empty
		}

		return results;
	}

	// ── Omnibus dispatcher ──────────────────────────────────────────────────

	internal static object ManageCompilation( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"compile_project"   => CompileProject( args ),
			"get_compile_errors" => GetCompileErrors( args ),
			"wait_for_compile"  => WaitForCompile( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation '{operation}'. Valid: compile_project, get_compile_errors, wait_for_compile." )
		};
	}

	// ── Schema ──────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageCompilation
	{
		get
		{
			var props = new Dictionary<string, object>
			{
				["operation"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "The compilation operation to perform.",
					["enum"]        = new[] { "compile_project", "get_compile_errors", "wait_for_compile" }
				},
				["project"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Project name or path substring to filter (optional). If omitted, compile_project targets the active project; get_compile_errors returns diagnostics for all projects."
				},
				["severity"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Minimum severity filter for get_compile_errors: 'Error' (errors only), 'Warning' (errors + warnings, default), or 'Info' (all).",
					["enum"]        = new[] { "Error", "Warning", "Info" }
				},
				["maxResults"] = new Dictionary<string, object>
				{
					["type"]        = "integer",
					["description"] = "Maximum diagnostics to return for get_compile_errors (default 50)."
				}
			};

			var schema = new Dictionary<string, object>
			{
				["type"]       = "object",
				["properties"] = props,
				["required"]   = new[] { "operation" }
			};

			return new Dictionary<string, object>
			{
				["name"]        = "manage_compilation",
				["description"] = "Compilation management: trigger a project recompile and get build status (compile_project), read compilation errors and warnings with file/line/column info (get_compile_errors), or check if all compiles have finished (wait_for_compile). Use get_compile_errors after writing code to verify it compiles. Each diagnostic includes severity, error code, message, file path, line, and column.",
				["inputSchema"] = schema
			};
		}
	}
}
