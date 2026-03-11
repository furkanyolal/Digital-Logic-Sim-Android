using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DLS.Description;
using DLS.Graphics;
using DLS.SaveSystem;
using Seb.Helpers;
using UnityEngine;

namespace DLS.Game
{
	public static class Main
	{
		public static readonly Version DLSVersion = new(2, 1, 6);
		public static readonly Version DLSVersion_EarliestCompatible = new(2, 0, 0);
		public const string LastUpdatedString = "12 Mar 2026";
		public static AppSettings ActiveAppSettings;

		public static Project ActiveProject { get; private set; }

		public static Vector2Int FullScreenResolution => new(Display.main.systemWidth, Display.main.systemHeight);
		public static AudioState audioState;

		public static void Init(AudioState audioState)
		{
			SavePaths.EnsureDirectoryExists(SavePaths.ProjectsPath);
			SaveAndApplyAppSettings(Loader.LoadAppSettings());
			Main.audioState = audioState;

			// Unity defaults to 30 FPS on mobile — set to 60 for smoother interaction
			if (Application.platform == RuntimePlatform.Android)
			{
				Application.targetFrameRate = 60;
				QualitySettings.vSyncCount = 0; // Must be 0 for targetFrameRate to take effect
			}
			Seb.Helpers.Haptics.Init();
		}

		static float lastInteractionTime;
		const float activeFramerateTimeout = 2.0f;

		public static void Update()
		{
			InputHelper.UpdatePerFrame();

			if (InputHelper.AnyKeyOrMouseHeldThisFrame || InputHelper.AnyKeyOrMouseDownThisFrame || Seb.Helpers.InputHandling.TouchInputSource.TwoFingerDoubleTapThisFrame || Seb.Helpers.InputHandling.TouchInputSource.ThreeFingerDoubleTapThisFrame)
			{
				lastInteractionTime = Time.time;
			}

			// Throttle framerate on Android to save battery when idle
			if (Application.platform == RuntimePlatform.Android)
			{
				bool isSimRunning = ActiveProject != null && !ActiveProject.simPaused;
				if (Time.time - lastInteractionTime < activeFramerateTimeout || isSimRunning)
				{
					if (Application.targetFrameRate != 60) Application.targetFrameRate = 60;
				}
				else
				{
					if (Application.targetFrameRate != 30) Application.targetFrameRate = 30;
				}
			}

			if (UIDrawer.ActiveMenu != UIDrawer.MenuType.MainMenu)
			{
				CameraController.Update();
				ActiveProject.Update();

				InteractionState.ClearFrame();
				WorldDrawer.DrawWorld(ActiveProject);
			}

			UIDrawer.Draw();

			HandleGlobalInput();
		}


		public static void SaveAndApplyAppSettings(AppSettings newSettings)
		{
			// Save new settings
			ActiveAppSettings = newSettings;
			Saver.SaveAppSettings(newSettings);

			// Apply settings to app (skip resolution/fullscreen on Android — OS manages display)
			if (Application.platform != RuntimePlatform.Android)
			{
				int width = newSettings.fullscreenMode is FullScreenMode.Windowed ? newSettings.ResolutionX : FullScreenResolution.x;
				int height = newSettings.fullscreenMode is FullScreenMode.Windowed ? newSettings.ResolutionY : FullScreenResolution.y;
				Screen.SetResolution(width, height, newSettings.fullscreenMode);
			}

			QualitySettings.vSyncCount = newSettings.VSyncEnabled ? 1 : 0;
		}

		public static void LoadMainMenu()
		{
			UIDrawer.SetActiveMenu(UIDrawer.MenuType.MainMenu);
		}

		public static void CreateOrLoadProject(string projectName, string startupChipName = "")
		{
			if (Loader.ProjectExists(projectName)) ActiveProject = LoadProject(projectName);
			else ActiveProject = CreateProject(projectName);

			ActiveProject.LoadDevChipOrCreateNewIfDoesntExist(startupChipName);
			ActiveProject.description.Prefs_SimPaused = false;
			ActiveProject.StartSimulation();
			ActiveProject.audioState = audioState;
			UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
		}

		static Project CreateProject(string projectName)
		{
			ProjectDescription initialDescription = new()
			{
				ProjectName = projectName,
				DLSVersion_LastSaved = DLSVersion.ToString() + " (Android)",
				DLSVersion_EarliestCompatible = DLSVersion_EarliestCompatible.ToString() + " (Android)",
				CreationTime = DateTime.Now,
				Prefs_ChipPinNamesDisplayMode = PreferencesMenu.DisplayMode_OnHover,
				Prefs_MainPinNamesDisplayMode = PreferencesMenu.DisplayMode_OnHover,
				Prefs_SimTargetStepsPerSecond = 1000,
				Prefs_SimStepsPerClockTick = 250,
				Prefs_SimPaused = false,
				Prefs_UseRadialMenu = true,
				Prefs_HapticFeedback = true,
				AllCustomChipNames = Array.Empty<string>(),
				StarredList = BuiltinCollectionCreator.GetDefaultStarredList().ToList(),
				ChipCollections = new List<ChipCollection>(BuiltinCollectionCreator.CreateDefaultChipCollections())
			};

			Saver.SaveProjectDescription(initialDescription);
			return LoadProject(projectName);
		}

		public static void OpenSaveDataFolderInFileBrowser()
		{
			// Not supported on Android — no desktop file browser
			if (Application.platform == RuntimePlatform.Android)
			{
				Debug.Log("Save data path: " + SavePaths.AllData);
				return;
			}

			try
			{
				string path = SavePaths.AllData;

				if (!Directory.Exists(path)) throw new Exception("Path does not not exist: " + path);

				path = path.Replace("\\", "/");
				string url = "file://" + (path.StartsWith("/") ? path : "/" + path);
				Application.OpenURL(url);
			}
			catch (Exception e)
			{
				Debug.LogError("Error opening folder: " + e.Message);
			}
		}

		static Project LoadProject(string projectName) => Loader.LoadProject(projectName);

		static void HandleGlobalInput()
		{
			if (KeyboardShortcuts.OpenSaveDataFolderShortcutTriggered) OpenSaveDataFolderInFileBrowser();
		}

		public class Version
		{
			public readonly int Major;
			public readonly int Minor;
			public readonly int Patch;

			public Version(int major, int minor, int patch)
			{
				Major = major;
				Minor = minor;
				Patch = patch;
			}

			public int ToInt() => Major * 100000 + Minor * 1000 + Patch;

			public static Version Parse(string versionString)
			{
				string[] versionParts = versionString.Split('.');
				int major = int.Parse(versionParts[0]);
				int minor = int.Parse(versionParts[1]);
				int patch = int.Parse(versionParts[2]);
				return new Version(major, minor, patch);
			}

			public static bool TryParse(string versionString, out Version version)
			{
				try
				{
					version = Parse(versionString);
					return true;
				}
				catch
				{
					version = null;
					return false;
				}
			}

			public override string ToString() => $"{Major}.{Minor}.{Patch}";
		}
	}
}