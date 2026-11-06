using System;
using System.Reflection;
using Durango.UI;
using Durango.UI.Control;
using UnityEngine;
using Yaml;

namespace OffServer.Rift;

public sealed class RiftScopeCard : MonoBehaviour
{
	[SerializeField]
	private UILabel _title;

	[SerializeField]
	private UILabel _info;

	[SerializeField]
	private UILabel _note;

	[SerializeField]
	private UISprite _portrait;

	private ScopeAnimal _species;

	private Action<int> _select;

	public static RiftScopeCard Adopt(EncyclopediaCropWidget source, UISprite portraitTemplate)
	{
		RiftScopeCard riftScopeCard = source.gameObject.AddComponent<RiftScopeCard>();
		riftScopeCard._title = Field<UILabel>(source, "_titleLabel");
		riftScopeCard._info = Field<UILabel>(source, "_infoLabel");
		riftScopeCard._note = Field<UILabel>(source, "_masteryLabel");
		UISprite uISprite = Field<UISprite>(source, "_progressSprite");
		if (uISprite != null)
		{
			uISprite.gameObject.SetActive(value: false);
		}
		Field<ListObjectPool>(source, "_masteryList")?.Clear();
		ItemIconTex itemIconTex = Field<ItemIconTex>(source, "_iconTexture");
		Transform parent = itemIconTex.transform.parent;
		GameObject gameObject = UnityEngine.Object.Instantiate(portraitTemplate.gameObject, itemIconTex.transform.parent, worldPositionStays: false);
		gameObject.name = "AnimalPortrait";
		riftScopeCard._portrait = gameObject.GetComponent<UISprite>();
		riftScopeCard._portrait.SetAnchor((Transform)null);
		gameObject.transform.localPosition = itemIconTex.transform.localPosition;
		gameObject.transform.localScale = Vector3.one;
		UISprite portrait = riftScopeCard._portrait;
		int width = (riftScopeCard._portrait.height = 54);
		portrait.width = width;
		riftScopeCard._portrait.pivot = UIWidget.Pivot.Center;
		riftScopeCard._portrait.depth = itemIconTex.depth;
		gameObject.SetActive(value: true);
		itemIconTex.gameObject.SetActive(value: false);
		Collider[] componentsInChildren = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
		for (width = 0; width < componentsInChildren.Length; width++)
		{
			componentsInChildren[width].enabled = false;
		}
		RectLayoutComponent[] componentsInChildren2 = source.GetComponentsInChildren<RectLayoutComponent>(includeInactive: true);
		for (width = 0; width < componentsInChildren2.Length; width++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren2[width]);
		}
		UIWidget component = source.GetComponent<UIWidget>();
		component.SetAnchor((Transform)null);
		component.pivot = UIWidget.Pivot.Center;
		component.width = 252;
		component.height = 146;
		Place(riftScopeCard, riftScopeCard._portrait, 13, 16, 54, 54);
		Place(riftScopeCard, riftScopeCard._title, 80, 14, 160, 58);
		Place(riftScopeCard, riftScopeCard._info, 14, 88, 224, 24);
		Place(riftScopeCard, riftScopeCard._note, 14, 116, 224, 24);
		if (parent != source.transform)
		{
			parent.gameObject.SetActive(value: false);
		}
		Text(riftScopeCard._title, 20, 2);
		Text(riftScopeCard._info, 18, 1);
		Text(riftScopeCard._note, 16, 1);
		UnityEngine.Object.DestroyImmediate(source);
		return riftScopeCard;
	}

	private static void Place(RiftScopeCard card, UIWidget widget, int x, int y, int width, int height)
	{
		widget.transform.SetParent(card.transform, worldPositionStays: false);
		widget.SetAnchor((Transform)null);
		widget.pivot = UIWidget.Pivot.TopLeft;
		widget.transform.localPosition = new Vector3(x - 126, 73 - y, 0f);
		widget.transform.localScale = Vector3.one;
		widget.width = width;
		widget.height = height;
	}

	private static void Text(UILabel label, int size, int lines)
	{
		label.fontSize = size;
		label.maxLineCount = lines;
		label.overflowMethod = UILabel.Overflow.ClampContent;
		label.overflowEllipsis = true;
		label.spacingY = 2;
		label.alignment = NGUIText.Alignment.Left;
	}

	private static T Field<T>(EncyclopediaCropWidget source, string name) where T : class
	{
		return typeof(EncyclopediaCropWidget).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source) as T;
	}

	public void Set(ScopeAnimal species, int nearby, bool selected, Action<int> select)
	{
		_species = species;
		_select = select;
		_title.text = species.DisplayName;
		_title.color = (selected ? PresetColor.UIYellow : Color.white);
		_info.text = RiftText.T("ใกล\u0e49ต\u0e31ว {0} ต\u0e31ว", "{0} nearby", nearby);
		_note.text = (selected ? RiftText.T("เล\u0e37อกชน\u0e34ดน\u0e35\u0e49แล\u0e49ว", "Selected species") : RiftScope.RoleName(species.Role));
		string portrait = AnimalYaml.GetPortrait(species.Type);
		bool flag = !string.IsNullOrEmpty(portrait) && ResourceSingleton<UISpriteManager>.Instance().GetSprite(portrait) != null;
		_portrait.spriteName = (flag ? portrait : RiftScope.MapIcon(species));
		_portrait.color = (flag ? Color.white : RiftScope.RoleColor(species.Role));
		UIUtility.UpdateAnchors(base.transform);
	}

	private void OnClick()
	{
		if (_species != null && _select != null)
		{
			_select(_species.Type);
		}
	}
}
