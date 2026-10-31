using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ITKamianets.Engine.Shell.Editor
{
	/// <summary>
	/// What a game's scene builder calls to get the shell scenes and the build settings, so every game is assembled the same way:
	/// <c>Boot</c> (splash), <c>Menu</c> (landing, onboarding, lobby, profile), then the game's own scenes; and the same Android
	/// and iOS settings.
	/// </summary>
	public static class ShellSceneKit
	{
		public const string BootPath = "Assets/Scenes/Boot.unity";
		public const string MenuPath = "Assets/Scenes/Menu.unity";

		/// <summary>Writes <c>Assets/Scenes/Boot.unity</c>. Opens a new scene, so call it after the game scene is saved and copy out anything the builder still needs from the old one.: a camera and the <see cref="ShellBoot"/> object.</summary>
		public static void BuildBoot(string path = BootPath)
		{
			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			new GameObject("Boot", typeof(ShellBoot));
			Save(scene, path);
		}

		/// <summary>Writes <c>Assets/Scenes/Menu.unity</c>: the <see cref="ShellMenu"/> object, which makes its own camera and canvas.</summary>
		public static void BuildMenu(string path = MenuPath)
		{
			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			new GameObject("Menu", typeof(ShellMenu));
			Save(scene, path);
		}

		static void Save(Scene scene, string path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			EditorSceneManager.SaveScene(scene, path);
		}

		/// <summary>Boot first, then Menu, then the game's scenes in the order given. The Boot and Menu scenes must exist.</summary>
		public static void SetBuildOrder(params string[] gameScenes)
		{
			var scenes = new List<EditorBuildSettingsScene>
			{
				new EditorBuildSettingsScene(BootPath, true),
				new EditorBuildSettingsScene(MenuPath, true),
			};
			foreach (var path in gameScenes) scenes.Add(new EditorBuildSettingsScene(path, true));
			EditorBuildSettings.scenes = scenes.ToArray();
		}

		/// <summary>
		/// Names the game and sets what Android and iOS need: the application id, version, landscape, 64-bit IL2CPP, Android 8 and iOS 15
		/// or newer, and the internet permission that online play and updates use. <paramref name="applicationId"/> looks like
		/// <c>com.temnorid.hippodrome</c>.
		/// </summary>
		public static void ApplyPlatformSettings(string company, string productName, string applicationId, string version)
		{
			PlayerSettings.companyName = company;
			PlayerSettings.productName = productName;
			PlayerSettings.bundleVersion = version;

			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId);
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, applicationId);

			PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
			PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
			PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
			PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
			PlayerSettings.Android.forceInternetPermission = true;
			PlayerSettings.Android.bundleVersionCode = VersionCode(version);

			PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
			PlayerSettings.iOS.targetOSVersionString = "15.0";
			PlayerSettings.iOS.buildNumber = VersionCode(version).ToString();

			PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
			PlayerSettings.allowedAutorotateToPortrait = false;
			PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
			PlayerSettings.allowedAutorotateToLandscapeLeft = true;
			PlayerSettings.allowedAutorotateToLandscapeRight = true;
		}

		/// <summary>1.2.3 becomes 10203, so each release has a higher version code than the one before.</summary>
		public static int VersionCode(string version)
		{
			var parts = (version ?? "0.0.0").Split('.');
			int Part(int i) => i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
			return Part(0) * 10000 + Part(1) * 100 + Part(2);
		}

		/// <summary>Builds an Android package. <paramref name="bundle"/> makes an .aab for Google Play instead of an .apk.</summary>
		public static bool BuildAndroid(string outputPath, bool bundle = false)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
			EditorUserBuildSettings.buildAppBundle = bundle;
			var options = new BuildPlayerOptions
			{
				scenes = EnabledScenes(),
				locationPathName = outputPath,
				target = BuildTarget.Android,
				options = BuildOptions.None,
			};
			var report = BuildPipeline.BuildPlayer(options);
			Debug.Log("[Shell] Android build " + report.summary.result + ", " + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " errors");
			return report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
		}

		/// <summary>Exports an Xcode project to open on a Mac. Only possible where the editor has iOS build support.</summary>
		public static bool ExportIos(string outputFolder)
		{
			Directory.CreateDirectory(outputFolder);
			var options = new BuildPlayerOptions
			{
				scenes = EnabledScenes(),
				locationPathName = outputFolder,
				target = BuildTarget.iOS,
				options = BuildOptions.None,
			};
			var report = BuildPipeline.BuildPlayer(options);
			Debug.Log("[Shell] iOS export " + report.summary.result);
			return report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
		}

		static string[] EnabledScenes()
		{
			var list = new List<string>();
			foreach (var scene in EditorBuildSettings.scenes)
				if (scene.enabled) list.Add(scene.path);
			return list.ToArray();
		}
	}
}
