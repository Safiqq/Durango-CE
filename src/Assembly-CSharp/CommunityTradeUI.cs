using System;
using System.Collections.Generic;
using System.Linq;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using Shared.Economy;
using UnityEngine;

public class CommunityTradeUI : MonoBehaviour
{
	private sealed class SlotView
	{
		public GameObject Go;

		public UISprite Frame;

		public Durango.UI.Control.ItemIconWidget Icon;

		public UILabel Plus;

		public string Proto = string.Empty;

		public int Level;

		public List<string> Ids = new List<string>();

		public ItemData Item;
	}

	private sealed class SideView
	{
		public UILabel Name;

		public UILabel Status;

		public UILabel TStone;

		public readonly List<SlotView> Slots = new List<SlotView>();
	}

	private const int Depth = 5900;

	private const int W = 1120;

	private const int H = 640;

	private const int ColW = 520;

	private const int Slot = 118;

	private static CommunityTradeUI _win;

	private static readonly Dictionary<string, SelectableButton> _buttons = new Dictionary<string, SelectableButton>(StringComparer.Ordinal);

	private static SelectableButton _btnTpl;

	private static UILabel _lblTpl;

	private static Durango.UI.Control.ItemIconWidget _iconTpl;

	private static readonly Dictionary<string, UISprite> _surfaces = new Dictionary<string, UISprite>();

	private static JObject _previewTrade;

	private static readonly Dictionary<string, ItemData> _previewItems = new Dictionary<string, ItemData>();

	private static bool _opening;

	private GameObject _detailsLayer;

	private ItemInfoTooltip _details;

	private UIPanel _panel;

	private UILabel _title;

	private UILabel _sub;

	private UILabel _info;

	private readonly SideView _me = new SideView();

	private readonly SideView _other = new SideView();

	private SelectableButton _bAdd;

	private SelectableButton _bMoney;

	private SelectableButton _bReady;

	private SelectableButton _bConfirm;

	private SelectableButton _bCancel;

	private float _lockUntil;

	private float _nextTick;

	private int _shownVersion = -1;

	private bool _busy;

	private bool _anyReady;

