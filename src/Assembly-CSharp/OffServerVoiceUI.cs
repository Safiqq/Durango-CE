using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Durango.Logic.Clusters;
using Durango.System.Config;
using Durango.UI;
using Durango.UI.Popup;
using Durango.Utils;
using UnityEngine;

public class OffServerVoiceUI : MonoBehaviour
{
	private sealed class Button
	{
		public GameObject Go;

		public UISprite Bg;

		public UILabel Label;
	}

	private sealed class Row
	{
		public GameObject Go;

		public UILabel Name;

		public UILabel Info;

		public Button Mute;

		public string EntityId;
	}

	private const int PanelDepth = 6000;

	private const int Width = 640;

	private const int RowHeight = 46;

	private const int NearbyRows = 5;

	private const float NearbyTiles = 12f;

	private static readonly Color BtnOff = new Color(0.22f, 0.22f, 0.24f, 0.95f);

	private static readonly Color BtnOn = new Color(0.98f, 0.72f, 0.18f, 1f);

	private static readonly Color BtnMuted = new Color(0.75f, 0.25f, 0.22f, 1f);

	private static readonly Color TextOn = new Color(0.1f, 0.08f, 0.05f, 1f);

	private static readonly Color TextOff = new Color(0.95f, 0.95f, 0.95f, 1f);

	private static readonly Color Talk = new Color(0.35f, 1f, 0.45f, 1f);

	private static OffServerVoiceUI _panel;

	private static GameObject _micButton;

	private static Action _relayout;

	private static UISprite _micIcon;

	private static GameObject _wave1;

	private static GameObject _wave2;

	private static bool _micHooked;

	private UIPanel _uiPanel;

	private UILabel _style;

	private UILabel _status;

	private UISprite _meter;

	private Button _hold;

	private Button _auto;

	private readonly Button[] _levels = new Button[3];

	private UILabel _volume;

	private Button _deaf;

	private UILabel _sens;

	private UILabel _nearbyHead;

	private readonly List<Row> _rows = new List<Row>();

	private float _refreshAt;

	private int _top;

	private static Func<bool> _micVisible;

	private static OffServerVoiceRing _myRing;

