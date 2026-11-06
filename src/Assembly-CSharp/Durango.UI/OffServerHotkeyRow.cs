using Durango.System.Config;
using UnityEngine;

namespace Durango.UI;

public class OffServerHotkeyRow : MonoBehaviour
{
	private const float RightMargin = 64f;

	private UIWidget _widget;

	private UILabel _label;

	public static void Decorate(ButtonBoxWidget box, Setting setting, UILabel style)
	{
		if (box == null)
		{
			return;
		}
		string text = OffServerHotkeys.CaptionFor(setting);
		OffServerHotkeyRow offServerHotkeyRow = box.GetComponent<OffServerHotkeyRow>();
		if (text == null)
		{
			if (offServerHotkeyRow != null && offServerHotkeyRow._label != null)
			{
				offServerHotkeyRow._label.gameObject.SetActive(value: false);
			}
			return;
		}
		if (offServerHotkeyRow == null)
		{
			offServerHotkeyRow = box.gameObject.AddComponent<OffServerHotkeyRow>();
		}
		offServerHotkeyRow.Show(box.Widget, text, style);
	}

	private void Show(UIWidget widget, string caption, UILabel style)
	{
		_widget = widget;
		if (_label == null)
		{
			_label = base.gameObject.AddWidget<UILabel>();
			_label.name = "OffServerHotkeyKey";
			if (style != null)
			{
				if (style.bitmapFont != null)
				{
					_label.bitmapFont = style.bitmapFont;
				}
				else
				{
					_label.trueTypeFont = style.trueTypeFont;
				}
				_label.fontSize = style.fontSize;
				_label.fontStyle = FontStyle.Bold;
				_label.color = style.color;
				_label.effectStyle = style.effectStyle;
				_label.effectColor = style.effectColor;
				_label.effectDistance = style.effectDistance;
			}
			_label.overflowMethod = UILabel.Overflow.ResizeFreely;
			_label.alignment = NGUIText.Alignment.Right;
			_label.pivot = UIWidget.Pivot.Right;
		}
		_label.gameObject.SetActive(value: true);
		_label.text = caption;
		Place();
	}

	private void LateUpdate()
	{
		if (_label != null && _label.gameObject.activeSelf)
		{
			Place();
		}
	}

	private void Place()
	{
		if (!(_widget == null) && !(_label == null))
		{
			Vector3[] localCorners = _widget.localCorners;
			Vector3 vector = (localCorners[2] + localCorners[3]) * 0.5f;
			Vector3 localPosition = _widget.transform.localPosition;
			if (_label.transform.parent != _widget.transform)
			{
				vector += localPosition;
			}
			vector.x -= 64f;
			vector.z = 0f;
			if (_label.transform.localPosition != vector)
			{
				_label.transform.localPosition = vector;
			}
		}
	}
}
