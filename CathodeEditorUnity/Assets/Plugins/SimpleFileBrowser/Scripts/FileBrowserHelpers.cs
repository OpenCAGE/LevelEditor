using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SimpleFileBrowser
{
	public struct FileSystemEntry
	{
		public readonly string Path;
		public readonly string Name;
		public readonly string Extension;
		public readonly FileAttributes Attributes;

		public bool IsDirectory { get { return ( Attributes & FileAttributes.Directory ) == FileAttributes.Directory; } }

		public FileSystemEntry( string path, string name, string extension, bool isDirectory )
		{
			Path = path;
			Name = name;
			Extension = extension;
			Attributes = isDirectory ? FileAttributes.Directory : FileAttributes.Normal;
		}

		public FileSystemEntry( FileSystemInfo fileInfo, string extension )
		{
			Path = fileInfo.FullName;
			Name = fileInfo.Name;
			Extension = extension;
			Attributes = fileInfo.Attributes;
		}
		public FileSystemEntry(FileSystemInfoWrapper fileInfo, string extension)
		{
			Path = fileInfo.FullName;
			Name = fileInfo.Name;
			Extension = extension;
			Attributes = fileInfo.Attributes;
		}
	}

	public static class FileBrowserHelpers
	{
		//Populate all possible directories inside the PAK
		private static List<string> PakFilepaths;
		private static List<string> PakFolders;
		public static void SetupFilepaths(List<string> filepaths)
        {
			PakFilepaths = filepaths;
			for (int i = 0; i < PakFilepaths.Count; i++) PakFilepaths[i] = PakFilepaths[i].Replace('\\', '/');

			PakFolders = new List<string>();
			for (int i = 0; i < PakFilepaths.Count; i++)
			{
				string folder = PakFilepaths[i].Substring(0, PakFilepaths[i].Length - Path.GetFileName(PakFilepaths[i]).Length).Replace('\\', '/');
				List<string> possibleFolders = new List<string>();
				possibleFolders.Add(folder);
				string[] splitFolder = folder.Split('/');
				for (int x = 0; x < splitFolder.Length; x++)
				{
					string folderPath = "";
					for (int z = 0; z < x; z++) folderPath += splitFolder[z] + "/";
					possibleFolders.Add(folderPath);
				}
				for (int x = 0; x < possibleFolders.Count; x++) if (!PakFolders.Contains(possibleFolders[x])) PakFolders.Add(possibleFolders[x]);
			}
			PakFolders.Sort();
		}

#if !UNITY_EDITOR && UNITY_ANDROID
		private static AndroidJavaClass m_ajc = null;
		public static AndroidJavaClass AJC
		{
			get
			{
				if( m_ajc == null )
					m_ajc = new AndroidJavaClass( "com.yasirkula.unity.FileBrowser" );

				return m_ajc;
			}
		}

		private static AndroidJavaObject m_context = null;
		public static AndroidJavaObject Context
		{
			get
			{
				if( m_context == null )
				{
					using( AndroidJavaObject unityClass = new AndroidJavaClass( "com.unity3d.player.UnityPlayer" ) )
					{
						m_context = unityClass.GetStatic<AndroidJavaObject>( "currentActivity" );
					}
				}

				return m_context;
			}
		}

		private static string m_temporaryFilePath = null;
		private static string TemporaryFilePath
		{
			get
			{
				if( m_temporaryFilePath == null )
				{
					m_temporaryFilePath = Path.Combine( Application.temporaryCachePath, "tmpFile" );
					Directory.CreateDirectory( Application.temporaryCachePath );
				}

				return m_temporaryFilePath;
			}
		}
		
		// On Android 10+, filesystem can be accessed via Storage Access Framework only
		private static bool? m_shouldUseSAF = null;
		public static bool ShouldUseSAF
		{
			get
			{
				if( m_shouldUseSAF == null )
					m_shouldUseSAF = AJC.CallStatic<bool>( "CheckSAF" );

				return m_shouldUseSAF.Value;
			}
		}
#endif

		public static bool FileExists( string path )
		{
			for (int i = 0; i < PakFilepaths.Count; i++)
			{
				if (PakFilepaths[i].ToUpper() == path.ToUpper())
				{
					return true;
				}
			}
			return false;
		}

		public static bool DirectoryExists( string path )
		{
			if (path != "")
			{
				path = path.Replace('\\', '/').ToUpper();
				if (path[path.Length - 1] != '/') path += '/';
			}
			if (path == "/") return true;
			for (int i = 0; i < PakFolders.Count; i++)
            {
				if (PakFolders[i].ToUpper() == path.ToUpper()) return true;
            }
			return false;
		}

		public static bool IsDirectory( string path )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
				return AJC.CallStatic<bool>( "SAFEntryDirectory", Context, path );
#endif
			if(DirectoryExists( path ) )
				return true;
			if(FileExists( path ) )
				return false;

			string extension = Path.GetExtension( path );
			return extension == null || extension.Length <= 1; // extension includes '.'
		}

		public static string GetDirectoryName( string path )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
				return AJC.CallStatic<string>( "GetParentDirectory", Context, path );
#endif
			return Path.GetDirectoryName( path );
		}

		public static FileSystemEntry[] GetEntriesInDirectory( string path, bool extractOnlyLastSuffixFromExtensions )
		{
			if (path != "")
			{
				path = path.Replace('\\', '/').ToUpper();
				if (path[path.Length - 1] != '/') path += '/';
			}

			//Get all files and directories in current path
			List<FileSystemInfoWrapper> currentPathEntries = new List<FileSystemInfoWrapper>();
			for (int i = 0; i < PakFilepaths.Count; i++)
            {
				if (PakFilepaths[i].Length >= path.Length && 
					PakFilepaths[i].Substring(0, PakFilepaths[i].Length - Path.GetFileName(PakFilepaths[i]).Length).ToUpper() == path.ToUpper())
				{
					FileSystemInfoWrapper info_cathode = new FileSystemInfoWrapper();
                    info_cathode.FullName = PakFilepaths[i];
					info_cathode.Name = Path.GetFileName(PakFilepaths[i]);
					info_cathode.Attributes = FileAttributes.Normal;
					currentPathEntries.Add(info_cathode);
				}
			}
			for (int x = 0; x < PakFolders.Count; x++)
			{
				if (PakFolders[x].Split('/').Length == path.Split('/').Length + 1 && PakFolders[x].Substring(0, path.Length).ToUpper() == path.ToUpper())
				{
					FileSystemInfoWrapper info_cathode = new FileSystemInfoWrapper();
					info_cathode.FullName = PakFolders[x];
					info_cathode.Name = PakFolders[x].Split('/')[PakFolders[x].Split('/').Length - 2];
					info_cathode.Attributes = FileAttributes.Directory;
					currentPathEntries.Add(info_cathode);
				}
			}
			try
			{
				FileSystemEntry[] result = new FileSystemEntry[currentPathEntries.Count];
				int index = 0;
				for (int i = 0; i < currentPathEntries.Count; i++)
				{
					try
					{
						result[index] = new FileSystemEntry(currentPathEntries[i], FileBrowser.GetExtensionFromFilename(currentPathEntries[i].Name, extractOnlyLastSuffixFromExtensions));
						index++;
					}
					catch (System.Exception e)
					{
						Debug.LogException(e);
					}
				}

				if (result.Length != index)
					System.Array.Resize(ref result, index);

				return result;
			}
			catch (System.Exception e)
			{
				Debug.LogException(e);
				return null;
			}
		}

		public static string CreateFileInDirectory( string directoryPath, string filename )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
				return AJC.CallStatic<string>( "CreateSAFEntry", Context, directoryPath, false, filename );
