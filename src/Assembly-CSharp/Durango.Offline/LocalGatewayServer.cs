using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Durango.Logic.Clusters;
using Durango.Utils;
using UnityEngine;

namespace Durango.Offline;

/// <summary>
/// Embedded Zero-Configuration Local Gateway Server.
/// Compiles directly into Durango.exe / Assembly-CSharp.dll.
/// Uses raw TCP sockets (TcpListener) on 127.0.0.1:8190 and 8290 so it requires
/// zero Windows administrator privileges, zero netsh urlacl registrations, and
/// zero external Python/Node processes.
/// </summary>
public static class LocalGatewayServer
{
	private static TcpListener _listener8190;
	private static TcpListener _listener8290;
	private static Thread _thread8190;
	private static Thread _thread8290;
	private static volatile bool _running;
	private const int PrimaryPort = 8290;
	private const int SecondaryPort = 8190;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	public static void AutoStart()
	{
		EnsureStarted();
	}

	public static void EnsureStarted()
	{
		if (_running)
		{
			return;
		}

		_running = true;
		StartListener(SecondaryPort, ref _listener8190, ref _thread8190);
		StartListener(PrimaryPort, ref _listener8290, ref _thread8290);

		try
		{
			Server.EnsureLocalServer();
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[LocalGatewayServer] EnsureLocalServer error: {ex.Message}");
		}

		try
		{
			Application.quitting += Stop;
		}
		catch
		{
		}
		Debug.Log($"[LocalGatewayServer] Embedded gateway running on 127.0.0.1:{SecondaryPort} and {PrimaryPort}");
	}

	private static void StartListener(int port, ref TcpListener listener, ref Thread thread)
	{
		try
		{
			listener = new TcpListener(IPAddress.Loopback, port);
			listener.Start();
			int localPort = port;
			TcpListener l = listener;
			thread = new Thread(() => AcceptLoop(l, localPort))
			{
				IsBackground = true,
				Name = $"LocalGatewayServer_{port}"
			};
			thread.Start();
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[LocalGatewayServer] Failed to bind port {port}: {ex.Message}");
		}
	}

	private static void AcceptLoop(TcpListener listener, int port)
	{
		while (_running)
		{
			try
			{
				TcpClient client = listener.AcceptTcpClient();
				ThreadPool.QueueUserWorkItem(ProcessClient, client);
			}
			catch (SocketException)
			{
				break;
			}
			catch (Exception ex)
			{
				if (!_running) break;
				Debug.LogWarning($"[LocalGatewayServer] Accept exception on {port}: {ex.Message}");
			}
		}
	}

	private static void ProcessClient(object state)
	{
		TcpClient client = (TcpClient)state;
		try
		{
			using (client)
			using (NetworkStream stream = client.GetStream())
			{
				client.ReceiveTimeout = 5000;
				client.SendTimeout = 5000;

				byte[] buffer = new byte[8192];
				int totalRead = 0;
				int headerEnd = -1;
				while (totalRead < buffer.Length)
				{
					int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
					if (read <= 0) break;
					totalRead += read;
					headerEnd = FindHeaderEnd(buffer, totalRead);
					if (headerEnd >= 0) break;
				}

				if (headerEnd < 0) return;

				string headerText = Encoding.ASCII.GetString(buffer, 0, headerEnd);
				string[] lines = headerText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
				if (lines.Length == 0) return;

				string requestLine = lines[0];
				string[] reqParts = requestLine.Split(' ');
				if (reqParts.Length < 2) return;

				string method = reqParts[0].ToUpperInvariant();
				string fullUrl = reqParts[1];
				string path = fullUrl;
				int qIdx = path.IndexOf('?');
				string queryString = "";
				if (qIdx >= 0)
				{
					queryString = path.Substring(qIdx + 1);
					path = path.Substring(0, qIdx);
				}

				int contentLength = 0;
				string contentType = "";
				for (int i = 1; i < lines.Length; i++)
				{
					string line = lines[i];
					if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
					{
						int.TryParse(line.Substring(15).Trim(), out contentLength);
					}
					else if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
					{
						contentType = line.Substring(13).Trim();
					}
				}

				byte[] bodyBytes = new byte[contentLength];
				int bodyAlreadyRead = totalRead - (headerEnd + 4);
				if (bodyAlreadyRead > 0)
				{
					Array.Copy(buffer, headerEnd + 4, bodyBytes, 0, Math.Min(bodyAlreadyRead, contentLength));
				}
				int bodyRemaining = contentLength - bodyAlreadyRead;
				int bodyOffset = Math.Max(0, bodyAlreadyRead);
				while (bodyRemaining > 0)
				{
					int read = stream.Read(bodyBytes, bodyOffset, bodyRemaining);
					if (read <= 0) break;
					bodyOffset += read;
					bodyRemaining -= read;
				}

				HandleRoute(stream, method, path, fullUrl, queryString, bodyBytes, contentType);
			}
		}
		catch
		{
		}
	}

