using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using HongqiBrowser;

static class UpdateTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);checks++;Console.WriteLine("PASS "+name);}
 static void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,name);}
 static ReleaseInfo Release(string v){return new ReleaseInfo{Version=v,Url="https://github.com/"+Updates.Repository+"/releases/download/v"+v+"/"+Updates.AssetName(v),Sha256=new string('a',64),Size=256};}
 static void Entry(ZipArchive zip,string name,string data){using(var writer=new StreamWriter(zip.CreateEntry(name).Open()))writer.Write(data);}
 static string Package(string root,string name,string version,string extra=null,bool duplicate=false){string path=Path.Combine(root,name+".zip");using(var zip=ZipFile.Open(path,ZipArchiveMode.Create)){
  Entry(zip,"version.txt",version);
  foreach(string f in new[]{"HongqiBrowser.exe","app.ico","runtime/firefox.exe","theme/home.html","theme/chrome/userChrome.css","theme/chrome/userContent.css"})Entry(zip,f,"test fixture "+f);
  if(extra!=null)Entry(zip,extra,"bad");if(duplicate)Entry(zip,"APP.ICO","duplicate");
 }return path;}
 static int Main(string[] args){try{
  if(args.Length==2 && args[0]=="--live"){
   var liveRelease=Updates.FetchLatest(CancellationToken.None).GetAwaiter().GetResult();Console.WriteLine("LIVE_VERSION "+liveRelease.Version);
   string file=Path.Combine(args[1],Guid.NewGuid().ToString("N")+".zip");Updates.Download(liveRelease,file,null,CancellationToken.None).GetAwaiter().GetResult();
   Updates.ExtractPackage(file,Path.Combine(args[1],"verified-"+Guid.NewGuid().ToString("N")),liveRelease.Version);Console.WriteLine("LIVE_DOWNLOAD_HASH_EXTRACT_PASS");return 0;
  }
  string root=Path.Combine(Path.GetTempPath(),"HongqiUpdateTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Console.WriteLine("FIXTURES "+root);
  string cache=Path.Combine(root,"required.json");
  Check(!Updates.MustUpdate("1.0.0",Updates.LoadRequired(cache)),"offline start with no known update");
  Updates.RememberNewest(cache,Release("1.1.0"));Check(Updates.MustUpdate("1.0.0",Updates.LoadRequired(cache)),"known update blocks offline old version");
  Updates.RememberNewest(cache,Release("1.0.1"));Check(Updates.LoadRequired(cache).Version=="1.1.0","server rollback cannot clear requirement");
  Check(!Updates.MustUpdate("1.1.0",Updates.LoadRequired(cache)),"updated version may start");
  Check(!Updates.MustUpdate("2.0.0",Updates.LoadRequired(cache)),"newer installed version is not downgraded");
  Reject(()=>Updates.ParseVersion("../1.0.0"),"reject path-like version");
  Reject(()=>Updates.ParseVersion("1.0.0-beta"),"reject unstable version");
  var bad=Release("1.0.0");bad.Url=bad.Url.Replace(Updates.Repository,"attacker/repo");Reject(()=>Updates.Validate(bad),"reject other repository");
  var r=Release("1.0.0");var json=new JavaScriptSerializer();
  string metadata=json.Serialize(new {draft=false,prerelease=false,tag_name="v1.0.0",assets=new[]{new{name=Updates.AssetName(r.Version),browser_download_url=r.Url,digest="sha256:"+r.Sha256,size=r.Size}}});
  Check(Updates.ParseRelease(metadata).Version=="1.0.0","parse GitHub release and digest");
  Reject(()=>Updates.ParseRelease(metadata.Replace("sha256:","md5:")),"reject missing SHA256");
  Reject(()=>Updates.ParseRelease(metadata.Replace("\"prerelease\":false","\"prerelease\":true")),"reject prerelease");
  Check(!Updates.AllowedTransport(new Uri("http://github.com/a")) && !Updates.AllowedTransport(new Uri("https://github.com.attacker.test/a")),"reject HTTP and deceptive redirect host");
  string zip=Package(root,"good","1.1.0"),stage=Path.Combine(root,"stage"),install=Path.Combine(root,"install");
  using(var sha=SHA256.Create())using(var file=File.OpenRead(zip))Updates.VerifyHash(zip,BitConverter.ToString(sha.ComputeHash(file)).Replace("-",""));checks++;Console.WriteLine("PASS valid SHA256");
  Reject(()=>Updates.VerifyHash(zip,new string('0',64)),"reject corrupt hash before install");
  string profile=Path.Combine(root,"profile");Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"bookmarks-sentinel"),"keep user data");
  Updates.ExtractPackage(zip,stage,"1.1.0");Updates.CommitVersion(install,stage,"1.1.0");
  Check(File.ReadAllText(Path.Combine(install,"current.txt"))=="1.1.0","complete package atomically selected");
  Check(File.ReadAllText(Path.Combine(profile,"bookmarks-sentinel"))=="keep user data","profile remains outside version replacement");
  string recovery=Path.Combine(root,"recovery");Updates.ExtractPackage(zip,recovery,"1.1.0");Updates.AtomicWrite(Path.Combine(install,"current.txt"),"1.0.0");Updates.CommitVersion(install,recovery,"1.1.0");Check(File.ReadAllText(Path.Combine(install,"current.txt"))=="1.1.0","recover completed directory before pointer commit");
  string mismatch=Path.Combine(root,"mismatch");Updates.ExtractPackage(zip,mismatch,"1.1.0");File.AppendAllText(Path.Combine(mismatch,"app.ico"),"corrupt");Reject(()=>Updates.CommitVersion(install,mismatch,"1.1.0"),"reject existing version content mismatch");
  Reject(()=>Updates.ExtractPackage(Package(root,"traversal","1.2.0","../escaped.txt"),Path.Combine(root,"traversal"),"1.2.0"),"reject ZIP traversal");Check(!File.Exists(Path.Combine(root,"escaped.txt")),"traversal did not write outside stage");
  Reject(()=>Updates.ExtractPackage(Package(root,"duplicate","1.2.0",null,true),Path.Combine(root,"duplicate"),"1.2.0"),"reject case-insensitive duplicate paths");
  Reject(()=>Updates.ExtractPackage(Package(root,"absolute","1.2.0","C:/escaped.txt"),Path.Combine(root,"absolute"),"1.2.0"),"reject absolute ZIP paths");
  string link=Package(root,"symlink","1.2.0");using(var archive=ZipFile.Open(link,ZipArchiveMode.Update)){var e=archive.CreateEntry("evil-link");e.ExternalAttributes=unchecked((int)0xA1FF0000);}
  Reject(()=>Updates.ExtractPackage(link,Path.Combine(root,"symlink"),"1.2.0"),"reject ZIP symlinks");
  Reject(()=>Updates.ExtractPackage(zip,Path.Combine(root,"wrong-version"),"1.2.0"),"reject internal version mismatch");
  string incomplete=Path.Combine(root,"incomplete.zip");using(var archive=ZipFile.Open(incomplete,ZipArchiveMode.Create))Entry(archive,"version.txt","1.2.0");
  Reject(()=>Updates.ExtractPackage(incomplete,Path.Combine(root,"incomplete"),"1.2.0"),"reject incomplete package");
  string truncated=Path.Combine(root,"truncated.zip");File.WriteAllBytes(truncated,new byte[]{80,75,3,4});Reject(()=>Updates.ExtractPackage(truncated,Path.Combine(root,"truncated"),"1.2.0"),"reject truncated ZIP");
  Check(File.ReadAllText(Path.Combine(install,"current.txt"))=="1.1.0","all rejected updates retain working pointer");
  File.WriteAllText(cache,"corrupted");Reject(()=>Updates.LoadRequired(cache),"corrupt mandatory state fails closed");
  Console.WriteLine("ALL_PASS "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
