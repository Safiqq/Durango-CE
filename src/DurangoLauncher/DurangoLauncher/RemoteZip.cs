using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public sealed class RemoteZip
{
	public sealed record Entry(string Name, int Method, long CompressedSize, long Size, long HeaderOffset)
	{
		public long End { get; internal set; }
	}

	private sealed class NetStream : Stream
	{
		private readonly Stream _inner;

		private readonly TimeSpan _stall;

		private readonly Action<long> _onRead;

		public long Consumed { get; private set; }

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public override long Position
		{
			get
			{
				return Consumed;
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		public NetStream(Stream inner, TimeSpan stall, Action<long> onRead)
		{
			_inner = inner;
			_stall = stall;
			_onRead = onRead;
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default(CancellationToken))
		{
			using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(_stall);
			int num;
			try
			{
				num = await _inner.ReadAsync(buffer, cts.Token);
			}
			catch (OperationCanceledException) when (!ct.IsCancellationRequested)
			{
				throw new TimeoutException("download stalled");
			}
			if (num == 0 && buffer.Length > 0)
			{
				throw new IOException("connection closed early");
			}
			Consumed += num;
			_onRead(Consumed);
			return num;
		}

		public async Task SkipAsync(long count, byte[] buf, CancellationToken ct)
		{
			if (count < 0)
			{
				throw new InvalidDataException("zip: overlapping entries");
			}
			while (count > 0)
			{
				long num = count;
				count = num - await ReadAsync(buf.AsMemory(0, (int)Math.Min(buf.Length, count)), ct);
			}
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}
	}

	private sealed class SubStream : Stream
	{
		private readonly NetStream _inner;

		private long _left;

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public override long Position
		{
			get
			{
				throw new NotSupportedException();
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		public SubStream(NetStream inner, long length)
		{
			_inner = inner;
			_left = length;
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default(CancellationToken))
		{
			if (_left <= 0 || buffer.Length == 0)
			{
				return 0;
			}
			int num = await _inner.ReadAsync(buffer.Slice(0, (int)Math.Min(buffer.Length, _left)), ct);
			_left -= num;
			return num;
		}

		public async Task DrainAsync(byte[] buf, CancellationToken ct)
		{
			await _inner.SkipAsync(_left, buf, ct);
			_left = 0L;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}
	}

	private const long MergeGap = 1048576L;

	private const int Retries = 5;

	private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60L);

	private static readonly HttpClient Http = CreateHttp();

	private readonly string _url;

	public long Length { get; }

	public IReadOnlyDictionary<string, Entry> Entries { get; }

	private static HttpClient CreateHttp()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromMinutes(2L);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DurangoLauncher/1.0 (+https://github.com/Safiqq/Durango-CE)");
		return httpClient;
	}

	private RemoteZip(string url, long length, Dictionary<string, Entry> entries)
	{
		_url = url;
		Length = length;
		Entries = entries;
	}

	public static async Task<RemoteZip> OpenAsync(string url, CancellationToken ct)
	{
		long length;
		using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url))
		{
			req.Headers.Range = new RangeHeaderValue(0L, 0L);
			using HttpResponseMessage httpResponseMessage = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
			httpResponseMessage.EnsureSuccessStatusCode();
			if (httpResponseMessage.StatusCode != HttpStatusCode.PartialContent)
			{
				goto IL_011d;
			}
			long? num = httpResponseMessage.Content.Headers.ContentRange?.Length;
			if (!num.HasValue)
			{
				goto IL_011d;
			}
			long valueOrDefault = num.GetValueOrDefault();
			length = valueOrDefault;
			goto end_IL_00cb;
			IL_011d:
			throw new InvalidOperationException(L.T("ท\u0e35\u0e48เก\u0e47บไฟล\u0e4cไม\u0e48รองร\u0e31บการโหลดเป\u0e47นช\u0e48วง (Range)", "The file host does not support ranged downloads"));
			end_IL_00cb:;
		}
		int num2 = (int)Math.Min(length, 65577L);
		long tailStart = length - num2;
		byte[] array = await GetRangeAsync(url, tailStart, num2, ct);
		int num3 = -1;
		for (int num4 = array.Length - 22; num4 >= 0; num4--)
		{
			if (U32(array, num4) == 101010256)
			{
				num3 = num4;
				break;
			}
		}
		if (num3 < 0)
		{
			throw new InvalidDataException("zip: EOCD not found");
		}
		long count = U16(array, num3 + 10);
		long num5 = U32(array, num3 + 12);
		long cdOffset = U32(array, num3 + 16);
		if (num3 >= 20 && U32(array, num3 - 20) == 117853008)
		{
			long num6 = (long)U64(array, num3 - 20 + 8);
			byte[] array2 = ((num6 < tailStart || num6 + 56 > length) ? (await GetRangeAsync(url, num6, 56, ct)) : array.AsSpan((int)(num6 - tailStart), 56).ToArray());
			byte[] b = array2;
			if (U32(b, 0) != 101075792)
			{
				throw new InvalidDataException("zip64: EOCD record not found");
			}
			count = (long)U64(b, 32);
			num5 = (long)U64(b, 40);
			cdOffset = (long)U64(b, 48);
		}
		byte[] array3 = await GetRangeAsync(url, cdOffset, checked((int)num5), ct);
		List<Entry> list = new List<Entry>((int)count);
		int num9;
		int num10;
		int num11;
		for (int i = 0; i + 46 <= array3.Length && U32(array3, i) == 33639248; i += 46 + num9 + num10 + num11)
		{
			int method = U16(array3, i + 10);
			long num7 = U32(array3, i + 20);
			long num8 = U32(array3, i + 24);
			num9 = U16(array3, i + 28);
			num10 = U16(array3, i + 30);
			num11 = U16(array3, i + 32);
			long num12 = U32(array3, i + 42);
			string entryName = Encoding.UTF8.GetString(array3, i + 46, num9);
			int j = i + 46 + num9;
			int num15;
			for (int num13 = j + num10; j + 4 <= num13; j += 4 + num15)
			{
				int num14 = U16(array3, j);
				num15 = U16(array3, j + 2);
				int num16 = j + 4;
				if (num14 == 1)
				{
					if (num8 == uint.MaxValue && num16 + 8 <= j + 4 + num15)
					{
						num8 = (long)U64(array3, num16);
						num16 += 8;
					}
					if (num7 == uint.MaxValue && num16 + 8 <= j + 4 + num15)
					{
						num7 = (long)U64(array3, num16);
						num16 += 8;
					}
					if (num12 == uint.MaxValue && num16 + 8 <= j + 4 + num15)
					{
						num12 = (long)U64(array3, num16);
					}
				}
			}
			list.Add(new Entry(Normalize(entryName), method, num7, num8, num12));
		}
		list.Sort((Entry a, Entry entry) => a.HeaderOffset.CompareTo(entry.HeaderOffset));
		for (int num17 = 0; num17 < list.Count; num17++)
		{
			list[num17].End = ((num17 + 1 < list.Count) ? list[num17 + 1].HeaderOffset : cdOffset);
		}
		string text = RootFolder(list);
		Dictionary<string, Entry> dictionary = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
		foreach (Entry item in list)
		{
			if (!item.Name.EndsWith('/'))
			{
				string text2;
				if (text.Length <= 0 || !item.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
				{
					text2 = item.Name;
				}
				else
				{
					string name = item.Name;
					int length2 = text.Length;
					text2 = name.Substring(length2, name.Length - length2);
				}
				string text3 = text2;
				if (text3.Length > 0)
				{
					dictionary[text3] = item;
				}
			}
		}
		Log.Write($"zip {url} · {length:N0} B · {dictionary.Count} ไฟล\u0e4c" + ((text.Length > 0) ? (" · ห\u0e48อด\u0e49วย " + text) : ""));
		return new RemoteZip(url, length, dictionary);
	}

	public static long DownloadBytes(IEnumerable<Entry> entries)
	{
		return Groups(entries).Sum((List<Entry> g) => g[g.Count - 1].End - g[0].HeaderOffset);
	}

	public async Task ExtractAsync(IEnumerable<Entry> entries, Func<Entry, string> tempPath, Action<Entry, string> done, Action<long> bytes, CancellationToken ct)
	{
		long groupsDone = 0L;
		byte[] buf = new byte[1048576];
		byte[] lh = new byte[30];
		foreach (List<Entry> group in Groups(entries))
		{
			long groupStart = group[0].HeaderOffset;
			long groupEnd = group[group.Count - 1].End;
			int next = 0;
			int attempt = 0;
			while (next < group.Count)
			{
				long start = group[next].HeaderOffset;
				long before = groupsDone + (start - groupStart);
				try
				{
					using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, _url);
					req.Headers.Range = new RangeHeaderValue(start, groupEnd - 1);
					using HttpResponseMessage resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
					resp.EnsureSuccessStatusCode();
					if (resp.StatusCode != HttpStatusCode.PartialContent)
					{
						throw new InvalidOperationException(L.T("ท\u0e35\u0e48เก\u0e47บไฟล\u0e4cไม\u0e48รองร\u0e31บการโหลดเป\u0e47นช\u0e48วง (Range)", "The file host does not support ranged downloads"));
					}
					await using Stream raw = await resp.Content.ReadAsStreamAsync(ct);
					NetStream net = new NetStream(raw, StallTimeout, delegate(long n)
					{
						bytes(before + n);
					});
					long num = start;
					for (; next < group.Count; next++)
					{
						Entry e = group[next];
						await net.SkipAsync(e.HeaderOffset - num, buf, ct);
						await net.ReadExactlyAsync(lh, ct);
						if (U32(lh, 0) != 67324752)
						{
							throw new InvalidDataException("zip: bad local header " + e.Name);
						}
						int skip = U16(lh, 26) + U16(lh, 28);
						await net.SkipAsync(skip, buf, ct);
						string arg = await WriteEntryAsync(net, e, tempPath(e), buf, ct);
						num = e.HeaderOffset + 30 + skip + e.CompressedSize;
						done(e, arg);
					}
				}
				catch (Exception ex) when (((ex is IOException || ex is HttpRequestException || ex is TimeoutException) ? true : false) && !ct.IsCancellationRequested)
				{
					int num2 = attempt + 1;
					attempt = num2;
					if (num2 > 5)
					{
						throw;
					}
					Log.Write($"zip: เน\u0e47ตสะด\u0e38ด ({ex.Message}) · ลองใหม\u0e48คร\u0e31\u0e49งท\u0e35\u0e48 {attempt} จาก {group[next].Name}");
					await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
				}
			}
			groupsDone += groupEnd - groupStart;
			bytes(groupsDone);
		}
	}

	private static async Task<string> WriteEntryAsync(NetStream net, Entry e, string path, byte[] buf, CancellationToken ct)
	{
		SubStream limited = new SubStream(net, e.CompressedSize);
		Stream data = e.Method switch
		{
			0 => limited, 
			8 => new DeflateStream(limited, CompressionMode.Decompress, leaveOpen: true), 
			_ => throw new NotSupportedException($"zip: compression method {e.Method} ({e.Name})"), 
		};
		using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		await using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
		{
			int num;
			while ((num = await data.ReadAsync(buf, ct)) > 0)
			{
				hash.AppendData(buf, 0, num);
				await fs.WriteAsync(buf.AsMemory(0, num), ct);
			}
		}
		if (data != limited)
		{
			await data.DisposeAsync();
		}
		await limited.DrainAsync(buf, ct);
		return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
	}

	private static List<List<Entry>> Groups(IEnumerable<Entry> entries)
	{
		List<List<Entry>> list = new List<List<Entry>>();
		foreach (Entry item in from e in entries.Distinct()
			orderby e.HeaderOffset
			select e)
		{
			if (list.Count > 0)
			{
				long headerOffset = item.HeaderOffset;
				List<Entry> list2 = list[list.Count - 1];
				if (headerOffset - list2[list2.Count - 1].End <= 1048576)
				{
					list[list.Count - 1].Add(item);
					continue;
				}
			}
			list.Add(new List<Entry> { item });
		}
		return list;
	}

	private static async Task<byte[]> GetRangeAsync(string url, long offset, int count, CancellationToken ct)
	{
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
		req.Headers.Range = new RangeHeaderValue(offset, offset + count - 1);
		using HttpResponseMessage resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
		resp.EnsureSuccessStatusCode();
		if (resp.StatusCode != HttpStatusCode.PartialContent)
		{
			throw new InvalidOperationException("zip: Range not honoured");
		}
		byte[] array = await resp.Content.ReadAsByteArrayAsync(ct);
		if (array.Length != count)
		{
			throw new IOException($"zip: got {array.Length} of {count} bytes");
		}
		return array;
	}

	private static string RootFolder(List<Entry> all)
	{
		string root = null;
		foreach (Entry item in all)
		{
			int num = item.Name.IndexOf('/');
			if (num < 0)
			{
				return "";
			}
			string text = item.Name.Substring(0, num + 1);
			if (root == null)
			{
				root = text;
			}
			else if (!string.Equals(root, text, StringComparison.OrdinalIgnoreCase))
			{
				return "";
			}
		}
		if (root == null || !all.Any((Entry e) => string.Equals(e.Name, root + "Durango.exe", StringComparison.OrdinalIgnoreCase)))
		{
			return "";
		}
		return root;
	}

	private static string Normalize(string entryName)
	{
		string text = entryName.Replace('\\', '/');
		while (text.StartsWith("./"))
		{
			string text2 = text;
			text = text2.Substring(2, text2.Length - 2);
		}
		return text;
	}

	private static int U16(byte[] b, int i)
	{
		return BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(i));
	}

	private static long U32(byte[] b, int i)
	{
		return BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(i));
	}

	private static ulong U64(byte[] b, int i)
	{
		return BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(i));
	}
}
