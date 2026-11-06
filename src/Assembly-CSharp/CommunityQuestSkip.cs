using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.Logic.PlayGuide;
using Durango.Network;
using Durango.UI.Control;
using Durango.Utils;
using Messages;
using Shared.Economy;
using UnityEngine;
using Yaml;
using Yaml.Util;

public static class CommunityQuestSkip
{
	public sealed class HudCollection : ToDoCollection
	{
		public override Detail? GetDetail()
		{
			Detail? detail = base.GetDetail();
			if (!detail.HasValue || base.IsDisabled || !TryGetPrice(base.Key, out var price))
			{
				return detail;
			}
			string questId = base.Key;
			Detail value = detail.Value;
			value.ButtonText = ((price > 0) ? Text("btn_hud", price) : Text("btn_hud_free"));
			value.ButtonStyle = PresetButton.Style.Solid;
			value.ButtonClicked = delegate
			{
				Ask(questId);
			};
			return value;
		}
	}

	public const string OptionEnabled = "quest.skip.enabled";

	private const float ReplyTimeoutSec = 15f;

	public const long ConfirmFreePrice = -1L;

	private static readonly Dictionary<string, long> _prices = new Dictionary<string, long>(StringComparer.Ordinal);

	private static readonly Dictionary<string, float> _freeAt = new Dictionary<string, float>(StringComparer.Ordinal);

	private static bool _freeTimer;

	private static float _freeTimerAt;

	private static bool _listing;

	private static bool _listAgain;

	private static bool _listScheduled;

	private static float _listSentAt;

	private static bool _busy;

	private static float _busySince;

	private static OptionSystem _hookedOptions;

	private static QuestSystem _hookedQuests;

