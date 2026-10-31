using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ITKamianets.Engine.Shell
{
	/// <summary>
	/// An on-screen button for phones and tablets that a game reads like a key: <see cref="Held"/> while a finger is on it, and
	/// <see cref="ConsumePress"/> once per tap. Games build a few of them (steer, boost, jump) only when <see cref="IsNeeded"/>.
	/// </summary>
	public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
	{
		public bool Held { get; private set; }

		bool _pressed;
		Image _image;
		Color _idle;

		/// <summary>True on phones and tablets, where there is no keyboard.</summary>
		public static bool IsNeeded => Application.isMobilePlatform;

		/// <summary>Makes a round-cornered translucent button with a label between two anchors.</summary>
		public static TouchButton Create(UiBuilder ui, Transform parent, string label, Vector2 min, Vector2 max, int fontSize = 40)
		{
			var box = UiBuilder.Box("Touch " + label, parent, min, max, new Color(1f, 1f, 1f, 0.18f));
			ui.Label("Text", box.transform, label, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.9f));
			var button = box.AddComponent<TouchButton>();
			button._image = box.GetComponent<Image>();
			button._idle = button._image.color;
			return button;
		}

		/// <summary>True once for each new press, then false until the next one.</summary>
		public bool ConsumePress()
		{
			var was = _pressed;
			_pressed = false;
			return was;
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			Held = true;
			_pressed = true;
			if (_image != null) _image.color = new Color(1f, 1f, 1f, 0.4f);
		}

		public void OnPointerUp(PointerEventData eventData) => Release();

		public void OnPointerExit(PointerEventData eventData) => Release();

		void Release()
		{
			Held = false;
			if (_image != null) _image.color = _idle;
		}

		void OnDisable()
		{
			Held = false;
			_pressed = false;
		}
	}
}
