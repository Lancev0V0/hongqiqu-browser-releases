using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HongqiBrowser {
 public static class WindowsBrand {
  public const string AppId="Lancie.HongqiquPatrioticBrowser";
  static readonly Guid PropertyGuid=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] public static extern int SetCurrentProcessExplicitAppUserModelID(string id);
  [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr hwnd,ref Guid iid,out IPropertyStore store);
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHGetPropertyStoreFromParsingName(string path,IntPtr bind,uint flags,ref Guid iid,out IPropertyStore store);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowProc callback,IntPtr param);
  delegate bool EnumWindowProc(IntPtr hwnd,IntPtr param);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd,StringBuilder name,int count);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int command);
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
  [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
  [StructLayout(LayoutKind.Sequential,Pack=4)] struct PropertyKey { public Guid fmtid; public uint pid; public PropertyKey(uint id){fmtid=PropertyGuid;pid=id;} }
  [StructLayout(LayoutKind.Explicit,Size=24)] struct PropVariant : IDisposable {
   [FieldOffset(0)] public ushort vt;
   [FieldOffset(8)] public IntPtr value;
   public static PropVariant Text(string s){return new PropVariant{vt=31,value=Marshal.StringToCoTaskMemUni(s)};}
   public void Dispose(){if(value!=IntPtr.Zero){Marshal.FreeCoTaskMem(value);value=IntPtr.Zero;}}
  }
  [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IPropertyStore {
   uint GetCount(); void GetAt(uint index,out PropertyKey key); void GetValue(ref PropertyKey key,out PropVariant value); void SetValue(ref PropertyKey key,ref PropVariant value); void Commit();
  }
  static void Put(IPropertyStore store,uint key,string text) { var k=new PropertyKey(key);using(var value=PropVariant.Text(text)){var v=value;store.SetValue(ref k,ref v);} }
  public static void ConfigureShortcut(string shortcut) {
   Guid iid=typeof(IPropertyStore).GUID;IPropertyStore store;
   Marshal.ThrowExceptionForHR(SHGetPropertyStoreFromParsingName(shortcut,IntPtr.Zero,2,ref iid,out store));
   try{Put(store,5,AppId);store.Commit();}finally{Marshal.ReleaseComObject(store);}
  }
  public static void CreateShortcuts(string root) {
   dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
   string name="红旗渠爱国浏览器.lnk";
   foreach(string directory in new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"红旗渠爱国浏览器")}) {
    Directory.CreateDirectory(directory);string path=Path.Combine(directory,name);dynamic link=shell.CreateShortcut(path);
    link.TargetPath=Path.Combine(root,"HongqiBrowser.exe");link.WorkingDirectory=root;link.IconLocation=Path.Combine(root,"app.ico")+",0";link.Description="红旗渠爱国浏览器";link.Save();Marshal.FinalReleaseComObject(link);ConfigureShortcut(path);
   }
   Marshal.FinalReleaseComObject(shell);
  }
  public static List<IntPtr> WindowsForProcess(int pid) {
   var windows=new List<IntPtr>();EnumWindows(delegate(IntPtr h,IntPtr p){uint owner;GetWindowThreadProcessId(h,out owner);if(owner==(uint)pid && IsWindowVisible(h)){var cls=new StringBuilder(128);GetClassName(h,cls,128);if(cls.ToString()=="MozillaWindowClass")windows.Add(h);}return true;},IntPtr.Zero);return windows;
  }
  public static List<Process> BrowsersUnder(string root) {
   var result=new List<Process>();string prefix=Path.GetFullPath(Path.Combine(root,"versions"))+Path.DirectorySeparatorChar;
   foreach(var p in Process.GetProcessesByName("firefox"))try{if(p.MainModule.FileName.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&WindowsForProcess(p.Id).Count>0)result.Add(p);else p.Dispose();}catch{p.Dispose();}
   return result;
  }
  public static void Activate(Process p) {foreach(IntPtr h in WindowsForProcess(p.Id)){ShowWindow(h,9);SetForegroundWindow(h);break;}}
  public static void Apply(IntPtr hwnd,string root,Icon small,Icon big,bool properties) {
   IntPtr ignored;SendMessageTimeout(hwnd,0x80,IntPtr.Zero,small.Handle,2,500,out ignored);SendMessageTimeout(hwnd,0x80,new IntPtr(1),big.Handle,2,500,out ignored);
   if(!properties)return;Guid iid=typeof(IPropertyStore).GUID;IPropertyStore store;
   Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(hwnd,ref iid,out store));
   try{Put(store,3,Path.Combine(root,"app.ico")+",0");Put(store,2,"\""+Path.Combine(root,"HongqiBrowser.exe")+"\"");Put(store,4,"红旗渠爱国浏览器");Put(store,5,AppId);store.Commit();}finally{Marshal.ReleaseComObject(store);}
  }
 }
}
