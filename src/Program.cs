using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HongqiBrowser {
 static class Program {
  public const string Product="红旗渠爱国浏览器";
  public static readonly string Data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HongqiquBrowser");
  public static string Version { get { var v=Assembly.GetExecutingAssembly().GetName().Version;return v.Major+"."+v.Minor+"."+v.Build; } }
  public static void Log(string text){try{Directory.CreateDirectory(Data);File.AppendAllText(Path.Combine(Data,"launcher.log"),DateTime.UtcNow.ToString("o")+" "+text+Environment.NewLine);}catch{}}
  public static string Quote(string s){return "\""+s.Replace("\"","")+"\"";}
  [STAThread] static int Main(string[] args) {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);WindowsBrand.SetCurrentProcessExplicitAppUserModelID(WindowsBrand.AppId);
   try {
    if(args.Length==2 && args[0]=="--wait-for") {
     int previous;if(!Int32.TryParse(args[1],out previous))throw new ArgumentException("等待进程参数无效。");
     try{using(var p=Process.GetProcessById(previous)){if(!p.WaitForExit(20000))throw new IOException("旧启动器尚未退出，请重新启动。");}}catch(ArgumentException){}
    }
    string here=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    if(args.Length==1 && args[0]=="--can-install") {
     foreach(var p in Process.GetProcessesByName("HongqiBrowser"))try{if(p.Id!=Process.GetCurrentProcess().Id && p.MainModule.FileName.StartsWith(here+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return 2;}finally{p.Dispose();}
     var live=WindowsBrand.BrowsersUnder(here);foreach(var p in live)p.Dispose();return live.Count==0?0:2;
    }
    if(args.Length==1 && args[0]=="--configure-shortcuts"){WindowsBrand.CreateShortcuts(here);return 0;}
    if(File.Exists(Path.Combine(here,"current.txt"))) {
     string current=File.ReadAllText(Path.Combine(here,"current.txt")).Trim();Updates.ParseVersion(current);
     string app=Path.Combine(here,"versions",current);Updates.ValidateInstalled(app,current);
     Process.Start(new ProcessStartInfo(Path.Combine(app,"HongqiBrowser.exe")){UseShellExecute=false,WorkingDirectory=app});return 0;
    }
    if(new DirectoryInfo(here).Parent.Name!="versions" || new DirectoryInfo(here).Name!=Version)throw new InvalidDataException("安装目录无效，请重新安装。");
    string root=new DirectoryInfo(here).Parent.Parent.FullName;
    Directory.CreateDirectory(Data);bool created;string mutexKey;
    using(var sha=SHA256.Create())mutexKey=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(root.ToLowerInvariant()))).Replace("-","");
    using(var mutex=new Mutex(true,"Local\\HongqiquBrowser."+mutexKey,out created)) {
     if(!created){foreach(var browser in WindowsBrand.BrowsersUnder(root)){WindowsBrand.Activate(browser);browser.Dispose();break;}return 0;}
     using(var form=new Launcher(root,here)){Application.Run(form);}return 0;
    }
   }catch(Exception e){Log(e.ToString());MessageBox.Show(e.Message,Product,MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
  }
 }
 sealed class Launcher:Form {
  readonly string root,app;
  readonly Label detail=new Label(),versionLabel=new Label();readonly ProgressBar progress=new ProgressBar();readonly Button retry=new Button(),exit=new Button();
  readonly CancellationTokenSource cancel=new CancellationTokenSource();
  readonly System.Windows.Forms.Timer watch=new System.Windows.Forms.Timer();Icon small,big;Process browser;bool busy,observedWindow;DateTime startupDeadline;
  public Launcher(string installRoot,string appRoot) {
   root=installRoot;app=appRoot;Text=Program.Product;ClientSize=new Size(540,290);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;BackColor=Color.FromArgb(250,247,240);Font=new Font("Microsoft YaHei UI",10);
   Icon=new Icon(Path.Combine(app,"app.ico"));
   var picture=new PictureBox{Location=new Point(28,27),Size=new Size(54,54),SizeMode=PictureBoxSizeMode.Zoom,Image=Icon.ToBitmap()};Controls.Add(picture);
   Controls.Add(new Label{Text=Program.Product,Location=new Point(97,24),Size=new Size(420,36),Font=new Font("Microsoft YaHei UI",18,FontStyle.Bold),ForeColor=Color.FromArgb(165,13,23)});
   versionLabel.Text="当前版本 "+Program.Version;versionLabel.Location=new Point(99,63);versionLabel.Size=new Size(410,26);versionLabel.ForeColor=Color.FromArgb(121,92,73);Controls.Add(versionLabel);
   detail.Location=new Point(30,109);detail.Size=new Size(480,66);detail.Text="正在检查更新…";Controls.Add(detail);
   progress.Location=new Point(30,182);progress.Size=new Size(480,8);progress.Style=ProgressBarStyle.Marquee;Controls.Add(progress);
   retry.Text="重试";retry.Location=new Point(316,226);retry.Size=new Size(92,34);retry.Visible=false;retry.Click+=async delegate{await Run();};Controls.Add(retry);
   exit.Text="退出";exit.Location=new Point(420,226);exit.Size=new Size(90,34);exit.Click+=delegate{Close();};Controls.Add(exit);
   Shown+=async delegate{await Run();};FormClosing+=delegate{cancel.Cancel();watch.Stop();};watch.Interval=1000;watch.Tick+=delegate{BrandWindows();};
  }
  void Status(string text,int percent=-1){if(IsDisposed)return;detail.Text=text;progress.Style=percent<0?ProgressBarStyle.Marquee:ProgressBarStyle.Continuous;if(percent>=0)progress.Value=Math.Max(0,Math.Min(100,percent));}
  async Task Run() {
   if(busy)return;busy=true;retry.Visible=false;
   try {
    Status("正在检查 GitHub 上的正式版本…");string statePath=Path.Combine(Program.Data,"required-release.json");ReleaseInfo required=Updates.LoadRequired(statePath);
    try{var remote=await Updates.FetchLatest(cancel.Token);required=Updates.RememberNewest(statePath,remote);Program.Log("CHECK_OK version="+remote.Version);}
    catch(OperationCanceledException){if(cancel.IsCancellationRequested)return;Program.Log("CHECK_OFFLINE timeout");}
    catch(System.Net.Http.HttpRequestException e){Program.Log("CHECK_OFFLINE "+e.Message);}
    catch(System.Net.WebException e){Program.Log("CHECK_OFFLINE "+e.Status);}
    if(Updates.MustUpdate(Program.Version,required)) {
     Status("发现新版本 "+required.Version+"，必须更新成功后才能启动。",0);
     while(true) {
      var active=WindowsBrand.BrowsersUnder(root);if(active.Count==0)break;foreach(var p in active)p.Dispose();
      Status("新版已就绪。请保存工作并关闭红旗渠浏览器，关闭后自动更新。");await Task.Delay(1200,cancel.Token);
     }
     string downloads=Path.Combine(Program.Data,"updates");Directory.CreateDirectory(downloads);
     string id=Guid.NewGuid().ToString("N"),zip=Path.Combine(downloads,id+".zip"),stage=Path.Combine(root,"versions",".stage-"+id);
     await Updates.Download(required,zip,p=>{if(!IsDisposed)BeginInvoke((Action)(()=>Status("正在下载 "+required.Version+" · "+p+"%",p)));},cancel.Token);
     Status("正在校验并安装新版本…");await Task.Run(()=>{Updates.ExtractPackage(zip,stage,required.Version);Updates.CommitVersion(root,stage,required.Version);},cancel.Token);
     try{File.Copy(Path.Combine(root,"versions",required.Version,"app.ico"),Path.Combine(root,"app.ico"),true);}catch(IOException e){Program.Log("SHORTCUT_ICON "+e.Message);}
     using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\HongqiquBrowser",true)){if(key!=null)key.SetValue("DisplayVersion",required.Version);}
     Program.Log("UPDATE_COMMITTED version="+required.Version);try{File.Delete(zip);}catch(IOException e){Program.Log("CLEANUP "+e.Message);}
     Process.Start(new ProcessStartInfo(Path.Combine(root,"versions",required.Version,"HongqiBrowser.exe"),"--wait-for "+Process.GetCurrentProcess().Id){UseShellExecute=false});Close();return;
    }
    if(required==null)Program.Log("START_OFFLINE no-known-required-update");
    StartBrowser();
   }catch(OperationCanceledException){if(!cancel.IsCancellationRequested)Failure("操作超时，请重试。");}
   catch(Exception e){Program.Log(e.ToString());Failure(e.Message+"\r\n不会跳过已知的必需更新。");}
   finally{busy=false;}
  }
  void Failure(string message){Status(message,0);retry.Visible=true;}
  void StartBrowser() {
   string profile=Path.Combine(Program.Data,"profile"),theme=Path.Combine(app,"theme");Directory.CreateDirectory(profile);Directory.CreateDirectory(Path.Combine(profile,"chrome"));
   foreach(string file in new[]{"userChrome.css","userContent.css"})File.Copy(Path.Combine(theme,"chrome",file),Path.Combine(profile,"chrome",file),true);
   if(!File.Exists(Path.Combine(profile,"prefs.js")))File.Copy(Path.Combine(theme,"initial-prefs.js"),Path.Combine(profile,"prefs.js"));
   var json=new JavaScriptSerializer();string home=new Uri(Path.Combine(theme,"home.html")).AbsoluteUri;
   string prefs=File.ReadAllText(Path.Combine(theme,"user.js"),Encoding.UTF8)+"\nuser_pref(\"browser.startup.homepage\", "+json.Serialize(home)+");\n";
   Updates.AtomicWrite(Path.Combine(profile,"user.js"),prefs);
   var existing=WindowsBrand.BrowsersUnder(root);
   if(existing.Count>0){browser=existing[0];for(int i=1;i<existing.Count;i++)existing[i].Dispose();WindowsBrand.Activate(browser);}
   else {
    browser=Process.Start(new ProcessStartInfo(Path.Combine(app,"runtime","firefox.exe"),"-no-remote -profile "+Program.Quote(profile)+" "+Program.Quote(home)){UseShellExecute=false,WorkingDirectory=app});
    Program.Log("BROWSER_STARTED version="+Program.Version+" pid="+browser.Id);
   }
   using(var source=new MemoryStream(File.ReadAllBytes(Path.Combine(app,"app.ico"))))using(var original=new Icon(source)){small=new Icon(original,32,32);big=new Icon(original,256,256);}
   startupDeadline=DateTime.UtcNow.AddSeconds(60);observedWindow=false;watch.Start();BrandWindows();Hide();ShowInTaskbar=false;
  }
  void BrandWindows() {
   if(browser==null)return;
   try{
    // Firefox's signed Windows launcher can exit after spawning its real browser process.
    if(browser.HasExited){
     var actual=WindowsBrand.BrowsersUnder(root);
     if(actual.Count>0){browser.Dispose();browser=actual[0];for(int i=1;i<actual.Count;i++)actual[i].Dispose();Program.Log("BROWSER_ATTACHED pid="+browser.Id);}
     else if(observedWindow){Close();return;}
     else if(DateTime.UtcNow>startupDeadline){watch.Stop();ShowInTaskbar=true;Show();Failure("Firefox 未能完成启动，请重试。");return;}
     else return;
    }
    foreach(IntPtr hwnd in WindowsBrand.WindowsForProcess(browser.Id)){WindowsBrand.Apply(hwnd,root,small,big);observedWindow=true;}
   }
   catch(Exception e){Program.Log("ICON "+e.Message);}
  }
  protected override void Dispose(bool disposing){if(disposing){watch.Dispose();cancel.Dispose();if(small!=null)small.Dispose();if(big!=null)big.Dispose();if(browser!=null)browser.Dispose();}base.Dispose(disposing);}
 }
}
