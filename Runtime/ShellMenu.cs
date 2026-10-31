using System.Text;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.UI;
using ITKamianets.Engine.Update;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ITKamianets.Engine.Shell
{
	/// <summary>
	/// The Menu scene: the screens before the game, in the order of the Lords app. Landing (title, play, multiplayer, profile), then
	/// onboarding the first time (a name and a colour), the profile, and the lobby for playing with others.
	/// </summary>
	public sealed class ShellMenu : MonoBehaviour
	{
		enum Screen
		{
			Landing,
			Onboarding,
			Profile,
			Lobby,
		}

		const int RosterLines = 8;

		UiBuilder _ui;
		Canvas _canvas;
		GameObject _landing;
		GameObject _onboarding;
		GameObject _lobbyChoose;
		GameObject _lobbyRoom;
		ShellProfilePanel _profile;
		Screen _screen = Screen.Landing;
		bool _afterOnboardingMultiplayer;

		Text _updateBanner;
		Button _updateButton;
		Text _lobbyStatus;
		Text _roomCode;
		Text _roomStatus;
		Text[] _roster;
		Button _readyButton;
		Button _startButton;
		InputField _joinField;
		float _refreshAt;

		void Awake()
		{
			Time.timeScale = 1f;
			var config = ShellConfig.Current;
			var camera = Camera.main;
			if (camera == null)
			{
				var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
				camera = cameraObject.GetComponent<Camera>();
			}
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = config.Background;

			ShellFlow.Begin();
			if (!ShellFlow.Booted) ShellUpdates.CheckIfPossible();

			UiBuilder.EnsureEventSystem(transform);
			_canvas = UiBuilder.CreateCanvas(transform, "MenuCanvas");
			_ui = ShellUi.Builder();
			UiBuilder.Box("Ground", _canvas.transform, Vector2.zero, Vector2.one, config.Background);
			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(_canvas.transform, false);
			var root = safe.transform;

			BuildLanding(root);
			BuildOnboarding(root);
			_profile = new ShellProfilePanel(_ui, root, "Profile", () => Show(Screen.Landing));
			BuildLobby(root);
			NetSession.Instance.Leave();

			var net = NetSession.Instance;
			net.RosterChanged += OnRoster;
			net.GameStarted += OnGameStarted;
			net.Disconnected += OnDisconnected;

			Show(Screen.Landing);
		}

		void OnDestroy()
		{
			var net = NetSession.Instance;
			if (net == null) return;
			net.RosterChanged -= OnRoster;
			net.GameStarted -= OnGameStarted;
			net.Disconnected -= OnDisconnected;
		}

		// ---------------------------------------------------------------- landing

		void BuildLanding(Transform root)
		{
			var config = ShellConfig.Current;
			_landing = UiBuilder.Box("Landing", root, Vector2.zero, Vector2.one, Color.clear);
			_ui.Label("Title", _landing.transform, config.Title, 100, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.92f), config.Accent);
			_ui.Label("Tagline", _landing.transform, config.Tagline, 36, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.67f), new Vector2(0.9f, 0.74f), ShellUi.Muted, true);

			var top = 0.58f;
			const float step = 0.12f;
			ShellUi.Primary(_ui, "Play", _landing.transform, new Vector2(0.36f, top - 0.1f), new Vector2(0.64f, top), OnPlay);
			top -= step;
			if (config.Multiplayer)
			{
				_ui.Button("Play with others", _landing.transform, new Vector2(0.36f, top - 0.1f), new Vector2(0.64f, top), OnMultiplayer);
				top -= step;
			}
			_ui.Button("Profile", _landing.transform, new Vector2(0.36f, top - 0.1f), new Vector2(0.64f, top), () => Show(Screen.Profile));
			top -= step;
			if (!Application.isMobilePlatform)
				_ui.Button("Quit", _landing.transform, new Vector2(0.36f, top - 0.1f), new Vector2(0.64f, top), Quit);

			_ui.Label("Version", _landing.transform, "v" + config.Version, 26, TextAnchor.LowerLeft, new Vector2(0.02f, 0.01f), new Vector2(0.3f, 0.06f), ShellUi.Muted);
			_updateBanner = _ui.Label("Update", _landing.transform, "", 28, TextAnchor.MiddleRight, new Vector2(0.45f, 0.01f), new Vector2(0.78f, 0.07f), ShellConfig.Current.Accent);
			_updateButton = _ui.Button("Update", _landing.transform, new Vector2(0.8f, 0.012f), new Vector2(0.98f, 0.068f), () => ShellUpdates.Updater.Install());
			_updateButton.GetComponentInChildren<Text>().fontSize = 28;
			_updateButton.gameObject.SetActive(false);
		}

		void OnPlay()
		{
			if (!ShellProfile.Current.onboarded)
			{
				_afterOnboardingMultiplayer = false;
				Show(Screen.Onboarding);
				return;
			}
			ShellFlow.PlaySolo();
		}

		void OnMultiplayer()
		{
			if (!ShellProfile.Current.onboarded)
			{
				_afterOnboardingMultiplayer = true;
				Show(Screen.Onboarding);
				return;
			}
			Show(Screen.Lobby);
		}

		static void Quit()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		// ---------------------------------------------------------------- onboarding

		void BuildOnboarding(Transform root)
		{
			var config = ShellConfig.Current;
			_onboarding = UiBuilder.Box("Onboarding", root, new Vector2(0.28f, 0.14f), new Vector2(0.72f, 0.86f), ShellUi.Panel);
			_ui.Label("Title", _onboarding.transform, "Who is playing?", 64, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.97f), config.Accent);
			_ui.Label("Hint", _onboarding.transform, "Pick a name and a colour. You can change them later in Profile.", 28, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.83f), ShellUi.Muted, true);

			var profile = ShellProfile.Current;
			if (string.IsNullOrWhiteSpace(profile.name)) profile.name = "";
			var field = ShellUi.Input(_ui, "Name", _onboarding.transform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.7f), profile.name, "Your name", 16, value => ShellProfile.Current.name = ShellProfile.CleanName(value));
			var show = (System.Action<int>)null;
			show = ShellUi.Swatches(_onboarding.transform, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.54f), config.PlayerColours, profile.colour, index =>
			{
				ShellProfile.Current.colour = index;
				show(index);
			});
			ShellUi.Primary(_ui, "Continue", _onboarding.transform, new Vector2(0.25f, 0.2f), new Vector2(0.75f, 0.32f), () =>
			{
				var current = ShellProfile.Current;
				current.name = ShellProfile.CleanName(field.text);
				if (string.IsNullOrEmpty(current.name)) current.name = "Player";
				current.onboarded = true;
				current.Save();
				if (_afterOnboardingMultiplayer) Show(Screen.Lobby);
				else ShellFlow.PlaySolo();
			});
			_ui.Button("Back", _onboarding.transform, new Vector2(0.35f, 0.06f), new Vector2(0.65f, 0.16f), () => Show(Screen.Landing)).GetComponentInChildren<Text>().fontSize = 30;
		}

		// ---------------------------------------------------------------- lobby

		void BuildLobby(Transform root)
		{
			var config = ShellConfig.Current;

			_lobbyChoose = UiBuilder.Box("LobbyChoose", root, new Vector2(0.28f, 0.1f), new Vector2(0.72f, 0.9f), ShellUi.Panel);
			_ui.Label("Title", _lobbyChoose.transform, "Play with others", 64, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), config.Accent);
			_ui.Button("Host a game here", _lobbyChoose.transform, new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.84f), OnHostDirect).GetComponentInChildren<Text>().fontSize = 30;
			_ui.Label("HostHint", _lobbyChoose.transform, "Friends on the same network join with the address it shows.", 24, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.72f), ShellUi.Muted, true);
			_ui.Button("Host online (a short code)", _lobbyChoose.transform, new Vector2(0.1f, 0.52f), new Vector2(0.9f, 0.64f), OnHostOnline).GetComponentInChildren<Text>().fontSize = 30;
			_joinField = ShellUi.Input(_ui, "Join", _lobbyChoose.transform, new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.46f), "", "Address or code of a game", 40);
			_ui.Button("Join", _lobbyChoose.transform, new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.34f), OnJoin).GetComponentInChildren<Text>().fontSize = 30;
			_lobbyStatus = _ui.Label("Status", _lobbyChoose.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.23f), ShellUi.Bad, true);
			_ui.Button("Back", _lobbyChoose.transform, new Vector2(0.3f, 0.03f), new Vector2(0.7f, 0.13f), () => Show(Screen.Landing)).GetComponentInChildren<Text>().fontSize = 30;

			_lobbyRoom = UiBuilder.Box("LobbyRoom", root, new Vector2(0.28f, 0.06f), new Vector2(0.72f, 0.94f), ShellUi.Panel);
			_ui.Label("Title", _lobbyRoom.transform, "Game room", 60, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.99f), config.Accent);
			_roomCode = _ui.Label("Code", _lobbyRoom.transform, "", 40, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.81f), new Vector2(0.95f, 0.9f), Color.white);
			_roster = new Text[RosterLines];
			for (var i = 0; i < RosterLines; i++)
			{
				var top = 0.79f - i * 0.065f;
				_roster[i] = _ui.Label("Player" + i, _lobbyRoom.transform, "", 32, TextAnchor.MiddleLeft, new Vector2(0.12f, top - 0.06f), new Vector2(0.88f, top), Color.white);
			}
			_roomStatus = _ui.Label("Status", _lobbyRoom.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.205f), new Vector2(0.95f, 0.27f), ShellUi.Muted, true);
			_readyButton = _ui.Button("Ready", _lobbyRoom.transform, new Vector2(0.08f, 0.11f), new Vector2(0.48f, 0.2f), OnReady);
			_readyButton.GetComponentInChildren<Text>().fontSize = 30;
			_startButton = ShellUi.Primary(_ui, "Start", _lobbyRoom.transform, new Vector2(0.52f, 0.11f), new Vector2(0.92f, 0.2f), OnStart);
			_startButton.GetComponentInChildren<Text>().fontSize = 30;
			_ui.Button("Leave", _lobbyRoom.transform, new Vector2(0.3f, 0.01f), new Vector2(0.7f, 0.1f), OnLeave).GetComponentInChildren<Text>().fontSize = 30;
		}

		void OnHostDirect()
		{
			ShellProfile.Current.ApplyToNet();
			var net = NetSession.Instance;
			if (net.HostDirect()) Show(Screen.Lobby);
			else UiBuilder.SetText(_lobbyStatus, net.LastError);
		}

		async void OnHostOnline()
		{
			ShellProfile.Current.ApplyToNet();
			UiBuilder.SetText(_lobbyStatus, "Opening an online game…");
			var ok = await NetSession.Instance.HostOnlineAsync();
			if (this == null) return;
			if (ok) Show(Screen.Lobby);
			else UiBuilder.SetText(_lobbyStatus, NetSession.Instance.LastError);
		}

		async void OnJoin()
		{
			ShellProfile.Current.ApplyToNet();
			var net = NetSession.Instance;
			var text = _joinField.text.Trim();
			if (text.Length == 0)
			{
				UiBuilder.SetText(_lobbyStatus, "Type the address or code first.");
				return;
			}

			UiBuilder.SetText(_lobbyStatus, "Connecting…");
			// An address has a dot or a colon in it; an online code is letters and digits only.
			var direct = text.IndexOf('.') >= 0 || text.IndexOf(':') >= 0;
			var ok = direct ? net.JoinDirect(text) : await net.JoinOnlineAsync(text);
			if (this == null) return;
			if (ok) Show(Screen.Lobby);
			else UiBuilder.SetText(_lobbyStatus, net.LastError);
		}

		void OnReady()
		{
			var net = NetSession.Instance;
			var me = net.Local;
			net.SetReady(me == null || !me.Ready);
		}

		void OnStart()
		{
			var net = NetSession.Instance;
			if (net.IsHost && net.Players.Count >= 2 && net.AllReady) net.StartGame(ShellConfig.Current.GameScene);
		}

		void OnLeave()
		{
			NetSession.Instance.Leave();
			Show(Screen.Lobby);
		}

		void OnRoster()
		{
			if (_screen == Screen.Lobby) RefreshLobby();
		}

		void OnGameStarted(string scene)
		{
			Time.timeScale = 1f;
			SceneManager.LoadScene(string.IsNullOrEmpty(scene) ? ShellConfig.Current.GameScene : scene);
		}

		void OnDisconnected(string reason)
		{
			UiBuilder.SetText(_lobbyStatus, reason);
			if (_screen == Screen.Lobby) RefreshLobby();
		}

		void RefreshLobby()
		{
			var net = NetSession.Instance;
			var inRoom = net.IsOnline;
			_lobbyChoose.SetActive(!inRoom);
			_lobbyRoom.SetActive(inRoom);
			if (!inRoom) return;

			var join = net.ViaRelay ? "Code: " + net.JoinCode : "Address: " + net.JoinCode;
			UiBuilder.SetText(_roomCode, net.IsHost ? join : net.Connecting ? "Connecting…" : "Joined " + net.JoinCode);
			for (var i = 0; i < _roster.Length; i++)
			{
				if (i < net.Players.Count)
				{
					var player = net.Players[i];
					var line = new StringBuilder();
					line.Append(player.Id == NetSessionIds.Host ? "* " : "   ").Append(player.Name);
					if (player.Id == net.LocalId) line.Append("  (you)");
					_roster[i].text = line.ToString();
					_roster[i].color = player.Ready || player.Id == NetSessionIds.Host ? ShellConfig.Current.ColourOf(player.Colour) : Color.Lerp(ShellConfig.Current.ColourOf(player.Colour), ShellUi.Muted, 0.55f);
				}
				else _roster[i].text = "";
			}

			var me = net.Local;
			UiBuilder.SetText(_readyButton.GetComponentInChildren<Text>(), me != null && me.Ready ? "Not ready" : "Ready");
			_readyButton.gameObject.SetActive(!net.IsHost);
			_startButton.gameObject.SetActive(net.IsHost);
			_startButton.interactable = net.IsHost && net.Players.Count >= 2 && net.AllReady;
			UiBuilder.SetText(_roomStatus,
				net.IsHost
					? net.Players.Count < 2 ? "Waiting for someone to join." : net.AllReady ? "Everybody is ready." : "Waiting for everybody to get ready."
					: net.Connecting ? "" : "Waiting for the host to start.");
		}

		// ---------------------------------------------------------------- screens

		void Show(Screen screen)
		{
			_screen = screen;
			_landing.SetActive(screen == Screen.Landing);
			_onboarding.SetActive(screen == Screen.Onboarding);
			_profile.Root.SetActive(screen == Screen.Profile);
			_lobbyChoose.SetActive(false);
			_lobbyRoom.SetActive(false);
			if (screen == Screen.Lobby)
			{
				UiBuilder.SetText(_lobbyStatus, "");
				RefreshLobby();
			}
			else if (screen == Screen.Profile) _profile.Refresh();

			var active = screen == Screen.Landing ? _landing : screen == Screen.Onboarding ? _onboarding : screen == Screen.Profile ? _profile.Root : NetSession.Instance.IsOnline ? _lobbyRoom : _lobbyChoose;
			UiBuilder.Select(active);
		}

		void Update()
		{
			if (Time.unscaledTime < _refreshAt) return;
			_refreshAt = Time.unscaledTime + 0.25f;

			var updater = ShellUpdates.Updater;
			var showUpdate = updater.State == UpdateState.Available || updater.State == UpdateState.Downloading;
			if (_updateButton.gameObject.activeSelf != showUpdate) _updateButton.gameObject.SetActive(showUpdate);
			UiBuilder.SetText(_updateBanner, updater.State == UpdateState.Available && updater.Latest != null
				? updater.Latest.Version + " is available"
				: updater.State == UpdateState.Downloading ? "Downloading " + Mathf.RoundToInt(updater.Progress * 100f) + "%" : "");

			if (_screen == Screen.Profile) _profile.Refresh();
			if (_screen == Screen.Lobby) RefreshLobby();
		}
	}

	static class NetSessionIds
	{
		public const ulong Host = 0UL;
	}
}
