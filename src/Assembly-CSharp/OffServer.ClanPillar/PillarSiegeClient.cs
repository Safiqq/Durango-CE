using System;
using System.Collections;
using Durango.Development;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.Network;
using Durango.UI;
using InteractionData;
using Messages;
using Newtonsoft.Json.Linq;
using OffServer.Rift;
using Shared.Chat;
using UnityEngine;

namespace OffServer.ClanPillar;

public sealed class PillarSiegeClient : MonoBehaviour
{
	private const double PortReach = 8.0;

	public const long DeclarationFee = 5000L;

	private static PillarSiegeClient instance;

	private InteractionSystem interaction;

	private MessageBox ownedPopup;

	private PillarSiegeDialog dialog = new PillarSiegeDialog();

	private string popupRegion;

	private string popupSite;

	private string popupOwner;

	private string region;

	private Vector2 popupPort;

	private bool statusPopup;

	private float nextTick;

	private float pendingUntil;

	private int sentCommands;

	private string lastCommand;

	private static PillarSiegeState fixture;

	private static float fixtureClock;

	private static double fixtureAt;

	private int blockedFixtureCommands;

	private static Vector2 PlayerTile => PlayerBehavior.LocalPlayer.CurrentTile.ToVector2();

	private static string CurrentRegion
	{
		get
		{
			if (GameManager.Region != null)
			{
				return GameManager.Region.Id;
			}
			return null;
		}
	}

	private static string Text(string thai, string english)
	{
		return RiftText.T(thai, english);
	}

	public static bool AtPort(Vector2 player, Vector2 port)
	{
		return (double)(player - port).sqrMagnitude <= 64.0;
	}

	private static bool RealPort(InteractionObject target)
	{
		if (target != null && target.IsValid() && target.EntityType == 7001)
		{
			return !RiftGateClient.IsGate(target.EntityId);
		}
		return false;
	}

	public static bool TryState(string id, out PillarSiegeState state, out double now)
	{
		if (fixture != null && fixture.RegionId == id)
		{
			state = fixture;
			now = fixtureAt + (double)Time.realtimeSinceStartup - (double)fixtureClock;
			return true;
		}
		return ClanPillarClient.TrySiege(id, out state, out now);
	}

	private void Awake()
	{
		instance = this;
	}

	private void OnDestroy()
	{
		if (interaction != null)
		{
			interaction.PostTouched -= OnPortTouched;
		}
		CloseOwnedPopup();
		if (instance == this)
		{
			instance = null;
		}
	}

	private void Update()
	{
		if (Time.realtimeSinceStartup < nextTick)
		{
			return;
		}
		nextTick = Time.realtimeSinceStartup + 0.5f;
		if (GameManager.IsTitleScene || PlayerBehavior.LocalPlayer == null)
		{
			CloseOwnedPopup();
			fixture = null;
			region = null;
			return;
		}
		if (region != CurrentRegion)
		{
			CloseOwnedPopup();
			fixture = null;
			pendingUntil = 0f;
			region = CurrentRegion;
		}
		InteractionSystem interactionSystem = GameSystem<InteractionSystem>.Instance();
		if (interactionSystem != interaction)
		{
			if (interaction != null)
			{
				interaction.PostTouched -= OnPortTouched;
			}
			interaction = interactionSystem;
			interaction.PostTouched += OnPortTouched;
			interaction.AddInteractionHandler(Interaction.PillarSiegeDeclare, OpenDeclaration);
			interaction.AddInteractionHandler(Interaction.PillarSiegeStatus, OpenStatus);
		}
		if (ownedPopup != null && (!ownedPopup.IsShow || !dialog.Visible))
		{
			ownedPopup = null;
			statusPopup = false;
		}
		if (statusPopup && ownedPopup != null)
		{
			if (TryState(popupRegion, out var state, out var now))
			{
				dialog.Fill(state, now, declaration: false, fixture != null);
			}
			else
			{
				CloseOwnedPopup();
			}
		}
		if (RealPort(interaction.Target))
		{
			UpdateMenu(interaction.MenuList, interaction.Target, apply: true);
		}
	}

