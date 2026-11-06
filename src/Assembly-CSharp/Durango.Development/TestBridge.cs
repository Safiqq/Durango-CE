using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Crafting;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.Logic.Faction;
using Durango.Logic.InputSystem;
using Durango.Logic.Item;
using Durango.Logic.Market;
using Durango.Logic.Npc;
using Durango.Logic.PlayGuide;
using Durango.Logic.Skill;
using Durango.Logic.Social;
using Durango.Network;
using Durango.System.Config;
using Durango.Terrain;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.InGame;
using Durango.UI.Popup;
using Durango.Utils;
using InteractionData;
using Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Community.ClanPillar;
using Community.Rift;
using Shared.Ability;
using Shared.Battle;
using Shared.Economy;
using Shared.Quest;
using Shared.Skill;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yaml;
using Yaml.Util;

namespace Durango.Development;

public class TestBridge : MonoBehaviour
{
	public sealed class Result
	{
		public readonly Dictionary<string, object> Data = new Dictionary<string, object>();

		public bool Ok = true;

		public Result Set(string key, object value)
		{
			Data[key] = value;
			return this;
		}

		public Result Fail(string error)
		{
			Ok = false;
			Data["error"] = error;
			return this;
		}
	}

	private delegate IEnumerator Handler(string[] args, Result result);

	private const int LogCapacity = 200;

	private static readonly List<string> _log = new List<string>();

	private readonly Dictionary<string, Handler> _handlers = new Dictionary<string, Handler>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, string> _help = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static readonly string[] GaugeKeys = new string[8] { "life", "health", "stamina", "energy", "fatigue", "groggy", "hunger", "thirst" };

	private readonly List<int> _rangeFxPreviews = new List<int>();

	public static void EnsureStarted(int port)
	{
		// SANITIZED: the original TestBridge opened a local TCP command server.
		// That is a dormant remote-control backdoor, so it is disabled here.
		// Do not re-enable this unless you fully understand the command handlers.
	}

	public static void Record(string kind, string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		lock (_log)
		{
			_log.Add(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [" + kind + "] " + text);
			if (_log.Count > 200)
			{
				_log.RemoveRange(0, _log.Count - 200);
			}
		}
	}

	private void Awake()
	{
		RegisterHandlers();
		Application.logMessageReceived += OnUnityLog;
	}

	private void OnDestroy()
	{
		Application.logMessageReceived -= OnUnityLog;
	}

	private void OnUnityLog(string condition, string stackTrace, LogType type)
	{
		if (type == LogType.Error || type == LogType.Exception)
		{
			Record(type.ToString().ToLowerInvariant(), condition);
		}
	}

	// SANITIZED: the TCP listener, accept loop, command dispatcher and reply writer
	// were removed. The command handlers below are retained only because other
	// decompiled types reference Result/Record/StripTags. They cannot be invoked
	// over the network in this source tree.

	private void Add(string name, string help, Handler handler)
	{
		_handlers[name] = handler;
		_help[name] = help;
	}

	private void RegisterHandlers()
	{
		Add("help", "รายการคำส\u0e31\u0e48ง", CmdHelp);
		Add("ping", "ตอบ pong + เวลาในเกม", CmdPing);
		Add("udp", "udp [status|fallback|hello|unblock|sim <loss%> [delayMs]|tcp|auto] — ช\u0e31\u0e49นร\u0e31บส\u0e48ง UDP ของสาย frontend (UDP-TRANSPORT 3.3.0 · CommunityUdp) · fallback = กล\u0e31บ TCP เด\u0e35\u0e4bยวน\u0e35\u0e49 · hello = ขอ Offer ใหม\u0e48ท\u0e31นท\u0e35 (UDP-CHANNELS · ย\u0e31งห\u0e48าง ≥ 10 ว\u0e34) · unblock = ล\u0e49างบล\u0e47อก 1 ชม.ของเคร\u0e37\u0e48องน\u0e35\u0e49 · sim = จำลองหาย/หน\u0e48วงเฉพาะช\u0e48อง 2 ของต\u0e31วเกมน\u0e35\u0e49 (0 0 = ป\u0e34ด) · tcp/auto = ต\u0e31\u0e49งค\u0e48า «การเช\u0e37\u0e48อมต\u0e48อ»", CmdUdp);
		Add("voice", "voice [panel|auto 0/1|level 0-2|talk 1/0|mute <id> 0/1|deaf 0/1|volume x] — สถานะ/ค\u0e38มไมค\u0e4cตามระยะ (VOICE-CHAT 3.1.9 · CommunityVoice)", CmdVoice);
		Add("dodo", "dodo [state|send <kind> <json>|fake <kind> <json|@file>|click <key>|cheer 0/1|bet <side> <amount>|hello|lobby|close|reset] — Dodo Fight v2 (3.2.0 · CommunityDodoFight) · fake = เล\u0e48น DodoDown ในเคร\u0e37\u0e48องไม\u0e48ต\u0e49องม\u0e35เซ\u0e34ร\u0e4cฟ · json ใช\u0e49 ' แทนเคร\u0e37\u0e48องหมายคำพ\u0e39ดค\u0e39\u0e48 เช\u0e48น {'enabled':true}", CmdDodo);
		Add("riftscope", "riftscope [state|fake [json]|tab <0-3>|track [i]|close|clear|coin <n>|wallet] — S02 กล\u0e49องส\u0e48องส\u0e31ตว\u0e4c (RiftScopeDown 7300027) + ยอดเหร\u0e35ยญรอยแยก · fake ไม\u0e48ใส\u0e48 json = ส\u0e31ตว\u0e4cรอบต\u0e31วจร\u0e34งในฉากเป\u0e47นผลส\u0e48อง (ไม\u0e48ส\u0e48งอะไรถ\u0e36งเซ\u0e34ร\u0e4cฟ) · json ใช\u0e49 ' แทนเคร\u0e37\u0e48องหมายคำพ\u0e39ดค\u0e39\u0e48 · coin = ใส\u0e48ยอดสก\u0e38ล 8 ในกระเป\u0e4bาเง\u0e34นเคร\u0e37\u0e48องน\u0e35\u0e49", CmdRiftScope);
		Add("riftcompass", "riftcompass [state|scan|fold|close] — native compass; scan uses a real inventory compass, state is read-only", CmdRiftCompass);
		Add("craftstats", "craftstats [open|refresh|probe] — authoritative crafting abilities and native displayed rows/reward ladder", CmdCraftStats);
		Add("skillstate", "skillstate [select category-id] — read skill cache, refresh counters and displayed EXP gauges", CmdSkillState);
		Add("state", "สถานะรวม: ฉาก/ผ\u0e39\u0e49เล\u0e48น/ไทล\u0e4c/เกจ/เกาะ/guide event", CmdState);
		Add("gauges", "เกจเอาช\u0e35ว\u0e34ตรอดท\u0e31\u0e49งหมด (ค\u0e48า/ส\u0e39งส\u0e38ด/อ\u0e31ตรา)", CmdGauges);
		Add("effects", "สถานะต\u0e34ดต\u0e31วของผ\u0e39\u0e49เล\u0e48น", CmdEffects);
		Add("inv", "ไอเทมในกระเป\u0e4bา [filter]", CmdInventory);
		Add("use", "use <itemId|prototypeId|tag:xxx> — ใช\u0e49/ก\u0e34นไอเทม", CmdUse);
		Add("equip", "equip <itemId|prototypeId> — สวมใส\u0e48", CmdEquip);
		Add("drop", "drop <itemId|prototypeId> — ท\u0e34\u0e49งไอเทม", CmdDrop);
		Add("near", "near [prop|movable|all] [radius] — ว\u0e31ตถ\u0e38โต\u0e49ตอบได\u0e49รอบต\u0e31ว", CmdNear);
		Add("touch", "touch <entityId|entityType|name> [waitMs] — แตะว\u0e31ตถ\u0e38แล\u0e49วค\u0e37นเมน\u0e39โต\u0e49ตอบ", CmdTouch);
		Add("menu", "เมน\u0e39โต\u0e49ตอบของเป\u0e49าหมายป\u0e31จจ\u0e38บ\u0e31น", CmdMenu);
		Add("select", "select <Interaction|index> [id] — กดป\u0e38\u0e48มในเมน\u0e39โต\u0e49ตอบ", CmdSelect);
		Add("context", "ป\u0e38\u0e48มบร\u0e34บท (ด\u0e37\u0e48มน\u0e49ำ/ล\u0e49างต\u0e31ว/สแกน ฯลฯ) ท\u0e35\u0e48ใช\u0e49ได\u0e49ตอนน\u0e35\u0e49", CmdContext);
		Add("do", "do <Interaction> — กดป\u0e38\u0e48มบร\u0e34บท เช\u0e48น DrinkWater WashBody SearchWarphole", CmdDo);
		Add("move", "move <tileX> <tileY> [timeoutMs] — เด\u0e34นไปไทล\u0e4c (รอจนถ\u0e36ง)", CmdMove);
		Add("moveto", "moveto <entityId> [timeoutMs] — เด\u0e34นไปหาว\u0e31ตถ\u0e38", CmdMoveTo);
		Add("stop", "หย\u0e38ดเด\u0e34น", CmdStop);
		Add("triggers", "triggers — trigger ของ guide (PlayerTriggerGuide) ท\u0e35\u0e48โหลดอย\u0e39\u0e48 พร\u0e49อมกรอบ collider เป\u0e47นไทล\u0e4c", CmdTriggers);
		Add("npc", "npc — NPC ประจำเกาะท\u0e35\u0e48วางไว\u0e49 (ช\u0e37\u0e48อ/ไทล\u0e4c/คอมโพเนนต\u0e4c) · npc talk <index> — ส\u0e48งค\u0e38ย (InteractWithEpicNPC) · npc respawn", CmdNpc);
		Add("override", "override <ResourcesPath> — เส\u0e49นทางไฟล\u0e4cท\u0e31บ (game/override/…bytes) ท\u0e35\u0e48เกมจะใช\u0e49แทน Resources", CmdOverride);
		Add("nav", "nav — สถานะ NavGrid (ใช\u0e49ก\u0e31บ NPC เด\u0e34นเล\u0e48นเท\u0e48าน\u0e31\u0e49น) · nav <x> <y> — ต\u0e49นท\u0e38นไทล\u0e4c · nav path <x> <y> — เส\u0e49นทาง NavGrid จากต\u0e31วละครไปไทล\u0e4c", CmdNav);
		Add("craft", "craft <recipeId> [qty] — คราฟต\u0e4cด\u0e49วยว\u0e31ตถ\u0e38ด\u0e34บท\u0e35\u0e48เล\u0e37อกอ\u0e31ตโนม\u0e31ต\u0e34", CmdCraft);
		Add("recipes", "recipes [filter] — ส\u0e39ตรท\u0e35\u0e48ร\u0e39\u0e49จ\u0e31ก", CmdRecipes);
		Add("workbench", "workbench <entityId> [filter] — artifact tags/durability as the client sees them + recipes matching filter (default dgs02): available / needs / valid at this workbench (read-only)", CmdWorkbench);
		Add("guide", "guide — event/flow/todo ของ play guide · guide flow <name> · guide complete · guide reload", CmdGuide);
		Add("dialog", "dialog — บทพ\u0e39ดท\u0e35\u0e48แสดงอย\u0e39\u0e48 · dialog next [n] — กดข\u0e49าม · dialog quiz <index>", CmdDialog);
		Add("msgbox", "msgbox — กล\u0e48องข\u0e49อความท\u0e35\u0e48เป\u0e34ดอย\u0e39\u0e48 · msgbox ok|cancel", CmdMsgBox);
		Add("ui", "ui list|elements [filter] · ui tap|hit <x> <y> (top-left pixels) · ui models · ui click <path|label> · ui open|close <TypeName>", CmdUi);
		Add("key", "key <InputCommand> [down|up|press] — ย\u0e34งคำส\u0e31\u0e48งค\u0e35ย\u0e4cล\u0e31ดของเกม (เช\u0e48น HelperButtonAction, OpenInventory) โดยไม\u0e48ต\u0e49องกดค\u0e35ย\u0e4cจร\u0e34ง", CmdKey);
		Add("keys", "keys [filter] — รายช\u0e37\u0e48อ InputCommand ท\u0e31\u0e49งหมด", CmdKeys);
		Add("emote", "emote <emoticonId> — อ\u0e35โมต\u0e34คอน เช\u0e48น smile", CmdEmote);
		Add("motion", "motion <motionId> — ท\u0e48าทาง", CmdMotion);
		Add("cheat", "cheat <text> — ส\u0e48ง Cheat ไปเซ\u0e34ร\u0e4cฟ (เช\u0e48น m 96 94 = วาร\u0e4cป)", CmdCheat);
		Add("boat", "boat — สถานะแพหน\u0e35เกาะ · boat put — ใส\u0e48ว\u0e31ตถ\u0e38ด\u0e34บท\u0e35\u0e48ม\u0e35ท\u0e31\u0e49งหมด · boat depart", CmdBoat);
		Add("screenshot", "screenshot [path] — บ\u0e31นท\u0e36กภาพหน\u0e49าจอ", CmdScreenshot);
		Add("log", "log [n] — ข\u0e49อความระบบ/ประกาศ/แชท/ข\u0e49อผ\u0e34ดพลาดล\u0e48าส\u0e38ด", CmdLog);
		Add("wait", "wait <ms>", CmdWait);
		Add("discordlink", "discordlink [status|start|click|cancel] — ป\u0e38\u0e48ม Link Discord: start = เร\u0e34\u0e48ม flow ไม\u0e48เป\u0e34ดเบราว\u0e4cเซอร\u0e4c (ค\u0e37น url ให\u0e49จำลอง callback) · click = เหม\u0e37อนกดป\u0e38\u0e48ม (เป\u0e34ดเบราว\u0e4cเซอร\u0e4c)", CmdDiscordLink);
		Add("movecode", "movecode [status|click|redeem <รห\u0e31ส>] — ป\u0e38\u0e48ม «ย\u0e49ายเคร\u0e37\u0e48อง»: click = เหม\u0e37อนกดป\u0e38\u0e48ม (ขอรห\u0e31ส 9 หล\u0e31ก · ค\u0e37น code) · redeem = กรอกรห\u0e31สในช\u0e48องค\u0e39ปองแบบ ConfigInstance.SendCoupon", CmdMoveCode);
		Add("exchange", "exchange [state|preview|previewitem|auction [browse|selling|bidding|claims]|listui|rows|trade <entityId|near>|tkeys|tclick <key>|add <proto> [n]|send <kind> <json>] — AUCTION-TRADE ประม\u0e39ล/เทรด (CommunityExchange) · auction = เป\u0e34ดตลาดแท\u0e47บประม\u0e39ล · tclick = ป\u0e38\u0e48มหน\u0e49าต\u0e48างเทรด (ready/confirm/cancel/add/money/close) · add = วางของในเทรดตรง ๆ (ข\u0e49ามหน\u0e49าเล\u0e37อก) · json ใช\u0e49 ' แทน \"", CmdExchange);
		Add("career", "career [state|open|click <0..7|confirm|back>]", CmdCareer);
		Add("lava", "lava [state|play [entityId|self] [arrive|leave]|on|off|mul <x>|stop] — LAVA-SHIELD-FX โล\u0e48ลาวา (CommunityLavaShieldNet) · play = เล\u0e48นในเคร\u0e37\u0e48องไม\u0e48ผ\u0e48านเซ\u0e34ร\u0e4cฟ · on/off = สว\u0e34ตช\u0e4cในหน\u0e49าต\u0e31\u0e49งค\u0e48า (ส\u0e48งเซ\u0e34ร\u0e4cฟ)", CmdLava);
		Add("ranked", "ranked [state|queue 1v1|3v3|cancel|weapon <proto>|side 0|1|leave|lobby|close|hello|fake <kind> <json>|demo <lobby|queue|place|master|found|hud|count|ot|break|breakwin|win|down|gm|dc|place5|title|off> [3v3]] — PVP-RANKED-BO3 ดวลจ\u0e31ดอ\u0e31นด\u0e31บ (CommunityRanked) · demo = ข\u0e49อม\u0e39ลต\u0e31วอย\u0e48างในเคร\u0e37\u0e48อง (ไม\u0e48ผ\u0e48านเซ\u0e34ร\u0e4cฟ) ไว\u0e49ด\u0e39ท\u0e38กหน\u0e49าจอ · hello = ส\u0e48ง Hello เอง (เซ\u0e34ร\u0e4cฟ DEV ท\u0e35\u0e48ย\u0e31งไม\u0e48 push)", CmdRanked);
		Add("duel", "duel [state|challenge <entityId>|accept [fromId]|decline [fromId]|leave|fake <kind> <json>] — DUEL-CHALLENGE ท\u0e49าดวลท\u0e38กเกาะ (CommunityDuel) · state = สว\u0e34ตช\u0e4c/ดวล/ค\u0e39\u0e48/วงท\u0e35\u0e48วาด", CmdDuel);
		Add("run", "run [state|on|off|toggle|press 1/0|hold 0/1|hello|pos <x> <y> [size]|reset] — RUN-BUTTON ป\u0e38\u0e48มว\u0e34\u0e48ง 3.2.4 (CommunityRunButton) · on/off/toggle = เหม\u0e37อนกดป\u0e38\u0e48ม (ส\u0e48งเซ\u0e34ร\u0e4cฟ) · hold = ต\u0e31\u0e49งค\u0e48า «กดค\u0e49างเพ\u0e37\u0e48อว\u0e34\u0e48ง» · pos = ย\u0e49ายป\u0e38\u0e48มบนจอ (หน\u0e48วย UI จากม\u0e38มขวาล\u0e48าง · จำในเคร\u0e37\u0e48อง) · reset = ตำแหน\u0e48งเร\u0e34\u0e48ม", CmdRun);
		Add("blood", "blood [state|play [intensity 0.3-1.4]|on|off|mul <x>|color <red|green|blue|purple|black>|clear] — BLOOD-FX เล\u0e37อดสาด + คราบพ\u0e37\u0e49น (CommunityBloodFx · ในเคร\u0e37\u0e48องเท\u0e48าน\u0e31\u0e49น) · play = สาดท\u0e35\u0e48ต\u0e31วเอง · on/off = สว\u0e34ตช\u0e4cในหน\u0e49าต\u0e31\u0e49งค\u0e48า", CmdBlood);
		Add("hud", "hud [state|on|off|fetch] — HUD-STATS แถบคนออนไลน\u0e4c · FPS · Ping กลางบน (CommunityFpsCounter) · on/off = สว\u0e34ตช\u0e4cในหน\u0e49าต\u0e31\u0e49งค\u0e48า · fetch = ถาม /status ท\u0e31นท\u0e35", CmdHud);
		Add("banner", "banner [state|fetch|demo|off|onlyduel 0/1/server|server] — BANNER-STYLE แบนเนอร\u0e4cล\u0e48างจอแบบใหม\u0e48 (CommunityBannerStyle) · fetch = ถาม /banners ท\u0e31นท\u0e35 · demo = แบบใหม\u0e48 + ป\u0e49ายต\u0e31วอย\u0e48างในเคร\u0e37\u0e48องน\u0e35\u0e49 (ไม\u0e48ผ\u0e48านเซ\u0e34ร\u0e4cฟ) · off = แบบเด\u0e34มในเคร\u0e37\u0e48องน\u0e35\u0e49 · onlyduel 1 = เหล\u0e37อแค\u0e48ช\u0e48องดวลในเคร\u0e37\u0e48องน\u0e35\u0e49 (server = ตามเซ\u0e34ร\u0e4cฟ) · server = กล\u0e31บไปใช\u0e49ค\u0e48าจากเซ\u0e34ร\u0e4cฟท\u0e31\u0e49งหมด", CmdBanner);
		Add("seasonpass", "seasonpass [status|fetch|open|close|claim <tier|all>|missions] — Season Pass: fetch = ด\u0e36งสถานะ · open = เป\u0e34ดหน\u0e49า (WarpRushGroup โหมด Pass) · claim = ร\u0e31บข\u0e31\u0e49น (all = ท\u0e31\u0e49งหมด)", CmdSeasonPass);
		Add("eventcalendar", "eventcalendar [status|open|close|fetch|notify|click key|source loopback-url]", CmdEventCalendar);
		Add("mtreasury", "mtreasury [status|fetch|open|close|set <coin>|click <key>|x <coin>] — M-TREASURY คล\u0e31งเง\u0e34นเอ\u0e47ม: open = หน\u0e49าต\u0e48าง (เหม\u0e37อนกดป\u0e38\u0e48มในร\u0e49าน) · set = ใส\u0e48จำนวนในหน\u0e49าต\u0e48าง · click = ป\u0e38\u0e48มในหน\u0e49าต\u0e48าง (keys ใน status) · x = แลกตรง (ไม\u0e48ผ\u0e48านย\u0e37นย\u0e31น)", CmdTreasury);
		Add("coupon", "coupon [status|open|move|close|set <ข\u0e49อความ>|click <key>] — COUPON-UI ป\u0e4aอปอ\u0e31ปใส\u0e48ค\u0e39ปอง (open = เหม\u0e37อนกด «ใส\u0e48ค\u0e39ปอง») / กรอกรห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง (move) · set = พ\u0e34มพ\u0e4cในช\u0e48อง · click use = กดป\u0e38\u0e48ม (keys ใน status) · status ค\u0e37น hint/result ท\u0e35\u0e48ป\u0e4aอปอ\u0e31ปแสดง", CmdCoupon);
		Add("gemx", "gemx [status|open|t2g <gem>|g2t <gem>] — แลก T-Stone ↔ Gem: open = กล\u0e48องเล\u0e37อกท\u0e34ศ (เหม\u0e37อนคล\u0e34กช\u0e48อง Gem ในกระเป\u0e4bา) · t2g/g2t = ส\u0e48งเซ\u0e34ร\u0e4cฟตรง (ไม\u0e48ผ\u0e48าน popup)", CmdGemExchange);
		Add("anim", "anim <entityId|entityType|name|pet> [samples] [intervalMs] — ส\u0e31ตว\u0e4cใกล\u0e49ส\u0e38ด: ม\u0e38มห\u0e31น/คล\u0e34ปท\u0e35\u0e48เล\u0e48น/เวลาคล\u0e34ป/สถานะ PetAI ตามเวลา (ANIM-CLIENT · อ\u0e48านอย\u0e48างเด\u0e35ยว)", CmdAnim);
		Add("nametag", "nametag [status] — atlas อ\u0e35โมจ\u0e34 Twemoji + ป\u0e49ายช\u0e37\u0e48อบนห\u0e31วท\u0e35\u0e48ม\u0e35อ\u0e35โมจ\u0e34 · nametag demo [CC] [mobile|pc|none] — ใส\u0e48ธง/ไอคอนหน\u0e49าช\u0e37\u0e48อบนห\u0e31วท\u0e38กคน (เฉพาะเคร\u0e37\u0e48องน\u0e35\u0e49) · nametag label <ข\u0e49อความ> [CC] [mobile|pc|none] — ใส\u0e48หน\u0e49า UILabel ท\u0e38กต\u0e31วท\u0e35\u0e48ม\u0e35ข\u0e49อความน\u0e31\u0e49น (แชท/เพ\u0e37\u0e48อน/แคลน/อ\u0e31นด\u0e31บ)", CmdNameTag);
	}

	private IEnumerator CmdSettingsUi(string[] args, Result r)
	{
		if (ConfigInstance.Settings == null)
		{
			r.Fail("Settings not initialized");
			yield break;
		}
		string text = ((args.Length > 1) ? args[1] : "state");
		switch (text)
		{
		case "open":
		{
			if (!UiReady())
			{
				r.Fail("UI not ready");
				yield break;
			}
			string category = ((args.Length > 2) ? args[2] : "community_combat");
			UIManager.FindScript<ConfigGroup>().Open(category);
			yield return new WaitForSeconds(0.4f);
			break;
		}
		case "range":
		case "blood":
			CommunitySettingsMenu.HandleAction((text == "range") ? "settings_more_range" : "settings_more_blood");
			yield return new WaitForSeconds(0.2f);
			break;
		default:
			if (text != "state")
			{
				r.Fail("settingsui state | open category | range | blood");
				yield break;
			}
			break;
		}
		List<object> list = new List<object>();
		HashSet<string> hashSet = new HashSet<string>();
		List<string> list2 = new List<string>();
		HashSet<string> hashSet2 = new HashSet<string>();
		List<string> list3 = new List<string>();
		foreach (KeyValuePair<string, List<Setting>> item in CommunitySettingsMenu.AllRows())
		{
			List<string> list4 = new List<string>();
			List<string> list5 = new List<string>();
			foreach (Setting item2 in item.Value)
			{
				list4.Add(item2.Key);
				if (!hashSet.Add(item2.Key))
				{
					list3.Add(item2.Key);
				}
			}
			foreach (Setting item3 in CommunitySettingsMenu.Rows(item.Key))
			{
				if (!Setting.IsHidden(item3))
				{
					list5.Add(item3.Key);
				}
			}
			list.Add(Dict("key", item.Key, "label", CommunitySettingsMenu.Label(item.Key), "allKeys", list4, "visibleKeys", list5));
		}
		foreach (KeyValuePair<string, List<Setting>> setting in ConfigInstance.Settings)
		{
			foreach (Setting item4 in setting.Value)
			{
				if (CommunitySettingsMenu.Omitted(item4))
				{
					hashSet2.Add(item4.Key);
				}
				else if (item4.Type != SettingType.Grid && item4.Type != SettingType.Category && !Setting.IsHidden(item4) && (!(setting.Key == "community_voice") || CommunityVoiceSettings.Visible) && !hashSet.Contains(item4.Key))
				{
					list2.Add(item4.Key);
				}
			}
		}
		r.Set("groups", list).Set("missing", list2).Set("duplicateKeys", list3)
			.Set("removedLegacyRows", new List<string>(hashSet2));
		if (list2.Count > 0 || list3.Count > 0)
		{
			r.Fail("Settings grouping lost or duplicated a functional row");
		}
	}

	private IEnumerator CmdRangeFx(string[] args, Result r)
	{
		if (args.Length > 1 && args[1] == "offline")
		{
			yield return OfflineRangeFx(args, r);
			yield break;
		}
		if (!InWorld())
		{
			r.Fail("not in world");
			yield break;
		}
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "state");
		switch (text)
		{
		case "verify":
			yield return VerifyRangeFx(r);
			yield break;
		case "bench":
			yield return BenchRangeFx(args, r);
			yield break;
		case "arena":
		{
			string text2 = ((args.Length > 2) ? args[2] : "status");
			if (text2 != "start" && text2 != "stop" && text2 != "status" && text2 != "reset" && text2 != "cancel")
			{
				r.Fail("rangefx arena start [type] | status | cancel | reset | stop");
				yield break;
			}
			string text3 = ((args.Length > 3) ? args[3] : "2004");
			string text4 = "range_test " + text2 + ((text2 == "start") ? (" " + text3) : "");
			Connections.Frontend.Send(new Cheat
			{
				_Cheat = text4
			});
			r.Set("sent", text4).Set("devOnly", true);
			yield break;
		}
		case "settings":
		{
			string[] array = new string[5] { "attack_range_intensity", "attack_range_width", "attack_range_reduce_motion", "attack_range_color_assist", "attack_range_quality" };
			if (args.Length >= 4)
			{
				if (Array.IndexOf(array, args[2]) < 0)
				{
					r.Fail("unknown range setting");
					yield break;
				}
				if (args[2] == array[2] || args[2] == array[3])
				{
					if (!bool.TryParse(args[3], out var result))
					{
						r.Fail("expected true or false");
						yield break;
					}
					ConfigInstance.ChangeValue(args[2], result);
				}
				else
				{
					if (Array.IndexOf((args[2] == array[1]) ? AttackRangeStyle.Widths : AttackRangeStyle.Levels, args[3]) < 0)
					{
						r.Fail("unsupported range setting value");
						yield break;
					}
					ConfigInstance.ChangeValue(args[2], args[3]);
				}
			}
			List<object> list = new List<object>();
			string[] array2 = array;
			foreach (string text5 in array2)
			{
				list.Add(Dict("key", text5, "label", CommunityGameplayOptions.Label(text5), "value", (text5 == array[2] || text5 == array[3]) ? ((object)ConfigInstance.GetValue(text5, defaultValue: false)) : ConfigInstance.GetValue(text5, "normal")));
			}
			r.Set("settings", list);
			yield break;
		}
		case "preview":
		case "stop":
			foreach (int rangeFxPreview in _rangeFxPreviews)
			{
				AreaOfEffectVisualizer.Stop(rangeFxPreview);
			}
			_rangeFxPreviews.Clear();
			break;
		}
		if (text == "preview")
		{
			Vector3 currentPosition = PlayerBehavior.LocalPlayer.CurrentPosition;
			float num = AreaOfEffectVisualizer.Now();
			_rangeFxPreviews.Add(AreaOfEffectVisualizer.ShowCircle(AreaOfEffectVisualizer.Type.Player, currentPosition + new Vector3(-220f, 0f, 0f), 250f, num, num + 12f, num, num + 12.2f));
			Vector3 vector = Durango.Terrain.Util.ClientPositionToWorldPosition(currentPosition + new Vector3(350f, 0f, 0f));
			List<int> list2 = new List<int>();
			for (int j = 0; j < 26; j++)
			{
				int num2 = -1;
				int item = -1;
				for (int k = 0; k < 30; k++)
				{
					float num3 = ((float)k + 0.5f - 15f) / 15f;
					float num4 = ((float)j + 0.5f - 13f) / 13f;
					if (num3 * num3 + num4 * num4 <= 1f)
					{
						if (num2 < 0)
						{
							num2 = k;
						}
						item = k;
					}
				}
				if (num2 >= 0)
				{
					list2.Add(j);
					list2.Add(num2);
					list2.Add(item);
				}
			}
			double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
			_rangeFxPreviews.Add(AreaOfEffectVisualizer.ShowAttackAlerted(new AttackAlerted
			{
				EntityId = "testbridge:rangefx-preview",
				EventAt = predictedServerTime,
				AttackTime = predictedServerTime + 12.0,
				Center = new WorldPosition(vector.x, vector.z),
				GroundMask = new AttackRangeMask
				{
					X = vector.x - 300f,
					Z = vector.z - 260f,
					Cell = 20f,
					Width = 30,
					Height = 26,
					Runs = list2.ToArray(),
					ActiveUntil = predictedServerTime + 12.2
				}
			}));
			r.Set("previewOnly", true).Set("damage", false).Set("duration", 12)
				.Set("ids", _rangeFxPreviews.ToArray());
			yield return null;
		}
		else if (text != "state" && text != "stop")
		{
			r.Fail("rangefx [state|preview|verify|stop]");
			yield break;
		}
		List<object> list3 = new List<object>();
		FillBorderAlert[] array3 = Resources.FindObjectsOfTypeAll<FillBorderAlert>();
		foreach (FillBorderAlert fillBorderAlert in array3)
		{
			if (!fillBorderAlert.gameObject.activeInHierarchy || !fillBorderAlert.gameObject.scene.IsValid())
			{
				continue;
			}
			List<object> list4 = new List<object>();
			MeshFilter[] componentsInChildren = fillBorderAlert.GetComponentsInChildren<MeshFilter>();
			foreach (MeshFilter meshFilter in componentsInChildren)
			{
				if (meshFilter.sharedMesh != null)
				{
					list4.Add(Dict("object", meshFilter.gameObject.name, "vertices", meshFilter.sharedMesh.vertexCount, "shader", meshFilter.GetComponent<MeshRenderer>().sharedMaterial.shader.name));
				}
			}
			list3.Add(Dict("id", fillBorderAlert.Id, "phase", fillBorderAlert.Stage, "meshes", list4));
		}
		r.Set("style", "native-halo-v5").Set("accentShader", FillBorderAlerts.AccentShader.name).Set("active", FillBorderAlert.ActiveViewers)
			.Set("cacheHits", AttackRangeMaskGlow.Hits)
			.Set("cacheMisses", AttackRangeMaskGlow.Misses)
			.Set("viewers", list3);
	}

