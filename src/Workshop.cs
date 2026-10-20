using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShroudWorkshop {
static class Data {
    public static readonly string ExecutableHome = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string Home = PrepareHome();
    static string PrepareHome(){string root=Environment.GetEnvironmentVariable("ENSHROUDED_WORKSHOP_DATA_DIR");if(String.IsNullOrEmpty(root))root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EnshroudedServerWorkshop");Directory.CreateDirectory(root);return root;}
    public static string FindServer(){
        var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
        try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){if(key!=null){var path=Convert.ToString(key.GetValue("SteamPath"));if(!String.IsNullOrEmpty(path))roots.Add(path);}}}catch{}
        foreach(string root in roots.ToArray()){string libraries=Path.Combine(root,"steamapps","libraryfolders.vdf");if(File.Exists(libraries))foreach(Match m in Regex.Matches(File.ReadAllText(libraries),"\"path\"\\s+\"([^\"]+)\""))roots.Add(m.Groups[1].Value.Replace(@"\\",@"\"));}
        foreach(string root in roots){string apps=Path.Combine(root,"steamapps"),manifest=Path.Combine(apps,"appmanifest_2278520.acf"),folder="EnshroudedServer";if(File.Exists(manifest)){var m=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s+\"([^\"]+)\"");if(m.Success)folder=m.Groups[1].Value;}string candidate=Path.Combine(apps,"common",folder);if(File.Exists(Path.Combine(candidate,"enshrouded_server.exe")))return candidate;}
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam","steamapps","common","EnshroudedServer");
    }
    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength=16000000, RecursionLimit=100 };
    public static Dictionary<string,object> Parse(string s) { var d=Json.DeserializeObject(s) as Dictionary<string,object>; if(d==null) throw new Exception("Settings must be a JSON object."); return d; }
    public static object[] Array(object v) { var a=v as object[]; if(a!=null)return a; var b=v as ArrayList; return b==null?new object[0]:b.ToArray(); }
    public static string Pretty(object o) { return Format(Json.Serialize(o)); }
    static string Format(string s) {
        var b=new StringBuilder(); int depth=0; bool quoted=false,escape=false;
        foreach(char c in s) {
            if(quoted){b.Append(c);if(escape)escape=false;else if(c=='\\')escape=true;else if(c=='"')quoted=false;continue;}
            if(c=='"'){quoted=true;b.Append(c);} else if(c=='{'||c=='['){b.Append(c);depth++;b.AppendLine();b.Append(' ',depth*2);} else if(c=='}'||c==']'){depth--;b.AppendLine();b.Append(' ',depth*2);b.Append(c);} else if(c==','){b.Append(c);b.AppendLine();b.Append(' ',depth*2);} else if(c==':')b.Append(": ");else b.Append(c);
        } return b.ToString();
    }
    public static void Atomic(string path,string text) {
        string tmp=path+".workshop-"+Guid.NewGuid().ToString("N");
        File.WriteAllText(tmp,text,new UTF8Encoding(false));
        try { if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path); } finally { if(File.Exists(tmp))File.Delete(tmp); }
    }
    public static string Backup(string path) {
        if(!File.Exists(path))return null;
        string dir=Path.Combine(Home,"backups",DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff"));Directory.CreateDirectory(dir);
        string dest=Path.Combine(dir,Path.GetFileName(path)); File.Copy(path,dest); return dest;
    }
    public static bool Running { get { return Process.GetProcessesByName("enshrouded_server").Length>0; } }
    public static void Stopped() { if(Running)throw new Exception("Your server is running. In its console, press Ctrl+C and wait for it to finish saving. Then try again."); }
    public static async Task<string> Get(string url) {
        using(var c=new WebClient()){c.Headers["User-Agent"]="EnshroudedWorkshop/2.0";c.Encoding=Encoding.UTF8;return await c.DownloadStringTaskAsync(new Uri(url));}
    }
    public static async Task Download(string url,string path) {
        var uri=new Uri(url);if(uri.Scheme!="https")throw new Exception("Use an HTTPS download link.");
        using(var c=new WebClient()) {
            c.Headers["User-Agent"]="EnshroudedWorkshop/2.0";
            bool tooLarge=false;
            c.DownloadProgressChanged+=(s,e)=>{if(e.BytesReceived>100*1024*1024 || e.TotalBytesToReceive>100*1024*1024){tooLarge=true;c.CancelAsync();}};
            try { await c.DownloadFileTaskAsync(uri,path); } catch {if(tooLarge)throw new Exception("The package exceeds the 100 MB limit.");throw;}
        }
    }
    public static string InspectMod(string zip) {
        using(var a=ZipFile.OpenRead(zip)) {
            long total=0;var manifests=new List<string>();
            if(a.Entries.Count>10000)throw new Exception("This ZIP contains too many files.");
            foreach(var e in a.Entries) {
                string n=e.FullName.Replace('\\','/'); total+=e.Length;
                if(n.StartsWith("/")||n.Contains(":")||n.Split('/').Any(p=>p=="..")||((e.ExternalAttributes>>16)&0xF000)==0xA000)throw new Exception("This ZIP contains an unsafe path or symbolic link.");
                if(Regex.IsMatch(n,@"\.(exe|bat|cmd|ps1|vbs|js|lnk|msi)$",RegexOptions.IgnoreCase))throw new Exception("This package contains an installer or executable script. Follow its author's manual installation instructions.");
                if(n.Equals("mod.json",StringComparison.OrdinalIgnoreCase)||n.EndsWith("/mod.json",StringComparison.OrdinalIgnoreCase))manifests.Add(n);
            }
            if(total>250*1024*1024)throw new Exception("The expanded ZIP exceeds the 250 MB limit.");
            if(manifests.Count!=1)throw new Exception("This is not a single EML mod package. Choose a ZIP with one mod.json file. Other formats need the author's installation instructions.");
            return manifests[0];
        }
    }
    public static void Extract(string zip,string dest) {
        string root=Path.GetFullPath(dest).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        using(var a=ZipFile.OpenRead(zip))foreach(var e in a.Entries) {
            string target=Path.GetFullPath(Path.Combine(root,e.FullName.Replace('/',Path.DirectorySeparatorChar)));
            if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe archive path.");
            if(e.Name.Length==0){Directory.CreateDirectory(target);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(target));e.ExtractToFile(target,false);
        }
    }
    public static void CopyTree(string source,string dest) {
        Directory.CreateDirectory(dest);foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(dest,Path.GetFileName(file)));
        foreach(string dir in Directory.GetDirectories(source))CopyTree(dir,Path.Combine(dest,Path.GetFileName(dir)));
    }
}

