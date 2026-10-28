using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FocoLocal {
 public class FocusDialog:Form {
  CheckedListBox sites; NumericUpDown duration;
  Rule[] rules;
  public FocusDialog(string selected) {
   Text="Vigília | Sessão de estudo";ClientSize=new Size(610,590);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Paper;Font=new Font("Segoe UI",10);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;Theme.Prepare(this);
   Controls.Add(Theme.Label("Reserve tempo para estudar.",24,22,560,38,21,false));
   Controls.Add(Theme.Label("Escolha os sites que ficarão bloqueados durante a sessão.",24,72,560,28,11,false));
   rules=Store.With(s=>s.rules.Where(r=>!r.archived).ToArray(),false);
   sites=new CheckedListBox {Location=new Point(24,111),Size=new Size(562,220),CheckOnClick=true,BorderStyle=BorderStyle.FixedSingle};
   foreach(var r in rules){string status=r.focusUntil>Engine.Stamp(DateTime.UtcNow)?" · foco até "+Engine.Epoch.AddSeconds(r.focusUntil).ToLocalTime().ToString("dd/MM HH:mm"):r.always?" · bloqueio permanente já ativo":"";sites.Items.Add(r.name+status,r.id==selected);}
   Controls.Add(sites);
   Controls.Add(Theme.Label("Duração em minutos",24,350,260,26,11,true));
   duration=new NumericUpDown {Location=new Point(24,382),Size=new Size(140,30),Minimum=1,Maximum=1440,Value=120};Controls.Add(duration);
   var two=Theme.Button("2 horas",188,376,120,false);var three=Theme.Button("3 horas",324,376,120,false);Controls.Add(two);Controls.Add(three);two.Click+=(s,e)=>duration.Value=120;three.Click+=(s,e)=>duration.Value=180;
   Controls.Add(Theme.Label("O prazo continua correndo com o painel fechado ou o computador suspenso.\nAo terminar, voltam a valer as outras regras, incluindo bloqueios permanentes.\nAdicionar tempo nunca encurta uma sessão que já está em andamento.",24,432,562,69,9,false));
   var cancel=Theme.Button("Pedir fim antecipado",24,523,250,false);var begin=Theme.Button("Iniciar bloqueio",336,523,250,true);Controls.Add(cancel);Controls.Add(begin);
   begin.Enabled=cancel.Enabled=rules.Length>0;
   if(rules.Length==0) Controls.Add(Theme.Label("Adicione sites no painel antes de iniciar uma sessão.",32,128,535,45,11,true));
   begin.Click+=(s,e)=>{try{var ids=Selected();Store.With(state=>{Engine.StartFocus(state,ids,(int)duration.Value,DateTime.UtcNow);return true;},true);DialogResult=DialogResult.OK;Close();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Selecione os sites");}};
   cancel.Click+=(s,e)=>{var ids=Selected();if(ids.Length==0){MessageBox.Show(this,"Selecione os sites cuja sessão deseja encerrar.");return;}if(MessageBox.Show(this,"Encerrar o estudo libera acesso e exige esperar 24 horas. A sessão ainda pode terminar naturalmente antes disso. Solicitar?","Encerrar foco",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;try{Store.With(state=>{Changes.EndFocus(state,ids,DateTime.UtcNow);return true;},true);Close();}catch(Exception ex){MessageBox.Show(this,ex.Message);}};
  }
  string[] Selected(){return sites.CheckedIndices.Cast<int>().Select(i=>rules[i].id).ToArray();}
 }
}
