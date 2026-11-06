using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public static class GameSync
{
	public sealed record Progress(string Phase, long Done, long Total);

	public sealed record Manifest(string Version, string LauncherVersion, Dictionary<string, (string Sha, long Size)> Files);

	public sealed record Plan(Manifest Manifest, List<string> Need, List<string> Extras, bool UpdateLauncher, bool UnsafeDir)
	{
		public bool Clean
		{
			get
			{
				if (Need.Count == 0 && !UpdateLauncher)
				{
					if (Extras.Count != 0)
					{
						return UnsafeDir;
					}
					return true;
				}
				return false;
			}
		}
	}

	public sealed record Result(int Downloaded, int Deleted, int DeleteFailed, bool LauncherUpdated);

	public const string TempDir = ".update";

	private static readonly HttpClient Http = CreateHttp();

	public const string LanguagesDir = "Languages";

	private static readonly EnumerationOptions WalkOptions = new EnumerationOptions
	{
		RecurseSubdirectories = true,
		IgnoreInaccessible = true,
		AttributesToSkip = FileAttributes.ReparsePoint
	};

	private static HttpClient CreateHttp()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromMinutes(2L);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DurangoLauncher/1.0 (+https://github.com/Safiqq/Durango-CE)");
		return httpClient;
	}

	public static bool Keep(string rel)
	{
		if (!rel.Equals("server.txt", StringComparison.OrdinalIgnoreCase) && !rel.Equals("offserver.txt", StringComparison.OrdinalIgnoreCase) && (rel.Contains('/') || !rel.StartsWith(Path.GetFileNameWithoutExtension("DurangoLauncher.exe"), StringComparison.OrdinalIgnoreCase)) && !rel.EndsWith(".log", StringComparison.OrdinalIgnoreCase) && !rel.StartsWith(".update/", StringComparison.OrdinalIgnoreCase))
		{
			return rel.StartsWith("Languages/", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static async Task<Manifest> FetchManifestAsync(string url, CancellationToken ct)
	{
		byte[] array = await Http.GetByteArrayAsync(url, ct);
		int start = ((array.Length >= 3 && array[0] == 239 && array[1] == 187 && array[2] == 191) ? 3 : 0);
		using JsonDocument jsonDocument = JsonDocument.Parse(array.AsMemory(start));
		JsonElement rootElement = jsonDocument.RootElement;
		Dictionary<string, (string, long)> dictionary = new Dictionary<string, (string, long)>(StringComparer.OrdinalIgnoreCase);
		foreach (JsonProperty item2 in rootElement.GetProperty("files").EnumerateObject())
		{
			JsonElement value;
			string text = (item2.Value.TryGetProperty("sha256", out value) ? (value.GetString() ?? "") : "");
			JsonElement value2;
			long item = ((item2.Value.TryGetProperty("size", out value2) && value2.ValueKind == JsonValueKind.Number) ? value2.GetInt64() : (-1));
			dictionary[item2.Name.Replace('\\', '/')] = (text.ToLowerInvariant(), item);
		}
		return new Manifest(Str(rootElement, "version"), Str(rootElement, "launcher_version"), dictionary);
	}

	public static Task<Plan> ScanAsync(GameInstall game, Manifest m, IProgress<Progress> progress, CancellationToken ct)
	{
		return Task.Run(delegate
		{
			string fullPath = Path.GetFullPath(game.Dir + Path.DirectorySeparatorChar);
			List<string> list = new List<string>();
			List<(string, string, string, long)> list2 = new List<(string, string, string, long)>();
			foreach (KeyValuePair<string, (string, long)> file in m.Files)
			{
				file.Deconstruct(out var key, out var value);
				(string, long) tuple = value;
				string text = key;
				var (item, num) = tuple;
				if (!IsLauncher(text))
				{
					string fullPath2 = Path.GetFullPath(Path.Combine(fullPath, text.Replace('/', Path.DirectorySeparatorChar)));
					if (!fullPath2.StartsWith(fullPath, StringComparison.OrdinalIgnoreCase))
					{
						throw new InvalidDataException("manifest: path นอกโฟลเดอร\u0e4cเกม " + text);
					}
					if (text.Equals("server.txt", StringComparison.OrdinalIgnoreCase) || text.Equals("offserver.txt", StringComparison.OrdinalIgnoreCase))
					{
						if (!File.Exists(fullPath2))
						{
							list.Add(text);
						}
					}
					else
					{
						FileInfo fileInfo = new FileInfo(fullPath2);
						if (!fileInfo.Exists || fileInfo.Length != num)
						{
							list.Add(text);
						}
						else
						{
							list2.Add((text, fullPath2, item, num));
						}
					}
				}
			}
			long num2 = list2.Sum<(string, string, string, long)>(((string Rel, string Path, string Sha, long Size) c) => c.Size);
			long num3 = 0L;
			byte[] array = new byte[1048576];
			DateTime dateTime = DateTime.MinValue;
			foreach (var (item2, path, value2, _) in list2)
			{
				ct.ThrowIfCancellationRequested();
				using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
				try
				{
					using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536);
					int num4;
					while ((num4 = fileStream.Read(array, 0, array.Length)) > 0)
					{
						ct.ThrowIfCancellationRequested();
						incrementalHash.AppendData(array, 0, num4);
						num3 += num4;
						if ((DateTime.UtcNow - dateTime).TotalMilliseconds > 150.0)
						{
							progress.Report(new Progress("verify", num3, num2));
							dateTime = DateTime.UtcNow;
						}
					}
				}
				catch (IOException)
				{
					list.Add(item2);
					goto end_IL_01b0;
				}
				catch (UnauthorizedAccessException)
				{
					list.Add(item2);
					goto end_IL_01b0;
				}
				if (!Convert.ToHexString(incrementalHash.GetHashAndReset()).Equals(value2, StringComparison.OrdinalIgnoreCase))
				{
					list.Add(item2);
				}
				end_IL_01b0:;
			}
			progress.Report(new Progress("verify", num2, num2));
			List<string> list3 = new List<string>();
			if (Directory.Exists(fullPath))
			{
				foreach (string item3 in Directory.EnumerateFiles(fullPath, "*", WalkOptions))
				{
					string text2 = Path.GetRelativePath(fullPath, item3).Replace(Path.DirectorySeparatorChar, '/');
					if (!m.Files.ContainsKey(text2) && !Keep(text2) && !string.Equals(item3, GameInstall.SelfPath, StringComparison.OrdinalIgnoreCase))
					{
						list3.Add(text2);
					}
				}
			}
			bool flag = m.LauncherVersion.Length > 0 && m.Files.ContainsKey("DurangoLauncher.exe") && GameInstall.SelfPath != null && GameInstall.CompareVersion(m.LauncherVersion, GameInstall.LauncherVersion) > 0;
			bool flag2 = IsUnsafeDir(fullPath);
			Log.Write($"scan v{m.Version}: {m.Files.Count} ไฟล\u0e4c · ต\u0e49องโหลด {list.Count} · เก\u0e34น {list3.Count}" + (flag2 ? " (โฟลเดอร\u0e4cรวม — ไม\u0e48ลบ)" : "") + (flag ? (" · launcher " + GameInstall.LauncherVersion + " → " + m.LauncherVersion) : "") + ((list.Count > 0) ? (" · " + string.Join(", ", list.Take(10))) : ""));
			return new Plan(m, list, list3, flag, flag2);
		}, ct);
	}

	public static async Task<Result> ApplyAsync(GameInstall game, Plan plan, string zipUrl, IProgress<Progress> progress, CancellationToken ct, string launcherUrl = "", string chunksUrl = "", string blocksUrl = "")
	{
		string root = Path.GetFullPath(game.Dir + Path.DirectorySeparatorChar);
		int deleted = 0;
		int failed = 0;
		if (!plan.UnsafeDir)
		{
			(deleted, failed) = DeleteExtras(root, plan.Extras);
		}
		List<string> wanted = new List<string>(plan.Need);
		if (plan.UpdateLauncher)
		{
			wanted.Add("DurangoLauncher.exe");
		}
		if (wanted.Count == 0)
		{
			return new Result(0, deleted, failed, LauncherUpdated: false);
		}
		long num = wanted.Sum((string r) => Math.Max(0L, plan.Manifest.Files[r].Size));
		Directory.CreateDirectory(root);
		long availableFreeSpace = new DriveInfo(Path.GetPathRoot(root)).AvailableFreeSpace;
		if (availableFreeSpace < num + 67108864)
		{
			throw new IOException(L.T($"พ\u0e37\u0e49นท\u0e35\u0e48ด\u0e34สก\u0e4cไม\u0e48พอ: ต\u0e49องการ {(double)num / 1048576.0:N0} MB · ว\u0e48าง {(double)availableFreeSpace / 1048576.0:N0} MB", $"Not enough disk space: need {(double)num / 1048576.0:N0} MB · free {(double)availableFreeSpace / 1048576.0:N0} MB"));
		}
		string temp = Path.Combine(root, ".update");
		try
		{
			if (Directory.Exists(temp))
			{
				Directory.Delete(temp, recursive: true);
			}
		}
		catch
		{
		}
		bool swapped = false;
		int downloaded = 0;
		string pendingLauncher = null;
		if (plan.UpdateLauncher && launcherUrl.Length > 0)
		{
			(string, long) tuple2 = plan.Manifest.Files["DurangoLauncher.exe"];
			string item = tuple2.Item1;
			long lsize = tuple2.Item2;
			string ltmp = Path.Combine(temp, "DurangoLauncher.exe");
			if (await LauncherSelfUpdate.TryDownloadAsync(Http, launcherUrl, ltmp, item, lsize, delegate(long b)
			{
				progress.Report(new Progress("download", b, lsize));
			}, ct))
			{
				pendingLauncher = ltmp;
				int num2 = downloaded;
				downloaded = num2 + 1;
				wanted.Remove("DurangoLauncher.exe");
			}
		}
		if (wanted.Count == 0)
		{
			FinishLauncher();
			try
			{
				Directory.Delete(temp, recursive: true);
			}
			catch
			{
			}
			Log.Write($"sync v{plan.Manifest.Version}: launcher จาก exe แยก (ไม\u0e48เป\u0e34ด zip) · ลบ {deleted}" + ((failed > 0) ? $" · ลบไม\u0e48ได\u0e49 {failed}" : ""));
			return new Result(downloaded, deleted, failed, swapped);
		}
		long chunkBytes = 0L;
		if (chunksUrl.Length > 0 && blocksUrl.Length > 0)
		{
			(HashSet<string>, long) tuple3 = await ApplyChunksAsync(root, temp, plan.Manifest, wanted, chunksUrl, blocksUrl, progress, ct);
			chunkBytes = tuple3.Item2;
			downloaded += tuple3.Item1.Count;
			wanted.RemoveAll(tuple3.Item1.Contains);
		}
		if (wanted.Count == 0)
		{
			FinishLauncher();
			try
			{
				Directory.Delete(temp, recursive: true);
			}
			catch
			{
			}
			Log.Write($"sync v{plan.Manifest.Version}: ช\u0e34\u0e49นอย\u0e48างเด\u0e35ยว ({chunkBytes:N0} B · ไม\u0e48เป\u0e34ด zip) · ลบ {deleted}" + ((failed > 0) ? $" · ลบไม\u0e48ได\u0e49 {failed}" : ""));
			return new Result(downloaded, deleted, failed, swapped);
		}
		progress.Report(new Progress("download", chunkBytes, -1L));
		RemoteZip zip = await RemoteZip.OpenAsync(zipUrl, ct);
		Dictionary<RemoteZip.Entry, string> byEntry = new Dictionary<RemoteZip.Entry, string>();
		foreach (string item2 in wanted)
		{
			if (!zip.Entries.TryGetValue(item2, out RemoteZip.Entry value))
			{
				if (!IsLauncher(item2))
				{
					throw new InvalidDataException(L.T("ในช\u0e38ดเต\u0e47มไม\u0e48ม\u0e35ไฟล\u0e4c ", "The full package has no file ") + item2);
				}
				Log.Write("launcher: ไม\u0e48ม\u0e35ใน zip เต\u0e47ม — ข\u0e49ามการอ\u0e31ป launcher รอบน\u0e35\u0e49");
			}
			else
			{
				byEntry[value] = item2;
			}
		}
		List<RemoteZip.Entry> last = byEntry.Keys.Where((RemoteZip.Entry e) => byEntry[e].Equals("Durango.exe", StringComparison.OrdinalIgnoreCase)).ToList();
		List<RemoteZip.Entry> entries = byEntry.Keys.Except(last).ToList();
		long firstBytes = chunkBytes + RemoteZip.DownloadBytes(entries);
		long total = firstBytes + RemoteZip.DownloadBytes(last);
		await zip.ExtractAsync(entries, TempOf, Place, delegate(long b)
		{
			progress.Report(new Progress("download", chunkBytes + b, total));
		}, ct);
		await zip.ExtractAsync(last, TempOf, Place, delegate(long b)
		{
			progress.Report(new Progress("download", firstBytes + b, total));
		}, ct);
		FinishLauncher();
		try
		{
			Directory.Delete(temp, recursive: true);
		}
		catch
		{
		}
		Log.Write($"sync v{plan.Manifest.Version}: โหลด {downloaded} ไฟล\u0e4c ({total:N0} B" + ((chunkBytes > 0) ? $" · ช\u0e34\u0e49น {chunkBytes:N0} B" : "") + $") · ลบ {deleted}" + ((failed > 0) ? $" · ลบไม\u0e48ได\u0e49 {failed}" : ""));
		return new Result(downloaded, deleted, failed, swapped);
		void FinishLauncher()
		{
			if (pendingLauncher != null)
			{
				SwapLauncher(pendingLauncher);
				swapped = true;
				pendingLauncher = null;
			}
		}
		void Place(RemoteZip.Entry e, string sha)
		{
			string text = byEntry[e];
			string text2 = TempOf(e);
			bool flag = sha.Equals(plan.Manifest.Files[text].Sha, StringComparison.OrdinalIgnoreCase);
			if (!flag && IsLauncher(text))
			{
				Log.Write("launcher: exe ใน zip เต\u0e47มไม\u0e48ตรง manifest — ข\u0e49ามการอ\u0e31ป launcher รอบน\u0e35\u0e49");
				try
				{
					File.Delete(text2);
					return;
				}
				catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
				{
					return;
				}
			}
			if (!flag)
			{
				throw new InvalidDataException(L.T("ไฟล\u0e4cท\u0e35\u0e48โหลดมาไม\u0e48ตรงรายการ: ", "Downloaded file does not match the file list: ") + text);
			}
			if (IsLauncher(text))
			{
				pendingLauncher = text2;
			}
			else
			{
				string text3 = Path.Combine(root, text.Replace('/', Path.DirectorySeparatorChar));
				Directory.CreateDirectory(Path.GetDirectoryName(text3));
				if (File.Exists(text3))
				{
					File.SetAttributes(text3, FileAttributes.Normal);
				}
				File.Move(text2, text3, overwrite: true);
			}
			downloaded++;
		}
		string TempOf(RemoteZip.Entry e)
		{
			return Path.Combine(temp, byEntry[e].Replace('/', Path.DirectorySeparatorChar));
		}
	}

	private static async Task<(HashSet<string> Placed, long Bytes)> ApplyChunksAsync(string root, string temp, Manifest m, List<string> wanted, string chunksUrl, string blocksUrl, IProgress<Progress> progress, CancellationToken ct)
	{
		HashSet<string> placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		ChunkSync.Index index;
		try
		{
			index = ChunkSync.ParseIndex(await Http.GetByteArrayAsync(chunksUrl, ct));
		}
		catch (Exception ex) when (!ct.IsCancellationRequested)
		{
			Log.Write("chunks: ไม\u0e48ม\u0e35/อ\u0e48านด\u0e31ชน\u0e35ไม\u0e48ได\u0e49 (" + ex.Message + ") — โหลดรายไฟล\u0e4cจาก zip");
			return (Placed: placed, Bytes: 0L);
		}
		progress.Report(new Progress("download", 0L, -1L));
		ChunkSync.FileChunks value;
		List<string> candidates = wanted.Where((string rel) => !IsLauncher(rel) && !rel.Equals("Durango.exe", StringComparison.OrdinalIgnoreCase) && index.Files.TryGetValue(rel, out value) && value.Sha.Equals(m.Files[rel].Sha, StringComparison.OrdinalIgnoreCase)).ToList();
		List<ChunkSync.FilePlan> plans = await Task.Run(() => candidates.Select((string rel) => ChunkSync.PlanFile(rel, index.Files[rel], Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)), ct)).OfType<ChunkSync.FilePlan>().ToList(), ct);
		if (plans.Count == 0)
		{
			return (Placed: placed, Bytes: 0L);
		}
		long total = ChunkSync.DownloadBytes(plans);
		long got = 0L;
		Log.Write($"chunks v{index.Version}: {plans.Count} ไฟล\u0e4c ({plans.Sum((ChunkSync.FilePlan p) => p.Info.Size):N0} B) → ด\u0e36งช\u0e34\u0e49น {total:N0} B");
		try
		{
			await ChunkSync.BuildAsync(plans, new ChunkSync.HttpRangeSource(Http, blocksUrl), Path.Combine(temp, ".chunks"), TempOf, delegate(ChunkSync.FilePlan p, bool ok)
			{
				if (ok)
				{
					string text = Path.Combine(root, p.Rel.Replace('/', Path.DirectorySeparatorChar));
					if (File.Exists(text))
					{
						File.SetAttributes(text, FileAttributes.Normal);
					}
					File.Move(TempOf(p), text, overwrite: true);
					placed.Add(p.Rel);
				}
			}, delegate(long b)
			{
				progress.Report(new Progress("download", got = b, total));
			}, ct);
		}
		catch (Exception ex2) when (!ct.IsCancellationRequested && (!(ex2 is IOException) || ex2.HResult != -2147024784))
		{
			Log.Write("chunks: " + ex2.Message + " — ท\u0e35\u0e48เหล\u0e37อโหลดจาก zip");
		}
		if (placed.Count < plans.Count)
		{
			Log.Write($"chunks: ประกอบไม\u0e48ผ\u0e48าน {plans.Count - placed.Count} ไฟล\u0e4c — ด\u0e36งจาก zip แทน");
		}
		return (Placed: placed, Bytes: got);
		string TempOf(ChunkSync.FilePlan p)
		{
			return Path.Combine(temp, p.Rel.Replace('/', Path.DirectorySeparatorChar));
		}
	}

	private static (int Deleted, int Failed) DeleteExtras(string root, List<string> extras)
	{
		// SANITIZED: Auto-deletion of unlisted files disabled to prevent accidental data loss.
		return (Deleted: 0, Failed: 0);
	}

	public static bool IsUnsafeDir(string dir)
	{
		string full = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar);
		if (string.Equals(full, Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		return new List<string>
		{
			folderPath,
			Path.Combine(folderPath, "Downloads"),
			Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
			Environment.GetFolderPath(Environment.SpecialFolder.Personal),
			Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
			Environment.GetFolderPath(Environment.SpecialFolder.Windows)
		}.Any((string r) => r.Length > 0 && string.Equals(full, r.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsLauncher(string rel)
	{
		return rel.Equals("DurangoLauncher.exe", StringComparison.OrdinalIgnoreCase);
	}

	private static void SwapLauncher(string tmp)
	{
		string selfPath = GameInstall.SelfPath;
		File.Move(selfPath, selfPath + ".old", overwrite: true);
		File.Move(tmp, selfPath);
	}

	private static string Str(JsonElement e, string name)
	{
		if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return "";
		}
		return value.GetString() ?? "";
	}
}
