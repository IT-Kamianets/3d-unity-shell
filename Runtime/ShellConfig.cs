using System.Collections.Generic;
using UnityEngine;

namespace ITKamianets.Engine.Shell
{
	/// <summary>One tab of the in-game bottom bar (and, on desktop, of the top bar): a place the player can go.</summary>
	public sealed class ShellPlace
	{
		public string Id;
		public string Label;
		/// <summary>One or two characters drawn above the label, since no icon font is shipped.</summary>
		public string Glyph;

		public ShellPlace(string id, string label, string glyph = "")
		{
			Id = id;
			Label = label;
			Glyph = glyph;
		}
	}

	/// <summary>
	/// Everything the shell needs to know about a game. A game fills <see cref="Current"/> once, before any scene loads
	/// (a <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]</c> method), and the shell scenes
	/// (Boot, Menu) and the in-game chrome read it.
	/// </summary>
	public sealed class ShellConfig
	{
		public static ShellConfig Current { get; set; } = new ShellConfig();

		public string Title = "Temnorid";
		public string Tagline = "";
		/// <summary>Short id used for saved settings and for telling games apart online, for example <c>hippodrome</c>.</summary>
		public string GameId = "game";
		/// <summary>GitHub repository the releases come from, for example <c>WebArtWork/hippodrome.temnorid.com</c>.</summary>
		public string Repo = "";
		public string InstallerSuffix = "-Setup.exe";
		public string StoreUrl = "";
		public int MaxPlayers = 4;
		/// <summary>Whether the landing screen offers playing with others.</summary>
		public bool Multiplayer = true;
		/// <summary>Scene names. The Boot scene is always first in the build.</summary>
		public string MenuScene = "Menu";
		public string GameScene = "Main";
		/// <summary>Gold by default, like the Lords app.</summary>
		public Color Accent = new Color(0.894f, 0.655f, 0.29f);
		public Color Background = new Color(0.07f, 0.067f, 0.067f);
		/// <summary>The places of the in-game bars, in order. The shell adds a Profile place of its own.</summary>
		public List<ShellPlace> Places = new List<ShellPlace>();
		/// <summary>Colours a player can pick at onboarding. Also used by games to tint players apart.</summary>
		public List<Color> PlayerColours = new List<Color>
		{
			new Color(0.86f, 0.28f, 0.25f), new Color(0.25f, 0.55f, 0.9f), new Color(0.3f, 0.75f, 0.4f), new Color(0.95f, 0.75f, 0.2f),
			new Color(0.65f, 0.4f, 0.85f), new Color(0.95f, 0.5f, 0.2f), new Color(0.2f, 0.8f, 0.8f), new Color(0.9f, 0.9f, 0.9f),
		};

		public string Version => Application.version;

		public Color ColourOf(int index)
		{
			if (PlayerColours.Count == 0) return Color.white;
			return PlayerColours[((index % PlayerColours.Count) + PlayerColours.Count) % PlayerColours.Count];
		}
	}
}
