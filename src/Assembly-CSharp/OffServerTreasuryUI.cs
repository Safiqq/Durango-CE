using System;
using System.Collections.Generic;
using System.Globalization;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using Shared.Economy;
using UnityEngine;

public class OffServerTreasuryUI : MonoBehaviour
{
	private const int Depth = 5900;

	private const int W = 650;

	private const int H = 670;

	private static OffServerTreasuryUI _win;

	private static readonly Dictionary<string, SelectableButton> _buttons = new Dictionary<string, SelectableButton>(StringComparer.Ordinal);

	private static SelectableButton _btnBorderTpl;

	private static SelectableButton _btnSolidTpl;

	private static UILabel _lblTpl;

	private static readonly Dictionary<string, UISprite> _surfaces = new Dictionary<string, UISprite>();

	private static readonly Color Beige = new Color(0.86f, 0.82f, 0.7f);

	private static readonly Color Gold = new Color(1f, 0.85f, 0.45f);

	private static readonly Color Grey = new Color(0.68f, 0.68f, 0.66f);

	private static readonly Color Green = new Color(0.55f, 0.85f, 0.45f);

	private static readonly Color Red = new Color(1f, 0.47f, 0.4f);

	private long _want;

	private bool _loading;

	private bool _fromShop;

	private UILabel _title;

	private UILabel _balance;

	private UILabel _balanceTitle;

	private UILabel _maxTitle;

	private UILabel _max;

	private UISprite _maxIcon;

	private UILabel _amountTitle;

	private UILabel _have;

	private UISprite _haveIcon;

	private UILabel _amount;

	private UISprite _amountIcon;

	private UILabel _payTitle;

	private UILabel _feeTitle;

	private UILabel _getTitle;

	private UILabel _pay;

	private UILabel _fee;

	private UILabel _get;

	private UILabel _status;

	private UILabel _nowTitle;

	private UILabel _afterTitle;

	private UILabel _nowT;

	private UILabel _nowC;

	private UILabel _afterT;

	private UILabel _afterC;

	private SelectableButton _bCancel;

	private SelectableButton _bTrade;

	private readonly List<SelectableButton> _quick = new List<SelectableButton>();

	public static string LastText = string.Empty;

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

	public static long Want
	{
		get
		{
			if (!(_win != null))
			{
				return 0L;
			}
			return _win._want;
		}
	}

	public static void Open()
	{
		if (IsOpen)
		{
			Refresh();
			OffServerTreasury.Fetch(OnFetched);
		}
		else if (!UIManager.MessageBox.IsShow && Ensure())
		{
			TooltipBase.HideNonModal(null);
			_win._want = 0L;
			_win._loading = true;
			ShopGroup shopGroup = UIManager.FindScript<ShopGroup>();
			_win._fromShop = shopGroup != null && shopGroup.IsOpened;
			_win.gameObject.SetActive(value: true);
			_win.Fit();
			Refresh();
			OffServerTreasury.Fetch(OnFetched);
		}
	}

