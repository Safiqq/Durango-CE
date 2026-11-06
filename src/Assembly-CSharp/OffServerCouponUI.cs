using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.Network;
using Durango.System;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using Messages;
using Shared.Economy;
using UnityEngine;

public class OffServerCouponUI : MonoBehaviour
{
	public enum Mode
	{
		Coupon,
		Move
	}

	private enum Kind
	{
		Empty,
		CoinOk,
		CoinBadChar,
		CoinLen,
		RewardOk,
		RewardLen,
		Move9,
		Move9Len,
		MoveKey,
		BotCode,
		Account
	}

	private struct Info
	{
		public Kind K;

		public string Norm;

		public string Bad;

		public int Len;
	}

	private struct Hint
	{
		public string Text;

		public Color Color;

		public string Icon;

		public bool Can;

		public bool Bad;
	}

	public const string CouponIntent = "offserver:coupon";

	private const int Depth = 5900;

	private const float ReplyTimeoutSec = 15f;

	private const string Alpha = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

	private static readonly Regex KeyShape = new Regex("^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$");

	private static OffServerCouponUI _coupon;

	private static OffServerCouponUI _move;

	private static SelectableButton _btnBorderTpl;

	private static SelectableButton _btnSolidTpl;

	private static UILabel _lblTpl;

	private static readonly Dictionary<string, UISprite> _surfaces = new Dictionary<string, UISprite>();

	private static readonly Color Beige = new Color(0.86f, 0.82f, 0.7f);

	private static readonly Color Gold = new Color(1f, 0.85f, 0.45f);

	private static readonly Color Grey = new Color(0.68f, 0.68f, 0.66f);

	private static readonly Color Orange = new Color(0.98f, 0.76f, 0.2f);

	private static readonly Color Red = new Color(1f, 0.32f, 0.32f);

	private static uint _sendSeq;

	public static string LastHint = string.Empty;

	public static string LastResult = string.Empty;

	private readonly Dictionary<string, SelectableButton> _buttons = new Dictionary<string, SelectableButton>(StringComparer.Ordinal);

	private readonly Dictionary<string, UISprite> _kindIcons = new Dictionary<string, UISprite>(StringComparer.Ordinal);

	private readonly Dictionary<string, UISprite> _resIcons = new Dictionary<string, UISprite>(StringComparer.Ordinal);

	private Mode _mode;

	private bool _fromConfig;

	private ConfigGroup _pageOwner;

	private UILabel _coinLabel;

	private int _plainInputX = 34;

	private int _iconInputX = 76;

	private UIInput _input;

	private UISprite _outline;

	private UILabel _hint;

	private UILabel _resTitle;

	private UILabel _resSub;

	private UILabel _resIdle;

	private SelectableButton _bUse;

	private bool _busy;

	private uint _busySeq;

	private float _sentAt;

	private long _coinBefore;

	private bool _hasResult;

	private bool _resOk;

	private string _resKind = string.Empty;

	private string _resTitleText = string.Empty;

