using System;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ITKamianets.Engine.Shell
{
	/// <summary>The look shared by every shell screen (dark ground, gold accent, like the Lords app) and the few controls UiBuilder does not make.</summary>
	public static class ShellUi
	{
		public static readonly Color Panel = new Color(0.11f, 0.105f, 0.105f, 0.96f);
		public static readonly Color PanelLight = new Color(0.17f, 0.16f, 0.15f, 1f);
		public static readonly Color Text = new Color(0.95f, 0.93f, 0.88f);
		public static readonly Color Muted = new Color(0.68f, 0.65f, 0.6f);
		public static readonly Color Good = new Color(0.4f, 0.8f, 0.45f);
		public static readonly Color Bad = new Color(0.9f, 0.4f, 0.35f);

		public static UiBuilder Builder()
		{
			var accent = ShellConfig.Current.Accent;
			return new UiBuilder
			{
				ButtonColour = PanelLight,
				ButtonHighlight = Color.Lerp(accent, Color.white, 0.5f),
				ButtonSelected = accent,
				ButtonPressed = Color.Lerp(accent, Color.black, 0.3f),
				ButtonFontSize = 34,
			};
		}

		/// <summary>A button tinted with the game's accent colour, for the one thing a screen wants the player to press.</summary>
		public static Button Primary(UiBuilder ui, string text, Transform parent, Vector2 min, Vector2 max, Action onClick)
		{
			var button = ui.Button(text, parent, min, max, onClick);
			button.GetComponent<Image>().color = Color.Lerp(ShellConfig.Current.Accent, Color.black, 0.45f);
			return button;
		}

		/// <summary>A one-line text field. Returns the field; <paramref name="onChanged"/> fires on every edit.</summary>
		public static InputField Input(UiBuilder ui, string name, Transform parent, Vector2 min, Vector2 max, string initial, string placeholder, int maxLength, Action<string> onChanged = null)
		{
			var box = UiBuilder.Box(name, parent, min, max, new Color(0.05f, 0.05f, 0.05f, 1f));
			var field = box.AddComponent<InputField>();
			var text = ui.Label("Text", box.transform, "", 34, TextAnchor.MiddleLeft, new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), Color.white);
			var hint = ui.Label("Placeholder", box.transform, placeholder, 34, TextAnchor.MiddleLeft, new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), new Color(1f, 1f, 1f, 0.35f));
			text.supportRichText = false;
			field.textComponent = text;
			field.placeholder = hint;
			field.characterLimit = maxLength;
			field.lineType = InputField.LineType.SingleLine;
			field.text = initial ?? "";
			if (onChanged != null) field.onValueChanged.AddListener(value => onChanged(value));
			text.raycastTarget = false;
			return field;
		}

		/// <summary>A row of colour swatches; the picked one gets a bright frame. Returns a function that moves the frame.</summary>
		public static Action<int> Swatches(Transform parent, Vector2 min, Vector2 max, System.Collections.Generic.IList<Color> colours, int selected, Action<int> onPick)
		{
			var row = UiBuilder.Box("Swatches", parent, min, max, Color.clear);
			var frames = new Image[colours.Count];
			var width = 1f / colours.Count;
			for (var i = 0; i < colours.Count; i++)
			{
				var index = i;
				var frame = UiBuilder.Box("Frame" + i, row.transform, new Vector2(i * width + 0.01f, 0.05f), new Vector2((i + 1) * width - 0.01f, 0.95f), new Color(1f, 1f, 1f, 0f));
				frames[i] = frame.GetComponent<Image>();
				var swatch = UiBuilder.Box("Swatch", frame.transform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), colours[i]);
				var button = frame.AddComponent<Button>();
				button.targetGraphic = frames[i];
				button.onClick.AddListener(() => onPick(index));
				swatch.GetComponent<Image>().raycastTarget = false;
			}

			void Show(int pick)
			{
				for (var i = 0; i < frames.Length; i++) frames[i].color = i == pick ? Color.white : new Color(1f, 1f, 1f, 0f);
			}

			Show(selected);
			return Show;
		}
	}

	/// <summary>Keeps a screen's content inside the safe area of a phone: notches, rounded corners and the home bar.</summary>
	[ExecuteAlways]
	public sealed class SafeAreaFitter : MonoBehaviour
	{
		Rect _last;
		Vector2Int _size;

		void Update()
		{
			var area = Screen.safeArea;
			var size = new Vector2Int(Screen.width, Screen.height);
			if (area == _last && size == _size) return;
			_last = area;
			_size = size;
			if (size.x <= 0 || size.y <= 0) return;

			var rect = (RectTransform)transform;
			rect.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
			rect.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}
	}
}
