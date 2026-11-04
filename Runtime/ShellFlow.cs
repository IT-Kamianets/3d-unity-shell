using ITKamianets.Engine.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ITKamianets.Engine.Shell
{
	/// <summary>
	/// How the scenes follow each other, as the Lords app's routes do: Boot (splash) to Menu (landing, onboarding, lobby, profile)
	/// to the game. A game scene can also be opened straight (in the editor, in tests) and still works: the shell is only
	/// ever a screen in front of it.
	/// </summary>
	public static class ShellFlow
	{
		/// <summary>True once the Boot scene has run, so the game scene knows the profile and the update check are already done.</summary>
		public static bool Booted { get; internal set; }

		/// <summary>Set by the Menu scene before it loads a game: the player chose to play against others.</summary>
		public static bool Online => NetSession.Instance.IsOnline;

		/// <summary>Raised just before <see cref="QuitToMenu"/> leaves the game scene, so a game can save first.</summary>
		public static event System.Action BeforeQuit;

		public static void LoadMenu()
		{
			Time.timeScale = 1f;
			SceneManager.LoadScene(ShellConfig.Current.MenuScene);
		}

		/// <summary>Plays alone: leaves any session and loads the game scene.</summary>
		public static void PlaySolo()
		{
			NetSession.Instance.Leave();
			LoadGame();
		}

		/// <summary>True once the game scene has been loaded in this run, so a later return to the menu shows its landing screen instead of skipping it.</summary>
		public static bool GameLoaded { get; private set; }

		public static void LoadGame()
		{
			GameLoaded = true;
			Time.timeScale = 1f;
			SceneManager.LoadScene(ShellConfig.Current.GameScene);
		}

		/// <summary>Leaves the game scene for the landing screen, closing any session.</summary>
		public static void QuitToMenu()
		{
			BeforeQuit?.Invoke();
			NetSession.Instance.Leave();
			LoadMenu();
		}

		/// <summary>Starts hooking the player's profile into the session and returns the profile. Call once when a scene starts.</summary>
		public static ShellProfile Begin()
		{
			var profile = ShellProfile.Current;
			profile.ApplyToNet();
			return profile;
		}
	}
}
