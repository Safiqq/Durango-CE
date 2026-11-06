using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.System;
using Durango.UI;
using Durango.UI.Control;
using Durango.Utils;
using Community.GatheringTiming;
using UnityEngine;

[DefaultExecutionOrder(32000)]
public sealed class CommunityHudLayout : MonoBehaviour
{
	[Serializable]
	public sealed class Placement
	{
		public string id;

		public float x;

		public float y;

		public float scale = 1f;

		public float alpha = 1f;

		public bool hidden;
	}

	[Serializable]
	public sealed class Profile
	{
		public int version = 1;

		public List<Placement> items = new List<Placement>();
	}

	private sealed class Target
	{
		public string id;

		public string label;

		public string group;

		public Transform transform;

		public Vector3 position;

		public Vector3 scale;

		public UIWidget widget;

		public float alpha;

		public Transform[] anchors;

		public bool overridden;

		public SelectableButton frame;

		public readonly List<Companion> companions = new List<Companion>();

		public readonly List<UIWidget> extraWidgets = new List<UIWidget>();

		public readonly Dictionary<UIWidget, bool> hiddenWidgets = new Dictionary<UIWidget, bool>();

		public readonly Dictionary<Collider, bool> hiddenColliders = new Dictionary<Collider, bool>();
	}

	private sealed class Companion
	{
		public Transform transform;

		public Vector3 position;

		public Vector3 scale;

		public Vector3 offset;

		public UIWidget widget;

		public float alpha;
	}

	private sealed class EditorControl
	{
		public SelectableButton button;

		public Action action;
	}

	public const string SettingKey = "hud_layout_edit";

	private static CommunityHudLayout instance;

	private readonly List<Target> targets = new List<Target>();

	private readonly Dictionary<Collider, bool> colliders = new Dictionary<Collider, bool>();

	private Profile saved = new Profile();

	private Profile draft;

	private string profileKey;

	private GameObject overlay;

	private SelectableButton template;

	private SelectableButton groupButton;

	private SelectableButton sizeButton;

	private SelectableButton alphaButton;

	private Target selected;

	private bool grouped;

	private int dragFinger = int.MinValue;

	private float releaseUntil;

	private MenuWidget inventoryShortcut;

	private float inventoryLookupAt;

	private float discoveryAt;

	private readonly List<EditorControl> controls = new List<EditorControl>();

	private EditorControl pressedControl;

	private Vector2 lastPointer;

	private SelectableButton visibilityButton;

	private bool listOpen;

	private int listPage;

	private readonly List<EditorControl> listRows = new List<EditorControl>();

	private EditorControl previousPage;

	private EditorControl nextPage;

	private GameObject recoveryRoot;

	private SelectableButton recoveryButton;

	public static Transform InventoryShortcutTransform
	{
		get
		{
			if (!(instance != null) || !(instance.inventoryShortcut != null) || !instance.inventoryShortcut.gameObject.activeInHierarchy)
			{
				return null;
			}
			return instance.inventoryShortcut.transform;
		}
	}

	public static bool Editing
	{
		get
		{
			if (instance != null)
			{
				return instance.draft != null;
			}
			return false;
		}
	}

	public static bool BlocksInput
	{
		get
		{
			if (instance != null)
			{
				if (!Editing && !(Time.unscaledTime < instance.releaseUntil))
				{
					return instance.RecoveryPointer();
				}
				return true;
			}
			return false;
		}
	}

	private static CommunityHudLayout Ensure()
	{
		if (instance != null)
		{
			return instance;
		}
		if (!Singleton<UIManager>.HasInstance() || Singleton<UIManager>.Instance().UIRoot == null)
		{
			return null;
		}
		GameObject obj = Singleton<UIManager>.Instance().UIRoot.gameObject.AddChild();
		obj.name = "CommunityHudLayout";
		instance = obj.AddComponent<CommunityHudLayout>();
		instance.Load();
		return instance;
	}

	public static void Register(string id, Transform transform, string label, string group)
	{
		if (transform == null)
		{
			return;
		}
		CommunityHudLayout communityHudLayout = Ensure();
		if (communityHudLayout == null)
		{
			return;
		}
		foreach (Target target in communityHudLayout.targets)
		{
			if (target.transform == transform)
			{
				return;
			}
		}
		UIWidget uIWidget = transform.GetComponent<UIWidget>();
		if (uIWidget == null)
		{
			uIWidget = transform.gameObject.AddComponent<UIWidget>();
		}
		communityHudLayout.targets.Add(new Target
		{
			id = id,
			label = label,
			group = group,
			transform = transform,
			position = transform.localPosition,
			scale = transform.localScale,
			widget = uIWidget,
			alpha = uIWidget.alpha,
			anchors = new Transform[4]
			{
				uIWidget.leftAnchor.target,
				uIWidget.bottomAnchor.target,
				uIWidget.rightAnchor.target,
				uIWidget.topAnchor.target
			}
		});
	}

	public static bool HasPlacement(string id)
	{
		if (instance != null)
		{
			return Find(instance.draft ?? instance.saved, id) != null;
		}
		return false;
	}

	public static bool IsHidden(string id)
	{
		return ((instance == null) ? null : Find(instance.draft ?? instance.saved, id))?.hidden ?? false;
	}

	public static bool IsHidden(Transform node)
	{
		if (instance == null || node == null)
		{
			return false;
		}
		foreach (Target target in instance.targets)
		{
			if (target.transform != null && (node == target.transform || node.IsChildOf(target.transform)) && IsHidden(target.id))
			{
				return true;
			}
		}
		return false;
	}

