using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Durango.Logic.Clusters;
using Durango.Render.Camera;
using Durango.Terrain;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using Shared.Economy;
using UnityEngine;
using Yaml;
using Yaml.Util;

public class CommunityDodoFightUI : MonoBehaviour
{
	private enum Kind
	{
		Lobby,
		Detail,
		Hud,
		Pick
	}

	private sealed class Btn
	{
		public string Key;

		public GameObject Go;

		public UISprite Bg;

		public UILabel Label;

		public Action OnClick;

		public bool Enabled = true;
	}

	private sealed class PlayerRow
	{
		public GameObject Go;

		public UILabel Name;

		public UILabel Info;

		public Btn View;

		public Btn Challenge;

		public string PlayerId;

		public string PlayerName;
	}

	private sealed class InfoRow
	{
		public Btn Button;

		public UILabel Title;

		public UILabel Desc;
	}

	private sealed class HeadBar
	{
		public GameObject Go;

		public UISprite Fill;

		public int Side;

		public float Ratio;

		public bool Seen;

		public UILabel Mine;
	}

	private sealed class Floater
	{
		public UILabel Label;

		public UISprite Icon;

		public string Entity;

		public float Start;
	}

	private const int LobbyDepth = 6000;

	private const int DetailDepth = 6010;

	private const int HudDepth = 5990;

	private const int PickDepth = 6005;

	private const int LobbyW = 880;

	private const int LobbyH = 590;

	private const int DetailW = 1080;

	private const int DetailH = 540;

	private const int TopW = 560;

	private const int TopH = 40;

	private const int HudTopMargin = 86;

	private const int HeadBarW = 120;

	private const int HeadBarH = 12;

	private const float HeadBarLift = 40f;

	private const int MineTagLift = 24;

	private const int CheerW = 210;

	private const int CheerH = 54;

	private const int CheerBottom = 236;

	private const int CheerRight = 24;

	private const float FloatSec = 1.6f;

	private const float FloatRise = 70f;

	private const float BannerSec = 2.5f;

	private const int RowsPerPage = 6;

	private const int StatBarW = 170;

	private const int PrefW = 300;

	private static readonly Color PanelBg = new Color(0.07f, 0.07f, 0.09f, 0.94f);

	private static readonly Color CardBg = new Color(0.13f, 0.13f, 0.16f, 0.96f);

	private static readonly Color FrameBg = new Color(0.26f, 0.24f, 0.2f, 1f);

	private static readonly Color BarBack = new Color(0.2f, 0.2f, 0.22f, 1f);

	private static readonly Color HudBg = new Color(0.03f, 0.03f, 0.05f, 0.62f);

	private static readonly Color HeadBarBack = new Color(0f, 0f, 0f, 0.7f);

	private static readonly Color[] HeadBarColor = new Color[2]
	{
		new Color(0.93f, 0.22f, 0.2f, 1f),
		new Color(0.22f, 0.5f, 1f, 1f)
	};

	private static readonly Color[] BetColor = new Color[2]
	{
		new Color(0.72f, 0.2f, 0.18f, 1f),
		new Color(0.18f, 0.4f, 0.78f, 1f)
	};

	private static readonly string[] SideHex = new string[2] { "ff7a6e", "6fa8ff" };

	private static readonly Color BtnOff = new Color(0.22f, 0.22f, 0.24f, 0.95f);

	private static readonly Color BtnGold = new Color(0.98f, 0.72f, 0.18f, 1f);

	private static readonly Color BtnGreen = new Color(0.32f, 0.78f, 0.38f, 1f);

	private static readonly Color BtnRed = new Color(0.75f, 0.25f, 0.22f, 1f);

	private static readonly Color BtnDisabled = new Color(0.16f, 0.16f, 0.17f, 0.9f);

	private static readonly Color BtnSel = new Color(0.4f, 0.32f, 0.1f, 0.98f);

	private static readonly Color TextLight = new Color(0.95f, 0.95f, 0.95f, 1f);

	private static readonly Color TextDark = new Color(0.1f, 0.08f, 0.05f, 1f);

	private static readonly Color TextDim = new Color(0.72f, 0.72f, 0.74f, 1f);

	private static readonly Color TextDisabled = new Color(0.48f, 0.48f, 0.5f, 1f);

	private static readonly Color Gold = new Color(1f, 0.82f, 0.35f, 1f);

	private static readonly Color AtkColor = new Color(0.95f, 0.42f, 0.3f, 1f);

	private static readonly Color DefColor = new Color(0.35f, 0.62f, 0.98f, 1f);

	private static readonly Color[] SideColor = new Color[2]
	{
		new Color(1f, 0.52f, 0.46f, 1f),
		new Color(0.45f, 0.72f, 1f, 1f)
	};

	private static readonly Color[] StatColor = new Color[4]
	{
		new Color(0.95f, 0.4f, 0.35f, 1f),
		new Color(0.4f, 0.9f, 0.5f, 1f),
		new Color(0.45f, 0.7f, 1f, 1f),
		new Color(1f, 0.82f, 0.3f, 1f)
	};

	private static CommunityDodoFightUI _lobby;

	private static CommunityDodoFightUI _detail;

	private static CommunityDodoFightUI _hud;

	private static CommunityDodoFightUI _pick;

	private static readonly Dictionary<string, Btn> _buttons = new Dictionary<string, Btn>(StringComparer.Ordinal);

	private static int _page;

	private static int _detailSel = -1;

	private Kind _kind;

	private UIPanel _uiPanel;

	private UILabel _style;

	private UIRoot _root;

	private int _seen = -1;

	private float _refreshAt;

	private UISprite _portrait;

	private UILabel _portraitAlt;

	private UILabel _cardName;

	private UILabel _cardInfo;

	private UILabel _cardSkills;

	private UILabel _noDodo;

	private Btn _bDetail;

	private Btn _bReroll;

	private Btn _bClaim;

	private Btn _bQueue;

	private Btn _bPractice;

	private Btn _bWatch;

	private Btn _bPrev;

	private Btn _bNext;

	private UILabel _arena;

	private UILabel _playersHead;

	private UILabel _pageLabel;

	private readonly List<PlayerRow> _rows = new List<PlayerRow>();

	private UIModelViewer _dModel;

	private int _dModelSpecies = -1;

	private UISprite _dPortrait;

	private UILabel _dPortraitAlt;

	private UILabel _dName;

	private UILabel _dSpecies;

	private UILabel _dRecord;

	private readonly UISprite[] _statFill = new UISprite[4];

	private readonly UILabel[] _statValue = new UILabel[4];

	private UILabel _prefLabel;

	private UISprite _prefAtk;

	private UISprite _prefDef;

	private readonly InfoRow[] _infoRows = new InfoRow[4];

	private UILabel _dDesc;

	private GameObject _hTop;

	private UISprite _hTopBg;

	private UILabel _hHeader;

	private UILabel _hBanner;

	private float _hBannerUntil;

	private readonly Dictionary<string, HeadBar> _hBars = new Dictionary<string, HeadBar>(StringComparer.Ordinal);

	private readonly List<Floater> _floaters = new List<Floater>();

	private GameObject _hCheer;

	private readonly Btn[] _hCheerBtn = new Btn[2];

	private UILabel _hCheerHint;

	private GameObject _hBet;

	private UISprite _hBetBg;

	private UILabel _hBetHead;

	private readonly Btn[] _hBetBtn = new Btn[2];

	private UILabel _hBetMine;

	private UILabel _pHint;

	private float _pSentAt = -100f;

	private bool _pHaveSpot;

