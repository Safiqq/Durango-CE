using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DurangoLauncher;

public sealed class LauncherForm : Form
{
	private enum Auth
	{
		Login,
		Su1,
		Su2,
		Wait,
		Forgot,
		Reset,
		Linked,
		EmAsk,
		EmCode,
		NoId
	}

	private sealed record BannerInfo(string Kind, string? Msg = null, int N = -1);

	private enum Page
	{
		Home,
		News,
		Guide,
		Community,
		Chars,
		Chat,
		Market,
		Trade,
		Calendar,
		Coupon
	}

	private sealed record Hit(Rectangle R, string Id, Action? Click, bool Enabled);

	private sealed class OfferDraftUi
	{
		public readonly OfferForm F;

		public string? Err;

		public bool Busy;

		public bool PlayersBusy;

		public bool ItemsBusy;

		public TradePlayer[]? Players;

		public string PlayersFor = "";

		public TradeItem[]? Items;

		public string? ItemsFor;

		public OfferMode Mode => F.Mode;

		public OfferDraftUi(OfferForm f)
		{
			F = f;
		}
	}

	private bool _aeOpen;

	private bool _measuring;

	private int _aeStep = 1;

	private EmailSent? _aeSent;

	private DateTime _aeSentAt;

	private DateTime _aeResendAt;

	private const int CardW = 372;

	private const int AccW = 324;

	private static readonly Color K4 = Color.FromArgb(58, 51, 82);

	private static readonly Color DiscordBlue = Color.FromArgb(88, 101, 242);

	private static readonly Color DiscordLight = Color.FromArgb(174, 182, 255);

	private Auth _scr;

	private BannerInfo? _banner;

	private Session? _session;

	private AccountConfig _acct = AccountConfig.Default;

	private bool _authBusy;

	private bool _restoring;

	private bool _restoreStarted;

	private DateTime _lockUntil;

	private bool _remember = true;

	private string _waitFor = "signup";

	private DateTime _waitStart;

	private int _waitSecs = 300;

	private string? _waitUrl;

	private CancellationTokenSource? _waitCts;

	private bool _waitUserCancel;

	private string? _signupToken;

	private string? _resetToken;

	private string? _resetUser;

	private string? _linkedUser;

	private DiscordUser? _pendingDiscord;

	private string? _loginToken;

	private bool _waitGot;

	private AccountFeatures _features = AccountFeatures.Off;

	private DateTime _featuresAt;

	private string _emailFor = "signup";

	private string _suVia = "discord";

	private string _resetVia = "discord";

	private string _verifiedEmail = "";

	private EmailSent? _emailSent;

	private DateTime _emailSentAt;

	private DateTime _resendAt;

	private CancellationTokenSource? _linkCts;

	private DateTime _linkStart;

	private string? _linkUrl;

	private bool _linkUserCancel;

	private readonly Dictionary<string, TextBox> _boxes = new Dictionary<string, TextBox>();

	private readonly HashSet<string> _boxesUsed = new HashSet<string>();

	private readonly Dictionary<string, (string Th, string En)> _fieldErr = new Dictionary<string, (string, string)>();

	private readonly HashSet<string> _red = new HashSet<string>();

	private string? _focusNext;

	private int _tabOrder;

	private bool _chpwOpen;

	private (string Text, string Icon, Color Color)? _toast;

	private DateTime _toastUntil;

	private readonly Dictionary<string, Bitmap?> _avatars = new Dictionary<string, Bitmap>();

	private Rectangle _cardRect;

	private static readonly StringFormat LeftWrap = new StringFormat
	{
		Trimming = (StringTrimming)3,
		FormatFlags = (StringFormatFlags)8192
	};

	private static readonly StringFormat CenterWrap = new StringFormat
	{
		Alignment = (StringAlignment)1,
		Trimming = (StringTrimming)3,
		FormatFlags = (StringFormatFlags)8192
	};

	private static readonly Color CalPanel = Color.FromArgb(38, 32, 56);

	private static readonly Color CalCell = Color.FromArgb(12, 10, 20);

	private static readonly Color CalCellHover = Color.FromArgb(8, 7, 15);

	private static readonly Color CalCellBorder = Color.FromArgb(122, 112, 163);

	private static readonly Color CalCellSel = Color.FromArgb(84, 68, 49);

	private CalendarState _cal = new CalendarState();

	private string? _calGw;

	private int _calGen;

	private bool _calBusy;

	private DateTime _calNext;

	private DateTime _calAttemptAt;

	private int _calListScroll;

	private int _calDetScroll;

	private int _calDetMax;

	private Rectangle _calListRect;

	private Rectangle _calDetRect;

	private readonly Dictionary<string, Bitmap?> _calIcons = new Dictionary<string, Bitmap>();

	private readonly CouponState _cp = new CouponState();

	private CouponHistory? _cpHist;

	private string? _cpHistErr;

	private string? _cpChar;

	private bool _cpHistBusy;

	private bool _cpHistAgain;

	private int _cpHistScroll;

	private int _cpGen;

	private Rectangle _cpHistRect;

	private const int BaseW = 1160;

	private const int BaseH = 690;

	private const int TopH = 56;

	private const int SideW = 236;

	private const int BarH = 84;

	private const int PadX = 28;

	private const string DiscordUrl = "https://discord.gg/RMfeg6Drbu";

	private const string FacebookUrl = "https://www.facebook.com/share/g/1BZp6apPM8/";

	private static readonly string[] ThaiMonths = new string[12]
	{
		"ม.ค.", "ก.พ.", "ม\u0e35.ค.", "เม.ย.", "พ.ค.", "ม\u0e34.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.",
		"พ.ย.", "ธ.ค."
	};

	private static readonly string[] EnMonths = new string[12]
	{
		"Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct",
		"Nov", "Dec"
	};

	private static readonly (string Icon, string Th, string En, string Page)[] GuideLinks = new(string, string, string, string)[5]
	{
		("compass", "เร\u0e34\u0e48มต\u0e49นเล\u0e48น", "Getting started", "getting-started"),
		("hammer", "คราฟต\u0e4cและแปรร\u0e39ป", "Crafting and processing", "crafting"),
		("paw", "ส\u0e31ตว\u0e4cและการฝ\u0e36ก", "Animals and taming", "taming"),
		("home-2", "ท\u0e35\u0e48ด\u0e34นและบ\u0e49าน", "Land and housing", "land"),
		("book", "เป\u0e34ดค\u0e39\u0e48ม\u0e37อท\u0e31\u0e49งหมด", "Open the full guide", "")
	};

	private readonly Bitmap? _hero = Ui.LoadImage("hero.jpg");

	private readonly Bitmap? _logo = Ui.LoadImage("logo.png");

	private Bitmap? _heroCache;

	private readonly Dictionary<(float, bool), Font> _fonts = new Dictionary<(float, bool), Font>();

	private float _s = 1f;

	private readonly List<Hit> _hits = new List<Hit>();

	private string? _hover;

	private string? _pressed;

	private Page _page;

	private int _newsScroll;

	private int _newsMax;

	private int _newsFocus = -1;

	private GameInstall? _game;

	private UpdateInfo? _info;

	private bool _checking;

	private bool _synced;

	private bool _launching;

	private string? _busy;

	private string _phase = "verify";

	private CancellationTokenSource? _cts;

	private double _progress = -1.0;

	private Func<string> _progressText = () => "";

	private Func<string> _status = () => L.T("กำล\u0e31งตรวจสอบ...", "Checking...");

	private Color _statusColor = Ui.TextDim;

	private Countdown? _countdown;

	private TimeSpan _skew;

	private int? _lastOnline;

	private bool _cdShown;

	private DateTime _onlineUpUntil;

	private readonly System.Windows.Forms.Timer _anim = new System.Windows.Forms.Timer
	{
		Interval = 33
	};

	private readonly System.Windows.Forms.Timer _clock = new System.Windows.Forms.Timer
	{
		Interval = 1000
	};

	private readonly System.Windows.Forms.Timer _onlineTimer = new System.Windows.Forms.Timer
	{
		Interval = 30000
	};

	private int _animT;

	private Rectangle _progressRect;

	private Rectangle _mainRect;

	private static readonly bool TestMode = Environment.GetEnvironmentVariable("DURANGO_LAUNCHER_TEST") == "1";

	private static readonly Color Gbg = Color.FromArgb(22, 51, 35);

	private static readonly Color Rbg = Color.FromArgb(58, 26, 23);

	private static readonly Color Ibg = Color.FromArgb(42, 32, 80);

	private const int LtItemsMs = 30000;

	private const int LtChatMs = 3000;

	private const int LtChatIdleMs = 15000;

	private const int LtOffersMs = 10000;

	private const int LtSummaryMs = 30000;

	private const int LtFeaturesMs = 300000;

	private LauncherTradeApi? _lt;

	private Session? _ltSession;

	private LauncherFeatures _ltf = LauncherFeatures.Off;

	private DateTime _ltfAt;

	private bool _ltfBusy;

	private TradeSummary? _summary;

	private readonly TradeGate _gate = new TradeGate();

	private readonly Backoff _sumBack = new Backoff(30000);

	private readonly Backoff _itemBack = new Backoff(30000);

	private readonly Backoff _chatBack = new Backoff(3000);

	private readonly Backoff _offerBack = new Backoff(10000);

	private readonly Backoff _ochatBack = new Backoff(10000);

	private DateTime _sumNext;

	private DateTime _itemNext;

	private DateTime _chatNext;

	private DateTime _offerNext;

	private DateTime _ochatNext;

	private bool _sumBusy;

	private bool _itemBusy;

	private bool _chatBusy;

	private bool _offerBusy;

	private bool _ochatBusy;

	private bool _charsBusy;

	private readonly System.Windows.Forms.Timer _poll = new System.Windows.Forms.Timer
	{
		Interval = 400
	};

	private readonly IconCache _icons = new IconCache();

	private int _ltGen;

	private string? _ltStatus;

	private Color _ltStatusColor = Ui.TextMute;

	private CharactersResult? _chars;

	private string? _charSel;

	private string? _speaker;

	private string? _buyer;

	private string? _trader;

	private ItemsResult? _items;

	private string? _itemsFor;

	private string _bagTab = "bag";

	private string _cat = "all";

	private string? _itemSel;

	private int _itemScroll;

	private readonly Dictionary<string, PriceResult?> _prices = new Dictionary<string, PriceResult>();

	private readonly Dictionary<string, long> _listedPrice = new Dictionary<string, long>();

	private string? _listedFor;

	private string? _itemsErr;

	private readonly List<ChatMsg> _chatMsgs = new List<ChatMsg>();

	private long _chatSince;

	private long _chatSeen;

	private ChatHistory? _chatInfo;

	private DateTime _chatCdUntil;

	private string? _chatErr;

	private bool _chatSending;

	private bool _chatOff;

	private bool _chatPrefBusy;

	private int _chatScroll;

	private readonly List<(long T, string Text)> _chatSys = new List<(long, string)>();

	private (Rectangle Anchor, string[] Items, int Sel, Action<int> Pick)? _dd;

	private string _mTab = "buy";

	private string _mGroup = "all";

	private string _mSort = "price_asc";

	private string _auMode = "search";

	private string _auKind = "bidding";

	private int _mLv;

	private int _mSkip;

	private int _mScroll;

	private bool _mFav;

	private bool _mPending;

	private bool _mBusy;

	private bool _buyBusy;

	private bool _collectBusy;

	private bool _favBusy;

	private bool _bidBusy;

	private bool _claimBusy;

	private MarketSearch? _market;

	private MarketMine? _mine;

	private AuctionResult? _au;

	private string? _mSel;

	private string? _mErr;

	private string? _mNote;

	private readonly BuyFlow _buy = new BuyFlow();

	private string _tTab = "in";

	private readonly Dictionary<string, TradeOffer[]> _offers = new Dictionary<string, TradeOffer[]>();

	private string? _tSel;

	private string? _tErr;

	private string? _tChatFor;

	private string? _tChatErr;

	private bool _offerActBusy;

	private bool _tChatSending;

	private readonly List<OfferChatMsg> _tChat = new List<OfferChatMsg>();

	private long _tChatSince;

	private long _offerSkew;

	private OfferChat? _tChatInfo;

	private int _tListScroll;

	private int _tChatScroll;

	private Rectangle _tChatRect;

	private readonly AcceptFlow _accept = new AcceptFlow();

	private double _feeRate = -1.0;

	private (string Id, string Act, DateTime Until)? _tConfirm;

	private OfferDraftUi? _od;

	private DateTime _odTyped;

	private int _odScroll;

	private bool ModalOpen
	{
		get
		{
			if (!_chpwOpen)
			{
				return _aeOpen;
			}
			return true;
		}
	}

	private bool ShowAddEmailRow
	{
		get
		{
			Session session = _session;
			if (session != null)
			{
				return _features.CanAddEmail(session.HasDiscord, session.Email);
			}
			return false;
		}
	}

	private bool LoggedIn => _session != null;

	private bool NeedLogin
	{
		get
		{
			if (_session == null)
			{
				return _acct.Required;
			}
			return false;
		}
	}

	private bool Locked => DateTime.UtcNow < _lockUntil;

	private bool AuthAnimating
	{
		get
		{
			bool flag = _session == null && _page == Page.Home;
			if (flag)
			{
				Auth scr = _scr;
				bool flag2 = ((scr == Auth.Wait || scr == Auth.EmCode) ? true : false);
				flag = flag2 || _authBusy;
			}
			if (!flag && !_restoring && (!ModalOpen || !_authBusy))
			{
				return _linkCts != null;
			}
			return true;
		}
	}

	private bool ShowDiscordViaRow
	{
		get
		{
			Session session = _session;
			if (session != null)
			{
				return LoginVia.ShowDiscordRow(session.Via, session.HasDiscord);
			}
			return false;
		}
	}

	private static long Tick => Stopwatch.GetTimestamp();

	private bool AccountSection => TradeMenu.ShowSection(LoggedIn, _ltf, TradeMenu.ShowCalendar(_cal));

	private int CalBadge => _cal.LiveCount(Tick);

	private bool CouponAnimating
	{
		get
		{
			if (_page == Page.Coupon)
			{
				if (!_cp.Busy)
				{
					return _cpHistBusy;
				}
				return true;
			}
			return false;
		}
	}

	private IReadOnlyList<TradeCharacter> CouponChars => _chars?.Characters ?? Array.Empty<TradeCharacter>();

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ClassStyle |= 0x20000;
			createParams.Style |= 0x20000;
			return createParams;
		}
	}

	protected override bool ShowWithoutActivation => TestMode;

	private bool Idle
	{
		get
		{
			if (!_checking && _busy == null)
			{
				return !_launching;
			}
			return false;
		}
	}

	private bool Installed => _game?.Installed ?? false;

	private bool Blocked
	{
		get
		{
			if (Installed && _info != null && !string.IsNullOrEmpty(_info.MinVersion))
			{
				return GameInstall.CompareVersion(_game.Version, _info.MinVersion) < 0;
			}
			return false;
		}
	}

	private bool PlayEnabled
	{
		get
		{
			if (Installed && _info != null && Idle && !Blocked && !NeedLogin && !_restoring)
			{
				return !_authBusy;
			}
			return false;
		}
	}

	private DateTimeOffset Now => DateTimeOffset.UtcNow + _skew;

	private Countdown? VisibleCountdown
	{
		get
		{
			Countdown countdown = _countdown;
			if ((object)countdown == null || countdown.PhaseAt(Now) == Countdown.Phase.Hidden)
			{
				return null;
			}
			return countdown;
		}
	}

	private bool TradeSection => TradeMenu.ShowSection(LoggedIn, _ltf);

	private bool TradeAnimating
	{
		get
		{
			if (!_charsBusy && !_itemBusy && !_chatSending && !_buyBusy && !_offerActBusy)
			{
				OfferDraftUi? od = _od;
				if ((od == null || !od.Busy) && (!_calBusy || _page != Page.Calendar))
				{
					return CouponAnimating;
				}
			}
			return true;
		}
	}

	private bool TradePage
	{
		get
		{
			Page page = _page;
			if ((uint)(page - 4) <= 3u || page == Page.Coupon)
			{
				return true;
			}
			return false;
		}
	}

	private long NowUnix => TradeFmt.NowUnix();

	private TradeOffer? SelectedOffer => OffersOf(_tTab).FirstOrDefault((TradeOffer o) => o.Id == _tSel) ?? OffersOf(_tTab).FirstOrDefault();

	private void OpenAddEmail()
	{
		string[] array = new string[3] { "ae_m", "ae_p", "ae_c" };
		foreach (string key in array)
		{
			((Control)_boxes[key]).Text = "";
		}
		_boxes["ae_p"].UseSystemPasswordChar = true;
		_aeSent = null;
		_aeOpen = true;
		AeStep(1);
	}

	private void CloseAddEmail()
	{
		string[] array = new string[3] { "ae_m", "ae_p", "ae_c" };
		foreach (string key in array)
		{
			((Control)_boxes[key]).Text = "";
		}
		_fieldErr.Clear();
		_banner = null;
		_aeSent = null;
		_aeOpen = false;
		if (((ContainerControl)this).ActiveControl is TextBox)
		{
			((ContainerControl)this).ActiveControl = null;
		}
		((Control)this).Invalidate();
	}

	private void AeStep(int step)
	{
		_aeStep = step;
		_fieldErr.Clear();
		_red.Clear();
		_banner = null;
		_focusNext = ((step == 2) ? "ae_c" : ((((Control)_boxes["ae_m"]).Text.Length > 0) ? "ae_p" : "ae_m"));
		if (((ContainerControl)this).ActiveControl is TextBox)
		{
			((ContainerControl)this).ActiveControl = null;
		}
		((Control)this).Invalidate();
	}

	private async Task SubmitAddEmailStartAsync(bool resend = false)
	{
		Session s = _session;
		if (_authBusy || s == null)
		{
			return;
		}
		string em = EmailInput.Clean((!resend) ? ((Control)_boxes["ae_m"]).Text : _aeSent?.Email);
		string text = ((Control)_boxes["ae_p"]).Text;
		if (!EmailInput.LooksOk(em))
		{
			_fieldErr["ae_m"] = ("อ\u0e35เมลไม\u0e48ถ\u0e39กต\u0e49อง — เช\u0e48น name@gmail.com", "Not a valid email — e.g. name@gmail.com");
			_focusNext = "ae_m";
			((Control)this).Invalidate();
			return;
		}
		if (text.Length == 0)
		{
			_fieldErr["ae_p"] = ("ใส\u0e48รห\u0e31สผ\u0e48านป\u0e31จจ\u0e38บ\u0e31น", "Enter your current password");
			_focusNext = "ae_p";
			((Control)this).Invalidate();
			return;
		}
		_fieldErr.Remove("ae_m");
		_fieldErr.Remove("ae_p");
		SetAuthBusy(busy: true);
		try
		{
			EmailSent emailSent = await AccountApi.EmailStartAsync(_acct, em, "add", s.RefreshToken, text);
			if (_aeOpen && _session == s)
			{
				_aeSent = emailSent;
				_aeSentAt = DateTime.UtcNow;
				_aeResendAt = _aeSentAt.AddSeconds(Math.Clamp(emailSent.ResendIn, 1, 600));
				((Control)_boxes["ae_c"]).Text = "";
				Log.Write("ขอรห\u0e31สเพ\u0e34\u0e48มอ\u0e35เมล");
				if (resend)
				{
					_banner = new BannerInfo("em-resent");
					_fieldErr.Remove("ae_c");
					_focusNext = "ae_c";
				}
				else
				{
					AeStep(2);
				}
			}
		}
		catch (ApiError apiError)
		{
			if (!_aeOpen || _session != s)
			{
				return;
			}
			bool flag = resend;
			if (flag)
			{
				bool flag2;
				switch (apiError.Code)
				{
				case "bad_credentials":
				case "invalid_credentials":
				case "locked":
				case "email_taken":
				case "email_invalid":
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = flag2;
			}
			if (flag)
			{
				AeStep(1);
			}
			Dictionary<string, (string Th, string En)> fieldErr;
			(string, string) value;
			switch (apiError.Code)
			{
			case "bad_credentials":
			case "invalid_credentials":
			{
				((Control)_boxes["ae_p"]).Text = "";
				fieldErr = _fieldErr;
				int? attemptsLeft = apiError.AttemptsLeft;
				if (attemptsLeft.HasValue)
				{
					int valueOrDefault = attemptsLeft.GetValueOrDefault();
					if (valueOrDefault >= 0)
					{
						value = ($"รห\u0e31สผ\u0e48านป\u0e31จจ\u0e38บ\u0e31นไม\u0e48ถ\u0e39กต\u0e49อง — ผ\u0e34ดได\u0e49อ\u0e35ก {valueOrDefault} คร\u0e31\u0e49งก\u0e48อนถ\u0e39กพ\u0e31ก 10 นาท\u0e35", $"Current password is incorrect — {valueOrDefault} more {((valueOrDefault == 1) ? "try" : "tries")} before a 10-minute lock");
						goto IL_04de;
					}
				}
				value = ("รห\u0e31สผ\u0e48านป\u0e31จจ\u0e38บ\u0e31นไม\u0e48ถ\u0e39กต\u0e49อง", "Current password is incorrect");
				goto IL_04de;
			}
			case "locked":
				_fieldErr["ae_p"] = (apiError.Message, apiError.Message);
				break;
			case "email_invalid":
				_fieldErr["ae_m"] = (apiError.Message, apiError.Message);
				_focusNext = "ae_m";
				break;
			case "email_taken":
				_fieldErr["ae_m"] = ("อ\u0e35เมลน\u0e35\u0e49ม\u0e35ไอด\u0e35อ\u0e37\u0e48นใช\u0e49แล\u0e49ว — 1 อ\u0e35เมลใช\u0e49ได\u0e49 1 ไอด\u0e35", "This email already belongs to another ID — one email per ID");
				_focusNext = "ae_m";
				break;
			case "email_already_set":
				EmailAlreadySet(s, apiError);
				break;
			case "resend_wait":
				_aeResendAt = DateTime.UtcNow.AddSeconds(Math.Clamp(apiError.RetryAfter ?? 60, 1, 600));
				if (!resend && _aeSent != null && _aeSent.Email == em.ToLowerInvariant())
				{
					AeStep(2);
				}
				else
				{
					_banner = new BannerInfo("error", apiError.Message);
				}
				break;
			default:
				{
					if (apiError.IsAuthDead)
					{
						CloseAddEmail();
						SignOutLocal(new BannerInfo("relogin"), s.Username);
					}
					else
					{
						_banner = new BannerInfo("error", apiError.Message);
					}
					break;
				}
				IL_04de:
				fieldErr["ae_p"] = value;
				_focusNext = "ae_p";
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Write("account: " + ex);
			_banner = new BannerInfo("error");
		}
		finally
		{
			SetAuthBusy(busy: false);
		}
	}

	private async Task SubmitAddEmailVerifyAsync()
	{
		Session s = _session;
		EmailSent sent = _aeSent;
		if (_authBusy || s == null || sent == null)
		{
			return;
		}
		string text = EmailInput.CleanCode(((Control)_boxes["ae_c"]).Text);
		if (text.Length != 6)
		{
			_fieldErr["ae_c"] = ("ใส\u0e48รห\u0e31ส 6 หล\u0e31กจากอ\u0e35เมล", "Enter the 6-digit code from the email");
			_focusNext = "ae_c";
			((Control)this).Invalidate();
			return;
		}
		SetAuthBusy(busy: true);
		try
		{
			EmailVerified emailVerified = await AccountApi.EmailVerifyAsync(_acct, sent.EmailId, text);
			if (_session == s)
			{
				s.Email = emailVerified.Email.OrIfEmpty(sent.Email);
				s.Save();
				CloseAddEmail();
				Log.Write("เพ\u0e34\u0e48มอ\u0e35เมลให\u0e49ไอด\u0e35 " + s.Username + " แล\u0e49ว");
				Toast("เพ\u0e34\u0e48มอ\u0e35เมลแล\u0e49ว — ล\u0e37มรห\u0e31สผ\u0e48านก\u0e39\u0e49ทางอ\u0e35เมลได\u0e49", "Email added — you can now reset your password by email", "mail");
			}
		}
		catch (ApiError apiError)
		{
			if (!_aeOpen || _session != s)
			{
				return;
			}
			((Control)_boxes["ae_c"]).Text = "";
			_focusNext = "ae_c";
			switch (apiError.Code)
			{
			case "code_invalid":
				if ((apiError.AttemptsLeft ?? 1) > 0)
				{
					_fieldErr["ae_c"] = ($"รห\u0e31สไม\u0e48ถ\u0e39กต\u0e49อง — ลองได\u0e49อ\u0e35ก {apiError.AttemptsLeft} คร\u0e31\u0e49ง", $"Wrong code — {apiError.AttemptsLeft} {((apiError.AttemptsLeft == 1) ? "try" : "tries")} left");
					break;
				}
				goto case "code_expired";
			case "code_expired":
				_banner = new BannerInfo("code-expired");
				_aeResendAt = ((DateTime.UtcNow < _aeResendAt) ? _aeResendAt : DateTime.UtcNow);
				break;
			case "email_taken":
				AeStep(1);
				_fieldErr["ae_m"] = ("อ\u0e35เมลน\u0e35\u0e49ม\u0e35ไอด\u0e35อ\u0e37\u0e48นใช\u0e49แล\u0e49ว — 1 อ\u0e35เมลใช\u0e49ได\u0e49 1 ไอด\u0e35", "This email already belongs to another ID — one email per ID");
				break;
			case "email_already_set":
				EmailAlreadySet(s, apiError);
				break;
			default:
				_banner = new BannerInfo("error", apiError.Message);
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Write("account: " + ex);
			_banner = new BannerInfo("error");
		}
		finally
		{
			SetAuthBusy(busy: false);
		}
	}

	private async void EmailAlreadySet(Session s, ApiError e)
	{
		CloseAddEmail();
		Toast(e.Message, e.Message, "info-circle", Ui.PurpleLight);
		try
		{
			LoginResult r = await AccountApi.RefreshAsync(_acct, s.RefreshToken);
			if (_session == s)
			{
				s.Apply(r);
				s.Save();
				((Control)this).Invalidate();
			}
		}
		catch (Exception ex)
		{
			Log.Write("ต\u0e48ออาย\u0e38หล\u0e31งเพ\u0e34\u0e48มอ\u0e35เมลไม\u0e48ได\u0e49: " + ex.Message);
		}
	}

	private int DrawAddEmailRow(Graphics g, int x, int y, int w)
	{
		Font f = F(13f);
		string s = L.T("เพ\u0e34\u0e48มอ\u0e35เมล (ก\u0e39\u0e49รห\u0e31สผ\u0e48านทางเมลได\u0e49)", "Add an email (password reset by email)");
		int num = Math.Min(Ui.Measure(s, f).Width + S(2f), w - S(19f) - S(20f));
		Rectangle r = new Rectangle(x, y, S(19f) + num + S(20f), S(20f));
		Color color = (AddHit(r, "add-email", OpenAddEmail, !_launching) ? Ui.Text : Ui.PurpleLight);
		Icons.Draw(g, "mail", new Point(r.X + S(7f), r.Y + S(10f)), S(14f), color);
		Ui.Draw(g, s, f, new Rectangle(r.X + S(19f), r.Y, num, r.Height), color, (TextFormatFlags)32772);
		Icons.Draw(g, "chevron-right", new Point(r.X + S(19f) + num + S(10f), r.Y + S(10f)), S(13f), color);
		return r.Bottom;
	}

	private void DrawAddEmailModal(Graphics g)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Expected O, but got Unknown
		Session session = _session;
		if (session == null)
		{
			_aeOpen = false;
			return;
		}
		Rectangle clientRectangle = ((Control)this).ClientRectangle;
		SolidBrush val = new SolidBrush(Color.FromArgb(184, 8, 7, 12));
		try
		{
			g.FillRectangle((Brush)(object)val, clientRectangle);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		AddHit(clientRectangle, "modal-bg", null, enabled: false);
		int num = S(400f);
		int w = num - S(48f);
		int count = _hits.Count;
		_measuring = true;
		int num2;
		try
		{
			Bitmap val2 = new Bitmap(1, 1);
			try
			{
				Graphics val3 = Graphics.FromImage((Image)(object)val2);
				try
				{
					num2 = AddEmailBody(val3, 0, S(20f), w, session);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			_measuring = false;
			_hits.RemoveRange(count, _hits.Count - count);
		}
		int num3 = num2 + S(22f);
		Rectangle r = new Rectangle(clientRectangle.X + (clientRectangle.Width - num) / 2, clientRectangle.Y + Math.Max(S(12f), (clientRectangle.Height - num3) / 2), num, num3);
		Ui.Card(g, r, S(14f), Ui.K1, Ui.K3);
		AddEmailBody(g, r.X + S(24f), r.Y + S(20f), w, session);
	}

	private int AddEmailBody(Graphics g, int x, int y, int w, Session s)
	{
		EmailSent aeSent = _aeSent;
		bool flag = _aeStep == 2 && aeSent != null;
		Ui.Draw(g, flag ? L.T("ใส\u0e48รห\u0e31สย\u0e37นย\u0e31น", "Enter the code") : L.T("เพ\u0e34\u0e48มอ\u0e35เมล", "Add an email"), F(22f, medium: true), new Rectangle(x, y, w - S(44f), S(30f)), Ui.Text, (TextFormatFlags)4);
		Rectangle r = new Rectangle(x + w - S(40f), y - S(2f), S(40f), S(34f));
		bool flag2 = AddHit(r, "ae-x", CloseAddEmail);
		if (flag2)
		{
			Ui.Fill(g, r, S(8f), Ui.K2);
		}
		Icons.Draw(g, "x", new Point(r.X + r.Width / 2, r.Y + r.Height / 2), S(17f), flag2 ? Ui.Text : Ui.TextMute);
		y += S(30f);
		if (!flag)
		{
			y = Sub(g, x, y, w, L.T("เพ\u0e34\u0e48มอ\u0e35เมลให\u0e49ไอด\u0e35 " + s.Username + " ไว\u0e49ต\u0e31\u0e49งรห\u0e31สผ\u0e48านใหม\u0e48ทางอ\u0e35เมล\nเพ\u0e34\u0e48มได\u0e49คร\u0e31\u0e49งเด\u0e35ยว — เปล\u0e35\u0e48ยนเองไม\u0e48ได\u0e49", "Add an email to ID " + s.Username + " to reset your password by email.\nIt can be added once and cannot be changed."));
			y = Banner(g, x, y, w);
			y = Field(g, x, y, w, "ae_m", L.T("อ\u0e35เมล", "Email"), "mail", "name@gmail.com", _authBusy) + S(12f);
			y = Field(g, x, y, w, "ae_p", L.T("รห\u0e31สผ\u0e48านป\u0e31จจ\u0e38บ\u0e31น", "Current password"), "lock", L.T("ย\u0e37นย\u0e31นว\u0e48าเป\u0e47นเจ\u0e49าของไอด\u0e35", "Confirms the ID is yours"), _authBusy);
			if (!_fieldErr.ContainsKey("ae_p"))
			{
				y = Hint(g, x, y + S(6f), w, "info-circle", L.T("เราจะส\u0e48งรห\u0e31ส 6 หล\u0e31กไปท\u0e35\u0e48อ\u0e35เมลน\u0e35\u0e49 · หมดอาย\u0e38ใน 10 นาท\u0e35", "We will send a 6-digit code to this email · it expires in 10 minutes"), Ui.TextMute);
			}
			bool ok = EmailInput.LooksOk(((Control)_boxes["ae_m"]).Text) && ((Control)_boxes["ae_p"]).Text.Length > 0;
			return ModalButtons(g, x, y + S(16f), w, "ae-send", "send", L.T("ส\u0e48งรห\u0e31สย\u0e37นย\u0e31น", "Send code"), L.T("กำล\u0e31งส\u0e48งรห\u0e31ส…", "Sending code…"), ok, delegate
			{
				SubmitAddEmailStartAsync();
			});
		}
		y = Sub(g, x, y, w, L.T("ส\u0e48งรห\u0e31ส 6 หล\u0e31กไปท\u0e35\u0e48 " + aeSent.Email + " แล\u0e49ว", "We sent a 6-digit code to " + aeSent.Email));
		y = Banner(g, x, y, w);
		y = Field(g, x, y, w, "ae_c", L.T("รห\u0e31สย\u0e37นย\u0e31น 6 หล\u0e31ก", "6-digit code"), "key", "123456", _authBusy);
		TimeSpan timeSpan = _aeSentAt.AddSeconds(aeSent.ExpiresIn) - DateTime.UtcNow;
		if (!_fieldErr.ContainsKey("ae_c"))
		{
			y = ((timeSpan > TimeSpan.Zero) ? Hint(g, x, y + S(6f), w, "clock-hour-4", L.T("รห\u0e31สหมดอาย\u0e38ใน ", "Code expires in ") + Mmss(timeSpan, minutesPad: false), Ui.TextMute) : Hint(g, x, y + S(6f), w, "alert-circle", L.T("รห\u0e31สหมดอาย\u0e38แล\u0e49ว — ส\u0e48งรห\u0e31สใหม\u0e48", "The code has expired — send a new one"), Ui.Bad));
		}
		bool ok2 = EmailInput.CleanCode(((Control)_boxes["ae_c"]).Text).Length == 6;
		y = ModalButtons(g, x, y + S(16f), w, "ae-verify", "circle-check", L.T("ย\u0e37นย\u0e31นรห\u0e31ส", "Verify code"), L.T("กำล\u0e31งตรวจรห\u0e31ส…", "Checking…"), ok2, delegate
		{
			SubmitAddEmailVerifyAsync();
		});
		y = ResendRow(g, x, y + S(16f), w, _aeResendAt, "ae-resend", delegate
		{
			SubmitAddEmailStartAsync(resend: true);
		});
		AltRow(g, x, y + S(12f), w, L.T("อ\u0e35เมลผ\u0e34ด?", "Wrong email?"), L.T("เปล\u0e35\u0e48ยนอ\u0e35เมล", "Change email"), "ae-change", delegate
		{
			AeStep(1);
		});
		return y + S(12f) + S(22f);
	}

	private int ModalButtons(Graphics g, int x, int y, int w, string id, string icon, string text, string busyText, bool ok, Action click)
	{
		Font f = F(15f, medium: true);
		string text2 = (_authBusy ? busyText : text);
		int num = S(18f) + S(18f) + S(8f) + Ui.Measure(text2, f).Width + S(18f);
		Rectangle r = new Rectangle(x + w - num, y, num, S(44f));
		Button(g, r, id, _authBusy ? "loader-2" : icon, text2, primary: true, ok && !_authBusy, click, 18f, 15f, _authBusy);
		string s = L.T("ยกเล\u0e34ก", "Cancel");
		int num2 = S(18f) + Ui.Measure(s, F(14.5f)).Width + S(18f);
		Rectangle r2 = new Rectangle(r.X - S(10f) - num2, y, num2, S(44f));
		bool flag = AddHit(r2, id + "-cancel", CloseAddEmail);
		if (flag)
		{
			Ui.Fill(g, r2, S(9f), Ui.K2);
		}
		Ui.Stroke(g, r2, S(9f), Ui.K3);
		Ui.Draw(g, s, F(14.5f), r2, flag ? Ui.Text : Ui.TextDim, (TextFormatFlags)5);
		return r.Bottom;
	}

	private void InitAuth()
	{
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Expected O, but got Unknown
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_030c: Expected O, but got Unknown
		(string, bool)[] array = new(string, bool)[15]
		{
			("lu", false),
			("lp", true),
			("su_u", false),
			("su_p", true),
			("su_c", true),
			("rs_p", true),
			("rs_c", true),
			("cp_o", true),
			("cp_n", true),
			("cp_c", true),
			("em", false),
			("ec", false),
			("ae_m", false),
			("ae_p", true),
			("ae_c", false)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, bool) tuple = array[i];
			string item = tuple.Item1;
			bool item2 = tuple.Item2;
			TextBox val = new TextBox();
			((TextBoxBase)val).BorderStyle = (BorderStyle)0;
			((Control)val).BackColor = Ui.K0;
			((Control)val).ForeColor = Ui.Text;
			((Control)val).Visible = false;
			((Control)val).Tag = item2;
			val.UseSystemPasswordChar = item2;
			TextBox val2 = val;
			int maxLength;
			if (item2)
			{
				maxLength = 128;
			}
			else
			{
				int num;
				if ((item == "em" || item == "ae_m") ? true : false)
				{
					num = 254;
				}
				else
				{
					bool flag = ((item == "ec" || item == "ae_c") ? true : false);
					num = (flag ? 12 : 32);
				}
				maxLength = num;
			}
			((TextBoxBase)val2).MaxLength = maxLength;
			((TextBoxBase)val).ShortcutsEnabled = true;
			TextBox val3 = val;
			if (item == "su_u")
			{
				val3.CharacterCasing = (CharacterCasing)2;
			}
			string n = item;
			((Control)val3).TextChanged += delegate
			{
				_fieldErr.Remove(n);
				_red.Remove(n);
				((Control)this).Invalidate();
			};
			((Control)val3).GotFocus += delegate
			{
				((Control)this).Invalidate();
			};
			((Control)val3).LostFocus += delegate
			{
				((Control)this).Invalidate();
			};
			((Control)this).Controls.Add((Control)(object)val3);
			_boxes[item] = val3;
		}
		((Control)_boxes["lu"]).Text = Session.LastUser();
		_session = Session.Load();
		_restoring = _session != null;
		if (_session == null)
		{
			_focusNext = ((((Control)_boxes["lu"]).Text.Length > 0) ? "lp" : "lu");
		}
		((Form)this).FormClosed += (FormClosedEventHandler)delegate
		{
			_waitCts?.Cancel();
			_linkCts?.Cancel();
			Session s = _session;
			if (s != null && !s.Remember)
			{
				string refreshToken = s.RefreshToken;
				if (refreshToken != null && refreshToken.Length > 0)
				{
					AccountConfig acct = _acct;
					try
					{
						Task.Run(() => AccountApi.LogoutAsync(acct, s.RefreshToken)).Wait(2000);
					}
					catch
					{
					}
				}
			}
		};
	}

	private void TakeAccount(AccountConfig cfg)
	{
		_acct = cfg;
		RefreshFeaturesAsync(force: true);
		if (!_restoreStarted)
		{
			_restoreStarted = true;
			if (_session != null)
			{
				RestoreAsync();
			}
		}
	}

	private async Task RestoreAsync()
	{
		Session s = _session;
		UpdateAnim();
		try
		{
			LoginResult r = await AccountApi.RefreshAsync(_acct, s.RefreshToken);
			if (_session == s)
			{
				s.Apply(r);
				s.Save();
				Log.Write("ต\u0e48ออาย\u0e38บ\u0e31ญช\u0e35 " + s.Username + " แล\u0e49ว" + (s.Supporter ? " (Supporter)" : ""));
			}
		}
		catch (ApiError apiError) when (apiError.IsAuthDead)
		{
			if (_session == s)
			{
				SignOutLocal(new BannerInfo("relogin"), s.Username);
			}
		}
		catch (ApiError apiError2)
		{
			Log.Write("ต\u0e48ออาย\u0e38บ\u0e31ญช\u0e35ไม\u0e48ได\u0e49 (ลองใหม\u0e48ตอนเข\u0e49าเกม): " + apiError2.Code);
		}
		finally
		{
			_restoring = false;
			UpdateAnim();
			((Control)this).Invalidate();
		}
	}

	private void UpdateAnim()
	{
		if (_checking || _busy != null || _launching || AuthAnimating || TradeAnimating)
		{
			_anim.Start();
		}
		else
		{
			_anim.Stop();
		}
	}

	private async Task<string> TicketAsync()
	{
		Session s = _session;
		if (s.Ticket != null && DateTime.UtcNow - s.TicketAtUtc < TimeSpan.FromSeconds(90L))
		{
			string? ticket = s.Ticket;
			s.Ticket = null;
			return ticket;
		}
		LoginResult loginResult = await AccountApi.RefreshAsync(_acct, s.RefreshToken);
		s.Apply(loginResult);
		s.Save();
		s.Ticket = null;
		return loginResult.Ticket;
	}

	private async Task RefreshFeaturesAsync(bool force = false)
	{
		if (force || !(DateTime.UtcNow - _featuresAt < TimeSpan.FromSeconds(30L)))
		{
			_featuresAt = DateTime.UtcNow;
			AccountFeatures accountFeatures = await AccountApi.FeaturesAsync(_acct);
			if (!(accountFeatures == _features))
			{
				_features = accountFeatures;
				Log.Write($"สม\u0e31ครด\u0e49วยอ\u0e35เมล: {(accountFeatures.EmailSignup ? "เป\u0e34ด" : "ป\u0e34ด")} · ล\u0e37มรห\u0e31สทางอ\u0e35เมล: {(accountFeatures.EmailReset ? "เป\u0e34ด" : "ป\u0e34ด")} · เพ\u0e34\u0e48มอ\u0e35เมล: {(accountFeatures.EmailAdd ? "เป\u0e34ด" : "ป\u0e34ด")} · เข\u0e49าส\u0e39\u0e48ระบบด\u0e49วย Discord: {(accountFeatures.DiscordLogin ? "เป\u0e34ด" : "ป\u0e34ด")}");
				((Control)this).Invalidate();
			}
		}
	}

	private void GoAuth(Auth scr, BannerInfo? banner = null)
	{
		if ((scr == Auth.Su1 || scr == Auth.Forgot) ? true : false)
		{
			RefreshFeaturesAsync();
		}
		if (_scr == Auth.Wait && scr != Auth.Wait && _waitCts != null)
		{
			_waitUserCancel = true;
			_waitCts.Cancel();
		}
		_scr = scr;
		_banner = banner;
		_fieldErr.Clear();
		_red.Clear();
		_page = Page.Home;
		_focusNext = scr switch
		{
			Auth.Login => (((Control)_boxes["lu"]).Text.Length > 0) ? "lp" : "lu", 
			Auth.Su2 => "su_u", 
			Auth.Reset => "rs_p", 
			Auth.EmAsk => "em", 
			Auth.EmCode => "ec", 
			_ => null, 
		};
		if (((ContainerControl)this).ActiveControl is TextBox)
		{
			((ContainerControl)this).ActiveControl = null;
		}
		UpdateAnim();
		((Control)this).Invalidate();
	}

	private void Toast(string th, string en, string icon = "circle-check", Color? color = null)
	{
		_toast = (L.T(th, en), icon, color ?? Ui.Good);
		_toastUntil = DateTime.UtcNow.AddMilliseconds(2600.0);
		((Control)this).Invalidate();
	}

	private void SignIn(Session s, string toastTh, string toastEn)
	{
		_session = s;
		s.Save();
		foreach (TextBox value in _boxes.Values)
		{
			if ((bool)((Control)value).Tag)
			{
				((Control)value).Text = "";
			}
		}
		((Control)_boxes["su_u"]).Text = "";
		((Control)_boxes["ec"]).Text = "";
		_signupToken = (_resetToken = (_loginToken = null));
		_emailSent = null;
		_pendingDiscord = null;
		_lockUntil = default(DateTime);
		_scr = Auth.Login;
		_banner = null;
		_fieldErr.Clear();
		_red.Clear();
		_page = Page.Home;
		if (((ContainerControl)this).ActiveControl is TextBox)
		{
			((ContainerControl)this).ActiveControl = null;
		}
		Log.Write("เข\u0e49าส\u0e39\u0e48ระบบ " + s.Username + (s.Supporter ? " (Supporter)" : "") + (s.Remember ? " · จำไว\u0e49" : ""));
		Toast(toastTh, toastEn);
		UpdateAnim();
	}

	private void SignOutLocal(BannerInfo banner, string? prefill)
	{
		Session session = _session;
		_session = null;
		_chpwOpen = (_aeOpen = false);
		Session.Delete();
		((Control)_boxes["lu"]).Text = prefill ?? "";
		((Control)_boxes["lp"]).Text = "";
		if (session != null)
		{
			Log.Write($"ออกจากระบบ {session.Username} ({banner.Kind})");
		}
		GoAuth(Auth.Login, banner);
	}

	private async void SignOut()
	{
		Session session = _session;
		if (session != null)
		{
			if (session.Remember)
			{
				Session.SaveLastUser(session.Username);
			}
			SignOutLocal(new BannerInfo("logout"), session.Remember ? session.Username : "");
			await AccountApi.LogoutAsync(_acct, session.RefreshToken);
		}
	}

	private void SetAuthBusy(bool busy)
	{
		_authBusy = busy;
		UpdateAnim();
		((Control)this).Invalidate();
	}

	private void Submit()
	{
		if (_chpwOpen)
		{
			SubmitChangePasswordAsync();
		}
		else if (_aeOpen)
		{
			if (_aeStep != 2 || !(_aeSent != null))
			{
				SubmitAddEmailStartAsync();
			}
			else
			{
				SubmitAddEmailVerifyAsync();
			}
		}
		else if (!LoggedIn && _page == Page.Home)
		{
			switch (_scr)
			{
			case Auth.Login:
				SubmitLoginAsync();
				break;
			case Auth.Su2:
				SubmitSignupAsync();
				break;
			case Auth.Reset:
				SubmitResetAsync();
				break;
			case Auth.EmAsk:
				SubmitEmailStartAsync();
				break;
			case Auth.EmCode:
				SubmitEmailVerifyAsync();
				break;
			case Auth.Su1:
			case Auth.Wait:
			case Auth.Forgot:
			case Auth.Linked:
				break;
			}
		}
	}

	private async Task SubmitLoginAsync()
	{
		if (_authBusy || Locked)
		{
			return;
		}
		string u = ((Control)_boxes["lu"]).Text.Trim();
		string text = ((Control)_boxes["lp"]).Text;
		_fieldErr.Clear();
		_red.Clear();
		if (u.Length == 0)
		{
			_fieldErr["lu"] = ("ใส\u0e48ช\u0e37\u0e48อผ\u0e39\u0e49ใช\u0e49", "Enter your username");
		}
		if (text.Length == 0)
		{
			_fieldErr["lp"] = ("ใส\u0e48รห\u0e31สผ\u0e48าน", "Enter your password");
		}
		if (u.Length == 0 || text.Length == 0)
		{
			_banner = null;
			_focusNext = ((u.Length == 0) ? "lu" : "lp");
			((Control)this).Invalidate();
			return;
		}
		SetAuthBusy(busy: true);
		try
		{
			LoginResult loginResult = await AccountApi.LoginAsync(_acct, u, text, remember: true);
			SignIn(Session.From(loginResult, _remember, u), "เข\u0e49าส\u0e39\u0e48ระบบแล\u0e49ว · " + loginResult.Username.OrIfEmpty(u), "Signed in · " + loginResult.Username.OrIfEmpty(u));
		}
		catch (ApiError apiError)
		{
			((Control)_boxes["lp"]).Text = "";
			if (apiError.IsWrongPassword)
			{
				_banner = new BannerInfo("wrong", null, apiError.AttemptsLeft ?? (-1));
				_red.Add("lu");
				_red.Add("lp");
				_focusNext = "lp";
			}
			else if (apiError.Code == "locked")
			{
				_lockUntil = DateTime.UtcNow.AddSeconds(Math.Clamp(apiError.RetryAfter ?? 600, 1, 3600));
				_banner = new BannerInfo("locked");
			}
			else
			{
				_banner = new BannerInfo("error", apiError.Message);
			}
		}
		catch (Exception ex)
		{
			Log.Write("account: " + ex);
			_banner = new BannerInfo("error");
		}
		finally
		{
			SetAuthBusy(busy: false);
		}
	}

	private async Task SubmitSignupAsync()
	{
		if (_authBusy || _signupToken == null)
		{
			return;
		}
		string u = ((Control)_boxes["su_u"]).Text.Trim();
		string text = ((Control)_boxes["su_p"]).Text;
		string text2 = ((Control)_boxes["su_c"]).Text;
		if (!SignupValid(u, text, text2))
		{
			return;
		}
		SetAuthBusy(busy: true);
		try
		{
			LoginResult loginResult = await AccountApi.SignupAsync(_acct, _signupToken, u, text, remember: true);
			_remember = true;
			SignIn(Session.From(loginResult, remember: true, u, "signup"), "สร\u0e49างไอด\u0e35 " + loginResult.Username.OrIfEmpty(u) + " แล\u0e49ว — ย\u0e34นด\u0e35ต\u0e49อนร\u0e31บ", "ID " + loginResult.Username.OrIfEmpty(u) + " created — welcome");
		}
		catch (ApiError apiError)
		{
			switch (apiError.Code)
			{
			case "username_taken":
				_fieldErr["su_u"] = ("ช\u0e37\u0e48อน\u0e35\u0e49ม\u0e35คนใช\u0e49แล\u0e49ว", "This name is already taken");
				_focusNext = "su_u";
				break;
			case "username_invalid":
				_fieldErr["su_u"] = (apiError.Message, apiError.Message);
				_focusNext = "su_u";
				break;
			case "password_invalid":
				_fieldErr["su_p"] = (apiError.Message, apiError.Message);
				_focusNext = "su_p";
				break;
			case "discord_already_registered":
				TakeLinked(apiError);
				GoAuth(Auth.Linked);
				break;
			case "invalid_token":
			case "token_expired":
			case "expired":
			case "bad_token":
				if (_suVia == "email")
				{
					_signupToken = null;
					_emailFor = "signup";
					GoAuth(Auth.EmAsk, new BannerInfo("em-timeout"));
				}
				else
				{
					_signupToken = null;
					GoAuth(Auth.Su1, new BannerInfo("timeout"));
				}
				break;
			case "email_taken":
				_signupToken = null;
				_emailFor = "signup";
				GoAuth(Auth.EmAsk, new BannerInfo("em-taken"));
				break;
			default:
				_banner = new BannerInfo("error", apiError.Message);
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Write("account: " + ex);
			_banner = new BannerInfo("error");
		}
		finally
		{
			SetAuthBusy(busy: false);
		}
	}

	private async Task SubmitResetAsync()
	{
		if (_authBusy || _resetToken == null)
		{
			return;
		}
		string user = _resetUser ?? "";
		string text = ((Control)_boxes["rs_p"]).Text;
		string text2 = ((Control)_boxes["rs_c"]).Text;
		if (PwLen(text) != true || PwNoUser(text, user) == false || PwMatch(text, text2) != true)
		{
			return;
		}
		SetAuthBusy(busy: true);
		try
		{
			LoginResult loginResult = await AccountApi.ResetAsync(_acct, _resetToken, text, remember: false);
			_resetToken = null;
			TextBox obj = _boxes["rs_p"];
			string text3 = (((Control)_boxes["rs_c"]).Text = "");
			((Control)obj).Text = text3;
			((Control)_boxes["lu"]).Text = loginResult.Username.OrIfEmpty(user);
			((Control)_boxes["lp"]).Text = "";
			_lockUntil = default(DateTime);
			Log.Write("ต\u0e31\u0e49งรห\u0e31สใหม\u0e48 " + ((Control)_boxes["lu"]).Text);
			GoAuth(Auth.Login, new BannerInfo("reset-ok"));
		}
		catch (ApiError apiError)
		{
			switch (apiError.Code)
			{
			case "password_invalid":
				_fieldErr["rs_p"] = (apiError.Message, apiError.Message);
				_focusNext = "rs_p";
				break;
			case "token_expired":
			case "expired":
			case "invalid_token":
			case "bad_token":
				if (_resetVia == "email")
				{
					_resetToken = null;
					_emailFor = "reset";
					GoAuth(Auth.EmAsk, new BannerInfo("em-timeout"));
				}
				else
				{
					_resetToken = null;
					GoAuth(Auth.Forgot, new BannerInfo("timeout"));
				}
				break;
			default:
				_banner = new BannerInfo("error", apiError.Message);
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Write("account: " + ex);
			_banner = new BannerInfo("error");
		}
		finally
		{
			SetAuthBusy(busy: false);
		}
	}

	private async Task SubmitChangePasswordAsync()
	{
		Session s = _session;
		if (_authBusy || s == null)
		{
			return;
		}
		string text = ((Control)_boxes["cp_o"]).Text;
		string text2 = ((Control)_boxes["cp_n"]).Text;
		string text3 = ((Control)_boxes["cp_c"]).Text;
		if (!ChpwValid(text, text2, text3, s.Username))
		{
			return;
		}
		SetAuthBusy(busy: true);
		try
		{
			(string, int) obj = await AccountApi.ChangePasswordAsync(_acct, s.RefreshToken, text, text2);
			string item = obj.Item1;
			int item2 = obj.Item2;
			s.RefreshToken = item;
			s.RefreshUntilUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, item2));
			s.Save();
			CloseChpw();
			Toast("เปล\u0e35\u0e48ยนรห\u0e31สผ\u0e48านแล\u0e49ว", "Password changed");
		}
		catch (ApiError apiError)
		{
			if (apiError.IsWrongPassword)
			{
				_fieldErr["cp_o"] = ("รห\u0e31สผ\u0e48านป\u0e31จจ\u0e38บ\u0e31นไม\u0e48ถ\u0e39กต\u0e49อง", "Current password is incorrect");
				((Control)_boxes["cp_o"]).Text = "";
				_fieldErr["cp_o"] = ("รห\u0e31สผ\u0e48านป\u0e31จจ\u0e38บ\u0e31นไม\u0e48ถ\u0e39กต\u0e49อง", "Current password is incorrect");
				_focusNext = "cp_o";
			}
			else if (apiError.Code == "password_invalid")
			{
				_fieldErr["cp_n"] = (apiError.Message, apiError.Message);
				_focusNext = "cp_n";
			}
			else if (apiError.IsAuthDead)
			{
				SignOutLocal(new BannerInfo("relogin"), s.Username);
			}
			else if (apiError.Code == "locked")
			{
				_fieldErr["cp_o"] = (apiError.Message, apiError.Message);
			}
			else
			{
				Toast(apiError.Message, apiError.Message, "alert-circle", Ui.Bad);
			}
		}
		catch (Exception ex)
		{
			Log.Write("account: " + ex);
			_banner = new BannerInfo("error");
		}
		finally
		{
			SetAuthBusy(busy: false);
		}
	}

	private void OpenChpw()
	{
		string[] array = new string[3] { "cp_o", "cp_n", "cp_c" };
		foreach (string key in array)
		{
			((Control)_boxes[key]).Text = "";
			_boxes[key].UseSystemPasswordChar = true;
		}
		_fieldErr.Clear();
		_chpwOpen = true;
		_focusNext = "cp_o";
		((Control)this).Invalidate();
	}

	private void CloseChpw()
	{
		string[] array = new string[3] { "cp_o", "cp_n", "cp_c" };
		foreach (string key in array)
		{
			((Control)_boxes[key]).Text = "";
		}
		_fieldErr.Clear();
		_chpwOpen = false;
		if (((ContainerControl)this).ActiveControl is TextBox)
		{
			((ContainerControl)this).ActiveControl = null;
		}
		((Control)this).Invalidate();
	}

	private static Auth DiscordBack(string purpose)
	{
		if (!(purpose == "reset"))
		{
			if (purpose == "login")
			{
				return Auth.Login;
			}
			return Auth.Su1;
		}
		return Auth.Forgot;
	}

	private async Task DiscordAsync(string purpose)
	{
		_waitCts?.Cancel();
		Auth back = DiscordBack(purpose);
		using DiscordLoopback lb = DiscordLoopback.TryOpen();
		if (lb == null)
		{
			GoAuth(back, new BannerInfo("error", L.T("เป\u0e34ดพอร\u0e4cต 38431–38433 ไม\u0e48ได\u0e49 — ป\u0e34ดร\u0e31นเช\u0e48อ/โปรแกรมอ\u0e37\u0e48นท\u0e35\u0e48ใช\u0e49อย\u0e39\u0e48แล\u0e49วลองใหม\u0e48", "Ports 38431–38433 are busy — close other launchers/apps using them and try again")));
			return;
		}
		using CancellationTokenSource cts = new CancellationTokenSource();
		_pendingDiscord = null;
		_linkedUser = null;
		_loginToken = null;
		_waitFor = purpose;
		_waitGot = false;
		_waitStart = DateTime.UtcNow;
		_waitSecs = 300;
		_waitUrl = null;
		GoAuth(Auth.Wait);
		_waitCts = cts;
		_waitUserCancel = false;
		cts.CancelAfter(TimeSpan.FromSeconds(_waitSecs));
		try
		{
			DiscordStart discordStart = await AccountApi.DiscordStartAsync(_acct, purpose, lb.RedirectUri, cts.Token);
			_waitSecs = Math.Clamp(discordStart.ExpiresIn, 30, 600);
			cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1.0, (_waitStart.AddSeconds(_waitSecs) - DateTime.UtcNow).TotalSeconds)));
			_waitUrl = discordStart.Url;
			OpenUrl(discordStart.Url);
			((Control)this).Invalidate();
			DiscordDone done = await AwaitDiscordAsync(discordStart, lb, cts.Token);
			if (_waitCts != cts)
			{
				return;
			}
			BringToFront();
			_pendingDiscord = done.Discord;
			_waitGot = true;
			switch (DiscordFlow.OnDone(purpose, done))
			{
			case DiscordFlow.Next.Signup2:
			{
				_suVia = "discord";
				_signupToken = done.SignupToken;
				TextBox obj = _boxes["su_u"];
				TextBox obj2 = _boxes["su_p"];
				string text = (((Control)_boxes["su_c"]).Text = "");
				string text3 = (((Control)obj2).Text = text);
				((Control)obj).Text = text3;
				_scr = Auth.Login;
				GoAuth(Auth.Su2);
				break;
			}
			case DiscordFlow.Next.Reset:
			{
				_resetVia = "discord";
				_resetToken = done.ResetToken;
				_resetUser = done.Username;
				TextBox obj3 = _boxes["rs_p"];
				string text3 = (((Control)_boxes["rs_c"]).Text = "");
				((Control)obj3).Text = text3;
				_scr = Auth.Login;
				GoAuth(Auth.Reset);
				break;
			}
			case DiscordFlow.Next.Login:
			{
				((Control)this).Invalidate();
				LoginResult r = await AccountApi.DiscordLoginAsync(_acct, done.LoginToken, _remember, cts.Token);
				if (_waitCts == cts)
				{
					SignInDiscord(r, done.Username);
				}
				break;
			}
			default:
				_scr = Auth.Login;
				GoAuth(back, new BannerInfo("error", L.T("ย\u0e37นย\u0e31น Discord ไม\u0e48สำเร\u0e47จ — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Discord verification failed — please try again")));
				break;
			}
		}
		catch (OperationCanceledException)
		{
			if (_waitCts == cts && !_waitUserCancel)
			{
				_scr = Auth.Login;
				GoAuth(back, new BannerInfo("timeout"));
			}
		}
		catch (ApiError apiError)
		{
			if (_waitCts == cts)
			{
				BringToFront();
				_scr = Auth.Login;
				switch (DiscordFlow.OnError(purpose, apiError))
				{
				case DiscordFlow.Next.Deny:
					GoAuth(back, new BannerInfo("deny"));
					break;
				case DiscordFlow.Next.Timeout:
					GoAuth(back, new BannerInfo("timeout"));
					break;
				case DiscordFlow.Next.Linked:
					TakeLinked(apiError);
					GoAuth(Auth.Linked);
					break;
				case DiscordFlow.Next.NoId:
				{
					_suVia = "discord";
					_signupToken = apiError.SignupToken;
					_pendingDiscord = DiscordOf(apiError);
					TextBox obj4 = _boxes["su_u"];
					TextBox obj5 = _boxes["su_p"];
					string text = (((Control)_boxes["su_c"]).Text = "");
					string text3 = (((Control)obj5).Text = text);
					((Control)obj4).Text = text3;
					GoAuth(Auth.NoId);
					break;
				}
				case DiscordFlow.Next.NoIdBanner:
					GoAuth((!(purpose == "login")) ? Auth.Forgot : Auth.Login, new BannerInfo("noid"));
					break;
				default:
					GoAuth(back, new BannerInfo("error", apiError.Message));
					break;
				}
			}
		}
		catch (Exception ex2)
		{
			Log.Write("discord: " + ex2);
			if (_waitCts == cts)
			{
				_scr = Auth.Login;
				GoAuth(back, new BannerInfo("error"));
			}
		}
		finally
		{
			if (_waitCts == cts)
			{
				_waitCts = null;
			}
			UpdateAnim();
		}
	}

	private async Task<DiscordDone> AwaitDiscordAsync(DiscordStart st, DiscordLoopback lb, CancellationToken ct)
	{
		if (st.PollId != null)
		{
			lb.Dispose();
			DiscordDone discordDone = null;
			while (discordDone == null)
			{
				await Task.Delay(TimeSpan.FromSeconds(st.Interval), ct);
				discordDone = await AccountApi.DiscordPollAsync(_acct, st.PollId, ct);
			}
			return discordDone;
		}
		DiscordLoopback.Callback callback = await lb.WaitAsync(ct);
		BringToFront();
		if (callback.Error != null)
		{
			throw new ApiError((callback.Error == "access_denied") ? "denied" : callback.Error, callback.Error);
		}
		if (callback.Code == null || callback.State != st.State)
		{
			throw new ApiError("bad_state", L.T("ล\u0e34งก\u0e4cย\u0e37นย\u0e31น Discord ไม\u0e48ตรงก\u0e31บท\u0e35\u0e48ร\u0e31นเช\u0e48อเป\u0e34ด — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "The Discord reply does not match this launcher — please try again"));
		}
		return await AccountApi.DiscordFinishAsync(_acct, st.State, callback.Code, lb.RedirectUri, ct);
	}

	private static DiscordUser? DiscordOf(ApiError e)
	{
		if (e.DiscordId.Length <= 0 && e.DiscordName.Length <= 0)
		{
			return null;
		}
		return new DiscordUser(e.DiscordId, e.DiscordName, e.DiscordAvatar);
	}

	private void TakeLinked(ApiError e)
	{
		_linkedUser = e.Username;
		_loginToken = e.LoginToken;
		DiscordUser discordUser = DiscordOf(e);
		if ((object)discordUser != null)
		{
			_pendingDiscord = discordUser;
		}
	}

	private void SignInDiscord(LoginResult r, string? userHint)
	{
		Session session = Session.From(r, _remember, userHint, "discord");
		Log.Write("เข\u0e49าส\u0e39\u0e48ระบบด\u0e49วย Discord (" + session.DiscordName.OrIfEmpty("?") + ")");
		SignIn(session, "เข\u0e49าส\u0e39\u0e48ระบบด\u0e49วย Discord แล\u0e49ว — ย\u0e34นด\u0e35ต\u0e49อนร\u0e31บ " + session.Username, "Signed in with Discord — welcome " + session.Username);
	}

	private async Task LinkedLoginAsync()
	{
		if (_authBusy)
		{
			return;
		}
		string loginToken = _loginToken;
		if (loginToken == null)
		{
			return;
		}
		SetAuthBusy(busy: true);
		try
		{
			LoginResult r = await AccountApi.DiscordLoginAsync(_acct, loginToken, _remember);
			_loginToken = null;
			SignInDiscord(r, _linkedUser);
		}
		catch (ApiError apiError)
		{
			_loginToken = null;
			string code = apiError.Code;
			bool flag = ((code == "token_expired" || code == "expired") ? true : false);
			_banner = (flag ? new BannerInfo("error", L.T("หมดเวลาเข\u0e49าส\u0e39\u0e48ระบบด\u0e49วย Discord — เข\u0e49าด\u0e49วยรห\u0e31สผ\u0e48าน หร\u0e37อกด «ใช\u0e49บ\u0e31ญช\u0e35 Discord อ\u0e37\u0e48น» เพ\u0e37\u0e48อย\u0e37นย\u0e31นใหม\u0e48", "The Discord sign-in expired — sign in with your password, or press “Use another Discord account” to verify again")) : new BannerInfo("error", apiError.Message));
		}
		catch (Exception ex)
		{
			Log.Write("discord login: " + ex);
			_banner = new BannerInfo("error");
		}
		finally
		{
			SetAuthBusy(busy: false);
		}
	}

	private void StartEmail(string purpose)
	{
		_emailFor = purpose;
		_emailSent = null;
		((Control)_boxes["ec"]).Text = "";
		GoAuth(Auth.EmAsk);
	}

	private async Task SubmitEmailStartAsync(bool resend = false)
	{
		if (_authBusy)
		{
			return;
		}
		string em = EmailInput.Clean((!resend) ? ((Control)_boxes["em"]).Text : _emailSent?.Email);
		if (!EmailInput.LooksOk(em))
		{
			_fieldErr["em"] = ("อ\u0e35เมลไม\u0e48ถ\u0e39กต\u0e49อง — เช\u0e48น name@gmail.com", "Not a valid email — e.g. name@gmail.com");
			_focusNext = "em";
			((Control)this).Invalidate();
			return;
		}
		_fieldErr.Remove("em");
		SetAuthBusy(busy: true);
		try
		{
			EmailSent emailSent = (_emailSent = await AccountApi.EmailStartAsync(_acct, em, _emailFor));
			_emailSentAt = DateTime.UtcNow;
			_resendAt = _emailSentAt.AddSeconds(Math.Clamp(emailSent.ResendIn, 1, 600));
			((Control)_boxes["ec"]).Text = "";
			Log.Write("ขอรห\u0e31สทางอ\u0e35เมล (" + _emailFor + ")");
			if (resend)
			{
				_banner = new BannerInfo("em-resent");
				_fieldErr.Remove("ec");
				_focusNext = "ec";
			}
			else
			{
				GoAuth(Auth.EmCode);
			}
		}
		catch (ApiError apiError)
		{
			switch (apiError.Code)
			{
			case "email_invalid":
				if (!resend)
				{
					_fieldErr["em"] = (apiError.Message, apiError.Message);
					_focusNext = "em";
					return;
				}
				break;
			case "email_taken":
				GoAuth(Auth.EmAsk, new BannerInfo("em-taken"));
				return;
			case "resend_wait":
				_resendAt = DateTime.UtcNow.AddSeconds(Math.Clamp(apiError.RetryAfter ?? 60, 1, 600));
				if (!resend && _emailSent != null && _emailSent.Email == em.ToLowerInvariant())
				{
					GoAuth(Auth.EmCode);
				}
				else
				{
					_banner = new BannerInfo("error", apiError.Message);
				}
				return;
			}
			_banner = new BannerInfo("error", apiError.Message);
		}
		catch (Exception ex)
		{
			Log.Write("account: " + ex);
			_banner = new BannerInfo("error");
		}
		finally
		{
			SetAuthBusy(busy: false);
		}
	}

	private async Task SubmitEmailVerifyAsync()
	{
		EmailSent sent = _emailSent;
		if (_authBusy || sent == null)
		{
			return;
		}
		string text = EmailInput.CleanCode(((Control)_boxes["ec"]).Text);
		if (text.Length != 6)
		{
			_fieldErr["ec"] = ("ใส\u0e48รห\u0e31ส 6 หล\u0e31กจากอ\u0e35เมล", "Enter the 6-digit code from the email");
			_focusNext = "ec";
			((Control)this).Invalidate();
			return;
		}
		SetAuthBusy(busy: true);
		try
		{
			EmailVerified emailVerified = await AccountApi.EmailVerifyAsync(_acct, sent.EmailId, text);
			_verifiedEmail = emailVerified.Email.OrIfEmpty(sent.Email);
			_pendingDiscord = null;
			if (emailVerified.SignupToken != null)
			{
				_suVia = "email";
				_signupToken = emailVerified.SignupToken;
				TextBox obj = _boxes["su_u"];
				TextBox obj2 = _boxes["su_p"];
				string text2 = (((Control)_boxes["su_c"]).Text = "");
				string text4 = (((Control)obj2).Text = text2);
				((Control)obj).Text = text4;
				GoAuth(Auth.Su2);
			}
			else
			{
				_resetVia = "email";
				_resetToken = emailVerified.ResetToken;
				_resetUser = emailVerified.Username;
				TextBox obj3 = _boxes["rs_p"];
				string text4 = (((Control)_boxes["rs_c"]).Text = "");
				((Control)obj3).Text = text4;
				GoAuth(Auth.Reset);
			}
		}
		catch (ApiError apiError)
		{
			((Control)_boxes["ec"]).Text = "";
			_focusNext = "ec";
			switch (apiError.Code)
			{
			case "code_invalid":
				if ((apiError.AttemptsLeft ?? 1) > 0)
				{
					_fieldErr["ec"] = ($"รห\u0e31สไม\u0e48ถ\u0e39กต\u0e49อง — ลองได\u0e49อ\u0e35ก {apiError.AttemptsLeft} คร\u0e31\u0e49ง", $"Wrong code — {apiError.AttemptsLeft} {((apiError.AttemptsLeft == 1) ? "try" : "tries")} left");
					break;
				}
				goto case "code_expired";
			case "code_expired":
				_banner = new BannerInfo("code-expired");
				_resendAt = ((DateTime.UtcNow < _resendAt) ? _resendAt : DateTime.UtcNow);
				break;
			case "email_taken":
				GoAuth(Auth.EmAsk, new BannerInfo("em-taken"));
				break;
			default:
				_banner = new BannerInfo("error", apiError.Message);
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Write("account: " + ex);
			_banner = new BannerInfo("error");
		}
		finally
		{
			SetAuthBusy(busy: false);
		}
	}

	private async Task LinkDiscordAsync()
	{
		Session s = _session;
		if (s == null || _linkCts != null)
		{
			return;
		}
		using DiscordLoopback lb = DiscordLoopback.TryOpen();
		if (lb == null)
		{
			Toast("เป\u0e34ดพอร\u0e4cต 38431–38433 ไม\u0e48ได\u0e49 — ป\u0e34ดโปรแกรมท\u0e35\u0e48ใช\u0e49อย\u0e39\u0e48แล\u0e49วลองใหม\u0e48", "Ports 38431–38433 are busy — close other apps using them and try again", "alert-circle", Ui.Bad);
			return;
		}
		using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(300L));
		_linkCts = cts;
		_linkStart = DateTime.UtcNow;
		_linkUrl = null;
		_linkUserCancel = false;
		UpdateAnim();
		((Control)this).Invalidate();
		try
		{
			DiscordStart discordStart = await AccountApi.DiscordStartAsync(_acct, "link", lb.RedirectUri, cts.Token, s.RefreshToken);
			_linkUrl = discordStart.Url;
			OpenUrl(discordStart.Url);
			((Control)this).Invalidate();
			DiscordDone done = await AwaitDiscordAsync(discordStart, lb, cts.Token);
			BringToFront();
			if (_session == s)
			{
				s.Apply(await AccountApi.RefreshAsync(_acct, s.RefreshToken));
				s.Save();
				Log.Write("ผ\u0e39ก Discord ก\u0e31บไอด\u0e35 " + s.Username + " แล\u0e49ว" + (s.Supporter ? " (Supporter)" : ""));
				Toast("ผ\u0e39ก Discord " + done.Discord.Name.OrIfEmpty(s.DiscordName) + " แล\u0e49ว", "Linked Discord " + done.Discord.Name.OrIfEmpty(s.DiscordName), "brand-discord", Ui.PurpleLight);
			}
		}
		catch (OperationCanceledException)
		{
			if (!_linkUserCancel)
			{
				Toast("หมดเวลารอ Discord — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Timed out waiting for Discord — please try again", "clock-hour-4", Ui.Bad);
			}
		}
		catch (ApiError apiError)
		{
			BringToFront();
			if (apiError.Code == "invalid_refresh" && _session == s)
			{
				SignOutLocal(new BannerInfo("relogin"), s.Username);
			}
			else if (apiError.Code == "denied")
			{
				Toast("ยกเล\u0e34กการเช\u0e37\u0e48อม Discord แล\u0e49ว", "Discord sign-in was cancelled", "info-circle", Ui.PurpleLight);
			}
			else
			{
				Toast(apiError.Message, apiError.Message, "alert-circle", Ui.Bad);
			}
		}
		catch (Exception ex2)
		{
			Log.Write("discord link: " + ex2);
			Toast("ผ\u0e39ก Discord ไม\u0e48สำเร\u0e47จ — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Could not link Discord — please try again", "alert-circle", Ui.Bad);
		}
		finally
		{
			if (_linkCts == cts)
			{
				_linkCts = null;
			}
			UpdateAnim();
			((Control)this).Invalidate();
		}
	}

	private void CancelLink()
	{
		_linkUserCancel = true;
		_linkCts?.Cancel();
	}

	private void BringToFront()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		try
		{
			if ((int)((Form)this).WindowState == 1)
			{
				((Form)this).WindowState = (FormWindowState)0;
			}
			((Form)this).Activate();
		}
		catch
		{
		}
	}

	private static string UserCheck(string u)
	{
		if (u.Length == 0)
		{
			return "n";
		}
		string result;
		if (!u.All(delegate(char c)
		{
			switch (c)
			{
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			case '_':
			case 'a':
			case 'b':
			case 'c':
			case 'd':
			case 'e':
			case 'f':
			case 'g':
			case 'h':
			case 'i':
			case 'j':
			case 'k':
			case 'l':
			case 'm':
			case 'n':
			case 'o':
			case 'p':
			case 'q':
			case 'r':
			case 's':
			case 't':
			case 'u':
			case 'v':
			case 'w':
			case 'x':
			case 'y':
			case 'z':
				return true;
			default:
				return false;
			}
		}))
		{
			result = "chars";
		}
		else
		{
			int length = u.Length;
			bool flag = ((length < 4 || length > 16) ? true : false);
			result = (flag ? "len" : "ok");
		}
		return result;
	}

	private static bool? PwLen(string p)
	{
		if (p.Length != 0)
		{
			return p.Length >= 8;
		}
		return null;
	}

	private static bool? PwNoUser(string p, string u)
	{
		if (p.Length != 0 && u.Length != 0)
		{
			return !p.Contains(u, StringComparison.OrdinalIgnoreCase);
		}
		return null;
	}

	private static bool? PwMatch(string p, string c)
	{
		if (c.Length != 0)
		{
			return c == p;
		}
		return null;
	}

	private static bool SignupValid(string u, string p, string c)
	{
		if (UserCheck(u) == "ok" && PwLen(p) == true && PwNoUser(p, u) == true)
		{
			return PwMatch(p, c) == true;
		}
		return false;
	}

	private static bool ChpwValid(string o, string n, string c, string u)
	{
		if (o.Length > 0 && PwLen(n) == true && PwNoUser(n, u) != false && PwMatch(n, c) == true)
		{
			return n != o;
		}
		return false;
	}

	private void DrawAuthHome(Graphics g, Rectangle pane)
	{
		int top = pane.Y + S(2f);
		Rectangle rectangle = Rectangle.FromLTRB(pane.Right - S(372f), top, pane.Right, pane.Bottom);
		Rectangle hero = Rectangle.FromLTRB(pane.X, top, rectangle.X - S(16f), pane.Bottom);
		DrawHero(g, hero, auth: true);
		_cardRect = rectangle;
		Ui.Card(g, rectangle, S(14f), Ui.K1, Ui.K3);
		Rectangle r = Rectangle.FromLTRB(rectangle.X + S(26f), rectangle.Y + S(22f), rectangle.Right - S(26f), rectangle.Bottom - S(22f));
		GraphicsState val = g.Save();
		g.SetClip(rectangle);
		switch (_scr)
		{
		case Auth.Su1:
			DrawSu1(g, r);
			break;
		case Auth.Su2:
			DrawSu2(g, r);
			break;
		case Auth.Wait:
			DrawWait(g, r);
			break;
		case Auth.Forgot:
			DrawForgot(g, r);
			break;
		case Auth.Reset:
			DrawReset(g, r);
			break;
		case Auth.Linked:
			DrawLinked(g, r);
			break;
		case Auth.EmAsk:
			DrawEmAsk(g, r);
			break;
		case Auth.EmCode:
			DrawEmCode(g, r);
			break;
		case Auth.NoId:
			DrawNoId(g, r);
			break;
		default:
			DrawLoginFit(g, r);
			break;
		}
		g.Restore(val);
	}

	private void DrawLoginFit(Graphics g, Rectangle r)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		LoginSpacing sp = LoginLayout.Levels[0];
		if (_features.DiscordLogin)
		{
			int count = _hits.Count;
			_measuring = true;
			try
			{
				Bitmap val = new Bitmap(1, 1);
				try
				{
					Graphics val2 = Graphics.FromImage((Image)(object)val);
					try
					{
						LoginSpacing[] levels = LoginLayout.Levels;
						foreach (LoginSpacing loginSpacing in levels)
						{
							sp = loginSpacing;
							if (DrawLogin(val2, r, loginSpacing) <= r.Bottom)
							{
								break;
							}
						}
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}
			finally
			{
				_measuring = false;
				_hits.RemoveRange(count, _hits.Count - count);
			}
		}
		DrawLogin(g, r, sp);
	}

	private int DrawLogin(Graphics g, Rectangle r, LoginSpacing sp)
	{
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		bool locked = Locked;
		bool discordLogin = _features.DiscordLogin;
		y = Title(g, x, y, width, L.T("เข\u0e49าส\u0e39\u0e48ระบบ", "Sign in"));
		if (sp.Sub)
		{
			y = WrapText(g, L.T("ใช\u0e49 Durango ID ของค\u0e38ณเพ\u0e37\u0e48อเข\u0e49าเกม", "Use your Durango ID to play"), F(13.5f), x, y + S(4f), width, Ui.TextDim) + S(sp.SubGap);
		}
		y = Banner(g, x, y, width);
		y = Field(g, x, y, width, "lu", L.T("ช\u0e37\u0e48อผ\u0e39\u0e49ใช\u0e49", "Username"), "user", L.T("ช\u0e37\u0e48อผ\u0e39\u0e49ใช\u0e49 Durango ID", "Your Durango ID"), locked || _authBusy, sp.BoxH) + S(sp.FieldGap);
		y = Field(g, x, y, width, "lp", L.T("รห\u0e31สผ\u0e48าน", "Password"), "lock", L.T("รห\u0e31สผ\u0e48าน", "Password"), locked || _authBusy, sp.BoxH) + S(sp.FieldGap);
		y += S(2f);
		Font f = F(13.5f);
		string s = L.T("จำฉ\u0e31นไว\u0e49 30 ว\u0e31น", "Remember me for 30 days");
		Rectangle r2 = new Rectangle(x, y, S(18f) + S(8f) + Ui.Measure(s, f).Width, S(22f));
		bool flag = locked && !discordLogin;
		bool flag2 = AddHit(r2, "remember", delegate
		{
			_remember = !_remember;
			((Control)this).Invalidate();
		}, !flag && !_authBusy);
		Rectangle r3 = new Rectangle(x, y + S(2f), S(18f), S(18f));
		if (_remember)
		{
			Ui.Fill(g, r3, S(5f), flag ? Ui.A(Ui.Yellow, 128) : Ui.Yellow);
			Icons.Draw(g, "check", new Point(r3.X + r3.Width / 2, r3.Y + r3.Height / 2), S(13f), Ui.OnYellow);
		}
		else
		{
			Ui.Stroke(g, r3, S(5f), flag2 ? Ui.PurpleLight : K4);
		}
		Ui.Draw(g, s, f, new Rectangle(r3.Right + S(8f), y, r2.Width, S(22f)), flag ? Ui.A(Ui.TextDim, 128) : Ui.TextDim, (TextFormatFlags)4);
		string text = L.T("ล\u0e37มรห\u0e31สผ\u0e48าน?", "Forgot password?");
		int num = Ui.Measure(text, f).Width + S(2f);
		LinkAt(g, new Rectangle(x + width - num, y, num, S(22f)), "forgot", text, f, delegate
		{
			_emailFor = "reset";
			GoAuth(Auth.Forgot);
		}, Ui.PurpleLight);
		y += S(22f) + S(sp.RowGap);
		Rectangle r4 = new Rectangle(x, y, width, S(46f));
		if (locked)
		{
			TimeSpan t = _lockUntil - DateTime.UtcNow;
			Button(g, r4, "login-submit", "clock-hour-4", L.T("รอ ", "Wait ") + Mmss(t), primary: true, enabled: false, null);
		}
		else if (_authBusy)
		{
			Button(g, r4, "login-submit", "loader-2", L.T("กำล\u0e31งเข\u0e49าส\u0e39\u0e48ระบบ…", "Signing in…"), primary: true, enabled: false, null, 19f, 16f, spin: true);
		}
		else
		{
			Button(g, r4, "login-submit", "login", L.T("เข\u0e49าส\u0e39\u0e48ระบบ", "Sign in"), primary: true, enabled: true, delegate
			{
				SubmitLoginAsync();
			});
		}
		y = r4.Bottom;
		if (discordLogin)
		{
			y = Or(g, x, y + S(sp.OrTop), width, S(sp.OrGap));
			Rectangle r5 = new Rectangle(x, y, width, S(46f));
			Button(g, r5, "discord-login", "brand-discord", L.T("เข\u0e49าส\u0e39\u0e48ระบบด\u0e49วย Discord", "Sign in with Discord"), primary: false, !_authBusy, delegate
			{
				DiscordAsync("login");
			}, 19f, 15f, spin: false, DiscordLight);
			y = HintCenter(g, x, r5.Bottom + S(sp.HintGap), width, "info-circle", L.T("สำหร\u0e31บไอด\u0e35ท\u0e35\u0e48ผ\u0e39ก Discord ไว\u0e49แล\u0e49ว · ไม\u0e48ต\u0e49องใส\u0e48รห\u0e31สผ\u0e48าน", "For IDs already linked to Discord · no password needed"), Ui.TextMute);
		}
		y += S(discordLogin ? sp.AltGap : 16);
		AltRow(g, x, y, width, L.T("ย\u0e31งไม\u0e48ม\u0e35ไอด\u0e35?", "No ID yet?"), L.T("สม\u0e31ครสมาช\u0e34ก", "Sign up"), "to-su1", delegate
		{
			_emailFor = "signup";
			GoAuth(Auth.Su1);
		});
		if (discordLogin)
		{
			return y + S(22f);
		}
		string s2 = (_features.EmailSignup ? L.T("สม\u0e31ครไอด\u0e35ด\u0e49วย Discord หร\u0e37ออ\u0e35เมลก\u0e47ได\u0e49 — ไอด\u0e35เด\u0e35ยวใช\u0e49ได\u0e49ท\u0e38กเซ\u0e34ร\u0e4cฟ", "Sign up with Discord or email — one ID for every server") : L.T("เป\u0e34ด OBT เร\u0e34\u0e48มโลกใหม\u0e48 — ท\u0e38กคนสม\u0e31ครไอด\u0e35ใหม\u0e48ด\u0e49วย Discord", "OBT starts a fresh world — everyone signs up with Discord"));
		Font f2 = F(12.5f);
		int num2 = Ui.MeasureWrapped(g, s2, f2, width - S(23f));
		int num3 = Math.Max(y + S(36f), r.Bottom - num2);
		Icons.Draw(g, "info-circle", new Point(x + S(7f), num3 + S(9f)), S(15f), Ui.PurpleLight);
		Ui.DrawWrapped(g, s2, f2, new Rectangle(x + S(23f), num3, width - S(23f), num2 + S(2f)), Ui.TextMute);
		return num3 + num2;
	}

	private void DrawSu1(Graphics g, Rectangle r)
	{
		_emailFor = "signup";
		if (_features.EmailSignup)
		{
			DrawSu1Both(g, r);
			return;
		}
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		y = Back(g, x, y);
		y = Steps(g, x, y, width, 1);
		y = Title(g, x, y, width, L.T("สม\u0e31ครสมาช\u0e34ก", "Sign up"));
		y = Sub(g, x, y, width, L.T("ข\u0e31\u0e49นแรก ย\u0e37นย\u0e31นต\u0e31วตนด\u0e49วย Discord — 1 บ\u0e31ญช\u0e35 Discord สม\u0e31ครได\u0e49 1 ไอด\u0e35", "First, verify with Discord — one Discord account gets one ID"));
		y = Banner(g, x, y, width);
		string[] obj = new string[3]
		{
			L.T("ใช\u0e49ย\u0e37นย\u0e31นต\u0e31วตนตอนล\u0e37มรห\u0e31สผ\u0e48าน", "Used to verify you if you forget your password"),
			L.T("ยศ Supporter ใน Discord ใช\u0e49ในเกมได\u0e49ท\u0e31นท\u0e35", "Your Discord Supporter role works in game right away"),
			L.T("ขอแค\u0e48ช\u0e37\u0e48อ ร\u0e39ปโปรไฟล\u0e4c และยศในเซ\u0e34ร\u0e4cฟ Discord", "Only reads your name, avatar and server roles")
		};
		Font f = F(13.5f);
		string[] array = obj;
		foreach (string s in array)
		{
			Icons.Draw(g, "circle-check", new Point(x + S(8f), y + S(10f)), S(16f), Ui.Good);
			int num = Ui.MeasureWrapped(g, s, f, width - S(26f));
			Ui.DrawWrapped(g, s, f, new Rectangle(x + S(26f), y, width - S(26f), num + S(2f)), Ui.TextDim);
			y += num + S(9f);
		}
		y += S(9f);
		Button(g, new Rectangle(x, y, width, S(46f)), "discord-signup", "brand-discord", L.T("สม\u0e31ครด\u0e49วย Discord", "Sign up with Discord"), primary: true, enabled: true, delegate
		{
			DiscordAsync("signup");
		}, 20f);
		y += S(46f) + S(10f);
		y = HintCenter(g, x, y, width, "browser", L.T("จะเป\u0e34ดหน\u0e49า Discord ในเบราว\u0e4cเซอร\u0e4c", "Opens Discord in your browser"), Ui.TextMute);
		AltRow(g, x, y + S(16f), width, L.T("ม\u0e35ไอด\u0e35แล\u0e49ว?", "Already have an ID?"), L.T("เข\u0e49าส\u0e39\u0e48ระบบ", "Sign in"), "to-login", delegate
		{
			GoAuth(Auth.Login);
		});
	}

	private void DrawSu1Both(Graphics g, Rectangle r)
	{
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		y = Back(g, x, y);
		y = Steps(g, x, y, width, 1);
		y = Title(g, x, y, width, L.T("สม\u0e31ครสมาช\u0e34ก", "Sign up"));
		y = Sub(g, x, y, width, L.T("เล\u0e37อกว\u0e34ธ\u0e35ย\u0e37นย\u0e31นต\u0e31วตน — 1 Discord หร\u0e37อ 1 อ\u0e35เมล สม\u0e31ครได\u0e49 1 ไอด\u0e35", "Choose how to verify — one Discord or one email gets one ID"));
		y = Banner(g, x, y, width);
		Button(g, new Rectangle(x, y, width, S(46f)), "discord-signup", "brand-discord", L.T("สม\u0e31ครด\u0e49วย Discord", "Sign up with Discord"), primary: true, enabled: true, delegate
		{
			DiscordAsync("signup");
		}, 20f);
		y += S(46f) + S(8f);
		y = Hint(g, x, y, width, "circle-check", L.T("ยศ Supporter ใน Discord ใช\u0e49ในเกมได\u0e49ท\u0e31นท\u0e35", "Your Discord Supporter role works in game right away"), Ui.Good);
		y = Or(g, x, y + S(12f), width);
		Button(g, new Rectangle(x, y, width, S(46f)), "email-signup", "mail", L.T("สม\u0e31ครด\u0e49วยอ\u0e35เมล", "Sign up with email"), primary: false, enabled: true, delegate
		{
			StartEmail("signup");
		}, 19f, 15f);
		y += S(46f) + S(8f);
		y = Hint(g, x, y, width, "link", L.T("ไม\u0e48ต\u0e49องม\u0e35 Discord · ผ\u0e39ก Discord ท\u0e35หล\u0e31งได\u0e49ในหน\u0e49าบ\u0e31ญช\u0e35", "No Discord needed · link Discord later from your account"), Ui.TextMute);
		AltRow(g, x, y + S(16f), width, L.T("ม\u0e35ไอด\u0e35แล\u0e49ว?", "Already have an ID?"), L.T("เข\u0e49าส\u0e39\u0e48ระบบ", "Sign in"), "to-login", delegate
		{
			GoAuth(Auth.Login);
		});
	}

	private int Or(Graphics g, int x, int y, int w, int? after = null)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		Font f = F(12.5f);
		string s = L.T("หร\u0e37อ", "or");
		int width = Ui.Measure(s, f).Width;
		int num = x + (w - width) / 2;
		Pen val = new Pen(Ui.K3);
		try
		{
			g.DrawLine(val, x, y + S(10f), num - S(10f), y + S(10f));
			g.DrawLine(val, num + width + S(10f), y + S(10f), x + w, y + S(10f));
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Ui.Draw(g, s, f, new Rectangle(num, y, width + S(2f), S(20f)), Ui.TextMute, (TextFormatFlags)4);
		return y + S(20f) + (after ?? S(12f));
	}

	private void DrawEmAsk(Graphics g, Rectangle r)
	{
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		bool flag = _emailFor == "signup";
		y = Back(g, x, y);
		if (flag)
		{
			y = Steps(g, x, y, width, 1);
		}
		y = Title(g, x, y, width, flag ? L.T("สม\u0e31ครด\u0e49วยอ\u0e35เมล", "Sign up with email") : L.T("ล\u0e37มรห\u0e31สผ\u0e48าน", "Forgot password"));
		y = Sub(g, x, y, width, flag ? L.T("ใส\u0e48อ\u0e35เมลของค\u0e38ณ เราจะส\u0e48งรห\u0e31สย\u0e37นย\u0e31น 6 หล\u0e31กไปให\u0e49 — 1 อ\u0e35เมลสม\u0e31ครได\u0e49 1 ไอด\u0e35", "Enter your email and we will send you a 6-digit code — one email gets one ID") : L.T("ใส\u0e48อ\u0e35เมลท\u0e35\u0e48ผ\u0e39กก\u0e31บไอด\u0e35ไว\u0e49 เราจะส\u0e48งรห\u0e31ส 6 หล\u0e31กไปให\u0e49ต\u0e31\u0e49งรห\u0e31สผ\u0e48านใหม\u0e48", "Enter the email linked to your ID and we will send a 6-digit code to reset your password"));
		y = Banner(g, x, y, width);
		y = Field(g, x, y, width, "em", L.T("อ\u0e35เมล", "Email"), "mail", "name@gmail.com", _authBusy);
		if (!_fieldErr.ContainsKey("em"))
		{
			y = Hint(g, x, y + S(6f), width, "info-circle", L.T("รห\u0e31สหมดอาย\u0e38ใน 10 นาท\u0e35 · ไม\u0e48เจอให\u0e49ด\u0e39ในโฟลเดอร\u0e4cสแปม", "The code expires in 10 minutes · check your spam folder if it is not there"), Ui.TextMute);
		}
		y += S(16f);
		bool enabled = EmailInput.LooksOk(((Control)_boxes["em"]).Text);
		Rectangle r2 = new Rectangle(x, y, width, S(46f));
		if (_authBusy)
		{
			Button(g, r2, "email-send", "loader-2", L.T("กำล\u0e31งส\u0e48งรห\u0e31ส…", "Sending code…"), primary: true, enabled: false, null, 19f, 16f, spin: true);
		}
		else
		{
			Button(g, r2, "email-send", "send", L.T("ส\u0e48งรห\u0e31สย\u0e37นย\u0e31น", "Send code"), primary: true, enabled, delegate
			{
				SubmitEmailStartAsync();
			});
		}
		y = r2.Bottom + S(16f);
		if (flag)
		{
			AltRow(g, x, y, width, L.T("อยากใช\u0e49 Discord แทน?", "Prefer Discord?"), L.T("สม\u0e31ครด\u0e49วย Discord", "Sign up with Discord"), "to-su1", delegate
			{
				GoAuth(Auth.Su1);
			});
		}
		else
		{
			AltRow(g, x, y, width, L.T("ผ\u0e39ก Discord ไว\u0e49?", "Linked Discord?"), L.T("ย\u0e37นย\u0e31นด\u0e49วย Discord", "Verify with Discord"), "to-forgot", delegate
			{
				GoAuth(Auth.Forgot);
			});
		}
	}

	private void DrawEmCode(Graphics g, Rectangle r)
	{
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		EmailSent emailSent = _emailSent;
		bool flag = _emailFor == "signup";
		string text = emailSent?.Email ?? "";
		y = Back(g, x, y);
		if (flag)
		{
			y = Steps(g, x, y, width, 1);
		}
		y = Title(g, x, y, width, L.T("ใส\u0e48รห\u0e31สย\u0e37นย\u0e31น", "Enter the code"));
		y = Sub(g, x, y, width, flag ? L.T("ส\u0e48งรห\u0e31ส 6 หล\u0e31กไปท\u0e35\u0e48 " + text + " แล\u0e49ว", "We sent a 6-digit code to " + text) : L.T("ถ\u0e49า " + text + " ผ\u0e39กก\u0e31บไอด\u0e35ไว\u0e49 เราส\u0e48งรห\u0e31ส 6 หล\u0e31กไปแล\u0e49ว", "If " + text + " is linked to an ID, we sent it a 6-digit code"));
		y = Banner(g, x, y, width);
		y = Field(g, x, y, width, "ec", L.T("รห\u0e31สย\u0e37นย\u0e31น 6 หล\u0e31ก", "6-digit code"), "key", "123456", _authBusy);
		TimeSpan timeSpan = ((emailSent == null) ? TimeSpan.Zero : (_emailSentAt.AddSeconds(emailSent.ExpiresIn) - DateTime.UtcNow));
		if (!_fieldErr.ContainsKey("ec"))
		{
			y = ((timeSpan > TimeSpan.Zero) ? Hint(g, x, y + S(6f), width, "clock-hour-4", L.T("รห\u0e31สหมดอาย\u0e38ใน ", "Code expires in ") + Mmss(timeSpan, minutesPad: false), Ui.TextMute) : Hint(g, x, y + S(6f), width, "alert-circle", L.T("รห\u0e31สหมดอาย\u0e38แล\u0e49ว — ส\u0e48งรห\u0e31สใหม\u0e48", "The code has expired — send a new one"), Ui.Bad));
		}
		y += S(16f);
		bool enabled = EmailInput.CleanCode(((Control)_boxes["ec"]).Text).Length == 6 && emailSent != null;
		Rectangle r2 = new Rectangle(x, y, width, S(46f));
		if (_authBusy)
		{
			Button(g, r2, "email-verify", "loader-2", L.T("กำล\u0e31งตรวจรห\u0e31ส…", "Checking…"), primary: true, enabled: false, null, 19f, 16f, spin: true);
		}
		else
		{
			Button(g, r2, "email-verify", "circle-check", L.T("ย\u0e37นย\u0e31นรห\u0e31ส", "Verify code"), primary: true, enabled, delegate
			{
				SubmitEmailVerifyAsync();
			});
		}
		y = ResendRow(g, x, r2.Bottom + S(14f), width, _resendAt, "email-resend", delegate
		{
			SubmitEmailStartAsync(resend: true);
		});
		AltRow(g, x, y + S(14f), width, L.T("อ\u0e35เมลผ\u0e34ด?", "Wrong email?"), L.T("เปล\u0e35\u0e48ยนอ\u0e35เมล", "Change email"), "email-change", delegate
		{
			GoAuth(Auth.EmAsk);
		});
	}

	private int ResendRow(Graphics g, int x, int y, int w, DateTime resendAt, string id, Action click)
	{
		TimeSpan timeSpan = resendAt - DateTime.UtcNow;
		if (timeSpan > TimeSpan.Zero || _authBusy)
		{
			return HintCenter(g, x, y, w, "refresh", L.T("ส\u0e48งรห\u0e31สอ\u0e35กคร\u0e31\u0e49งได\u0e49ใน ", "Send again in ") + Mmss((timeSpan > TimeSpan.Zero) ? timeSpan : TimeSpan.Zero, minutesPad: false), Ui.TextMute);
		}
		Font f = F(13.5f, medium: true);
		string s = L.T("ส\u0e48งรห\u0e31สอ\u0e35กคร\u0e31\u0e49ง", "Send a new code");
		int num = S(14f) + S(6f) + Ui.Measure(s, f).Width + S(2f);
		Rectangle r = new Rectangle(x + (w - num) / 2, y, num, S(20f));
		bool flag = AddHit(r, id, click);
		Icons.Draw(g, "refresh", new Point(r.X + S(7f), r.Y + S(10f)), S(14f), flag ? Ui.Text : Ui.PurpleLight);
		Ui.Draw(g, s, f, new Rectangle(r.X + S(20f), r.Y, num, r.Height), flag ? Ui.Text : Ui.PurpleLight, (TextFormatFlags)4);
		return r.Bottom;
	}

	private void DrawSu2(Graphics g, Rectangle r)
	{
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		y = Steps(g, x, y, width, 2);
		DiscordUser pendingDiscord = _pendingDiscord;
		y = ((_suVia == "email") ? EChip(g, x, y, width, _verifiedEmail, Verified) : DChip(g, x, y, width, pendingDiscord?.Name ?? "Discord", pendingDiscord?.AvatarUrl ?? "", Verified));
		y = Banner(g, x, y, width);
		string text = ((Control)_boxes["su_u"]).Text.Trim();
		string text2 = ((Control)_boxes["su_p"]).Text;
		string text3 = ((Control)_boxes["su_c"]).Text;
		y = Field(g, x, y, width, "su_u", L.T("ช\u0e37\u0e48อผ\u0e39\u0e49ใช\u0e49 (Durango ID)", "Username (Durango ID)"), "user", L.T("เช\u0e48น rex_rider", "e.g. rex_rider"), _authBusy);
		if (!_fieldErr.ContainsKey("su_u"))
		{
			string text4 = UserCheck(text);
			(string, string) tuple = text4 switch
			{
				"chars" => ("ใช\u0e49ได\u0e49เฉพาะ a–z 0–9 และ _ (ต\u0e31วเล\u0e47ก ไม\u0e48ม\u0e35เว\u0e49นวรรค)", "Only a–z, 0–9 and _ (lowercase, no spaces)"), 
				"len" => ($"ต\u0e49องยาว 4–16 ต\u0e31ว (ตอนน\u0e35\u0e49 {text.Length})", $"Must be 4–16 characters (now {text.Length})"), 
				"ok" => ("ร\u0e39ปแบบใช\u0e49ได\u0e49 · เปล\u0e35\u0e48ยนภายหล\u0e31งไม\u0e48ได\u0e49", "Looks good · cannot be changed later"), 
				_ => ("a–z 0–9 _ · 4–16 ต\u0e31ว · เปล\u0e35\u0e48ยนภายหล\u0e31งไม\u0e48ได\u0e49", "a–z 0–9 _ · 4–16 characters · cannot be changed later"), 
			};
			y = Hint(g, x, y + S(6f), width, (text4 == "ok") ? "circle-check" : ((text4 == "n") ? "info-circle" : "alert-circle"), L.T(tuple.Item1, tuple.Item2), (text4 == "ok") ? Ui.Good : ((text4 == "n") ? Ui.TextMute : Ui.Bad));
		}
		y += S(12f);
		y = Field(g, x, y, width, "su_p", L.T("รห\u0e31สผ\u0e48าน", "Password"), "lock", L.T("อย\u0e48างน\u0e49อย 8 ต\u0e31ว", "At least 8 characters"), _authBusy);
		y = Rules(g, x, y + S(6f), width, ("len", PwLen(text2)), ("nou", PwNoUser(text2, text))) + S(12f);
		y = Field(g, x, y, width, "su_c", L.T("ย\u0e37นย\u0e31นรห\u0e31สผ\u0e48าน", "Confirm password"), "lock", L.T("พ\u0e34มพ\u0e4cรห\u0e31สอ\u0e35กคร\u0e31\u0e49ง", "Type it again"), _authBusy);
		y = Rules(g, x, y + S(6f), width, ("match", PwMatch(text2, text3))) + S(12f);
		bool enabled = SignupValid(text, text2, text3);
		Rectangle r2 = new Rectangle(x, y, width, S(46f));
		if (_authBusy)
		{
			Button(g, r2, "signup-submit", "loader-2", L.T("กำล\u0e31งสร\u0e49างไอด\u0e35…", "Creating ID…"), primary: true, enabled: false, null, 19f, 16f, spin: true);
			return;
		}
		Button(g, r2, "signup-submit", "user-plus", L.T("สร\u0e49างไอด\u0e35และเข\u0e49าส\u0e39\u0e48ระบบ", "Create ID and sign in"), primary: true, enabled, delegate
		{
			SubmitSignupAsync();
		});
		int Verified(Graphics gg, Rectangle rr)
		{
			Font f = F(12f);
			string s = L.T("ย\u0e37นย\u0e31นแล\u0e49ว", "Verified");
			int width2 = Ui.Measure(s, f).Width;
			Ui.Draw(gg, s, f, new Rectangle(rr.Right - width2 - S(1f), rr.Y, width2 + S(2f), rr.Height), Ui.Good, (TextFormatFlags)4);
			Icons.Draw(gg, "circle-check", new Point(rr.Right - width2 - S(4f) - S(7f), rr.Y + rr.Height / 2), S(14f), Ui.Good);
			return width2 + S(4f) + S(14f);
		}
	}

	private void DrawWait(Graphics g, Rectangle r)
	{
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Expected O, but got Unknown
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Expected O, but got Unknown
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Expected O, but got Unknown
		//IL_0422: Unknown result type (might be due to invalid IL or missing references)
		//IL_0429: Expected O, but got Unknown
		//IL_049b: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a2: Expected O, but got Unknown
		bool flag = _waitFor == "reset";
		bool flag2 = _waitFor == "login";
		int x = r.X;
		int width = r.Width;
		int num = S(128f) + S(60f) + S(12f) + S(120f) + S(32f) + S(46f) + S(34f);
		int num2 = r.Y + Math.Max(0, (r.Height - num) / 2);
		Rectangle rectangle = new Rectangle(x + (width - S(76f)) / 2, num2 + S(2f), S(76f), S(76f));
		Pen val = new Pen(Ui.K3, (float)S(3f));
		try
		{
			g.DrawEllipse(val, RectangleF.Inflate(rectangle, -S(1.5f), -S(1.5f)));
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Pen val2 = new Pen(Ui.Yellow, (float)S(3f))
		{
			StartCap = (LineCap)2,
			EndCap = (LineCap)2
		};
		try
		{
			g.DrawArc(val2, RectangleF.Inflate(rectangle, -S(1.5f), -S(1.5f)), (float)(-90 + _animT * 11 % 360), 90f);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		Rectangle rectangle2 = Rectangle.Inflate(rectangle, -S(13f), -S(13f));
		SolidBrush val3 = new SolidBrush(Ui.PurpleDark);
		try
		{
			g.FillEllipse((Brush)(object)val3, rectangle2);
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
		Icons.Draw(g, "brand-discord", new Point(rectangle2.X + rectangle2.Width / 2, rectangle2.Y + rectangle2.Height / 2), S(28f), Ui.PurpleLight);
		num2 = rectangle.Bottom + S(14f);
		string s = L.T("รอย\u0e37นย\u0e31นใน Discord…", "Waiting for Discord…");
		Ui.Draw(g, s, F(22f, medium: true), new Rectangle(x, num2, width, S(30f)), Ui.Text, (TextFormatFlags)5);
		num2 += S(30f) + S(4f);
		string s2 = ((flag || flag2) ? L.T("เป\u0e34ดหน\u0e49า Discord ในเบราว\u0e4cเซอร\u0e4cแล\u0e49ว กด «อน\u0e38ญาต» ด\u0e49วยบ\u0e31ญช\u0e35ท\u0e35\u0e48ผ\u0e39กก\u0e31บไอด\u0e35ไว\u0e49 แล\u0e49วกล\u0e31บมาท\u0e35\u0e48น\u0e35\u0e48", "We opened Discord in your browser. Authorize with the account linked to your ID, then come back") : L.T("เป\u0e34ดหน\u0e49า Discord ในเบราว\u0e4cเซอร\u0e4cแล\u0e49ว กด «อน\u0e38ญาต» แล\u0e49วกล\u0e31บมาท\u0e35\u0e48น\u0e35\u0e48ได\u0e49เลย", "We opened Discord in your browser. Press “Authorize”, then come back here"));
		num2 = WrapText(g, s2, F(13.5f), x, num2, width, Ui.TextDim, center: true) + S(12f);
		string[] array = new string[3]
		{
			L.T("เบราว\u0e4cเซอร\u0e4cเป\u0e34ดหน\u0e49า Discord", "Browser opened Discord"),
			L.T("กด «อน\u0e38ญาต» ในหน\u0e49า Discord", "Press “Authorize” on Discord"),
			flag2 ? L.T("Launcher เข\u0e49าส\u0e39\u0e48ระบบให\u0e49เอง", "The launcher signs you in") : (flag ? L.T("Launcher พาไปต\u0e31\u0e49งรห\u0e31สใหม\u0e48เอง", "The launcher continues to a new password") : L.T("Launcher พาไปต\u0e31\u0e49งไอด\u0e35เอง", "The launcher continues to set your ID"))
		};
		bool flag3 = _waitUrl != null;
		int num3 = (_waitGot ? 2 : (flag3 ? 1 : 0));
		for (int i = 0; i < 3; i++)
		{
			Rectangle r2 = new Rectangle(x, num2, width, S(36f));
			bool flag4 = i < num3;
			bool flag5 = i == num3;
			if (flag5)
			{
				Ui.Fill(g, r2, S(8f), Ui.K2);
			}
			Rectangle rectangle3 = new Rectangle(r2.X + S(10f), r2.Y + (r2.Height - S(22f)) / 2, S(22f), S(22f));
			if (flag4)
			{
				SolidBrush val4 = new SolidBrush(Ui.PurpleDark);
				try
				{
					g.FillEllipse((Brush)(object)val4, rectangle3);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
				Icons.Draw(g, "check", new Point(rectangle3.X + rectangle3.Width / 2, rectangle3.Y + rectangle3.Height / 2), S(13f), Ui.Yellow);
			}
			else
			{
				Pen val5 = new Pen(flag5 ? Ui.Yellow : K4);
				try
				{
					g.DrawEllipse(val5, rectangle3.X, rectangle3.Y, rectangle3.Width - 1, rectangle3.Height - 1);
				}
				finally
				{
					((IDisposable)val5)?.Dispose();
				}
				Ui.Draw(g, (i + 1).ToString(), F(12f), rectangle3, flag5 ? Ui.Yellow : Ui.TextMute, (TextFormatFlags)5);
			}
			Ui.Draw(g, array[i], F(13.5f), Rectangle.FromLTRB(rectangle3.Right + S(10f), r2.Y, r2.Right - S(8f), r2.Bottom), flag4 ? Ui.TextDim : (flag5 ? Ui.Text : Ui.TextMute), (TextFormatFlags)32772);
			num2 += S(38f);
		}
		num2 += S(10f);
		TimeSpan t = _waitStart.AddSeconds(_waitSecs) - DateTime.UtcNow;
		string s3 = L.T("หมดเวลาใน ", "Times out in ");
		Font f = F(12.5f);
		Font f2 = F(12.5f, medium: true);
		string s4 = Mmss(t, minutesPad: false);
		int width2 = Ui.Measure(s3, f).Width;
		int width3 = Ui.Measure(s4, f2).Width;
		int num4 = x + (width - width2 - width3) / 2;
		Ui.Draw(g, s3, f, new Rectangle(num4, num2, width2 + S(2f), S(20f)), Ui.TextMute, (TextFormatFlags)4);
		Ui.Draw(g, s4, f2, new Rectangle(num4 + width2, num2, width3 + S(2f), S(20f)), Ui.TextDim, (TextFormatFlags)4);
		num2 += S(20f) + S(12f);
		Button(g, new Rectangle(x, num2, width, S(46f)), "reopen", "external-link", L.T("เป\u0e34ดเบราว\u0e4cเซอร\u0e4cอ\u0e35กคร\u0e31\u0e49ง", "Open browser again"), primary: false, flag3, delegate
		{
			if (_waitUrl != null)
			{
				OpenUrl(_waitUrl);
				Toast("เป\u0e34ดหน\u0e49า Discord ในเบราว\u0e4cเซอร\u0e4cอ\u0e35กคร\u0e31\u0e49งแล\u0e49ว", "Opened Discord in your browser again", "external-link", Ui.PurpleLight);
			}
		}, 17f, 14.5f);
		num2 += S(46f) + S(12f);
		string text = L.T("ยกเล\u0e34ก", "Cancel");
		Font f3 = F(13.5f);
		int num5 = Ui.Measure(text, f3).Width + S(4f);
		LinkAt(g, new Rectangle(x + (width - num5) / 2, num2, num5, S(22f)), "cancel-wait", text, f3, delegate
		{
			GoAuth(DiscordBack(_waitFor));
		}, Ui.PurpleLight);
	}

	private void DrawForgot(Graphics g, Rectangle r)
	{
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Expected O, but got Unknown
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Expected O, but got Unknown
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Expected O, but got Unknown
		_emailFor = "reset";
		bool emailReset = _features.EmailReset;
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		y = Back(g, x, y);
		y = Title(g, x, y, width, L.T("ล\u0e37มรห\u0e31สผ\u0e48าน", "Forgot password"));
		y = Sub(g, x, y, width, emailReset ? L.T("ย\u0e37นย\u0e31นต\u0e31วตนด\u0e49วย Discord หร\u0e37ออ\u0e35เมลท\u0e35\u0e48ผ\u0e39กก\u0e31บไอด\u0e35ไว\u0e49 แล\u0e49วต\u0e31\u0e49งรห\u0e31สผ\u0e48านใหม\u0e48ได\u0e49ท\u0e31นท\u0e35", "Verify with the Discord account or email linked to your ID, then set a new password right away") : L.T("ย\u0e37นย\u0e31นต\u0e31วตนด\u0e49วย Discord ท\u0e35\u0e48ผ\u0e39กก\u0e31บไอด\u0e35ไว\u0e49 แล\u0e49วต\u0e31\u0e49งรห\u0e31สผ\u0e48านใหม\u0e48ได\u0e49ท\u0e31นท\u0e35", "Verify with the Discord account linked to your ID, then set a new password right away"));
		y = Banner(g, x, y, width);
		y += S(2f);
		int num = x + width / 2;
		Rectangle item = new Rectangle(num - S(35f) - S(10f) - S(52f), y, S(52f), S(52f));
		Rectangle item2 = new Rectangle(num + S(35f) + S(10f), y, S(52f), S(52f));
		(Rectangle, string, Color)[] array = new(Rectangle, string, Color)[2]
		{
			(item, "key", Ui.Yellow),
			(item2, "brand-discord", Ui.PurpleLight)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(Rectangle, string, Color) tuple = array[i];
			Rectangle item3 = tuple.Item1;
			string item4 = tuple.Item2;
			Color item5 = tuple.Item3;
			SolidBrush val = new SolidBrush(Ui.K2);
			try
			{
				g.FillEllipse((Brush)(object)val, item3);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			Pen val2 = new Pen(Ui.K3);
			try
			{
				g.DrawEllipse(val2, item3.X, item3.Y, item3.Width - 1, item3.Height - 1);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			Icons.Draw(g, item4, new Point(item3.X + item3.Width / 2, item3.Y + item3.Height / 2), S(22f), item5);
		}
		Pen val3 = new Pen(K4, (float)S(2f))
		{
			DashStyle = (DashStyle)1
		};
		try
		{
			g.DrawLine(val3, item.Right + S(10f), y + S(26f), item2.X - S(10f), y + S(26f));
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
		y += S(52f) + S(18f);
		Button(g, new Rectangle(x, y, width, S(46f)), "discord-reset", "brand-discord", L.T("ย\u0e37นย\u0e31นด\u0e49วย Discord ท\u0e35\u0e48ผ\u0e39กไว\u0e49", "Verify with linked Discord"), primary: true, enabled: true, delegate
		{
			DiscordAsync("reset");
		}, 20f);
		y += S(46f) + S(10f);
		if (emailReset)
		{
			y = Or(g, x, y + S(2f), width);
			Button(g, new Rectangle(x, y, width, S(46f)), "email-reset", "mail", L.T("ร\u0e31บรห\u0e31สทางอ\u0e35เมล", "Get a code by email"), primary: false, enabled: true, delegate
			{
				StartEmail("reset");
			}, 19f, 15f);
			y += S(46f) + S(10f);
		}
		y = HintCenter(g, x, y, width, "info-circle", emailReset ? L.T("ล\u0e37มช\u0e37\u0e48อผ\u0e39\u0e49ใช\u0e49ด\u0e49วย? ย\u0e37นย\u0e31นแล\u0e49วจะเห\u0e47นไอด\u0e35ของค\u0e38ณ", "Forgot your username too? You will see it after verifying") : L.T("ล\u0e37มช\u0e37\u0e48อผ\u0e39\u0e49ใช\u0e49ด\u0e49วย? ย\u0e37นย\u0e31น Discord แล\u0e49วจะเห\u0e47นไอด\u0e35ของค\u0e38ณ", "Forgot your username too? You will see it after verifying"), Ui.TextMute);
		AltRow(g, x, y + S(16f), width, L.T("น\u0e36กออกแล\u0e49ว?", "Remembered it?"), L.T("เข\u0e49าส\u0e39\u0e48ระบบ", "Sign in"), "to-login", delegate
		{
			GoAuth(Auth.Login);
		});
	}

	private void DrawReset(Graphics g, Rectangle r)
	{
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		string user = _resetUser ?? "";
		y = Title(g, x, y, width, L.T("ต\u0e31\u0e49งรห\u0e31สผ\u0e48านใหม\u0e48", "Set a new password"));
		if (_resetVia == "email")
		{
			y = Sub(g, x, y, width, L.T("ย\u0e37นย\u0e31นอ\u0e35เมลแล\u0e49ว — ต\u0e31\u0e49งรห\u0e31สใหม\u0e48ให\u0e49ไอด\u0e35น\u0e35\u0e49", "Email verified — set a new password for this ID"));
			y = EChip(g, x, y, width, _verifiedEmail, (Graphics gg, Rectangle rr) => IdTag(gg, rr, user));
		}
		else
		{
			y = Sub(g, x, y, width, L.T("ย\u0e37นย\u0e31น Discord แล\u0e49ว — ต\u0e31\u0e49งรห\u0e31สใหม\u0e48ให\u0e49ไอด\u0e35น\u0e35\u0e49", "Discord verified — set a new password for this ID"));
			y = DChip(g, x, y, width, _pendingDiscord?.Name ?? "Discord", _pendingDiscord?.AvatarUrl ?? "", (Graphics gg, Rectangle rr) => IdTag(gg, rr, user));
		}
		y = Banner(g, x, y, width);
		string text = ((Control)_boxes["rs_p"]).Text;
		string text2 = ((Control)_boxes["rs_c"]).Text;
		y = Field(g, x, y, width, "rs_p", L.T("รห\u0e31สผ\u0e48านใหม\u0e48", "New password"), "lock", L.T("อย\u0e48างน\u0e49อย 8 ต\u0e31ว", "At least 8 characters"), _authBusy);
		y = Rules(g, x, y + S(6f), width, ("len", PwLen(text)), ("nou", PwNoUser(text, user))) + S(12f);
		y = Field(g, x, y, width, "rs_c", L.T("ย\u0e37นย\u0e31นรห\u0e31สผ\u0e48านใหม\u0e48", "Confirm new password"), "lock", L.T("พ\u0e34มพ\u0e4cรห\u0e31สอ\u0e35กคร\u0e31\u0e49ง", "Type it again"), _authBusy);
		y = Rules(g, x, y + S(6f), width, ("match", PwMatch(text, text2))) + S(12f);
		bool enabled = PwLen(text) == true && PwNoUser(text, user) != false && PwMatch(text, text2) == true;
		Rectangle r2 = new Rectangle(x, y, width, S(46f));
		if (_authBusy)
		{
			Button(g, r2, "reset-submit", "loader-2", L.T("กำล\u0e31งบ\u0e31นท\u0e36ก…", "Saving…"), primary: true, enabled: false, null, 19f, 16f, spin: true);
			return;
		}
		Button(g, r2, "reset-submit", "check", L.T("บ\u0e31นท\u0e36กรห\u0e31สใหม\u0e48", "Save new password"), primary: true, enabled, delegate
		{
			SubmitResetAsync();
		});
	}

	private void DrawLinked(Graphics g, Rectangle r)
	{
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		string user = _linkedUser ?? "";
		y = Back(g, x, y);
		y = Steps(g, x, y, width, 1);
		y = Title(g, x, y, width, L.T("สม\u0e31ครสมาช\u0e34ก", "Sign up")) + S(14f);
		y = BannerBox(g, x, y, width, "bad", "alert-circle", L.T("Discord บ\u0e31ญช\u0e35น\u0e35\u0e49ม\u0e35ไอด\u0e35แล\u0e49ว", "This Discord account already has an ID"), L.T("1 บ\u0e31ญช\u0e35 Discord สม\u0e31ครได\u0e49 1 ไอด\u0e35เท\u0e48าน\u0e31\u0e49น", "One Discord account can have only one ID"));
		y = Banner(g, x, y, width);
		if (user.Length > 0 || _pendingDiscord != null)
		{
			y = DChip(g, x, y, width, _pendingDiscord?.Name ?? "Discord", _pendingDiscord?.AvatarUrl ?? "", (Graphics gg, Rectangle rr) => (user.Length > 0) ? IdTag(gg, rr, user) : 0);
		}
		Rectangle r2 = new Rectangle(x, y, width, S(46f));
		bool flag = _loginToken != null && user.Length > 0;
		if (flag)
		{
			if (_authBusy)
			{
				Button(g, r2, "linked-login", "loader-2", L.T("กำล\u0e31งเข\u0e49าส\u0e39\u0e48ระบบ…", "Signing in…"), primary: true, enabled: false, null, 19f, 16f, spin: true);
			}
			else
			{
				Button(g, r2, "linked-login", "login", L.T("เข\u0e49าส\u0e39\u0e48ระบบเป\u0e47น " + user, "Sign in as " + user), primary: true, enabled: true, delegate
				{
					LinkedLoginAsync();
				});
			}
		}
		else
		{
			Button(g, r2, "linked-login", "login", (user.Length > 0) ? L.T("เข\u0e49าส\u0e39\u0e48ระบบด\u0e49วย " + user, "Sign in as " + user) : L.T("เข\u0e49าส\u0e39\u0e48ระบบ", "Sign in"), primary: true, enabled: true, delegate
			{
				if (user.Length > 0)
				{
					((Control)_boxes["lu"]).Text = user;
				}
				((Control)_boxes["lp"]).Text = "";
				GoAuth(Auth.Login);
			});
		}
		y += S(46f) + S(10f);
		Button(g, new Rectangle(x, y, width, S(46f)), "linked-forgot", "key", L.T("ล\u0e37มรห\u0e31สผ\u0e48าน? ต\u0e31\u0e49งใหม\u0e48ด\u0e49วย Discord", "Forgot password? Reset with Discord"), primary: false, !_authBusy, delegate
		{
			_emailFor = "reset";
			GoAuth(Auth.Forgot);
		}, 17f, 14.5f);
		y += S(46f) + S(flag ? 12 : 16);
		if (flag)
		{
			y = HintCenter(g, x, y, width, "info-circle", L.T("เพ\u0e34\u0e48งย\u0e37นย\u0e31น Discord บ\u0e31ญช\u0e35น\u0e35\u0e49แล\u0e49ว จ\u0e36งเข\u0e49าได\u0e49โดยไม\u0e48ต\u0e49องใส\u0e48รห\u0e31ส", "You just verified this Discord account, so no password is needed"), Ui.TextMute) + S(14f);
		}
		string text = L.T("ใช\u0e49บ\u0e31ญช\u0e35 Discord อ\u0e37\u0e48น", "Use another Discord account");
		Font f = F(13.5f, medium: true);
		int num = Ui.Measure(text, f).Width + S(4f);
		LinkAt(g, new Rectangle(x + (width - num) / 2, y, num, S(22f)), "discord-again", text, f, delegate
		{
			if (!_authBusy)
			{
				DiscordAsync("signup");
			}
		}, Ui.PurpleLight);
	}

	private void DrawNoId(Graphics g, Rectangle r)
	{
		int x = r.X;
		int width = r.Width;
		int y = r.Y;
		DiscordUser pendingDiscord = _pendingDiscord;
		y = Back(g, x, y);
		y = Title(g, x, y, width, L.T("เข\u0e49าส\u0e39\u0e48ระบบด\u0e49วย Discord", "Sign in with Discord"));
		y = Sub(g, x, y, width, L.T("ย\u0e37นย\u0e31น Discord แล\u0e49ว แต\u0e48ย\u0e31งไม\u0e48ม\u0e35 Durango ID ท\u0e35\u0e48ผ\u0e39กก\u0e31บบ\u0e31ญช\u0e35น\u0e35\u0e49", "Discord is verified, but no Durango ID is linked to this account yet"));
		y = BannerBox(g, x, y, width, "warn", "alert-circle", L.T("Discord น\u0e35\u0e49ย\u0e31งไม\u0e48ม\u0e35ไอด\u0e35", "This Discord has no ID yet"), L.T("สม\u0e31ครไอด\u0e35ใหม\u0e48ด\u0e49วย Discord น\u0e35\u0e49ได\u0e49เลย ไม\u0e48ต\u0e49องย\u0e37นย\u0e31นซ\u0e49ำ", "Create a new ID with this Discord right away — no need to verify again"));
		y = Banner(g, x, y, width);
		y = DChip(g, x, y, width, pendingDiscord?.Name.OrIfEmpty("Discord") ?? "Discord", pendingDiscord?.AvatarUrl ?? "", VerifiedTag);
		Button(g, new Rectangle(x, y, width, S(46f)), "noid-signup", "user-plus", L.T("สม\u0e31ครไอด\u0e35ด\u0e49วย Discord น\u0e35\u0e49", "Create an ID with this Discord"), primary: true, _signupToken != null, delegate
		{
			_scr = Auth.Login;
			GoAuth(Auth.Su2);
		});
		y = Or(g, x, y + S(46f) + S(10f), width);
		Button(g, new Rectangle(x, y, width, S(46f)), "noid-login", "login", L.T("เข\u0e49าด\u0e49วยรห\u0e31สผ\u0e48าน", "Sign in with password"), primary: false, enabled: true, delegate
		{
			GoAuth(Auth.Login);
		}, 19f, 15f);
		y += S(46f) + S(10f);
		Hint(g, x, y, width, "link", L.T("ม\u0e35ไอด\u0e35ท\u0e35\u0e48สม\u0e31ครด\u0e49วยอ\u0e35เมล? เข\u0e49าด\u0e49วยรห\u0e31สผ\u0e48าน แล\u0e49วกด «ผ\u0e39ก Discord» ในการ\u0e4cดบ\u0e31ญช\u0e35 คร\u0e31\u0e49งหน\u0e49าใช\u0e49ป\u0e38\u0e48ม Discord ได\u0e49", "Have an ID made with email? Sign in with your password, then press “Link Discord” on your account card — next time the Discord button works"), Ui.TextMute);
	}

	private int VerifiedTag(Graphics g, Rectangle rr)
	{
		Font f = F(12f);
		string s = L.T("ย\u0e37นย\u0e31นแล\u0e49ว", "Verified");
		int width = Ui.Measure(s, f).Width;
		Ui.Draw(g, s, f, new Rectangle(rr.Right - width - S(1f), rr.Y, width + S(2f), rr.Height), Ui.Good, (TextFormatFlags)4);
		Icons.Draw(g, "circle-check", new Point(rr.Right - width - S(4f) - S(7f), rr.Y + rr.Height / 2), S(14f), Ui.Good);
		return width + S(4f) + S(14f);
	}

	private int AccountCardHeight(Graphics g)
	{
		int num = (_session.Supporter ? S(38f) : S(58f));
		int num2 = (ShowAddEmailRow ? S(30f) : 0);
		int num3 = (ShowDiscordViaRow ? S(44f) : 0);
		return S(14f) + S(30f) + S(46f) + S(12f) + num + num2 + num3 + S(14f) + S(36f) + S(14f);
	}

	private int DrawDiscordViaRow(Graphics g, int x, int y, int w, Session s)
	{
		Rectangle r = new Rectangle(x, y, w, S(34f));
		Ui.Card(g, r, S(9f), Ui.K2, Ui.K3);
		Icons.Draw(g, "brand-discord", new Point(r.X + S(10f) + S(8f), r.Y + r.Height / 2), S(15f), DiscordLight);
		Font f = F(13f);
		Font f2 = F(13f, medium: true);
		string s2 = L.T("เข\u0e49าส\u0e39\u0e48ระบบด\u0e49วย Discord ", "Signed in with Discord ");
		int width = Ui.Measure(s2, f).Width;
		int num = r.X + S(10f) + S(16f) + S(8f);
		Ui.Draw(g, s2, f, new Rectangle(num, r.Y, width + S(2f), r.Height), Ui.TextDim, (TextFormatFlags)4);
		Ui.Draw(g, s.DiscordName.OrIfEmpty("Discord"), f2, Rectangle.FromLTRB(num + width, r.Y, r.Right - S(10f), r.Bottom), Ui.Text, (TextFormatFlags)32772);
		return r.Bottom;
	}

	private void DrawAccountCard(Graphics g, Rectangle r)
	{
		Session session = _session;
		Ui.Card(g, r, S(12f), Ui.K1, Ui.K3);
		int num = r.X + S(16f);
		int num2 = r.Width - S(32f);
		int num3 = r.Y + S(14f);
		Font f = F(13f);
		Ui.Draw(g, L.T("บ\u0e31ญช\u0e35ของฉ\u0e31น", "My account"), f, new Rectangle(num, num3, num2, S(20f)), Ui.TextMute, (TextFormatFlags)4);
		if (session.Remember)
		{
			DateTime dateTime = session.RefreshUntilUtc.ToLocalTime();
			string s = L.T($"จำไว\u0e49ถ\u0e36ง {dateTime.Day} {ThaiMonths[dateTime.Month - 1]}", $"Remembered until {EnMonths[dateTime.Month - 1]} {dateTime.Day}");
			Font f2 = F(12f);
			int width = Ui.Measure(s, f2).Width;
			Ui.Draw(g, s, f2, new Rectangle(num + num2 - width - S(1f), num3, width + S(2f), S(20f)), Ui.TextMute, (TextFormatFlags)4);
			Icons.Draw(g, "clock-hour-4", new Point(num + num2 - width - S(5f) - S(6f), num3 + S(10f)), S(13f), Ui.TextMute);
		}
		num3 += S(30f);
		DrawAvatar(g, new Rectangle(num, num3, S(46f), S(46f)), session.DiscordName.OrIfEmpty(session.Username), session.AvatarUrl, Ui.K1, session.HasDiscord);
		string s2 = (session.Supporter ? "Supporter" : L.T("ไม\u0e48ม\u0e35ยศ", "No role"));
		Font f3 = F(12.5f, session.Supporter);
		int num4 = Ui.Measure(s2, f3).Width + S(18f) + (session.Supporter ? S(17f) : 0);
		Rectangle r2 = new Rectangle(num + num2 - num4, num3 + (S(46f) - S(24f)) / 2, num4, S(24f));
		if (session.Supporter)
		{
			Ui.Fill(g, r2, S(6f), Ui.Yellow);
			Icons.Fill(g, "star-f", new RectangleF(r2.X + S(9f), (float)r2.Y + (float)(r2.Height - S(13f)) / 2f, S(13f), S(13f)), Ui.OnYellow);
			Ui.Draw(g, s2, f3, new Rectangle(r2.X + S(9f) + S(17f), r2.Y, num4, r2.Height), Ui.OnYellow, (TextFormatFlags)4);
		}
		else
		{
			Ui.Card(g, r2, S(6f), Ui.K2, Ui.K3);
			Ui.Draw(g, s2, f3, r2, Ui.TextMute, (TextFormatFlags)5);
		}
		int num5 = num + S(46f) + S(12f);
		int num6 = r2.X - S(8f) - num5;
		Ui.Draw(g, session.Username, F(18f, medium: true), new Rectangle(num5, num3, num6, S(26f)), Ui.Text, (TextFormatFlags)32772);
		Icons.Draw(g, session.HasDiscord ? "brand-discord" : "mail", new Point(num5 + S(7f), num3 + S(26f) + S(9f)), S(14f), Ui.PurpleLight);
		Ui.Draw(g, session.HasDiscord ? session.DiscordName.OrIfEmpty("Discord") : session.Email.OrIfEmpty(L.T("ไอด\u0e35อ\u0e35เมล", "Email ID")), F(13f), new Rectangle(num5 + S(19f), num3 + S(26f), num6 - S(19f), S(19f)), Ui.TextDim, (TextFormatFlags)32772);
		num3 += S(46f) + S(12f);
		Font f4 = F(13f);
		if (session.Supporter)
		{
			Rectangle r3 = new Rectangle(num, num3, num2, S(38f));
			Ui.Fill(g, r3, S(9f), Ui.PurpleDark);
			Icons.Draw(g, "bolt", new Point(r3.X + S(12f) + S(8f), r3.Y + S(19f)), S(16f), Ui.Yellow);
			Ui.Draw(g, L.T("Supporter: เข\u0e49าเลนพ\u0e34เศษ ไม\u0e48ต\u0e49องรอค\u0e34ว", "Supporter: priority lane — no queue"), f4, Rectangle.FromLTRB(r3.X + S(37f), r3.Y, r3.Right - S(10f), r3.Bottom), Ui.Text, (TextFormatFlags)32772);
			num3 = r3.Bottom;
		}
		else if (!session.HasDiscord)
		{
			Rectangle r4 = new Rectangle(num, num3, num2, S(58f));
			Ui.Card(g, r4, S(9f), Ui.K2, Ui.K3);
			Icons.Draw(g, "brand-discord", new Point(r4.X + S(12f) + S(8f), r4.Y + S(9f) + S(10f)), S(16f), Ui.PurpleLight);
			int num7 = r4.X + S(37f);
			bool flag = _linkCts != null;
			Ui.Draw(g, flag ? (L.T("รอย\u0e37นย\u0e31นใน Discord… ", "Waiting for Discord… ") + Mmss(_linkStart.AddSeconds(300.0) - DateTime.UtcNow, minutesPad: false)) : L.T("ย\u0e31งไม\u0e48ได\u0e49ผ\u0e39ก Discord —", "Discord not linked —"), f4, new Rectangle(num7, r4.Y + S(9f), r4.Right - num7 - S(10f), S(20f)), Ui.TextDim, (TextFormatFlags)32772);
			string s3 = (flag ? L.T("ยกเล\u0e34ก", "Cancel") : L.T("ผ\u0e39ก Discord (ร\u0e31บยศ Supporter)", "Link Discord (Supporter role)"));
			int width2 = Ui.Measure(s3, f4).Width;
			Rectangle r5 = new Rectangle(num7, r4.Y + S(29f), width2 + S(18f), S(20f));
			bool flag2 = AddHit(r5, "link-discord", flag ? new Action(CancelLink) : ((Action)delegate
			{
				LinkDiscordAsync();
			}), !_launching);
			Ui.Draw(g, s3, f4, new Rectangle(r5.X, r5.Y, width2 + S(2f), r5.Height), flag2 ? Ui.Text : Ui.PurpleLight, (TextFormatFlags)4);
			if (!flag)
			{
				Icons.Draw(g, "link", new Point(r5.X + width2 + S(3f) + S(7f), r5.Y + S(10f)), S(13f), flag2 ? Ui.Text : Ui.PurpleLight);
			}
			num3 = r4.Bottom;
		}
		else
		{
			Rectangle r6 = new Rectangle(num, num3, num2, S(58f));
			Ui.Card(g, r6, S(9f), Ui.K2, Ui.K3);
			Icons.Draw(g, "info-circle", new Point(r6.X + S(12f) + S(8f), r6.Y + S(9f) + S(10f)), S(16f), Ui.PurpleLight);
			int num8 = r6.X + S(37f);
			Ui.Draw(g, L.T("ไม\u0e48ม\u0e35ยศ Supporter —", "No Supporter role —"), f4, new Rectangle(num8, r6.Y + S(9f), r6.Right - num8 - S(10f), S(20f)), Ui.TextDim, (TextFormatFlags)4);
			string s4 = L.T("ด\u0e39ว\u0e34ธ\u0e35ร\u0e31บยศใน Discord", "see how to get it on Discord");
			int width3 = Ui.Measure(s4, f4).Width;
			Rectangle r7 = new Rectangle(num8, r6.Y + S(29f), width3 + S(18f), S(20f));
			bool flag3 = AddHit(r7, "get-role", delegate
			{
				OpenUrl(_acct.DiscordInvite);
			});
			Ui.Draw(g, s4, f4, new Rectangle(r7.X, r7.Y, width3 + S(2f), r7.Height), flag3 ? Ui.Text : Ui.PurpleLight, (TextFormatFlags)4);
			Icons.Draw(g, "external-link", new Point(r7.X + width3 + S(3f) + S(7f), r7.Y + S(10f)), S(13f), flag3 ? Ui.Text : Ui.PurpleLight);
			num3 = r6.Bottom;
		}
		if (ShowAddEmailRow)
		{
			num3 = DrawAddEmailRow(g, num, num3 + S(10f), num2);
		}
		if (ShowDiscordViaRow)
		{
			num3 = DrawDiscordViaRow(g, num, num3 + S(10f), num2, session);
		}
		num3 += S(14f);
		Font f5 = F(13.5f);
		string s5 = L.T("ออกจากระบบ", "Sign out");
		int width4 = S(12f) + S(16f) + S(7f) + Ui.Measure(s5, f5).Width + S(12f);
		Rectangle r8 = new Rectangle(num, num3, width4, S(36f));
		bool flag4 = AddHit(r8, "logout", SignOut, !_launching);
		if (flag4)
		{
			Ui.Fill(g, r8, S(9f), Ui.K2);
		}
		Ui.Stroke(g, r8, S(9f), Ui.K3);
		Icons.Draw(g, "logout", new Point(r8.X + S(12f) + S(8f), r8.Y + r8.Height / 2), S(16f), flag4 ? Ui.Text : Ui.TextDim);
		Ui.Draw(g, s5, f5, new Rectangle(r8.X + S(12f) + S(16f) + S(7f), r8.Y, width4, r8.Height), flag4 ? Ui.Text : Ui.TextDim, (TextFormatFlags)4);
		Font f6 = F(12.5f);
		string s6 = L.T("เปล\u0e35\u0e48ยนรห\u0e31สผ\u0e48าน", "Change password");
		int num9 = S(14f) + S(5f) + Ui.Measure(s6, f6).Width + S(2f);
		Rectangle r9 = new Rectangle(num + num2 - num9, num3, num9, S(36f));
		bool flag5 = AddHit(r9, "chpw", OpenChpw, !_launching);
		Icons.Draw(g, "key", new Point(r9.X + S(7f), r9.Y + r9.Height / 2), S(14f), flag5 ? Ui.Text : Ui.PurpleLight);
		Ui.Draw(g, s6, f6, new Rectangle(r9.X + S(19f), r9.Y, num9, r9.Height), flag5 ? Ui.Text : Ui.PurpleLight, (TextFormatFlags)4);
	}

	private int DrawAccountPill(Graphics g, int right)
	{
		Session session = _session;
		Font f = F(13.5f);
		int width = Ui.Measure(session.Username, f).Width;
		int num = S(4f) + S(24f) + S(8f) + width + (session.Supporter ? (S(8f) + S(13f)) : 0) + S(12f);
		Rectangle r = new Rectangle(right - num, S(12f), num, S(32f));
		bool flag = AddHit(r, "acct", delegate
		{
			ShowPage(Page.Home);
		});
		Ui.Card(g, r, (float)r.Height / 2f, Ui.K2, flag ? K4 : Ui.K3);
		DrawAvatar(g, new Rectangle(r.X + S(4f), r.Y + S(4f), S(24f), S(24f)), session.DiscordName.OrIfEmpty(session.Username), session.AvatarUrl, Ui.K2, badge: false);
		int num2 = r.X + S(4f) + S(24f) + S(8f);
		Ui.Draw(g, session.Username, f, new Rectangle(num2, r.Y, width + S(2f), r.Height), Ui.Text, (TextFormatFlags)4);
		if (session.Supporter)
		{
			Icons.Fill(g, "star-f", new RectangleF(num2 + width + S(8f), (float)r.Y + (float)(r.Height - S(13f)) / 2f, S(13f), S(13f)), Ui.Yellow);
		}
		return num;
	}

	private void DrawChpwModal(Graphics g)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		Session session = _session;
		if (session == null)
		{
			_chpwOpen = false;
			return;
		}
		Rectangle clientRectangle = ((Control)this).ClientRectangle;
		SolidBrush val = new SolidBrush(Color.FromArgb(184, 8, 7, 12));
		try
		{
			g.FillRectangle((Brush)(object)val, clientRectangle);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		AddHit(clientRectangle, "modal-bg", null, enabled: false);
		int num = _fieldErr.Keys.Count((string k) => k.StartsWith("cp_"));
		int num2 = S(62f) + S(69f) + S(12f) + 2 * S(93f) + S(12f) + S(60f) + S(22f) + num * S(22f);
		Rectangle r = new Rectangle(clientRectangle.X + (clientRectangle.Width - S(400f)) / 2, clientRectangle.Y + (clientRectangle.Height - num2) / 2, S(400f), num2);
		Ui.Card(g, r, S(14f), Ui.K1, Ui.K3);
		int num3 = r.X + S(24f);
		int num4 = r.Width - S(48f);
		int num5 = r.Y + S(20f);
		Ui.Draw(g, L.T("เปล\u0e35\u0e48ยนรห\u0e31สผ\u0e48าน", "Change password"), F(22f, medium: true), new Rectangle(num3, num5, num4 - S(44f), S(30f)), Ui.Text, (TextFormatFlags)4);
		Rectangle r2 = new Rectangle(num3 + num4 - S(40f), num5 - S(2f), S(40f), S(34f));
		bool flag = AddHit(r2, "chpw-x", CloseChpw);
		if (flag)
		{
			Ui.Fill(g, r2, S(8f), Ui.K2);
		}
		Icons.Draw(g, "x", new Point(r2.X + r2.Width / 2, r2.Y + r2.Height / 2), S(17f), flag ? Ui.Text : Ui.TextMute);
		num5 += S(30f) + S(12f);
		string text = ((Control)_boxes["cp_o"]).Text;
		string text2 = ((Control)_boxes["cp_n"]).Text;
		string text3 = ((Control)_boxes["cp_c"]).Text;
		num5 = Field(g, num3, num5, num4, "cp_o", L.T("รห\u0e31สผ\u0e48านป\u0e31จจ\u0e38บ\u0e31น", "Current password"), "lock", L.T("รห\u0e31สท\u0e35\u0e48ใช\u0e49อย\u0e39\u0e48", "Your current password"), _authBusy) + S(12f);
		num5 = Field(g, num3, num5, num4, "cp_n", L.T("รห\u0e31สผ\u0e48านใหม\u0e48", "New password"), "key", L.T("อย\u0e48างน\u0e49อย 8 ต\u0e31ว", "At least 8 characters"), _authBusy);
		bool? item = ((text2.Length > 0 && text.Length > 0) ? new bool?(text2 != text) : ((bool?)null));
		num5 = Rules(g, num3, num5 + S(6f), num4, ("len", PwLen(text2)), ("nou", PwNoUser(text2, session.Username)), ("diff", item)) + S(12f);
		num5 = Field(g, num3, num5, num4, "cp_c", L.T("ย\u0e37นย\u0e31นรห\u0e31สผ\u0e48านใหม\u0e48", "Confirm new password"), "key", L.T("พ\u0e34มพ\u0e4cรห\u0e31สอ\u0e35กคร\u0e31\u0e49ง", "Type it again"), _authBusy);
		num5 = Rules(g, num3, num5 + S(6f), num4, ("match", PwMatch(text2, text3))) + S(16f);
		bool flag2 = ChpwValid(text, text2, text3, session.Username);
		Font f = F(15f, medium: true);
		string text4 = (_authBusy ? L.T("กำล\u0e31งบ\u0e31นท\u0e36ก…", "Saving…") : L.T("บ\u0e31นท\u0e36กรห\u0e31สใหม\u0e48", "Save new password"));
		int num6 = S(18f) + S(18f) + S(8f) + Ui.Measure(text4, f).Width + S(18f);
		Rectangle r3 = new Rectangle(num3 + num4 - num6, num5, num6, S(44f));
		Button(g, r3, "chpw-save", _authBusy ? "loader-2" : "check", text4, primary: true, flag2 && !_authBusy, delegate
		{
			SubmitChangePasswordAsync();
		}, 18f, 15f, _authBusy);
		string s = L.T("ยกเล\u0e34ก", "Cancel");
		int num7 = S(18f) + Ui.Measure(s, F(14.5f)).Width + S(18f);
		Rectangle r4 = new Rectangle(r3.X - S(10f) - num7, num5, num7, S(44f));
		bool flag3 = AddHit(r4, "chpw-cancel", CloseChpw);
		if (flag3)
		{
			Ui.Fill(g, r4, S(9f), Ui.K2);
		}
		Ui.Stroke(g, r4, S(9f), Ui.K3);
		Ui.Draw(g, s, F(14.5f), r4, flag3 ? Ui.Text : Ui.TextDim, (TextFormatFlags)5);
	}

	private void DrawToast(Graphics g)
	{
		(string, string, Color)? toast = _toast;
		if (toast.HasValue)
		{
			(string, string, Color) valueOrDefault = toast.GetValueOrDefault();
			if (DateTime.UtcNow > _toastUntil)
			{
				_toast = null;
				return;
			}
			Font f = F(13.5f);
			int width = Ui.Measure(valueOrDefault.Item1, f).Width;
			int num = S(14f) + S(16f) + S(8f) + width + S(14f);
			Rectangle r = new Rectangle((((Form)this).ClientSize.Width - num) / 2, ((Form)this).ClientSize.Height - S(100f) - S(38f), num, S(38f));
			Ui.Card(g, r, S(10f), Ui.K2, Ui.K3);
			Icons.Draw(g, valueOrDefault.Item2, new Point(r.X + S(14f) + S(8f), r.Y + r.Height / 2), S(16f), valueOrDefault.Item3);
			Ui.Draw(g, valueOrDefault.Item1, f, new Rectangle(r.X + S(14f) + S(16f) + S(8f), r.Y, width + S(4f), r.Height), Ui.Text, (TextFormatFlags)4);
		}
	}

	private int Title(Graphics g, int x, int y, int w, string text)
	{
		Ui.Draw(g, text, F(22f, medium: true), new Rectangle(x, y, w, S(30f)), Ui.Text, (TextFormatFlags)32772);
		return y + S(30f);
	}

	private int Sub(Graphics g, int x, int y, int w, string text)
	{
		return WrapText(g, text, F(13.5f), x, y + S(4f), w, Ui.TextDim) + S(16f);
	}

	private int WrapText(Graphics g, string s, Font f, int x, int y, int w, Color c, bool center = false)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		StringFormat val = (center ? CenterWrap : LeftWrap);
		int num = (int)Math.Ceiling(g.MeasureString(s, f, w, val).Height);
		SolidBrush val2 = new SolidBrush(c);
		try
		{
			TextRenderingHint textRenderingHint = g.TextRenderingHint;
			g.TextRenderingHint = (TextRenderingHint)3;
			g.DrawString(s, f, (Brush)(object)val2, new RectangleF(x, y, w, num + 2), val);
			g.TextRenderingHint = textRenderingHint;
			return y + num;
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private int Back(Graphics g, int x, int y)
	{
		string s = L.T("กล\u0e31บไปหน\u0e49าเข\u0e49าส\u0e39\u0e48ระบบ", "Back to sign in");
		Font f = F(13f);
		int width = Ui.Measure(s, f).Width;
		Rectangle r = new Rectangle(x, y - S(2f), S(16f) + S(6f) + width + S(2f), S(22f));
		Color color = (AddHit(r, "back", delegate
		{
			GoAuth(Auth.Login);
		}) ? Ui.Text : Ui.TextDim);
		Icons.Draw(g, "arrow-left", new Point(r.X + S(8f), r.Y + r.Height / 2), S(16f), color);
		Ui.Draw(g, s, f, new Rectangle(r.X + S(22f), r.Y, width + S(2f), r.Height), color, (TextFormatFlags)4);
		return r.Bottom + S(14f);
	}

	private int Steps(Graphics g, int x, int y, int w, int n)
	{
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Expected O, but got Unknown
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Expected O, but got Unknown
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Expected O, but got Unknown
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Expected O, but got Unknown
		//IL_0357: Unknown result type (might be due to invalid IL or missing references)
		//IL_035e: Expected O, but got Unknown
		Font f = F(12.5f);
		Font f2 = F(12f, medium: true);
		Auth scr = _scr;
		bool flag = (uint)(scr - 7) <= 1u;
		string s = ((flag || (_scr == Auth.Su2 && _suVia == "email")) ? L.T("ย\u0e37นย\u0e31นอ\u0e35เมล", "Verify email") : ((_scr == Auth.Su1 && _features.EmailSignup) ? L.T("ย\u0e37นย\u0e31นต\u0e31วตน", "Verify") : L.T("ย\u0e37นย\u0e31น Discord", "Verify Discord")));
		string s2 = L.T("ต\u0e31\u0e49งไอด\u0e35และรห\u0e31ส", "Set ID & password");
		int width = Ui.Measure(s, f).Width;
		int width2 = Ui.Measure(s2, f).Width;
		Rectangle rectangle = new Rectangle(x, y, S(22f), S(22f));
		if (n > 1)
		{
			SolidBrush val = new SolidBrush(Ui.PurpleDark);
			try
			{
				g.FillEllipse((Brush)(object)val, rectangle);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			Icons.Draw(g, "check", new Point(rectangle.X + S(11f), rectangle.Y + S(11f)), S(13f), Ui.Yellow);
		}
		else
		{
			SolidBrush val2 = new SolidBrush(Ui.Yellow);
			try
			{
				g.FillEllipse((Brush)(object)val2, rectangle);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			Ui.Draw(g, "1", f2, rectangle, Ui.OnYellow, (TextFormatFlags)5);
		}
		Ui.Draw(g, s, f, new Rectangle(rectangle.Right + S(7f), y, width + S(2f), S(22f)), (n > 1) ? Ui.TextDim : Ui.Text, (TextFormatFlags)4);
		int x2 = x + w - width2 - S(7f) - S(22f);
		Rectangle rectangle2 = new Rectangle(x2, y, S(22f), S(22f));
		if (n == 2)
		{
			SolidBrush val3 = new SolidBrush(Ui.Yellow);
			try
			{
				g.FillEllipse((Brush)(object)val3, rectangle2);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			Ui.Draw(g, "2", f2, rectangle2, Ui.OnYellow, (TextFormatFlags)5);
		}
		else
		{
			Pen val4 = new Pen(K4);
			try
			{
				g.DrawEllipse(val4, rectangle2.X, rectangle2.Y, rectangle2.Width - 1, rectangle2.Height - 1);
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			Ui.Draw(g, "2", F(12f), rectangle2, Ui.TextMute, (TextFormatFlags)5);
		}
		Ui.Draw(g, s2, f, new Rectangle(rectangle2.Right + S(7f), y, width2 + S(2f), S(22f)), (n == 2) ? Ui.Text : Ui.TextMute, (TextFormatFlags)4);
		int num = rectangle.Right + S(7f) + width + S(8f);
		int num2 = rectangle2.X - S(8f);
		if (num2 > num)
		{
			Pen val5 = new Pen(Ui.K3);
			try
			{
				g.DrawLine(val5, num, y + S(11f), num2, y + S(11f));
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
		}
		return y + S(22f) + S(16f);
	}

	private int Banner(Graphics g, int x, int y, int w)
	{
		BannerInfo banner = _banner;
		if ((object)banner == null)
		{
			return y;
		}
		switch (banner.Kind)
		{
		case "wrong":
			return BannerBox(g, x, y, w, "bad", "alert-circle", L.T("ช\u0e37\u0e48อผ\u0e39\u0e49ใช\u0e49หร\u0e37อรห\u0e31สผ\u0e48านไม\u0e48ถ\u0e39กต\u0e49อง", "Incorrect username or password"), (banner.N >= 0) ? L.T($"ผ\u0e34ดได\u0e49อ\u0e35ก {banner.N} คร\u0e31\u0e49ง ก\u0e48อนถ\u0e39กพ\u0e31ก 10 นาท\u0e35", $"{banner.N} more {((banner.N == 1) ? "try" : "tries")} before a 10-minute lock") : null);
		case "locked":
			if (!Locked)
			{
				_banner = null;
				return y;
			}
			if (_features.DiscordLogin && _scr == Auth.Login)
			{
				return BannerBox(g, x, y, w, "bad", "lock", L.T("รห\u0e31สผ\u0e34ดครบ 5 คร\u0e31\u0e49ง — รอ ", "5 wrong passwords — wait ") + Mmss(_lockUntil - DateTime.UtcNow), L.T("ไอด\u0e35ท\u0e35\u0e48ผ\u0e39ก Discord ไว\u0e49เข\u0e49าด\u0e49วย Discord ได\u0e49เลย", "IDs linked to Discord can sign in with Discord now"));
			}
			return BannerBox(g, x, y, w, "bad", "lock", L.T("ใส\u0e48รห\u0e31สผ\u0e34ด 5 คร\u0e31\u0e49ง — พ\u0e31ก 10 นาท\u0e35", "5 wrong attempts — locked for 10 minutes"), L.T("ลองใหม\u0e48ได\u0e49ใน ", "Try again in ") + Mmss(_lockUntil - DateTime.UtcNow) + L.T(" · หร\u0e37อกด «ล\u0e37มรห\u0e31สผ\u0e48าน?»", " · or use “Forgot password?”"));
		case "logout":
			return BannerBox(g, x, y, w, "info", "logout", null, L.T("ออกจากระบบแล\u0e49ว", "You have signed out"));
		case "relogin":
			return BannerBox(g, x, y, w, "info", "info-circle", null, L.T("เซสช\u0e31นหมดอาย\u0e38 — เข\u0e49าส\u0e39\u0e48ระบบอ\u0e35กคร\u0e31\u0e49ง", "Your session expired — please sign in again"));
		case "reset-ok":
			return BannerBox(g, x, y, w, "good", "circle-check", L.T("ต\u0e31\u0e49งรห\u0e31สผ\u0e48านใหม\u0e48แล\u0e49ว", "New password saved"), L.T("เข\u0e49าส\u0e39\u0e48ระบบด\u0e49วยรห\u0e31สใหม\u0e48ได\u0e49เลย", "Sign in with your new password"));
		case "deny":
			return BannerBox(g, x, y, w, "info", "info-circle", null, L.T("ยกเล\u0e34กการเช\u0e37\u0e48อม Discord แล\u0e49ว — กดใหม\u0e48ได\u0e49ท\u0e38กเม\u0e37\u0e48อ", "Discord sign-in was cancelled — try again any time"));
		case "timeout":
			return BannerBox(g, x, y, w, "bad", "clock-hour-4", null, L.T("หมดเวลารอ Discord — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Timed out waiting for Discord — please try again"));
		case "em-taken":
			return BannerBox(g, x, y, w, "bad", "alert-circle", L.T("อ\u0e35เมลน\u0e35\u0e49ม\u0e35ไอด\u0e35แล\u0e49ว", "This email already has an ID"), L.T("1 อ\u0e35เมลสม\u0e31ครได\u0e49 1 ไอด\u0e35 — เข\u0e49าส\u0e39\u0e48ระบบ หร\u0e37อต\u0e31\u0e49งรห\u0e31สใหม\u0e48ทางอ\u0e35เมล", "One email can have only one ID — sign in, or reset the password by email"), L.T("ล\u0e37มรห\u0e31สผ\u0e48าน?", "Forgot password?"), delegate
			{
				StartEmail("reset");
			});
		case "code-expired":
			return BannerBox(g, x, y, w, "bad", "clock-hour-4", L.T("รห\u0e31สหมดอาย\u0e38หร\u0e37อใช\u0e49ไม\u0e48ได\u0e49แล\u0e49ว", "This code has expired or can no longer be used"), L.T("กด «ส\u0e48งรห\u0e31สอ\u0e35กคร\u0e31\u0e49ง» เพ\u0e37\u0e48อร\u0e31บรห\u0e31สใหม\u0e48", "Press “Send a new code” to get a new one"));
		case "em-resent":
			return BannerBox(g, x, y, w, "good", "send", L.T("ส\u0e48งรห\u0e31สใหม\u0e48แล\u0e49ว", "New code sent"), L.T("รห\u0e31สก\u0e48อนหน\u0e49าใช\u0e49ไม\u0e48ได\u0e49แล\u0e49ว", "The previous code no longer works"));
		case "em-timeout":
			return BannerBox(g, x, y, w, "bad", "clock-hour-4", null, L.T("หมดเวลา — ย\u0e37นย\u0e31นอ\u0e35เมลใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Timed out — verify your email again"));
		case "noid":
			return BannerBox(g, x, y, w, "bad", "alert-circle", L.T("Discord บ\u0e31ญช\u0e35น\u0e35\u0e49ย\u0e31งไม\u0e48ได\u0e49ผ\u0e39กก\u0e31บไอด\u0e35ไหน", "This Discord account has no Durango ID"), L.T("สม\u0e31ครสมาช\u0e34กด\u0e49วย Discord บ\u0e31ญช\u0e35น\u0e35\u0e49ได\u0e49เลย", "You can sign up with it now"), L.T("สม\u0e31ครสมาช\u0e34ก", "Sign up"), delegate
			{
				GoAuth(Auth.Su1);
			});
		default:
			return BannerBox(g, x, y, w, "bad", "alert-circle", null, banner.Msg ?? L.T("เก\u0e34ดข\u0e49อผ\u0e34ดพลาด — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Something went wrong — please try again"));
		}
	}

	private int BannerBox(Graphics g, int x, int y, int w, string kind, string icon, string? bold, string? body, string? linkText = null, Action? linkClick = null)
	{
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_0318: Expected O, but got Unknown
		Color fill;
		Color color;
		Color border;
		Color color2;
		switch (kind)
		{
		case "good":
			fill = Color.FromArgb(22, 51, 35);
			color = Ui.Good;
			border = Ui.A(Ui.Good, 77);
			color2 = Color.FromArgb(214, 247, 226);
			break;
		case "info":
			fill = Color.FromArgb(42, 32, 80);
			color = Ui.PurpleLight;
			border = Ui.A(Ui.PurpleLight, 61);
			color2 = Ui.Text;
			break;
		case "warn":
			fill = Color.FromArgb(58, 48, 16);
			color = Ui.Yellow;
			border = Ui.A(Ui.Yellow, 77);
			color2 = Color.FromArgb(255, 240, 191);
			break;
		default:
			fill = Color.FromArgb(58, 26, 23);
			color = Ui.Bad;
			border = Ui.A(Ui.Bad, 82);
			color2 = Color.FromArgb(255, 217, 210);
			break;
		}
		Font val = F(13f);
		Font val2 = F(13f, medium: true);
		int num = x + S(12f) + S(17f) + S(10f);
		int num2 = x + w - S(12f) - num;
		int num3 = S(9f);
		int num4 = ((bold != null) ? ((int)Math.Ceiling(g.MeasureString(bold, val2, num2, LeftWrap).Height)) : 0);
		int num5 = ((body != null) ? ((int)Math.Ceiling(g.MeasureString(body, val, num2, LeftWrap).Height)) : 0);
		int num6 = ((linkText != null) ? S(20f) : 0);
		num3 += num4 + num5 + num6 + S(9f);
		Rectangle r = new Rectangle(x, y, w, num3);
		Ui.Card(g, r, S(9f), fill, border);
		Icons.Draw(g, icon, new Point(x + S(12f) + S(8f), y + S(9f) + S(10f)), S(17f), color);
		int y2 = y + S(9f);
		if (bold != null)
		{
			y2 = WrapText(g, bold, val2, num, y2, num2, color2);
		}
		if (body != null)
		{
			y2 = WrapText(g, body, val, num, y2, num2, color);
		}
		if (linkText != null && linkClick != null)
		{
			int width = Ui.Measure(linkText, val2).Width + S(2f);
			Rectangle r2 = new Rectangle(num, y2, width, S(20f));
			bool flag = AddHit(r2, "banner-link", linkClick);
			Ui.Draw(g, linkText, val2, r2, flag ? color2 : Ui.Text, (TextFormatFlags)4);
			Pen val3 = new Pen(flag ? color2 : Ui.Text);
			try
			{
				g.DrawLine(val3, r2.X, r2.Bottom - S(2f), r2.Right - S(2f), r2.Bottom - S(2f));
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		return r.Bottom + S(14f);
	}

	private int Field(Graphics g, int x, int y, int w, string name, string label, string icon, string placeholder, bool disabled, int boxH = 44)
	{
		TextBox tb = _boxes[name];
		Ui.Draw(g, label, F(13f), new Rectangle(x, y, w, S(19f)), Ui.TextDim, (TextFormatFlags)4);
		y += S(19f) + S(6f);
		Rectangle rectangle = new Rectangle(x, y, w, S(boxH));
		bool flag = ((Control)tb).Focused && !disabled;
		bool flag2 = _fieldErr.ContainsKey(name) || _red.Contains(name);
		if (!flag2)
		{
			bool flag3 = name == "su_u";
			if (flag3)
			{
				string text = UserCheck(((Control)tb).Text.Trim());
				bool flag4 = ((text == "ok" || text == "n") ? true : false);
				flag3 = !flag4;
			}
			flag2 = flag3;
		}
		bool flag5 = flag2;
		bool flag6 = name == "su_u" && UserCheck(((Control)tb).Text.Trim()) == "ok" && !flag5;
		AddHit(rectangle, "f-" + name, delegate
		{
			((Control)tb).Focus();
		}, !disabled);
		if (flag)
		{
			Ui.Fill(g, Rectangle.Inflate(rectangle, S(3f), S(3f)), S(12f), Ui.A(flag5 ? Ui.Bad : Ui.PurpleLight, flag5 ? 38 : 36));
		}
		Ui.Fill(g, rectangle, S(9f), Ui.K0);
		Ui.Stroke(g, rectangle, S(9f), flag5 ? Ui.Bad : (flag ? Ui.PurpleLight : (flag6 ? Ui.A(Ui.Good, 153) : Ui.K3)));
		Icons.Draw(g, icon, new Point(rectangle.X + S(12f) + S(8f), rectangle.Y + rectangle.Height / 2), S(17f), disabled ? Ui.A(Ui.TextMute, 128) : Ui.TextMute);
		int num = rectangle.X + S(12f) + S(17f) + S(10f);
		int num2 = rectangle.Right - S(8f);
		if ((bool)((Control)tb).Tag)
		{
			Rectangle r = new Rectangle(rectangle.Right - S(6f) - S(32f), rectangle.Y + (rectangle.Height - S(32f)) / 2, S(32f), S(32f));
			bool flag7 = AddHit(r, "eye-" + name, delegate
			{
				tb.UseSystemPasswordChar = !tb.UseSystemPasswordChar;
				((Control)this).Invalidate();
			}, !disabled);
			if (flag7)
			{
				Ui.Fill(g, r, S(7f), Ui.K2);
			}
			Icons.Draw(g, tb.UseSystemPasswordChar ? "eye" : "eye-off", new Point(r.X + r.Width / 2, r.Y + r.Height / 2), S(17f), flag7 ? Ui.Text : Ui.TextMute);
			num2 = r.X - S(4f);
		}
		PlaceBox(name, tb, new Rectangle(num, rectangle.Y, num2 - num, rectangle.Height), placeholder, disabled);
		y = rectangle.Bottom;
		if (_fieldErr.TryGetValue(name, out (string, string) value))
		{
			y += S(5f);
			string s = L.T(value.Item1, value.Item2);
			Icons.Draw(g, "alert-circle", new Point(x + S(7f), y + S(8f)), S(13f), Ui.Bad);
			y = WrapText(g, s, F(12f), x + S(18f), y, w - S(18f), Ui.Bad);
		}
		return y;
	}

	private void PlaceBox(string name, TextBox tb, Rectangle area, string placeholder, bool disabled)
	{
		if (!_measuring)
		{
			Font val = F(15f);
			if (((Control)tb).Font != val)
			{
				((Control)tb).Font = val;
			}
			Rectangle rectangle = new Rectangle(area.X, area.Y + (area.Height - ((Control)tb).Height) / 2, Math.Max(S(20f), area.Width), ((Control)tb).Height);
			if (((Control)tb).Bounds != rectangle)
			{
				((Control)tb).Bounds = rectangle;
			}
			if (tb.PlaceholderText != placeholder)
			{
				tb.PlaceholderText = placeholder;
			}
			if (((TextBoxBase)tb).ReadOnly != disabled)
			{
				((TextBoxBase)tb).ReadOnly = disabled;
				((Control)tb).ForeColor = (disabled ? Ui.TextMute : Ui.Text);
				((Control)tb).BackColor = Ui.K0;
			}
			((Control)tb).TabStop = !disabled;
			if (((Control)tb).TabIndex != _tabOrder)
			{
				((Control)tb).TabIndex = _tabOrder;
			}
			_tabOrder++;
			if (!((Control)tb).Visible)
			{
				((Control)tb).Visible = true;
			}
			_boxesUsed.Add(name);
		}
	}

	private void BeginBoxes()
	{
		_boxesUsed.Clear();
		_tabOrder = 0;
	}

	private void EndBoxes()
	{
		foreach (var (item, val2) in _boxes)
		{
			if (!_boxesUsed.Contains(item) && ((Control)val2).Visible)
			{
				if (((Control)val2).Focused)
				{
					((ContainerControl)this).ActiveControl = null;
				}
				((Control)val2).Visible = false;
			}
		}
		if (_focusNext == null || !_boxes.TryGetValue(_focusNext, out TextBox want) || !_boxesUsed.Contains(_focusNext))
		{
			return;
		}
		_focusNext = null;
		if (((TextBoxBase)want).ReadOnly)
		{
			return;
		}
		((Control)this).BeginInvoke((Action)delegate
		{
			if (((Control)want).Visible)
			{
				((Control)want).Focus();
				((TextBoxBase)want).SelectionStart = ((TextBoxBase)want).TextLength;
			}
		});
	}

	private int Hint(Graphics g, int x, int y, int w, string icon, string text, Color c)
	{
		Icons.Draw(g, icon, new Point(x + S(7f), y + S(9f)), S(13f), c);
		return WrapText(g, text, F(12f), x + S(19f), y, w - S(19f), c);
	}

	private int HintCenter(Graphics g, int x, int y, int w, string icon, string text, Color c)
	{
		Font f = F(12f);
		int width = Ui.Measure(text, f).Width;
		if (width + S(20f) > w)
		{
			return Hint(g, x, y, w, icon, text, c);
		}
		int num = x + (w - width - S(20f)) / 2;
		Icons.Draw(g, icon, new Point(num + S(7f), y + S(9f)), S(14f), c);
		Ui.Draw(g, text, f, new Rectangle(num + S(20f), y, width + S(2f), S(18f)), c, (TextFormatFlags)4);
		return y + S(18f);
	}

	private int Rules(Graphics g, int x, int y, int w, params (string Key, bool? Ok)[] rules)
	{
		Font f = F(12f);
		int num = x;
		int num2 = S(18f);
		for (int i = 0; i < rules.Length; i++)
		{
			(string Key, bool? Ok) tuple = rules[i];
			string item = tuple.Key;
			bool? item2 = tuple.Ok;
			string s = item switch
			{
				"len" => L.T("อย\u0e48างน\u0e49อย 8 ต\u0e31ว", "8+ characters"), 
				"nou" => L.T("ไม\u0e48ม\u0e35ช\u0e37\u0e48อผ\u0e39\u0e49ใช\u0e49อย\u0e39\u0e48ในรห\u0e31ส", "No username inside"), 
				"match" => L.T("รห\u0e31สตรงก\u0e31น", "Passwords match"), 
				_ => L.T("ไม\u0e48ซ\u0e49ำรห\u0e31สเด\u0e34ม", "Not the current one"), 
			};
			int num3 = S(12f) + S(5f) + Ui.Measure(s, f).Width;
			if (num > x && num + num3 > x + w)
			{
				num = x;
				y += num2 + S(3f);
			}
			Color color = ((!item2.HasValue) ? Ui.TextMute : (item2.Value ? Ui.Good : Ui.Bad));
			Point point = new Point(num + S(6f), y + num2 / 2);
			if (!item2.HasValue)
			{
				Ui.Dot(g, point, S(6f), color);
			}
			else
			{
				Icons.Draw(g, item2.Value ? "check" : "x", point, S(12f), color);
			}
			Ui.Draw(g, s, f, new Rectangle(num + S(17f), y, num3, num2), color, (TextFormatFlags)4);
			num += num3 + S(14f);
		}
		return y + num2;
	}

	private void Button(Graphics g, Rectangle r, string id, string icon, string text, bool primary, bool enabled, Action? click, float iconPx = 19f, float fontPx = 16f, bool spin = false, Color? iconColor = null)
	{
		bool flag = AddHit(r, id, click, enabled && click != null);
		Color color;
		if (primary)
		{
			if (enabled || spin)
			{
				Ui.Fill(g, r, S(10f), flag ? Ui.YellowHover : Ui.Yellow);
			}
			else
			{
				Ui.Fill(g, r, S(10f), Ui.K2);
				Ui.Stroke(g, r, S(10f), Ui.K3);
			}
			color = ((enabled || spin) ? Ui.OnYellow : Ui.TextMute);
		}
		else
		{
			if (flag)
			{
				Ui.Fill(g, r, S(9f), Ui.K2);
			}
			Ui.Stroke(g, r, S(9f), Ui.K3);
			color = ((!enabled) ? Ui.A(Ui.TextMute, 140) : (flag ? Ui.Text : Ui.TextDim));
		}
		Font f = F(fontPx, primary);
		int width = Ui.Measure(text, f).Width;
		int num = S(iconPx);
		int num2 = S(9f);
		int num3 = r.X + (r.Width - (num + num2 + width)) / 2;
		RectangleF box = new RectangleF(num3, (float)r.Y + (float)(r.Height - num) / 2f, num, num);
		Color valueOrDefault = default(Color);
		int num4;
		if (iconColor.HasValue)
		{
			valueOrDefault = iconColor.GetValueOrDefault();
			num4 = 1;
		}
		else
		{
			num4 = 0;
		}
		Icons.Draw(g, icon, box, (((uint)num4 & (enabled ? 1u : 0u)) != 0) ? valueOrDefault : color, 2f, spin ? (_animT * 12) : 0);
		Ui.Draw(g, text, f, new Rectangle(num3 + num + num2, r.Y, width + S(4f), r.Height), color, (TextFormatFlags)4);
	}

	private void LinkAt(Graphics g, Rectangle r, string id, string text, Font f, Action click, Color color)
	{
		bool flag = AddHit(r, id, click);
		Ui.Draw(g, text, f, r, flag ? Ui.Text : color, (TextFormatFlags)4);
	}

	private void AltRow(Graphics g, int x, int y, int w, string lead, string link, string id, Action click)
	{
		Font f = F(13.5f);
		Font f2 = F(13.5f, medium: true);
		int width = Ui.Measure(lead + " ", f).Width;
		int num = Ui.Measure(link, f2).Width + S(2f);
		int num2 = x + (w - width - num) / 2;
		Ui.Draw(g, lead + " ", f, new Rectangle(num2, y, width + S(2f), S(22f)), Ui.TextDim, (TextFormatFlags)4);
		LinkAt(g, new Rectangle(num2 + width, y, num, S(22f)), id, link, f2, click, Ui.PurpleLight);
	}

	private int DChip(Graphics g, int x, int y, int w, string name, string avatar, Func<Graphics, Rectangle, int> right)
	{
		Rectangle r = new Rectangle(x, y, w, S(54f));
		Ui.Card(g, r, S(10f), Ui.K2, Ui.K3);
		DrawAvatar(g, new Rectangle(r.X + S(12f), r.Y + S(8f), S(38f), S(38f)), name, avatar, Ui.K2);
		Rectangle arg = Rectangle.FromLTRB(r.X + S(12f) + S(38f) + S(11f), r.Y, r.Right - S(12f), r.Bottom);
		int num = right(g, arg);
		Rectangle r2 = Rectangle.FromLTRB(arg.X, r.Y + S(9f), arg.Right - num - S(8f), r.Y + S(9f) + S(19f));
		Ui.Draw(g, name, F(14f, medium: true), r2, Ui.Text, (TextFormatFlags)32772);
		Ui.Draw(g, "Discord", F(12f), new Rectangle(r2.X, r2.Bottom, r2.Width, S(17f)), Ui.TextMute, (TextFormatFlags)4);
		return r.Bottom + S(14f);
	}

	private int EChip(Graphics g, int x, int y, int w, string email, Func<Graphics, Rectangle, int> right)
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected O, but got Unknown
		Rectangle r = new Rectangle(x, y, w, S(54f));
		Ui.Card(g, r, S(10f), Ui.K2, Ui.K3);
		Rectangle rectangle = new Rectangle(r.X + S(12f), r.Y + S(8f), S(38f), S(38f));
		SolidBrush val = new SolidBrush(Ui.PurpleDark);
		try
		{
			g.FillEllipse((Brush)(object)val, rectangle);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Icons.Draw(g, "mail", new Point(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2), S(18f), Ui.Yellow);
		Rectangle arg = Rectangle.FromLTRB(rectangle.Right + S(11f), r.Y, r.Right - S(12f), r.Bottom);
		int num = right(g, arg);
		Rectangle r2 = Rectangle.FromLTRB(arg.X, r.Y + S(9f), arg.Right - num - S(8f), r.Y + S(9f) + S(19f));
		Ui.Draw(g, email, F(14f, medium: true), r2, Ui.Text, (TextFormatFlags)32772);
		Ui.Draw(g, L.T("อ\u0e35เมล", "Email"), F(12f), new Rectangle(r2.X, r2.Bottom, r2.Width, S(17f)), Ui.TextMute, (TextFormatFlags)4);
		return r.Bottom + S(14f);
	}

	private int IdTag(Graphics g, Rectangle r, string user)
	{
		Font f = F(12f);
		Font f2 = F(13.5f, medium: true);
		string s = L.T("ไอด\u0e35 ", "ID ");
		int width = Ui.Measure(s, f).Width;
		int width2 = Ui.Measure(user, f2).Width;
		int num = Math.Min(width + width2 + S(2f), r.Width / 2);
		int num2 = r.Right - num;
		Ui.Draw(g, s, f, new Rectangle(num2, r.Y, width + S(2f), r.Height), Ui.TextMute, (TextFormatFlags)4);
		Ui.Draw(g, user, f2, new Rectangle(num2 + width, r.Y, num - width, r.Height), Ui.Yellow, (TextFormatFlags)32772);
		return num;
	}

	private void DrawAvatar(Graphics g, Rectangle r, string name, string url, Color bg, bool badge = true)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected O, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Expected O, but got Unknown
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Expected O, but got Unknown
		Bitmap val = AvatarImg(url);
		GraphicsPath val2 = new GraphicsPath();
		try
		{
			val2.AddEllipse(r);
			if (val != null)
			{
				GraphicsState val3 = g.Save();
				g.SetClip(val2, (CombineMode)1);
				g.DrawImage((Image)(object)val, r);
				g.Restore(val3);
			}
			else
			{
				LinearGradientBrush val4 = new LinearGradientBrush(r, Color.FromArgb(138, 108, 255), Ui.PurpleDark, 45f);
				try
				{
					g.FillPath((Brush)(object)val4, val2);
					string s = ((name.Length > 0) ? char.ToUpperInvariant(name[0]).ToString() : "?");
					Ui.Draw(g, s, F((float)r.Height * 0.44f / _s, medium: true), r, Color.White, (TextFormatFlags)5);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		if (badge)
		{
			int num = (int)((float)r.Width * 0.44f);
			Rectangle rectangle = new Rectangle(r.Right - num + S(3f), r.Bottom - num + S(3f), num, num);
			SolidBrush val5 = new SolidBrush(bg);
			try
			{
				g.FillEllipse((Brush)(object)val5, Rectangle.Inflate(rectangle, S(2f), S(2f)));
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
			SolidBrush val6 = new SolidBrush(DiscordBlue);
			try
			{
				g.FillEllipse((Brush)(object)val6, rectangle);
			}
			finally
			{
				((IDisposable)val6)?.Dispose();
			}
			Icons.Draw(g, "brand-discord", new Point(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2), Math.Max(S(9f), (float)r.Width * 0.26f), Color.White);
		}
	}

	private Bitmap? AvatarImg(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return null;
		}
		if (_avatars.TryGetValue(url, out Bitmap value))
		{
			return value;
		}
		_avatars[url] = null;
		LoadAvatarAsync(url);
		return null;
	}

	private async Task LoadAvatarAsync(string url)
	{
		Bitmap val = await AccountApi.AvatarAsync(url);
		if (((Control)this).IsDisposed)
		{
			if (val != null)
			{
				((Image)val).Dispose();
			}
		}
		else
		{
			_avatars[url] = val;
			((Control)this).Invalidate();
		}
	}

	private static string Mmss(TimeSpan t, bool minutesPad = true)
	{
		int num = Math.Max(0, (int)Math.Ceiling(t.TotalSeconds));
		return (minutesPad ? (num / 60).ToString("00") : (num / 60).ToString()) + ":" + (num % 60).ToString("00");
	}

	private void CalTick()
	{
		string text = _game?.Gateway;
		if (text != null)
		{
			if (!string.Equals(text, _calGw, StringComparison.OrdinalIgnoreCase))
			{
				_calGw = text;
				_cal = new CalendarState();
				_calGen++;
				_calNext = DateTime.MinValue;
				_calListScroll = (_calDetScroll = 0);
			}
			if (!_calBusy && DateTime.UtcNow >= _calNext)
			{
				PollCalendarAsync();
			}
		}
	}

	private async Task PollCalendarAsync()
	{
		string calGw = _calGw;
		if (calGw == null)
		{
			return;
		}
		_calBusy = true;
		_calAttemptAt = DateTime.UtcNow;
		UpdateAnim();
		int gen = _calGen;
		CalendarState st = _cal;
		try
		{
			CalFetchResult calFetchResult = await EventCalendarApi.FetchAsync(calGw);
			if (!((Control)this).IsDisposed && gen == _calGen)
			{
				bool hidden = st.Hidden;
				bool known = st.Known;
				st.Apply(calFetchResult, Tick);
				if (!known || hidden != st.Hidden || calFetchResult.Kind != CalFetchKind.Ok)
				{
					Log.Write($"events/calendar: {calFetchResult.Kind}{((calFetchResult.Detail.Length > 0) ? (" · " + calFetchResult.Detail) : "")}" + ((calFetchResult.Data != null) ? $" · {calFetchResult.Data.Events.Length} อ\u0e35เวนต\u0e4c" : ""));
				}
				if (st.Hidden && _page == Page.Calendar)
				{
					ShowPage(Page.Home);
				}
				_calNext = DateTime.UtcNow.AddMilliseconds(st.NextDelayMs(_page == Page.Calendar));
				((Control)this).Invalidate();
			}
		}
		catch (Exception ex)
		{
			Log.Write("events/calendar: " + ex.Message);
			_calNext = DateTime.UtcNow.AddMilliseconds(300000.0);
		}
		finally
		{
			_calBusy = false;
			UpdateAnim();
		}
	}

	private void CalOnPage()
	{
		_cal.EnsureSelection(Tick);
		DateTime dateTime = _calAttemptAt.AddMilliseconds(_cal.NextDelayMs(pageOpen: true));
		if (dateTime < _calNext)
		{
			_calNext = dateTime;
		}
	}

	private void CalRefreshNow()
	{
		if (!_calBusy)
		{
			_calNext = DateTime.MinValue;
			((Control)this).Invalidate();
		}
	}

	private void CalSelect(int day)
	{
		_cal.Select(day);
		_calListScroll = 0;
		_calDetScroll = 0;
		((Control)this).Invalidate();
	}

	private bool CalWheel(MouseEventArgs e)
	{
		if (_page != Page.Calendar || ModalOpen || _od != null)
		{
			return false;
		}
		int num = ((e.Delta <= 0) ? 1 : (-1));
		if (_calListRect.Contains(e.Location))
		{
			_calListScroll = Math.Max(0, _calListScroll + num);
			((Control)this).Invalidate();
			return true;
		}
		if (_calDetRect.Contains(e.Location))
		{
			_calDetScroll = Math.Clamp(_calDetScroll + num * S(40f), 0, Math.Max(0, _calDetMax));
			((Control)this).Invalidate();
			return true;
		}
		return false;
	}

	private void DrawCalIcon(Graphics g, string icon, Rectangle box)
	{
		string text = CalIcons.Resource(icon);
		Bitmap value = null;
		if (text != null && !_calIcons.TryGetValue(text, out value))
		{
			value = (_calIcons[text] = Ui.LoadImage(text));
		}
		if (value == null)
		{
			Icons.Draw(g, "calendar", new Point(box.X + box.Width / 2, box.Y + box.Height / 2), (float)box.Width * 0.8f, Ui.PurpleLight);
			return;
		}
		float num = Math.Min((float)box.Width / (float)((Image)value).Width, (float)box.Height / (float)((Image)value).Height);
		float num2 = (float)((Image)value).Width * num;
		float num3 = (float)((Image)value).Height * num;
		g.DrawImage((Image)(object)value, new RectangleF((float)box.X + ((float)box.Width - num2) / 2f, (float)box.Y + ((float)box.Height - num3) / 2f, num2, num3));
	}

	private int CalHeader(Graphics g, Rectangle pane, string title, int actW, string sub)
	{
		int num = pane.Y + S(4f);
		int width = pane.Width - actW - S(16f);
		Ui.Draw(g, title, F(26f, medium: true), new Rectangle(pane.X, num, width, S(34f)), Ui.Text, (TextFormatFlags)4);
		Ui.Draw(g, sub, F(13f), new Rectangle(pane.X, num + S(34f), width, S(20f)), Ui.TextDim, (TextFormatFlags)32772);
		return num + S(34f) + S(20f) + S(12f);
	}

	private void DrawCalendar(Graphics g, Rectangle pane)
	{
		long tick = Tick;
		CalendarState cal = _cal;
		CalendarData data = cal.Data;
		MonthGrid grid = cal.Grid;
		int num = cal.Today(tick);
		double now = cal.ServerNow(tick);
		string text = CalFmt.Tz(data?.Offset ?? CalendarData.DefaultOffset);
		string text2 = L.T("ว\u0e31นน\u0e35\u0e49", "Today") + ((num > 0 && grid != null) ? $" {num}/{grid.Month:00}" : "");
		int num2 = TradeBtnW("clock-hour-4", text2, "");
		int num3 = S(34f);
		int top = CalHeader(g, pane, L.T("ปฏ\u0e34ท\u0e34นก\u0e34จกรรม", "Event calendar"), num2 + S(8f) + num3, L.T("ข\u0e49อม\u0e39ลเด\u0e35ยวก\u0e31บหน\u0e49า Events ในเกม · เวลาเซ\u0e34ร\u0e4cฟเวอร\u0e4c " + text + " · ร\u0e35เฟรชเองท\u0e38ก 15 ว\u0e34 · ด\u0e39ได\u0e49โดยไม\u0e48ต\u0e49องล\u0e47อกอ\u0e34น", "Same data as the in-game Events page · server time " + text + " · refreshes every 15 s · no sign-in needed"));
		TradeBtn(g, new Rectangle(pane.Right - num3, pane.Y + S(22f), num3, S(34f)), "cal-refresh", _calBusy ? "loader-2" : "refresh", "", "", !_calBusy, CalRefreshNow, 13f, _calBusy);
		TradeBtn(g, new Rectangle(pane.Right - num3 - S(8f) - num2, pane.Y + S(22f), num2, S(34f)), "cal-today", "clock-hour-4", text2, "", num > 0, delegate
		{
			_cal.SelectToday(Tick);
			_calListScroll = 0;
			_calDetScroll = 0;
			((Control)this).Invalidate();
		});
		Rectangle rectangle = Rectangle.FromLTRB(pane.X, top, pane.Right, pane.Bottom);
		_calListRect = (_calDetRect = Rectangle.Empty);
		if (data == null || grid == null)
		{
			if (cal.ShowError)
			{
				int value = Math.Max(1, (int)Math.Ceiling((_calNext - DateTime.UtcNow).TotalSeconds));
				CenterPanel(g, rectangle, "calendar", L.T("ย\u0e31งโหลดปฏ\u0e34ท\u0e34นไม\u0e48ได\u0e49", "Couldn't load the calendar"), L.T($"ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49หร\u0e37อเซ\u0e34ร\u0e4cฟย\u0e31งไม\u0e48พร\u0e49อม — กดลองใหม\u0e48 หร\u0e37อรอร\u0e31นเช\u0e48อลองเองอ\u0e35กใน {value} ว\u0e34", $"The server can't be reached or isn't ready — press Retry, or the launcher tries again in {value} s"), Array.Empty<(string, string)>(), (Text: L.T("ลองใหม\u0e48", "Retry"), Icon: _calBusy ? "loader-2" : "refresh", Click: CalRefreshNow, Spin: _calBusy), L.T("ข\u0e49อม\u0e39ลก\u0e34จกรรมมาจากเซ\u0e34ร\u0e4cฟเกม · ไม\u0e48ใช\u0e48ว\u0e48าไม\u0e48ม\u0e35ก\u0e34จกรรม", "Event data comes from the game server — this does not mean there are no events"));
			}
			else
			{
				Ui.Card(g, rectangle, S(10f), Ui.K1, Ui.K3);
				Muted(g, rectangle, L.T("กำล\u0e31งโหลดปฏ\u0e34ท\u0e34น…", "Loading the calendar…"));
			}
		}
		else
		{
			cal.EnsureSelection(tick);
			int num4 = S(320f);
			int num5 = S(16f);
			Rectangle r = Rectangle.FromLTRB(rectangle.X, rectangle.Y, rectangle.Right - num4 - num5, rectangle.Bottom);
			DrawCalGrid(g, r, cal, data, grid, num, tick);
			DrawCalDay(g, Rectangle.FromLTRB(r.Right + num5, rectangle.Y, rectangle.Right, rectangle.Bottom), cal, data, grid, now);
		}
	}

	private void DrawCalGrid(Graphics g, Rectangle r, CalendarState st, CalendarData data, MonthGrid grid, int today, long tick)
	{
		Ui.Card(g, r, S(10f), CalPanel, K4);
		int num = r.X + S(12f);
		int num2 = r.Width - S(24f);
		int num3 = r.Y + S(8f);
		Ui.Draw(g, CalFmt.Month(grid), F(22f, medium: true), new Rectangle(num, num3, num2 / 2, S(40f)), Ui.Yellow, (TextFormatFlags)4);
		string s;
		Color color;
		if (st.Stale)
		{
			int value = Math.Max(1, (int)Math.Ceiling((_calNext - DateTime.UtcNow).TotalSeconds));
			s = L.T($"อ\u0e31ปเดตไม\u0e48ได\u0e49 — แสดงข\u0e49อม\u0e39ลเม\u0e37\u0e48อ {CalFmt.Hms(data.Now, data.Offset)} · ลองใหม\u0e48ใน {value} ว\u0e34", $"Update failed — showing data from {CalFmt.Hms(data.Now, data.Offset)} · retry in {value} s");
			color = Ui.Yellow;
		}
		else
		{
			s = L.T("อ\u0e31ปเดตจากเซ\u0e34ร\u0e4cฟ ", "Updated from server ") + CalFmt.Hms(data.Now, data.Offset);
			color = Ui.TextMute;
		}
		Font f = F(12f);
		int num4 = Math.Min(num2 / 2, Ui.Measure(s, f).Width + S(4f));
		Icons.Draw(g, st.Stale ? "alert-circle" : "info-circle", new Point(num + num2 - num4 - S(6f) - S(7f), num3 + S(20f)), S(14f), color);
		Ui.Draw(g, s, f, new Rectangle(num + num2 - num4, num3, num4, S(40f)), color, (TextFormatFlags)32774);
		num3 += S(40f) + S(6f);
		int num5 = S(10f);
		float num6 = ((float)num2 - 6f * (float)num5) / 7f;
		Font f2 = F(12f);
		for (int i = 0; i < 7; i++)
		{
			Ui.Draw(g, CalFmt.Weekday(i), f2, new Rectangle((int)((float)num + (float)i * (num6 + (float)num5)), num3, (int)num6, S(18f)), (i >= 5) ? Ui.PurpleLight : Ui.TextMute, (TextFormatFlags)32773);
		}
		num3 += S(18f) + S(4f);
		int num7 = S(18f);
		int num8 = r.Bottom - S(10f) - num7;
		Font f3 = F(12f);
		int num9 = num;
		(string, Color)[] array = new(string, Color)[3]
		{
			(L.T("เล\u0e37อกว\u0e31นท\u0e35\u0e48เพ\u0e37\u0e48อด\u0e39เวลาและรายละเอ\u0e35ยดอ\u0e35เวนต\u0e4c", "Pick a day to see event times and details"), Ui.TextMute),
			(L.T("ว\u0e31นน\u0e35\u0e49/ว\u0e31นท\u0e35\u0e48เล\u0e37อก = เหล\u0e37อง", "Today / selected = yellow"), Ui.Yellow),
			(L.T("แสดงเฉพาะว\u0e31นในเด\u0e37อนน\u0e35\u0e49", "Only this month is shown"), Ui.TextMute)
		};
		for (int j = 0; j < array.Length; j++)
		{
			(string, Color) tuple = array[j];
			string item = tuple.Item1;
			Color item2 = tuple.Item2;
			int num10 = Math.Min(Ui.Measure(item, f3).Width + S(2f), num + num2 - num9);
			if (num10 <= S(20f))
			{
				break;
			}
			Ui.Draw(g, item, f3, new Rectangle(num9, num8, num10, num7), item2, (TextFormatFlags)32772);
			num9 += num10 + S(14f);
		}
		int num11 = num8 - S(8f) - num3;
		int rows = grid.Rows;
		float num12 = ((float)num11 - ((float)rows - 1f) * (float)num5) / (float)rows;
		Font val = F(14f);
		Font val2 = F(14f, medium: true);
		Font f4 = F(10f);
		for (int k = 0; k < grid.Cells; k++)
		{
			int num13 = grid.DayAt(k);
			if (num13 == 0)
			{
				continue;
			}
			int num14 = k % 7;
			int num15 = k / 7;
			Rectangle r2 = Rectangle.FromLTRB((int)((float)num + (float)num14 * (num6 + (float)num5)), (int)((float)num3 + (float)num15 * (num12 + (float)num5)), (int)((float)num + (float)num14 * (num6 + (float)num5) + num6), (int)((float)num3 + (float)num15 * (num12 + (float)num5) + num12));
			bool flag = num13 == st.Sel;
			bool flag2 = num13 == today;
			int d = num13;
			bool flag3 = AddHit(r2, "cal-d" + num13, delegate
			{
				CalSelect(d);
			});
			Ui.Fill(g, r2, S(3f), flag ? CalCellSel : (flag3 ? CalCellHover : CalCell));
			Ui.Stroke(g, r2, S(3f), flag ? Ui.Yellow : (flag3 ? Ui.PurpleLight : CalCellBorder));
			int num16 = r2.Y + S(3f);
			Ui.Draw(g, num13.ToString(), (flag || flag2) ? val2 : val, new Rectangle(r2.X + S(5f), num16, r2.Width - S(10f), S(17f)), (flag || flag2) ? Ui.Yellow : Ui.Text, (TextFormatFlags)2);
			CalEvent[] array2 = st.EventsOn(num13);
			if (array2.Length == 0)
			{
				continue;
			}
			int num17 = num16 + S(17f) + S(1f);
			int num18 = Math.Min(S(19f), r2.Bottom - S(3f) - num17);
			if (num18 >= S(10f))
			{
				int num19 = Math.Min(2, array2.Length);
				int num20 = S(3f);
				int num21 = r2.X + (r2.Width - (num19 * num18 + (num19 - 1) * num20)) / 2;
				for (int num22 = 0; num22 < num19; num22++)
				{
					DrawCalIcon(g, array2[num22].Icon, new Rectangle(num21 + num22 * (num18 + num20), num17 + (S(20f) - num18) / 2, num18, num18));
				}
				int num23 = num17 + S(20f);
				if (num23 + S(12f) <= r2.Bottom - S(2f))
				{
					Ui.Draw(g, CalFmt.Count(array2.Length), f4, new Rectangle(r2.X + S(2f), num23, r2.Width - S(4f), S(13f)), flag ? Ui.Yellow : Ui.TextDim, (TextFormatFlags)32773);
				}
			}
		}
	}

	private void DrawCalDay(Graphics g, Rectangle r, CalendarState st, CalendarData data, MonthGrid grid, double now)
	{
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Expected O, but got Unknown
		TimeSpan offset = data.Offset;
		int y = r.Y;
		Font f = F(16f, medium: true);
		string s = CalFmt.Dmy(grid, st.Sel);
		int width = Ui.Measure(s, f).Width;
		Ui.Draw(g, s, f, new Rectangle(r.X, y, width + S(2f), S(28f)), Ui.Yellow, (TextFormatFlags)4);
		Ui.Draw(g, L.T("· อ\u0e35เวนต\u0e4cประจำว\u0e31น", "· events of the day"), F(13f), new Rectangle(r.X + width + S(6f), y, r.Width - width - S(6f) - S(70f), S(28f)), Ui.TextMute, (TextFormatFlags)32772);
		TradeBtn(g, new Rectangle(r.Right - S(28f), y, S(28f), S(28f)), "cal-nd", "chevron-right", "", "", st.Sel < grid.Days, delegate
		{
			CalSelect(st.Sel + 1);
		});
		TradeBtn(g, new Rectangle(r.Right - S(28f) - S(6f) - S(28f), y, S(28f), S(28f)), "cal-pd", "chevron-left", "", "", st.Sel > 1, delegate
		{
			CalSelect(st.Sel - 1);
		});
		y += S(28f) + S(8f);
		CalEvent[] array = st.EventsOn(st.Sel);
		if (array.Length == 0)
		{
			Rectangle r2 = new Rectangle(r.X, y, r.Width, S(64f));
			Pen val = new Pen(Ui.K3)
			{
				DashStyle = (DashStyle)1
			};
			try
			{
				GraphicsPath val2 = Ui.Round(new RectangleF((float)r2.X + 0.5f, (float)r2.Y + 0.5f, r2.Width - 1, r2.Height - 1), S(9f));
				try
				{
					g.DrawPath(val, val2);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			Ui.Draw(g, L.T("ไม\u0e48ม\u0e35อ\u0e35เวนต\u0e4cในว\u0e31นน\u0e35\u0e49", "No events on this day"), F(13f), r2, Ui.TextMute, (TextFormatFlags)5);
			return;
		}
		int num = S(54f);
		int num2 = S(6f);
		int num3 = Math.Min(Math.Max(1, (S(184f) + num2) / (num + num2)), array.Length);
		_calListScroll = Math.Clamp(_calListScroll, 0, Math.Max(0, array.Length - num3));
		st.SelEvent = Math.Clamp(st.SelEvent, 0, array.Length - 1);
		Rectangle rectangle = (_calListRect = new Rectangle(r.X, y, r.Width, num3 * (num + num2) - num2));
		int width2 = ((array.Length > num3) ? (r.Width - S(8f)) : r.Width);
		for (int num4 = 0; num4 < num3; num4++)
		{
			int idx = num4 + _calListScroll;
			CalEvent calEvent = array[idx];
			Rectangle r3 = new Rectangle(r.X, y + num4 * (num + num2), width2, num);
			bool flag = idx == st.SelEvent;
			bool flag2 = AddHit(r3, "cal-ev" + idx, delegate
			{
				_cal.SelEvent = idx;
				_calDetScroll = 0;
				((Control)this).Invalidate();
			});
			Ui.Card(g, r3, S(9f), Ui.K1, flag ? Ui.Yellow : (flag2 ? Ui.PurpleLight : Ui.K3));
			DrawCalIcon(g, calEvent.Icon, new Rectangle(r3.X + S(10f), r3.Y + (num - S(30f)) / 2, S(30f), S(30f)));
			string s2 = CalFmt.ListTime(calEvent, offset);
			Font f2 = F(12f);
			int num5 = Ui.Measure(s2, f2).Width + S(2f);
			Ui.Draw(g, s2, f2, new Rectangle(r3.Right - S(10f) - num5, r3.Y, num5, num), Ui.TextDim, (TextFormatFlags)6);
			int num6 = r3.X + S(50f);
			int num7 = r3.Right - S(10f) - num5 - S(10f) - num6;
			Ui.Draw(g, calEvent.Name, F(14f, medium: true), new Rectangle(num6, r3.Y + S(8f), num7, S(20f)), Ui.Text, (TextFormatFlags)32772);
			CalStatus calStatus = calEvent.StatusAt(now);
			string s3 = CalFmt.Status(calStatus);
			Font f3 = F(12f);
			int num8 = Ui.Measure(s3, f3).Width + S(2f);
			string s4 = ((calEvent.Place.Length > 0) ? (calEvent.Place + " · ") : "");
			int num9 = Math.Max(0, Math.Min(Ui.Measure(s4, f3).Width + S(1f), num7 - num8));
			if (num9 > 0)
			{
				Ui.Draw(g, s4, f3, new Rectangle(num6, r3.Y + S(29f), num9, S(17f)), Ui.TextMute, (TextFormatFlags)32772);
			}
			Ui.Draw(g, s3, f3, new Rectangle(num6 + num9, r3.Y + S(29f), Math.Max(0, num7 - num9), S(17f)), calStatus switch
			{
				CalStatus.Ended => Ui.Bad, 
				CalStatus.Live => Ui.Good, 
				_ => Ui.TextDim, 
			}, (TextFormatFlags)32772);
		}
		if (array.Length > num3)
		{
			Rectangle rectangle2 = new Rectangle(r.Right - S(4f), rectangle.Y, S(4f), rectangle.Height);
			int num10 = Math.Max(S(20f), rectangle2.Height * num3 / array.Length);
			Ui.Fill(g, new Rectangle(rectangle2.X, rectangle2.Y + (rectangle2.Height - num10) * _calListScroll / Math.Max(1, array.Length - num3), rectangle2.Width, num10), S(2f), Ui.K3);
		}
		y = rectangle.Bottom + S(8f);
		Rectangle card = Rectangle.FromLTRB(r.X, y, r.Right, r.Bottom);
		if (card.Height > S(60f))
		{
			DrawCalDetail(g, card, array[st.SelEvent], now, offset);
		}
	}

	private void DrawCalDetail(Graphics g, Rectangle card, CalEvent e, double now, TimeSpan off)
	{
		Ui.Card(g, card, S(10f), Ui.K1, Ui.K3);
		Rectangle rect = Rectangle.FromLTRB(card.X + S(14f), card.Y + S(12f), card.Right - S(14f), card.Bottom - S(12f));
		_calDetRect = card;
		GraphicsState val = g.Save();
		g.SetClip(Rectangle.Inflate(rect, S(2f), S(2f)));
		int x = rect.X;
		int width = rect.Width;
		int num = rect.Y - _calDetScroll;
		int num2 = num;
		DrawCalIcon(g, e.Icon, new Rectangle(x, num2 + S(2f), S(40f), S(40f)));
		Ui.Draw(g, e.Name, F(18f, medium: true), new Rectangle(x + S(50f), num2, width - S(50f), S(22f)), Ui.Yellow, (TextFormatFlags)32772);
		CalStatus calStatus = e.StatusAt(now);
		TagChip(g, x + S(50f), num2 + S(24f), CalFmt.StatusLine(e, now, off), calStatus switch
		{
			CalStatus.Ended => "r", 
			CalStatus.Live => "g", 
			_ => "m", 
		});
		num2 += S(48f) + S(4f);
		Font f = F(13f);
		string s = L.T("เวลา", "Time");
		string s2 = L.T("สถานท\u0e35\u0e48", "Place");
		int num3 = Math.Max(Ui.Measure(s, f).Width, Ui.Measure(s2, f).Width) + S(12f);
		Ui.Draw(g, s, f, new Rectangle(x, num2, num3, S(20f)), Ui.TextMute, (TextFormatFlags)4);
		Ui.Draw(g, CalFmt.When(e, off), f, new Rectangle(x + num3, num2, width - num3, S(20f)), Ui.Text, (TextFormatFlags)32772);
		num2 += S(22f);
		Ui.Draw(g, s2, f, new Rectangle(x, num2, num3, S(20f)), Ui.TextMute, (TextFormatFlags)4);
		Icons.Draw(g, "map-pin", new Point(x + num3 + S(7f), num2 + S(10f)), S(14f), Ui.Text);
		Ui.Draw(g, (e.Place.Length > 0) ? e.Place : "—", f, new Rectangle(x + num3 + S(20f), num2, width - num3 - S(20f), S(20f)), Ui.Text, (TextFormatFlags)32772);
		num2 += S(22f) + S(6f);
		if (e.Description.Length > 0)
		{
			int num4 = Ui.MeasureWrapped(g, e.Description, f, width);
			Ui.DrawWrapped(g, e.Description, f, new Rectangle(x, num2, width, num4 + S(2f)), Ui.TextDim);
			num2 += num4 + S(8f);
		}
		CalGuideStep[] array = e.Steps();
		if (array.Length != 0)
		{
			Ui.Draw(g, L.T("ว\u0e34ธ\u0e35เข\u0e49าร\u0e48วม", "How to join"), F(12f, medium: true), new Rectangle(x, num2, width, S(18f)), Ui.TextMute, (TextFormatFlags)4);
			num2 += S(18f) + S(6f);
			Font f2 = F(12f, medium: true);
			Font f3 = F(12f);
			int x2 = x + S(29f);
			int width2 = width - S(29f);
			for (int i = 0; i < array.Length; i++)
			{
				Rectangle r = new Rectangle(x, num2 + S(1f), S(20f), S(20f));
				Ui.Fill(g, r, S(10f), Ui.PurpleDark);
				Ui.Draw(g, (i + 1).ToString(), F(11f), r, Ui.PurpleLight, (TextFormatFlags)5);
				int num5 = Ui.MeasureWrapped(g, array[i].Title, f2, width2);
				Ui.DrawWrapped(g, array[i].Title, f2, new Rectangle(x2, num2, width2, num5 + S(2f)), Ui.Text);
				int num6 = num2 + num5;
				if (array[i].Lines.Length != 0)
				{
					string s3 = string.Join("\n", array[i].Lines);
					int num7 = Ui.MeasureWrapped(g, s3, f3, width2);
					Ui.DrawWrapped(g, s3, f3, new Rectangle(x2, num6, width2, num7 + S(2f)), Ui.TextDim);
					num6 += num7;
				}
				num2 = Math.Max(num6, num2 + S(22f)) + S(8f);
			}
		}
		g.Restore(val);
		int num8 = num2 - num;
		_calDetMax = Math.Max(0, num8 - rect.Height);
		if (_calDetScroll > _calDetMax)
		{
			_calDetScroll = _calDetMax;
			((Control)this).Invalidate();
		}
		if (_calDetMax > 0)
		{
			Rectangle rectangle = new Rectangle(card.Right - S(6f), rect.Y, S(3f), rect.Height);
			int num9 = Math.Max(S(24f), rectangle.Height * rect.Height / Math.Max(1, num8));
			Ui.Fill(g, new Rectangle(rectangle.X, rectangle.Y + (rectangle.Height - num9) * _calDetScroll / Math.Max(1, _calDetMax), rectangle.Width, num9), S(2f), Ui.K3);
		}
	}

	private void CenterPanel(Graphics g, Rectangle card, string icon, string title, string body, (string Icon, string Text)[] chips, (string Text, string Icon, Action Click, bool Spin) button, string small)
	{
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Expected O, but got Unknown
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Expected O, but got Unknown
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0328: Unknown result type (might be due to invalid IL or missing references)
		Ui.Card(g, card, S(10f), Ui.K1, Ui.K3);
		Font f = F(20f, medium: true);
		Font val = F(14f);
		Font chipF = F(13f);
		Font f2 = F(12f);
		int num = Math.Min(S(460f), card.Width - S(48f));
		int num2 = Ui.MeasureWrapped(g, body, val, num);
		int[] array = chips.Select(((string Icon, string Text) c) => S(12f) + S(14f) + S(6f) + Ui.Measure(c.Text, chipF).Width + S(12f)).ToArray();
		int num3 = array.Sum() + Math.Max(0, chips.Length - 1) * S(10f);
		int num4 = TradeBtnW(button.Icon, button.Text, "y", 14f) + S(16f);
		int num5 = S(64f) + S(12f) + S(28f) + S(12f) + num2 + ((chips.Length != 0) ? (S(12f) + S(32f)) : 0) + S(14f) + S(40f) + ((small.Length > 0) ? (S(12f) + S(18f)) : 0);
		int num6 = card.X + card.Width / 2;
		int num7 = card.Y + Math.Max(S(16f), (card.Height - num5) / 2);
		Rectangle r = new Rectangle(num6 - S(32f), num7, S(64f), S(64f));
		Ui.Fill(g, r, S(32f), Ui.PurpleDark);
		Icons.Draw(g, icon, new Point(num6, num7 + S(32f)), S(30f), Ui.Yellow);
		num7 += S(64f) + S(12f);
		Ui.Draw(g, title, f, new Rectangle(card.X + S(16f), num7, card.Width - S(32f), S(28f)), Ui.Text, (TextFormatFlags)32773);
		num7 += S(28f) + S(12f);
		StringFormat val2 = new StringFormat
		{
			Alignment = (StringAlignment)1,
			Trimming = (StringTrimming)3,
			FormatFlags = (StringFormatFlags)8192
		};
		try
		{
			SolidBrush val3 = new SolidBrush(Ui.TextDim);
			try
			{
				TextRenderingHint textRenderingHint = g.TextRenderingHint;
				g.TextRenderingHint = (TextRenderingHint)3;
				g.DrawString(body, val, (Brush)(object)val3, new RectangleF((float)num6 - (float)num / 2f, num7, num, num2 + 2), val2);
				g.TextRenderingHint = textRenderingHint;
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		num7 += num2;
		if (chips.Length != 0)
		{
			num7 += S(12f);
			int num8 = num6 - num3 / 2;
			for (int num9 = 0; num9 < chips.Length; num9++)
			{
				Rectangle r2 = new Rectangle(num8, num7, array[num9], S(32f));
				Ui.Card(g, r2, S(8f), Ui.K2, Ui.K3);
				Icons.Draw(g, chips[num9].Icon, new Point(r2.X + S(12f) + S(7f), r2.Y + r2.Height / 2), S(14f), Ui.Yellow);
				Ui.Draw(g, chips[num9].Text, chipF, new Rectangle(r2.X + S(12f) + S(14f) + S(6f), r2.Y, r2.Width - S(12f) - S(14f) - S(6f), r2.Height), Ui.Text, (TextFormatFlags)4);
				num8 += array[num9] + S(10f);
			}
			num7 += S(32f);
		}
		num7 += S(14f);
		TradeBtn(g, new Rectangle(num6 - num4 / 2, num7, num4, S(40f)), "center-btn", button.Icon, button.Text, "y", !button.Spin, button.Click, 14f, button.Spin);
		num7 += S(40f);
		if (small.Length > 0)
		{
			num7 += S(12f);
			Ui.Draw(g, small, f2, new Rectangle(card.X + S(16f), num7, card.Width - S(32f), S(18f)), Ui.TextMute, (TextFormatFlags)32773);
		}
	}

	private void InitCoupon()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		TextBox val = new TextBox
		{
			BorderStyle = (BorderStyle)0,
			BackColor = Ui.K0,
			ForeColor = Ui.Text,
			Visible = false,
			Tag = false,
			MaxLength = 200,
			ShortcutsEnabled = true,
			CharacterCasing = (CharacterCasing)1
		};
		((Control)val).TextChanged += delegate
		{
			OnTradeTextChanged("coupon");
			((Control)this).Invalidate();
		};
		((Control)val).GotFocus += delegate
		{
			((Control)this).Invalidate();
		};
		((Control)val).LostFocus += delegate
		{
			((Control)this).Invalidate();
		};
		((Control)this).Controls.Add((Control)(object)val);
		_boxes["coupon"] = val;
	}

	private void ResetCoupon()
	{
		_cpGen++;
		_cp.Reset();
		_cpHist = null;
		_cpHistErr = null;
		_cpChar = null;
		_cpHistScroll = 0;
		_cpHistAgain = false;
		if (_boxes.TryGetValue("coupon", out TextBox value) && ((Control)value).Text.Length > 0)
		{
			((Control)value).Text = "";
		}
	}

	private void CouponTextChanged()
	{
		if (_cp.SetText(((Control)_boxes["coupon"]).Text))
		{
			((Control)this).Invalidate();
		}
	}

	private void CouponOnPage()
	{
		if (_chars == null)
		{
			LoadCharactersAsync();
		}
		LoadCouponHistoryAsync();
		_cpHistScroll = 0;
		_focusNext = "coupon";
	}

	private TradeCharacter? CouponReceiver(CouponCode.Kind kind)
	{
		return CouponChar.Pick(CouponChars, (kind == CouponCode.Kind.Reward) ? _cpChar : null);
	}

	private async Task SubmitCouponAsync()
	{
		if (_lt == null)
		{
			return;
		}
		CouponCode.Result p = _cp.Parsed;
		TradeCharacter who = CouponReceiver(p.Kind);
		if (!_cp.CanSubmit(DateTime.UtcNow, who != null))
		{
			return;
		}
		_cp.Begin();
		UpdateAnim();
		((Control)this).Invalidate();
		int gen = _ltGen;
		int cg = _cpGen;
		try
		{
			CouponResult couponResult = await _lt.CouponAsync(who.EntityId, p.Code);
			if (gen == _ltGen && cg == _cpGen && !((Control)this).IsDisposed)
			{
				if (couponResult.ToName.Length == 0 && !couponResult.IsCoin)
				{
					couponResult = couponResult with
					{
						ToName = who.Name
					};
				}
				_cp.Success(couponResult);
				if (couponResult.IsCoin && _chars != null)
				{
					_chars = _chars with
					{
						Coin = couponResult.Coin
					};
				}
				Log.Write(couponResult.IsCoin ? $"เต\u0e34ม Coin สำเร\u0e47จ +{couponResult.CoinAdded}" : ("ใช\u0e49ค\u0e39ปองของรางว\u0e31ลสำเร\u0e47จ → " + couponResult.ToName));
				LoadCouponHistoryAsync();
			}
		}
		catch (ApiError apiError)
		{
			if (gen != _ltGen || cg != _cpGen || ((Control)this).IsDisposed)
			{
				return;
			}
			Log.Write($"ค\u0e39ปองไม\u0e48ผ\u0e48าน: {apiError.Code} ({CouponCode.Mask(p.Code)})");
			if (!HandleTradeError(apiError, "coupon"))
			{
				_cp.Fail(apiError, DateTime.UtcNow);
				if (apiError.Code == "not_your_character")
				{
					_cpChar = null;
					LoadCharactersAsync();
				}
			}
		}
		catch (Exception ex)
		{
			Log.Write("coupon: " + ex.Message);
			_cp.Fail(new ApiError("network", L.T("ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟเวอร\u0e4cไม\u0e48ได\u0e49 — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Cannot reach the server — please try again")), DateTime.UtcNow);
		}
		finally
		{
			_cp.End();
			UpdateAnim();
			((Control)this).Invalidate();
		}
	}

	private async Task LoadCouponHistoryAsync()
	{
		if (_lt == null)
		{
			return;
		}
		if (_cpHistBusy)
		{
			_cpHistAgain = true;
			return;
		}
		_cpHistBusy = true;
		UpdateAnim();
		int gen = _ltGen;
		int cg = _cpGen;
		try
		{
			CouponHistory cpHist = await _lt.CouponHistoryAsync();
			if (gen == _ltGen && cg == _cpGen && !((Control)this).IsDisposed)
			{
				_cpHist = cpHist;
				_cpHistErr = null;
				_cpHistScroll = 0;
			}
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && cg == _cpGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "coupon"))
			{
				_cpHistErr = apiError.Message;
			}
		}
		catch (Exception ex)
		{
			Log.Write("coupon/history: " + ex.Message);
			_cpHistErr = L.T("โหลดประว\u0e31ต\u0e34ไม\u0e48ได\u0e49 — ลองเป\u0e34ดหน\u0e49าใหม\u0e48", "Could not load the history — reopen the page");
		}
		finally
		{
			_cpHistBusy = false;
			UpdateAnim();
			((Control)this).Invalidate();
			if (_cpHistAgain && cg == _cpGen)
			{
				_cpHistAgain = false;
				LoadCouponHistoryAsync();
			}
		}
	}

	private void PasteCoupon()
	{
		string text = "";
		try
		{
			if (Clipboard.ContainsText())
			{
				text = Clipboard.GetText();
			}
		}
		catch (Exception ex)
		{
			Log.Write("clipboard: " + ex.Message);
		}
		text = text.Trim();
		if (text.Length == 0)
		{
			Toast("คล\u0e34ปบอร\u0e4cดว\u0e48าง — ค\u0e31ดลอกโค\u0e49ดก\u0e48อนแล\u0e49วกด «วาง»", "The clipboard is empty — copy a code first", "info-circle", Ui.PurpleLight);
			return;
		}
		if (text.Length > 200)
		{
			text = text.Substring(0, 200);
		}
		((Control)_boxes["coupon"]).Text = text;
		_focusNext = "coupon";
		((Control)this).Invalidate();
	}

	private void DrawCoupon(Graphics g, Rectangle pane)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Expected O, but got Unknown
		int num = S(300f);
		int num2 = S(18f);
		Rectangle pane2 = Rectangle.FromLTRB(pane.X, pane.Y, pane.Right - num - num2, pane.Bottom);
		int num3 = TradeHeader(g, pane2, L.T("ใส\u0e48ค\u0e39ปอง", "Redeem a code"), 0, L.T("โค\u0e49ดเต\u0e34ม Coin และค\u0e39ปองของรางว\u0e31ล · ใช\u0e49ได\u0e49ท\u0e31\u0e49งตอนต\u0e31วละครออนไลน\u0e4cและออฟไลน\u0e4c", "Coin top-up codes and reward coupons · works whether your character is online or offline"));
		int num4 = S(20f);
		int num5 = S(16f);
		int w = pane2.Width - 2 * num4;
		int count = _hits.Count;
		_measuring = true;
		int num6;
		try
		{
			Bitmap val = new Bitmap(1, 1);
			try
			{
				Graphics val2 = Graphics.FromImage((Image)(object)val);
				try
				{
					num6 = CouponCardBody(val2, 0, 0, w);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		finally
		{
			_measuring = false;
			_hits.RemoveRange(count, _hits.Count - count);
		}
		Rectangle rectangle = new Rectangle(pane2.X, num3 - S(2f), pane2.Width, Math.Min(pane2.Bottom - (num3 - S(2f)), num6 + 2 * num5));
		Ui.Card(g, rectangle, S(12f), Ui.K1, Ui.K3);
		GraphicsState val3 = g.Save();
		g.SetClip(rectangle);
		CouponCardBody(g, rectangle.X + num4, rectangle.Y + num5, w);
		g.Restore(val3);
		DrawCouponSide(g, Rectangle.FromLTRB(pane2.Right + num2, pane.Y + S(4f), pane.Right, pane.Bottom));
	}

	private unsafe int CouponCardBody(Graphics g, int x, int y, int w)
	{
		//IL_116b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1170: Unknown result type (might be due to invalid IL or missing references)
		//IL_1179: Expected O, but got Unknown
		CouponCode.Result parsed = _cp.Parsed;
		DateTime utcNow = DateTime.UtcNow;
		TextBox tb = _boxes["coupon"];
		Ui.Draw(g, L.T("โค\u0e49ดค\u0e39ปอง", "Coupon code"), F(13f), new Rectangle(x, y, w, S(19f)), Ui.TextDim, (TextFormatFlags)4);
		y += S(19f) + S(5f);
		Rectangle rectangle = new Rectangle(x, y, w, S(44f));
		bool flag = ((Control)tb).Focused && !_cp.Busy;
		bool flag2 = parsed.Kind == CouponCode.Kind.CoinBad || (parsed.Kind == CouponCode.Kind.Invalid && parsed.Reason != "short");
		AddHit(rectangle, "f-coupon", delegate
		{
			((Control)tb).Focus();
		}, !_cp.Busy);
		if (flag)
		{
			Ui.Fill(g, Rectangle.Inflate(rectangle, S(3f), S(3f)), S(12f), Ui.A(flag2 ? Ui.Bad : Ui.PurpleLight, 36));
		}
		Ui.Fill(g, rectangle, S(10f), Ui.K0);
		Ui.Stroke(g, rectangle, S(10f), flag2 ? Ui.Bad : (flag ? Ui.PurpleLight : (parsed.CanSend ? Ui.A(Ui.Good, 153) : Ui.K3)));
		Icons.Draw(g, "ticket", new Point(rectangle.X + S(12f) + S(8f), rectangle.Y + rectangle.Height / 2), S(17f), Ui.TextMute);
		Font f = F(12f);
		string s = L.T("วาง", "Paste");
		int num = S(10f) + S(14f) + S(5f) + Ui.Measure(s, f).Width + S(10f);
		Rectangle r = new Rectangle(rectangle.Right - S(8f) - num, rectangle.Y + (rectangle.Height - S(28f)) / 2, num, S(28f));
		bool flag3 = AddHit(r, "cp-paste", PasteCoupon, !_cp.Busy);
		if (flag3)
		{
			Ui.Fill(g, r, S(7f), Ui.K2);
		}
		Ui.Stroke(g, r, S(7f), K4);
		Icons.Draw(g, "clipboard", new Point(r.X + S(10f) + S(7f), r.Y + r.Height / 2), S(14f), flag3 ? Ui.Text : Ui.TextDim);
		Ui.Draw(g, s, f, new Rectangle(r.X + S(10f) + S(14f) + S(5f), r.Y, num, r.Height), flag3 ? Ui.Text : Ui.TextDim, (TextFormatFlags)4);
		int num2 = rectangle.X + S(12f) + S(17f) + S(10f);
		PlaceBox("coupon", tb, new Rectangle(num2, rectangle.Y, r.X - S(8f) - num2, rectangle.Height), L.T("เช\u0e48น HALLOWEEN26 หร\u0e37อ DC-XXXX-XXXX", "e.g. HALLOWEEN26 or DC-XXXX-XXXX"), _cp.Busy);
		y = rectangle.Bottom + S(6f);
		Font f2 = F(12.5f);
		if (parsed.Kind == CouponCode.Kind.Empty)
		{
			Ui.Draw(g, L.T("พ\u0e34มพ\u0e4cหร\u0e37อวางโค\u0e49ด ร\u0e31นเช\u0e48อบอกชน\u0e34ดให\u0e49เอง", "Type or paste a code — the launcher tells you what it is"), f2, new Rectangle(x, y, w, S(20f)), Ui.TextMute, (TextFormatFlags)4);
		}
		else if (parsed.Kind == CouponCode.Kind.Invalid)
		{
			bool flag4 = parsed.Reason == "short";
			Icons.Draw(g, flag4 ? "info-circle" : "alert-circle", new Point(x + S(7f), y + S(10f)), S(13f), flag4 ? Ui.TextMute : Ui.Bad);
			Ui.Draw(g, CouponText.InputHint(parsed) ?? "", f2, new Rectangle(x + S(19f), y, w - S(19f), S(20f)), flag4 ? Ui.TextMute : Ui.Bad, (TextFormatFlags)32772);
		}
		else
		{
			string s2 = L.T("ชน\u0e34ดโค\u0e49ด", "Code type");
			int width = Ui.Measure(s2, f2).Width;
			Ui.Draw(g, s2, f2, new Rectangle(x, y, width + S(2f), S(20f)), Ui.TextDim, (TextFormatFlags)4);
			object obj = parsed.Kind switch
			{
				CouponCode.Kind.Reward => ("", "gift"), 
				CouponCode.Kind.Move => ("r", "key"), 
				_ => ("y", "coin"), 
			};
			string item = ((ValueTuple<string, string>*)(&obj))->Item1;
			string item2 = ((ValueTuple<string, string>*)(&obj))->Item2;
			int num3 = TagChip(g, x + width + S(6f), y, CouponText.KindLabel(parsed.Kind), item, item2);
			if (parsed.Kind == CouponCode.Kind.CoinBad)
			{
				Ui.Draw(g, CouponText.InputHint(parsed) ?? "", F(12f), Rectangle.FromLTRB(x + width + S(6f) + num3 + S(8f), y, x + w, y + S(20f)), Ui.Bad, (TextFormatFlags)32772);
			}
		}
		y += S(20f);
		TradeCharacter who = CouponReceiver(parsed.Kind);
		if (parsed.Kind == CouponCode.Kind.Reward)
		{
			y += S(12f);
			Ui.Draw(g, L.T("ส\u0e48งของให\u0e49ต\u0e31วละคร", "Send the items to"), F(13f), new Rectangle(x, y, w, S(19f)), Ui.TextDim, (TextFormatFlags)4);
			y += S(19f) + S(5f);
			Rectangle sel = new Rectangle(x, y, w, S(44f));
			IReadOnlyList<TradeCharacter> chars = CouponChars;
			bool flag5 = chars.Count > 0 && !_cp.Busy;
			bool flag6 = AddHit(sel, "cp-char", flag5 ? ((Action)delegate
			{
				TradeCharacter[] list = chars.ToArray();
				OpenDropdown(sel, list.Select((TradeCharacter c) => $"{c.Name} · Lv {c.Level}" + (c.Online ? L.T(" · ออนไลน\u0e4c", " · online") : "")).ToArray(), Array.FindIndex(list, (TradeCharacter c) => c.EntityId == who?.EntityId), delegate(int i)
				{
					_cpChar = list[i].EntityId;
					((Control)this).Invalidate();
				});
			}) : null, flag5);
			Ui.Fill(g, sel, S(10f), flag6 ? Ui.K2 : Ui.K0);
			Ui.Stroke(g, sel, S(10f), flag6 ? K4 : Ui.K3);
			Icons.Draw(g, "mail", new Point(sel.X + S(12f) + S(8f), sel.Y + sel.Height / 2), S(17f), Ui.TextMute);
			int left = sel.X + S(12f) + S(17f) + S(10f);
			int num4 = sel.Right - S(12f) - S(14f) - S(10f);
			Icons.Draw(g, "chevron-down", new Point(sel.Right - S(12f) - S(7f), sel.Y + sel.Height / 2), S(14f), flag5 ? Ui.TextDim : Ui.TextMute);
			if (who != null)
			{
				Font f3 = F(14f);
				string s3 = $"{who.Name} · Lv {who.Level}";
				if (who.Online)
				{
					num4 -= TagChip(g, num4, sel.Y + (sel.Height - S(20f)) / 2, L.T("ออนไลน\u0e4c", "Online"), "g", null, right: true) + S(8f);
				}
				Ui.Draw(g, s3, f3, Rectangle.FromLTRB(left, sel.Y, num4, sel.Bottom), Ui.Text, (TextFormatFlags)32772);
			}
			else
			{
				Ui.Draw(g, (_chars == null) ? (_itemsErr ?? L.T("กำล\u0e31งโหลดต\u0e31วละคร…", "Loading characters…")) : L.T("ไอด\u0e35น\u0e35\u0e49ย\u0e31งไม\u0e48ม\u0e35ต\u0e31วละครบนเซ\u0e34ร\u0e4cฟน\u0e35\u0e49 — เข\u0e49าเกมสร\u0e49างต\u0e31วละครก\u0e48อน", "No character on this server yet — create one in game first"), F(13f), Rectangle.FromLTRB(left, sel.Y, num4, sel.Bottom), Ui.TextMute, (TextFormatFlags)32772);
			}
			y = Hint(g, x, sel.Bottom + S(6f), w, "info-circle", L.T("ของส\u0e48งเข\u0e49ากล\u0e48องจดหมายของต\u0e31วท\u0e35\u0e48เล\u0e37อก · ค\u0e48าเร\u0e34\u0e48ม = ต\u0e31วท\u0e35\u0e48เล\u0e48นล\u0e48าส\u0e38ด", "Items go to the chosen character's mailbox · default = the one you played last"), Ui.TextMute);
		}
		else if (parsed.Kind == CouponCode.Kind.Coin)
		{
			y += S(12f);
			Ui.Draw(g, L.T("ร\u0e31บท\u0e35\u0e48", "Goes to"), F(13f), new Rectangle(x, y, w, S(19f)), Ui.TextDim, (TextFormatFlags)4);
			y += S(19f) + S(5f);
			Rectangle r2 = new Rectangle(x, y, w, S(44f));
			Ui.Fill(g, r2, S(10f), Ui.K0);
			Ui.Stroke(g, r2, S(10f), Ui.K3);
			Icons.Draw(g, "coin", new Point(r2.X + S(12f) + S(8f), r2.Y + r2.Height / 2), S(17f), Ui.Yellow);
			int num5 = r2.X + S(12f) + S(17f) + S(10f);
			Font f4 = F(14f);
			Font f5 = F(14f, medium: true);
			string s4 = L.T("Coin เข\u0e49าบ\u0e31ญช\u0e35 ", "Coin goes to account ");
			string s5 = _session?.Username ?? "";
			string s6 = L.T(" · ใช\u0e49ได\u0e49ท\u0e38กต\u0e31วละคร", " · usable by every character");
			int width2 = Ui.Measure(s4, f4).Width;
			int width3 = Ui.Measure(s5, f5).Width;
			Ui.Draw(g, s4, f4, new Rectangle(num5, r2.Y, width2 + S(2f), r2.Height), Ui.Text, (TextFormatFlags)4);
			Ui.Draw(g, s5, f5, new Rectangle(num5 + width2, r2.Y, width3 + S(2f), r2.Height), Ui.Text, (TextFormatFlags)4);
			Ui.Draw(g, s6, f4, Rectangle.FromLTRB(num5 + width2 + width3, r2.Y, r2.Right - S(10f), r2.Bottom), Ui.TextDim, (TextFormatFlags)32772);
			y = r2.Bottom;
			if (who == null)
			{
				y = Hint(g, x, y + S(6f), w, "alert-circle", (_chars == null) ? L.T("กำล\u0e31งโหลดต\u0e31วละคร…", "Loading characters…") : L.T("ต\u0e49องม\u0e35ต\u0e31วละครบนเซ\u0e34ร\u0e4cฟน\u0e35\u0e49อย\u0e48างน\u0e49อย 1 ต\u0e31ว", "You need at least one character on this server"), Ui.TextMute);
			}
		}
		y += S(14f);
		Rectangle r3 = new Rectangle(x, y, w, S(46f));
		switch (_cp.Button(utcNow, who != null))
		{
		case CouponState.Btn.Busy:
			Button(g, r3, "cp-use", "loader-2", L.T("กำล\u0e31งใช\u0e49โค\u0e49ด…", "Redeeming…"), primary: true, enabled: false, null, 19f, 16f, spin: true);
			break;
		case CouponState.Btn.Used:
			Button(g, r3, "cp-use", "check", L.T("ใช\u0e49แล\u0e49ว", "Redeemed"), primary: true, enabled: false, null);
			break;
		case CouponState.Btn.Wait:
			Button(g, r3, "cp-use", "clock-hour-4", L.T($"รอ {_cp.RetryLeft(utcNow)} ว\u0e34", $"Wait {_cp.RetryLeft(utcNow)} s"), primary: true, enabled: false, null);
			break;
		case CouponState.Btn.Use:
			Button(g, r3, "cp-use", "ticket", L.T("ใช\u0e49โค\u0e49ด", "Redeem"), primary: true, enabled: true, delegate
			{
				SubmitCouponAsync();
			});
			break;
		default:
			Button(g, r3, "cp-use", "ticket", L.T("ใช\u0e49โค\u0e49ด", "Redeem"), primary: true, enabled: false, null);
			break;
		}
		y = r3.Bottom + S(14f);
		if (_cp.ShowMove)
		{
			return BannerBox(g, x, y, w, "warn", "key", CouponText.MoveTitle, CouponText.MoveBody) - S(14f);
		}
		CouponResult ok = _cp.Ok;
		if ((object)ok != null)
		{
			y = BannerBox(g, x, y, w, "good", "circle-check", CouponText.OkTitle(ok), CouponText.OkBody(ok, who?.Name ?? ""));
			(string, string, int)[] array = CouponText.Gifts(ok);
			if (array.Length == 0)
			{
				return y - S(14f);
			}
			y -= S(4f);
			return GiftChips(g, x, y, w, array);
		}
		ApiError err = _cp.Err;
		if (err != null)
		{
			return BannerBox(g, x, y, w, "bad", "alert-circle", err.Message, CouponText.ErrHint(err, _cp.RetryLeft(utcNow))) - S(14f);
		}
		Rectangle rectangle2 = new Rectangle(x, y, w, S(80f));
		Pen val = new Pen(Ui.K3)
		{
			DashStyle = (DashStyle)1
		};
		try
		{
			GraphicsPath val2 = Ui.Round(new RectangleF((float)rectangle2.X + 0.5f, (float)rectangle2.Y + 0.5f, rectangle2.Width - 1, rectangle2.Height - 1), S(10f));
			try
			{
				g.DrawPath(val, val2);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Icons.Draw(g, "ticket", new Point(rectangle2.X + rectangle2.Width / 2, rectangle2.Y + S(16f) + S(13f)), S(26f), Ui.TextMute);
		Ui.Draw(g, L.T("ใส\u0e48โค\u0e49ดแล\u0e49วกด «ใช\u0e49โค\u0e49ด»", "Enter a code, then press “Redeem”"), F(13f), new Rectangle(rectangle2.X, rectangle2.Y + S(46f), rectangle2.Width, S(20f)), Ui.TextMute, (TextFormatFlags)5);
		return rectangle2.Bottom;
	}

	private int GiftChips(Graphics g, int x, int y, int w, (string Icon, string Name, int Count)[] gifts)
	{
		Font f = F(13f);
		Font f2 = F(13f, medium: true);
		int num = x;
		int num2 = S(28f);
		for (int i = 0; i < gifts.Length; i++)
		{
			(string Icon, string Name, int Count) tuple = gifts[i];
			string item = tuple.Icon;
			string item2 = tuple.Name;
			int item3 = tuple.Count;
			string s = "×" + TradeFmt.N(item3);
			int width = Ui.Measure(item2, f).Width;
			int width2 = Ui.Measure(s, f2).Width;
			int num3 = Math.Min(w, S(10f) + S(14f) + S(5f) + width + S(5f) + width2 + S(10f));
			if (num > x && num + num3 > x + w)
			{
				num = x;
				y += num2 + S(6f);
			}
			Rectangle r = new Rectangle(num, y, num3, num2);
			Ui.Card(g, r, S(8f), Ui.K2, Ui.K3);
			Icons.Draw(g, item, new Point(r.X + S(10f) + S(7f), r.Y + num2 / 2), S(14f), (item == "gem") ? Ui.Yellow : Ui.PurpleLight);
			int left = r.X + S(10f) + S(14f) + S(5f);
			Ui.Draw(g, item2, f, Rectangle.FromLTRB(left, r.Y, r.Right - S(10f) - width2 - S(5f), r.Bottom), Ui.Text, (TextFormatFlags)32772);
			Ui.Draw(g, s, f2, new Rectangle(r.Right - S(10f) - width2, r.Y, width2 + S(2f), num2), Ui.Yellow, (TextFormatFlags)4);
			num += num3 + S(6f);
		}
		return y + num2;
	}

	private void DrawCouponSide(Graphics g, Rectangle r)
	{
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0376: Expected O, but got Unknown
		//IL_0467: Unknown result type (might be due to invalid IL or missing references)
		//IL_046e: Expected O, but got Unknown
		Font bf = F(12.5f);
		int bw = r.Width - S(28f) - S(16f);
		string text = L.T("ซ\u0e37\u0e49อ Coin ก\u0e31บท\u0e35มงาน Dorangu ได\u0e49โค\u0e49ด DC-XXXX-XXXX", "Buy Coin from the Dorangu team to get a DC-XXXX-XXXX code");
		if (Ui.Measure(text, bf).Width > bw)
		{
			text = L.T("ซ\u0e37\u0e49อ Coin ก\u0e31บท\u0e35มงาน Dorangu\nได\u0e49โค\u0e49ด DC-XXXX-XXXX", "Buy Coin from the Dorangu team\nto get a DC-XXXX-XXXX code");
		}
		string[] array = new string[3]
		{
			L.T("ประกาศในห\u0e49อง Discord และก\u0e34จกรรมในเกม", "Announcements on Discord and in-game events"),
			text,
			L.T("ค\u0e39ปองของรางว\u0e31ลใช\u0e49ได\u0e49บ\u0e31ญช\u0e35ละคร\u0e31\u0e49ง · บางโค\u0e49ดใส\u0e48ได\u0e49เฉพาะช\u0e48วงเวลา", "Reward coupons work once per account · some only at certain hours")
		};
		int num = S(12f) + S(20f) + S(6f) + array.Sum((string t) => Ui.MeasureWrapped(g, t, bf, bw) + S(4f)) + S(10f);
		CouponHistoryItem[] array2 = _cpHist?.Items ?? Array.Empty<CouponHistoryItem>();
		int num2 = S(50f);
		int val = S(12f) + S(20f) + S(4f) + Math.Max(1, array2.Length) * num2 + S(8f);
		Rectangle r2 = (_cpHistRect = new Rectangle(r.X, r.Y, r.Width, Math.Min(val, r.Height - num - S(12f))));
		Ui.Card(g, r2, S(12f), Ui.K1, Ui.K3);
		int num3 = r2.X + S(14f);
		int num4 = r2.Width - S(28f);
		int num5 = r2.Y + S(12f);
		Icons.Draw(g, _cpHistBusy ? "loader-2" : "clock-hour-4", new RectangleF(num3, num5 + S(3f), S(14f), S(14f)), Ui.TextMute, 2f, _cpHistBusy ? (_animT * 12) : 0);
		Ui.Draw(g, L.T("ใช\u0e49ล\u0e48าส\u0e38ด (บ\u0e31ญช\u0e35น\u0e35\u0e49)", "Recently used (this account)"), F(12.5f), new Rectangle(num3 + S(20f), num5, num4 - S(20f), S(20f)), Ui.TextMute, (TextFormatFlags)4);
		num5 += S(20f) + S(4f);
		GraphicsState val2 = g.Save();
		g.SetClip(Rectangle.FromLTRB(r2.X, num5, r2.Right, r2.Bottom - S(4f)));
		if (array2.Length == 0)
		{
			string s = _cpHistErr ?? ((_cpHist == null) ? L.T("กำล\u0e31งโหลด…", "Loading…") : L.T("ย\u0e31งไม\u0e48ม\u0e35การใช\u0e49ค\u0e39ปองในบ\u0e31ญช\u0e35น\u0e35\u0e49", "No codes used on this account yet"));
			Pen val3 = new Pen(Ui.K2);
			try
			{
				g.DrawLine(val3, num3, num5, num3 + num4, num5);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			Ui.DrawWrapped(g, s, F(12.5f), new Rectangle(num3, num5 + S(8f), num4, num2 - S(8f)), Ui.TextMute);
		}
		else
		{
			int num6 = Math.Max(1, (r2.Bottom - S(8f) - num5) / num2);
			_cpHistScroll = Math.Clamp(_cpHistScroll, 0, Math.Max(0, array2.Length - num6));
			foreach (CouponHistoryItem item in array2.Skip(_cpHistScroll))
			{
				if (num5 >= r2.Bottom - S(8f))
				{
					break;
				}
				Pen val4 = new Pen(Ui.K2);
				try
				{
					g.DrawLine(val4, num3, num5, num3 + num4, num5);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
				Icons.Draw(g, item.IsCoin ? "coin" : "gift", new Point(num3 + S(7f), num5 + S(7f) + S(10f)), S(13f), item.IsCoin ? Ui.Yellow : Ui.PurpleLight);
				Ui.Draw(g, item.Code, F(13f, medium: true), new Rectangle(num3 + S(20f), num5 + S(7f), num4 - S(20f), S(19f)), Ui.Text, (TextFormatFlags)32772);
				Ui.Draw(g, CouponText.HistoryLine(item), F(12f), new Rectangle(num3, num5 + S(7f) + S(19f), num4, S(17f)), Ui.TextMute, (TextFormatFlags)32772);
				num5 += num2;
			}
			if (array2.Length > num6)
			{
				Ui.Draw(g, $"{_cpHistScroll + 1}–{Math.Min(array2.Length, _cpHistScroll + num6)}/{array2.Length}", F(11f), new Rectangle(r2.Right - S(14f) - S(60f), r2.Y + S(12f), S(60f), S(20f)), Ui.TextMute, (TextFormatFlags)6);
			}
		}
		g.Restore(val2);
		Rectangle r3 = new Rectangle(r.X, r2.Bottom + S(12f), r.Width, num);
		Ui.Card(g, r3, S(12f), Ui.K1, Ui.K3);
		int num7 = r3.X + S(14f);
		int num8 = r3.Y + S(12f);
		Ui.Draw(g, L.T("ค\u0e39ปองได\u0e49จากไหน", "Where to get codes"), F(13.5f, medium: true), new Rectangle(num7, num8, r3.Width - S(28f), S(20f)), Ui.Text, (TextFormatFlags)4);
		num8 += S(20f) + S(6f);
		string[] array3 = array;
		foreach (string s2 in array3)
		{
			Ui.Dot(g, new PointF(num7 + S(6f), num8 + S(9f)), S(4f), Ui.TextDim);
			int num10 = Ui.MeasureWrapped(g, s2, bf, bw);
			Ui.DrawWrapped(g, s2, bf, new Rectangle(num7 + S(16f), num8, bw, num10 + S(2f)), Ui.TextDim);
			num8 += num10 + S(4f);
		}
	}

	public LauncherForm()
	{
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Expected O, but got Unknown
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Expected O, but got Unknown
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Expected O, but got Unknown
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Expected O, but got Unknown
		//IL_044e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0458: Expected O, but got Unknown
		((Control)this).Text = "Durango: Community Edition Launcher";
		((Form)this).FormBorderStyle = (FormBorderStyle)0;
		((Form)this).StartPosition = (FormStartPosition)1;
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)0;
		((Control)this).BackColor = Ui.K0;
		base.DoubleBuffered = true;
		base.SetStyle((ControlStyles)139282, true);
		_s = (float)((Control)this).DeviceDpi / 96f;
		((Form)this).ClientSize = new Size(S(1160f), S(690f));
		try
		{
			((Form)this).Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
		}
		if (!Ui.FontsLoaded)
		{
			Log.Write("ฟอนต\u0e4c Kanit โหลดไม\u0e48ครบ — ใช\u0e49 " + Ui.Fallback);
		}
		InitAuth();
		InitTrade();
		if (TestMode)
		{
			((Form)this).StartPosition = (FormStartPosition)0;
			((Form)this).Location = new Point(-3000, 40);
		}
		_onlineTimer.Tick += async delegate
		{
			if (_game != null)
			{
				UpdateInfo info = _info;
				if (info != null && info.ServerOnline && Idle)
				{
					UpdateInfo info2 = _info;
					int? num = await UpdateSource.FetchOnlineAsync(_game.Gateway);
					if (num.HasValue && info2 == _info)
					{
						info2.OnlinePlayers = num;
						NoteOnline(num);
						((Control)this).Invalidate();
					}
				}
			}
		};
		_onlineTimer.Start();
		_anim.Tick += delegate
		{
			_animT++;
			if (_progress < 0.0 && !_progressRect.IsEmpty)
			{
				((Control)this).Invalidate(Rectangle.Inflate(_progressRect, 2, 2));
			}
			if (!_mainRect.IsEmpty)
			{
				((Control)this).Invalidate(_mainRect);
			}
			if ((ModalOpen && _authBusy) || TradeAnimating)
			{
				((Control)this).Invalidate();
			}
			else if (AuthAnimating && !_cardRect.IsEmpty)
			{
				((Control)this).Invalidate(_cardRect);
			}
		};
		_clock.Tick += delegate
		{
			bool flag = VisibleCountdown != null;
			if (flag || flag != _cdShown || _onlineUpUntil != default(DateTime) || _toast.HasValue || _aeOpen || (!LoggedIn && (Locked || _banner?.Kind == "locked" || _scr == Auth.Wait)) || (LoggedIn && TradePage) || _od != null || _page == Page.Calendar)
			{
				((Control)this).Invalidate();
			}
			_cdShown = flag;
			if (_onlineUpUntil != default(DateTime) && DateTime.UtcNow > _onlineUpUntil)
			{
				_onlineUpUntil = default(DateTime);
			}
		};
		_clock.Start();
		((Form)this).Load += async delegate
		{
			await RefreshAsync();
		};
		((Form)this).FormClosing += (FormClosingEventHandler)delegate(object? _, FormClosingEventArgs e)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Invalid comparison between Unknown and I4
			if (_cts != null)
			{
				if ((int)MessageBox.Show(L.T("กำล\u0e31งตรวจ/โหลดไฟล\u0e4cเกมอย\u0e39\u0e48 — ยกเล\u0e34กแล\u0e49วป\u0e34ด? (ไฟล\u0e4cท\u0e35\u0e48โหลดเสร\u0e47จแล\u0e49วไม\u0e48ต\u0e49องโหลดซ\u0e49ำ)", "Checking/downloading game files — cancel and close? (files already downloaded are kept)"), ((Control)this).Text, (MessageBoxButtons)4, (MessageBoxIcon)48) == 6)
				{
					_cts.Cancel();
				}
				else
				{
					((CancelEventArgs)(object)e).Cancel = true;
				}
			}
		};
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint hwnd, int msg, nint w, nint l);

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		try
		{
			int value = 2;
			DwmSetWindowAttribute(((Control)this).Handle, 33, ref value, 4);
		}
		catch
		{
		}
	}

	protected override void OnDpiChanged(DpiChangedEventArgs e)
	{
		base.OnDpiChanged(e);
		_s = (float)e.DeviceDpiNew / 96f;
		foreach (Font value in _fonts.Values)
		{
			value.Dispose();
		}
		_fonts.Clear();
		((Form)this).ClientSize = new Size(S(1160f), S(690f));
		((Control)this).Invalidate();
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Invalid comparison between Unknown and I4
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Invalid comparison between Unknown and I4
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Invalid comparison between Unknown and I4
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		if ((int)keyData == 13 && ((ContainerControl)this).ActiveControl is TextBox)
		{
			if (!ModalOpen && TradeSubmit())
			{
				return true;
			}
			Submit();
			return true;
		}
		if ((int)keyData == 27)
		{
			if (_dd.HasValue)
			{
				_dd = null;
				((Control)this).Invalidate();
				return true;
			}
			if (_od != null)
			{
				if (!_od.Busy)
				{
					CloseOffer();
				}
				return true;
			}
			if (_chpwOpen)
			{
				CloseChpw();
				return true;
			}
			if (_aeOpen)
			{
				CloseAddEmail();
				return true;
			}
			bool flag = !LoggedIn && _page == Page.Home;
			if (flag)
			{
				Auth scr = _scr;
				bool flag2 = (((uint)(scr - 2) <= 1u || scr == Auth.NoId) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				GoAuth((_scr == Auth.Wait) ? DiscordBack(_waitFor) : Auth.Login);
				return true;
			}
		}
		if ((int)keyData == 13 && PlayEnabled && !ModalOpen && _od == null)
		{
			OnPlay();
			return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	private int S(float v)
	{
		return (int)Math.Round(v * _s);
	}

	private Font F(float px, bool medium = false)
	{
		(float, bool) key = (px, medium);
		if (!_fonts.TryGetValue(key, out Font value))
		{
			value = (_fonts[key] = Ui.NewFont(px * _s, medium));
		}
		return value;
	}

	private (Color Color, string Text) ServerState()
	{
		if (!_checking && _info != null)
		{
			if (!_info.ServerOnline)
			{
				return (Color: Ui.Bad, Text: L.T("ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49", "Server unreachable"));
			}
			return (Color: Ui.Good, Text: L.T("เซ\u0e34ร\u0e4cฟเวอร\u0e4cออนไลน\u0e4c", "Server online"));
		}
		return (Color: Ui.Yellow, Text: L.T("กำล\u0e31งตรวจสอบ...", "Checking..."));
	}

	private void TakeInfo(UpdateInfo info)
	{
		_info = info;
		_countdown = (info.ServerOnline ? info.Countdown : null);
		_skew = info.ClockSkew;
		NoteOnline(info.OnlinePlayers);
		TakeAccount(info.Account);
	}

	private void NoteOnline(int? n)
	{
		if (n.HasValue)
		{
			int valueOrDefault = n.GetValueOrDefault();
			int? lastOnline = _lastOnline;
			if (lastOnline.HasValue)
			{
				int valueOrDefault2 = lastOnline.GetValueOrDefault();
				if (valueOrDefault > valueOrDefault2)
				{
					_onlineUpUntil = DateTime.UtcNow.AddMilliseconds(900.0);
				}
			}
		}
		if (n.HasValue)
		{
			_lastOnline = n;
		}
	}

	private async Task RefreshAsync()
	{
		if (!Idle)
		{
			return;
		}
		_checking = true;
		_synced = false;
		_info = null;
		_anim.Start();
		SetStatus(Ui.TextDim, "กำล\u0e31งตรวจสอบ...", "Checking...");
		_game = GameInstall.Locate();
		((Control)this).Invalidate();
		string gateway = _game.Gateway;
		Task<LauncherChannel.Release?> channel = ((TestMode || GameInstall.SelfPath == null) ? null : LauncherChannelHttp.FetchAsync());
		TakeInfo(await UpdateSource.FetchAsync(gateway));
		_checking = false;
		UpdateAnim();
		bool flag = channel != null;
		if (flag)
		{
			flag = await UpdateLauncherAsync(await channel);
		}
		if (flag)
		{
			return;
		}
		string target = _info.LatestVersion;
		if (GameInstall.GameRunning())
		{
			SetStatus(Ui.Yellow, "เกมเป\u0e34ดอย\u0e39\u0e48 — ป\u0e34ดเกมแล\u0e49วกดตรวจไฟล\u0e4c", "The game is running — close it and press Verify");
			return;
		}
		if (TestMode && Installed)
		{
			_synced = true;
			SetStatus(Ui.TextDim, "โหมดทดสอบ: ข\u0e49ามการตรวจไฟล\u0e4c · v" + _game.Version, "Test mode: file check skipped · v" + _game.Version);
			return;
		}
		if (target.Length == 0)
		{
			SetStatus(Installed ? Ui.Yellow : Ui.Bad, Installed ? ("ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟและ GitHub ไม\u0e48ได\u0e49 — ตรวจไฟล\u0e4cไม\u0e48ได\u0e49 · เวอร\u0e4cช\u0e31น " + _game.Version + " กดเล\u0e48นได\u0e49") : "ต\u0e34ดต\u0e48อ GitHub ไม\u0e48ได\u0e49 — ย\u0e31งต\u0e34ดต\u0e31\u0e49งเกมไม\u0e48ได\u0e49 กดต\u0e34ดต\u0e31\u0e49งเกมเพ\u0e37\u0e48อลองใหม\u0e48", Installed ? ("Cannot reach the server or GitHub — files not checked · version " + _game.Version + " can still be played") : "Cannot reach GitHub — the game cannot be installed yet. Press Install to try again");
			return;
		}
		await SyncAsync(target);
		if (!string.Equals(_game.Gateway, gateway, StringComparison.OrdinalIgnoreCase))
		{
			UpdateInfo updateInfo = await UpdateSource.FetchAsync(_game.Gateway);
			updateInfo.LatestVersion = target;
			TakeInfo(updateInfo);
			((Control)this).Invalidate();
		}
	}

	private async Task<bool> UpdateLauncherAsync(LauncherChannel.Release? rel)
	{
		if (rel == null || !LauncherChannel.IsNewer(rel.Version, GameInstall.LauncherVersion))
		{
			return false;
		}
		string self = GameInstall.SelfPath;
		string tmp = self + ".new";
		Log.Write($"launcher {GameInstall.LauncherVersion} → {rel.Version} (pc-launcher.json)");
		BeginBusy("launcher");
		SetStatus(Ui.TextDim, $"กำล\u0e31งอ\u0e31ปเดตร\u0e31นเช\u0e48อเป\u0e47น {rel.Version} ({(double)rel.Size / 1048576.0:N0} MB · ไม\u0e48ต\u0e49องโหลดต\u0e31วเกมใหม\u0e48) ...", $"Updating the launcher to {rel.Version} ({(double)rel.Size / 1048576.0:N0} MB · game files are not re-downloaded) ...");
		try
		{
			Progress<GameSync.Progress> progress = new Progress<GameSync.Progress>(OnProgress);
			bool flag = await LauncherSelfUpdate.TryDownloadAsync(LauncherChannelHttp.Client, rel.Url, tmp, rel.Sha256, rel.Size, delegate(long b)
			{
				((IProgress<GameSync.Progress>)progress).Report(new GameSync.Progress("download", b, rel.Size));
			}, _cts.Token);
			if (flag && !LauncherChannel.VersionMatches(FileVersionInfo.GetVersionInfo(tmp).FileVersion ?? "", rel.Version))
			{
				Log.Write($"launcher: exe ท\u0e35\u0e48โหลดมาเป\u0e47นร\u0e38\u0e48น {FileVersionInfo.GetVersionInfo(tmp).FileVersion} ≠ {rel.Version} — ข\u0e49าม");
				flag = false;
			}
			if (!flag)
			{
				try
				{
					File.Delete(tmp);
				}
				catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
				{
				}
				return false;
			}
			File.Move(self, self + ".old", overwrite: true);
			File.Move(tmp, self);
			Log.Write("launcher อ\u0e31ปต\u0e31วเองแล\u0e49ว (pc-launcher.json) → เป\u0e34ดต\u0e31วใหม\u0e48");
			Process.Start(new ProcessStartInfo(self)
			{
				UseShellExecute = true,
				WorkingDirectory = Path.GetDirectoryName(self)
			});
			Application.Exit();
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception ex3) when (((ex3 is IOException || ex3 is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			Log.Write("launcher: สล\u0e31บ exe ไม\u0e48ได\u0e49 — " + ex3.Message);
			return false;
		}
		finally
		{
			if (!((Control)this).IsDisposed)
			{
				EndBusy();
			}
		}
	}

	private async Task SyncAsync(string version)
	{
		GameInstall game = _game;
		bool fresh = !game.Installed;
		BeginBusy("sync");
		_progressText = () => L.T("กำล\u0e31งโหลดรายการไฟล\u0e4c...", "Loading file list...");
		SetStatus(Ui.TextDim, fresh ? ("กำล\u0e31งเตร\u0e35ยมต\u0e34ดต\u0e31\u0e49ง Durango v" + version + " ...") : ("กำล\u0e31งตรวจไฟล\u0e4cเกม v" + version + " ..."), fresh ? ("Preparing to install Durango v" + version + " ...") : ("Verifying game files v" + version + " ..."));
		try
		{
			CancellationToken ct = _cts.Token;
			Progress<GameSync.Progress> progress = new Progress<GameSync.Progress>(OnProgress);
			ReleaseAssets rel = (await UpdateSource.FetchReleaseAsync(version)) ?? throw new InvalidOperationException(L.T("ไม\u0e48พบไฟล\u0e4cของเวอร\u0e4cช\u0e31น " + version + " บน GitHub", "No files for version " + version + " on GitHub"));
			GameSync.Plan plan = await GameSync.ScanAsync(game, await GameSync.FetchManifestAsync(rel.ManifestUrl, ct), progress, ct);
			GameSync.Result result = new GameSync.Result(0, 0, 0, LauncherUpdated: false);
			if (!plan.Clean)
			{
				int count = plan.Need.Count;
				SetStatus(Ui.TextDim, fresh ? ("กำล\u0e31งต\u0e34ดต\u0e31\u0e49ง Durango v" + version + " ...") : ((count > 0) ? $"กำล\u0e31งโหลดไฟล\u0e4cท\u0e35\u0e48ขาด/เส\u0e35ย {count:N0} ไฟล\u0e4c · v{version} ..." : "กำล\u0e31งลบไฟล\u0e4cเก\u0e34น ..."), fresh ? ("Installing Durango v" + version + " ...") : ((count > 0) ? $"Downloading {count:N0} missing/broken files · v{version} ..." : "Removing extra files ..."));
				result = await GameSync.ApplyAsync(game, plan, rel.FullZipUrl, progress, ct, rel.LauncherExeUrl, rel.ChunksUrl, rel.BlocksUrl);
				game.Reload();
			}
			if (result.LauncherUpdated && GameInstall.SelfPath != null)
			{
				Log.Write("launcher อ\u0e31ปต\u0e31วเองแล\u0e49ว → เป\u0e34ดต\u0e31วใหม\u0e48");
				Process.Start(new ProcessStartInfo(GameInstall.SelfPath)
				{
					UseShellExecute = true,
					WorkingDirectory = Path.GetDirectoryName(GameInstall.SelfPath)
				});
				Application.Exit();
			}
			else
			{
				_synced = true;
				ReadyStatus(fresh, plan, result);
			}
		}
		catch (OperationCanceledException)
		{
			SetStatus(Ui.Yellow, fresh ? "ยกเล\u0e34กการต\u0e34ดต\u0e31\u0e49ง — กดต\u0e34ดต\u0e31\u0e49งเกมเพ\u0e37\u0e48อทำต\u0e48อ" : "ยกเล\u0e34กการตรวจไฟล\u0e4c — กดตรวจไฟล\u0e4cเพ\u0e37\u0e48อทำต\u0e48อ", fresh ? "Install cancelled — press Install to continue" : "File check cancelled — press Verify to continue");
		}
		catch (Exception ex2)
		{
			Log.Write("sync failed: " + ex2);
			string text = (fresh ? "ต\u0e34ดต\u0e31\u0e49งเกมไม\u0e48สำเร\u0e47จ" : "ตรวจ/ซ\u0e48อมไฟล\u0e4cเกมไม\u0e48สำเร\u0e47จ");
			string text2 = (fresh ? "Game install failed" : "Game file check/repair failed");
			SetStatus(Ui.Bad, text + ": " + ex2.Message, text2 + ": " + ex2.Message);
			MessageBox.Show(L.T(text, text2) + ":\n" + ex2.Message + "\n\n" + L.T("กดตรวจไฟล\u0e4cเพ\u0e37\u0e48อลองใหม\u0e48 (ไฟล\u0e4cท\u0e35\u0e48โหลดเสร\u0e47จแล\u0e49วไม\u0e48ต\u0e49องโหลดซ\u0e49ำ)", "Press Verify to try again (files already downloaded are kept)"), ((Control)this).Text, (MessageBoxButtons)0, (MessageBoxIcon)16);
		}
		finally
		{
			EndBusy();
		}
	}

	private void ReadyStatus(bool fresh, GameSync.Plan plan, GameSync.Result res)
	{
		GameInstall game = _game;
		string version = game.Version;
		if (Blocked)
		{
			SetStatus(Ui.Bad, $"เวอร\u0e4cช\u0e31น {version} ต\u0e48ำกว\u0e48าข\u0e31\u0e49นต\u0e48ำของเซ\u0e34ร\u0e4cฟ ({_info.MinVersion}) — รอผ\u0e39\u0e49ด\u0e39แลอ\u0e31ปไฟล\u0e4cเวอร\u0e4cช\u0e31นใหม\u0e48", $"Version {version} is below the server minimum ({_info.MinVersion}) — wait for the new files to be published");
			return;
		}
		if (plan.UnsafeDir && plan.Extras.Count > 0)
		{
			SetStatus(Ui.Yellow, $"v{version} พร\u0e49อมเล\u0e48น · ไม\u0e48ลบไฟล\u0e4cอ\u0e37\u0e48น {plan.Extras.Count:N0} ไฟล\u0e4c เพราะเกมอย\u0e39\u0e48ใน {game.Dir} — ย\u0e49ายเกมไปไว\u0e49ในโฟลเดอร\u0e4cของม\u0e31นเอง", $"v{version} ready · {plan.Extras.Count:N0} other files were not removed because the game is in {game.Dir} — move the game into its own folder");
			return;
		}
		if (res.DeleteFailed > 0)
		{
			SetStatus(Ui.Yellow, $"v{version} พร\u0e49อมเล\u0e48น · ลบไฟล\u0e4cเก\u0e34นไม\u0e48ได\u0e49 {res.DeleteFailed:N0} ไฟล\u0e4c (ม\u0e35โปรแกรมเป\u0e34ดอย\u0e39\u0e48) — จะลองใหม\u0e48รอบหน\u0e49า", $"v{version} ready · {res.DeleteFailed:N0} extra files could not be removed (in use) — will retry next time");
			return;
		}
		UpdateInfo info = _info;
		bool flag = info != null && !info.ServerOnline;
		string text = (flag ? " · เซ\u0e34ร\u0e4cฟเวอร\u0e4cไม\u0e48ตอบ อาจเข\u0e49าโลกไม\u0e48ได\u0e49" : "");
		string text2 = (flag ? " · server not responding, you may not get into the world" : "");
		if (fresh)
		{
			SetStatus(Ui.Good, "ต\u0e34ดต\u0e31\u0e49ง Durango v" + version + " เสร\u0e47จแล\u0e49ว — พร\u0e49อมเล\u0e48น" + text, "Durango v" + version + " installed — ready to play" + text2);
		}
		else if (res.Downloaded > 0 || res.Deleted > 0)
		{
			string value = string.Join(" · ", new string[2]
			{
				(res.Downloaded > 0) ? $"โหลด {res.Downloaded:N0} ไฟล\u0e4c" : "",
				(res.Deleted > 0) ? $"ลบไฟล\u0e4cเก\u0e34น {res.Deleted:N0} ไฟล\u0e4c" : ""
			}.Where((string s) => s.Length > 0));
			string value2 = string.Join(" · ", new string[2]
			{
				(res.Downloaded > 0) ? $"downloaded {res.Downloaded:N0} files" : "",
				(res.Deleted > 0) ? $"removed {res.Deleted:N0} extra files" : ""
			}.Where((string s) => s.Length > 0));
			SetStatus(Ui.Good, $"ซ\u0e48อมไฟล\u0e4cเกมแล\u0e49ว ({value}) — v{version} พร\u0e49อมเล\u0e48น{text}", $"Game files repaired ({value2}) — v{version} ready to play{text2}");
		}
		else
		{
			SetStatus(flag ? Ui.Yellow : Ui.TextDim, $"พร\u0e49อมเล\u0e48น · ไฟล\u0e4cเกมครบ {plan.Manifest.Files.Count:N0} ไฟล\u0e4c · v{version} ล\u0e48าส\u0e38ดแล\u0e49ว{text}", $"Ready to play · all {plan.Manifest.Files.Count:N0} game files OK · v{version} is up to date{text2}");
		}
	}

	private void SetStatus(Color color, string th, string en)
	{
		_statusColor = color;
		_status = () => L.T(th, en);
		((Control)this).Invalidate();
	}

	private void BeginBusy(string what)
	{
		_busy = what;
		_phase = "verify";
		_cts = new CancellationTokenSource();
		_progress = -1.0;
		_progressText = () => "";
		_anim.Start();
		((Control)this).Invalidate();
	}

	private void EndBusy()
	{
		_busy = null;
		_cts?.Dispose();
		_cts = null;
		_progress = -1.0;
		_progressText = () => "";
		UpdateAnim();
		((Control)this).Invalidate();
	}

	private void OnProgress(GameSync.Progress p)
	{
		bool dl = p.Phase == "download";
		_phase = p.Phase;
		double done = (double)p.Done / 1048576.0;
		double total = (double)p.Total / 1048576.0;
		if (p.Total > 0)
		{
			double frac = (_progress = Math.Clamp((double)p.Done / (double)p.Total, 0.0, 1.0));
			_progressText = () => $"{done:N0} / {total:N0} MB · {frac * 100.0:N0}%";
		}
		else
		{
			_progress = -1.0;
			_progressText = () => (!dl) ? $"{done:N0} MB" : L.T("กำล\u0e31งเช\u0e37\u0e48อมต\u0e48อ...", "Connecting...");
		}
		((Control)this).Invalidate();
	}

	private async void OnPlay()
	{
		if (_game == null || !PlayEnabled)
		{
			return;
		}
		if (!File.Exists(_game.ExePath))
		{
			MessageBox.Show(L.T("ไม\u0e48พบไฟล\u0e4cเกม:\n", "Game file not found:\n") + _game.ExePath, ((Control)this).Text, (MessageBoxButtons)0, (MessageBoxIcon)16);
			return;
		}
		GameInstall game = _game;
		_launching = true;
		UpdateAnim();
		Session session = _session;
		if (session != null)
		{
			if (session.Supporter)
			{
				SetStatus(Ui.TextDim, "กำล\u0e31งเป\u0e34ดเกม · Supporter เข\u0e49าเลนพ\u0e34เศษ ไม\u0e48ต\u0e49องรอค\u0e34ว", "Starting game · Supporter priority lane, no queue");
			}
			else
			{
				SetStatus(Ui.TextDim, "กำล\u0e31งเป\u0e34ดเกม · ล\u0e47อกอ\u0e34นในเกมให\u0e49อ\u0e31ตโนม\u0e31ต\u0e34", "Starting game · signing you in automatically");
			}
		}
		else
		{
			SetStatus(Ui.TextDim, "Starting Durango.exe…", "Starting Durango.exe…");
		}
		((Control)this).Update();
		string ticket = null;
		if (_session != null)
		{
			try
			{
				ticket = await TicketAsync();
			}
			catch (ApiError apiError)
			{
				_launching = false;
				UpdateAnim();
				if (apiError.IsAuthDead)
				{
					SignOutLocal(new BannerInfo("relogin"), _session?.Username);
					SetStatus(Ui.Yellow, "เซสช\u0e31นหมดอาย\u0e38 — เข\u0e49าส\u0e39\u0e48ระบบอ\u0e35กคร\u0e31\u0e49งแล\u0e49วกดเข\u0e49าเกม", "Your session expired — sign in again, then press Play");
				}
				else
				{
					SetStatus(Ui.Bad, "ขอต\u0e31\u0e4bวเข\u0e49าเกมไม\u0e48ได\u0e49: " + apiError.Message, "Could not get a game ticket: " + apiError.Message);
				}
				return;
			}
		}
		try
		{
			game.Launch(ticket);
			Application.Exit();
		}
		catch (Exception ex)
		{
			_launching = false;
			UpdateAnim();
			Log.Write("launch failed: " + ex.Message);
			SetStatus(Ui.Bad, "เป\u0e34ดเกมไม\u0e48ได\u0e49: " + ex.Message, "Could not start the game: " + ex.Message);
			MessageBox.Show(L.T("เป\u0e34ดเกมไม\u0e48ได\u0e49:\n", "Could not start the game:\n") + ex.Message, ((Control)this).Text, (MessageBoxButtons)0, (MessageBoxIcon)16);
		}
	}

	private static void OpenUrl(string url)
	{
		if (TestMode)
		{
			Log.Write("test: open " + url);
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(url)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Log.Write("open url: " + ex.Message);
		}
	}

	private void OpenFolder()
	{
		if (_game != null && Directory.Exists(_game.Dir))
		{
			Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _game.Dir + "\"")
			{
				UseShellExecute = true
			});
		}
	}

	private static string GuideUrl(string page)
	{
		return GuideLink.Url(page, L.En);
	}

	private void ShowPage(Page page, int focus = -1)
	{
		if (page != _page)
		{
			TradeOnPage(page);
		}
		_page = page;
		_newsScroll = 0;
		_newsFocus = focus;
		((Control)this).Invalidate();
	}

	private void ToggleLanguage()
	{
		L.Set(!L.En);
		((Control)this).Invalidate();
	}

	private Hit? HitAt(Point p)
	{
		for (int num = _hits.Count - 1; num >= 0; num--)
		{
			if (_hits[num].R.Contains(p))
			{
				return _hits[num];
			}
		}
		return null;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		Hit hit = HitAt(e.Location);
		string text = (((object)hit != null && hit.Enabled) ? hit.Id : null);
		((Control)this).Cursor = ((text == null) ? Cursors.Default : (text.StartsWith("f-", StringComparison.Ordinal) ? Cursors.IBeam : Cursors.Hand));
		if (text != _hover)
		{
			_hover = text;
			((Control)this).Invalidate();
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		if (_hover != null)
		{
			_hover = null;
			((Control)this).Invalidate();
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		base.OnMouseDown(e);
		if ((int)e.Button != 1048576)
		{
			return;
		}
		Hit hit = HitAt(e.Location);
		if (hit == null)
		{
			if (e.Y < S(56f) || (e.X < S(236f) && e.Y < S(104f)))
			{
				ReleaseCapture();
				SendMessage(((Control)this).Handle, 161, 2, IntPtr.Zero);
			}
		}
		else
		{
			_pressed = (hit.Enabled ? hit.Id : null);
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		Hit hit = HitAt(e.Location);
		string pressed = _pressed;
		_pressed = null;
		if ((object)hit != null && hit.Enabled && hit.Id == pressed)
		{
			hit.Click?.Invoke();
		}
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		base.OnMouseWheel(e);
		if (!CalWheel(e) && !TradeWheel(e) && _page == Page.News)
		{
			_newsScroll = Math.Clamp(_newsScroll - e.Delta * S(90f) / 120, 0, _newsMax);
			((Control)this).Invalidate();
		}
	}

	private bool AddHit(Rectangle r, string id, Action? click, bool enabled = true)
	{
		_hits.Add(new Hit(r, id, click, enabled));
		if (enabled)
		{
			return _hover == id;
		}
		return false;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Expected O, but got Unknown
		Graphics graphics = e.Graphics;
		_hits.Clear();
		BeginBoxes();
		graphics.SmoothingMode = (SmoothingMode)4;
		graphics.InterpolationMode = (InterpolationMode)7;
		graphics.PixelOffsetMode = (PixelOffsetMode)2;
		graphics.Clear(Ui.K0);
		int width = ((Form)this).ClientSize.Width;
		int height = ((Form)this).ClientSize.Height;
		Rectangle rectangle = new Rectangle(0, 0, S(236f), height);
		SolidBrush val = new SolidBrush(Ui.K1);
		try
		{
			graphics.FillRectangle((Brush)(object)val, rectangle);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Pen val2 = new Pen(Ui.K3);
		try
		{
			graphics.DrawLine(val2, rectangle.Right - 1, 0, rectangle.Right - 1, height);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		DrawSidebar(graphics, rectangle);
		Rectangle main = Rectangle.FromLTRB(rectangle.Right, 0, width, height);
		DrawTopBar(graphics, main);
		Rectangle bar = Rectangle.FromLTRB(main.X, height - S(84f), main.Right, height);
		Rectangle pane = Rectangle.FromLTRB(main.X + S(28f), S(56f), main.Right - S(28f), bar.Y - S(16f));
		switch (_page)
		{
		case Page.Home:
			DrawHome(graphics, pane);
			break;
		case Page.News:
			DrawNews(graphics, pane);
			break;
		case Page.Guide:
			DrawGuide(graphics, pane);
			break;
		case Page.Chars:
			DrawChars(graphics, pane);
			break;
		case Page.Chat:
			DrawChat(graphics, pane);
			break;
		case Page.Market:
			DrawMarket(graphics, pane);
			break;
		case Page.Trade:
			DrawTrade(graphics, pane);
			break;
		case Page.Calendar:
			DrawCalendar(graphics, pane);
			break;
		case Page.Coupon:
			DrawCoupon(graphics, pane);
			break;
		default:
			DrawCommunity(graphics, pane);
			break;
		}
		DrawBottomBar(graphics, bar);
		if (_chpwOpen)
		{
			DrawChpwModal(graphics);
		}
		else if (_aeOpen)
		{
			DrawAddEmailModal(graphics);
		}
		else if (_od != null)
		{
			DrawOfferModal(graphics);
		}
		DrawDropdown(graphics);
		DrawToast(graphics);
		EndBoxes();
		Pen val3 = new Pen(Ui.K3);
		try
		{
			graphics.SmoothingMode = (SmoothingMode)3;
			graphics.DrawRectangle(val3, 0, 0, width - 1, height - 1);
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
	}

	private void DrawSidebar(Graphics g, Rectangle r)
	{
		int num = r.X + S(16f);
		int num2 = r.Width - S(32f);
		Font f = F(34f, medium: true);
		Ui.Draw(g, "Durango", f, new Rectangle(num + S(8f), S(20f), num2, S(46f)), Ui.Yellow, (TextFormatFlags)4);
		DrawSpaced(g, "reborn", F(14.5f), new Point(num + S(11f), S(64f)), S(20f), Ui.PurpleLight, S(3.6f));
		int num3 = S(116f);
		NavItem(g, new Rectangle(num, num3, num2, S(46f)), "nav-home", "home", L.T("หน\u0e49าแรก", "Home"), Page.Home);
		num3 += S(52f);
		NavItem(g, new Rectangle(num, num3, num2, S(46f)), "nav-news", "news", L.T("ข\u0e48าวสาร", "News"), Page.News);
		num3 += S(52f);
		NavItem(g, new Rectangle(num, num3, num2, S(46f)), "nav-guide", "book", L.T("ค\u0e39\u0e48ม\u0e37อผ\u0e39\u0e49เล\u0e48น", "Player guide"), Page.Guide);
		num3 += S(52f);
		NavItem(g, new Rectangle(num, num3, num2, S(46f)), "nav-comm", "users", L.T("คอมม\u0e39น\u0e34ต\u0e35\u0e49", "Community"), Page.Community);
		bool accountSection = AccountSection;
		int num4 = (accountSection ? DrawTradeNav(g, num, num2, num3 + S(52f), S(46f), S(6f)) : (num3 + S(46f)));
		int num5 = r.Bottom - S(20f);
		Ui.Draw(g, "Launcher v" + GameInstall.LauncherVersion, F(12f), new Rectangle(num + S(6f), num5 - S(18f), num2, S(18f)), Ui.A(Ui.TextMute, 190), (TextFormatFlags)4);
		num5 -= S(20f);
		string s = (Installed ? L.T("เวอร\u0e4cช\u0e31นเกม " + _game.Version, "Game version " + _game.Version) : ((_game == null) ? "" : L.T("ย\u0e31งไม\u0e48ได\u0e49ต\u0e34ดต\u0e31\u0e49งเกม", "Game not installed")));
		Ui.Draw(g, s, F(13f), new Rectangle(num + S(6f), num5 - S(20f), num2, S(20f)), Ui.TextMute, (TextFormatFlags)4);
		num5 -= S(28f);
		var (color, s2) = ServerState();
		Ui.Dot(g, new PointF(num + S(10f), num5 - S(10f)), S(8f), color);
		Ui.Draw(g, s2, F(13.5f), new Rectangle(num + S(22f), num5 - S(20f), num2 - S(22f), S(20f)), Ui.TextDim, (TextFormatFlags)4);
		num5 -= S(34f);
		if (accountSection && num4 + S(12f) > num5 - S(68f))
		{
			return;
		}
		Countdown visibleCountdown = VisibleCountdown;
		if ((object)visibleCountdown != null)
		{
			Rectangle r2 = new Rectangle(num, num5 - S(68f), num2, S(68f));
			Ui.Fill(g, r2, S(10f), Ui.PurpleDark);
			bool flag = visibleCountdown.PhaseAt(Now) == Countdown.Phase.Done;
			object obj;
			if (!flag)
			{
				obj = visibleCountdown.LabelText(L.En);
			}
			else
			{
				string text = visibleCountdown.TagText(L.En);
				obj = ((text != null && text.Length > 0) ? text : visibleCountdown.LabelText(L.En));
			}
			string s3 = (string)obj;
			Ui.Draw(g, s3, F(13f), new Rectangle(r2.X + S(14f), r2.Y + S(10f), r2.Width - S(28f), S(20f)), Ui.PurpleLight, (TextFormatFlags)32772);
			string s4 = (flag ? visibleCountdown.DoneText(L.En) : Countdown.Short(visibleCountdown.Left(Now), L.En));
			Ui.Draw(g, s4, F(21f, medium: true), new Rectangle(r2.X + S(14f), r2.Y + S(31f), r2.Width - S(28f), S(28f)), flag ? Ui.Good : Ui.Yellow, (TextFormatFlags)32772);
		}
	}

	private static void DrawSpaced(Graphics g, string s, Font f, Point at, int height, Color c, int spacing)
	{
		int num = at.X;
		for (int i = 0; i < s.Length; i++)
		{
			string s2 = s[i].ToString();
			int width = Ui.Measure(s2, f).Width;
			Ui.Draw(g, s2, f, new Rectangle(num, at.Y, width + 2, height), c, (TextFormatFlags)4);
			num += width + spacing;
		}
	}

	private void NavItem(Graphics g, Rectangle r, string id, string icon, string text, Page page, int badge = 0, bool star = false, bool locked = false)
	{
		bool flag = _page == page;
		bool flag2 = AddHit(r, id, delegate
		{
			ShowPage(page);
		});
		if (flag)
		{
			Ui.Fill(g, r, S(10f), Ui.PurpleDark);
		}
		else if (flag2)
		{
			Ui.Fill(g, r, S(10f), Ui.K2);
		}
		Color color = ((locked && !flag && !flag2) ? Ui.TextMute : ((flag || flag2) ? Ui.Text : Ui.TextDim));
		Icons.Draw(g, icon, new Point(r.X + S(26f), r.Y + r.Height / 2), S(20f), flag ? Ui.Yellow : color);
		Font f = F(15.5f);
		Ui.Draw(g, text, f, new Rectangle(r.X + S(48f), r.Y, r.Width - S(56f), r.Height), color, (TextFormatFlags)4);
		if (star)
		{
			int num = r.X + S(48f) + Ui.Measure(text, f).Width + S(6f);
			Icons.Draw(g, "star", new Point(num + S(7f), r.Y + r.Height / 2), S(14f), Ui.Yellow);
		}
		if (locked)
		{
			Font f2 = F(11f);
			string s = "Supporter";
			int num2 = S(6f) + S(12f) + S(4f) + Ui.Measure(s, f2).Width + S(6f);
			Rectangle r2 = new Rectangle(r.Right - S(14f) - num2, r.Y + (r.Height - S(20f)) / 2, num2, S(20f));
			Ui.Stroke(g, r2, S(5f), K4);
			Icons.Draw(g, "lock", new Point(r2.X + S(6f) + S(6f), r2.Y + r2.Height / 2), S(12f), Ui.Yellow);
			Ui.Draw(g, s, f2, new Rectangle(r2.X + S(22f), r2.Y, num2 - S(22f), r2.Height), Ui.Yellow, (TextFormatFlags)4);
		}
		NavBadge(g, r, badge);
	}

	private void DrawTopBar(Graphics g, Rectangle main)
	{
		Rectangle r = new Rectangle(main.Right - S(10f) - S(40f), S(11f), S(40f), S(34f));
		Rectangle r2 = new Rectangle(r.X - S(40f), r.Y, S(40f), S(34f));
		bool flag = AddHit(r, "close", (Action?)base.Close, enabled: true);
		bool flag2 = AddHit(r2, "min", delegate
		{
			((Form)this).WindowState = (FormWindowState)1;
		});
		if (flag)
		{
			Ui.Fill(g, r, S(8f), Ui.A(Ui.Bad, 200));
		}
		if (flag2)
		{
			Ui.Fill(g, r2, S(8f), Ui.K2);
		}
		Icons.Draw(g, "x", new Point(r.X + r.Width / 2, r.Y + r.Height / 2), S(17f), flag ? Color.White : Ui.TextMute);
		Icons.Draw(g, "minus", new Point(r2.X + r2.Width / 2, r2.Y + r2.Height / 2), S(17f), flag2 ? Ui.Text : Ui.TextMute);
		Font f = F(12.5f, medium: true);
		string s = (L.En ? "EN" : "TH");
		int num = S(10f) + S(15f) + S(5f) + Ui.Measure(s, f).Width + S(10f);
		Rectangle r3 = new Rectangle(r2.X - S(14f) - num, S(14f), num, S(28f));
		bool flag3 = AddHit(r3, "lang", ToggleLanguage);
		Ui.Stroke(g, r3, S(6f), flag3 ? Ui.Yellow : Ui.K3);
		Icons.Draw(g, "world", new Point(r3.X + S(10f) + S(7f), r3.Y + r3.Height / 2), S(15f), flag3 ? Ui.Yellow : Ui.PurpleLight);
		Ui.Draw(g, s, f, new Rectangle(r3.X + S(30f), r3.Y, r3.Width - S(30f), r3.Height), flag3 ? Ui.Yellow : Ui.TextDim, (TextFormatFlags)4);
		(Color Color, string Text) tuple = ServerState();
		Color item = tuple.Color;
		string item2 = tuple.Text;
		Font item3 = F(13.5f);
		Font item4 = F(13.5f, medium: true);
		List<(string, Font, Color)> list = new List<(string, Font, Color)>();
		UpdateInfo info;
		if (!_checking)
		{
			info = _info;
			if (info != null && info.ServerOnline)
			{
				int? onlinePlayers = info.OnlinePlayers;
				if (onlinePlayers.HasValue)
				{
					int valueOrDefault = onlinePlayers.GetValueOrDefault();
					Color item5 = ((DateTime.UtcNow < _onlineUpUntil) ? Ui.Yellow : Ui.Text);
					if (L.En)
					{
						list.Add((valueOrDefault.ToString("N0"), item4, item5));
						list.Add((" players online", item3, Ui.TextDim));
					}
					else
					{
						list.Add(("ผ\u0e39\u0e49เล\u0e48นออนไลน\u0e4c ", item3, Ui.TextDim));
						list.Add((valueOrDefault.ToString("N0"), item4, item5));
						list.Add((" คน", item3, Ui.TextDim));
					}
					goto IL_048f;
				}
			}
		}
		info = _info;
		list.Add((item2, item3, (info != null && !info.ServerOnline && !_checking) ? Ui.Bad : Ui.TextDim));
		goto IL_048f;
		IL_048f:
		int num2 = list.Sum<(string, Font, Color)>(((string Text, Font Font, Color Color) p) => Ui.Measure(p.Text, p.Font).Width);
		int num3 = S(12f) + S(16f) + S(8f) + S(8f) + S(8f) + num2 + S(14f);
		Rectangle r4 = new Rectangle(r3.X - S(10f) - num3, S(12f), num3, S(32f));
		if (_session != null)
		{
			DrawAccountPill(g, r4.X - S(10f));
		}
		Ui.Card(g, r4, (float)r4.Height / 2f, Ui.K2, Ui.K3);
		int num4 = r4.X + S(12f);
		Icons.Draw(g, "users", new Point(num4 + S(8f), r4.Y + r4.Height / 2), S(16f), Ui.PurpleLight);
		num4 += S(16f) + S(8f);
		Ui.Dot(g, new PointF(num4 + S(4f), (float)r4.Y + (float)r4.Height / 2f), S(8f), item);
		num4 += S(8f) + S(8f);
		foreach (var item9 in list)
		{
			string item6 = item9.Item1;
			Font item7 = item9.Item2;
			Color item8 = item9.Item3;
			int width = Ui.Measure(item6, item7).Width;
			Ui.Draw(g, item6, item7, new Rectangle(num4, r4.Y, width + S(2f), r4.Height), item8, (TextFormatFlags)4);
			num4 += width;
		}
	}

	private void DrawHome(Graphics g, Rectangle pane)
	{
		if (_session == null)
		{
			DrawAuthHome(g, pane);
			return;
		}
		_cardRect = Rectangle.Empty;
		int x = pane.X;
		int width = pane.Width;
		int num = pane.Y + S(2f);
		int num2 = AccountCardHeight(g);
		Rectangle hero = new Rectangle(x, num, width, Math.Max(S(180f), Math.Min(S(250f), pane.Bottom - num - S(16f) - num2)));
		DrawHero(g, hero, auth: false);
		num = hero.Bottom + S(16f);
		Rectangle r = new Rectangle(pane.Right - S(324f), num, S(324f), num2);
		DrawAccountCard(g, r);
		DrawLatest(g, Rectangle.FromLTRB(x, num, r.X - S(24f), pane.Bottom));
	}

	private void DrawHero(Graphics g, Rectangle hero, bool auth)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		EnsureHeroCache(hero.Size);
		if (_heroCache != null)
		{
			TextureBrush val = new TextureBrush((Image)(object)_heroCache);
			try
			{
				val.TranslateTransform((float)hero.X, (float)hero.Y);
				GraphicsPath val2 = Ui.Round(hero, S(14f));
				try
				{
					g.FillPath((Brush)(object)val, val2);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		else
		{
			Ui.Fill(g, hero, S(14f), Ui.K1);
		}
		Ui.Stroke(g, hero, S(14f), Ui.K3);
		Countdown visibleCountdown = VisibleCountdown;
		bool done = visibleCountdown != null && visibleCountdown.PhaseAt(Now) == Countdown.Phase.Done;
		string text = visibleCountdown?.TitleText(L.En, done) ?? "";
		if (text.Length > 0)
		{
			int width = (int)((float)hero.Width * (auth ? 0.72f : 0.5f));
			Font f = F(30f, medium: true);
			int num = Math.Min(Ui.MeasureWrapped(g, text, f, width), S(84f));
			string text2 = (auth ? L.T("สม\u0e31คร Durango ID ด\u0e49วย Discord แล\u0e49วเข\u0e49าเกมได\u0e49เลย", "Sign up with Discord, then jump in") : "");
			Font f2 = F(14f);
			int num2 = ((text2.Length > 0) ? Ui.MeasureWrapped(g, text2, f2, width) : 0);
			int num3 = hero.Bottom - S(22f) - num - ((num2 > 0) ? (num2 + S(6f)) : 0);
			Ui.DrawWrapped(g, text, f, new Rectangle(hero.X + S(20f), num3, width, num + S(2f)), Ui.Text);
			if (num2 > 0)
			{
				Ui.DrawWrapped(g, text2, f2, new Rectangle(hero.X + S(20f), num3 + num + S(6f), width, num2 + S(2f)), Ui.TextDim);
			}
			string text3 = visibleCountdown.TagText(L.En);
			if (text3.Length > 0)
			{
				Font f3 = F(12.5f, medium: true);
				int width2 = Ui.Measure(text3, f3).Width + S(18f);
				Rectangle r = new Rectangle(hero.X + S(22f), num3 - S(8f) - S(24f), width2, S(24f));
				Ui.Fill(g, r, S(5f), Ui.Yellow);
				Ui.Draw(g, text3, f3, r, Ui.OnYellow, (TextFormatFlags)5);
			}
		}
		else if (_logo != null)
		{
			int num4 = Math.Min(S(300f), hero.Width - S(48f));
			int num5 = num4 * ((Image)_logo).Height / ((Image)_logo).Width;
			g.DrawImage((Image)(object)_logo, new Rectangle(hero.X + S(24f), hero.Bottom - S(26f) - num5, num4, num5));
		}
		if (visibleCountdown != null)
		{
			DrawCountdownCard(g, hero, visibleCountdown, done);
		}
	}

	private void DrawLatest(Graphics g, Rectangle area)
	{
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Expected O, but got Unknown
		int x = area.X;
		int width = area.Width;
		int y = area.Y;
		Ui.Draw(g, L.T("อ\u0e31ปเดตล\u0e48าส\u0e38ด", "Latest updates"), F(16.5f, medium: true), new Rectangle(x, y, S(320f), S(30f)), Ui.Text, (TextFormatFlags)4);
		Font f = F(13.5f);
		string s = L.T("ด\u0e39ท\u0e31\u0e49งหมด", "View all");
		int num = Ui.Measure(s, f).Width + S(4f);
		Rectangle r = new Rectangle(x + width - num, y, num, S(30f));
		bool flag = AddHit(r, "news-all", delegate
		{
			ShowPage(Page.News);
		});
		Ui.Draw(g, s, f, r, flag ? Ui.Text : Ui.PurpleLight, (TextFormatFlags)6);
		y += S(32f);
		NewsItem[] array = _info?.News ?? Array.Empty<NewsItem>();
		if (array.Length == 0)
		{
			Ui.Draw(g, NoNewsText(), F(14f), new Rectangle(x, y, width, S(44f)), Ui.TextMute, (TextFormatFlags)32772);
			return;
		}
		int num2 = S(44f);
		int num3 = Math.Min(array.Length, Math.Max(1, (area.Bottom - y) / num2));
		(NewsKind, string, string)[] array2 = (from n in array.Take(num3)
			select NewsTags.Classify(n.Title, n.Tag, L.En)).ToArray();
		Font chipF = F(12.5f);
		int num4 = array2.Max(((NewsKind Kind, string Chip, string Title) i) => Ui.Measure(i.Chip, chipF).Width) + S(14f) + S(6f) + S(18f);
		Font f2 = F(12.5f);
		Pen val = new Pen(Ui.K3);
		try
		{
			for (int num5 = 0; num5 < num3; num5++)
			{
				Rectangle rectangle = new Rectangle(x, y + num5 * num2, width, num2);
				int idx = num5;
				if (AddHit(rectangle, "row" + num5, delegate
				{
					ShowPage(Page.News, idx);
				}))
				{
					Ui.Fill(g, Rectangle.Inflate(rectangle, S(8f), -S(3f)), S(8f), Ui.K2);
				}
				if (num5 < num3 - 1)
				{
					g.DrawLine(val, rectangle.X, rectangle.Bottom - 1, rectangle.Right, rectangle.Bottom - 1);
				}
				var (kind, text, s2) = array2[num5];
				DrawNewsChip(g, new Rectangle(rectangle.X, rectangle.Y + (num2 - S(24f)) / 2, num4, S(24f)), kind, text, chipF);
				string s3 = ShortDate(array[num5].Date);
				int width2 = Ui.Measure(s3, f2).Width;
				Ui.Draw(g, s3, f2, new Rectangle(rectangle.Right - width2 - S(2f), rectangle.Y, width2 + S(2f), rectangle.Height), Ui.TextMute, (TextFormatFlags)4);
				Ui.Draw(g, s2, F(15f), Rectangle.FromLTRB(rectangle.X + num4 + S(14f), rectangle.Y, rectangle.Right - width2 - S(20f), rectangle.Bottom), Ui.Text, (TextFormatFlags)32772);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private string NoNewsText()
	{
		if (!_checking)
		{
			UpdateInfo info = _info;
			if (info == null || info.ServerOnline)
			{
				return L.T("ไม\u0e48ม\u0e35ข\u0e48าวสาร", "No news");
			}
			return L.T("ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49 — กดตรวจไฟล\u0e4cเพ\u0e37\u0e48อลองใหม\u0e48", "Cannot reach the server — press Verify to try again");
		}
		return L.T("กำล\u0e31งโหลดข\u0e48าว...", "Loading news...");
	}

	private void DrawNewsChip(Graphics g, Rectangle r, NewsKind kind, string text, Font f)
	{
		int width = Ui.Measure(text, f).Width;
		Rectangle r2 = new Rectangle(r.X, r.Y, Math.Min(r.Width, S(9f) + S(14f) + S(5f) + width + S(9f)), r.Height);
		Ui.Fill(g, r2, S(6f), Ui.PurpleDark);
		Icons.Draw(g, NewsTags.Icon(kind), new Point(r2.X + S(9f) + S(7f), r2.Y + r2.Height / 2), S(14f), Ui.PurpleLight);
		Ui.Draw(g, text, f, new Rectangle(r2.X + S(28f), r2.Y, r2.Width - S(30f), r2.Height), Ui.PurpleLight, (TextFormatFlags)4);
	}

	private void DrawCountdownCard(Graphics g, Rectangle hero, Countdown cd, bool done)
	{
		//IL_0466: Unknown result type (might be due to invalid IL or missing references)
		//IL_046d: Expected O, but got Unknown
		int num = S(58f);
		int num2 = S(14f);
		int num3 = S(18f) + num * 4 + num2 * 3 + S(18f);
		int height = S(146f);
		Rectangle r = new Rectangle(hero.Right - S(18f) - num3, hero.Y + S(18f), num3, height);
		Ui.Card(g, r, S(12f), Ui.A(Ui.K0, 219), Ui.K3);
		int num4 = r.X + S(18f);
		int num5 = r.Y + S(13f);
		int num6 = r.Width - S(36f);
		Icons.Draw(g, "clock-hour-4", new Point(num4 + S(8f), num5 + S(10f)), S(16f), Ui.PurpleLight);
		object s;
		if (!done)
		{
			s = cd.LabelText(L.En);
		}
		else
		{
			string text = cd.TagText(L.En);
			s = ((text != null && text.Length > 0) ? text : cd.LabelText(L.En));
		}
		Ui.Draw(g, (string)s, F(13f), new Rectangle(num4 + S(22f), num5, num6 - S(22f), S(20f)), Ui.PurpleLight, (TextFormatFlags)32772);
		num5 += S(26f);
		Rectangle rectangle = new Rectangle(num4, num5, num6, S(60f));
		if (done)
		{
			Ui.Dot(g, new PointF(rectangle.X + S(6f), rectangle.Y + S(24f)), S(11f), Ui.Good);
			Ui.Draw(g, cd.DoneText(L.En), F(28f, medium: true), new Rectangle(rectangle.X + S(20f), rectangle.Y, rectangle.Width - S(20f), S(48f)), Ui.Good, (TextFormatFlags)32772);
		}
		else
		{
			(int Days, int Hours, int Minutes, int Seconds) tuple = Countdown.Split(cd.Left(Now));
			int item = tuple.Days;
			int item2 = tuple.Hours;
			int item3 = tuple.Minutes;
			int item4 = tuple.Seconds;
			(string, string)[] array = new(string, string)[4]
			{
				(item.ToString(CultureInfo.InvariantCulture), L.T("ว\u0e31น", "days")),
				(item2.ToString("00"), L.T("ชม.", "hrs")),
				(item3.ToString("00"), L.T("นาท\u0e35", "min")),
				(item4.ToString("00"), L.T("ว\u0e34นาท\u0e35", "sec"))
			};
			Font f = F(34f, medium: true);
			Font f2 = F(12f);
			int num7 = rectangle.X;
			for (int i = 0; i < array.Length; i++)
			{
				if (i > 0)
				{
					Ui.Draw(g, ":", F(26f, medium: true), new Rectangle(num7, rectangle.Y, num2, S(42f)), Ui.PurpleDark, (TextFormatFlags)5);
					num7 += num2;
				}
				Ui.Draw(g, array[i].Item1, f, new Rectangle(num7, rectangle.Y, num, S(42f)), Ui.Yellow, (TextFormatFlags)5);
				Ui.Draw(g, array[i].Item2, f2, new Rectangle(num7, rectangle.Y + S(41f), num, S(17f)), Ui.TextMute, (TextFormatFlags)5);
				num7 += num;
			}
		}
		num5 += S(64f);
		Pen val = new Pen(Ui.K3);
		try
		{
			g.DrawLine(val, num4, num5, num4 + num6, num5);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Ui.Draw(g, cd.WhenText(L.En), F(12.5f), new Rectangle(num4, num5 + S(6f), num6, S(20f)), Ui.TextDim, (TextFormatFlags)32772);
	}

	private void EnsureHeroCache(Size size)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Expected O, but got Unknown
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Expected O, but got Unknown
		if (_heroCache != null && ((Image)_heroCache).Size == size)
		{
			return;
		}
		Bitmap? heroCache = _heroCache;
		if (heroCache != null)
		{
			((Image)heroCache).Dispose();
		}
		_heroCache = null;
		if (_hero == null || size.Width <= 0 || size.Height <= 0)
		{
			return;
		}
		Bitmap val = new Bitmap(size.Width, size.Height);
		Graphics val2 = Graphics.FromImage((Image)(object)val);
		try
		{
			val2.InterpolationMode = (InterpolationMode)7;
			val2.PixelOffsetMode = (PixelOffsetMode)2;
			Rectangle dst = new Rectangle(Point.Empty, size);
			ImageAttributes val3 = Ui.PurpleTint();
			try
			{
				Ui.DrawCover(val2, (Image)(object)_hero, dst, new RectangleF(0f, 60f, ((Image)_hero).Width, ((Image)_hero).Height - 120), val3);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			LinearGradientBrush val4 = new LinearGradientBrush(new Rectangle(0, size.Height / 3 - 1, size.Width, size.Height * 2 / 3 + 2), Ui.A(Ui.K0, 0), Ui.A(Ui.K0, 200), (LinearGradientMode)1);
			try
			{
				val2.FillRectangle((Brush)(object)val4, 0, size.Height / 3, size.Width, size.Height * 2 / 3);
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			int num = (int)((float)size.Width * 0.62f);
			LinearGradientBrush val5 = new LinearGradientBrush(new Rectangle(-1, 0, num + 2, size.Height), Ui.A(Ui.K0, 150), Ui.A(Ui.K0, 0), (LinearGradientMode)0);
			try
			{
				val2.FillRectangle((Brush)(object)val5, 0, 0, num, size.Height);
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		_heroCache = val;
	}

	private void DrawNews(Graphics g, Rectangle pane)
	{
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Expected O, but got Unknown
		int x = pane.X;
		int width = pane.Width;
		int num = pane.Y + S(6f);
		PageTitle(g, x, num, width, L.T("ข\u0e48าวสาร", "News"), L.T("อ\u0e31ปเดตต\u0e31วเกมและประกาศจากท\u0e35มงาน", "Game updates and announcements from the team"));
		Rectangle clip = Rectangle.FromLTRB(x, num + S(84f), pane.Right + S(16f), pane.Bottom);
		NewsItem[] array = _info?.News ?? Array.Empty<NewsItem>();
		if (array.Length == 0)
		{
			Ui.Draw(g, NoNewsText(), F(14f), new Rectangle(clip.X, clip.Y, clip.Width, S(40f)), Ui.TextMute, (TextFormatFlags)4);
			return;
		}
		int num2 = width;
		Font f = F(17f, medium: true);
		Font f2 = F(14.5f);
		(NewsKind, string, string)[] array2 = array.Select((NewsItem n) => NewsTags.Classify(n.Title, n.Tag, L.En)).ToArray();
		int[] array3 = new int[array.Length];
		int num3 = 0;
		for (int num4 = 0; num4 < array.Length; num4++)
		{
			array3[num4] = S(34f) + Ui.MeasureWrapped(g, array2[num4].Item3, f, num2) + S(6f) + Ui.MeasureWrapped(g, array[num4].Body, f2, num2) + S(36f);
			num3 += array3[num4];
		}
		_newsMax = Math.Max(0, num3 - clip.Height);
		if (_newsFocus >= 0)
		{
			int num5 = 0;
			for (int num6 = 0; num6 < Math.Min(_newsFocus, array.Length); num6++)
			{
				num5 += array3[num6];
			}
			_newsScroll = num5;
			_newsFocus = -1;
		}
		_newsScroll = Math.Clamp(_newsScroll, 0, _newsMax);
		GraphicsState val = g.Save();
		g.SetClip(clip);
		int num7 = clip.Y - _newsScroll;
		Font f3 = F(12.5f);
		Pen val2 = new Pen(Ui.K3);
		try
		{
			for (int num8 = 0; num8 < array.Length; num8++)
			{
				int num9 = num7;
				num7 += array3[num8];
				if (num7 >= clip.Y && num9 <= clip.Bottom)
				{
					(NewsKind, string, string) tuple = array2[num8];
					NewsKind item = tuple.Item1;
					string item2 = tuple.Item2;
					string item3 = tuple.Item3;
					int num10 = Ui.Measure(item2, f3).Width + S(14f) + S(6f) + S(18f);
					DrawNewsChip(g, new Rectangle(x, num9, num10, S(24f)), item, item2, f3);
					Ui.Draw(g, FormatDate(array[num8].Date), F(13f), new Rectangle(x + num10 + S(12f), num9, S(220f), S(24f)), Ui.TextMute, (TextFormatFlags)4);
					int num11 = num9 + S(34f);
					int num12 = Ui.MeasureWrapped(g, item3, f, num2);
					Ui.DrawWrapped(g, item3, f, new Rectangle(x - S(2f), num11, num2 + S(4f), num12 + S(2f)), Ui.Text);
					num11 += num12 + S(6f);
					int num13 = Ui.MeasureWrapped(g, array[num8].Body, f2, num2);
					Ui.DrawWrapped(g, array[num8].Body, f2, new Rectangle(x - S(2f), num11, num2 + S(4f), num13 + S(2f)), Ui.TextDim);
					if (num8 < array.Length - 1)
					{
						g.DrawLine(val2, x, num7 - S(18f), x + width, num7 - S(18f));
					}
				}
			}
			g.Restore(val);
			if (_newsMax > 0)
			{
				int height = clip.Height;
				int num14 = Math.Max(S(40f), height * clip.Height / num3);
				int y = clip.Y + (int)((long)(height - num14) * (long)_newsScroll / _newsMax);
				Ui.Fill(g, new Rectangle(clip.Right - S(6f), y, S(4f), num14), S(2f), Ui.K3);
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void PageTitle(Graphics g, int x, int y, int w, string title, string sub)
	{
		Ui.Draw(g, title, F(26f, medium: true), new Rectangle(x, y, w, S(40f)), Ui.Text, (TextFormatFlags)4);
		Ui.Draw(g, sub, F(14.5f), new Rectangle(x, y + S(42f), w, S(24f)), Ui.TextDim, (TextFormatFlags)32772);
	}

	private void DrawGuide(Graphics g, Rectangle pane)
	{
		int x = pane.X;
		int width = pane.Width;
		int num = pane.Y + S(6f);
		PageTitle(g, x, num, width, L.T("ค\u0e39\u0e48ม\u0e37อผ\u0e39\u0e49เล\u0e48น", "Player guide"), L.T("เป\u0e34ดในเบราว\u0e4cเซอร\u0e4c — ค\u0e39\u0e48ม\u0e37อฉบ\u0e31บเต\u0e47มอย\u0e39\u0e48บนเว\u0e47บ", "Opens in your browser — the full guide is on the web"));
		num += S(84f);
		for (int i = 0; i < GuideLinks.Length; i++)
		{
			var (icon, th, en, page) = GuideLinks[i];
			LinkRow(g, new Rectangle(x, num + i * S(54f), width, S(54f)), "guide" + i, icon, L.T(th, en), GuideUrl(page), i < GuideLinks.Length - 1);
		}
	}

	private void DrawCommunity(Graphics g, Rectangle pane)
	{
		int x = pane.X;
		int width = pane.Width;
		int num = pane.Y + S(6f);
		PageTitle(g, x, num, width, L.T("คอมม\u0e39น\u0e34ต\u0e35\u0e49", "Community"), L.T("ค\u0e38ยก\u0e31บผ\u0e39\u0e49เล\u0e48นคนอ\u0e37\u0e48น แจ\u0e49งบ\u0e31\u0e4aก และร\u0e31บข\u0e48าวก\u0e48อนใคร", "Chat with other players, report bugs and get news first"));
		num += S(84f);
		LinkRow(g, new Rectangle(x, num, width, S(54f)), "discord", "brand-discord", "Discord", "https://discord.gg/RMfeg6Drbu", line: true);
		LinkRow(g, new Rectangle(x, num + S(54f), width, S(54f)), "facebook", "brand-facebook", L.T("กล\u0e38\u0e48ม Facebook", "Facebook group"), "https://www.facebook.com/share/g/1BZp6apPM8/", line: false);
	}

	private void LinkRow(Graphics g, Rectangle row, string id, string icon, string text, string url, bool line)
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		bool flag = AddHit(row, id, delegate
		{
			OpenUrl(url);
		});
		if (flag)
		{
			Ui.Fill(g, Rectangle.Inflate(row, S(8f), -S(4f)), S(8f), Ui.K2);
		}
		if (line)
		{
			Pen val = new Pen(Ui.K3);
			try
			{
				g.DrawLine(val, row.X, row.Bottom - 1, row.Right, row.Bottom - 1);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		Icons.Draw(g, icon, new Point(row.X + S(12f), row.Y + row.Height / 2), S(21f), Ui.Yellow);
		Ui.Draw(g, text, F(15.5f), new Rectangle(row.X + S(38f), row.Y, row.Width - S(80f), row.Height), Ui.Text, (TextFormatFlags)4);
		Icons.Draw(g, "external-link", new Point(row.Right - S(12f), row.Y + row.Height / 2), S(17f), flag ? Ui.TextDim : Ui.TextMute);
	}

	private void DrawBottomBar(Graphics g, Rectangle bar)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		SolidBrush val = new SolidBrush(Ui.K1);
		try
		{
			g.FillRectangle((Brush)(object)val, bar);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Pen val2 = new Pen(Ui.K3);
		try
		{
			g.DrawLine(val2, bar.X, bar.Y, bar.Right, bar.Y);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		int num = bar.Y + bar.Height / 2;
		Rectangle rectangle = new Rectangle(bar.Right - S(28f) - S(224f), num - S(26f), S(224f), S(52f));
		_mainRect = Rectangle.Inflate(rectangle, 2, 2);
		DrawMainButton(g, rectangle);
		Rectangle r = new Rectangle(rectangle.X - S(10f) - S(46f), num - S(23f), S(46f), S(46f));
		bool flag = _game != null && Directory.Exists(_game.Dir);
		bool flag2 = AddHit(r, "folder", OpenFolder, flag);
		if (flag2)
		{
			Ui.Fill(g, r, S(9f), Ui.K2);
		}
		Ui.Stroke(g, r, S(9f), Ui.K3);
		Icons.Draw(g, "folder", new Point(r.X + r.Width / 2, r.Y + r.Height / 2), S(19f), (!flag) ? Ui.A(Ui.TextMute, 120) : (flag2 ? Ui.Text : Ui.TextDim));
		Font f = F(14.5f);
		string s = L.T("ตรวจไฟล\u0e4c", "Verify");
		int num2 = S(16f) + S(18f) + S(8f) + Ui.Measure(s, f).Width + S(18f);
		Rectangle r2 = new Rectangle(r.X - S(10f) - num2, r.Y, num2, S(46f));
		bool flag3 = Installed && Idle;
		bool flag4 = AddHit(r2, "verify", delegate
		{
			RefreshAsync();
		}, flag3);
		if (flag4)
		{
			Ui.Fill(g, r2, S(9f), Ui.K2);
		}
		Ui.Stroke(g, r2, S(9f), Ui.K3);
		Color color = ((!flag3) ? Ui.A(Ui.TextMute, 140) : (flag4 ? Ui.Text : Ui.TextDim));
		Icons.Draw(g, "shield-check", new Point(r2.X + S(16f) + S(9f), r2.Y + r2.Height / 2), S(18f), color);
		Ui.Draw(g, s, f, new Rectangle(r2.X + S(16f) + S(18f) + S(8f), r2.Y, num2 - S(42f), r2.Height), color, (TextFormatFlags)4);
		int num3 = bar.X + S(28f);
		int num4 = r2.X - S(24f) - num3;
		string text = ((_busy != null) ? _progressText() : "");
		Font f2 = F(13.5f);
		int num5 = ((text.Length > 0) ? (Ui.Measure(text, f2).Width + S(4f)) : 0);
		if (num5 > 0)
		{
			Ui.Draw(g, text, f2, new Rectangle(num3 + num4 - num5, num - S(22f), num5, S(22f)), Ui.TextMute, (TextFormatFlags)6);
		}
		string s2 = _status();
		Color c = _statusColor;
		if (NeedLogin && _busy == null && !_checking && !_launching && Installed && !Blocked && _statusColor != Ui.Bad && _statusColor != Ui.Yellow)
		{
			s2 = L.T("ต\u0e49องเข\u0e49าส\u0e39\u0e48ระบบก\u0e48อนเข\u0e49าเกม · ไฟล\u0e4cเกมพร\u0e49อม v" + _game.Version, "Sign in to play · game files ready v" + _game.Version);
			c = Ui.TextDim;
		}
		else if (_restoring && _busy == null && !_checking && !_launching)
		{
			s2 = L.T("กำล\u0e31งเข\u0e49าส\u0e39\u0e48ระบบด\u0e49วย Durango ID ท\u0e35\u0e48จำไว\u0e49…", "Signing in with your remembered Durango ID…");
			c = Ui.TextDim;
		}
		Ui.Draw(g, s2, f2, new Rectangle(num3, num - S(22f), num4 - num5 - ((num5 > 0) ? S(12f) : 0), S(22f)), c, (TextFormatFlags)32772);
		Rectangle rectangle2 = new Rectangle(num3, num + S(8f), num4, S(6f));
		_progressRect = ((_busy != null) ? rectangle2 : Rectangle.Empty);
		DrawProgress(g, rectangle2);
	}

	private void DrawMainButton(Graphics g, Rectangle r)
	{
		Action action = null;
		float spin = 0f;
		string icon;
		string text;
		if (_launching)
		{
			icon = "loader-2";
			text = L.T("กำล\u0e31งเป\u0e34ด", "Starting");
			spin = _animT * 12;
		}
		else if (_game == null || _checking)
		{
			icon = "loader-2";
			text = L.T("กำล\u0e31งตรวจ…", "Checking…");
			spin = _animT * 12;
		}
		else if (_busy != null && _phase == "download")
		{
			icon = "x";
			text = L.T("ยกเล\u0e34ก", "Cancel");
			action = delegate
			{
				_cts?.Cancel();
			};
		}
		else if (_busy != null)
		{
			bool num = _hover == "main";
			icon = (num ? "x" : "loader-2");
			text = (num ? L.T("ยกเล\u0e34ก", "Cancel") : L.T("กำล\u0e31งตรวจ…", "Checking…"));
			spin = ((!num) ? (_animT * 12) : 0);
			action = delegate
			{
				_cts?.Cancel();
			};
		}
		else if (!Installed)
		{
			icon = "download";
			text = L.T("ต\u0e34ดต\u0e31\u0e49งเกม", "Install game");
			action = delegate
			{
				RefreshAsync();
			};
		}
		else if (Blocked)
		{
			icon = "download";
			text = L.T("อ\u0e31ปเดต", "Update");
			action = delegate
			{
				RefreshAsync();
			};
		}
		else if (_restoring)
		{
			icon = "loader-2";
			text = L.T("กำล\u0e31งเข\u0e49าส\u0e39\u0e48ระบบ…", "Signing in…");
			spin = _animT * 12;
		}
		else
		{
			if (NeedLogin)
			{
				bool flag = AddHit(r, "main", delegate
				{
					ShowPage(Page.Home);
					if (_scr != Auth.Login)
					{
						GoAuth(Auth.Login);
					}
					_focusNext = (Locked ? null : ((((Control)_boxes["lu"]).Text.Length > 0) ? "lp" : "lu"));
					((Control)this).Invalidate();
				});
				Ui.Fill(g, r, S(10f), Ui.K2);
				Ui.Stroke(g, r, S(10f), flag ? K4 : Ui.K3);
				DrawMainContent(g, r, "lock", L.T("เข\u0e49าเกม", "Play"), Ui.TextMute, 0f);
				return;
			}
			icon = "player-play";
			text = L.T("เข\u0e49าเกม", "Play");
			action = (PlayEnabled ? new Action(OnPlay) : null);
		}
		bool flag2 = AddHit(r, "main", action, action != null);
		Ui.Fill(g, r, S(10f), flag2 ? Ui.YellowHover : Ui.Yellow);
		DrawMainContent(g, r, icon, text, Ui.OnYellow, spin);
	}

	private void DrawMainContent(Graphics g, Rectangle r, string icon, string text, Color fg, float spin)
	{
		Font f = F(17.5f, medium: true);
		int width = Ui.Measure(text, f).Width;
		int num = S(20f);
		int num2 = S(9f);
		int num3 = r.X + (r.Width - (num + num2 + width)) / 2;
		Icons.Draw(g, icon, new RectangleF(num3, (float)r.Y + (float)(r.Height - num) / 2f, num, num), fg, 2.2f, spin);
		Ui.Draw(g, text, f, new Rectangle(num3 + num + num2, r.Y, width + S(4f), r.Height), fg, (TextFormatFlags)4);
	}

	private void DrawProgress(Graphics g, Rectangle bar)
	{
		Ui.Fill(g, bar, (float)bar.Height / 2f, Ui.K3);
		Rectangle r;
		if (_busy == null)
		{
			r = ((_synced && PlayEnabled) ? bar : Rectangle.Empty);
		}
		else if (_progress >= 0.0)
		{
			r = new Rectangle(bar.X, bar.Y, Math.Max(bar.Height, (int)((double)bar.Width * _progress)), bar.Height);
		}
		else
		{
			int num = bar.Width / 4;
			int num2 = _animT * S(8f) % (bar.Width + num) - num;
			r = Rectangle.Intersect(bar, new Rectangle(bar.X + num2, bar.Y, num, bar.Height));
		}
		if (r.Width > 0)
		{
			Ui.Fill(g, r, (float)bar.Height / 2f, Ui.Yellow);
		}
	}

	private static string FormatDate(string date)
	{
		if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return date;
		}
		return L.T($"{result.Day} {ThaiMonths[result.Month - 1]} {result.Year}", $"{EnMonths[result.Month - 1]} {result.Day}, {result.Year}");
	}

	private static string ShortDate(string date)
	{
		if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return date;
		}
		return L.T($"{result.Day} {ThaiMonths[result.Month - 1]}", $"{EnMonths[result.Month - 1]} {result.Day}");
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			((Component)(object)_anim).Dispose();
			((Component)(object)_clock).Dispose();
			((Component)(object)_onlineTimer).Dispose();
			DisposeTrade();
			Bitmap? hero = _hero;
			if (hero != null)
			{
				((Image)hero).Dispose();
			}
			Bitmap? logo = _logo;
			if (logo != null)
			{
				((Image)logo).Dispose();
			}
			Bitmap? heroCache = _heroCache;
			if (heroCache != null)
			{
				((Image)heroCache).Dispose();
			}
			foreach (Font value in _fonts.Values)
			{
				value.Dispose();
			}
		}
		base.Dispose(disposing);
	}

	private void InitTrade()
	{
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Expected O, but got Unknown
		(string, int)[] array = new(string, int)[9]
		{
			("q_item", 40),
			("chat_in", 300),
			("q_market", 40),
			("t_chat", 300),
			("o_player", 24),
			("o_give_t", 15),
			("o_want_t", 15),
			("o_msg", 120),
			("au_amt", 15)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, int) tuple = array[i];
			string item = tuple.Item1;
			int item2 = tuple.Item2;
			TextBox val = new TextBox
			{
				BorderStyle = (BorderStyle)0,
				BackColor = Ui.K2,
				ForeColor = Ui.Text,
				Visible = false,
				Tag = false,
				MaxLength = item2,
				ShortcutsEnabled = true
			};
			string n = item;
			((Control)val).TextChanged += delegate
			{
				OnTradeTextChanged(n);
				((Control)this).Invalidate();
			};
			((Control)val).GotFocus += delegate
			{
				((Control)this).Invalidate();
			};
			((Control)val).LostFocus += delegate
			{
				((Control)this).Invalidate();
			};
			((Control)this).Controls.Add((Control)(object)val);
			_boxes[item] = val;
		}
		InitCoupon();
		_poll.Tick += delegate
		{
			TradeTick();
		};
		_poll.Start();
	}

	private void DisposeTrade()
	{
		((Component)(object)_poll).Dispose();
		_icons.Dispose();
	}

	private void TradeTick()
	{
		if (((Control)this).IsDisposed)
		{
			return;
		}
		CalTick();
		if (_session != _ltSession)
		{
			_ltSession = _session;
			ResetTradeState();
			if (_session != null)
			{
				_sumNext = DateTime.MinValue;
				_ltfAt = default(DateTime);
			}
		}
		if (_lt == null || _lt.Base != _acct.LauncherApi.TrimEnd('/') || _lt.Server != _acct.Server)
		{
			_lt = new LauncherTradeApi(_acct.LauncherApi, _acct.Server, () => _session?.RefreshToken);
			_icons.Configure(_acct.LauncherApi, (Action)((Control)this).Invalidate);
			_ltfAt = default(DateTime);
		}
		if (!LoggedIn)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		if (!_ltfBusy && utcNow - _ltfAt > TimeSpan.FromMilliseconds(300000L, 0L))
		{
			RefreshLauncherFeaturesAsync();
		}
		if (_ltf.Any)
		{
			if (!_sumBusy && utcNow >= _sumNext)
			{
				PollSummaryAsync();
			}
			if (_page == Page.Chars && _ltf.Items && !_itemBusy && utcNow >= _itemNext)
			{
				PollItemsAsync();
			}
			if (_ltf.Chat && !_chatBusy && utcNow >= _chatNext)
			{
				PollChatAsync();
			}
			if (_page == Page.Trade && _ltf.Trade && _gate.CanPoll && !_offerBusy && utcNow >= _offerNext)
			{
				PollOffersAsync();
			}
			if (_page == Page.Trade && _ltf.Trade && _gate.CanPoll && SelectedOffer != null && !_ochatBusy && utcNow >= _ochatNext)
			{
				PollOfferChatAsync();
			}
			if (_od != null)
			{
				OfferDraftTick();
			}
			if (_ltStatus != null && _sumBack.Failures == 0 && _itemBack.Failures == 0 && _chatBack.Failures == 0 && _offerBack.Failures == 0 && _ltStatusColor == Ui.Yellow)
			{
				_ltStatus = null;
				((Control)this).Invalidate();
			}
		}
	}

	private void ResetTradeState()
	{
		_ltGen++;
		_summary = null;
		_gate.Reset();
		_chars = null;
		_items = null;
		_itemsFor = null;
		_itemSel = null;
		_itemsErr = null;
		_prices.Clear();
		_listedPrice.Clear();
		_listedFor = null;
		_charSel = (_speaker = (_buyer = (_trader = null)));
		_chatMsgs.Clear();
		_chatSys.Clear();
		_chatSince = (_chatSeen = 0L);
		_chatInfo = null;
		_chatErr = null;
		_chatScroll = 0;
		_market = null;
		_mine = null;
		_au = null;
		_mSel = null;
		_mErr = null;
		_buy.Done();
		_mSkip = 0;
		_offers.Clear();
		_tSel = null;
		_tChat.Clear();
		_tChatFor = null;
		_tChatSince = 0L;
		_tErr = null;
		_accept.Done();
		_od = null;
		_dd = null;
		ResetCoupon();
		_sumBack.Ok();
		_itemBack.Ok();
		_chatBack.Ok();
		_offerBack.Ok();
		_ochatBack.Ok();
		_sumNext = (_itemNext = (_chatNext = (_offerNext = (_ochatNext = DateTime.MinValue))));
		_ltStatus = null;
		if (TradePage)
		{
			ShowPage(Page.Home);
		}
		((Control)this).Invalidate();
	}

	private async Task RefreshLauncherFeaturesAsync()
	{
		if (_lt == null)
		{
			return;
		}
		_ltfBusy = true;
		_ltfAt = DateTime.UtcNow;
		int gen = _ltGen;
		try
		{
			LauncherFeatures launcherFeatures = await _lt.FeaturesAsync();
			if (gen == _ltGen && !((Control)this).IsDisposed && launcherFeatures != _ltf)
			{
				_ltf = launcherFeatures;
				Log.Write($"launcher features: items={launcherFeatures.Items} chat={launcherFeatures.Chat} market_buy={launcherFeatures.MarketBuy} trade={launcherFeatures.Trade} coupon={launcherFeatures.Coupon} api={launcherFeatures.Api}");
				if (!PageAllowed(_page))
				{
					ShowPage(Page.Home);
				}
				((Control)this).Invalidate();
			}
		}
		finally
		{
			_ltfBusy = false;
		}
	}

	private bool PageAllowed(Page p)
	{
		return p switch
		{
			Page.Chars => TradeMenu.ShowChars(LoggedIn, _ltf), 
			Page.Chat => TradeMenu.ShowChat(LoggedIn, _ltf), 
			Page.Market => TradeMenu.ShowMarket(LoggedIn, _ltf), 
			Page.Trade => TradeMenu.ShowTrade(LoggedIn, _ltf), 
			Page.Calendar => !_cal.Hidden, 
			Page.Coupon => TradeMenu.ShowCoupon(LoggedIn, _ltf), 
			_ => true, 
		};
	}

	private bool HandleTradeError(ApiError e, string feature)
	{
		if (e.IsAuthDead)
		{
			SignOutLocal(new BannerInfo("relogin"), _session?.Username);
			return true;
		}
		if (e.Code == "feature_disabled")
		{
			_ltf = _ltf.Without(e.Feature ?? feature);
			if (!PageAllowed(_page))
			{
				ShowPage(Page.Home);
			}
			Toast(e.Message, e.Message, "info-circle", Ui.PurpleLight);
			((Control)this).Invalidate();
			return true;
		}
		switch (SupporterError.Of(e))
		{
		case SupporterError.Kind.LockMe:
			LockTradeNow(e.Message);
			return true;
		case SupporterError.Kind.Lost:
		{
			string text = SupporterError.LostText(e);
			_accept.Done();
			if (_od != null)
			{
				CloseOffer();
			}
			_tErr = text;
			_offerNext = (_ochatNext = (_sumNext = DateTime.MinValue));
			LoadCharactersAsync();
			ToastT(text, "alert-circle", Ui.Yellow);
			((Control)this).Invalidate();
			return true;
		}
		default:
			return false;
		}
	}

	private static string TradeErrText(ApiError e)
	{
		if (SupporterError.Of(e) != SupporterError.Kind.Them)
		{
			return e.Message;
		}
		return SupporterError.ThemText;
	}

	private void LockTradeNow(string? why)
	{
		bool locked = _gate.Locked;
		_gate.DeniedMe();
		_accept.Done();
		if (_od != null)
		{
			CloseOffer();
		}
		_offers.Clear();
		_tSel = null;
		_tChat.Clear();
		_tChatFor = null;
		_tChatSince = 0L;
		_tErr = null;
		_tConfirm = null;
		if (_summary != null)
		{
			_summary = _summary with
			{
				OffersIn = 0,
				OffersChanged = 0
			};
		}
		if (why != null && TradeMenu.ShowTrade(LoggedIn, _ltf) && _page != Page.Trade)
		{
			ShowPage(Page.Trade);
		}
		if (!locked && why != null && why.Length > 0)
		{
			ToastT(why, "lock", Ui.Yellow);
		}
		((Control)this).Invalidate();
	}

	private void NoteBackoff(Backoff b, ApiError e)
	{
		int num = b.Fail(e.RetryAfter);
		_ltStatus = ((e.Code == "rate_limited") ? L.T($"เซ\u0e34ร\u0e4cฟขอให\u0e49ชะลอ — ลองใหม\u0e48ใน {num / 1000} ว\u0e34", $"Server asked to slow down — retrying in {num / 1000} s") : L.T($"ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49 — ลองใหม\u0e48ใน {num / 1000} ว\u0e34", $"Cannot reach the server — retrying in {num / 1000} s"));
		_ltStatusColor = Ui.Yellow;
	}

	private async Task PollSummaryAsync()
	{
		if (_lt == null)
		{
			return;
		}
		_sumBusy = true;
		int gen = _ltGen;
		try
		{
			TradeSummary tradeSummary = await _lt.SummaryAsync(_chatSeen);
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				_summary = tradeSummary;
				bool locked = _gate.Locked;
				_gate.FromSummary(tradeSummary);
				if (_gate.Locked && !locked)
				{
					LockTradeNow(null);
				}
				else if (!_gate.Locked && locked)
				{
					_offerNext = (_ochatNext = DateTime.MinValue);
				}
				_sumNext = DateTime.UtcNow.AddMilliseconds(_sumBack.Ok());
				if (_chars == null && !_charsBusy)
				{
					LoadCharactersAsync();
				}
				((Control)this).Invalidate();
			}
		}
		catch (ApiError apiError)
		{
			if (gen != _ltGen || ((Control)this).IsDisposed)
			{
				return;
			}
			if (HandleTradeError(apiError, ""))
			{
				_sumNext = DateTime.UtcNow.AddMilliseconds(30000.0);
				return;
			}
			_sumNext = DateTime.UtcNow.AddMilliseconds(Backoff.ShouldBackoff(apiError) ? _sumBack.Fail(apiError.RetryAfter) : _sumBack.Ok());
			if (Backoff.ShouldBackoff(apiError))
			{
				NoteBackoff(_sumBack, apiError);
			}
		}
		catch (Exception ex)
		{
			Log.Write("summary: " + ex.Message);
			_sumNext = DateTime.UtcNow.AddMilliseconds(_sumBack.Fail());
		}
		finally
		{
			_sumBusy = false;
		}
	}

	private async Task LoadCharactersAsync()
	{
		if (_lt == null || _charsBusy)
		{
			return;
		}
		_charsBusy = true;
		UpdateAnim();
		int gen = _ltGen;
		try
		{
			CharactersResult charactersResult = await _lt.CharactersAsync();
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				_chars = charactersResult;
				_charSel = CharPick.Pick(charactersResult.Characters, _charSel)?.EntityId;
				_speaker = CharPick.Pick(charactersResult.Characters, _speaker)?.EntityId;
				_buyer = CharPick.Pick(charactersResult.Characters, _buyer)?.EntityId;
				_trader = CharPick.Pick(charactersResult.Characters, _trader)?.EntityId;
				if (_page == Page.Chars)
				{
					_itemNext = DateTime.MinValue;
				}
				((Control)this).Invalidate();
			}
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				if (!HandleTradeError(apiError, "items"))
				{
					_itemsErr = apiError.Message;
				}
				((Control)this).Invalidate();
			}
		}
		finally
		{
			_charsBusy = false;
			UpdateAnim();
		}
	}

	private TradeCharacter? Char(string? id)
	{
		if (id != null)
		{
			return _chars?.Characters.FirstOrDefault((TradeCharacter c) => c.EntityId == id);
		}
		return null;
	}

	private string CharName(string? id)
	{
		return Char(id)?.Name ?? L.T("เล\u0e37อกต\u0e31วละคร", "Choose character");
	}

	private void TradeOnPage(Page p)
	{
		_dd = null;
		if (p == Page.Chars)
		{
			_itemNext = DateTime.MinValue;
			if (_chars == null)
			{
				LoadCharactersAsync();
			}
		}
		if (p == Page.Chat)
		{
			_chatNext = DateTime.MinValue;
			_chatScroll = 0;
			MarkChatSeen();
		}
		if (p == Page.Market)
		{
			_mSkip = 0;
			SearchMarketAsync();
			if (_chars == null)
			{
				LoadCharactersAsync();
			}
		}
		if (p == Page.Trade)
		{
			_offerNext = DateTime.MinValue;
			_ochatNext = DateTime.MinValue;
			if (_chars == null)
			{
				LoadCharactersAsync();
			}
		}
		if (p == Page.Calendar)
		{
			CalOnPage();
		}
		if (p == Page.Coupon)
		{
			CouponOnPage();
		}
		if (p != Page.Chars && _page == Page.Chars)
		{
			_focusNext = null;
		}
	}

	private async Task PollItemsAsync()
	{
		if (_lt == null)
		{
			return;
		}
		string id = _charSel;
		if (id == null)
		{
			_itemNext = DateTime.UtcNow.AddSeconds(2.0);
			return;
		}
		_itemBusy = true;
		UpdateAnim();
		int gen = _ltGen;
		try
		{
			ItemsResult itemsResult = await _lt.ItemsAsync(id);
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				if (id == _charSel)
				{
					_items = itemsResult;
					_itemsFor = id;
					_itemsErr = null;
				}
				_itemNext = DateTime.UtcNow.AddMilliseconds(_itemBack.Ok());
				if (_itemSel != null && !itemsResult.Bag.Concat(itemsResult.Equipped).Any((TradeItem i) => i.Id == _itemSel))
				{
					_itemSel = null;
				}
				((Control)this).Invalidate();
			}
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "items"))
			{
				_itemsErr = apiError.Message;
				bool flag = Backoff.ShouldBackoff(apiError);
				_itemNext = DateTime.UtcNow.AddMilliseconds(flag ? _itemBack.Fail(apiError.RetryAfter) : 30000);
				if (flag)
				{
					NoteBackoff(_itemBack, apiError);
				}
				((Control)this).Invalidate();
			}
		}
		finally
		{
			_itemBusy = false;
			UpdateAnim();
		}
	}

	private void SelectChar(string id)
	{
		if (!(_charSel == id))
		{
			_charSel = id;
			_itemSel = null;
			_itemScroll = 0;
			if (_itemsFor != id)
			{
				_items = null;
			}
			_itemNext = DateTime.MinValue;
			((Control)this).Invalidate();
		}
	}

	private void SelectItem(TradeItem it)
	{
		_itemSel = it.Id;
		string key = it.Proto + "|" + it.Level;
		if (!_prices.ContainsKey(key))
		{
			_prices[key] = null;
			LoadPriceAsync(it, key);
		}
		if (Listed.NeedMine(it) && _charSel != null && _listedFor != _charSel)
		{
			LoadListedAsync(_charSel);
		}
		((Control)this).Invalidate();
	}

	private async Task LoadListedAsync(string eid)
	{
		if (_lt == null || !_ltf.MarketBuy)
		{
			return;
		}
		_listedFor = eid;
		int gen = _ltGen;
		try
		{
			MarketMine marketMine = await _lt.MarketMineAsync(eid);
			if (gen != _ltGen || ((Control)this).IsDisposed)
			{
				return;
			}
			MarketProduct[] selling = marketMine.Selling;
			foreach (MarketProduct marketProduct in selling)
			{
				if (marketProduct.Item.Id.Length > 0)
				{
					_listedPrice[marketProduct.Item.Id] = marketProduct.Price;
				}
			}
			((Control)this).Invalidate();
		}
		catch (ApiError e)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				_listedFor = null;
				HandleTradeError(e, "market_buy");
			}
		}
		catch (Exception ex)
		{
			_listedFor = null;
			Log.Write("market/mine (ราคาท\u0e35\u0e48ลงไว\u0e49): " + ex.Message);
		}
	}

	private async Task LoadPriceAsync(TradeItem it, string key)
	{
		if (_lt == null)
		{
			return;
		}
		int gen = _ltGen;
		try
		{
			PriceResult value = await _lt.PriceAsync(it.Proto, it.Level);
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				_prices[key] = value;
				((Control)this).Invalidate();
			}
		}
		catch (ApiError e)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				_prices.Remove(key);
				HandleTradeError(e, "items");
			}
		}
		catch (Exception ex)
		{
			Log.Write("market/price: " + ex.Message);
			_prices.Remove(key);
		}
	}

	private async Task PollChatAsync()
	{
		if (_lt == null)
		{
			return;
		}
		_chatBusy = true;
		int gen = _ltGen;
		bool onPage = _page == Page.Chat;
		try
		{
			ChatHistory chatHistory = await _lt.ChatHistoryAsync(_chatSince);
			if (gen != _ltGen || ((Control)this).IsDisposed)
			{
				return;
			}
			_chatInfo = chatHistory;
			if (((TextBoxBase)_boxes["chat_in"]).MaxLength != Math.Max(1, chatHistory.MaxLen))
			{
				((TextBoxBase)_boxes["chat_in"]).MaxLength = Math.Max(1, chatHistory.MaxLen);
			}
			if (chatHistory.Messages.Length != 0)
			{
				HashSet<long> known = _chatMsgs.Select((ChatMsg m) => m.Id).ToHashSet();
				_chatMsgs.AddRange(chatHistory.Messages.Where((ChatMsg m) => !known.Contains(m.Id)));
				_chatMsgs.Sort((ChatMsg a, ChatMsg b) => (a.T == b.T) ? a.Id.CompareTo(b.Id) : a.T.CompareTo(b.T));
				if (_chatMsgs.Count > 300)
				{
					_chatMsgs.RemoveRange(0, _chatMsgs.Count - 300);
				}
			}
			if (chatHistory.Last > _chatSince)
			{
				_chatSince = chatHistory.Last;
			}
			else if (chatHistory.Messages.Length != 0)
			{
				_chatSince = Math.Max(_chatSince, chatHistory.Messages.Max((ChatMsg m) => m.T));
			}
			if (_page == Page.Chat && _chatScroll == 0)
			{
				MarkChatSeen();
			}
			_chatBack.Ok();
			_chatNext = DateTime.UtcNow.AddMilliseconds((_page == Page.Chat) ? 3000 : 15000);
			((Control)this).Invalidate();
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "chat"))
			{
				bool flag = Backoff.ShouldBackoff(apiError);
				_chatNext = DateTime.UtcNow.AddMilliseconds(flag ? _chatBack.Fail(apiError.RetryAfter) : (onPage ? 3000 : 15000));
				if (flag)
				{
					NoteBackoff(_chatBack, apiError);
				}
				if (onPage)
				{
					_chatErr = apiError.Message;
					((Control)this).Invalidate();
				}
			}
		}
		catch (Exception ex)
		{
			Log.Write("chat/history: " + ex.Message);
			_chatNext = DateTime.UtcNow.AddMilliseconds(_chatBack.Fail());
		}
		finally
		{
			_chatBusy = false;
		}
	}

	private void MarkChatSeen()
	{
		long num;
		if (_chatMsgs.Count <= 0)
		{
			num = _chatSince;
		}
		else
		{
			List<ChatMsg> chatMsgs = _chatMsgs;
			num = chatMsgs[chatMsgs.Count - 1].T;
		}
		long num2 = num;
		if (num2 > _chatSeen)
		{
			_chatSeen = num2;
			if (_summary != null)
			{
				_summary = _summary with
				{
					ChatUnread = 0
				};
			}
		}
	}

	private async Task SendChatAsync()
	{
		if (_lt == null || _chatSending)
		{
			return;
		}
		string text = ((Control)_boxes["chat_in"]).Text.Trim();
		string speaker = _speaker;
		if (text.Length == 0)
		{
			return;
		}
		if (speaker == null)
		{
			_chatErr = L.T("เล\u0e37อกต\u0e31วละครท\u0e35\u0e48จะพ\u0e39ดก\u0e48อน", "Choose a character to speak as first");
			((Control)this).Invalidate();
		}
		else
		{
			if (DateTime.UtcNow < _chatCdUntil)
			{
				return;
			}
			_chatSending = true;
			_chatErr = null;
			UpdateAnim();
			int gen = _ltGen;
			try
			{
				await _lt.ChatSayAsync(speaker, text);
				if (gen == _ltGen && !((Control)this).IsDisposed)
				{
					((Control)_boxes["chat_in"]).Text = "";
					_chatCdUntil = DateTime.UtcNow.AddSeconds(_chatInfo?.Cooldown ?? 10);
					_chatNext = DateTime.UtcNow.AddMilliseconds(400.0);
					_focusNext = "chat_in";
				}
			}
			catch (ApiError apiError)
			{
				if (gen != _ltGen || ((Control)this).IsDisposed || HandleTradeError(apiError, "chat"))
				{
					return;
				}
				_chatErr = apiError.Message;
				if (apiError.Code == "cooldown")
				{
					int? wait = apiError.Wait;
					if (wait.HasValue)
					{
						int valueOrDefault = wait.GetValueOrDefault();
						_chatCdUntil = DateTime.UtcNow.AddSeconds(Math.Clamp(valueOrDefault, 1, 600));
					}
				}
				if (apiError.Code == "muted")
				{
					int? wait = apiError.Hours;
					if (wait.HasValue)
					{
						int valueOrDefault2 = wait.GetValueOrDefault();
						_chatErr = L.T($"ถ\u0e39ก GM ป\u0e34ดแชทอ\u0e35ก {valueOrDefault2} ชม.", $"Muted by a GM for {valueOrDefault2} more hr");
					}
				}
			}
			catch (Exception ex)
			{
				Log.Write("chat/say: " + ex);
				_chatErr = L.T("ส\u0e48งไม\u0e48สำเร\u0e47จ — ลองใหม\u0e48", "Could not send — try again");
			}
			finally
			{
				_chatSending = false;
				UpdateAnim();
				((Control)this).Invalidate();
			}
		}
	}

	private async Task ToggleChatPrefAsync()
	{
		if (_lt == null || _chatPrefBusy || _speaker == null)
		{
			return;
		}
		_chatPrefBusy = true;
		bool off = !_chatOff;
		int gen = _ltGen;
		try
		{
			bool flag = await _lt.ChatPrefAsync(_speaker, off);
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				_chatOff = flag;
				_chatSys.Add((NowUnix, flag ? L.T("ป\u0e34ดร\u0e31บแชทโลกในเกมแล\u0e49ว (/g off)", "Global chat in game turned off (/g off)") : L.T("เป\u0e34ดร\u0e31บแชทโลกแล\u0e49ว (/g on) · ข\u0e49อความจากร\u0e31นเช\u0e48อจะข\u0e36\u0e49นในเกมเป\u0e47น [G] [W] " + CharName(_speaker), "Global chat on (/g on) · launcher messages appear in game as [G] [W] " + CharName(_speaker))));
			}
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "chat"))
			{
				_chatErr = apiError.Message;
			}
		}
		finally
		{
			_chatPrefBusy = false;
			((Control)this).Invalidate();
		}
	}

	private void OnTradeTextChanged(string name)
	{
		if (name == "q_item")
		{
			_itemScroll = 0;
		}
		if (name == "o_player")
		{
			_odTyped = DateTime.UtcNow;
		}
		bool flag;
		switch (name)
		{
		case "o_give_t":
		case "o_want_t":
		case "o_msg":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag && _od != null)
		{
			_od.Err = null;
			((Control)this).Invalidate();
		}
		if (name == "chat_in")
		{
			_chatErr = null;
		}
		if (name == "coupon")
		{
			CouponTextChanged();
		}
	}

	private bool TradeSubmit()
	{
		if (_od != null)
		{
			if ((object)((ContainerControl)this).ActiveControl == _boxes["o_player"])
			{
				SearchPlayersAsync();
				return true;
			}
			if (((ContainerControl)this).ActiveControl is TextBox)
			{
				SubmitOfferAsync();
				return true;
			}
			return true;
		}
		if (!LoggedIn || !TradePage)
		{
			return false;
		}
		if (_page == Page.Chat && (object)((ContainerControl)this).ActiveControl == _boxes["chat_in"])
		{
			SendChatAsync();
			return true;
		}
		if (_page == Page.Market && (object)((ContainerControl)this).ActiveControl == _boxes["q_market"])
		{
			_mSkip = 0;
			SearchMarketAsync();
			return true;
		}
		if (_page == Page.Market && (object)((ContainerControl)this).ActiveControl == _boxes["au_amt"])
		{
			BidAsync();
			return true;
		}
		if (_page == Page.Trade && (object)((ContainerControl)this).ActiveControl == _boxes["t_chat"])
		{
			SendOfferChatAsync();
			return true;
		}
		if (_page == Page.Coupon && (object)((ContainerControl)this).ActiveControl == _boxes["coupon"])
		{
			SubmitCouponAsync();
			return true;
		}
		return TradePage;
	}

	private bool TradeWheel(MouseEventArgs e)
	{
		int num = ((e.Delta <= 0) ? 1 : (-1));
		if (_od != null)
		{
			_odScroll = Math.Max(0, _odScroll + num);
			((Control)this).Invalidate();
			return true;
		}
		if (!TradePage)
		{
			return false;
		}
		switch (_page)
		{
		case Page.Chars:
			_itemScroll = Math.Max(0, _itemScroll + num);
			break;
		case Page.Chat:
			_chatScroll = Math.Max(0, _chatScroll - num * S(60f));
			if (_chatScroll == 0)
			{
				MarkChatSeen();
			}
			break;
		case Page.Market:
			_mScroll = Math.Max(0, _mScroll + num);
			break;
		case Page.Trade:
			if (_tChatRect.Contains(e.Location))
			{
				_tChatScroll = Math.Max(0, _tChatScroll - num * S(60f));
			}
			else
			{
				_tListScroll = Math.Max(0, _tListScroll + num);
			}
			break;
		case Page.Coupon:
			if (_cpHistRect.Contains(e.Location))
			{
				_cpHistScroll = Math.Max(0, _cpHistScroll + num);
			}
			break;
		}
		((Control)this).Invalidate();
		return true;
	}

	private int DrawTradeNav(Graphics g, int x, int w, int y, int itemH, int gap)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected O, but got Unknown
		(int Chat, int Market, int Trade) tuple = TradeMenu.Badges(_summary, _gate);
		int item = tuple.Chat;
		int item2 = tuple.Market;
		int item3 = tuple.Trade;
		y += S(6f);
		Pen val = new Pen(Ui.K3);
		try
		{
			g.DrawLine(val, x, y, x + w, y);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		y += S(10f);
		if (L.En)
		{
			DrawSpaced(g, "MY ACCOUNT", F(11f), new Point(x + S(20f), y), S(16f), Ui.TextMute, S(1f));
		}
		else
		{
			Ui.Draw(g, "บ\u0e31ญช\u0e35ของฉ\u0e31น", F(11.5f), new Rectangle(x + S(20f), y, w - S(20f), S(16f)), Ui.TextMute, (TextFormatFlags)4);
		}
		y += S(16f) + S(6f);
		string[] array = TradeMenu.Order(LoggedIn, _ltf, TradeMenu.ShowCalendar(_cal));
		int limit = ((Form)this).ClientSize.Height - S(94f);
		(int ItemH, int Gap) tuple2 = TradeMenu.Fit(y, limit, array.Length, itemH, gap, S(36f));
		itemH = tuple2.ItemH;
		gap = tuple2.Gap;
		string[] array2 = array;
		foreach (string text in array2)
		{
			Rectangle r = new Rectangle(x, y, w, itemH);
			switch (text)
			{
			case "chars":
				NavItem(g, r, "nav-chars", "user", L.T("ต\u0e31วละคร", "Characters"), Page.Chars);
				break;
			case "chat":
				NavItem(g, r, "nav-chat", "message-circle", L.T("แชท", "Chat"), Page.Chat, item);
				break;
			case "calendar":
				NavItem(g, r, "nav-cal", "calendar", L.T("ปฏ\u0e34ท\u0e34นก\u0e34จกรรม", "Event calendar"), Page.Calendar, CalBadge);
				break;
			case "market":
				NavItem(g, r, "nav-market", "building-store", L.T("ตลาด", "Market"), Page.Market, item2);
				break;
			case "trade":
				NavItem(g, r, "nav-trade", "arrows-exchange", L.T("เทรด", "Trade"), Page.Trade, item3, _gate.Star, _gate.Locked);
				break;
			case "coupon":
				NavItem(g, r, "nav-coupon", "ticket", L.T("ค\u0e39ปอง", "Coupons"), Page.Coupon);
				break;
			}
			y += itemH + gap;
		}
		return y;
	}

	private void NavBadge(Graphics g, Rectangle r, int n)
	{
		if (n > 0)
		{
			string s = ((n > 99) ? "99+" : n.ToString());
			Font f = F(11f, medium: true);
			int num = Math.Max(S(20f), Ui.Measure(s, f).Width + S(12f));
			Rectangle r2 = new Rectangle(r.Right - S(16f) - num, r.Y + (r.Height - S(20f)) / 2, num, S(20f));
			Ui.Fill(g, r2, S(10f), Ui.Yellow);
			Ui.Draw(g, s, f, r2, Ui.OnYellow, (TextFormatFlags)5);
		}
	}

	private int TradeHeader(Graphics g, Rectangle pane, string title, int actW, string sub)
	{
		int num = pane.Y + S(4f);
		int width = pane.Width - actW - S(16f);
		Ui.Draw(g, title, F(26f, medium: true), new Rectangle(pane.X, num, width, S(34f)), Ui.Text, (TextFormatFlags)4);
		Ui.Draw(g, sub, F(13f), new Rectangle(pane.X, num + S(34f), width, S(20f)), Ui.TextDim, (TextFormatFlags)32772);
		string ltStatus = _ltStatus;
		if (ltStatus != null)
		{
			Ui.Draw(g, ltStatus, F(11.5f), new Rectangle(pane.X, num + S(54f), width, S(14f)), _ltStatusColor, (TextFormatFlags)32772);
		}
		return num + S(34f) + S(20f) + S(12f);
	}

	private int TradeBtnW(string? icon, string text, string kind, float fontPx = 13f)
	{
		Font f = F(fontPx, kind == "y");
		int num = ((icon != null) ? S((fontPx >= 14f) ? 18 : 16) : 0);
		return S(14f) + num + ((icon != null && text.Length > 0) ? S(7f) : 0) + ((text.Length > 0) ? Ui.Measure(text, f).Width : 0) + S(14f);
	}

	private int TradeBtn(Graphics g, Rectangle r, string id, string? icon, string text, string kind, bool enabled, Action? click, float fontPx = 13f, bool spin = false)
	{
		Font f = F(fontPx, kind == "y");
		int num = ((text.Length > 0) ? Ui.Measure(text, f).Width : 0);
		int num2 = ((icon != null) ? S((fontPx >= 14f) ? 18 : 16) : 0);
		int num3 = ((icon != null && text.Length > 0) ? S(7f) : 0);
		int num4 = ((r.Width > 0) ? r.Width : TradeBtnW(icon, text, kind, fontPx));
		r = new Rectangle(r.X, r.Y, num4, r.Height);
		bool flag = AddHit(r, id, click, enabled && click != null);
		Color color;
		if (kind == "y")
		{
			Ui.Fill(g, r, S(8f), (!enabled && !spin) ? Ui.K2 : (flag ? Ui.YellowHover : Ui.Yellow));
			if (!enabled && !spin)
			{
				Ui.Stroke(g, r, S(8f), Ui.K3);
			}
			color = ((enabled || spin) ? Ui.OnYellow : Ui.TextMute);
		}
		else if (kind == "d")
		{
			if (flag)
			{
				Ui.Fill(g, r, S(8f), Rbg);
			}
			Ui.Stroke(g, r, S(8f), enabled ? Ui.A(Ui.Bad, 110) : Ui.K3);
			color = (enabled ? Ui.Bad : Ui.A(Ui.TextMute, 140));
		}
		else
		{
			if (flag)
			{
				Ui.Fill(g, r, S(8f), Ui.K2);
			}
			Ui.Stroke(g, r, S(8f), Ui.K3);
			color = ((!enabled) ? Ui.A(Ui.TextMute, 140) : (flag ? Ui.Text : Ui.TextDim));
		}
		int num5 = r.X + Math.Max(S(6f), (r.Width - (num2 + num3 + num)) / 2);
		if (icon != null)
		{
			Icons.Draw(g, icon, new RectangleF(num5, (float)r.Y + (float)(r.Height - num2) / 2f, num2, num2), color, 2f, spin ? (_animT * 12) : 0);
		}
		if (num > 0)
		{
			Ui.Draw(g, text, f, new Rectangle(num5 + num2 + num3, r.Y, Math.Min(num + S(4f), r.Right - S(6f) - num5 - num2 - num3), r.Height), color, (TextFormatFlags)32772);
		}
		return num4;
	}

	private int ChipW(string text, string? icon = null)
	{
		return S(12f) + ((icon != null) ? S(14f) : 0) + ((icon != null && text.Length > 0) ? S(5f) : 0) + ((text.Length > 0) ? Ui.Measure(text, F(12f)).Width : 0) + S(12f);
	}

	private int Chip(Graphics g, int x, int y, string id, string text, bool on, Action? click, string? icon = null, int h = 28)
	{
		Font f = F(12f);
		int num = ((icon != null) ? S(14f) : 0);
		int num2 = ChipW(text, icon);
		Rectangle r = new Rectangle(x, y, num2, S(h));
		bool flag = AddHit(r, id, click, click != null);
		if (on)
		{
			Ui.Fill(g, r, (float)r.Height / 2f, Ui.PurpleDark);
		}
		else
		{
			Ui.Stroke(g, r, (float)r.Height / 2f, flag ? K4 : Ui.K3);
		}
		Color color = ((on || flag) ? Ui.Text : Ui.TextDim);
		if (icon != null)
		{
			Icons.Draw(g, icon, new Point(r.X + S(12f) + num / 2, r.Y + r.Height / 2), num, color);
		}
		if (text.Length > 0)
		{
			Ui.Draw(g, text, f, new Rectangle(r.X + S(12f) + num + ((icon != null) ? S(5f) : 0), r.Y, num2, r.Height), color, (TextFormatFlags)4);
		}
		return num2;
	}

	private int TagChip(Graphics g, int x, int y, string text, string kind, string? icon = null, bool right = false)
	{
		Font f = F(12f, kind == "y");
		int num = ((icon != null) ? S(13f) : 0);
		int num2 = S(9f) + num + ((icon != null) ? S(4f) : 0) + Ui.Measure(text, f).Width + S(9f);
		if (right)
		{
			x -= num2;
		}
		Rectangle r = new Rectangle(x, y, num2, S(20f));
		var (c, color) = kind switch
		{
			"g" => (Gbg, Ui.Good), 
			"r" => (Rbg, Ui.Bad), 
			"y" => (Ui.Yellow, Ui.OnYellow), 
			"m" => (Ui.K3, Ui.TextDim), 
			_ => (Ui.PurpleDark, Ui.PurpleLight), 
		};
		Ui.Fill(g, r, S(5f), c);
		if (icon != null)
		{
			Icons.Draw(g, icon, new Point(r.X + S(9f) + num / 2, r.Y + r.Height / 2), num, color);
		}
		Ui.Draw(g, text, f, new Rectangle(r.X + S(9f) + num + ((icon != null) ? S(4f) : 0), r.Y, num2, r.Height), color, (TextFormatFlags)4);
		return num2;
	}

	private void Tabs(Graphics g, int x, int y, int w, (string Id, string Text, string? Pill, string? Note, string? Icon)[] tabs, string active, Action<string> pick)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		//IL_0389: Unknown result type (might be due to invalid IL or missing references)
		//IL_0390: Expected O, but got Unknown
		Pen val = new Pen(Ui.K3);
		try
		{
			g.DrawLine(val, x, y + S(36f), x + w, y + S(36f));
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Font f = F(14f);
		Font f2 = F(11f);
		int num = x;
		for (int i = 0; i < tabs.Length; i++)
		{
			(string, string, string, string, string) tuple = tabs[i];
			bool flag = tuple.Item1 == active;
			bool flag2 = tuple.Item4 != null;
			int num2 = ((tuple.Item5 != null) ? (S(14f) + S(6f)) : 0);
			int width = Ui.Measure(tuple.Item2, f).Width;
			int num3 = ((tuple.Item3 != null) ? (S(6f) + Math.Max(S(18f), Ui.Measure(tuple.Item3, f2).Width + S(12f))) : 0);
			int num4 = ((tuple.Item4 != null) ? (S(6f) + Ui.Measure(tuple.Item4, f2).Width) : 0);
			int num5 = S(12f) + num2 + width + num3 + num4 + S(12f);
			Rectangle r = new Rectangle(num, y, num5, S(36f));
			string id = tuple.Item1;
			bool flag3 = AddHit(r, "tab-" + id, flag2 ? null : ((Action)delegate
			{
				pick(id);
			}), !flag2);
			Color color = (flag2 ? Ui.TextMute : ((flag || flag3) ? Ui.Text : Ui.TextDim));
			int num6 = r.X + S(12f);
			if (tuple.Item5 != null)
			{
				Icons.Draw(g, tuple.Item5, new Point(num6 + S(7f), r.Y + r.Height / 2), S(14f), color);
				num6 += num2;
			}
			Ui.Draw(g, tuple.Item2, f, new Rectangle(num6, r.Y, width + S(2f), r.Height), color, (TextFormatFlags)4);
			num6 += width;
			if (tuple.Item3 != null)
			{
				Rectangle r2 = new Rectangle(num6 + S(6f), r.Y + (r.Height - S(18f)) / 2, num3 - S(6f), S(18f));
				Ui.Fill(g, r2, S(9f), Ui.K3);
				Ui.Draw(g, tuple.Item3, f2, r2, Ui.TextDim, (TextFormatFlags)5);
				num6 += num3;
			}
			if (tuple.Item4 != null)
			{
				Ui.Draw(g, tuple.Item4, f2, new Rectangle(num6 + S(6f), r.Y, num4, r.Height), Ui.TextMute, (TextFormatFlags)4);
			}
			if (flag)
			{
				Pen val2 = new Pen(Ui.Yellow, (float)S(2f));
				try
				{
					g.DrawLine(val2, r.X, r.Bottom - 1, r.Right, r.Bottom - 1);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			num += num5 + S(4f);
		}
	}

	private int Money(Graphics g, int x, int y, int h, long amount, float px = 13f, bool right = false, Color? color = null)
	{
		Font f = F(px, medium: true);
		string s = TradeFmt.T(amount);
		int num = S(px + 2f);
		int num2 = num + S(5f) + Ui.Measure(s, f).Width;
		if (right)
		{
			x -= num2;
		}
		Color color2 = color ?? Ui.Yellow;
		Icons.Draw(g, "gem", new Point(x + num / 2, y + h / 2), num, color2);
		Ui.Draw(g, s, f, new Rectangle(x + num + S(5f), y, num2, h), color2, (TextFormatFlags)4);
		return num2;
	}

	private void Slot(Graphics g, Rectangle r, TradeItem? it, bool on, string? id, Action? click, bool showHeld = true, bool dim = false)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Expected O, but got Unknown
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Expected O, but got Unknown
		bool flag = r.Width < S(52f);
		bool flag2 = id != null && AddHit(r, id, click, click != null);
		if (it == null)
		{
			Pen val = new Pen(Ui.K3)
			{
				DashStyle = (DashStyle)1
			};
			try
			{
				GraphicsPath val2 = Ui.Round(new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, r.Width - 1, r.Height - 1), S(9f));
				try
				{
					g.DrawPath(val, val2);
					return;
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		Ui.Fill(g, r, S(9f), Ui.K2);
		Ui.Stroke(g, r, S(9f), on ? Ui.Yellow : (flag2 ? Ui.PurpleLight : Ui.K3), on ? 2f : 1f);
		int num = (flag ? S(32f) : S(44f));
		Rectangle rectangle = new Rectangle(r.X + (r.Width - num) / 2, r.Y + (r.Height - num) / 2, num, num);
		Bitmap val3 = _icons.Get(it);
		if (val3 != null)
		{
			if (dim)
			{
				ImageAttributes val4 = new ImageAttributes();
				try
				{
					ColorMatrix colorMatrix = new ColorMatrix
					{
						Matrix33 = 0.45f
					};
					val4.SetColorMatrix(colorMatrix);
					g.DrawImage((Image)(object)val3, rectangle, 0, 0, ((Image)val3).Width, ((Image)val3).Height, (GraphicsUnit)2, val4);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			else
			{
				g.DrawImage((Image)(object)val3, rectangle);
			}
		}
		else
		{
			Font f = F(flag ? 8.5f : 9.5f);
			Ui.DrawWrapped(g, it.Name, f, Rectangle.Inflate(r, -S(4f), -S(6f)), Ui.TextMute);
		}
		int? level = it.Level;
		if (level.HasValue)
		{
			string s = "Lv" + level.GetValueOrDefault();
			Font f2 = F(flag ? 9f : 10f);
			Ui.Draw(g, s, f2, new Rectangle(r.X + S(4f) + 1, r.Y + S(2f) + 1, S(40f), S(13f)), Color.Black, (TextFormatFlags)4);
			Ui.Draw(g, s, f2, new Rectangle(r.X + S(4f), r.Y + S(2f), S(40f), S(13f)), Ui.PurpleLight, (TextFormatFlags)4);
		}
		if (it.Count > 1)
		{
			string s2 = it.Count.ToString();
			Font f3 = F(flag ? 10f : 11f, medium: true);
			int width = Ui.Measure(s2, f3).Width;
			Ui.Draw(g, s2, f3, new Rectangle(r.Right - S(4f) - width + 1, r.Bottom - S(16f) + 1, width + S(2f), S(14f)), Color.Black, (TextFormatFlags)4);
			Ui.Draw(g, s2, f3, new Rectangle(r.Right - S(4f) - width, r.Bottom - S(16f), width + S(2f), S(14f)), Ui.Text, (TextFormatFlags)4);
		}
		if (showHeld && it.Held != null)
		{
			Ui.Fill(g, r, S(9f), Color.FromArgb(150, 12, 10, 18));
			Ui.Draw(g, L.T("พ\u0e31กไว\u0e49", "Held"), F(flag ? 9f : 10f), new Rectangle(r.X, r.Bottom - S(16f), r.Width, S(14f)), Ui.Yellow, (TextFormatFlags)5);
		}
	}

	private void DurBar(Graphics g, Rectangle r, double frac)
	{
		Ui.Fill(g, r, (float)r.Height / 2f, Ui.K3);
		int num = (int)((double)r.Width * Math.Clamp(frac, 0.0, 1.0));
		if (num > 0)
		{
			Ui.Fill(g, new Rectangle(r.X, r.Y, Math.Max(r.Height, num), r.Height), (float)r.Height / 2f, (frac < 0.5) ? Ui.Yellow : Ui.Good);
		}
	}

	private void InputBox(Graphics g, Rectangle r, string name, string placeholder, bool disabled = false, string? icon = null)
	{
		TextBox tb = _boxes[name];
		bool flag = _od != null && !name.StartsWith("o_", StringComparison.Ordinal);
		bool flag2 = ((Control)tb).Focused && !disabled && !flag;
		AddHit(r, "f-" + name, delegate
		{
			((Control)tb).Focus();
		}, !disabled && !flag);
		Ui.Fill(g, r, S(8f), Ui.K2);
		Ui.Stroke(g, r, S(8f), flag2 ? Ui.PurpleLight : Ui.K3);
		int num = r.X + S(12f);
		if (icon != null)
		{
			Icons.Draw(g, icon, new Point(num + S(7f), r.Y + r.Height / 2), S(14f), Ui.TextMute);
			num += S(20f);
		}
		if (flag)
		{
			Ui.Draw(g, (((Control)tb).Text.Length > 0) ? ((Control)tb).Text : placeholder, F(13f), new Rectangle(num, r.Y, r.Right - S(10f) - num, r.Height), (((Control)tb).Text.Length > 0) ? Ui.Text : Ui.TextMute, (TextFormatFlags)32772);
			return;
		}
		if (((Control)tb).BackColor != Ui.K2)
		{
			((Control)tb).BackColor = Ui.K2;
		}
		Font val = F((r.Height >= S(38f)) ? 14f : 13f);
		if (((Control)tb).Font != val)
		{
			((Control)tb).Font = val;
		}
		PlaceBox(name, tb, new Rectangle(num, r.Y, r.Right - S(10f) - num, r.Height), placeholder, disabled);
	}

	private int CharPickerW(string lead, string? current, long? money = null)
	{
		Font f = F(13f);
		Font f2 = F(13f, medium: true);
		return S(14f) + Ui.Measure(lead, f).Width + S(4f) + Ui.Measure(CharName(current), f2).Width + (money.HasValue ? (S(10f) + S(15f) + S(5f) + Ui.Measure(TradeFmt.T(money.Value), f2).Width) : 0) + S(8f) + S(14f) + S(12f);
	}

	private int CharPicker(Graphics g, int x, int y, int h, string id, string lead, string? current, Action<string> pick, long? money = null)
	{
		Font f = F(13f);
		Font f2 = F(13f, medium: true);
		string s = CharName(current);
		int num = CharPickerW(lead, current, money);
		Rectangle r = new Rectangle(x, y, num, h);
		TradeCharacter[] chars = _chars?.Characters ?? Array.Empty<TradeCharacter>();
		if (AddHit(r, id, (chars.Length == 0) ? null : ((Action)delegate
		{
			OpenDropdown(r, chars.Select((TradeCharacter c) => $"{c.Name} · Lv {c.Level}" + (c.Online ? L.T(" · ออนไลน\u0e4c", " · online") : "")).ToArray(), Array.FindIndex(chars, (TradeCharacter c) => c.EntityId == current), delegate(int i)
			{
				pick(chars[i].EntityId);
			});
		}), chars.Length != 0))
		{
			Ui.Fill(g, r, S(8f), Ui.K2);
		}
		Ui.Stroke(g, r, S(8f), Ui.K3);
		int num2 = r.X + S(14f);
		Ui.Draw(g, lead, f, new Rectangle(num2, r.Y, S(300f), r.Height), Ui.TextDim, (TextFormatFlags)4);
		num2 += Ui.Measure(lead, f).Width + S(4f);
		Ui.Draw(g, s, f2, new Rectangle(num2, r.Y, S(300f), r.Height), Ui.Text, (TextFormatFlags)4);
		num2 += Ui.Measure(s, f2).Width;
		if (money.HasValue)
		{
			Ui.Draw(g, " · ", f, new Rectangle(num2, r.Y, S(20f), r.Height), Ui.TextDim, (TextFormatFlags)4);
			num2 += S(10f);
			num2 += Money(g, num2, r.Y, r.Height, money.Value) + S(0f);
		}
		Icons.Draw(g, "chevron-down", new Point(r.Right - S(12f) - S(7f), r.Y + r.Height / 2), S(14f), Ui.TextDim);
		return num;
	}

	private void OpenDropdown(Rectangle anchor, string[] items, int sel, Action<int> pick)
	{
		_dd = (anchor, items, sel, pick);
		((Control)this).Invalidate();
	}

	private void DrawDropdown(Graphics g)
	{
		(Rectangle, string[], int, Action<int>)? dd = _dd;
		if (!dd.HasValue)
		{
			return;
		}
		(Rectangle Anchor, string[] Items, int Sel, Action<int> Pick) dd2 = dd.GetValueOrDefault();
		AddHit(((Control)this).ClientRectangle, "dd-bg", delegate
		{
			_dd = null;
			((Control)this).Invalidate();
		});
		Font f = F(13f);
		int num = S(34f);
		int num2 = Math.Max(dd2.Anchor.Width, dd2.Items.Max((string i) => Ui.Measure(i, f).Width) + S(40f));
		int num3 = num * dd2.Items.Length + S(8f);
		int val = ((dd2.Anchor.Bottom + S(4f) + num3 > ((Form)this).ClientSize.Height - S(8f)) ? (dd2.Anchor.Y - S(4f) - num3) : (dd2.Anchor.Bottom + S(4f)));
		Rectangle r = new Rectangle(Math.Min(dd2.Anchor.X, ((Form)this).ClientSize.Width - num2 - S(8f)), Math.Max(S(8f), val), num2, num3);
		Ui.Card(g, r, S(10f), Ui.K1, K4);
		for (int num4 = 0; num4 < dd2.Items.Length; num4++)
		{
			Rectangle r2 = new Rectangle(r.X + S(4f), r.Y + S(4f) + num4 * num, r.Width - S(8f), num);
			int idx = num4;
			bool flag = AddHit(r2, "dd-" + num4, delegate
			{
				_dd = null;
				dd2.Pick(idx);
				((Control)this).Invalidate();
			});
			if (flag)
			{
				Ui.Fill(g, r2, S(7f), Ui.K2);
			}
			if (num4 == dd2.Sel)
			{
				Icons.Draw(g, "check", new Point(r2.X + S(14f), r2.Y + num / 2), S(14f), Ui.Yellow);
			}
			Ui.Draw(g, dd2.Items[num4], f, new Rectangle(r2.X + S(28f), r2.Y, r2.Width - S(32f), num), (num4 == dd2.Sel || flag) ? Ui.Text : Ui.TextDim, (TextFormatFlags)32772);
		}
	}

	private void ToastT(string text, string icon = "circle-check", Color? color = null)
	{
		Toast(text, text, icon, color);
	}

	private void Muted(Graphics g, Rectangle r, string text)
	{
		Ui.DrawWrapped(g, text, F(13.5f), new Rectangle(r.X + S(10f), r.Y + Math.Max(0, (r.Height - S(44f)) / 2), r.Width - S(20f), S(44f)), Ui.TextMute);
	}

	private static string SrcIcon(string src)
	{
		if (!(src == "web"))
		{
			if (src == "launcher")
			{
				return "device-desktop";
			}
			return "device-gamepad";
		}
		return "world";
	}

	private void DrawChars(Graphics g, Rectangle pane)
	{
		long nowUnix = NowUnix;
		string text = ((_items != null && _items.SyncedAt > 0) ? L.T(" · ซ\u0e34งก\u0e4cจากเซ\u0e34ร\u0e4cฟล\u0e48าส\u0e38ด " + TradeFmt.Clock(_items.SyncedAt, nowUnix), " · last synced " + TradeFmt.Clock(_items.SyncedAt, nowUnix)) : "");
		int num = TradeBtnW("refresh", L.T("ร\u0e35เฟรช", "Refresh"), "");
		int num2 = TradeHeader(g, pane, L.T("ต\u0e31วละครและไอเทม", "Characters & items"), num, L.T("เฉพาะของท\u0e35\u0e48อย\u0e39\u0e48ก\u0e31บต\u0e31วละคร (กระเป\u0e4bา + ท\u0e35\u0e48สวมใส\u0e48) ไม\u0e48รวมกล\u0e48อง/บ\u0e49าน", "Only what the character carries (bag + equipped), not boxes/houses") + text + L.T(" · ซ\u0e37\u0e49อ/เทรดได\u0e49แม\u0e49ต\u0e31วกำล\u0e31งเล\u0e48นอย\u0e39\u0e48 (ของเข\u0e49าจดหมาย)", " · buy/trade works while playing (items go to mail)"));
		TradeBtn(g, new Rectangle(pane.Right - num, pane.Y + S(22f), num, S(34f)), "lt-refresh", (_itemBusy || _charsBusy) ? "loader-2" : "refresh", L.T("ร\u0e35เฟรช", "Refresh"), "", !_itemBusy && !_charsBusy, delegate
		{
			_itemNext = DateTime.MinValue;
			LoadCharactersAsync();
		}, 13f, _itemBusy || _charsBusy);
		int x = pane.X;
		int num3 = S(214f);
		int num4 = num2;
		TradeCharacter[] array = _chars?.Characters ?? Array.Empty<TradeCharacter>();
		if (_chars == null)
		{
			Muted(g, new Rectangle(x, num4, num3, S(80f)), _itemsErr ?? (_charsBusy ? L.T("กำล\u0e31งโหลดต\u0e31วละคร…", "Loading characters…") : L.T("ย\u0e31งไม\u0e48ม\u0e35ข\u0e49อม\u0e39ลต\u0e31วละคร", "No character data yet")));
		}
		else if (array.Length == 0)
		{
			Muted(g, new Rectangle(x, num4, num3, S(80f)), L.T("ไอด\u0e35น\u0e35\u0e49ย\u0e31งไม\u0e48ม\u0e35ต\u0e31วละครบน " + _acct.Server, "This ID has no character on " + _acct.Server));
		}
		int num5 = S(78f);
		int count = Math.Max(1, (pane.Bottom - S(60f) - num4) / (num5 + S(8f)));
		foreach (TradeCharacter item in array.Take(count))
		{
			Rectangle r = new Rectangle(x, num4, num3, num5);
			bool flag = item.EntityId == _charSel;
			string eid = item.EntityId;
			bool flag2 = AddHit(r, "lt-char-" + eid, delegate
			{
				SelectChar(eid);
			});
			Ui.Card(g, r, S(10f), Ui.K1, flag ? Ui.Yellow : (flag2 ? K4 : Ui.K3));
			Ui.Dot(g, new PointF(r.X + S(16f), r.Y + S(21f)), S(8f), item.Online ? Ui.Good : Ui.TextMute);
			int num6 = TagChip(g, r.Right - S(12f), r.Y + S(11f), "Lv " + item.Level, "m", null, right: true);
			Ui.Draw(g, item.Name, F(15f, medium: true), Rectangle.FromLTRB(r.X + S(28f), r.Y + S(8f), r.Right - S(12f) - num6 - S(6f), r.Y + S(34f)), Ui.Text, (TextFormatFlags)32772);
			Ui.Draw(g, _acct.Server, F(12f), new Rectangle(r.X + S(12f), r.Y + S(36f), r.Width - S(24f), S(18f)), Ui.TextMute, (TextFormatFlags)32772);
			Ui.Draw(g, TradeFmt.Presence(item, nowUnix), F(12f), new Rectangle(r.X + S(12f), r.Y + S(54f), r.Width - S(24f), S(18f)), item.Online ? Ui.Good : Ui.TextMute, (TextFormatFlags)32772);
			num4 += num5 + S(8f);
		}
		if (_chars != null)
		{
			TradeCharacter tradeCharacter = Char(_charSel);
			Rectangle r2 = new Rectangle(x, pane.Bottom - S(58f), num3, S(58f));
			Ui.Card(g, r2, S(10f), Ui.K1, Ui.K3);
			Money(g, r2.X + S(12f), r2.Y + S(8f), S(22f), tradeCharacter?.Tstone ?? 0, 14f);
			Font f = F(12f);
			Ui.Draw(g, L.T("Coin บ\u0e31ญช\u0e35 ", "Account Coin "), f, new Rectangle(r2.X + S(12f), r2.Y + S(32f), S(90f), S(18f)), Ui.TextDim, (TextFormatFlags)4);
			Ui.Draw(g, TradeFmt.N(_chars.Coin), F(12f, medium: true), new Rectangle(r2.X + S(12f) + Ui.Measure(L.T("Coin บ\u0e31ญช\u0e35 ", "Account Coin "), f).Width, r2.Y + S(32f), S(90f), S(18f)), Ui.Text, (TextFormatFlags)4);
		}
		int num7 = pane.X + num3 + S(16f);
		int w = pane.Right - num7;
		TradeCharacter tradeCharacter2 = Char(_charSel);
		ItemsResult? items = _items;
		int value = (((object)items != null) ? items.Bag.Length : (tradeCharacter2?.BagUsed ?? 0));
		int? num8 = _items?.BagMax;
		int num9;
		if (num8.HasValue)
		{
			int valueOrDefault = num8.GetValueOrDefault();
			if (valueOrDefault > 0)
			{
				num9 = valueOrDefault;
				goto IL_07d8;
			}
		}
		num9 = tradeCharacter2?.BagMax ?? 0;
		goto IL_07d8;
		IL_07d8:
		int num10 = num9;
		(string, string, string, string, string)[] tabs = new(string, string, string, string, string)[2]
		{
			("bag", L.T("กระเป\u0e4bา", "Bag"), (num10 > 0) ? $"{value}/{num10}" : null, null, null),
			("eq", L.T("ต\u0e34ดต\u0e31ว", "Equipped"), (_items != null && _items.Equipped.Length != 0) ? _items.Equipped.Length.ToString() : null, null, null)
		};
		Tabs(g, num7, num2, w, tabs, _bagTab, delegate(string t)
		{
			_bagTab = t;
			_itemScroll = 0;
			((Control)this).Invalidate();
		});
		InputBox(g, new Rectangle(pane.Right - S(170f), num2 + S(3f), S(170f), S(30f)), "q_item", L.T("ค\u0e49นช\u0e37\u0e48อไอเทม", "Search items"), disabled: false, "search");
		int num11 = num2 + S(46f);
		int num12 = num7;
		string[] chips = ItemFilter.Chips;
		foreach (string text2 in chips)
		{
			string c2 = text2;
			num12 += Chip(g, num12, num11, "lt-cat-" + text2, ItemFilter.ChipText(text2), _cat == text2, delegate
			{
				_cat = c2;
				_itemScroll = 0;
				((Control)this).Invalidate();
			}) + S(6f);
		}
		int num14 = num11 + S(28f) + S(10f);
		TradeItem[] array2 = ((_items == null) ? Array.Empty<TradeItem>() : ((_bagTab == "bag") ? _items.Bag : _items.Equipped));
		TradeItem[] array3 = ItemFilter.Apply(array2, _cat, ((Control)_boxes["q_item"]).Text);
		int num15 = S(58f);
		int num16 = S(7f);
		int num17 = 6;
		int width = num17 * num15 + (num17 - 1) * num16;
		int num18 = S(232f);
		int num19 = Math.Max(1, (pane.Bottom - num14 + num16) / (num15 + num16));
		int num20 = Math.Max(num19, (Math.Max(array3.Length, (_bagTab == "bag") ? num10 : 0) + num17 - 1) / num17);
		_itemScroll = Math.Clamp(_itemScroll, 0, Math.Max(0, num20 - num19));
		Rectangle r3 = new Rectangle(num7, num14, width, num19 * (num15 + num16) - num16);
		if (_items == null)
		{
			Muted(g, r3, _itemsErr ?? ((_charSel == null) ? L.T("เล\u0e37อกต\u0e31วละครทางซ\u0e49าย", "Pick a character on the left") : L.T("กำล\u0e31งโหลดไอเทม…", "Loading items…")));
		}
		else
		{
			int val = ((_bagTab == "bag") ? Math.Max(array3.Length, Math.Min(num10, num20 * num17)) : array3.Length);
			for (int num21 = 0; num21 < num19; num21++)
			{
				for (int num22 = 0; num22 < num17; num22++)
				{
					int num23 = (num21 + _itemScroll) * num17 + num22;
					Rectangle r4 = new Rectangle(num7 + num22 * (num15 + num16), num14 + num21 * (num15 + num16), num15, num15);
					if (num23 < array3.Length)
					{
						TradeItem it = array3[num23];
						Slot(g, r4, it, it.Id == _itemSel, "lt-item-" + it.Id, delegate
						{
							SelectItem(it);
						});
					}
					else if (num23 < Math.Max(val, num19 * num17))
					{
						Slot(g, r4, null, on: false, null, null);
					}
				}
			}
			if (array3.Length == 0)
			{
				Muted(g, r3, (array2.Length != 0) ? L.T("ไม\u0e48พบไอเทมตามท\u0e35\u0e48ค\u0e49น", "No items match") : ((_bagTab == "bag") ? L.T("กระเป\u0e4bาว\u0e48าง", "Bag is empty") : L.T("ไม\u0e48ได\u0e49สวมใส\u0e48อะไร", "Nothing equipped")));
			}
			if (num20 > num19)
			{
				Rectangle rectangle = new Rectangle(r3.Right + S(4f), r3.Y, S(4f), r3.Height);
				int num24 = Math.Max(S(24f), rectangle.Height * num19 / num20);
				Ui.Fill(g, new Rectangle(rectangle.X, rectangle.Y + (rectangle.Height - num24) * _itemScroll / Math.Max(1, num20 - num19), rectangle.Width, num24), S(2f), Ui.K3);
			}
		}
		DrawItemDetail(g, new Rectangle(pane.Right - num18, num14, num18, pane.Bottom - num14), array3.FirstOrDefault((TradeItem i) => i.Id == _itemSel) ?? array2.FirstOrDefault((TradeItem i) => i.Id == _itemSel));
	}

	private void DrawItemDetail(Graphics g, Rectangle card, TradeItem? it)
	{
		//IL_0899: Unknown result type (might be due to invalid IL or missing references)
		//IL_089e: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_08b9: Expected O, but got Unknown
		//IL_08be: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c5: Expected O, but got Unknown
		Ui.Card(g, card, S(10f), Ui.K1, Ui.K3);
		int num = card.X + S(14f);
		int w = card.Width - S(28f);
		int y = card.Y + S(14f);
		if (it == null)
		{
			Muted(g, card, L.T("เล\u0e37อกไอเทมเพ\u0e37\u0e48อด\u0e39รายละเอ\u0e35ยด", "Select an item to see details"));
			return;
		}
		Slot(g, new Rectangle(num, y, S(58f), S(58f)), it, on: false, null, null, showHeld: false);
		Font f = F(14f, medium: true);
		int num2 = Math.Min(Ui.MeasureWrapped(g, it.Name, f, w - S(68f)), S(40f));
		Ui.DrawWrapped(g, it.Name, f, new Rectangle(num + S(68f), y + S(2f), w - S(68f), num2 + S(2f)), Ui.Text);
		Ui.Draw(g, it.TagLine, F(12f), new Rectangle(num + S(68f), y + S(4f) + num2, w - S(68f), S(18f)), Ui.TextMute, (TextFormatFlags)32772);
		y += S(58f) + S(12f);
		Font f2 = F(12f);
		Font f3 = F(12f, medium: true);
		int tx;
		if (it.IsEquip)
		{
			tx = num;
			Part(L.T("เลเวล ", "Level "), f2, Ui.TextDim);
			Part(it.Level.GetValueOrDefault().ToString(), f3, Ui.Text);
			int? maxDurability = it.MaxDurability;
			if (maxDurability.HasValue)
			{
				int valueOrDefault = maxDurability.GetValueOrDefault();
				Part(L.T(" · ความทน ", " · Durability "), f2, Ui.TextDim);
				Part($"{it.Durability.GetValueOrDefault()}/{valueOrDefault}", f3, Ui.Text);
			}
			y += S(18f) + S(4f);
			double? durabilityFrac = it.DurabilityFrac;
			if (durabilityFrac.HasValue)
			{
				double valueOrDefault2 = durabilityFrac.GetValueOrDefault();
				DurBar(g, new Rectangle(num, y, w, S(4f)), valueOrDefault2);
				y += S(4f) + S(10f);
			}
		}
		else
		{
			Ui.Draw(g, L.T("จำนวน ", "Count "), f2, new Rectangle(num, y, w, S(18f)), Ui.TextDim, (TextFormatFlags)4);
			int width = Ui.Measure(L.T("จำนวน ", "Count "), f2).Width;
			Ui.Draw(g, it.Count.ToString(), f3, new Rectangle(num + width, y, w, S(18f)), Ui.Text, (TextFormatFlags)4);
			Ui.Draw(g, L.T(" ช\u0e34\u0e49น", " pcs"), f2, new Rectangle(num + width + Ui.Measure(it.Count.ToString(), f3).Width, y, w, S(18f)), Ui.TextDim, (TextFormatFlags)4);
			y += S(18f) + S(10f);
		}
		if (it.Held == "market")
		{
			TagChip(g, num, y, Listed.Label(it, _listedPrice), "y", "building-store");
			y += S(20f) + S(10f);
		}
		else if (it.Held == "trade")
		{
			TagChip(g, num, y, L.T("พ\u0e31กอย\u0e39\u0e48ในข\u0e49อเสนอเทรด", "Held in a trade offer"), "m", "arrows-exchange");
			y += S(20f) + S(10f);
		}
		else if (!it.Tradable)
		{
			TagChip(g, num, y, L.T("ล\u0e47อกเทรด", "Trade-locked"), "m", "lock");
			y += S(20f) + S(10f);
		}
		string key = it.Proto + "|" + it.Level;
		PriceResult value;
		string s = ((!_prices.TryGetValue(key, out value) || value == null) ? "…" : ((value.Count == 0) ? L.T("ไม\u0e48ม\u0e35ขาย", "none listed") : TradeFmt.T(value.MinPrice)));
		string s2 = L.T("ราคาในตลาดตอนน\u0e35\u0e49 ต\u0e48ำส\u0e38ด ", "Market now · lowest ");
		Ui.Draw(g, s2, f2, new Rectangle(num, y, w, S(18f)), Ui.TextMute, (TextFormatFlags)4);
		Ui.Draw(g, s, f2, new Rectangle(num + Ui.Measure(s2, f2).Width, y, w, S(18f)), Ui.TextDim, (TextFormatFlags)4);
		y += S(18f);
		string text = L.T("ลงขายตลาดทำในเกมเท\u0e48าน\u0e31\u0e49น · ของท\u0e35\u0e48ใส\u0e48ข\u0e49อเสนอเทรดจะถ\u0e39กพ\u0e31กไว\u0e49ท\u0e35\u0e48เซ\u0e34ร\u0e4cฟ", "Listing on the market is in-game only · offered items are held on the server");
		Font val = F(11f);
		int num3 = Ui.MeasureWrapped(g, text, val, w);
		int num4 = card.Bottom - S(14f) - num3 - S(6f) - S(40f);
		bool enabled = TradeMenu.ShowTrade(LoggedIn, _ltf) && !_gate.Locked && it.Held == null && it.Tradable && _bagTab == "bag";
		TradeBtn(g, new Rectangle(num, num4, w, S(40f)), "lt-offer-item", _gate.Locked ? "lock" : "arrows-exchange", _gate.Locked ? L.T("เทรดเฉพาะ Supporter", "Supporter-only trade") : L.T("ใส\u0e48ข\u0e49อเสนอเทรด", "Offer in a trade"), "y", enabled, delegate
		{
			OpenOffer(OfferMode.New, null, it);
		}, 14f);
		StringFormat val2 = new StringFormat
		{
			Alignment = (StringAlignment)1,
			Trimming = (StringTrimming)3,
			FormatFlags = (StringFormatFlags)8192
		};
		try
		{
			SolidBrush val3 = new SolidBrush(Ui.TextMute);
			try
			{
				g.DrawString(text, val, (Brush)(object)val3, new RectangleF(num, num4 + S(40f) + S(6f), w, num3 + 2), val2);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		void Part(string s3, Font f4, Color c)
		{
			Ui.Draw(g, s3, f4, new Rectangle(tx, y, w, S(18f)), c, (TextFormatFlags)4);
			tx += Ui.Measure(s3, f4).Width;
		}
	}

	private void DrawChat(Graphics g, Rectangle pane)
	{
		int num = TradeHeader(g, pane, L.T("แชท", "Chat"), 0, L.T("ช\u0e48องเด\u0e35ยวก\u0e31บ /g ในเกม · ท\u0e38กเกาะในเซ\u0e34ร\u0e4cฟเห\u0e47น · ข\u0e49อความท\u0e35\u0e48พ\u0e34มพ\u0e4cจากร\u0e31นเช\u0e48อม\u0e35ป\u0e49าย [W] ในเกม", "Same channel as /g in game · every island on the server sees it · messages from the launcher show a [W] tag in game"));
		(string, string, string, string, string)[] tabs = new(string, string, string, string, string)[3]
		{
			("world", L.T("แชทโลก", "World"), null, null, "world"),
			("dm", L.T("ส\u0e48วนต\u0e31ว", "Direct"), null, L.T("ระยะ 2", "Phase 2"), null),
			("clan", L.T("แคลน", "Clan"), null, L.T("ระยะ 2", "Phase 2"), null)
		};
		Tabs(g, pane.X, num, pane.Width, tabs, "world", delegate
		{
		});
		string server = _acct.Server;
		UpdateInfo info = _info;
		object obj;
		if (info != null && info.ServerOnline)
		{
			int? onlinePlayers = info.OnlinePlayers;
			if (onlinePlayers.HasValue)
			{
				int valueOrDefault = onlinePlayers.GetValueOrDefault();
				obj = $" · {valueOrDefault:N0}";
				goto IL_017f;
			}
		}
		obj = "";
		goto IL_017f;
		IL_017f:
		string text = server + (string?)obj;
		Chip(g, pane.Right - ChipW(text), num + S(4f), "lt-srv", text, on: true, null);
		int num2 = num + S(46f);
		int rightW = S(230f);
		int num3 = pane.Width - rightW - S(14f);
		int num4 = S(18f);
		int num5 = S(40f);
		Rectangle rectangle = new Rectangle(pane.X, pane.Bottom - num4 - S(6f) - num5, num3, num5);
		Rectangle rectangle2 = Rectangle.FromLTRB(pane.X, num2, pane.X + num3, rectangle.Y - S(10f));
		Ui.Card(g, rectangle2, S(10f), Ui.K1, Ui.K3);
		DrawChatMessages(g, Rectangle.Inflate(rectangle2, -S(6f), -S(8f)));
		bool flag = _speaker != null && (_chatInfo?.Enabled ?? true);
		int num6 = CharPicker(g, pane.X, rectangle.Y, rectangle.Height, "lt-speaker", L.T("พ\u0e39ดในช\u0e37\u0e48อ", "Speak as"), _speaker, delegate(string id)
		{
			_speaker = id;
			((Control)this).Invalidate();
		});
		int num7 = S(110f);
		Rectangle r = new Rectangle(rectangle.Right - num7, rectangle.Y, num7, rectangle.Height);
		Rectangle r2 = Rectangle.FromLTRB(pane.X + num6 + S(8f), rectangle.Y, r.X - S(8f), rectangle.Bottom);
		int value = _chatInfo?.MaxLen ?? 300;
		InputBox(g, r2, "chat_in", L.T($"พ\u0e34มพ\u0e4cข\u0e49อความ… ไม\u0e48เก\u0e34น {value} ต\u0e31วอ\u0e31กษร", $"Type a message… up to {value} characters"), !flag);
		bool flag2 = DateTime.UtcNow < _chatCdUntil;
		TradeBtn(g, r, "lt-send", _chatSending ? "loader-2" : "send", L.T("ส\u0e48ง", "Send"), "y", flag && !flag2 && !_chatSending && ((Control)_boxes["chat_in"]).Text.Trim().Length > 0, delegate
		{
			SendChatAsync();
		}, 14f, _chatSending);
		Font f = F(12f);
		int num8 = pane.Bottom - num4;
		int value2 = _chatInfo?.Cooldown ?? 10;
		object obj2;
		if (_chatErr == null)
		{
			if (!flag2)
			{
				ChatHistory? chatInfo = _chatInfo;
				obj2 = (((object)chatInfo != null && !chatInfo.Enabled) ? L.T("แชทโลกป\u0e34ดอย\u0e39\u0e48 (chat.global)", "World chat is disabled (chat.global)") : L.T($"พร\u0e49อมส\u0e48ง · หน\u0e48วง {value2} ว\u0e34ต\u0e48อบ\u0e31ญช\u0e35", $"Ready · {value2} s cooldown per account"));
			}
			else
			{
				obj2 = L.T($"ส\u0e48งได\u0e49อ\u0e35ก {Math.Ceiling((_chatCdUntil - DateTime.UtcNow).TotalSeconds)} ว\u0e34 (หน\u0e48วง {value2} ว\u0e34ต\u0e48อบ\u0e31ญช\u0e35)", $"Send again in {Math.Ceiling((_chatCdUntil - DateTime.UtcNow).TotalSeconds)} s ({value2} s per account)");
			}
		}
		else
		{
			obj2 = _chatErr;
		}
		string s = (string)obj2;
		Icons.Draw(g, (_chatErr != null) ? "alert-circle" : "clock-hour-4", new Point(pane.X + S(7f), num8 + num4 / 2), S(13f), (_chatErr != null) ? Ui.Bad : Ui.TextMute);
		Ui.Draw(g, s, f, new Rectangle(pane.X + S(19f), num8, num3 - S(190f), num4), (_chatErr != null) ? Ui.Bad : Ui.TextMute, (TextFormatFlags)32772);
		string s2 = L.T("ร\u0e31บแชทโลกในเกม", "Receive world chat in game");
		int width = Ui.Measure(s2, f).Width;
		Rectangle r3 = new Rectangle(pane.X + num3 - S(30f), num8 + S(1f), S(30f), S(16f));
		Rectangle r4 = new Rectangle(r3.X - width - S(8f), num8, width + S(8f) + S(30f), num4);
		bool flag3 = AddHit(r4, "lt-pref", delegate
		{
			ToggleChatPrefAsync();
		}, _speaker != null && !_chatPrefBusy);
		Ui.Draw(g, s2, f, new Rectangle(r4.X, num8, width + S(2f), num4), flag3 ? Ui.Text : Ui.TextMute, (TextFormatFlags)4);
		bool flag4 = !_chatOff;
		Ui.Fill(g, r3, S(8f), flag4 ? Ui.Good : K4);
		Ui.Dot(g, new PointF(flag4 ? (r3.Right - S(8f)) : (r3.X + S(8f)), r3.Y + S(8f)), S(12f), Ui.K0);
		int x = pane.Right - rightW;
		int y = num2;
		string[] array = new string[4]
		{
			L.T($"ข\u0e49อความยาวไม\u0e48เก\u0e34น {value} ต\u0e31วอ\u0e31กษร", $"Messages up to {value} characters"),
			L.T($"หน\u0e48วง {value2} ว\u0e34ต\u0e48อบ\u0e31ญช\u0e35 (รวมในเกม)", $"{value2} s cooldown per account (shared with in-game)"),
			L.T("คนท\u0e35\u0e48บล\u0e47อกก\u0e31นในเกมจะไม\u0e48เห\u0e47นก\u0e31น", "Players blocked in game do not see each other"),
			L.T("โดน GM ป\u0e34ดแชท = พ\u0e34มพ\u0e4cท\u0e35\u0e48น\u0e35\u0e48ไม\u0e48ได\u0e49ด\u0e49วย", "Muted by a GM = cannot type here either")
		};
		Font rf = F(13f);
		int height = S(12f) + S(20f) + S(6f) + array.Sum((string s5) => Ui.MeasureWrapped(g, s5, rf, rightW - S(44f)) + S(3f)) + S(10f);
		Rectangle r5 = new Rectangle(x, y, rightW, height);
		Ui.Card(g, r5, S(10f), Ui.K1, Ui.K3);
		Ui.Draw(g, L.T("กต\u0e34กา", "Rules"), F(13.5f, medium: true), new Rectangle(r5.X + S(14f), r5.Y + S(10f), rightW, S(20f)), Ui.Text, (TextFormatFlags)4);
		int num9 = r5.Y + S(36f);
		string[] array2 = array;
		foreach (string s3 in array2)
		{
			Ui.Dot(g, new PointF(r5.X + S(22f), num9 + S(9f)), S(4f), Ui.TextDim);
			int num11 = Ui.MeasureWrapped(g, s3, rf, rightW - S(44f));
			Ui.DrawWrapped(g, s3, rf, new Rectangle(r5.X + S(30f), num9, rightW - S(44f), num11 + 2), Ui.TextDim);
			num9 += num11 + S(3f);
		}
		y = r5.Bottom + S(10f);
		Rectangle nc = new Rectangle(x, y, rightW, S(12f) + S(20f) + S(6f) + S(22f) + S(6f) + S(22f) + S(12f));
		Ui.Card(g, nc, S(10f), Ui.K1, Ui.K3);
		Ui.Draw(g, L.T("แจ\u0e49งเต\u0e37อน", "Alerts"), F(13.5f, medium: true), new Rectangle(nc.X + S(14f), nc.Y + S(10f), rightW, S(20f)), Ui.Text, (TextFormatFlags)4);
		var (n, _, n2) = TradeMenu.Badges(_summary, _gate);
		AlertRow(nc.Y + S(36f), "bell", L.T("ข\u0e49อความใหม\u0e48ในแชทโลก", "New world-chat messages"), n);
		AlertRow(nc.Y + S(36f) + S(28f), "arrows-exchange", L.T("ข\u0e49อเสนอเทรดใหม\u0e48", "New trade offers"), n2, _gate.Locked);
		y = nc.Bottom + S(10f);
		string s4 = L.T("ต\u0e31วละครท\u0e35\u0e48พ\u0e39ดต\u0e49องอย\u0e39\u0e48ในเซ\u0e34ร\u0e4cฟท\u0e35\u0e48เล\u0e37อก ไม\u0e48ต\u0e49องออนไลน\u0e4c · จ\u0e38ดเลขบนเมน\u0e39 = แจ\u0e49งเต\u0e37อน (ย\u0e31งไม\u0e48ม\u0e35เส\u0e35ยง/ป\u0e4aอปอ\u0e31ป)", "The speaking character must be on this server, online or not · menu badges are the alerts (no sound/pop-up yet)");
		int num12 = Ui.MeasureWrapped(g, s4, rf, rightW - S(50f));
		Rectangle r6 = new Rectangle(x, y, rightW, num12 + S(20f));
		Ui.Card(g, r6, S(10f), Ui.K1, Ui.K3);
		Icons.Draw(g, "info-circle", new Point(r6.X + S(14f) + S(8f), r6.Y + S(10f) + S(9f)), S(16f), Ui.Yellow);
		Ui.DrawWrapped(g, s4, rf, new Rectangle(r6.X + S(36f), r6.Y + S(10f), rightW - S(50f), num12 + 2), Ui.TextDim);
		void AlertRow(int ay, string icon, string s5, int num14, bool locked = false)
		{
			Icons.Draw(g, icon, new Point(nc.X + S(14f) + S(9f), ay + S(11f)), S(17f), Ui.TextDim);
			int num13 = (locked ? TagChip(g, nc.Right - S(14f), ay + S(1f), "Supporter", "m", "lock", right: true) : TagChip(g, nc.Right - S(14f), ay + S(1f), (num14 > 0) ? num14.ToString() : L.T("เป\u0e34ด", "On"), (num14 > 0) ? "y" : "g", null, right: true));
			int num15 = nc.X + S(14f) + S(26f);
			Ui.Draw(g, s5, rf, new Rectangle(num15, ay, Math.Max(S(20f), nc.Right - S(14f) - num13 - S(6f) - num15), S(22f)), Ui.TextDim, (TextFormatFlags)32772);
		}
	}

	private void DrawChatMessages(Graphics g, Rectangle area)
	{
		GraphicsState val = g.Save();
		g.SetClip(area);
		if (_chatMsgs.Count == 0 && _chatSys.Count == 0)
		{
			Muted(g, area, _chatErr ?? ((_chatInfo == null) ? L.T("กำล\u0e31งโหลดแชท…", "Loading chat…") : L.T("ย\u0e31งไม\u0e48ม\u0e35ข\u0e49อความ — ท\u0e31กทายคนแรกเลย", "No messages yet — say hello")));
			g.Restore(val);
			return;
		}
		HashSet<string> hashSet = (_chars?.Characters.Select((TradeCharacter c) => c.Name) ?? Enumerable.Empty<string>()).ToHashSet<string>(StringComparer.Ordinal);
		Font f = F(12f);
		Font f2 = F(14f, medium: true);
		Font f3 = F(14f);
		Font f4 = F(13f);
		long nowUnix = NowUnix;
		int num = area.X + S(8f) + S(40f) + S(10f);
		int val2 = S(130f);
		List<(bool, long, string, string, string)> list = (from tuple2 in _chatMsgs.Select((ChatMsg m) => (Sys: false, T: m.T, Name: m.Name, Src: m.Src, Text: m.Text)).Concat<(bool, long, string, string, string)>(_chatSys.Select<(long, string), (bool, long, string, string, string)>(((long T, string Text) s) => (Sys: true, T: s.T, Name: "", Src: "", Text: s.Text)))
			orderby tuple2.Item2
			select tuple2).ToList();
		int num2 = area.Bottom + _chatScroll;
		int num3 = list.Count - 1;
		while (num3 >= 0 && num2 > area.Y - S(40f))
		{
			(bool, long, string, string, string) tuple = list[num3];
			int num4 = area.Right - S(8f) - num;
			if (tuple.Item1)
			{
				int num5 = Ui.MeasureWrapped(g, tuple.Item5, f4, num4) + S(8f);
				num2 -= num5;
				if (num2 <= area.Bottom)
				{
					Ui.Draw(g, TradeFmt.Clock(tuple.Item2, nowUnix), f, new Rectangle(area.X + S(8f), num2 + S(4f), S(40f), S(18f)), Ui.TextMute, (TextFormatFlags)4);
					Ui.DrawWrapped(g, tuple.Item5, f4, new Rectangle(num, num2 + S(4f), num4, num5), Ui.TextMute);
				}
			}
			else
			{
				bool flag = hashSet.Contains(tuple.Item3);
				int num6 = Math.Min(val2, Ui.Measure(tuple.Item3 + ":", f2).Width) + S(14f) + S(4f) + S(6f);
				int num5 = Ui.MeasureWrapped(g, tuple.Item5, f3, num4 - num6) + S(10f);
				num2 -= num5;
				if (num2 <= area.Bottom)
				{
					Rectangle r = new Rectangle(area.X, num2, area.Width, num5);
					if (AddHit(r, "lt-msg-" + num3, null, enabled: false))
					{
						Ui.Fill(g, r, S(8f), Ui.K2);
					}
					Ui.Draw(g, TradeFmt.Clock(tuple.Item2, nowUnix), f, new Rectangle(area.X + S(8f), num2 + S(5f), S(40f), S(18f)), Ui.TextMute, (TextFormatFlags)4);
					Icons.Draw(g, SrcIcon(tuple.Item4), new Point(num + S(7f), num2 + S(14f)), S(14f), Ui.TextMute);
					Ui.Draw(g, tuple.Item3 + ":", f2, new Rectangle(num + S(18f), num2 + S(5f), num6 - S(18f), S(20f)), flag ? Ui.Yellow : Ui.PurpleLight, (TextFormatFlags)32772);
					Ui.DrawWrapped(g, tuple.Item5, f3, new Rectangle(num + num6, num2 + S(5f), num4 - num6, num5 - S(8f)), Ui.Text);
				}
			}
			num3--;
		}
		g.Restore(val);
		if (_chatScroll > 0)
		{
			Rectangle r2 = new Rectangle(area.X + (area.Width - S(150f)) / 2, area.Bottom - S(28f), S(150f), S(24f));
			bool flag2 = AddHit(r2, "lt-chat-bottom", delegate
			{
				_chatScroll = 0;
				MarkChatSeen();
				((Control)this).Invalidate();
			});
			Ui.Card(g, r2, S(12f), flag2 ? Ui.PurpleDark : Ui.K2, Ui.K3);
			Ui.Draw(g, L.T("↓ ไปข\u0e49อความล\u0e48าส\u0e38ด", "↓ Latest messages"), F(12f), r2, Ui.Text, (TextFormatFlags)5);
		}
	}

	private IEnumerable<TradeOffer> OffersOf(string box)
	{
		if (!_offers.TryGetValue(box, out TradeOffer[] value))
		{
			return Array.Empty<TradeOffer>();
		}
		return value;
	}

	private void MarketReload()
	{
		_mPending = true;
		_mScroll = 0;
		((Control)this).Invalidate();
	}

	private async Task SearchMarketAsync()
	{
		if (_lt == null)
		{
			return;
		}
		string buyer = _buyer;
		if (buyer == null)
		{
			_mPending = true;
			return;
		}
		_mPending = false;
		_mBusy = true;
		int gen = _ltGen;
		string mTab = _mTab;
		try
		{
			if (mTab == "buy")
			{
				(int? Min, int? Max) tuple = LevelChip.Ranges[_mLv];
				int? item = tuple.Min;
				int? item2 = tuple.Max;
				MarketSearch marketSearch = await _lt.MarketSearchAsync(buyer, ((Control)_boxes["q_market"]).Text, ItemFilter.GroupParam(_mGroup), item, item2, _mSort, _mSkip, _mFav);
				if (gen != _ltGen || ((Control)this).IsDisposed)
				{
					return;
				}
				_market = marketSearch;
				if (_mSel == null || !marketSearch.Products.Any((MarketProduct p) => p.ProductId == _mSel))
				{
					_mSel = marketSearch.Products.FirstOrDefault()?.ProductId;
				}
			}
			else if (mTab == "mine")
			{
				MarketMine marketMine = await _lt.MarketMineAsync(buyer);
				if (gen != _ltGen || ((Control)this).IsDisposed)
				{
					return;
				}
				_mine = marketMine;
				if (_mSel == null || !marketMine.Selling.Any((MarketProduct p) => p.ProductId == _mSel))
				{
					_mSel = marketMine.Selling.FirstOrDefault()?.ProductId;
				}
			}
			else
			{
				AuctionResult auctionResult = ((!(_auMode == "mine")) ? (await _lt.AuctionSearchAsync(buyer, ((Control)_boxes["q_market"]).Text, ItemFilter.GroupParam(_mGroup), _mSort, _mSkip)) : (await _lt.AuctionMineAsync(buyer, _auKind)));
				AuctionResult auctionResult2 = auctionResult;
				if (gen != _ltGen || ((Control)this).IsDisposed)
				{
					return;
				}
				_au = auctionResult2;
				if (_mSel == null || !auctionResult2.Lots.Any((AuctionLot l) => l.Id == _mSel))
				{
					_mSel = auctionResult2.Lots.FirstOrDefault()?.Id;
				}
				if (_mSel != null)
				{
					AuctionLot auctionLot = auctionResult2.Lots.FirstOrDefault((AuctionLot l) => l.Id == _mSel);
					if ((object)auctionLot != null)
					{
						((Control)_boxes["au_amt"]).Text = TradeFmt.N(Math.Max(auctionLot.MinBid, auctionLot.Current + 1));
					}
				}
			}
			_mErr = null;
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "market_buy"))
			{
				_mErr = apiError.Message;
			}
		}
		catch (Exception ex)
		{
			Log.Write("market: " + ex);
			_mErr = L.T("โหลดตลาดไม\u0e48สำเร\u0e47จ — ลองใหม\u0e48", "Could not load the market — try again");
		}
		finally
		{
			_mBusy = false;
			((Control)this).Invalidate();
		}
	}

	private async Task BuyAsync(MarketProduct p)
	{
		if (_lt == null || _buyBusy || _buyer == null)
		{
			return;
		}
		string who = _buyer;
		_buyBusy = true;
		_mErr = null;
		if (_buy.ProductId != p.ProductId)
		{
			_buy.Start(p);
		}
		else
		{
			_buy.Start(p with
			{
				Price = _buy.Expect
			});
		}
		UpdateAnim();
		int gen = _ltGen;
		try
		{
			BuyResult r = await _lt.MarketBuyAsync(who, p.ProductId, _buy.Expect);
			if (gen != _ltGen || ((Control)this).IsDisposed)
			{
				return;
			}
			_buy.Done();
			TradeCharacter tradeCharacter = Char(who);
			if (_chars != null && tradeCharacter != null)
			{
				_chars = _chars with
				{
					Characters = _chars.Characters.Select((TradeCharacter x) => (!(x.EntityId == who)) ? x : x with
					{
						Tstone = r.Tstone
					}).ToArray()
				};
			}
			ToastT(L.T($"ซ\u0e37\u0e49อแล\u0e49ว {TradeFmt.T(r.Price)} · ของเข\u0e49ากล\u0e48องจดหมาย {tradeCharacter?.Name ?? ""} ร\u0e31บตอนเข\u0e49าเกม", $"Bought for {TradeFmt.T(r.Price)} · sent to {tradeCharacter?.Name ?? ""}'s mailbox"));
			_mNote = L.T("ซ\u0e37\u0e49อแล\u0e49ว · ห\u0e31กค\u0e48าธรรมเน\u0e35ยม " + TradeFmt.T(r.Fee) + " · เหล\u0e37อ " + TradeFmt.T(r.Tstone), "Bought · fee " + TradeFmt.T(r.Fee) + " · balance " + TradeFmt.T(r.Tstone));
			MarketReload();
		}
		catch (ApiError apiError)
		{
			if (gen != _ltGen || ((Control)this).IsDisposed || HandleTradeError(apiError, "market_buy"))
			{
				return;
			}
			if (_buy.Fail(apiError))
			{
				_mErr = L.T("ราคาเปล\u0e35\u0e48ยนเป\u0e47น " + TradeFmt.T(_buy.NewPrice.GetValueOrDefault()) + " — กดย\u0e37นย\u0e31นอ\u0e35กคร\u0e31\u0e49งถ\u0e49าย\u0e31งต\u0e49องการ", "Price changed to " + TradeFmt.T(_buy.NewPrice.GetValueOrDefault()) + " — press confirm again if you still want it");
				return;
			}
			_mErr = apiError.Message;
			string code = apiError.Code;
			if ((code == "gone" || code == "own") ? true : false)
			{
				MarketReload();
			}
		}
		catch (Exception ex)
		{
			Log.Write("market/buy: " + ex);
			_mErr = L.T("ซ\u0e37\u0e49อไม\u0e48สำเร\u0e47จ — ลองใหม\u0e48", "Purchase failed — try again");
			_buy.Done();
		}
		finally
		{
			_buyBusy = false;
			UpdateAnim();
			((Control)this).Invalidate();
		}
	}

	private async Task CollectAsync()
	{
		if (_lt == null || _collectBusy || _buyer == null)
		{
			return;
		}
		_collectBusy = true;
		int gen = _ltGen;
		try
		{
			CollectResult collectResult = await _lt.MarketCollectAsync(_buyer);
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				ToastT(L.T($"ร\u0e31บเง\u0e34น {TradeFmt.T(collectResult.Total)} จาก {collectResult.Count} รายการแล\u0e49ว (ภาษ\u0e35 {TradeFmt.T(collectResult.Tax)})", $"Collected {TradeFmt.T(collectResult.Total)} from {collectResult.Count} sales (tax {TradeFmt.T(collectResult.Tax)})"), "gem", Ui.Yellow);
				_sumNext = DateTime.MinValue;
				LoadCharactersAsync();
				MarketReload();
			}
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "market_buy"))
			{
				_mErr = apiError.Message;
			}
		}
		finally
		{
			_collectBusy = false;
			((Control)this).Invalidate();
		}
	}

	private async Task FavoriteAsync(MarketProduct p)
	{
		if (_lt == null || _favBusy || _buyer == null)
		{
			return;
		}
		_favBusy = true;
		int gen = _ltGen;
		try
		{
			string[] favs = await _lt.MarketFavoriteAsync(_buyer, p.ProductId, !p.Favorite);
			if (gen == _ltGen && !((Control)this).IsDisposed && _market != null)
			{
				_market = _market with
				{
					Products = _market.Products.Select((MarketProduct x) => x with
					{
						Favorite = favs.Contains(x.ProductId)
					}).ToArray()
				};
			}
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "market_buy"))
			{
				_mErr = apiError.Message;
			}
		}
		finally
		{
			_favBusy = false;
			((Control)this).Invalidate();
		}
	}

	private async Task BidAsync()
	{
		if (_lt == null || _bidBusy || _buyer == null || _au == null)
		{
			return;
		}
		AuctionLot auctionLot = _au.Lots.FirstOrDefault((AuctionLot l) => l.Id == _mSel);
		if (auctionLot == null)
		{
			return;
		}
		long amount = TradeFmt.ParseT(((Control)_boxes["au_amt"]).Text);
		if (amount < Math.Max(auctionLot.MinBid, 1L))
		{
			_mErr = L.T("ต\u0e49องประม\u0e39ลอย\u0e48างน\u0e49อย " + TradeFmt.T(auctionLot.MinBid), "Minimum bid is " + TradeFmt.T(auctionLot.MinBid));
			((Control)this).Invalidate();
			return;
		}
		_bidBusy = true;
		_mErr = null;
		int gen = _ltGen;
		try
		{
			await _lt.AuctionBidAsync(_buyer, auctionLot.Id, amount, auctionLot.Current);
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				ToastT(L.T("ประม\u0e39ล " + TradeFmt.T(amount) + " แล\u0e49ว", "Bid " + TradeFmt.T(amount) + " placed"), "gavel", Ui.Yellow);
				LoadCharactersAsync();
				MarketReload();
			}
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "market_buy"))
			{
				_mErr = apiError.Message;
				bool flag;
				switch (apiError.Code)
				{
				case "stale":
				case "low":
				case "ended":
				case "gone":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (flag)
				{
					MarketReload();
				}
			}
		}
		finally
		{
			_bidBusy = false;
			((Control)this).Invalidate();
		}
	}

	private async Task ClaimAsync(string? claimId)
	{
		if (_lt == null || _claimBusy || _buyer == null)
		{
			return;
		}
		_claimBusy = true;
		int gen = _ltGen;
		try
		{
			await _lt.AuctionClaimAsync(_buyer, claimId);
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				ToastT(L.T("ร\u0e31บของแล\u0e49ว · อย\u0e39\u0e48ในกล\u0e48องจดหมาย ร\u0e31บตอนเข\u0e49าเกม", "Claimed · it is in your mailbox, collect in game"));
				LoadCharactersAsync();
				MarketReload();
			}
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "market_buy"))
			{
				_mErr = apiError.Message;
			}
		}
		finally
		{
			_claimBusy = false;
			((Control)this).Invalidate();
		}
	}

	private void DrawMarket(Graphics g, Rectangle pane)
	{
		long nowUnix = NowUnix;
		if (_mPending && !_mBusy && _buyer != null)
		{
			SearchMarketAsync();
		}
		TradeCharacter tradeCharacter = Char(_buyer);
		int num = CharPickerW(L.T("ซ\u0e37\u0e49อในนาม", "Buy as"), _buyer, tradeCharacter?.Tstone);
		int num2 = TradeHeader(g, pane, L.T("ตลาด", "Market"), num, L.T("ตลาดเด\u0e35ยวก\u0e31บในเกม · ซ\u0e37\u0e49อจากร\u0e31นเช\u0e48อได\u0e49 ลงขายทำในเกมเท\u0e48าน\u0e31\u0e49น · ของท\u0e35\u0e48ซ\u0e37\u0e49อส\u0e48งเข\u0e49ากล\u0e48องจดหมาย ร\u0e31บตอนเข\u0e49าเกม", "Same market as in game · buy from the launcher, list in game only · purchases go to your mailbox, collect in game"));
		CharPicker(g, pane.Right - num, pane.Y + S(22f), S(34f), "lt-buyer", L.T("ซ\u0e37\u0e49อในนาม", "Buy as"), _buyer, delegate(string id)
		{
			_buyer = id;
			_mSel = null;
			_buy.Done();
			MarketReload();
		}, tradeCharacter?.Tstone);
		MarketMine? mine = _mine;
		int num3 = (((object)mine != null) ? mine.Selling.Length : (_summary?.MarketCollectable ?? 0));
		(string, string, string, string, string)[] tabs = new(string, string, string, string, string)[3]
		{
			("buy", L.T("ซ\u0e37\u0e49อ", "Buy"), null, null, null),
			("mine", L.T("ตลาดของฉ\u0e31น", "My market"), (num3 > 0) ? num3.ToString() : null, null, null),
			("au", L.T("ประม\u0e39ล", "Auction"), null, null, null)
		};
		Tabs(g, pane.X, num2, pane.Width, tabs, _mTab, delegate(string mTab2)
		{
			_mTab = mTab2;
			_mSel = null;
			_mSkip = 0;
			_mErr = null;
			_buy.Done();
			MarketReload();
		});
		int num4 = pane.Right;
		if (_mTab == "buy")
		{
			int num5 = ChipW("", "star");
			num4 -= num5;
			Chip(g, num4, num2 + S(4f), "lt-fav", "", _mFav, delegate
			{
				_mFav = !_mFav;
				_mSkip = 0;
				MarketReload();
			}, "star");
			num4 -= S(6f);
		}
		if (_mTab != "mine")
		{
			string text = SortChip.Text(_mSort);
			int num6 = ChipW(text);
			num4 -= num6;
			Chip(g, num4, num2 + S(4f), "lt-sort", text, on: true, delegate
			{
				_mSort = SortChip.Next(_mSort);
				_mSkip = 0;
				MarketReload();
			});
			num4 -= S(6f);
			if (_mTab == "buy")
			{
				string text2 = LevelChip.Text(_mLv);
				int num7 = ChipW(text2);
				num4 -= num7;
				Chip(g, num4, num2 + S(4f), "lt-lv", text2, _mLv != 0, delegate
				{
					_mLv = LevelChip.Next(_mLv);
					_mSkip = 0;
					MarketReload();
				});
				num4 -= S(6f);
			}
			string text3 = L.T("หมวด: ", "Group: ") + ItemFilter.ChipText(_mGroup);
			int num8 = ChipW(text3, "filter");
			num4 -= num8;
			Chip(g, num4, num2 + S(4f), "lt-group", text3, _mGroup != "all", delegate
			{
				int num11 = Array.IndexOf(ItemFilter.Chips, _mGroup);
				_mGroup = ItemFilter.Chips[(num11 + 1) % ItemFilter.Chips.Length];
				_mSkip = 0;
				MarketReload();
			}, "filter");
			num4 -= S(6f);
			num4 -= S(190f);
			InputBox(g, new Rectangle(num4, num2 + S(3f), S(190f), S(30f)), "q_market", L.T("ค\u0e49นหาช\u0e37\u0e48อไอเทม ⏎", "Search item name ⏎"), disabled: false, "search");
		}
		int num9 = num2 + S(46f);
		int num10 = S(250f);
		int width = pane.Width - num10 - S(14f);
		Rectangle t = new Rectangle(pane.X, num9, width, pane.Bottom - num9);
		Rectangle card = new Rectangle(pane.Right - num10, num9, num10, pane.Bottom - num9);
		string mTab = _mTab;
		if (!(mTab == "mine"))
		{
			if (mTab == "au")
			{
				DrawAuctionTable(g, t, nowUnix);
				DrawAuctionCard(g, card, nowUnix);
			}
			else
			{
				DrawBuyTable(g, t, nowUnix);
				DrawBuyCard(g, card, nowUnix);
			}
		}
		else
		{
			DrawMineTable(g, t, nowUnix);
			DrawMineCard(g, card, nowUnix);
		}
	}

	private int[] Cols(int tableW, params int[] fixedW)
	{
		int num = S(8f);
		int num2 = S(10f);
		int num3 = tableW - num2 * 2 - fixedW.Sum((int w) => S(w)) - num * fixedW.Length;
		int[] array = new int[fixedW.Length + 2];
		int num4 = num2;
		for (int num5 = 0; num5 <= fixedW.Length; num5++)
		{
			array[num5] = num4;
			num4 += ((num5 == 1) ? num3 : S(fixedW[(num5 != 0) ? (num5 - 1) : 0])) + num;
		}
		array[^1] = num4;
		return array;
	}

	private void TableHeader(Graphics g, Rectangle t, int[] xs, int[] w, string[] names)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		Font f = F(12f);
		for (int i = 0; i < names.Length; i++)
		{
			Ui.Draw(g, names[i], f, new Rectangle(t.X + xs[i], t.Y, w[i], S(30f)), Ui.TextMute, (TextFormatFlags)32772);
		}
		Pen val = new Pen(Ui.K3);
		try
		{
			g.DrawLine(val, t.X, t.Y + S(30f) - 1, t.Right, t.Y + S(30f) - 1);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawBuyTable(Graphics g, Rectangle t, long now)
	{
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Expected O, but got Unknown
		//IL_037c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0383: Expected O, but got Unknown
		int[] array = new int[7] { 44, 44, 64, 40, 56, 92, 56 };
		int[] array2 = Cols(t.Width, array);
		int num = S(8f);
		int num2 = S(10f);
		int num3 = t.Width - num2 * 2 - array.Sum((int w) => S(w)) - num * array.Length;
		int[] array3 = new int[8]
		{
			S(44f),
			num3,
			S(44f),
			S(64f),
			S(40f),
			S(56f),
			S(92f),
			S(56f)
		};
		TableHeader(g, t, array2, array3, new string[8]
		{
			"",
			L.T("ไอเทม", "Item"),
			L.T("เลเวล", "Level"),
			L.T("ความทน", "Durability"),
			L.T("จำนวน", "Qty"),
			L.T("เหล\u0e37อ", "Left"),
			L.T("ราคา", "Price"),
			""
		});
		MarketProduct[] array4 = _market?.Products ?? Array.Empty<MarketProduct>();
		int num4 = S(52f);
		int num5 = t.Y + S(30f);
		int num6 = S(34f);
		int num7 = Math.Max(1, (t.Bottom - num6 - num5) / num4);
		if (_market == null)
		{
			Muted(g, new Rectangle(t.X, num5, t.Width, S(80f)), _mErr ?? ((_buyer == null) ? L.T("กำล\u0e31งโหลดต\u0e31วละคร…", "Loading characters…") : L.T("กำล\u0e31งโหลดตลาด…", "Loading market…")));
			return;
		}
		if (array4.Length == 0)
		{
			Muted(g, new Rectangle(t.X, num5, t.Width, S(80f)), _mErr ?? L.T("ไม\u0e48พบรายการตามท\u0e35\u0e48ค\u0e49น", "No listings match"));
		}
		_mScroll = Math.Clamp(_mScroll, 0, Math.Max(0, array4.Length - num7));
		Font f = F(13f);
		Font f2 = F(11f);
		Font f3 = F(13f);
		Pen val = new Pen(Ui.K3);
		try
		{
			for (int num8 = 0; num8 < num7 && num8 + _mScroll < array4.Length; num8++)
			{
				MarketProduct marketProduct = array4[num8 + _mScroll];
				Rectangle r = new Rectangle(t.X, num5 + num8 * num4, t.Width, num4);
				bool num9 = marketProduct.ProductId == _mSel;
				string pid = marketProduct.ProductId;
				bool flag = AddHit(r, "lt-prod-" + pid, delegate
				{
					_mSel = pid;
					_mErr = null;
					_buy.Done();
					((Control)this).Invalidate();
				});
				if (num9 || flag)
				{
					Ui.Fill(g, r, 0f, Ui.K2);
				}
				if (num9)
				{
					SolidBrush val2 = new SolidBrush(Ui.Yellow);
					try
					{
						g.FillRectangle((Brush)(object)val2, r.X, r.Y, S(3f), r.Height);
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
				g.DrawLine(val, r.X, r.Bottom - 1, r.Right, r.Bottom - 1);
				int y = r.Y + (num4 - S(44f)) / 2;
				Slot(g, new Rectangle(r.X + array2[0], y, S(44f), S(44f)), marketProduct.Item, on: false, null, null, showHeld: false);
				int num10 = r.X + array2[1];
				int num11 = (marketProduct.Mine ? TagChip(g, num10 + Math.Min(array3[1] - S(60f), Ui.Measure(marketProduct.Item.Name, f).Width + S(6f)), r.Y + S(8f), L.T("ของฉ\u0e31น", "Mine"), "y") : 0);
				Ui.Draw(g, marketProduct.Item.Name, f, new Rectangle(num10, r.Y + S(6f), array3[1] - num11 - ((num11 > 0) ? S(6f) : 0), S(22f)), Ui.Text, (TextFormatFlags)32772);
				Ui.Draw(g, ((marketProduct.Item.TagLine.Length > 0) ? (marketProduct.Item.TagLine + " · ") : "") + L.T("ผ\u0e39\u0e49ขาย ", "Seller ") + marketProduct.SellerName, f2, new Rectangle(num10, r.Y + S(28f), array3[1], S(16f)), Ui.TextMute, (TextFormatFlags)32772);
				int? level = marketProduct.Level;
				Ui.Draw(g, (!level.HasValue) ? "—" : ("Lv " + level.GetValueOrDefault()), f3, new Rectangle(r.X + array2[2], r.Y, array3[2], num4), Ui.TextDim, (TextFormatFlags)4);
				level = marketProduct.Item.MaxDurability;
				if (level.HasValue)
				{
					int valueOrDefault = level.GetValueOrDefault();
					Ui.Draw(g, $"{marketProduct.Durability ?? marketProduct.Item.Durability.GetValueOrDefault()}/{valueOrDefault}", F(12f), new Rectangle(r.X + array2[3], r.Y + S(10f), array3[3], S(18f)), Ui.TextDim, (TextFormatFlags)4);
					DurBar(g, new Rectangle(r.X + array2[3], r.Y + S(32f), array3[3], S(4f)), marketProduct.Item.DurabilityFrac.GetValueOrDefault());
				}
				else
				{
					Ui.Draw(g, "—", f3, new Rectangle(r.X + array2[3], r.Y, array3[3], num4), Ui.TextDim, (TextFormatFlags)4);
				}
				Ui.Draw(g, "×" + marketProduct.Count, f3, new Rectangle(r.X + array2[4], r.Y, array3[4], num4), Ui.TextDim, (TextFormatFlags)4);
				Ui.Draw(g, TradeFmt.Left(marketProduct.ExpiresAt, now), f3, new Rectangle(r.X + array2[5], r.Y, array3[5], num4), Ui.TextMute, (TextFormatFlags)32772);
				Money(g, r.X + array2[6], r.Y, num4, marketProduct.Price);
				Rectangle r2 = new Rectangle(r.X + array2[7], r.Y + (num4 - S(30f)) / 2, array3[7], S(30f));
				if (marketProduct.Mine)
				{
					TradeBtn(g, r2, "lt-view-" + pid, null, L.T("ด\u0e39", "View"), "", enabled: true, delegate
					{
						_mSel = pid;
						((Control)this).Invalidate();
					});
					continue;
				}
				TradeBtn(g, r2, "lt-buy-" + pid, null, L.T("ซ\u0e37\u0e49อ", "Buy"), "y", !_buyBusy, delegate
				{
					_mSel = pid;
					_mErr = null;
					_buy.Done();
					((Control)this).Invalidate();
				});
			}
			Rectangle rectangle = new Rectangle(t.X, t.Bottom - num6 + S(4f), t.Width, S(30f));
			int total = _market.Total;
			int size = Math.Max(1, _market.PageSize);
			string s = ((total == 0) ? "" : L.T($"{_mSkip + 1}–{Math.Min(total, _mSkip + array4.Length)} / {total:N0}", $"{_mSkip + 1}–{Math.Min(total, _mSkip + array4.Length)} of {total:N0}"));
			Ui.Draw(g, s, F(12f), new Rectangle(rectangle.X + S(10f), rectangle.Y, S(200f), rectangle.Height), Ui.TextMute, (TextFormatFlags)4);
			TradeBtn(g, new Rectangle(rectangle.Right - S(72f), rectangle.Y, S(34f), S(30f)), "lt-next", "chevron-right", "", "", _mSkip + array4.Length < total && !_mBusy, delegate
			{
				_mSkip += size;
				MarketReload();
			});
			TradeBtn(g, new Rectangle(rectangle.Right - S(72f) - S(6f) - S(34f), rectangle.Y, S(34f), S(30f)), "lt-prev", "chevron-left", "", "", _mSkip > 0 && !_mBusy, delegate
			{
				_mSkip = Math.Max(0, _mSkip - size);
				MarketReload();
			});
			if (_mBusy)
			{
				Icons.Draw(g, "loader-2", new Point(rectangle.Right - S(150f), rectangle.Y + rectangle.Height / 2), S(16f), Ui.TextMute, _animT * 12);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static string ListingState(string state)
	{
		switch (state)
		{
		case "listed":
		case "selling":
		case "open":
		case "":
			return L.T("กำล\u0e31งขาย", "Listed");
		case "sold":
			return L.T("ขายแล\u0e49ว", "Sold");
		case "expired":
			return L.T("หมดเวลา", "Expired");
		case "frozen":
		case "held":
			return L.T("พ\u0e31กไว\u0e49", "Held");
		default:
			return state;
		}
	}

	private void CardTitle(Graphics g, Rectangle card, string title)
	{
		Ui.Draw(g, title, F(14f, medium: true), new Rectangle(card.X + S(14f), card.Y + S(12f), card.Width - S(28f), S(22f)), Ui.Text, (TextFormatFlags)4);
	}

	private int KV(Graphics g, int x, int y, int w, string k, string v, Color? vc = null, bool money = false, string? icon = null)
	{
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Expected O, but got Unknown
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Expected O, but got Unknown
		Font f = F(13f);
		Font val = F(13f);
		int num = Ui.Measure(k, f).Width;
		Ui.Draw(g, k, f, new Rectangle(x, y, num + S(2f), S(20f)), Ui.TextDim, (TextFormatFlags)4);
		if (icon != null)
		{
			Icons.Draw(g, icon, new Point(x + num + S(6f) + S(7f), y + S(10f)), S(13f), Ui.TextDim);
			num += S(20f);
		}
		if (money)
		{
			Money(g, x + w, y, S(20f), long.Parse(v, CultureInfo.InvariantCulture), 13f, right: true);
			return y + S(24f);
		}
		int num2 = w - num - S(8f);
		int val2 = Ui.MeasureWrapped(g, v, val, num2);
		StringFormat val3 = new StringFormat
		{
			Alignment = (StringAlignment)2,
			Trimming = (StringTrimming)3,
			FormatFlags = (StringFormatFlags)8192
		};
		try
		{
			SolidBrush val4 = new SolidBrush(vc ?? Ui.Text);
			try
			{
				g.DrawString(v, val, (Brush)(object)val4, new RectangleF(x + num + S(8f), y + 1, num2, Math.Min(val2, S(40f)) + 2), val3);
				return y + Math.Max(S(20f), Math.Min(val2, S(40f)) + S(2f)) + S(4f);
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
	}

	private void DrawBuyCard(Graphics g, Rectangle card, long now)
	{
		//IL_0a08: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a0d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a14: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a1b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a28: Expected O, but got Unknown
		//IL_0a2d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a34: Expected O, but got Unknown
		Ui.Card(g, card, S(10f), Ui.K1, Ui.K3);
		MarketProduct p = _market?.Products.FirstOrDefault((MarketProduct x) => x.ProductId == _mSel);
		if (p == null)
		{
			Muted(g, card, _mNote ?? L.T("เล\u0e37อกรายการทางซ\u0e49ายเพ\u0e37\u0e48อซ\u0e37\u0e49อ", "Pick a listing on the left to buy"));
			return;
		}
		int num = card.X + S(14f);
		int num2 = card.Width - S(28f);
		int num3 = card.Y + S(12f);
		CardTitle(g, card, p.Mine ? L.T("รายการของฉ\u0e31น", "My listing") : L.T("ย\u0e37นย\u0e31นการซ\u0e37\u0e49อ", "Confirm purchase"));
		if (!p.Mine)
		{
			Rectangle r = new Rectangle(card.Right - S(14f) - S(26f), num3 - S(2f), S(26f), S(26f));
			bool flag = AddHit(r, "lt-favtoggle", delegate
			{
				FavoriteAsync(p);
			}, !_favBusy);
			if (flag)
			{
				Ui.Fill(g, r, S(6f), Ui.K2);
			}
			if (p.Favorite)
			{
				Icons.Fill(g, "star-f", new RectangleF(r.X + S(5f), r.Y + S(5f), S(16f), S(16f)), Ui.Yellow);
			}
			else
			{
				Icons.Draw(g, "star", new Point(r.X + S(13f), r.Y + S(13f)), S(16f), flag ? Ui.Text : Ui.TextMute);
			}
		}
		num3 += S(32f);
		Slot(g, new Rectangle(num, num3, S(58f), S(58f)), p.Item, on: false, null, null, showHeld: false);
		Font f = F(14f, medium: true);
		int num4 = Math.Min(Ui.MeasureWrapped(g, p.Item.Name, f, num2 - S(68f)), S(40f));
		Ui.DrawWrapped(g, p.Item.Name, f, new Rectangle(num + S(68f), num3 + S(2f), num2 - S(68f), num4 + 2), Ui.Text);
		string s = (p.Item.IsEquip ? L.T($"Lv {p.Level ?? p.Item.Level.GetValueOrDefault()} · ความทน {p.Durability ?? p.Item.Durability.GetValueOrDefault()}/{p.Item.MaxDurability.GetValueOrDefault()}", $"Lv {p.Level ?? p.Item.Level.GetValueOrDefault()} · durability {p.Durability ?? p.Item.Durability.GetValueOrDefault()}/{p.Item.MaxDurability.GetValueOrDefault()}") : L.T($"×{p.Count} ช\u0e34\u0e49น", $"×{p.Count} pcs"));
		Ui.Draw(g, s, F(12f), new Rectangle(num + S(68f), num3 + S(4f) + num4, num2 - S(68f), S(18f)), Ui.TextMute, (TextFormatFlags)32772);
		num3 += S(58f) + S(12f);
		long num5;
		if (_buy.ProductId == p.ProductId)
		{
			long? newPrice = _buy.NewPrice;
			if (newPrice.HasValue)
			{
				long valueOrDefault = newPrice.GetValueOrDefault();
				num5 = valueOrDefault;
				goto IL_0619;
			}
		}
		num5 = p.Price;
		goto IL_0619;
		IL_0619:
		long num6 = num5;
		num3 = KV(g, num, num3, num2, L.T("ราคา", "Price"), num6.ToString(CultureInfo.InvariantCulture), null, money: true);
		num3 = ((!p.Mine) ? KV(g, num, num3, num2, L.T("ผ\u0e39\u0e49ขาย", "Seller"), p.SellerName) : KV(g, num, num3, num2, L.T("สถานะ", "Status"), ListingState(p.State)));
		num3 = KV(g, num, num3, num2, L.T("เหล\u0e37อเวลา", "Time left"), TradeFmt.Left(p.ExpiresAt, now));
		TradeCharacter tradeCharacter = Char(_buyer);
		if (!p.Mine)
		{
			num3 = KV(g, num, num3, num2, L.T("ส\u0e48งไปท\u0e35\u0e48", "Deliver to"), L.T("กล\u0e48องจดหมาย " + tradeCharacter?.Name + " · ร\u0e31บตอนเข\u0e49าเกม", tradeCharacter?.Name + "'s mailbox · collect in game"), null, money: false, "mail");
			long num7 = (tradeCharacter?.Tstone ?? 0) - num6;
			num3 = KV(g, num, num3, num2, L.T("คงเหล\u0e37อหล\u0e31งซ\u0e37\u0e49อ", "Balance after"), TradeFmt.T(num7), (num7 < 0) ? Ui.Bad : Ui.Text);
		}
		if (_mErr != null)
		{
			Font f2 = F(12f);
			int num8 = Ui.MeasureWrapped(g, _mErr, f2, num2 - S(22f));
			Icons.Draw(g, "alert-circle", new Point(num + S(7f), num3 + S(9f)), S(14f), Ui.Bad);
			Ui.DrawWrapped(g, _mErr, f2, new Rectangle(num + S(20f), num3, num2 - S(22f), num8 + 2), Ui.Bad);
			num3 += num8 + S(6f);
		}
		string text = (p.Mine ? L.T("ลงขาย/ถอนรายการ ทำในเกมเท\u0e48าน\u0e31\u0e49น", "List/withdraw in game only") : L.T("กดแล\u0e49วห\u0e31กเง\u0e34นท\u0e31นท\u0e35 ยกเล\u0e34กไม\u0e48ได\u0e49", "Charged immediately · cannot be cancelled"));
		Font val = F(11f);
		int num9 = Ui.MeasureWrapped(g, text, val, num2);
		int num10 = card.Bottom - S(14f) - num9 - S(6f) - S(40f);
		if (p.Mine)
		{
			TradeBtn(g, new Rectangle(num, num10, num2, S(40f)), "lt-collect", "gem", L.T("ร\u0e31บเง\u0e34นท\u0e35\u0e48ขายได\u0e49", "Collect sales"), "", !_collectBusy, delegate
			{
				CollectAsync();
			}, 14f);
		}
		else
		{
			bool enabled = !_buyBusy && tradeCharacter != null && tradeCharacter.Tstone >= num6;
			TradeBtn(g, new Rectangle(num, num10, num2, S(40f)), "lt-confirm", _buyBusy ? "loader-2" : "check", L.T("ย\u0e37นย\u0e31นซ\u0e37\u0e49อ " + TradeFmt.T(num6), "Buy for " + TradeFmt.T(num6)), "y", enabled, delegate
			{
				BuyAsync(p);
			}, 14f, _buyBusy);
		}
		StringFormat val2 = new StringFormat
		{
			Alignment = (StringAlignment)1,
			Trimming = (StringTrimming)3,
			FormatFlags = (StringFormatFlags)8192
		};
		try
		{
			SolidBrush val3 = new SolidBrush(Ui.TextMute);
			try
			{
				g.DrawString(text, val, (Brush)(object)val3, new RectangleF(num, num10 + S(40f) + S(6f), num2, num9 + 2), val2);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void DrawMineTable(Graphics g, Rectangle t, long now)
	{
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Expected O, but got Unknown
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d8: Expected O, but got Unknown
		int[] array = new int[5] { 44, 44, 64, 92, 72 };
		int[] array2 = Cols(t.Width, array);
		int num = S(8f);
		int num2 = S(10f);
		int num3 = t.Width - num2 * 2 - array.Sum((int w) => S(w)) - num * array.Length;
		int[] array3 = new int[6]
		{
			S(44f),
			num3,
			S(44f),
			S(64f),
			S(92f),
			S(72f)
		};
		TableHeader(g, t, array2, array3, new string[6]
		{
			"",
			L.T("รายการท\u0e35\u0e48ลงขายอย\u0e39\u0e48 (จากในเกม)", "Listed (from in game)"),
			L.T("เลเวล", "Level"),
			L.T("เหล\u0e37อ", "Left"),
			L.T("ราคา", "Price"),
			L.T("สถานะ", "State")
		});
		int num4 = t.Y + S(30f);
		int num5 = S(52f);
		if (_mine == null)
		{
			Muted(g, new Rectangle(t.X, num4, t.Width, S(80f)), _mErr ?? L.T("กำล\u0e31งโหลด…", "Loading…"));
			return;
		}
		Font f = F(13f);
		Pen val = new Pen(Ui.K3);
		try
		{
			int num6 = num4;
			if (_mine.Selling.Length == 0)
			{
				Muted(g, new Rectangle(t.X, num6, t.Width, S(50f)), L.T("ไม\u0e48ม\u0e35รายการท\u0e35\u0e48ลงขายอย\u0e39\u0e48 — ลงขายได\u0e49ในเกม", "Nothing listed — list items in game"));
				num6 += S(50f);
			}
			MarketProduct[] selling = _mine.Selling;
			foreach (MarketProduct marketProduct in selling)
			{
				if (num6 + num5 > t.Bottom - S(60f))
				{
					break;
				}
				Rectangle r = new Rectangle(t.X, num6, t.Width, num5);
				bool num8 = marketProduct.ProductId == _mSel;
				string pid = marketProduct.ProductId;
				bool flag = AddHit(r, "lt-mine-" + pid, delegate
				{
					_mSel = pid;
					((Control)this).Invalidate();
				});
				if (num8 || flag)
				{
					Ui.Fill(g, r, 0f, Ui.K2);
				}
				if (num8)
				{
					SolidBrush val2 = new SolidBrush(Ui.Yellow);
					try
					{
						g.FillRectangle((Brush)(object)val2, r.X, r.Y, S(3f), r.Height);
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
				g.DrawLine(val, r.X, r.Bottom - 1, r.Right, r.Bottom - 1);
				Slot(g, new Rectangle(r.X + array2[0], r.Y + S(4f), S(44f), S(44f)), marketProduct.Item, on: false, null, null, showHeld: false);
				Ui.Draw(g, marketProduct.Item.Name + ((marketProduct.Count > 1) ? $" ×{marketProduct.Count}" : ""), f, new Rectangle(r.X + array2[1], r.Y, array3[1], num5), Ui.Text, (TextFormatFlags)32772);
				int? level = marketProduct.Level;
				Ui.Draw(g, level.HasValue ? ("Lv " + level.GetValueOrDefault()) : "—", f, new Rectangle(r.X + array2[2], r.Y, array3[2], num5), Ui.TextDim, (TextFormatFlags)4);
				Ui.Draw(g, TradeFmt.Left(marketProduct.ExpiresAt, now), f, new Rectangle(r.X + array2[3], r.Y, array3[3], num5), Ui.TextMute, (TextFormatFlags)4);
				Money(g, r.X + array2[4], r.Y, num5, marketProduct.Price);
				TagChip(g, r.X + array2[5], r.Y + (num5 - S(20f)) / 2, ListingState(marketProduct.State), "m");
				num6 += num5;
			}
			num6 += S(10f);
			Ui.Draw(g, L.T($"ขายแล\u0e49ว {_mine.Sold.Length} รายการ", $"Sold {_mine.Sold.Length}"), F(13f, medium: true), new Rectangle(t.X + S(10f), num6, t.Width, S(22f)), Ui.Text, (TextFormatFlags)4);
			num6 += S(26f);
			Font f2 = F(12.5f);
			SoldItem[] sold = _mine.Sold;
			foreach (SoldItem soldItem in sold)
			{
				if (num6 + S(30f) > t.Bottom)
				{
					break;
				}
				Slot(g, new Rectangle(t.X + S(10f), num6, S(28f), S(28f)), soldItem.Item, on: false, null, null, showHeld: false);
				Ui.Draw(g, soldItem.Item.Name + L.T(" · ขายเม\u0e37\u0e48อ " + TradeFmt.Clock(soldItem.SoldAt, now), " · sold " + TradeFmt.Clock(soldItem.SoldAt, now)), f2, new Rectangle(t.X + S(46f), num6, array3[1] + S(100f), S(28f)), Ui.TextDim, (TextFormatFlags)32772);
				Money(g, t.Right - S(10f), num6, S(28f), soldItem.Price - soldItem.Tax, 12.5f, right: true);
				num6 += S(30f);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawMineCard(Graphics g, Rectangle card, long now)
	{
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		//IL_02eb: Expected O, but got Unknown
		//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Expected O, but got Unknown
		Ui.Card(g, card, S(10f), Ui.K1, Ui.K3);
		if (_mine == null)
		{
			Muted(g, card, L.T("กำล\u0e31งโหลด…", "Loading…"));
			return;
		}
		int num = card.X + S(14f);
		int num2 = card.Width - S(28f);
		int num3 = card.Y + S(12f);
		CardTitle(g, card, L.T("เง\u0e34นท\u0e35\u0e48ขายได\u0e49", "Sales to collect"));
		num3 += S(34f);
		num3 = KV(g, num, num3, num2, L.T("รายการท\u0e35\u0e48ขายแล\u0e49ว", "Sold items"), _mine.CollectCount.ToString());
		num3 = KV(g, num, num3, num2, L.T("ราคารวม", "Gross"), _mine.CollectGross.ToString(CultureInfo.InvariantCulture), null, money: true);
		num3 = KV(g, num, num3, num2, L.T("ภาษ\u0e35ตลาด", "Market tax"), "−" + TradeFmt.T(_mine.CollectTax), Ui.TextDim);
		num3 = KV(g, num, num3, num2, L.T("ได\u0e49ร\u0e31บส\u0e38ทธ\u0e34", "You receive"), _mine.CollectTotal.ToString(CultureInfo.InvariantCulture), null, money: true);
		if (_mErr != null)
		{
			Ui.DrawWrapped(g, _mErr, F(12f), new Rectangle(num, num3, num2, S(40f)), Ui.Bad);
			num3 += S(40f);
		}
		string text = L.T("ลงขาย/ถอนรายการ ทำในเกมเท\u0e48าน\u0e31\u0e49น", "List/withdraw in game only");
		Font val = F(11f);
		int num4 = Ui.MeasureWrapped(g, text, val, num2);
		int num5 = card.Bottom - S(14f) - num4 - S(6f) - S(40f);
		TradeBtn(g, new Rectangle(num, num5, num2, S(40f)), "lt-collect", _collectBusy ? "loader-2" : "gem", L.T("ร\u0e31บเง\u0e34นท\u0e35\u0e48ขายได\u0e49", "Collect sales"), (_mine.CollectCount > 0) ? "y" : "", _mine.CollectCount > 0 && !_collectBusy, delegate
		{
			CollectAsync();
		}, 14f, _collectBusy);
		StringFormat val2 = new StringFormat
		{
			Alignment = (StringAlignment)1,
			Trimming = (StringTrimming)3,
			FormatFlags = (StringFormatFlags)8192
		};
		try
		{
			SolidBrush val3 = new SolidBrush(Ui.TextMute);
			try
			{
				g.DrawString(text, val, (Brush)(object)val3, new RectangleF(num, num5 + S(40f) + S(6f), num2, num4 + 2), val2);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void DrawAuctionTable(Graphics g, Rectangle t, long now)
	{
		//IL_03c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c8: Expected O, but got Unknown
		//IL_0483: Unknown result type (might be due to invalid IL or missing references)
		//IL_048a: Expected O, but got Unknown
		int x = t.X;
		x += Chip(g, x, t.Y, "lt-au-search", L.T("ค\u0e49นหา", "Browse"), _auMode == "search", delegate
		{
			_auMode = "search";
			_mSel = null;
			MarketReload();
		}, "search") + S(6f);
		x += Chip(g, x, t.Y, "lt-au-bidding", L.T("ท\u0e35\u0e48ฉ\u0e31นประม\u0e39ล", "My bids"), _auMode == "mine" && _auKind == "bidding", delegate
		{
			_auMode = "mine";
			_auKind = "bidding";
			_mSel = null;
			MarketReload();
		}, "gavel") + S(6f);
		x += Chip(g, x, t.Y, "lt-au-selling", L.T("ท\u0e35\u0e48ฉ\u0e31นลงไว\u0e49 (จากในเกม)", "My lots (listed in game)"), _auMode == "mine" && _auKind == "selling", delegate
		{
			_auMode = "mine";
			_auKind = "selling";
			_mSel = null;
			MarketReload();
		}, "box") + S(6f);
		t = new Rectangle(t.X, t.Y + S(36f), t.Width, t.Height - S(36f));
		int[] array = new int[6] { 44, 44, 92, 48, 64, 70 };
		int[] array2 = Cols(t.Width, array);
		int num = S(8f);
		int num2 = S(10f);
		int num3 = t.Width - num2 * 2 - array.Sum((int w) => S(w)) - num * array.Length;
		int[] array3 = new int[7]
		{
			S(44f),
			num3,
			S(44f),
			S(92f),
			S(48f),
			S(64f),
			S(70f)
		};
		TableHeader(g, t, array2, array3, new string[7]
		{
			"",
			L.T("ไอเทม", "Item"),
			L.T("เลเวล", "Level"),
			L.T("ราคาตอนน\u0e35\u0e49", "Current"),
			L.T("บ\u0e34ด", "Bids"),
			L.T("เหล\u0e37อ", "Left"),
			""
		});
		int num4 = t.Y + S(30f);
		int num5 = S(52f);
		if (_au == null)
		{
			Muted(g, new Rectangle(t.X, num4, t.Width, S(80f)), _mErr ?? L.T("กำล\u0e31งโหลดประม\u0e39ล…", "Loading auctions…"));
			return;
		}
		AuctionLot[] lots = _au.Lots;
		int num6 = Math.Max(1, (t.Bottom - num4) / num5);
		if (lots.Length == 0)
		{
			Muted(g, new Rectangle(t.X, num4, t.Width, S(60f)), _mErr ?? L.T("ไม\u0e48ม\u0e35ล\u0e47อตประม\u0e39ล", "No lots"));
		}
		_mScroll = Math.Clamp(_mScroll, 0, Math.Max(0, lots.Length - num6));
		Font f = F(13f);
		Font f2 = F(11f);
		Pen val = new Pen(Ui.K3);
		try
		{
			for (int num7 = 0; num7 < num6 && num7 + _mScroll < lots.Length; num7++)
			{
				AuctionLot l = lots[num7 + _mScroll];
				Rectangle r = new Rectangle(t.X, num4 + num7 * num5, t.Width, num5);
				bool num8 = l.Id == _mSel;
				string lid = l.Id;
				bool flag = AddHit(r, "lt-lot-" + lid, delegate
				{
					_mSel = lid;
					_mErr = null;
					((Control)_boxes["au_amt"]).Text = TradeFmt.N(Math.Max(l.MinBid, l.Current + 1));
					((Control)this).Invalidate();
				});
				if (num8 || flag)
				{
					Ui.Fill(g, r, 0f, Ui.K2);
				}
				if (num8)
				{
					SolidBrush val2 = new SolidBrush(Ui.Yellow);
					try
					{
						g.FillRectangle((Brush)(object)val2, r.X, r.Y, S(3f), r.Height);
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
				g.DrawLine(val, r.X, r.Bottom - 1, r.Right, r.Bottom - 1);
				Slot(g, new Rectangle(r.X + array2[0], r.Y + S(4f), S(44f), S(44f)), l.Item, on: false, null, null, showHeld: false);
				Ui.Draw(g, (l.Item.Name.Length > 0) ? (l.Item.Name + ((l.Count > 1) ? $" ×{l.Count}" : "")) : l.Proto, f, new Rectangle(r.X + array2[1], r.Y + S(6f), array3[1], S(22f)), Ui.Text, (TextFormatFlags)32772);
				string s = L.T("ผ\u0e39\u0e49ขาย ", "Seller ") + l.SellerName + ((l.TopName.Length > 0) ? (L.T(" · นำโดย ", " · top ") + l.TopName) : "") + (l.Leading ? L.T(" · ค\u0e38ณนำอย\u0e39\u0e48", " · you lead") : (l.Bidded ? L.T(" · ถ\u0e39กแซง", " · outbid") : ""));
				Ui.Draw(g, s, f2, new Rectangle(r.X + array2[1], r.Y + S(28f), array3[1], S(16f)), l.Leading ? Ui.Good : (l.Bidded ? Ui.Bad : Ui.TextMute), (TextFormatFlags)32772);
				Ui.Draw(g, (l.Level > 0) ? ("Lv " + l.Level) : "—", f, new Rectangle(r.X + array2[2], r.Y, array3[2], num5), Ui.TextDim, (TextFormatFlags)4);
				Money(g, r.X + array2[3], r.Y, num5, (l.Current > 0) ? l.Current : l.StartPrice);
				Ui.Draw(g, l.BidCount.ToString(), f, new Rectangle(r.X + array2[4], r.Y, array3[4], num5), Ui.TextDim, (TextFormatFlags)4);
				Ui.Draw(g, (l.RemainSec > 0) ? TradeFmt.Span(l.RemainSec) : TradeFmt.Left(l.EndsAt, now), f, new Rectangle(r.X + array2[5], r.Y, array3[5], num5), Ui.TextMute, (TextFormatFlags)4);
				if (!l.Mine)
				{
					TradeBtn(g, new Rectangle(r.X + array2[6], r.Y + (num5 - S(30f)) / 2, array3[6], S(30f)), "lt-bid-" + lid, null, L.T("ประม\u0e39ล", "Bid"), "y", enabled: true, delegate
					{
						_mSel = lid;
						((Control)_boxes["au_amt"]).Text = TradeFmt.N(Math.Max(l.MinBid, l.Current + 1));
						_focusNext = "au_amt";
						((Control)this).Invalidate();
					});
				}
				else
				{
					TagChip(g, r.X + array2[6], r.Y + (num5 - S(20f)) / 2, L.T("ของฉ\u0e31น", "Mine"), "y");
				}
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawAuctionCard(Graphics g, Rectangle card, long now)
	{
		Ui.Card(g, card, S(10f), Ui.K1, Ui.K3);
		int num = card.X + S(14f);
		int num2 = card.Width - S(28f);
		int num3 = card.Y + S(12f);
		AuctionClaim[] array = _au?.Claims ?? Array.Empty<AuctionClaim>();
		AuctionLot auctionLot = _au?.Lots.FirstOrDefault((AuctionLot l) => l.Id == _mSel);
		if (auctionLot == null && array.Length == 0)
		{
			Muted(g, card, _mErr ?? L.T("เล\u0e37อกล\u0e47อตทางซ\u0e49ายเพ\u0e37\u0e48อประม\u0e39ล", "Pick a lot on the left to bid"));
			return;
		}
		if (auctionLot != null)
		{
			CardTitle(g, card, auctionLot.Mine ? L.T("ล\u0e47อตของฉ\u0e31น", "My lot") : L.T("ประม\u0e39ล", "Place a bid"));
			num3 += S(34f);
			Slot(g, new Rectangle(num, num3, S(58f), S(58f)), auctionLot.Item, on: false, null, null, showHeld: false);
			Font f = F(14f, medium: true);
			string s = ((auctionLot.Item.Name.Length > 0) ? auctionLot.Item.Name : auctionLot.Proto);
			int num4 = Math.Min(Ui.MeasureWrapped(g, s, f, num2 - S(68f)), S(40f));
			Ui.DrawWrapped(g, s, f, new Rectangle(num + S(68f), num3 + S(2f), num2 - S(68f), num4 + 2), Ui.Text);
			Ui.Draw(g, L.T($"×{auctionLot.Count} · ผ\u0e39\u0e49ขาย {auctionLot.SellerName}", $"×{auctionLot.Count} · seller {auctionLot.SellerName}"), F(12f), new Rectangle(num + S(68f), num3 + S(4f) + num4, num2 - S(68f), S(18f)), Ui.TextMute, (TextFormatFlags)32772);
			num3 += S(58f) + S(12f);
			num3 = KV(g, num, num3, num2, L.T("ราคาตอนน\u0e35\u0e49", "Current"), ((auctionLot.Current > 0) ? auctionLot.Current : auctionLot.StartPrice).ToString(CultureInfo.InvariantCulture), null, money: true);
			num3 = KV(g, num, num3, num2, L.T("ประม\u0e39ลข\u0e31\u0e49นต\u0e48ำ", "Minimum bid"), auctionLot.MinBid.ToString(CultureInfo.InvariantCulture), null, money: true);
			num3 = KV(g, num, num3, num2, L.T("จำนวนบ\u0e34ด", "Bids"), auctionLot.BidCount + ((auctionLot.TopName.Length > 0) ? (" · " + auctionLot.TopName) : ""));
			num3 = KV(g, num, num3, num2, L.T("เหล\u0e37อเวลา", "Time left"), (auctionLot.RemainSec > 0) ? TradeFmt.Span(auctionLot.RemainSec) : TradeFmt.Left(auctionLot.EndsAt, now));
			if (!auctionLot.Mine)
			{
				Ui.Draw(g, L.T("จำนวน T ท\u0e35\u0e48ประม\u0e39ล", "Your bid (T)"), F(12.5f), new Rectangle(num, num3, num2, S(18f)), Ui.TextDim, (TextFormatFlags)4);
				num3 += S(20f);
				InputBox(g, new Rectangle(num, num3, num2, S(34f)), "au_amt", TradeFmt.N(auctionLot.MinBid), disabled: false, "gem");
				num3 += S(34f) + S(8f);
				TradeCharacter tradeCharacter = Char(_buyer);
				Ui.Draw(g, L.T("ม\u0e35 " + TradeFmt.T(tradeCharacter?.Tstone ?? 0) + " · ชนะแล\u0e49วของเข\u0e49าจดหมาย", "You have " + TradeFmt.T(tradeCharacter?.Tstone ?? 0) + " · wins go to mail"), F(11.5f), new Rectangle(num, num3, num2, S(18f)), Ui.TextMute, (TextFormatFlags)32772);
				num3 += S(22f);
				long num5 = TradeFmt.ParseT(((Control)_boxes["au_amt"]).Text);
				TradeBtn(g, new Rectangle(num, num3, num2, S(40f)), "lt-bid-go", _bidBusy ? "loader-2" : "gavel", L.T("ประม\u0e39ล " + TradeFmt.T(num5), "Bid " + TradeFmt.T(num5)), "y", !_bidBusy && num5 >= Math.Max(1L, auctionLot.MinBid), delegate
				{
					BidAsync();
				}, 14f, _bidBusy);
				num3 += S(40f) + S(8f);
			}
		}
		if (_mErr != null)
		{
			Ui.DrawWrapped(g, _mErr, F(12f), new Rectangle(num, num3, num2, S(40f)), Ui.Bad);
			num3 += S(42f);
		}
		if (array.Length == 0)
		{
			return;
		}
		Ui.Draw(g, L.T($"รอร\u0e31บ {array.Length} รายการ", $"{array.Length} to claim"), F(13f, medium: true), new Rectangle(num, num3, num2, S(20f)), Ui.Text, (TextFormatFlags)4);
		num3 += S(24f);
		foreach (AuctionClaim item in array.Take(3))
		{
			string s2 = ((item.Text.Length > 0) ? item.Text : (item.Item?.Name ?? ((item.Tstone > 0) ? TradeFmt.T(item.Tstone) : item.Id)));
			Ui.Draw(g, s2, F(12f), new Rectangle(num, num3, num2 - S(70f), S(30f)), Ui.TextDim, (TextFormatFlags)32772);
			string cid = item.Id;
			TradeBtn(g, new Rectangle(num + num2 - S(64f), num3, S(64f), S(30f)), "lt-claim-" + cid, null, L.T("ร\u0e31บของ", "Claim"), "y", !_claimBusy, delegate
			{
				ClaimAsync((cid.Length > 0) ? cid : null);
			});
			num3 += S(34f);
		}
		if (array.Length > 1)
		{
			TradeBtn(g, new Rectangle(num, num3, num2, S(34f)), "lt-claim-all", null, L.T("ร\u0e31บท\u0e31\u0e49งหมด", "Claim all"), "", !_claimBusy, delegate
			{
				ClaimAsync(null);
			});
		}
	}

	private async Task PollOffersAsync()
	{
		if (_lt == null)
		{
			return;
		}
		string me = _trader;
		if (me == null)
		{
			_offerNext = DateTime.UtcNow.AddSeconds(2.0);
			return;
		}
		_offerBusy = true;
		int gen = _ltGen;
		try
		{
			string[] array = ((!(_tTab == "hist")) ? new string[2] { "in", "out" } : new string[3] { "in", "out", "hist" });
			string[] array2 = array;
			foreach (string box in array2)
			{
				OfferList offerList = await _lt.OffersAsync(me, box);
				if (gen != _ltGen || ((Control)this).IsDisposed || me != _trader)
				{
					return;
				}
				_offers[box] = offerList.Offers;
				if (offerList.Now > 0)
				{
					_offerSkew = offerList.Now - NowUnix;
				}
				TradeOffer[] offers = offerList.Offers;
				foreach (TradeOffer tradeOffer in offers)
				{
					if (tradeOffer.FeeRate > 0.0)
					{
						_feeRate = tradeOffer.FeeRate;
					}
				}
			}
			_offerBack.Ok();
			_offerNext = DateTime.UtcNow.AddMilliseconds(10000.0);
			if (_accept.NeedReload && _accept.Reloaded(OffersOf("in").FirstOrDefault((TradeOffer o) => o.Id == _accept.OfferId)))
			{
				_tErr = L.T("ข\u0e49อเสนอเปล\u0e35\u0e48ยนแล\u0e49ว — ตรวจของอ\u0e35กคร\u0e31\u0e49งแล\u0e49วกดตกลงเทรดอ\u0e35กคร\u0e31\u0e49ง", "The offer changed — check it again, then press Accept");
			}
			if (_tSel != null && !OffersOf(_tTab).Any((TradeOffer o) => o.Id == _tSel))
			{
				_tSel = null;
			}
			((Control)this).Invalidate();
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "trade"))
			{
				bool flag = Backoff.ShouldBackoff(apiError);
				_offerNext = DateTime.UtcNow.AddMilliseconds(flag ? _offerBack.Fail(apiError.RetryAfter) : 10000);
				if (flag)
				{
					NoteBackoff(_offerBack, apiError);
				}
				else
				{
					_tErr = apiError.Message;
				}
				((Control)this).Invalidate();
			}
		}
		catch (Exception ex)
		{
			Log.Write("offers: " + ex);
			_offerNext = DateTime.UtcNow.AddMilliseconds(_offerBack.Fail());
		}
		finally
		{
			_offerBusy = false;
		}
	}

	private void SelectOffer(TradeOffer o)
	{
		if (!(_tSel == o.Id))
		{
			_tSel = o.Id;
			_tErr = null;
			_tChatErr = null;
			_tConfirm = null;
			if (_tChatFor != o.Id)
			{
				_tChat.Clear();
				_tChatSince = 0L;
				_tChatInfo = null;
				_tChatFor = o.Id;
				_tChatScroll = 0;
			}
			_ochatNext = DateTime.MinValue;
			((Control)this).Invalidate();
		}
	}

	private async Task PollOfferChatAsync()
	{
		if (_lt == null || _trader == null)
		{
			return;
		}
		TradeOffer selectedOffer = SelectedOffer;
		if (selectedOffer == null)
		{
			_ochatNext = DateTime.UtcNow.AddSeconds(2.0);
			return;
		}
		if (_tChatFor != selectedOffer.Id)
		{
			_tChat.Clear();
			_tChatSince = 0L;
			_tChatInfo = null;
			_tChatFor = selectedOffer.Id;
			_tChatScroll = 0;
		}
		_ochatBusy = true;
		int gen = _ltGen;
		string trader = _trader;
		string oid = selectedOffer.Id;
		try
		{
			OfferChat offerChat = await _lt.OfferChatAsync(trader, oid, _tChatSince);
			if (gen != _ltGen || ((Control)this).IsDisposed || _tChatFor != oid)
			{
				return;
			}
			_tChatInfo = offerChat;
			if (offerChat.Messages.Length != 0)
			{
				HashSet<long> known = _tChat.Select((OfferChatMsg m) => m.Id).ToHashSet();
				_tChat.AddRange(offerChat.Messages.Where((OfferChatMsg m) => !known.Contains(m.Id)));
				_tChat.Sort((OfferChatMsg a, OfferChatMsg b) => (a.T == b.T) ? a.Id.CompareTo(b.Id) : a.T.CompareTo(b.T));
				_tChatSince = Math.Max(_tChatSince, offerChat.Messages.Max((OfferChatMsg m) => m.T));
			}
			_ochatBack.Ok();
			_ochatNext = DateTime.UtcNow.AddMilliseconds(10000.0);
			((Control)this).Invalidate();
		}
		catch (ApiError apiError)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && !HandleTradeError(apiError, "trade"))
			{
				bool flag = Backoff.ShouldBackoff(apiError);
				_ochatNext = DateTime.UtcNow.AddMilliseconds(flag ? _ochatBack.Fail(apiError.RetryAfter) : 10000);
				if (!flag)
				{
					_tChatErr = apiError.Message;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Write("offer chat: " + ex);
			_ochatNext = DateTime.UtcNow.AddMilliseconds(_ochatBack.Fail());
		}
		finally
		{
			_ochatBusy = false;
		}
	}

	private async Task SendOfferChatAsync()
	{
		if (_lt == null || _trader == null || _tChatSending)
		{
			return;
		}
		TradeOffer selectedOffer = SelectedOffer;
		string text = ((Control)_boxes["t_chat"]).Text.Trim();
		if (selectedOffer == null || text.Length == 0)
		{
			return;
		}
		_tChatSending = true;
		_tChatErr = null;
		int gen = _ltGen;
		try
		{
			await _lt.OfferChatSayAsync(_trader, selectedOffer.Id, text);
			if (gen == _ltGen && !((Control)this).IsDisposed)
			{
				((Control)_boxes["t_chat"]).Text = "";
				_ochatNext = DateTime.UtcNow.AddMilliseconds(300.0);
				_focusNext = "t_chat";
			}
		}
		catch (ApiError apiError)
		{
			if (gen != _ltGen || ((Control)this).IsDisposed || HandleTradeError(apiError, "trade"))
			{
				return;
			}
			LauncherForm launcherForm = this;
			string tChatErr;
			if (apiError.Code == "cooldown")
			{
				int? wait = apiError.Wait;
				if (wait.HasValue)
				{
					int valueOrDefault = wait.GetValueOrDefault();
					tChatErr = L.T($"ส\u0e48งได\u0e49อ\u0e35ก {valueOrDefault} ว\u0e34", $"Wait {valueOrDefault} s");
					goto IL_0233;
				}
			}
			tChatErr = apiError.Message;
			goto IL_0233;
			IL_0233:
			launcherForm._tChatErr = tChatErr;
		}
		finally
		{
			_tChatSending = false;
			((Control)this).Invalidate();
		}
	}

	private bool Confirmed(string id, string act)
	{
		(string, string, DateTime)? tConfirm = _tConfirm;
		if (tConfirm.HasValue)
		{
			(string, string, DateTime) valueOrDefault = tConfirm.GetValueOrDefault();
			if (valueOrDefault.Item1 == id && valueOrDefault.Item2 == act && DateTime.UtcNow < valueOrDefault.Item3)
			{
				_tConfirm = null;
				return true;
			}
		}
		_tConfirm = (id, act, DateTime.UtcNow.AddSeconds(4.0));
		((Control)this).Invalidate();
		return false;
	}

	private async Task OfferActAsync(TradeOffer o, string act)
	{
		if (_lt == null || _trader == null || _offerActBusy)
		{
			return;
		}
		string me = _trader;
		_offerActBusy = true;
		_tErr = null;
		UpdateAnim();
		int gen = _ltGen;
		try
		{
			switch (act)
			{
			case "accept":
			{
				_accept.Start(o);
				TradeOffer item = (await _lt.OfferAcceptAsync(me, o.Id, o.Version)).Item1;
				if (gen != _ltGen || ((Control)this).IsDisposed)
				{
					return;
				}
				_accept.Done();
				ToastT(L.T("เทรดก\u0e31บ " + item.Other(me).Name + " สำเร\u0e47จ · ของเข\u0e49ากล\u0e48องจดหมายท\u0e31\u0e49งสองฝ\u0e48าย ร\u0e31บตอนเข\u0e49าเกม", "Trade with " + item.Other(me).Name + " done · items are in both mailboxes"));
				_tTab = "hist";
				_tSel = item.Id;
				break;
			}
			case "decline":
				await _lt.OfferDeclineAsync(me, o.Id);
				if (gen != _ltGen || ((Control)this).IsDisposed)
				{
					return;
				}
				ToastT(L.T("ปฏ\u0e34เสธข\u0e49อเสนอแล\u0e49ว", "Offer declined"), "x", Ui.TextDim);
				break;
			case "cancel":
				await _lt.OfferCancelAsync(me, o.Id);
				if (gen != _ltGen || ((Control)this).IsDisposed)
				{
					return;
				}
				ToastT(L.T("ยกเล\u0e34กข\u0e49อเสนอแล\u0e49ว · ของค\u0e37นทางจดหมาย", "Offer cancelled · items returned by mail"), "x", Ui.TextDim);
				break;
			}
			_offerNext = DateTime.MinValue;
			_sumNext = DateTime.MinValue;
			LoadCharactersAsync();
		}
		catch (ApiError apiError)
		{
			if (gen != _ltGen || ((Control)this).IsDisposed || HandleTradeError(apiError, "trade"))
			{
				return;
			}
			if (act == "accept" && _accept.Fail(apiError))
			{
				_tErr = apiError.Message;
				_offerNext = DateTime.MinValue;
				return;
			}
			_tErr = TradeErrText(apiError);
			bool flag;
			switch (apiError.Code)
			{
			case "gone":
			case "state":
			case "expired":
			case "changed":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				_offerNext = DateTime.MinValue;
			}
		}
		catch (Exception ex)
		{
			Log.Write("offer " + act + ": " + ex);
			_tErr = L.T("ทำรายการไม\u0e48สำเร\u0e47จ — ลองใหม\u0e48", "Action failed — try again");
		}
		finally
		{
			_offerActBusy = false;
			UpdateAnim();
			((Control)this).Invalidate();
		}
	}

	private void DrawTradeLocked(Graphics g, Rectangle pane)
	{
		int top = CalHeader(g, pane, L.T("เทรด", "Trade"), 0, L.T("เทรดผ\u0e48านร\u0e31นเช\u0e48อเป\u0e34ดให\u0e49บ\u0e31ญช\u0e35 Supporter เท\u0e48าน\u0e31\u0e49น", "Trading in the launcher is for Supporter accounts only"));
		CenterPanel(g, Rectangle.FromLTRB(pane.X, top, pane.Right, pane.Bottom), "lock", L.T("เทรดแบบข\u0e49อเสนอ สำหร\u0e31บ Supporter", "Offer trading for Supporters"), L.T("ส\u0e48งข\u0e49อเสนอแลกไอเทมและ T Stone ก\u0e31บผ\u0e39\u0e49เล\u0e48นอ\u0e37\u0e48นได\u0e49โดยไม\u0e48ต\u0e49องออนไลน\u0e4cพร\u0e49อมก\u0e31น พร\u0e49อมห\u0e49องแชทส\u0e48วนต\u0e31วของแต\u0e48ละข\u0e49อเสนอ", "Offer items and T Stone to other players without being online together, with a private chat room for each offer"), new(string, string)[2]
		{
			("star", L.T("บ\u0e31ญช\u0e35 Supporter ท\u0e31\u0e49งสองฝ\u0e48าย", "Supporter accounts on both sides")),
			("user", L.T("ต\u0e31วละคร Lv 60 ข\u0e36\u0e49นไปท\u0e31\u0e49งสองฝ\u0e48าย", "Characters Lv 60+ on both sides"))
		}, (Text: L.T("ด\u0e39ส\u0e34ทธ\u0e34\u0e4c Supporter", "See Supporter perks"), Icon: "star", Click: delegate
		{
			OpenUrl("https://discord.gg/RMfeg6Drbu");
		}, Spin: false), L.T("ต\u0e31วละคร · แชท · ปฏ\u0e34ท\u0e34นก\u0e34จกรรม ใช\u0e49ได\u0e49ท\u0e38กบ\u0e31ญช\u0e35", "Characters · chat · event calendar work for every account"));
	}

	private void DrawTrade(Graphics g, Rectangle pane)
	{
		if (_gate.Locked)
		{
			DrawTradeLocked(g, pane);
			return;
		}
		long now = NowUnix + _offerSkew;
		int num = CharPickerW(L.T("ในนาม", "As"), _trader);
		int num2 = TradeBtnW("plus", L.T("สร\u0e49างข\u0e49อเสนอใหม\u0e48", "New offer"), "y", 13.5f);
		int y = TradeHeader(g, pane, L.T("เทรด", "Trade"), num + S(8f) + num2, L.T("เสนอแลกไอเทม/T Stone ให\u0e49ผ\u0e39\u0e49เล\u0e48นอ\u0e37\u0e48น ไม\u0e48ต\u0e49องออนไลน\u0e4cพร\u0e49อมก\u0e31น · ค\u0e38ยในห\u0e49องแชทส\u0e48วนต\u0e31วของข\u0e49อเสนอ · ของและเง\u0e34นพ\u0e31กท\u0e35\u0e48เซ\u0e34ร\u0e4cฟ", "Offer items/T Stone to another player, no need to be online together · chat privately per offer · items and T are held on the server"));
		CharPicker(g, pane.Right - num, pane.Y + S(22f), S(34f), "lt-trader", L.T("ในนาม", "As"), _trader, delegate(string id)
		{
			_trader = id;
			_offers.Clear();
			_tSel = null;
			_offerNext = DateTime.MinValue;
			((Control)this).Invalidate();
		});
		TradeBtn(g, new Rectangle(pane.Right - num - S(8f) - num2, pane.Y + S(22f), num2, S(34f)), "lt-newoffer", "plus", L.T("สร\u0e49างข\u0e49อเสนอใหม\u0e48", "New offer"), "y", _trader != null, delegate
		{
			OpenOffer(OfferMode.New, null, null);
		}, 13.5f);
		Rectangle r = new Rectangle(pane.X, y, pane.Width, S(40f));
		Ui.Card(g, r, S(10f), Ui.K1, Ui.K3);
		Icons.Draw(g, "lock", new Point(r.X + S(14f) + S(8f), r.Y + r.Height / 2), S(17f), Ui.Yellow);
		string text = ((_feeRate > 0.0) ? L.T(" (ค\u0e48าธรรมเน\u0e35ยม " + TradeFmt.Pct(_feeRate) + " ของ T ท\u0e35\u0e48ส\u0e48ง)", " (" + TradeFmt.Pct(_feeRate) + " fee on T sent)") : L.T(" (ม\u0e35ค\u0e48าธรรมเน\u0e35ยมจาก T ท\u0e35\u0e48ส\u0e48ง)", " (a fee applies to T sent)"));
		Ui.Draw(g, L.T("ท\u0e31\u0e49งสองฝ\u0e48าย Lv 60 ข\u0e36\u0e49นไป · แลกได\u0e49ท\u0e31\u0e49งไอเทมและ T Stone", "Both sides Lv 60+ · trade items and T Stone") + text + L.T(" · หมดอาย\u0e38ใน 24 ชม. ของค\u0e37นเจ\u0e49าของอ\u0e31ตโนม\u0e31ต\u0e34 · ห\u0e49องแชทเห\u0e47นแค\u0e48ค\u0e39\u0e48เทรด", " · expires in 24 hr, items return automatically · chat is visible to the two of you only"), F(13f), Rectangle.FromLTRB(r.X + S(38f), r.Y, r.Right - S(12f), r.Bottom), Ui.TextDim, (TextFormatFlags)32772);
		int num3 = r.Bottom + S(10f);
		int num4 = S(240f);
		int num5 = OffersOf("in").Count((TradeOffer tradeOffer) => tradeOffer.Open);
		int num6 = OffersOf("out").Count((TradeOffer tradeOffer) => tradeOffer.Open);
		(string, string, string, string, string)[] tabs = new(string, string, string, string, string)[3]
		{
			("in", L.T("ร\u0e31บเข\u0e49า", "Incoming"), (num5 > 0) ? num5.ToString() : null, null, null),
			("out", L.T("ส\u0e48งออก", "Outgoing"), (num6 > 0) ? num6.ToString() : null, null, null),
			("hist", L.T("ประว\u0e31ต\u0e34", "History"), null, null, null)
		};
		Tabs(g, pane.X, num3, num4, tabs, _tTab, delegate(string t)
		{
			_tTab = t;
			_tSel = null;
			_tErr = null;
			_tListScroll = 0;
			_tConfirm = null;
			_offerNext = DateTime.MinValue;
			_ochatNext = DateTime.MinValue;
			((Control)this).Invalidate();
		});
		TradeOffer[] source = OffersOf(_tTab).ToArray();
		source = ((!(_tTab == "hist")) ? source.Where((TradeOffer tradeOffer) => tradeOffer.Open).ToArray() : source.Where((TradeOffer tradeOffer) => !tradeOffer.Open).ToArray());
		int num7 = num3 + S(46f);
		int num8 = S(70f);
		int num9 = S(8f);
		int num10 = Math.Max(1, (pane.Bottom - num7 + num9) / (num8 + num9));
		_tListScroll = Math.Clamp(_tListScroll, 0, Math.Max(0, source.Length - num10));
		string text2 = _trader ?? "";
		if (source.Length == 0)
		{
			Muted(g, new Rectangle(pane.X, num7, num4, S(80f)), (_trader == null) ? L.T("กำล\u0e31งโหลดต\u0e31วละคร…", "Loading characters…") : ((!_offers.ContainsKey("in")) ? (_tErr ?? L.T("กำล\u0e31งโหลดข\u0e49อเสนอ…", "Loading offers…")) : ((_tTab == "in") ? L.T("ย\u0e31งไม\u0e48ม\u0e35ข\u0e49อเสนอเข\u0e49ามา", "No incoming offers") : ((_tTab == "out") ? L.T("ย\u0e31งไม\u0e48ได\u0e49ส\u0e48งข\u0e49อเสนอ", "No outgoing offers") : L.T("ย\u0e31งไม\u0e48ม\u0e35ประว\u0e31ต\u0e34", "No history yet")))));
		}
		TradeOffer sel = SelectedOffer;
		for (int num11 = 0; num11 < num10 && num11 + _tListScroll < source.Length; num11++)
		{
			TradeOffer o = source[num11 + _tListScroll];
			Rectangle r2 = new Rectangle(pane.X, num7 + num11 * (num8 + num9), num4, num8);
			bool flag = sel != null && o.Id == sel.Id;
			bool flag2 = AddHit(r2, "lt-off-" + o.Id, delegate
			{
				SelectOffer(o);
			});
			Ui.Card(g, r2, S(10f), Ui.K1, flag ? Ui.Yellow : (flag2 ? Ui.PurpleLight : Ui.K3));
			OfferSide offerSide = o.Other(text2);
			DrawAvatar(g, new Rectangle(r2.X + S(12f), r2.Y + S(17f), S(36f), S(36f)), offerSide.Name, "", Ui.K1, badge: false);
			(string Text, OfferTag.Kind Kind) tuple = OfferTag.Of(o, text2);
			string text3 = tuple.Text;
			OfferTag.Kind kind = tuple.Kind;
			string text4 = OfferTag.EditedChip(o, text2);
			if (text4 != null)
			{
				kind = OfferTag.Kind.Yellow;
				text3 = text4;
			}
			int x = r2.Right - S(12f);
			int y2 = r2.Y + S(10f);
			string text5 = text3;
			int num12 = TagChip(g, x, y2, text5, kind switch
			{
				OfferTag.Kind.Yellow => "y", 
				OfferTag.Kind.Green => "g", 
				OfferTag.Kind.Red => "r", 
				_ => "m", 
			}, null, right: true);
			int num13 = r2.X + S(58f);
			int num14 = r2.Width - S(58f) - S(12f) - num12 - S(6f);
			Font f = F(13.5f, medium: true);
			int num15 = Math.Min(num14 - S(40f), Ui.Measure(offerSide.Name, f).Width);
			Ui.Draw(g, offerSide.Name, f, new Rectangle(num13, r2.Y + S(8f), num15 + S(2f), S(20f)), Ui.Text, (TextFormatFlags)32772);
			Ui.Draw(g, "Lv " + offerSide.Level, F(12f), new Rectangle(num13 + num15 + S(6f), r2.Y + S(8f), S(60f), S(20f)), Ui.TextMute, (TextFormatFlags)4);
			var (offerBundle, offerBundle2) = o.Sides(text2);
			Ui.Draw(g, OfferTag.GiveLine(o.IsIncoming(text2) ? offerBundle : offerBundle2), F(12f), new Rectangle(num13, r2.Y + S(28f), r2.Width - S(58f) - S(12f), S(17f)), Ui.TextMute, (TextFormatFlags)32772);
			string s = (o.Open ? TradeFmt.ExpiresIn(o.ExpiresAt, now) : TradeFmt.Clock((o.ClosedAt > 0) ? o.ClosedAt : o.ExpiresAt, now)) + ((o.Edited && o.Open) ? (" · " + OfferTag.EditedLine(o, now)) : "");
			Ui.Draw(g, s, F(12f), new Rectangle(num13, r2.Y + S(45f), r2.Width - S(58f) - S(12f), S(17f)), Ui.TextMute, (TextFormatFlags)32772);
			if (o.Unread > 0 && !flag)
			{
				NavBadge(g, new Rectangle(r2.Right - S(44f), r2.Bottom - S(30f), S(60f), S(24f)), o.Unread);
			}
		}
		if (source.Length > num10)
		{
			Rectangle rectangle = new Rectangle(pane.X + num4 + S(4f), num7, S(4f), num10 * (num8 + num9) - num9);
			int num16 = Math.Max(S(24f), rectangle.Height * num10 / source.Length);
			Ui.Fill(g, new Rectangle(rectangle.X, rectangle.Y + (rectangle.Height - num16) * _tListScroll / Math.Max(1, source.Length - num10), rectangle.Width, num16), S(2f), Ui.K3);
		}
		int num17 = pane.X + num4 + S(14f);
		int num18 = pane.Right - num17;
		_tChatRect = Rectangle.Empty;
		if (sel == null)
		{
			if (source.Length == 0)
			{
				Rectangle r3 = new Rectangle(num17, num3, num18, pane.Bottom - num3);
				Ui.Card(g, r3, S(10f), Ui.K1, Ui.K3);
				Muted(g, r3, (_tTab == "in") ? L.T("เม\u0e37\u0e48อม\u0e35คนส\u0e48งข\u0e49อเสนอให\u0e49ค\u0e38ณ จะข\u0e36\u0e49นท\u0e35\u0e48น\u0e35\u0e48 · กด «สร\u0e49างข\u0e49อเสนอใหม\u0e48» เพ\u0e37\u0e48อเสนอแลกก\u0e31บผ\u0e39\u0e49เล\u0e48นอ\u0e37\u0e48น", "Offers sent to you appear here · press “New offer” to propose a trade") : L.T("ย\u0e31งไม\u0e48ม\u0e35ข\u0e49อเสนอในกล\u0e48องน\u0e35\u0e49", "Nothing in this box yet"));
			}
			return;
		}
		bool flag3 = sel.IsIncoming(text2);
		bool closed = !sel.Open;
		(OfferBundle Theirs, OfferBundle Mine) tuple3 = sel.Sides(text2);
		OfferBundle item = tuple3.Theirs;
		OfferBundle item2 = tuple3.Mine;
		OfferSide offerSide2 = sel.Other(text2);
		int num19 = (num18 - S(22f) - S(16f)) / 2;
		int num20 = S(136f);
		Rectangle r4 = new Rectangle(num17, num3, num19, num20);
		string title = L.T(offerSide2.Name + " ให\u0e49", offerSide2.Name + " gives");
		string text6 = OfferTag.EditedChip(sel, text2);
		DrawOfferHalf(g, r4, title, item, (text6 != null) ? (text6, OfferTag.Kind.Yellow) : OfferTag.Of(sel, text2));
		Icons.Draw(g, "arrows-exchange", new Point(num17 + num19 + S(8f) + S(11f), num3 + num20 / 2), S(22f), Ui.PurpleLight);
		DrawOfferHalf(g, new Rectangle(num17 + num19 + S(22f) + S(16f), num3, num19, num20), L.T("ค\u0e38ณให\u0e49", "You give"), item2, null);
		int num21 = pane.Bottom - S(40f);
		Rectangle card = Rectangle.FromLTRB(num17, num3 + num20 + S(8f), pane.Right, num21 - S(8f));
		DrawOfferChat(g, card, sel, offerSide2, closed);
		string s2 = (sel.Open ? TradeFmt.ExpiresIn(sel.ExpiresAt, now) : (TradeFmt.Clock((sel.ClosedAt > 0) ? sel.ClosedAt : sel.ExpiresAt, now) + ((sel.CloseReason.Length > 0) ? (" · " + sel.CloseReason) : ""))) + (sel.Edited ? (" · " + OfferTag.EditedLine(sel, now)) : "") + ((flag3 && sel.Open) ? L.T(" · ตกลงแล\u0e49วของเข\u0e49ากล\u0e48องจดหมายท\u0e31\u0e49งสองฝ\u0e48าย ร\u0e31บตอนเข\u0e49าเกม", " · once accepted, items go to both mailboxes") : "");
		if (_tErr != null)
		{
			s2 = _tErr;
		}
		int right = pane.Right;
		bool offerActBusy;
		string text7;
		if (sel.Open && flag3)
		{
			offerActBusy = _offerActBusy;
			int num22 = TradeBtnW("check", L.T("ตกลงเทรด", "Accept trade"), "y", 14.5f) + S(16f);
			right -= num22;
			TradeBtn(g, new Rectangle(right, num21, num22, S(40f)), "lt-accept", (offerActBusy && _accept.Busy) ? "loader-2" : "check", L.T("ตกลงเทรด", "Accept trade"), "y", !offerActBusy, delegate
			{
				OfferActAsync(sel, "accept");
			}, 14.5f, offerActBusy && _accept.Busy);
			right -= S(8f);
			int num23 = TradeBtnW(null, L.T("ส\u0e48งข\u0e49อเสนอโต\u0e49กล\u0e31บ", "Counter-offer"), "");
			right -= num23;
			TradeBtn(g, new Rectangle(right, num21 + S(3f), num23, S(34f)), "lt-counter", null, L.T("ส\u0e48งข\u0e49อเสนอโต\u0e49กล\u0e31บ", "Counter-offer"), "", !offerActBusy, delegate
			{
				OpenOffer(OfferMode.Counter, sel, null);
			});
			right -= S(8f);
			(string, string, DateTime)? tConfirm = _tConfirm;
			if (tConfirm.HasValue)
			{
				(string, string, DateTime) valueOrDefault = tConfirm.GetValueOrDefault();
				if (valueOrDefault.Item1 == sel.Id && valueOrDefault.Item2 == "decline" && DateTime.UtcNow < valueOrDefault.Item3)
				{
					text7 = L.T("ย\u0e37นย\u0e31นปฏ\u0e34เสธ?", "Confirm decline?");
					goto IL_1162;
				}
			}
			text7 = L.T("ปฏ\u0e34เสธ", "Decline");
			goto IL_1162;
		}
		bool offerActBusy2;
		string text8;
		if (sel.Open)
		{
			offerActBusy2 = _offerActBusy;
			int num24 = TradeBtnW(null, L.T("แก\u0e49ข\u0e49อเสนอ", "Edit offer"), "");
			right -= num24;
			TradeBtn(g, new Rectangle(right, num21 + S(3f), num24, S(34f)), "lt-edit", null, L.T("แก\u0e49ข\u0e49อเสนอ", "Edit offer"), "", !offerActBusy2, delegate
			{
				OpenOffer(OfferMode.Edit, sel, null);
			});
			right -= S(8f);
			(string, string, DateTime)? tConfirm = _tConfirm;
			if (tConfirm.HasValue)
			{
				(string, string, DateTime) valueOrDefault2 = tConfirm.GetValueOrDefault();
				if (valueOrDefault2.Item1 == sel.Id && valueOrDefault2.Item2 == "cancel" && DateTime.UtcNow < valueOrDefault2.Item3)
				{
					text8 = L.T("ย\u0e37นย\u0e31นยกเล\u0e34ก?", "Confirm cancel?");
					goto IL_12fb;
				}
			}
			text8 = L.T("ยกเล\u0e34กข\u0e49อเสนอ", "Cancel offer");
			goto IL_12fb;
		}
		bool flag4 = sel.From.EntityId == text2;
		int num25 = TradeBtnW(null, L.T("ส\u0e48งข\u0e49อเสนอแบบเด\u0e34มอ\u0e35กคร\u0e31\u0e49ง", "Send the same offer again"), "");
		right -= num25;
		TradeBtn(g, new Rectangle(right, num21 + S(3f), num25, S(34f)), "lt-again", null, L.T("ส\u0e48งข\u0e49อเสนอแบบเด\u0e34มอ\u0e35กคร\u0e31\u0e49ง", "Send the same offer again"), "", flag4 || sel.State != "accepted", delegate
		{
			OpenOffer(OfferMode.Again, sel, null);
		});
		goto IL_1420;
		IL_12fb:
		string text9 = text8;
		int num26 = TradeBtnW(null, text9, "d");
		right -= num26;
		TradeBtn(g, new Rectangle(right, num21 + S(3f), num26, S(34f)), "lt-cancel", null, text9, "d", !offerActBusy2, delegate
		{
			if (Confirmed(sel.Id, "cancel"))
			{
				OfferActAsync(sel, "cancel");
			}
		});
		goto IL_1420;
		IL_1162:
		string text10 = text7;
		int num27 = TradeBtnW(null, text10, "d");
		right -= num27;
		TradeBtn(g, new Rectangle(right, num21 + S(3f), num27, S(34f)), "lt-decline", null, text10, "d", !offerActBusy, delegate
		{
			if (Confirmed(sel.Id, "decline"))
			{
				OfferActAsync(sel, "decline");
			}
		});
		goto IL_1420;
		IL_1420:
		Ui.Draw(g, s2, F(12f), new Rectangle(num17, num21, right - S(12f) - num17, S(40f)), (_tErr != null) ? Ui.Bad : Ui.TextMute, (TextFormatFlags)32772);
	}

	private void DrawOfferHalf(Graphics g, Rectangle r, string title, OfferBundle b, (string Text, OfferTag.Kind Kind)? tag)
	{
		Ui.Card(g, r, S(10f), Ui.K1, Ui.K3);
		int num = r.X + S(14f);
		int num2 = r.Y + S(12f);
		int num3 = r.Width - S(28f);
		Icons.Draw(g, "user", new Point(num + S(7f), num2 + S(10f)), S(14f), Ui.TextDim);
		Ui.Draw(g, title, F(14f, medium: true), new Rectangle(num + S(20f), num2, num3 - S(20f) - S(90f), S(20f)), Ui.TextDim, (TextFormatFlags)32772);
		if (tag.HasValue)
		{
			(string, OfferTag.Kind) valueOrDefault = tag.GetValueOrDefault();
			int x = r.Right - S(14f);
			int y = num2;
			var (text, _) = valueOrDefault;
			TagChip(g, x, y, text, valueOrDefault.Item2 switch
			{
				OfferTag.Kind.Yellow => "y", 
				OfferTag.Kind.Green => "g", 
				OfferTag.Kind.Red => "r", 
				_ => "m", 
			}, null, right: true);
		}
		num2 += S(28f);
		for (int i = 0; i < 5; i++)
		{
			TradeItem tradeItem = ((i < b.Items.Length) ? b.Items[i] : null);
			Rectangle r2 = new Rectangle(num + i * S(48f), num2, S(44f), S(44f));
			Slot(g, r2, tradeItem, on: false, (tradeItem != null) ? ("lt-half-" + title + i) : null, null, showHeld: false);
		}
		num2 += S(44f) + S(8f);
		Ui.Draw(g, "T Stone", F(13f), new Rectangle(num, num2, S(80f), S(24f)), Ui.TextDim, (TextFormatFlags)4);
		Money(g, num + S(70f), num2, S(24f), b.Tstone, 14f);
	}

	private void DrawOfferChat(Graphics g, Rectangle card, TradeOffer o, OfferSide oth, bool closed)
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Expected O, but got Unknown
		Ui.Card(g, card, S(10f), Ui.K1, Ui.K3);
		closed = closed || (_tChatInfo?.Closed ?? false);
		Rectangle rectangle = new Rectangle(card.X, card.Y, card.Width, S(34f));
		Pen val = new Pen(Ui.K3);
		try
		{
			g.DrawLine(val, rectangle.X, rectangle.Bottom, rectangle.Right, rectangle.Bottom);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Icons.Draw(g, "message-circle", new Point(rectangle.X + S(12f) + S(7f), rectangle.Y + rectangle.Height / 2), S(14f), Ui.TextDim);
		Font f = F(13f);
		string s = L.T("ห\u0e49องแชทส\u0e48วนต\u0e31วก\u0e31บ ", "Private chat with ");
		Ui.Draw(g, s, f, new Rectangle(rectangle.X + S(28f), rectangle.Y, S(200f), rectangle.Height), Ui.TextDim, (TextFormatFlags)4);
		Ui.Draw(g, oth.Name, F(13f, medium: true), new Rectangle(rectangle.X + S(28f) + Ui.Measure(s, f).Width, rectangle.Y, S(200f), rectangle.Height), Ui.Text, (TextFormatFlags)4);
		string s2 = (closed ? L.T("ป\u0e34ดแล\u0e49ว · อ\u0e48านได\u0e49อย\u0e48างเด\u0e35ยว", "Closed · read only") : L.T("เห\u0e47นแค\u0e48 2 คน · เก\u0e47บไว\u0e49จนป\u0e34ดข\u0e49อเสนอ", "Only you two · kept until the offer closes"));
		Ui.Draw(g, s2, F(11f), new Rectangle(rectangle.X, rectangle.Y, rectangle.Width - S(12f), rectangle.Height), Ui.TextMute, (TextFormatFlags)6);
		Rectangle rectangle2 = new Rectangle(card.X + S(8f), card.Bottom - S(8f) - S(32f), card.Width - S(16f), S(32f));
		Pen val2 = new Pen(Ui.K3);
		try
		{
			g.DrawLine(val2, card.X, rectangle2.Y - S(8f), card.Right, rectangle2.Y - S(8f));
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		int num = S(70f);
		InputBox(g, Rectangle.FromLTRB(rectangle2.X, rectangle2.Y, rectangle2.Right - num - S(6f), rectangle2.Bottom), "t_chat", closed ? L.T("ห\u0e49องน\u0e35\u0e49ป\u0e34ดแล\u0e49ว", "This room is closed") : L.T("พ\u0e34มพ\u0e4cถ\u0e36ง " + oth.Name + "…", "Message " + oth.Name + "…"), closed);
		TradeBtn(g, new Rectangle(rectangle2.Right - num, rectangle2.Y, num, rectangle2.Height), "lt-tsend", _tChatSending ? "loader-2" : "send", L.T("ส\u0e48ง", "Send"), "y", !closed && !_tChatSending && ((Control)_boxes["t_chat"]).Text.Trim().Length > 0, delegate
		{
			SendOfferChatAsync();
		}, 13f, _tChatSending);
		Rectangle clip = (_tChatRect = Rectangle.FromLTRB(card.X + S(6f), rectangle.Bottom + S(4f), card.Right - S(6f), rectangle2.Y - S(12f)));
		GraphicsState val3 = g.Save();
		g.SetClip(clip);
		long nowUnix = NowUnix;
		Font f2 = F(12f);
		Font f3 = F(13.5f, medium: true);
		Font f4 = F(13.5f);
		string text = _trader ?? "";
		List<(long, string, bool, string)> list = new List<(long, string, bool, string)>();
		if (_tChatFor == o.Id)
		{
			foreach (OfferChatMsg item in _tChat)
			{
				list.Add((item.T, item.Name, item.EntityId == text, item.Text));
			}
		}
		list.Add((o.CreatedAt, null, false, L.T("ของในข\u0e49อเสนอถ\u0e39กพ\u0e31กไว\u0e49ท\u0e35\u0e48เซ\u0e34ร\u0e4cฟแล\u0e49ว · ผ\u0e39\u0e49ส\u0e48งแก\u0e49ได\u0e49ขณะย\u0e31งรอตอบ (อ\u0e35กฝ\u0e48ายต\u0e49องตกลงก\u0e31บฉบ\u0e31บใหม\u0e48)", "Offered items are now held on the server · the sender can edit while it is pending (the other side must accept the new version)")));
		if (o.Edited)
		{
			list.Add((o.EditedAt, null, false, (o.From.EntityId == text) ? L.T("ค\u0e38ณแก\u0e49ข\u0e49อเสนอแล\u0e49ว — อ\u0e35กฝ\u0e48ายต\u0e49องตกลงก\u0e31บฉบ\u0e31บใหม\u0e48", "You edited the offer — the other side must accept the new version") : L.T(o.From.Name + " แก\u0e49ข\u0e49อเสนอแล\u0e49ว — ตรวจของอ\u0e35กคร\u0e31\u0e49งก\u0e48อนกดตกลง", o.From.Name + " edited the offer — check the items again before accepting")));
		}
		if (!o.Open)
		{
			list.Add(((o.ClosedAt > 0) ? o.ClosedAt : o.ExpiresAt, null, false, (o.State == "accepted") ? L.T("เทรดสำเร\u0e47จ · ของเข\u0e49ากล\u0e48องจดหมายท\u0e31\u0e49งสองฝ\u0e48ายแล\u0e49ว · ห\u0e49องแชทป\u0e34ด", "Trade done · items delivered to both mailboxes · room closed") : L.T("ข\u0e49อเสนอป\u0e34ดแล\u0e49ว (" + OfferTag.Of(o, text).Text + ") · ของค\u0e37นเจ\u0e49าของแล\u0e49ว · ห\u0e49องแชทป\u0e34ด", "Offer closed (" + OfferTag.Of(o, text).Text + ") · items returned · room closed")));
		}
		list.Sort(((long T, string? Who, bool Me, string Text) a, (long T, string? Who, bool Me, string Text) b) => a.T.CompareTo(b.T));
		if (_tChatErr != null)
		{
			list.Add((nowUnix, null, false, _tChatErr));
		}
		int num2 = clip.X + S(8f) + S(40f) + S(8f);
		int num3 = clip.Bottom + _tChatScroll;
		int num4 = list.Count - 1;
		while (num4 >= 0 && num3 > clip.Y - S(40f))
		{
			(long, string, bool, string) tuple = list[num4];
			int num5 = clip.Right - S(8f) - num2;
			if (tuple.Item2 == null)
			{
				int num6 = Ui.MeasureWrapped(g, tuple.Item4, F(12.5f), num5) + S(8f);
				num3 -= num6;
				if (num3 <= clip.Bottom)
				{
					Ui.Draw(g, TradeFmt.Clock(tuple.Item1, nowUnix), f2, new Rectangle(clip.X + S(8f), num3 + S(4f), S(40f), S(18f)), Ui.TextMute, (TextFormatFlags)4);
					Ui.DrawWrapped(g, tuple.Item4, F(12.5f), new Rectangle(num2, num3 + S(4f), num5, num6), (tuple.Item4 == _tChatErr) ? Ui.Bad : Ui.TextMute);
				}
			}
			else
			{
				int num7 = Math.Min(S(120f), Ui.Measure(tuple.Item2 + ":", f3).Width) + S(6f);
				int num8 = Ui.MeasureWrapped(g, tuple.Item4, f4, num5 - num7) + S(8f);
				num3 -= num8;
				if (num3 <= clip.Bottom)
				{
					Ui.Draw(g, TradeFmt.Clock(tuple.Item1, nowUnix), f2, new Rectangle(clip.X + S(8f), num3 + S(4f), S(40f), S(18f)), Ui.TextMute, (TextFormatFlags)4);
					Ui.Draw(g, tuple.Item2 + ":", f3, new Rectangle(num2, num3 + S(4f), num7 - S(6f), S(20f)), tuple.Item3 ? Ui.Yellow : Ui.PurpleLight, (TextFormatFlags)32772);
					Ui.DrawWrapped(g, tuple.Item4, f4, new Rectangle(num2 + num7, num3 + S(4f), num5 - num7, num8 - S(6f)), Ui.Text);
				}
			}
			num4--;
		}
		g.Restore(val3);
	}

	private void OpenOffer(OfferMode mode, TradeOffer? baseOffer, TradeItem? withItem)
	{
		if (_trader == null)
		{
			Toast("ย\u0e31งไม\u0e48ม\u0e35ต\u0e31วละครท\u0e35\u0e48จะเทรด", "No character to trade with", "alert-circle", Ui.Bad);
			return;
		}
		if (_gate.Locked)
		{
			ShowPage(Page.Trade);
			return;
		}
		OfferDraftUi offerDraftUi = new OfferDraftUi(new OfferForm(mode, baseOffer, _trader, withItem));
		((Control)_boxes["o_player"]).Text = "";
		FillOfferBoxes(offerDraftUi.F);
		_od = offerDraftUi;
		_odScroll = 0;
		_dd = null;
		_focusNext = ((offerDraftUi.F.FixedTo == null) ? "o_player" : null);
		if (((ContainerControl)this).ActiveControl is TextBox)
		{
			((ContainerControl)this).ActiveControl = null;
		}
		EnsureDraftItemsAsync();
		if (mode == OfferMode.Edit)
		{
			string text = baseOffer?.ReplyTo;
			if (text != null)
			{
				LoadOfferOriginAsync(offerDraftUi, text);
			}
		}
		UpdateAnim();
		((Control)this).Invalidate();
	}

	private void FillOfferBoxes(OfferForm f)
	{
		((Control)_boxes["o_give_t"]).Text = ((f.GiveT > 0) ? TradeFmt.N(f.GiveT) : "");
		((Control)_boxes["o_want_t"]).Text = ((f.WantT > 0) ? TradeFmt.N(f.WantT) : "");
		((Control)_boxes["o_msg"]).Text = f.Message;
	}

	private void SyncOfferForm(OfferDraftUi d)
	{
		d.F.GiveT = TradeFmt.ParseT(((Control)_boxes["o_give_t"]).Text);
		d.F.WantT = TradeFmt.ParseT(((Control)_boxes["o_want_t"]).Text);
		d.F.Message = ((Control)_boxes["o_msg"]).Text;
	}

	private void CloseOffer()
	{
		_od = null;
		if (((ContainerControl)this).ActiveControl is TextBox)
		{
			((ContainerControl)this).ActiveControl = null;
		}
		UpdateAnim();
		((Control)this).Invalidate();
	}

	private async Task EnsureDraftItemsAsync()
	{
		OfferDraftUi d = _od;
		if (d == null || _lt == null || _trader == null)
		{
			return;
		}
		if (_items != null && _itemsFor == _trader)
		{
			d.Items = _items.Bag;
			d.ItemsFor = _trader;
			((Control)this).Invalidate();
		}
		else
		{
			if (d.ItemsBusy)
			{
				return;
			}
			d.ItemsBusy = true;
			string who = _trader;
			int gen = _ltGen;
			try
			{
				ItemsResult itemsResult = await _lt.ItemsAsync(who);
				if (gen == _ltGen && !((Control)this).IsDisposed && _od == d)
				{
					d.Items = itemsResult.Bag;
					d.ItemsFor = who;
					if (_charSel == who)
					{
						_items = itemsResult;
						_itemsFor = who;
					}
				}
			}
			catch (ApiError apiError)
			{
				if (gen == _ltGen && !((Control)this).IsDisposed && _od == d && !HandleTradeError(apiError, "items"))
				{
					d.Err = apiError.Message;
				}
			}
			finally
			{
				d.ItemsBusy = false;
				((Control)this).Invalidate();
			}
		}
	}

	private async Task LoadOfferOriginAsync(OfferDraftUi d, string originId)
	{
		if (_lt == null || _trader == null)
		{
			return;
		}
		int gen = _ltGen;
		try
		{
			TradeOffer origin = await _lt.OfferGetAsync(_trader, originId);
			if (gen == _ltGen && !((Control)this).IsDisposed && _od == d)
			{
				d.F.Origin = origin;
				((Control)this).Invalidate();
			}
		}
		catch (ApiError apiError)
		{
			Log.Write("offer origin: " + apiError.Code);
		}
		catch (Exception ex)
		{
			Log.Write("offer origin: " + ex.Message);
		}
	}

	private void OfferDraftTick()
	{
		OfferDraftUi od = _od;
		if (od != null && !(DateTime.UtcNow - _odTyped < TimeSpan.FromMilliseconds(400L, 0L)))
		{
			string text = ((Control)_boxes["o_player"]).Text.Trim();
			if (od.F.FixedTo == null && od.F.To == null && text.Length >= 3 && text != od.PlayersFor && !od.PlayersBusy)
			{
				SearchPlayersAsync();
			}
		}
	}

	private async Task SearchPlayersAsync()
	{
		OfferDraftUi d = _od;
		if (d == null || _lt == null || _trader == null || d.PlayersBusy)
		{
			return;
		}
		string text = ((Control)_boxes["o_player"]).Text.Trim();
		if (text.Length < 3)
		{
			d.Players = null;
			d.PlayersFor = text;
			((Control)this).Invalidate();
			return;
		}
		d.PlayersBusy = true;
		d.PlayersFor = text;
		int gen = _ltGen;
		try
		{
			TradePlayer[] players = await _lt.TradePlayersAsync(_trader, text);
			if (gen == _ltGen && !((Control)this).IsDisposed && _od == d)
			{
				d.Players = players;
				d.Err = null;
			}
		}
		catch (ApiError e)
		{
			if (gen == _ltGen && !((Control)this).IsDisposed && _od == d && !HandleTradeError(e, "trade"))
			{
				d.Err = TradeErrText(e);
			}
		}
		finally
		{
			d.PlayersBusy = false;
			((Control)this).Invalidate();
		}
	}

	private async Task SubmitOfferAsync()
	{
		OfferDraftUi d = _od;
		if (d == null || _lt == null || _trader == null || d.Busy)
		{
			return;
		}
		SyncOfferForm(d);
		OfferForm f = d.F;
		f.Prune(d.Items);
		string text = f.Check(Char(_trader)?.Tstone);
		if (text != null)
		{
			d.Err = text;
			((Control)this).Invalidate();
			return;
		}
		OfferDraft d2 = f.Draft();
		d.Busy = true;
		d.Err = null;
		UpdateAnim();
		int gen = _ltGen;
		string me = _trader;
		try
		{
			TradeOffer tradeOffer = ((f.Mode == OfferMode.Counter && f.Base != null) ? (await _lt.OfferCounterAsync(me, f.Base.Id, d2)) : ((f.Mode != OfferMode.Edit || !(f.Base != null)) ? (await _lt.OfferCreateAsync(me, d2)) : (await _lt.OfferEditAsync(me, f.Base.Id, f.Version, d2, f.WantItemsAllowed))));
			if (gen != _ltGen || ((Control)this).IsDisposed)
			{
				return;
			}
			if (_od == d)
			{
				_od = null;
			}
			_tTab = "out";
			_tSel = tradeOffer.Id;
			if (f.Mode != OfferMode.Edit)
			{
				_tChatFor = null;
			}
			_offerNext = DateTime.MinValue;
			_itemNext = DateTime.MinValue;
			LoadCharactersAsync();
			ToastT((f.Mode == OfferMode.Edit) ? L.T("บ\u0e31นท\u0e36กการแก\u0e49ข\u0e49อเสนอแล\u0e49ว · " + tradeOffer.To.Name + " ต\u0e49องตกลงก\u0e31บฉบ\u0e31บใหม\u0e48", "Offer updated · " + tradeOffer.To.Name + " must accept the new version") : L.T("ส\u0e48งข\u0e49อเสนอให\u0e49 " + tradeOffer.To.Name + " แล\u0e49ว · ของพ\u0e31กไว\u0e49ท\u0e35\u0e48เซ\u0e34ร\u0e4cฟ หมดอาย\u0e38ใน 24 ชม.", "Offer sent to " + tradeOffer.To.Name + " · items held on the server, expires in 24 hr"), "send", Ui.Yellow);
			if (_page != Page.Trade)
			{
				ShowPage(Page.Trade);
			}
		}
		catch (ApiError apiError)
		{
			if (gen != _ltGen || ((Control)this).IsDisposed || HandleTradeError(apiError, "trade"))
			{
				return;
			}
			if (f.Mode == OfferMode.Edit && f.Base != null && apiError.Code == "stale")
			{
				d.Err = apiError.Message;
				try
				{
					TradeOffer fresh = await _lt.OfferGetAsync(me, f.Base.Id);
					if (gen != _ltGen || ((Control)this).IsDisposed || _od != d)
					{
						return;
					}
					bool flag = f.Rebase(fresh);
					FillOfferBoxes(f);
					d.Err = (flag ? L.T("ข\u0e49อเสนอถ\u0e39กแก\u0e49จากท\u0e35\u0e48อ\u0e37\u0e48น — โหลดฉบ\u0e31บล\u0e48าส\u0e38ดแล\u0e49ว ตรวจแล\u0e49วแก\u0e49อ\u0e35กคร\u0e31\u0e49งก\u0e48อนกดบ\u0e31นท\u0e36ก", "The offer was changed elsewhere — the latest version is loaded; check it and edit again") : L.T("ข\u0e49อเสนอน\u0e35\u0e49ป\u0e34ดไปแล\u0e49ว — แก\u0e49ไม\u0e48ได\u0e49", "This offer is closed — it can no longer be edited"));
				}
				catch (ApiError apiError2)
				{
					d.Err = apiError2.Message;
				}
				goto IL_071a;
			}
			string err;
			if (apiError.Code == "declined_recent")
			{
				int? wait = apiError.Wait;
				if (wait.HasValue)
				{
					int valueOrDefault = wait.GetValueOrDefault();
					err = L.T($"อ\u0e35กฝ\u0e48ายเพ\u0e34\u0e48งปฏ\u0e34เสธ — ส\u0e48งใหม\u0e48ได\u0e49ใน {valueOrDefault / 60} นาท\u0e35", $"They just declined — try again in {valueOrDefault / 60} min");
					goto IL_0715;
				}
			}
			err = TradeErrText(apiError);
			goto IL_0715;
			IL_0715:
			d.Err = err;
			goto IL_071a;
			IL_071a:
			string code = apiError.Code;
			if ((code == "items" || code == "stale") ? true : false)
			{
				EnsureDraftItemsAsync();
			}
			bool flag2;
			switch (apiError.Code)
			{
			case "stale":
			case "gone":
			case "state":
			case "not_yours":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			if (flag2)
			{
				_offerNext = DateTime.MinValue;
			}
		}
		catch (Exception ex)
		{
			Log.Write("offer submit: " + ex);
			d.Err = L.T("ส\u0e48งข\u0e49อเสนอไม\u0e48สำเร\u0e47จ — ลองใหม\u0e48", "Could not send the offer — try again");
		}
		finally
		{
			d.Busy = false;
			UpdateAnim();
			((Control)this).Invalidate();
		}
	}

	private void DrawOfferModal(Graphics g)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		OfferDraftUi d = _od;
		if (d == null)
		{
			return;
		}
		OfferForm f = d.F;
		SyncOfferForm(d);
		f.Prune(d.Items);
		Rectangle clientRectangle = ((Control)this).ClientRectangle;
		SolidBrush val = new SolidBrush(Color.FromArgb(184, 8, 7, 12));
		try
		{
			g.FillRectangle((Brush)(object)val, clientRectangle);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		AddHit(clientRectangle, "od-bg", null, enabled: false);
		int num = S(860f);
		int num2 = S(600f);
		Rectangle r = new Rectangle((clientRectangle.Width - num) / 2, (clientRectangle.Height - num2) / 2, num, num2);
		Ui.Card(g, r, S(14f), Ui.K1, Ui.K3);
		int num3 = r.X + S(24f);
		int num4 = r.Y + S(18f);
		int num5 = r.Width - S(48f);
		Ui.Draw(g, d.Mode switch
		{
			OfferMode.Counter => L.T("โต\u0e49กล\u0e31บข\u0e49อเสนอของ " + f.ToName, "Counter " + f.ToName + "'s offer"), 
			OfferMode.Edit => L.T("แก\u0e49ข\u0e49อเสนอถ\u0e36ง " + f.ToName, "Edit your offer to " + f.ToName), 
			OfferMode.Again => L.T("ส\u0e48งข\u0e49อเสนอแบบเด\u0e34มอ\u0e35กคร\u0e31\u0e49ง", "Send the same offer again"), 
			_ => L.T("สร\u0e49างข\u0e49อเสนอใหม\u0e48", "New offer"), 
		}, F(21f, medium: true), new Rectangle(num3, num4, num5 - S(44f), S(30f)), Ui.Text, (TextFormatFlags)32772);
		Rectangle r2 = new Rectangle(r.Right - S(24f) - S(36f), num4 - S(2f), S(36f), S(32f));
		bool flag = AddHit(r2, "od-x", CloseOffer, !d.Busy);
		if (flag)
		{
			Ui.Fill(g, r2, S(8f), Ui.K2);
		}
		Icons.Draw(g, "x", new Point(r2.X + r2.Width / 2, r2.Y + r2.Height / 2), S(17f), flag ? Ui.Text : Ui.TextMute);
		num4 += S(30f) + S(14f);
		int num6 = S(400f);
		int num7 = num3 + num6 + S(20f);
		int width = num5 - num6 - S(20f);
		Font f2 = F(13f);
		int num8 = r.Bottom - S(24f) - S(44f);
		TradeCharacter tradeCharacter = Char(_trader);
		int num9 = num4;
		Ui.Draw(g, L.T("ส\u0e48งถ\u0e36ง", "Send to"), f2, new Rectangle(num3, num9, num6, S(18f)), Ui.TextDim, (TextFormatFlags)4);
		num9 += S(22f);
		if (f.ToId.Length > 0)
		{
			Rectangle r3 = new Rectangle(num3, num9, num6, S(44f));
			Ui.Card(g, r3, S(9f), Ui.K2, Ui.K3);
			string toName = f.ToName;
			DrawAvatar(g, new Rectangle(r3.X + S(8f), r3.Y + S(7f), S(30f), S(30f)), toName, "", Ui.K2, badge: false);
			Ui.Draw(g, toName, F(14f, medium: true), new Rectangle(r3.X + S(46f), r3.Y, S(220f), r3.Height), Ui.Text, (TextFormatFlags)4);
			string text = f.ToLevel.ToString();
			TradePlayer to = f.To;
			Ui.Draw(g, "Lv " + text + (((object)to != null && to.Online) ? L.T(" · ออนไลน\u0e4c", " · online") : ""), F(12f), new Rectangle(r3.X + S(46f) + Ui.Measure(toName, F(14f, medium: true)).Width + S(8f), r3.Y, S(160f), r3.Height), Ui.TextMute, (TextFormatFlags)4);
			if (f.FixedTo == null)
			{
				string text2 = L.T("เปล\u0e35\u0e48ยน", "Change");
				Rectangle r4 = new Rectangle(r3.Right - S(10f) - Ui.Measure(text2, f2).Width - S(4f), r3.Y, Ui.Measure(text2, f2).Width + S(4f), r3.Height);
				LinkAt(g, r4, "od-change", text2, f2, delegate
				{
					f.To = null;
					d.Players = null;
					d.PlayersFor = "";
					_focusNext = "o_player";
					((Control)this).Invalidate();
				}, Ui.PurpleLight);
			}
			num9 = r3.Bottom + S(12f);
		}
		else
		{
			InputBox(g, new Rectangle(num3, num9, num6, S(38f)), "o_player", L.T("พ\u0e34มพ\u0e4cช\u0e37\u0e48อผ\u0e39\u0e49เล\u0e48นอย\u0e48างน\u0e49อย 3 ต\u0e31วอ\u0e31กษร", "Type at least 3 letters of the player's name"), d.Busy, "search");
			num9 += S(38f) + S(4f);
			string text3 = ((Control)_boxes["o_player"]).Text.Trim();
			if (d.PlayersBusy)
			{
				Ui.Draw(g, L.T("กำล\u0e31งค\u0e49น…", "Searching…"), F(12f), new Rectangle(num3, num9, num6, S(20f)), Ui.TextMute, (TextFormatFlags)4);
				num9 += S(22f);
			}
			else
			{
				TradePlayer[] players = d.Players;
				if (players != null && d.PlayersFor == text3)
				{
					if (players.Length == 0)
					{
						Ui.Draw(g, L.T("ไม\u0e48พบผ\u0e39\u0e49เล\u0e48นช\u0e37\u0e48อน\u0e35\u0e49บนเซ\u0e34ร\u0e4cฟ", "No player with that name on this server"), F(12f), new Rectangle(num3, num9, num6, S(20f)), Ui.TextMute, (TextFormatFlags)4);
						num9 += S(22f);
					}
					foreach (TradePlayer p in players.Take(4))
					{
						Rectangle r5 = new Rectangle(num3, num9, num6, S(34f));
						if (AddHit(r5, "od-p-" + p.EntityId, delegate
						{
							f.To = p;
							d.Err = SupporterError.WhyPick(p);
							((Control)this).Invalidate();
						}))
						{
							Ui.Fill(g, r5, S(7f), Ui.K2);
						}
						Ui.Dot(g, new PointF(r5.X + S(12f), r5.Y + S(17f)), S(7f), p.Online ? Ui.Good : Ui.TextMute);
						Ui.Draw(g, p.Name, F(13.5f, medium: true), new Rectangle(r5.X + S(24f), r5.Y, S(200f), r5.Height), p.Eligible ? Ui.Text : Ui.TextMute, (TextFormatFlags)4);
						Ui.Draw(g, "Lv " + p.Level + (p.Eligible ? "" : (" · " + SupporterError.WhyShort(p))), F(12f), new Rectangle(r5.X + S(24f) + Ui.Measure(p.Name, F(13.5f, medium: true)).Width + S(8f), r5.Y, num6 - S(24f) - Ui.Measure(p.Name, F(13.5f, medium: true)).Width - S(8f), r5.Height), p.Eligible ? Ui.TextMute : Ui.Bad, (TextFormatFlags)32772);
						num9 += S(34f);
					}
				}
				else if (text3.Length > 0 && text3.Length < 3)
				{
					Ui.Draw(g, L.T("พ\u0e34มพ\u0e4cอย\u0e48างน\u0e49อย 3 ต\u0e31วอ\u0e31กษร", "At least 3 letters"), F(12f), new Rectangle(num3, num9, num6, S(20f)), Ui.TextMute, (TextFormatFlags)4);
					num9 += S(22f);
				}
			}
			num9 += S(8f);
		}
		TradeItem[] array = f.GiveChoices(d.Items);
		string s = ((d.Mode == OfferMode.Edit) ? L.T($"ของท\u0e35\u0e48ค\u0e38ณให\u0e49 · ช\u0e34\u0e49นเด\u0e34มพ\u0e31กต\u0e48อ เพ\u0e34\u0e48มจากกระเป\u0e4bาได\u0e49 {f.GiveIds.Count}/{5}", $"You give · kept items stay held, add from your bag {f.GiveIds.Count}/{5}") : L.T($"ของท\u0e35\u0e48ค\u0e38ณให\u0e49 · เล\u0e37อกจากกระเป\u0e4bา {f.GiveIds.Count}/{5}", $"You give · pick from your bag {f.GiveIds.Count}/{5}"));
		Ui.Draw(g, s, f2, new Rectangle(num3, num9, num6, S(18f)), Ui.TextDim, (TextFormatFlags)32772);
		num9 += S(22f);
		int num10 = S(44f);
		int num11 = S(4f);
		int num12 = 8;
		int num13 = num8 - S(12f) - S(18f) - S(34f) - S(10f) - num9;
		int num14 = Math.Max(1, (num13 + num11) / (num10 + num11));
		Rectangle r6 = new Rectangle(num3, num9, num12 * num10 + (num12 - 1) * num11, num14 * (num10 + num11) - num11);
		int num15 = (array.Length + num12 - 1) / num12;
		_odScroll = Math.Clamp(_odScroll, 0, Math.Max(0, num15 - num14));
		if (d.ItemsBusy && array.Length == 0)
		{
			Muted(g, r6, L.T("กำล\u0e31งโหลดกระเป\u0e4bา…", "Loading your bag…"));
		}
		else if (array.Length == 0)
		{
			Muted(g, r6, (d.Items == null) ? (d.Err ?? L.T("ย\u0e31งไม\u0e48ม\u0e35ข\u0e49อม\u0e39ลกระเป\u0e4bา", "No bag data")) : L.T("ไม\u0e48ม\u0e35ของท\u0e35\u0e48เทรดได\u0e49ในกระเป\u0e4bา", "Nothing tradable in your bag"));
		}
		else
		{
			for (int num16 = 0; num16 < num14; num16++)
			{
				for (int num17 = 0; num17 < num12; num17++)
				{
					int num18 = (num16 + _odScroll) * num12 + num17;
					Rectangle r7 = new Rectangle(num3 + num17 * (num10 + num11), num9 + num16 * (num10 + num11), num10, num10);
					if (num18 >= array.Length)
					{
						if (num16 == 0)
						{
							Slot(g, r7, null, on: false, null, null);
						}
						continue;
					}
					TradeItem it = array[num18];
					bool flag2 = f.GiveIds.Contains(it.Id);
					Slot(g, r7, it, flag2, "od-g-" + it.Id, delegate
					{
						d.Err = f.ToggleGive(it.Id);
						((Control)this).Invalidate();
					}, showHeld: false, !flag2 && f.GiveIds.Count >= 5);
					if (f.IsKept(it.Id))
					{
						Icons.Draw(g, "lock", new Point(r7.Right - S(8f), r7.Y + S(8f)), S(10f), Ui.Yellow);
					}
				}
			}
		}
		num9 = r6.Bottom + S(10f);
		Ui.Draw(g, L.T("T Stone ท\u0e35\u0e48ให\u0e49", "T Stone you give"), f2, new Rectangle(num3, num9, S(150f), S(18f)), Ui.TextDim, (TextFormatFlags)4);
		num9 += S(18f);
		InputBox(g, new Rectangle(num3, num9, S(150f), S(34f)), "o_give_t", "0", d.Busy, "gem");
		string text4 = ((d.Mode == OfferMode.Edit && f.Base != null) ? L.T("พ\u0e31กไว\u0e49แล\u0e49ว " + TradeFmt.T(f.Base.Give.Tstone) + " · ม\u0e35อ\u0e35ก " + TradeFmt.T(tradeCharacter?.Tstone ?? 0), TradeFmt.T(f.Base.Give.Tstone) + " already held · " + TradeFmt.T(tradeCharacter?.Tstone ?? 0) + " more available") : L.T("ม\u0e35 " + TradeFmt.T(tradeCharacter?.Tstone ?? 0), "You have " + TradeFmt.T(tradeCharacter?.Tstone ?? 0)));
		Ui.Draw(g, text4 + ((_feeRate > 0.0) ? L.T(" · ห\u0e31กค\u0e48าธรรมเน\u0e35ยม " + TradeFmt.Pct(_feeRate) + " ตอนสำเร\u0e47จ", " · " + TradeFmt.Pct(_feeRate) + " fee when completed") : ""), F(12f), new Rectangle(num3 + S(160f), num9, num6 - S(160f), S(34f)), Ui.TextMute, (TextFormatFlags)32772);
		int num19 = num4;
		if (f.WantItemsAllowed)
		{
			TradeItem[] array2 = f.WantChoices();
			string s2 = ((d.Mode == OfferMode.Counter) ? L.T($"ขอจาก {f.ToName} · ต\u0e34\u0e4aกจากท\u0e35\u0e48เขาเสนอให\u0e49 {f.WantIds.Count}/{array2.Length}", $"Ask from {f.ToName} · pick from what they offered {f.WantIds.Count}/{array2.Length}") : L.T($"ขอจาก {f.ToName} · ต\u0e34\u0e4aกจากข\u0e49อเสนอเด\u0e34มของเขา {f.WantIds.Count}/{array2.Length}", $"Ask from {f.ToName} · pick from their original offer {f.WantIds.Count}/{array2.Length}"));
			Ui.Draw(g, s2, f2, new Rectangle(num7, num19, width, S(18f)), Ui.TextDim, (TextFormatFlags)32772);
			num19 += S(22f);
			for (int num20 = 0; num20 < Math.Max(1, array2.Length); num20++)
			{
				TradeItem it2 = ((num20 < array2.Length) ? array2[num20] : null);
				Rectangle r8 = new Rectangle(num7 + num20 % 8 * (num10 + num11), num19 + num20 / 8 * (num10 + num11), num10, num10);
				if (it2 == null)
				{
					Slot(g, r8, null, on: false, null, null);
					continue;
				}
				bool flag3 = f.WantIds.Contains(it2.Id);
				Slot(g, r8, it2, flag3, "od-w-" + it2.Id, delegate
				{
					f.ToggleWant(it2.Id);
					((Control)this).Invalidate();
				}, showHeld: false, !flag3);
			}
			num19 += (Math.Max(1, array2.Length) + 7) / 8 * (num10 + num11) + S(4f);
			if (array2.Length == 0)
			{
				Ui.Draw(g, L.T("ข\u0e49อเสนอเด\u0e34มของเขาไม\u0e48ม\u0e35ไอเทม — ขอเป\u0e47น T ได\u0e49", "Their offer has no items — ask for T instead"), F(12f), new Rectangle(num7, num19, width, S(18f)), Ui.TextMute, (TextFormatFlags)4);
				num19 += S(20f);
			}
			num19 += S(6f);
		}
		else
		{
			Rectangle r9 = new Rectangle(num7, num19, width, S(86f));
			Ui.Card(g, r9, S(10f), Ui.K2, Ui.K3);
			Icons.Draw(g, "message-circle", new Point(r9.X + S(14f) + S(8f), r9.Y + S(22f)), S(17f), Ui.Yellow);
			Ui.Draw(g, L.T("ฝ\u0e31\u0e48งขอ = T Stone เท\u0e48าน\u0e31\u0e49น", "You can ask for T Stone only"), F(13.5f, medium: true), new Rectangle(r9.X + S(40f), r9.Y + S(10f), r9.Width - S(52f), S(22f)), Ui.Text, (TextFormatFlags)32772);
			Ui.DrawWrapped(g, OfferForm.WantHint, F(12f), new Rectangle(r9.X + S(40f), r9.Y + S(34f), r9.Width - S(52f), r9.Height - S(38f)), Ui.TextDim);
			num19 = r9.Bottom + S(12f);
		}
		Ui.Draw(g, L.T("T Stone ท\u0e35\u0e48ขอ", "T Stone you ask for"), f2, new Rectangle(num7, num19, S(150f), S(18f)), Ui.TextDim, (TextFormatFlags)4);
		num19 += S(18f);
		InputBox(g, new Rectangle(num7, num19, S(150f), S(34f)), "o_want_t", "0", d.Busy, "gem");
		num19 += S(34f) + S(10f);
		Ui.Draw(g, L.T("ข\u0e49อความแนบ (ไม\u0e48บ\u0e31งค\u0e31บ)", "Message (optional)"), f2, new Rectangle(num7, num19, width, S(18f)), Ui.TextDim, (TextFormatFlags)4);
		num19 += S(18f);
		InputBox(g, new Rectangle(num7, num19, width, S(34f)), "o_msg", L.T("เช\u0e48น ตามท\u0e35\u0e48ค\u0e38ยในแชทโลก", "e.g. as discussed in world chat"), d.Busy);
		num19 += S(34f) + S(10f);
		string s3;
		if (d.Mode != OfferMode.Edit || !(f.Base != null))
		{
			s3 = ((d.Mode != OfferMode.Counter) ? L.T("ส\u0e48งแล\u0e49วของและ T ฝ\u0e31\u0e48งค\u0e38ณจะถ\u0e39กพ\u0e31กไว\u0e49ท\u0e35\u0e48เซ\u0e34ร\u0e4cฟท\u0e31นท\u0e35 · อ\u0e35กฝ\u0e48ายตอบได\u0e49ภายใน 24 ชม. · ตกลงแล\u0e49วของเข\u0e49ากล\u0e48องจดหมายท\u0e31\u0e49งสองฝ\u0e48าย", "Your items and T are held on the server as soon as you send · they have 24 hr to reply · on accept, items go to both mailboxes") : L.T("ข\u0e49อเสนอเด\u0e34มจะป\u0e34ดเป\u0e47น «โต\u0e49กล\u0e31บแล\u0e49ว» · ของและ T ฝ\u0e31\u0e48งค\u0e38ณพ\u0e31กท\u0e35\u0e48เซ\u0e34ร\u0e4cฟท\u0e31นท\u0e35 · " + f.ToName + " ตอบได\u0e49ภายใน 24 ชม. · ห\u0e49องแชทเด\u0e34มผ\u0e39กต\u0e48อ", "The original offer closes as “Countered” · your items and T are held right away · " + f.ToName + " has 24 hr to reply · same chat room"));
		}
		else
		{
			(int Added, int Removed, long GiveTDelta) tuple = f.Delta();
			int item = tuple.Added;
			int item2 = tuple.Removed;
			long item3 = tuple.GiveTDelta;
			List<string> list = new List<string>();
			if (item > 0)
			{
				list.Add(L.T($"เพ\u0e34\u0e48ม {item} ช\u0e34\u0e49น (พ\u0e31กเพ\u0e34\u0e48ม)", $"+{item} item(s) held"));
			}
			if (item2 > 0)
			{
				list.Add(L.T($"เอาออก {item2} ช\u0e34\u0e49น (ค\u0e37นทางจดหมาย)", $"−{item2} item(s) back by mail"));
			}
			if (item3 > 0)
			{
				list.Add(L.T("T ให\u0e49เพ\u0e34\u0e48ม " + TradeFmt.T(item3) + " (ห\u0e31กเพ\u0e34\u0e48ม)", "+" + TradeFmt.T(item3) + " given (charged now)"));
			}
			if (item3 < 0)
			{
				list.Add(L.T("T ให\u0e49ลด " + TradeFmt.T(-item3) + " (ค\u0e37นทางจดหมาย)", "−" + TradeFmt.T(-item3) + " given (back by mail)"));
			}
			if (list.Count > 0)
			{
				string s4 = string.Join(" · ", list);
				int num21 = Ui.MeasureWrapped(g, s4, F(12f, medium: true), width);
				Ui.DrawWrapped(g, s4, F(12f, medium: true), new Rectangle(num7, num19, width, num21 + S(2f)), Ui.Yellow);
				num19 += num21 + S(6f);
			}
			s3 = L.T($"ช\u0e34\u0e49นท\u0e35\u0e48คงไว\u0e49พ\u0e31กต\u0e48อ ไม\u0e48ต\u0e49องค\u0e37นก\u0e48อน · {f.ToName} ต\u0e49องตกลงก\u0e31บฉบ\u0e31บใหม\u0e48 · ห\u0e49องแชทเด\u0e34ม · ไม\u0e48ต\u0e48ออาย\u0e38 ({TradeFmt.ExpiresIn(f.Base.ExpiresAt, NowUnix + _offerSkew)})", $"Kept items stay held · {f.ToName} must accept the new version · same chat room · expiry unchanged ({TradeFmt.ExpiresIn(f.Base.ExpiresAt, NowUnix + _offerSkew)})");
		}
		Ui.DrawWrapped(g, s3, F(11.5f), new Rectangle(num7, num19, width, Math.Max(S(18f), num8 - S(12f) - num19)), Ui.TextMute);
		OfferDraft offerDraft = f.Draft();
		string text5 = d.Mode switch
		{
			OfferMode.Counter => L.T("ส\u0e48งข\u0e49อเสนอโต\u0e49กล\u0e31บ", "Send counter-offer"), 
			OfferMode.Edit => L.T("บ\u0e31นท\u0e36กการแก\u0e49ไข", "Save changes"), 
			_ => L.T("ส\u0e48งข\u0e49อเสนอ", "Send offer"), 
		};
		int num22 = TradeBtnW("send", text5, "y", 15f) + S(10f);
		bool enabled = !d.Busy && offerDraft.ToEntityId.Length > 0 && !offerDraft.IsEmpty && f.Changed;
		TradeBtn(g, new Rectangle(r.Right - S(24f) - num22, num8, num22, S(44f)), "od-go", d.Busy ? "loader-2" : ((d.Mode == OfferMode.Edit) ? "check" : "send"), text5, "y", enabled, delegate
		{
			SubmitOfferAsync();
		}, 15f, d.Busy);
		int num23 = TradeBtnW(null, L.T("ยกเล\u0e34ก", "Cancel"), "", 14f);
		TradeBtn(g, new Rectangle(r.Right - S(24f) - num22 - S(10f) - num23, num8, num23, S(44f)), "od-cancel", null, L.T("ยกเล\u0e34ก", "Cancel"), "", !d.Busy, CloseOffer, 14f);
		Rectangle r10 = new Rectangle(num3, num8, r.Right - S(24f) - num22 - S(10f) - num23 - S(16f) - num3, S(44f));
		if (d.Err != null)
		{
			Icons.Draw(g, "alert-circle", new Point(r10.X + S(8f), r10.Y + S(22f)), S(15f), Ui.Bad);
			Ui.DrawWrapped(g, d.Err, F(12.5f), new Rectangle(r10.X + S(22f), r10.Y + S(2f), r10.Width - S(22f), S(40f)), Ui.Bad);
		}
		else
		{
			Ui.Draw(g, (d.Mode == OfferMode.Edit && !f.Changed) ? L.T("ย\u0e31งไม\u0e48ได\u0e49แก\u0e49อะไร — เปล\u0e35\u0e48ยนของ/T/ข\u0e49อความแล\u0e49วกดบ\u0e31นท\u0e36ก", "Nothing changed yet — change items, T or the message, then save") : f.Summary(), F(13f), r10, Ui.TextDim, (TextFormatFlags)32772);
		}
	}
}
