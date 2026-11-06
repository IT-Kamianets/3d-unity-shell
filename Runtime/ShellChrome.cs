using System;
using System.Collections.Generic;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ITKamianets.Engine.Shell
{
	/// <summary>
	/// The frame around every game screen, as in the Lords app: a top bar with the game, the place the player is in and who they
	/// are, and the places themselves as tabs, under the thumb at the bottom on a phone and in the top bar on a desktop. A Profile
	/// place is always last; the game adds its own before it and shows its own content for each.
	/// <para>
	/// The game creates it once with <see cref="Create"/>, sets <see cref="ShellConfig.Places"/> (or calls <see cref="SetPlaces"/>),
	/// listens to <see cref="PlaceSelected"/> and keeps its own HUD inside the strips left free: <see cref="TopFraction"/> and
	/// <see cref="BottomFraction"/> are how much of the screen height the bars take.
	/// </para>
	/// </summary>
	public sealed class ShellChrome : MonoBehaviour
	{
		public const string ProfilePlace = "profile";

		public static ShellChrome Current { get; private set; }

		/// <summary>The tabs go to the bottom of the screen instead of the top bar. True on phones and tablets.</summary>
		public bool BottomTabs = Application.isMobilePlatform;

		public event Action<string> PlaceSelected;

		/// <summary>The arrows on each side of the tabs, for a game that shows one place at a time (<see cref="EnableNavigation"/>).</summary>
		public event Action NavigatePrevious;

		public event Action NavigateNext;

		public string CurrentPlace { get; private set; } = "";
		public float TopFraction => 0.07f;
		public float BottomFraction => BottomTabs ? 0.1f : 0f;
		public bool ProfileOpen => _profile != null && _profile.Root.activeSelf;

		UiBuilder _ui;
		Canvas _canvas;
		RectTransform _safe;
		Transform _topBar;
		Text _placeTitle;
		Text _player;
		Text _status;
		readonly List<ShellPlace> _places = new List<ShellPlace>();
		readonly List<Image> _tabBack = new List<Image>();
		readonly List<Text> _tabText = new List<Text>();
		GameObject _tabs;
		GameObject _previous;
		GameObject _next;
		bool _navigation;
		ShellProfilePanel _profile;
		float _refreshAt;

		/// <summary>Builds the bars on a new overlay canvas. Call once per game scene, after the scene's own camera exists.</summary>
		public static ShellChrome Create(Transform parent = null, int sortingOrder = 50)
		{
			var go = new GameObject("ShellChrome");
			if (parent != null) go.transform.SetParent(parent, false);
			var chrome = go.AddComponent<ShellChrome>();
			chrome.Build(sortingOrder);
			Current = chrome;
			return chrome;
		}

		void OnDestroy()
		{
			if (Current == this) Current = null;
		}

		void Build(int sortingOrder)
		{
			var config = ShellConfig.Current;
			ShellFlow.Begin();
			UiBuilder.EnsureEventSystem(transform);
			_canvas = UiBuilder.CreateCanvas(transform, "ChromeCanvas");
			_canvas.sortingOrder = sortingOrder;
			_ui = ShellUi.Builder();

			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(_canvas.transform, false);
			_safe = (RectTransform)safe.transform;

			var bar = UiBuilder.Box("TopBar", _safe, new Vector2(0f, 1f - TopFraction), Vector2.one, new Color(0.07f, 0.067f, 0.067f, 0.92f));
			_topBar = bar.transform;
			_ui.Label("Game", bar.transform, config.Title, 34, TextAnchor.MiddleLeft, new Vector2(0.015f, 0f), new Vector2(0.3f, 1f), config.Accent);
			_placeTitle = _ui.Label("Place", bar.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0.3f, 0f), new Vector2(0.7f, 1f), ShellUi.Text);
			_status = _ui.Label("Status", bar.transform, "", 24, TextAnchor.MiddleRight, new Vector2(0.55f, 0f), new Vector2(0.8f, 1f), ShellUi.Muted);
			_player = _ui.Label("Player", bar.transform, "", 28, TextAnchor.MiddleRight, new Vector2(0.8f, 0f), new Vector2(0.985f, 1f), ShellUi.Text);
			_previous = MakeArrow(bar.transform, "<", new Vector2(0.27f, 0.1f), new Vector2(0.32f, 0.9f), () => NavigatePrevious?.Invoke());
			_next = MakeArrow(bar.transform, ">", new Vector2(0.74f, 0.1f), new Vector2(0.79f, 0.9f), () => NavigateNext?.Invoke());

			_places.Clear();
			foreach (var place in config.Places) _places.Add(place);
			if (config.Profile) _places.Add(new ShellPlace(ProfilePlace, "Profile", ""));
			BuildTabs();

			_profile = new ShellProfilePanel(_ui, _safe, "Profile", () => Select(_places.Count > OwnPlaces ? _places[0].Id : ""), "Resume", () => ShellFlow.QuitToMenu(), "Main menu");
			_profile.Root.SetActive(false);

			if (_places.Count > OwnPlaces) Select(_places[0].Id, false);
			RefreshBar();
		}

		/// <summary>Replaces the game's places. The Profile place is kept at the end.</summary>
		public void SetPlaces(IEnumerable<ShellPlace> places)
		{
			_places.Clear();
			foreach (var place in places) _places.Add(place);
			if (ShellConfig.Current.Profile) _places.Add(new ShellPlace(ProfilePlace, "Profile", ""));
			BuildTabs();
			if (_places.Count > OwnPlaces) Select(_places[0].Id, false);
		}

		GameObject MakeArrow(Transform parent, string text, Vector2 min, Vector2 max, Action onClick)
		{
			var button = _ui.Button(text, parent, min, max, onClick);
			button.GetComponentInChildren<Text>().fontSize = 34;
			button.gameObject.SetActive(false);
			return button.gameObject;
		}

		/// <summary>Makes room for the arrows around the tabs. Call once; then <see cref="SetNavigationVisible"/> shows or hides them.</summary>
		public void EnableNavigation()
		{
			if (_navigation) return;
			_navigation = true;
			BuildTabs();
		}

		public void SetNavigationVisible(bool visible)
		{
			if (_previous != null) _previous.SetActive(visible && _navigation);
			if (_next != null) _next.SetActive(visible && _navigation);
		}

		/// <summary>How many places are the Profile place: none when the game has no Profile.</summary>
		static int OwnPlaces => ShellConfig.Current.Profile ? 1 : 0;

		void BuildTabs()
		{
			if (_tabs != null) Destroy(_tabs);
			_tabBack.Clear();
			_tabText.Clear();

			var bottom = BottomTabs;
			_tabs = UiBuilder.Box("Tabs", _safe, bottom ? Vector2.zero : new Vector2(_navigation ? 0.33f : 0.28f, 1f - TopFraction), bottom ? new Vector2(1f, BottomFraction) : new Vector2(_navigation ? 0.73f : 0.78f, 1f), bottom ? new Color(0.07f, 0.067f, 0.067f, 0.94f) : Color.clear);
			var count = _places.Count;
			for (var i = 0; i < count; i++)
			{
				var place = _places[i];
				var min = new Vector2(i / (float)count, bottom ? 0f : 0.1f);
				var max = new Vector2((i + 1) / (float)count, bottom ? 1f : 0.9f);
				var box = UiBuilder.Box("Tab " + place.Id, _tabs.transform, min, max, Color.clear);
				var button = box.AddComponent<Button>();
				button.targetGraphic = box.GetComponent<Image>();
				var id = place.Id;
				button.onClick.AddListener(() => Select(id));
				_tabBack.Add(box.GetComponent<Image>());
				var label = bottom && !string.IsNullOrEmpty(place.Glyph)
					? place.Glyph + "\n" + place.Label
					: place.Label;
				_tabText.Add(_ui.Label("Text", box.transform, label, bottom ? 24 : 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, ShellUi.Muted));
			}
			// Tabs sit in front of the bar's own labels.
			_tabs.transform.SetAsLastSibling();
			if (_profile != null) _profile.Root.transform.SetAsLastSibling();
		}

		/// <summary>Goes to a place: highlights its tab, shows its name, and tells the game. The Profile place opens the shell's own panel.</summary>
		public void Select(string id, bool notify = true)
		{
			CurrentPlace = id;
			for (var i = 0; i < _places.Count; i++)
			{
				var on = _places[i].Id == id;
				_tabBack[i].color = on ? new Color(ShellConfig.Current.Accent.r, ShellConfig.Current.Accent.g, ShellConfig.Current.Accent.b, 0.22f) : Color.clear;
				_tabText[i].color = on ? ShellConfig.Current.Accent : ShellUi.Muted;
				// On a desktop the highlighted tab already says where the player is.
					if (on && BottomTabs) UiBuilder.SetText(_placeTitle, _places[i].Label);
			}

			if (_profile != null)
			{
				_profile.Root.SetActive(id == ProfilePlace);
				if (id == ProfilePlace) _profile.Refresh();
			}
			if (notify) PlaceSelected?.Invoke(id);
		}

		/// <summary>A short line on the right of the top bar: a score, a wave, a lap.</summary>
		public void SetStatus(string text)
		{
			if (_status != null) UiBuilder.SetText(_status, text ?? "");
		}

		/// <summary>The scene's overlay canvas, for a game that wants its HUD on the same canvas settings. The safe-area rect is <see cref="Safe"/>.</summary>
		public Canvas Canvas => _canvas;

		public RectTransform Safe => _safe;

		/// <summary>The top bar, for a game that adds its own small readouts (icons with numbers) to it.</summary>
		public Transform TopBar => _topBar;

		/// <summary>Sets the name shown in the middle of the top bar (a place, a map), whatever the platform.</summary>
		public void SetTitle(string text)
		{
			if (_placeTitle != null) UiBuilder.SetText(_placeTitle, text ?? "");
		}

		void RefreshBar()
		{
			var net = NetSession.Instance;
			var profile = ShellProfile.Current;
			var online = net.IsOnline ? (net.IsHost ? "Host · " : "") + net.Players.Count + " online  " : "";
			UiBuilder.SetText(_player, online + profile.DisplayName);
			_player.color = profile.Tint;
		}

		void Update()
		{
			if (Time.unscaledTime < _refreshAt) return;
			_refreshAt = Time.unscaledTime + 0.5f;
			RefreshBar();
		}
	}
}