	private Vector3 _pSpot;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"title",
			new string[4] { "ลานส\u0e39\u0e49โดโด\u0e49", "Dodo Fight Arena", "Arena Adu Dodo", "도도 결투장" }
		},
		{
			"refresh",
			new string[4] { "ร\u0e35เฟรช", "Refresh", "Muat ulang", "새로고침" }
		},
		{
			"get_dodo",
			new string[4] { "ร\u0e31บโดโด\u0e49 (ฟร\u0e35)", "Get a dodo (free)", "Ambil dodo (gratis)", "도도 받기 (무료)" }
		},
		{
			"no_dodo",
			new string[4] { "ย\u0e31งไม\u0e48ม\u0e35โดโด\u0e49น\u0e31กส\u0e39\u0e49 — ร\u0e31บฟร\u0e35 1 ต\u0e31ว ส\u0e38\u0e48มค\u0e48าพล\u0e31ง น\u0e34ส\u0e31ย และสก\u0e34ลพ\u0e34เศษ 2 ช\u0e48อง", "You have no fighting dodo yet — get one free with random stats, style and 2 special skills", "Kamu belum punya dodo petarung — ambil satu gratis dengan stat, gaya, dan 2 skill spesial acak", "아직 싸움 도도가 없습니다 — 능력치·성향·특수 스킬 2개가 무작위인 도도 1마리를 무료로 받으세요" }
		},
		{
			"detail",
			new string[4] { "รายละเอ\u0e35ยด", "Details", "Detail", "상세 정보" }
		},
		{
			"reroll",
			new string[4] { "ส\u0e38\u0e48มใหม\u0e48 {0} T", "Reroll {0} T", "Acak ulang {0} T", "다시 뽑기 {0} T" }
		},
		{
			"reroll_free",
			new string[4] { "ส\u0e38\u0e48มใหม\u0e48 (ฟร\u0e35)", "Reroll (free)", "Acak ulang (gratis)", "다시 뽑기 (무료)" }
		},
		{
			"reroll_confirm",
			new string[4] { "ส\u0e38\u0e48มโดโด\u0e49ใหม\u0e48? ค\u0e48าพล\u0e31ง น\u0e34ส\u0e31ย และสก\u0e34ลพ\u0e34เศษจะถ\u0e39กส\u0e38\u0e48มใหม\u0e48ท\u0e31\u0e49งหมด", "Reroll your dodo? Its stats, fighting style and special skills are all rerolled", "Acak ulang dodo? Stat, gaya bertarung, dan skill spesialnya diacak ulang semua", "도도를 다시 뽑을까요? 능력치·전투 성향·특수 스킬이 모두 새로 정해집니다" }
		},
		{
			"reroll_btn",
			new string[4] { "ส\u0e38\u0e48มใหม\u0e48", "Reroll", "Acak ulang", "다시 뽑기" }
		},
		{
			"balance",
			new string[4] { "T Stone ท\u0e35\u0e48ม\u0e35: {0}", "Your T Stone: {0}", "T Stone kamu: {0}", "보유 T Stone: {0}" }
		},
		{
			"queue_join",
			new string[4] { "ลงค\u0e34ว (รอ {0})", "Join queue ({0} waiting)", "Masuk antrean ({0} menunggu)", "대기열 참가 ({0}명 대기)" }
		},
		{
			"queue_leave",
			new string[4] { "ออกค\u0e34ว (รอ {0})", "Leave queue ({0} waiting)", "Keluar antrean ({0} menunggu)", "대기열 나가기 ({0}명 대기)" }
		},
		{
			"practice",
			new string[4] { "ฝ\u0e36กก\u0e31บส\u0e31ตว\u0e4cป\u0e48า", "Practice vs wild", "Latihan vs hewan liar", "야생 동물과 연습" }
		},
		{
			"watch",
			new string[4] { "ด\u0e39แมตช\u0e4c", "Watch match", "Tonton laga", "경기 관전" }
		},
		{
			"watch_far",
			new string[4] { "เด\u0e34นไปใกล\u0e49สนามเพ\u0e37\u0e48อด\u0e39แมตช\u0e4cและเด\u0e34มพ\u0e31น", "Walk close to the arena to watch and bet", "Mendekatlah ke arena untuk menonton dan bertaruh", "경기장 가까이 가면 관전하고 베팅할 수 있습니다" }
		},
		{
			"arena_idle",
			new string[4] { "สนาม: ว\u0e48าง", "Arena: free", "Arena: kosong", "경기장: 비어 있음" }
		},
		{
			"arena_countdown",
			new string[4] { "สนาม: กำล\u0e31งจะเร\u0e34\u0e48ม — {0} vs {1}", "Arena: about to start — {0} vs {1}", "Arena: segera mulai — {0} vs {1}", "경기장: 곧 시작 — {0} vs {1}" }
		},
		{
			"arena_fighting",
			new string[4] { "สนาม: กำล\u0e31งส\u0e39\u0e49 — {0} vs {1}", "Arena: fighting — {0} vs {1}", "Arena: sedang bertarung — {0} vs {1}", "경기장: 결투 중 — {0} vs {1}" }
		},
		{
			"players",
			new string[4] { "ผ\u0e39\u0e49เล\u0e48นท\u0e35\u0e48ม\u0e35โดโด\u0e49 ({0})", "Players with a dodo ({0})", "Pemain dengan dodo ({0})", "도도가 있는 플레이어 ({0})" }
		},
		{
			"no_players",
			new string[4] { "ย\u0e31งไม\u0e48ม\u0e35ผ\u0e39\u0e49เล\u0e48นคนอ\u0e37\u0e48นท\u0e35\u0e48ม\u0e35โดโด\u0e49ใกล\u0e49 ๆ", "No other players with a dodo nearby", "Belum ada pemain lain dengan dodo di dekat sini", "근처에 도도가 있는 다른 플레이어가 없습니다" }
		},
		{
			"view",
			new string[4] { "ด\u0e39", "View", "Lihat", "보기" }
		},
		{
			"challenge",
			new string[4] { "ท\u0e49า", "Challenge", "Tantang", "도전" }
		},
		{
			"busy",
			new string[4] { "ไม\u0e48ว\u0e48าง", "busy", "sibuk", "바쁨" }
		},
		{
			"challenge_sent",
			new string[4] { "ส\u0e48งคำท\u0e49าถ\u0e36ง {0} แล\u0e49ว", "Challenge sent to {0}", "Tantangan dikirim ke {0}", "{0}님에게 도전을 보냈습니다" }
		},
		{
			"bet_hex",
			new string[4] { "แทง {0} ×{1}", "Bet {0} ×{1}", "Taruh {0} ×{1}", "{0} 베팅 ×{1}" }
		},
		{
			"pick_title",
			new string[4] { "ท\u0e49าผ\u0e39\u0e49เล\u0e48น — เล\u0e37อกค\u0e39\u0e48ต\u0e48อส\u0e39\u0e49", "Challenge — pick a player", "Tantang — pilih pemain", "도전 — 상대 선택" }
		},
		{
			"pick_none",
			new string[4] { "ย\u0e31งไม\u0e48ม\u0e35ผ\u0e39\u0e49เล\u0e48นท\u0e35\u0e48ม\u0e35โดโด\u0e49บนเกาะน\u0e35\u0e49", "Nobody on this island has a dodo yet", "Belum ada pemain dengan dodo di pulau ini", "이 섬에 도도를 가진 플레이어가 없습니다" }
		},
		{
			"pick_what",
			new string[4] { "{0}", "{0}", "{0}", "{0}" }
		},
		{
			"pick_view",
			new string[4] { "ด\u0e39โดโด\u0e49", "View dodo", "Lihat dodo", "도도 보기" }
		},
		{
			"pick_challenge",
			new string[4] { "ท\u0e49า", "Challenge", "Tantang", "도전" }
		},
		{
			"stats_head",
			new string[4] { "ค\u0e48าพล\u0e31ง", "Stats", "Statistik", "능력치" }
		},
		{
			"detail_pref",
			new string[4] { "น\u0e34ส\u0e31ยต\u0e35 %", "Attack style %", "Gaya serang %", "공격 성향 %" }
		},
		{
			"power",
			new string[4] { "พล\u0e31ง {0}", "Power {0}", "Kekuatan {0}", "전투력 {0}" }
		},
		{
			"record",
			new string[4] { "ชนะ {0} · แพ\u0e49 {1}", "W {0} · L {1}", "Menang {0} · Kalah {1}", "{0}승 · {1}패" }
		},
		{
			"owner",
			new string[4] { "ของ {0}", "Owner {0}", "Milik {0}", "주인 {0}" }
		},
		{
			"you",
			new string[4] { "(ค\u0e38ณ)", "(you)", "(kamu)", "(나)" }
		},
		{
			"detail_title",
			new string[4] { "รายละเอ\u0e35ยดโดโด\u0e49", "Dodo details", "Detail dodo", "도도 상세 정보" }
		},
		{
			"stat0",
			new string[4] { "STR เล\u0e37อด", "STR · HP", "STR · HP", "STR · 체력" }
		},
		{
			"stat1",
			new string[4] { "AGI ความเร\u0e47ว/หลบ", "AGI · speed/dodge", "AGI · cepat/elak", "AGI · 속도/회피" }
		},
		{
			"stat2",
			new string[4] { "DEX ความแม\u0e48น", "DEX · accuracy", "DEX · akurasi", "DEX · 명중" }
		},
		{
			"stat3",
			new string[4] { "LUK โอกาสสก\u0e34ล", "LUK · skill chance", "LUK · peluang skill", "LUK · 스킬 확률" }
		},
		{
			"pref",
			new string[4] { "น\u0e34ส\u0e31ย: โจมต\u0e35 {0}% · ป\u0e49องก\u0e31น {1}%", "Style: attack {0}% · defend {1}%", "Gaya: serang {0}% · bertahan {1}%", "성향: 공격 {0}% · 방어 {1}%" }
		},
		{
			"basic_head",
			new string[4] { "ท\u0e48าพ\u0e37\u0e49นฐาน (ท\u0e38กต\u0e31วม\u0e35)", "Basic moves (every dodo)", "Gerakan dasar (semua dodo)", "기본 동작 (모든 도도)" }
		},
		{
			"skills_head",
			new string[4] { "สก\u0e34ลพ\u0e34เศษ — แตะเพ\u0e37\u0e48อด\u0e39ผลเต\u0e47ม", "Special skills — tap for details", "Skill spesial — ketuk untuk detail", "특수 스킬 — 눌러서 자세히 보기" }
		},
		{
			"line_attack",
			new string[4] { "โจมต\u0e35", "Attack", "Serang", "공격" }
		},
		{
			"line_defend",
			new string[4] { "ป\u0e49องก\u0e31น", "Defend", "Bertahan", "방어" }
		},
		{
			"rate",
			new string[4] { "ต\u0e34ด {0}%", "{0}% chance", "peluang {0}%", "발동 {0}%" }
		},
		{
			"rate_full",
			new string[4] { "โอกาสออกเองต\u0e48อแอคช\u0e31\u0e48นท\u0e35\u0e48ตรงสาย: {0}% (จาก LUK)", "Chance on each action of its line: {0}% (from LUK)", "Peluang di tiap aksi sejenis: {0}% (dari LUK)", "같은 계열 행동마다 발동 확률: {0}% (LUK 기반)" }
		},
		{
			"basic_full",
			new string[4] { "ท\u0e48าพ\u0e37\u0e49นฐาน · ใช\u0e49เม\u0e37\u0e48อไม\u0e48ต\u0e34ดสก\u0e34ลพ\u0e34เศษ", "Basic move · used when no special skill triggers", "Gerakan dasar · dipakai saat skill spesial tidak aktif", "기본 동작 · 특수 스킬이 발동하지 않을 때 사용" }
		},
		{
			"mode_practice",
			new string[4] { "ฝ\u0e36กซ\u0e49อม", "Practice", "Latihan", "연습" }
		},
		{
			"ph_countdown",
			new string[4] { "เร\u0e34\u0e48มใน {0} ว\u0e34", "Starts in {0}s", "Mulai dalam {0} dtk", "{0}초 후 시작" }
		},
		{
			"ph_running",
			new string[4] { "ยก {0}/{1} · {2} ว\u0e34", "Round {0}/{1} · {2}s", "Ronde {0}/{1} · {2} dtk", "{0}/{1} 라운드 · {2}초" }
		},
		{
			"ph_break",
			new string[4] { "พ\u0e31กยก {0} ว\u0e34", "Break {0}s", "Jeda {0} dtk", "휴식 {0}초" }
		},
		{
			"ph_ended",
			new string[4] { "จบแมตช\u0e4c", "Match over", "Laga selesai", "경기 종료" }
		},
		{
			"pick_hint",
			new string[4] { "แตะพ\u0e37\u0e49นเพ\u0e37\u0e48อเล\u0e37อกจ\u0e38ดฝ\u0e36ก (ไม\u0e48เก\u0e34น {0} ไทล\u0e4cจากต\u0e31ว)", "Tap the ground to pick the practice spot (within {0} tiles)", "Ketuk tanah untuk memilih tempat latihan (maks. {0} petak)", "연습 장소를 땅에서 선택하세요 ({0}타일 이내)" }
		},
		{
			"pick_far",
			new string[4] { "ไกลเก\u0e34นไป — เล\u0e37อกในระยะ {0} ไทล\u0e4cจากต\u0e31ว", "Too far — pick within {0} tiles", "Terlalu jauh — pilih dalam {0} petak", "너무 멉니다 — {0}타일 이내로" }
		},
		{
			"pick_sent",
			new string[4] { "กำล\u0e31งเร\u0e34\u0e48มฝ\u0e36ก…", "Starting practice…", "Memulai latihan…", "연습 시작 중…" }
		},
		{
			"pick_cancel",
			new string[4] { "ยกเล\u0e34ก", "Cancel", "Batal", "취소" }
		},
		{
			"dodge_float",
			new string[4] { "หลบ!", "Dodge!", "Elak!", "회피!" }
		},
		{
			"mine_tag_owner",
			new string[4] { "★ โดโด\u0e49ของค\u0e38ณ", "★ Your dodo", "★ Dodo kamu", "★ 내 도도" }
		},
		{
			"mine_tag_owner_bet",
			new string[4] { "★ โดโด\u0e49ของค\u0e38ณ · แทง {0} T", "★ Your dodo · bet {0} T", "★ Dodo kamu · taruhan {0} T", "★ 내 도도 · 베팅 {0} T" }
		},
		{
			"mine_tag_bet",
			new string[4] { "★ ต\u0e31วท\u0e35\u0e48ค\u0e38ณแทง {0} T", "★ Your bet {0} T", "★ Taruhanmu {0} T", "★ 내 베팅 {0} T" }
		},
		{
			"mode_house",
			new string[4] { "แมตช\u0e4cเจ\u0e49าม\u0e37อ", "House match", "Laga bandar", "하우스 경기" }
		},
		{
			"ph_open",
			new string[4] { "รอคนแทง — แทงเพ\u0e37\u0e48อเป\u0e34ดแมตช\u0e4c", "Place a bet to open the match", "Pasang taruhan untuk membuka laga", "베팅하면 경기가 열립니다" }
		},
		{
			"bet_head_open",
			new string[4] { "เด\u0e34มพ\u0e31น T Stone · คนแรกท\u0e35\u0e48แทง = เป\u0e34ดแมตช\u0e4c แล\u0e49วร\u0e31บแทงอ\u0e35ก {0} ว\u0e34", "Bet T Stone · the first bet opens the match, then {0}s to bet", "Taruhan T Stone · taruhan pertama membuka laga, lalu {0} dtk", "T Stone 베팅 · 첫 베팅으로 경기 시작, 이후 {0}초" }
		},
		{
			"cd",
			new string[4] { "ค\u0e39ลดาวน\u0e4c {0} ว\u0e34", "Cooldown {0}s", "Jeda {0} dtk", "쿨타임 {0}초" }
		},
		{
			"cd_left",
			new string[4] { "พร\u0e49อมใน {0} ว\u0e34", "Ready in {0}s", "Siap dalam {0} dtk", "{0}초 후 준비" }
		},
		{
			"cheer_desc",
			new string[4] { "เช\u0e35ยร\u0e4cโดโด\u0e49: +{0}% ให\u0e49สก\u0e34ลน\u0e35\u0e49ในแอคช\u0e31\u0e48นถ\u0e31ดไปท\u0e35\u0e48ตรงสาย (ยกละ 3 คร\u0e31\u0e49ง)", "Cheer your dodo: +{0}% for this skill on its next action (3 per round)", "Sorak dodo: +{0}% untuk skill ini di aksi berikutnya (3 per ronde)", "도도 응원: 다음 행동에서 이 스킬 +{0}% (라운드당 3회)" }
		},
		{
			"cheer_short",
			new string[4] { "เช\u0e35ยร\u0e4c {0}/{1} · +{2}%", "Cheers {0}/{1} · +{2}%", "Sorak {0}/{1} · +{2}%", "응원 {0}/{1} · +{2}%" }
		},
		{
			"cheer_float",
			new string[4] { "เช\u0e35ยร\u0e4c!", "Cheer!", "Sorak!", "응원!" }
		},
		{
			"armed",
			new string[4] { "รอใช\u0e49", "Armed", "Siap", "대기 중" }
		},
		{
			"cheer_left",
			new string[4] { "เช\u0e35ยร\u0e4cเหล\u0e37อ {0}/{1} ในยกน\u0e35\u0e49 · กด = +{2}% ให\u0e49สก\u0e34ลน\u0e31\u0e49นในแอคช\u0e31\u0e48นถ\u0e31ดไป", "Cheers left {0}/{1} this round · tap = +{2}% for that skill on the next action", "Sorakan tersisa {0}/{1} ronde ini · ketuk = +{2}% untuk skill itu di aksi berikutnya", "이번 라운드 응원 {0}/{1} · 누르면 다음 행동에서 그 스킬 +{2}%" }
		},
		{
			"bet_head",
			new string[4] { "เด\u0e34มพ\u0e31น T Stone · ป\u0e34ดร\u0e31บใน {0} ว\u0e34", "Bet T Stone · closes in {0}s", "Taruhan T Stone · tutup dalam {0} dtk", "T Stone 베팅 · {0}초 후 마감" }
		},
		{
			"bet_amount",
			new string[4] { "เด\u0e34มพ\u0e31น {0} ×{1}", "Bet on {0} ×{1}", "Taruhan untuk {0} ×{1}", "{0}에 베팅 ×{1}" }
		},
		{
			"bet_confirm",
			new string[4] { "เด\u0e34มพ\u0e31น {0} ×{1}?", "Bet on {0} ×{1}?", "Bertaruh untuk {0} ×{1}?", "{0}에 ×{1}로 베팅할까요?" }
		},
		{
			"bet_sub",
			new string[4] { "ถ\u0e49าชนะได\u0e49ร\u0e31บ {0} T Stone · ยกเล\u0e34กแมตช\u0e4c = ค\u0e37นเง\u0e34น", "If it wins you get {0} T Stone · cancelled match = refund", "Jika menang dapat {0} T Stone · laga batal = uang kembali", "이기면 {0} T Stone 지급 · 경기 취소 시 환불" }
		},
		{
			"bet_btn",
			new string[4] { "เด\u0e34มพ\u0e31น", "Bet", "Bertaruh", "베팅" }
		},
		{
			"bet_sub_tax",
			new string[4] { "ถ\u0e49าชนะได\u0e49ร\u0e31บ {0} T Stone (ห\u0e31กภาษ\u0e35 {1}% · {2} T) · ยกเล\u0e34กแมตช\u0e4c = ค\u0e37นเง\u0e34น", "If it wins you get {0} T Stone ({1}% tax · {2} T) · cancelled match = refund", "Jika menang dapat {0} T Stone (pajak {1}% · {2} T) · laga batal = uang kembali", "이기면 {0} T Stone 지급 (세금 {1}% · {2} T) · 경기 취소 시 환불" }
		},
		{
			"bet_min",
			new string[4] { "เด\u0e34มพ\u0e31นข\u0e31\u0e49นต\u0e48ำ {0} T Stone", "Minimum bet {0} T Stone", "Taruhan minimal {0} T Stone", "최소 베팅 {0} T Stone" }
		},
		{
			"bet_min_need",
			new string[4] { "ต\u0e49องม\u0e35 T Stone อย\u0e48างน\u0e49อย {0} (เด\u0e34มพ\u0e31นข\u0e31\u0e49นต\u0e48ำ)", "You need at least {0} T Stone (minimum bet)", "Butuh minimal {0} T Stone (taruhan minimal)", "최소 {0} T Stone이 필요합니다 (최소 베팅)" }
		},
		{
			"bet_mine",
			new string[4] { "เด\u0e34มพ\u0e31น {0} {1} T (×{2})", "Your bet: {0} {1} T (×{2})", "Taruhanmu: {0} {1} T (×{2})", "내 베팅: {0} {1} T (×{2})" }
		},
		{
			"no_tstone",
			new string[4] { "T Stone ไม\u0e48พอ", "Not enough T Stone", "T Stone tidak cukup", "T Stone이 부족합니다" }
		},
		{
			"challenged",
			new string[4] { "{0} ท\u0e49าส\u0e39\u0e49โดโด\u0e49!", "{0} challenges you to a dodo fight!", "{0} menantangmu adu dodo!", "{0}님이 도도 결투를 신청했습니다!" }
		},
		{
			"challenged_sub",
			new string[4] { "{0} · พล\u0e31ง {1} · ชนะ {2} แพ\u0e49 {3}", "{0} · power {1} · W {2} L {3}", "{0} · kekuatan {1} · M {2} K {3}", "{0} · 전투력 {1} · {2}승 {3}패" }
		},
		{
			"challenged_expire",
			new string[4] { "ตอบภายใน {0} ว\u0e34", "Answer within {0}s", "Jawab dalam {0} dtk", "{0}초 안에 응답하세요" }
		},
		{
			"accept",
			new string[4] { "ร\u0e31บคำท\u0e49า", "Accept", "Terima", "수락" }
		},
		{
			"decline",
			new string[4] { "ปฏ\u0e34เสธ", "Decline", "Tolak", "거절" }
		},
		{
			"sk_peck_storm",
			new string[4] { "จ\u0e34กพาย\u0e38", "Storm Peck", "Patuk Badai", "폭풍 쪼기" }
		},
		{
			"sk_peck_pierce",
			new string[4] { "จ\u0e34กเจาะเกราะ", "Piercing Peck", "Patuk Penembus", "관통 쪼기" }
		},
		{
			"sk_peck_double",
			new string[4] { "จ\u0e34กค\u0e39\u0e48", "Double Peck", "Patuk Ganda", "이중 쪼기" }
		},
		{
			"sk_peck_hard",
			new string[4] { "จ\u0e34กแรง", "Hard Peck", "Patuk Keras", "강한 쪼기" }
		},
		{
			"sk_meteor",
			new string[4] { "อ\u0e38กกาบาตพ\u0e38\u0e48งชน", "Meteor Charge", "Terjangan Meteor", "운석 돌진" }
		},
		{
			"sk_rend",
			new string[4] { "กรงเล\u0e47บฉ\u0e35ก", "Rending Claws", "Cakar Pencabik", "찢는 발톱" }
		},
		{
			"sk_headbutt",
			new string[4] { "โหม\u0e48งกระแทก", "Headbutt", "Sundulan", "박치기" }
		},
		{
			"sk_dropkick",
			new string[4] { "กระโดดถ\u0e35บ", "Dropkick", "Tendangan Terbang", "날아차기" }
		},
		{
			"sk_primal_armor",
			new string[4] { "เกราะบรรพกาล", "Primal Armor", "Zirah Purba", "원시 갑옷" }
		},
		{
			"sk_counter",
			new string[4] { "สวนกล\u0e31บ", "Counter", "Serangan Balik", "반격" }
		},
		{
			"sk_fluff_up",
			new string[4] { "ขนพอง", "Fluff Up", "Bulu Mengembang", "깃털 부풀리기" }
		},
		{
			"sk_evade",
			new string[4] { "หลบพล\u0e34\u0e49ว", "Nimble Dodge", "Elakan Lincah", "날렵한 회피" }
		},
		{
			"sk_lick",
			new string[4] { "เล\u0e35ยแผล", "Lick Wounds", "Jilat Luka", "상처 핥기" }
		},
		{
			"sk_thorns",
			new string[4] { "หนามสะท\u0e49อน", "Thorns", "Duri Pemantul", "가시 반사" }
		},
		{
			"sk_roar",
			new string[4] { "ข\u0e39\u0e48คำราม", "Roar", "Raungan", "위협의 포효" }
		},
		{
			"sk_wall",
			new string[4] { "ต\u0e31\u0e49งกำแพง", "Wall Up", "Tembok", "철벽" }
		},
		{
			"sk_strike",
			new string[4] { "ต\u0e35ธรรมดา", "Strike", "Pukulan Biasa", "기본 공격" }
		},
		{
			"sk_brace",
			new string[4] { "ต\u0e31\u0e49งหล\u0e31ก", "Brace", "Kuda-kuda", "버티기" }
		},
		{
			"skd_peck_storm",
			new string[4] { "จ\u0e34ก 5 คร\u0e31\u0e49ง คร\u0e31\u0e49งละ 50% · คร\u0e34 30% ต\u0e48อคร\u0e31\u0e49ง", "5 pecks × 50% · 30% crit per peck", "5 pecks × 50% · 30% crit per peck", "5 pecks × 50% · 30% crit per peck" }
		},
		{
			"skd_peck_pierce",
			new string[4] { "ดาเมจ 150% ไม\u0e48สนพล\u0e31งป\u0e49องก\u0e31น", "150% damage, ignores defense", "150% damage, ignores defense", "150% damage, ignores defense" }
		},
		{
			"skd_peck_double",
			new string[4] { "จ\u0e34ก 2 คร\u0e31\u0e49ง คร\u0e31\u0e49งละ 70%", "2 pecks × 70%", "2 pecks × 70%", "2 pecks × 70%" }
		},
		{
			"skd_peck_hard",
			new string[4] { "ดาเมจ 120%", "120% damage", "120% damage", "120% damage" }
		},
		{
			"skd_meteor",
			new string[4] { "ดาเมจ 300% + ม\u0e36น 2 แอคช\u0e31\u0e48น", "300% damage + stun for 2 actions", "300% damage + stun for 2 actions", "300% damage + stun for 2 actions" }
		},
		{
			"skd_rend",
			new string[4] { "ดาเมจ 180% + เล\u0e37อดไหล 5%/แอคช\u0e31\u0e48น 3 แอคช\u0e31\u0e48น", "180% damage + bleed 5% per action for 3 actions", "180% damage + bleed 5% per action for 3 actions", "180% damage + bleed 5% per action for 3 actions" }
		},
		{
			"skd_headbutt",
			new string[4] { "ดาเมจ 150% + 40% ม\u0e36น 1 แอคช\u0e31\u0e48น", "150% damage + 40% stun for 1 action", "150% damage + 40% stun for 1 action", "150% damage + 40% stun for 1 action" }
		},
		{
			"skd_dropkick",
			new string[4] { "ดาเมจ 130% + อ\u0e35กฝ\u0e48ายช\u0e49าลง 1 แอคช\u0e31\u0e48น", "130% damage + slows the foe for 1 action", "130% damage + slows the foe for 1 action", "130% damage + slows the foe for 1 action" }
		},
		{
			"skd_primal_armor",
			new string[4] { "ไม\u0e48โดนดาเมจ 2 แอคช\u0e31\u0e48น + สะท\u0e49อน 50%", "No damage for 2 actions + reflects 50%", "No damage for 2 actions + reflects 50%", "No damage for 2 actions + reflects 50%" }
		},
		{
			"skd_counter",
			new string[4] { "บล\u0e47อกการโจมต\u0e35คร\u0e31\u0e49งถ\u0e31ดไป + ต\u0e35สวน 120%", "Blocks the next attack + strikes back 120%", "Blocks the next attack + strikes back 120%", "Blocks the next attack + strikes back 120%" }
		},
		{
			"skd_fluff_up",
			new string[4] { "ลดดาเมจ 50% 2 แอคช\u0e31\u0e48น", "50% less damage for 2 actions", "50% less damage for 2 actions", "50% less damage for 2 actions" }
		},
		{
			"skd_evade",
			new string[4] { "หลบการโจมต\u0e35คร\u0e31\u0e49งถ\u0e31ดไป", "Dodges the next attack", "Dodges the next attack", "Dodges the next attack" }
		},
		{
			"skd_lick",
			new string[4] { "ฟ\u0e37\u0e49น HP 8% ของเต\u0e47ม", "Heals 8% of max HP", "Heals 8% of max HP", "Heals 8% of max HP" }
		},
		{
			"skd_thorns",
			new string[4] { "สะท\u0e49อนดาเมจ 30% 2 แอคช\u0e31\u0e48น", "Reflects 30% of damage for 2 actions", "Reflects 30% of damage for 2 actions", "Reflects 30% of damage for 2 actions" }
		},
		{
			"skd_roar",
			new string[4] { "ลดดาเมจค\u0e39\u0e48ต\u0e48อส\u0e39\u0e49 30% 2 แอคช\u0e31\u0e48น", "Foe deals 30% less damage for 2 actions", "Foe deals 30% less damage for 2 actions", "Foe deals 30% less damage for 2 actions" }
		},
		{
			"skd_wall",
			new string[4] { "ลดดาเมจ 60% แอคช\u0e31\u0e48นน\u0e35\u0e49", "60% less damage this action", "60% less damage this action", "60% less damage this action" }
		},
		{
			"skd_strike",
			new string[4] { "จ\u0e34กธรรมดา ดาเมจ 100%", "Normal peck, 100% damage", "Normal peck, 100% damage", "Normal peck, 100% damage" }
		},
		{
			"skd_brace",
			new string[4] { "ลดดาเมจ 30% แอคช\u0e31\u0e48นน\u0e35\u0e49 + สตาม\u0e34นา +10%", "30% less damage this action + 10% stamina", "30% less damage this action + 10% stamina", "30% less damage this action + 10% stamina" }
		},
		{
			"skf_peck_storm",
			new string[4] { "คร\u0e34 = ดาเมจ ×1.5 · ท\u0e38ก hit ส\u0e38\u0e48ม ±10%", "Crit = ×1.5 damage · every hit varies ±10%", "Crit = ×1.5 damage · every hit varies ±10%", "Crit = ×1.5 damage · every hit varies ±10%" }
		},
		{
			"skf_peck_pierce",
			new string[4] { "ย\u0e31งโดนลดดาเมจจากท\u0e48าป\u0e49องก\u0e31นของอ\u0e35กฝ\u0e48าย", "Still reduced by the foe's defending moves", "Still reduced by the foe's defending moves", "Still reduced by the foe's defending moves" }
		},
		{
			"skf_meteor",
			new string[4] { "ม\u0e36น = เส\u0e35ยแอคช\u0e31\u0e48นถ\u0e31ดไปตามจำนวน · สก\u0e34ลท\u0e35\u0e48ส\u0e38\u0e48มได\u0e49ตอนม\u0e36นหาย", "Stun = the next actions are lost · a skill rolled while stunned is lost", "Stun = the next actions are lost · a skill rolled while stunned is lost", "Stun = the next actions are lost · a skill rolled while stunned is lost" }
		},
		{
			"skf_rend",
			new string[4] { "5% ของ HP เต\u0e47ม · เร\u0e34\u0e48มแอคช\u0e31\u0e48นถ\u0e31ดไป · ไม\u0e48สนพล\u0e31งป\u0e49องก\u0e31น · ต\u0e34ดซ\u0e49ำ = น\u0e31บใหม\u0e48", "5% of max HP · starts next action · ignores defense · re-applying restarts it", "5% of max HP · starts next action · ignores defense · re-applying restarts it", "5% of max HP · starts next action · ignores defense · re-applying restarts it" }
		},
		{
			"skf_dropkick",
			new string[4] { "ลดความเร\u0e47ว 50% ในแอคช\u0e31\u0e48นถ\u0e31ดไป", "Speed −50% on the next action", "Speed −50% on the next action", "Speed −50% on the next action" }
		},
		{
			"skf_primal_armor",
			new string[4] { "แอคช\u0e31\u0e48นน\u0e35\u0e49และถ\u0e31ดไป · ก\u0e31นเล\u0e37อดไหลด\u0e49วย · สะท\u0e49อน 50% ของดาเมจท\u0e35\u0e48ควรโดน ไม\u0e48สนพล\u0e31งป\u0e49องก\u0e31นผ\u0e39\u0e49ต\u0e35", "This action and the next · also stops bleeding · reflects 50% of the damage it would take, ignoring the attacker's defense", "This action and the next · also stops bleeding · reflects 50% of the damage it would take, ignoring the attacker's defense", "This action and the next · also stops bleeding · reflects 50% of the damage it would take, ignoring the attacker's defense" }
		},
		{
			"skf_counter",
			new string[4] { "บล\u0e47อกท\u0e31\u0e49งท\u0e48า (ท\u0e38ก hit + ผลต\u0e34ดต\u0e31ว) · ค\u0e49างจนโดนต\u0e35หร\u0e37อจบยก · ต\u0e35สวนไม\u0e48คร\u0e34", "Blocks the whole move (every hit + effects) · waits until hit or the round ends · the strike back never crits", "Blocks the whole move (every hit + effects) · waits until hit or the round ends · the strike back never crits", "Blocks the whole move (every hit + effects) · waits until hit or the round ends · the strike back never crits" }
		},
		{
			"skf_fluff_up",
			new string[4] { "แอคช\u0e31\u0e48นน\u0e35\u0e49และถ\u0e31ดไป · ลดดาเมจหลายอ\u0e31นซ\u0e49อน = ใช\u0e49ค\u0e48ามากส\u0e38ด", "This action and the next · several reductions = the biggest one counts", "This action and the next · several reductions = the biggest one counts", "This action and the next · several reductions = the biggest one counts" }
		}
	};

	public static bool LobbyOpen => IsOpen(_lobby);

	public static bool DetailOpen => IsOpen(_detail);

	public static bool PickOpen => IsOpen(_pick);

	public static bool HudOpen => IsOpen(_hud);

	private static bool IsOpen(CommunityDodoFightUI ui)
	{
		if (ui != null)
		{
			return ui.gameObject.activeSelf;
		}
		return false;
	}

	public static void ShowLobby()
	{
		if (CommunityDodoFight.Enabled && CommunityDodoFight.CurrentLobby != null)
		{
			CommunityDodoFightUI communityDodoFightUI = Ensure(ref _lobby, Kind.Lobby);
			if (communityDodoFightUI != null)
			{
				communityDodoFightUI.gameObject.SetActive(value: true);
				communityDodoFightUI.Refresh();
			}
		}
	}

	public static void ShowDetail()
	{
		if (CommunityDodoFight.Enabled && CommunityDodoFight.DetailCard != null)
		{
			CommunityDodoFightUI communityDodoFightUI = Ensure(ref _detail, Kind.Detail);
			if (communityDodoFightUI != null)
			{
				_detailSel = -1;
				communityDodoFightUI.gameObject.SetActive(value: true);
				communityDodoFightUI.Refresh();
			}
		}
	}

	public static void MatchChanged()
	{
		if (!CommunityDodoFight.Enabled || CommunityDodoFight.CurrentMatch == null)
		{
			Hide(_hud);
			return;
		}
		CommunityDodoFightUI communityDodoFightUI = Ensure(ref _hud, Kind.Hud);
		if (communityDodoFightUI != null)
		{
			communityDodoFightUI.gameObject.SetActive(value: true);
			communityDodoFightUI.Refresh();
		}
	}

	public static void OwnMatchStarted()
	{
		if (LobbyOpen)
		{
			CloseLobby(tellServer: true);
		}
		Hide(_detail);
		ClosePick();
	}

	public static void CloseAll()
	{
		Hide(_lobby);
		Hide(_detail);
		Hide(_hud);
		ClosePick();
	}

	public static void StartPick()
	{
		if (CommunityDodoFight.Enabled)
		{
			if (LobbyOpen)
			{
				CloseLobby(tellServer: true);
			}
			Hide(_detail);
			CommunityDodoFightUI communityDodoFightUI = Ensure(ref _pick, Kind.Pick);
			if (communityDodoFightUI != null)
			{
				communityDodoFightUI._pSentAt = -100f;
				communityDodoFightUI._pHaveSpot = false;
				communityDodoFightUI.gameObject.SetActive(value: true);
				communityDodoFightUI.Refresh();
			}
		}
	}

	public static void ClosePick()
	{
		Hide(_pick);
		CommunityDodoFightFx.HidePick();
	}

	public static void MatchEvent(CommunityDodoFight.Match match, int side, string kind, string text, string skill, string tier)
	{
		CommunityDodoFightUI hud = _hud;
		if (hud == null || !hud.gameObject.activeSelf || match == null)
		{
			return;
		}
		try
		{
			switch (kind)
			{
			case "skill":
			{
				if (side < 0 || side >= 2)
				{
					break;
				}
				string line = string.Empty;
				if (match.Sides[side].Card != null)
				{
					foreach (CommunityDodoFight.Skill skill2 in match.Sides[side].Card.Skills)
					{
						if (skill2.Id == skill)
						{
							line = skill2.Line;
						}
					}
				}
				hud.Float(match.Sides[side].Entity, "[" + SideHex[side] + "]" + TierTag(tier, colored: false) + SkillName(skill) + "![-]", CommunityDodoFightFx.SkillIcon(skill, line));
				break;
			}
			case "miss":
				if (side >= 0 && side < 2)
				{
					string entity = match.Sides[1 - side].Entity;
					hud.Float(entity, "[8fe9ff]" + T("dodge_float") + "[-]");
					CommunityDodoFightFx.PlayDodge(entity);
				}
				break;
			case "cheer":
				if (side >= 0 && side < 2)
				{
					hud.Float(match.Sides[side].Entity, "[ff9ad6]" + T("cheer_float") + "[-]");
				}
				break;
			case "round":
			case "end":
				if (match == CommunityDodoFight.CurrentMatch && !string.IsNullOrEmpty(text) && UIManager.Alarm != null)
				{
					UIManager.Alarm.ShowNotify(Plain(text), "action_attack", major: true, 2.5f);
				}
				break;
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static void Hide(CommunityDodoFightUI ui)
	{
		if (ui != null && ui.gameObject.activeSelf)
		{
			ui.gameObject.SetActive(value: false);
		}
	}

	private static void CloseLobby(bool tellServer)
	{
		bool lobbyOpen = LobbyOpen;
		Hide(_lobby);
		Hide(_detail);
		if (tellServer && lobbyOpen)
		{
			CommunityDodoFight.SendClose();
		}
	}

	private static void CloseDetail()
	{
		Hide(_detail);
	}

	public static void ShowChallenged(string from, string name, CommunityDodoFight.Card card, float expiresIn)
	{
		MessageBox messageBox = UIManager.MessageBox;
		if (messageBox == null || string.IsNullOrEmpty(from))
		{
			return;
		}
		string text = Plain(string.IsNullOrEmpty(name) ? from : name);
		StringBuilder stringBuilder = new StringBuilder();
		if (card != null)
		{
			stringBuilder.Append(T("challenged_sub", Plain(CardName(card)), card.Power, card.Wins, card.Losses));
			string text2 = SkillsLine(card, colored: false);
			if (text2.Length > 0)
			{
				stringBuilder.Append('\n').Append(text2);
			}
			stringBuilder.Append('\n');
		}
		int num = Mathf.Max(1, Mathf.RoundToInt(expiresIn));
		stringBuilder.Append(T("challenged_expire", num));
		messageBox.Show(T("challenged", text), stringBuilder.ToString(), delegate(bool ok)
		{
			CommunityDodoFight.ReplyChallenge(from, ok);
		}, T("accept"), T("decline"));
		if (expiresIn > 0f)
		{
			messageBox.SetHideTimer(Time.time + expiresIn);
		}
	}

	public static bool Click(string key)
	{
		if (!string.IsNullOrEmpty(key) && _buttons.TryGetValue(key, out var value))
		{
			return Press(value);
		}
		return false;
	}

	public static List<string> ClickableKeys()
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, Btn> button in _buttons)
		{
			Btn value = button.Value;
			if (value != null && value.Go != null && value.Go.activeInHierarchy && value.Enabled)
			{
				list.Add(button.Key);
			}
		}
		list.Sort((string a, string b) => string.CompareOrdinal(a, b));
		return list;
	}

	public static Dictionary<string, object> DebugState()
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary["lobbyOpen"] = LobbyOpen;
		dictionary["detailOpen"] = DetailOpen;
		dictionary["hudOpen"] = HudOpen;
		dictionary["pickOpen"] = PickOpen;
		if (PickOpen)
		{
			dictionary["pickText"] = _pick._pHint.text + (_pick._pHaveSpot ? (" · spot " + _pick._pSpot) : string.Empty);
		}
		dictionary["page"] = _page;
		dictionary["detailSelected"] = _detailSel;
		dictionary["clickable"] = ClickableKeys();
		if (LobbyOpen)
		{
			CommunityDodoFightUI lobby = _lobby;
			List<object> list = new List<object>();
			foreach (PlayerRow row in lobby._rows)
			{
				if (row.Go.activeSelf)
				{
					list.Add(row.PlayerId + " | " + row.Name.text + " | " + row.Info.text);
				}
			}
			dictionary["lobbyText"] = new List<object>
			{
				Shown(lobby._cardName),
				Shown(lobby._cardInfo),
				Shown(lobby._cardSkills),
				Shown(lobby._noDodo),
				lobby._bReroll.Go.activeSelf ? lobby._bReroll.Label.text : string.Empty,
				lobby._bQueue.Label.text,
				lobby._arena.text,
				lobby._playersHead.text,
				lobby._pageLabel.text,
				"portrait " + (lobby._portrait.gameObject.activeSelf ? lobby._portrait.spriteName : "(none)")
			};
			dictionary["lobbyRows"] = list;
		}
		if (DetailOpen)
		{
			CommunityDodoFightUI detail = _detail;
			List<object> list2 = new List<object>();
			list2.Add(detail._dName.text);
			list2.Add(detail._dSpecies.text);
			list2.Add(detail._dRecord.text);
			for (int i = 0; i < 4; i++)
			{
				list2.Add(T("stat" + i) + " " + detail._statValue[i].text);
			}
			list2.Add(detail._prefLabel.text);
			for (int j = 0; j < detail._infoRows.Length; j++)
			{
				InfoRow infoRow = detail._infoRows[j];
				if (infoRow.Button.Go.activeSelf)
				{
					list2.Add(j + ": " + infoRow.Title.text + " — " + infoRow.Desc.text);
				}
			}
			list2.Add("box: " + detail._dDesc.text);
			dictionary["detailText"] = list2;
		}
		if (HudOpen)
		{
			CommunityDodoFightUI hud = _hud;
			List<object> list3 = new List<object>();
			list3.Add(hud._hHeader.text);
			if (hud._hBanner.gameObject.activeSelf)
			{
				list3.Add("banner: " + hud._hBanner.text);
			}
			foreach (KeyValuePair<string, HeadBar> hBar in hud._hBars)
			{
				list3.Add("headbar " + hBar.Key + " side " + hBar.Value.Side + " hp " + Mathf.RoundToInt(hBar.Value.Ratio * 100f) + "% " + (hBar.Value.Go.activeSelf ? ("at " + Mathf.RoundToInt(hBar.Value.Go.transform.localPosition.x) + "," + Mathf.RoundToInt(hBar.Value.Go.transform.localPosition.y)) : "hidden"));
			}
			foreach (Floater floater in hud._floaters)
			{
				if (floater.Label.gameObject.activeSelf)
				{
					list3.Add("float " + floater.Entity + ": " + floater.Label.text);
				}
			}
			if (hud._hCheer.activeSelf)
			{
				for (int k = 0; k < 2; k++)
				{
					Btn btn = hud._hCheerBtn[k];
					if (btn.Go.activeSelf)
					{
						list3.Add("cheer" + k + ": " + btn.Label.text.Replace("\n", " / ") + (btn.Enabled ? string.Empty : " (disabled)"));
					}
				}
				list3.Add(hud._hCheerHint.text + " at " + Mathf.RoundToInt(hud._hCheer.transform.localPosition.x) + "," + Mathf.RoundToInt(hud._hCheer.transform.localPosition.y));
			}
			if (hud._hBet.activeSelf)
			{
				list3.Add(Shown(hud._hBetHead));
				for (int l = 0; l < 2; l++)
				{
					if (hud._hBetBtn[l].Go.activeSelf)
					{
						list3.Add("bet" + l + ": " + hud._hBetBtn[l].Label.text);
					}
				}
				list3.Add(Shown(hud._hBetMine));
			}
			list3.Add("top " + Mathf.RoundToInt(hud._hTop.transform.localPosition.y));
			dictionary["hudText"] = list3;
		}
		return dictionary;
	}

	private static string Shown(UILabel label)
	{
		if (!(label != null) || !label.gameObject.activeSelf)
		{
			return string.Empty;
		}
		return label.text;
	}

	private static CommunityDodoFightUI Ensure(ref CommunityDodoFightUI slot, Kind kind)
	{
		if (slot != null)
		{
			return slot;
		}
		UIRoot uIRoot = (Durango.Utils.Singleton<UIManager>.HasInstance() ? Durango.Utils.Singleton<UIManager>.Instance().UIRoot : null);
		if (uIRoot == null)
		{
			return null;
		}
		GameObject obj = uIRoot.gameObject.AddChild();
		obj.name = "CommunityDodo" + kind;
		CommunityDodoFightUI communityDodoFightUI = obj.AddComponent<CommunityDodoFightUI>();
		communityDodoFightUI._kind = kind;
		communityDodoFightUI._root = uIRoot;
		try
		{
			communityDodoFightUI.Build();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		slot = communityDodoFightUI;
		return communityDodoFightUI;
	}

	private void Build()
	{
		_uiPanel = base.gameObject.AddComponent<UIPanel>();
		_uiPanel.depth = ((_kind == Kind.Hud) ? 5990 : ((_kind == Kind.Detail) ? 6010 : ((_kind == Kind.Pick) ? 6005 : 6000)));
		_style = FindStyle();
		switch (_kind)
		{
		case Kind.Lobby:
			BuildLobby();
			break;
		case Kind.Detail:
			BuildDetail();
			break;
		case Kind.Pick:
			BuildPick();
			break;
		default:
			BuildHud();
			break;
		}
	}

	private void OnDestroy()
	{
		string value = KeyPrefix(_kind);
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, Btn> button in _buttons)
		{
			if (button.Key.StartsWith(value, StringComparison.Ordinal))
			{
				list.Add(button.Key);
			}
		}
		foreach (string item in list)
		{
			_buttons.Remove(item);
		}
		if (_lobby == this)
		{
			_lobby = null;
		}
		if (_detail == this)
		{
			_detail = null;
		}
		if (_pick == this)
		{
			_pick = null;
		}
		if (_hud == this)
		{
			_hud = null;
		}
	}

	private static string KeyPrefix(Kind kind)
	{
		return kind switch
		{
			Kind.Lobby => "lobby.",
			Kind.Detail => "detail.",
			Kind.Pick => "pick.",
			_ => "hud.",
		};
	}

	private void BuildLobby()
	{
		GameObject parent = base.gameObject;
		Catcher(delegate
		{
			CloseLobby(tellServer: true);
		});
		Box(parent, 0, 0, 880, 590, PanelBg, 1, UIWidget.Pivot.Center, blocking: true);
		int num = -440;
		int num2 = 440;
		int y = 295 - 32;
		Label(parent, T("title"), num + 24, y, 30, Gold, 420, UIWidget.Pivot.Left);
		MakeButton(parent, "lobby.close", "X", num2 - 44, y, 72, 56, delegate
		{
			CloseLobby(tellServer: true);
		});
		MakeButton(parent, "lobby.refresh", T("refresh"), num2 - 44 - 36 - 12 - 70, y, 140, 56, CommunityDodoFight.RequestLobby);
		int num3 = 295 - 64 - 65;
		Box(parent, 0, num3, 848, 130, CardBg, 2);
		int num4 = num + 16 + 12 + 52;
		Box(parent, num4, num3, 108, 108, FrameBg, 3);
		_portrait = PortraitSprite(parent, num4, num3, 100, 100, 4);
		_portraitAlt = Label(parent, "?", num4, num3, 40, TextDim, 100, UIWidget.Pivot.Center);
		int x = num4 + 54 + 16;
		_cardName = Label(parent, string.Empty, x, num3 + 38, 28, TextLight, 420, UIWidget.Pivot.Left);
		_cardInfo = Label(parent, string.Empty, x, num3 + 4, 22, TextDim, 420, UIWidget.Pivot.Left);
		_cardSkills = Label(parent, string.Empty, x, num3 - 32, 20, TextLight, 420, UIWidget.Pivot.Left);
		_noDodo = Label(parent, T("no_dodo"), x, num3, 22, TextLight, 400, UIWidget.Pivot.Left, 3);
		int x2 = num2 - 16 - 12 - 95;
		_bDetail = MakeButton(parent, "lobby.detail", T("detail"), x2, num3 + 32, 190, 60, delegate
		{
			CommunityDodoFight.RequestDetail(string.Empty);
		});
		_bReroll = MakeButton(parent, "lobby.reroll", string.Empty, x2, num3 - 32, 190, 60, AskReroll);
		_bClaim = MakeButton(parent, "lobby.claim", T("get_dodo"), x2, num3, 190, 100, CommunityDodoFight.Reroll);
		int num5 = num3 - 65 - 8 - 31;
		_bQueue = MakeButton(parent, "lobby.queue", string.Empty, -285, num5, 270, 62, ToggleQueue);
		_bPractice = MakeButton(parent, "lobby.practice", T("practice"), 0, num5, 270, 62, CommunityDodoFight.Practice);
		_bWatch = MakeButton(parent, "lobby.watch", T("watch"), 285, num5, 270, 62, Watch);
		int num6 = num5 - 31 - 8 - 15;
		_arena = Label(parent, string.Empty, num + 24, num6, 22, TextLight, 832, UIWidget.Pivot.Left);
		int num7 = num6 - 15 - 8 - 30;
		_playersHead = Label(parent, string.Empty, num + 24, num7, 22, TextDim, 500, UIWidget.Pivot.Left);
		_bPrev = MakeButton(parent, "lobby.prev", "<", 230, num7, 80, 56, delegate
		{
			_page--;
			Refresh();
		});
		_pageLabel = Label(parent, string.Empty, 310, num7, 22, TextLight, 80, UIWidget.Pivot.Center);
		_bNext = MakeButton(parent, "lobby.next", ">", 390, num7, 80, 56, delegate
		{
			_page++;
			Refresh();
		});
		int num8 = num7 - 30 - 8 - 33;
		for (int num9 = 0; num9 < 6; num9++)
		{
			int num10 = ((num9 % 2 == 0) ? (-218) : 218);
			int num11 = num8 - num9 / 2 * 72;
			PlayerRow playerRow = new PlayerRow();
			playerRow.Go = parent.AddChild();
			playerRow.Go.name = "row" + num9;
			Box(playerRow.Go, num10, num11, 424, 66, CardBg, 2);
			playerRow.Name = Label(playerRow.Go, string.Empty, num10 - 200, num11 + 13, 22, TextLight, 200, UIWidget.Pivot.Left);
			playerRow.Info = Label(playerRow.Go, string.Empty, num10 - 200, num11 - 15, 18, TextDim, 200, UIWidget.Pivot.Left);
			PlayerRow captured = playerRow;
			playerRow.View = MakeButton(playerRow.Go, "lobby.row" + num9 + ".view", T("view"), num10 + 62, num11, 96, 58, delegate
			{
				CommunityDodoFight.RequestDetail(captured.PlayerId);
			});
			playerRow.Challenge = MakeButton(playerRow.Go, "lobby.row" + num9 + ".challenge", T("challenge"), num10 + 162, num11, 96, 58, delegate
			{
				ChallengeRow(captured);
			});
			_rows.Add(playerRow);
		}
	}

	private void BuildDetail()
	{
		GameObject gameObject = base.gameObject;
		Catcher(CloseDetail);
		Box(gameObject, 0, 0, 1080, 540, PanelBg, 1, UIWidget.Pivot.Center, blocking: true);
		int num = 270;
		int num2 = -540;
		int num3 = 540;
		int num4 = num - 32;
		Label(gameObject, T("detail_title"), num2 + 24, num4, 28, Gold, 500, UIWidget.Pivot.Left);
		MakeButton(gameObject, "detail.close", "X", num3 - 44, num4, 72, 56, CloseDetail);
		int num5 = num2 + 16 + 150;
		int num6 = num4 - 48 - 160;
		Box(gameObject, num5, num6, 300, 320, FrameBg, 3);
		_dPortrait = PortraitSprite(gameObject, num5, num6, 104, 104, 4);
		_dPortraitAlt = Label(gameObject, "?", num5, num6, 40, TextDim, 100, UIWidget.Pivot.Center);
		try
		{
			GameObject gameObject2 = gameObject.AddChild();
			gameObject2.name = "dodoModel";
			gameObject2.transform.localPosition = new Vector3(num5, num6, 0f);
			_dModel = gameObject2.AddComponent<UIModelViewer>();
			_dModel.width = 292;
			_dModel.height = 312;
			_dModel.depth = 5;
			NGUITools.AddWidgetCollider(gameObject2);
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
			_dModel = null;
		}
		int num7 = num6 - 160 - 26;
		_dName = Label(gameObject, string.Empty, num2 + 16, num7, 26, TextLight, 300, UIWidget.Pivot.Left);
		_dSpecies = Label(gameObject, string.Empty, num2 + 16, num7 - 30, 20, TextDim, 300, UIWidget.Pivot.Left);
		_dRecord = Label(gameObject, string.Empty, num2 + 16, num7 - 56, 20, TextLight, 300, UIWidget.Pivot.Left);
		int num8 = num2 + 16 + 300 + 28;
		int num9 = num4 - 70;
		Label(gameObject, T("stats_head"), num8, num9 + 40, 20, TextDim, 300, UIWidget.Pivot.Left);
		for (int i = 0; i < 4; i++)
		{
			int y = num9 - i * 44;
			Label(gameObject, T("stat" + i), num8, y, 20, TextLight, 70, UIWidget.Pivot.Left);
			Box(gameObject, num8 + 74, y, 170, 18, BarBack, 3, UIWidget.Pivot.Left);
			_statFill[i] = Box(gameObject, num8 + 74, y, 2, 18, StatColor[i], 4, UIWidget.Pivot.Left);
			_statValue[i] = Label(gameObject, string.Empty, num8 + 74 + 170 + 10, y, 20, TextLight, 44, UIWidget.Pivot.Left);
		}
		int num10 = num9 - 176 - 20;
		_prefLabel = Label(gameObject, string.Empty, num8, num10, 20, TextLight, 300, UIWidget.Pivot.Left);
		Box(gameObject, num8, num10 - 30, 300, 20, BarBack, 3, UIWidget.Pivot.Left);
		_prefAtk = Box(gameObject, num8, num10 - 30, 2, 20, AtkColor, 4, UIWidget.Pivot.Left);
		_prefDef = Box(gameObject, num8 + 300, num10 - 30, 2, 20, DefColor, 4, UIWidget.Pivot.Right);
		int num11 = 360;
		int num12 = num3 - 16 - num11 / 2;
		int num13 = num12 - num11 / 2;
		int num14 = num4 - 70;
		Label(gameObject, T("basic_head"), num13, num14 + 40, 20, TextDim, num11, UIWidget.Pivot.Left);
		_infoRows[0] = MakeInfoRow(gameObject, "detail.basic0", num12, num14, num11, 58, 0);
		_infoRows[1] = MakeInfoRow(gameObject, "detail.basic1", num12, num14 - 64, num11, 58, 1);
		int num15 = num14 - 64 - 29 - 22;
		Label(gameObject, T("skills_head"), num13, num15, 20, TextDim, num11, UIWidget.Pivot.Left);
		_infoRows[2] = MakeInfoRow(gameObject, "detail.skill0", num12, num15 - 12 - 8 - 35, num11, 70, 2);
		_infoRows[3] = MakeInfoRow(gameObject, "detail.skill1", num12, num15 - 12 - 8 - 35 - 76, num11, 70, 3);
		int num16 = num15 - 12 - 8 - 35 - 76 - 35 - 8;
		int num17 = -num + 14;
		Box(gameObject, num12, (num16 + num17) / 2, num11, num16 - num17, CardBg, 2);
		_dDesc = Label(gameObject, string.Empty, num13 + 12, num16 - 6, 19, TextLight, num11 - 24, UIWidget.Pivot.TopLeft, 4);
		_dDesc.height = Mathf.Max(30, num16 - num17 - 12);
	}

	private void SetDetailModel(int species)
	{
		bool flag = false;
		if (_dModel != null)
		{
			try
			{
				if (species == _dModelSpecies && _dModel.ModelObject != null)
				{
					flag = true;
				}
				else
				{
					Animal animal = SingletonDict<int, Animal>.Get(species);
					if (animal != null && !string.IsNullOrEmpty(animal.PrefabPath))
					{
						_dModelSpecies = species;
						_dModel.SetPlainModel(animal.PrefabPath, new UIModelViewer.Arguments
						{
							CameraAngle = 20f,
							Rotation = 140f,
							Loaded = _dModel.DefaultAnimalPlay("idle", "stand")
						});
						flag = true;
					}
				}
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		_dPortrait.gameObject.SetActive(!flag);
		_dPortraitAlt.gameObject.SetActive(!flag && _dPortraitAlt.gameObject.activeSelf);
		if (!flag)
		{
			SetPortrait(_dPortrait, _dPortraitAlt, species);
		}
	}

	private InfoRow MakeInfoRow(GameObject g, string key, int x, int y, int w, int h, int index)
	{
		InfoRow infoRow = new InfoRow();
		infoRow.Button = MakeButton(g, key, string.Empty, x, y, w, h, delegate
		{
			_detailSel = index;
			Refresh();
		});
		infoRow.Button.Label.gameObject.SetActive(value: false);
		int num = ((h >= 66) ? 15 : 12);
		infoRow.Title = Label(infoRow.Button.Go, string.Empty, -w / 2 + 12, num, 21, TextLight, w - 24, UIWidget.Pivot.Left);
		infoRow.Desc = Label(infoRow.Button.Go, string.Empty, -w / 2 + 12, -num, 17, TextDim, w - 24, UIWidget.Pivot.Left);
		return infoRow;
	}

	private void BuildPick()
	{
		GameObject parent = base.gameObject;
		Catcher(PickTap);
		int num = Mathf.RoundToInt(((_root != null) ? ((float)_root.activeHeight) : 720f) * 0.5f) - 86;
		Box(parent, 0, num, 720, 104, HudBg, 1, UIWidget.Pivot.Top);
		_pHint = Label(parent, string.Empty, 0, num - 26, 22, Gold, 700, UIWidget.Pivot.Center);
		MakeButton(parent, "pick.cancel", T("pick_cancel"), 0, num - 74, 200, 46, ClosePick, 20);
	}

	private static bool GroundAt(Vector2 screen, out Vector3 world, out Vector2 tile)
	{
		world = Vector3.zero;
		tile = Vector2.zero;
		if (!Durango.Utils.Singleton<MainCamera>.HasInstance())
		{
			return false;
		}
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		float height = ((localPlayer != null) ? localPlayer.transform.position.y : 0f);
		world = MainCamera.ScreenPosToWorldPos(new Vector3(screen.x, screen.y, 0f), height);
		tile = Util.ClientPositionToTilePosition(world);
		return true;
	}

	private static bool InReach(Vector2 tile)
	{
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		if (localPlayer == null)
		{
			return true;
		}
		Vector2 vector = Util.ClientPositionToTilePosition(localPlayer.transform.position);
		return (tile - vector).magnitude <= CommunityDodoFight.PickMaxTiles;
	}

	private void PickTap()
	{
		if (GroundAt(UICamera.lastEventPosition, out var world, out var tile))
		{
			_pSpot = world;
			_pHaveSpot = true;
			if (!InReach(tile))
			{
				CommunityDodoFightFx.ShowPick(world, valid: false);
				_pHint.text = "[ff8a80]" + T("pick_far", Mathf.RoundToInt(CommunityDodoFight.PickMaxTiles)) + "[-]";
			}
			else
			{
				CommunityDodoFightFx.ShowPick(world, valid: true);
				CommunityDodoFight.PracticeAt(tile.x, tile.y);
				_pSentAt = Time.realtimeSinceStartup;
				_pHint.text = T("pick_sent");
			}
		}
	}

	private void UpdatePick()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			ClosePick();
			return;
		}
		bool flag = Time.realtimeSinceStartup - _pSentAt < 4f;
		if (!flag && _pHint.text == T("pick_sent"))
		{
			_pHint.text = T("pick_hint", Mathf.RoundToInt(CommunityDodoFight.PickMaxTiles));
		}
		if (!flag)
		{
			if (!Application.isMobilePlatform && GroundAt(Input.mousePosition, out var world, out var tile))
			{
				CommunityDodoFightFx.ShowPick(world, InReach(tile));
			}
			else if (_pHaveSpot)
			{
				CommunityDodoFightFx.ShowPick(_pSpot, InReach(Util.ClientPositionToTilePosition(_pSpot)));
			}
		}
	}

	private void BuildHud()
	{
		GameObject parent = base.gameObject;
		_hTop = parent.AddChild();
		_hTop.name = "top";
		_hTopBg = Box(_hTop, 0, 0, 560, 40, HudBg, 1, UIWidget.Pivot.Top);
		_hHeader = Label(_hTop, string.Empty, 0, -20, 22, TextLight, 540, UIWidget.Pivot.Center);
		_hBanner = Label(_hTop, string.Empty, 0, -70, 28, Gold, 760, UIWidget.Pivot.Center);
		Outline(_hBanner);
		_hBanner.gameObject.SetActive(value: false);
		_hBet = _hTop.AddChild();
		_hBet.name = "bet";
		_hBet.transform.localPosition = new Vector3(0f, -44f, 0f);
		_hBetBg = Box(_hBet, 0, 0, 560, 108, HudBg, 1, UIWidget.Pivot.Top);
		_hBetHead = Label(_hBet, string.Empty, 0, -18, 20, Gold, 540, UIWidget.Pivot.Center);
		for (int i = 0; i < 2; i++)
		{
			int side = i;
			_hBetBtn[i] = MakeButton(_hBet, "hud.bet" + i, string.Empty, (i == 0) ? (-136) : 136, -68, 260, 54, delegate
			{
				AskBet(side);
			});
		}
		_hBetMine = Label(_hBet, string.Empty, 0, -18, 20, TextLight, 540, UIWidget.Pivot.Center);
		_hCheer = parent.AddChild();
		_hCheer.name = "cheer";
		for (int num = 0; num < 2; num++)
		{
			int slot = num;
			_hCheerBtn[num] = MakeButton(_hCheer, "hud.cheer" + num, string.Empty, (num == 0) ? (-323) : (-105), 27, 210, 54, delegate
			{
				CommunityDodoFight.Cheer(slot);
			}, 20);
		}
		_hCheerHint = Label(_hCheer, string.Empty, -214, 72, 18, TextLight, 428, UIWidget.Pivot.Center);
		Outline(_hCheerHint);
		PlaceHud();
	}

	private static void Outline(UILabel label)
	{
		label.effectStyle = UILabel.Effect.Outline;
		label.effectColor = new Color(0f, 0f, 0f, 0.85f);
	}

	private void PlaceHud()
	{
		if (_root == null)
		{
			_root = NGUITools.FindInParents<UIRoot>(base.gameObject);
		}
		if (base.transform.localPosition.sqrMagnitude > 0.25f)
		{
			base.transform.localPosition = Vector3.zero;
		}
		float num = ((_root != null) ? ((float)_root.activeHeight) : 720f);
		float num2 = ((Screen.height > 0) ? (num * (float)Screen.width / (float)Screen.height) : 1280f);
		_hTop.transform.localPosition = new Vector3(0f, Mathf.Round(num * 0.5f - 86f), 0f);
		_hCheer.transform.localPosition = new Vector3(Mathf.Round(num2 * 0.5f - 24f), Mathf.Round(0f - num * 0.5f + 236f), 0f);
	}

	private HeadBar MakeHeadBar(int side)
	{
		HeadBar headBar = new HeadBar();
		headBar.Side = side;
		headBar.Go = base.gameObject.AddChild();
		headBar.Go.name = "headbar";
		Box(headBar.Go, -62, 0, 124, 16, HeadBarBack, 1, UIWidget.Pivot.Left);
		headBar.Fill = Box(headBar.Go, -60, 0, 120, 12, HeadBarColor[side], 2, UIWidget.Pivot.Left);
		headBar.Mine = Label(headBar.Go, string.Empty, 0, 24, 20, Gold, 320, UIWidget.Pivot.Center);
		Outline(headBar.Mine);
		headBar.Mine.gameObject.SetActive(value: false);
		headBar.Go.SetActive(value: false);
		return headBar;
	}

	private static string MineTag(CommunityDodoFight.Match match, int i)
	{
		bool flag = match.Bet.HasMine && match.Bet.MineSide == i && match.Bet.MineAmount > 0;
		string text = match.Bet.MineAmount.ToString("N0", CultureInfo.InvariantCulture);
		if (match.IsOwner && match.MySide == i)
		{
			if (!flag)
			{
				return T("mine_tag_owner");
			}
			return T("mine_tag_owner_bet", text);
		}
		if (!flag)
		{
			return null;
		}
		return T("mine_tag_bet", text);
	}

	private void Float(string entity, string text, string icon = null)
	{
		if (string.IsNullOrEmpty(entity))
		{
			return;
		}
		Floater floater = null;
		foreach (Floater floater2 in _floaters)
		{
			if (!floater2.Label.gameObject.activeSelf)
			{
				floater = floater2;
				break;
			}
		}
		if (floater == null)
		{
			floater = new Floater();
			floater.Label = Label(base.gameObject, string.Empty, 0, 0, 24, TextLight, 420, UIWidget.Pivot.Center);
			Outline(floater.Label);
			_floaters.Add(floater);
		}
		int num = 0;
		foreach (Floater floater3 in _floaters)
		{
			if (floater3 != floater && floater3.Entity == entity && floater3.Label.gameObject.activeSelf && Time.realtimeSinceStartup - floater3.Start < 0.6f)
			{
				num++;
			}
		}
		floater.Entity = entity;
		floater.Start = Time.realtimeSinceStartup + (float)num * 0.25f;
		floater.Label.text = text;
		floater.Label.alpha = 1f;
		floater.Label.gameObject.SetActive(value: false);
		floater.Label.gameObject.SetActive(value: true);
		if (!string.IsNullOrEmpty(icon))
		{
			if (floater.Icon == null)
			{
				floater.Icon = base.gameObject.AddSprite(icon, 9);
				floater.Icon.type = UIBasicSprite.Type.Simple;
			}
			floater.Icon.spriteName = icon;
			floater.Icon.width = 58;
			floater.Icon.height = 58;
			floater.Icon.alpha = 1f;
			floater.Icon.gameObject.SetActive(value: true);
		}
		else if (floater.Icon != null)
		{
			floater.Icon.gameObject.SetActive(value: false);
		}
	}

	private void Update()
	{
		if (!CommunityDodoFight.Enabled)
		{
			base.gameObject.SetActive(value: false);
			return;
		}
		if (_kind == Kind.Pick)
		{
			UpdatePick();
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (_seen != CommunityDodoFight.Version || realtimeSinceStartup >= _refreshAt)
		{
			Refresh();
		}
	}

	private void LateUpdate()
	{
		if (_kind != Kind.Hud)
		{
			return;
		}
		try
		{
			FollowHeads();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static bool ToNgui(Vector3 world, out Vector3 ngui)
	{
		ngui = Vector3.zero;
		if (!Durango.Utils.Singleton<MainCamera>.HasInstance())
		{
			return false;
		}
		Vector3 nguiPos = MainCamera.WorldToScreenPos(world);
		if (nguiPos.z <= 0f)
		{
			return false;
		}
		ngui = MainCamera.ScreenPosToNGUIPos(nguiPos);
		return true;
	}

	private void FollowHeads()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		foreach (KeyValuePair<string, HeadBar> hBar in _hBars)
		{
			HeadBar value = hBar.Value;
			Vector3 ngui = Vector3.zero;
			Vector3 top;
			bool flag = CommunityDodoFightFx.TopOf(hBar.Key, out top) && ToNgui(top, out ngui);
			if (value.Go.activeSelf != flag)
			{
				value.Go.SetActive(flag);
			}
			if (flag)
			{
				value.Go.transform.localPosition = new Vector3(Mathf.Round(ngui.x), Mathf.Round(ngui.y + 40f), 0f);
			}
		}
		foreach (Floater floater in _floaters)
		{
			if (!floater.Label.gameObject.activeSelf)
			{
				continue;
			}
			float num = realtimeSinceStartup - floater.Start;
			if (num > 1.6f)
			{
				floater.Label.gameObject.SetActive(value: false);
				if (floater.Icon != null)
				{
					floater.Icon.gameObject.SetActive(value: false);
				}
				continue;
			}
			if (!CommunityDodoFightFx.TopOf(floater.Entity, out var top2) || !ToNgui(top2, out var ngui2))
			{
				floater.Label.alpha = 0f;
				if (floater.Icon != null)
				{
					floater.Icon.alpha = 0f;
				}
				continue;
			}
			float num2 = Mathf.Clamp01(num / 1.6f);
			if (_hBars.TryGetValue(floater.Entity, out var value2) && value2.Mine.gameObject.activeSelf)
			{
				ngui2.y += 24f;
			}
			floater.Label.transform.localPosition = new Vector3(Mathf.Round(ngui2.x), Mathf.Round(ngui2.y + 40f + 26f + num2 * 70f), 0f);
			floater.Label.alpha = ((num < 0f) ? 0f : ((num2 < 0.7f) ? 1f : (1f - (num2 - 0.7f) / 0.3f)));
			if (floater.Icon != null && floater.Icon.gameObject.activeSelf)
			{
				float num3 = ((num < 0f) ? 0.4f : ((num < 0.22f) ? Mathf.Lerp(0.4f, 1.25f, num / 0.22f) : ((num < 0.4f) ? Mathf.Lerp(1.25f, 1f, (num - 0.22f) / 0.18f) : 1f)));
				floater.Icon.transform.localScale = new Vector3(num3, num3, 1f);
				floater.Icon.transform.localPosition = new Vector3(Mathf.Round(ngui2.x), Mathf.Round(ngui2.y + 40f + 74f + num2 * 70f), 0f);
				floater.Icon.alpha = floater.Label.alpha;
			}
		}
		if (_hBanner.gameObject.activeSelf != realtimeSinceStartup < _hBannerUntil)
		{
			_hBanner.gameObject.SetActive(realtimeSinceStartup < _hBannerUntil);
		}
		if (_hBanner.gameObject.activeSelf)
		{
			float num4 = -70f;
			if (_hBet.activeSelf)
			{
				num4 = -44 - _hBetBg.height - 30;
			}
			if (Mathf.Abs(_hBanner.transform.localPosition.y - num4) > 0.5f)
			{
				_hBanner.transform.localPosition = new Vector3(0f, num4, 0f);
			}
		}
	}

	private void Refresh()
	{
		_seen = CommunityDodoFight.Version;
		_refreshAt = Time.realtimeSinceStartup + ((_kind == Kind.Hud) ? 0.2f : 1f);
		try
		{
			switch (_kind)
			{
			case Kind.Lobby:
				RefreshLobby();
				break;
			case Kind.Detail:
				RefreshDetail();
				break;
			case Kind.Pick:
				_pHint.text = T("pick_hint", Mathf.RoundToInt(CommunityDodoFight.PickMaxTiles));
				break;
			default:
				RefreshHud();
				break;
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private void RefreshLobby()
	{
		CommunityDodoFight.Lobby currentLobby = CommunityDodoFight.CurrentLobby;
		if (currentLobby == null)
		{
			base.gameObject.SetActive(value: false);
			return;
		}
		CommunityDodoFight.Card me = currentLobby.Me;
		bool hasDodo = currentLobby.HasDodo;
		SetPortrait(_portrait, _portraitAlt, (hasDodo && me != null) ? me.Species : 0);
		_portraitAlt.text = (hasDodo ? "?" : "+");
		_cardName.gameObject.SetActive(hasDodo);
		_cardInfo.gameObject.SetActive(hasDodo);
		_cardSkills.gameObject.SetActive(hasDodo);
		_noDodo.gameObject.SetActive(!hasDodo);
		if (hasDodo)
		{
			_cardName.text = ((me != null) ? Plain(CardTitle(me)) : "?");
			_cardInfo.text = ((me != null) ? (T("power", me.Power) + " · " + T("record", me.Wins, me.Losses)) : string.Empty);
			_cardSkills.text = ((me != null) ? SkillsLine(me, colored: true) : string.Empty);
		}
		_bDetail.Go.SetActive(hasDodo);
		_bReroll.Go.SetActive(hasDodo);
		_bClaim.Go.SetActive(!hasDodo);
		long rerollPrice = CommunityDodoFight.RerollPrice;
		_bReroll.Label.text = ((rerollPrice > 0) ? T("reroll", Num(rerollPrice)) : T("reroll_free"));
		Paint(_bDetail, hasDodo, BtnOff);
		Paint(_bReroll, hasDodo, BtnOff);
		Paint(_bClaim, !hasDodo, BtnGold);
		_bQueue.Label.text = T(currentLobby.InQueue ? "queue_leave" : "queue_join", currentLobby.Queue);
		Paint(_bQueue, hasDodo, currentLobby.InQueue ? BtnRed : BtnGold);
		Paint(_bPractice, hasDodo && currentLobby.PracticeOk, BtnOff);
		bool flag = currentLobby.ArenaState == "countdown" || currentLobby.ArenaState == "fighting";
		Paint(_bWatch, flag, BtnOff);
		string text = Plain(currentLobby.ArenaSides[0]);
		string text2 = Plain(currentLobby.ArenaSides[1]);
		_arena.text = ((currentLobby.ArenaState == "countdown") ? T("arena_countdown", text, text2) : ((currentLobby.ArenaState == "fighting") ? T("arena_fighting", text, text2) : T("arena_idle")));
		List<CommunityDodoFight.PlayerEntry> players = currentLobby.Players;
		int num = Mathf.Max(1, (players.Count + 6 - 1) / 6);
		_page = Mathf.Clamp(_page, 0, num - 1);
		_playersHead.text = ((players.Count > 0) ? T("players", players.Count) : T("no_players"));
		_pageLabel.text = _page + 1 + "/" + num;
		_pageLabel.gameObject.SetActive(num > 1);
		_bPrev.Go.SetActive(num > 1);
		_bNext.Go.SetActive(num > 1);
		Paint(_bPrev, _page > 0, BtnOff);
		Paint(_bNext, _page < num - 1, BtnOff);
		for (int i = 0; i < _rows.Count; i++)
		{
			PlayerRow playerRow = _rows[i];
			int num2 = _page * 6 + i;
			if (num2 >= players.Count)
			{
				playerRow.PlayerId = null;
				playerRow.Go.SetActive(value: false);
				continue;
			}
			CommunityDodoFight.PlayerEntry playerEntry = players[num2];
			playerRow.Go.SetActive(value: true);
			playerRow.PlayerId = playerEntry.Id;
			playerRow.PlayerName = Plain(playerEntry.Name);
			playerRow.Name.text = playerRow.PlayerName;
			playerRow.Info.text = T("power", playerEntry.Power) + (playerEntry.Busy ? (" · [ff8f6a]" + T("busy") + "[-]") : string.Empty);
			Paint(playerRow.View, enabled: true, BtnOff);
			Paint(playerRow.Challenge, hasDodo && !playerEntry.Busy, BtnGold);
		}
	}

	private void RefreshDetail()
	{
		CommunityDodoFight.Card detailCard = CommunityDodoFight.DetailCard;
		if (detailCard == null)
		{
			base.gameObject.SetActive(value: false);
			return;
		}
		SetDetailModel(detailCard.Species);
		_dName.text = Plain(CardName(detailCard));
		string text = SpeciesName(detailCard.Species);
		if (detailCard.OwnerName.Length > 0)
		{
			text = ((text.Length > 0) ? (text + " · ") : string.Empty) + T("owner", Plain(detailCard.OwnerName));
		}
		_dSpecies.text = text;
		_dRecord.text = T("power", detailCard.Power) + " · " + T("record", detailCard.Wins, detailCard.Losses);
		int[] array = new int[4] { detailCard.Str, detailCard.Agi, detailCard.Dex, detailCard.Luk };
		float num = Mathf.Max(1, CommunityDodoFight.StatMax);
		for (int i = 0; i < 4; i++)
		{
			SetBar(_statFill[i], 170, (float)array[i] / num);
			_statValue[i].text = array[i].ToString(CultureInfo.InvariantCulture);
		}
		int num2 = Mathf.Clamp(detailCard.AtkPref, 0, 100);
		_prefLabel.text = T("pref", num2, 100 - num2);
		SetBar(_prefAtk, 300, (float)num2 / 100f);
		SetBar(_prefDef, 300, (float)(100 - num2) / 100f);
		CommunityDodoFight.Skill[] array2 = new CommunityDodoFight.Skill[4]
		{
			BasicAt(detailCard, 0),
			BasicAt(detailCard, 1),
			(detailCard.Skills.Count > 0) ? detailCard.Skills[0] : null,
			(detailCard.Skills.Count > 1) ? detailCard.Skills[1] : null
		};
		if (_detailSel < 0 || _detailSel > 3 || array2[_detailSel] == null)
		{
			_detailSel = ((array2[2] != null) ? 2 : 0);
		}
		for (int j = 0; j < 4; j++)
		{
			InfoRow infoRow = _infoRows[j];
			CommunityDodoFight.Skill skill = array2[j];
			infoRow.Button.Go.SetActive(skill != null);
			if (skill != null)
			{
				infoRow.Title.text = ((j < 2) ? (SkillName(skill.Id) + " · " + LineName(skill.Line)) : (TierTag(skill.Tier, colored: true) + SkillName(skill.Id) + " · " + LineName(skill.Line) + " · " + T("rate", Rate(skill.Rate)) + ((skill.Cooldown > 0f) ? (" · " + T("cd", Mathf.RoundToInt(skill.Cooldown))) : string.Empty)));
				infoRow.Desc.text = SkillDesc(skill.Id);
				infoRow.Button.Bg.color = ((j == _detailSel) ? BtnSel : BtnOff);
			}
		}
		_dDesc.text = FullDesc(array2[_detailSel], _detailSel >= 2);
	}

	private void RefreshHud()
	{
		CommunityDodoFight.Match currentMatch = CommunityDodoFight.CurrentMatch;
		if (currentMatch == null)
		{
			base.gameObject.SetActive(value: false);
			return;
		}
		PlaceHud();
		string text = "[" + SideHex[0] + "]" + currentMatch.RoundWins[0] + "[-] : [" + SideHex[1] + "]" + currentMatch.RoundWins[1] + "[-]   ";
		if (currentMatch.Mode == "practice")
		{
			text = text + T("mode_practice") + " · ";
		}
		else if (currentMatch.Mode == "house")
		{
			text = text + T("mode_house") + " · ";
		}
		int num = Mathf.CeilToInt(currentMatch.TimeLeftNow);
		text = currentMatch.Phase switch
		{
			"countdown" => text + T("ph_countdown", num),
			"running" => text + T("ph_running", currentMatch.Round, currentMatch.MaxRounds, num),
			"break" => text + T("ph_break", num),
			"ended" => text + T("ph_ended"),
			"open" => text + T("ph_open"),
			_ => text + currentMatch.Phase,
		};
		_hHeader.text = text;
		foreach (HeadBar value2 in _hBars.Values)
		{
			value2.Seen = false;
		}
		foreach (CommunityDodoFight.Match match in CommunityDodoFight.Matches)
		{
			for (int i = 0; i < 2; i++)
			{
				CommunityDodoFight.Side side = match.Sides[i];
				if (string.IsNullOrEmpty(side.Entity))
				{
					continue;
				}
				int num2 = ((side.Aura == "blue") ? 1 : ((!(side.Aura == "red")) ? i : 0));
				if (!_hBars.TryGetValue(side.Entity, out var value) || value.Side != num2)
				{
					if (value != null)
					{
						UnityEngine.Object.Destroy(value.Go);
					}
					value = MakeHeadBar(num2);
					_hBars[side.Entity] = value;
				}
				value.Seen = true;
				value.Ratio = ((side.HpMax > 0f) ? Mathf.Clamp01(side.Hp / side.HpMax) : 0f);
				SetBar(value.Fill, 120, value.Ratio);
				string text2 = MineTag(match, i);
				if (value.Mine.gameObject.activeSelf != (text2 != null))
				{
					value.Mine.gameObject.SetActive(text2 != null);
				}
				if (text2 != null && value.Mine.text != text2)
				{
					value.Mine.text = text2;
				}
			}
		}
		List<string> list = null;
		foreach (KeyValuePair<string, HeadBar> hBar in _hBars)
		{
			if (!hBar.Value.Seen)
			{
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(hBar.Key);
			}
		}
		if (list != null)
		{
			foreach (string item in list)
			{
				UnityEngine.Object.Destroy(_hBars[item].Go);
				_hBars.Remove(item);
			}
		}
		CommunityDodoFight.Side side2 = (currentMatch.IsOwner ? currentMatch.Sides[currentMatch.MySide] : null);
		bool flag = side2 != null && side2.Card != null && side2.Card.Skills.Count > 0 && currentMatch.Phase != "ended";
		bool cheerInHoneycomb = CommunityDodoFight.CheerInHoneycomb;
		_hCheer.SetActive(flag);
		for (int j = 0; j < 2; j++)
		{
			if (cheerInHoneycomb)
			{
				_hCheerBtn[j].Go.SetActive(value: false);
			}
		}
		if (flag)
		{
			for (int k = 0; k < 2; k++)
			{
				Btn btn = _hCheerBtn[k];
				bool flag2 = k < side2.Card.Skills.Count && !cheerInHoneycomb;
				btn.Go.SetActive(flag2);
				if (flag2)
				{
					CommunityDodoFight.Skill skill = side2.Card.Skills[k];
					bool flag3 = side2.Armed[k];
					int num3 = Mathf.CeilToInt(side2.ReadyIn(k));
					btn.Label.text = TierTag(skill.Tier, !flag3) + SkillName(skill.Id) + (flag3 ? ("\n" + T("armed")) : string.Empty) + ((num3 > 0) ? ("\n[b0b0b0]" + T("cd_left", num3) + "[-]") : string.Empty);
					if (flag3)
					{
						btn.Enabled = false;
						btn.Bg.color = BtnGreen;
						btn.Label.color = TextDark;
					}
					else
					{
						Paint(btn, side2.CheersLeft > 0, BtnGold);
					}
				}
			}
			_hCheerHint.text = T("cheer_short", side2.CheersLeft, CommunityDodoFight.CheersPerRound, CommunityDodoFight.CheerPct);
		}
		_hBet.SetActive(value: false);
	}

	public static void AskReroll()
	{
		long rerollPrice = CommunityDodoFight.RerollPrice;
		if (rerollPrice <= 0)
		{
			CommunityDodoFight.Reroll();
			return;
		}
		MessageBox messageBox = UIManager.MessageBox;
		if (messageBox == null)
		{
			return;
		}
		long num = Balance();
		messageBox.ShowPayConfirm(rerollPrice, Currency.TStone, T("reroll_confirm"), (num >= 0) ? T("balance", Num(num)) : null, delegate(bool ok)
		{
			if (ok)
			{
				CommunityDodoFight.Reroll();
			}
		}, T("reroll_btn"));
	}

	private static void ToggleQueue()
	{
		CommunityDodoFight.Lobby currentLobby = CommunityDodoFight.CurrentLobby;
		if (currentLobby != null)
		{
			CommunityDodoFight.SetQueue(!currentLobby.InQueue);
		}
	}

	private static void Watch()
	{
		if (CommunityDodoFight.CurrentMatch != null)
		{
			CloseLobby(tellServer: true);
			MatchChanged();
		}
		else
		{
			UIManager.SystemMsg(T("watch_far"));
		}
	}

	private static void ChallengeRow(PlayerRow row)
	{
		if (row != null && !string.IsNullOrEmpty(row.PlayerId))
		{
			CommunityDodoFight.Challenge(row.PlayerId);
			UIManager.SystemMsg(T("challenge_sent", row.PlayerName ?? row.PlayerId), 2f);
		}
	}

	private static void AskBet(int side)
	{
		CommunityDodoFight.Match currentMatch = CommunityDodoFight.CurrentMatch;
		if (currentMatch != null && currentMatch.Bet.Open && currentMatch.Bet.Allowed && !currentMatch.Bet.HasMine)
		{
			AskBetFor(currentMatch.Id, side, SideLabel(currentMatch, side), currentMatch.Bet.Odds[Mathf.Clamp(side, 0, 1)]);
		}
	}

	public static void AskBetFor(string matchId, int side, string sideName, float odds)
	{
		if (string.IsNullOrEmpty(matchId))
		{
			return;
		}
		long num = Balance();
		if (num <= 0)
		{
			UIManager.SystemMsg(T("no_tstone"));
			return;
		}
		long betMin = CommunityDodoFight.BetMin;
		if (betMin > 0 && num < betMin)
		{
			UIManager.SystemMsg(T("bet_min_need", Num(betMin)));
		}
		else
		{
			if (UIManager.Popup == null)
			{
				return;
			}
			NumberInputPopup numberInputPopup = UIManager.Popup.Tooltip<NumberInputPopup>();
			if (!(numberInputPopup == null))
			{
				numberInputPopup.Show((betMin > 0) ? betMin : Math.Min(100L, num), Currency.TStone, T("bet_amount", sideName, Odds(odds)), delegate(long amount)
				{
					ConfirmBet(matchId, side, sideName, odds, amount);
				}, num);
			}
		}
	}

	public static string SideLabel(CommunityDodoFight.Match match, int side)
	{
		return Plain(SideName(match, side));
	}

	public static void PickPlayer(JArray players)
	{
		List<JObject> rows = new List<JObject>();
		if (players != null)
		{
			foreach (JToken item in (IEnumerable<JToken>)players)
			{
				if (item is JObject jObject && CommunityDodoFight.Str(jObject, "id").Length > 0)
				{
					rows.Add(jObject);
				}
			}
		}
		if (rows.Count == 0)
		{
			UIManager.SystemMsg(T("pick_none"));
		}
		else
		{
			if (UIManager.Popup == null)
			{
				return;
			}
			GenericSelector genericSelector = UIManager.Popup.Tooltip<GenericSelector>();
			if (genericSelector == null)
			{
				return;
			}
			genericSelector.ResetArguments();
			genericSelector.SetTitle(T("pick_title"));
			foreach (JObject item2 in rows)
			{
				bool valueOrDefault = (bool?)item2["busy"] == true;
				string arg = T("record", (int)CommunityDodoFight.Num(item2, "wins", 0.0), (int)CommunityDodoFight.Num(item2, "losses", 0.0)) + (valueOrDefault ? (" · " + T("busy")) : string.Empty);
				genericSelector.AddItem(string.Format("{0}\n[size=4] [/size]\n[size=20][777163]{1}", Plain(CommunityDodoFight.Str(item2, "name")), arg));
			}
			genericSelector.SetSelected(delegate(int index)
			{
				if (index >= 0 && index < rows.Count)
				{
					AskPlayerAction(rows[index]);
				}
			});
			genericSelector.Show();
		}
	}

	private static void AskPlayerAction(JObject row)
	{
		MessageBox messageBox = UIManager.MessageBox;
		if (messageBox == null)
		{
			return;
		}
		string id = CommunityDodoFight.Str(row, "id");
		string name = Plain(CommunityDodoFight.Str(row, "name"));
		bool valueOrDefault = (bool?)row["busy"] == true;
		messageBox.Show(T("pick_what", name), valueOrDefault ? T("busy") : null, delegate(int pick)
		{
			switch (pick)
			{
			case 0:
				CommunityDodoFight.RequestDetail(id);
				break;
			case 1:
				CommunityDodoFight.Challenge(id);
				UIManager.SystemMsg(T("challenge_sent", name), 2f);
				break;
			}
		}, new MessageBox.Button(T("pick_view")), new MessageBox.Button(T("pick_challenge"), PresetButton.Style.Solid, null, valueOrDefault), new MessageBox.Button(T("pick_cancel")));
	}

	private static void ConfirmBet(string matchId, int side, string sideName, float odds, long amount)
	{
		if (amount <= 0)
		{
			return;
		}
		long betMin = CommunityDodoFight.BetMin;
		if (betMin > 0 && amount < betMin)
		{
			UIManager.SystemMsg(T("bet_min", Num(betMin)));
			return;
		}
		MessageBox messageBox = UIManager.MessageBox;
		if (messageBox == null)
		{
			return;
		}
		long num = (long)Math.Floor((double)amount * Math.Round(odds, 2));
		long num2 = ((CommunityDodoFight.WinTax > 0.0) ? ((long)Math.Floor((double)num * CommunityDodoFight.WinTax)) : 0);
		string subText = ((num2 > 0) ? T("bet_sub_tax", Num(num - num2), (CommunityDodoFight.WinTax * 100.0).ToString("0.##", CultureInfo.InvariantCulture), Num(num2)) : T("bet_sub", Num(num)));
		messageBox.ShowPayConfirm(amount, Currency.TStone, T("bet_confirm", sideName, Odds(odds)), subText, delegate(bool ok)
		{
			if (ok)
			{
				CommunityDodoFight.SendBet(matchId, side, amount);
			}
		}, T("bet_btn"));
	}

	private static long Balance()
	{
		try
		{
			return InventorySystem.Wallet.GetBalance(Currency.TStone);
		}
		catch (Exception)
		{
			return -1L;
		}
	}

	private static bool Press(Btn b)
	{
		if (b == null || b.Go == null || !b.Go.activeInHierarchy || !b.Enabled || b.OnClick == null)
		{
			return false;
		}
		try
		{
			b.OnClick();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		return true;
	}

	private static void Paint(Btn b, bool enabled, Color bg)
	{
		if (b != null)
		{
			b.Enabled = enabled;
			b.Bg.color = (enabled ? bg : BtnDisabled);
			b.Label.color = ((!enabled) ? TextDisabled : ((bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f > 0.55f) ? TextDark : TextLight));
		}
	}

	private static void SetBar(UISprite bar, int full, float ratio)
	{
		int num = Mathf.RoundToInt((float)full * Mathf.Clamp01(ratio));
		bool flag = num >= 2;
		if (bar.gameObject.activeSelf != flag)
		{
			bar.gameObject.SetActive(flag);
		}
		if (flag)
		{
			bar.width = num;
		}
	}

	private static void SetPortrait(UISprite sprite, UILabel alt, int species)
	{
		string text = null;
		if (species > 0)
		{
			try
			{
				text = AnimalYaml.GetPortrait(species);
			}
			catch (Exception)
			{
				text = null;
			}
		}
		bool flag = false;
		if (!string.IsNullOrEmpty(text))
		{
			if (sprite.spriteName != text)
			{
				sprite.spriteName = text;
				sprite.type = UIBasicSprite.Type.Simple;
				sprite.color = Color.white;
			}
			flag = sprite.isValid;
		}
		sprite.gameObject.SetActive(flag);
		alt.gameObject.SetActive(!flag);
	}

	private void Catcher(Action onClick)
	{
		UIWidget uIWidget = base.gameObject.AddWidget<UIWidget>(0);
		uIWidget.width = 4000;
		uIWidget.height = 4000;
		NGUITools.AddWidgetCollider(uIWidget.gameObject);
		UIEventListener.Get(uIWidget.gameObject).onClick = delegate
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
	}

	private UISprite Box(GameObject parent, int x, int y, int w, int h, Color color, int depth, UIWidget.Pivot pivot = UIWidget.Pivot.Center, bool blocking = false)
	{
		UISprite uISprite = parent.AddSprite("bg_white", depth);
		uISprite.type = UIBasicSprite.Type.Simple;
		uISprite.pivot = pivot;
		uISprite.width = Mathf.Max(2, w);
		uISprite.height = Mathf.Max(2, h);
		uISprite.color = color;
		uISprite.transform.localPosition = new Vector3(x, y, 0f);
		if (blocking)
		{
			NGUITools.AddWidgetCollider(uISprite.gameObject);
		}
		return uISprite;
	}

	private UISprite PortraitSprite(GameObject parent, int x, int y, int w, int h, int depth)
	{
		UISprite uISprite = parent.AddSprite("bg_white", depth);
		uISprite.type = UIBasicSprite.Type.Simple;
		uISprite.width = w;
		uISprite.height = h;
		uISprite.transform.localPosition = new Vector3(x, y, 0f);
		uISprite.gameObject.SetActive(value: false);
		return uISprite;
	}

	private UILabel Label(GameObject parent, string text, int x, int y, int size, Color color, int width, UIWidget.Pivot pivot, int lines = 1)
	{
		UILabel uILabel = parent.AddWidget<UILabel>(6);
		ApplyFont(uILabel);
		uILabel.fontSize = size;
		uILabel.color = color;
		uILabel.pivot = pivot;
		switch (pivot)
		{
		case UIWidget.Pivot.TopLeft:
		case UIWidget.Pivot.Left:
		case UIWidget.Pivot.BottomLeft:
			uILabel.alignment = NGUIText.Alignment.Left;
			break;
		case UIWidget.Pivot.TopRight:
		case UIWidget.Pivot.Right:
		case UIWidget.Pivot.BottomRight:
			uILabel.alignment = NGUIText.Alignment.Right;
			break;
		default:
			uILabel.alignment = NGUIText.Alignment.Center;
			break;
		}
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		uILabel.width = width;
		uILabel.height = lines * (size + 6) + 4;
		uILabel.text = text;
		uILabel.transform.localPosition = new Vector3(x, y, 0f);
		return uILabel;
	}

	private Btn MakeButton(GameObject parent, string key, string text, int x, int y, int w, int h, Action onClick, int fontSize = 22)
	{
		Btn btn = new Btn();
		btn.Key = key;
		btn.Bg = parent.AddSprite("bg_white", 3);
		btn.Bg.type = UIBasicSprite.Type.Simple;
		btn.Bg.width = w;
		btn.Bg.height = h;
		btn.Bg.color = BtnOff;
		btn.Bg.transform.localPosition = new Vector3(x, y, 0f);
		btn.Go = btn.Bg.gameObject;
		btn.Go.name = key;
		btn.Label = btn.Go.AddWidget<UILabel>(7);
		ApplyFont(btn.Label);
		btn.Label.fontSize = fontSize;
		btn.Label.color = TextLight;
		btn.Label.alignment = NGUIText.Alignment.Center;
		btn.Label.overflowMethod = UILabel.Overflow.ShrinkContent;
		btn.Label.width = w - 10;
		btn.Label.height = h - 6;
		btn.Label.text = text;
		btn.OnClick = onClick;
		NGUITools.AddWidgetCollider(btn.Go);
		Btn captured = btn;
		UIEventListener.Get(btn.Go).onClick = delegate
		{
			Press(captured);
		};
		_buttons[key] = btn;
		return btn;
	}

	private void ApplyFont(UILabel label)
	{
		if (_style != null)
		{
			if (_style.bitmapFont != null)
			{
				label.bitmapFont = _style.bitmapFont;
			}
			else
			{
				label.trueTypeFont = _style.trueTypeFont;
			}
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
		componentsInChildren = UnityEngine.Object.FindObjectsByType<UILabel>();
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

	private static string Plain(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return string.Empty;
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

	private static string SpeciesName(int species)
	{
		if (species <= 0)
		{
			return string.Empty;
		}
		try
		{
			return AnimalYaml.GetName(species) ?? string.Empty;
		}
		catch (Exception)
		{
			return species.ToString(CultureInfo.InvariantCulture);
		}
	}

	private static string CardName(CommunityDodoFight.Card card)
	{
		if (card == null)
		{
			return string.Empty;
		}
		if (card.Name.Length <= 0)
		{
			return SpeciesName(card.Species);
		}
		return card.Name;
	}

	private static string CardTitle(CommunityDodoFight.Card card)
	{
		string text = CardName(card);
		string text2 = SpeciesName(card.Species);
		if (text2.Length > 0 && text2 != text)
		{
			return text + " · " + text2;
		}
		return text;
	}

	private static string SideName(CommunityDodoFight.Match match, int side)
	{
		CommunityDodoFight.Side side2 = match.Sides[Mathf.Clamp(side, 0, 1)];
		if (side2.Name.Length > 0)
		{
			return side2.Name;
		}
		if (side2.Card != null)
		{
			return CardName(side2.Card);
		}
		if (side != 0)
		{
			return "B";
		}
		return "A";
	}

	private static string SkillsLine(CommunityDodoFight.Card card, bool colored)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (CommunityDodoFight.Skill skill in card.Skills)
		{
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(" · ");
			}
			stringBuilder.Append(TierTag(skill.Tier, colored)).Append(SkillName(skill.Id));
		}
		return stringBuilder.ToString();
	}

	private static CommunityDodoFight.Skill BasicAt(CommunityDodoFight.Card card, int index)
	{
		if (index < card.Basic.Count)
		{
			return card.Basic[index];
		}
		return new CommunityDodoFight.Skill
		{
			Id = ((index == 0) ? "strike" : "brace"),
			Line = ((index == 0) ? "attack" : "defend")
		};
	}

	private static string TierTag(string tier, bool colored)
	{
		if (string.IsNullOrEmpty(tier))
		{
			return string.Empty;
		}
		string text = Plain(tier).ToUpperInvariant();
		if (!colored)
		{
			return "[" + text + "] ";
		}
		string text2 = text switch
		{
			"S" => "ffc833",
			"A" => "d07bff",
			"B" => "5ab4ff",
			_ => "9ad89a",
		};
		return "[" + text2 + "][" + text + "][-] ";
	}

	private static string EventHex(CommunityDodoFight.EventLine e)
	{
		switch (e.Kind)
		{
		case "round":
		case "end":
			return "ffd25a";
		case "bet":
			return "8fe38f";
		case "cheer":
			return "ff9ad6";
		default:
			if (e.Side == 0)
			{
				return "ff8f80";
			}
			if (e.Side == 1)
			{
				return "8fc0ff";
			}
			return "e6e6e6";
		}
	}

	private static string LineName(string line)
	{
		if (line == "attack")
		{
			return T("line_attack");
		}
		if (line == "defend")
		{
			return T("line_defend");
		}
		return line ?? string.Empty;
	}

	public static string SkillName(string id)
	{
		if (!string.IsNullOrEmpty(id) && Texts.TryGetValue("sk_" + id, out var value))
		{
			return Pick(value);
		}
		return id ?? string.Empty;
	}

	private static string SkillDesc(string id)
	{
		if (!string.IsNullOrEmpty(id) && Texts.TryGetValue("skd_" + id, out var value))
		{
			return Pick(value);
		}
		return string.Empty;
	}

	private static string FullDesc(CommunityDodoFight.Skill skill, bool special)
	{
		if (skill == null)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(special ? TierTag(skill.Tier, colored: true) : string.Empty).Append("[ffd25a]").Append(SkillName(skill.Id))
			.Append("[-] · ")
			.Append(LineName(skill.Line));
		string text = SkillDesc(skill.Id);
		if (text.Length > 0)
		{
			stringBuilder.Append('\n').Append(text);
		}
		if (Texts.TryGetValue("skf_" + skill.Id, out var value))
		{
			stringBuilder.Append('\n').Append(Pick(value));
		}
		stringBuilder.Append('\n').Append(special ? T("rate_full", Rate(skill.Rate)) : T("basic_full"));
		return stringBuilder.ToString();
	}

	private static string Rate(float rate)
	{
		return rate.ToString("0.#", CultureInfo.InvariantCulture);
	}

	private static string Odds(float odds)
	{
		return odds.ToString("0.00", CultureInfo.InvariantCulture);
	}

	private static string Num(long value)
	{
		return value.ToString("N0", CultureInfo.InvariantCulture);
	}

	private static string Pick(string[] value)
	{
		return CommunityDiscordLink.Lang switch
		{
			"th" => value[0],
			"id" => value[2],
			"ko" => value[3],
			_ => value[1],
		};
	}

	public static string T(string key, params object[] args)
	{
		if (!Texts.TryGetValue(key, out var value))
		{
			return key;
		}
		string text = Pick(value);
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
