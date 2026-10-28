using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace FocoLocal {
 public class Rule {
  public string id = Guid.NewGuid().ToString("N");
  public string name = "";
  public string[] domains = new string[0];
  public bool always = true;
  public bool blockWindow = false;
  public bool windowQuota = false;
  public bool archived = false;
  public double focusUntil = 0;
  public double pauseSuppressedUntil = 0;
  public int minutes = 30, start = 0, end = 0, days = 127;
 }
 public class State {
  public int version = 7;
  public List<PendingChange> pendingChanges = new List<PendingChange>();
  public List<Pause> pauses = new List<Pause>();
  public List<Rule> rules = new List<Rule>();
  public string day = "";
  public Dictionary<string,double> usage = new Dictionary<string,double>();
  public Dictionary<string,double> windowUsage = new Dictionary<string,double>();
  public Dictionary<string,double> seen = new Dictionary<string,double>();
  public Dictionary<string,bool> ready = new Dictionary<string,bool>();
  public double chargedUntil = 0;
  public string streakStart = "", streakTracking = "";
  public int streakBest = 0;
  public List<string> streakLapses = new List<string>();
  public Dictionary<string,int> streakDayKinds = new Dictionary<string,int>();
 }
 public class Row {
  public string id, name, reason;
  public string[] domains;
  public bool always, blocked, blockWindow, windowQuota, archived;
  public string pauseStatus;
  public string changeStatus;
  public int minutes, start, end, days;
  public double usedSeconds;
  public double windowUsedSeconds;
  public double focusUntil;
  public double pauseRequestedAt, pauseStartsAt, pauseEndsAt;
 }
 public class Snapshot {
  public bool ok = true;
  public string day, error;
  public List<Row> rules;
  public List<Row> archivedRules;
  public Dictionary<string,double> seen;
  public Dictionary<string,bool> ready;
  public int streakDays, streakBest;
  public string streakTracking;
  public List<string> streakLapses;
  public Dictionary<string,int> streakDayKinds;
 }
 public class Sample { public string type, domain, browser; public bool active, ready; }
 public class Pause {
  public string id = Guid.NewGuid().ToString("N");
  public string[] domains = new string[0];
  public double requestedAt, startsAt, endsAt, nextAt;
  public bool cancelled;
  public bool windowsManaged;
  public bool confirmationPending;
  public bool cancelRequested;
 }
 public static class Engine {
  public static readonly DateTime Epoch = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
  public static double Stamp(DateTime utc) { return (utc.ToUniversalTime()-Epoch).TotalSeconds; }
  public static string Normalize(string input) {
   string value = (input ?? "").Trim();
   Uri uri;
   if (!value.Contains("://")) value = "https://"+value;
   if (!Uri.TryCreate(value,UriKind.Absolute,out uri) || (uri.Scheme!="https" && uri.Scheme!="http") || uri.UserInfo!="" || uri.AbsolutePath!="/" || uri.Query!="" || uri.Fragment!="" || !uri.IsDefaultPort) throw new Exception("Use um domínio sem caminho, por exemplo: youtube.com");
   string host = new IdnMapping().GetAscii(uri.DnsSafeHost.TrimEnd('.')).ToLowerInvariant();
   if (host.Length>253 || !Regex.IsMatch(host,@"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z](?:[a-z0-9-]{0,61}[a-z0-9])?$")) throw new Exception("Domínio inválido: "+input);
   return host;
  }
  public static bool Match(string host, string domain) { return host==domain || host.EndsWith("."+domain,StringComparison.Ordinal); }
  public static string[] PauseDomains(string[] domains) {
   var result=domains.Select(Normalize).ToList();
   if(result.Any(d=>Match(d,"x.com")||Match(d,"twitter.com"))) {result.Add("x.com");result.Add("twitter.com");}
   return result.Distinct().ToArray();
  }
  public static bool Overlap(string[] a,string[] b) { return a.Any(x=>b.Any(y=>Match(x,y)||Match(y,x))); }
  // Pause the panel can count down: confirmed, not cancelled, not overridden by study or a tightened rule.
  public static Pause LivePause(State s,Rule r,DateTime utc) {double now=Stamp(utc);var p=LatestPause(s,r);return r.always&&!r.archived&&r.focusUntil<=now&&now>=r.pauseSuppressedUntil&&p!=null&&!p.cancelled&&!p.confirmationPending&&!p.cancelRequested&&now<p.endsAt?p:null;}
  public static Pause LatestPause(State s,Rule r) {return s.pauses.Where(p=>Overlap(p.domains,PauseDomains(r.domains))).OrderByDescending(p=>p.nextAt).FirstOrDefault();}
  public static bool Paused(State s,Rule r,DateTime utc) {double now=Stamp(utc);var p=LatestPause(s,r);return !r.archived&&r.always&&now>=r.pauseSuppressedUntil&&p!=null&&!p.confirmationPending&&!p.cancelRequested&&!p.cancelled&&now>=p.startsAt&&now<p.endsAt;}
  public static Pause RequestPause(State s,Rule r,DateTime utc) {
   if(r==null||r.archived||!r.always)throw new Exception("A pausa é para regras ativas de bloqueio permanente.");
   if(r.focusUntil>Stamp(utc))throw new Exception("Aguarde o fim da sessão de estudo antes de pedir uma pausa.");
   var last=LatestPause(s,r);if(last!=null&&Stamp(utc)<last.nextAt)throw new Exception("Novo pedido disponível em "+Epoch.AddSeconds(last.nextAt).ToLocalTime().ToString("dd/MM HH:mm")+".");
   s.pauses.RemoveAll(expired=>expired.nextAt<=Stamp(utc));
   var p=new Pause {domains=PauseDomains(r.domains),requestedAt=Stamp(utc),startsAt=Stamp(utc.AddMinutes(30)),endsAt=Stamp(utc.AddMinutes(40)),nextAt=Stamp(utc.AddMinutes(30).AddHours(24))};s.pauses.Add(p);return p;
  }
  public static string PauseStatus(State s,Rule r,DateTime utc) {
   var p=LatestPause(s,r);double now=Stamp(utc);if(p!=null&&p.cancelRequested&&!p.cancelled)return "Cancelamento aguardando confirmação do Windows.";if(p!=null&&p.confirmationPending&&!p.cancelled)return "Pedido aguardando confirmação. Abra a pausa para verificar.";if(p==null||now>=p.nextAt)return "Pausa disponível: espera de 30 min, acesso de 10 min.";
   if(!p.cancelled&&now<p.startsAt)return "Pedido em espera · libera "+Epoch.AddSeconds(p.startsAt).ToLocalTime().ToString("dd/MM HH:mm:ss");
   if(!p.cancelled&&now<p.endsAt){if(r.focusUntil>now)return "Sessão de estudo ativa · pausa não libera a extensão.";if(r.pauseSuppressedUntil>now)return "Regra reforçada · pausa não libera a extensão.";return "Pausa até "+Epoch.AddSeconds(p.endsAt).ToLocalTime().ToString("HH:mm:ss");}
   return (p.cancelled?"Pedido cancelado. ":"")+"Novo pedido: "+Epoch.AddSeconds(p.nextAt).ToLocalTime().ToString("dd/MM HH:mm");
  }
  public static void Validate(Rule rule) {
   rule.name = (rule.name ?? "").Trim();
   if (rule.name.Length<1 || rule.name.Length>80) throw new Exception("Dê um nome de até 80 caracteres à regra.");
   rule.domains = (rule.domains ?? new string[0]).Select(Normalize).Distinct().ToArray();
   if (rule.domains.Length<1 || rule.domains.Length>30) throw new Exception("Informe de 1 a 30 domínios.");
   if (rule.minutes<0 || rule.minutes>1440 || rule.start<0 || rule.start>1439 || rule.end<0 || rule.end>1439 || rule.days<1 || rule.days>127) throw new Exception("Revise os minutos, os horários e os dias permitidos.");
   if(rule.windowQuota && (rule.always || rule.blockWindow || rule.minutes==0)) throw new Exception("A cota na faixa precisa ter pelo menos 1 minuto e não pode ser combinada com outro modo.");
  }
  public static bool InWindow(Rule r,DateTime local) {
   int t = local.Hour*60+local.Minute, d = (int)local.DayOfWeek;
   if (r.start==r.end) return (r.days & (1<<d))!=0;
   if (r.start<r.end) return (r.days & (1<<d))!=0 && t>=r.start && t<r.end;
   return (t>=r.start && (r.days & (1<<d))!=0) || (t<r.end && (r.days & (1<<((d+6)%7)))!=0);
  }
  public static void Rollover(State s, DateTime local) {
   string day = local.ToString("yyyy-MM-dd");
   // Moving the clock backwards must not replenish a quota already consumed.
   if (String.CompareOrdinal(day,s.day)>0) { s.day=day; s.usage.Clear(); }
   string cutoff=local.Date.AddDays(-7).ToString("yyyy-MM-dd");
   foreach(string key in s.windowUsage.Keys.Where(k=>k.Length>=10 && String.CompareOrdinal(k.Substring(k.Length-10),cutoff)<0).ToArray()) s.windowUsage.Remove(key);
  }
  public static string WindowKey(Rule r,DateTime date) {return r.id+"|"+date.ToString("yyyy-MM-dd");}
  public static double WindowUsed(Rule r,State s,DateTime local) {
   DateTime date=local.Date;
   if(r.start>r.end && local.Hour*60+local.Minute<r.start) date=date.AddDays(-1);
   double used;s.windowUsage.TryGetValue(WindowKey(r,date),out used);return used;
  }
  public static string Reason(Rule r,State s,DateTime local) {
   if (r.archived) return "Arquivada";
   if (r.always && r.focusUntil<=Stamp(local.ToUniversalTime()) && Paused(s,r,local.ToUniversalTime())) return "Permitido agora";
   if (r.always) return "Bloqueio permanente";
   if (r.focusUntil>Stamp(local.ToUniversalTime())) return "Sessão de foco até "+Epoch.AddSeconds(r.focusUntil).ToLocalTime().ToString("dd/MM HH:mm");
   if(r.windowQuota) {
    if(InWindow(r,local) && WindowUsed(r,s,local)>=r.minutes*60) return "Cota da faixa esgotada";
    return "Permitido agora";
   }
   if (r.blockWindow && InWindow(r,local)) return "Horário de bloqueio";
   if (!r.blockWindow && !InWindow(r,local)) return "Fora do horário permitido";
   double used; s.usage.TryGetValue(r.id,out used);
   if (r.minutes>0 && used>=r.minutes*60) return "Limite diário atingido";
   return "Permitido agora";
  }
  public static void StartFocus(State s,string[] ids,int minutes,DateTime utc) {
   if(minutes<1 || minutes>1440) throw new Exception("Escolha de 1 a 1440 minutos.");
   if(ids==null || ids.Length==0 || ids.Any(id=>!s.rules.Any(r=>r.id==id&&!r.archived))) throw new Exception("Selecione pelo menos um site ativo da lista.");
   double until=Stamp(utc.AddMinutes(minutes));
   foreach(var r in s.rules.Where(r=>ids.Contains(r.id))) r.focusUntil=Math.Max(r.focusUntil,until);
   s.pendingChanges.RemoveAll(p=>ids.Contains(p.ruleId));
  }
  public static void Charge(State s,string host,double from,double to,DateTime utc) {
   DateTime local = utc.ToLocalTime(); Rollover(s,local);
   if (String.IsNullOrEmpty(host) || to<=from || to-from>4) return;
   // Only the current local day and non-overlapping foreground intervals count.
   double midnight = Stamp(local.Date.ToUniversalTime());
   double uniqueFrom=Math.Max(from,s.chargedUntil);
   double seconds = Math.Max(0,to-Math.Max(uniqueFrom,midnight));
   if(to<=uniqueFrom) return;
   var matches=s.rules.Where(r=>!r.archived&&r.domains.Any(d=>Match(host,d))).ToArray();
   foreach(var r in matches) {
    if(r.windowQuota && !r.always && r.focusUntil<=from) {
     // Clip to actual daily windows so a sample crossing a boundary never
     // charges the free time outside it. Overnight windows keep one quota.
     DateTime first=Epoch.AddSeconds(uniqueFrom).ToLocalTime().Date.AddDays(-1);
     for(DateTime date=first;date<=local.Date;date=date.AddDays(1)) {
      if((r.days & (1<<(int)date.DayOfWeek))==0) continue;
      DateTime begin=r.start==r.end?date:date.AddMinutes(r.start);
      DateTime finish=r.start==r.end?date.AddDays(1):date.AddMinutes(r.end).AddDays(r.start>r.end?1:0);
      double overlap=Math.Min(to,Stamp(finish.ToUniversalTime()))-Math.Max(uniqueFrom,Stamp(begin.ToUniversalTime()));
      if(overlap<=0) continue;
      string key=WindowKey(r,date);double usedInWindow;s.windowUsage.TryGetValue(key,out usedInWindow);
      s.windowUsage[key]=Math.Max(usedInWindow,Math.Min(r.minutes*60,usedInWindow+overlap));
     }
     double daily;s.usage.TryGetValue(r.id,out daily);s.usage[r.id]=daily+seconds;
     continue;
    }
    if (Reason(r,s,local)!="Permitido agora") continue;
    double used; s.usage.TryGetValue(r.id,out used);
    s.usage[r.id] = r.minutes>0 ? Math.Min(r.minutes*60,used+seconds) : used+seconds;
    if (r.always && seconds>0) Streak.Record(s,local,true);
   }
   s.chargedUntil = Math.Max(s.chargedUntil,to);
  }
  public static Snapshot View(State s,DateTime utc) {
   DateTime local=utc.ToLocalTime(); Rollover(s,local);
   Func<Rule,Row> row=r=>{var live=LivePause(s,r,utc);var item=new Row {id=r.id,name=r.name,domains=r.domains,archived=r.archived,changeStatus=s.pendingChanges.Any(p=>p.ruleId==r.id)?Changes.Describe(s.pendingChanges.First(p=>p.ruleId==r.id)):"",pauseStatus=PauseStatus(s,r,utc),always=r.always,blockWindow=r.blockWindow,windowQuota=r.windowQuota,windowUsedSeconds=WindowUsed(r,s,local),focusUntil=r.focusUntil,minutes=r.minutes,start=r.start,end=r.end,days=r.days,usedSeconds=s.usage.ContainsKey(r.id)?s.usage[r.id]:0,reason=Reason(r,s,local),blocked=!r.archived&&Reason(r,s,local)!="Permitido agora"};if(live!=null){item.pauseRequestedAt=live.requestedAt;item.pauseStartsAt=live.startsAt;item.pauseEndsAt=live.endsAt;}return item;};
   Streak.Ensure(s,local);
   return new Snapshot {day=s.day, seen=s.seen,ready=s.ready,streakDays=Streak.Current(s,local),streakBest=Streak.Best(s,local),streakTracking=s.streakTracking,streakLapses=s.streakLapses.ToList(),streakDayKinds=new Dictionary<string,int>(s.streakDayKinds),rules=s.rules.Where(r=>!r.archived).Select(row).ToList(),archivedRules=s.rules.Where(r=>r.archived).Select(row).ToList()};
  }
 }
 public static class Streak {
  public const int NoUse=0,WithinPause=1,OutsidePause=2,LegacyUnknown=3,Untracked=-1;
  // Clean days since the last use of a permanently blocked rule. Display only: it never changes access.
  static bool Valid(string day,out DateTime date){return DateTime.TryParseExact(day??"","yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date);}
  public static bool Ensure(State s,DateTime local){bool changed=false;DateTime parsed;string today=local.ToString("yyyy-MM-dd");
   if(s.streakLapses==null){s.streakLapses=new List<string>();changed=true;}
   if(s.streakDayKinds==null){s.streakDayKinds=new Dictionary<string,int>();changed=true;}
   foreach(string day in s.streakLapses)if(!s.streakDayKinds.ContainsKey(day)){s.streakDayKinds[day]=LegacyUnknown;changed=true;}
   if(!Valid(s.streakTracking,out parsed)){s.streakTracking=today;changed=true;}
   if(!Valid(s.streakStart,out parsed)){s.streakStart=today;changed=true;}
   return changed;}
  public static int Current(State s,DateTime local){Ensure(s,local);DateTime start;Valid(s.streakStart,out start);int days=(local.Date-start).Days;return days<0?0:days+1;}
  public static int Best(State s,DateTime local){return Math.Max(s.streakBest,Current(s,local));}
  public static int DayKind(string tracking,System.Collections.Generic.IDictionary<string,int> kinds,DateTime day){int kind;if(kinds!=null&&kinds.TryGetValue(day.ToString("yyyy-MM-dd"),out kind))return kind==WithinPause||kind==OutsidePause?kind:LegacyUnknown;return String.IsNullOrEmpty(tracking)||String.CompareOrdinal(day.ToString("yyyy-MM-dd"),tracking)<0?Untracked:NoUse;}
  public static string KindLabel(int kind){return kind==NoUse?"Sem uso registrado":kind==WithinPause?"Uso dentro da pausa de 10 min":kind==OutsidePause?"Uso fora da regra de 10 min":kind==LegacyUnknown?"Uso antigo sem classificação":"Sem acompanhamento";}
  public static void Lapse(State s,DateTime local){Record(s,local,false);}
  public static void Record(State s,DateTime local,bool withinPause,bool manual=false){Ensure(s,local);DateTime start;Valid(s.streakStart,out start);
   // s.day only moves forward, so setting the clock back cannot dodge a lapse.
   DateTime effective=local.Date,known;if(Valid(s.day,out known)&&known>effective)effective=known;string today=effective.ToString("yyyy-MM-dd");
   int previous;s.streakDayKinds.TryGetValue(today,out previous);
   // Red wins over gray. Old unclassified use cannot be inferred from a later automatic sample.
   s.streakDayKinds[today]=!withinPause||previous==OutsidePause?OutsidePause:previous==LegacyUnknown&&!manual?LegacyUnknown:WithinPause;
   if(!s.streakLapses.Contains(today))s.streakLapses.Add(today);if(s.streakLapses.Count>400)s.streakLapses.RemoveRange(0,s.streakLapses.Count-400);
   foreach(string expired in s.streakDayKinds.Keys.Where(k=>!s.streakLapses.Contains(k)).ToArray())s.streakDayKinds.Remove(expired);
   // Today is not clean, so the finished streak ends yesterday. A lapse never moves the start backwards.
   s.streakBest=Math.Max(s.streakBest,(effective-start).Days);string next=effective.AddDays(1).ToString("yyyy-MM-dd");if(String.CompareOrdinal(next,s.streakStart)>0)s.streakStart=next;}
 }
 public class Store {
  public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
  public static readonly string FilePath=Path.Combine(Root,"dados.json");
  static readonly Mutex Gate=new Mutex(false,@"Local\FocoLocalPersonal_v1");
  public static readonly JavaScriptSerializer Json = new JavaScriptSerializer {MaxJsonLength=1024*1024};
  public static T With<T>(Func<State,T> action, bool save) {
   bool held=false;
   try {
    try { held=Gate.WaitOne(5000); } catch(AbandonedMutexException) { held=true; }
    if(!held) throw new Exception("O painel está ocupado. Tente novamente.");
    State s;
    if(File.Exists(FilePath)) {
     s=Json.Deserialize<State>(File.ReadAllText(FilePath,Encoding.UTF8));
     if(s==null || s.version<1 || s.version>7 || s.rules==null || s.pendingChanges==null || s.pauses==null || s.usage==null || s.windowUsage==null || s.seen==null || s.ready==null) throw new Exception("Dados inválidos ou de outra versão. O backup foi preservado em dados.anterior.json.");
    } else { s=new State(); }
    bool migrated=s.version<7;s.version=7;
    bool due=Changes.ApplyDue(s,DateTime.UtcNow);
    due|=Streak.Ensure(s,DateTime.Now);
    T result=action(s);
    if(save||due||migrated) {
     string temp=Path.Combine(Root,"dados."+Guid.NewGuid().ToString("N")+".tmp");
     File.WriteAllText(temp,Json.Serialize(s),new UTF8Encoding(false));
     try { if(File.Exists(FilePath)) File.Replace(temp,FilePath,Path.Combine(Root,"dados.anterior.json")); else File.Move(temp,FilePath); }
     finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    return result;
   } finally { if(held) Gate.ReleaseMutex(); }
  }
 }
}
