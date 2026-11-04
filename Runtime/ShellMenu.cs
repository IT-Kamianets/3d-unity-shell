using System.Text;
using ITKamianets.Engine.Account;
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
			Sign,
			Onboarding,
			Profile,
			Lobby,
		}

		const int RosterLines = 8;

		UiBuilder _ui;
		Canvas _canvas;
		GameObject _landing;
		GameObject _onboarding;
		GameObject _sign;
		Text _signStatus;
		Text _signTitle;
		Text _signHint;
		Button _signBack;
		Text _accountLine;
		bool _afterSignMultiplayer;
		/// <summary>True while the welcome screen is the first thing the player sees: no Back button, and it ends on the landing screen.</summary>
		bool _welcome;
		/// <summary>Set when a guest could not be created (no connection): the player plays offline this run and is asked again next start.</summary>
		bool _offlineThisRun;
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
			if (config.Accounts) _ = GameAccount.RestoreAsync();

			UiBuilder.EnsureEventSystem(transform);
			_canvas = UiBuilder.CreateCanvas(transform, "MenuCanvas");
			_ui = ShellUi.Builder();
			UiBuilder.Box("Ground", _canvas.transform, Vector2.zero, Vector2.one, config.Background);
			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(_canvas.transform, false);
			var root = safe.transform;

			BuildLanding(root);
			BuildSign(root);
			BuildOnboarding(root);
			_profile = new ShellProfilePanel(_ui, root, "Profile", () => Show(Screen.Landing));
			BuildLobby(root);
			NetSession.Instance.Leave();

			var net = NetSession.Instance;
			net.RosterChanged += OnRoster;
			net.GameStarted += OnGameStarted;
			net.Disconnected += OnDisconnected;

			if (NeedsAccountChoice())
			{
				_welcome = true;
				Show(Screen.Sign);
			}
			else if (SkipsLanding()) Proceed(false);
			else Show(Screen.Landing);
		}

		/// <summary>True on the first start of a game that has its own start scene: no landing screen between sign-in and the game.</summary>
		static bool SkipsLanding() => ShellConfig.Current.SkipLanding && !ShellFlow.GameLoaded;

		/// <summary>The one question every game asks the first time: Google or a guest. Not asked again once answered, or while signed in.</summary>
		static bool NeedsAccountChoice() => ShellConfig.Current.Accounts && !GameAccount.IsSignedIn && !ShellProfile.Current.accountAsked;

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
			if (config.Profile)
			{
			_ui.Button("Profile", _landing.transform, new Vector2(0.36f, top - 0.1f), new Vector2(0.64f, top), () => Show(Screen.Profile));
				top -= step;
			}
			if (!Application.isMobilePlatform)
				_ui.Button("Quit", _landing.transform, new Vector2(0.36f, top - 0.1f), new Vector2(0.64f, top), Quit);

			_accountLine = _ui.Label("Account", _landing.transform, "", 26, TextAnchor.LowerLeft, new Vector2(0.02f, 0.06f), new Vector2(0.4f, 0.11f), ShellUi.Muted);
			_ui.Label("Version", _landing.transform, "v" + config.Version, 26, TextAnchor.LowerLeft, new Vector2(0.02f, 0.01f), new Vector2(0.3f, 0.06f), ShellUi.Muted);
			_updateBanner = _ui.Label("Update", _landing.transform, "", 28, TextAnchor.MiddleRight, new Vector2(0.45f, 0.01f), new Vector2(0.78f, 0.07f), ShellConfig.Current.Accent);
			_updateButton = _ui.Button("Update", _landing.transform, new Vector2(0.8f, 0.012f), new Vector2(0.98f, 0.068f), () => ShellUpdates.Updater.Install());
			_updateButton.GetComponentInChildren<Text>().fontSize = 28;
			_updateButton.gameObject.SetActive(false);
		}

		void OnPlay() => Proceed(false);

		void OnMultiplayer() => Proceed(true);

		// Lords' order: sign in (once), onboarding (once), then the places.
		void Proceed(bool multiplayer)
		{
			var profile = ShellProfile.Current;
			if (!_offlineThisRun && NeedsAccountChoice())
			{
				_afterSignMultiplayer = multiplayer;
				Show(Screen.Sign);
				return;
			}
			AdoptAccount();
			if (!profile.onboarded)
			{
				_afterOnboardingMultiplayer = multiplayer;
				Show(Screen.Onboarding);
				return;
			}
			if (multiplayer) Show(Screen.Lobby);
			else ShellFlow.PlaySolo();
		}

		// ---------------------------------------------------------------- sign in

		void BuildSign(Transform root)
		{
			var config = ShellConfig.Current;
			_sign = UiBuilder.Box("Sign", root, new Vector2(0.28f, 0.14f), new Vector2(0.72f, 0.86f), ShellUi.Panel);
			_signTitle = _ui.Label("Title", _sign.transform, "Welcome", 64, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.97f), config.Accent);
			_signHint = _ui.Label("Hint", _sign.transform, "", 28, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.83f), ShellUi.Muted, true);
			var google = ShellUi.Primary(_ui, "Sign in with Google", _sign.transform, new Vector2(0.12f, 0.5f), new Vector2(0.88f, 0.63f), OnGoogle);
			google.GetComponentInChildren<Text>().fontSize = 32;
			google.gameObject.name = "GoogleButton";
			var guest = _ui.Button("Continue as a guest", _sign.transform, new Vector2(0.12f, 0.34f), new Vector2(0.88f, 0.47f), OnGuest);
			guest.GetComponentInChildren<Text>().fontSize = 32;
			guest.gameObject.name = "GuestButton";
			_signStatus = _ui.Label("Status", _sign.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.32f), ShellUi.Bad, true);
			_signBack = _ui.Button("Back", _sign.transform, new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.14f), () => Show(Screen.Landing));
			_signBack.GetComponentInChildren<Text>().fontSize = 30;
		}

		/// <summary>Fills the sign-in screen for how it was reached: the welcome at first start, or the gate in front of Play.</summary>
		void PrepareSign()
		{
			UiBuilder.SetText(_signTitle, _welcome ? "Welcome" : "Sign in");
			UiBuilder.SetText(_signHint, (_welcome ? ShellConfig.Current.Title + ": sign in" : "Sign in") + " with the same Google account as in Lords, or continue as a guest." + (ShellConfig.Current.Profile ? " You can sign in later from Profile and keep your progress." : ""));
			UiBuilder.SetText(_signStatus, "");
			_signBack.gameObject.SetActive(!_welcome);
		}

		async void OnGoogle()
		{
			if (GameAccount.Busy) return;
			UiBuilder.SetText(_signStatus, GameAccount.GoogleAvailable ? "Finish signing in in your browser…" : "Google sign-in is not set up on this device yet. Play as a guest for now.");
			if (!GameAccount.GoogleAvailable) return;
			var ok = await GameAccount.SignInWithGoogleAsync();
			if (this == null) return;
			if (ok) SignedIn();
			else UiBuilder.SetText(_signStatus, GameAccount.LastError);
		}

		async void OnGuest()
		{
			if (GameAccount.Busy) return;
			UiBuilder.SetText(_signStatus, "Signing in…");
			var ok = await GameAccount.SignInAsGuestAsync();
			if (this == null) return;
			if (ok) SignedIn();
			else
			{
				// No connection is not a reason to stop the player: they play offline and are asked again next time.
				_offlineThisRun = true;
				UiBuilder.SetText(_signStatus, "No connection, so you play offline this time.");
				Continue();
			}
		}

		void SignedIn()
		{
			var profile = ShellProfile.Current;
			profile.accountAsked = true;
			if (string.IsNullOrWhiteSpace(profile.name) && !string.IsNullOrWhiteSpace(GameAccount.DisplayName))
				profile.name = ShellProfile.CleanName(GameAccount.DisplayName);
			profile.Save();
			Continue();
		}

		/// <summary>
		/// A player signed in with Google already has a name (their Google name, and a lord's name in Lords), so the "Who is playing?"
		/// screen is for guests and offline players only. Their profile is filled in from the account and marked as done.
		/// </summary>
		static void AdoptAccount()
		{
			if (!GameAccount.IsSignedIn || GameAccount.IsGuest) return;
			var profile = ShellProfile.Current;
			if (profile.onboarded && !string.IsNullOrWhiteSpace(profile.name)) return;

			var name = ShellProfile.CleanName(GameAccount.DisplayName);
			if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(GameAccount.Email)) name = ShellProfile.CleanName(GameAccount.Email.Split('@')[0]);
			if (string.IsNullOrWhiteSpace(profile.name)) profile.name = string.IsNullOrEmpty(name) ? "Player" : name;
			profile.onboarded = true;
			profile.accountAsked = true;
			profile.Save();
		}

		void Continue()
		{
			AdoptAccount();
			if (_welcome)
			{
				_welcome = false;
				if (SkipsLanding()) Proceed(false);
				else Show(Screen.Landing);
				return;
			}

			var profile = ShellProfile.Current;
			if (!profile.onboarded)
			{
				_afterOnboardingMultiplayer = _afterSignMultiplayer;
				Show(Screen.Onboarding);
			}
			else if (_afterSignMultiplayer) Show(Screen.Lobby);
			else ShellFlow.PlaySolo();
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
			_sign.SetActive(screen == Screen.Sign);
			if (screen == Screen.Sign) PrepareSign();
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

			var active = screen == Screen.Landing ? _landing : screen == Screen.Sign ? _sign : screen == Screen.Onboarding ? _onboarding : screen == Screen.Profile ? _profile.Root : NetSession.Instance.IsOnline ? _lobbyRoom : _lobbyChoose;
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

			UiBuilder.SetText(_accountLine, !ShellConfig.Current.Accounts ? "" : GameAccount.IsSignedIn ? (GameAccount.IsGuest ? "Guest" : "Signed in: " + (string.IsNullOrEmpty(GameAccount.DisplayName) ? GameAccount.Email : GameAccount.DisplayName)) : "Not signed in");
			if (_screen == Screen.Profile) _profile.Refresh();
			if (_screen == Screen.Lobby) RefreshLobby();
		}
	}

	static class NetSessionIds
	{
		public const ulong Host = 0UL;
	}
}