#endif

			string path = Path.Combine( directoryPath, filename );
			using( File.Create( path ) ) { }
			return path;
		}

		public static string CreateFolderInDirectory( string directoryPath, string folderName )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
				return AJC.CallStatic<string>( "CreateSAFEntry", Context, directoryPath, true, folderName );
#endif

			string path = Path.Combine( directoryPath, folderName );
			Directory.CreateDirectory( path );
			return path;
		}

		public static void WriteBytesToFile( string targetPath, byte[] bytes )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				File.WriteAllBytes( TemporaryFilePath, bytes );
				AJC.CallStatic( "WriteToSAFEntry", Context, targetPath, TemporaryFilePath, false );
				File.Delete( TemporaryFilePath );
				
				return;
			}
#endif
			File.WriteAllBytes( targetPath, bytes );
		}

		public static void WriteTextToFile( string targetPath, string text )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				File.WriteAllText( TemporaryFilePath, text );
				AJC.CallStatic( "WriteToSAFEntry", Context, targetPath, TemporaryFilePath, false );
				File.Delete( TemporaryFilePath );
				
				return;
			}
#endif
			File.WriteAllText( targetPath, text );
		}

		public static void AppendBytesToFile( string targetPath, byte[] bytes )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				File.WriteAllBytes( TemporaryFilePath, bytes );
				AJC.CallStatic( "WriteToSAFEntry", Context, targetPath, TemporaryFilePath, true );
				File.Delete( TemporaryFilePath );
				
				return;
			}
#endif
			using( var stream = new FileStream( targetPath, FileMode.Append, FileAccess.Write ) )
			{
				stream.Write( bytes, 0, bytes.Length );
			}
		}

		public static void AppendTextToFile( string targetPath, string text )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				File.WriteAllText( TemporaryFilePath, text );
				AJC.CallStatic( "WriteToSAFEntry", Context, targetPath, TemporaryFilePath, true );
				File.Delete( TemporaryFilePath );
				
				return;
			}
