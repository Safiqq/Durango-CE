using System.Collections.Generic;
using UnityEngine;

public class OffServerEmojiLinks
{
	private class Link : ITextRectLayout
	{
		public UILabel Parent;

		public UITexture Widget;

		public bool Valid;

		void ITextRectLayout.Set(Vector2 pos, Vector2 size, Color color)
		{
			Valid = true;
			pos += Parent.FontOffset;
			int num = Mathf.Max(2, Mathf.RoundToInt(size.x));
			int num2 = Mathf.Max(2, Mathf.RoundToInt(size.y));
			if (Widget.width != num || Widget.height != num2)
			{
				Widget.SetDimensions(num, num2);
			}
			Vector2 pivotOffset = Widget.pivotOffset;
			Vector3 vector = new Vector3(Mathf.Round(pos.x + size.x * pivotOffset.x), Mathf.Round(pos.y + size.y * pivotOffset.y));
			Transform cachedTransform = Widget.cachedTransform;
			if (cachedTransform.localPosition != vector)
			{
				cachedTransform.localPosition = vector;
			}
			if (cachedTransform.localScale != Vector3.one)
			{
				cachedTransform.localScale = Vector3.one;
			}
			Color color2 = new Color(1f, 1f, 1f, color.a);
			if (Widget.color != color2)
			{
				Widget.color = color2;
			}
		}
	}

	public const int DepthOffset = 1;

	private readonly List<Link> _links = new List<Link>();

	private int _count;

	public int Count => _count;

	public void BeginParse()
	{
		_count = 0;
	}

	public static void DestroyOrphans(UILabel owner, OffServerEmojiLinks tracked)
	{
		if (!Application.isPlaying)
		{
			return;
		}
		Transform cachedTransform = owner.cachedTransform;
		for (int num = cachedTransform.childCount - 1; num >= 0; num--)
		{
			Transform child = cachedTransform.GetChild(num);
			if (!(child.name != "emoji"))
			{
				UITexture component = child.GetComponent<UITexture>();
				if (!(component == null) && (tracked == null || !tracked.Owns(component)))
				{
					component.enabled = false;
					Object.Destroy(child.gameObject);
				}
			}
		}
	}

	private bool Owns(UITexture widget)
	{
		for (int i = 0; i < _links.Count; i++)
		{
			if (_links[i].Widget == widget)
			{
				return true;
			}
		}
		return false;
	}

	public ITextRectLayout Get(UILabel owner, int cell)
	{
		Texture2D texture = OffServerEmoji.Texture;
		if (texture == null)
		{
			return null;
		}
		Link link;
		if (_count < _links.Count)
		{
			link = _links[_count];
		}
		else
		{
			link = new Link();
			_links.Add(link);
		}
		_count++;
		if (link.Widget == null)
		{
			UITexture uITexture = owner.gameObject.AddChild<UITexture>();
			uITexture.name = "emoji";
			uITexture.gameObject.hideFlags = HideFlags.DontSave;
			uITexture.pivot = UIWidget.Pivot.BottomLeft;
			uITexture.mainTexture = texture;
			link.Parent = owner;
			link.Widget = uITexture;
		}
		link.Widget.depth = owner.depth + 1;
		link.Widget.uvRect = OffServerEmoji.GetUV(cell);
		link.Valid = false;
		return link;
	}

	public void EndParse()
	{
		for (int i = _count; i < _links.Count; i++)
		{
			UITexture widget = _links[i].Widget;
			if (widget != null)
			{
				Object.Destroy(widget.gameObject);
			}
		}
		if (_count < _links.Count)
		{
			_links.RemoveRange(_count, _links.Count - _count);
		}
	}

	public void BeginLayout()
	{
		for (int i = 0; i < _links.Count; i++)
		{
			_links[i].Valid = false;
		}
	}

	public void EndLayout()
	{
		for (int i = 0; i < _links.Count; i++)
		{
			if (_links[i].Widget != null)
			{
				_links[i].Widget.visible = _links[i].Valid;
			}
		}
	}

	public void SetDepth(int depth)
	{
		for (int i = 0; i < _links.Count; i++)
		{
			if (_links[i].Widget != null)
			{
				_links[i].Widget.depth = depth + 1;
			}
		}
	}
}
