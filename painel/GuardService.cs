using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace FocoLocal {
 public class GuardService:ServiceBase {
  readonly object gate=new object();volatile bool stopping;Thread server;Timer timer;NamedPipeServerStream listener;
  readonly string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"FocoLocalGuard");
  readonly string hosts=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"drivers\etc\hosts");
  GuardState state;string sid;string error="";
  public GuardService(){ServiceName="FocoLocalGuard";CanStop=true;CanShutdown=true;AutoLog=true;}
  void Save(){string path=Path.Combine(root,"state.json"),temp=path+".tmp";File.WriteAllText(temp,GuardWire.Json.Serialize(state),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
  protected override void OnStart(string[] args){sid=File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"user.sid")).Trim();string path=Path.Combine(root,"state.json");state=File.Exists(path)?GuardWire.Json.Deserialize<GuardState>(File.ReadAllText(path)):new GuardState();if(state==null||state.pauses==null||state.held==null)throw new Exception("Estado inválido; backup preservado.");stopping=false;Tick(null);timer=new Timer(Tick,null,1000,1000);server=new Thread(Serve){IsBackground=true};server.Start();}
  void Tick(object unused){lock(gate){try{if(stopping)return;UpdateHosts(false);error="";}catch(Exception ex){error=ex.Message;}}}
  void UpdateHosts(bool restoreOnly){
   double now=Engine.Stamp(DateTime.UtcNow);
   if(state.held.Count==0&&!state.pauses.Any(p=>!restoreOnly&&!p.cancelled&&now>=p.startsAt&&now<p.endsAt))return;
   using(var file=new FileStream(hosts,FileMode.Open,FileAccess.ReadWrite,FileShare.Read)){
    if(file.Length>4*1024*1024)throw new IOException("Arquivo hosts excede o limite suportado.");
    var before=new byte[(int)file.Length];int read=0;while(read<before.Length){int n=file.Read(before,read,before.Length-read);if(n==0)throw new IOException("Leitura incompleta.");read+=n;}
    Encoding encoding=new UTF8Encoding(false,true);int skip=0;
    if(before.Length>=3&&before[0]==239&&before[1]==187&&before[2]==191){encoding=new UTF8Encoding(true,true);skip=3;}
    else if(before.Length>=2&&before[0]==255&&before[1]==254){encoding=Encoding.Unicode;skip=2;}
    else if(before.Length>=2&&before[0]==254&&before[1]==255){encoding=Encoding.BigEndianUnicode;skip=2;}
    string original=encoding.GetString(before,skip,before.Length-skip),updated=GuardHosts.Transform(original,state,now,restoreOnly);
    // Persist the restoration journal BEFORE removing any hosts line.
    if(updated!=original)Save();
    if(updated!=original){byte[] body=encoding.GetBytes(updated),preamble=encoding.GetPreamble();byte[] after=preamble.Concat(body).ToArray();try{file.Position=0;file.Write(after,0,after.Length);file.SetLength(after.Length);file.Flush(true);}catch{file.Position=0;file.Write(before,0,before.Length);file.SetLength(before.Length);file.Flush(true);throw;}}
    int heldCount=state.held.Count;GuardHosts.Finish(state,now,restoreOnly);if(heldCount!=state.held.Count)Save();
    if(updated!=original)DnsFlushResolverCache();
   }
  }
  [System.Runtime.InteropServices.DllImport("dnsapi.dll",SetLastError=true)]static extern bool DnsFlushResolverCache();
  void Serve(){while(!stopping){try{
   var acl=new PipeSecurity();acl.SetAccessRuleProtection(true,false);acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid),PipeAccessRights.ReadWrite,AccessControlType.Allow));
   using(var pipe=new NamedPipeServerStream(GuardWire.Pipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,acl)){
    listener=pipe;pipe.WaitForConnection();if(stopping)return;
    GuardReply reply;try{var request=GuardWire.Receive<GuardRequest>(pipe);lock(gate){
     if(request==null)throw new Exception("Pedido inválido.");
     if(request.command=="request"){if(error!="")throw new Exception(error);var pause=GuardHosts.Request(state,request,DateTime.UtcNow);Save();reply=new GuardReply{ok=true,pause=pause};}
     else if(request.command=="cancel"){var pause=state.pauses.FirstOrDefault(p=>p.id==request.id);if(pause!=null){pause.cancelled=true;Save();UpdateHosts(false);}reply=new GuardReply{ok=true,pause=pause};}
     else if(request.command=="status")reply=new GuardReply{ok=error=="",error=error,pauses=state.pauses.ToList()};
     else throw new Exception("Comando inválido.");
    }}catch(Exception ex){reply=new GuardReply{ok=false,error=ex.Message};}GuardWire.Send(pipe,reply);
   }
  }catch{if(stopping)return;Thread.Sleep(250);}}}
  protected override void OnStop(){stopping=true;if(timer!=null)timer.Dispose();if(listener!=null)listener.Dispose();lock(gate){UpdateHosts(true);}}
  protected override void OnShutdown(){OnStop();}
  public static void Main(){ServiceBase.Run(new GuardService());}
 }
}
