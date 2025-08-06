using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Editor.CodeEditors;

[Title( "Cursor" )]
public class Cursor : ICodeEditor

{
	public void OpenFile( string path, int? line, int? column )
	{
		var codeWorkspace = $"{Environment.CurrentDirectory}/s&box.code-workspace";
		CreateWorkspace( codeWorkspace );

		Launch( $"\"{codeWorkspace}\" -g \"{path}:{line}:{column}\"" );
	}

	public void OpenSolution()
	{
		var codeWorkspace = $"{Environment.CurrentDirectory}/s&box.code-workspace";
		CreateWorkspace( codeWorkspace );

		// Need to wrap the code workspace in quotes, but CreateWorkspace doesn't need that
		Launch( $"\"{codeWorkspace}\"" );
	}

	public void OpenAddon( Project addon )
	{
		var projectPath = (addon != null) ? addon.GetRootPath() : "";

		Launch( $"\"{projectPath}\"" );
	}

	public bool IsInstalled() => !string.IsNullOrEmpty( GetLocation() );

	private static void Launch( string arguments )
	{
		var startInfo = new System.Diagnostics.ProcessStartInfo
		{
			FileName = GetLocation(),
			Arguments = arguments,
			CreateNoWindow = true,
		};

		System.Diagnostics.Process.Start( startInfo );
	}

	private static void CreateWorkspace( string path )
	{
		StringBuilder builder = new();
		builder.AppendLine( "{" );
		builder.AppendLine( "    \"folders\": [" );

		foreach ( var addon in EditorUtility.Projects.GetAll() )
		{
			if ( !addon.Active ) continue;

			builder.AppendLine( "        {" );
			builder.AppendLine( $"            \"name\": \"{addon.Config.Ident}\"," );
			builder.AppendLine( $"            \"path\": \"{addon.GetRootPath().Replace( @"\", @"\\" )}\"," );
			builder.AppendLine( "        }," );
		}

		builder.AppendLine( "    ]" );

		// You need the C# extension to do anything
		// builder.AppendLine( "    \"extensions\": {" );
		// builder.AppendLine( "        \"recommendations\": [" );
		// builder.AppendLine( "            \"ms-dotnettools.csharp\"" );
		// builder.AppendLine( "        ]," );
		// builder.AppendLine( "    }" );

		// Settings: make sure we're using .net 6 and that roslyn analyzers are on (they never fucking are)
		// builder.AppendLine( "    \"settings\": {" );
		// builder.AppendLine( "        \"omnisharp.useModernNet\": true," );
		// builder.AppendLine( "        \"omnisharp.enableRoslynAnalyzers\": true" );
		// builder.AppendLine( "    }" );

		builder.AppendLine( "}" );

		File.WriteAllText( path, builder.ToString() );
	}

	static string Location;

	[System.Diagnostics.CodeAnalysis.SuppressMessage( "Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>" )]
	private static string GetLocation()
	{
		if ( Location != null )
		{
			return Location;
		}

		string value = null;
		using ( var key = Registry.ClassesRoot.OpenSubKey( @"Applications\\Cursor.exe\\shell\\open\\command" ) )
		{
			value = key?.GetValue( "" ) as string;
		}

		if ( value == null )
		{
			return null;
		}

		// Given `"C:\Users\<user>\AppData\Local\Programs\cursor\Cursor.exe" "%1"` grab the first bit
		Regex rgx = new Regex( "\"(.*)\" \".*\"", RegexOptions.IgnoreCase );
		var matches = rgx.Matches( value );
		if ( matches.Count == 0 || matches[0].Groups.Count < 2 )
		{
			return null;
		}

		Location = matches[0].Groups[1].Value;
		return Location;
	}
}
