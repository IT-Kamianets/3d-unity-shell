using System;
using System.Threading.Tasks;
using ITKamianets.Engine.Account;

namespace ITKamianets.Engine.Shell
{
	/// <summary>Keeps the player's name the lord's name from Lords, so the top bar and the lobby show the same lord in every game.</summary>
	public static class ShellLord
	{
		static bool _busy;

		/// <summary>The class of the player's lord in Lords (<c>warrior</c>, <c>mage</c>...), once it has been read; null before that, for a guest or offline.</summary>
		public static string ClassId { get; private set; }

		/// <summary>Reads the lord's name and, when there is one, makes it the player's name. Safe to call often; does nothing offline or for a guest.</summary>
		public static async Task SyncAsync()
		{
			if (_busy || !GameAccount.IsSignedIn) return;
			_busy = true;
			try
			{
				var classId = await LordProfile.GetClassAsync();
				if (!string.IsNullOrEmpty(classId)) ClassId = classId;
				var name = await LordProfile.GetNameAsync();
				if (string.IsNullOrEmpty(name)) return;
				var profile = ShellProfile.Current;
				if (profile.name == name) return;
				profile.name = name;
				profile.Save();
			}
			catch (Exception)
			{
				// Offline or an unexpected answer: the name stays as it was.
			}
			finally
			{
				_busy = false;
			}
		}

		/// <summary>Restores the saved sign-in, then reads the lord's name.</summary>
		public static async Task RestoreAndSyncAsync()
		{
			await GameAccount.RestoreAsync();
			await SyncAsync();
		}
	}
}