class Field {
    public string Label,Hint,Group; public string[] Choices; public decimal Min=0,Max=100; public bool Minutes;
    public Field(string label,string hint,string group="") {Label=label;Hint=hint;Group=group;}
}
static class Schema {
    public static Dictionary<string,Field> Fields=new Dictionary<string,Field>();
    static void Add(string key,string name,string hint,string group="",decimal min=0,decimal max=100,string choices=null,bool minutes=false) {
        Fields[key]=new Field(name,hint,group){Min=min,Max=max,Choices=choices==null?null:choices.Split('|'),Minutes=minutes};
    }
    static Schema() {
        Add("name","Server name","The name players will see in the server browser.");
        Add("slotCount","Player limit","Maximum players who can join at the same time.","",1,16);
        Add("queryPort","Server port","UDP port used to find your server. Default: 15637.","",1,65535);
        Add("ip","Listen address","0.0.0.0 accepts connections on all network adapters.");
        Add("tags","Server tags","Separate tags with commas. Leave empty for no tags.");
        Add("saveDirectory","World save folder","Relative folders are inside your server installation.");
        Add("logDirectory","Server log folder","Where the server writes its log files.");
        Add("enableVoiceChat","Voice chat","Allow players to talk using in-game voice.");
        Add("enableTextChat","Text chat","Allow players to send in-game text messages.");
        Add("voiceChatMode","Voice range","Proximity is local; Global reaches the whole server.","",0,0,"Proximity|Global");
        Add("gameSettingsPreset","Difficulty preset","Use Custom to adjust individual gameplay settings.","",0,0,"Default|Relaxed|Hard|Survival|Custom");
        Add("playerHealthFactor","Player health","1x is normal. Higher values give players more health.","Player & survival",.25m,4);
        Add("playerManaFactor","Player mana","Maximum mana, relative to the normal amount.","Player & survival",.25m,4);
        Add("playerStaminaFactor","Player stamina","Maximum stamina, relative to the normal amount.","Player & survival",.25m,4);
        Add("playerBodyHeatFactor","Cold resistance","Higher values let players withstand cold for longer.","Player & survival",.5m,2);
        Add("playerDivingTimeFactor","Underwater oxygen","Higher values allow longer dives.","Player & survival",.5m,2);
        Add("enableDurability","Weapon durability","When off, weapons no longer wear out.","Player & survival");
        Add("enableStarvingDebuff","Hunger & starvation","When on, starvation can damage players.","Player & survival");
        Add("foodBuffDurationFactor","Food effect duration","How long food bonuses last. 1x is normal.","Player & survival",.5m,2);
        Add("fromHungerToStarving","Time before starvation","Minutes spent hungry before starvation begins.","Player & survival",5,20,null,true);
        Add("shroudTimeFactor","Time in the Shroud","How long players can stay in the Shroud.","Player & survival",.5m,2);
        Add("tombstoneMode","Items lost on death","Materials only, everything, or keep your backpack.","Player & survival",0,0,"AddBackpackMaterials|Everything|NoTombstone");
        Add("enableGliderTurbulences","Glider turbulence","Allow weather turbulence to affect gliding.","World & activities");
        Add("weatherFrequency","Weather frequency","How often weather changes occur.","World & activities",0,0,"Disabled|Rare|Normal|Often");
        Add("fishingDifficulty","Fishing difficulty","How challenging it is to reel in a fish.","World & activities",0,0,"VeryEasy|Easy|Normal|Hard|VeryHard");
        Add("miningDamageFactor","Mining effectiveness","Higher values mine more material per hit.","Resources & progression",.5m,2);
        Add("plantGrowthSpeedFactor","Plant growth","Growth multiplier. 1x is normal.","Resources & progression",.25m,2);
        Add("resourceDropStackAmountFactor","Resource yield","Amount of materials in resource drops.","Resources & progression",.25m,2);
        Add("factoryProductionSpeedFactor","Workstation production","Production modifier. 1x is normal.","Resources & progression",.25m,2);
        Add("perkUpgradeRecyclingFactor","Weapon salvage refund","Fraction of upgrade runes returned when salvaging.","Resources & progression",0,1);
        Add("perkCostFactor","Weapon upgrade cost","Rune cost multiplier. Lower values cost less.","Resources & progression",.25m,2);
        Add("experienceCombatFactor","Combat experience","Experience gained from fighting.","Resources & progression",.25m,2);
        Add("experienceMiningFactor","Mining experience","Experience gained from mining.","Resources & progression",0,2);
        Add("experienceExplorationQuestsFactor","Exploration & quest experience","Experience from exploration and quests.","Resources & progression",.25m,2);
        Add("randomSpawnerAmount","Enemy population","How many enemies appear in the world.","Enemies & combat",0,0,"Automatic|Few|Normal|Many|Extreme");
        Add("aggroPoolAmount","Simultaneous attackers","How many enemies may attack at once.","Enemies & combat",0,0,"Few|Normal|Many|Extreme");
        Add("enemyDamageFactor","Enemy damage","Damage from ordinary enemies. Excludes bosses.","Enemies & combat",.25m,5);
        Add("enemyHealthFactor","Enemy health","Health of ordinary enemies. Excludes bosses.","Enemies & combat",.25m,4);
        Add("enemyStaminaFactor","Enemy stun resistance","Higher values make enemies harder to stun.","Enemies & combat",.5m,2);
        Add("enemyPerceptionRangeFactor","Enemy awareness","Distance at which enemies notice players.","Enemies & combat",.5m,2);
        Add("bossDamageFactor","Boss damage","Damage dealt by bosses.","Enemies & combat",.2m,5);
        Add("bossHealthFactor","Boss health","Health of boss enemies.","Enemies & combat",.2m,5);
        Add("threatBonus","Enemy attack frequency","How often ordinary enemies attack.","Enemies & combat",.25m,4);
        Add("pacifyAllEnemies","Peaceful ordinary enemies","Enemies retaliate only when attacked. Excludes bosses.","Enemies & combat");
        Add("tamingStartleRepercussion","Taming setback","What happens when an animal is startled.","World & activities",0,0,"KeepProgress|LoseSomeProgress|LoseAllProgress");
        Add("dayTimeDuration","Day length","Real-world minutes of daylight per cycle.","World & activities",2,60,null,true);
        Add("nightTimeDuration","Night length","Real-world minutes of night per cycle.","World & activities",2,60,null,true);
        Add("curseModifier","Shroud curse","Easy disables the curse; Hard increases its chance.","World & activities",0,0,"Easy|Normal|Hard");
        Add("flameLevelFogRemoval","Flame fog removal level","Server-version-specific setting; the current value is preserved.","World & activities",0,100);
        Add("password","Group password","Players use this password to join with this group's permissions.");
        Add("canKickBan","Kick & ban players","Allow this group to remove players.");
        Add("canAccessInventories","Access inventories","Allow access to inventories under this role.");
        Add("canEditWorld","Change the world","Allow building and terrain changes outside bases.");
        Add("canEditBase","Edit bases","Allow changes to existing bases.");
        Add("canExtendBase","Expand bases","Allow extending the base area.");
        Add("reservedSlots","Reserved player slots","Keep these slots available for this group.","",0,16);
    }
    public static string Human(string s) {return Regex.Replace(Regex.Replace(s,"([a-z])([A-Z])","$1 $2"),"^.",m=>m.Value.ToUpper());}
    public static Field Get(string key) {Field f;return Fields.TryGetValue(key,out f)?f:new Field(Human(key),"Additional setting from your installed server version.","Other settings");}
}