	private IEnumerator OfflineRangeFx(string[] args, Result r)
	{
		if (!GameManager.IsTitleScene)
		{
			r.Fail("offline graphics QA requires the title scene");
			yield break;
		}
		GameObject gameObject = Resources.Load<GameObject>("ingame/AreaOfEffectViewer");
		if (gameObject == null)
		{
			r.Fail("Native AreaOfEffectViewer resource missing");
			yield break;
		}
		FillBorderAlerts[] componentsInChildren = gameObject.GetComponentsInChildren<FillBorderAlerts>(includeInactive: true);
		if (componentsInChildren.Length < 2)
		{
			r.Fail("Native range templates missing");
			yield break;
		}
		string directory = Path.GetFullPath((args.Length > 2) ? args[2] : Path.Combine(Path.GetDirectoryName(Application.dataPath), "RangePreview/offline"));
		Directory.CreateDirectory(directory);
		string[] keys = new string[5] { "attack_range_intensity", "attack_range_width", "attack_range_reduce_motion", "attack_range_color_assist", "attack_range_quality" };
		object[] saved = new object[5]
		{
			ConfigInstance.GetValue(keys[0], "normal"),
			ConfigInstance.GetValue(keys[1], "normal"),
			ConfigInstance.GetValue(keys[2], defaultValue: false),
			ConfigInstance.GetValue(keys[3], defaultValue: false),
			ConfigInstance.GetValue(keys[4], "normal")
		};
		List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
		List<FillBorderAlert> viewers = new List<FillBorderAlert>();
		List<object> results = new List<object>();
		List<object> checks = new List<object>();
		int failed = 0;
		int priorActive = FillBorderAlert.ActiveViewers;
		Dictionary<string, float> normalWidths = new Dictionary<string, float>();
		Dictionary<string, float> normalAlphas = new Dictionary<string, float>();
		bool settingsRestored;
		void Check(string name, bool flag2)
		{
			checks.Add(Dict("name", name, "pass", flag2));
			if (!flag2)
			{
				failed++;
			}
		}
		void Setting(int num13, object value)
		{
			if (value is bool value2)
			{
				ConfigInstance.ChangeValue(keys[num13], value2, save: false);
			}
			else
			{
				ConfigInstance.ChangeValue(keys[num13], (string)value, save: false);
			}
		}
		try
		{
			GameObject gameObject2 = new GameObject("Range graphics QA (offline)");
			temporary.Add(gameObject2);
			GameObject gameObject3 = new GameObject("Range QA camera");
			temporary.Add(gameObject3);
			Camera camera = gameObject3.AddComponent<Camera>();
			camera.enabled = false;
			camera.orthographic = true;
			camera.orthographicSize = 430f;
			camera.transform.position = new Vector3(0f, 1200f, 0f);
			camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			camera.nearClipPlane = 1f;
			camera.farClipPlane = 2400f;
			camera.cullingMask = 1073741824;
			camera.clearFlags = CameraClearFlags.Color;
			RenderTexture target = new RenderTexture(1200, 720, 24, RenderTextureFormat.ARGB32);
			target.Create();
			temporary.Add(target);
			camera.targetTexture = target;
			Texture2D image = new Texture2D(1200, 720, TextureFormat.RGB24, mipChain: false);
			temporary.Add(image);
			List<int> list = new List<int>();
			for (int i = 0; i < 26; i++)
			{
				int num = -1;
				int item = -1;
				for (int j = 0; j < 30; j++)
				{
					float num2 = ((float)j + 0.5f - 15f) / 15f;
					float num3 = ((float)i + 0.5f - 13f) / 13f;
					if (num2 * num2 + num3 * num3 <= 1f)
					{
						if (num < 0)
						{
							num = j;
						}
						item = j;
					}
				}
				if (num >= 0)
				{
					list.Add(i);
					list.Add(num);
					list.Add(item);
				}
			}
			FillBorderAlerts[] array = componentsInChildren;
			foreach (FillBorderAlerts obj in array)
			{
				Color bgColor = (Color)typeof(FillBorderAlerts).GetField("_backgroundColor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
				Color borderColor = (Color)typeof(FillBorderAlerts).GetField("_borderColor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
				bool enemy = borderColor.r > borderColor.g;
				if (!viewers.Exists((FillBorderAlert v) => v.name == (enemy ? "Enemy mask" : "Player circle")))
				{
					AnimationCurve animationCurve = (AnimationCurve)typeof(FillBorderAlerts).GetField("_borderAlpha", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
					Texture2D texture2D = new Texture2D(64, 1, TextureFormat.RGBA32, mipChain: false)
					{
						wrapMode = TextureWrapMode.Clamp,
						filterMode = FilterMode.Bilinear
					};
					temporary.Add(texture2D);
					for (int num4 = 0; num4 < 64; num4++)
					{
						Color white = Color.white;
						white.a = ((animationCurve.length > 0) ? animationCurve.Evaluate(((float)num4 + 0.5f) / 64f * animationCurve.keys[animationCurve.length - 1].time) : 1f);
						texture2D.SetPixel(num4, 0, white);
					}
					texture2D.Apply();
					GameObject obj2 = new GameObject(enemy ? "Enemy mask" : "Player circle");
					obj2.transform.SetParent(gameObject2.transform, worldPositionStays: false);
					FillBorderAlert fillBorderAlert = obj2.AddComponent<FillBorderAlert>();
					fillBorderAlert.Init(bgColor, borderColor, texture2D);
					viewers.Add(fillBorderAlert);
					if (enemy)
					{
						fillBorderAlert.SetMask(new AttackRangeMask
						{
							X = 60f,
							Z = -260f,
							Cell = 20f,
							Width = 30,
							Height = 26,
							Runs = list.ToArray(),
							ActiveUntil = 1.0
						});
					}
					else
					{
						fillBorderAlert.SetArc(new Vector3(-350f, 0f, 0f), 250f, 0f, 360f);
					}
					Transform[] componentsInChildren2 = obj2.GetComponentsInChildren<Transform>(includeInactive: true);
					for (int num5 = 0; num5 < componentsInChildren2.Length; num5++)
					{
						componentsInChildren2[num5].gameObject.layer = 30;
					}
				}
			}
			Check("native_player_and_enemy_templates", viewers.Count == 2);
			string[] array2 = new string[6] { "normal", "bright-floor", "colour-assist", "reduced-low", "high-thick", "low-thin" };
			string[] array3 = array2;
			foreach (string profile in array3)
			{
				Setting(0, (profile == "high-thick") ? "high" : ((profile == "low-thin") ? "low" : "normal"));
				Setting(1, (profile == "high-thick") ? "thick" : ((profile == "low-thin") ? "thin" : "normal"));
				Setting(2, profile == "reduced-low");
				Setting(3, profile == "colour-assist");
				Setting(4, (profile == "reduced-low") ? "low" : "normal");
				float num7 = AreaOfEffectVisualizer.Now();
				foreach (FillBorderAlert item2 in viewers)
				{
					item2.Show(num7, num7 + 1f, num7, num7 + 2f);
				}
				camera.backgroundColor = ((profile == "bright-floor") ? new Color(0.72f, 0.68f, 0.52f) : new Color(0.14f, 0.19f, 0.09f));
				yield return new WaitForSeconds(0.25f);
				camera.Render();
				RenderTexture active = RenderTexture.active;
				try
				{
					RenderTexture.active = target;
					image.ReadPixels(new Rect(0f, 0f, 1200f, 720f), 0, 0);
					image.Apply();
				}
				finally
				{
					RenderTexture.active = active;
				}
				string text = Path.Combine(directory, profile + ".png");
				File.WriteAllBytes(text, image.EncodeToPNG());
				results.Add(Dict("profile", profile, "file", text, "nativeGraphics", true, "synthetic", true));
				foreach (FillBorderAlert item3 in viewers)
				{
					MeshFilter[] componentsInChildren3 = item3.GetComponentsInChildren<MeshFilter>();
					MeshRenderer[] componentsInChildren4 = item3.GetComponentsInChildren<MeshRenderer>();
					float num8 = Vector3.Distance(componentsInChildren3[2].sharedMesh.vertices[0], componentsInChildren3[2].sharedMesh.vertices[1]);
					float a = componentsInChildren3[0].sharedMesh.colors[0].a;
					if (profile == "normal")
					{
						normalWidths[item3.name] = num8;
						normalAlphas[item3.name] = a;
					}
					if (profile == "high-thick")
					{
						Check(profile + ":" + item3.name + ":width_increases", num8 > normalWidths[item3.name]);
						Check(profile + ":" + item3.name + ":opacity_increases", a > normalAlphas[item3.name]);
					}
					if (profile == "low-thin")
					{
						Check(profile + ":" + item3.name + ":width_decreases", num8 < normalWidths[item3.name]);
						Check(profile + ":" + item3.name + ":opacity_decreases", a < normalAlphas[item3.name]);
					}
					Check(profile + ":" + item3.name + ":alpha_core", componentsInChildren4[2].sharedMaterial.shader.name == NGUITools.defaultShader.name);
					Check(profile + ":" + item3.name + ":additive_halo", componentsInChildren4[3].sharedMaterial.shader.name == FillBorderAlerts.AccentShader.name);
					if (profile == "reduced-low")
					{
						float num9 = 0f;
						Color[] colors = componentsInChildren3[3].sharedMesh.colors;
						for (int k = 0; k < colors.Length; k++)
						{
							Color color = colors[k];
							num9 = Mathf.Max(num9, color.a);
						}
						Check(profile + ":" + item3.name + ":low_quality_skips_glow", num9 == 0f);
					}
					if (!(item3.name == "Enemy mask"))
					{
						continue;
					}
					bool pass = true;
					MeshFilter[] array4 = componentsInChildren3;
					for (int k = 0; k < array4.Length; k++)
					{
						Vector3[] vertices = array4[k].sharedMesh.vertices;
						for (int num5 = 0; num5 < vertices.Length; num5++)
						{
							Vector3 vector = vertices[num5];
							if (vector.x < 0f || vector.z < 0f || vector.x > 600f || vector.z > 520f)
							{
								pass = false;
							}
						}
					}
					Check(profile + ":mask_style_inside_original_bounds", pass);
				}
			}
			foreach (FillBorderAlert item4 in viewers)
			{
				float num10 = AreaOfEffectVisualizer.Now();
				item4.Show(num10 - 1f, num10 - 0.1f, num10 - 1f, num10 + 0.2f);
				Check(item4.name + ":contact_phase", item4.Stage == "active");
				float a2 = item4.GetComponent<MeshFilter>().sharedMesh.colors[0].a;
				item4.Show(num10 - 1f, num10 - 0.5f, num10 - 1f, num10 - 0.15f);
				Check(item4.name + ":fade_phase", item4.Stage == "fade");
				Check(item4.name + ":fade_lowers_opacity", item4.GetComponent<MeshFilter>().sharedMesh.colors[0].a < a2);
			}
			r.Set("offline", true).Set("networkCommands", 0).Set("damage", false)
				.Set("renders", results);
		}
		finally
		{
			foreach (FillBorderAlert item5 in viewers)
			{
				if (item5 != null)
				{
					item5.Stop(0f);
				}
			}
			foreach (UnityEngine.Object item6 in temporary)
			{
				if (item6 != null)
				{
					UnityEngine.Object.Destroy(item6);
				}
			}
			for (int i = 0; i < keys.Length; i++)
			{
				Setting(i, saved[i]);
			}
		}
		settingsRestored = true;
		for (int num12 = 0; num12 < keys.Length; num12++)
		{
			settingsRestored &= ((saved[num12] is bool flag) ? (ConfigInstance.GetValue(keys[num12], defaultValue: false) == flag) : (ConfigInstance.GetValue(keys[num12], "normal") == (string)saved[num12]));
		}
		Check("settings_restored", settingsRestored);
		Check("no_remaining_test_viewers", FillBorderAlert.ActiveViewers == priorActive);
		r.Set("checks", checks).Set("passed", checks.Count - failed).Set("failed", failed);
		if (failed > 0)
		{
			r.Fail("Offline native range QA failed");
		}
	}

	private IEnumerator VerifyRangeFx(Result r)
	{
		List<object> checks = new List<object>();
		int failures = 0;
		float now = AreaOfEffectVisualizer.Now();
		Vector3 pos = PlayerBehavior.LocalPlayer.CurrentPosition;
		Vector3 vector = Durango.Terrain.Util.ClientPositionToWorldPosition(pos);
		AttackRangeMask attackRangeMask = new AttackRangeMask();
		attackRangeMask.X = vector.x;
		attackRangeMask.Z = vector.z;
		attackRangeMask.Cell = 20f;
		attackRangeMask.Width = 4;
		attackRangeMask.Height = 2;
		attackRangeMask.Runs = new int[6] { 0, 0, 3, 1, 0, 3 };
		attackRangeMask.ActiveUntil = Connections.Frontend.GetPredictedServerTime() + 10.0;
		AttackRangeMask groundMask = attackRangeMask;
		int id = AreaOfEffectVisualizer.ShowCircle(AreaOfEffectVisualizer.Type.Alert, pos, 200f, now, now + 1f, now, now + 2f);
		AreaOfEffectVisualizer.Rotate(id, 47f);
		FillBorderAlert pooled = Viewer(id);
		AreaOfEffectVisualizer.Stop(id);
		AreaOfEffectVisualizer.Stop(id);
		double fixtureAt = Connections.Frontend.GetPredictedServerTime();
		AttackAlerted fixture = new AttackAlerted
		{
			EntityId = "testbridge:verify",
			EventAt = fixtureAt,
			AttackTime = fixtureAt + 1.0,
			Center = new WorldPosition(vector.x, vector.z),
			GroundMask = groundMask
		};
		int id2 = AreaOfEffectVisualizer.ShowAttackAlerted(fixture);
		yield return new WaitForSeconds(0.2f);
		FillBorderAlert fillBorderAlert = Viewer(id2);
		Check("pool_reuses_stopped_viewer_once", fillBorderAlert != null && fillBorderAlert == pooled);
		if (fillBorderAlert != null)
		{
			Check("pool_resets_rotation", Quaternion.Angle(fillBorderAlert.transform.localRotation, Quaternion.identity) < 0.01f);
			Mesh sharedMesh = fillBorderAlert.GetComponent<MeshFilter>().sharedMesh;
			float num = 0f;
			Vector3[] vertices = sharedMesh.vertices;
			int[] triangles = sharedMesh.triangles;
			for (int i = 0; i < triangles.Length; i += 3)
			{
				num += Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).magnitude * 0.5f;
			}
			Check("mask_union_area_no_overlapping_fill", Mathf.Abs(num - 3200f) < 0.01f);
			bool pass = true;
			MeshFilter[] componentsInChildren = fillBorderAlert.GetComponentsInChildren<MeshFilter>();
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				Vector3[] vertices2 = componentsInChildren[j].sharedMesh.vertices;
				for (int k = 0; k < vertices2.Length; k++)
				{
					Vector3 vector2 = vertices2[k];
					if (vector2.x < -0.001f || vector2.x > 80.001f || vector2.z < -0.001f || vector2.z > 40.001f)
					{
						pass = false;
					}
				}
			}
			Check("all_style_vertices_inside_footprint", pass);
			Check("mask_has_only_exterior_border", fillBorderAlert.GetComponentsInChildren<MeshFilter>()[1].sharedMesh.vertexCount == 48);
			Check("mask_has_inner_glow", fillBorderAlert.GetComponentsInChildren<MeshFilter>()[3].sharedMesh.vertexCount > 0);
			Check("native_additive_shader_loaded", fillBorderAlert.GetComponentsInChildren<MeshRenderer>()[3].sharedMaterial.shader.name != NGUITools.defaultShader.name);
			Check("warning_phase_before_contact", fillBorderAlert.Stage == "warning");
			float num2 = 0f;
			Color[] colors = fillBorderAlert.GetComponentsInChildren<MeshFilter>()[2].sharedMesh.colors;
			for (int j = 0; j < colors.Length; j++)
			{
				Color color = colors[j];
				num2 = Mathf.Max(num2, color.a);
			}
			Check("border_is_readable_before_contact", num2 >= 0.6f * AttackRangeStyle.Intensity);
		}
		Check("duplicate_packet_keeps_same_viewer", AreaOfEffectVisualizer.ShowAttackAlerted(fixture) == id2);
		fixture.EventAt -= 1.0;
		Check("older_packet_cannot_replace_range", AreaOfEffectVisualizer.ShowAttackAlerted(fixture) == id2);
		AreaOfEffectVisualizer.ShowAttackAlerted(new AttackAlerted
		{
			EntityId = fixture.EntityId,
			EventAt = fixtureAt + 0.1,
			DamageType = DamageType.Invalid
		});
		Check("cancellation_stops_range", Viewer(id2) == null);
		Check("late_packet_cannot_revive_cancelled_range", AreaOfEffectVisualizer.ShowAttackAlerted(fixture) == -1);
		fixture.EventAt = fixtureAt + 0.2;
		int id3 = AreaOfEffectVisualizer.ShowAttackAlerted(fixture);
		Check("new_attack_after_cancel_is_allowed", Viewer(id3) != null);
		AreaOfEffectVisualizer.Stop(id3);
		int id4 = AreaOfEffectVisualizer.ShowCircle(AreaOfEffectVisualizer.Type.Alert, pos, 200f, now, now + 1f, now, now + 2f);
		int id5 = AreaOfEffectVisualizer.ShowCircle(AreaOfEffectVisualizer.Type.Alert, pos, 250f, now, now + 1f, now, now + 2f);
		Check("concurrent_ranges_have_distinct_viewers", Viewer(id4) != null && Viewer(id5) != null && Viewer(id4) != Viewer(id5));
		AreaOfEffectVisualizer.Stop(id4);
		AreaOfEffectVisualizer.Stop(id5);
		int expired = AreaOfEffectVisualizer.ShowCircle(AreaOfEffectVisualizer.Type.Alert, pos, 200f, now - 3f, now - 1f, now - 3f, now - 0.1f);
		yield return new WaitForSeconds(0.4f);
		Check("expired_range_leaves_no_active_viewer", Viewer(expired) == null);
		r.Set("graphicsOnly", true).Set("damage", false).Set("checks", checks)
			.Set("passed", checks.Count - failures)
			.Set("failed", failures);
		if (failures > 0)
		{
			r.Fail("native range verification failed");
		}
		void Check(string name, bool flag)
		{
			checks.Add(Dict("name", name, "pass", flag));
			if (!flag)
			{
				failures++;
			}
		}
		static FillBorderAlert Viewer(int num3)
		{
			FillBorderAlert[] array = Resources.FindObjectsOfTypeAll<FillBorderAlert>();
			foreach (FillBorderAlert fillBorderAlert2 in array)
			{
				if (fillBorderAlert2.Id == num3 && fillBorderAlert2.gameObject.activeInHierarchy)
				{
					return fillBorderAlert2;
				}
			}
			return null;
		}
	}

	private IEnumerator BenchRangeFx(string[] args, Result r)
	{
		int count = Mathf.Clamp((args.Length > 2) ? ((int)ParseFloat(args[2], 24f)) : 24, 1, 40);
		float num = Mathf.Clamp((args.Length > 3) ? ParseFloat(args[3], 2f) : 2f, 1f, 5f);
		List<int> ids = new List<int>();
		List<float> frameMs = new List<float>();
		AreaOfEffectVisualizer.Now();
		double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
		Vector3 vector = Durango.Terrain.Util.ClientPositionToWorldPosition(PlayerBehavior.LocalPlayer.CurrentPosition);
		List<int> list = new List<int>();
		for (int i = 0; i < 24; i++)
		{
			list.AddRange(new int[3] { i, 0, 23 });
		}
		Stopwatch build = Stopwatch.StartNew();
		try
		{
			for (int j = 0; j < count; j++)
			{
				AttackRangeMask attackRangeMask = new AttackRangeMask
				{
					X = vector.x + (float)((j % 6 - 3) * 60),
					Z = vector.z + (float)((j / 6 - 2) * 60),
					Cell = 20f,
					Width = 24,
					Height = 24,
					Runs = list.ToArray(),
					ActiveUntil = predictedServerTime + (double)num + 0.2
				};
				ids.Add(AreaOfEffectVisualizer.ShowAttackAlerted(new AttackAlerted
				{
					EntityId = "testbridge:bench:" + j,
					EventAt = predictedServerTime,
					AttackTime = predictedServerTime + (double)num * 0.5,
					Center = new WorldPosition(attackRangeMask.X + 240f, attackRangeMask.Z + 240f),
					GroundMask = attackRangeMask
				}));
			}
			build.Stop();
			int vertices = 0;
			HashSet<Material> materials = new HashSet<Material>();
			FillBorderAlert[] array = Resources.FindObjectsOfTypeAll<FillBorderAlert>();
			foreach (FillBorderAlert fillBorderAlert in array)
			{
				if (fillBorderAlert.gameObject.activeInHierarchy && ids.Contains(fillBorderAlert.Id))
				{
					MeshFilter[] componentsInChildren = fillBorderAlert.GetComponentsInChildren<MeshFilter>();
					foreach (MeshFilter meshFilter in componentsInChildren)
					{
						vertices += meshFilter.sharedMesh.vertexCount;
					}
					MeshRenderer[] componentsInChildren2 = fillBorderAlert.GetComponentsInChildren<MeshRenderer>();
					foreach (MeshRenderer meshRenderer in componentsInChildren2)
					{
						materials.Add(meshRenderer.sharedMaterial);
					}
				}
			}
			float until = Time.unscaledTime + num;
			while (Time.unscaledTime < until)
			{
				frameMs.Add(Time.unscaledDeltaTime * 1000f);
				yield return null;
			}
			frameMs.Sort();
			float num2 = 0f;
			foreach (float item in frameMs)
			{
				num2 += item;
			}
			r.Set("graphicsOnly", true).Set("damage", false).Set("count", count)
				.Set("buildMs", build.Elapsed.TotalMilliseconds)
				.Set("vertices", vertices)
				.Set("sharedMaterials", materials.Count)
				.Set("frames", frameMs.Count)
				.Set("meanFrameMs", (frameMs.Count > 0) ? (num2 / (float)frameMs.Count) : 0f)
				.Set("p95FrameMs", (frameMs.Count > 0) ? frameMs[Mathf.Min(frameMs.Count - 1, Mathf.FloorToInt((float)frameMs.Count * 0.95f))] : 0f)
				.Set("cacheHits", AttackRangeMaskGlow.Hits)
				.Set("cacheEntries", AttackRangeMaskGlow.Entries);
		}
		finally
		{
			foreach (int item2 in ids)
			{
				AreaOfEffectVisualizer.Stop(item2);
			}
		}
	}

	private IEnumerator CmdAnim(string[] args, Result r)
	{
		if (!InWorld())
		{
			r.Fail("not in world");
			yield break;
		}
		string text = ((args.Length > 1) ? args[1] : "pet");
		int samples = Mathf.Clamp((int)((args.Length > 2) ? ParseFloat(args[2], 1f) : 1f), 1, 400);
		float interval = Mathf.Max(0.02f, ((args.Length > 3) ? ParseFloat(args[3], 100f) : 100f) / 1000f);
		AnimalBehavior target = null;
		if (string.Equals(text, "pet", StringComparison.OrdinalIgnoreCase))
		{
			PetAI petObject = Durango.Utils.Singleton<PetManager>.Instance().GetPetObject(Durango.Utils.Singleton<PetManager>.Instance().GetPlayerPetId());
			target = ((petObject != null) ? petObject.TargetAnimal : null);
		}
		else
		{
			int result;
			bool flag = int.TryParse(text, out result);
			float num = float.MaxValue;
			AnimalBehavior[] array = UnityEngine.Object.FindObjectsByType<AnimalBehavior>();
			foreach (AnimalBehavior animalBehavior in array)
			{
				if (!(animalBehavior == null) && (string.Equals(animalBehavior.EntityId, text, StringComparison.OrdinalIgnoreCase) || (flag && animalBehavior.EntityTypeId == result) || animalBehavior.name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
				{
					float num2 = Vector3.Distance(animalBehavior.transform.position, PlayerBehavior.LocalPlayer.CurrentPosition);
					if (num2 < num)
					{
						num = num2;
						target = animalBehavior;
					}
				}
			}
		}
		if (target == null)
		{
			r.Fail("animal not found: " + text);
			yield break;
		}
		PetAI petAI = target.GetComponent<PetAI>();
		r.Set("entityId", target.EntityId).Set("entityType", target.EntityTypeId).Set("object", target.name);
		List<object> list = new List<object>();
		float t0 = Time.time;
		for (int j = 0; j < samples; j++)
		{
			if (target == null)
			{
				break;
			}
			AnimationState curAnimState = target.CurAnimState;
			Vector2 vector = Durango.Terrain.Util.ClientPositionToTilePosition(target.transform.position);
			list.Add(Dict("t", Math.Round(Time.time - t0, 2), "yaw", Math.Round(target.transform.eulerAngles.y, 1), "clip", target.CurrentAnimClipName, "time", (curAnimState != null) ? Math.Round(curAnimState.time, 2) : (-1.0), "len", (curAnimState != null) ? Math.Round(curAnimState.length, 2) : (-1.0), "loop", curAnimState != null && curAnimState.wrapMode == WrapMode.Loop, "x", Math.Round(vector.x, 2), "y", Math.Round(vector.y, 2), "pet", (petAI != null) ? petAI.AIState.ToString() : null));
			if (j + 1 < samples)
			{
				yield return new WaitForSeconds(interval);
			}
		}
		r.Set("samples", list);
	}

	private IEnumerator CmdNameTag(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "status");
		r.Set("atlas", CommunityEmoji.Available).Set("caps", "X-Community-Caps: nametag-emoji, item-link, server-popup, voice-chat, i18n-keys, pet-scale, pvp-ranked, pvp-duel, gather-check, rift-coin, rift-scope");
		if (CommunityEmoji.Texture != null)
		{
			r.Set("texture", CommunityEmoji.Texture.width + "x" + CommunityEmoji.Texture.height + " mips " + CommunityEmoji.Texture.mipmapCount);
		}
		FieldInfo field = typeof(PlayerFloatingControl).GetField("_nametagLabel", BindingFlags.Instance | BindingFlags.NonPublic);
		List<UILabel> nameLabels = new List<UILabel>();
		PlayerFloatingControl[] array = UnityEngine.Object.FindObjectsByType<PlayerFloatingControl>();
		for (int i = 0; i < array.Length; i++)
		{
			UILabel uILabel = ((field != null) ? (field.GetValue(array[i]) as UILabel) : null);
			if (uILabel != null)
			{
				nameLabels.Add(uILabel);
			}
		}
		if (text == "demo" || text == "label")
		{
			int num = 2;
			string text2 = null;
			if (text == "label")
			{
				if (args.Length <= 2)
				{
					r.Fail("nametag label <ข\u0e49อความ> [CC] [mobile|pc|none]");
					yield break;
				}
				text2 = args[2];
				num = 3;
			}
			string text3 = ((args.Length > num) ? args[num].ToUpperInvariant() : "TH");
			string text4 = ((args.Length > num + 1) ? args[num + 1].ToLowerInvariant() : "mobile");
			if (text3.Length != 2 || text3[0] < 'A' || text3[0] > 'Z' || text3[1] < 'A' || text3[1] > 'Z')
			{
				r.Fail("CC = รห\u0e31สประเทศ 2 ต\u0e31ว เช\u0e48น TH JP US");
				yield break;
			}
			string text5 = ((text4 == "pc") ? "\ud83d\udcbb " : ((text4 == "none") ? string.Empty : "\ud83d\udcf1 ")) + char.ConvertFromUtf32(127462 + (text3[0] - 65)) + char.ConvertFromUtf32(127462 + (text3[1] - 65)) + " ";
			List<UILabel> list = new List<UILabel>();
			if (text2 == null)
			{
				list.AddRange(nameLabels);
			}
			else
			{
				UILabel[] array2 = UnityEngine.Object.FindObjectsByType<UILabel>();
				for (int j = 0; j < array2.Length; j++)
				{
					if (array2[j].text != null && array2[j].text.Contains(text2))
					{
						list.Add(array2[j]);
					}
				}
			}
			List<object> list2 = new List<object>();
			for (int k = 0; k < list.Count; k++)
			{
				if (!list[k].text.StartsWith(text5))
				{
					list[k].text = text5 + list[k].text;
				}
				list2.Add(list[k].name + ": " + list[k].text);
			}
			r.Set("changed", list2);
			yield return null;
		}
		List<object> list3 = new List<object>();
		for (int l = 0; l < nameLabels.Count; l++)
		{
			UILabel uILabel2 = nameLabels[l];
			int num2 = 0;
			foreach (Transform item in uILabel2.transform)
			{
				UITexture component = item.GetComponent<UITexture>();
				if (component != null && component.mainTexture == CommunityEmoji.Texture && component.isVisible)
				{
					num2++;
				}
			}
			list3.Add(uILabel2.text + " | emoji images " + num2 + " | printed " + uILabel2.printedSize.x.ToString("0") + "x" + uILabel2.printedSize.y.ToString("0") + " | font " + uILabel2.fontSize);
		}
		r.Set("nametags", list3);
	}

	private IEnumerator CmdCraftStats(string[] args, Result r)
	{
		if (!UiReady())
		{
			r.Fail("Ability UI not ready");
			yield break;
		}
		StatisticsSystem stats = GameSystem<StatisticsSystem>.Instance();
		if (args.Length > 1 && args[1] == "open")
		{
			CharacterInfoGroup.ShowRepresentTypePopup(RepresentType.CraftingPower);
		}
		else if (args.Length > 1 && args[1] == "refresh")
		{
			stats.RefreshAbilityUi();
		}
		else if (args.Length > 1 && args[1] == "probe")
		{
			int received = 0;
			int before = stats.StatisticsSnapshots;
			Action failing = delegate
			{
				throw new InvalidOperationException("intentional local AbilityUI isolation probe");
			};
			Action following = delegate
			{
				received++;
			};
			stats.StatisticsUpdated += failing;
			stats.StatisticsUpdated += following;
			try
			{
				stats.RefreshAbilityUi();
				float until = Time.realtimeSinceStartup + 3f;
				while (stats.StatisticsSnapshots == before && Time.realtimeSinceStartup < until)
				{
					yield return null;
				}
			}
			finally
			{
				stats.StatisticsUpdated -= failing;
				stats.StatisticsUpdated -= following;
			}
			r.Set("followingListenerCalls", received);
		}
		yield return null;
		RepresentTypePopup representTypePopup = UIManager.Popup.FindTooltip<RepresentTypePopup>();
		List<object> list = new List<object>();
		List<object> list2 = new List<object>();
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;
		if (representTypePopup != null && representTypePopup.IsVisible)
		{
			KScrollView kScrollView = typeof(RepresentTypePopup).GetField("_scrollView", bindingAttr).GetValue(representTypePopup) as KScrollView;
			List<Derived> list3 = typeof(RepresentTypePopup).GetField("_deriveds", bindingAttr).GetValue(representTypePopup) as List<Derived>;
			int num = 0;
			while (kScrollView != null && list3 != null && num < kScrollView.Nodes.Count && num < list3.Count)
			{
				UILabel component = kScrollView.Nodes[num].transform.Find("Value").GetComponent<UILabel>();
				list.Add(Dict("derived", (int)list3[num], "value", stats.GetDeriveds(list3[num]), "display", component.text));
				num++;
			}
			RepresentTypeRewardNode[] componentsInChildren = representTypePopup.GetComponentsInChildren<RepresentTypeRewardNode>();
			foreach (RepresentTypeRewardNode obj in componentsInChildren)
			{
				UILabel uILabel = typeof(RepresentTypeRewardNode).GetField("_pointLabel", bindingAttr).GetValue(obj) as UILabel;
				UISprite uISprite = typeof(RepresentTypeRewardNode).GetField("_bg1Sprite", bindingAttr).GetValue(obj) as UISprite;
				list2.Add(Dict("threshold", (uILabel == null) ? "" : uILabel.text, "color", (uILabel == null) ? "" : uILabel.color.ToString(), "fill", (uISprite == null) ? 0f : uISprite.fillAmount));
			}
		}
		List<object> list4 = new List<object>();
		int[] array = new int[9] { 210, 211, 215, 216, 217, 218, 219, 220, 239 };
		foreach (int num3 in array)
		{
			list4.Add(Dict("derived", num3, "value", stats.GetDeriveds((Derived)num3)));
		}
		r.Set("open", representTypePopup != null && representTypePopup.IsVisible).Set("requests", stats.UiRefreshRequests).Set("snapshots", stats.StatisticsSnapshots)
			.Set("abilities", list4)
			.Set("rows", list)
			.Set("rewards", list2);
	}

	private IEnumerator CmdSkillState(string[] args, Result r)
	{
		if (!UiReady())
		{
			r.Fail("Skill UI not ready");
			yield break;
		}
		SkillSystem skills = GameSystem<SkillSystem>.Instance();
		SkillGroup page = UIManager.FindScript<SkillGroup>();
		if (args.Length > 2 && args[1] == "select" && int.TryParse(args[2], out var result))
		{
			if (skills.GetSkillCategory((Shared.Skill.Category)result) == null)
			{
				r.Fail("Unknown skill category");
				yield break;
			}
			page.Open((Shared.Skill.Category)result);
			yield return null;
		}
		List<object> list = new List<object>();
		for (int i = 0; i < skills.SkillCategoryCount; i++)
		{
			Durango.Logic.Skill.Category skillCategory = skills.GetSkillCategory(i);
			skills.GetCategoryExp(skillCategory.Type, out var current, out var max);
			list.Add(Dict("id", (int)skillCategory.Type, "level", skillCategory.Level, "exp", current, "needed", max, "researching", skillCategory.IsResearching()));
		}
		List<object> list2 = new List<object>();
		if (page != null)
		{
			SkillCategoryProgressGauge[] componentsInChildren = page.GetComponentsInChildren<SkillCategoryProgressGauge>(includeInactive: true);
			foreach (SkillCategoryProgressGauge skillCategoryProgressGauge in componentsInChildren)
			{
				BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;
				UILabel uILabel = typeof(SkillCategoryProgressGauge).GetField("_expLabel", bindingAttr).GetValue(skillCategoryProgressGauge) as UILabel;
				UISprite uISprite = typeof(SkillCategoryProgressGauge).GetField("_expUpperSprite", bindingAttr).GetValue(skillCategoryProgressGauge) as UISprite;
				Durango.Logic.Skill.Category category = typeof(SkillCategoryProgressGauge).GetField("_category", bindingAttr).GetValue(skillCategoryProgressGauge) as Durango.Logic.Skill.Category;
				list2.Add(Dict("active", skillCategoryProgressGauge.gameObject.activeInHierarchy, "category", (int)(category?.Type ?? Shared.Skill.Category.Invalid), "text", (uILabel == null) ? "" : StripTags(uILabel.text), "fill", (uISprite == null) ? 0f : uISprite.fillAmount));
			}
		}
		r.Set("open", page != null && page.IsOpened).Set("sp", skills.SkillPoint).Set("remainSp", skills.RemainSkillPoint)
			.Set("refreshRequests", skills.SkillUiRefreshRequests)
			.Set("snapshots", skills.SkillSnapshotsReceived)
			.Set("expNotices", skills.SkillExperienceNotices)
			.Set("refreshPending", skills.SkillUiRefreshPending)
			.Set("categories", list)
			.Set("gauges", list2);
	}

	private IEnumerator CmdEventCalendar(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1] : "status");
		if (text == "native-tab" && args.Length > 2)
		{
			EventGroup eventGroup = UIManager.FindScript<EventGroup>();
			if (eventGroup == null || !eventGroup.IsOpened || !int.TryParse(args[2], out var result) || result < 0)
			{
				r.Fail("Invalid native Event tab");
				yield break;
			}
			eventGroup.SendMessage("OnClickTab", result, SendMessageOptions.RequireReceiver);
		}
		else
		{
			switch (text)
			{
			case "title":
				Durango.Utils.Singleton<GameManager>.Instance().MoveToTitle();
				break;
			case "open":
				r.Set("opened", CommunityEventCalendar.Open());
				break;
			case "close":
				CommunityEventCalendar.Close();
				break;
			case "fetch":
				CommunityEventCalendar.Fetch();
				break;
			case "notify":
				r.Set("shown", CommunityEventCalendar.TestNotification());
				break;
			case "click":
				if (args.Length > 2)
				{
					r.Set("clicked", CommunityEventCalendar.Click(args[2]));
					break;
				}
				goto default;
			default:
				if (!(text == "source") || args.Length <= 2)
				{
					break;
				}
				if (args[2] == "server")
				{
					CommunityEventCalendar.TestSource = "";
				}
				else
				{
					if (!Uri.TryCreate(args[2], UriKind.Absolute, out var result2) || !result2.IsLoopback || !(result2.Scheme == "http"))
					{
						r.Fail("Only loopback HTTP test sources are allowed");
						yield break;
					}
					CommunityEventCalendar.TestSource = args[2];
				}
				CommunityEventCalendar.Fetch();
				break;
			}
		}
		yield return null;
		r.Set("open", CommunityEventCalendar.IsOpen).Set("status", CommunityEventCalendar.State).Set("events", CommunityEventCalendar.EventCount)
			.Set("loginOpens", CommunityEventCalendar.LoginOpens)
			.Set("prefetching", CommunityEventCalendar.Prefetching)
			.Set("cached", CommunityEventCalendar.HasCachedData)
			.Set("noticeVisible", CommunityEventCalendar.NoticeVisible)
			.Set("noticeTitle", CommunityEventCalendar.NoticeTitle)
			.Set("pendingNotices", CommunityEventCalendar.PendingNotices)
			.Set("noticeProgress", CommunityEventCalendar.NoticeProgress)
			.Set("noticeAlpha", CommunityEventCalendar.NoticeAlpha);
	}