	private void OnPortTouched(InteractionMenuList list, InteractionObject target)
	{
		UpdateMenu(list, target, apply: false);
	}

	private void UpdateMenu(InteractionMenuList list, InteractionObject target, bool apply)
	{
		if (RealPort(target))
		{
			PillarSiegeState state;
			double now;
			bool flag = TryState(CurrentRegion, out state, out now);
			bool flag2 = list.IndexOf(Interaction.PillarSiegeDeclare) >= 0;
			bool flag3 = list.IndexOf(Interaction.PillarSiegeStatus) >= 0;
			if (!flag || !state.CanDeclare(now) || !AtPort(PlayerTile, target.Tile) || !(state.OwnerId != PlayerBehavior.LocalPlayer.ClanId))
			{
				list.Remove(Interaction.PillarSiegeDeclare, null);
			}
			else
			{
				list.Add(new InteractionMenuData(Interaction.PillarSiegeDeclare)
				{
					Name = Text("ประกาศสงคราม", "Declare war"),
					Icon = new ItemIcon("act_occupy_warphole"),
					Disabled = (Time.realtimeSinceStartup < pendingUntil)
				});
			}
			if (!flag)
			{
				list.Remove(Interaction.PillarSiegeStatus, null);
			}
			else
			{
				list.Add(new InteractionMenuData(Interaction.PillarSiegeStatus)
				{
					Name = (state.HasCountdown(now) ? (Text((state.Stage == "prep") ? "เตร\u0e35ยมสงคราม" : "สงครามเสาแคลน", (state.Stage == "prep") ? "War preparation" : "Pillar siege") + "\n" + PillarSiegeState.Clock(state.Remaining(now))) : Text("สถานะสงคราม", "Siege status")),
					Icon = new ItemIcon("skill_clan_command")
				});
			}
			if (apply && (flag || flag2 || flag3))
			{
				list.Apply();
			}
		}
	}

	private void CloseOwnedPopup()
	{
		MessageBox messageBox = ownedPopup;
		ownedPopup = null;
		statusPopup = false;
		if (messageBox != null && messageBox.IsShow && dialog.Visible)
		{
			messageBox.Hide();
		}
	}

	private MessageBox NewPopup()
	{
		MessageBox messageBox = UIManager.MessageBox;
		if (messageBox == null || messageBox.IsShow)
		{
			return null;
		}
		ownedPopup = messageBox;
		messageBox.SetCustomWidget(dialog.Build(messageBox), MessageBox.Position.Center);
		return messageBox;
	}

	private void OpenDeclaration(InteractionObject target)
	{
		if (!RealPort(target) || !AtPort(PlayerTile, target.Tile) || Time.realtimeSinceStartup < pendingUntil || !TryState(CurrentRegion, out var state, out var now) || !state.CanDeclare(now))
		{
			return;
		}
		MessageBox messageBox = NewPopup();
		if (messageBox == null)
		{
			return;
		}
		popupRegion = state.RegionId;
		popupSite = state.SiteId;
		popupOwner = state.OwnerId;
		popupPort = target.Tile;
		dialog.Fill(state, now, declaration: true, fixture != null);
		messageBox.Show(Text("ประกาศสงครามเสาแคลน", "Declare a pillar siege"), delegate(bool yes)
		{
			if (yes)
			{
				ConfirmDeclaration();
			}
			else
			{
				ownedPopup = null;
			}
		}, Text("ประกาศสงคราม", "Declare war"), Text("ยกเล\u0e34ก", "Cancel"));
	}