	public static void RegisterVisibilityWidgets(string id, UIWidget[] widgets)
	{
		if (instance == null || widgets == null)
		{
			return;
		}
		Target target = instance.targets.Find((Target item) => item.id == id);
		if (target == null)
		{
			return;
		}
		foreach (UIWidget uIWidget in widgets)
		{
			if (uIWidget != null && !target.extraWidgets.Contains(uIWidget))
			{
				target.extraWidgets.Add(uIWidget);
			}
		}
	}

	private static void RestoreVisibility(Target t)
	{
		foreach (KeyValuePair<UIWidget, bool> hiddenWidget in t.hiddenWidgets)
		{
			if (hiddenWidget.Key != null)
			{
				hiddenWidget.Key.enabled = hiddenWidget.Value;
			}
		}
		foreach (KeyValuePair<Collider, bool> hiddenCollider in t.hiddenColliders)
		{
			if (hiddenCollider.Key != null)
			{
				hiddenCollider.Key.enabled = hiddenCollider.Value;
			}
		}
		t.hiddenWidgets.Clear();
		t.hiddenColliders.Clear();
	}

	private void ApplyVisibility(Target t)
	{
		if (!IsHidden(t.id))
		{
			RestoreVisibility(t);
			return;
		}
		List<UIWidget> list = new List<UIWidget>(t.transform.GetComponentsInChildren<UIWidget>(includeInactive: true));
		list.AddRange(t.extraWidgets);
		foreach (Companion companion in t.companions)
		{
			if (companion.transform != null)
			{
				list.AddRange(companion.transform.GetComponentsInChildren<UIWidget>(includeInactive: true));
			}
		}
		foreach (UIWidget item in list)
		{
			if (!(item == null))
			{
				if (!t.hiddenWidgets.ContainsKey(item))
				{
					t.hiddenWidgets.Add(item, item.enabled);
				}
				item.enabled = false;
			}
		}
		Collider[] componentsInChildren = t.transform.GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider collider in componentsInChildren)
		{
			if (!t.hiddenColliders.ContainsKey(collider))
			{
				t.hiddenColliders.Add(collider, colliders.ContainsKey(collider) ? colliders[collider] : collider.enabled);
			}
			collider.enabled = false;
		}
	}

	private void ToggleVisibility(Target t)
	{
		if (draft != null && t != null && !(t.transform == null))
		{
			selected = t;
			Placement placement = Capture(t);
			placement.hidden = !placement.hidden;
			ApplyVisibility(t);
		}
	}

	public static void AttachCompanion(Transform button, Transform visual)
	{
		if (!(instance == null) && !(visual == null) && !visual.IsChildOf(button))
		{
			Target target = instance.targets.Find((Target item) => item.transform == button);
			if (target != null && !target.companions.Exists((Companion item) => item.transform == visual))
			{
				UIWidget component = visual.GetComponent<UIWidget>();
				target.companions.Add(new Companion
				{
					transform = visual,
					position = visual.localPosition,
					scale = visual.localScale,
					offset = button.InverseTransformPoint(visual.position),
					widget = component,
					alpha = ((component == null) ? 1f : component.alpha)
				});
			}
		}
	}

	private bool Leaf(Target t)
	{
		foreach (Target target in targets)
		{
			if (target != t && target.transform != null && target.transform.IsChildOf(t.transform))
			{
				return false;
			}
		}
		return true;
	}

	private static string StablePath(Transform node, Transform root)
	{
		List<string> list = new List<string>();
		while (node != null)
		{
			int num = 0;
			if (node.parent != null)
			{
				for (int i = 0; i < node.GetSiblingIndex(); i++)
				{
					if (node.parent.GetChild(i).name == node.name)
					{
						num++;
					}
				}
			}
			list.Add(node.name.Replace("/", "%2F") + "#" + num);
			if (node == root)
			{
				break;
			}
			node = node.parent;
		}
		list.Reverse();
		return string.Join("/", list.ToArray());
	}

	private void DiscoverButtons()
	{
		if (!GameManager.IsReady || Time.unscaledTime < discoveryAt || (!Editing && UIBase.HasOpenedUI))
		{
			return;
		}
		discoveryAt = Time.unscaledTime + 0.5f;
		Collider[] componentsInChildren = base.transform.parent.GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider collider in componentsInChildren)
		{
			if (collider.transform.IsChildOf(base.transform))
			{
				continue;
			}
			UIBase componentInParent = collider.GetComponentInParent<UIBase>();
			bool flag = componentInParent is BottomLeftMenuGroupBase || componentInParent is MinimapGroupBase || componentInParent is CombatGroup || componentInParent is ContextActionGroupBase || componentInParent is PlayerHudGroupBase || componentInParent is ToDoListGroupBase || componentInParent is PlayGuideHelperGroup;
			if (componentInParent is MenuListGroupBase)
			{
				flag = ((MenuListGroupBase)componentInParent).IsHudLayoutButton(collider.transform);
			}
			if (!flag || componentInParent == null)
			{
				continue;
			}
			Transform transform = null;
			Transform parent = collider.transform;
			while (parent != null && parent != componentInParent.transform)
			{
				bool num = parent.GetComponent<Selectable>() != null || parent.GetComponent<BattleActionButton>() != null || parent.GetComponent<ContextActionButtonBase>() != null || parent.GetComponent<CommunicationButtonBase>() != null;
				UIEventListener component = parent.GetComponent<UIEventListener>();
				if (num || (component != null && (component.onClick != null || component.onPress != null)))
				{
					transform = parent;
				}
				parent = parent.parent;
			}
			if (transform == null || !transform.gameObject.activeInHierarchy)
			{
				continue;
			}
			bool flag2 = false;
			foreach (Target target in targets)
			{
				if (!(target.transform == null) && (transform == target.transform || (transform.IsChildOf(target.transform) && target.id != "context_actions")))
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				string text = ((componentInParent is BottomLeftMenuGroupBase) ? "social" : ((componentInParent is CombatGroup) ? "combat" : ((componentInParent is ContextActionGroupBase) ? "context" : componentInParent.GetType().Name)));
				Register("hud/" + StablePath(transform, componentInParent.transform), transform, transform.name.Replace("(Clone)", ""), text);
			}
		}
	}

	public static float ScaleFor(string id)
	{
		Placement placement = ((instance == null) ? null : Find(instance.draft ?? instance.saved, id));
		if (placement != null)
		{
			return Mathf.Clamp(placement.scale, 0.5f, 1.8f);
		}
		return 1f;
	}

	public static float AlphaFor(string id)
	{
		Placement placement = ((instance == null) ? null : Find(instance.draft ?? instance.saved, id));
		if (placement != null)
		{
			return Mathf.Clamp(placement.alpha, 0.2f, 1f);
		}
		return 1f;
	}

	public static void Reapply(Transform root)
	{
		if (instance == null || !Application.isPlaying)
		{
			return;
		}
		foreach (Target target in instance.targets)
		{
			if (target.transform != null && (target.transform == root || target.transform.IsChildOf(root)))
			{
				instance.Apply(target, Find(instance.draft ?? instance.saved, target.id));
			}
		}
	}

	private static Placement Find(Profile profile, string id)
	{
		return profile.items.Find((Placement p) => p.id == id);
	}

	private static Profile Copy(Profile value)
	{
		return JsonUtility.FromJson<Profile>(JsonUtility.ToJson(value));
	}

	private void Load()
	{
		profileKey = "community_hud_layout_v1_" + (Platform.Instance.UsePCUI ? "pc" : "mobile");
		try
		{
			saved = JsonUtility.FromJson<Profile>(PlayerPrefs.GetString(profileKey, ""));
			if (saved == null || saved.version != 1 || saved.items == null)
			{
				saved = new Profile();
			}
			saved.items.RemoveAll((Placement p) => p == null || string.IsNullOrEmpty(p.id) || !Finite(p.x) || !Finite(p.y) || !Finite(p.scale) || !Finite(p.alpha));
		}
		catch (Exception)
		{
			saved = new Profile();
		}
	}

	private static bool Finite(float f)
	{
		if (!float.IsNaN(f))
		{
			return !float.IsInfinity(f);
		}
		return false;
	}

	private static Rect SafeArea()
	{
		Rect rect = Screen.safeArea;
		if (rect.width <= 0f || rect.height <= 0f)
		{
			rect = new Rect(0f, 0f, Screen.width, Screen.height);
		}
		return new Rect(rect.x + 8f, rect.y + 8f, Mathf.Max(1f, rect.width - 16f), Mathf.Max(1f, rect.height - 16f));
	}

	private static Camera CameraFor(Transform t)
	{
		return NGUITools.FindCameraForLayer(t.gameObject.layer);
	}

	private static Rect Bounds(Target t)
	{
		Camera camera = CameraFor(t.transform);
		Vector2 lhs = new Vector2(float.MaxValue, float.MaxValue);
		Vector2 lhs2 = new Vector2(float.MinValue, float.MinValue);
		List<UIWidget> list = new List<UIWidget>();
		if (t.id == "map" || !t.transform.gameObject.activeInHierarchy)
		{
			list.Add(t.widget);
		}
		else
		{
			list.AddRange(t.transform.GetComponentsInChildren<UIWidget>());
		}
		foreach (Companion companion in t.companions)
		{
			if (companion.widget != null && companion.widget.gameObject.activeInHierarchy)
			{
				list.Add(companion.widget);
			}
		}
		foreach (UIWidget item in list)
		{
			Vector3[] worldCorners = item.worldCorners;
			foreach (Vector3 position in worldCorners)
			{
				Vector2 rhs = camera.WorldToScreenPoint(position);
				lhs = Vector2.Min(lhs, rhs);
				lhs2 = Vector2.Max(lhs2, rhs);
			}
		}
		return Rect.MinMaxRect(lhs.x, lhs.y, lhs2.x, lhs2.y);
	}

	private static void MoveScreen(Target t, Vector2 delta)
	{
		Camera camera = CameraFor(t.transform);
		Vector3 position = camera.WorldToScreenPoint(t.transform.position);
		position.x += delta.x;
		position.y += delta.y;
		t.transform.position = camera.ScreenToWorldPoint(position);
	}

	private static void ClearAnchors(UIWidget w)
	{
		UIRect.AnchorPoint leftAnchor = w.leftAnchor;
		UIRect.AnchorPoint bottomAnchor = w.bottomAnchor;
		UIRect.AnchorPoint rightAnchor = w.rightAnchor;
		Transform transform = (w.topAnchor.target = null);
		Transform transform3 = (rightAnchor.target = transform);
		Transform target = (bottomAnchor.target = transform3);
		leftAnchor.target = target;
	}

	private void Restore(Target t)
	{
		if (!t.overridden || t.transform == null)
		{
			return;
		}
		t.transform.localPosition = t.position;
		t.transform.localScale = t.scale;
		t.widget.alpha = t.alpha;
		t.widget.leftAnchor.target = t.anchors[0];
		t.widget.bottomAnchor.target = t.anchors[1];
		t.widget.rightAnchor.target = t.anchors[2];
		t.widget.topAnchor.target = t.anchors[3];
		t.overridden = false;
		t.widget.ResetAndUpdateAnchors();
		foreach (Companion companion in t.companions)
		{
			if (!(companion.transform == null))
			{
				companion.transform.localPosition = companion.position;
				companion.transform.localScale = companion.scale;
				if (companion.widget != null)
				{
					companion.widget.alpha = companion.alpha;
				}
			}
		}
	}

	private void Apply(Target t, Placement p)
	{
		if (t.transform == null || CameraFor(t.transform) == null)
		{
			return;
		}
		if (p == null)
		{
			if (t.overridden)
			{
				Restore(t);
				return;
			}
			t.position = t.transform.localPosition;
			t.scale = t.transform.localScale;
			t.alpha = t.widget.alpha;
			t.anchors = new Transform[4]
			{
				t.widget.leftAnchor.target,
				t.widget.bottomAnchor.target,
				t.widget.rightAnchor.target,
				t.widget.topAnchor.target
			};
		}
		else if (Visible(t))
		{
			t.overridden = true;
			ClearAnchors(t.widget);
			Rect rect = SafeArea();
			t.transform.localScale = t.scale * Mathf.Clamp(p.scale, 0.5f, 1.8f);
			t.widget.alpha = t.alpha * Mathf.Clamp(p.alpha, 0.2f, 1f);
			Vector2 vector = CameraFor(t.transform).WorldToScreenPoint(t.transform.position);
			MoveScreen(t, new Vector2(rect.x + Mathf.Clamp01(p.x) * rect.width, rect.y + Mathf.Clamp01(p.y) * rect.height) - vector);
			SyncCompanions(t, p.alpha);
			Rect rect2 = Bounds(t);
			float num = Mathf.Min(1f, Mathf.Min(rect.width / Mathf.Max(1f, rect2.width), rect.height / Mathf.Max(1f, rect2.height)));
			if (num < 1f)
			{
				t.transform.localScale *= num;
				SyncCompanions(t, p.alpha);
				rect2 = Bounds(t);
			}
			Vector2 delta = new Vector2((rect2.xMin < rect.xMin) ? (rect.xMin - rect2.xMin) : ((rect2.xMax > rect.xMax) ? (rect.xMax - rect2.xMax) : 0f), (rect2.yMin < rect.yMin) ? (rect.yMin - rect2.yMin) : ((rect2.yMax > rect.yMax) ? (rect.yMax - rect2.yMax) : 0f));
			MoveScreen(t, delta);
			SyncCompanions(t, p.alpha);
		}
	}

	private static void SyncCompanions(Target t, float alpha)
	{
		foreach (Companion companion in t.companions)
		{
			if (!(companion.transform == null))
			{
				companion.transform.position = t.transform.TransformPoint(companion.offset);
				companion.transform.localScale = companion.scale * (t.transform.localScale.x / Mathf.Max(0.001f, t.scale.x));
				if (companion.widget != null)
				{
					companion.widget.alpha = companion.alpha * Mathf.Clamp(alpha, 0.2f, 1f);
				}
			}
		}
	}

	private Placement Capture(Target t)
	{
		Placement placement = Find(draft, t.id);
		if (placement == null)
		{
			placement = new Placement
			{
				id = t.id
			};
			draft.items.Add(placement);
		}
		Rect rect = SafeArea();
		Vector3 vector = CameraFor(t.transform).WorldToScreenPoint(t.transform.position);
		placement.x = (vector.x - rect.x) / rect.width;
		placement.y = (vector.y - rect.y) / rect.height;
		return placement;
	}

	public static void Open(SelectableButton source)
	{
		CommunityHudLayout owner = Ensure();
		if (!(owner == null) && !Editing && !(source == null) && GameManager.IsReady)
		{
			GatherSkillCheck.SuspendForHudEdit();
			owner.draft = Copy(owner.saved);
			owner.overlay = owner.gameObject.AddChild();
			owner.overlay.name = "HudLayoutEditor";
			owner.overlay.AddComponent<UIPanel>().depth = 30000;
			owner.template = UnityEngine.Object.Instantiate(source, owner.overlay.transform);
			owner.template.gameObject.SetActive(value: false);
			owner.Button("บ\u0e31นท\u0e36ก / Save", delegate
			{
				owner.Finish(save: true);
			}, 0);
			owner.Button("ยกเล\u0e34ก / Cancel", delegate
			{
				owner.Finish(save: false);
			}, 1);
			owner.Button("ค\u0e37นค\u0e48าเด\u0e34ม / Reset", owner.ResetDraft, 2);
			owner.groupButton = owner.Button("แยกป\u0e38\u0e48ม / Single", delegate
			{
				owner.grouped = !owner.grouped;
				owner.groupButton.Text = (owner.grouped ? "ท\u0e31\u0e49งกล\u0e38\u0e48ม / Group" : "แยกป\u0e38\u0e48ม / Single");
			}, 3);
			owner.sizeButton = owner.Button("ขนาด − / Size −", delegate
			{
				owner.Adjust(-0.1f, 0f);
			}, 4);
			owner.Button("ขนาด + / Size +", delegate
			{
				owner.Adjust(0.1f, 0f);
			}, 5);
			owner.alphaButton = owner.Button("โปร\u0e48งใส − / Opacity −", delegate
			{
				owner.Adjust(0f, -0.1f);
			}, 6);
			owner.Button("โปร\u0e48งใส + / Opacity +", delegate
			{
				owner.Adjust(0f, 0.1f);
			}, 7);
			owner.visibilityButton = owner.Button("แสดง / ซ\u0e48อนป\u0e38\u0e48ม", delegate
			{
				owner.ToggleVisibility(owner.selected);
			}, 8);
			owner.Button("รายการป\u0e38\u0e48ม / Buttons", delegate
			{
				owner.listOpen = !owner.listOpen;
			}, 9);
			ConfigGroup configGroup = UIManager.FindScript<ConfigGroup>();
			if (configGroup != null)
			{
				configGroup.Close();
			}
			GameSystem<InputSystem>.Instance().ResetHudLayoutInput();
			if (Singleton<PlayerController>.HasInstance())
			{
				Singleton<PlayerController>.Instance().StopMove();
			}
			owner.DisableGameplayColliders();
		}
	}

	private SelectableButton Button(string text, Action action, int index)
	{
		SelectableButton selectableButton = UnityEngine.Object.Instantiate(template, overlay.transform);
		selectableButton.gameObject.name = "HudLayoutControl" + index;
		ClearAnchors(selectableButton.Widget);
		selectableButton.transform.localScale = Vector3.one;
		selectableButton.ShortcutCommand = InputCommand.None;
		selectableButton.Clicked = action;
		selectableButton.SubClicked = null;
		selectableButton.LongPressed = (selectableButton.RightClicked = (selectableButton.DoubleClicked = null));
		selectableButton.Text = text;
		selectableButton.SetStyle(PresetButton.Style.Border);
		selectableButton.gameObject.SetActive(value: true);
		selectableButton.Widget.depth = 100;
		selectableButton.SetDimensions(210, 52);
		selectableButton.Init();
		Collider[] componentsInChildren = selectableButton.GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider obj in componentsInChildren)
		{
			obj.enabled = obj.gameObject == selectableButton.gameObject;
		}
		NGUITools.AddWidgetCollider(selectableButton.gameObject);
		if (index >= 0)
		{
			controls.Add(new EditorControl
			{
				button = selectableButton,
				action = action
			});
		}
		return selectableButton;
	}

	private void ResetDraft()
	{
		draft = new Profile();
		selected = null;
		foreach (Target target in targets)
		{
			Restore(target);
			RestoreVisibility(target);
		}
	}

	private IEnumerable<Target> Selection()
	{
		foreach (Target target in targets)
		{
			if (target.transform != null && Visible(target) && Leaf(target) && (target == selected || (grouped && selected != null && target.group == selected.group)))
			{
				yield return target;
			}
		}
	}

	private static bool Visible(Target t)
	{
		if (t.transform.gameObject.activeInHierarchy)
		{
			return true;
		}
		if (!t.id.StartsWith("context_slot_", StringComparison.Ordinal) || t.transform.parent == null || !t.transform.parent.gameObject.activeInHierarchy)
		{
			return false;
		}
		foreach (Companion companion in t.companions)
		{
			if (companion.transform != null && companion.transform.gameObject.activeInHierarchy)
			{
				return true;
			}
		}
		return false;
	}

	private void Adjust(float size, float alpha)
	{
		if (selected == null)
		{
			return;
		}
		foreach (Target item in Selection())
		{
			Placement placement = Capture(item);
			placement.scale = Mathf.Clamp(placement.scale + size, 0.5f, 1.8f);
			placement.alpha = Mathf.Clamp(placement.alpha + alpha, 0.2f, 1f);
			Apply(item, placement);
		}
	}

	private void Drag(Target target, Vector2 delta)
	{
		if (dragFinger == UICamera.currentTouchID && target == selected)
		{
			MoveSelection(delta);
		}
	}

	private void MoveSelection(Vector2 delta)
	{
		Rect rect = SafeArea();
		float num = float.MinValue;
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		float num4 = float.MaxValue;
		foreach (Target item in Selection())
		{
			Rect rect2 = Bounds(item);
			num = Mathf.Max(num, rect.xMin - rect2.xMin);
			num2 = Mathf.Min(num2, rect.xMax - rect2.xMax);
			num3 = Mathf.Max(num3, rect.yMin - rect2.yMin);
			num4 = Mathf.Min(num4, rect.yMax - rect2.yMax);
		}
		delta.x = ((num <= num2) ? Mathf.Clamp(delta.x, num, num2) : 0f);
		delta.y = ((num3 <= num4) ? Mathf.Clamp(delta.y, num3, num4) : 0f);
		foreach (Target item2 in Selection())
		{
			MoveScreen(item2, delta);
			Capture(item2);
		}
	}

	private void DisableGameplayColliders()
	{
		Collider[] componentsInChildren = base.transform.parent.GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider collider in componentsInChildren)
		{
			if (!(overlay != null) || !collider.transform.IsChildOf(overlay.transform))
			{
				if (!colliders.ContainsKey(collider))
				{
					colliders.Add(collider, collider.enabled);
				}
				collider.enabled = false;
			}
		}
	}

	private void Finish(bool save)
	{
		if (save)
		{
			PlayerPrefs.SetString(profileKey, JsonUtility.ToJson(draft));
			PlayerPrefs.Save();
			saved = Copy(draft);
		}
		draft = null;
		selected = null;
		dragFinger = int.MinValue;
		controls.Clear();
		pressedControl = null;
		listRows.Clear();
		previousPage = (nextPage = null);
		listOpen = false;
		listPage = 0;
		foreach (Target target in targets)
		{
			if (target.transform != null)
			{
				Apply(target, Find(saved, target.id));
			}
			target.frame = null;
		}
		foreach (KeyValuePair<Collider, bool> collider in colliders)
		{
			if (collider.Key != null)
			{
				collider.Key.enabled = collider.Value;
			}
		}
		colliders.Clear();
		foreach (Target target2 in targets)
		{
			if (target2.transform != null)
			{
				ApplyVisibility(target2);
			}
		}
		if (Application.isPlaying)
		{
			UnityEngine.Object.Destroy(overlay);
		}
		else
		{
			UnityEngine.Object.DestroyImmediate(overlay);
		}
		overlay = null;
		releaseUntil = Time.unscaledTime + 0.25f;
		if (GameSystem<InputSystem>.HasInstance())
		{
			GameSystem<InputSystem>.Instance().ResetHudLayoutInput();
		}
	}

	private void LateUpdate()
	{
		UpdateInventoryShortcut();
		DiscoverButtons();
		if ("community_hud_layout_v1_" + (Platform.Instance.UsePCUI ? "pc" : "mobile") != profileKey)
		{
			if (Editing)
			{
				Finish(save: false);
			}
			foreach (Target target in targets)
			{
				Restore(target);
			}
			Load();
		}
		targets.RemoveAll((Target target) => target.transform == null);
		foreach (Target target2 in targets)
		{
			Apply(target2, Find(draft ?? saved, target2.id));
		}
		foreach (Target target3 in targets)
		{
			ApplyVisibility(target3);
		}
		UpdateRecovery();
		if (!Editing)
		{
			if (Time.unscaledTime < releaseUntil && (Input.touchCount > 0 || Input.GetMouseButton(0)))
			{
				releaseUntil = Time.unscaledTime + 0.25f;
			}
			return;
		}
		if (!GameManager.IsReady)
		{
			Finish(save: false);
			return;
		}
		DisableGameplayColliders();
		LayoutToolbar();
		LayoutButtonList();
		visibilityButton.Text = ((selected == null) ? "เล\u0e37อกป\u0e38\u0e48มก\u0e48อน / Select" : (IsHidden(selected.id) ? "เป\u0e34ดป\u0e38\u0e48ม / Show" : "ซ\u0e48อนป\u0e38\u0e48ม / Hide"));
		if (selected != null)
		{
			Placement placement = Find(draft, selected.id);
			sizeButton.Text = "ขนาด − / Size " + Mathf.RoundToInt((placement?.scale ?? 1f) * 100f) + "% −";
			alphaButton.Text = "โปร\u0e48งใส − / Opacity " + Mathf.RoundToInt((placement?.alpha ?? 1f) * 100f) + "% −";
		}
		foreach (Target t in targets)
		{
			if (!ShowFrame(t))
			{
				if (t.frame != null)
				{
					t.frame.gameObject.SetActive(value: false);
				}
				continue;
			}
			if (t.frame == null)
			{
				t.frame = Button(t.label, delegate
				{
					selected = t;
				}, -1);
				t.frame.name = "HudLayoutTarget_" + t.id;
				t.frame.Widget.depth = 10;
				UIEventListener uIEventListener = UIEventListener.Get(t.frame.gameObject);
				uIEventListener.onPress = (UIEventListener.BoolDelegate)Delegate.Combine(uIEventListener.onPress, (UIEventListener.BoolDelegate)delegate(GameObject go, bool down)
				{
					if (down && dragFinger == int.MinValue)
					{
						selected = t;
						dragFinger = UICamera.currentTouchID;
					}
					else if (!down && dragFinger == UICamera.currentTouchID)
					{
						dragFinger = int.MinValue;
					}
				});
				uIEventListener.onDrag = (UIEventListener.VectorDelegate)Delegate.Combine(uIEventListener.onDrag, (UIEventListener.VectorDelegate)delegate(GameObject go, Vector2 delta)
				{
					Drag(t, delta);
				});
			}
			t.frame.gameObject.SetActive(value: true);
			t.frame.Selected = t == selected;
			t.frame.Text = (IsHidden(t.id) ? "[ซ\u0e48อน] " : "") + t.label;
			Rect rect = Bounds(t);
			PlaceOverlay(t.frame, rect.center, Mathf.Max(48f, rect.width), Mathf.Max(48f, rect.height));
		}
		Collider[] componentsInChildren = overlay.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int num = 0; num < componentsInChildren.Length; num++)
		{
			componentsInChildren[num].enabled = false;
		}
		ProcessPointer();
	}

	private static Rect WidgetScreenRect(UIWidget widget)
	{
		Camera camera = CameraFor(widget.transform);
		Vector2 vector = new Vector2(float.MaxValue, float.MaxValue);
		Vector2 lhs = -vector;
		Vector3[] worldCorners = widget.worldCorners;
		foreach (Vector3 position in worldCorners)
		{
			Vector2 rhs = camera.WorldToScreenPoint(position);
			vector = Vector2.Min(vector, rhs);
			lhs = Vector2.Max(lhs, rhs);
		}
		return Rect.MinMaxRect(vector.x, vector.y, lhs.x, lhs.y);
	}

	private bool ShowFrame(Target t)
	{
		if (selected != null && Visible(t) && Leaf(t))
		{
			if (t != selected)
			{
				if (grouped)
				{
					return t.group == selected.group;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private static Rect HitRect(Target t)
	{
		Rect rect = Bounds(t);
		Vector2 vector = new Vector2(Mathf.Max(48f, rect.width), Mathf.Max(48f, rect.height));
		return new Rect(rect.center - vector * 0.5f, vector);
	}

	private void BeginPointer(int id, Vector2 point)
	{
		if (dragFinger != int.MinValue)
		{
			return;
		}
		dragFinger = id;
		lastPointer = point;
		pressedControl = null;
		foreach (EditorControl control in controls)
		{
			if (control.button != null && control.button.gameObject.activeInHierarchy && WidgetScreenRect(control.button.Widget).Contains(point))
			{
				pressedControl = control;
				control.button.Selected = true;
				return;
			}
		}
		Target target = null;
		float num = float.MaxValue;
		foreach (Target target2 in targets)
		{
			if (target2.transform == null || !Visible(target2) || !Leaf(target2) || CameraFor(target2.transform) == null || (IsHidden(target2.id) && target2 != selected))
			{
				continue;
			}
			Rect rect = HitRect(target2);
			if (rect.Contains(point))
			{
				float sqrMagnitude = (point - rect.center).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					target = target2;
					num = sqrMagnitude;
				}
			}
		}
		selected = target;
	}

	private void UpdatePointer(int id, Vector2 point, bool released, bool canceled)
	{
		if (dragFinger != id)
		{
			return;
		}
		if (!released && !canceled && pressedControl == null && selected != null)
		{
			MoveSelection(point - lastPointer);
		}
		lastPointer = point;
		if (!released && !canceled)
		{
			return;
		}
		EditorControl editorControl = pressedControl;
		pressedControl = null;
		dragFinger = int.MinValue;
		if (editorControl != null)
		{
			editorControl.button.Selected = false;
			if (!canceled && WidgetScreenRect(editorControl.button.Widget).Contains(point))
			{
				editorControl.action();
			}
		}
	}

	private void ProcessPointer()
	{
		if (Input.touchCount > 0)
		{
			for (int i = 0; i < Input.touchCount; i++)
			{
				Touch touch = Input.GetTouch(i);
				if (touch.phase == TouchPhase.Began)
				{
					BeginPointer(touch.fingerId, touch.position);
				}
				else
				{
					UpdatePointer(touch.fingerId, touch.position, touch.phase == TouchPhase.Ended, touch.phase == TouchPhase.Canceled);
				}
				if (!Editing)
				{
					break;
				}
			}
		}
		else
		{
			Vector2 point = Input.mousePosition;
			if (Input.GetMouseButtonDown(0))
			{
				BeginPointer(-1000, point);
			}
			else if (dragFinger == -1000)
			{
				UpdatePointer(-1000, point, Input.GetMouseButtonUp(0), !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0));
			}
		}
	}

	private void UpdateInventoryShortcut()
	{
		bool flag = Editing || HasPlacement("inventory");
		if (inventoryShortcut == null && flag && GameManager.IsReady && Time.unscaledTime >= inventoryLookupAt)
		{
			inventoryLookupAt = Time.unscaledTime + 1f;
			MenuWidget[] array = UnityEngine.Object.FindObjectsByType<MenuWidget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (MenuWidget menuWidget in array)
			{
				if (menuWidget.Type != MenuType.Inventory || (menuWidget.GetComponentInParent<MenuListWidgetBase>() == null && menuWidget.GetComponentInParent<LandscapeMenuListBase>() == null))
				{
					continue;
				}
				inventoryShortcut = UnityEngine.Object.Instantiate(menuWidget, base.transform);
				inventoryShortcut.name = "HudInventoryShortcut";
				inventoryShortcut.Init();
				inventoryShortcut.Set(MenuType.Inventory);
				inventoryShortcut.Clicked = delegate
				{
					if (!BlocksInput)
					{
						UIManager.Inventory.Open();
					}
				};
				ClearAnchors(inventoryShortcut.Widget);
				inventoryShortcut.transform.localScale = Vector3.one;
				Camera camera = CameraFor(inventoryShortcut.transform);
				Rect rect = SafeArea();
				Vector3 position = camera.WorldToScreenPoint(inventoryShortcut.transform.position);
				position.x = rect.x + rect.width * 0.12f;
				position.y = rect.y + rect.height * 0.78f;
				inventoryShortcut.transform.position = camera.ScreenToWorldPoint(position);
				Register("inventory", inventoryShortcut.transform, "กระเป\u0e4bา / Bag", "menu");
				break;
			}
		}
		if (inventoryShortcut != null)
		{
			inventoryShortcut.gameObject.SetActive(flag && GameManager.IsReady && !GameManager.IsPrologueMode && (Editing || !UIBase.HasOpenedUI));
		}
	}

	private List<Target> ListedTargets()
	{
		List<Target> list = targets.FindAll((Target t) => t.transform != null && Leaf(t));
		list.Sort(delegate(Target a, Target b)
		{
			int num = string.Compare(a.label, b.label, StringComparison.Ordinal);
			return (num == 0) ? string.Compare(a.id, b.id, StringComparison.Ordinal) : num;
		});
		return list;
	}

	private EditorControl ListControl(string name, Action action)
	{
		SelectableButton selectableButton = Button(name, action, -1);
		selectableButton.name = "HudLayoutButtonList";
		selectableButton.Widget.depth = 200;
		EditorControl editorControl = new EditorControl
		{
			button = selectableButton,
			action = action
		};
		controls.Add(editorControl);
		return editorControl;
	}

	private void LayoutButtonList()
	{
		if (!listOpen)
		{
			foreach (EditorControl listRow in listRows)
			{
				listRow.button.gameObject.SetActive(value: false);
			}
			if (previousPage != null)
			{
				previousPage.button.gameObject.SetActive(value: false);
			}
			if (nextPage != null)
			{
				nextPage.button.gameObject.SetActive(value: false);
			}
			return;
		}
		while (listRows.Count < 6)
		{
			listRows.Add(ListControl("", delegate
			{
			}));
		}
		if (previousPage == null)
		{
			previousPage = ListControl("ก\u0e48อนหน\u0e49า / Previous", delegate
			{
				listPage = Mathf.Max(0, listPage - 1);
			});
		}
		if (nextPage == null)
		{
			nextPage = ListControl("ถ\u0e31ดไป / Next", delegate
			{
				listPage++;
			});
		}
		List<Target> list = ListedTargets();
		int num = Mathf.Max(1, (list.Count + 6 - 1) / 6);
		listPage = Mathf.Clamp(listPage, 0, num - 1);
		Rect rect = SafeArea();
		float num2 = Mathf.Min(400f, rect.width * 0.9f);
		float a = Mathf.Clamp(rect.height * 0.065f, 34f, 56f);
		int num3 = ((rect.width >= 900f) ? 4 : 2);
		float num4 = Mathf.Clamp(rect.height * 0.075f, 40f, 64f) * (float)((10 + num3 - 1) / num3);
		a = Mathf.Min(a, Mathf.Max(18f, (rect.height - num4 - 16f) / 7f));
		float num5 = rect.yMax - num4 - 8f;
		for (int num6 = 0; num6 < 6; num6++)
		{
			EditorControl editorControl = listRows[num6];
			int num7 = listPage * 6 + num6;
			editorControl.button.gameObject.SetActive(num7 < list.Count);
			if (num7 < list.Count)
			{
				Target t = list[num7];
				editorControl.button.Text = (IsHidden(t.id) ? "○ ซ\u0e48อน / Hidden · " : "● แสดง / Shown · ") + t.label;
				editorControl.action = delegate
				{
					ToggleVisibility(t);
				};
				PlaceOverlay(editorControl.button, new Vector2(rect.center.x, num5 - a * ((float)num6 + 0.5f)), num2, a - 3f);
			}
		}
		previousPage.button.gameObject.SetActive(value: true);
		nextPage.button.gameObject.SetActive(value: true);
		previousPage.button.Text = "◀ " + (listPage + 1) + " / " + num;
		PlaceOverlay(previousPage.button, new Vector2(rect.center.x - num2 * 0.25f, num5 - a * 6.5f), num2 * 0.5f - 3f, a - 3f);
		PlaceOverlay(nextPage.button, new Vector2(rect.center.x + num2 * 0.25f, num5 - a * 6.5f), num2 * 0.5f - 3f, a - 3f);
	}

	private void PlaceOverlay(SelectableButton button, Vector2 screen, float width, float height)
	{
		Camera camera = CameraFor(button.transform);
		float z = camera.WorldToScreenPoint(button.transform.position).z;
		Vector3 vector = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z));
		Vector3 vector2 = camera.ScreenToWorldPoint(new Vector3(screen.x + width, screen.y + height, z));
		button.transform.position = vector;
		Vector3 vector3 = button.transform.parent.InverseTransformVector(vector2 - vector);
		button.SetDimensions(Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(vector3.x))), Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(vector3.y))));
		NGUITools.UpdateWidgetCollider(button.gameObject);
	}

	private void LayoutToolbar()
	{
		Rect rect = SafeArea();
		int num = ((rect.width >= 900f) ? 4 : 2);
		float num2 = Mathf.Min(220f, (rect.width - 16f) / (float)num);
		float num3 = Mathf.Clamp(rect.height * 0.075f, 40f, 64f);
		int num4 = 0;
		foreach (Transform item in overlay.transform)
		{
			if (item.name.StartsWith("HudLayoutControl", StringComparison.Ordinal) && !item.name.EndsWith("-1", StringComparison.Ordinal))
			{
				SelectableButton component = item.GetComponent<SelectableButton>();
				PlaceOverlay(component, new Vector2(rect.center.x + ((float)(num4 % num) - (float)(num - 1) * 0.5f) * num2, rect.yMax - num3 * (0.5f + (float)(num4 / num))), num2 - 4f, num3 - 4f);
				num4++;
			}
		}
	}

	private bool RecoveryPointer()
	{
		if (recoveryButton == null || !recoveryButton.gameObject.activeInHierarchy)
		{
			return false;
		}
		Rect rect = WidgetScreenRect(recoveryButton.Widget);
		for (int i = 0; i < Input.touchCount; i++)
		{
			if (rect.Contains(Input.GetTouch(i).position))
			{
				return true;
			}
		}
		if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
		{
			return rect.Contains(Input.mousePosition);
		}
		return false;
	}

	private void UpdateRecovery()
	{
		bool flag = saved.items.Exists((Placement p) => p.hidden) && !Editing && GameManager.IsReady && !UIBase.HasOpenedUI;
		if (flag && recoveryButton == null)
		{
			ConfigMainWidget configMainWidget = UIManager.FindScript<ConfigMainWidget>();
			if (configMainWidget != null && configMainWidget.HudLayoutTemplate != null)
			{
				recoveryRoot = base.gameObject.AddChild();
				recoveryRoot.name = "HudLayoutRecovery";
				recoveryRoot.AddComponent<UIPanel>().depth = 29900;
				recoveryButton = UnityEngine.Object.Instantiate(configMainWidget.HudLayoutTemplate, recoveryRoot.transform);
				ClearAnchors(recoveryButton.Widget);
				recoveryButton.transform.localScale = Vector3.one;
				recoveryButton.ShortcutCommand = InputCommand.None;
				recoveryButton.Clicked = null;
				recoveryButton.Text = "จ\u0e31ด HUD / Restore";
				recoveryButton.SetStyle(PresetButton.Style.Border);
				recoveryButton.gameObject.SetActive(value: true);
				recoveryButton.Init();
			}
		}
		if (recoveryButton == null)
		{
			return;
		}
		recoveryRoot.SetActive(flag);
		if (!flag)
		{
			return;
		}
		Rect rect = SafeArea();
		PlaceOverlay(recoveryButton, new Vector2(rect.center.x, rect.yMin + 28f), Mathf.Min(190f, rect.width), 48f);
		Collider[] componentsInChildren = recoveryRoot.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int num = 0; num < componentsInChildren.Length; num++)
		{
			componentsInChildren[num].enabled = false;
		}
		bool flag2 = Input.touchCount == 0 && Input.GetMouseButtonUp(0);
		for (int num2 = 0; num2 < Input.touchCount; num2++)
		{
			if (Input.GetTouch(num2).phase == TouchPhase.Ended)
			{
				flag2 = true;
			}
		}
		if (flag2 && RecoveryPointer())
		{
			Open(recoveryButton);
			listOpen = true;
			int num3 = ListedTargets().FindIndex((Target t) => IsHidden(t.id));
			listPage = ((num3 >= 0) ? (num3 / 6) : 0);
			recoveryRoot.SetActive(value: false);
		}
	}

	private void OnApplicationFocus(bool focus)
	{
		if (!focus && Editing)
		{
			Finish(save: false);
		}
	}

	private void OnDestroy()
	{
		if (draft != null)
		{
			foreach (Target target in targets)
			{
				if (target.transform != null)
				{
					Apply(target, Find(saved, target.id));
				}
			}
		}
		foreach (KeyValuePair<Collider, bool> collider in colliders)
		{
			if (collider.Key != null)
			{
				collider.Key.enabled = collider.Value;
			}
		}
		foreach (Target target2 in targets)
		{
			RestoreVisibility(target2);
		}
		if (instance == this)
		{
			instance = null;
		}
	}
}