	private IEnumerator CmdTreasury(string[] args, Result r)
	{
		long result;
		switch ((args.Length > 1) ? args[1].ToLowerInvariant() : "status")
		{
		case "fetch":
			CommunityTreasury.Fetch(null);
			yield return new WaitForSeconds(2f);
			break;
		case "open":
			if (!UiReady())
			{
				r.Fail("UI ย\u0e31งไม\u0e48พร\u0e49อม (ย\u0e31งไม\u0e48เข\u0e49าโลก?)");
				yield break;
			}
			CommunityTreasuryUI.Open();
			yield return new WaitForSeconds(2f);
			break;
		case "close":
			CommunityTreasuryUI.Close();
			break;
		case "set":
			if (args.Length <= 2 || !long.TryParse(args[2], out result))
			{
				r.Fail("ต\u0e49องใส\u0e48จำนวน Coin");
				yield break;
			}
			CommunityTreasuryUI.SetAmountForTest(result);
			break;
		case "click":
			if (args.Length <= 2 || !CommunityTreasuryUI.Click(args[2]))
			{
				r.Fail("ป\u0e38\u0e48มน\u0e35\u0e49กดไม\u0e48ได\u0e49 (ไม\u0e48ม\u0e35/ป\u0e34ด/หน\u0e49าต\u0e48างไม\u0e48เป\u0e34ด) — keys: " + string.Join(",", CommunityTreasuryUI.Keys().ToArray()));
				yield break;
			}
			yield return new WaitForSeconds(1f);
			break;
		case "x":
			if (args.Length <= 2 || !long.TryParse(args[2], out result) || result <= 0)
			{
				r.Fail("ต\u0e49องใส\u0e48จำนวน Coin");
				yield break;
			}
			CommunityTreasuryUI.Trade(result);
			yield return new WaitForSeconds(3f);
			break;
		}
		r.Set("active", CommunityTreasury.Active).Set("balance", CommunityTreasury.Balance).Set("rate", CommunityTreasury.Rate)
			.Set("fee", CommunityTreasury.Fee)
			.Set("maxCoins", CommunityTreasury.MaxCoins)
			.Set("coin", CommunityTreasury.CoinBalance)
			.Set("tStone", CommunityTreasury.TStoneBalance)
			.Set("last", CommunityTreasury.LastEvent)
			.Set("open", CommunityTreasuryUI.IsOpen)
			.Set("want", CommunityTreasuryUI.Want)
			.Set("status", CommunityTreasuryUI.LastText)
			.Set("keys", new List<object>(CommunityTreasuryUI.Keys()));
	}

