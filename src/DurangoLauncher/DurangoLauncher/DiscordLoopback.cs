using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public sealed class DiscordLoopback : IDisposable
{
	public sealed record Callback(string? Code, string? State, string? Error);

	public static readonly int[] Ports = new int[3] { 38431, 38432, 38433 };

	private readonly TcpListener _listener;

	public int Port { get; }

	public string RedirectUri => $"http://127.0.0.1:{Port}/callback";

	private DiscordLoopback(TcpListener l, int port)
	{
		_listener = l;
		Port = port;
	}

	public static DiscordLoopback? TryOpen()
	{
		int[] ports = Ports;
		foreach (int num in ports)
		{
			TcpListener tcpListener = new TcpListener(IPAddress.Loopback, num);
			try
			{
				tcpListener.Server.ExclusiveAddressUse = true;
				tcpListener.Start();
				return new DiscordLoopback(tcpListener, num);
			}
			catch (SocketException ex)
			{
				Log.Write($"loopback {num}: {ex.SocketErrorCode}");
				tcpListener.Stop();
			}
		}
		return null;
	}

	public async Task<Callback> WaitAsync(CancellationToken ct)
	{
		while (true)
		{
			using TcpClient client = await _listener.AcceptTcpClientAsync(ct);
			try
			{
				using CancellationTokenSource io = CancellationTokenSource.CreateLinkedTokenSource(ct);
				io.CancelAfter(TimeSpan.FromSeconds(10L));
				NetworkStream stream = client.GetStream();
				var (text, q) = ParseRequestLine(await ReadHeadAsync(stream, io.Token));
				if (!(text != "/callback"))
				{
					Callback cb = new Callback(Get(q, "code"), Get(q, "state"), Get(q, "error"));
					await WriteAsync(stream, 200, "text/html; charset=utf-8", Page(cb.Error == null), io.Token);
					return cb;
				}
				await WriteAsync(stream, 404, "text/plain; charset=utf-8", "not found", io.Token);
			}
			catch (OperationCanceledException) when (!ct.IsCancellationRequested)
			{
				goto end_IL_008d;
			}
			catch (IOException)
			{
				goto end_IL_008d;
			}
			end_IL_008d:;
		}
	}

	private static async Task<string> ReadHeadAsync(NetworkStream s, CancellationToken ct)
	{
		byte[] buf = new byte[8192];
		int n = 0;
		while (n < buf.Length)
		{
			int num = await s.ReadAsync(buf.AsMemory(n), ct);
			if (num == 0)
			{
				break;
			}
			n += num;
			if (Encoding.ASCII.GetString(buf, 0, n).Contains("\r\n\r\n"))
			{
				break;
			}
		}
		return Encoding.ASCII.GetString(buf, 0, n);
	}

	public static (string Path, Dictionary<string, string> Query) ParseRequestLine(string head)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		string[] array = head.Split('\n')[0].Trim().Split(' ');
		if (array.Length < 2 || array[0] != "GET")
		{
			return (Path: "", Query: dictionary);
		}
		string text = array[1];
		int num = text.IndexOf('?');
		string item = ((num < 0) ? text : text.Substring(0, num));
		if (num >= 0)
		{
			string text2 = text;
			int num2 = num + 1;
			string[] array2 = text2.Substring(num2, text2.Length - num2).Split('&', StringSplitOptions.RemoveEmptyEntries);
			foreach (string text3 in array2)
			{
				int num3 = text3.IndexOf('=');
				string key = Uri.UnescapeDataString(((num3 < 0) ? text3 : text3.Substring(0, num3)).Replace('+', ' '));
				object obj;
				if (num3 >= 0)
				{
					text2 = text3;
					num2 = num3 + 1;
					obj = Uri.UnescapeDataString(text2.Substring(num2, text2.Length - num2).Replace('+', ' '));
				}
				else
				{
					obj = "";
				}
				string value = (string)obj;
				dictionary[key] = value;
			}
		}
		return (Path: item, Query: dictionary);
	}

	private static string? Get(Dictionary<string, string> q, string k)
	{
		if (!q.TryGetValue(k, out string value) || value.Length <= 0)
		{
			return null;
		}
		return value;
	}

	private static async Task WriteAsync(NetworkStream s, int status, string type, string body, CancellationToken ct)
	{
		byte[] b = Encoding.UTF8.GetBytes(body);
		string s2 = $"HTTP/1.1 {status} {((status == 200) ? "OK" : "Not Found")}\r\nContent-Type: {type}\r\nContent-Length: {b.Length}\r\n" + "Cache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\n\r\n";
		await s.WriteAsync(Encoding.ASCII.GetBytes(s2), ct);
		await s.WriteAsync(b, ct);
		await s.FlushAsync(ct);
	}

	private static string Page(bool ok)
	{
		string text = (ok ? "ย\u0e37นย\u0e31น Discord แล\u0e49ว" : "ยกเล\u0e34กการเช\u0e37\u0e48อม Discord แล\u0e49ว");
		string text2 = (ok ? "Discord verified" : "Discord sign-in was cancelled");
		string text3 = L.T("กล\u0e31บไปท\u0e35\u0e48ร\u0e31นเช\u0e48อได\u0e49เลย — ป\u0e34ดแท\u0e47บน\u0e35\u0e49ได\u0e49", "You can go back to the launcher — this tab can be closed");
		string text4 = L.T("You can go back to the launcher", "กล\u0e31บไปท\u0e35\u0e48ร\u0e31นเช\u0e48อได\u0e49เลย");
		return "<!doctype html><html lang=\"" + (L.En ? "en" : "th") + "\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Durango Launcher</title><style>body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#0C0A12;color:#F5F2FB;font-family:Kanit,'Leelawadee UI',Tahoma,system-ui,sans-serif}.c{background:#15121E;border:1px solid #2B253D;border-radius:14px;padding:28px 32px;max-width:420px;text-align:center}.d{font-size:30px;font-weight:600;color:#F5C518}.r{color:#B8A4FF;letter-spacing:4px;font-size:13px;margin-bottom:18px}h1{font-size:21px;font-weight:500;margin:0 0 8px;color:" + (ok ? "#5BD68A" : "#F0806E") + "}p{margin:4px 0;color:#B0A9C4;font-size:15px}.m{color:#7A7391;font-size:13px}</style></head><body><div class=\"c\"><div class=\"d\">Durango</div><div class=\"r\">reborn</div><h1>" + (L.En ? text2 : text) + "</h1><p>" + text3 + "</p><p class=\"m\">" + text4 + "</p></div></body></html>";
	}

	public void Dispose()
	{
		try
		{
			_listener.Stop();
		}
		catch
		{
		}
	}
}