	private string _resSubText = string.Empty;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"coupon_breadcrumb",
			new string[4] { "บ\u0e31ญช\u0e35 / ค\u0e39ปอง", "Account / Coupon", "Akun / Kupon", "계정 / 쿠폰" }
		},
		{
			"title_coupon",
			new string[4] { "ใส\u0e48ค\u0e39ปอง", "Enter Coupon", "Masukkan Kupon", "쿠폰 입력" }
		},
		{
			"title_move",
			new string[4] { "กรอกรห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง", "Enter Move Code", "Masukkan Kode Pindah", "이전 코드 입력" }
		},
		{
			"kind_coin",
			new string[4] { "โค\u0e49ดเต\u0e34ม Coin", "Coin top-up code", "Kode isi Coin", "Coin 충전 코드" }
		},
		{
			"kind_coin_sub",
			new string[4] { "DC-XXXX-XXXX · ได\u0e49จาก ticket\nเต\u0e34มเง\u0e34นในด\u0e34ส · Coin เข\u0e49าท\u0e31นท\u0e35", "DC-XXXX-XXXX · from a top-up\nticket on Discord · added at once", "DC-XXXX-XXXX · dari tiket isi ulang\ndi Discord · langsung masuk", "DC-XXXX-XXXX · 디스코드 충전\n티켓에서 받음 · 즉시 지급" }
		},
		{
			"kind_reward",
			new string[4] { "ค\u0e39ปองของรางว\u0e31ล", "Reward coupon", "Kupon hadiah", "보상 쿠폰" }
		},
		{
			"kind_reward_sub",
			new string[4] { "จากประกาศและก\u0e34จกรรม\nของรางว\u0e31ลส\u0e48งเข\u0e49าจดหมาย", "From announcements and events\nRewards go to your mail", "Dari pengumuman dan event\nHadiah dikirim ke surat", "공지와 이벤트에서 받음\n보상은 우편으로 지급" }
		},
		{
			"code",
			new string[4] { "โค\u0e49ด", "Code", "Kode", "코드" }
		},
		{
			"placeholder_coupon",
			new string[4] { "พ\u0e34มพ\u0e4cหร\u0e37อวางโค\u0e49ด", "Type or paste a code", "Ketik atau tempel kode", "코드 입력 또는 붙여넣기" }
		},
		{
			"placeholder_move",
			new string[4] { "รห\u0e31ส 9 หล\u0e31ก", "9-digit code", "Kode 9 digit", "9자리 코드" }
		},
		{
			"paste",
			new string[4] { "วาง", "Paste", "Tempel", "붙여넣기" }
		},
		{
			"close",
			new string[4] { "ป\u0e34ด", "Close", "Tutup", "닫기" }
		},
		{
			"cancel",
			new string[4] { "ยกเล\u0e34ก", "Cancel", "Batal", "취소" }
		},
		{
			"use",
			new string[4] { "ใช\u0e49โค\u0e49ด", "Use code", "Pakai kode", "코드 사용" }
		},
		{
			"move_use",
			new string[4] { "ย\u0e49ายมาเคร\u0e37\u0e48องน\u0e35\u0e49", "Move here", "Pindah ke sini", "이 기기로 이전" }
		},
		{
			"checking",
			new string[4] { "กำล\u0e31งตรวจ...", "Checking...", "Memeriksa...", "확인 중..." }
		},
		{
			"help",
			new string[4] { "ย\u0e31งไม\u0e48ม\u0e35โค\u0e49ดเต\u0e34ม Coin? เป\u0e34ด ticket เต\u0e34มเง\u0e34นในด\u0e34สของเซ\u0e34ร\u0e4cฟ · พ\u0e34มพ\u0e4c /topup ในแชทเพ\u0e37\u0e48อด\u0e39ว\u0e34ธ\u0e35เต\u0e34ม", "No top-up code yet? Open a top-up ticket on our Discord · type /topup in chat to see how", "Belum punya kode? Buka tiket isi ulang di Discord · ketik /topup di chat untuk caranya", "충전 코드가 없나요? 디스코드에서 충전 티켓을 여세요 · 채팅에 /topup 을 입력하면 방법이 나옵니다" }
		},
		{
			"h_empty",
			new string[4] { "ร\u0e31บโค\u0e49ดเต\u0e34ม Coin (DC-XXXX-XXXX) และค\u0e39ปองของรางว\u0e31ล", "Takes Coin top-up codes (DC-XXXX-XXXX) and reward coupons", "Menerima kode isi Coin (DC-XXXX-XXXX) dan kupon hadiah", "Coin 충전 코드(DC-XXXX-XXXX)와 보상 쿠폰을 받습니다" }
		},
		{
			"h_coin_ok",
			new string[4] { "✓ โค\u0e49ดเต\u0e34ม Coin · {0}", "✓ Coin top-up code · {0}", "✓ Kode isi Coin · {0}", "✓ Coin 충전 코드 · {0}" }
		},
		{
			"h_coin_bad",
			new string[4] { "โค\u0e49ดเต\u0e34ม Coin ไม\u0e48ถ\u0e39กต\u0e49อง — ม\u0e35ต\u0e31ว {0} ท\u0e35\u0e48โค\u0e49ดไม\u0e48ใช\u0e49 (ไม\u0e48ม\u0e35เลข 0/1 และต\u0e31ว O/I)", "Invalid top-up code — {0} is never used in codes (no 0/1 and no O/I)", "Kode isi tidak valid — {0} tidak dipakai di kode (tanpa 0/1 dan O/I)", "잘못된 충전 코드 — {0} 은(는) 코드에 쓰이지 않습니다 (0/1, O/I 없음)" }
		},
		{
			"h_coin_len",
			new string[4] { "โค\u0e49ดเต\u0e34ม Coin ต\u0e49องม\u0e35 8 ต\u0e31วหล\u0e31ง DC- · ตอนน\u0e35\u0e49 {0} ต\u0e31ว", "A top-up code has 8 characters after DC- · now {0}", "Kode isi punya 8 karakter setelah DC- · sekarang {0}", "충전 코드는 DC- 뒤에 8자입니다 · 현재 {0}자" }
		},
		{
			"h_reward_ok",
			new string[4] { "✓ ค\u0e39ปองของรางว\u0e31ล · {0}", "✓ Reward coupon · {0}", "✓ Kupon hadiah · {0}", "✓ 보상 쿠폰 · {0}" }
		},
		{
			"h_reward_len",
			new string[4] { "ค\u0e39ปองของรางว\u0e31ลยาว 8-16 ต\u0e31ว (A-Z 0-9) · ตอนน\u0e35\u0e49 {0} ต\u0e31ว", "Reward coupons are 8-16 characters (A-Z 0-9) · now {0}", "Kupon hadiah 8-16 karakter (A-Z 0-9) · sekarang {0}", "보상 쿠폰은 8-16자입니다 (A-Z 0-9) · 현재 {0}자" }
		},
		{
			"h_is_move",
			new string[4] { "น\u0e35\u0e48ค\u0e37อรห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง — ใช\u0e49ป\u0e38\u0e48ม «กรอกรห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง» ในต\u0e31\u0e49งค\u0e48า > บ\u0e31ญช\u0e35", "This is a device move code — use «Enter Move Code» in Settings > Account", "Ini kode pindah perangkat — pakai «Masukkan Kode Pindah» di Pengaturan > Akun", "기기 이전 코드입니다 — 설정 > 계정 > «이전 코드 입력»을 사용하세요" }
		},
		{
			"m_empty",
			new string[4] { "รห\u0e31ส 9 หล\u0e31ก · ค\u0e35ย\u0e4c XXXX-XXXX-XXXX · โค\u0e49ดบอท 6 ต\u0e31ว · หร\u0e37อ Account Number", "9-digit code · key XXXX-XXXX-XXXX · 6-letter bot code · or Account Number", "Kode 9 digit · kunci XXXX-XXXX-XXXX · kode bot 6 huruf · atau Account Number", "9자리 코드 · 키 XXXX-XXXX-XXXX · 봇 코드 6자 · 또는 Account Number" }
		},
		{
			"m_code_ok",
			new string[4] { "✓ รห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง · {0}", "✓ Move code · {0}", "✓ Kode pindah · {0}", "✓ 이전 코드 · {0}" }
		},
		{
			"m_code_len",
			new string[4] { "รห\u0e31สย\u0e49ายเคร\u0e37\u0e48องม\u0e35 9 หล\u0e31ก · ตอนน\u0e35\u0e49 {0} หล\u0e31ก", "A move code has 9 digits · now {0}", "Kode pindah punya 9 digit · sekarang {0}", "이전 코드는 9자리입니다 · 현재 {0}자리" }
		},
		{
			"m_key_ok",
			new string[4] { "✓ ค\u0e35ย\u0e4cย\u0e49ายเคร\u0e37\u0e48อง · {0}", "✓ Move key · {0}", "✓ Kunci pindah · {0}", "✓ 이전 키 · {0}" }
		},
		{
			"m_bot_ok",
			new string[4] { "✓ โค\u0e49ด 6 ต\u0e31วจากบอท Discord", "✓ 6-letter code from the Discord bot", "✓ Kode 6 huruf dari bot Discord", "✓ 디스코드 봇의 6자 코드" }
		},
		{
			"m_acc_ok",
			new string[4] { "✓ Account Number", "✓ Account Number", "✓ Account Number", "✓ Account Number" }
		},
		{
			"m_is_coin",
			new string[4] { "น\u0e35\u0e48ค\u0e37อโค\u0e49ดเต\u0e34ม Coin — ใช\u0e49ป\u0e38\u0e48ม «ใส\u0e48ค\u0e39ปอง»", "This is a Coin top-up code — use «Enter Coupon»", "Ini kode isi Coin — pakai «Masukkan Kupon»", "Coin 충전 코드입니다 — «쿠폰 입력»을 사용하세요" }
		},
		{
			"m_is_reward",
			new string[4] { "น\u0e35\u0e48ด\u0e39เหม\u0e37อนค\u0e39ปองของรางว\u0e31ล — ใช\u0e49ป\u0e38\u0e48ม «ใส\u0e48ค\u0e39ปอง»", "This looks like a reward coupon — use «Enter Coupon»", "Ini seperti kupon hadiah — pakai «Masukkan Kupon»", "보상 쿠폰 같습니다 — «쿠폰 입력»을 사용하세요" }
		},
		{
			"step1",
			new string[4] { "ท\u0e35\u0e48เคร\u0e37\u0e48องเด\u0e34ม: ต\u0e31\u0e49งค\u0e48า > บ\u0e31ญช\u0e35 > ย\u0e49ายเคร\u0e37\u0e48อง > ได\u0e49รห\u0e31ส 9 หล\u0e31ก (ใช\u0e49ได\u0e49 10 นาท\u0e35)", "On the old device: Settings > Account > Move Device > get a 9-digit code (10 minutes)", "Di perangkat lama: Pengaturan > Akun > Pindah Perangkat > dapat kode 9 digit (10 menit)", "기존 기기: 설정 > 계정 > 기기 이전 > 9자리 코드 받기 (10분 유효)" }
		},
		{
			"step2",
			new string[4] { "ท\u0e35\u0e48เคร\u0e37\u0e48องน\u0e35\u0e49: กรอกรห\u0e31สด\u0e49านล\u0e48าง แล\u0e49วกด «ย\u0e49ายมาเคร\u0e37\u0e48องน\u0e35\u0e49»", "On this device: enter the code below and press «Move here»", "Di perangkat ini: masukkan kode di bawah lalu tekan «Pindah ke sini»", "이 기기: 아래에 코드를 입력하고 «이 기기로 이전»을 누르세요" }
		},
		{
			"step_note",
			new string[4] { "ร\u0e31บค\u0e35ย\u0e4cย\u0e49ายเคร\u0e37\u0e48อง XXXX-XXXX-XXXX · โค\u0e49ดบอท 6 ต\u0e31ว · Account Number ด\u0e49วย", "Also takes a move key XXXX-XXXX-XXXX · 6-letter bot code · Account Number", "Juga menerima kunci XXXX-XXXX-XXXX · kode bot 6 huruf · Account Number", "이전 키 XXXX-XXXX-XXXX · 봇 코드 6자 · Account Number 도 받습니다" }
		},
		{
			"caution",
			new string[4] { "ต\u0e31วละครบนเคร\u0e37\u0e48องน\u0e35\u0e49จะสล\u0e31บเป\u0e47นของบ\u0e31ญช\u0e35เด\u0e34ม · เกมจะกล\u0e31บหน\u0e49า Title ให\u0e49เข\u0e49าใหม\u0e48", "This device switches to your old account's characters · the game returns to the title screen", "Perangkat ini beralih ke karakter akun lama · game kembali ke layar judul", "이 기기는 기존 계정의 캐릭터로 바뀝니다 · 타이틀 화면으로 돌아갑니다" }
		},
		{
			"idle_coupon",
			new string[4] { "ใส\u0e48โค\u0e49ดแล\u0e49วกด «ใช\u0e49โค\u0e49ด» · ผลข\u0e36\u0e49นท\u0e35\u0e48น\u0e35\u0e48", "Enter a code and press «Use code» · the result shows here", "Masukkan kode lalu tekan «Pakai kode» · hasil muncul di sini", "코드를 넣고 «코드 사용»을 누르세요 · 결과가 여기에 표시됩니다" }
		},
		{
			"idle_move",
			new string[4] { "ผลการย\u0e49ายเคร\u0e37\u0e48องข\u0e36\u0e49นท\u0e35\u0e48น\u0e35\u0e48", "The result shows here", "Hasil muncul di sini", "결과가 여기에 표시됩니다" }
		},
		{
			"ok_coin",
			new string[4] { "เต\u0e34ม Durango Coin แล\u0e49ว", "Durango Coin added", "Durango Coin ditambahkan", "Durango Coin 충전 완료" }
		},
		{
			"ok_coin_delta",
			new string[4] { "เต\u0e34ม Durango Coin +{0} แล\u0e49ว", "Durango Coin +{0} added", "Durango Coin +{0} ditambahkan", "Durango Coin +{0} 충전 완료" }
		},
		{
			"ok_coin_sub",
			new string[4] { "ยอด Coin ตอนน\u0e35\u0e49 {0}", "Coin balance now {0}", "Saldo Coin sekarang {0}", "현재 Coin 잔액 {0}" }
		},
		{
			"ok_reward",
			new string[4] { "ได\u0e49ร\u0e31บของรางว\u0e31ลแล\u0e49ว", "Reward received", "Hadiah diterima", "보상 지급 완료" }
		},
		{
			"ok_reward_sub",
			new string[4] { "ส\u0e48งเข\u0e49าจดหมายแล\u0e49ว — เป\u0e34ดร\u0e31บท\u0e35\u0e48จดหมาย", "Sent to your mail — open the mailbox to claim it", "Dikirim ke surat — buka kotak surat untuk mengambil", "우편으로 보냈습니다 — 우편함에서 받으세요" }
		},
		{
			"net_error",
			new string[4] { "ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49", "Couldn't reach the server", "Tidak dapat menghubungi server", "서버에 연결할 수 없습니다" }
		},
		{
			"net_error_sub",
			new string[4] { "ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Please try again", "Silakan coba lagi", "다시 시도하세요" }
		},
		{
			"fail",
			new string[4] { "ใช\u0e49โค\u0e49ดไม\u0e48สำเร\u0e47จ", "Couldn't use the code", "Kode gagal dipakai", "코드를 사용하지 못했습니다" }
		},
		{
			"clip_empty",
			new string[4] { "คล\u0e34ปบอร\u0e4cดว\u0e48าง — ค\u0e31ดลอกโค\u0e49ดก\u0e48อนแล\u0e49วกด «วาง»", "Clipboard is empty — copy the code first, then press «Paste»", "Papan klip kosong — salin kode dulu lalu tekan «Tempel»", "클립보드가 비어 있습니다 — 코드를 먼저 복사한 뒤 «붙여넣기»를 누르세요" }
		}
	};

	private int W
	{
		get
		{
			if (_mode != Mode.Coupon)
			{
				return 650;
			}
			return 1004;
		}
	}

	private int H
	{
		get
		{
			if (_mode != Mode.Coupon)
			{
				return 620;
			}
			return 584;
		}
	}

	public static bool CouponPageOpen
	{
		get
		{
			if (_coupon != null && _coupon.gameObject.activeInHierarchy)
			{
				return _coupon._pageOwner != null;
			}
			return false;
		}
	}

	public static bool IsOpen
	{
		get
		{
			if (!(_coupon != null) || !_coupon.gameObject.activeSelf)
			{
				if (_move != null)
				{
					return _move.gameObject.activeSelf;
				}
				return false;
			}
			return true;
		}
	}

	public static string OpenMode
	{
		get
		{
			if (_coupon != null && _coupon.gameObject.activeSelf)
			{
				return "coupon";
			}
			if (_move != null && _move.gameObject.activeSelf)
			{
				return "move";
			}
			return string.Empty;
		}
	}

	public static bool Busy
	{
		get
		{
			OffServerCouponUI current = Current;
			if (current != null)
			{
				return current._busy;
			}
			return false;
		}
	}

	private static OffServerCouponUI Current
	{
		get
		{
			if (_coupon != null && _coupon.gameObject.activeSelf)
			{
				return _coupon;
			}
			if (_move != null && _move.gameObject.activeSelf)
			{
				return _move;
			}
			return null;
		}
	}

	public static bool Open(Mode mode)
	{
		if (UIManager.MessageBox.IsShow)
		{
			return true;
		}
		OffServerCouponUI offServerCouponUI = Ensure(mode);
		if (offServerCouponUI == null)
		{
			return false;
		}
		Close();
		TooltipBase.HideNonModal(null);
		ConfigGroup configGroup = UIManager.FindScript<ConfigGroup>();
		offServerCouponUI._fromConfig = configGroup != null && configGroup.IsOpened;
		if (mode == Mode.Coupon)
		{
			if (!offServerCouponUI._fromConfig)
			{
				return false;
			}
			offServerCouponUI._pageOwner = configGroup;
			configGroup.ShowCouponContent(offServerCouponUI.transform);
		}
		offServerCouponUI._hasResult = false;
		offServerCouponUI._input.value = string.Empty;
		offServerCouponUI.gameObject.SetActive(value: true);
		offServerCouponUI.Fit();
		offServerCouponUI.Redraw();
		return true;
	}

	public static void Close()
	{
		if (_coupon != null && _coupon.gameObject.activeSelf)
		{
			_coupon.gameObject.SetActive(value: false);
		}
		if (_move != null && _move.gameObject.activeSelf)
		{
			_move.gameObject.SetActive(value: false);
		}
	}

	public static bool SetText(string text)
	{
		OffServerCouponUI current = Current;
		if (current == null)
		{
			return false;
		}
		current._input.value = text ?? string.Empty;
		current._hasResult = false;
		current.Redraw();
		return true;
	}

	public static bool Click(string key)
	{
		OffServerCouponUI current = Current;
		if (current == null || !current._buttons.TryGetValue(key ?? string.Empty, out var value) || value == null || value.Disabled || value.Clicked == null)
		{
			return false;
		}
		value.Clicked();
		return true;
	}

	public static List<string> Keys()
	{
		OffServerCouponUI current = Current;
		if (!(current == null))
		{
			return new List<string>(current._buttons.Keys);
		}
		return new List<string>();
	}

	private static OffServerCouponUI Ensure(Mode mode)
	{
		OffServerCouponUI offServerCouponUI = ((mode == Mode.Coupon) ? _coupon : _move);
		if (offServerCouponUI != null)
		{
			return offServerCouponUI;
		}
		if (!Singleton<UIManager>.HasInstance())
		{
			return null;
		}
		UIRoot uIRoot = Singleton<UIManager>.Instance().UIRoot;
		if (uIRoot == null || !FindTemplates())
		{
			UnityEngine.Debug.Log("[OffServer] coupon popup: no " + ((uIRoot == null) ? "UIRoot" : ("templates border=" + (_btnBorderTpl != null) + " solid=" + (_btnSolidTpl != null) + " lbl=" + (_lblTpl != null))));
			return null;
		}
		GameObject gameObject = uIRoot.gameObject.AddChild();
		gameObject.name = ((mode == Mode.Coupon) ? "OffServerCoupon" : "OffServerMoveCode");
		offServerCouponUI = gameObject.AddComponent<OffServerCouponUI>();
		offServerCouponUI._mode = mode;
		try
		{
			offServerCouponUI.Build();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
			UnityEngine.Object.Destroy(gameObject);
			return null;
		}
		gameObject.SetActive(value: false);
		if (mode == Mode.Coupon)
		{
			_coupon = offServerCouponUI;
		}
		else
		{
			_move = offServerCouponUI;
		}
		return offServerCouponUI;
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
		if (_mode == Mode.Coupon)
		{
			BuildCouponPage();
			return;
		}
		bool flag = _mode == Mode.Coupon;
		base.gameObject.AddComponent<UIPanel>().depth = 5900;
		UISprite uISprite = Box(0, 0, 8000, 8000, new Color(0f, 0f, 0f, 0.78f), 0);
		uISprite.transform.localPosition = Vector3.zero;
		NGUITools.AddWidgetCollider(uISprite.gameObject);
		Box(0, 0, W, H, new Color(0.11764706f, 9f / 85f, 7f / 85f, 1f), 1);
		Sprite("bg_tooltip_grunge", 0, 0, W, H, 2);
		Sprite("bg_tooltip_grunge2", 0, 0, W, H, 2);
		Box(0, 0, W, 61, new Color(0f, 0f, 0f, 0.45f), 3);
		Label(Text(flag ? "title_coupon" : "title_move"), 24, 8, 600, 46, 30, Color.white, UIWidget.Pivot.Left);
		Line(0, 60, W);
		int num;
		if (flag)
		{
			Box(0, 61, W, 110, new Color(0.1254902f, 0.11372549f, 0.08627451f, 0.94f), 3);
			Sprite(Durango.Logic.Item.Inventory.GetIcon(Currency.Coin), 20, 74, 46, 46, 5);
			Label(Text("kind_coin"), 78, 70, 240, 30, 22, Gold, UIWidget.Pivot.Left);
			Label(Text("kind_coin_sub"), 78, 100, 240, 60, 17, Grey, UIWidget.Pivot.Left);
			Sprite("bg_dotline_white2_height", 324, 78, 2, 80, 4).color = new Color(1f, 1f, 1f, 0.12f);
			Sprite("icon_gift", 335, 72, 50, 50, 5);
			Label(Text("kind_reward"), 393, 70, 240, 30, 22, Gold, UIWidget.Pivot.Left);
			Label(Text("kind_reward_sub"), 393, 100, 240, 60, 17, Grey, UIWidget.Pivot.Left);
			Line(0, 170, W);
			Label(Text("code"), 20, 182, 200, 30, 22, new Color(1f, 1f, 1f, 0.82f), UIWidget.Pivot.Left);
			num = 218;
		}
		else
		{
			Box(0, 61, W, 132, new Color(0.1254902f, 0.11372549f, 0.08627451f, 0.94f), 3);
			Step(1, Text("step1"), 70);
			Step(2, Text("step2"), 112);
			Label(Text("step_note"), 62, 152, 570, 32, 16, Grey, UIWidget.Pivot.Left);
			Line(0, 192, W);
			num = 210;
		}
		UISprite uISprite2 = Sprite("bg_roundbox2", 20, num, 490, 56, 4);
		uISprite2.color = new Color(11f / 51f, 0.20392157f, 0.1764706f, 1f);
		NGUITools.AddWidgetCollider(uISprite2.gameObject);
		UIEventListener.Get(uISprite2.gameObject).onClick = delegate
		{
			_input.isSelected = true;
		};
		_outline = Sprite("img_whitebox_thin_black", 20, num, 490, 56, 5);
		_kindIcons["coin"] = Sprite(Durango.Logic.Item.Inventory.GetIcon(Currency.Coin), 30, num + 10, 36, 36, 6);
		_kindIcons["gift"] = Sprite("icon_gift", 28, num + 8, 40, 40, 6);
		_kindIcons["caution"] = Sprite("icon_caution", 32, num + 12, 32, 32, 6);
		_kindIcons["move"] = Sprite("warp_elec_smartphone", 28, num + 8, 40, 40, 6);
		UILabel uILabel = Label(string.Empty, 76, num, 424, 56, 30, Color.white, UIWidget.Pivot.Left);
		uILabel.overflowMethod = UILabel.Overflow.ClampContent;
		uILabel.depth = 22;
		NGUITools.AddWidgetCollider(uILabel.gameObject);
		_input = uILabel.gameObject.AddComponent<UIInput>();
		_input.label = uILabel;
		_input.activeTextColor = Color.white;
		_input.characterLimit = 64;
		_input.onReturnKey = UIInput.OnReturnKey.Submit;
		_input.defaultText = Text(flag ? "placeholder_coupon" : "placeholder_move");
		EventDelegate.Add(_input.onChange, OnInputChanged);
		EventDelegate.Add(_input.onSubmit, Submit);
		Style(Button(_btnBorderTpl, "paste", Text("paste"), 520, num, 110, 56, Paste), PresetButton.Style.Border);
		_hint = Label(string.Empty, 20, num + 62, 610, 42, 19, Grey, UIWidget.Pivot.Left);
		_hint.overflowMethod = UILabel.Overflow.ShrinkContent;
		int num2;
		int num3;
		if (flag)
		{
			num2 = 326;
			num3 = 148;
		}
		else
		{
			Box(20, 322, 610, 64, new Color(0.886f, 0.427f, 0.2f, 0.16f), 3);
			Sprite("icon_caution", 34, 338, 32, 32, 5);
			Label(Text("caution"), 82, 322, 540, 64, 18, Beige, UIWidget.Pivot.Left);
			num2 = 396;
			num3 = 124;
		}
		Box(0, num2, W, num3, new Color(13f / 85f, 12f / 85f, 9f / 85f, 0.94f), 3);
		Line(0, num2, W);
		Line(0, num2 + num3 - 2, W);
		int num4 = num2 + num3 / 2;
		_resIcons["coin"] = Sprite(Durango.Logic.Item.Inventory.GetIcon(Currency.Coin), 28, num4 - 28, 56, 56, 6);
		_resIcons["mail"] = Sprite("icon_mainhud_mail", 26, num4 - 30, 60, 60, 6);
		_resIcons["caution"] = Sprite("icon_caution", 34, num4 - 22, 44, 44, 6);
		_resIcons["move"] = Sprite("warp_elec_smartphone", 26, num4 - 30, 60, 60, 6);
		_resTitle = Label(string.Empty, 104, num4 - 44, 526, 44, 26, Gold, UIWidget.Pivot.Left);
		_resSub = Label(string.Empty, 104, num4, 526, 48, 18, new Color(1f, 1f, 1f, 0.7f), UIWidget.Pivot.Left);
		_resIdle = Label(string.Empty, 0, num2, W, num3, 20, new Color(1f, 1f, 1f, 0.4f), UIWidget.Pivot.Center);
		if (flag)
		{
			Label(Text("help"), 20, 478, 610, 50, 17, new Color(1f, 1f, 1f, 0.55f), UIWidget.Pivot.Center);
		}
		Box(0, 530, W, 90, new Color(0f, 0f, 0f, 0.35f), 3);
		Line(0, 530, W);
		Style(Button(_btnBorderTpl, "close", Text(flag ? "close" : "cancel"), 11, 540, 244, 70, Close), PresetButton.Style.Border);
		_bUse = Button(_btnSolidTpl, "use", Text(flag ? "use" : "move_use"), 266, 540, 374, 70, Submit);
		Style(_bUse, PresetButton.Style.Solid);
	}

	private void BuildCouponPage()
	{
		base.gameObject.AddComponent<UIPanel>().depth = 5900;
		NGUITools.AddWidgetCollider(Box(0, 0, W, H, new Color(0.08f, 0.07f, 0.055f, 1f), 0).gameObject);
		Sprite("bg_contents_full", 0, 0, W, H, 1).color = new Color(1f, 1f, 1f, 0.78f);
		Style(Button(_btnBorderTpl, "close", "‹", 0, 0, 38, 32, Close), PresetButton.Style.Border);
		Label(Text("coupon_breadcrumb"), 52, 0, W - 52, 30, 18, Grey, UIWidget.Pivot.Left);
		Label(Text("title_coupon"), 0, 34, 640, 44, 34, Beige, UIWidget.Pivot.Left);
		Sprite(Durango.Logic.Item.Inventory.GetIcon(Currency.Coin), W - 224, 36, 38, 38, 5);
		_coinLabel = Label(string.Empty, W - 178, 32, 170, 44, 26, Gold, UIWidget.Pivot.Right);
		Line(0, 92, W);
		Sprite(Durango.Logic.Item.Inventory.GetIcon(Currency.Coin), 0, 112, 46, 46, 5);
		Label(Text("kind_coin"), 64, 106, 420, 34, 24, Gold, UIWidget.Pivot.Left);
		Label(Text("kind_coin_sub"), 64, 146, 420, 54, 18, Grey, UIWidget.Pivot.Left);
		Sprite("bg_dotline_white2_height", 500, 110, 2, 78, 4).color = new Color(1f, 1f, 1f, 0.12f);
		Sprite("icon_gift", 530, 110, 48, 48, 5);
		Label(Text("kind_reward"), 594, 106, 410, 34, 24, Gold, UIWidget.Pivot.Left);
		Label(Text("kind_reward_sub"), 594, 146, 410, 54, 18, Grey, UIWidget.Pivot.Left);
		Label(Text("code"), 0, 212, 600, 32, 22, Beige, UIWidget.Pivot.Left);
		UISprite uISprite = Sprite("bg_roundbox2", 0, 252, 844, 64, 4);
		uISprite.color = new Color(0.216f, 0.204f, 0.176f, 1f);
		NGUITools.AddWidgetCollider(uISprite.gameObject);
		UIEventListener.Get(uISprite.gameObject).onClick = delegate
		{
			_input.isSelected = true;
		};
		_outline = Sprite("img_whitebox_thin_black", 0, 252, 844, 64, 5);
		_kindIcons["coin"] = Sprite(Durango.Logic.Item.Inventory.GetIcon(Currency.Coin), 10, 266, 36, 36, 6);
		_kindIcons["gift"] = Sprite("icon_gift", 8, 264, 40, 40, 6);
		_kindIcons["caution"] = Sprite("icon_caution", 12, 268, 32, 32, 6);
		_plainInputX = 18;
		_iconInputX = 62;
		UILabel uILabel = Label(string.Empty, 62, 252, 764, 64, 30, Color.white, UIWidget.Pivot.Left);
		uILabel.overflowMethod = UILabel.Overflow.ClampContent;
		uILabel.depth = 22;
		NGUITools.AddWidgetCollider(uILabel.gameObject);
		_input = uILabel.gameObject.AddComponent<UIInput>();
		_input.label = uILabel;
		_input.activeTextColor = Color.white;
		_input.characterLimit = 64;
		_input.onReturnKey = UIInput.OnReturnKey.Submit;
		_input.defaultText = Text("placeholder_coupon");
		EventDelegate.Add(_input.onChange, OnInputChanged);
		EventDelegate.Add(_input.onSubmit, Submit);
		Style(Button(_btnBorderTpl, "paste", Text("paste"), 864, 252, 140, 64, Paste), PresetButton.Style.Border);
		_hint = Label(string.Empty, 0, 326, W, 38, 20, Grey, UIWidget.Pivot.Left);
		Line(0, 376, W);
		_resIcons["coin"] = Sprite(Durango.Logic.Item.Inventory.GetIcon(Currency.Coin), 8, 398, 48, 48, 6);
		_resIcons["mail"] = Sprite("icon_mainhud_mail", 4, 392, 60, 60, 6);
		_resIcons["caution"] = Sprite("icon_caution", 12, 402, 40, 40, 6);
		_resTitle = Label(string.Empty, 80, 388, W - 90, 40, 26, Gold, UIWidget.Pivot.Left);
		_resSub = Label(string.Empty, 80, 430, W - 90, 46, 20, Grey, UIWidget.Pivot.Left);
		_resIdle = Label(string.Empty, 0, 388, W, 80, 22, Grey, UIWidget.Pivot.Left);
		Line(0, 494, W);
		Label(Text("help"), 0, 510, 654, 64, 18, Grey, UIWidget.Pivot.Left);
		_bUse = Button(_btnSolidTpl, "use", Text("use"), 694, 510, 310, 64, Submit);
		Style(_bUse, PresetButton.Style.Solid);
	}

	private static void Style(SelectableButton button, PresetButton.Style style)
	{
		try
		{
			button.SetStyle(style);
		}
		catch (Exception)
		{
		}
	}

	private void Step(int n, string text, int y)
	{
		Sprite("circle_32", 20, y + 4, 30, 30, 5).color = Gold;
		Label(n.ToString(), 20, y + 4, 30, 30, 20, Color.black, UIWidget.Pivot.Center);
		Label(text, 62, y, 570, 38, 19, Color.white, UIWidget.Pivot.Left);
	}

	private Vector3 At(int x, int y, int w, int h)
	{
		return new Vector3(x + w / 2 - W / 2, H / 2 - (y + h / 2), 0f);
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

	private void Line(int x, int y, int w)
	{
		Sprite("bg_dotline_white2", x, y, w, 2, 4).color = new Color(1f, 1f, 1f, 0.12f);
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
		uILabel.transform.localPosition = new Vector3(num - W / 2, H / 2 - (y + h / 2), 0f);
		return uILabel;
	}

	private SelectableButton Button(SelectableButton tpl, string key, string text, int x, int y, int w, int h, Action onClick)
	{
		GameObject obj = UnityEngine.Object.Instantiate(tpl.gameObject, base.transform);
		obj.name = "Coupon_" + key;
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
		if (_mode == Mode.Coupon && _pageOwner != null)
		{
			UIWidget couponHost = _pageOwner.CouponHost;
			if (!(couponHost == null))
			{
				Vector3[] worldCorners = couponHost.worldCorners;
				Transform parent = base.transform.parent;
				Vector3 vector = parent.InverseTransformPoint(worldCorners[0]);
				Vector3 vector2 = parent.InverseTransformPoint(worldCorners[2]);
				float num = Mathf.Abs(vector2.x - vector.x);
				float num2 = Mathf.Abs(vector2.y - vector.y);
				base.transform.localScale = Vector3.one * Mathf.Max(0.1f, Mathf.Min((num - 40f) / (float)W, (num2 - 32f) / (float)H));
				base.transform.localPosition = (vector + vector2) * 0.5f;
			}
		}
		else
		{
			float num3 = Singleton<UIManager>.Instance().UIRoot.activeHeight;
			float num4 = num3 * (float)Screen.width / Math.Max(1f, Screen.height);
			base.transform.localScale = Vector3.one * Mathf.Min(1f, (num3 - 32f) / (float)H, (num4 - 32f) / (float)W);
		}
	}

	private void OnEnable()
	{
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
		if (_pageOwner != null)
		{
			_pageOwner.RestoreCouponContent();
			_pageOwner = null;
		}
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
		if (_busy && Time.realtimeSinceStartup - _sentAt > 15f)
		{
			_busy = false;
			SetResult(ok: false, "caution", Text("net_error"), Text("net_error_sub"));
		}
		if (_fromConfig)
		{
			ConfigGroup configGroup = UIManager.FindScript<ConfigGroup>();
			if (configGroup == null || !configGroup.IsOpened)
			{
				Close();
			}
		}
	}

	private void OnInputChanged()
	{
		if (_hasResult && !_busy)
		{
			_hasResult = false;
		}
		Redraw();
	}

	private void Redraw()
	{
		if (_input == null)
		{
			return;
		}
		if (_coinLabel != null)
		{
			_coinLabel.text = N(CoinBalance());
		}
		Info i = Classify(_input.value);
		Hint hint = ((_mode == Mode.Coupon) ? CouponHint(i) : MoveHint(i));
		_hint.text = hint.Text;
		_hint.color = hint.Color;
		_outline.color = (hint.Bad ? Red : ((hint.Can && !string.IsNullOrEmpty(hint.Icon)) ? new Color(1f, 0.85f, 0.45f, 0.9f) : new Color(1f, 1f, 1f, 0.18f)));
		foreach (KeyValuePair<string, UISprite> kindIcon in _kindIcons)
		{
			kindIcon.Value.enabled = kindIcon.Key == hint.Icon;
		}
		_input.label.transform.localPosition = new Vector3(((hint.Icon == null) ? _plainInputX : _iconInputX) - W / 2, _input.label.transform.localPosition.y, 0f);
		_bUse.Disabled = !hint.Can || _busy;
		_bUse.Text = (_busy ? Text("checking") : Text((_mode == Mode.Coupon) ? "use" : "move_use"));
		bool flag = _hasResult && !_busy;
		foreach (KeyValuePair<string, UISprite> resIcon in _resIcons)
		{
			resIcon.Value.enabled = flag && resIcon.Key == _resKind;
		}
		string text = _resSubText;
		if (flag && _resOk && _resKind == "coin")
		{
			long num = CoinBalance();
			long num2 = num - _coinBefore;
			_resTitleText = ((num2 > 0) ? Text("ok_coin_delta", N(num2)) : Text("ok_coin"));
			text = Text("ok_coin_sub", N(num));
		}
		_resTitle.text = (flag ? _resTitleText : string.Empty);
		_resTitle.color = (_resOk ? Gold : Red);
		_resSub.text = (flag ? text : string.Empty);
		_resIdle.text = (flag ? string.Empty : (_busy ? Text("checking") : Text((_mode == Mode.Coupon) ? "idle_coupon" : "idle_move")));
		LastHint = hint.Text;
		LastResult = (flag ? (_resTitleText + ((text.Length > 0) ? (" | " + text) : string.Empty)) : string.Empty);
	}

	private void SetResult(bool ok, string kind, string title, string sub)
	{
		_hasResult = true;
		_resOk = ok;
		_resKind = kind;
		_resTitleText = title ?? string.Empty;
		_resSubText = sub ?? string.Empty;
		Redraw();
	}

	private void Paste()
	{
		string text = string.Empty;
		try
		{
			text = GUIUtility.systemCopyBuffer ?? string.Empty;
		}
		catch (Exception)
		{
		}
		text = text.Replace("\r", " ").Replace("\n", " ").Trim();
		if (text.Length == 0)
		{
			UIManager.SystemMsg(Text("clip_empty"));
			return;
		}
		_input.value = ((text.Length > 64) ? text.Substring(0, 64) : text);
		_hasResult = false;
		Redraw();
	}

	private void Submit()
	{
		if (_busy || _input == null)
		{
			return;
		}
		string text = (_input.value ?? string.Empty).Trim();
		Info i = Classify(text);
		Hint obj = ((_mode == Mode.Coupon) ? CouponHint(i) : MoveHint(i));
		if (!obj.Can)
		{
			Redraw();
			return;
		}
		_busy = true;
		_sentAt = Time.realtimeSinceStartup;
		_hasResult = false;
		_coinBefore = CoinBalance();
		uint seq = ++_sendSeq;
		_busySeq = seq;
		Mode mode = _mode;
		Kind kind = i.K;
		string toyToken = ((mode == Mode.Coupon) ? "offserver:coupon" : (OffServerLink.Active ? OffServerLink.AccountId : Platform.Instance.Token));
		UnityEngine.Debug.Log("[OffServer] coupon popup (" + mode.ToString() + "): send " + kind);
		Connections.Frontend.Send(new AcceptTENCoupon
		{
			CouponNum = text,
			ToyToken = toyToken
		}).On<OK>(delegate
		{
			OnReply(seq, ok: true, kind, null);
		}).On(delegate(Abort msg, PacketHeader header)
		{
			OnReply(seq, ok: false, kind, msg.Text);
		});
		Redraw();
	}

	private void OnReply(uint seq, bool ok, Kind kind, string text)
	{
		if (seq != _busySeq)
		{
			return;
		}
		_busy = false;
		if (_mode == Mode.Move)
		{
			if (ok)
			{
				Close();
				if (kind == Kind.Move9)
				{
					OffServerMoveDevice.OnRedeemed();
				}
			}
			else
			{
				SplitServerText(text, out var title, out var sub);
				SetResult(ok: false, "move", title, sub);
			}
		}
		else if (ok)
		{
			_input.value = string.Empty;
			if (kind == Kind.CoinOk)
			{
				SetResult(ok: true, "coin", Text("ok_coin"), string.Empty);
			}
			else
			{
				SetResult(ok: true, "mail", Text("ok_reward"), Text("ok_reward_sub"));
			}
		}
		else
		{
			SplitServerText(text, out var title2, out var sub2);
			SetResult(ok: false, "caution", title2, sub2);
		}
	}

	private static void SplitServerText(string text, out string title, out string sub)
	{
		string text2 = OffServerDiscordLink.Clean(text ?? string.Empty);
		if (text2.Length == 0)
		{
			title = Text("fail");
			sub = string.Empty;
			return;
		}
		int num = text2.IndexOf(" — ", StringComparison.Ordinal);
		if (num > 0)
		{
			title = text2.Substring(0, num).Trim();
			sub = text2.Substring(num + 3).Trim();
		}
		else
		{
			title = text2;
			sub = string.Empty;
		}
	}

	private static long CoinBalance()
	{
		try
		{
			return Math.Max(0L, InventorySystem.Wallet.GetBalance(Currency.Coin));
		}
		catch (Exception)
		{
			return 0L;
		}
	}

	private static string N(long value)
	{
		return value.ToString("N0", CultureInfo.InvariantCulture);
	}

	private static Info Classify(string raw)
	{
		Info result = new Info
		{
			K = Kind.Empty,
			Norm = string.Empty,
			Bad = string.Empty
		};
		string text = (raw ?? string.Empty).Trim().ToUpperInvariant();
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = new StringBuilder();
		bool flag = true;
		string text2 = text;
		foreach (char c in text2)
		{
			if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
			{
				stringBuilder.Append(c);
			}
			if (c >= '0' && c <= '9')
			{
				stringBuilder2.Append(c);
			}
			else if (c != '-' && !char.IsWhiteSpace(c))
			{
				flag = false;
			}
		}
		string text3 = stringBuilder.ToString();
		string text4 = stringBuilder2.ToString();
		if (text3.Length == 0)
		{
			return result;
		}
		string text5 = text.Replace(" ", string.Empty);
		if (flag && text4.Length == 9)
		{
			result.K = Kind.Move9;
			result.Norm = text4;
			return result;
		}
		if (KeyShape.IsMatch(text5))
		{
			result.K = Kind.MoveKey;
			result.Norm = text5;
			return result;
		}
		if (text.Length >= 20 && text.IndexOf('-') >= 0 && text.IndexOf(' ') < 0)
		{
			result.K = Kind.Account;
			return result;
		}
		if (text3.Length == 6 && text3.Length == text.Length)
		{
			result.K = Kind.BotCode;
			return result;
		}
		if (text3.StartsWith("DC", StringComparison.Ordinal) && text3.Length <= 14)
		{
			string text6 = text3.Substring(2);
			List<string> list = new List<string>();
			text2 = text6;
			for (int i = 0; i < text2.Length; i++)
			{
				char value = text2[i];
				string item = "«" + value + "»";
				if ("ABCDEFGHJKLMNPQRSTUVWXYZ23456789".IndexOf(value) < 0 && !list.Contains(item))
				{
					list.Add(item);
				}
			}
			if (list.Count > 0)
			{
				result.K = Kind.CoinBadChar;
				result.Bad = string.Join(" ", list.ToArray());
				return result;
			}
			if (text6.Length != 8)
			{
				result.K = Kind.CoinLen;
				result.Len = text6.Length;
				return result;
			}
			result.K = Kind.CoinOk;
			result.Norm = "DC-" + text6.Substring(0, 4) + "-" + text6.Substring(4);
			return result;
		}
		if (flag && text4.Length > 3 && text4.Length < 12)
		{
			result.K = Kind.Move9Len;
			result.Len = text4.Length;
			return result;
		}
		bool flag2 = true;
		text2 = text5;
		foreach (char c2 in text2)
		{
			if ((c2 < 'A' || c2 > 'Z') && (c2 < '0' || c2 > '9') && c2 != '-')
			{
				flag2 = false;
				break;
			}
		}
		if (flag2 && text3.Length >= 8 && text3.Length <= 16)
		{
			result.K = Kind.RewardOk;
			result.Norm = text3;
			return result;
		}
		result.K = Kind.RewardLen;
		result.Len = text3.Length;
		return result;
	}

	private static Hint MakeHint(string text, Color color, string icon, bool can, bool bad = false)
	{
		return new Hint
		{
			Text = text,
			Color = color,
			Icon = icon,
			Can = can,
			Bad = bad
		};
	}

	private static Hint CouponHint(Info i)
	{
		return i.K switch
		{
			Kind.Empty => MakeHint(Text("h_empty"), Grey, null, can: false),
			Kind.CoinOk => MakeHint(Text("h_coin_ok", i.Norm), Color.white, "coin", can: true),
			Kind.CoinBadChar => MakeHint(Text("h_coin_bad", i.Bad), Red, "coin", can: false, bad: true),
			Kind.CoinLen => MakeHint(Text("h_coin_len", i.Len), Orange, "coin", can: false),
			Kind.RewardOk => MakeHint(Text("h_reward_ok", i.Norm), Color.white, "gift", can: true),
			Kind.RewardLen => MakeHint(Text("h_reward_len", i.Len), Orange, "gift", can: false),
			_ => MakeHint(Text("h_is_move"), Orange, "move", can: false),
		};
	}

	private static Hint MoveHint(Info i)
	{
		switch (i.K)
		{
		case Kind.Empty:
			return MakeHint(Text("m_empty"), Grey, null, can: false);
		case Kind.Move9:
			return MakeHint(Text("m_code_ok", i.Norm.Substring(0, 3) + " " + i.Norm.Substring(3, 3) + " " + i.Norm.Substring(6)), Color.white, "move", can: true);
		case Kind.Move9Len:
			return MakeHint(Text("m_code_len", i.Len), Orange, "move", can: false);
		case Kind.MoveKey:
			return MakeHint(Text("m_key_ok", i.Norm), Color.white, "move", can: true);
		case Kind.BotCode:
			return MakeHint(Text("m_bot_ok"), Color.white, "move", can: true);
		case Kind.Account:
			return MakeHint(Text("m_acc_ok"), Color.white, "move", can: true);
		case Kind.RewardOk:
		case Kind.RewardLen:
			return MakeHint(Text("m_is_reward"), Orange, "gift", can: false);
		default:
			return MakeHint(Text("m_is_coin"), Orange, "coin", can: false);
		}
	}

	public static string Text(string key, params object[] args)
	{
		if (!Texts.TryGetValue(key, out var value))
		{
			return key;
		}
		string text = OffServerDiscordLink.Lang switch
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
