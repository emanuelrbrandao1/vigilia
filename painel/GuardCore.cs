using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FocoLocal {
 public class GuardRequest {public string command,id;public string[] domains;}
 public class GuardReply {public bool ok;public string error;public Pause pause;public List<Pause> pauses;}
 public class HeldLine {public string line;public string domain;}
 public class GuardState {public List<Pause> pauses=new List<Pause>();public List<HeldLine> held=new List<HeldLine>();}
 public static class PauseRecovery {
  public static Pause Prepare(State s,Rule r,DateTime utc){var p=Engine.LatestPause(s,r);if(p!=null&&p.confirmationPending&&!p.cancelled&&!p.cancelRequested)return p;p=Engine.RequestPause(s,r,utc);p.windowsManaged=true;p.confirmationPending=true;return p;}
  public static void Merge(State s,IEnumerable<Pause> remote){if(remote==null)return;foreach(var item in remote){var serializer=new JavaScriptSerializer();var p=serializer.Deserialize<Pause>(serializer.Serialize(item));p.windowsManaged=true;p.confirmationPending=false;int index=s.pauses.FindIndex(x=>x.id==p.id);if(index>=0){p.cancelled=p.cancelled||s.pauses[index].cancelled;p.cancelRequested=!p.cancelled&&s.pauses[index].cancelRequested;s.pauses[index]=p;}else s.pauses.Add(p);}}
 }
 // Only the application's marked hosts block is eligible for temporary changes.
 public static class GuardHosts {
  public const string Begin="# BEGIN BLOQUEADOR-SIMPLES-LOCAL",End="# END BLOQUEADOR-SIMPLES-LOCAL";
  static readonly Regex Block=new Regex(@"(?ms)^"+Regex.Escape(Begin)+@"\r?\n(.*?)^"+Regex.Escape(End)+@"(?:\r?\n|$)");
  public static string Transform(string original,GuardState state,double now,bool restoreOnly) {
   var blocks=Block.Matches(original);
   if(Regex.Matches(original,@"(?m)^"+Regex.Escape(Begin)+@"\r?$").Count!=blocks.Count||Regex.Matches(original,@"(?m)^"+Regex.Escape(End)+@"\r?$").Count!=blocks.Count||blocks.Count>1)throw new Exception("Bloco hosts incompleto; restauração pendente.");
   string newline=original.Contains("\r\n")?"\r\n":"\n";
   var active=state.pauses.Where(p=>!restoreOnly&&!p.cancelled&&now>=p.startsAt&&now<p.endsAt).ToArray();
   Func<string,bool> paused=d=>active.Any(p=>p.domains.Any(root=>Engine.Match(d,root)));
   var lines=blocks.Count==0?new List<string>():Regex.Split(blocks[0].Groups[1].Value,@"\r?\n").ToList();
   if(lines.Count>0&&lines[lines.Count-1]=="")lines.RemoveAt(lines.Count-1);
   foreach(string line in lines.ToArray()) {
    var match=Regex.Match(line,@"^(?:0\.0\.0\.0|::)\s+([a-zA-Z0-9.-]+)\s*$");
    if(match.Success&&paused(match.Groups[1].Value.ToLowerInvariant())) {
     if(!state.held.Any(h=>h.line==line))state.held.Add(new HeldLine{line=line,domain=match.Groups[1].Value.ToLowerInvariant()});
     lines.Remove(line);
    }
   }
   foreach(var held in state.held.Where(h=>!paused(h.domain)).ToArray())if(!lines.Any(l=>l.Trim()==held.line.Trim()))lines.Add(held.line);
   // Keep held records until the caller has successfully written hosts.
   if(blocks.Count==0&&lines.Count==0)return original;
   string replacement=Begin+newline+String.Join(newline,lines)+ (lines.Count>0?newline:"")+End+newline;
   if(blocks.Count==1)return original.Substring(0,blocks[0].Index)+replacement+original.Substring(blocks[0].Index+blocks[0].Length);
   return original+(original.Length>0&&!original.EndsWith("\n")?newline:"")+replacement;
  }
  public static void Finish(GuardState state,double now,bool restoreOnly) {
   state.held.RemoveAll(h=>restoreOnly||!state.pauses.Any(p=>!p.cancelled&&now>=p.startsAt&&now<p.endsAt&&p.domains.Any(d=>Engine.Match(h.domain,d))));
  }
  public static Pause Request(GuardState state,GuardRequest request,DateTime utc) {
   Guid id;if(!Guid.TryParseExact(request.id,"N",out id))throw new Exception("Identificador inválido.");
   var existing=state.pauses.FirstOrDefault(p=>p.id==request.id);if(existing!=null)return existing;
   if(request.domains==null||request.domains.Length<1||request.domains.Length>32)throw new Exception("Domínios inválidos.");
   var r=new Rule{domains=Engine.PauseDomains(request.domains)};
   var local=new State{pauses=state.pauses};var pause=Engine.RequestPause(local,r,utc);pause.id=request.id;return pause;
  }
 }
 public static class GuardWire {
  public const string Pipe="FocoLocalGuard_v1";
  public static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=65536};
  public static byte[] Read(Stream stream,int count) {var bytes=new byte[count];int offset=0;while(offset<count){var async=stream.BeginRead(bytes,offset,count-offset,null,null);if(!async.AsyncWaitHandle.WaitOne(4000)){stream.Dispose();throw new IOException("O serviço não respondeu a tempo.");}int n=stream.EndRead(async);async.AsyncWaitHandle.Close();if(n==0)throw new IOException("Conexão encerrada.");offset+=n;}return bytes;}
  public static T Receive<T>(Stream stream){int length=BitConverter.ToInt32(Read(stream,4),0);if(length<2||length>65536)throw new IOException("Mensagem inválida.");return Json.Deserialize<T>(Encoding.UTF8.GetString(Read(stream,length)));}
  public static void Send(Stream stream,object value){var data=Encoding.UTF8.GetBytes(Json.Serialize(value));stream.Write(BitConverter.GetBytes(data.Length),0,4);stream.Write(data,0,data.Length);stream.Flush();}
  public static GuardReply Call(GuardRequest request){using(var pipe=new System.IO.Pipes.NamedPipeClientStream(".",Pipe,System.IO.Pipes.PipeDirection.InOut,System.IO.Pipes.PipeOptions.Asynchronous)){pipe.Connect(2500);Send(pipe,request);var result=Receive<GuardReply>(pipe);if(!result.ok)throw new Exception(result.error);return result;}}
 }
}
