using System;

namespace DurangoLauncher;

public sealed class ApiError : Exception
{
	public string Code { get; }

	public int Status { get; }

	public int? AttemptsLeft { get; init; }

	public int? RetryAfter { get; init; }

	public string? Username { get; init; }

	public string? Reason { get; init; }

	public int? Wait { get; init; }

	public int? Hours { get; init; }

	public long? Price { get; init; }

	public long? Fee { get; init; }

	public string? Feature { get; init; }

	public string? Who { get; init; }

	public string? SignupToken { get; init; }

	public string? LoginToken { get; init; }

	public string DiscordId { get; init; } = "";

	public string DiscordName { get; init; } = "";

	public string DiscordAvatar { get; init; } = "";

	public long? UsedAt { get; init; }

	public long? Since { get; init; }

	public string? HoursText { get; init; }

	public bool IsWrongPassword
	{
		get
		{
			string code = Code;
			if (code == "bad_credentials" || code == "invalid_credentials")
			{
				return true;
			}
			return false;
		}
	}

	public bool IsAuthDead
	{
		get
		{
			string code = Code;
			if ((!(code == "invalid_refresh") && !(code == "token_expired")) || 1 == 0)
			{
				if (Status == 401)
				{
					return !IsWrongPassword;
				}
				return false;
			}
			return true;
		}
	}

	public ApiError(string code, string message, int status = 0)
		: base(message)
	{
		Code = code;
		Status = status;
	}
}
