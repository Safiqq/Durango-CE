using System;
using System.Collections.Generic;
using BestHTTP;
using Durango.Utils;

public class SendReportSystem : GameSystem<SendReportSystem>
{
	public enum ReportType
	{
		Players,
		Scribbles,
		Nameables,
		Clans,
		ServerStatus,
		Suggestion
	}

	public enum PlayerReportCategory
	{
		None,
		ImproperName,
		Insult,
		Cheating,
		Spam,
		Etc
	}

	public enum Response
	{
		Done,
		BadRequest,
		NotFound,
		Conflict,
		Error
	}

	public void SendReport(ReportType type, string entityId, PlayerReportCategory category, string content, Action<Response> callback)
	{
		string value = "none";
		switch (category)
		{
		case PlayerReportCategory.ImproperName:
			value = "improper_name";
			break;
		case PlayerReportCategory.Insult:
			value = "insult";
			break;
		case PlayerReportCategory.Cheating:
			value = "cheating";
			break;
		case PlayerReportCategory.Spam:
			value = "spam";
			break;
		case PlayerReportCategory.Etc:
			value = "etc";
			break;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		dictionary.Add("type", type.ToString());
		dictionary.Add("reporter_id", PlayerBehavior.LocalPlayer.EntityId);
		dictionary.Add("reportee_id", entityId);
		dictionary.Add("category", value);
		dictionary.Add("text", content);
		Http.Request(GameManager.GatewayUrl + "/reports", delegate(byte[] bytes, HTTPResponse response)
		{
			Response obj = Response.Error;
			if (response != null)
			{
				if (response.IsSuccess)
				{
					obj = Response.Done;
				}
				else
				{
					switch (response.StatusCode)
					{
					case 400:
						obj = Response.BadRequest;
						break;
					case 404:
						obj = Response.NotFound;
						break;
					case 409:
						obj = Response.Conflict;
						break;
					}
				}
			}
			if (callback != null)
			{
				callback(obj);
			}
		}, disableCache: true, addSession: true, dictionary, HTTPMethods.Post);
	}

	public void SendServerStatus(string text, string title, Action<bool> callback)
	{
		// SANITIZED: the original method uploaded the player name, entity ID, client
		// version, system language, server key and recent errors to a remote reporting
		// endpoint. It is now a local no-op; the UI simply sees a failed submission.
		callback?.Invoke(false);
	}
}
