using System;
using System.Collections.Generic;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.Logic.Market;
using Durango.Network;
using Durango.UI;
using Durango.UI.Popup;
using Durango.Utils;
using L10N;
using Newtonsoft.Json.Linq;
using Shared.Economy;
using Shared.Market;
using UnityEngine;

public static class OffServerAuctionUI
{
	public enum Tab
	{
		Browse,
		Selling,
		Bidding,
		Claims
	}

	public static readonly Tab[] Tabs = new Tab[4]
	{
		Tab.Browse,
		Tab.Selling,
		Tab.Bidding,
		Tab.Claims
	};

	public static readonly Commodities Data = new Commodities();

	public static Tab Current;

	public static string Query = string.Empty;

	public static string Group = string.Empty;

	public static long MoneyWaiting;

	public static int ItemClaims;

	public static int Loads;

	private static int _loadSeq;

	private static int _sessionSeq;

	private static readonly string[] Groups = new string[7] { "", "weapon", "clothing", "accessory", "material", "food", "other" };

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"preview_notice",
			new string[4] { "ต\u0e31วอย\u0e48าง UI เท\u0e48าน\u0e31\u0e49น ไม\u0e48ม\u0e35การลงประม\u0e39ล เสนอราคา หร\u0e37อร\u0e31บของจร\u0e34ง", "UI preview only. No listings, bids or claims are submitted.", "Pratinjau UI saja, tanpa transaksi.", "UI 미리보기입니다. 실제 거래는 전송되지 않습니다." }
		},
		{
			"auction_fee",
			new string[4] { "ค\u0e48าลงประม\u0e39ล {0}% · ภาษ\u0e35เม\u0e37\u0e48อขายได\u0e49 {1}%", "Listing fee {0}% · sale tax {1}%", "Biaya lelang {0}% · pajak {1}%", "등록 수수료 {0}% · 판매세 {1}%" }
		},
		{
			"status",
			new string[4] { "สถานะ", "Status", "Status", "상태" }
		},
		{
			"sell_btn",
			new string[4] { "ขาย", "Sell", "Jual", "판매" }
		},
		{
			"sell_btn_n",
			new string[4] { "ขาย {0} ช\u0e34\u0e49น", "Sell {0} items", "Jual {0} barang", "{0}개 판매" }
		},
		{
			"menu_auction",
			new string[4] { "ประม\u0e39ล", "Auction", "Lelang", "경매" }
		},
		{
			"menu_list",
			new string[4] { "ลงประม\u0e39ล", "List for auction", "Lelangkan", "경매 등록" }
		},
		{
			"auction_off",
			new string[4] { "ระบบประม\u0e39ลป\u0e34ดอย\u0e39\u0e48ช\u0e31\u0e48วคราว", "The auction house is closed for now", "Lelang ditutup sementara", "경매장이 잠시 닫혀 있습니다" }
		},
		{
			"auction_wait",
			new string[4] { "กำล\u0e31งเช\u0e37\u0e48อมต\u0e48อระบบประม\u0e39ล ลองอ\u0e35กคร\u0e31\u0e49ง", "Connecting to the auction house — try again", "Menghubungkan lelang", "경매장 연결 중" }
		},
		{
			"tab_browse",
			new string[4] { "ท\u0e31\u0e49งหมด", "All", "Semua", "전체" }
		},
		{
			"tab_selling",
			new string[4] { "ท\u0e35\u0e48ฉ\u0e31นลงขาย", "My listings", "Lelangku", "내 등록" }
		},
		{
			"tab_bidding",
			new string[4] { "ท\u0e35\u0e48ฉ\u0e31นเสนอราคา", "My bids", "Tawaranku", "내 입찰" }
		},
		{
			"tab_claims",
			new string[4] { "รอร\u0e31บ", "To collect", "Ambil", "수령 대기" }
		},
		{
			"start_price",
			new string[4] { "ราคาเร\u0e34\u0e48มต\u0e49น", "Start price", "Harga awal", "시작가" }
		},
		{
			"bids_n",
			new string[4] { "เสนอแล\u0e49ว {0} คร\u0e31\u0e49ง", "{0} bids", "{0} tawaran", "입찰 {0}회" }
		},
		{
			"you_lead",
			new string[4] { "ค\u0e38ณเสนอส\u0e39งส\u0e38ด", "You lead", "Kamu tertinggi", "최고 입찰" }
		},
		{
			"outbid",
			new string[4] { "ถ\u0e39กแซงราคา", "Outbid", "Terlampaui", "추월됨" }
		},
		{
			"extended",
			new string[4] { "ต\u0e48อเวลาแล\u0e49ว", "Extended", "Diperpanjang", "연장됨" }
		},
		{
			"state_sold",
			new string[4] { "ขายแล\u0e49ว", "Sold", "Terjual", "판매됨" }
		},
		{
			"state_unsold",
			new string[4] { "ไม\u0e48ม\u0e35ผ\u0e39\u0e49เสนอ", "No bids", "Tanpa tawaran", "유찰" }
		},
		{
			"state_cancelled",
			new string[4] { "ยกเล\u0e34ก", "Cancelled", "Dibatalkan", "취소" }
		},
		{
			"waiting",
			new string[4] { "รอร\u0e31บ", "Waiting", "Menunggu", "대기" }
		},
		{
			"cancel_lot",
			new string[4] { "ยกเล\u0e34กการประม\u0e39ล", "Cancel listing", "Batalkan", "등록 취소" }
		},
		{
			"bid_btn",
			new string[4] { "เสนอราคา (ข\u0e31\u0e49นต\u0e48ำ {0})", "Bid (min {0})", "Tawar (min {0})", "입찰 (최소 {0})" }
		},
		{
			"cancel_main",
			new string[4] { "ยกเล\u0e34กการประม\u0e39ล <em>{0}</em>?", "Cancel the auction of <em>{0}</em>?", "Batalkan lelang <em>{0}</em>?", "<em>{0}</em> 경매를 취소할까요?" }
		},
		{
			"cancel_sub",
			new string[4] { "ของจะกล\u0e31บไปอย\u0e39\u0e48ในแท\u0e47บ «รอร\u0e31บ» · ค\u0e48าลงประม\u0e39ลไม\u0e48ค\u0e37น", "The item goes to «To collect» · the listing fee is not returned", "Barang kembali ke «Ambil»", "아이템은 «수령 대기»로 돌아갑니다" }
		},
		{
			"cancelled",
			new string[4] { "ยกเล\u0e34กการประม\u0e39ลแล\u0e49ว — ร\u0e31บของค\u0e37นในแท\u0e47บ «รอร\u0e31บ»", "Cancelled — collect the item in «To collect»", "Dibatalkan", "취소되었습니다" }
		},
		{
			"bid_title",
			new string[4] { "เสนอราคา (ข\u0e31\u0e49นต\u0e48ำ {0} T)", "Your bid (min {0} T)", "Tawaran (min {0} T)", "입찰가 (최소 {0} T)" }
		},
		{
			"bid_low",
			new string[4] { "ต\u0e49องเสนอไม\u0e48ต\u0e48ำกว\u0e48า {0}", "Bid at least {0}", "Minimal {0}", "최소 {0} 이상" }
		},
		{
			"bid_poor",
			new string[4] { "T Stone ไม\u0e48พอ — ต\u0e49องใช\u0e49 {0} (ม\u0e35 {1})", "Not enough T Stone — need {0} (have {1})", "T Stone kurang — perlu {0}", "T Stone 부족 — {0} 필요 (보유 {1})" }
		},
		{
			"bid_main",
			new string[4] { "เสนอราคา <em>{0}</em>", "Bid on <em>{0}</em>", "Tawar <em>{0}</em>", "<em>{0}</em> 입찰" }
		},
		{
			"bid_sub",
			new string[4] { "เง\u0e34นถ\u0e39กพ\u0e31กไว\u0e49ท\u0e31นท\u0e35 · ถ\u0e49าม\u0e35คนเสนอส\u0e39งกว\u0e48า ได\u0e49ค\u0e37นอ\u0e31ตโนม\u0e31ต\u0e34 · เหล\u0e37อเวลา {0} (เสนอช\u0e48วงท\u0e49ายจะต\u0e48อเวลา)", "The T Stone is held now · returned at once if someone bids higher · {0} left (late bids extend the time)", "T Stone ditahan · kembali jika kalah · sisa {0}", "입찰금은 즉시 보관 · 추월되면 자동 반환 · 남은 시간 {0}" }
		},
		{
			"bid_done",
			new string[4] { "เสนอราคา {0} แล\u0e49ว", "Bid {0} placed", "Tawaran {0} dikirim", "{0} 입찰 완료" }
		},
		{
			"claimed",
			new string[4] { "ร\u0e31บแล\u0e49ว: ของ {0} ช\u0e34\u0e49น · {1}", "Collected: {0} item(s) · {1}", "Diambil: {0} barang · {1}", "수령: 아이템 {0}개 · {1}" }
		},
		{
			"money_wait",
			new string[4] { "เง\u0e34นรอร\u0e31บ {0} · ของรอร\u0e31บ {1} รายการ", "Waiting: {0} · {1} item lot(s)", "Menunggu: {0} · {1} barang", "수령 대기: {0} · 아이템 {1}건" }
		},
		{
			"claim_all",
			new string[4] { "ร\u0e31บท\u0e31\u0e49งหมด", "Collect all", "Ambil semua", "모두 받기" }
		},
		{
			"search_name",
			new string[4] { "ค\u0e49นหาตามช\u0e37\u0e48อ…", "Search by name…", "Cari nama…", "이름으로 검색…" }
		},
		{
			"search_hint",
			new string[4] { "พ\u0e34มพ\u0e4cช\u0e37\u0e48อไอเทม (เว\u0e49นว\u0e48าง = ท\u0e31\u0e49งหมด)", "Item name (empty = all)", "Nama barang (kosong = semua)", "아이템 이름 (비우면 전체)" }
		},
		{
			"search_btn",
			new string[4] { "ค\u0e49นหา", "Search", "Cari", "검색" }
		},
		{
			"group_all",
			new string[4] { "ท\u0e38กประเภท", "All types", "Semua jenis", "전체 분류" }
		},
		{
			"group_weapon",
			new string[4] { "อาว\u0e38ธ/เคร\u0e37\u0e48องม\u0e37อ", "Weapons/tools", "Senjata/alat", "무기/도구" }
		},
		{
			"group_clothing",
			new string[4] { "เส\u0e37\u0e49อผ\u0e49า", "Clothing", "Pakaian", "의상" }
		},
		{
			"group_accessory",
			new string[4] { "เคร\u0e37\u0e48องประด\u0e31บ", "Accessories", "Aksesori", "장신구" }
		},
		{
			"group_material",
			new string[4] { "ว\u0e31ตถ\u0e38ด\u0e34บ", "Materials", "Bahan", "재료" }
		},
		{
			"group_food",
			new string[4] { "อาหาร/ยา/เมล\u0e47ด", "Food/medicine/seeds", "Makanan/obat/benih", "음식/약/씨앗" }
		},
		{
			"group_other",
			new string[4] { "อ\u0e37\u0e48น ๆ", "Other", "Lainnya", "기타" }
		},
		{
			"pick_item",
			new string[4] { "เล\u0e37อกของจากกระเป\u0e4bาก\u0e48อน", "Pick an item from your bag", "Pilih barang dulu", "아이템을 선택하세요" }
		},
		{
			"same_kind",
			new string[4] { "หน\u0e36\u0e48งรายการต\u0e49องเป\u0e47นของชน\u0e34ดและเลเวลเด\u0e35ยวก\u0e31น", "One listing = one kind and level", "Satu jenis dan level", "한 번에 같은 종류·레벨만 등록" }
		},
		{
			"too_many",
			new string[4] { "ลงได\u0e49ไม\u0e48เก\u0e34น {0} ช\u0e34\u0e49นต\u0e48อรายการ", "At most {0} pieces per listing", "Maks {0} per lelang", "최대 {0}개" }
		},
		{
			"price_low",
			new string[4] { "ราคาเร\u0e34\u0e48มต\u0e49นต\u0e49องไม\u0e48ต\u0e48ำกว\u0e48า {0}", "Start price at least {0}", "Harga awal min {0}", "시작가는 {0} 이상" }
		},
		{
			"hours",
			new string[4] { "{0} ช\u0e31\u0e48วโมง", "{0} hours", "{0} jam", "{0}시간" }
		},
		{
			"duration_main",
			new string[4] { "ระยะเวลาประม\u0e39ล <em>{0}</em>", "Auction length for <em>{0}</em>", "Durasi lelang <em>{0}</em>", "<em>{0}</em> 경매 기간" }
		},
		{
			"duration_sub",
			new string[4] { "ของถ\u0e39กพ\u0e31กไว\u0e49ท\u0e35\u0e48ระบบประม\u0e39ลจนจบ · ไม\u0e48ม\u0e35ผ\u0e39\u0e49เสนอ = ของกล\u0e31บมาให\u0e49กดร\u0e31บ", "The item is held until the end · no bids = it comes back to collect", "Barang ditahan sampai selesai", "종료까지 보관 · 유찰 시 반환" }
		},
		{
			"kv_start",
			new string[4] { "ราคาเร\u0e34\u0e48มต\u0e49น", "Start price", "Harga awal", "시작가" }
		},
		{
			"kv_duration",
			new string[4] { "ระยะเวลา", "Length", "Durasi", "기간" }
		},
		{
			"kv_tax",
			new string[4] { "ภาษ\u0e35เม\u0e37\u0e48อขายได\u0e49 ({0}%)", "Tax when sold ({0}%)", "Pajak ({0}%)", "판매세 ({0}%)" }
		},
		{
			"kv_net",
			new string[4] { "ร\u0e31บส\u0e38ทธ\u0e34ท\u0e35\u0e48ราคาเร\u0e34\u0e48มต\u0e49น", "Net at the start price", "Bersih di harga awal", "시작가 기준 순수익" }
		},
		{
			"list_main",
			new string[4] { "ลงประม\u0e39ล <em>{0}</em>?", "List <em>{0}</em> for auction?", "Lelangkan <em>{0}</em>?", "<em>{0}</em> 경매 등록?" }
		},
		{
			"list_sub",
			new string[4] { "<alert_icon/> ม\u0e35ผ\u0e39\u0e49เสนอราคาแล\u0e49วยกเล\u0e34กไม\u0e48ได\u0e49 · ค\u0e48าลงประม\u0e39ลไม\u0e48ค\u0e37น", "<alert_icon/> No cancel after the first bid · listing fee not returned", "<alert_icon/> Tidak bisa batal setelah ada tawaran", "<alert_icon/> 입찰 후 취소 불가 · 등록비 환불 없음" }
		},
		{
			"listed",
			new string[4] { "ลงประม\u0e39ล <em>{0}</em> แล\u0e49ว", "<em>{0}</em> listed", "<em>{0}</em> dilelang", "<em>{0}</em> 등록 완료" }
		},
		{
			"list_btn",
			new string[4] { "ลงประม\u0e39ล", "List", "Lelangkan", "경매 등록" }
		},
		{
			"list_btn_n",
			new string[4] { "ลงประม\u0e39ล {0} ช\u0e34\u0e49น", "List {0} pieces", "Lelangkan {0}", "{0}개 경매 등록" }
		},
		{
			"mode_sell",
			new string[4] { "ขาย (ราคาตายต\u0e31ว)", "Sell (fixed price)", "Jual (harga tetap)", "판매 (고정가)" }
		},
		{
			"mode_auction",
			new string[4] { "ประม\u0e39ล", "Auction", "Lelang", "경매" }
		},
		{
			"mode_auction_hint",
			new string[4] { "โหมดประม\u0e39ล — ราคาท\u0e35\u0e48ใส\u0e48 = ราคาเร\u0e34\u0e48มต\u0e49น · เล\u0e37อกของชน\u0e34ดและเลเวลเด\u0e35ยวก\u0e31นได\u0e49หลายช\u0e34\u0e49นเป\u0e47นรายการเด\u0e35ยว · กดล\u0e39กศรข\u0e49างป\u0e38\u0e48มเพ\u0e37\u0e48อกล\u0e31บไปขายราคาตายต\u0e31ว", "Auction mode — the price is the start price · several pieces of one kind and level = one listing · the arrow beside the button switches back to a fixed-price sale", "Mode lelang — harga = harga awal · panah untuk kembali ke jual", "경매 모드 — 입력 가격 = 시작가 · 화살표로 판매 전환" }
		}
	};

	public static bool IsLoading { get; private set; }

	public static bool IsBusy { get; private set; }

	public static bool Available
	{
		get
		{
			if (!OffServerExchange.AuctionEnabled)
			{
				return OffServerAuctionPreview.Active;
			}
			return true;
		}
	}

	public static event Action Updated;

	public static void Reset()
	{
		_loadSeq++;
		_sessionSeq++;
		IsLoading = false;
		IsBusy = false;
		OffServerAuctionPreview.End();
		Data.Reset();
		MoneyWaiting = 0L;
		ItemClaims = 0;
		NotifyUpdated();
	}

	private static void RequestMutation(byte kind, JObject body, Action<JObject> completed, bool preview = false)
	{
		if (IsBusy || IsLoading || !Available)
		{
			return;
		}
		if (preview || OffServerAuctionPreview.Active)
		{
			UIManager.MessageBox.Show(Text("preview_notice"));
			return;
		}
		int session = _sessionSeq;
		IsBusy = true;
		NotifyUpdated();
		OffServerExchange.Request(kind, body, delegate(JObject reply)
		{
			if (session != _sessionSeq)
			{
				return;
			}
			IsBusy = false;
			try
			{
				completed(reply);
			}
			finally
			{
				NotifyUpdated();
			}
		});
	}

	private static void NotifyUpdated()
	{
		Action updated = OffServerAuctionUI.Updated;
		if (updated == null)
		{
			return;
		}
		Delegate[] invocationList = updated.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			Action action = (Action)invocationList[i];
			try
			{
				if (action.Target is UnityEngine.Object obj && obj == null)
				{
					Updated -= action;
				}
				else
				{
					action();
				}
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
	}

	public static string TabName(Tab tab)
	{
		return tab switch
		{
			Tab.Selling => Text("tab_selling"),
			Tab.Bidding => Text("tab_bidding"),
			Tab.Claims => Text("tab_claims"),
			_ => Text("tab_browse"),
		};
	}

	public static ProductType ProductTypeOf(Tab tab)
	{
		if (tab != Tab.Claims)
		{
			return ProductType.Registered;
		}
		return ProductType.Sold;
	}

	public static void Load(Tab tab)
	{
		Current = tab;
		int seq = ++_loadSeq;
		IsLoading = true;
		NotifyUpdated();
		if (OffServerAuctionPreview.Active)
		{
			Fill(tab, OffServerAuctionPreview.Response(tab));
			return;
		}
		JObject jObject = new JObject();
		byte kind;
		switch (tab)
		{
		case Tab.Browse:
			kind = 10;
			jObject["name"] = Query ?? string.Empty;
			jObject["group"] = Group ?? string.Empty;
			jObject["sort"] = "ending";
			break;
		case Tab.Selling:
			kind = 11;
			jObject["kind"] = "selling";
			break;
		case Tab.Bidding:
			kind = 11;
			jObject["kind"] = "bidding";
			break;
		default:
			kind = 11;
			jObject["kind"] = "selling";
			break;
		}
		OffServerExchange.Request(kind, jObject, delegate(JObject r)
		{
			if (seq != _loadSeq)
			{
				OffServerExchange.Trace("auction load " + tab.ToString() + " seq " + seq + " stale (now " + _loadSeq + ")");
				return;
			}
			Loads++;
			try
			{
				Fill(tab, r);
				OffServerExchange.Trace("auction fill " + tab.ToString() + " rows " + Data.Goods.Count + ((r == null) ? " (no reply)" : string.Empty));
			}
			catch (Exception ex)
			{
				OffServerExchange.Trace("auction fill " + tab.ToString() + " failed: " + ex.GetType().Name + " " + ex.Message);
				throw;
			}
		});
	}

	private static void Fill(Tab tab, JObject r)
	{
		IsLoading = false;
		Data.Reset();
		Data.Request.NoMore = true;
		Data.Request.IsLoading = false;
		MoneyWaiting = 0L;
		ItemClaims = 0;
		if (r == null)
		{
			UIManager.SystemMsg(OffServerExchange.Text("no_reply"));
		}
		else if (!OffServerExchange.Bool(r, "ok"))
		{
			UIManager.SystemMsg(OffServerExchange.Str(r, "text"));
		}
		else
		{
			if (r["claims"] is JArray jArray)
			{
				foreach (JToken item in (IEnumerable<JToken>)jArray)
				{
					if (!(item is JObject jObject))
					{
						continue;
					}
					long num = (long)OffServerExchange.Num(jObject, "tstone", 0.0);
					MoneyWaiting += num;
					if (!(OffServerExchange.Num(jObject, "count", 0.0) > 0.0))
					{
						continue;
					}
					ItemClaims++;
					if (tab == Tab.Claims)
					{
						Commodity commodity = ClaimCommodity(jObject);
						if (commodity != null)
						{
							Data.Goods.Add(commodity);
						}
					}
				}
				if (!OffServerAuctionPreview.Active)
				{
					OffServerExchange.PendingClaims = jArray.Count;
				}
			}
			if (tab != Tab.Claims && r["lots"] is JArray jArray2)
			{
				foreach (JToken item2 in (IEnumerable<JToken>)jArray2)
				{
					Commodity commodity2 = ((item2 is JObject lot) ? LotCommodity(lot) : null);
					if (commodity2 != null)
					{
						Data.Goods.Add(commodity2);
					}
				}
			}
		}
		NotifyUpdated();
	}

	private static Commodity LotCommodity(JObject lot)
	{
		ItemData itemData = OffServerAuctionPreview.ResolveItem(lot["item"]);
		if (itemData == null)
		{
			OffServerExchange.Trace("lot " + OffServerExchange.Str(lot, "id") + " item decode failed");
			return null;
		}
		long num = (long)OffServerExchange.Num(lot, "current", 0.0);
		long num2 = (long)OffServerExchange.Num(lot, "startPrice", 0.0);
		int num3 = (int)OffServerExchange.Num(lot, "bidCount", 0.0);
		int num4 = (int)OffServerExchange.Num(lot, "count", 1.0);
		int num5 = (int)OffServerExchange.Num(lot, "state", 1.0);
		Commodity commodity = new Commodity();
		commodity.Id = OffServerExchange.Str(lot, "id");
		commodity.Items = new ItemData[1] { itemData };
		commodity.ExpireAt = OffServerExchange.Num(lot, "endsAt", 0.0);
		commodity.DeletesAt = commodity.ExpireAt;
		commodity.Price = ((num > 0) ? num : num2);
		commodity.CurrencyType = Currency.TStone;
		commodity.State = ProductState.Registered;
		commodity.OffServerTag = lot;
		string text = OffServerExchange.TStoneText(commodity.Price);
		string text2 = ((num3 > 0) ? Text("bids_n", num3) : Text("start_price"));
		if (OffServerExchange.Bool(lot, "leading"))
		{
			text2 = Text("you_lead");
		}
		else if (OffServerExchange.Bool(lot, "bidded") && num5 == 1)
		{
			text2 = Text("outbid");
		}
		commodity.OffServerPriceText = text + "\n" + text2;
		if (num4 > 1)
		{
			commodity.OffServerNameSuffix = " ×" + num4;
		}
		switch (num5)
		{
		case 2:
			commodity.OffServerDurationText = Text("state_sold");
			break;
		case 3:
			commodity.OffServerDurationText = Text("state_unsold");
			break;
		case 4:
			commodity.OffServerDurationText = Text("state_cancelled");
			break;
		default:
			if (OffServerExchange.Bool(lot, "extended"))
			{
				commodity.OffServerDurationSuffix = "\n" + Text("extended");
			}
			break;
		}
		return commodity;
	}

	private static Commodity ClaimCommodity(JObject claim)
	{
		ItemData itemData = OffServerAuctionPreview.ResolveItem(claim["item"]);
		if (itemData == null)
		{
			return null;
		}
		string id = OffServerExchange.Str(claim, "id");
		int num = (int)OffServerExchange.Num(claim, "count", 1.0);
		Commodity commodity = new Commodity();
		commodity.Id = id;
		commodity.Items = new ItemData[1] { itemData };
		commodity.Price = 0L;
		commodity.CurrencyType = Currency.TStone;
		commodity.State = ProductState.PaymentPending;
		commodity.PurchasedAt = OffServerExchange.Num(claim, "at", 0.0);
		commodity.OffServerTag = claim;
		commodity.OffServerPriceText = OffServerExchange.Str(claim, "text");
		commodity.OffServerDurationText = Text("waiting");
		if (num > 1)
		{
			commodity.OffServerNameSuffix = " ×" + num;
		}
		commodity.OffServerReceive = delegate
		{
			Claim(id);
		};
		return commodity;
	}

	public static void OnNotice(string kind)
	{
		switch (kind)
		{
		case "outbid":
		case "bid":
		case "won":
		case "sold":
		case "unsold":
		case "claim":
		{
			MarketGroup marketGroup = (Singleton<UIManager>.HasInstance() ? UIManager.FindScript<MarketGroup>() : null);
			if (marketGroup != null && marketGroup.IsOpened && marketGroup.OffServerAuctionOpen)
			{
				Load(Current);
			}
			break;
		}
		}
	}

	public static string ActionText(Commodity c)
	{
		JObject jObject = ((c != null) ? (c.OffServerTag as JObject) : null);
		if (jObject == null || Current == Tab.Claims)
		{
			return null;
		}
		if ((int)OffServerExchange.Num(jObject, "state", 1.0) != 1 || OffServerExchange.Num(jObject, "endsAt", 0.0) <= Connections.Frontend.GetPredictedServerTime())
		{
			return null;
		}
		if (OffServerExchange.Bool(jObject, "mine"))
		{
			if (!(OffServerExchange.Num(jObject, "bidCount", 0.0) > 0.0))
			{
				return Text("cancel_lot");
			}
			return null;
		}
		if (OffServerExchange.Bool(jObject, "leading"))
		{
			return null;
		}
		return Text("bid_btn", ((long)OffServerExchange.Num(jObject, "minBid", 0.0)).ToString("N0"));
	}

	public static void DoAction(Commodity c)
	{
		if (IsLoading || IsBusy || ActionText(c) == null)
		{
			return;
		}
		JObject jObject = ((c != null) ? (c.OffServerTag as JObject) : null);
		if (jObject != null)
		{
			if (OffServerExchange.Bool(jObject, "mine"))
			{
				AskCancel(c, jObject);
			}
			else
			{
				AskBid(c, jObject);
			}
		}
	}

	private static void AskCancel(Commodity c, JObject lot)
	{
		bool preview = OffServerAuctionPreview.Active;
		ItemData item = c.GetItem();
		string lotId = OffServerExchange.Str(lot, "id");
		UIManager.MessageBox.Show(Text("cancel_main", (item != null) ? item.Name : "?"), Text("cancel_sub"), delegate(bool ok)
		{
			if (ok)
			{
				RequestMutation(14, new JObject { ["lotId"] = lotId }, delegate(JObject r)
				{
					Report(r, Text("cancelled"));
					Load(Current);
				}, preview);
			}
		});
	}

	private static void AskBid(Commodity c, JObject lot)
	{
		bool preview = OffServerAuctionPreview.Active;
		ItemData item = c.GetItem();
		string lotId = OffServerExchange.Str(lot, "id");
		long current = (long)OffServerExchange.Num(lot, "current", 0.0);
		long min = (long)OffServerExchange.Num(lot, "minBid", 0.0);
		int num = (int)OffServerExchange.Num(lot, "count", 1.0);
		string name = ((item != null) ? item.Name : "?") + ((num > 1) ? (" ×" + num) : string.Empty);
		UIManager.Popup.Tooltip<NumberInputPopup>().Show(min, Currency.TStone, Text("bid_title", min.ToString("N0")), delegate(long amount)
		{
			if (amount < min)
			{
				UIManager.MessageBox.Show(Text("bid_low", OffServerExchange.TStoneText(min)));
			}
			else
			{
				long num2 = InventorySystem.Wallet.GetBalance(Currency.TStone);
				if (OffServerAuctionPreview.Active)
				{
					num2 = Math.Max(num2, 10000L);
				}
				if (num2 < amount)
				{
					UIManager.MessageBox.Show(Text("bid_poor", OffServerExchange.TStoneText(amount), OffServerExchange.TStoneText(num2)));
				}
				else
				{
					double val = OffServerExchange.Num(lot, "endsAt", 0.0) - Connections.Frontend.GetPredictedServerTime();
					string subText = Text("bid_sub", TimedeltaFormatter.Format(Math.Max(0.0, val), 2, "min"));
					UIManager.MessageBox.ShowPayConfirm(amount, Currency.TStone, Text("bid_main", name), subText, delegate(bool ok)
					{
						if (ok)
						{
							RequestMutation(13, new JObject
							{
								["lotId"] = lotId,
								["amount"] = amount,
								["expect"] = current
							}, delegate(JObject r)
							{
								if (r != null && OffServerExchange.Bool(r, "ok"))
								{
									UIManager.SystemMsg(Text("bid_done", OffServerExchange.TStoneText(amount)) + ((OffServerExchange.Num(r, "extended", 0.0) > 0.0) ? (" · " + Text("extended")) : string.Empty));
								}
								else
								{
									UIManager.MessageBox.Show((r == null) ? OffServerExchange.Text("no_reply") : OffServerExchange.Str(r, "text"));
								}
								Load(Current);
							}, preview);
						}
					});
				}
			}
		}, Math.Max(min, 99999999L));
	}

	public static void Claim(string claimId)
	{
		JObject jObject = new JObject();
		if (!string.IsNullOrEmpty(claimId))
		{
			jObject["claimId"] = claimId;
		}
		RequestMutation(15, jObject, delegate(JObject r)
		{
			if (r != null && OffServerExchange.Bool(r, "ok"))
			{
				long num = 0L;
				int num2 = 0;
				if (r["got"] is JArray jArray)
				{
					foreach (JToken item in (IEnumerable<JToken>)jArray)
					{
						num += (long)OffServerExchange.Num(item as JObject, "tstone", 0.0);
						num2 += (int)OffServerExchange.Num(item as JObject, "count", 0.0);
					}
				}
				string text = Text("claimed", num2, OffServerExchange.TStoneText(num));
				string text2 = OffServerExchange.Str(r, "text");
				UIManager.SystemMsg((text2.Length > 0) ? (text + " · " + text2) : text);
			}
			else
			{
				UIManager.MessageBox.Show((r == null) ? OffServerExchange.Text("no_reply") : OffServerExchange.Str(r, "text"));
			}
			Load(Current);
		});
	}

	public static void ClaimAll()
	{
		Claim(null);
	}

	public static void AskSearch(GameObject anchor)
	{
		List<string> list = new List<string>();
		list.Add(Text("search_name") + ((Query.Length > 0) ? (" («" + Query + "»)") : string.Empty));
		for (int i = 0; i < Groups.Length; i++)
		{
			list.Add(((Groups[i] == Group) ? "● " : "○ ") + Text("group_" + ((Groups[i].Length == 0) ? "all" : Groups[i])));
		}
		StringSelector stringSelector = UIManager.Popup.Tooltip<StringSelector>();
		stringSelector.Set(list, delegate(int index)
		{
			if (index == 0)
			{
				UIManager.Popup.Tooltip<TextInputPopup>().Show(delegate(string text)
				{
					Query = (text ?? string.Empty).Trim();
					Load(Tab.Browse);
				}, Text("search_hint"), Query, isMultiline: false, Text("search_btn"), 40);
			}
			else
			{
				Group = Groups[index - 1];
				Load(Tab.Browse);
			}
		});
		if (anchor != null)
		{
			stringSelector.Show(anchor, Vector2.zero);
		}
		else
		{
			stringSelector.Show();
		}
	}

	public static string CheckSelection(List<ItemData> items)
	{
		if (items == null || items.Count == 0)
		{
			return Text("pick_item");
		}
		for (int i = 1; i < items.Count; i++)
		{
			if (items[i].PrototypeId != items[0].PrototypeId || items[i].Level != items[0].Level)
			{
				return Text("same_kind");
			}
		}
		if (items.Count > OffServerExchange.MaxPieces)
		{
			return Text("too_many", OffServerExchange.MaxPieces);
		}
		return null;
	}

	public static void AskList(List<ItemData> items, long startPrice, Action onListed)
	{
		bool preview = OffServerAuctionPreview.Active;
		if (IsBusy || !Available)
		{
			return;
		}
		string text = CheckSelection(items);
		if (text != null)
		{
			UIManager.SystemMsg(text);
			return;
		}
		if (startPrice < OffServerExchange.MinStartPrice)
		{
			UIManager.SystemMsg(Text("price_low", OffServerExchange.TStoneText(OffServerExchange.MinStartPrice)));
			return;
		}
		List<ItemData> chosen = new List<ItemData>(items);
		int[] hours = OffServerExchange.Durations;
		MessageBox.Button[] array = new MessageBox.Button[hours.Length + 1];
		array[hours.Length] = new MessageBox.Button(T._("취소"));
		for (int i = 0; i < hours.Length; i++)
		{
			array[i] = new MessageBox.Button(Text("hours", hours[i]));
		}
		string name = chosen[0].Name + ((chosen.Count > 1) ? (" ×" + chosen.Count) : string.Empty);
		UIManager.MessageBox.Show(Text("duration_main", name), Text("duration_sub"), delegate(int index)
		{
			if (index >= 0 && index < hours.Length)
			{
				int h = hours[index];
				long cost = CeilPct(startPrice, OffServerExchange.ListFeeRatio);
				long num = CeilPct(startPrice, OffServerExchange.TaxRate);
				MessageBox messageBox = UIManager.MessageBox;
				messageBox.AddKeyValueInfo(Text("kv_start"), OffServerExchange.TStoneText(startPrice));
				messageBox.AddKeyValueInfo(Text("kv_duration"), Text("hours", h));
				messageBox.AddKeyValueInfo(Text("kv_tax", (OffServerExchange.TaxRate * 100.0).ToString("0.#")), OffServerExchange.TStoneText(num));
				messageBox.AddKeyValueInfo(Text("kv_net"), OffServerExchange.TStoneText(startPrice - num));
				messageBox.ShowPayConfirm(cost, Currency.TStone, Text("list_main", name), Text("list_sub"), delegate(bool ok)
				{
					if (ok)
					{
						JObject jObject = new JObject();
						JArray jArray = new JArray();
						foreach (ItemData item in chosen)
						{
							jArray.Add(item.Id);
						}
						jObject["itemIds"] = jArray;
						jObject["startPrice"] = startPrice;
						jObject["hours"] = h;
						RequestMutation(12, jObject, delegate(JObject r)
						{
							if (r != null && OffServerExchange.Bool(r, "ok"))
							{
								UIManager.SystemMsg(Text("listed", name));
								if (onListed != null)
								{
									onListed();
								}
							}
							else
							{
								UIManager.MessageBox.Show((r == null) ? OffServerExchange.Text("no_reply") : OffServerExchange.Str(r, "text"));
							}
						}, preview);
					}
				});
			}
		}, array);
	}

	public static long CeilPct(long amount, double rate)
	{
		if (amount <= 0 || rate <= 0.0)
		{
			return 0L;
		}
		decimal num = Math.Round((decimal)rate, 6);
		return (long)Math.Ceiling((decimal)amount * num);
	}

	private static void Report(JObject r, string okText)
	{
		if (r != null && OffServerExchange.Bool(r, "ok"))
		{
			UIManager.SystemMsg(okText);
		}
		else
		{
			UIManager.MessageBox.Show((r == null) ? OffServerExchange.Text("no_reply") : OffServerExchange.Str(r, "text"));
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