	public static void Close()
	{
		if (_win != null && _win.gameObject.activeSelf)
		{
			_win.gameObject.SetActive(value: false);
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

	public static void SetAmountForTest(long coin)
	{
		if (IsOpen)
		{
			_win._want = Math.Max(0L, coin);
			Refresh();
		}
	}

	private static void OnFetched(bool ok)
	{
		if (_win != null)
		{
			_win._loading = false;
		}
		Refresh();
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
			UnityEngine.Debug.Log("[OffServer] m-treasury window: no " + ((uIRoot == null) ? "UIRoot" : ("templates border=" + (_btnBorderTpl != null) + " solid=" + (_btnSolidTpl != null) + " lbl=" + (_lblTpl != null))));
			return false;
		}
		GameObject gameObject = uIRoot.gameObject.AddChild();
		gameObject.name = "OffServerTreasury";
		_win = gameObject.AddComponent<OffServerTreasuryUI>();
		try
		{
			_win.Build();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
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
		if (_btnBorderTpl != null && _btnSolidTpl != null && _lblTpl != null)
		{
			return true;
		}
		ShopCoinAcceptPopup shopCoinAcceptPopup = UIManager.Popup.Tooltip<ShopCoinAcceptPopup>();
		TransferCoinPopup transferCoinPopup = UIManager.Popup.Tooltip<TransferCoinPopup>();
		MonoBehaviour[] array = new MonoBehaviour[2] { shopCoinAcceptPopup, transferCoinPopup };
		foreach (MonoBehaviour monoBehaviour in array)
		{
			if (monoBehaviour == null)
			{
				continue;
			}
			SelectableButton[] componentsInChildren = monoBehaviour.GetComponentsInChildren<SelectableButton>(includeInactive: true);
			foreach (SelectableButton selectableButton in componentsInChildren)
			{
				if (_btnBorderTpl == null && selectableButton.name == "Cancel")
				{
					_btnBorderTpl = selectableButton;
				}
				if (_btnSolidTpl == null && (selectableButton.name == "Accept" || selectableButton.name == "Confirm"))
				{
					_btnSolidTpl = selectableButton;
				}
			}
			UILabel[] componentsInChildren2 = monoBehaviour.GetComponentsInChildren<UILabel>(includeInactive: true);
			foreach (UILabel uILabel in componentsInChildren2)
			{
				if (_lblTpl == null && (uILabel.trueTypeFont != null || uILabel.bitmapFont != null) && !(uILabel is UISpriteLabel))
				{
					_lblTpl = uILabel;
				}
			}
			UISprite[] componentsInChildren3 = monoBehaviour.GetComponentsInChildren<UISprite>(includeInactive: true);
			foreach (UISprite uISprite in componentsInChildren3)
			{
				if (!string.IsNullOrEmpty(uISprite.spriteName) && !_surfaces.ContainsKey(uISprite.spriteName))
				{
					_surfaces[uISprite.spriteName] = uISprite;
				}
			}
		}
		if (_btnSolidTpl == null)
		{
			_btnSolidTpl = _btnBorderTpl;
		}
		if (_btnBorderTpl == null)
		{
			_btnBorderTpl = _btnSolidTpl;
		}
		if (_btnBorderTpl != null && _btnSolidTpl != null)
		{
			return _lblTpl != null;
		}
		return false;
	}

	private void Build()
	{
		base.gameObject.AddComponent<UIPanel>().depth = 5900;
		UISprite uISprite = Sprite("bg_white", 0, 0, 4000, 4000, 0);
		uISprite.transform.localPosition = Vector3.zero;
		uISprite.color = new Color(0f, 0f, 0f, 0.55f);
		NGUITools.AddWidgetCollider(uISprite.gameObject);
		Box(0, 0, 650, 670, new Color(0.11764706f, 9f / 85f, 7f / 85f, 1f), 1);
		Sprite("bg_tooltip_grunge", 0, 0, 650, 670, 2);
		Sprite("bg_tooltip_grunge2", 0, 0, 650, 670, 2);
		Box(0, 0, 650, 61, new Color(0f, 0f, 0f, 0.45f), 3);
		_title = Label(OffServerTreasury.Text("title"), 24, 8, 600, 46, 30, Color.white, UIWidget.Pivot.Left);
		Sprite("bg_dotline_white2", 0, 60, 650, 2, 4).color = new Color(1f, 1f, 1f, 0.12f);
		Box(0, 61, 650, 96, new Color(0.1254902f, 0.11372549f, 0.08627451f, 0.94f), 3);
		Sprite("tstone_icon_clan", 22, 82, 62, 51, 5);
		_balanceTitle = Label(string.Empty, 100, 66, 300, 28, 20, Beige, UIWidget.Pivot.Left);
		_balance = Label(string.Empty, 100, 94, 300, 50, 40, Gold, UIWidget.Pivot.Left);
		_maxTitle = Label(string.Empty, 400, 70, 228, 26, 18, Grey, UIWidget.Pivot.Right);
		_max = Label(string.Empty, 400, 98, 228, 40, 28, Color.white, UIWidget.Pivot.Right);
		_maxIcon = Sprite(Inventory.GetIcon(Currency.Coin), 0, 0, 30, 30, 6);
		Sprite("bg_dotline_white2", 0, 156, 650, 2, 4).color = new Color(1f, 1f, 1f, 0.12f);
		_amountTitle = Label(string.Empty, 20, 168, 330, 30, 22, new Color(1f, 1f, 1f, 0.82f), UIWidget.Pivot.Left);
		_have = Label(string.Empty, 330, 168, 300, 30, 22, Color.white, UIWidget.Pivot.Right);
		_haveIcon = Sprite(Inventory.GetIcon(Currency.Coin), 0, 0, 26, 26, 6);
		UISprite uISprite2 = Sprite("bg_roundbox2", 20, 204, 610, 56, 4);
		uISprite2.color = new Color(0f, 0f, 0f, 0.55f);
		NGUITools.AddWidgetCollider(uISprite2.gameObject);
		UIEventListener.Get(uISprite2.gameObject).onClick = delegate
		{
			AskAmount();
		};
		_amountIcon = Sprite(Inventory.GetIcon(Currency.Coin), 36, 214, 36, 36, 6);
		_amount = Label(string.Empty, 84, 206, 530, 52, 30, Color.white, UIWidget.Pivot.Left);
		long[] packs = OffServerTreasury.Packs;
		for (int num = 0; num < 4; num++)
		{
			int x = 20 + num * 156;
			if (num < 3)
			{
				long pack = ((num < packs.Length) ? packs[num] : (num switch
				{
					1 => 100L,
					0 => 10L,
					_ => 1000L,
				}));
				_quick.Add(Button(_btnBorderTpl, "q" + pack, OffServerTreasury.N(pack), x, 270, 142, 46, delegate
				{
					_want = pack;
					Redraw();
				}));
			}
			else
			{
				_quick.Add(Button(_btnBorderTpl, "max", OffServerTreasury.Text("max_btn"), x, 270, 142, 46, delegate
				{
					_want = Math.Max(0L, Math.Min(OffServerTreasury.CoinBalance, OffServerTreasury.EffectiveCoins(long.MaxValue)));
					Redraw();
				}));
			}
		}
		Box(0, 330, 650, 146, new Color(13f / 85f, 12f / 85f, 9f / 85f, 0.94f), 3);
		Sprite("bg_dotline_white2", 0, 330, 650, 2, 4).color = new Color(1f, 1f, 1f, 0.12f);
		_payTitle = Label(string.Empty, 30, 336, 330, 34, 22, Beige, UIWidget.Pivot.Left);
		_feeTitle = Label(string.Empty, 30, 370, 330, 34, 22, Beige, UIWidget.Pivot.Left);
		_getTitle = Label(string.Empty, 30, 404, 330, 34, 22, Gold, UIWidget.Pivot.Left);
		_pay = Label(string.Empty, 300, 336, 320, 34, 22, Color.white, UIWidget.Pivot.Right);
		_fee = Label(string.Empty, 300, 370, 320, 34, 22, Grey, UIWidget.Pivot.Right);
		_get = Label(string.Empty, 300, 404, 320, 34, 26, Gold, UIWidget.Pivot.Right);
		Sprite("bg_dotline_white2", 20, 370, 610, 2, 4).color = new Color(1f, 1f, 1f, 0.1f);
		Sprite("bg_dotline_white2", 20, 404, 610, 2, 4).color = new Color(1f, 1f, 1f, 0.1f);
		_status = Label(string.Empty, 10, 440, 630, 30, 19, Green, UIWidget.Pivot.Center);
		Sprite("bg_dotline_white2", 0, 476, 650, 2, 4).color = new Color(1f, 1f, 1f, 0.12f);
		_nowTitle = Label(string.Empty, 80, 482, 190, 24, 20, Beige, UIWidget.Pivot.Center);
		_afterTitle = Label(string.Empty, 380, 482, 190, 24, 20, Beige, UIWidget.Pivot.Center);
		_nowT = Money(80, 510, Inventory.GetIcon(Currency.TStone));
		_nowC = Money(80, 544, Inventory.GetIcon(Currency.Coin));
		_afterT = Money(380, 510, Inventory.GetIcon(Currency.TStone));
		_afterC = Money(380, 544, Inventory.GetIcon(Currency.Coin));
		Sprite("popup_arrow", 314, 536, 22, 12, 6);
		Box(0, 580, 650, 90, new Color(0f, 0f, 0f, 0.35f), 3);
		Sprite("bg_dotline_white2", 0, 580, 650, 2, 4).color = new Color(1f, 1f, 1f, 0.12f);
		_bCancel = Button(_btnBorderTpl, "cancel", OffServerTreasury.Text("cancel"), 11, 590, 244, 70, Close);
		_bTrade = Button(_btnSolidTpl, "trade", OffServerTreasury.Text("exchange"), 266, 590, 374, 70, AskTrade);
		try
		{
			_bCancel.SetStyle(PresetButton.Style.Border);
			_bTrade.SetStyle(PresetButton.Style.Solid);
			foreach (SelectableButton item in _quick)
			{
				item.SetStyle(PresetButton.Style.Border);
			}
		}
		catch (Exception)
		{
		}
	}

	private static Vector3 At(int x, int y, int w, int h)
	{
		return new Vector3(x + w / 2 - 325, 335 - (y + h / 2), 0f);
	}

	private UISprite Sprite(string name, int x, int y, int w, int h, int depth)
	{
		UISprite uISprite = base.gameObject.AddSprite(name, depth);
		if (_surfaces.TryGetValue(name, out var value))
		{
			uISprite.spriteName = value.spriteName;
			uISprite.type = value.type;
			uISprite.color = value.color;
		}
		uISprite.width = Mathf.Max(2, w);
		uISprite.height = Mathf.Max(2, h);
		uISprite.transform.localPosition = At(x, y, w, h);
		return uISprite;
	}

	private UISprite Box(int x, int y, int w, int h, Color color, int depth)
	{
		UISprite uISprite = Sprite("bg_white", x, y, w, h, depth);
		uISprite.type = UIBasicSprite.Type.Simple;
		uISprite.color = color;
		return uISprite;
	}

	private UILabel Label(string text, int x, int y, int w, int h, int size, Color color, UIWidget.Pivot pivot)
	{
		GameObject obj = base.gameObject.AddChild();
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
		uILabel.width = w;
		uILabel.height = h;
		uILabel.fontSize = size;
		uILabel.color = color;
		uILabel.depth = 20;
		uILabel.text = text;
		int num = pivot switch
		{
			UIWidget.Pivot.Right => x + w,
			UIWidget.Pivot.Left => x,
			_ => x + w / 2,
		};
		uILabel.transform.localPosition = new Vector3(num - 325, 335 - (y + h / 2), 0f);
		return uILabel;
	}

	private UILabel Money(int x, int y, string icon)
	{
		Sprite("money_bg", x, y, 190, 30, 5).color = new Color(0f, 0f, 0f, 0.5f);
		Sprite(icon, x + 6, y + 3, 24, 24, 6);
		return Label(string.Empty, x + 34, y, 150, 30, 20, Color.white, UIWidget.Pivot.Right);
	}

	private SelectableButton Button(SelectableButton tpl, string key, string text, int x, int y, int w, int h, Action onClick)
	{
		GameObject obj = UnityEngine.Object.Instantiate(tpl.gameObject, base.transform);
		obj.name = "Treasury_" + key;
		UIRect component = obj.GetComponent<UIRect>();
		if (component != null)
		{
			component.SetAnchor((Transform)null);
		}
		obj.SetActive(value: true);
		obj.transform.localPosition = At(x, y, w, h);
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
		base.transform.localScale = Vector3.one * Mathf.Min(1f, (num - 32f) / 670f, (num2 - 32f) / 650f);
	}

	private void OnEnable()
	{
		OffServerTreasury.Changed += Redraw;
		try
		{
			GameSystem<InventorySystem>.Instance().WalletUpdated += Redraw;
		}
		catch (Exception)
		{
		}
	}

	private void OnDisable()
	{
		OffServerTreasury.Changed -= Redraw;
		try
		{
			GameSystem<InventorySystem>.Instance().WalletUpdated -= Redraw;
		}
		catch (Exception)
		{
		}
	}

	private void Update()
	{
		Fit();
		if (_fromShop)
		{
			ShopGroup shopGroup = UIManager.FindScript<ShopGroup>();
			if (shopGroup == null || !shopGroup.IsOpened)
			{
				Close();
			}
		}
	}

	private static void PlaceLeftOf(UISprite icon, UILabel label)
	{
		Vector3 localPosition = label.transform.localPosition;
		icon.transform.localPosition = new Vector3(localPosition.x - label.printedSize.x - (float)(icon.width / 2) - 6f, localPosition.y, 0f);
	}

	private void Redraw()
	{
		bool active = OffServerTreasury.Active;
		long coinBalance = OffServerTreasury.CoinBalance;
		long tStoneBalance = OffServerTreasury.TStoneBalance;
		long rate = OffServerTreasury.Rate;
		long num = OffServerTreasury.EffectiveCoins(_want);
		long num2 = num * rate;
		long num3 = OffServerTreasury.PayoutOf(num2);
		long num4 = num2 - num3;
		double num5 = Math.Round(OffServerTreasury.Fee * 100.0, 1);
		_title.text = OffServerTreasury.Text("title");
		_balanceTitle.text = OffServerTreasury.Text("balance");
		_balance.text = OffServerTreasury.N(OffServerTreasury.Balance) + " T";
		_maxTitle.text = OffServerTreasury.Text("max");
		_max.text = OffServerTreasury.N(OffServerTreasury.EffectiveCoins(long.MaxValue));
		PlaceLeftOf(_maxIcon, _max);
		_amountTitle.text = OffServerTreasury.Text("amount");
		_have.text = OffServerTreasury.Text("have", OffServerTreasury.N(coinBalance));
		PlaceLeftOf(_haveIcon, _have);
		_amount.text = ((_want > 0) ? OffServerTreasury.N(_want) : OffServerTreasury.Text("tap"));
		_amount.color = ((_want > 0) ? Color.white : Grey);
		_payTitle.text = OffServerTreasury.Text("pay");
		_feeTitle.text = OffServerTreasury.Text("fee", num5.ToString("0.#", CultureInfo.InvariantCulture));
		_getTitle.text = OffServerTreasury.Text("get");
		_pay.text = OffServerTreasury.N(num2) + " T";
		_fee.text = ((num4 > 0) ? "−" : string.Empty) + OffServerTreasury.N(num4) + " T";
		_get.text = OffServerTreasury.N(num3) + " T";
		_nowTitle.text = OffServerTreasury.Text("now");
		_afterTitle.text = OffServerTreasury.Text("after");
		_nowT.text = OffServerTreasury.N(tStoneBalance);
		_nowC.text = OffServerTreasury.N(coinBalance);
		_afterT.text = OffServerTreasury.N(tStoneBalance + num3);
		_afterC.text = OffServerTreasury.N(Math.Max(0L, coinBalance - num));
		bool flag = false;
		string text;
		Color color;
		if (_loading && rate <= 0)
		{
			text = OffServerTreasury.Text("st_loading");
			color = Grey;
		}
		else if (!active)
		{
			text = OffServerTreasury.Text("st_off");
			color = Red;
		}
		else if (OffServerTreasury.EffectiveCoins(long.MaxValue) <= 0 && OffServerTreasury.MaxCoins <= 0)
		{
			text = OffServerTreasury.Text("st_empty");
			color = Red;
		}
		else if (_want <= 0)
		{
			text = OffServerTreasury.Text("st_none");
			color = Grey;
		}
		else if (num > coinBalance || coinBalance <= 0)
		{
			text = OffServerTreasury.Text("st_coin");
			color = Red;
		}
		else if (num <= 0)
		{
			text = OffServerTreasury.Text("st_daily", OffServerTreasury.N(0L));
			color = Red;
		}
		else if (num < _want)
		{
			text = OffServerTreasury.Text("st_partial", OffServerTreasury.N(num));
			color = Gold;
			flag = _want <= coinBalance;
		}
		else
		{
			text = OffServerTreasury.Text("st_ok");
			color = Green;
			flag = true;
		}
		_status.text = text;
		_status.color = color;
		_bTrade.Text = ((num > 0) ? OffServerTreasury.Text("exchange_n", OffServerTreasury.N(num)) : OffServerTreasury.Text("exchange"));
		_bTrade.Disabled = !flag || OffServerTreasury.Busy || num3 <= 0;
		bool disabled = !active || OffServerTreasury.Busy;
		foreach (SelectableButton item in _quick)
		{
			item.Disabled = disabled;
		}
		LastText = text;
	}

	private void AskAmount()
	{
		if (!OffServerTreasury.Active || OffServerTreasury.Busy)
		{
			return;
		}
		long coinBalance = OffServerTreasury.CoinBalance;
		if (coinBalance <= 0)
		{
			UIManager.SystemMsg(OffServerTreasury.Text("st_coin"));
			return;
		}
		UIManager.Popup.Tooltip<NumberInputPopup>().Show(Math.Max(1L, Math.Min(_want, coinBalance)), Currency.Coin, OffServerTreasury.Text("amount"), delegate(long value)
		{
			_want = Math.Max(0L, Math.Min(value, coinBalance));
			Redraw();
		}, coinBalance);
	}

	private void AskTrade()
	{
		long want = _want;
		long num = OffServerTreasury.EffectiveCoins(want);
		long num2 = num * OffServerTreasury.Rate;
		long num3 = OffServerTreasury.PayoutOf(num2);
		if (num <= 0 || num3 <= 0 || OffServerTreasury.Busy)
		{
			return;
		}
		UIManager.MessageBox.ShowPayConfirm(num, Currency.Coin, OffServerTreasury.Text("confirm", OffServerTreasury.N(num3), OffServerTreasury.N(num2 - num3)), delegate(bool ok)
		{
			if (ok)
			{
				Trade(want);
			}
		}, OffServerTreasury.Text("exchange"));
	}

	public static void Trade(long want)
	{
		OffServerTreasury.Exchange(want, delegate(bool ok, string reason, JObject o)
		{
			if (ok)
			{
				if (_win != null)
				{
					_win._want = 0L;
				}
			}
			else if (reason == "unresolved")
			{
				UIManager.SystemMsg(OffServerTreasury.Text("fail_unresolved", (o != null && o["ref"] != null) ? ((string)o["ref"]) : "-"), 8f);
			}
			else
			{
				UIManager.SystemMsg(OffServerTreasury.HasText("fail_" + reason) ? OffServerTreasury.Text("fail_" + reason) : (OffServerTreasury.Text("failed") + " (" + reason + ")"));
			}
			Refresh();
		});
		Refresh();
	}
}
