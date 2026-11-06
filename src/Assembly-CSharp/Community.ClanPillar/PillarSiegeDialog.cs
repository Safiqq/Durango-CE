using System.Collections.Generic;
using System.Reflection;
using Durango.UI;
using Community.Rift;
using UnityEngine;

namespace Community.ClanPillar;

public sealed class PillarSiegeDialog
{
	private UIWidget root;

	private UILabel island;

	private UILabel defender;

	private UILabel attacker;

	private UILabel phase;

	private UILabel clock;

	private UILabel detail;

	private UILabel detail2;

	private UILabel footnote;

	private UISprite icon;

	public bool Visible
	{
		get
		{
			if (root != null)
			{
				return root.gameObject.activeInHierarchy;
			}
			return false;
		}
	}

	public Dictionary<string, object> Diagnostics()
	{
		return new Dictionary<string, object>
		{
			{ "visible", Visible },
			{
				"phase",
				(phase == null) ? null : phase.text
			},
			{
				"countdown",
				(clock == null) ? null : clock.text
			},
			{
				"detail",
				(detail == null) ? null : detail.text
			},
			{
				"captureRule",
				(detail2 == null) ? null : detail2.text
			},
			{
				"icon",
				(icon == null) ? null : icon.spriteName
			},
			{
				"iconValid",
				icon != null && icon.isValid
			}
		};
	}

	private static string T(string th, string en)
	{
		return RiftText.T(th, en);
	}

	public UIWidget Build(MessageBox box)
	{
		if (root != null)
		{
			return root;
		}
		UILabel source = (UILabel)typeof(MessageBox).GetField("_mainTextLabel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(box);
		GameObject gameObject = new GameObject("PillarSiegeDetails");
		gameObject.SetActive(value: false);
		gameObject.transform.SetParent(box.transform, worldPositionStays: false);
		root = gameObject.AddComponent<UIWidget>();
		root.width = 560;
		root.height = 350;
		root.pivot = UIWidget.Pivot.Top;
		icon = Sprite("SiegeIcon", "skill_clan_command", -34, -14, 68, 68);
		island = Label(source, "Island", 0, -92, 540, 32, 25);
		defender = Label(source, "Defender", 0, -132, 540, 28, 21);
		attacker = Label(source, "Attacker", 0, -166, 540, 28, 21);
		phase = Label(source, "Phase", 0, -204, 540, 27, 21);
		clock = Label(source, "Countdown", 0, -246, 540, 48, 38);
		clock.color = PresetColor.UIYellow;
		detail = Label(source, "Details", 0, -282, 560, 28, 20);
		detail2 = Label(source, "CaptureRule", 0, -310, 560, 26, 18);
		footnote = Label(source, "Footnote", 0, -338, 560, 26, 17);
		footnote.color = new Color(0.75f, 0.75f, 0.75f);
		return root;
	}

	private UILabel Label(UILabel source, string name, int x, int y, int w, int h, int size)
	{
		GameObject gameObject = Object.Instantiate(source.gameObject, root.transform, worldPositionStays: false);
		gameObject.name = name;
		Collider[] componentsInChildren = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			Object.Destroy(componentsInChildren[i]);
		}
		UILabel component = gameObject.GetComponent<UILabel>();
		component.SetAnchor((Transform)null);
		component.pivot = UIWidget.Pivot.Center;
		component.transform.localPosition = new Vector3(x, y, 0f);
		component.transform.localScale = Vector3.one;
		component.width = w;
		component.height = h;
		component.fontSize = size;
		component.maxLineCount = 1;
		component.spacingX = 0;
		component.spacingY = 2;
		component.useFloatSpacing = false;
		component.overflowMethod = UILabel.Overflow.ShrinkContent;
		component.alignment = NGUIText.Alignment.Center;
		component.supportEncoding = false;
		component.color = Color.white;
		component.depth = 30;
		component.text = "";
		gameObject.SetActive(value: true);
		return component;
	}

