/*
using UnityEngine;
using System.Collections;
using System.IO;
using SimpleFileBrowser;
using System.Collections.Generic;
using CATHODE.LEGACY;

public class FiileBrowserTest : MonoBehaviour
{
	// Warning: paths returned by FileBrowser dialogs do not contain a trailing '\' character
	// Warning: FileBrowser can only show 1 dialog at a time

	CathodeTextures LevelTextures;
	string levelName = "BSP_TORRENS";

	void Start()
	{
		string levelPath = @"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV\PRODUCTION\" + levelName + "\\";
		LevelTextures = new CathodeTextures(levelPath + "/RENDERABLE/LEVEL_TEXTURES.ALL.PAK", levelPath + "/RENDERABLE/LEVEL_TEXTURE_HEADERS.ALL.BIN");

		FileBrowserHelpers.SetupFilepaths(new List<string>(LevelTextures.TextureFilePaths));

		//FileBrowser.SetFilters(true, new FileBrowser.Filter("Images", ".jpg", ".png"), new FileBrowser.Filter("Text Files", ".txt", ".pdf"));
		//FileBrowser.SetDefaultFilter(".jpg");
		//FileBrowser.SetExcludedExtensions(".lnk", ".tmp", ".zip", ".rar", ".exe");
		//FileBrowser.AddQuickLink("Users", "C:\\Users", null);
		StartCoroutine(ShowLoadDialogCoroutine());
	}

	IEnumerator ShowLoadDialogCoroutine()
	{
		yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.FilesAndFolders, true, "", null, levelName + " Textures", "Open");

		Debug.Log(FileBrowser.Success);

		if (FileBrowser.Success)
		{
			// Print paths of the selected files (FileBrowser.Result) (null, if FileBrowser.Success is false)
			for (int i = 0; i < FileBrowser.Result.Length; i++)
				Debug.Log(FileBrowser.Result[i]);

			// Read the bytes of the first file via FileBrowserHelpers
			// Contrary to File.ReadAllBytes, this function works on Android 10+, as well
			byte[] bytes = FileBrowserHelpers.ReadBytesFromFile(FileBrowser.Result[0]);

			// Or, copy the first file to persistentDataPath
			string destinationPath = Path.Combine(Application.persistentDataPath, FileBrowserHelpers.GetFilename(FileBrowser.Result[0]));
			FileBrowserHelpers.CopyFile(FileBrowser.Result[0], destinationPath);
		}
	}
}*/