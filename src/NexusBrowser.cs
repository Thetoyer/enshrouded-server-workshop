using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ShroudWorkshop {
class NexusBrowser:Form {
    readonly WebView2 browser; readonly Label status;readonly TextBox address;readonly ListBox downloads;readonly Action<string> install;readonly string folder;readonly List<CoreWebView2DownloadOperation> active=new List<CoreWebView2DownloadOperation>(); bool test,downloadCompleted;TcpListener fixtureServer;
    const string Home="https://www.nexusmods.com/enshrouded/mods/";
    readonly string startUrl;readonly bool serverOnly;
    static bool Domain(string host,string domain){return host.Equals(domain,StringComparison.OrdinalIgnoreCase)||host.EndsWith("."+domain,StringComparison.OrdinalIgnoreCase);}
    public static bool AllowedPage(string url){Uri u;return Uri.TryCreate(url,UriKind.Absolute,out u)&&u.Scheme=="https"&&String.IsNullOrEmpty(u.UserInfo)&&Domain(u.IdnHost,"nexusmods.com");}
    public static bool AllowedDownload(string url){Uri u;return Uri.TryCreate(url,UriKind.Absolute,out u)&&u.Scheme=="https"&&String.IsNullOrEmpty(u.UserInfo)&&(Domain(u.IdnHost,"nexusmods.com")||Domain(u.IdnHost,"nexus-cdn.com"));}
    public NexusBrowser(Action<string> installMod,bool smoke=false,string initialUrl=null,bool onlyServerMods=false){
        startUrl=initialUrl??Home;serverOnly=onlyServerMods;
        install=installMod;test=smoke;folder=test?Path.Combine(Path.GetTempPath(),"workshop-browser-test-"+Guid.NewGuid().ToString("N")):Path.Combine(Data.Home,"downloads");Text="Nexus Mods · Enshrouded Workshop";Size=new Size(1200,850);MinimumSize=new Size(960,680);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(242,245,247);
        var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=54,Padding=new Padding(10,8,10,8),WrapContents=false};Controls.Add(top);
        AddButton(top,"Back",70,()=>{if(browser.CoreWebView2!=null&&browser.CanGoBack)browser.GoBack();});AddButton(top,"Forward",85,()=>{if(browser.CoreWebView2!=null&&browser.CanGoForward)browser.GoForward();});AddButton(top,"Reload",80,()=>{if(browser.CoreWebView2!=null)browser.Reload();});AddButton(top,serverOnly?"Selected server mod":"Enshrouded mods",165,()=>Navigate(startUrl));
        address=new TextBox{ReadOnly=true,Width=490,Margin=new Padding(10,5,0,0)};top.Controls.Add(address);
        var bottom=new Panel{Dock=DockStyle.Bottom,Height=146,Padding=new Padding(12),BackColor=Color.White};Controls.Add(bottom);
        var tip=new Label{Text="Use Nexus's Manual Download, then choose the download option offered by your account. Completed ZIPs appear below for installation.",Dock=DockStyle.Top,Height=32};bottom.Controls.Add(tip);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Right,Width=235,FlowDirection=FlowDirection.TopDown,Padding=new Padding(8,0,0,0)};bottom.Controls.Add(actions);
        AddButton(actions,"Install selected ZIP",215,InstallSelected);AddButton(actions,"Open downloads folder",215,()=>{Directory.CreateDirectory(folder);System.Diagnostics.Process.Start(folder);});
        downloads=new ListBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.FixedSingle};bottom.Controls.Add(downloads);downloads.BringToFront();
        status=new Label{Dock=DockStyle.Bottom,Height=32,Padding=new Padding(12,6,0,0),Text="Starting embedded browser…"};Controls.Add(status);
        browser=new WebView2{Dock=DockStyle.Fill};Controls.Add(browser);browser.BringToFront();
        Shown+=async(s,e)=>await Initialize();
        FormClosing+=(s,e)=>{if(active.Count>0){if(MessageBox.Show(this,"Downloads are still in progress. Cancel them and close the Nexus browser?","Downloads running",MessageBoxButtons.YesNo)!=DialogResult.Yes){e.Cancel=true;return;}foreach(var op in active.ToArray())op.Cancel();}};
        FormClosed+=(s,e)=>{browser.Dispose();if(fixtureServer!=null)fixtureServer.Stop();if(test&&Directory.Exists(folder))Directory.Delete(folder,true);};RefreshDownloads();
    }
    void AddButton(Control parent,string text,int width,Action action){var b=new Button{Text=text,Width=width,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.White,Cursor=Cursors.Hand};b.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Nexus browser");}};parent.Controls.Add(b);}
    void Navigate(string url){if(browser.CoreWebView2!=null)browser.CoreWebView2.Navigate(url);}
    async Task Initialize(){try{
        var env=await CoreWebView2Environment.CreateAsync(null,test?Path.Combine(Path.GetTempPath(),"workshop-webview-test-profile"):Path.Combine(Data.Home,"browser-profile"));status.Text="Browser environment ready. Starting web view…";await browser.EnsureCoreWebView2Async(env);status.Text="Browser started. Loading page…";
        browser.CoreWebView2.Settings.IsWebMessageEnabled=false;
        browser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled=false;
        browser.CoreWebView2.ScriptDialogOpening+=(s,e)=>{status.Text="A webpage script dialog was suppressed.";};
        browser.CoreWebView2.NavigationStarting+=(s,e)=>{Uri uri;if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)){e.Cancel=true;return;}if(uri.Scheme=="nxm"){e.Cancel=true;status.Text="Choose Manual Download on Nexus. Workshop catches the downloaded ZIP directly.";}else if(!AllowedPage(e.Uri)&&!AllowedDownload(e.Uri)&&!(test&&(uri.IsLoopback||uri.Scheme=="about"))){e.Cancel=true;status.Text="Blocked navigation away from Nexus Mods: "+uri.Host;}};
        browser.CoreWebView2.NavigationStarting+=(s,e)=>{if(!serverOnly||e.Cancel)return;Uri u;if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out u))return;if(AllowedPage(e.Uri)&&(u.Host.Equals("www.nexusmods.com",StringComparison.OrdinalIgnoreCase)||u.Host.Equals("nexusmods.com",StringComparison.OrdinalIgnoreCase))){string p=u.AbsolutePath;bool download=p.IndexOf("/download",StringComparison.OrdinalIgnoreCase)>=0||p.IndexOf("/file",StringComparison.OrdinalIgnoreCase)>=0;bool auth=p.IndexOf("/login",StringComparison.OrdinalIgnoreCase)>=0||p.IndexOf("/oauth",StringComparison.OrdinalIgnoreCase)>=0;if(!ServerCatalog.IsServerModPage(e.Uri)&&!download&&!auth){e.Cancel=true;status.Text="Server-only mode: unreviewed and client-required mod pages are hidden. Return to the catalog to choose a server mod.";}}};
        browser.CoreWebView2.SourceChanged+=(s,e)=>{address.Text=browser.Source==null?"":browser.Source.ToString();status.Text="Nexus browser ready. Browse mods and use Manual Download to keep ZIPs in Workshop.";};
        browser.CoreWebView2.NavigationCompleted+=(s,e)=>status.Text=e.IsSuccess?"Browse Nexus normally. Sign in yourself if a download requires your account.":"Page could not load: "+e.WebErrorStatus+". Check your connection, then Reload.";
        browser.CoreWebView2.NewWindowRequested+=(s,e)=>{e.Handled=true;status.Text="Popup blocked. Use links in the current Nexus page.";};
        browser.CoreWebView2.PermissionRequested+=(s,e)=>e.State=CoreWebView2PermissionState.Deny;
        browser.CoreWebView2.DownloadStarting+=StartingDownload;
        if(test){byte[] payload;using(var memory=new MemoryStream()){using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true)){using(var w=new StreamWriter(archive.CreateEntry("mod.json").Open()))w.Write("{\"name\":\"Browser download fixture\"}");archive.CreateEntry("src/main.lua");}payload=memory.ToArray();}fixtureServer=new TcpListener(IPAddress.Loopback,0);fixtureServer.Start();int port=((IPEndPoint)fixtureServer.LocalEndpoint).Port;Task.Run(()=>{try{using(var client=fixtureServer.AcceptTcpClient()){using(var stream=client.GetStream()){var reader=new StreamReader(stream,Encoding.ASCII);string line;while(!String.IsNullOrEmpty(line=reader.ReadLine())){}byte[] head=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/zip\r\nContent-Disposition: attachment; filename=workshop-download-test.zip\r\nContent-Length: "+payload.Length+"\r\nConnection: close\r\n\r\n");stream.Write(head,0,head.Length);stream.Write(payload,0,payload.Length);}}}catch{}});browser.NavigateToString("<html><body><h1>Embedded browser ready</h1><a id='test-download' href='http://127.0.0.1:"+port+"/workshop-download-test.zip'>Download test fixture</a></body></html>");}else Navigate(startUrl);
    }catch(Exception ex){status.Text="Browser could not start.";MessageBox.Show(this,"The embedded browser could not start. It needs Microsoft's WebView2 Runtime (normally installed with Edge).\n\n"+ex.Message,"Nexus browser");}}
    void StartingDownload(object sender,CoreWebView2DownloadStartingEventArgs e){
        var op=e.DownloadOperation;Uri downloadUri;if(!AllowedDownload(op.Uri)&&!(test&&Uri.TryCreate(op.Uri,UriKind.Absolute,out downloadUri)&&downloadUri.IsLoopback)){e.Cancel=true;status.Text="Blocked a download from outside Nexus Mods.";return;}string name=Path.GetFileName(e.ResultFilePath);if(!name.EndsWith(".zip",StringComparison.OrdinalIgnoreCase)){e.Cancel=true;status.Text="Only EML ZIP packages can be installed automatically. This file uses another format.";return;}
        if(op.TotalBytesToReceive.HasValue&&op.TotalBytesToReceive.Value>100*1024*1024){e.Cancel=true;status.Text="Download exceeds the 100 MB mod-package limit.";return;}
        Directory.CreateDirectory(folder);string file=Path.Combine(folder,name);if(File.Exists(file))file=Path.Combine(folder,Path.GetFileNameWithoutExtension(name)+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip");
        e.ResultFilePath=file;e.Handled=true;active.Add(op);string target=file;
        op.BytesReceivedChanged+=(s,a)=>{if(op.BytesReceived>100*1024*1024){op.Cancel();status.Text="Download cancelled: package exceeds 100 MB.";return;}status.Text="Downloading "+name+"  ·  "+(op.BytesReceived/1024)+" KB";};
        op.StateChanged+=(s,a)=>{if(op.State==CoreWebView2DownloadState.InProgress)return;active.Remove(op);if(op.State==CoreWebView2DownloadState.Completed){RefreshDownloads();downloads.SelectedItem=Path.GetFileName(target);downloadCompleted=true;status.Text="Downloaded "+name+". Ready to review and install.";if(!test&&MessageBox.Show(this,"Download complete: "+name+"\n\nReview and install this mod now?","Mod downloaded",MessageBoxButtons.YesNo)==DialogResult.Yes)InstallSelected();}else{status.Text="Download interrupted: "+op.InterruptReason+". Try the download again on Nexus.";}};
    }
    void RefreshDownloads(){downloads.Items.Clear();if(Directory.Exists(folder))foreach(string file in Directory.GetFiles(folder,"*.zip").OrderByDescending(File.GetLastWriteTime))downloads.Items.Add(Path.GetFileName(file));}
    void InstallSelected(){try{if(downloads.SelectedItem==null)throw new Exception("Select a completed ZIP download first.");Data.Stopped();install(Path.Combine(folder,Convert.ToString(downloads.SelectedItem)));}catch(Exception ex){MessageBox.Show(this,ex.Message,"Install mod");}}
    public void Smoke(string log){Shown+=(s,e)=>{var t=new Timer{Interval=1000};int n=0;bool clicked=false,checking=false;t.Tick+=async(s2,e2)=>{if(checking)return;checking=true;try{n++;if(browser.CoreWebView2!=null&&!clicked){string result=await browser.ExecuteScriptAsync("(function(){var a=document.getElementById('test-download');if(!a)return false;a.click();return true;})()");clicked=result=="true";}if(downloadCompleted){try{string file=Directory.GetFiles(folder,"*.zip").Single();Data.InspectMod(file);File.WriteAllText(log,"PASS: WebView2 initialized; ZIP download intercepted, completed, and accepted by the EML package validator. No server files changed.");}catch(Exception ex){File.WriteAllText(log,"FAIL: "+ex);}t.Stop();Close();}else if(n>60){File.WriteAllText(log,"FAIL: browser/download test timed out. "+status.Text);t.Stop();Close();}}finally{checking=false;}};t.Start();};}
}
}