	private void ConfirmDeclaration()
	{
		ownedPopup = null;
		if (PlayerBehavior.LocalPlayer == null || popupRegion != CurrentRegion || !AtPort(PlayerTile, popupPort) || !TryState(popupRegion, out var state, out var now) || state.SiteId != popupSite || state.OwnerId != popupOwner || !state.CanDeclare(now) || Time.realtimeSinceStartup < pendingUntil)
		{
			UIManager.SystemMsg(Text("สถานะหร\u0e37อตำแหน\u0e48งเปล\u0e35\u0e48ยนแล\u0e49ว กร\u0e38ณาเป\u0e34ดเมน\u0e39ท\u0e48าเร\u0e37ออ\u0e35กคร\u0e31\u0e49ง", "The state or your position changed. Open the port menu again."));
		}
		else if (SendCommand("/siege declare"))
		{
			pendingUntil = Time.realtimeSinceStartup + 10f;
		}
	}

	private bool SendCommand(string text)
	{
		if (fixture != null)
		{
			blockedFixtureCommands++;
			lastCommand = text;
			UIManager.SystemMsg(Text("ข\u0e49อม\u0e39ลทดสอบ UI — ไม\u0e48ส\u0e48งคำส\u0e31\u0e48งไปเซ\u0e34ร\u0e4cฟเวอร\u0e4c", "UI fixture — no command sent to the server"));
			return false;
		}
		SocialSystem socialSystem = GameSystem<SocialSystem>.Instance();
		if (!socialSystem.CanSay())
		{
			UIManager.SystemMsg(Text("ย\u0e31งเช\u0e37\u0e48อมต\u0e48อแชทไม\u0e48สำเร\u0e47จ กร\u0e38ณาลองใหม\u0e48", "Chat is not connected. Please try again."));
			return false;
		}
		socialSystem.SystemSay(new RadioTalk
		{
			Text = text
		}, ChannelType.Region);
		lastCommand = text;
		sentCommands++;
		return true;
	}

	private void OpenStatus(InteractionObject target)
	{
		if (!RealPort(target) || !TryState(CurrentRegion, out var state, out var now))
		{
			return;
		}
		MessageBox messageBox = NewPopup();
		if (messageBox == null)
		{
			return;
		}
		popupRegion = state.RegionId;
		statusPopup = true;
		dialog.Fill(state, now, declaration: false, fixture != null);
		messageBox.Show(Text("สงครามเสาแคลน", "Pillar siege"), delegate(int selection)
		{
			ownedPopup = null;
			statusPopup = false;
			if (selection == 0)
			{
				SendCommand("/siege");
			}
		}, new MessageBox.Button(Text("ด\u0e39ค\u0e39ลดาวน\u0e4cแคลน", "Clan cooldown")), Text("ป\u0e34ด", "Close"));
	}

	private static string Escape(string text)
	{
		return NGUIText.StripSymbols(text ?? "").Replace("<", "〈").Replace(">", "〉");
	}

	private static string StatusText(string regionId)
	{
		if (!TryState(regionId, out var state, out var now))
		{
			return Text("กำล\u0e31งรอข\u0e49อม\u0e39ลสงครามจากเซ\u0e34ร\u0e4cฟเวอร\u0e4c", "Waiting for siege data from the server");
		}
		string text = ((fixture == null) ? "" : Text("[ข\u0e49อม\u0e39ลทดสอบ UI]\n", "[UI fixture]\n")) + Text("สงครามเสาแคลน", "Pillar siege") + "\n" + Text("แคลนผ\u0e39\u0e49ครอง: ", "Defender: ") + Escape(state.OwnerName ?? state.OwnerId ?? Text("เสาว\u0e48าง", "Unoccupied"));
		if (state.Stage != null)
		{
			text = text + "\n" + Text("ผ\u0e39\u0e49บ\u0e38ก: ", "Attacker: ") + Escape(state.AttackerName ?? state.AttackerId);
			return text + "\n\n<em>" + (state.HasCountdown(now) ? (Text((state.Stage == "prep") ? "เร\u0e34\u0e48มสงครามใน " : "สงครามเหล\u0e37อ ", (state.Stage == "prep") ? "Starts in " : "Time remaining ") + PillarSiegeState.Clock(state.Remaining(now))) : Text("กำล\u0e31งอ\u0e31ปเดตสถานะสงคราม", "Updating siege state")) + "</em>";
		}
		if (state.LockUntil > now)
		{
			return text + "\n\n" + Text("เสาพ\u0e31กอ\u0e35ก ", "Pillar locked for ") + PillarSiegeState.Clock(state.Remaining(now));
		}
		return text + "\n\n" + Text(string.IsNullOrEmpty(state.OwnerId) ? "เสาว\u0e48าง ย\u0e36ดได\u0e49โดยไม\u0e48ต\u0e49องประกาศ" : "เสาพร\u0e49อมร\u0e31บประกาศสงคราม", string.IsNullOrEmpty(state.OwnerId) ? "Unoccupied: capture without declaring" : "This pillar can receive a declaration");
	}

