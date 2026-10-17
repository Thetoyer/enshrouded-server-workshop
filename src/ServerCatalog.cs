using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ShroudWorkshop {
class CatalogMod {
    public int Id;public string Name,Side,Summary,Setup,Build,Warning;
    public string Url {get{return "https://www.nexusmods.com/enshrouded/mods/"+Id;}}
    public override string ToString(){return Name;}
}
static class ServerCatalog {
    public static readonly CatalogMod[] Entries={
        new CatalogMod{Id=110,Name="Global XP Share",Side="server-only",Summary="Shares experience across players. The author explicitly says only the host installs it; clients need no mod.",Setup="Manual DLL setup. Uses its own dbghelp.dll and INI file, not an EML mod.json package. Do not overwrite an existing EML loader without checking compatibility.",Build="Not tested by Workshop on your installed build. Author page last updated June 19, 2026.",Warning=""},
        new CatalogMod{Id=155,Name="Enshrouded Server Whitelist",Side="server-only",Summary="Restricts dedicated-server admission using a SteamID64 list. The author explicitly says no client mod is required.",Setup="Manual setup. Requires the author's separate startup script; Workshop's normal Start server button would bypass that script.",Build="Author documents build 25967645. Workshop has not validated this mod against your installed build.",Warning="Nexus reported 'Some suspicious files' when checked October 17, 2026. Server-side classification is not a safety endorsement. Review the file report before considering installation."},
        new CatalogMod{Id=117,Name="Player PVP",Side="server-and-clients",Summary="Author requires matching patched server and client files.",Setup="",Build="",Warning=""},
        new CatalogMod{Id=109,Name="No Shroud",Side="server-and-clients",Summary="Author requires the same patched files on the server and clients.",Setup="",Build="",Warning=""}
    };
    public static CatalogMod[] Visible(string query=""){return Entries.Where(m=>m.Side=="server-only"&&(m.Name+" "+m.Summary).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).ToArray();}
    public static int? ModId(string url){Uri u;if(!Uri.TryCreate(url,UriKind.Absolute,out u)||!NexusBrowser.AllowedPage(url))return null;var match=Regex.Match(u.AbsolutePath,@"^/(?:games/)?enshrouded/mods/(\d+)(?:/|$)",RegexOptions.IgnoreCase);int id;return match.Success&&Int32.TryParse(match.Groups[1].Value,out id)?(int?)id:null;}
    public static bool IsServerModPage(string url){int? id=ModId(url);return id.HasValue&&Entries.Any(e=>e.Id==id.Value&&e.Side=="server-only");}
}
class ServerCatalogWindow:Form {
    public ServerCatalogWindow(Action<string> install){
        Text="Server-only mods · Enshrouded Workshop";Size=new Size(1050,790);MinimumSize=new Size(920,700);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(242,245,247);Padding=new Padding(24);
        var header=new Label{Dock=DockStyle.Top,Height=95,Text="SERVER-ONLY MODS\r\nAuthor-documented: clients do not need to install these mods.\r\nSmall reviewed catalog • checked October 17, 2026 • not a complete Nexus index",Font=new Font("Segoe UI",11),ForeColor=Color.FromArgb(27,40,53)};Controls.Add(header);
        var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=52};Controls.Add(bottom);
        var open=new Button{Text="Open selected mod on Nexus",Width=285,Height=38,BackColor=Color.FromArgb(15,118,110),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};bottom.Controls.Add(open);
        var info=new Label{Text="Manual setup is shown explicitly. Nothing is installed by opening a page.",Width=580,Height=42,Padding=new Padding(12,8,0,0)};bottom.Controls.Add(info);
        var split=new SplitContainer{Size=new Size(970,500),Dock=DockStyle.Fill,SplitterDistance=310};Controls.Add(split);split.BringToFront();
        var query=new TextBox{Dock=DockStyle.Top,AccessibleName="Search server-only mods"};split.Panel1.Controls.Add(query);
        var list=new ListBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",11),IntegralHeight=false};split.Panel1.Controls.Add(list);list.BringToFront();
        var detail=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,Font=new Font("Segoe UI",11)};split.Panel2.Padding=new Padding(18);split.Panel2.BackColor=Color.White;split.Panel2.Controls.Add(detail);
        Action refresh=()=>{list.Items.Clear();list.Items.AddRange(ServerCatalog.Visible(query.Text));if(list.Items.Count>0)list.SelectedIndex=0;else{detail.Text="No server-only entries match. Unknown and client-required mods remain hidden.";open.Enabled=false;}};
        list.SelectedIndexChanged+=(s,e)=>{var mod=list.SelectedItem as CatalogMod;open.Enabled=mod!=null;if(mod!=null)detail.Text=mod.Name+"\r\n\r\nSERVER ONLY — AUTHOR DOCUMENTED\r\n\r\n"+mod.Summary+"\r\n\r\nINSTALLATION\r\n"+mod.Setup+"\r\n\r\nGAME BUILD\r\n"+mod.Build+(String.IsNullOrEmpty(mod.Warning)?"":"\r\n\r\nFILE REPORT\r\n"+mod.Warning)+"\r\n\r\nSOURCE\r\n"+mod.Url+"\r\n\r\nClassification describes where the mod runs. It does not certify safety or compatibility with the current game update.";};
        query.TextChanged+=(s,e)=>refresh();open.Click+=(s,e)=>{var mod=list.SelectedItem as CatalogMod;if(mod==null)return;using(var browser=new NexusBrowser(install,false,mod.Url,true)){browser.ShowDialog(this);}};
        refresh();
    }
}
}
