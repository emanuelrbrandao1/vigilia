using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FocoLocal {
 public class PauseDialog:Form {
  readonly string ruleId;Label status,service;Button request,cancel,install;Timer timer;bool connected,probing,busy;int ticks;
  static readonly string[][] quotes={
   new[]{"Nada te perturbe / Nada te espante","Santa Teresa de Jesus · Catecismo, §227","https://www.vatican.va/archive/cathechism_po/index_new/p1s2c1_198-421_po.html"},
   new[]{"Tarde te amei, ó Beleza tão antiga e tão nova, tarde te amei!","Santo Agostinho · Confissões X, 27; Angelus de 27/08/2006","https://www.vatican.va/content/benedict-xvi/pt/angelus/2006/documents/hf_ben-xvi_ang_20060827.html"}
  };
  public PauseDialog(string id,bool preview=false){
   ruleId=id;var rule=Store.With(s=>s.rules.FirstOrDefault(r=>r.id==id),false);if(rule==null)throw new Exception("Selecione uma regra.");
   Text="Vigília | Pausa temporária";ClientSize=new Size(720,640);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;Theme.Prepare(this);
   var eyebrow=Theme.Label("Pausa temporária",26,22,668,24,10,false);eyebrow.ForeColor=Theme.Muted;Controls.Add(eyebrow);Controls.Add(Theme.Label(rule.name,26,58,668,46,25,false));
   string[] values={"30 min","10 min","24 h"},labels={"Espera","Acesso","Entre liberações"};
   for(int i=0;i<3;i++){var card=new Card{Location=new Point(26+i*226,126),Size=new Size(216,88)};card.Controls.Add(Theme.Label(values[i],16,12,184,34,22,false));var label=Theme.Label(labels[i],16,55,184,20,10,false);label.ForeColor=Theme.Muted;card.Controls.Add(label);Controls.Add(card);}
   status=Theme.Label("",26,238,668,64,12,true);Controls.Add(status);
   service=Theme.Label("Verificando o serviço do Windows…",26,310,668,24,9,false);service.ForeColor=Theme.Muted;Controls.Add(service);
   var quote=quotes[new Random().Next(quotes.Length)];var reflection=new Card{Location=new Point(26,354),Size=new Size(668,114)};reflection.Controls.Add(Theme.Label("“"+quote[0]+"”",18,14,632,48,13,false));var source=new LinkLabel{Location=new Point(18,73),Size=new Size(632,26),Text=quote[1],LinkColor=Theme.Ink,Font=new Font("Segoe UI",9)};source.LinkClicked+=(s,e)=>Open(quote[2]);reflection.Controls.Add(source);Controls.Add(reflection);
   var help=new LinkLabel{Location=new Point(26,486),Size=new Size(300,25),Text="Refletir sobre este pedido",LinkColor=Theme.Ink};Controls.Add(help);help.LinkClicked+=(s,e)=>{using(var dialog=new ReflectionDialog(rule))dialog.ShowDialog(this);};
   install=Theme.Button("Ativar serviço do Windows",358,480,336,false);Controls.Add(install);install.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),Arguments="-NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(Store.Root,"Instalar-Guard.ps1")+"\"",UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});service.Text="Confirme o aviso de administrador. A conexão será verificada aqui.";}catch(Exception ex){MessageBox.Show(this,ex.Message,"Instalação não concluída");}};
   var note=Theme.Label("A pausa começa automaticamente após a espera, mesmo com o painel fechado.\nCancelar não reinicia o intervalo de 24 horas. Outros sites têm prazos independentes.",26,533,668,43,9,false);note.ForeColor=Theme.Muted;Controls.Add(note);
   cancel=Theme.Button("Cancelar pausa",26,586,240,false);request=Theme.Button("Solicitar pausa",282,586,412,true);Controls.Add(cancel);Controls.Add(request);request.Click+=(s,e)=>Request();cancel.Click+=(s,e)=>Cancel();
   timer=new Timer{Interval=1000};timer.Tick+=(s,e)=>{RefreshState();if(!preview&&++ticks%10==0)Probe();};FormClosed+=(s,e)=>timer.Dispose();RefreshState();timer.Start();if(!preview)Probe();else service.Text="Prévia · nenhuma pausa solicitada.";
  }
  internal static string Countdown(double seconds){var span=TimeSpan.FromSeconds(Math.Max(0,Math.Ceiling(seconds)));return span.TotalHours>=1?((int)span.TotalHours)+"h "+span.Minutes.ToString("00")+"m":span.Minutes.ToString("00")+":"+span.Seconds.ToString("00");}
  void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,ex.Message);}}
  async void Probe(){if(probing||busy||IsDisposed)return;probing=true;try{
   var reply=await Task.Run(()=>GuardWire.Call(new GuardRequest{command="status"}));if(IsDisposed)return;
   Store.With(s=>{PauseRecovery.Merge(s,reply.pauses);return true;},reply.pauses!=null&&reply.pauses.Count>0);
   var cancelled=Store.With(s=>s.pauses.Where(p=>p.cancelRequested&&!p.cancelled).Select(p=>p.id).ToArray(),false);
   foreach(var id in cancelled){await Task.Run(()=>GuardWire.Call(new GuardRequest{command="cancel",id=id}));if(IsDisposed)return;Store.With(s=>{var p=s.pauses.FirstOrDefault(x=>x.id==id);if(p!=null){p.cancelled=true;p.cancelRequested=false;p.confirmationPending=false;}return true;},true);}
   connected=true;service.Text="Windows conectado · restauração automática disponível.";
  }catch{if(!IsDisposed){connected=false;service.Text="Serviço indisponível. Ative-o para solicitar uma pausa.";}}finally{probing=false;if(!IsDisposed)RefreshState();}}
  void RefreshState(){try{Store.With(s=>{var r=s.rules.FirstOrDefault(x=>x.id==ruleId);if(r==null){request.Enabled=cancel.Enabled=false;status.Text="Regra removida.";return false;}
   var p=Engine.LatestPause(s,r);double now=Engine.Stamp(DateTime.UtcNow);bool retry=p!=null&&p.confirmationPending&&!p.cancelled&&!p.cancelRequested;
   request.Text=retry?"Confirmar pedido salvo":"Solicitar pausa";
   request.Enabled=!busy&&connected&&!r.archived&&r.always&&(retry||p==null||now>=p.nextAt)&&r.focusUntil<=now;
   cancel.Enabled=!busy&&p!=null&&!p.cancelled&&(p.confirmationPending||p.cancelRequested||now<p.endsAt);install.Visible=!connected;
   status.Text=r.archived?"Reative esta regra para solicitar uma pausa.":!r.always?"A pausa é exclusiva para regras de bloqueio permanente.":r.focusUntil>now?"Sessão de estudo ativa. Aguarde seu término.":Engine.PauseStatus(s,r,DateTime.UtcNow);
   if(r.always&&!r.archived&&r.focusUntil<=now&&now>=r.pauseSuppressedUntil&&p!=null&&!p.cancelled&&!p.confirmationPending&&!p.cancelRequested&&now<p.endsAt)status.Text=(now<p.startsAt?"Espera · faltam "+Countdown(p.startsAt-now):"Acesso liberado · restam "+Countdown(p.endsAt-now))+"\n"+Engine.PauseStatus(s,r,DateTime.UtcNow);
   return true;},false);}catch(Exception ex){status.Text=ex.Message;request.Enabled=false;}}
  async void Request(){if(busy)return;busy=true;bool prepared=false;RefreshState();try{
   var local=Store.With(s=>PauseRecovery.Prepare(s,s.rules.First(r=>r.id==ruleId),DateTime.UtcNow),true);
   prepared=true;var reply=await Task.Run(()=>GuardWire.Call(new GuardRequest{command="request",id=local.id,domains=local.domains}));
   Store.With(s=>{PauseRecovery.Merge(s,new[]{reply.pause});return true;},true);
  }catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,(prepared?"Seu pedido ficou salvo para conferência. Nenhum pedido duplicado será criado.\n":"Pedido não enviado.\n")+ex.Message,prepared?"Confirmação pendente":"Pedido não enviado");}finally{busy=false;if(!IsDisposed){RefreshState();Probe();}}}
  async void Cancel(){if(busy)return;if(MessageBox.Show(this,"Cancelar o pedido ou encerrar a pausa agora? O intervalo para um novo pedido continua valendo.","Retomar bloqueio",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;busy=true;RefreshState();try{
   string id=Store.With(s=>{var r=s.rules.First(x=>x.id==ruleId);var p=Engine.LatestPause(s,r);if(p==null)throw new Exception("Pedido não encontrado.");p.cancelRequested=true;return p.id;},true);
   await Task.Run(()=>GuardWire.Call(new GuardRequest{command="cancel",id=id}));
   Store.With(s=>{var p=s.pauses.First(x=>x.id==id);p.cancelled=true;p.cancelRequested=false;p.confirmationPending=false;return true;},true);
  }catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,"O cancelamento será confirmado ao reconectar. O Windows ainda pode ter uma pausa em andamento.\n"+ex.Message,"Cancelamento pendente");}finally{busy=false;if(!IsDisposed){RefreshState();Probe();}}}
 }
 public class ReflectionDialog:Form {
  public ReflectionDialog(Rule rule){Text="Vigília | Reflexão";ClientSize=new Size(650,450);StartPosition=FormStartPosition.CenterParent;Theme.Prepare(this);
   Controls.Add(Theme.Label("Qual é a intenção deste acesso?",26,24,598,46,21,false));
   var intro=Theme.Label("Uma necessidade concreta, uma pausa consciente ou um impulso?\nEscreva para organizar a decisão. Este texto não é salvo pelo aplicativo.",26,86,598,58,11,false);intro.ForeColor=Theme.Muted;Controls.Add(intro);
   var text=new TextBox{Location=new Point(26,157),Size=new Size(598,162),Multiline=true,ScrollBars=ScrollBars.Vertical,AccessibleName="Sua reflexão"};Controls.Add(text);
   var chat=Theme.Button("Copiar e conversar no ChatGPT",26,342,344,false);Controls.Add(chat);chat.Click+=(s,e)=>{try{Clipboard.SetText("Quero refletir sobre uma pausa de 10 minutos em "+rule.name+". Meu motivo: "+text.Text+"\nAjude-me com perguntas curtas e alternativas práticas. Se trouxer orientação católica, use fontes verificáveis, sem assumir o papel de confessor. A conversa não controla nem autoriza o bloqueador.");Process.Start(new ProcessStartInfo("https://chatgpt.com/"){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,ex.Message);}};
   var close=Theme.Button("Concluir reflexão",386,342,238,true);Controls.Add(close);close.Click+=(s,e)=>Close();
   var note=Theme.Label("A conversa é opcional. Nada é enviado automaticamente.",26,403,598,25,9,false);note.ForeColor=Theme.Muted;Controls.Add(note);
  }
 }
}
