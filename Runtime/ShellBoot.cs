using System.Collections;
using ITKamianets.Engine.Account;
using ITKamianets.Engine.UI;
using ITKamianets.Engine.Update;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ITKamianets.Engine.Shell
{
	/// <summary>
	/// The Boot scene: the splash (a turning ring on the near-black ground, as in the Lords app) while the game finds out who is
	/// playing and whether it is the newest version. On Windows a newer release is downloaded and installed here, before anything
	/// else, so the player never plays an old build. Then it goes to the Menu scene.
	/// </summary>
	public sealed class ShellBoot : MonoBehaviour
	{
		/// <summary>The splash stays at least this long, so it does not flash.</summary>
		public float MinimumSeconds = 1.2f;
		/// <summary>Seconds to wait for the update check before going on.</summary>
		public float UpdateWaitSeconds = 8f;

		const int Dots = 12;

		Image[] _dots;
		Text _status;
		Image _progress;
		GameObject _progressBack;

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

			UiBuilder.EnsureEventSystem(transform);
			var canvas = UiBuilder.CreateCanvas(transform, "SplashCanvas");
			var ui = ShellUi.Builder();
			UiBuilder.Box("Ground", canvas.transform, Vector2.zero, Vector2.one, config.Background);

			var ring = UiBuilder.Box("Ring", canvas.transform, new Vector2(0.45f, 0.5f), new Vector2(0.55f, 0.7f), Color.clear);
			_dots = new Image[Dots];
			for (var i = 0; i < Dots; i++)
			{
				var angle = i * Mathf.PI * 2f / Dots;
				var centre = new Vector2(0.5f + Mathf.Sin(angle) * 0.36f, 0.5f + Mathf.Cos(angle) * 0.36f);
				var dot = UiBuilder.Box("Dot" + i, ring.transform, centre - new Vector2(0.05f, 0.04f), centre + new Vector2(0.05f, 0.04f), config.Accent);
				_dots[i] = dot.GetComponent<Image>();
			}

			ui.Label("Title", canvas.transform, config.Title, 84, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.5f), config.Accent);
			_status = ui.Label("Status", canvas.transform, "", 32, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.26f), new Vector2(0.9f, 0.34f), ShellUi.Muted);
			_progress = UiBar(canvas.transform, new Vector2(0.35f, 0.22f), new Vector2(0.65f, 0.24f), config.Accent, out _progressBack);
			_progressBack.SetActive(false);
		}

		static Image UiBar(Transform parent, Vector2 min, Vector2 max, Color colour, out GameObject back)
		{
			var fill = UiBuilder.Bar("Progress", parent, min, max, colour);
			back = fill.transform.parent.gameObject;
			UiBuilder.SetFill(fill, 0f);
			return fill;
		}

		IEnumerator Start()
		{
			var profile = ShellFlow.Begin();
			var started = Time.unscaledTime;
			SetStatus("");

			// Who was playing last time, in the background: the splash does not wait for the network for this.
			if (ShellConfig.Current.Accounts) _ = GameAccount.RestoreAsync();

			var updater = ShellUpdates.Updater;
			if (profile.autoUpdate && !string.IsNullOrEmpty(ShellConfig.Current.Repo))
			{
				SetStatus("Checking for updates…");
				ShellUpdates.CheckIfPossible();
				var give = Time.unscaledTime + UpdateWaitSeconds;
				while (updater.State == UpdateState.Checking && Time.unscaledTime < give) yield return null;

				if (updater.State == UpdateState.Available && ShellUpdates.CanSelfUpdate)
				{
					SetStatus("Updating to " + (updater.Latest != null ? updater.Latest.Version : "the newest version") + "…");
					_progressBack.SetActive(true);
					updater.Install();
					while (updater.State == UpdateState.Downloading || updater.State == UpdateState.Installing)
					{
						UiBuilder.SetFill(_progress, updater.Progress);
						yield return null;
					}
					// Reaching here means the download failed; play the version that is installed.
					_progressBack.SetActive(false);
				}
			}

			SetStatus("");
			while (Time.unscaledTime - started < MinimumSeconds) yield return null;
			ShellFlow.Booted = true;
			SceneManager.LoadScene(ShellConfig.Current.MenuScene);
		}

		void SetStatus(string text)
		{
			if (_status != null) UiBuilder.SetText(_status, text);
		}

		void Update()
		{
			if (_dots == null) return;
			var head = Time.unscaledTime * 10f;
			for (var i = 0; i < _dots.Length; i++)
			{
				var behind = Mathf.Repeat(head - i, _dots.Length) / _dots.Length;
				var colour = ShellConfig.Current.Accent;
				colour.a = Mathf.Lerp(1f, 0.12f, behind);
				_dots[i].color = colour;
			}
		}
	}
}
