using System;
using ITKamianets.Engine.Account;
using ITKamianets.Engine.UI;
using ITKamianets.Engine.Update;
using UnityEngine;
using UnityEngine.UI;

namespace ITKamianets.Engine.Shell
{
	/// <summary>
	/// The profile screen: name, colour, keep-up-to-date switch, the version and a way to look for an update. Used by the
	/// landing screen and by the in-game bar, so a player edits who they are in the same way everywhere.
	/// </summary>
	public sealed class ShellProfilePanel
	{
		public GameObject Root { get; }

		readonly UiBuilder _ui;
		readonly Text _updateLine;
		readonly Text _autoLabel;
		Text _accountLabel;
		Button _updateButton;
		Action<int> _showColour;
		InputField _name;

		public ShellProfilePanel(UiBuilder ui, Transform parent, string title, Action onBack, string backLabel = "Back", Action onLeave = null, string leaveLabel = "Main menu")
		{
			_ui = ui;
			var config = ShellConfig.Current;
			Root = UiBuilder.Box("Profile", parent, new Vector2(0.28f, 0.1f), new Vector2(0.72f, 0.9f), ShellUi.Panel);

			ui.Label("Title", Root.transform, title, 64, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), config.Accent);
			ui.Label("NameLabel", Root.transform, "Name", 32, TextAnchor.LowerLeft, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.84f), ShellUi.Muted);
			_name = ShellUi.Input(ui, "Name", Root.transform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.78f), ShellProfile.Current.name, "Your name", 16, value =>
			{
				ShellProfile.Current.name = ShellProfile.CleanName(value);
				ShellProfile.Current.Save();
			});

			ui.Label("ColourLabel", Root.transform, "Colour", 32, TextAnchor.LowerLeft, new Vector2(0.08f, 0.60f), new Vector2(0.92f, 0.66f), ShellUi.Muted);
			_showColour = ShellUi.Swatches(Root.transform, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.60f), config.PlayerColours, ShellProfile.Current.colour, index =>
			{
				ShellProfile.Current.colour = index;
				ShellProfile.Current.Save();
				_showColour(index);
			});

			Button auto = null;
			auto = ui.Button("", Root.transform, new Vector2(0.08f, 0.37f), new Vector2(0.48f, 0.47f), () =>
			{
				ShellProfile.Current.autoUpdate = !ShellProfile.Current.autoUpdate;
				ShellProfile.Current.Save();
				Refresh();
			});
			_autoLabel = auto.GetComponentInChildren<Text>();
			_autoLabel.fontSize = 24;

			if (ShellConfig.Current.Accounts)
			{
				var account = ui.Button("", Root.transform, new Vector2(0.52f, 0.37f), new Vector2(0.92f, 0.47f), OnAccountButton);
				_accountLabel = account.GetComponentInChildren<Text>();
				_accountLabel.fontSize = 24;
			}

			_updateButton = ui.Button("Check for updates", Root.transform, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.35f), OnUpdateButton);
			_updateButton.GetComponentInChildren<Text>().fontSize = 30;
			_updateLine = ui.Label("UpdateLine", Root.transform, "", 28, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.24f), ShellUi.Muted);

			if (onLeave != null)
			{
				ui.Button(leaveLabel, Root.transform, new Vector2(0.08f, 0.06f), new Vector2(0.48f, 0.16f), onLeave).GetComponentInChildren<Text>().fontSize = 30;
				ShellUi.Primary(ui, backLabel, Root.transform, new Vector2(0.52f, 0.06f), new Vector2(0.92f, 0.16f), onBack).GetComponentInChildren<Text>().fontSize = 30;
			}
			else
			{
				ShellUi.Primary(ui, backLabel, Root.transform, new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.16f), onBack).GetComponentInChildren<Text>().fontSize = 30;
			}

			Refresh();
		}

		public void Refresh()
		{
			if (_name != null && !_name.isFocused) _name.SetTextWithoutNotify(ShellProfile.Current.name);
			_showColour?.Invoke(ShellProfile.Current.colour);
			UiBuilder.SetText(_autoLabel, "Update by itself: " + (ShellProfile.Current.autoUpdate ? "On" : "Off"));

			if (_accountLabel != null)
			{
				UiBuilder.SetText(_accountLabel, !GameAccount.IsSignedIn ? "Sign in as guest" : GameAccount.IsGuest ? (GameAccount.GoogleAvailable ? "Sign in with Google" : "Guest account") : "Sign out");
			}

			var updater = ShellUpdates.Updater;
			var version = "Version " + ShellConfig.Current.Version;
			switch (updater.State)
			{
				case UpdateState.Checking:
					UiBuilder.SetText(_updateLine, version + "  ·  checking…");
					break;
				case UpdateState.Available:
					UiBuilder.SetText(_updateLine, version + "  ·  " + (updater.Latest != null ? updater.Latest.Version : "newer") + " is available");
					UiBuilder.SetText(_updateButton.GetComponentInChildren<Text>(), "Update now");
					break;
				case UpdateState.Downloading:
					UiBuilder.SetText(_updateLine, version + "  ·  downloading " + Mathf.RoundToInt(updater.Progress * 100f) + "%");
					break;
				case UpdateState.UpToDate:
					UiBuilder.SetText(_updateLine, version + "  ·  up to date");
					UiBuilder.SetText(_updateButton.GetComponentInChildren<Text>(), "Check for updates");
					break;
				case UpdateState.Failed:
					UiBuilder.SetText(_updateLine, version + "  ·  could not check (offline?)");
					UiBuilder.SetText(_updateButton.GetComponentInChildren<Text>(), "Check for updates");
					break;
				default:
					UiBuilder.SetText(_updateLine, version);
					break;
			}
		}

		async void OnAccountButton()
		{
			if (!GameAccount.IsSignedIn) await GameAccount.SignInAsGuestAsync();
			else if (GameAccount.IsGuest) { if (GameAccount.GoogleAvailable) await GameAccount.SignInWithGoogleAsync(); }
			else GameAccount.SignOut();
			if (ShellProfile.Current != null && GameAccount.IsSignedIn && string.IsNullOrWhiteSpace(ShellProfile.Current.name) && !string.IsNullOrWhiteSpace(GameAccount.DisplayName))
			{
				ShellProfile.Current.name = ShellProfile.CleanName(GameAccount.DisplayName);
				ShellProfile.Current.Save();
			}
			Refresh();
		}

		void OnUpdateButton()
		{
			var updater = ShellUpdates.Updater;
			if (updater.State == UpdateState.Available) updater.Install();
			else if (string.IsNullOrEmpty(ShellConfig.Current.Repo)) UiBuilder.SetText(_updateLine, "Updates are not set up for this build.");
			else updater.Check();
			Refresh();
		}
	}
}
