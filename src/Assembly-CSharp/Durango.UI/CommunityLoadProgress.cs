using Durango.Logic.Clusters;
using UnityEngine;

namespace Durango.UI;

public static class CommunityLoadProgress
{
	public enum Stage
	{
		Connect,
		Data,
		Island
	}

	public const float TitleShare = 0.45f;

	public const string BarSprite = "bg_skill_gauge";

	public const int BarHeight = 18;

	public static readonly Color BarColor = new Color(0.080774225f, 83f / 136f, 0.34736007f, 0.5019608f);

	private static float _target;

	private static float _shown;

	private static Stage _stage;

	public static float Value => _shown;

	public static void Begin()
	{
		_target = 0f;
		_shown = 0f;
		_stage = Stage.Connect;
		CommunityLink.BeginLoadRun();
	}

	public static void EnterIsland()
	{
		if (_target > 0.45099998f)
		{
			_target = 0f;
			_shown = 0f;
		}
	}

	public static float Tick(Stage stage, float target)
	{
		_stage = stage;
		_target = Mathf.Max(_target, Mathf.Clamp01(target));
		_shown = Mathf.MoveTowards(_shown, _target, Time.unscaledDeltaTime * 0.8f);
		return _shown;
	}

	public static string Text()
	{
		return StageText() + "  " + Mathf.FloorToInt(_shown * 100f) + "%";
	}

	private static string StageText()
	{
		string lang = CommunityDiscordLink.Lang;
		return _stage switch
		{
			Stage.Connect => lang switch
			{
				"id" => "Menghubungkan ke server",
				"ko" => "서버에 접속 중",
				_ => "Connecting to server",
			},
			Stage.Data => lang switch
			{
				"id" => "Memuat data game",
				"ko" => "게임 데이터 불러오는 중",
				_ => "Loading game data",
			},
			_ => lang switch
			{
				"id" => "Memuat pulau",
				"ko" => "섬 불러오는 중",
				_ => "Loading island",
			},
		};
	}
}
