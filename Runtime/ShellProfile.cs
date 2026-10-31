using System;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Net;
using UnityEngine;

namespace ITKamianets.Engine.Shell
{
	/// <summary>Who is playing on this device: a name and a colour picked once at onboarding, and whether to keep the game up to date by itself.</summary>
	[Serializable]
	public sealed class ShellProfile
	{
		public string name = "";
		public int colour;
		public bool onboarded;
		public bool autoUpdate = true;

		static ShellProfile _current;

		static string Key => "shell.profile." + ShellConfig.Current.GameId;

		public static ShellProfile Current => _current ??= Load();

		public static ShellProfile Load()
		{
			try
			{
				return EngineStorage.Current.GetJson(Key, new ShellProfile()) ?? new ShellProfile();
			}
			catch (ArgumentException)
			{
				return new ShellProfile();
			}
		}

		public void Save()
		{
			EngineStorage.Current.SetJson(Key, this);
			EngineStorage.Current.Save();
			ApplyToNet();
		}

		public string DisplayName => string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();

		public Color Tint => ShellConfig.Current.ColourOf(colour);

		/// <summary>Gives the multiplayer session this player's name and colour and the game's identity.</summary>
		public void ApplyToNet()
		{
			var config = ShellConfig.Current;
			var net = NetSession.Ensure();
			net.GameId = config.GameId;
			net.GameVersion = config.Version;
			net.MaxPlayers = config.MaxPlayers;
			net.LocalName = DisplayName;
			net.LocalColour = colour;
		}

		/// <summary>Forgets the loaded profile so the next <see cref="Current"/> reads it again. For tests.</summary>
		public static void Reset()
		{
			_current = null;
		}

		public static string CleanName(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw)) return "";
			var text = raw.Trim();
			return text.Length > 16 ? text.Substring(0, 16) : text;
		}
	}
}
