using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public static class ChunkSync
{
	public sealed record Chunk(int Len, string H, long Off, int CLen, bool Z);

	public sealed record FileChunks(string Sha, long Size, Chunk[] Chunks);

	public sealed record Index(string Version, long BlocksSize, Dictionary<string, FileChunks> Files);

	public interface IRangeSource
	{
		Task<byte[]> ReadAsync(long offset, int count, CancellationToken ct);
	}

	public sealed class HttpRangeSource(HttpClient http, string url) : IRangeSource
	{
		public async Task<byte[]> ReadAsync(long offset, int count, CancellationToken ct)
		{
			using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(TimeSpan.FromSeconds(60 + count / 32768));
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
			req.Headers.Range = new RangeHeaderValue(offset, offset + count - 1);
			using HttpResponseMessage resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
			resp.EnsureSuccessStatusCode();
			if (resp.StatusCode == HttpStatusCode.PartialContent)
			{
				ContentRangeHeaderValue? contentRange = resp.Content.Headers.ContentRange;
				if (contentRange != null && contentRange.From == offset)
				{
					byte[] array = await resp.Content.ReadAsByteArrayAsync(cts.Token);
					if (array.Length != count)
					{
						throw new IOException($"blocks: ได\u0e49 {array.Length} B จาก {count}");
					}
					return array;
				}
			}
			throw new InvalidOperationException("blocks: Range not honoured");
		}
	}

	public sealed class FileRangeSource(string path) : IRangeSource
	{
		public Task<byte[]> ReadAsync(long offset, int count, CancellationToken ct)
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			byte[] array = new byte[count];
			fileStream.Position = offset;
			fileStream.ReadExactly(array);
			return Task.FromResult(array);
		}
	}

	public sealed record FilePlan(string Rel, FileChunks Info, string OldPath, Dictionary<string, (long Off, int Len)> Local, List<Chunk> Remote)
	{
		public long RemoteBytes => ((IEnumerable<Chunk>)Remote).Sum((Func<Chunk, long>)((Chunk c) => c.CLen));
	}

	public const double MaxRemoteRatio = 0.7;

	public const long MergeGap = 262144L;

	public const long MaxRange = 4194304L;

	private const int Retries = 3;

	public static Index ParseIndex(byte[] gz)
	{
		using GZipStream utf8Json = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress);
		using JsonDocument jsonDocument = JsonDocument.Parse(utf8Json);
		JsonElement rootElement = jsonDocument.RootElement;
		if (rootElement.GetProperty("format").GetInt32() != 1)
		{
			throw new InvalidDataException("chunks: format ไม\u0e48ร\u0e39\u0e49จ\u0e31ก");
		}
		Dictionary<string, FileChunks> dictionary = new Dictionary<string, FileChunks>(StringComparer.OrdinalIgnoreCase);
		foreach (JsonProperty item in rootElement.GetProperty("files").EnumerateObject())
		{
			Chunk[] chunks = (from c in item.Value.GetProperty("chunks").EnumerateArray()
				select new Chunk(c[0].GetInt32(), c[1].GetString(), c[2].GetInt64(), c[3].GetInt32(), c[4].GetInt32() == 1)).ToArray();
			dictionary[item.Name.Replace('\\', '/')] = new FileChunks(item.Value.GetProperty("sha256").GetString().ToLowerInvariant(), item.Value.GetProperty("size").GetInt64(), chunks);
		}
		JsonElement value;
		return new Index(rootElement.TryGetProperty("version", out value) ? (value.GetString() ?? "") : "", rootElement.GetProperty("blocks_size").GetInt64(), dictionary);
	}

	public static FilePlan? PlanFile(string rel, FileChunks info, string oldPath, CancellationToken ct)
	{
		if (!File.Exists(oldPath))
		{
			return null;
		}
		Dictionary<string, (long, int)> local = new Dictionary<string, (long, int)>();
		using (FileStream s = new FileStream(oldPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536))
		{
			Cdc.Split(s, delegate(long off, ReadOnlySpan<byte> data)
			{
				local.TryAdd(Cdc.H(data), (off, data.Length));
			}, ct);
		}
		List<Chunk> list = new List<Chunk>();
		HashSet<string> hashSet = new HashSet<string>();
		Chunk[] chunks = info.Chunks;
		foreach (Chunk chunk in chunks)
		{
			if (!local.ContainsKey(chunk.H) && hashSet.Add(chunk.H))
			{
				list.Add(chunk);
			}
		}
		FilePlan filePlan = new FilePlan(rel, info, oldPath, local, list);
		if (!((double)filePlan.RemoteBytes > (double)info.Size * 0.7))
		{
			return filePlan;
		}
		return null;
	}

	public static async Task BuildAsync(IReadOnlyList<FilePlan> plans, IRangeSource src, string storePath, Func<FilePlan, string> tempOf, Action<FilePlan, bool> done, Action<long> onBytes, CancellationToken ct)
	{
		List<Chunk> sorted = (from c in plans.SelectMany((FilePlan p) => p.Remote)
			group c by c.H into g
			select g.First() into c
			orderby c.Off
			select c).ToList();
		Dictionary<string, (long Off, int Len)> store = new Dictionary<string, (long, int)>();
		Directory.CreateDirectory(Path.GetDirectoryName(storePath));
		await using (FileStream fs = new FileStream(storePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
		{
			long fetched = 0L;
			foreach (List<Chunk> group in Groups(sorted))
			{
				long start = group[0].Off;
				int count;
				byte[] data;
				int attempt;
				checked
				{
					count = (int)(group[unchecked(group.Count - 1)].Off + group[unchecked(group.Count - 1)].CLen - start);
					data = null;
					attempt = 1;
				}
				while (data == null)
				{
					try
					{
						data = await src.ReadAsync(start, count, ct);
					}
					catch (Exception ex) when (!ct.IsCancellationRequested && attempt < 3)
					{
						Log.Write($"blocks {start}+{count}: {ex.Message} — ลองใหม\u0e48 ({attempt})");
						await Task.Delay(1000 * attempt, ct);
					}
					catch (Exception ex2) when (!ct.IsCancellationRequested)
					{
						Log.Write($"blocks {start}+{count}: {ex2.Message} — ไฟล\u0e4cท\u0e35\u0e48ใช\u0e49ช\u0e34\u0e49นช\u0e48วงน\u0e35\u0e49จะด\u0e36งจาก zip แทน");
						break;
					}
					attempt++;
				}
				fetched += count;
				onBytes(fetched);
				if (data == null)
				{
					continue;
				}
				foreach (Chunk item in group)
				{
					byte[] array = Unpack(data.AsSpan((int)(item.Off - start), item.CLen), item);
					if (array == null)
					{
						Log.Write("blocks: ช\u0e34\u0e49น " + item.H + " เส\u0e35ย");
						continue;
					}
					store[item.H] = (fs.Position, array.Length);
					fs.Write(array);
				}
			}
		}
		byte[] buf = new byte[262144];
		foreach (FilePlan plan in plans)
		{
			ct.ThrowIfCancellationRequested();
			bool flag = plan.Remote.All((Chunk c) => store.ContainsKey(c.H)) && Assemble(plan, storePath, store, tempOf(plan), buf);
			if (!flag)
			{
				try
				{
					File.Delete(tempOf(plan));
				}
				catch (Exception ex3) when (((ex3 is IOException || ex3 is UnauthorizedAccessException) ? 1 : 0) != 0)
				{
				}
			}
			done(plan, flag);
		}
		try
		{
			File.Delete(storePath);
		}
		catch (Exception ex4) when (((ex4 is IOException || ex4 is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
		}
	}

	public static long DownloadBytes(IEnumerable<FilePlan> plans)
	{
		return Groups((from c in plans.SelectMany((FilePlan p) => p.Remote)
			group c by c.H into g
			select g.First() into c
			orderby c.Off
			select c).ToList()).Sum((List<Chunk> g) => g[g.Count - 1].Off + g[g.Count - 1].CLen - g[0].Off);
	}

	private static List<List<Chunk>> Groups(List<Chunk> sorted)
	{
		List<List<Chunk>> list = new List<List<Chunk>>();
		foreach (Chunk item in sorted)
		{
			object obj;
			if (list.Count <= 0)
			{
				obj = null;
			}
			else
			{
				obj = list[list.Count - 1];
			}
			List<Chunk> list2 = (List<Chunk>)obj;
			if (list2 != null)
			{
				if (item.Off - (list2[list2.Count - 1].Off + list2[list2.Count - 1].CLen) <= 262144 && item.Off + item.CLen - list2[0].Off <= 4194304)
				{
					list2.Add(item);
					continue;
				}
			}
			int num = 1;
			List<Chunk> list3 = new List<Chunk>(num);
			CollectionsMarshal.SetCount(list3, num);
			Span<Chunk> span = CollectionsMarshal.AsSpan(list3);
			int index = 0;
			span[index] = item;
			list.Add(list3);
		}
		return list;
	}

	private static byte[]? Unpack(ReadOnlySpan<byte> packed, Chunk c)
	{
		byte[] array;
		if (!c.Z)
		{
			array = packed.ToArray();
		}
		else
		{
			array = new byte[c.Len];
			using DeflateStream deflateStream = new DeflateStream(new MemoryStream(packed.ToArray()), CompressionMode.Decompress);
			try
			{
				deflateStream.ReadExactly(array);
			}
			catch (Exception ex) when (((ex is InvalidDataException || ex is EndOfStreamException) ? 1 : 0) != 0)
			{
				return null;
			}
		}
		if (array.Length != c.Len || !(Cdc.H(array) == c.H))
		{
			return null;
		}
		return array;
	}

	private static bool Assemble(FilePlan p, string storePath, Dictionary<string, (long Off, int Len)> store, string dest, byte[] buf)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(dest));
		using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		using (FileStream fileStream = new FileStream(p.OldPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536))
		{
			using FileStream fileStream2 = new FileStream(storePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
			using FileStream fileStream3 = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 1048576);
			Chunk[] chunks = p.Info.Chunks;
			foreach (Chunk chunk in chunks)
			{
				long position;
				FileStream fileStream4;
				if (!p.Local.TryGetValue(chunk.H, out (long, int) value))
				{
					long item = store[chunk.H].Off;
					position = item;
					fileStream4 = fileStream2;
				}
				else
				{
					long item = value.Item1;
					position = item;
					fileStream4 = fileStream;
				}
				fileStream4.Position = position;
				fileStream4.ReadExactly(buf, 0, chunk.Len);
				incrementalHash.AppendData(buf, 0, chunk.Len);
				fileStream3.Write(buf, 0, chunk.Len);
			}
		}
		bool num = Convert.ToHexString(incrementalHash.GetHashAndReset()).Equals(p.Info.Sha, StringComparison.OrdinalIgnoreCase);
		if (!num)
		{
			Log.Write("chunks: " + p.Rel + " ประกอบแล\u0e49ว sha256 ไม\u0e48ตรง — ด\u0e36งจาก zip แทน");
		}
		return num;
	}
}