	private UISprite Sprite(string name, string key, int x, int y, int w, int h)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(root.transform, worldPositionStays: false);
		UISprite uISprite = gameObject.AddComponent<UISprite>();
		uISprite.spriteName = key;
		uISprite.depth = 25;
		uISprite.pivot = UIWidget.Pivot.TopLeft;
		uISprite.transform.localPosition = new Vector3(x, y, 0f);
		uISprite.width = w;
		uISprite.height = h;
		return uISprite;
	}

	private static string Clean(string value)
	{
		return NGUIText.StripSymbols(value ?? "").Replace("<", "〈").Replace(">", "〉");
	}

	public void Fill(PillarSiegeState state, double now, bool declaration, bool fixture)
	{
		if (!(root == null))
		{
			root.height = 350;
			island.text = Clean(GameManager.Region.Name ?? state.RegionId) + (fixture ? T(" · ข\u0e49อม\u0e39ลทดสอบ", " · UI fixture") : "");
			defender.text = T("ผ\u0e39\u0e49ครองเสา  ", "Defender  ") + Clean(state.OwnerName ?? state.OwnerId ?? T("เสาว\u0e48าง", "Unoccupied"));
			attacker.text = (declaration ? (T("ผ\u0e39\u0e49บ\u0e38ก  ", "Attacker  ") + Clean(GameSystem<ClanSystem>.Instance().PlayerClan?.Name ?? PlayerBehavior.LocalPlayer.ClanId)) : ((state.Stage == null) ? "" : (T("ผ\u0e39\u0e49บ\u0e38ก  ", "Attacker  ") + Clean(state.AttackerName ?? state.AttackerId))));
			icon.spriteName = (declaration ? "act_occupy_warphole" : ((state.Stage == "war") ? "act_Attack" : "skill_clan_command"));
			if (declaration)
			{
				phase.text = T("ค\u0e48าประกาศจากกองท\u0e38นแคลน", "Declaration fee from clan funds");
				clock.text = "5,000 T Stone";
				detail.text = T("เตร\u0e35ยมต\u0e31ว 1 ช\u0e31\u0e48วโมง  ·  สงคราม 12 ช\u0e31\u0e48วโมง", "Prepare 1 hour  ·  Siege lasts 12 hours");
				detail2.text = T("ย\u0e36ดเสาสำเร\u0e47จ สงครามจบท\u0e31นท\u0e35", "Capturing the pillar ends the siege immediately");
				footnote.text = T("ค\u0e48าประกาศไม\u0e48ค\u0e37น · ผ\u0e39\u0e49บ\u0e38กพ\u0e31ก 3 ว\u0e31นหล\u0e31งจบสงคราม", "No fee refund · Attacker cooldown: 3 days after the siege");
			}
			else
			{
				detail2.text = "";
				phase.text = ((state.Stage == "prep") ? T("เร\u0e34\u0e48มสงครามใน", "War starts in") : ((state.Stage == "war") ? T("เวลาสงครามคงเหล\u0e37อ", "Time remaining") : ((state.LockUntil > now) ? T("เสาพ\u0e31กอ\u0e35ก", "Pillar lock expires in") : T("สถานะเสาแคลน", "Pillar status"))));
				clock.text = ((state.Stage == null) ? ((state.LockUntil > now) ? PillarSiegeState.Clock(state.Remaining(now)) : T(string.IsNullOrEmpty(state.OwnerId) ? "เสาว\u0e48าง" : "พร\u0e49อมร\u0e31บประกาศ", string.IsNullOrEmpty(state.OwnerId) ? "Unoccupied" : "Ready for declaration")) : (state.HasCountdown(now) ? PillarSiegeState.Clock(state.Remaining(now)) : T("รออ\u0e31ปเดตสถานะ", "Updating state")));
				detail.text = ((state.Stage == "prep") ? T("เตร\u0e35ยมกำล\u0e31งพลและอ\u0e38ปกรณ\u0e4cก\u0e48อนเร\u0e34\u0e48มสงคราม", "Prepare your clan and equipment before the siege") : ((state.Stage == "war") ? T("ย\u0e36ดเสาเพ\u0e37\u0e48อชนะ · ครบเวลาผ\u0e39\u0e49ครองเสาชนะ", "Capture to win · Defenders win when time runs out") : T(string.IsNullOrEmpty(state.OwnerId) ? "เสาว\u0e48าง ย\u0e36ดได\u0e49โดยไม\u0e48ต\u0e49องประกาศ" : "ประกาศสงครามได\u0e49ท\u0e35\u0e48เมน\u0e39ท\u0e48าเร\u0e37อ", string.IsNullOrEmpty(state.OwnerId) ? "Capture this pillar without declaring war" : "Declare war through the port menu")));
				footnote.text = T("สถานะและผลสงครามย\u0e37นย\u0e31นโดยเซ\u0e34ร\u0e4cฟเวอร\u0e4c", "Siege state and results are confirmed by the server");
			}
		}
	}
}
