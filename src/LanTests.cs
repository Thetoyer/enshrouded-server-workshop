using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
namespace ShroudWorkshop {
static class LanTests {
static void Check(bool b,string why){if(!b)throw new Exception(why);}
public static void Run(string report){string root=Path.Combine(Data.Home,"lan-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
string source=Path.Combine(root,"source"),dest=Path.Combine(root,"dest");Directory.CreateDirectory(source);Directory.CreateDirectory(dest);File.WriteAllText(Path.Combine(source,"world3.ksd"),"new world");File.WriteAllText(Path.Combine(source,"characters.ksd"),"private character");File.WriteAllText(Path.Combine(dest,"world0.ksd"),"old world");File.WriteAllText(Path.Combine(dest,"world1.ksd"),"other world");var package=WorldTransfer.Pack(source,"world3.ksd");string backup=WorldTransfer.Install(package,dest,"world0.ksd");Check(File.ReadAllText(Path.Combine(dest,"world0.ksd"))=="new world","Slot rename");Check(File.ReadAllText(Path.Combine(backup,"world0.ksd"))=="old world","Backup");Check(File.ReadAllText(Path.Combine(dest,"world1.ksd"))=="other world","Other slot changed");Check(!File.Exists(Path.Combine(dest,"characters.ksd")),"Characters leaked");
using(var m=new MemoryStream()){using(var z=new ZipArchive(m,ZipArchiveMode.Create,true)){using(var w=new StreamWriter(z.CreateEntry("../world0.ksd").Open()))w.Write("bad");}bool rejected=false;try{WorldTransfer.Install(m.ToArray(),dest,"world0.ksd");}catch{rejected=true;}Check(rejected,"Traversal accepted");}
var key=WorldTransfer.Random(32);var nonce=WorldTransfer.Random(32);using(var m=new MemoryStream()){WorldTransfer.WritePacket(m,key,nonce,package);var bytes=m.ToArray();m.Position=0;Check(package.SequenceEqual(WorldTransfer.ReadPacket(m,key,nonce)),"Encrypted roundtrip");bytes[20]^=1;bool rejected=false;try{WorldTransfer.ReadPacket(new MemoryStream(bytes),key,nonce);}catch{rejected=true;}Check(rejected,"Tampering accepted");bool wrong=false;try{WorldTransfer.ReadPacket(new MemoryStream(m.ToArray()),WorldTransfer.Random(32),nonce);}catch{wrong=true;}Check(wrong,"Wrong key accepted");}
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;var receiver=Task.Run(()=>{using(var c=listener.AcceptTcpClient()){c.ReceiveTimeout=3000;c.SendTimeout=3000;var s=c.GetStream();var incoming=WorldTransfer.ReadPacket(s,key,nonce);Check(package.SequenceEqual(incoming),"Network data differs");WorldTransfer.WritePacket(s,key,nonce,Encoding.UTF8.GetBytes("approved"));}});using(var c=new TcpClient()){c.Connect(IPAddress.Loopback,port);c.ReceiveTimeout=3000;c.SendTimeout=3000;WorldTransfer.WritePacket(c.GetStream(),key,nonce,package);Check(Encoding.UTF8.GetString(WorldTransfer.ReadPacket(c.GetStream(),key,nonce))=="approved","Acknowledgement");}receiver.Wait();listener.Stop();Check(WorldTransfer.Private(IPAddress.Parse("192.168.4.42"))&&!WorldTransfer.Private(IPAddress.Parse("8.8.8.8")),"LAN restriction");File.WriteAllText(report,"ALL LAN TESTS PASSED: slot conversion, backup, character exclusion, other slots preserved, traversal rejection, encrypted roundtrip, tampering rejection, wrong-key rejection, TCP transfer acknowledgement, private IPv4 validation. Synthetic worlds only; no live game saves changed.");
}catch(Exception ex){File.WriteAllText(report,ex.ToString());Environment.ExitCode=1;}}
}
}