	private static float _flashUntil;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"title",
			new string[4] { "ไมค\u0e4cพ\u0e39ดตามระยะ", "Proximity voice", "Suara jarak dekat", "근거리 음성" }
		},
		{
			"mode",
			new string[4] { "โหมดพ\u0e39ด", "Talk mode", "Mode bicara", "말하기 방식" }
		},
		{
			"mode_hold",
			new string[4] { "กดค\u0e49างพ\u0e39ด", "Hold to talk", "Tahan untuk bicara", "누르고 말하기" }
		},
		{
			"mode_auto",
			new string[4] { "อ\u0e31ตโนม\u0e31ต\u0e34", "Auto (voice)", "Otomatis", "자동" }
		},
		{
			"range",
			new string[4] { "ระยะเส\u0e35ยง", "Range", "Jangkauan", "범위" }
		},
		{
			"level0",
			new string[4] { "กระซ\u0e34บ {0}", "Whisper {0}", "Bisik {0}", "속삭임 {0}" }
		},
		{
			"level1",
			new string[4] { "ปกต\u0e34 {0}", "Normal {0}", "Normal {0}", "보통 {0}" }
		},
		{
			"level2",
			new string[4] { "ตะโกน {0}", "Shout {0}", "Teriak {0}", "외침 {0}" }
		},
		{
			"volume",
			new string[4] { "เส\u0e35ยงคนอ\u0e37\u0e48น", "Others' volume", "Volume orang lain", "다른 사람 음량" }
		},
		{
			"deaf",
			new string[4] { "ป\u0e34ดเส\u0e35ยงท\u0e38กคน", "Mute everyone", "Bisukan semua", "모두 음소거" }
		},
		{
			"sens",
			new string[4] { "ความไวไมค\u0e4c (อ\u0e31ตโนม\u0e31ต\u0e34)", "Mic sensitivity (Auto)", "Sensitivitas mik (Otomatis)", "마이크 감도 (자동)" }
		},
		{
			"tab",
			new string[4] { "ไมค\u0e4c", "Voice chat", "Suara", "음성 채팅" }
		},
		{
			"nearby_button",
			new string[4] { "ผ\u0e39\u0e49เล\u0e48นใกล\u0e49ต\u0e31ว · ป\u0e34ดเส\u0e35ยงรายคน", "Nearby players · mute someone", "Pemain terdekat · bisukan pemain", "주변 플레이어 · 개별 음소거" }
		},
		{
			"nearby",
			new string[4] { "ผ\u0e39\u0e49เล\u0e48นใกล\u0e49ต\u0e31ว ({0}) · ช\u0e37\u0e48อส\u0e35เข\u0e35ยว = กำล\u0e31งพ\u0e39ด", "Nearby players ({0}) · green name = talking", "Pemain terdekat ({0}) · nama hijau = bicara", "주변 플레이어 ({0}) · 초록 이름 = 말하는 중" }
		},
		{
			"dist",
			new string[4] { "{0} ช\u0e48อง", "{0} tiles", "{0} petak", "{0}칸" }
		},
		{
			"tiles_word",
			new string[4] { "ช\u0e48อง", "tiles", "petak", "칸" }
		},
		{
			"tile_word",
			new string[4] { "ช\u0e48อง", "tile", "petak", "칸" }
		},
		{
			"mute",
			new string[4] { "ป\u0e34ดเส\u0e35ยง", "Mute", "Bisukan", "음소거" }
		},
		{
			"unmute",
			new string[4] { "เป\u0e34ดเส\u0e35ยง", "Unmute", "Bunyikan", "음소거 해제" }
		},
		{
			"talking",
			new string[4] { "กำล\u0e31งพ\u0e39ด — คนในระยะ {0} ช\u0e48อง (วงกลมท\u0e35\u0e48เท\u0e49า) ได\u0e49ย\u0e34น", "Talking — players within {0} tiles (circle at your feet) hear you", "Bicara — pemain dalam {0} petak (lingkaran di kaki) mendengar", "말하는 중 — {0}칸 안(발밑 원)의 플레이어가 듣습니다" }
		},
		{
			"hold_hint_pc",
			new string[4] { "กด {key} ค\u0e49างเพ\u0e37\u0e48อพ\u0e39ด · {range} = สล\u0e31บระยะ", "Hold {key} to talk · {range} = change range", "Tahan {key} untuk bicara · {range} = ganti jangkauan", "{key}를 누르고 말하기 · {range} = 범위 변경" }
		},
		{
			"pc_button_tip",
			new string[4] { "ไมค\u0e4c — กดค\u0e49างพ\u0e39ด · คล\u0e34ก = แผงไมค\u0e4c", "Voice — hold to talk · click = voice panel", "Suara — tahan untuk bicara · klik = panel suara", "음성 — 누르고 말하기 · 클릭 = 음성 설정 창" }
		},
		{
			"hold_hint_mobile",
			new string[4] { "กดป\u0e38\u0e48มไมค\u0e4cค\u0e49างเพ\u0e37\u0e48อพ\u0e39ด · แตะ = เป\u0e34ดแผงน\u0e35\u0e49", "Hold the mic button to talk · tap = this panel", "Tahan tombol mik untuk bicara · ketuk = panel ini", "마이크 버튼을 누르고 말하기 · 탭 = 이 창" }
		},
		{
			"auto_hint",
			new string[4] { "อ\u0e31ตโนม\u0e31ต\u0e34: ไมค\u0e4cส\u0e48งเส\u0e35ยงเองเม\u0e37\u0e48อพ\u0e39ด (ใส\u0e48ห\u0e39ฟ\u0e31งก\u0e31นเส\u0e35ยงสะท\u0e49อน)", "Auto: sends when you speak (use headphones to avoid echo)", "Otomatis: terkirim saat bicara (pakai headset agar tidak bergema)", "자동: 말하면 전송 (메아리 방지를 위해 이어폰 권장)" }
		},
		{
			"gm_muted",
			new string[4] { "ถ\u0e39กป\u0e34ดแชทและไมค\u0e4cอ\u0e35ก {0} ชม.", "Chat and voice are blocked for {0} more hour(s)", "Chat dan suara diblokir {0} jam lagi", "채팅과 음성이 {0}시간 동안 제한됩니다" }
		},
		{
			"no_mic",
			new string[4] { "ไม\u0e48พบไมค\u0e4cในเคร\u0e37\u0e48องน\u0e35\u0e49", "No microphone found", "Mikrofon tidak ditemukan", "마이크를 찾을 수 없습니다" }
		},
		{
			"perm_denied",
			new string[4] { "ต\u0e49องอน\u0e38ญาตไมค\u0e4cในต\u0e31\u0e49งค\u0e48าเคร\u0e37\u0e48องก\u0e48อนจ\u0e36งจะพ\u0e39ดได\u0e49", "Allow the microphone in the phone settings to talk", "Izinkan mikrofon di pengaturan ponsel untuk bicara", "말하려면 휴대폰 설정에서 마이크를 허용하세요" }
		}
	};

	public static bool PanelOpen
	{
		get
		{
			if (_panel != null)
			{
				return _panel.gameObject.activeSelf;
			}
			return false;
		}
	}

	public static Color LevelColor(int level)
	{
		return level switch
		{
			0 => new Color(0.3f, 0.95f, 0.4f, 1f),
			2 => new Color(1f, 0.25f, 0.22f, 1f),
			_ => new Color(0.72f, 0.4f, 1f, 1f),
		};
	}

	public static void FlashRange()
	{
		_flashUntil = Time.realtimeSinceStartup + 2.5f;
		UIManager.SystemMsg(LevelLabel(OffServerVoice.Level), 1.5f);
	}

	public static string LevelLabel(int level)
	{
		level = Mathf.Clamp(level, 0, 2);
		float num = OffServerVoice.Ranges[level];
		return string.Format(Text("level" + level), Tiles(num)) + " " + Text((Mathf.Abs(num - 1f) < 0.05f) ? "tile_word" : "tiles_word");
	}

	public static void AttachMicButton(GameObject button, Action relayout, Func<bool> visible = null)
	{
		if (!(button == null))
		{
			_micButton = button;
			_relayout = relayout;
			_micVisible = visible;
			Transform transform = button.transform.Find("Sprite");
			_micIcon = ((transform != null) ? transform.GetComponent<UISprite>() : null);
			Transform transform2 = button.transform.Find("WaveSprite1");
			Transform transform3 = button.transform.Find("WaveSprite2");
			_wave1 = ((transform2 != null) ? transform2.gameObject : null);
			_wave2 = ((transform3 != null) ? transform3.gameObject : null);
			UIEventListener uIEventListener = UIEventListener.Get(button);
			uIEventListener.onPress = (UIEventListener.BoolDelegate)Delegate.Combine(uIEventListener.onPress, new UIEventListener.BoolDelegate(OnMicPress));
			if (!_micHooked)
			{
				_micHooked = true;
				OffServerVoice.Changed += RefreshMicButton;
			}
			RefreshMicButton();
			if (button.GetComponent<OffServerVoiceButtonTicker>() == null)
			{
				button.AddComponent<OffServerVoiceButtonTicker>();
			}
		}
	}

	public static void AttachPcMicButton(Transform buttonLayout, Func<bool> visible)
	{
		Transform transform = ((buttonLayout != null) ? buttonLayout.Find("ChatButton") : null);
		if (!(transform == null))
		{
			UIWidget uIWidget = buttonLayout.gameObject.AddWidget<UIWidget>(0);
			GameObject obj = uIWidget.gameObject;
			obj.name = "OffServerVoiceButton";
			uIWidget.width = 30;
			uIWidget.height = 30;
			UISprite uISprite = obj.AddSprite("icon_stt", 2);
			uISprite.name = "Sprite";
			uISprite.type = UIBasicSprite.Type.Simple;
			uISprite.width = 17;
			uISprite.height = 30;
			uISprite.color = Color.white;
			UISprite uISprite2 = obj.AddSprite("icon_stt_sound1", 3);
			uISprite2.name = "WaveSprite1";
			uISprite2.type = UIBasicSprite.Type.Simple;
			uISprite2.width = 27;
			uISprite2.height = 10;
			uISprite2.transform.localPosition = new Vector3(0f, 21f, 0f);
			uISprite2.gameObject.SetActive(value: false);
			UISprite uISprite3 = obj.AddSprite("icon_stt_sound2", 3);
			uISprite3.name = "WaveSprite2";
			uISprite3.type = UIBasicSprite.Type.Simple;
			uISprite3.width = 36;
			uISprite3.height = 10;
			uISprite3.transform.localPosition = new Vector3(0f, 29f, 0f);
			uISprite3.gameObject.SetActive(value: false);
			NGUITools.AddWidgetCollider(obj);
			OffServerVoicePcMicPlacer offServerVoicePcMicPlacer = obj.AddComponent<OffServerVoicePcMicPlacer>();
			offServerVoicePcMicPlacer.Chat = transform;
			offServerVoicePcMicPlacer.Emote = buttonLayout.Find("CommunicationButton");
			offServerVoicePcMicPlacer.Place();
			UIEventListener uIEventListener = UIEventListener.Get(obj);
			uIEventListener.onHover = (UIEventListener.BoolDelegate)Delegate.Combine(uIEventListener.onHover, (UIEventListener.BoolDelegate)delegate(GameObject go, bool show)
			{
				ShowPcMicTooltip(uIWidget, show);
			});
			AttachMicButton(obj, null, visible);
		}
	}

	private static void ShowPcMicTooltip(UIWidget button, bool show)
	{
		ButtonInfoTooltip buttonInfoTooltip = UIManager.Popup.Tooltip<ButtonInfoTooltip>();
		if (!(buttonInfoTooltip == null))
		{
			buttonInfoTooltip.Sign = 1;
			if (show)
			{
				buttonInfoTooltip.Set(InputCommand.VoicePushToTalk, Text("pc_button_tip"));
				buttonInfoTooltip.Show(button, new Vector2(-5f, 20f), float.MaxValue);
			}
			else
			{
				buttonInfoTooltip.Hide();
			}
		}
	}

	private static void OnMicPress(GameObject go, bool pressed)
	{
		OffServerVoice.ButtonPress(pressed);
	}

	public static void RefreshMicButton()
	{
		if (_micButton == null)
		{
			return;
		}
		bool available = OffServerVoice.Available;
		bool flag = available;
		if (flag && _micVisible != null)
		{
			try
			{
				flag = _micVisible();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
		if (_micButton.activeSelf != flag)
		{
			_micButton.SetActive(flag);
			if (_relayout != null)
			{
				try
				{
					_relayout();
				}
				catch (Exception exception2)
				{
					Debug.LogException(exception2);
				}
			}
		}
		if (!available && PanelOpen)
		{
			ClosePanel();
		}
	}

	public static void TickMicButton()
	{
		if (_micButton == null || !_micButton.activeInHierarchy)
		{
			return;
		}
		bool talking = OffServerVoice.Talking;
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (_wave1 != null)
		{
			_wave1.SetActive(talking && realtimeSinceStartup % 0.6f < 0.45f);
		}
		if (_wave2 != null)
		{
			_wave2.SetActive(talking && realtimeSinceStartup % 0.6f >= 0.15f);
		}
		if (_micIcon != null)
		{
			Color color = Color.white;
			if (OffServerVoice.GmMutedHours > 0 || OffServerVoice.MicProblem != null)
			{
				color = new Color(1f, 0.4f, 0.35f, 1f);
			}
			else if (talking)
			{
				color = Talk;
			}
			else if (OffServerVoice.AutoMode)
			{
				color = new Color(0.75f, 1f, 0.8f, 1f);
			}
			if (_micIcon.color != color)
			{
				_micIcon.color = color;
			}
		}
	}

	public static void TogglePanel()
	{
		if (PanelOpen)
		{
			ClosePanel();
		}
		else
		{
			OpenPanel();
		}
	}

	public static void ClosePanel()
	{
		if (_panel != null)
		{
			_panel.gameObject.SetActive(value: false);
		}
	}

	public static void OpenPanel()
	{
		if (!OffServerVoice.Available)
		{
			return;
		}
		if (_panel == null)
		{
			UIRoot uIRoot = (Singleton<UIManager>.HasInstance() ? Singleton<UIManager>.Instance().UIRoot : null);
			if (uIRoot == null)
			{
				return;
			}
			GameObject obj = uIRoot.gameObject.AddChild();
			obj.name = "OffServerVoicePanel";
			_panel = obj.AddComponent<OffServerVoiceUI>();
			_panel.Build();
		}
		_panel.gameObject.SetActive(value: true);
		_panel._refreshAt = 0f;
		_panel.Refresh();
	}

	private void Build()
	{
		_uiPanel = base.gameObject.AddComponent<UIPanel>();
		_uiPanel.depth = 6000;
		_style = FindStyle();
		UIWidget uIWidget = base.gameObject.AddWidget<UIWidget>(0);
		uIWidget.width = 4000;
		uIWidget.height = 4000;
		NGUITools.AddWidgetCollider(uIWidget.gameObject);
		UIEventListener.Get(uIWidget.gameObject).onClick = delegate
		{
			ClosePanel();
		};
		int num = 616;
		UISprite uISprite = base.gameObject.AddSprite("bg_white", 1);
		uISprite.type = UIBasicSprite.Type.Simple;
		uISprite.width = 640;
		uISprite.height = num;
		uISprite.color = new Color(0.07f, 0.07f, 0.09f, 0.94f);
		NGUITools.AddWidgetCollider(uISprite.gameObject);
		_top = num / 2;
		int num2 = -296;
		int num3 = _top - 34;
		Label(Text("title"), num2, num3, 30, TextOff, 420, UIWidget.Pivot.Left);
		MakeButton("X", 280, num3, 56, 44, delegate
		{
			ClosePanel();
		}).Bg.color = BtnOff;
		num3 -= 46;
		_status = Label(string.Empty, num2, num3, 22, new Color(1f, 0.85f, 0.4f, 1f), 592, UIWidget.Pivot.Left);
		_meter = base.gameObject.AddSprite("bg_white", 4);
		_meter.type = UIBasicSprite.Type.Simple;
		_meter.pivot = UIWidget.Pivot.Left;
		_meter.height = 6;
		_meter.width = 1;
		_meter.color = Talk;
		_meter.transform.localPosition = new Vector3(num2, num3 - 20, 0f);
		num3 -= 46;
		int num4 = -90;
		Label(Text("mode"), num2, num3, 24, TextOff, 200, UIWidget.Pivot.Left);
		_hold = MakeButton(Text("mode_hold"), num4 + 90, num3, 176, 40, delegate
		{
			OffServerVoice.SetAutoMode(auto: false);
			Refresh();
		});
		_auto = MakeButton(Text("mode_auto"), num4 + 280, num3, 176, 40, delegate
		{
			OffServerVoice.SetAutoMode(auto: true);
			Refresh();
		});
		num3 -= 46;
		Label(Text("range"), num2, num3, 24, TextOff, 200, UIWidget.Pivot.Left);
		for (int num5 = 0; num5 < 3; num5++)
		{
			int level = num5;
			_levels[num5] = MakeButton(string.Empty, num4 + 58 + num5 * 128, num3, 120, 40, delegate
			{
				OffServerVoice.SetLevel(level);
				Refresh();
			});
		}
		num3 -= 46;
		Label(Text("volume"), num2, num3, 24, TextOff, 200, UIWidget.Pivot.Left);
		MakeButton("-", num4 + 24, num3, 52, 40, delegate
		{
			OffServerVoice.SetVolume(OffServerVoice.Volume - 0.25f);
			Refresh();
		}).Bg.color = BtnOff;
		_volume = Label(string.Empty, num4 + 100, num3, 24, TextOff, 90, UIWidget.Pivot.Center);
		MakeButton("+", num4 + 176, num3, 52, 40, delegate
		{
			OffServerVoice.SetVolume(OffServerVoice.Volume + 0.25f);
			Refresh();
		}).Bg.color = BtnOff;
		_deaf = MakeButton(Text("deaf"), num4 + 312, num3, 196, 40, delegate
		{
			OffServerVoice.SetDeaf(!OffServerVoice.Deaf);
			Refresh();
		});
		num3 -= 46;
		Label(Text("sens"), num2, num3, 24, TextOff, 210, UIWidget.Pivot.Left);
		MakeButton("-", num4 + 24, num3, 52, 40, delegate
		{
			OffServerVoice.SetSensitivity(OffServerVoice.Sensitivity - 1);
			Refresh();
		}).Bg.color = BtnOff;
		_sens = Label(string.Empty, num4 + 100, num3, 24, TextOff, 90, UIWidget.Pivot.Center);
		MakeButton("+", num4 + 176, num3, 52, 40, delegate
		{
			OffServerVoice.SetSensitivity(OffServerVoice.Sensitivity + 1);
			Refresh();
		}).Bg.color = BtnOff;
		num3 -= 46;
		_nearbyHead = Label(Text("nearby"), num2, num3 + 6, 22, new Color(0.8f, 0.8f, 0.8f, 1f), 592, UIWidget.Pivot.Left);
		num3 -= 34;
		for (int num6 = 0; num6 < 5; num6++)
		{
			Row row = new Row();
			row.Go = base.gameObject.AddChild();
			row.Name = Label(string.Empty, num2, num3, 24, TextOff, 300, UIWidget.Pivot.Left, row.Go);
			row.Info = Label(string.Empty, num2 + 330, num3, 20, new Color(0.75f, 0.75f, 0.75f, 1f), 110, UIWidget.Pivot.Left, row.Go);
			Row captured = row;
			row.Mute = MakeButton(string.Empty, 220, num3, 160, 38, delegate
			{
				if (!string.IsNullOrEmpty(captured.EntityId))
				{
					OffServerVoice.SetMuted(captured.EntityId, !OffServerVoice.IsMuted(captured.EntityId));
					Refresh();
				}
			}, row.Go);
			_rows.Add(row);
			num3 -= 46;
		}
		OffServerVoice.Changed += OnVoiceChanged;
	}

	private void OnDestroy()
	{
		OffServerVoice.Changed -= OnVoiceChanged;
		if (_panel == this)
		{
			_panel = null;
		}
	}

	private void OnVoiceChanged()
	{
		if (base.gameObject.activeSelf)
		{
			Refresh();
		}
	}

	private static UILabel FindStyle()
	{
		UILabel[] componentsInChildren;
		try
		{
			if (UIManager.MessageBox != null)
			{
				componentsInChildren = UIManager.MessageBox.GetComponentsInChildren<UILabel>(includeInactive: true);
				foreach (UILabel uILabel in componentsInChildren)
				{
					if (uILabel.trueTypeFont != null || uILabel.bitmapFont != null)
					{
						return uILabel;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		UILabel uILabel2 = null;
		componentsInChildren = UnityEngine.Object.FindObjectsByType<UILabel>(FindObjectsSortMode.None);
		foreach (UILabel uILabel3 in componentsInChildren)
		{
			if (!(uILabel3.bitmapFont == null) || !(uILabel3.trueTypeFont == null))
			{
				if (uILabel2 == null || uILabel3.fontSize > uILabel2.fontSize)
				{
					uILabel2 = uILabel3;
				}
				if (uILabel3.trueTypeFont != null && uILabel3.fontSize >= 20)
				{
					return uILabel3;
				}
			}
		}
		return uILabel2;
	}

	private UILabel Label(string text, int x, int y, int size, Color color, int width, UIWidget.Pivot pivot, GameObject parent = null)
	{
		UILabel uILabel = (parent ?? base.gameObject).AddWidget<UILabel>(6);
		if (_style != null)
		{
			if (_style.bitmapFont != null)
			{
				uILabel.bitmapFont = _style.bitmapFont;
			}
			else
			{
				uILabel.trueTypeFont = _style.trueTypeFont;
			}
		}
		uILabel.fontSize = size;
		uILabel.color = color;
		uILabel.pivot = pivot;
		uILabel.alignment = ((pivot != UIWidget.Pivot.Center) ? NGUIText.Alignment.Left : NGUIText.Alignment.Center);
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		uILabel.width = width;
		uILabel.height = size + 10;
		uILabel.text = text;
		uILabel.transform.localPosition = new Vector3(x, y, 0f);
		return uILabel;
	}

	private Button MakeButton(string text, int x, int y, int w, int h, Action onClick, GameObject parent = null)
	{
		Button button = new Button();
		button.Bg = (parent ?? base.gameObject).AddSprite("bg_white", 3);
		button.Bg.type = UIBasicSprite.Type.Simple;
		button.Bg.width = w;
		button.Bg.height = h;
		button.Bg.color = BtnOff;
		button.Bg.transform.localPosition = new Vector3(x, y, 0f);
		button.Go = button.Bg.gameObject;
		button.Label = button.Go.AddWidget<UILabel>(7);
		if (_style != null)
		{
			if (_style.bitmapFont != null)
			{
				button.Label.bitmapFont = _style.bitmapFont;
			}
			else
			{
				button.Label.trueTypeFont = _style.trueTypeFont;
			}
		}
		button.Label.fontSize = 22;
		button.Label.color = TextOff;
		button.Label.alignment = NGUIText.Alignment.Center;
		button.Label.overflowMethod = UILabel.Overflow.ShrinkContent;
		button.Label.width = w - 8;
		button.Label.height = h - 4;
		button.Label.text = text;
		NGUITools.AddWidgetCollider(button.Go);
		UIEventListener.Get(button.Go).onClick = delegate
		{
			try
			{
				onClick();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		};
		return button;
	}

	private static void Paint(Button b, bool on, Color? onColor = null)
	{
		if (b != null)
		{
			b.Bg.color = ((!on) ? BtnOff : (onColor ?? BtnOn));
			b.Label.color = (on ? TextOn : TextOff);
		}
	}

	private void Update()
	{
		if (Time.realtimeSinceStartup >= _refreshAt)
		{
			Refresh();
		}
		if (_meter != null)
		{
			float num = Mathf.Clamp01((OffServerVoice.MicLevelDb + 60f) / 60f);
			_meter.width = Mathf.Max(1, (int)(592f * num));
			_meter.color = (OffServerVoice.Talking ? Talk : new Color(0.6f, 0.6f, 0.6f, 0.8f));
		}
		if (!OffServerVoice.Available)
		{
			ClosePanel();
		}
	}

	private void Refresh()
	{
		_refreshAt = Time.realtimeSinceStartup + 0.3f;
		if (_status == null)
		{
			return;
		}
		_status.text = StatusText();
		Paint(_hold, !OffServerVoice.AutoMode);
		Paint(_auto, OffServerVoice.AutoMode);
		for (int i = 0; i < 3; i++)
		{
			_levels[i].Label.text = string.Format(Text("level" + i), Tiles(OffServerVoice.Ranges[i]));
			Paint(_levels[i], OffServerVoice.Level == i, LevelColor(i));
		}
		_volume.text = Mathf.RoundToInt(OffServerVoice.Volume * 100f) + "%";
		Paint(_deaf, OffServerVoice.Deaf, BtnMuted);
		_sens.text = OffServerVoice.Sensitivity + 1 + "/" + OffServerVoice.SensitivityDb.Length;
		List<KeyValuePair<PlayerBehavior, float>> list = Nearby();
		_nearbyHead.text = string.Format(Text("nearby"), list.Count);
		for (int j = 0; j < _rows.Count; j++)
		{
			Row row = _rows[j];
			if (j >= list.Count)
			{
				row.EntityId = null;
				row.Go.SetActive(value: false);
				continue;
			}
			PlayerBehavior key = list[j].Key;
			row.Go.SetActive(value: true);
			row.EntityId = key.EntityId;
			bool flag = OffServerVoice.IsMuted(key.EntityId);
			bool flag2 = OffServerVoice.IsSpeaking(key.EntityId);
			row.Name.text = (flag2 ? ("[44ff66]" + PlainName(key.PlayerName) + "[-]") : PlainName(key.PlayerName));
			row.Info.text = string.Format(Text("dist"), Tiles(list[j].Value));
			row.Mute.Label.text = Text(flag ? "unmute" : "mute");
			Paint(row.Mute, flag, BtnMuted);
		}
	}

	private string StatusText()
	{
		if (OffServerVoice.GmMutedHours > 0)
		{
			return string.Format(Text("gm_muted"), OffServerVoice.GmMutedHours);
		}
		if (OffServerVoice.MicProblem != null)
		{
			return OffServerVoice.MicProblem;
		}
		if (OffServerVoice.Talking)
		{
			return string.Format(Text("talking"), Tiles(OffServerVoice.RangeTiles));
		}
		if (OffServerVoice.AutoMode)
		{
			return Text("auto_hint");
		}
		return Text(Application.isMobilePlatform ? "hold_hint_mobile" : "hold_hint_pc").Replace("{key}", OffServerHotkeys_KeyCaption()).Replace("{range}", OffServerHotkeys.CaptionOf("voice_range", "B"));
	}

	private static string OffServerHotkeys_KeyCaption()
	{
		return OffServerHotkeys.CaptionOf("voice_talk", "X");
	}

	private static string Tiles(float t)
	{
		return t.ToString((Mathf.Abs(t - Mathf.Round(t)) < 0.05f) ? "0" : "0.#", CultureInfo.InvariantCulture);
	}

	private static List<KeyValuePair<PlayerBehavior, float>> Nearby()
	{
		List<KeyValuePair<PlayerBehavior, float>> list = new List<KeyValuePair<PlayerBehavior, float>>();
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		if (localPlayer == null || localPlayer.MainTransform == null || !Singleton<PlayerManager>.Exist())
		{
			return list;
		}
		Vector3 position = localPlayer.MainTransform.position;
		foreach (PlayerBehavior player in Singleton<PlayerManager>.Instance().GetPlayers())
		{
			if (!(player == null) && !(player.MainTransform == null) && !string.IsNullOrEmpty(player.EntityId) && !(player.EntityId == GameManager.PlayerId))
			{
				Vector3 vector = player.MainTransform.position - position;
				vector.y = 0f;
				float num = vector.magnitude / 200f;
				if (num <= 12f || OffServerVoice.IsSpeaking(player.EntityId))
				{
					list.Add(new KeyValuePair<PlayerBehavior, float>(player, num));
				}
			}
		}
		list.Sort(delegate(KeyValuePair<PlayerBehavior, float> a, KeyValuePair<PlayerBehavior, float> b)
		{
			bool flag = OffServerVoice.IsSpeaking(a.Key.EntityId);
			bool flag2 = OffServerVoice.IsSpeaking(b.Key.EntityId);
			if (flag == flag2)
			{
				return a.Value.CompareTo(b.Value);
			}
			return (!flag) ? 1 : (-1);
		});
		if (list.Count > 5)
		{
			list.RemoveRange(5, list.Count - 5);
		}
		return list;
	}

	private static string PlainName(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return "?";
		}
		StringBuilder stringBuilder = new StringBuilder(name.Length);
		foreach (char c in name)
		{
			if (!char.IsSurrogate(c) && c != '\ufe0f' && c != '[' && c != ']')
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString().Trim();
	}

	public static void TickRings()
	{
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		bool flag = OffServerVoice.Available && localPlayer != null && localPlayer.MainTransform != null && (OffServerVoice.Talking || PanelOpen || Time.realtimeSinceStartup < _flashUntil);
		if (flag && _myRing == null)
		{
			_myRing = OffServerVoiceRing.Create("OffServerVoiceRing", small: false);
		}
		if (_myRing != null)
		{
			_myRing.Show(flag ? localPlayer.MainTransform : null, OffServerVoice.RangeTiles * 200f, OffServerVoice.Level, OffServerVoice.Talking);
		}
	}

	public static void SetupHeadIcon(UISprite icon)
	{
		if (!(icon == null) && ResourceSingleton<UISpriteManager>.Instance().TryGet("icon_stt", out var _, out var _))
		{
			icon.spriteName = "icon_stt";
			icon.width = 24;
			icon.height = 42;
		}
	}

	public static bool HeadIconFor(PlayerBehavior player)
	{
		if (player != null && !player.IsLocalPlayer)
		{
			return OffServerVoice.IsSpeaking(player.EntityId);
		}
		return false;
	}

	public static string DebugMicButton()
	{
		if (_micButton == null)
		{
			return "none";
		}
		string text = _micButton.name;
		Transform parent = _micButton.transform.parent;
		while (parent != null)
		{
			text = parent.name + "/" + text;
			parent = parent.parent;
		}
		string text2 = string.Empty;
		Camera camera = NGUITools.FindCameraForLayer(_micButton.layer);
		if (camera != null)
		{
			Vector3 vector = camera.WorldToScreenPoint(_micButton.transform.position);
			text2 = " screen " + Mathf.RoundToInt(vector.x) + "," + Mathf.RoundToInt((float)Screen.height - vector.y) + " of " + Screen.width + "x" + Screen.height;
		}
		string text3 = ((_micIcon != null) ? (" sprite " + _micIcon.spriteName + (_micIcon.isValid ? "" : " (MISSING)")) : " no sprite");
		return text + " active " + _micButton.activeInHierarchy + text3 + text2;
	}

	public static List<object> DebugHeadIcons()
	{
		List<object> list = new List<object>();
		PlayerFloatingControl[] array = UnityEngine.Object.FindObjectsByType<PlayerFloatingControl>();
		foreach (PlayerFloatingControl playerFloatingControl in array)
		{
			Transform transform = playerFloatingControl.transform.Find("Top/VoiceIcon");
			if (transform != null && transform.gameObject.activeInHierarchy)
			{
				UISprite component = transform.GetComponent<UISprite>();
				list.Add(((playerFloatingControl.Target != null) ? playerFloatingControl.Target.EntityId : "?") + " " + ((component != null) ? component.spriteName : "?"));
			}
		}
		return list;
	}

	public static string Text(string key)
	{
		if (!Texts.TryGetValue(key, out var value))
		{
			return key;
		}
		return OffServerDiscordLink.Lang switch
		{
			"th" => value[0],
			"id" => value[2],
			"ko" => value[3],
			_ => value[1],
		};
	}
}