class Workshop : Form {
    static readonly Color Ink=Color.FromArgb(27,40,53), Muted=Color.FromArgb(99,114,128), Accent=Color.FromArgb(15,118,110), Canvas=Color.FromArgb(242,245,247), Border=Color.FromArgb(220,228,233);
    Panel sidebar,body,footer,header; FlowLayoutPanel page; Label pageTitle,subtitle,toast,serverStatus,dirtyLabel; Button save; Dictionary<string,Button> navs=new Dictionary<string,Button>();
    Dictionary<string,object> doc; string original,server,view="Overview",gameCategory="Player & survival"; bool dirty,building,busy,rawPending; TextBox raw; ErrorProvider errors=new ErrorProvider(); readonly HashSet<Control> invalid=new HashSet<Control>();
    static readonly string DefaultServer=Data.FindServer();
    public Workshop() {
        Text="Enshrouded Server Workshop";Font=new Font("Segoe UI",10);Size=new Size(1240,860);MinimumSize=new Size(1060,720);StartPosition=FormStartPosition.CenterScreen;BackColor=Canvas;AutoScaleMode=AutoScaleMode.Dpi;
        string icon=Path.Combine(Data.ExecutableHome,"Workshop.ico");if(File.Exists(icon))Icon=new Icon(icon);
        sidebar=new Panel{Dock=DockStyle.Left,Width=224,BackColor=Ink,Padding=new Padding(18,26,18,18)};Controls.Add(sidebar);
        var brand=LabelAt("ENSHROUDED",new Font("Segoe UI",16,FontStyle.Bold),Color.White);brand.SetBounds(20,28,200,32);sidebar.Controls.Add(brand);
        var word=LabelAt("SERVER WORKSHOP",new Font("Segoe UI",9,FontStyle.Bold),Color.FromArgb(108,198,185));word.SetBounds(22,63,190,25);sidebar.Controls.Add(word);
        string[] views={"Overview","Server settings","Gameplay","Players & access","Mods","World transfers","Backups","Advanced"};
        for(int i=0;i<views.Length;i++){string name=views[i];var n=MakeButton(name,false);n.SetBounds(12,121+i*48,200,42);n.TextAlign=ContentAlignment.MiddleLeft;n.Padding=new Padding(14,0,0,0);n.BackColor=Ink;n.ForeColor=Color.FromArgb(201,213,222);n.FlatAppearance.BorderSize=0;n.Click+=(s,e)=>Navigate(name);sidebar.Controls.Add(n);navs[name]=n;}
        var version=LabelAt("2.5 BETA  /  LOCAL APP",new Font("Segoe UI",8),Color.FromArgb(151,170,185));version.Dock=DockStyle.Bottom;version.Height=28;sidebar.Controls.Add(version);
        body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(30,24,30,12)};Controls.Add(body);body.BringToFront();
        footer=new Panel{Dock=DockStyle.Bottom,Height=64,BackColor=Color.White,Padding=new Padding(18,12,18,12)};body.Controls.Add(footer);
        save=MakeButton("Save changes",true);save.Dock=DockStyle.Right;save.Width=160;save.Click+=(s,e)=>Safe(Save);footer.Controls.Add(save);
        dirtyLabel=LabelAt("All changes saved",new Font("Segoe UI",10,FontStyle.Bold),Muted);dirtyLabel.Dock=DockStyle.Fill;dirtyLabel.TextAlign=ContentAlignment.MiddleLeft;footer.Controls.Add(dirtyLabel);dirtyLabel.BringToFront();
        toast=LabelAt("",new Font("Segoe UI",9),Muted);toast.Dock=DockStyle.Bottom;toast.Height=43;toast.Padding=new Padding(0,10,0,0);body.Controls.Add(toast);
        header=new Panel{Dock=DockStyle.Top,Height=100};body.Controls.Add(header);
        pageTitle=LabelAt("Overview",new Font("Segoe UI",25,FontStyle.Bold),Ink);pageTitle.SetBounds(0,0,660,48);header.Controls.Add(pageTitle);
        subtitle=LabelAt("Your world, your rules.",new Font("Segoe UI",10),Muted);subtitle.SetBounds(2,55,850,30);subtitle.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;header.Controls.Add(subtitle);
        page=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,0,12,16)};body.Controls.Add(page);page.BringToFront();
        page.Layout+=(s,e)=>{foreach(Control c in page.Controls){int width=Math.Max(400,page.ClientSize.Width-26);if(c.Width!=width)c.Width=width;}};
        errors.ContainerControl=this;errors.BlinkStyle=ErrorBlinkStyle.NeverBlink;
        server=DefaultServer;try{string p=Path.Combine(Data.Home,"preferences.json");if(File.Exists(p))server=Convert.ToString(Data.Parse(File.ReadAllText(p))["server"]);}catch{}
        try{LoadSettings();}catch(Exception ex){toast.Text=ex.Message;}
        Navigate("Overview");
        var timer=new Timer{Interval=2000};timer.Tick+=(s,e)=>{if(serverStatus!=null&&!serverStatus.IsDisposed)serverStatus.Text=Data.Running?"RUNNING":"STOPPED";};timer.Start();FormClosed+=(s,e)=>timer.Dispose();
        FormClosing+=(s,e)=>{if(busy){MessageBox.Show(this,"Wait for the current download or installation to finish.","Working");e.Cancel=true;}else if((dirty||rawPending)&&MessageBox.Show(this,"You have unsaved changes. Close without saving?","Unsaved changes",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
    }
    Label LabelAt(string text,Font font,Color color){return new Label{Text=text,Font=font,ForeColor=color,AutoSize=false,UseMnemonic=false};}
    Button MakeButton(string text,bool primary) {var b=new Button{Text=text,Height=38,Width=170,FlatStyle=FlatStyle.Flat,BackColor=primary?Accent:Color.White,ForeColor=primary?Color.White:Ink,Cursor=Cursors.Hand,Font=new Font("Segoe UI",10,FontStyle.Bold)};b.FlatAppearance.BorderColor=primary?Accent:Border;b.FlatAppearance.BorderSize=1;return b;}
    void Safe(Action action){try{action();}catch(UnauthorizedAccessException){MessageBox.Show(this,"Windows denied access to that folder. Close Workshop, right-click its desktop shortcut, and choose Run as administrator.","Folder access",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Unable to complete action",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
    async void Async(Func<Task> action){if(busy)return;busy=true;UseWaitCursor=true;foreach(var b in navs.Values)b.Enabled=false;page.Enabled=false;save.Enabled=false;try{await action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Download or installation failed",MessageBoxButtons.OK,MessageBoxIcon.Information);}finally{busy=false;UseWaitCursor=false;page.Enabled=true;foreach(var b in navs.Values)b.Enabled=true;UpdateDirty();}}
    void Open(string path){Safe(()=>Process.Start(new ProcessStartInfo(path){UseShellExecute=true}));}
    string Config {get{return Path.Combine(server,"enshrouded_server.json");}}
    void RequireServer(){if(!File.Exists(Path.Combine(server,"enshrouded_server.exe")))throw new Exception("Select the folder containing enshrouded_server.exe using Choose folder on Overview.");}
    void LoadSettings(){RequireServer();original=File.ReadAllText(Config);doc=Data.Parse(original);dirty=false;rawPending=false;invalid.Clear();errors.Clear();UpdateDirty();}
    void Mark(){if(building)return;dirty=true;UpdateDirty();}
    void UpdateDirty(){if(save==null)return;bool pending=dirty||rawPending;save.Enabled=pending&&!busy;dirtyLabel.Text=pending?"Unsaved changes  •  Save to apply on next start":"All changes saved";dirtyLabel.ForeColor=pending?Accent:Muted;}
    void Save(){RequireServer();Data.Stopped();ValidateChildren();if(invalid.Count>0)throw new Exception("Fix the highlighted values before saving.");if(File.ReadAllText(Config)!=original)throw new Exception("The server settings changed outside Workshop. Reload them from Advanced before saving.");if(rawPending&&raw!=null){var next=Data.Parse(raw.Text);ValidateDoc(next);doc=next;}ValidateDoc(doc);Data.Backup(Config);Data.Atomic(Config,Data.Pretty(doc));LoadSettings();toast.Text="Settings saved. A backup was created. Start the server to use your changes.";Navigate(view);}
    public static void ValidateDoc(Dictionary<string,object> d){if(!d.ContainsKey("name")||!(d["name"] is string)||String.IsNullOrWhiteSpace((string)d["name"]))throw new Exception("Give your server a name.");CheckNum(d,"slotCount",1,16,true);CheckNum(d,"queryPort",1,65535,true);object g;if(d.TryGetValue("gameSettings",out g)&&!(g is Dictionary<string,object>))throw new Exception("gameSettings must be an object.");object a;foreach(string k in new[]{"userGroups","tags","bannedAccounts"})if(d.TryGetValue(k,out a)&&!(a is object[])&&!(a is ArrayList))throw new Exception(k+" must be an array.");}
    static void CheckNum(Dictionary<string,object> d,string key,decimal min,decimal max,bool integer){object v;if(!d.TryGetValue(key,out v)||v is string||v is bool)throw new Exception(Schema.Get(key).Label+" must be a number.");decimal n=Convert.ToDecimal(v);if(n<min||n>max||(integer&&n!=Decimal.Truncate(n)))throw new Exception(Schema.Get(key).Label+" must be a whole number between "+min+" and "+max+".");}
    void Navigate(string name){if(rawPending){if(MessageBox.Show(this,"The JSON editor has unapplied changes. Discard those edits and leave this page? Choose No to stay and save or apply them.","JSON edits",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;rawPending=false;}if(invalid.Count>0){MessageBox.Show(this,"Correct the highlighted value before leaving this page.","Check your entry");return;}view=name;building=true;page.SuspendLayout();foreach(Control c in page.Controls.Cast<Control>().ToArray())c.Dispose();page.Controls.Clear();invalid.RemoveWhere(c=>c.IsDisposed);errors.Clear();serverStatus=null;raw=null;foreach(var pair in navs){pair.Value.BackColor=pair.Key==name?Color.FromArgb(44,66,79):Ink;pair.Value.ForeColor=pair.Key==name?Color.White:Color.FromArgb(201,213,222);}pageTitle.Text=name;
        subtitle.Text=new Dictionary<string,string>{{"Overview","Your world, your rules. Everything you need to get started."},{"Server settings","Name your server, set player limits, and choose how friends connect."},{"Gameplay","Tune the experience. Times are in minutes; multipliers show normal as 1x."},{"Players & access","Choose what each group can do. Players join using their group's password."},{"Mods","Find a mod, check its requirements, then install and manage it here."},{"World transfers","Copy worlds between PCs on your private network."},{"Backups","Recover previous settings or make a complete copy of your world saves."},{"Advanced","File locations, connection details, and full access to the configuration."}}[name];
        if(name=="World transfers"){var t=Card("Your world, on either PC","Send or receive a world over your LAN. Open the companion on both PCs; receiving always requires approval.",155);ActionButton(t,"Open LAN companion",20,102,240,()=>{string dir=Path.Combine(server,"savegame");if(doc!=null&&doc.ContainsKey("saveDirectory")){string rel=Convert.ToString(doc["saveDirectory"]);dir=Path.GetFullPath(Path.IsPathRooted(rel)?rel:Path.Combine(server,rel));}using(var w=new TransferWindow(dir))w.ShowDialog(this);},true);}else if(name=="Overview")Overview();else if(doc==null){Notice("Choose your server first","Go to Overview and select your server installation folder.");}else if(name=="Server settings")ServerPage();else if(name=="Gameplay")Gameplay();else if(name=="Players & access")Players();else if(name=="Mods")Mods();else if(name=="Backups")Backups();else Advanced();page.ResumeLayout();building=false;UpdateDirty();}
    Panel Card(string title,string desc,int height) {var p=new Panel{BackColor=Color.White,Height=height,Width=Math.Max(400,page.ClientSize.Width-26),Margin=new Padding(0,0,0,16),Padding=new Padding(20)};p.Paint+=(s,e)=>{using(var pen=new Pen(Border))e.Graphics.DrawRectangle(pen,0,0,p.Width-1,p.Height-1);};if(title!=null){var l=LabelAt(title,new Font("Segoe UI",13,FontStyle.Bold),Ink);l.SetBounds(20,17,p.Width-40,30);p.ClientSizeChanged+=(s,e)=>l.Width=Math.Max(100,p.ClientSize.Width-40);p.Controls.Add(l);}if(desc!=null){var l=LabelAt(desc,new Font("Segoe UI",9),Muted);l.SetBounds(20,49,p.Width-40,45);p.ClientSizeChanged+=(s,e)=>l.Width=Math.Max(100,p.ClientSize.Width-40);p.Controls.Add(l);}page.Controls.Add(p);return p;}
    void Notice(string title,string desc){Card(title,desc,110);}
    Button ActionButton(Panel p,string title,int x,int y,int width,Action click,bool primary=false){var b=MakeButton(title,primary);b.SetBounds(x,y,width,38);b.Click+=(s,e)=>Safe(click);p.Controls.Add(b);return b;}
    void Overview(){
        var p=Card(doc==null?"Connect your server":Convert.ToString(doc["name"]),"ENSHROUDED DEDICATED SERVER",230);
        serverStatus=LabelAt(Data.Running?"RUNNING":"STOPPED",new Font("Segoe UI",10,FontStyle.Bold),Accent);serverStatus.SetBounds(22,102,200,26);p.Controls.Add(serverStatus);
        var detail=LabelAt(doc==null?"Choose your existing Steam server to begin.":Convert.ToString(doc["slotCount"])+" player slots   /   UDP "+Convert.ToString(doc["queryPort"])+"   /   "+Convert.ToString(doc["gameSettingsPreset"])+" difficulty",new Font("Segoe UI",10),Muted);detail.SetBounds(22,138,p.Width-44,26);p.ClientSizeChanged+=(s,e)=>detail.Width=p.Width-44;p.Controls.Add(detail);
        ActionButton(p,"Start server",22,178,155,()=>{RequireServer();if(dirty)throw new Exception("Save your changes before starting the server.");if(Data.Running)throw new Exception("The server is already running.");Process.Start(new ProcessStartInfo(Path.Combine(server,"enshrouded_server.exe")){WorkingDirectory=server,UseShellExecute=true});toast.Text="Server started. To stop safely, press Ctrl+C in the server console and let it finish saving.";},true);
        ActionButton(p,"How to stop",190,178,150,()=>MessageBox.Show(this,"Open the Enshrouded server console, press Ctrl+C once, and wait for it to exit. This gives the server time to save your world. Closing Workshop leaves the server running.","Stop your server safely"));
        if(doc!=null){var identity=Card("Server name",null,60);int nameY=55;Row(identity,doc,"name",ref nameY);identity.Height=nameY+8;}
        var steps=Card("Make it yours","Start with the basics, then tune the experience for your group.",180);
        ActionButton(steps,"1   Server settings",20,105,210,()=>Navigate("Server settings"),true);ActionButton(steps,"2   Gameplay",245,105,200,()=>Navigate("Gameplay"));ActionButton(steps,"3   Players & access",460,105,220,()=>Navigate("Players & access"));
        var folder=Card("Server installation",server,145);ActionButton(folder,"Choose folder",20,90,165,ChooseFolder);ActionButton(folder,"Open folder",200,90,165,()=>{RequireServer();Open(server);});
        Notice("Playing with friends outside your home?","Allow and forward the server's UDP port to this PC. Workshop keeps your firewall and router settings under your control. The server console shows startup and connection errors.");
    }
    void ChooseFolder(){if(dirty&&MessageBox.Show(this,"Discard unsaved changes and choose another server?","Change server",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;using(var d=new FolderBrowserDialog{Description="Select the folder containing enshrouded_server.exe",SelectedPath=server})if(d.ShowDialog(this)==DialogResult.OK){string prev=server;try{server=d.SelectedPath;RequireServer();LoadSettings();Data.Atomic(Path.Combine(Data.Home,"preferences.json"),Data.Pretty(new Dictionary<string,object>{{"server",server}}));Navigate("Overview");}catch{server=prev;throw;}}}
    void ServerPage(){var p=Card("Server identity",null,60);int y=55;foreach(string k in new[]{"name","slotCount","tags"})Row(p,doc,k,ref y);p.Height=y+8;p=Card("Chat & communication",null,60);y=55;foreach(string k in new[]{"enableVoiceChat","enableTextChat","voiceChatMode"})Row(p,doc,k,ref y);p.Height=y+8;Notice("Looking for connection settings?","The Advanced page has the listen address, UDP port, and save/log folder locations.");}
    void Row(Panel p,Dictionary<string,object> obj,string key,ref int y,bool gameplay=false) {
        if(!obj.ContainsKey(key))return;Field f=Schema.Get(key);if(key=="name"&&!Object.ReferenceEquals(obj,doc))f=new Field("Group name","A label for this set of player permissions.");object value=obj[key];int top=y;
        var title=LabelAt(f.Label,new Font("Segoe UI",10,FontStyle.Bold),Ink);title.SetBounds(20,top,p.Width-310,25);title.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;p.Controls.Add(title);
        var hint=LabelAt(f.Hint,new Font("Segoe UI",9),Muted);hint.SetBounds(20,top+27,p.Width-310,37);hint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;p.Controls.Add(hint);
        Control input;int right= p.Width-260;
        Action<object> change=v=>{obj[key]=v;if(gameplay&&doc.ContainsKey("gameSettingsPreset")){doc["gameSettingsPreset"]="Custom";toast.Text="Custom gameplay enabled automatically. Save changes when you're ready.";}Mark();};
        if(value is bool){var cb=new CheckBox{Text=(bool)value?"Enabled":"Disabled",Checked=(bool)value,AutoSize=false,Width=210,Height=32,ForeColor=Ink};cb.CheckedChanged+=(s,e)=>{cb.Text=cb.Checked?"Enabled":"Disabled";change(cb.Checked);};input=cb;}
        else if(f.Choices!=null){var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=215,Font=new Font("Segoe UI",10)};foreach(var c in f.Choices)combo.Items.Add(new Choice(c,FriendlyChoice(c)));string current=Convert.ToString(value);if(!f.Choices.Contains(current))combo.Items.Add(new Choice(current,FriendlyChoice(current)));combo.SelectedItem=combo.Items.Cast<Choice>().First(c=>c.Value==current);combo.SelectedIndexChanged+=(s,e)=>change(((Choice)combo.SelectedItem).Value);input=combo;}
        else if(value!=null&&!(value is string)&&!(value is object[])&&!(value is ArrayList)&&!(value is Dictionary<string,object>)){
            decimal n=Convert.ToDecimal(value),display=f.Minutes?n/60000000000m:n;
            bool whole=key=="slotCount"||key=="queryPort"||key=="reservedSlots"||key=="flameLevelFogRemoval";
            var num=new NumericUpDown{Width=160,Height=32,Minimum=Math.Min(f.Min,display),Maximum=Math.Max(f.Max,display),DecimalPlaces=whole?0:2,Increment=whole?1:.1m,ThousandsSeparator=true,Value=display,Font=new Font("Segoe UI",11)};
            num.ValueChanged+=(s,e)=>change(f.Minutes?(object)(num.Value*60000000000m):(object)num.Value);input=num;
            string unit=f.Minutes?"min":key.EndsWith("Factor")?"x":"";var u=LabelAt(unit,new Font("Segoe UI",10),Muted);u.SetBounds(p.Width-78,top+10,45,25);u.Anchor=AnchorStyles.Top|AnchorStyles.Right;p.Controls.Add(u);
        }else {
            var tb=new TextBox{Width=215,Font=new Font("Segoe UI",11),BorderStyle=BorderStyle.FixedSingle};
            bool tags=key=="tags",complex=value is object[]||value is ArrayList||value is Dictionary<string,object>;
            tb.Text=tags?String.Join(", ",Data.Array(value).Select(v=>Convert.ToString(v))):complex?Data.Json.Serialize(value):Convert.ToString(value);
            tb.UseSystemPasswordChar=key=="password";
            if(key=="password"){tb.Width=215;var show=new CheckBox{Text="Show password",AutoSize=false,Size=new Size(215,26),Location=new Point(right,top+43)};p.ClientSizeChanged+=(s,e)=>show.Left=p.ClientSize.Width-260;show.CheckedChanged+=(s,e)=>tb.UseSystemPasswordChar=!show.Checked;p.Controls.Add(show);}
            tb.TextChanged+=(s,e)=>{try{object v=tags?(object)tb.Text.Split(',').Select(x=>x.Trim()).Where(x=>x.Length>0).Cast<object>().ToArray():complex?Data.Json.DeserializeObject(tb.Text):(object)tb.Text;change(v);errors.SetError(tb,"");invalid.Remove(tb);}catch{errors.SetError(tb,"Enter valid JSON for this advanced value.");invalid.Add(tb);}};input=tb;
        }
        input.Location=new Point(right,top+8);input.Anchor=AnchorStyles.Top|AnchorStyles.Right;input.AccessibleName=f.Label;p.Controls.Add(input);p.ClientSizeChanged+=(s,e)=>{title.Width=Math.Max(100,p.ClientSize.Width-310);hint.Width=Math.Max(100,p.ClientSize.Width-310);input.Left=p.ClientSize.Width-260;};y+=83;
        p.Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(237,241,244)))e.Graphics.DrawLine(pen,20,top+73,p.Width-20,top+73);};
    }
    class Choice {public string Value,Label;public Choice(string value,string label){Value=value;Label=label;}public override string ToString(){return Label;}}
    static string FriendlyChoice(string s){switch(s){case "AddBackpackMaterials":return "Lose materials only";case "Everything":return "Lose all backpack items";case "NoTombstone":return "Keep backpack items";case "LoseSomeProgress":return "Lose some progress";case "LoseAllProgress":return "Lose all progress";case "KeepProgress":return "Keep progress";default:return Schema.Human(s);}}
    void Gameplay(){var preset=Card("Choose your difficulty",null,60);int y=55;Row(preset,doc,"gameSettingsPreset",ref y);preset.Height=y+8;
        var help=LabelAt("Editing any gameplay value automatically selects Custom. Nothing is written until you save.",new Font("Segoe UI",9),Accent);help.Size=new Size(page.ClientSize.Width-26,35);help.Margin=new Padding(0,0,0,12);page.Controls.Add(help);
        var filter=Card("What would you like to change?",null,110);var categories=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,57),Width=350};categories.Items.AddRange(new object[]{"Player & survival","Resources & progression","Enemies & combat","World & activities","Other settings","All gameplay settings"});categories.SelectedItem=gameCategory;categories.SelectedIndexChanged+=(s,e)=>{gameCategory=Convert.ToString(categories.SelectedItem);Navigate("Gameplay");};filter.Controls.Add(categories);
        object g;if(!doc.TryGetValue("gameSettings",out g)||!(g is Dictionary<string,object>)){Notice("No gameplay settings found","Run this server once to generate its settings, then reload from Advanced.");return;}var game=(Dictionary<string,object>)g;
        foreach(string group in new[]{"Player & survival","Resources & progression","Enemies & combat","World & activities","Other settings"}) {
            if(gameCategory!="All gameplay settings"&&gameCategory!=group)continue;
            var keys=game.Keys.Where(k=>Schema.Get(k).Group==group).ToArray();if(keys.Length==0)continue;var card=Card(group,null,60);y=55;foreach(var k in keys)Row(card,game,k,ref y,true);card.Height=y+8;
        }
    }
    void Players(){Notice("Passwords grant permissions","Each group has its own join password. Share the visitor or guest password with new players; keep the administrator password private.");
        object g;if(!doc.TryGetValue("userGroups",out g))return;var groups=Data.Array(g).ToList();
        foreach(var item in groups){var group=item as Dictionary<string,object>;if(group==null)continue;var p=Card(Convert.ToString(group.ContainsKey("name")?group["name"]:"Player group"),null,60);int y=55;
            foreach(string k in group.Keys.ToArray())Row(p,group,k,ref y);ActionButton(p,"Remove group",20,y,175,()=>{if(MessageBox.Show(this,"Remove this permission group? Save changes to apply.","Remove group",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;groups.Remove(item);doc["userGroups"]=groups.ToArray();Mark();Navigate("Players & access");});p.Height=y+57;
        }
        var add=Card("Need another role?","Add a group, set its permissions, and share its unique password.",140);ActionButton(add,"Add player group",20,86,190,()=>{groups.Add(new Dictionary<string,object>{{"name","New group"},{"password",Guid.NewGuid().ToString("N").Substring(0,16)},{"canKickBan",false},{"canAccessInventories",false},{"canEditWorld",false},{"canEditBase",false},{"canExtendBase",false},{"reservedSlots",0}});doc["userGroups"]=groups.ToArray();Mark();Navigate("Players & access");},true);
        Notice("Banned accounts","The Advanced JSON editor includes the full banned-account list, preserving the format your server generated.");
    }
    void Advanced(){var p=Card("Connection & file locations",null,60);int y=55;foreach(string k in new[]{"ip","queryPort","saveDirectory","logDirectory"})Row(p,doc,k,ref y);p.Height=y+8;
        var known=new HashSet<string>(new[]{"name","slotCount","tags","enableVoiceChat","enableTextChat","voiceChatMode","gameSettingsPreset","gameSettings","userGroups","ip","queryPort","saveDirectory","logDirectory"});var extra=doc.Keys.Where(k=>!known.Contains(k)).ToArray();if(extra.Length>0){p=Card("Additional server settings","Structured values here use JSON. The editor below can change any key.",100);y=100;foreach(string k in extra)Row(p,doc,k,ref y);p.Height=y+8;}
        var full=Card("Full configuration","For custom keys and structures. Apply JSON to the editor, then Save changes. Passwords are visible in this view.",500);
        raw=new TextBox{Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,AcceptsTab=true,Font=new Font("Consolas",10),Text=Data.Pretty(doc),Location=new Point(20,100),Size=new Size(full.Width-40,330),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};full.Controls.Add(raw);raw.TextChanged+=(s,e)=>{rawPending=true;UpdateDirty();};
        ActionButton(full,"Apply JSON to editor",20,445,215,()=>{var next=Data.Parse(raw.Text);ValidateDoc(next);doc=next;rawPending=false;Mark();toast.Text="JSON applied to the editor. Save changes to write it to your server.";Navigate("Advanced");},true);
        ActionButton(full,"Reload from disk",250,445,180,()=>{if(dirty&&MessageBox.Show(this,"Discard unsaved changes and reload?","Reload settings",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;LoadSettings();Navigate("Advanced");});
        var refs=Card("Need the exact allowed values?","The game validates settings at startup. The official reference explains ranges and server behavior.",140);ActionButton(refs,"Official settings guide",20,85,220,()=>Open("https://enshrouded.zendesk.com/hc/en-us/articles/20453241249821-Server-Gameplay-Settings"));
    }
    void Backups(){var p=Card("Protect your world","Create a world backup while the server is stopped. Configuration backups are created whenever you save settings.",155);
        ActionButton(p,"Back up world now",20,102,220,()=>{RequireServer();Data.Stopped();string rel=Convert.ToString(doc["saveDirectory"]);string path=Path.GetFullPath(Path.IsPathRooted(rel)?rel:Path.Combine(server,rel));if(!Directory.Exists(path))throw new Exception("Your world save folder does not exist yet. Start the server once to create a world.");string dir=Path.Combine(Data.Home,"world-backups");Directory.CreateDirectory(dir);string dest=Path.Combine(dir,"world-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip");ZipFile.CreateFromDirectory(path,dest,CompressionLevel.Optimal,false);toast.Text="World backup created: "+Path.GetFileName(dest);},true);
        ActionButton(p,"Open backup folders",255,102,230,()=>{Directory.CreateDirectory(Path.Combine(Data.Home,"backups"));Open(Data.Home);});
        string root=Path.Combine(Data.Home,"backups");string[] files=Directory.Exists(root)?Directory.GetFiles(root,"enshrouded_server.json",SearchOption.AllDirectories).OrderByDescending(f=>File.GetLastWriteTime(f)).ToArray():new string[0];
        if(files.Length==0)Notice("No configuration backups yet","Your first settings save creates a backup of the previous configuration. Your current settings have not been changed.");
        foreach(string file in files.Take(30)){string selected=file;var card=Card(File.GetLastWriteTime(file).ToString("dddd, MMM d  ·  h:mm tt"),"Server configuration  /  includes group passwords",135);ActionButton(card,"Restore this version",20,82,210,()=>{Data.Stopped();if(MessageBox.Show(this,"Restore this configuration? Your current version will be backed up first. Unsaved edits will be discarded.","Restore settings",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;var next=Data.Parse(File.ReadAllText(selected));ValidateDoc(next);Data.Backup(Config);Data.Atomic(Config,Data.Pretty(next));LoadSettings();Navigate("Backups");toast.Text="Configuration restored. Your previous configuration is also backed up.";});}
        Notice("Restore a world save","Stop the server first. Open the ZIP and copy its contents into the configured world save folder. Back up the existing folder before replacing it. Configuration backups and world backups are separate.");
    }
    void Mods(){
        var intro=Card("1   Set up the mod loader",File.Exists(Path.Combine(server,"dbghelp.dll"))?"A proxy DLL is installed. Use the author's release notes to check your game build.":"EML loads supported community mods when your server starts.",155);
        var install=ActionButton(intro,File.Exists(Path.Combine(server,"dbghelp.dll"))?"Check / update EML":"Install EML loader",20,102,220,()=>Async(InstallLoader),true);
        ActionButton(intro,"Release notes",255,102,165,()=>Open("https://github.com/Brabb3l/kfc-parser/releases"));
        ActionButton(intro,"Disable loader",435,102,170,()=>{Data.Stopped();string f=Path.Combine(server,"dbghelp.dll");if(!File.Exists(f))throw new Exception("No loader is installed.");if(MessageBox.Show(this,"Disable the installed proxy loader? A backup is kept and your mod files stay in place.","Disable loader",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;Data.Backup(f);File.Delete(f);Navigate("Mods");});
        var find=Card("2   Find a server-only mod","Only author-documented server-only mods appear in the catalog. Client-required and unverified mods are hidden.",190);
        ActionButton(find,"Browse server-only catalog",20,103,300,()=>{using(var catalog=new ServerCatalogWindow(InstallMod)){catalog.ShowDialog(this);}},true);
        var catalogNote=LabelAt("Small reviewed catalog, not the complete Nexus library. Game-build support and installation method are shown separately.",new Font("Segoe UI",9),Muted);catalogNote.SetBounds(20,150,find.Width-40,33);find.Controls.Add(catalogNote);
        var pkg=Card("3   Install a downloaded mod","Choose an EML ZIP with a mod.json file. Review its details before it is installed. KFC replacements and other loaders need manual setup.",196);
        ActionButton(pkg,"Choose mod ZIP…",20,105,220,()=>{Data.Stopped();using(var d=new OpenFileDialog{Filter="EML mod package (*.zip)|*.zip",Title="Choose an EML mod ZIP"})if(d.ShowDialog(this)==DialogResult.OK)InstallMod(d.FileName);},true);
        ActionButton(pkg,"Install from URL…",255,105,220,()=>{using(var input=new InputDialog("Download a mod ZIP","Paste a direct HTTPS ZIP link, not a mod page.")){if(input.ShowDialog(this)==DialogResult.OK)Async(async()=>{Data.Stopped();string temp=Path.Combine(Path.GetTempPath(),"enshrouded-"+Guid.NewGuid().ToString("N")+".zip");try{toast.Text="Downloading mod package…";await Data.Download(input.Value,temp);InstallMod(temp);}finally{if(File.Exists(temp))File.Delete(temp);}});}});
        Notice("Compatibility comes first","Read the mod author's supported game version, dependencies, and client requirements. Workshop supports EML packages; it cannot verify that a community mod works with your current server build.");
        var heading=LabelAt("Installed mods",new Font("Segoe UI",15,FontStyle.Bold),Ink);heading.Size=new Size(page.ClientSize.Width-26,40);heading.Margin=new Padding(0,8,0,8);page.Controls.Add(heading);int count=0;
        foreach(string kind in new[]{"mods","mods-disabled"}){string dir=Path.Combine(server,kind);if(!Directory.Exists(dir))continue;foreach(string entry in Directory.GetFileSystemEntries(dir)){
            count++;string source=entry;bool enabled=kind=="mods";string name=Path.GetFileName(entry);var card=Card(name,enabled?"ENABLED  ·  Loads at next server start":"DISABLED  ·  Kept on disk, not loaded",140);
            ActionButton(card,enabled?"Disable mod":"Enable mod",20,88,175,()=>{Data.Stopped();string targetDir=Path.Combine(server,enabled?"mods-disabled":"mods");Directory.CreateDirectory(targetDir);string dest=Path.Combine(targetDir,Path.GetFileName(source));if(Directory.Exists(dest)||File.Exists(dest))throw new Exception("A package with that name already exists at the destination.");if(Directory.Exists(source))Directory.Move(source,dest);else File.Move(source,dest);Navigate("Mods");});
            ActionButton(card,"Open files",210,88,145,()=>Open(Directory.Exists(source)?source:Path.GetDirectoryName(source)));
        }}if(count==0)Notice("No mods installed","Install the loader above, then choose your first EML mod ZIP. You can disable a mod here later without deleting it.");
    }
    async Task InstallLoader(){RequireServer();Data.Stopped();toast.Text="Checking EML releases…";var r=Data.Parse(await Data.Get("https://api.github.com/repos/Brabb3l/kfc-parser/releases/latest"));var assets=Data.Array(r["assets"]).Cast<Dictionary<string,object>>();var asset=assets.FirstOrDefault(a=>Convert.ToString(a["name"]).Equals("dbghelp.dll",StringComparison.OrdinalIgnoreCase));if(asset==null)throw new Exception("The latest release packages the loader differently. Open Release notes and follow the author's instructions for dbghelp.dll.");
        if(MessageBox.Show(this,"Install EML "+r["tag_name"]+" from Brabb3l/kfc-parser?\n\nConfirm this release supports your server build. The loader runs community code inside the server.","Install mod loader",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        string temp=Path.Combine(Path.GetTempPath(),"eml-"+Guid.NewGuid().ToString("N")+".dll");try{toast.Text="Downloading EML…";await Data.Download(Convert.ToString(asset["browser_download_url"]),temp);Data.Stopped();byte[] magic=new byte[2];using(var s=File.OpenRead(temp))s.Read(magic,0,2);if(magic[0]!=77||magic[1]!=90)throw new Exception("The download is not a Windows DLL.");string target=Path.Combine(server,"dbghelp.dll");Data.Backup(target);string stage=target+".workshop-download";File.Copy(temp,stage,true);if(File.Exists(target))File.Replace(stage,target,null);else File.Move(stage,target);Directory.CreateDirectory(Path.Combine(server,"mods"));toast.Text="EML "+r["tag_name"]+" installed. You can now add an EML mod ZIP.";Navigate("Mods");}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    void InstallMod(string zip){RequireServer();Data.Stopped();string manifest=Data.InspectMod(zip),stage=Path.Combine(Path.GetTempPath(),"workshop-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try{Data.Extract(zip,stage);string source=Path.GetDirectoryName(Path.Combine(stage,manifest.Replace('/',Path.DirectorySeparatorChar)));var meta=Data.Parse(File.ReadAllText(Path.Combine(source,"mod.json")));string title=meta.ContainsKey("name")?Convert.ToString(meta["name"]):meta.ContainsKey("id")?Convert.ToString(meta["id"]):Path.GetFileNameWithoutExtension(zip);string name=Regex.Replace(title,@"[^a-zA-Z0-9_.-]","_").Trim('.');if(name.Length==0||name.Length>100)throw new Exception("This mod has an invalid package name.");
            using(var review=new ModReview(title,Data.Pretty(meta))){if(review.ShowDialog(Form.ActiveForm??this)!=DialogResult.OK)return;}
            Data.Stopped();string dir=Path.Combine(server,"mods"),dest=Path.Combine(dir,name);if(Directory.Exists(dest)||File.Exists(dest)||Directory.Exists(Path.Combine(server,"mods-disabled",name)))throw new Exception("This mod is already present. Move its existing folder out first if you want to replace it.");Directory.CreateDirectory(dir);
            string pending=Path.Combine(server,".workshop-staging-"+Guid.NewGuid().ToString("N"));try{Data.CopyTree(source,pending);Directory.Move(pending,dest);}finally{if(Directory.Exists(pending))Directory.Delete(pending,true);}Navigate("Mods");toast.Text=title+" installed. Start the server to load it.";
        }finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
    public void Smoke(string folder){Directory.CreateDirectory(folder);Shown+=(s,e)=>{var t=new Timer{Interval=500};int i=0;string[] names={"Overview","Server settings","Gameplay","Players & access","Mods","World transfers","Backups","Advanced"};t.Tick+=(s2,e2)=>{try{if(i==8)Size=MinimumSize;Navigate(names[i%8]);page.PerformLayout();using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(folder,(i>=8?"minimum-":"")+names[i%8].Replace(" & ","-").Replace(" ","-")+".png"));}if(i==0){var name=page.Controls.Cast<Control>().SelectMany(c=>c.Controls.Cast<Control>()).OfType<TextBox>().First(c=>c.AccessibleName=="Server name");name.Text="Workshop name edit test";if(Convert.ToString(doc["name"])!="Workshop name edit test"||!dirty)throw new Exception("Overview server name edit did not bind to the configuration.");LoadSettings();}i++;if(i==16){t.Stop();File.WriteAllText(Path.Combine(folder,"smoke.txt"),"PASS: all eight views rendered at normal and minimum sizes. Overview server name editing verified. Settings were not saved.");dirty=false;Close();}}catch(Exception ex){File.WriteAllText(Path.Combine(folder,"smoke-error.txt"),ex.ToString());t.Stop();dirty=false;Close();}};t.Start();};}
}
class InputDialog:Form {
    TextBox input;public string Value{get{return input.Text.Trim();}}
    public InputDialog(string title,string hint){Text=title;Size=new Size(650,210);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;Controls.Add(new Label{Text=hint,Location=new Point(20,20),Size=new Size(590,30)});input=new TextBox{Location=new Point(20,60),Width=590};Controls.Add(input);var ok=new Button{Text="Download & review",Location=new Point(410,110),Size=new Size(200,35),DialogResult=DialogResult.OK};Controls.Add(ok);AcceptButton=ok;}
}
class ModReview:Form {
    public ModReview(string name,string meta){Text="Review mod installation";Size=new Size(680,610);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);MinimizeBox=false;MaximizeBox=false;Controls.Add(new Label{Text=name,Font=new Font("Segoe UI",18,FontStyle.Bold),Location=new Point(22,20),Size=new Size(620,40)});Controls.Add(new Label{Text="Check the author, version, and requirements below. Install only if the mod supports your server and you trust its source.",Location=new Point(22,72),Size=new Size(620,54)});Controls.Add(new TextBox{Text=meta,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Font=new Font("Consolas",10),Location=new Point(22,137),Size=new Size(620,320)});var check=new CheckBox{Text="Author confirms server-only support; I checked compatibility.",Location=new Point(22,475),Size=new Size(620,30)};Controls.Add(check);var install=new Button{Text="Install mod",Enabled=false,Location=new Point(470,515),Size=new Size(170,36),DialogResult=DialogResult.OK};check.CheckedChanged+=(s,e)=>install.Enabled=check.Checked;Controls.Add(install);var cancel=new Button{Text="Cancel",Location=new Point(285,515),Size=new Size(170,36),DialogResult=DialogResult.Cancel};Controls.Add(cancel);CancelButton=cancel;}
}
static class Program {
    [STAThread]static void Main(string[] args){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Length>1&&args[0]=="/lantest"){LanTests.Run(args[1]);return;}if(args.Length>0&&args[0]=="/companion"){Application.Run(new TransferWindow(""));return;}if(args.Length>0&&args[0]=="/selftest"){Test.Run(args.Length>1?args[1]:Path.Combine(Data.Home,"test-results.txt"));return;}Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"Workshop");if(args.Length>1&&args[0]=="/browsertest"){var b=new NexusBrowser(p=>{},true);b.Smoke(args[1]);Application.Run(b);return;}var app=new Workshop();if(args.Length>1&&args[0]=="/smoke")app.Smoke(args[1]);Application.Run(app);}
}
static class Test {
    public static void Run(string log){var messages=new List<string>();string temp=Path.Combine(Data.Home,"workshop-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);try{
        var d=Data.Parse("{\"name\":\"Test\",\"slotCount\":16,\"queryPort\":15637,\"tags\":[],\"userGroups\":[{\"password\":\"test\"}],\"gameSettings\":{\"dayTimeDuration\":1800000000000,\"precise\":0.123456789012345,\"flag\":true},\"future\":{\"key\":[1,2,3]}}");Workshop.ValidateDoc(d);string json=Data.Pretty(d);var round=Data.Parse(json);if(Data.Array(round["tags"]).Length!=0||Convert.ToDecimal(((Dictionary<string,object>)round["gameSettings"])["dayTimeDuration"])!=1800000000000m)throw new Exception("Roundtrip lost array or time precision.");if(Data.Json.Serialize(d)!=Data.Json.Serialize(round))throw new Exception("JSON roundtrip changed values.");messages.Add("PASS: all types, unknown settings, nested groups, empty arrays, and time precision roundtrip.");
        string path=Path.Combine(temp,"config.json");Data.Atomic(path,json);Data.Atomic(path,json);if(File.ReadAllText(path)!=json)throw new Exception("Atomic save failed.");messages.Add("PASS: first save and atomic replacement.");
        d["slotCount"]=17;bool blocked=false;try{Workshop.ValidateDoc(d);}catch{blocked=true;}if(!blocked)throw new Exception("Player limit validation failed.");messages.Add("PASS: invalid player limit rejected.");
        string zip=Path.Combine(temp,"bad.zip");using(var a=ZipFile.Open(zip,ZipArchiveMode.Create)){a.CreateEntry("../outside.lua");a.CreateEntry("mod.json");}blocked=false;try{Data.InspectMod(zip);}catch{blocked=true;}if(!blocked)throw new Exception("ZIP traversal allowed.");messages.Add("PASS: unsafe archive path rejected.");
        string good=Path.Combine(temp,"good.zip");using(var a=ZipFile.Open(good,ZipArchiveMode.Create)){using(var w=new StreamWriter(a.CreateEntry("wrapper/mod.json").Open()))w.Write("{\"name\":\"Example\"}");a.CreateEntry("wrapper/src/main.lua");}if(Data.InspectMod(good)!="wrapper/mod.json")throw new Exception("Wrapper detection failed.");Data.Extract(good,Path.Combine(temp,"extracted"));if(!File.Exists(Path.Combine(temp,"extracted","wrapper","mod.json")))throw new Exception("Extraction failed.");messages.Add("PASS: wrapped EML package recognized and extracted.");
        foreach(string url in new[]{"https://green-peninsula-interlude-clearing.click/","https://nexusmods.com.evil.example/","http://www.nexusmods.com/","https://nexusmods.com@evil.example/","file:///C:/Windows/","javascript:alert(1)"})if(NexusBrowser.AllowedPage(url)||NexusBrowser.AllowedDownload(url))throw new Exception("Unsafe browser URL allowed: "+url);
        if(!NexusBrowser.AllowedPage("https://www.nexusmods.com/enshrouded/mods/")||!NexusBrowser.AllowedPage("https://users.nexusmods.com/")||!NexusBrowser.AllowedDownload("https://example.nexus-cdn.com/file.zip"))throw new Exception("Expected Nexus URL rejected.");
        messages.Add("PASS: scam domain, deceptive hostnames, credentials-in-URL, non-HTTPS and executable URL schemes blocked; Nexus hosts accepted.");
        if(ServerCatalog.Visible().Any(m=>m.Side!="server-only")||ServerCatalog.Visible().Length!=2)throw new Exception("Server-only catalog filter failed.");
        if(ServerCatalog.IsServerModPage("https://www.nexusmods.com/enshrouded/mods/117")||ServerCatalog.IsServerModPage("https://www.nexusmods.com/enshrouded/mods/999999")||ServerCatalog.IsServerModPage("https://evil.example/enshrouded/mods/110"))throw new Exception("Unverified/client-required mod passed catalog filter.");
        if(!ServerCatalog.IsServerModPage("https://www.nexusmods.com/games/enshrouded/mods/110?tab=files")||ServerCatalog.Visible("whitelist").Length!=1)throw new Exception("Catalog search or modern Nexus URL failed.");
        messages.Add("PASS: server-only catalog, search, old/new Nexus URLs, client-required exclusion, unknown exclusion and source-domain checks.");
        messages.Add("ALL TESTS PASSED");
    }catch(Exception ex){messages.Add("FAIL: "+ex);Environment.ExitCode=1;}finally{Directory.Delete(temp,true);File.WriteAllLines(log,messages);}}
}
}