#endif
			File.AppendAllText( targetPath, text );
		}

		private static void AppendFileToFile( string targetPath, string sourceFileToAppend )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				AJC.CallStatic( "WriteToSAFEntry", Context, targetPath, sourceFileToAppend, true );
				return;
			}
#endif
			using( Stream input = File.OpenRead( sourceFileToAppend ) )
			using( Stream output = new FileStream( targetPath, FileMode.Append, FileAccess.Write ) )
			{
				byte[] buffer = new byte[4096];
				int bytesRead;
				while( ( bytesRead = input.Read( buffer, 0, buffer.Length ) ) > 0 )
					output.Write( buffer, 0, bytesRead );
			}
		}

		public static byte[] ReadBytesFromFile( string sourcePath )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				AJC.CallStatic( "ReadFromSAFEntry", Context, sourcePath, TemporaryFilePath );
				byte[] result = File.ReadAllBytes( TemporaryFilePath );
				File.Delete( TemporaryFilePath );
				return result;
			}
#endif
			return File.ReadAllBytes( sourcePath );
		}

		public static string ReadTextFromFile( string sourcePath )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				AJC.CallStatic( "ReadFromSAFEntry", Context, sourcePath, TemporaryFilePath );
				string result = File.ReadAllText( TemporaryFilePath );
				File.Delete( TemporaryFilePath );
				return result;
			}
#endif
			return File.ReadAllText( sourcePath );
		}

		public static void CopyFile( string sourcePath, string destinationPath )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				AJC.CallStatic( "CopyFile", Context, sourcePath, destinationPath, false );
				return;
			}
#endif
			File.Copy( sourcePath, destinationPath, true );
		}

		public static void CopyDirectory( string sourcePath, string destinationPath )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				AJC.CallStatic( "CopyDirectory", Context, sourcePath, destinationPath, false );
				return;
			}
#endif
			CopyDirectoryRecursively( new DirectoryInfo( sourcePath ), destinationPath );
		}

		private static void CopyDirectoryRecursively( DirectoryInfo sourceDirectory, string destinationPath )
		{
			Directory.CreateDirectory( destinationPath );

			FileInfo[] files = sourceDirectory.GetFiles();
			for( int i = 0; i < files.Length; i++ )
				files[i].CopyTo( Path.Combine( destinationPath, files[i].Name ), true );

			DirectoryInfo[] subDirectories = sourceDirectory.GetDirectories();
			for( int i = 0; i < subDirectories.Length; i++ )
				CopyDirectoryRecursively( subDirectories[i], Path.Combine( destinationPath, subDirectories[i].Name ) );
		}

		public static void MoveFile( string sourcePath, string destinationPath )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				AJC.CallStatic( "CopyFile", Context, sourcePath, destinationPath, true );
				return;
			}
#endif
			File.Move( sourcePath, destinationPath );
		}

		public static void MoveDirectory( string sourcePath, string destinationPath )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				AJC.CallStatic( "CopyDirectory", Context, sourcePath, destinationPath, true );
				return;
			}
#endif
			Directory.Move( sourcePath, destinationPath );
		}

		public static string RenameFile( string path, string newName )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
				return AJC.CallStatic<string>( "RenameSAFEntry", Context, path, newName );
#endif
			string newPath = Path.Combine( Path.GetDirectoryName( path ), newName );
			File.Move( path, newPath );

			return newPath;
		}

		public static string RenameDirectory( string path, string newName )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
				return AJC.CallStatic<string>( "RenameSAFEntry", Context, path, newName );
#endif
			string newPath = Path.Combine( new DirectoryInfo( path ).Parent.FullName, newName );
			Directory.Move( path, newPath );

			return newPath;
		}

		public static void DeleteFile( string path )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				AJC.CallStatic<bool>( "DeleteSAFEntry", Context, path );
				return;
			}
#endif
			File.Delete( path );
		}

		public static void DeleteDirectory( string path )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
			{
				AJC.CallStatic<bool>( "DeleteSAFEntry", Context, path );
				return;
			}
#endif
			Directory.Delete( path, true );
		}

		public static string GetFilename( string path )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
				return AJC.CallStatic<string>( "SAFEntryName", Context, path );
#endif
			return Path.GetFileName( path );
		}

		public static long GetFilesize( string path )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			if( ShouldUseSAF )
				return AJC.CallStatic<long>( "SAFEntrySize", Context, path );
#endif
			return new FileInfo( path ).Length;
		}

		public static System.DateTime GetLastModifiedDate( string path )
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			// Credit: https://stackoverflow.com/a/28504416/2373034
			if( ShouldUseSAF )
				return new System.DateTime( 1970, 1, 1, 0, 0, 0 ).AddMilliseconds( AJC.CallStatic<long>( "SAFEntryLastModified", Context, path ) );
#endif
			return new FileInfo( path ).LastWriteTime;
		}
	}
}

public class FileSystemInfoWrapper
{
	public string FullName;
	public string Name;
	public FileAttributes Attributes;
}