	private static int FindHeaderEnd(byte[] buf, int len)
	{
		for (int i = 0; i <= len - 4; i++)
		{
			if (buf[i] == 13 && buf[i + 1] == 10 && buf[i + 2] == 13 && buf[i + 3] == 10)
			{
				return i;
			}
		}
		return -1;
	}

	private static void HandleRoute(NetworkStream stream, string method, string path, string fullUrl, string queryString, byte[] bodyBytes, string contentType)
	{
		if (method == "OPTIONS")
		{
			SendResponse(stream, 200, "text/plain", new byte[0]);
			return;
		}

		if (method == "GET")
		{
			if (path == "/status")
			{
				SendJsonResponse(stream, 200, "{\"ok\":true,\"online\":1,\"version\":\"3.3.6\"}");
				return;
			}
			if (path == "/knock")
			{
				string json = "{\"server_version\":\"3.3.6\",\"compatible\":true,\"assetbundle_index_url\":\"http://127.0.0.1:8190/Info.5.2.1.json\",\"assetbundle_url_root\":\"http://127.0.0.1:8190/AssetBundles/\"}";
				SendJsonResponse(stream, 200, json);
				return;
			}
			if (path == "/Info.5.2.1.json")
			{
				string json = "{\"PreloadHash\":\"c4408d73cff6e7ad670c0c93a2b04385\",\"PreloadCrc\":\"c4408d73cff6e7ad670c0c93a2b04385\",\"ItemList\":[],\"FileList\":[]}";
				SendJsonResponse(stream, 200, json);
				return;
			}
			if (path == "/notice")
			{
				SendJsonResponse(stream, 200, "{}");
				return;
			}
			if (path == "/admission")
			{
				SendJsonResponse(stream, 200, "{\"admitted\":true}");
				return;
			}
			if (path == "/entry")
			{
				SendJsonResponse(stream, 200, "{\"frontend_addresses\":[\"127.0.0.1:8191\"],\"cluster_mode\":\"Normal\"}");
				return;
			}
			if (path.StartsWith("/assets/"))
			{
				// Returning 404 prompts Yaml.Util.Loader to load complete embedded offline resources,
				// which permanently resolves the 24% loading screen hang.
				SendJsonResponse(stream, 404, "{\"error\":\"use embedded offline yaml\",\"path\":\"" + path + "\"}");
				return;
			}
			if (path == "/terrains/1")
			{
				Server.EnsureLocalServer();
				if (Server.CurrentGameServer != null && Server.CurrentGameServer.World != null)
				{
					string json = Json.Write(Server.CurrentGameServer.World.TerrainInfo);
					SendJsonResponse(stream, 200, json);
					return;
				}
			}
			if (path == "/terrains/1/whole_biomes")
			{
				Server.EnsureLocalServer();
				if (Server.CurrentGameServer != null && Server.CurrentGameServer.World != null)
				{
					byte[] biomes = Server.CurrentGameServer.World.Biomes;
					SendResponse(stream, 200, "application/octet-stream", biomes ?? new byte[0]);
					return;
				}
			}
			if (path.StartsWith("/terrains/1/ocean"))
			{
				Server.EnsureLocalServer();
				if (Server.CurrentGameServer != null && Server.CurrentGameServer.World != null)
				{
					Point2 pt = GetPoint2FromUrl(path);
					byte[] ocean = Server.CurrentGameServer.World.GetChunkOcean(pt);
					SendResponse(stream, 200, "application/octet-stream", ocean ?? new byte[0]);
					return;
				}
			}
			if (path.StartsWith("/terrains/1/rivers"))
			{
				Server.EnsureLocalServer();
				if (Server.CurrentGameServer != null && Server.CurrentGameServer.World != null)
				{
					Point2 pt = GetPoint2FromUrl(path);
					byte[] rivers = Server.CurrentGameServer.World.GetChunkRiver(pt);
					SendResponse(stream, 200, "application/octet-stream", rivers ?? new byte[0]);
					return;
				}
			}
			if (path.StartsWith("/terrains/1/"))
			{
				Server.EnsureLocalServer();
				if (Server.CurrentGameServer != null && Server.CurrentGameServer.World != null)
				{
					Point2 pt = GetPoint2FromUrl(path);
					byte[] chunkBiomes = Server.CurrentGameServer.World.GetChunkBiomes(pt);
					byte[] chunkOcean = Server.CurrentGameServer.World.GetChunkOcean(pt);
					byte[] chunkRiver = Server.CurrentGameServer.World.GetChunkRiver(pt);
					byte[] chunkLandmark = Server.CurrentGameServer.World.GetChunkLandmark(pt);
					using (MemoryStream ms = new MemoryStream())
					{
						if (chunkBiomes != null) ms.Write(chunkBiomes, 0, chunkBiomes.Length);
						if (chunkOcean != null) ms.Write(chunkOcean, 0, chunkOcean.Length);
						if (chunkRiver != null) ms.Write(chunkRiver, 0, chunkRiver.Length);
						if (chunkLandmark != null) ms.Write(chunkLandmark, 0, chunkLandmark.Length);
						SendResponse(stream, 200, "application/octet-stream", ms.ToArray());
						return;
					}
				}
			}

			SendJsonResponse(stream, 404, "{\"error\":\"not found\"}");
			return;
		}

		if (method == "POST")
		{
			Dictionary<string, string> formFields = ParseFormFields(bodyBytes);

			if (path == "/accounts")
			{
				Server.EnsureLocalServer();
				string json;
				if (Server.CurrentPlayer != null && Server.CurrentPlayer.PlayerInfo != null && !string.IsNullOrEmpty(Server.CurrentPlayer.PlayerInfo.PlayerName))
				{
					PlayerInfo p = Server.CurrentPlayer.PlayerInfo;
					json = "{\"players\":[{\"player_level\":" + p.PlayerLevel + ",\"disconnected_at\":0.0,\"player_name\":\"" + p.PlayerName + "\",\"player_entity_id\":\"" + p.PlayerEntityId + "\",\"deletes_at\":null}],\"max_player_slot_count\":4,\"player_slot_count\":1,\"account_id\":\"local_player\",\"discord_name\":\"LocalPlayer\"}";
				}
				else
				{
					json = "{\"players\":[],\"max_player_slot_count\":4,\"player_slot_count\":1,\"account_id\":\"local_player\",\"discord_name\":\"LocalPlayer\"}";
				}
				SendJsonResponse(stream, 200, json);
				return;
			}
			if (path == "/sessions")
			{
				string userId = (Server.CurrentPlayer != null && !string.IsNullOrEmpty(Server.CurrentPlayer.EntityId)) ? Server.CurrentPlayer.EntityId : "1";
				string json = "{\"user_id\":\"" + userId + "\",\"session_token\":\"local-session-token-01\"}";
				SendJsonResponse(stream, 200, json);
				return;
			}
			if (path == "/players")
			{
				Server.EnsureLocalServer();
				Server.UpdatePlayerFromPost(formFields);
				string entityId = (Server.CurrentPlayer != null && !string.IsNullOrEmpty(Server.CurrentPlayer.EntityId)) ? Server.CurrentPlayer.EntityId : "1001";
				SendJsonResponse(stream, 200, "{\"entity_id\":\"" + entityId + "\"}");
				return;
			}

			SendJsonResponse(stream, 404, "{\"error\":\"not found\"}");
			return;
		}

		SendJsonResponse(stream, 400, "{\"error\":\"bad request\"}");
	}