	public static string LastEvent = string.Empty;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"btn_hud",
			new string[4] { "ข\u0e49ามเควส · {0} Coin", "Skip quest · {0} Coin", "Lewati misi · {0} Coin", "퀘스트 건너뛰기 · {0} 코인" }
		},
		{
			"btn_node",
			new string[4] { "ข\u0e49าม {0} Coin", "Skip {0} Coin", "Lewati {0} Coin", "건너뛰기 {0} 코인" }
		},
		{
			"btn_confirm",
			new string[4] { "ข\u0e49ามเควส", "Skip", "Lewati", "건너뛰기" }
		},
		{
			"btn_hud_free",
			new string[4] { "ข\u0e49ามเควส (ฟร\u0e35)", "Skip quest (free)", "Lewati misi (gratis)", "퀘스트 건너뛰기 (무료)" }
		},
		{
			"btn_node_free",
			new string[4] { "ข\u0e49าม (ฟร\u0e35)", "Skip (free)", "Lewati (gratis)", "건너뛰기 (무료)" }
		},
		{
			"confirm_free",
			new string[4] { "ข\u0e49ามเควส «{0}» ฟร\u0e35?", "Skip the quest «{0}» for free?", "Lewati misi «{0}» gratis?", "«{0}» 퀘스트를 무료로 건너뛸까요?" }
		},
		{
			"confirm_free_sub",
			new string[4] { "ได\u0e49รางว\u0e31ลครบเหม\u0e37อนทำเอง · ข\u0e31\u0e49นถ\u0e31ดไปเป\u0e34ดตามปกต\u0e34", "You get the full reward, as if you finished it · the next step opens as usual", "Hadiah penuh seperti menyelesaikannya sendiri · langkah berikutnya terbuka seperti biasa", "직접 완료한 것과 같은 보상을 받고 다음 단계가 열립니다" }
		},
		{
			"confirm",
			new string[4] { "ข\u0e49ามเควส «{0}» ด\u0e49วย {1} Coin?\nได\u0e49รางว\u0e31ลครบเหม\u0e37อนทำเอง · ข\u0e31\u0e49นถ\u0e31ดไปเป\u0e34ดตามปกต\u0e34", "Skip the quest «{0}» for {1} Coin?\nYou get the full reward, as if you finished it · the next step opens as usual", "Lewati misi «{0}» dengan {1} Coin?\nHadiah penuh seperti menyelesaikannya sendiri · langkah berikutnya terbuka seperti biasa", "«{0}» 퀘스트를 {1} 코인으로 건너뛸까요?\n직접 완료한 것과 같은 보상을 받고 다음 단계가 열립니다" }
		},
		{
			"balance",
			new string[4] { "Coin ท\u0e35\u0e48ม\u0e35: {0:N0}", "Your Coin: {0:N0}", "Coin kamu: {0:N0}", "보유 코인: {0:N0}" }
		},
		{
			"done",
			new string[4] { "ข\u0e49ามเควสแล\u0e49ว", "Quest skipped", "Misi dilewati", "퀘스트를 건너뛰었습니다" }
		},
		{
			"busy",
			new string[4] { "กำล\u0e31งข\u0e49ามเควสอย\u0e39\u0e48 — รอส\u0e31กคร\u0e39\u0e48", "Already skipping a quest — please wait", "Sedang melewati misi — tunggu sebentar", "퀘스트를 건너뛰는 중입니다 — 잠시 기다려 주세요" }
		},
		{
			"offline",
			new string[4] { "ต\u0e48อเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49 — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Not connected — please try again", "Tidak terhubung — coba lagi", "서버에 연결되지 않았습니다 — 다시 시도해 주세요" }
		},
		{
			"fail",
			new string[4] { "ข\u0e49ามเควสไม\u0e48ได\u0e49 ({0})", "Couldn't skip the quest ({0})", "Tidak bisa melewati misi ({0})", "퀘스트를 건너뛸 수 없습니다 ({0})" }
		}
	};

	public static bool Enabled
	{
		get
		{
			if (CommunityLink.Active)
			{
				return OptionSystem.GetBool("quest.skip.enabled");
			}
			return false;
		}
	}

	public static event Action Changed;

	public static bool TryGetPrice(string questId, out long price)
	{
		price = 0L;
		if (!Enabled || string.IsNullOrEmpty(questId))
		{
			return false;
		}
		if (_prices.TryGetValue(questId, out price) && price > 0)
		{
			return true;
		}
		price = 0L;
		return IsFreeNow(questId);
	}

	public static bool IsFreeNow(string questId)
	{
		if (Enabled && !string.IsNullOrEmpty(questId) && _freeAt.TryGetValue(questId, out var value))
		{
			return Time.realtimeSinceStartup >= value;
		}
		return false;
	}

	public static void Install(QuestSystem quests)
	{
		Durango.Utils.Singleton<GameManager>.Instance().WelcomeReceived += delegate
		{
			OptionSystem.ResetServerValue("quest.skip.enabled", value: false);
			Clear();
		};
		OptionSystem optionSystem = GameSystem<OptionSystem>.Instance();
		if (optionSystem != null && optionSystem != _hookedOptions)
		{
			_hookedOptions = optionSystem;
			optionSystem.AddOnChange("quest.skip.enabled", delegate(bool on)
			{
				if (on)
				{
					RequestList();
				}
				else
				{
					Clear();
				}
			});
		}
		if (quests != null && quests != _hookedQuests)
		{
			_hookedQuests = quests;
			quests.QuestStarted += delegate
			{
				ScheduleList();
			};
			quests.QuestFinished += delegate
			{
				ScheduleList();
			};
			quests.QuestCategoryChanged += delegate
			{
				ScheduleList();
			};
		}
	}

	public static ToDoCollection NewHudCollection()
	{
		return new HudCollection();
	}

	public static string ButtonText(long price)
	{
		if (price <= 0)
		{
			return Text("btn_node_free");
		}
		return Text("btn_node", price);
	}

	private static void Clear()
	{
		_prices.Clear();
		_freeAt.Clear();
		_freeTimer = false;
		_listing = false;
		_listAgain = false;
		RaiseChanged();
	}

	private static void ScheduleListAt(float at)
	{
		if (_hookedQuests == null || (_freeTimer && _freeTimerAt <= at))
		{
			return;
		}
		_freeTimer = true;
		_freeTimerAt = at;
		KUtility.DelayedCall(_hookedQuests, delegate
		{
			if (_freeTimer && !(Math.Abs(_freeTimerAt - at) > 0.01f))
			{
				_freeTimer = false;
				RequestList();
			}
		}, Math.Max(0.1f, at - Time.realtimeSinceStartup));
	}

	private static void SetFree(string questId, long price)
	{
		float num = Time.realtimeSinceStartup + ((price < 0) ? ((float)(-price)) : 0f);
		_freeAt[questId] = num;
		if (price < 0)
		{
			ScheduleListAt(num + 0.5f);
		}
	}

	private static void RaiseChanged()
	{
		if (CommunityQuestSkip.Changed == null)
		{
			return;
		}
		try
		{
			CommunityQuestSkip.Changed();
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.Log("[QuestSkip] Changed handler: " + ex.Message);
		}
	}

	private static void ScheduleList()
	{
		if (!Enabled || _listScheduled)
		{
			return;
		}
		if (_hookedQuests == null)
		{
			RequestList();
			return;
		}
		_listScheduled = true;
		KUtility.DelayedCall(_hookedQuests, delegate
		{
			_listScheduled = false;
			RequestList();
		}, 0.5f);
	}

	public static void RequestList()
	{
		if (!Enabled)
		{
			Clear();
			return;
		}
		if (_listing && Time.realtimeSinceStartup - _listSentAt < 15f)
		{
			_listAgain = true;
			return;
		}
		_listing = true;
		_listSentAt = Time.realtimeSinceStartup;
		ReplyMessageHandlerRegistrar replyMessageHandlerRegistrar;
		try
		{
			replyMessageHandlerRegistrar = Connections.Frontend.Send(new QuestSkipRequest
			{
				QuestId = string.Empty,
				Price = 0L
			});
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.Log("[QuestSkip] list send: " + ex.Message);
			_listing = false;
			return;
		}
		if (replyMessageHandlerRegistrar.IsEmpty())
		{
			_listing = false;
			return;
		}
		replyMessageHandlerRegistrar.On(delegate(QuestSkipResult msg, PacketHeader _)
		{
			_listing = false;
			_prices.Clear();
			_freeAt.Clear();
			_freeTimer = false;
			int num = Math.Min(KUtility.GetSize(msg.Ids), KUtility.GetSize(msg.Prices));
			for (int i = 0; i < num; i++)
			{
				if (!string.IsNullOrEmpty(msg.Ids[i]))
				{
					if (msg.Prices[i] > 0)
					{
						_prices[msg.Ids[i]] = msg.Prices[i];
					}
					else
					{
						SetFree(msg.Ids[i], msg.Prices[i]);
					}
				}
			}
			LastEvent = "list " + _prices.Count + " free " + _freeAt.Count;
			RaiseChanged();
			if (_listAgain)
			{
				_listAgain = false;
				ScheduleList();
			}
		}).Rest(delegate
		{
			_listing = false;
		});
	}

	public static void Ask(string questId)
	{
		if (!Enabled || string.IsNullOrEmpty(questId))
		{
			return;
		}
		if (Busy())
		{
			UIManager.SystemMsg(Text("busy"));
			return;
		}
		Request(questId, 0L, delegate(QuestSkipResult r)
		{
			if (!string.IsNullOrEmpty(r.Reason))
			{
				ShowRefusal(r);
			}
			else if (r.Price == 0L)
			{
				ConfirmFree(questId);
			}
			else
			{
				Confirm(questId, r.Price, r.Coin);
			}
		});
	}

	private static void ConfirmFree(string questId)
	{
		UIManager.MessageBox.Show(Text("confirm_free", QuestName(questId)), Text("confirm_free_sub"), delegate(bool ok)
		{
			if (ok)
			{
				SkipFree(questId);
			}
		}, Text("btn_confirm"));
	}

	private static void SkipFree(string questId)
	{
		if (Busy())
		{
			UIManager.SystemMsg(Text("busy"));
			return;
		}
		Request(questId, -1L, delegate(QuestSkipResult r)
		{
			if (r.Done)
			{
				_freeAt.Remove(questId);
				_prices.Remove(questId);
				LastEvent = "skipped free " + questId;
				UIManager.SystemMsg(string.IsNullOrEmpty(r.Text) ? Text("done") : r.Text, 4f);
				RaiseChanged();
				ScheduleList();
			}
			else
			{
				ShowRefusal(r);
			}
		});
	}

	private static void Confirm(string questId, long price, long coin)
	{
		if (price <= 0)
		{
			return;
		}
		string comment = Text("confirm", QuestName(questId), price);
		string subText = ((coin >= 0) ? Text("balance", coin) : null);
		UIManager.MessageBox.ShowPayConfirm(price, Currency.Coin, comment, subText, delegate(bool ok)
		{
			if (ok)
			{
				Skip(questId, price);
			}
		}, Text("btn_confirm"));
	}

	private static void Skip(string questId, long price)
	{
		if (Busy())
		{
			UIManager.SystemMsg(Text("busy"));
			return;
		}
		Request(questId, price, delegate(QuestSkipResult r)
		{
			if (r.Done)
			{
				_prices.Remove(questId);
				LastEvent = "skipped " + questId;
				UIManager.SystemMsg(string.IsNullOrEmpty(r.Text) ? Text("done") : r.Text, 4f);
				RaiseChanged();
				ScheduleList();
			}
			else if (r.Reason == "price" && r.Price > 0)
			{
				Confirm(questId, r.Price, r.Coin);
			}
			else
			{
				ShowRefusal(r);
			}
		});
	}

	private static bool Busy()
	{
		if (_busy && Time.realtimeSinceStartup - _busySince > 15f)
		{
			_busy = false;
		}
		return _busy;
	}

	private static void Request(string questId, long price, Action<QuestSkipResult> done)
	{
		_busy = true;
		_busySince = Time.realtimeSinceStartup;
		ReplyMessageHandlerRegistrar replyMessageHandlerRegistrar;
		try
		{
			replyMessageHandlerRegistrar = Connections.Frontend.Send(new QuestSkipRequest
			{
				QuestId = questId,
				Price = price
			});
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.Log("[QuestSkip] send: " + ex.Message);
			replyMessageHandlerRegistrar = ReplyMessageHandlerRegistrar.Empty;
		}
		if (replyMessageHandlerRegistrar.IsEmpty())
		{
			_busy = false;
			UIManager.SystemMsg(Text("offline"));
			return;
		}
		replyMessageHandlerRegistrar.On(delegate(QuestSkipResult msg, PacketHeader _)
		{
			_busy = false;
			LastEvent = questId + " " + (msg.Done ? "done" : (string.IsNullOrEmpty(msg.Reason) ? "ok" : msg.Reason));
			done(msg);
		}).Rest(delegate
		{
			_busy = false;
		});
	}

	private static void ShowRefusal(QuestSkipResult r)
	{
		LastEvent = r.QuestId + " " + r.Reason;
		string text = (string.IsNullOrEmpty(r.Text) ? Text("fail", r.Reason) : r.Text);
		if (r.Reason == "coin")
		{
			UIManager.MessageBox.Show(text);
		}
		else
		{
			UIManager.SystemMsg(text, 4f);
		}
		if (r.Reason == "wait")
		{
			if (!string.IsNullOrEmpty(r.QuestId) && r.Price < 0)
			{
				SetFree(r.QuestId, r.Price);
				RaiseChanged();
			}
		}
		else if (r.Reason != "busy" && r.Reason != "error" && r.Reason != "unresolved" && r.Reason != "coin" && r.Reason != "price")
		{
			_prices.Remove(r.QuestId ?? string.Empty);
			_freeAt.Remove(r.QuestId ?? string.Empty);
			RaiseChanged();
			ScheduleList();
		}
	}

	private static string QuestName(string questId)
	{
		QuestYml questYml = SingletonDict<string, QuestYml>.Instance.Get(questId);
		if (questYml == null)
		{
			return questId;
		}
		string text = questYml.Subject;
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return questId;
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
