using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Durango.Offline;

/// <summary>
/// Embedded Zero-Configuration Local Gateway Server.
/// Compiles directly into Durango.exe / Assembly-CSharp.dll.
/// Automatically starts on 127.0.0.1:8290 (and 8190) in the background so
/// the game can run 100% standalone out of the box without requiring external
/// server scripts or background processes.
/// </summary>
public static class LocalGatewayServer
{
	private static HttpListener _listener;
	private static Thread _serverThread;
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

		try
		{
			_listener = new HttpListener();
			// Always bind loopback
			_listener.Prefixes.Add($"http://127.0.0.1:{PrimaryPort}/");
			_listener.Prefixes.Add($"http://127.0.0.1:{SecondaryPort}/");
			_listener.Prefixes.Add($"http://localhost:{PrimaryPort}/");
			_listener.Prefixes.Add($"http://localhost:{SecondaryPort}/");

			// Expose on LAN / Wi-Fi network interfaces so other local players can join
			try
			{
				string hostName = Dns.GetHostName();
				IPAddress[] localIps = Dns.GetHostAddresses(hostName);
				foreach (IPAddress ip in localIps)
				{
					if (ip.AddressFamily == global::System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
					{
						string lanIp = ip.ToString();
						_listener.Prefixes.Add($"http://{lanIp}:{PrimaryPort}/");
						_listener.Prefixes.Add($"http://{lanIp}:{SecondaryPort}/");
						Debug.Log($"[EmbeddedGateway] LAN multiplayer enabled on {lanIp}:{SecondaryPort}");
					}
				}
			}
			catch (Exception ex)
			{
				Debug.Log($"[EmbeddedGateway] LAN IP discovery note: {ex.Message}");
			}

			_listener.Start();
			_running = true;

			_serverThread = new Thread(ListenLoop)
			{
				IsBackground = true,
				Name = "EmbeddedGatewayServer"
			};
			_serverThread.Start();
			Application.quitting += Stop;
			Debug.Log($"[EmbeddedGateway] Running on ports {PrimaryPort}/{SecondaryPort} (Local + LAN)");
		}
		catch (Exception ex)
		{
			// Fallback: If registering LAN prefixes requires administrative elevation, fall back strictly to loopback
			try
			{
				_listener?.Close();
				_listener = new HttpListener();
				_listener.Prefixes.Add($"http://127.0.0.1:{PrimaryPort}/");
				_listener.Prefixes.Add($"http://127.0.0.1:{SecondaryPort}/");
				_listener.Start();
				_running = true;

				_serverThread = new Thread(ListenLoop)
				{
					IsBackground = true,
					Name = "EmbeddedGatewayServer"
				};
				_serverThread.Start();
				Application.quitting += Stop;
				Debug.Log($"[EmbeddedGateway] Loopback fallback active on 127.0.0.1:{SecondaryPort}");
			}
			catch (Exception ex2)
			{
				Debug.Log($"[EmbeddedGateway] Listener start failed: {ex2.Message}");
				_running = false;
			}
		}
	}

	private static void ListenLoop()
	{
		while (_running && _listener != null && _listener.IsListening)
		{
			try
			{
				HttpListenerContext context = _listener.GetContext();
				ThreadPool.QueueUserWorkItem((state) => HandleRequest((HttpListenerContext)state), context);
			}
			catch (HttpListenerException)
			{
				break;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[EmbeddedGateway] Listener exception: {ex.Message}");
			}
		}
	}

	private static void HandleRequest(HttpListenerContext context)
	{
		try
		{
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;

			string path = request.Url.AbsolutePath;
			string method = request.HttpMethod.ToUpperInvariant();

			string jsonResponse = null;
			int statusCode = 200;

			if (method == "GET")
			{
				if (path == "/status")
				{
					jsonResponse = "{\"ok\":true,\"online\":1,\"version\":\"3.3.6\"}";
				}
				else if (path == "/knock")
				{
					string host = request.Url.Host;
					int port = request.Url.Port;
					jsonResponse = "{\"server_version\":\"3.3.6\",\"compatible\":true,\"assetbundle_index_url\":\"http://" + host + ":" + port + "/Info.5.2.1.json\",\"assetbundle_url_root\":\"http://" + host + ":" + port + "/AssetBundles/\"}";
				}
				else if (path == "/Info.5.2.1.json")
				{
					jsonResponse = "{\"PreloadHash\":\"c4408d73cff6e7ad670c0c93a2b04385\",\"PreloadCrc\":\"c4408d73cff6e7ad670c0c93a2b04385\",\"ItemList\":[],\"FileList\":[]}";
				}
				else if (path == "/notice")
				{
					jsonResponse = "{}";
				}
				else if (path == "/admission")
				{
					jsonResponse = "{\"admitted\":true}";
				}
				else if (path == "/entry")
				{
					string host = request.Url.Host;
					jsonResponse = "{\"frontend_addresses\":[\"" + host + ":8191\"],\"cluster_mode\":\"Normal\"}";
				}
				else if (path.StartsWith("/assets/"))
				{
					// Returning 404 prompts Yaml.Util.Loader to load complete embedded offline resources,
					// which permanently resolves the 24% loading screen hang.
					statusCode = 404;
					jsonResponse = "{\"error\":\"use embedded offline yaml\",\"path\":\"" + path + "\"}";
				}
				else
				{
					statusCode = 404;
					jsonResponse = "{\"error\":\"not found\"}";
				}
			}
			else if (method == "POST")
			{
				if (path == "/accounts")
				{
					jsonResponse = "{\"players\":[],\"max_player_slot_count\":4,\"player_slot_count\":1,\"account_id\":\"local_player\",\"discord_name\":\"LocalPlayer\"}";
				}
				else if (path == "/sessions")
				{
					jsonResponse = "{\"user_id\":\"1\",\"session_token\":\"local-session-token-01\"}";
				}
				else if (path == "/players")
				{
					jsonResponse = "{\"entity_id\":\"1001\"}";
				}
				else
				{
					statusCode = 404;
					jsonResponse = "{\"error\":\"not found\"}";
				}
			}

			byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse ?? "{}");
			response.StatusCode = statusCode;
			response.ContentType = "application/json; charset=utf-8";
			response.ContentLength64 = buffer.Length;
			response.OutputStream.Write(buffer, 0, buffer.Length);
			response.OutputStream.Close();
		}
		catch
		{
		}
	}

	public static void Stop()
	{
		_running = false;
		try
		{
			_listener?.Stop();
			_listener?.Close();
		}
		catch
		{
		}
	}
}
