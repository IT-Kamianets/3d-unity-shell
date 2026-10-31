using ITKamianets.Engine.Update;
using UnityEngine;

namespace ITKamianets.Engine.Shell
{
	/// <summary>The one <see cref="AutoUpdater"/> of the game, set up from <see cref="ShellConfig"/>. It lives across scenes.</summary>
	public static class ShellUpdates
	{
		static AutoUpdater _updater;

		public static AutoUpdater Updater
		{
			get
			{
				if (_updater != null) return _updater;
				var config = ShellConfig.Current;
				var go = new GameObject("AutoUpdater");
				if (Application.isPlaying) Object.DontDestroyOnLoad(go);
				_updater = go.AddComponent<AutoUpdater>();
				_updater.Repo = config.Repo;
				_updater.WindowsAssetSuffix = config.InstallerSuffix;
				_updater.StoreUrl = config.StoreUrl;
				return _updater;
			}
		}

		/// <summary>Checks for an update unless this is the editor or no repository is configured.</summary>
		public static void CheckIfPossible()
		{
			if (string.IsNullOrEmpty(ShellConfig.Current.Repo)) return;
			if (Application.isEditor) return;
			Updater.Check();
		}

		public static bool CanSelfUpdate => Application.platform == RuntimePlatform.WindowsPlayer;
	}
}
