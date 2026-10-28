using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FocoLocal {
 public class ChangesDialog:Form {
  ListBox list;TextBox detail;PendingChange[] items;Button cancel;
  public ChangesDialog(){Text="Alterações pendentes";ClientSize=new Size(720,600);StartPosition=FormStartPosition.CenterParent;Theme.Prepare(this);
   Controls.Add(Theme.Label("Mais liberdade, depois de 24 horas",24,20,672,38,20,true));
   Controls.Add(Theme.Label("A regra atual continua valendo durante a espera.\nRestringir agora cancela o pedido anterior para a mesma regra.",24,70,672,55,11,false));
   list=new ListBox{Location=new Point(24,135),Size=new Size(672,145),HorizontalScrollbar=true};Controls.Add(list);
   detail=new TextBox{Location=new Point(24,298),Size=new Size(672,213),Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};Controls.Add(detail);
   cancel=Theme.Button("Cancelar pedido selecionado",24,535,332,false);Controls.Add(cancel);cancel.Click+=(s,e)=>{if(list.SelectedIndex<0)return;var selected=items[list.SelectedIndex];try{Store.With(state=>{state.pendingChanges.RemoveAll(p=>p.ruleId==selected.ruleId&&p.requestedAt==selected.requestedAt);return true;},true);Reload();}catch(Exception ex){MessageBox.Show(this,ex.Message);}};
   var close=Theme.Button("Fechar",536,535,160,true);Controls.Add(close);close.Click+=(s,e)=>Close();
   list.SelectedIndexChanged+=(s,e)=>Describe();Reload();
  }
  static string RuleText(Rule r){if(r==null)return "";string[] labels={"Dom","Seg","Ter","Qua","Qui","Sex","Sáb"};string days=String.Join(", ",Enumerable.Range(0,7).Where(d=>(r.days&(1<<d))!=0).Select(d=>labels[d]));return r.name+"\r\n"+String.Join(", ",r.domains)+"\r\n"+(r.always?"Bloquear sempre":r.windowQuota?"Cota na faixa, livre fora":r.blockWindow?"Bloquear na faixa":"Permitir apenas na faixa")+"\r\n"+Time(r.start)+" até "+Time(r.end)+" · "+days+"\r\n"+r.minutes+" minutos"+(r.minutes==0?" (sem cota diária)":"")+(r.archived?" · arquivada":"");}
  static string Time(int minute){return (minute/60).ToString("00")+":"+(minute%60).ToString("00");}
  void Reload(){Store.With(state=>{items=state.pendingChanges.ToArray();list.Items.Clear();foreach(var p in items){var r=state.rules.FirstOrDefault(x=>x.id==p.ruleId);list.Items.Add((r==null?"Regra removida":r.name)+" · "+Changes.Describe(p));}return true;},false);cancel.Enabled=items.Length>0;if(items.Length>0)list.SelectedIndex=0;else detail.Text="Nenhuma alteração pendente.";}
  void Describe(){if(list.SelectedIndex<0)return;var p=items[list.SelectedIndex];detail.Text=Changes.Describe(p)+"\r\n\r\n"+(p.target==null?"A configuração atual permanece até o prazo indicado.":"Configuração que entrará em vigor:\r\n"+RuleText(p.target))+"\r\n\r\nSe você substituir este pedido por outro diferente, a espera recomeça.";}
 }
}