	private static void SendJsonResponse(NetworkStream stream, int statusCode, string json)
	{
		byte[] body = Encoding.UTF8.GetBytes(json);
		SendResponse(stream, statusCode, "application/json; charset=utf-8", body);
	}

	private static void SendResponse(NetworkStream stream, int statusCode, string contentType, byte[] body)
	{
		string statusText = statusCode switch
		{
			200 => "OK",
			400 => "Bad Request",
			404 => "Not Found",
			_ => "OK"
		};
		string headers = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
		                 $"Content-Type: {contentType}\r\n" +
		                 $"Content-Length: {body.Length}\r\n" +
		                 "Connection: close\r\n" +
		                 "Access-Control-Allow-Origin: *\r\n" +
		                 "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
		                 "Access-Control-Allow-Headers: Content-Type, Authorization, X-Community-Client\r\n" +
		                 "\r\n";
		byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
		stream.Write(headerBytes, 0, headerBytes.Length);
		if (body.Length > 0)
		{
			stream.Write(body, 0, body.Length);
		}
		stream.Flush();
	}

	private static Dictionary<string, string> ParseFormFields(byte[] bodyBytes)
	{
		Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (bodyBytes == null || bodyBytes.Length == 0) return dict;

		string text = Encoding.UTF8.GetString(bodyBytes);
		string[] pairs = text.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string pair in pairs)
		{
			string[] kv = pair.Split(new[] { '=' }, 2);
			if (kv.Length == 2)
			{
				try
				{
					string key = Uri.UnescapeDataString(kv[0]);
					string val = Uri.UnescapeDataString(kv[1].Replace("+", " "));
					dict[key] = val;
				}
				catch
				{
				}
			}
		}
		return dict;
	}

	private static Point2 GetPoint2FromUrl(string url)
	{
		try
		{
			int num = url.LastIndexOf("/", StringComparison.Ordinal) + 1;
			string[] array = url.Substring(num, url.Length - num).Split(',');
			int x = int.TryParse(array[0], out int px) ? px : 0;
			int y = int.TryParse(array[1], out int py) ? py : 0;
			return new Point2(x, y);
		}
		catch
		{
			return new Point2(0, 0);
		}
	}

	public static void Stop()
	{
		_running = false;
		try { _listener8190?.Stop(); } catch { }
		try { _listener8290?.Stop(); } catch { }
		_listener8190 = null;
		_listener8290 = null;
	}
}
