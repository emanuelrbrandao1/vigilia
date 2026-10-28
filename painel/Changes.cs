using System;
using System.Collections.Generic;
using System.Linq;

namespace FocoLocal {
 public class PendingChange {
  public string ruleId,kind,error;
  public Rule target;
  public double requestedAt,executeAt;
 }
 public static class Changes {
  const int Week=7*1440;
  sealed class Schedule {
   public bool[] allowed=new bool[Week];
   public int[] bucket=new int[Week];
   public int limit;
   public Schedule(Rule r) {
    limit=r.minutes;
    var sunday=new DateTime(2026,10,25);
    for(int i=0;i<Week;i++) {
     int day=i/1440,minute=i%1440;bool inside=Engine.InWindow(r,sunday.AddMinutes(i));bucket[i]=-1;
     allowed[i]=r.archived||(!r.always&&(r.windowQuota||(r.blockWindow?!inside:inside)));
     if(!allowed[i]||r.archived||r.minutes==0)continue;
     if(!r.windowQuota)bucket[i]=day;
     else if(inside)bucket[i]=r.start>r.end&&minute<r.end?(day+6)%7:day;
    }
   }
  }
  // Compare feasible usage traces, not the meaning of individual form fields.
  // Quota buckets of one rule are disjoint. For each old bucket, the maximum
  // possible new consumption is the sum of each new bucket's capped overlap.
  public static bool Freer(Rule old,Rule next,DateTime utc) {
   if(old.archived)return false;
   var before=new Schedule(old);var after=new Schedule(next);
   // A fully unrestricted rule has no domain protection to lose. Replacing it
   // with a new block (even for a different site) only adds restrictions.
   bool quotaRestricts=Enumerable.Range(0,7).Any(b=>before.bucket.Count(x=>x==b)>before.limit);
   if(!quotaRestricts&&old.focusUntil<=Engine.Stamp(utc)&&before.allowed.All(a=>a))return false;
   if(old.domains.Any(d=>!next.domains.Any(n=>Engine.Match(d,n))))return true;
   if(old.focusUntil>Engine.Stamp(utc)&&(next.archived||next.focusUntil<old.focusUntil))return true;
   for(int i=0;i<Week;i++)if(after.allowed[i]&&!before.allowed[i])return true;
   for(int b=0;b<7;b++) {
    int free=0;var overlaps=new int[7];
    for(int i=0;i<Week;i++)if(before.bucket[i]==b&&after.allowed[i]){if(after.bucket[i]<0)free++;else overlaps[after.bucket[i]]++;}
    if(free+overlaps.Sum(n=>Math.Min(n,after.limit))>before.limit)return true;
   }
   return false;
  }
  public static Rule Copy(Rule r){return Store.Json.Deserialize<Rule>(Store.Json.Serialize(r));}
  sealed class Debt {public DateTime begin,end;public double remaining;}
  public static bool NeedsWait(State state,Rule old,Rule next,DateTime utc) {
   if(Freer(old,next,utc))return true;
   if(old.archived||old.always||old.minutes==0)return false;
   // A trace allowed by the new rule can still be a relaxation after a prefix
   // spent under the old rule. Check the remaining old budget periods too.
   var simulated=Store.Json.Deserialize<State>(Store.Json.Serialize(state));var local=utc.ToLocalTime();Engine.Rollover(simulated,local);
   var debts=new List<Debt>();
   if(old.windowQuota) {
    for(var date=local.Date.AddDays(-1);date<=local.Date;date=date.AddDays(1)) {
     if((old.days&(1<<(int)date.DayOfWeek))==0)continue;
     var start=old.start==old.end?date:date.AddMinutes(old.start);
     var finish=old.start==old.end?date.AddDays(1):date.AddMinutes(old.end).AddDays(old.start>old.end?1:0);
     double used;simulated.windowUsage.TryGetValue(Engine.WindowKey(old,date),out used);
     if(used>0&&finish.ToUniversalTime()>utc)debts.Add(new Debt{begin=start.ToUniversalTime(),end=finish.ToUniversalTime(),remaining=Math.Max(0,old.minutes*60-used)});
    }
   }else {double used;simulated.usage.TryGetValue(old.id,out used);if(used>0)debts.Add(new Debt{begin=local.Date.ToUniversalTime(),end=local.Date.AddDays(1).ToUniversalTime(),remaining=Math.Max(0,old.minutes*60-used)});}
   var candidate=Copy(next);Install(simulated,candidate,utc);
   if(old.domains.Any(d=>!next.domains.Any(n=>Engine.Match(d,n))))candidate=new Rule{archived=true};
   foreach(var debt in debts) {
    double free=0;var available=new Dictionary<string,double>();var caps=new Dictionary<string,double>();
    for(var at=debt.begin>utc?debt.begin:utc;at<debt.end;) {
     var end=new DateTime(at.Ticks-at.Ticks%TimeSpan.TicksPerMinute+TimeSpan.TicksPerMinute,DateTimeKind.Utc);if(end>debt.end)end=debt.end;
     var moment=at.ToLocalTime();bool inside=Engine.InWindow(candidate,moment);
     bool allowed=candidate.archived||(!candidate.always&&(candidate.windowQuota||(candidate.blockWindow?!inside:inside)));
     double duration=Math.Max(0,Engine.Stamp(end)-Math.Max(Engine.Stamp(at),candidate.archived?0:candidate.focusUntil));
     if(allowed&&duration>0) {
      string key=null;double used=0;
      if(!candidate.archived&&candidate.minutes>0) {
       if(candidate.windowQuota&&inside) {var date=moment.Date;if(candidate.start>candidate.end&&moment.Hour*60+moment.Minute<candidate.start)date=date.AddDays(-1);key=Engine.WindowKey(candidate,date);simulated.windowUsage.TryGetValue(key,out used);}
       else if(!candidate.windowQuota) {key="day|"+moment.ToString("yyyy-MM-dd");if(moment.ToString("yyyy-MM-dd")==simulated.day)simulated.usage.TryGetValue(candidate.id,out used);}
      }
      if(key==null)free+=duration;
      else {if(!available.ContainsKey(key))available[key]=0;available[key]+=duration;caps[key]=Math.Max(0,candidate.minutes*60-used);}
     }
     at=end;
    }
    if(free+available.Sum(pair=>Math.Min(pair.Value,caps[pair.Key]))>debt.remaining+0.0001)return true;
   }
   return false;
  }
  static void CheckDomains(State s,Rule target) {
   foreach(var other in s.rules.Where(r=>r.id!=target.id).Concat(s.pendingChanges.Where(p=>p.ruleId!=target.id&&p.target!=null).Select(p=>p.target)))
    if(Engine.Overlap(other.domains,target.domains))throw new Exception("Esse domínio já está coberto ou reservado pela regra: "+other.name);
  }
  static void Install(State s,Rule next,DateTime utc) {
   Engine.Rollover(s,utc.ToLocalTime());
   int index=s.rules.FindIndex(r=>r.id==next.id);var previous=index<0?null:s.rules[index];
   // Changing a window must not replenish the current allowance, especially
   // when changing which date owns an overnight window.
   if(previous!=null&&next.windowQuota) {
    double used=previous.windowQuota?Engine.WindowUsed(previous,s,utc.ToLocalTime()):(s.usage.ContainsKey(previous.id)?s.usage[previous.id]:0);
    var local=utc.ToLocalTime();var date=local.Date;
    if(next.start>next.end&&local.Hour*60+local.Minute<next.start)date=date.AddDays(-1);
    string key=Engine.WindowKey(next,date);double stored;s.windowUsage.TryGetValue(key,out stored);s.windowUsage[key]=Math.Max(stored,used);
   }
   if(previous!=null&&previous.windowQuota&&!next.windowQuota&&!next.always&&next.minutes>0){double used;s.usage.TryGetValue(next.id,out used);s.usage[next.id]=Math.Max(used,Engine.WindowUsed(previous,s,utc.ToLocalTime()));}
   if(next.always&&(previous==null||!previous.always||previous.archived||next.domains.Any(d=>!previous.domains.Any(pr=>Engine.Match(d,pr))))) {
    var pause=Engine.LatestPause(s,next);if(pause!=null)next.pauseSuppressedUntil=Math.Max(next.pauseSuppressedUntil,pause.endsAt);
   }
   if(index<0)s.rules.Add(next);else s.rules[index]=next;
  }
  public static PendingChange Save(State s,Rule candidate,DateTime utc) {
   ApplyDue(s,utc);var next=Copy(candidate);Engine.Validate(next);CheckDomains(s,next);
   var old=s.rules.FirstOrDefault(r=>r.id==next.id);
   if(old!=null){next.archived=old.archived;next.focusUntil=old.focusUntil;next.pauseSuppressedUntil=old.pauseSuppressedUntil;}
   if(old!=null&&NeedsWait(s,old,next,utc))return Queue(s,old.id,"edit",next,utc);
   s.pendingChanges.RemoveAll(p=>p.ruleId==next.id);Install(s,next,utc);return null;
  }
  static PendingChange Queue(State s,string id,string kind,Rule target,DateTime utc) {
   var existing=s.pendingChanges.FirstOrDefault(p=>p.ruleId==id);
   if(existing!=null&&existing.error==null&&existing.kind==kind&&Store.Json.Serialize(existing.target)==Store.Json.Serialize(target))return existing;
   s.pendingChanges.RemoveAll(p=>p.ruleId==id);
   var pending=new PendingChange{ruleId=id,kind=kind,target=target==null?null:Copy(target),requestedAt=Engine.Stamp(utc),executeAt=Engine.Stamp(utc.AddHours(24))};s.pendingChanges.Add(pending);return pending;
  }
  public static PendingChange Archive(State s,string id,DateTime utc) {
   ApplyDue(s,utc);var r=s.rules.First(x=>x.id==id);
   if(!r.archived){var next=Copy(r);next.archived=true;if(NeedsWait(s,r,next,utc))return Queue(s,id,"archive",null,utc);r.archived=true;s.pendingChanges.RemoveAll(p=>p.ruleId==id);return null;}
   r.archived=false;s.pendingChanges.RemoveAll(p=>p.ruleId==id);
   var pause=Engine.LatestPause(s,r);if(pause!=null)r.pauseSuppressedUntil=Math.Max(r.pauseSuppressedUntil,pause.endsAt);
   return null;
  }
  public static PendingChange Delete(State s,string id,DateTime utc) {
   ApplyDue(s,utc);var r=s.rules.First(x=>x.id==id);
   if(!r.archived){var next=Copy(r);next.archived=true;if(NeedsWait(s,r,next,utc))return Queue(s,id,"delete",null,utc);}
   s.rules.Remove(r);s.pendingChanges.RemoveAll(p=>p.ruleId==id);return null;
  }
  public static void EndFocus(State s,string[] ids,DateTime utc) {
   ApplyDue(s,utc);foreach(var r in s.rules.Where(r=>ids.Contains(r.id)&&r.focusUntil>Engine.Stamp(utc)))Queue(s,r.id,"endFocus",null,utc);
  }
  public static bool ApplyDue(State s,DateTime utc) {
   bool changed=false;
   foreach(var p in s.pendingChanges.Where(p=>p.error==null&&p.executeAt<=Engine.Stamp(utc)).ToArray()) {
    var old=s.rules.FirstOrDefault(r=>r.id==p.ruleId);changed=true;
    try {
     if(old!=null) {
      if(p.kind=="edit") {var next=Copy(p.target);Engine.Validate(next);CheckDomains(s,next);next.focusUntil=old.focusUntil;next.archived=old.archived;next.pauseSuppressedUntil=old.pauseSuppressedUntil;Install(s,next,utc);}
      else if(p.kind=="delete")s.rules.Remove(old);
      else if(p.kind=="archive")old.archived=true;
      else if(p.kind=="endFocus")old.focusUntil=0;
      else throw new Exception("Tipo de alteração desconhecido.");
     }
     s.pendingChanges.Remove(p);
    }catch(Exception e){p.error=e.Message;}
   }
   return changed;
  }
  public static string Describe(PendingChange p) {
   string title=p.kind=="edit"?"Editar regra":p.kind=="archive"?"Arquivar":p.kind=="delete"?"Excluir":"Encerrar estudo";
   return title+" · "+(p.error==null?Engine.Epoch.AddSeconds(p.executeAt).ToLocalTime().ToString("dd/MM HH:mm:ss"):"não aplicada: "+p.error);
  }
 }
}