	private IEnumerator CmdCoupon(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "status");
		switch (text)
		{
		case "open":
		case "move":
			if (!UiReady())
			{
				r.Fail("UI ย\u0e31งไม\u0e48พร\u0e49อม (ย\u0e31งไม\u0e48เข\u0e49าโลก?)");
				yield break;
			}
			if (!CommunityCouponUI.Open((text == "move") ? CommunityCouponUI.Mode.Move : CommunityCouponUI.Mode.Coupon))
			{
				r.Fail("สร\u0e49างป\u0e4aอปอ\u0e31ปไม\u0e48ได\u0e49 (ไม\u0e48เจอแม\u0e48แบบป\u0e38\u0e48ม/ฟอนต\u0e4cใน PopupGroup)");
				yield break;
			}
			yield return new WaitForSeconds(1f);
			break;
		case "close":
			CommunityCouponUI.Close();
			break;
		case "set":
			if (!CommunityCouponUI.SetText((args.Length > 2) ? string.Join(" ", args, 2, args.Length - 2) : string.Empty))
			{
				r.Fail("ป\u0e4aอปอ\u0e31ปไม\u0e48ได\u0e49เป\u0e34ด");
				yield break;
			}
			break;
		case "click":
		{
			if (args.Length <= 2 || !CommunityCouponUI.Click(args[2]))
			{
				r.Fail("ป\u0e38\u0e48มน\u0e35\u0e49กดไม\u0e48ได\u0e49 (ไม\u0e48ม\u0e35/ป\u0e34ด/ป\u0e4aอปอ\u0e31ปไม\u0e48เป\u0e34ด) — keys: " + string.Join(",", CommunityCouponUI.Keys().ToArray()));
				yield break;
			}
			for (int i = 0; i < 40; i++)
			{
				if (!CommunityCouponUI.Busy)
				{
					break;
				}
				yield return new WaitForSeconds(0.25f);
			}
			yield return new WaitForSeconds(0.5f);
			break;
		}
		}
		r.Set("open", CommunityCouponUI.IsOpen).Set("mode", CommunityCouponUI.OpenMode).Set("busy", CommunityCouponUI.Busy)
			.Set("hint", CommunityCouponUI.LastHint)
			.Set("result", CommunityCouponUI.LastResult)
			.Set("keys", new List<object>(CommunityCouponUI.Keys()));
	}

	private IEnumerator CmdGemExchange(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "status");
		switch (text)
		{
		case "open":
			if (!UiReady())
			{
				r.Fail("UI ย\u0e31งไม\u0e48พร\u0e49อม (ย\u0e31งไม\u0e48เข\u0e49าโลก?)");
				yield break;
			}
			CommunityWalletExchange.Open();
			yield return new WaitForSeconds(1f);
			break;
		case "t2g":
		case "g2t":
		{
			if (args.Length <= 2 || !long.TryParse(args[2], out var result) || result <= 0)
			{
				r.Fail("ต\u0e49องใส\u0e48จำนวน Gem");
				yield break;
			}
			CommunityWalletExchange.Exchange(text, result);
			yield return new WaitForSeconds(2.5f);
			break;
		}
		}
		long num = 0L;
		long num2 = 0L;
		try
		{
			num = InventorySystem.Wallet.GetBalance(Currency.TStone);
			num2 = InventorySystem.Wallet.GetBalance(Currency.Gem);
		}
		catch (Exception)
		{
		}
		r.Set("available", CommunityWalletExchange.Available).Set("last", CommunityWalletExchange.LastEvent).Set("rate", CommunityWalletExchange.Rate)
			.Set("tStone", num)
			.Set("gem", num2);
	}

	private IEnumerator CmdLava(string[] args, Result r)
	{
		r.Set("result", CommunityLavaShieldNet.DebugCommand(args));
		yield return new WaitForSeconds(0.1f);
	}

	private IEnumerator CmdRanked(string[] args, Result r)
	{
		r.Set("result", CommunityRanked.DebugCommand(args));
		yield return new WaitForSeconds(0.1f);
	}

	private IEnumerator CmdDuel(string[] args, Result r)
	{
		r.Set("result", CommunityDuel.DebugCommand(args));
		yield return new WaitForSeconds(0.1f);
	}

	private IEnumerator CmdRun(string[] args, Result r)
	{
		r.Set("result", CommunityRunButton.DebugCommand(args));
		yield return new WaitForSeconds(0.1f);
	}

	private IEnumerator CmdBlood(string[] args, Result r)
	{
		r.Set("result", CommunityBloodFx.DebugCommand(args));
		yield return new WaitForSeconds(0.1f);
	}

	private IEnumerator CmdHud(string[] args, Result r)
	{
		CommunityFpsCounter.DebugCommand(args);
		yield return new WaitForSeconds(0.6f);
		r.Set("result", CommunityFpsCounter.Describe());
	}

	private IEnumerator CmdBanner(string[] args, Result r)
	{
		string text = CommunityBannerStyle.DebugCommand(args);
		yield return new WaitForSeconds(0.6f);
		r.Set("result", text + " | now " + CommunityBannerStyle.Describe());
	}

	private IEnumerator CmdCareer(string[] args, Result r)
	{
		if (args.Length > 1 && args[1] == "open")
		{
			CommunityCareer.Reopen();
		}
		if (args.Length > 2 && args[1] == "click")
		{
			r.Set("clicked", CommunityCareerUI.Click(args[2]));
		}
		yield return new WaitForSeconds(0.3f);
		r.Set("open", CommunityCareerUI.IsOpen).Set("busy", CommunityCareer.Busy);
	}

	private IEnumerator CmdExchange(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "state");
		switch (text)
		{
		case "aupreview":
		{
			string text2 = ((args.Length > 2) ? args[2].ToLowerInvariant() : "browse");
			int num2;
			CommunityAuctionUI.Tab tab;
			switch (text2)
			{
			case "stop":
			{
				MarketGroup marketGroup = UIManager.FindScript<MarketGroup>();
				if (marketGroup != null)
				{
					marketGroup.ForceClose();
				}
				CommunityAuctionPreview.End();
				break;
			}
			default:
				num2 = 0;
				goto IL_0295;
			case "claims":
				num2 = 3;
				goto IL_0295;
			case "bidding":
				num2 = 2;
				goto IL_0295;
			case "selling":
				{
					num2 = 1;
					goto IL_0295;
				}
				IL_0295:
				tab = (CommunityAuctionUI.Tab)num2;
				r.Set("opened", CommunityAuctionPreview.Begin(tab, text2 == "list"));
				yield return new WaitForSeconds(0.5f);
				break;
			}
			break;
		}
		case "auaction":
		{
			if (!CommunityAuctionPreview.Active || args.Length < 3 || !int.TryParse(args[2], out var result2) || result2 < 0 || result2 >= CommunityAuctionUI.Data.Goods.Count)
			{
				r.Fail("auaction <row>: preview only");
			}
			else
			{
				CommunityAuctionUI.DoAction(CommunityAuctionUI.Data.Goods[result2]);
			}
			break;
		}
		case "auction":
		case "listui":
		{
			MarketGroup marketGroup2 = (UiReady() ? UIManager.FindScript<MarketGroup>() : null);
			if (marketGroup2 == null)
			{
				r.Fail("ไม\u0e48ม\u0e35 MarketGroup (ย\u0e31งไม\u0e48เข\u0e49าโลก?)");
				yield break;
			}
			if (text == "listui")
			{
				if (!marketGroup2.IsOpened)
				{
					marketGroup2.Open();
					yield return new WaitForSeconds(1f);
				}
				marketGroup2.SelectTabForTest(4);
			}
			else
			{
				CommunityAuctionUI.Tab tab2 = CommunityAuctionUI.Tab.Browse;
				if (args.Length > 2)
				{
					switch (args[2].ToLowerInvariant())
					{
					case "selling":
						tab2 = CommunityAuctionUI.Tab.Selling;
						break;
					case "bidding":
						tab2 = CommunityAuctionUI.Tab.Bidding;
						break;
					case "claims":
						tab2 = CommunityAuctionUI.Tab.Claims;
						break;
					}
				}
				marketGroup2.OpenAuctionTab(tab2);
			}
			yield return new WaitForSeconds(2f);
			r.Set("opened", marketGroup2.IsOpened).Set("auctionMode", marketGroup2.CommunityAuctionOpen);
			break;
		}
		case "trade":
		{
			string text3 = ((args.Length > 2) ? args[2] : "near");
			if (text3 == "near")
			{
				text3 = string.Empty;
				float num3 = float.MaxValue;
				Vector3 b = ((Camera.main != null) ? Camera.main.transform.position : Vector3.zero);
				PlayerBehavior[] array = UnityEngine.Object.FindObjectsByType<PlayerBehavior>();
				foreach (PlayerBehavior playerBehavior in array)
				{
					if (!(playerBehavior == null) && !string.IsNullOrEmpty(playerBehavior.EntityId) && !(playerBehavior.EntityId == GameManager.PlayerId))
					{
						float num5 = Vector3.Distance(playerBehavior.transform.position, b);
						if (num5 < num3)
						{
							num3 = num5;
							text3 = playerBehavior.EntityId;
						}
					}
				}
				if (text3.Length == 0)
				{
					r.Fail("ไม\u0e48เห\u0e47นผ\u0e39\u0e49เล\u0e48นคนอ\u0e37\u0e48นใกล\u0e49 ๆ");
					yield break;
				}
			}
			CommunityExchange.RequestTrade(text3);
			yield return new WaitForSeconds(1.5f);
			r.Set("target", text3);
			break;
		}
		case "preview":
			r.Set("opened", CommunityTradeUI.PreviewForTest());
			break;
		case "previewitem":
			r.Set("opened", CommunityTradeUI.PreviewItemForTest());
			break;
		case "tkeys":
			r.Set("keys", new List<object>(CommunityTradeUI.Keys()));
			break;
		case "tclick":
			if (args.Length <= 2 || !CommunityTradeUI.Click(args[2]))
			{
				r.Fail("ป\u0e38\u0e48มน\u0e35\u0e49กดไม\u0e48ได\u0e49 (ไม\u0e48ม\u0e35/ป\u0e34ด/หน\u0e49าต\u0e48างเทรดไม\u0e48เป\u0e34ด) — keys: " + string.Join(",", CommunityTradeUI.Keys().ToArray()));
				yield break;
			}
			yield return new WaitForSeconds(1f);
			break;
		case "add":
		{
			if (args.Length <= 2 || CommunityExchange.Trade == null)
			{
				r.Fail("add <proto> [n] — ต\u0e49องม\u0e35เทรดเป\u0e34ดอย\u0e39\u0e48");
				yield break;
			}
			int result3;
			int num = ((args.Length <= 3 || !int.TryParse(args[3], out result3)) ? 1 : result3);
			JArray jArray = new JArray();
			foreach (ItemData playerItem in GameSystem<InventorySystem>.Instance().PlayerItemList)
			{
				if (jArray.Count < num && playerItem.PrototypeId == args[2] && playerItem.Tradable && !playerItem.IsEquipments && !playerItem.Locked)
				{
					jArray.Add(playerItem.Id);
				}
			}
			JObject jObject = new JObject();
			jObject["tradeId"] = CommunityExchange.Str(CommunityExchange.Trade, "tradeId");
			jObject["itemIds"] = jArray;
			JObject reply3 = null;
			bool done3 = false;
			CommunityExchange.Request(22, jObject, delegate(JObject x)
			{
				reply3 = x;
				done3 = true;
			});
			float t = Time.realtimeSinceStartup;
			while (!done3 && Time.realtimeSinceStartup - t < 16f)
			{
				yield return null;
			}
			r.Set("sent", jArray.Count).Set("reply", (reply3 != null) ? reply3.ToString(Formatting.None) : "none");
			break;
		}
		case "send":
		{
			if (args.Length <= 2 || !byte.TryParse(args[2], out var result))
			{
				r.Fail("send <kind> <json>");
				yield break;
			}
			JObject body = CommunityExchange.Parse((args.Length > 3) ? string.Join(" ", args, 3, args.Length - 3).Replace('\'', '"') : "{}") ?? new JObject();
			JObject reply2 = null;
			bool done2 = false;
			CommunityExchange.Request(result, body, delegate(JObject x)
			{
				reply2 = x;
				done2 = true;
			});
			float t = Time.realtimeSinceStartup;
			while (!done2 && Time.realtimeSinceStartup - t < 16f)
			{
				yield return null;
			}
			r.Set("reply", (reply2 != null) ? reply2.ToString(Formatting.None) : "none");
			break;
		}
		}
		foreach (KeyValuePair<string, object> item2 in CommunityExchange.State())
		{
			r.Set(item2.Key, item2.Value);
		}
		List<object> list = new List<object>();
		foreach (Durango.Logic.Market.Commodity good in CommunityAuctionUI.Data.Goods)
		{
			ItemData item = good.GetItem();
			list.Add(Dict("id", good.Id, "item", (item != null) ? item.Name : "?", "price", good.Price, "text", good.CommunityPriceText ?? string.Empty));
		}
		r.Set("tab", CommunityAuctionUI.Current.ToString()).Set("rows", list).Set("moneyWaiting", CommunityAuctionUI.MoneyWaiting);
		if (CommunityExchange.Trade != null)
		{
			r.Set("tradeJson", CommunityExchange.Trade.ToString(Formatting.None));
		}
	}

	private IEnumerator CmdSeasonPass(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "status");
		switch (text)
		{
		case "fetch":
			CommunitySeasonPass.Fetch();
			yield return new WaitForSeconds(2f);
			break;
		case "open":
		case "close":
		{
			WarpRushGroup warpRushGroup = (UiReady() ? UIManager.FindScript<WarpRushGroup>() : null);
			if (warpRushGroup == null)
			{
				r.Fail("ไม\u0e48ม\u0e35 WarpRushGroup (ย\u0e31งไม\u0e48เข\u0e49าโลก?)");
				yield break;
			}
			if (text == "open")
			{
				WarpRushGroup.SeasonPassMode = true;
				warpRushGroup.Open();
				yield return new WaitForSeconds(2.5f);
			}
			else
			{
				warpRushGroup.Close();
				yield return new WaitForSeconds(0.5f);
			}
			r.Set("opened", warpRushGroup.IsOpened);
			break;
		}
		case "claim":
		{
			if (args.Length <= 2 || args[2] == "all" || !int.TryParse(args[2], out var result))
			{
				result = 0;
			}
			CommunitySeasonPass.Claim(result);
			yield return new WaitForSeconds(2.5f);
			break;
		}
		case "missions":
			r.Set("lines", new List<object>(CommunitySeasonPass.MissionLines()));
			break;
		}
		CommunitySeasonPass.Status current = CommunitySeasonPass.Current;
		r.Set("available", CommunitySeasonPass.Available).Set("last", CommunitySeasonPass.LastEvent).Set("claimed", CommunitySeasonPass.LastClaimed)
			.Set("mode", WarpRushGroup.SeasonPassMode);
		if (current == null)
		{
			r.Set("status", "none");
			yield break;
		}
		r.Set("active", current.Active).Set("season", current.SeasonId).Set("name", current.SeasonName)
			.Set("points", current.Points)
			.Set("tier", current.Tier)
			.Set("seasonLevel", current.SeasonLevel)
			.Set("expPerPoint", current.ExpPerPoint)
			.Set("expDailyPoints", current.ExpDailyPoints)
			.Set("expPointsToday", current.ExpPointsToday)
			.Set("tierPoints", current.TierPoints)
			.Set("seasonRatio", CommunitySeasonPass.SeasonRatio(current))
			.Set("seasonHud", CommunitySeasonPass.UseSeasonHud)
			.Set("hudText", CommunitySeasonPass.SeasonHudText)
			.Set("quotaText", CommunitySeasonPass.ExpQuotaText(current))
			.Set("fetchCount", CommunitySeasonPass.FetchCount)
			.Set("maxTier", current.MaxTier)
			.Set("pointsPerTier", current.PointsPerTier)
			.Set("claimable", current.Claimable)
			.Set("until", current.Until);
		List<object> list = new List<object>();
		foreach (CommunitySeasonPass.Tier tier in current.Tiers)
		{
			list.Add(Dict("tier", tier.Number, "state", tier.State, "box", tier.Box.Items.Count, "special", (tier.Special.Items.Count > 0 && tier.Special.Items[0].Item != null) ? tier.Special.Items[0].Item.PrototypeId : string.Empty));
		}
		r.Set("tiers", list);
		List<object> list2 = new List<object>();
		foreach (CommunitySeasonPass.Mission mission in current.Daily.Missions)
		{
			list2.Add(Dict("id", mission.Id, "progress", mission.Progress, "count", mission.Count, "done", mission.Done));
		}
		foreach (CommunitySeasonPass.Mission mission2 in current.Weekly.Missions)
		{
			list2.Add(Dict("id", mission2.Id, "progress", mission2.Progress, "count", mission2.Count, "done", mission2.Done));
		}
		r.Set("missions", list2);
	}

	private IEnumerator CmdDiscordLink(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "status");
		switch (text)
		{
		case "start":
			CommunityDiscordLink.Begin(openBrowser: false);
			break;
		case "click":
			ConfigInstance.NotifyAction("discord_link");
			break;
		case "cancel":
			CommunityDiscordLink.Cancel();
			break;
		}
		if (text != "status")
		{
			yield return new WaitForSeconds((args.Length > 2) ? (ParseFloat(args[2], 1500f) / 1000f) : 1.5f);
		}
		r.Set("busy", CommunityDiscordLink.Busy).Set("port", CommunityDiscordLink.Port).Set("url", CommunityDiscordLink.LastAuthorizeUrl)
			.Set("last", CommunityDiscordLink.LastEvent)
			.Set("label", CommunityDiscordLink.ButtonLabel)
			.Set("discord_name", CommunityLink.DiscordName ?? string.Empty)
			.Set("client_version", CommunityLink.ClientVersion)
			.Set("server_version", CommunityLink.LastServerVersion)
			.Set("caller", CommunityDiscordLink.LastCaller);
	}

	private IEnumerator CmdMoveCode(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "status");
		if (!(text == "click"))
		{
			if (text == "redeem")
			{
				if (args.Length < 3)
				{
					r.Fail("usage: movecode redeem <code>");
					yield break;
				}
				ConfigInstance.SendCoupon(string.Join(" ", args, 2, args.Length - 2));
			}
		}
		else
		{
			CommunityMoveDevice.LastCode = string.Empty;
			ConfigInstance.NotifyAction("move_device");
		}
		if (text != "status")
		{
			yield return new WaitForSeconds(2f);
		}
		r.Set("code", CommunityMoveDevice.LastCode).Set("last", CommunityMoveDevice.LastEvent).Set("label", CommunityMoveDevice.ButtonLabel)
			.Set("machine_key", CommunityLink.AccountId);
	}

	private IEnumerator CmdHelp(string[] args, Result r)
	{
		List<object> list = new List<object>();
		foreach (KeyValuePair<string, string> item in _help)
		{
			list.Add(item.Key + " — " + item.Value);
		}
		list.Sort((object a, object b) => string.CompareOrdinal((string)a, (string)b));
		r.Set("commands", list);
		yield break;
	}

	private IEnumerator CmdVoice(string[] args, Result r)
	{
		string[] array = new string[Math.Max(0, args.Length - 1)];
		Array.Copy(args, 1, array, 0, array.Length);
		string text = CommunityVoice.DebugCommand(array);
		if (text != null)
		{
			r.Set("done", text);
		}
		foreach (KeyValuePair<string, object> item in CommunityVoice.DebugState())
		{
			r.Set(item.Key, item.Value);
		}
		yield break;
	}

	private IEnumerator CmdDodo(string[] args, Result r)
	{
		string[] array = new string[Math.Max(0, args.Length - 1)];
		Array.Copy(args, 1, array, 0, array.Length);
		string text = CommunityDodoFight.DebugCommand(array);
		if (text != null)
		{
			r.Set("done", text);
		}
		foreach (KeyValuePair<string, object> item in CommunityDodoFight.DebugState())
		{
			r.Set(item.Key, item.Value);
		}
		yield break;
	}

	private IEnumerator CmdRiftCompass(string[] args, Result r)
	{
		foreach (KeyValuePair<string, object> item in RiftCompassHud.DebugCommand((args.Length > 1) ? args[1] : "state"))
		{
			r.Set(item.Key, item.Value);
		}
		yield break;
	}

	private IEnumerator CmdRiftScope(string[] args, Result r)
	{
		string[] array = new string[Math.Max(0, args.Length - 1)];
		Array.Copy(args, 1, array, 0, array.Length);
		string text = RiftScope.DebugCommand(array);
		if (text != null)
		{
			r.Set("done", text);
		}
		foreach (KeyValuePair<string, object> item in RiftScope.DebugState(args.Length > 1 && args[1] == "inspect"))
		{
			r.Set(item.Key, item.Value);
		}
		yield break;
	}

	private IEnumerator CmdPing(string[] args, Result r)
	{
		r.Set("pong", true).Set("time", Time.time).Set("frame", Time.frameCount);
		yield break;
	}

	private IEnumerator CmdUdp(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "status");
		Connection frontendIfAny = Connections.FrontendIfAny;
		switch (text)
		{
		case "fallback":
			frontendIfAny?.UdpForceFallback("testbridge");
			break;
		case "hello":
			if (frontendIfAny != null && frontendIfAny.Udp != null)
			{
				frontendIfAny.Udp.RequestHelloNow();
			}
			break;
		case "unblock":
			CommunityUdp.ClearBlock();
			break;
		case "sim":
			if (frontendIfAny != null && frontendIfAny.Udp != null)
			{
				frontendIfAny.Udp.SimLossPct = Mathf.Clamp((int)((args.Length > 2) ? ParseFloat(args[2], 0f) : 0f), 0, 100);
				frontendIfAny.Udp.SimDelayMs = Mathf.Clamp((int)((args.Length > 3) ? ParseFloat(args[3], 0f) : 0f), 0, 5000);
			}
			break;
		case "tcp":
		case "auto":
			ConfigInstance.ChangeValue("net_transport", text);
			break;
		}
		CommunityUdpTransport communityUdpTransport = frontendIfAny?.Udp;
		CommunityUdp.Offer pending = CommunityUdp.Pending;
		r.Set("setting", CommunityUdp.IsTcpOnly ? "tcp" : "auto");
		r.Set("blocked_sec", CommunityUdp.IsBlocked(out var remainSec) ? ((int)remainSec) : 0);
		r.Set("entry", CommunityUdp.LastEntryNote);
		r.Set("offer", (pending != null) ? ("port " + pending.Port + " ttl " + pending.TtlSec + (pending.Used ? " used" : " unused")) : "none");
		r.Set("tcp_connected", frontendIfAny?.Connected() ?? false);
		r.Set("phase", (communityUdpTransport != null) ? communityUdpTransport.Current.ToString() : "none");
		r.Set("active", communityUdpTransport?.Active ?? false);
		if (communityUdpTransport != null)
		{
			r.Set("target", communityUdpTransport.Target.ToString()).Set("rtt_ms", communityUdpTransport.RttMs).Set("up_sec", (int)communityUdpTransport.UpSeconds);
			r.Set("frames_sent", communityUdpTransport.FramesSent).Set("frames_recv", communityUdpTransport.FramesRecv);
			r.Set("bytes_sent", communityUdpTransport.BytesSent).Set("bytes_recv", communityUdpTransport.BytesRecv);
			r.Set("unacked", communityUdpTransport.Unacked).Set("held", communityUdpTransport.Held).Set("replayed", communityUdpTransport.Replayed)
				.Set("bad_recv", communityUdpTransport.BadRecv);
			r.Set("packet_loss", communityUdpTransport.PacketLoss).Set("fallbacks", communityUdpTransport.Fallbacks).Set("last_reason", communityUdpTransport.LastReason);
			r.Set("peer_channels", communityUdpTransport.PeerChannels).Set("ch2_sent", communityUdpTransport.Ch2Sent).Set("ch2_recv", communityUdpTransport.Ch2Recv);
			r.Set("move_sent", communityUdpTransport.MoveSent).Set("move_pending", communityUdpTransport.MovePending).Set("move_pending_tries", communityUdpTransport.MovePendingTries);
			r.Set("move_resends", communityUdpTransport.MoveResends).Set("move_escalated", communityUdpTransport.MoveEscalated).Set("move_acks", communityUdpTransport.MoveAcks)
				.Set("move_ack_tseq", (long)communityUdpTransport.MoveAckTseq);
			r.Set("hello_armed", communityUdpTransport.HelloArmed).Set("next_hello_sec", communityUdpTransport.NextHelloInSec).Set("hellos", communityUdpTransport.Hellos)
				.Set("offers", communityUdpTransport.Offers)
				.Set("last_offer", communityUdpTransport.LastOfferNote);
			r.Set("sim_loss_pct", communityUdpTransport.SimLossPct).Set("sim_delay_ms", communityUdpTransport.SimDelayMs).Set("sim_dropped", communityUdpTransport.SimDropped)
				.Set("sim_delayed", communityUdpTransport.SimDelayed);
		}
		yield break;
	}

	private static bool InWorld()
	{
		if (PlayerBehavior.LocalPlayer != null)
		{
			return !GameManager.IsTitleScene;
		}
		return false;
	}

	private static bool UiReady()
	{
		if (Durango.Utils.Singleton<UIManager>.HasInstance() && !GameManager.IsTitleScene && !GameManager.IsSceneClosing)
		{
			return PlayerBehavior.LocalPlayer != null;
		}
		return false;
	}

	private static Vector2 PlayerTile()
	{
		return Durango.Terrain.Util.ClientPositionToTilePosition(PlayerBehavior.LocalPlayer.CurrentPosition);
	}

	private IEnumerator CmdState(string[] args, Result r)
	{
		r.Set("scene", SceneManager.GetActiveScene().name);
		r.Set("title", GameManager.IsTitleScene);
		r.Set("loading", Durango.Utils.Singleton<UIManager>.HasInstance() && UIManager.IsLoadingCurtain);
		r.Set("uiReady", UiReady());
		r.Set("connected", Connections.Frontend != null && Connections.Frontend.Connected());
		r.Set("playerId", GameManager.PlayerId);
		if (GameManager.Region != null)
		{
			r.Set("region", Dict("id", GameManager.Region.Id, "name", GameManager.Region.Name, "template", GameManager.Region.TemplateId, "role", GameManager.Region.Role().ToString()));
		}
		if (InWorld())
		{
			PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
			Vector2 vector = PlayerTile();
			r.Set("tile", Dict("x", (int)vector.x, "y", (int)vector.y));
			r.Set("pos", Dict("x", localPlayer.CurrentPosition.x, "y", localPlayer.CurrentPosition.y, "z", localPlayer.CurrentPosition.z));
			r.Set("alive", localPlayer.IsAlive);
			r.Set("moving", (bool)localPlayer.IsMoving);
			r.Set("tired", localPlayer.IsTired);
			r.Set("riding", localPlayer.IsRiding);
			r.Set("gauges", GaugeDict(localPlayer));
			r.Set("effects", EffectList());
			r.Set("guide", GuideSummary());
			r.Set("dialog", DialogInfo());
		}
		yield break;
	}

	private static Dictionary<string, object> GaugeDict(CharacterBehavior p)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		for (int i = 0; i < GaugeKeys.Length; i++)
		{
			Gauge gauge = p.GetGauge(GaugeKeys[i]);
			if (gauge != null)
			{
				dictionary[GaugeKeys[i]] = Dict("value", gauge.Get(), "max", gauge.Max(), "velocity", gauge.Velocity(Connections.Frontend.GetBufferedServerTime() + 1.0));
			}
		}
		return dictionary;
	}

	private IEnumerator CmdGauges(string[] args, Result r)
	{
		if (!InWorld())
		{
			r.Fail("not in world");
		}
		else
		{
			r.Set("gauges", GaugeDict(PlayerBehavior.LocalPlayer));
		}
		yield break;
	}

	private static List<object> EffectList()
	{
		List<object> list = new List<object>();
		Durango.Logic.StatusEffects statusEffects = GameSystem<StatusEffectSystem>.Instance().GetStatusEffects();
		if (statusEffects != null)
		{
			foreach (Durango.Logic.StatusEffect item in statusEffects.List)
			{
				list.Add(Dict("id", item.Id, "level", item.Level, "name", item.Name, "until", item.Until));
			}
		}
		return list;
	}

	private IEnumerator CmdEffects(string[] args, Result r)
	{
		r.Set("effects", EffectList());
		yield break;
	}

	private static Dictionary<string, object> ItemDict(ItemData item)
	{
		List<object> list = new List<object>();
		foreach (TagData tag in item.Tags)
		{
			list.Add(tag.Id);
		}
		return Dict("id", item.Id, "prototype", item.PrototypeId, "name", item.Name, "level", item.Level, "equipment", item.IsEquipments, "size", item.Size, "tags", list);
	}

	private IEnumerator CmdInventory(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1] : null);
		List<object> list = new List<object>();
		foreach (ItemData playerItem in GameSystem<InventorySystem>.Instance().PlayerItemList)
		{
			if (text == null || Matches(playerItem, text))
			{
				list.Add(ItemDict(playerItem));
			}
		}
		r.Set("items", list).Set("count", list.Count);
		yield break;
	}

	private static bool Matches(ItemData item, string filter)
	{
		if (filter.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
		{
			return item.HasTag(filter.Substring(4));
		}
		if (!string.Equals(item.Id, filter, StringComparison.OrdinalIgnoreCase) && !string.Equals(item.PrototypeId, filter, StringComparison.OrdinalIgnoreCase))
		{
			if (item.Name != null)
			{
				return item.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			return false;
		}
		return true;
	}

	private static ItemData FindItem(string filter)
	{
		foreach (ItemData playerItem in GameSystem<InventorySystem>.Instance().PlayerItemList)
		{
			if (Matches(playerItem, filter))
			{
				return playerItem;
			}
		}
		return null;
	}

	private IEnumerator CmdUse(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("use <item>");
			yield break;
		}
		ItemData item = FindItem(args[1]);
		if (item == null)
		{
			r.Fail("ไม\u0e48พบไอเทม " + args[1]);
			yield break;
		}
		bool done = false;
		GameSystem<InventorySystem>.Instance().UseItem(item, playerAccepted: true, delegate
		{
			done = true;
		});
		float until = Time.time + 5f;
		while (!done && Time.time < until)
		{
			yield return null;
		}
		r.Set("item", ItemDict(item)).Set("succeeded", done);
	}

	private IEnumerator CmdEquip(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("equip <item>");
			yield break;
		}
		ItemData item = FindItem(args[1]);
		if (item == null)
		{
			r.Fail("ไม\u0e48พบไอเทม " + args[1]);
			yield break;
		}
		bool replied = false;
		GameSystem<EquipSystem>.Instance().EquipItem(item);
		float until = Time.time + 3f;
		while (!replied && Time.time < until)
		{
			replied = item.IsEquipments;
			yield return null;
		}
		r.Set("item", ItemDict(item));
	}

	private IEnumerator CmdDrop(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("drop <item>");
			yield break;
		}
		ItemData item = FindItem(args[1]);
		if (item == null)
		{
			r.Fail("ไม\u0e48พบไอเทม " + args[1]);
			yield break;
		}
		InventorySystem.DropItems(InventorySystem.MakeDumpItemsPacket(GameSystem<InventorySystem>.Instance().PlayerInventory, new string[1] { item.Id }));
		yield return new WaitForSeconds(0.5f);
		r.Set("dropped", item.Id);
	}

	private static Dictionary<string, object> ObjectDict(GameObject go)
	{
		Vector2 vector = Durango.Terrain.Util.ClientPositionToTilePosition(go.transform.position);
		string text = go.name;
		ImmovableBase component = go.GetComponent<ImmovableBase>();
		if (component != null)
		{
			text = component.GetName();
		}
		SelectableObject component2 = go.GetComponent<SelectableObject>();
		if (component2 != null)
		{
			text = component2.GetName();
		}
		return Dict("entityId", ObjectIdentifier.GetEntityId(go), "entityType", ObjectIdentifier.GetEntityType(go), "name", text, "object", go.name, "tile", Dict("x", (int)vector.x, "y", (int)vector.y), "distance", Vector3.Distance(go.transform.position, PlayerBehavior.LocalPlayer.CurrentPosition));
	}

	private static List<GameObject> SearchNear(string kind, float radius)
	{
		List<GameObject> list = new List<GameObject>();
		if (kind == "prop" || kind == "all")
		{
			InteractionSystem.GetNearObjectsInternal(list, LayerHelper.PropMask, radius, InteractionSystem.PropInteractionObjectFilter);
		}
		if (kind == "movable" || kind == "all")
		{
			InteractionSystem.GetNearObjectsInternal(list, LayerHelper.InteractionMask, radius, InteractionSystem.MovableInteractionObjectFilter);
		}
		return list;
	}

	private IEnumerator CmdNear(string[] args, Result r)
	{
		if (!InWorld())
		{
			r.Fail("not in world");
			yield break;
		}
		string kind = ((args.Length > 1) ? args[1].ToLowerInvariant() : "all");
		float radius = ((args.Length > 2) ? ParseFloat(args[2], 1600f) : 1600f);
		List<object> list = new List<object>();
		foreach (GameObject item in SearchNear(kind, radius))
		{
			list.Add(ObjectDict(item));
		}
		list.Sort((object a, object b) => ((float)((Dictionary<string, object>)a)["distance"]).CompareTo((float)((Dictionary<string, object>)b)["distance"]));
		r.Set("objects", list).Set("count", list.Count);
	}

	private static GameObject FindObject(string key, float radius)
	{
		GameObject result = null;
		float num = float.MaxValue;
		int result2;
		bool flag = int.TryParse(key, out result2);
		foreach (GameObject item in SearchNear("all", radius))
		{
			bool flag2 = string.Equals(ObjectIdentifier.GetEntityId(item), key, StringComparison.OrdinalIgnoreCase) || (flag && ObjectIdentifier.GetEntityType(item) == result2);
			if (!flag2)
			{
				ImmovableBase component = item.GetComponent<ImmovableBase>();
				SelectableObject component2 = item.GetComponent<SelectableObject>();
				string text = ((component != null) ? component.GetName() : ((component2 != null) ? component2.GetName() : item.name));
				flag2 = (text != null && text.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) || item.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			if (flag2)
			{
				float num2 = Vector3.Distance(item.transform.position, PlayerBehavior.LocalPlayer.CurrentPosition);
				if (num2 < num)
				{
					num = num2;
					result = item;
				}
			}
		}
		return result;
	}

	private static List<object> MenuList()
	{
		List<object> list = new List<object>();
		InteractionMenuList menuList = GameSystem<InteractionSystem>.Instance().MenuList;
		for (int i = 0; i < menuList.Count; i++)
		{
			InteractionMenuData interactionMenuData = menuList[i];
			list.Add(Dict("index", i, "action", interactionMenuData.Action.ToString(), "id", interactionMenuData.Id, "name", interactionMenuData.Name, "count", interactionMenuData.Count, "disabled", interactionMenuData.Disabled, "denied", interactionMenuData.AccessDenied));
		}
		return list;
	}

	private IEnumerator CmdTouch(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("touch <entityId|entityType|name>");
			yield break;
		}
		GameObject go = FindObject(args[1], 4000f);
		if (go == null)
		{
			r.Fail("ไม\u0e48พบว\u0e31ตถ\u0e38 " + args[1] + " ในระยะ");
			yield break;
		}
		InteractionSystem interactionSystem = GameSystem<InteractionSystem>.Instance();
		bool updated = false;
		Action onUpdated = delegate
		{
			updated = true;
		};
		interactionSystem.MenuList.Updated += onUpdated;
		interactionSystem.SetInteractionTarget(new InteractionObject(go));
		float num = ((args.Length > 2) ? (ParseFloat(args[2], 2000f) / 1000f) : 2f);
		float until = Time.time + num;
		while (!updated && Time.time < until)
		{
			yield return null;
		}
		yield return null;
		interactionSystem.MenuList.Updated -= onUpdated;
		r.Set("target", ObjectDict(go)).Set("menu", MenuList()).Set("menuName", interactionSystem.MenuList.Name);
	}

	private IEnumerator CmdMenu(string[] args, Result r)
	{
		InteractionSystem interactionSystem = GameSystem<InteractionSystem>.Instance();
		InteractionObject target = interactionSystem.Target;
		if (target != null && target.Target != null)
		{
			r.Set("target", ObjectDict(target.Target));
		}
		r.Set("menu", MenuList()).Set("menuName", interactionSystem.MenuList.Name);
		yield break;
	}

	private IEnumerator CmdSelect(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("select <Interaction|index> [id]");
			yield break;
		}
		InteractionSystem interactionSystem = GameSystem<InteractionSystem>.Instance();
		InteractionMenuList menuList = interactionSystem.MenuList;
		if (!int.TryParse(args[1], out var result))
		{
			result = -1;
			for (int i = 0; i < menuList.Count; i++)
			{
				InteractionMenuData interactionMenuData = menuList[i];
				bool num = string.Equals(interactionMenuData.Action.ToString(), args[1], StringComparison.OrdinalIgnoreCase);
				bool flag = interactionMenuData.Name != null && interactionMenuData.Name.IndexOf(args[1], StringComparison.OrdinalIgnoreCase) >= 0;
				bool flag2 = args.Length < 3 || string.Equals(interactionMenuData.Id, args[2], StringComparison.OrdinalIgnoreCase);
				if ((num || flag) && flag2)
				{
					result = i;
					break;
				}
			}
		}
		if (result < 0 || result >= menuList.Count)
		{
			r.Fail("ไม\u0e48ม\u0e35เมน\u0e39 " + args[1]).Set("menu", MenuList());
			yield break;
		}
		InteractionMenuData menu = menuList[result];
		interactionSystem.SelectTargetInteractionMenu(menu);
		yield return new WaitForSeconds(0.3f);
		r.Set("selected", Dict("action", menu.Action.ToString(), "id", menu.Id, "name", menu.Name));
	}

	private static List<InteractionMenuData> ContextActions()
	{
		List<InteractionMenuData> result = new List<InteractionMenuData>();
		GameSystem<InteractionSystem>.Instance().GetContextActionList(result);
		return result;
	}

	private IEnumerator CmdContext(string[] args, Result r)
	{
		List<object> list = new List<object>();
		foreach (InteractionMenuData item in ContextActions())
		{
			list.Add(Dict("action", item.Action.ToString(), "id", item.Id, "name", item.Name, "disabled", item.Disabled));
		}
		r.Set("actions", list);
		yield break;
	}

	private IEnumerator CmdDo(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("do <Interaction>");
			yield break;
		}
		InteractionMenuData? found = null;
		foreach (InteractionMenuData item in ContextActions())
		{
			if (string.Equals(item.Action.ToString(), args[1], StringComparison.OrdinalIgnoreCase) || (item.Name != null && item.Name.IndexOf(args[1], StringComparison.OrdinalIgnoreCase) >= 0))
			{
				found = item;
				break;
			}
		}
		if (!found.HasValue)
		{
			Interaction action;
			try
			{
				action = (Interaction)Enum.Parse(typeof(Interaction), args[1], ignoreCase: true);
			}
			catch
			{
				r.Fail("ไม\u0e48ร\u0e39\u0e49จ\u0e31ก Interaction " + args[1]);
				yield break;
			}
			found = new InteractionMenuData(action);
		}
		GameSystem<InteractionSystem>.Instance().DoNoneTargetAction(found.Value);
		yield return new WaitForSeconds(0.3f);
		r.Set("action", found.Value.Action.ToString());
	}

	private IEnumerator MoveAndWait(Vector3 position, GameObject target, float timeout, Result r)
	{
		bool done = false;
		Action onComplete = delegate
		{
			done = true;
		};
		Durango.Utils.Singleton<PlayerController>.Instance().TestBridgeMoveTo(position, target, onComplete);
		float until = Time.time + timeout;
		while (!done && Time.time < until)
		{
			yield return null;
		}
		Vector2 vector = PlayerTile();
		r.Set("arrived", done).Set("tile", Dict("x", (int)vector.x, "y", (int)vector.y));
		if (!done)
		{
			r.Fail("timeout — ย\u0e31งไม\u0e48ถ\u0e36ง (" + (int)vector.x + "," + (int)vector.y + ")");
		}
	}

	private IEnumerator CmdMove(string[] args, Result r)
	{
		if (!InWorld() || args.Length < 3)
		{
			r.Fail("move <tileX> <tileY> [timeoutMs]");
			yield break;
		}
		float x = ParseFloat(args[1], 0f);
		float y = ParseFloat(args[2], 0f);
		float timeout = ((args.Length > 3) ? (ParseFloat(args[3], 60000f) / 1000f) : 60f);
		Vector3 position = Durango.Terrain.Util.TilePositionToClientPosition(new Vector2(x, y), tileCenter: true);
		IEnumerator routine = MoveAndWait(position, null, timeout, r);
		while (routine.MoveNext())
		{
			yield return routine.Current;
		}
	}

	private IEnumerator CmdMoveTo(string[] args, Result r)
	{
		if (!InWorld() || args.Length < 2)
		{
			r.Fail("moveto <entityId|name> [timeoutMs]");
			yield break;
		}
		GameObject go = FindObject(args[1], 6000f);
		if (go == null)
		{
			r.Fail("ไม\u0e48พบว\u0e31ตถ\u0e38 " + args[1]);
			yield break;
		}
		float timeout = ((args.Length > 2) ? (ParseFloat(args[2], 60000f) / 1000f) : 60f);
		IEnumerator routine = MoveAndWait(Vector3.zero, go, timeout, r);
		while (routine.MoveNext())
		{
			yield return routine.Current;
		}
		r.Set("target", ObjectDict(go));
	}

	private IEnumerator CmdNav(string[] args, Result r)
	{
		NavGrid.EnsureLoaded();
		r.Set("ready", NavGrid.Ready).Set("size", NavGrid.Size).Set("error", NavGrid.LastError);
		if (args.Length >= 4 && args[1] == "path")
		{
			if (!InWorld())
			{
				r.Fail("not in world");
				yield break;
			}
			Vector3 toClient = Durango.Terrain.Util.TilePositionToClientPosition(new Vector2(ParseFloat(args[2], 0f), ParseFloat(args[3], 0f)), tileCenter: true);
			List<Vector3> list = NavGrid.FindWaypoints(PlayerBehavior.LocalPlayer.CurrentPosition, toClient);
			List<object> list2 = new List<object>();
			if (list != null)
			{
				foreach (Vector3 item in list)
				{
					Vector2 vector = Durango.Terrain.Util.ClientPositionToTilePosition(item);
					list2.Add((int)vector.x + "," + (int)vector.y);
				}
			}
			List<object> list3 = new List<object>();
			if (NavGrid.LastPathTiles != null)
			{
				foreach (Vector2 lastPathTile in NavGrid.LastPathTiles)
				{
					list3.Add((int)lastPathTile.x + "," + (int)lastPathTile.y);
				}
			}
			r.Set("found", list != null).Set("waypoints", list2).Set("tiles", list3);
		}
		else if (args.Length >= 3)
		{
			int num = (int)ParseFloat(args[1], 0f);
			int num2 = (int)ParseFloat(args[2], 0f);
			r.Set("tile", Dict("x", num, "y", num2)).Set("cost", NavGrid.CostAt(num, num2));
		}
	}

	private IEnumerator CmdNpc(string[] args, Result r)
	{
		if (args.Length >= 2 && args[1] == "respawn")
		{
			NpcSpawner.SpawnAll();
			r.Set("respawned", true);
			yield break;
		}
		if (args.Length >= 3 && args[1] == "clips")
		{
			int num = (int)ParseFloat(args[2], 0f);
			List<GameObject> instances = NpcSpawner.Instances;
			if (num < 0 || num >= instances.Count || instances[num] == null)
			{
				r.Set("error", "ไม\u0e48ม\u0e35 NPC ลำด\u0e31บ " + num);
				yield break;
			}
			CharacterBehavior component = instances[num].GetComponent<CharacterBehavior>();
			List<object> list = new List<object>();
			if (component != null && component.Anim != null)
			{
				foreach (AnimationState item in component.Anim)
				{
					list.Add(item.name);
				}
			}
			r.Set("object", instances[num].name).Set("clips", list).Set("count", list.Count);
			yield break;
		}
		if (args.Length >= 3 && args[1] == "info")
		{
			int num2 = (int)ParseFloat(args[2], 0f);
			List<GameObject> instances2 = NpcSpawner.Instances;
			if (num2 < 0 || num2 >= instances2.Count || instances2[num2] == null)
			{
				r.Set("error", "ไม\u0e48ม\u0e35 NPC ลำด\u0e31บ " + num2);
				yield break;
			}
			GameObject gameObject = instances2[num2];
			CharacterBehavior component2 = gameObject.GetComponent<CharacterBehavior>();
			List<object> list2 = new List<object>();
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				list2.Add(Dict("name", renderer.gameObject.name, "enabled", renderer.enabled, "active", renderer.gameObject.activeInHierarchy, "layer", renderer.gameObject.layer, "visible", renderer.isVisible, "bounds", renderer.bounds.center.ToString("F0") + " " + renderer.bounds.size.ToString("F0")));
			}
			Vector3 position = gameObject.transform.position;
			float? worldHeight = LocalMoveOperator.GetWorldHeight(position, 0, 0f);
			List<object> list3 = new List<object>();
			Transform[] componentsInChildren2 = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren2)
			{
				int num3 = 0;
				Transform transform2 = transform;
				while (transform2 != gameObject.transform && transform2 != null)
				{
					num3++;
					transform2 = transform2.parent;
				}
				if (num3 <= 2)
				{
					list3.Add(num3 + ":" + transform.name + " " + transform.position.ToString("F0") + ((transform.GetComponent<Animation>() != null) ? " [Animation]" : ""));
				}
			}
			Animation animation = ((component2 != null) ? component2.Anim : null);
			r.Set("object", gameObject.name).Set("active", gameObject.activeInHierarchy).Set("pos", position.ToString("F0"))
				.Set("scale", gameObject.transform.lossyScale.ToString("F2"))
				.Set("current", (component2 != null) ? component2.CurrentPosition.ToString("F0") : "")
				.Set("ground", worldHeight.HasValue ? worldHeight.Value.ToString("F0") : "null")
				.Set("animOn", (animation != null) ? animation.gameObject.name : "null")
				.Set("animClip", (animation != null && animation.clip != null) ? animation.clip.name : "null")
				.Set("tree", list3)
				.Set("renderers", list2);
			yield break;
		}
		if (args.Length >= 4 && args[1] == "act")
		{
			int num4 = (int)ParseFloat(args[2], 0f);
			List<GameObject> instances3 = NpcSpawner.Instances;
			if (num4 < 0 || num4 >= instances3.Count || instances3[num4] == null)
			{
				r.Set("error", "ไม\u0e48ม\u0e35 NPC ลำด\u0e31บ " + num4);
				yield break;
			}
			NpcWanderAI component3 = instances3[num4].GetComponent<NpcWanderAI>();
			if (component3 == null)
			{
				r.Set("error", "ไม\u0e48ม\u0e35 NpcWanderAI");
			}
			else
			{
				r.Set("played", component3.ForceActivity(args[3]));
			}
			yield break;
		}
		if (args.Length >= 3 && args[1] == "talk")
		{
			int num5 = (int)ParseFloat(args[2], 0f);
			Connections.Frontend.Send(new InteractWithEpicNPC
			{
				Npc = (EpicNPCType)num5,
				ItemIds = new string[0]
			});
			r.Set("sent", num5);
			yield break;
		}
		List<object> list4 = new List<object>();
		foreach (GameObject instance in NpcSpawner.Instances)
		{
			if (!(instance == null))
			{
				Vector2 vector = Durango.Terrain.Util.ClientPositionToTilePosition(instance.transform.position);
				List<object> list5 = new List<object>();
				Component[] components = instance.GetComponents<Component>();
				foreach (Component component4 in components)
				{
					list5.Add(component4.GetType().Name);
				}
				AnimalBehavior component5 = instance.GetComponent<AnimalBehavior>();
				string text = ((component5 != null && component5.CurAnimState != null) ? component5.CurAnimState.name : "");
				list4.Add(Dict("object", instance.name, "tile", (int)vector.x + "," + (int)vector.y, "clip", text, "components", list5));
			}
		}
		r.Set("npcs", list4).Set("count", list4.Count);
	}

	private IEnumerator CmdOverride(string[] args, Result r)
	{
		string text = ((args.Length >= 2) ? args[1] : "PlayGuide/tutorial_play_guide_flow");
		string value = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "override");
		r.Set("dataPath", Application.dataPath).Set("overrideDir", value).Set("file", text)
			.Set("found", Durango.Utils.Json.OverridePath(text));
		yield break;
	}

	private IEnumerator CmdTriggers(string[] args, Result r)
	{
		List<object> list = new List<object>();
		PlayerTriggerGuide[] array = UnityEngine.Object.FindObjectsByType<PlayerTriggerGuide>();
		foreach (PlayerTriggerGuide playerTriggerGuide in array)
		{
			Collider component = playerTriggerGuide.GetComponent<Collider>();
			Dictionary<string, object> dictionary = Dict("object", playerTriggerGuide.gameObject.name, "flow", playerTriggerGuide.TestBridgeFlowName());
			Vector2 vector = Durango.Terrain.Util.ClientPositionToTilePosition(playerTriggerGuide.transform.position);
			dictionary["tile"] = (int)vector.x + "," + (int)vector.y;
			if (component != null)
			{
				Bounds bounds = component.bounds;
				Vector2 vector2 = Durango.Terrain.Util.ClientPositionToTilePosition(bounds.min);
				Vector2 vector3 = Durango.Terrain.Util.ClientPositionToTilePosition(bounds.max);
				dictionary["bounds"] = (int)vector2.x + "," + (int)vector2.y + " - " + (int)vector3.x + "," + (int)vector3.y;
				dictionary["sizeTiles"] = (bounds.size.x / 200f).ToString("0.0") + "x" + (bounds.size.z / 200f).ToString("0.0");
			}
			list.Add(dictionary);
		}
		r.Set("triggers", list).Set("count", list.Count);
		yield break;
	}

	private IEnumerator CmdStop(string[] args, Result r)
	{
		Durango.Utils.Singleton<PlayerController>.Instance().StopMove();
		yield break;
	}

	private IEnumerator CmdRecipes(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1] : null);
		List<object> list = new List<object>();
		foreach (Crafting.Category category in GameSystem<RecipeSystem>.Instance().RecipeContainer.Categories)
		{
			foreach (Crafting.Recipe recipe in category.Recipes)
			{
				if (recipe != null && (text == null || recipe.Id.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || (recipe.Name != null && recipe.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)))
				{
					list.Add(Dict("id", recipe.Id, "name", recipe.Name, "category", category.Id));
				}
			}
		}
		r.Set("recipes", list).Set("count", list.Count);
		yield break;
	}

	private IEnumerator CmdWorkbench(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("workbench <entityId> [filter]");
			yield break;
		}
		Artifact artifact = Durango.Utils.Singleton<ArtifactManager>.Instance().Find(args[1]);
		if (artifact == null)
		{
			r.Fail("artifact not found: " + args[1]);
			yield break;
		}
		string value = ((args.Length > 2) ? args[2] : "dgs02");
		List<object> list = new List<object>();
		foreach (TagData tag in artifact.Tags)
		{
			list.Add(Dict("id", tag.Id, "level", tag.Level));
		}
		Gauge durability = artifact.Durability;
		List<object> list2 = new List<object>();
		foreach (Crafting.Category category in GameSystem<RecipeSystem>.Instance().RecipeContainer.Categories)
		{
			foreach (Crafting.Recipe recipe in category.Recipes)
			{
				if (recipe != null && recipe.Id.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					List<object> list3 = new List<object>();
					for (int i = 0; i < recipe.AllowedWorkbench.Length; i++)
					{
						list3.Add(recipe.AllowedWorkbench[i].Id + ":" + recipe.AllowedWorkbench[i].Level);
					}
					list2.Add(Dict("id", recipe.Id, "category", category.Id, "available", recipe.Available, "needs", list3, "valid", recipe.IsValidWorkbench(artifact)));
				}
			}
		}
		r.Set("entityType", artifact.EntityType).Set("tags", list).Set("durability", (durability == null) ? null : Dict("value", durability.Get(), "min", durability.Min(), "max", durability.Max()))
			.Set("recipes", list2)
			.Set("count", list2.Count);
	}

	private IEnumerator CmdCraft(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("craft <recipeId> [qty]");
			yield break;
		}
		Crafting.Recipe recipe = GameSystem<RecipeSystem>.Instance().RecipeContainer.GetRecipe(args[1]);
		if (recipe == null)
		{
			r.Fail("ไม\u0e48ร\u0e39\u0e49จ\u0e31กส\u0e39ตร " + args[1]);
			yield break;
		}
		CraftSystem craftSystem = GameSystem<CraftSystem>.Instance();
		CraftSlotContainer slotContainer = craftSystem.SlotContainer;
		slotContainer.Set(recipe, null, GameSystem<InventorySystem>.Instance().PlayerInventory, null);
		slotContainer.QuickFill();
		int b = ((args.Length <= 2) ? 1 : ((int)ParseFloat(args[2], 1f)));
		slotContainer.SetQuantity(Mathf.Max(1, b));
		List<object> materials = new List<object>();
		foreach (ItemData selectedMaterial in slotContainer.GetSelectedMaterials())
		{
			materials.Add(ItemDict(selectedMaterial));
		}
		bool finished = false;
		string outcome = null;
		Action<string, Crafted> onSucceed = delegate
		{
			finished = true;
			outcome = "succeed";
		};
		Action<string, ActionInfo> onFailed = delegate
		{
			finished = true;
			outcome = "failed";
		};
		craftSystem.CraftSucceed += onSucceed;
		craftSystem.CraftFailed += onFailed;
		craftSystem.Craft();
		float until = Time.time + 30f;
		while (!finished && Time.time < until)
		{
			yield return null;
		}
		craftSystem.CraftSucceed -= onSucceed;
		craftSystem.CraftFailed -= onFailed;
		r.Set("recipe", recipe.Id).Set("materials", materials).Set("result", outcome ?? "timeout");
		if (outcome != "succeed")
		{
			r.Ok = false;
		}
	}

	private static Dictionary<string, object> GuideSummary()
	{
		PlayGuideSystem playGuideSystem = GameSystem<PlayGuideSystem>.Instance();
		Dictionary<string, object> dictionary = Dict("begun", playGuideSystem.IsGuideBegin, "event", playGuideSystem.CurrentEventName);
		List<object> list = new List<object>();
		foreach (string item in playGuideSystem.ActiveFlowsInfo())
		{
			list.Add(item);
		}
		dictionary["flows"] = list;
		List<object> list2 = new List<object>();
		ToDoListSystem toDoListSystem = GameSystem<ToDoListSystem>.Instance();
		for (int i = 0; i < toDoListSystem.CollectionCount; i++)
		{
			ToDoCollection collection = toDoListSystem.GetCollection(i);
			foreach (ToDoBase toDo in collection.ToDoList)
			{
				object obj = null;
				if (collection is MissionToDoCollection missionToDoCollection)
				{
					Durango.Logic.Faction.MissionToDo currentToDo = missionToDoCollection.GetCurrentToDo();
					obj = ((currentToDo == null) ? "null" : currentToDo.DisplayText);
				}
				list2.Add(Dict("key", toDo.Key, "text", StripTags(toDo.LocalText), "progress", toDo.CurrentProgress, "target", toDo.TargetProgress, "completed", toDo.IsCompleted, "title", collection.Title, "event", (collection.GuideEvent != null) ? collection.GuideEvent.Name : null, "disabled", collection.IsDisabled, "order", (toDo is Durango.Logic.Faction.MissionToDo missionToDo) ? missionToDo.MissionOrder.ToString() : null, "current", obj, "regionId", GameManager.Region.Id));
			}
		}
		dictionary["todos"] = list2;
		return dictionary;
	}

	private IEnumerator CmdGuide(string[] args, Result r)
	{
		PlayGuideSystem playGuideSystem = GameSystem<PlayGuideSystem>.Instance();
		if (args.Length >= 3 && args[1] == "flow")
		{
			playGuideSystem.BeginFlow(args[2]);
			yield return null;
			r.Set("began", args[2]);
		}
		else if (args.Length >= 2 && args[1] == "complete")
		{
			playGuideSystem.CompleteCurrentEvent();
			yield return null;
		}
		else if (args.Length >= 2 && args[1] == "reload")
		{
			playGuideSystem.ReloadAll();
			yield return null;
		}
		r.Set("guide", GuideSummary());
	}

	private static DialogueGroupBase Dialogue()
	{
		if (!UiReady())
		{
			return null;
		}
		return UIManager.FindScript<DialogueGroupBase>();
	}

	private static object DialogInfo()
	{
		DialogueGroupBase dialogueGroupBase = Dialogue();
		if (dialogueGroupBase == null)
		{
			return null;
		}
		Dictionary<string, object> dictionary = dialogueGroupBase.TestBridgeInfo();
		GuideTooltip guideTooltip = ((UIManager.Popup != null) ? UIManager.Popup.Tooltip<GuideTooltip>() : null);
		dictionary["tooltip"] = guideTooltip != null && guideTooltip.IsVisible;
		return dictionary;
	}

	private IEnumerator CmdDialog(string[] args, Result r)
	{
		DialogueGroupBase dialogue = Dialogue();
		if (dialogue == null)
		{
			r.Fail("ไม\u0e48ม\u0e35 DialogueGroup");
			yield break;
		}
		if (args.Length >= 3 && args[1] == "replay")
		{
			if (!Uri.TryCreate(CommunityLink.Gateway, UriKind.Absolute, out var result) || !result.IsLoopback || result.Port != 8390)
			{
				r.Fail("dialog replay is available on local DEV only");
				yield break;
			}
			QuestYml questYml = SingletonDict<string, QuestYml>.Instance.Get(args[2]);
			QuestMessages[] array = ((questYml == null) ? null : ((args.Length > 3 && args[3] == "end") ? questYml.QuestEndMessages : questYml.QuestStartMessages));
			if (array == null || array.Length == 0)
			{
				r.Fail("No existing quest messages to replay");
				yield break;
			}
			dialogue.AddQuestMessages(args[2], array, addFront: true);
			r.Set("replay", args[2]).Set("serverProgressChanged", false);
		}
		else if (args.Length >= 2 && args[1] == "next")
		{
			int n = ((args.Length <= 2) ? 1 : ((int)ParseFloat(args[2], 1f)));
			int advanced = 0;
			for (int i = 0; i < n; i++)
			{
				if (!dialogue.IsOpened || dialogue.TestBridgeCurrentType() == null)
				{
					GuideTooltip guideTooltip = ((UIManager.Popup != null) ? UIManager.Popup.Tooltip<GuideTooltip>() : null);
					if (!(guideTooltip != null) || !guideTooltip.IsVisible)
					{
						break;
					}
					guideTooltip.Hide();
					advanced++;
					yield return new WaitForSeconds(0.3f);
				}
				else
				{
					dialogue.TestBridgePress(pressed: true);
					yield return null;
					yield return null;
					dialogue.TestBridgePress(pressed: false);
					advanced++;
					yield return new WaitForSeconds(0.15f);
				}
			}
			r.Set("advanced", advanced);
		}
		else if (args.Length >= 3 && args[1] == "quiz")
		{
			int index = (int)ParseFloat(args[2], 0f);
			if (!dialogue.TestBridgeAnswerQuiz(index))
			{
				r.Fail("ไม\u0e48ม\u0e35ต\u0e31วเล\u0e37อก " + index);
			}
			yield return new WaitForSeconds(0.2f);
		}
		r.Set("open", dialogue.IsOpened).Set("dialog", DialogInfo());
	}

	private IEnumerator CmdMsgBox(string[] args, Result r)
	{
		MessageBox messageBox = (UiReady() ? UIManager.MessageBox : null);
		if (messageBox == null)
		{
			r.Fail("ไม\u0e48ม\u0e35 MessageBox");
			yield break;
		}
		if (args.Length >= 2)
		{
			bool ok = args[1].Equals("ok", StringComparison.OrdinalIgnoreCase) || args[1].Equals("yes", StringComparison.OrdinalIgnoreCase);
			if (!messageBox.TestBridgeChoose(ok))
			{
				r.Fail("MessageBox ไม\u0e48ได\u0e49เป\u0e34ดอย\u0e39\u0e48");
			}
			yield return null;
		}
		r.Set("open", messageBox.IsShow).Set("text", messageBox.TestBridgeText());
	}

	private static string UiPath(Transform t)
	{
		string text = t.name;
		while (t.parent != null)
		{
			t = t.parent;
			text = t.name + "/" + text;
		}
		return text;
	}

	private static string UiLabel(GameObject go)
	{
		UILabel componentInChildren = go.GetComponentInChildren<UILabel>();
		if (!(componentInChildren != null))
		{
			return string.Empty;
		}
		return StripTags(componentInChildren.text);
	}

	private IEnumerator CmdUi(string[] args, Result r)
	{
		if (CommunityNinePresets.Handles(args))
		{
			yield return CommunityNinePresets.UiCommand(args, r);
			yield break;
		}
		if (TestBridgeScreen.Handles(args))
		{
			yield return TestBridgeScreen.Execute(args, r);
			yield break;
		}
		if (ClanPillarClient.Handles(args))
		{
			yield return ClanPillarClient.Command(args, r);
			yield break;
		}
		if (args.Length > 2 && args[1].ToLowerInvariant() == "story")
		{
			StoryMainViewer storyMainViewer = Array.Find(UnityEngine.Object.FindObjectsByType<StoryMainViewer>(), (StoryMainViewer v) => v.isActiveAndEnabled);
			if (storyMainViewer == null || !int.TryParse(args[2], out var page))
			{
				r.Fail("journal not open / page");
				yield break;
			}
			storyMainViewer.JumpTo(page);
			yield return new WaitForSecondsRealtime(0.5f);
			r.Set("page", page);
			yield break;
		}
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "list");
		if (text == "list")
		{
			string text2 = ((args.Length > 2) ? args[2] : null);
			List<object> list = new List<object>();
			UIButton[] array = UnityEngine.Object.FindObjectsByType<UIButton>();
			foreach (UIButton uIButton in array)
			{
				if (uIButton.gameObject.activeInHierarchy)
				{
					string text3 = UiPath(uIButton.transform);
					string text4 = UiLabel(uIButton.gameObject);
					if (text2 == null || text3.IndexOf(text2, StringComparison.OrdinalIgnoreCase) >= 0 || text4.IndexOf(text2, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						list.Add(Dict("path", text3, "label", text4, "enabled", uIButton.isEnabled));
					}
				}
			}
			Selectable[] array2 = UnityEngine.Object.FindObjectsByType<Selectable>();
			foreach (Selectable selectable in array2)
			{
				if (selectable.gameObject.activeInHierarchy && !(selectable.GetComponent<UIButton>() != null))
				{
					string text5 = UiPath(selectable.transform);
					string text6 = UiLabel(selectable.gameObject);
					if (text2 == null || text5.IndexOf(text2, StringComparison.OrdinalIgnoreCase) >= 0 || text6.IndexOf(text2, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						list.Add(Dict("path", text5, "label", text6, "enabled", true, "selectable", true));
					}
				}
			}
			r.Set("buttons", list).Set("count", list.Count);
		}
		else if (text == "click" && args.Length > 2)
		{
			GameObject target = FindUi(args[2]);
			if (target == null)
			{
				r.Fail("ไม\u0e48พบป\u0e38\u0e48ม " + args[2]);
				yield break;
			}
			Selectable component = target.GetComponent<Selectable>();
			if (component != null && component.Clicked != null)
			{
				Selectable.Current = component;
				try
				{
					component.Clicked();
				}
				finally
				{
					Selectable.Current = null;
				}
			}
			else
			{
				UIEventListener component2 = target.GetComponent<UIEventListener>();
				if (component2 != null && component2.onClick != null)
				{
					component2.onClick(target);
				}
				else
				{
					UICamera.Notify(target, "OnClick", null);
				}
			}
			yield return null;
			r.Set("clicked", UiPath(target.transform));
		}
		else if ((text == "open" || text == "close") && args.Length > 2)
		{
			UIBase ui = FindUiScript(args[2]);
			if (ui == null)
			{
				r.Fail("ไม\u0e48พบ UI " + args[2]);
				yield break;
			}
			bool result = ((text == "open") ? ui.Open() : ui.Close());
			yield return null;
			r.Set("type", ui.GetType().Name).Set("result", result).Set("opened", ui.IsOpened);
		}
		else
		{
			r.Fail("ui list [filter] · ui click <path|label> · ui open|close <TypeName>");
		}
	}

	private static GameObject FindUi(string key)
	{
		if (key.IndexOf('/') >= 0)
		{
			GameObject gameObject = GameObject.Find(key);
			if (gameObject != null && gameObject.activeInHierarchy && gameObject.GetComponent<UIEventListener>()?.onClick != null)
			{
				return gameObject;
			}
		}
		GameObject best = null;
		UIButton[] array = UnityEngine.Object.FindObjectsByType<UIButton>();
		foreach (UIButton uIButton in array)
		{
			if (uIButton.gameObject.activeInHierarchy && UiMatch(uIButton.gameObject, key, ref best))
			{
				return best;
			}
		}
		Selectable[] array2 = UnityEngine.Object.FindObjectsByType<Selectable>();
		foreach (Selectable selectable in array2)
		{
			if (selectable.gameObject.activeInHierarchy && UiMatch(selectable.gameObject, key, ref best))
			{
				return best;
			}
		}
		return best;
	}

	private static bool UiMatch(GameObject go, string key, ref GameObject best)
	{
		string text = UiPath(go.transform);
		if (string.Equals(text, key, StringComparison.OrdinalIgnoreCase) || text.EndsWith("/" + key, StringComparison.OrdinalIgnoreCase))
		{
			best = go;
			return true;
		}
		if (best == null && (text.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 || UiLabel(go).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0))
		{
			best = go;
		}
		return false;
	}

	private static UIBase FindUiScript(string typeName)
	{
		Type type = null;
		Type[] types = typeof(TestBridge).Assembly.GetTypes();
		foreach (Type type2 in types)
		{
			if (typeof(UIBase).IsAssignableFrom(type2) && (type2.Name == typeName || type2.FullName == typeName))
			{
				type = type2;
				break;
			}
		}
		if (type == null)
		{
			return null;
		}
		if (!UiReady())
		{
			return null;
		}
		return typeof(UIManager).GetMethod("FindScript", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(type).Invoke(null, null) as UIBase;
	}

	private IEnumerator CmdKey(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("key <InputCommand> [down|up|press]");
			yield break;
		}
		InputCommand command;
		try
		{
			command = (InputCommand)Enum.Parse(typeof(InputCommand), args[1], ignoreCase: true);
		}
		catch
		{
			r.Fail("ไม\u0e48ร\u0e39\u0e49จ\u0e31ก InputCommand " + args[1] + " — ด\u0e39 keys");
			yield break;
		}
		string mode = ((args.Length > 2) ? args[2].ToLowerInvariant() : "press");
		InputSystem input = GameSystem<InputSystem>.Instance();
		if (mode == "down")
		{
			input.TestBridgeDispatch(command, Trigger.Down);
		}
		else if (mode == "up")
		{
			input.TestBridgeDispatch(command, Trigger.Up);
		}
		else
		{
			input.TestBridgeDispatch(command, Trigger.Down);
			yield return null;
			input.TestBridgeDispatch(command, Trigger.Up);
		}
		yield return null;
		r.Set("command", command.ToString()).Set("mode", mode);
	}

	private IEnumerator CmdKeys(string[] args, Result r)
	{
		string text = ((args.Length > 1) ? args[1] : null);
		List<object> list = new List<object>();
		string[] names = Enum.GetNames(typeof(InputCommand));
		foreach (string text2 in names)
		{
			if (text == null || text2.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				list.Add(text2);
			}
		}
		r.Set("commands", list).Set("count", list.Count);
		yield break;
	}

	private IEnumerator CmdEmote(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("emote <id>");
			yield break;
		}
		SocialSystem socialSystem = GameSystem<SocialSystem>.Instance();
		Durango.Logic.Social.Emoticon emoticon = socialSystem.Emotional.GetEmoticon(args[1]);
		if (emoticon == null)
		{
			r.Fail("ไม\u0e48ร\u0e39\u0e49จ\u0e31กอ\u0e35โมต\u0e34คอน " + args[1]);
			yield break;
		}
		socialSystem.PlayEmoticon(emoticon);
		yield return null;
		r.Set("emoticon", args[1]).Set("available", emoticon.Available);
	}

	private IEnumerator CmdMotion(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("motion <id>");
			yield break;
		}
		SocialSystem socialSystem = GameSystem<SocialSystem>.Instance();
		Durango.Logic.Social.Motion motion = socialSystem.Emotional.GetMotion(args[1]);
		if (motion == null)
		{
			r.Fail("ไม\u0e48ร\u0e39\u0e49จ\u0e31กท\u0e48าทาง " + args[1]);
		}
		else
		{
			r.Set("motion", args[1]).Set("played", socialSystem.PlayMotion(motion));
		}
	}

	private IEnumerator CmdCheat(string[] args, Result r)
	{
		if (args.Length < 2)
		{
			r.Fail("cheat <text>");
			yield break;
		}
		string text = string.Join(" ", args, 1, args.Length - 1);
		Connections.Frontend.Send(new Cheat
		{
			_Cheat = text
		});
		yield return new WaitForSeconds(0.5f);
		r.Set("sent", text);
	}

	private IEnumerator CmdBoat(string[] args, Result r)
	{
		TutorialIslandSystem tutorial = GameSystem<TutorialIslandSystem>.Instance();
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "");
		if (text == "put")
		{
			string text2 = tutorial.TestBridgePutAllMaterials();
			if (text2 != null)
			{
				r.Fail(text2);
			}
			yield return new WaitForSeconds(0.5f);
		}
		else if (text == "depart")
		{
			string text3 = tutorial.TestBridgeDepart();
			if (text3 != null)
			{
				r.Fail(text3);
			}
			yield return new WaitForSeconds(0.5f);
		}
		r.Set("boat", tutorial.TestBridgeInfo());
	}

	private IEnumerator CmdScreenshot(string[] args, Result r)
	{
		string path = ((args.Length > 1) ? args[1] : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "testbridge_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".png"));
		ScreenCapture.CaptureScreenshot(path);
		yield return new WaitForEndOfFrame();
		yield return new WaitForSeconds(0.5f);
		r.Set("path", Path.GetFullPath(path)).Set("exists", File.Exists(path));
	}

	private IEnumerator CmdLog(string[] args, Result r)
	{
		int num = ((args.Length > 1) ? ((int)ParseFloat(args[1], 30f)) : 30);
		List<object> list = new List<object>();
		lock (_log)
		{
			for (int i = Mathf.Max(0, _log.Count - num); i < _log.Count; i++)
			{
				list.Add(_log[i]);
			}
		}
		r.Set("lines", list);
		yield break;
	}

	private IEnumerator CmdWait(string[] args, Result r)
	{
		float ms = ((args.Length > 1) ? ParseFloat(args[1], 1000f) : 1000f);
		yield return new WaitForSeconds(ms / 1000f);
		r.Set("waited", ms);
	}

	private static float ParseFloat(string text, float fallback)
	{
		if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return result;
	}

	public static string StripTags(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		int num = 0;
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			switch (c)
			{
			case '<':
				num++;
				continue;
			case '>':
				if (num > 0)
				{
					num--;
					continue;
				}
				break;
			}
			if (c == '[')
			{
				int num2 = text.IndexOf(']', i);
				if (num2 > i && num2 - i <= 9)
				{
					i = num2;
					continue;
				}
			}
			if (num == 0)
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static Dictionary<string, object> Dict(params object[] pairs)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		for (int i = 0; i + 1 < pairs.Length; i += 2)
		{
			dictionary[(string)pairs[i]] = pairs[i + 1];
		}
		return dictionary;
	}

	private static string[] SplitArgs(string line)
	{
		List<string> list = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		foreach (char c in line)
		{
			if (c == '"')
			{
				flag = !flag;
			}
			else if (char.IsWhiteSpace(c) && !flag)
			{
				if (stringBuilder.Length > 0)
				{
					list.Add(stringBuilder.ToString());
					stringBuilder.Length = 0;
				}
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		if (stringBuilder.Length > 0)
		{
			list.Add(stringBuilder.ToString());
		}
		return list.ToArray();
	}

	private static string Json(Result result)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("{\"ok\":").Append(result.Ok ? "true" : "false");
		foreach (KeyValuePair<string, object> datum in result.Data)
		{
			stringBuilder.Append(',');
			JsonString(stringBuilder, datum.Key);
			stringBuilder.Append(':');
			JsonValue(stringBuilder, datum.Value);
		}
		stringBuilder.Append('}');
		return stringBuilder.ToString();
	}

	private static void JsonValue(StringBuilder sb, object value)
	{
		if (value == null)
		{
			sb.Append("null");
		}
		else if (value is string)
		{
			JsonString(sb, (string)value);
		}
		else if (value is bool)
		{
			sb.Append(((bool)value) ? "true" : "false");
		}
		else if (value is int || value is long || value is short || value is byte || value is ushort)
		{
			sb.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture));
		}
		else if (value is float || value is double)
		{
			double d = Convert.ToDouble(value);
			if (double.IsNaN(d) || double.IsInfinity(d))
			{
				sb.Append("null");
			}
			else
			{
				sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
			}
		}
		else if (value is IDictionary<string, object>)
		{
			sb.Append('{');
			bool flag = true;
			foreach (KeyValuePair<string, object> item in (IDictionary<string, object>)value)
			{
				if (!flag)
				{
					sb.Append(',');
				}
				flag = false;
				JsonString(sb, item.Key);
				sb.Append(':');
				JsonValue(sb, item.Value);
			}
			sb.Append('}');
		}
		else if (value is IEnumerable)
		{
			sb.Append('[');
			bool flag2 = true;
			foreach (object item2 in (IEnumerable)value)
			{
				if (!flag2)
				{
					sb.Append(',');
				}
				flag2 = false;
				JsonValue(sb, item2);
			}
			sb.Append(']');
		}
		else
		{
			JsonString(sb, value.ToString());
		}
	}

	private static void JsonString(StringBuilder sb, string text)
	{
		sb.Append('"');
		if (text != null)
		{
			foreach (char c in text)
			{
				switch (c)
				{
				case '"':
					sb.Append("\\\"");
					continue;
				case '\\':
					sb.Append("\\\\");
					continue;
				case '\n':
					sb.Append("\\n");
					continue;
				case '\r':
					sb.Append("\\r");
					continue;
				case '\t':
					sb.Append("\\t");
					continue;
				}
				if (c < ' ')
				{
					StringBuilder stringBuilder = sb.Append("\\u");
					int num = c;
					stringBuilder.Append(num.ToString("x4", CultureInfo.InvariantCulture));
				}
				else
				{
					sb.Append(c);
				}
			}
		}
		sb.Append('"');
	}
}
