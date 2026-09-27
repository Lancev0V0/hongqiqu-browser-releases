using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HongqiBrowser {
 public sealed class ReleaseInfo {
  public string Version { get; set; }
  public string Url { get; set; }
  public string Sha256 { get; set; }
  public long Size { get; set; }
 }
 public static class Updates {
  public const string Repository = "Lancev0V0/hongqiqu-browser-releases";
  public const long MaxPackageSize = 600L * 1024 * 1024;
  static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
  public static Version ParseVersion(string value) {
   if (value == null || !Regex.IsMatch(value, @"^\d{1,5}\.\d{1,5}\.\d{1,5}$")) throw new InvalidDataException("版本号无效。");
   return new Version(value);
  }
  public static string AssetName(string version) { ParseVersion(version); return "HongqiquBrowser-" + version + "-win-x64.zip"; }
  public static void Validate(ReleaseInfo r) {
   if (r == null) throw new InvalidDataException("更新信息为空。");
   ParseVersion(r.Version);
   var expected = "https://github.com/" + Repository + "/releases/download/v" + r.Version + "/" + AssetName(r.Version);
   if (!String.Equals(r.Url, expected, StringComparison.Ordinal)) throw new InvalidDataException("更新下载地址不属于指定发布仓库。");
   if (r.Sha256 == null || !Regex.IsMatch(r.Sha256, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("更新缺少 SHA-256 校验值。");
   if (r.Size <= 0 || r.Size > MaxPackageSize) throw new InvalidDataException("更新包大小无效。");
  }
  public static ReleaseInfo ParseRelease(string body) {
   var d = Json.Deserialize<Dictionary<string, object>>(body);
   if (d == null || Convert.ToBoolean(d["draft"]) || Convert.ToBoolean(d["prerelease"])) throw new InvalidDataException("发布信息不是正式版本。");
   var tag = Convert.ToString(d["tag_name"]);
   if (!tag.StartsWith("v", StringComparison.Ordinal)) throw new InvalidDataException("版本标签格式无效。");
   var version = tag.Substring(1); ParseVersion(version);
   var assets = (System.Collections.IEnumerable)d["assets"];
   var matching = assets.Cast<Dictionary<string, object>>().Where(x => Convert.ToString(x["name"]) == AssetName(version)).ToArray();
   if (matching.Length != 1) throw new InvalidDataException("正式发布缺少唯一的 Windows 更新包。");
   var a = matching[0]; var digest = a.ContainsKey("digest") ? Convert.ToString(a["digest"]) : "";
   if (!digest.StartsWith("sha256:", StringComparison.Ordinal)) throw new InvalidDataException("GitHub 未提供更新包 SHA-256，暂不安装。");
   var result = new ReleaseInfo { Version = version, Url = Convert.ToString(a["browser_download_url"]), Sha256 = digest.Substring(7), Size = Convert.ToInt64(a["size"]) };
   Validate(result); return result;
  }
  public static void AtomicWrite(string path, string text) {
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
   File.WriteAllText(temporary, text, new UTF8Encoding(false));
   if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
  }
  public static ReleaseInfo LoadRequired(string statePath) {
   if (!File.Exists(statePath)) return null;
   var r = Json.Deserialize<ReleaseInfo>(File.ReadAllText(statePath, Encoding.UTF8)); Validate(r); return r;
  }
  public static ReleaseInfo RememberNewest(string statePath, ReleaseInfo remote) {
   Validate(remote); var cached = LoadRequired(statePath);
   if (cached != null && ParseVersion(cached.Version) > ParseVersion(remote.Version)) return cached;
   AtomicWrite(statePath, Json.Serialize(remote)); return remote;
  }
  public static bool MustUpdate(string local, ReleaseInfo required) {
   return required != null && ParseVersion(required.Version) > ParseVersion(local);
  }
  static HttpClient MakeClient() {
   ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
   var handler = new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
   var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(15) };
   client.DefaultRequestHeaders.UserAgent.ParseAdd("HongqiquBrowser/1.0");
   client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
   client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
   return client;
  }
  public static bool AllowedTransport(Uri uri) {
   return uri.Scheme == Uri.UriSchemeHttps && (uri.Host == "api.github.com" || uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com");
  }
  static async Task<HttpResponseMessage> Send(HttpClient client, string url, CancellationToken ct) {
   var next = new Uri(url);
   for (int i=0; i<7; i++) {
    if (!AllowedTransport(next)) throw new InvalidDataException("更新地址的安全来源检查失败。");
    var response = await client.GetAsync(next, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
    int status = (int)response.StatusCode;
    if (status >= 300 && status < 400 && response.Headers.Location != null) { next = new Uri(next, response.Headers.Location); response.Dispose(); continue; }
    response.EnsureSuccessStatusCode(); return response;
   }
   throw new InvalidDataException("更新下载重定向过多。");
  }
  public static async Task<ReleaseInfo> FetchLatest(CancellationToken ct) {
   using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
    timeout.CancelAfter(TimeSpan.FromSeconds(12));
    using(var client = MakeClient()) using(var response = await Send(client, "https://api.github.com/repos/" + Repository + "/releases/latest", timeout.Token).ConfigureAwait(false)) {
     using(var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false)) using(var output = new MemoryStream()) {
      var buffer = new byte[8192]; int count;
      while ((count = await stream.ReadAsync(buffer,0,buffer.Length,timeout.Token).ConfigureAwait(false)) > 0) {
       if(output.Length + count > 4 * 1024 * 1024) throw new InvalidDataException("更新信息过大。");
       output.Write(buffer,0,count);
      }
      return ParseRelease(Encoding.UTF8.GetString(output.ToArray()));
     }
    }
   }
  }
  public static async Task Download(ReleaseInfo release, string file, Action<int> progress, CancellationToken ct) {
   Validate(release); Directory.CreateDirectory(Path.GetDirectoryName(file));
   using(var client = MakeClient()) using(var response = await Send(client,release.Url,ct).ConfigureAwait(false))
   using(var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
   using(var output = new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
    byte[] buffer = new byte[131072]; long total=0; int count;
    while((count = await input.ReadAsync(buffer,0,buffer.Length,ct).ConfigureAwait(false)) > 0) {
     total+=count; if(total>release.Size) throw new InvalidDataException("更新包长度超出发布信息。");
     await output.WriteAsync(buffer,0,count,ct).ConfigureAwait(false); if(progress != null) progress((int)(total*100/release.Size));
    }
    if(total!=release.Size) throw new InvalidDataException("更新包下载不完整。");
   }
   VerifyHash(file,release.Sha256);
  }
  public static void VerifyHash(string file, string expected) {
   using(var input=File.OpenRead(file)) using(var sha=SHA256.Create()) {
    string actual=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");
    if(!String.Equals(actual,expected,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新包 SHA-256 不匹配，已停止安装。");
   }
  }
  public static void ExtractPackage(string zipPath,string target,string version) {
   ParseVersion(version); string root=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if(Directory.Exists(target)) throw new IOException("更新暂存目录已存在。");
   Directory.CreateDirectory(target); long expanded=0; int count=0; var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var archive=ZipFile.OpenRead(zipPath)) foreach(var entry in archive.Entries) {
    if(++count>20000) throw new InvalidDataException("更新包文件数量超限。");
    string name=entry.FullName.Replace('/',Path.DirectorySeparatorChar);
    if(name.IndexOf(':')>=0 || name.IndexOf('\0')>=0 || Path.IsPathRooted(name) || name.Split(Path.DirectorySeparatorChar).Any(x=>x=="..")) throw new InvalidDataException("更新包包含非法路径。");
    if(((entry.ExternalAttributes>>16)&0xF000)==0xA000) throw new InvalidDataException("更新包不允许符号链接。");
    string destination=Path.GetFullPath(Path.Combine(target,name));
    if(!destination.StartsWith(root,StringComparison.OrdinalIgnoreCase) || !names.Add(destination)) throw new InvalidDataException("更新包路径越界或重复。");
    expanded+=entry.Length; if(expanded>2L*1024*1024*1024) throw new InvalidDataException("更新包解压大小超限。");
    if(name.EndsWith(Path.DirectorySeparatorChar.ToString())) { Directory.CreateDirectory(destination); continue; }
    Directory.CreateDirectory(Path.GetDirectoryName(destination)); entry.ExtractToFile(destination,false);
   }
   ValidateInstalled(target,version);
  }
  public static void ValidateInstalled(string folder,string version) {
   if(File.ReadAllText(Path.Combine(folder,"version.txt")).Trim()!=version) throw new InvalidDataException("更新包内部版本不一致。");
   foreach(string file in new[]{"HongqiBrowser.exe","app.ico","runtime/firefox.exe","theme/home.html","theme/chrome/userChrome.css","theme/chrome/userContent.css"})
    if(!File.Exists(Path.Combine(folder,file))) throw new InvalidDataException("更新包缺少必要文件："+file);
  }
  public static void CommitVersion(string installRoot,string staged,string version) {
   ParseVersion(version); ValidateInstalled(staged,version);
   string versions=Path.Combine(installRoot,"versions"); Directory.CreateDirectory(versions);
   string final=Path.Combine(versions,version);
   if(Directory.Exists(final)) {
    // Recover a crash between the completed directory move and pointer write.
    ValidateInstalled(final,version);
    var sourceFiles=Directory.GetFiles(staged,"*",SearchOption.AllDirectories);
    if(sourceFiles.Length!=Directory.GetFiles(final,"*",SearchOption.AllDirectories).Length)throw new IOException("已有版本目录不完整，请修复安装。");
    foreach(var file in sourceFiles) {
     string other=Path.Combine(final,file.Substring(staged.TrimEnd(Path.DirectorySeparatorChar).Length+1));
     if(!File.Exists(other))throw new IOException("已有版本目录不完整，请修复安装。");
     using(var sha=SHA256.Create())using(var input=File.OpenRead(file))VerifyHash(other,BitConverter.ToString(sha.ComputeHash(input)).Replace("-",""));
    }
   } else Directory.Move(staged,final);
   AtomicWrite(Path.Combine(installRoot,"current.txt"),version);
  }
 }
}