	private bool _changedNotice;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"title",
			new string[4] { "แลกเปล\u0e35\u0e48ยนก\u0e31บ {0}", "Trading with {0}", "Bertukar dengan {0}", "{0}님과 거래" }
		},
		{
			"sub",
			new string[4] { "ของ/เง\u0e34นท\u0e35\u0e48วางถ\u0e39กพ\u0e31กไว\u0e49ท\u0e35\u0e48เซ\u0e34ร\u0e4cฟ · ข\u0e49อเสนอร\u0e38\u0e48นท\u0e35\u0e48 {0}", "Offers are held by the server · offer version {0}", "Ditahan server · versi {0}", "서버 보관 중 · 제안 버전 {0}" }
		},
		{
			"you",
			new string[4] { "ค\u0e38ณ", "You", "Kamu", "나" }
		},
		{
			"st_choosing",
			new string[4] { "กำล\u0e31งเล\u0e37อกของ", "Choosing", "Memilih", "선택 중" }
		},
		{
			"st_ready",
			new string[4] { "พร\u0e49อมแล\u0e49ว", "Ready", "Siap", "준비 완료" }
		},
		{
			"st_confirmed",
			new string[4] { "ย\u0e37นย\u0e31นแล\u0e49ว", "Confirmed", "Dikonfirmasi", "확정" }
		},
		{
			"lv",
			new string[4] { "Lv.{0}", "Lv.{0}", "Lv.{0}", "Lv.{0}" }
		},
		{
			"add",
			new string[4] { "+ เพ\u0e34\u0e48มของ", "+ Add items", "+ Tambah", "+ 아이템" }
		},
		{
			"money",
			new string[4] { "ใส\u0e48 T Stone", "Set T Stone", "Atur T Stone", "T Stone 설정" }
		},
		{
			"ready",
			new string[4] { "พร\u0e49อมแลกเปล\u0e35\u0e48ยน", "Ready", "Siap", "준비" }
		},
		{
			"unready",
			new string[4] { "ยกเล\u0e34กพร\u0e49อม", "Not ready", "Batal siap", "준비 취소" }
		},
		{
			"confirm",
			new string[4] { "ย\u0e37นย\u0e31นส\u0e38ดท\u0e49าย", "Final confirm", "Konfirmasi", "최종 확인" }
		},
		{
			"confirm_in",
			new string[4] { "ย\u0e37นย\u0e31นได\u0e49ใน {0} ว\u0e34", "Confirm in {0}s", "Konfirmasi {0} dtk", "{0}초 후 확인" }
		},
		{
			"waiting_other",
			new string[4] { "รออ\u0e35กฝ\u0e48ายย\u0e37นย\u0e31น", "Waiting for the other", "Menunggu", "상대 대기" }
		},
		{
			"cancel",
			new string[4] { "ยกเล\u0e34กเทรด", "Cancel trade", "Batalkan", "거래 취소" }
		},
		{
			"info_check",
			new string[4] { "ตรวจสอบไอเทมท\u0e31\u0e49งสองฝ\u0e48ายก\u0e48อนกดพร\u0e49อม", "Check both sides before pressing Ready", "Periksa kedua sisi", "양쪽 아이템을 확인하세요" }
		},
		{
			"info_changed",
			new string[4] { "แก\u0e49ไขข\u0e49อเสนอแล\u0e49ว — ท\u0e31\u0e49งสองฝ\u0e48ายต\u0e49องกดพร\u0e49อมและย\u0e37นย\u0e31นใหม\u0e48", "The offer changed — both must be ready and confirm again", "Penawaran berubah — siap ulang", "제안이 바뀌었습니다 — 다시 준비/확인" }
		},
		{
			"info_both_ready",
			new string[4] { "ท\u0e31\u0e49งสองฝ\u0e48ายพร\u0e49อมแล\u0e49ว — กดย\u0e37นย\u0e31นส\u0e38ดท\u0e49าย", "Both ready — press Final confirm", "Keduanya siap", "양쪽 준비 완료 — 최종 확인" }
		},
		{
			"info_confirmed",
			new string[4] { "ค\u0e38ณย\u0e37นย\u0e31นแล\u0e49ว — รออ\u0e35กฝ\u0e48าย (ถ\u0e49าข\u0e49อเสนอเปล\u0e35\u0e48ยน การย\u0e37นย\u0e31นจะถ\u0e39กล\u0e49าง)", "You confirmed — waiting (a change clears it)", "Kamu konfirmasi — menunggu", "확인 완료 — 상대 대기" }
		},
		{
			"info_fee",
			new string[4] { "ค\u0e48าธรรมเน\u0e35ยม T Stone {0}%", "T Stone fee {0}%", "Biaya {0}%", "수수료 {0}%" }
		},
		{
			"add_title",
			new string[4] { "เล\u0e37อกของท\u0e35\u0e48จะเทรด (ได\u0e49 {0} ช\u0e48อง · ชน\u0e34ด+เลเวลเด\u0e35ยวก\u0e31น = ช\u0e48องเด\u0e35ยว)", "Pick items to offer ({0} slots · same kind+level = one slot)", "Pilih barang ({0} slot)", "거래할 아이템 ({0}칸)" }
		},
		{
			"add_confirm",
			new string[4] { "วางในเทรด", "Offer", "Tawarkan", "올리기" }
		},
		{
			"add_how_many",
			new string[4] { "จำนวน {0} ท\u0e35\u0e48จะวาง (ม\u0e35 {1})", "How many {0} (you have {1})", "Berapa {0} (punya {1})", "{0} 몇 개 (보유 {1})" }
		},
		{
			"remove_how_many",
			new string[4] { "นำ {0} ออกก\u0e35\u0e48ช\u0e34\u0e49น (วางไว\u0e49 {1})", "Take back how many {0} (offered {1})", "Ambil kembali {0}", "{0} 몇 개 회수 ({1})" }
		},
		{
			"remove_main",
			new string[4] { "นำ <em>{0}</em> ออกจากเทรด?", "Take <em>{0}</em> back?", "Ambil <em>{0}</em> kembali?", "<em>{0}</em> 회수?" }
		},
		{
			"money_title",
			new string[4] { "T Stone ท\u0e35\u0e48ค\u0e38ณเสนอ", "T Stone you offer", "T Stone yang ditawarkan", "제안할 T Stone" }
		},
		{
			"confirm_main",
			new string[4] { "ย\u0e37นย\u0e31นการแลกเปล\u0e35\u0e48ยนก\u0e31บ <em>{0}</em>?", "Confirm the trade with <em>{0}</em>?", "Konfirmasi dengan <em>{0}</em>?", "<em>{0}</em>님과 거래 확정?" }
		},
		{
			"confirm_sub",
			new string[4] { "ค\u0e38ณให\u0e49: {0}\nค\u0e38ณได\u0e49ร\u0e31บ: {1}\n(ข\u0e49อเสนอร\u0e38\u0e48นท\u0e35\u0e48 {2} — เปล\u0e35\u0e48ยนก\u0e48อนจบ = ต\u0e49องย\u0e37นย\u0e31นใหม\u0e48)", "You give: {0}\nYou get: {1}\n(offer version {2} — any change needs a new confirm)", "Kamu beri: {0}\nKamu dapat: {1}", "주는 것: {0}\n받는 것: {1}\n(버전 {2})" }
		},
		{
			"cancel_main",
			new string[4] { "ยกเล\u0e34กเทรด? ของและเง\u0e34นท\u0e35\u0e48วางไว\u0e49จะค\u0e37นท\u0e31\u0e49งสองฝ\u0e48าย", "Cancel the trade? Everything goes back to both sides", "Batalkan? Semua dikembalikan", "거래 취소? 모두 돌려받습니다" }
		}
	};

	private static JObject TradeData => _previewTrade ?? CommunityExchange.Trade;

	public static bool BlocksOtherMenus
	{
		get
		{
			if (!_opening)
			{
				return IsOpen;
			}
			return true;
		}
	}

	public static bool IsOpen
	{
		get
		{
			if (_win != null)
			{
				return _win.gameObject.activeSelf;
			}
			return false;
		}
	}

	private static ItemData ItemFor(JObject row)
	{
		if (_previewTrade != null && _previewItems.TryGetValue(CommunityExchange.Str(row, "id"), out var value))
		{
			return value;
		}
		return CommunityExchange.ItemOf(row);
	}

	public static bool PreviewForTest()
	{
		if (CommunityExchange.Trade != null || CommunityCareerUI.BlocksOtherMenus || UIManager.MessageBox.IsShow)
		{
			return false;
		}
		InventorySystem inventorySystem = GameSystem<InventorySystem>.Instance();
		if (inventorySystem == null || inventorySystem.PlayerItemList == null)
		{
			return false;
		}
		_previewItems.Clear();
		JArray jArray = new JArray();
		foreach (ItemData item in (from x in inventorySystem.PlayerItemList
			where x != null
			group x by x.PrototypeId into x
			select x.First()).Take(3))
		{
			_previewItems[item.Id] = item;
			jArray.Add(new JObject
			{
				["id"] = item.Id,
				["proto"] = item.PrototypeId,
				["level"] = item.Level
			});
		}
		_previewTrade = new JObject
		{
			["tradeId"] = "ui-preview-only",
			["version"] = 1,
			["confirmLockSec"] = 0,
			["me"] = new JObject
			{
				["name"] = "UI Preview",
				["ready"] = true,
				["tstone"] = 120,
				["items"] = jArray
			},
			["other"] = new JObject
			{
				["name"] = "ต\u0e31วอย\u0e48าง UI",
				["ready"] = true,
				["tstone"] = 300,
				["items"] = jArray.DeepClone()
			}
		};
		ShowWindow();
		if (!IsOpen)
		{
			_previewTrade = null;
			_previewItems.Clear();
		}
		return IsOpen;
	}

	public static bool PreviewItemForTest()
	{
		if (!IsOpen || _previewTrade == null)
		{
			return false;
		}
		_win.OnSlot(mine: false, 0);
		return true;
	}

	public static void ShowWindow()
	{
		if (CommunityExchange.Trade != null && _previewTrade != null)
		{
			_previewTrade = null;
			_previewItems.Clear();
		}
		if (IsOpen)
		{
			Refresh();
		}
		else
		{
			if (CommunityCareerUI.BlocksOtherMenus || UIManager.MessageBox.IsShow)
			{
				return;
			}
			_opening = true;
			try
			{
				int num = 0;
				while (UIBase.CurrentUI != null && num < 32)
				{
					UIBase currentUI = UIBase.CurrentUI;
					currentUI.ForceClose();
					if (UIBase.CurrentUI == currentUI)
					{
						return;
					}
					num++;
				}
				if (!(UIBase.CurrentUI != null))
				{
					MenuListGroupBase menuListGroupBase = UIManager.FindScript<MenuListGroupBase>();
					if (menuListGroupBase != null && menuListGroupBase.IsOpened)
					{
						menuListGroupBase.Close();
					}
					if (Ensure())
					{
						TooltipBase.HideNonModal(null);
						_win.gameObject.SetActive(value: true);
						_win.Fit();
						Refresh();
					}
				}
			}
			finally
			{
				_opening = false;
			}
		}
	}

	public static void CloseWindow()
	{
		_previewTrade = null;
		_previewItems.Clear();
		if (_win != null)
		{
			if (_win._details != null)
			{
				_win._details.Hide();
			}
			_win.gameObject.SetActive(value: false);
			_win._busy = false;
			_win._shownVersion = -1;
			_win._anyReady = false;
			_win._changedNotice = false;
		}
	}

	public static void Refresh()
	{
		if (IsOpen)
		{
			try
			{
				_win.Redraw();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
	}

	public static bool Click(string key)
	{
		if (!IsOpen || !_buttons.TryGetValue(key ?? string.Empty, out var value) || value == null || value.Disabled || value.Clicked == null)
		{
			return false;
		}
		value.Clicked();
		return true;
	}

	public static List<string> Keys()
	{
		return new List<string>(_buttons.Keys);
	}

	private static bool Ensure()
	{
		if (_win != null)
		{
			return true;
		}
		if (!Singleton<UIManager>.HasInstance())
		{
			return false;
		}
		UIRoot uIRoot = Singleton<UIManager>.Instance().UIRoot;
		if (uIRoot == null || !FindTemplates())
		{
			CommunityExchange.Trace("trade window: no " + ((uIRoot == null) ? "UIRoot" : ("templates btn=" + (_btnTpl != null) + " lbl=" + (_lblTpl != null) + " icon=" + (_iconTpl != null))));
			return false;
		}
		GameObject gameObject = uIRoot.gameObject.AddChild();
		gameObject.name = "CommunityTrade";
		_win = gameObject.AddComponent<CommunityTradeUI>();
		try
		{
			_win.Build();
			CommunityExchange.Trace("trade window built");
		}
		catch (Exception ex)
		{
			CommunityExchange.Trace("trade window build failed: " + ex.GetType().Name + " " + ex.Message);
			UnityEngine.Debug.LogException(ex);
			UnityEngine.Object.Destroy(gameObject);
			_win = null;
			_buttons.Clear();
			return false;
		}
		gameObject.SetActive(value: false);
		return true;
	}

	private static bool FindTemplates()
	{
		if (_btnTpl != null && _lblTpl != null && _iconTpl != null)
		{
			return true;
		}
		MarketGroup marketGroup = UIManager.FindScript<MarketGroup>();
		if (marketGroup != null)
		{
			FindMarketTemplates(marketGroup);
		}
		return FindRootTemplates();
	}

	private static void FindMarketTemplates(MarketGroup marketGroup)
	{
		SelectableButton[] componentsInChildren = marketGroup.GetComponentsInChildren<SelectableButton>(includeInactive: true);
		foreach (SelectableButton selectableButton in componentsInChildren)
		{
			if (selectableButton.name == "BuyButton" || (_btnTpl == null && selectableButton.name == "ReceiveAllButton"))
			{
				_btnTpl = selectableButton;
			}
		}
		if (_btnTpl == null)
		{
			SelectableButton[] componentsInChildren2 = marketGroup.GetComponentsInChildren<SelectableButton>(includeInactive: true);
			if (componentsInChildren2.Length != 0)
			{
				_btnTpl = componentsInChildren2[0];
			}
		}
		UILabel[] componentsInChildren3 = marketGroup.GetComponentsInChildren<UILabel>(includeInactive: true);
		foreach (UILabel uILabel in componentsInChildren3)
		{
			if ((uILabel.trueTypeFont != null || uILabel.bitmapFont != null) && !(uILabel is UISpriteLabel) && (_lblTpl == null || uILabel.fontSize == 26))
			{
				_lblTpl = uILabel;
			}
		}
		UISprite[] componentsInChildren4 = marketGroup.GetComponentsInChildren<UISprite>(includeInactive: true);
		foreach (UISprite uISprite in componentsInChildren4)
		{
			if (!string.IsNullOrEmpty(uISprite.spriteName) && !_surfaces.ContainsKey(uISprite.spriteName))
			{
				_surfaces[uISprite.spriteName] = uISprite;
			}
		}
		FactionGroup factionGroup = UIManager.FindScript<FactionGroup>();
		if (factionGroup != null)
		{
			_iconTpl = (from x in factionGroup.GetComponentsInChildren<Durango.UI.Control.ItemIconWidget>(includeInactive: true)
				where x.GetType() == typeof(Durango.UI.Control.ItemIconWidget)
				orderby x.width descending
				select x).FirstOrDefault();
		}
	}

	private static bool FindRootTemplates()
	{
		UIRoot uIRoot = Singleton<UIManager>.Instance().UIRoot;
		if (uIRoot != null)
		{
			if (_btnTpl == null)
			{
				SelectableButton[] componentsInChildren = uIRoot.GetComponentsInChildren<SelectableButton>(includeInactive: true);
				if (componentsInChildren.Length != 0)
				{
					_btnTpl = componentsInChildren[0];
				}
			}
			if (_lblTpl == null)
			{
				UILabel[] componentsInChildren2 = uIRoot.GetComponentsInChildren<UILabel>(includeInactive: true);
				foreach (UILabel uILabel in componentsInChildren2)
				{
					if ((uILabel.trueTypeFont != null || uILabel.bitmapFont != null) && !(uILabel is UISpriteLabel))
					{
						_lblTpl = uILabel;
						break;
					}
				}
			}
		}
		if (_btnTpl == null || _lblTpl == null || _iconTpl == null)
		{
			UnityEngine.Debug.Log("[Exchange] trade window: market templates missing btn=" + (_btnTpl != null) + " lbl=" + (_lblTpl != null) + " icon=" + (_iconTpl != null));
		}
		if (_btnTpl != null && _lblTpl != null)
		{
			return _iconTpl != null;
		}
		return false;
	}

	private void Build()
	{
		_panel = base.gameObject.AddComponent<UIPanel>();
		_panel.depth = 5900;
		UISprite uISprite = Sprite(base.gameObject, "bg_white", 0, 0, 4000, 4000, 0);
		uISprite.color = new Color(0f, 0f, 0f, 0.18f);
		NGUITools.AddWidgetCollider(uISprite.gameObject);
		Sprite(base.gameObject, "bg_roundbox2_space", 0, 0, 1120, 640, 1).color = new Color(0f, 0f, 0f, 0.47058824f);
		Sprite(base.gameObject, "bg_box_grunge", 0, 0, 1120, 640, 2).color = Color.black;
		Sprite(base.gameObject, "bg_roundbox2_half", 0, 276, 1112, 80, 3).color = new Color(0f, 0f, 0f, 28f / 51f);
		Sprite(base.gameObject, "bg_dotline_white2", 0, 234, 1080, 2, 4).color = new Color(1f, 1f, 1f, 0.12f);
		_detailsLayer = base.gameObject.AddChild();
		_detailsLayer.name = "TradeItemDetails";
		_detailsLayer.AddComponent<UIPanel>().depth = 7000;
		_title = Label(base.gameObject, string.Empty, -528, 290, 30, new Color(1f, 0.85f, 0.45f), 760, UIWidget.Pivot.Left);
		_sub = Label(base.gameObject, string.Empty, -528, 256, 20, new Color(0.75f, 0.75f, 0.75f), 760, UIWidget.Pivot.Left);
		Button(base.gameObject, "close", "X", 504, 276, 84, 64, AskCancel);
		BuildSide(_me, -286, mine: true);
		BuildSide(_other, 286, mine: false);
		Sprite(base.gameObject, "bg_dotline_white2", 0, -188, 1080, 2, 4).color = new Color(1f, 1f, 1f, 0.12f);
		_info = Label(base.gameObject, string.Empty, 0, -212, 20, new Color(1f, 0.82f, 0.4f), 1040, UIWidget.Pivot.Center);
		_bCancel = Button(base.gameObject, "cancel", Text("cancel"), -400, -268, 260, 72, AskCancel);
		_bReady = Button(base.gameObject, "ready", Text("ready"), 120, -268, 280, 72, ToggleReady);
		_bConfirm = Button(base.gameObject, "confirm", Text("confirm"), 400, -268, 260, 72, AskConfirm);
		try
		{
			_bConfirm.SetStyle(PresetButton.Style.Solid);
			_bCancel.SetStyle(PresetButton.Style.Border);
		}
		catch (Exception)
		{
		}
	}

	private void BuildSide(SideView side, int cx, bool mine)
	{
		int num = 200;
		Sprite(base.gameObject, "bg_roundbox2_half", cx, num, 520, 52, 5).color = new Color(0f, 0f, 0f, 0.4f);
		side.Name = Label(base.gameObject, string.Empty, cx - 260 + 18, num, 26, Color.white, 300, UIWidget.Pivot.Left);
		side.Status = Label(base.gameObject, string.Empty, cx + 260 - 18, num, 22, new Color(0.7f, 0.7f, 0.7f), 200, UIWidget.Pivot.Right);
		int num2 = Math.Max(1, CommunityExchange.TradeMaxSlots);
		int num3 = Math.Min(num2, 3);
		int num4 = (num2 + num3 - 1) / num3;
		int num5 = 134;
		for (int i = 0; i < num2; i++)
		{
			int num6 = i % num3;
			int num7 = i / num3;
			int num8 = cx - (num3 - 1) * num5 / 2 + num6 * num5;
			int num9 = num - 104 - num7 * num5;
			SlotView slotView = new SlotView();
			slotView.Go = base.gameObject.AddChild();
			slotView.Go.transform.localPosition = new Vector3(num8, num9, 0f);
			slotView.Frame = Sprite(slotView.Go, "bg_roundbox2_space", 0, 0, 118, 118, 6);
			slotView.Frame.color = new Color(0f, 0f, 0f, 0.39f);
			NGUITools.AddWidgetCollider(slotView.Frame.gameObject);
			int index = i;
			UIEventListener.Get(slotView.Frame.gameObject).onClick = delegate
			{
				OnSlot(mine, index);
			};
			slotView.Icon = UnityEngine.Object.Instantiate(_iconTpl.gameObject, slotView.Go.transform).GetComponent<Durango.UI.Control.ItemIconWidget>();
			slotView.Icon.SetAnchor((Transform)null);
			slotView.Icon.transform.localPosition = Vector3.zero;
			slotView.Icon.transform.localScale = Vector3.one * (118f / (float)slotView.Icon.width);
			int num10 = slotView.Icon.GetComponentsInChildren<UIWidget>(includeInactive: true).Min((UIWidget x) => x.depth);
			UIWidget[] componentsInChildren = slotView.Icon.GetComponentsInChildren<UIWidget>(includeInactive: true);
			for (int num11 = 0; num11 < componentsInChildren.Length; num11++)
			{
				componentsInChildren[num11].depth += 8 - num10;
			}
			Transform transform = slotView.Icon.transform.Find("bag");
			if (transform != null)
			{
				transform.gameObject.SetActive(value: false);
			}
			NGUITools.AddWidgetCollider(slotView.Icon.gameObject);
			slotView.Plus = Label(slotView.Go, mine ? "+" : string.Empty, 0, 0, 44, new Color(0.6f, 0.6f, 0.6f), 118, UIWidget.Pivot.Center);
			side.Slots.Add(slotView);
		}
		int y = num - 104 - num4 * num5 + 30;
		Sprite(base.gameObject, "tstone_icon", cx - 260 + 36, y, 40, 40, 6);
		side.TStone = Label(base.gameObject, "0", cx - 260 + 66, y, 26, Color.white, 120, UIWidget.Pivot.Left);
		if (mine)
		{
			_bMoney = Button(base.gameObject, "money", Text("money"), cx + 260 - 90, y, 160, 56, AskMoney);
			_bAdd = Button(base.gameObject, "add", Text("add"), cx + 260 - 262, y, 160, 56, AskAdd);
		}
	}

	private UISprite Sprite(GameObject parent, string name, int x, int y, int w, int h, int depth)
	{
		UISprite uISprite = parent.AddSprite(name, depth);
		if (_surfaces.TryGetValue(name, out var value))
		{
			uISprite.spriteName = value.spriteName;
			uISprite.type = value.type;
			uISprite.color = value.color;
		}
		uISprite.width = Mathf.Max(2, w);
		uISprite.height = Mathf.Max(2, h);
		uISprite.transform.localPosition = new Vector3(x, y, 0f);
		return uISprite;
	}

	private UILabel Label(GameObject parent, string text, int x, int y, int size, Color color, int width, UIWidget.Pivot pivot)
	{
		GameObject obj = parent.AddChild();
		obj.name = "Label";
		UILabel uILabel = obj.AddComponent<UILabel>();
		uILabel.bitmapFont = _lblTpl.bitmapFont;
		if (_lblTpl.trueTypeFont != null)
		{
			uILabel.trueTypeFont = _lblTpl.trueTypeFont;
		}
		uILabel.fontStyle = _lblTpl.fontStyle;
		uILabel.effectStyle = _lblTpl.effectStyle;
		uILabel.effectColor = _lblTpl.effectColor;
		uILabel.SetAnchor((Transform)null);
		uILabel.transform.localScale = Vector3.one;
		uILabel.pivot = pivot;
		uILabel.alignment = pivot switch
		{
			UIWidget.Pivot.Right => NGUIText.Alignment.Right,
			UIWidget.Pivot.Left => NGUIText.Alignment.Left,
			_ => NGUIText.Alignment.Center,
		};
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		uILabel.width = width;
		uILabel.height = size + 14;
		uILabel.fontSize = size;
		uILabel.color = color;
		uILabel.depth = 20;
		uILabel.text = text;
		uILabel.transform.localPosition = new Vector3(x, y, 0f);
		return uILabel;
	}

	private SelectableButton Button(GameObject parent, string key, string text, int x, int y, int w, int h, Action onClick)
	{
		GameObject obj = UnityEngine.Object.Instantiate(_btnTpl.gameObject, parent.transform);
		obj.name = "Trade_" + key;
		UIRect component = obj.GetComponent<UIRect>();
		if (component != null)
		{
			component.SetAnchor((Transform)null);
		}
		obj.SetActive(value: true);
		obj.transform.localPosition = new Vector3(x, y, 0f);
		obj.transform.localScale = Vector3.one;
		SelectableButton component2 = obj.GetComponent<SelectableButton>();
		UIWidget[] componentsInChildren = obj.GetComponentsInChildren<UIWidget>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].depth += 10;
		}
		try
		{
			component2.SetDimensions(w, h);
		}
		catch (Exception)
		{
		}
		component2.Text = text;
		component2.Disabled = false;
		component2.Clicked = delegate
		{
			try
			{
				onClick();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		};
		_buttons[key] = component2;
		return component2;
	}

	private void Fit()
	{
		float num = Singleton<UIManager>.Instance().UIRoot.activeHeight;
		float num2 = num * (float)Screen.width / Math.Max(1f, Screen.height);
		base.transform.localScale = Vector3.one * Mathf.Min(1f, (num - 32f) / 640f, (num2 - 32f) / 1120f);
	}

	private void Update()
	{
		Fit();
		if (Time.realtimeSinceStartup >= _nextTick && _lockUntil > 0f)
		{
			_nextTick = Time.realtimeSinceStartup + 0.25f;
			Redraw();
		}
	}

	private void Redraw()
	{
		JObject tradeData = TradeData;
		if (tradeData != null)
		{
			JObject jObject = tradeData["me"] as JObject;
			JObject jObject2 = tradeData["other"] as JObject;
			int num = (int)CommunityExchange.Num(tradeData, "version", 0.0);
			if (num != _shownVersion)
			{
				_changedNotice = _shownVersion >= 0 && _anyReady;
				_shownVersion = num;
				_lockUntil = Time.realtimeSinceStartup + (float)CommunityExchange.Num(tradeData, "confirmLockSec", 0.0);
			}
			string text = CommunityExchange.Str(jObject2, "name");
			_title.text = Text("title", text);
			_sub.text = ((_previewTrade != null) ? "ต\u0e31วอย\u0e48าง UI · ไม\u0e48ม\u0e35การแลกเปล\u0e35\u0e48ยนของจร\u0e34ง" : Text("sub", num));
			DrawSide(_me, jObject, num, mine: true);
			DrawSide(_other, jObject2, num, mine: false);
			bool flag = CommunityExchange.Bool(jObject, "ready");
			bool flag2 = CommunityExchange.Bool(jObject2, "ready");
			bool flag3 = (int)CommunityExchange.Num(jObject, "confirmedVersion", -1.0) == num;
			_anyReady = flag || flag2;
			if (_anyReady)
			{
				_changedNotice = false;
			}
			float num2 = Mathf.Max(0f, _lockUntil - Time.realtimeSinceStartup);
			if (num2 <= 0f)
			{
				_lockUntil = 0f;
			}
			_bReady.Text = (flag ? Text("unready") : Text("ready"));
			_bReady.Disabled = _busy || flag3;
			_bConfirm.Disabled = _busy || !flag || !flag2 || flag3 || num2 > 0f;
			_bConfirm.Text = (flag3 ? Text("waiting_other") : ((num2 > 0f) ? Text("confirm_in", Mathf.CeilToInt(num2)) : Text("confirm")));
			_bAdd.Disabled = _busy;
			_bMoney.Disabled = _busy;
			string text2 = (flag3 ? Text("info_confirmed") : ((flag && flag2) ? Text("info_both_ready") : ((!_changedNotice) ? Text("info_check") : Text("info_changed"))));
			if (CommunityExchange.TradeFeeRatio > 0.0)
			{
				text2 = text2 + " · " + Text("info_fee", (CommunityExchange.TradeFeeRatio * 100.0).ToString("0.#"));
			}
			_info.text = text2;
		}
	}

	private void DrawSide(SideView side, JObject s, int version, bool mine)
	{
		side.Name.text = (mine ? Text("you") : CommunityExchange.Str(s, "name"));
		bool flag = CommunityExchange.Bool(s, "ready");
		bool flag2 = (int)CommunityExchange.Num(s, "confirmedVersion", -1.0) == version;
		side.Status.text = (flag2 ? Text("st_confirmed") : (flag ? Text("st_ready") : Text("st_choosing")));
		side.Status.color = (flag2 ? new Color(1f, 0.82f, 0.35f) : (flag ? new Color(0.45f, 0.9f, 0.5f) : new Color(0.7f, 0.7f, 0.7f)));
		side.TStone.text = ((long)CommunityExchange.Num(s, "tstone", 0.0)).ToString("N0") + " T";
		List<SlotView> list = new List<SlotView>();
		JArray jArray = ((s != null) ? (s["items"] as JArray) : null);
		Dictionary<string, SlotView> dictionary = new Dictionary<string, SlotView>(StringComparer.Ordinal);
		if (jArray != null)
		{
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				if (item is JObject jObject)
				{
					string text = CommunityExchange.Str(jObject, "proto");
					int level = (int)CommunityExchange.Num(jObject, "level", 1.0);
					string key = text + "@" + level;
					if (!dictionary.TryGetValue(key, out var value))
					{
						value = (dictionary[key] = new SlotView
						{
							Proto = text,
							Level = level,
							Item = ItemFor(jObject)
						});
						list.Add(value);
					}
					value.Ids.Add(CommunityExchange.Str(jObject, "id"));
				}
			}
		}
		for (int i = 0; i < side.Slots.Count; i++)
		{
			SlotView slotView2 = side.Slots[i];
			SlotView slotView3 = ((i < list.Count) ? list[i] : null);
			slotView2.Proto = ((slotView3 != null) ? slotView3.Proto : string.Empty);
			slotView2.Level = slotView3?.Level ?? 0;
			slotView2.Ids = ((slotView3 != null) ? slotView3.Ids : new List<string>());
			slotView2.Item = slotView3?.Item;
			bool flag3 = slotView2.Item != null;
			slotView2.Icon.gameObject.SetActive(flag3);
			if (flag3)
			{
				int slotIndex = i;
				slotView2.Icon.Set(slotView2.Item, slotView2.Ids.Count, alwaysShowCount: true, delegate
				{
					OnSlot(mine, slotIndex);
				});
				Transform transform = slotView2.Icon.transform.Find("Lv");
				if (transform != null)
				{
					transform.gameObject.SetActive(value: true);
				}
			}
			slotView2.Frame.gameObject.SetActive(!flag3);
			slotView2.Plus.gameObject.SetActive(mine && !flag3);
		}
	}

	private static string TradeId()
	{
		if (TradeData == null)
		{
			return string.Empty;
		}
		return CommunityExchange.Str(TradeData, "tradeId");
	}

	private static int Version()
	{
		if (TradeData == null)
		{
			return 0;
		}
		return (int)CommunityExchange.Num(TradeData, "version", 0.0);
	}

	private void Send(byte kind, JObject body, string okText = null)
	{
		if (_previewTrade != null)
		{
			if (kind == 27)
			{
				CloseWindow();
			}
			else
			{
				UIManager.SystemMsg("ต\u0e31วอย\u0e48าง UI เท\u0e48าน\u0e31\u0e49น — ไม\u0e48ส\u0e48งรายการซ\u0e37\u0e49อขาย");
			}
			return;
		}
		body["tradeId"] = TradeId();
		_busy = true;
		Redraw();
		CommunityExchange.Request(kind, body, delegate(JObject r)
		{
			_busy = false;
			if (r == null)
			{
				UIManager.SystemMsg(CommunityExchange.Text("no_reply"));
			}
			else if (!CommunityExchange.Bool(r, "ok"))
			{
				UIManager.MessageBox.Show(CommunityExchange.Str(r, "text"));
			}
			else if (okText != null)
			{
				UIManager.SystemMsg(okText);
			}
			Refresh();
		});
	}

	private void OnSlot(bool mine, int index)
	{
		SideView sideView = (mine ? _me : _other);
		if (index < 0 || index >= sideView.Slots.Count)
		{
			return;
		}
		SlotView slotView = sideView.Slots[index];
		if (slotView.Item == null)
		{
			if (mine)
			{
				AskAdd();
			}
			return;
		}
		if (!mine)
		{
			if (_details != null)
			{
				_details.Hide();
			}
			ItemInfoTooltip itemInfoTooltip = UIManager.Popup.Tooltip<ItemInfoTooltip>();
			_details = itemInfoTooltip;
			Transform originalParent = itemInfoTooltip.transform.parent;
			Vector3 originalPosition = itemInfoTooltip.transform.localPosition;
			Vector3 originalScale = itemInfoTooltip.transform.localScale;
			itemInfoTooltip.transform.SetParent(_detailsLayer.transform, worldPositionStays: false);
			itemInfoTooltip.transform.localScale = Vector3.one;
			itemInfoTooltip.Widget.ParentHasChanged();
			itemInfoTooltip.AddOnFinished(delegate
			{
				if (!(itemInfoTooltip == null))
				{
					itemInfoTooltip.transform.SetParent(originalParent, worldPositionStays: false);
					itemInfoTooltip.transform.localPosition = originalPosition;
					itemInfoTooltip.transform.localScale = originalScale;
					itemInfoTooltip.Widget.ParentHasChanged();
				}
			});
			itemInfoTooltip.Set(slotView.Item);
			itemInfoTooltip.Direction = TooltipBase.TooltipDirection.Horizontal;
			itemInfoTooltip.Show(slotView.Icon, Vector2.zero);
			return;
		}
		List<string> ids = new List<string>(slotView.Ids);
		Action<long> take = delegate(long n)
		{
			JObject jObject = new JObject();
			JArray jArray = new JArray();
			for (int i = 0; i < ids.Count && i < n; i++)
			{
				jArray.Add(ids[i]);
			}
			jObject["ids"] = jArray;
			Send(23, jObject);
		};
		if (ids.Count > 1)
		{
			UIManager.Popup.Tooltip<NumberInputPopup>().Show(ids.Count, Currency.Invalid, Text("remove_how_many", slotView.Item.Name, ids.Count), delegate(long n)
			{
				take(Math.Max(1L, Math.Min(n, ids.Count)));
			}, ids.Count);
			return;
		}
		UIManager.MessageBox.Show(Text("remove_main", slotView.Item.Name), delegate(bool ok)
		{
			if (ok)
			{
				take(1L);
			}
		});
	}

	private static bool Offered(string id)
	{
		JArray jArray = ((TradeData != null) ? ((TradeData["me"] as JObject)?["items"] as JArray) : null);
		if (jArray != null)
		{
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				if (CommunityExchange.Str(item as JObject, "id") == id)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool Tradable(ItemData item)
	{
		if (item != null && item.Tradable && !item.IsEquipments && !item.Locked)
		{
			return !Offered(item.Id);
		}
		return false;
	}

	private void AskAdd()
	{
		PopupItemSelector popupItemSelector = UIManager.Popup.Tooltip<PopupItemSelector>();
		popupItemSelector.MyInventory().Filter(Tradable).SelectableCount(Math.Max(1, CommunityExchange.TradeMaxPieces))
			.ConfirmText(Text("add_confirm"))
			.OnConfirmed(delegate(IList<ItemData> items)
			{
				if (items != null && items.Count != 0)
				{
					List<ItemData> list = new List<ItemData>(items);
					if (list.Count == 1)
					{
						ItemData one = list[0];
						List<ItemData> same = new List<ItemData>();
						foreach (ItemData playerItem in GameSystem<InventorySystem>.Instance().PlayerItemList)
						{
							if (Tradable(playerItem) && playerItem.PrototypeId == one.PrototypeId && playerItem.Level == one.Level)
							{
								same.Add(playerItem);
							}
						}
						if (same.Count > 1)
						{
							int num = Math.Min(same.Count, CommunityExchange.TradeMaxPieces);
							UIManager.Popup.Tooltip<NumberInputPopup>().Show(1L, Currency.Invalid, Text("add_how_many", one.Name, same.Count), delegate(long n)
							{
								List<ItemData> list2 = new List<ItemData> { one };
								foreach (ItemData item in same)
								{
									if (list2.Count >= n)
									{
										break;
									}
									if (item.Id != one.Id)
									{
										list2.Add(item);
									}
								}
								SendAdd(list2);
							}, num);
							return;
						}
					}
					SendAdd(list);
				}
			})
			.Title(Text("add_title", CommunityExchange.TradeMaxSlots));
		popupItemSelector.Show();
	}

	private void SendAdd(List<ItemData> items)
	{
		JObject jObject = new JObject();
		JArray jArray = new JArray();
		foreach (ItemData item in items)
		{
			jArray.Add(item.Id);
		}
		jObject["itemIds"] = jArray;
		Send(22, jObject);
	}

	private void AskMoney()
	{
		long num = (long)CommunityExchange.Num((TradeData != null) ? (TradeData["me"] as JObject) : null, "tstone", 0.0);
		long maxValue = InventorySystem.Wallet.GetBalance(Currency.TStone) + num;
		UIManager.Popup.Tooltip<NumberInputPopup>().Show(num, Currency.TStone, Text("money_title"), delegate(long amount)
		{
			Send(24, new JObject { ["amount"] = Math.Max(0L, amount) });
		}, maxValue);
	}

	private void ToggleReady()
	{
		JObject o = ((TradeData != null) ? (TradeData["me"] as JObject) : null);
		JObject jObject = new JObject();
		jObject["version"] = Version();
		jObject["ready"] = !CommunityExchange.Bool(o, "ready");
		Send(25, jObject);
	}

	private void AskConfirm()
	{
		JObject tradeData = TradeData;
		if (tradeData == null)
		{
			return;
		}
		int version = Version();
		string text = Summary(tradeData["me"] as JObject);
		string text2 = Summary(tradeData["other"] as JObject);
		UIManager.MessageBox.Show(Text("confirm_main", CommunityExchange.Str(tradeData["other"] as JObject, "name")), Text("confirm_sub", text, text2, version), delegate(bool ok)
		{
			if (ok)
			{
				JObject body = new JObject { ["version"] = version };
				Send(26, body);
			}
		});
	}

	private static string Summary(JObject side)
	{
		List<string> list = new List<string>();
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
		Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.Ordinal);
		JArray jArray = ((side != null) ? (side["items"] as JArray) : null);
		if (jArray != null)
		{
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				JObject jObject = item as JObject;
				string key = CommunityExchange.Str(jObject, "proto") + "@" + CommunityExchange.Str(jObject, "level");
				dictionary.TryGetValue(key, out var value);
				dictionary[key] = value + 1;
				if (!dictionary2.ContainsKey(key))
				{
					ItemData itemData = ItemFor(jObject);
					dictionary2[key] = ((itemData != null) ? itemData.Name : CommunityExchange.Str(jObject, "proto"));
				}
			}
		}
		foreach (KeyValuePair<string, int> item2 in dictionary)
		{
			list.Add(dictionary2[item2.Key] + " ×" + item2.Value);
		}
		long num = (long)CommunityExchange.Num(side, "tstone", 0.0);
		if (num > 0)
		{
			list.Add(CommunityExchange.TStoneText(num));
		}
		if (list.Count != 0)
		{
			return string.Join(", ", list.ToArray());
		}
		return "—";
	}

	private void AskCancel()
	{
		UIManager.MessageBox.Show(Text("cancel_main"), delegate(bool ok)
		{
			if (ok)
			{
				Send(27, new JObject());
			}
		});
	}

	public static string Text(string key, params object[] args)
	{
		if (!Texts.TryGetValue(key, out var value))
		{
			return key;
		}
		string text = CommunityDiscordLink.Lang switch
		{
			"th" => value[0],
			"id" => value[2],
			"ko" => value[3],
			_ => value[1],
		};
		if (args == null || args.Length == 0)
		{
			return text;
		}
		try
		{
			return string.Format(text, args);
		}
		catch (FormatException)
		{
			return text;
		}
	}
}