	public static IEnumerator Command(string[] args, TestBridge.Result result)
	{
		if (args.Length > 4 && args[3] == "fixture")
		{
			if (instance == null || PlayerBehavior.LocalPlayer == null || !ClanPillarClient.IsPillarRegion(GameManager.Region) || OffServerLink.TestBridgePort <= 0 || !Uri.TryCreate(GameManager.GatewayUrl, UriKind.Absolute, out var result2) || !result2.IsLoopback)
			{
				result.Fail("Fixture requires a pillar island, an explicit TestBridge port and a loopback DEV gateway");
				yield break;
			}
			string text = args[4];
			if (text == "clear")
			{
				fixture = null;
			}
			else
			{
				if (text != "ready" && text != "prep" && text != "war" && text != "locked" && text != "vacant")
				{
					result.Fail("fixture ready|prep|war|locked|vacant|clear [seconds]");
					yield break;
				}
				int result3 = 3600;
				if (args.Length > 5 && (!int.TryParse(args[5], out result3) || result3 < 1 || result3 > 86400))
				{
					result.Fail("fixture duration 1..86400 seconds");
					yield break;
				}
				fixtureAt = Connections.Frontend.GetPredictedServerTime();
				fixtureClock = Time.realtimeSinceStartup;
				JObject jObject = new JObject
				{
					["regionId"] = CurrentRegion,
					["siteId"] = CurrentRegion + "_cw_pillar",
					["clanId"] = ((text == "vacant") ? null : "ui_fixture_defender"),
					["clanName"] = ((text == "vacant") ? null : "DEF (UI fixture)"),
					["siegeLockUntil"] = ((text == "locked") ? (fixtureAt + (double)result3) : 0.0),
					["siege"] = null
				};
				if (text == "prep" || text == "war")
				{
					jObject["siege"] = new JObject
					{
						["attackerId"] = "ui_fixture_attacker",
						["attackerName"] = "ATK (UI fixture)",
						["defenderId"] = "ui_fixture_defender",
						["stage"] = text,
						["declaredAt"] = fixtureAt - 3600.0,
						["startAt"] = ((text == "prep") ? (fixtureAt + (double)result3) : (fixtureAt - 1.0)),
						["endAt"] = fixtureAt + (double)((text == "war") ? result3 : (result3 + 43200))
					};
				}
				fixture = PillarSiegeState.Read(jObject);
			}
			instance.CloseOwnedPopup();
			if (RealPort(instance.interaction?.Target))
			{
				instance.interaction.RefreshInteractionMenu();
			}
		}
		PillarSiegeState state;
		double now;
		bool flag = TryState(CurrentRegion, out state, out now);
		result.Set("supported", flag).Set("region", CurrentRegion).Set("stage", flag ? state.Stage : null)
			.Set("remaining", flag ? state.Remaining(now) : 0.0)
			.Set("sentCommands", (!(instance == null)) ? instance.sentCommands : 0)
			.Set("lastCommand", (instance == null) ? null : instance.lastCommand)
			.Set("fixture", fixture != null)
			.Set("blockedFixtureCommands", (!(instance == null)) ? instance.blockedFixtureCommands : 0)
			.Set("view", (instance == null) ? null : instance.dialog.Diagnostics())
			.Set("popup", instance != null && instance.ownedPopup != null && instance.ownedPopup.IsShow);
		yield return null;
	}
}
