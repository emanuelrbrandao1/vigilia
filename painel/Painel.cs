using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FocoLocal {
 public class RecordUseDialog:Form {
  public bool WithinPause;
  public RecordUseDialog(){Text="Vigília | Registrar uso de hoje";ClientSize=new Size(600,330);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;Theme.Prepare(this);
   Controls.Add(Theme.Label("Como foi o uso de hoje?",26,20,548,38,21,false));
   Controls.Add(Theme.Label("Considere os apps com bloqueio permanente, inclusive no celular.",26,69,548,35,10,false));
   var within=new RadioButton{Text="Cinza · usei apenas dentro da pausa permitida de 10 min",Location=new Point(26,112),Size=new Size(548,32),ForeColor=Theme.Ink};
   var outside=new RadioButton{Text="Vermelho · passei de 10 min ou usei fora da pausa permitida",Location=new Point(26,151),Size=new Size(548,32),ForeColor=Theme.Ink};Controls.Add(within);Controls.Add(outside);
   Controls.Add(Theme.Label("Sem uso, o dia fica verde automaticamente. Qualquer uso reinicia a\nsequência de dias sem apps. Um dia vermelho não volta a cinza.\nO registro não muda bloqueios nem libera acesso.",26,198,548,60,10,false));
   var save=Theme.Button("Registrar hoje",374,275,200,true);save.Enabled=false;var cancel=Theme.Button("Cancelar",220,275,138,false);Controls.Add(save);Controls.Add(cancel);
   within.CheckedChanged+=(s,e)=>save.Enabled=within.Checked||outside.Checked;outside.CheckedChanged+=(s,e)=>save.Enabled=within.Checked||outside.Checked;
   save.Click+=(s,e)=>{WithinPause=within.Checked;DialogResult=DialogResult.OK;Close();};cancel.Click+=(s,e)=>Close();CancelButton=cancel;AcceptButton=save;
  }
 }
 public class Editor:Form {
  public Rule Result;
  TextBox name,domains;ComboBox mode;Label minuteLabel,windowLabel,daysLabel,explanation;NumericUpDown minutes;TimeInput start,end;CheckedListBox days;string id;
  public Editor(Rule r) {
   id=r==null?Guid.NewGuid().ToString("N"):r.id;
   Text=r==null?"Adicionar site":"Editar regra";ClientSize=new Size(650,710);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;Theme.Prepare(this);
   Controls.Add(Theme.Label("Defina o seu limite.",26,22,598,37,21,false));
   var sub=Theme.Label("Restringir é imediato. Liberar mais acesso exige esperar 24 h.",27,66,598,25,11,false);sub.ForeColor=Theme.Muted;Controls.Add(sub);
   Controls.Add(Theme.Label("Nome da regra",26,110,598,24,10,true));
   name=new TextBox {Location=new Point(26,139),Size=new Size(598,30),Text=r==null?"":r.name,MaxLength=80};Controls.Add(name);
   Controls.Add(Theme.Label("Sites · um domínio por linha, inclui subdomínios",26,184,598,24,10,true));
   domains=new TextBox {Location=new Point(26,213),Size=new Size(598,64),Multiline=true,ScrollBars=ScrollBars.Vertical,Text=r==null?"":String.Join(Environment.NewLine,r.domains)};Controls.Add(domains);
   Controls.Add(Theme.Label("Como o acesso deve funcionar?",26,293,598,24,10,true));
   mode=new ComboBox {Location=new Point(26,323),Size=new Size(598,32),DropDownStyle=ComboBoxStyle.DropDownList,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=25};
   mode.Items.AddRange(new object[]{"Bloquear sempre pela extensão","Permitir apenas no horário + limite diário","Bloquear durante uma faixa de horário","Cota de minutos na faixa · livre fora dela"});
   mode.SelectedIndex=r!=null&&r.always?0:r!=null&&r.windowQuota?3:r!=null&&r.blockWindow?2:1;Controls.Add(mode);
   mode.DrawItem+=(s,e)=>{using(var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Theme.Hover:Theme.Surface))e.Graphics.FillRectangle(brush,e.Bounds);int index=e.Index<0?mode.SelectedIndex:e.Index;if(index>=0)TextRenderer.DrawText(e.Graphics,mode.Items[index].ToString(),mode.Font,e.Bounds,Theme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);e.DrawFocusRectangle();};
   minuteLabel=Theme.Label("Minutos por dia (0 = sem limite)",26,376,430,26,11,true);Controls.Add(minuteLabel);
   minutes=new NumericUpDown {Location=new Point(492,370),Size=new Size(132,34),Minimum=0,Maximum=1440,Value=r==null?30:r.minutes,Font=new Font("Segoe UI",13,FontStyle.Bold)};Controls.Add(minutes);
   windowLabel=Theme.Label("Horário permitido",26,420,598,25,10,true);Controls.Add(windowLabel);
   start=Time(26,449,r==null?0:r.start);end=Time(194,449,r==null?0:r.end);Controls.Add(start);Controls.Add(end);
   var to=Theme.Label("até",167,453,32,25,10,false);Controls.Add(to);
   Controls.Add(Theme.Label("Horários iguais = dia inteiro.\nFaixas noturnas passam da meia-noite.",360,446,264,46,9,false));
   daysLabel=Theme.Label("Dias permitidos",26,494,598,24,10,true);Controls.Add(daysLabel);
   days=new CheckedListBox {Location=new Point(26,525),Size=new Size(598,28),MultiColumn=true,ColumnWidth=82,CheckOnClick=true,BorderStyle=BorderStyle.None,BackColor=Theme.Paper};
   string[] labels={"Dom","Seg","Ter","Qua","Qui","Sex","Sáb"};for(int i=0;i<7;i++)days.Items.Add(labels[i],r==null||(r.days&(1<<i))!=0);Controls.Add(days);
   explanation=Theme.Label("",26,569,598,63,10,false);explanation.ForeColor=Theme.Green;Controls.Add(explanation);
   var save=Theme.Button("Salvar regra",434,647,190,true);var cancel=Theme.Button("Cancelar",280,647,138,false);Controls.Add(save);Controls.Add(cancel);cancel.Click+=(s,e)=>Close();
   Action describe=()=>{minutes.Enabled=start.Enabled=end.Enabled=days.Enabled=mode.SelectedIndex!=0;minuteLabel.Text=mode.SelectedIndex==3?"Minutos disponíveis nessa faixa":"Minutos por dia (0 = sem limite)";windowLabel.Text=mode.SelectedIndex==2?"Horário bloqueado":mode.SelectedIndex==3?"Faixa em que a cota será aplicada":"Horário permitido";daysLabel.Text=mode.SelectedIndex==2?"Dias de bloqueio":mode.SelectedIndex==3?"Dias com cota na faixa":"Dias permitidos";explanation.Text=mode.SelectedIndex==3?String.Format("Até {0} minutos entre {1:HH:mm} e {2:HH:mm}.\nFora dessa faixa, acesso livre. A cota renova a cada nova faixa.",minutes.Value,start.Value,end.Value):mode.SelectedIndex==2?"O site fica bloqueado nessa faixa. Fora dela, o acesso depende\nda cota diária acima, se houver.":mode.SelectedIndex==0?"A extensão bloqueia esse site o tempo todo.":"O site só fica disponível nos dias e horários escolhidos,\naté atingir a cota diária, se houver.";};
   bool presetApplied=r!=null;mode.SelectedIndexChanged+=(s,e)=>{if(mode.SelectedIndex>=2&&!presetApplied){start.Value=DateTime.Today.AddHours(12.5);end.Value=DateTime.Today.AddHours(18.5);minutes.Value=mode.SelectedIndex==3?20:0;presetApplied=true;}if(mode.SelectedIndex==3&&minutes.Value==0)minutes.Value=20;describe();};minutes.ValueChanged+=(s,e)=>describe();start.ValueChanged+=(s,e)=>describe();end.ValueChanged+=(s,e)=>describe();describe();
   save.Click+=(s,e)=>{try{int mask=0;for(int i=0;i<7;i++)if(days.GetItemChecked(i))mask|=1<<i;Result=new Rule {id=id,name=name.Text,domains=domains.Text.Split(new[]{'\r','\n',','},StringSplitOptions.RemoveEmptyEntries),always=mode.SelectedIndex==0,blockWindow=mode.SelectedIndex==2,windowQuota=mode.SelectedIndex==3,focusUntil=r==null?0:r.focusUntil,minutes=(int)minutes.Value,start=start.Value.Hour*60+start.Value.Minute,end=end.Value.Hour*60+end.Value.Minute,days=mask};Engine.Validate(Result);DialogResult=DialogResult.OK;Close();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Revise a regra");}};
   AcceptButton=save;CancelButton=cancel;
  }
  TimeInput Time(int x,int y,int value){return new TimeInput{Location=new Point(x,y),Value=DateTime.Today.AddMinutes(value)};}
 }
 public class Setup:Form {
  public Setup() {
   Text="Conectar Chrome e Edge";ClientSize=new Size(690,520);BackColor=Theme.Paper;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11);Theme.Prepare(this);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
   Controls.Add(Theme.Label("Uma instalação em cada navegador",26,24,640,40,20,true));
   Controls.Add(Theme.Label("1. Abra a página de extensões usando os botões abaixo.\n2. Ative o Modo do desenvolvedor.\n3. Clique em Carregar sem compactação / Carregar descompactado.\n4. Selecione a pasta da extensão indicada abaixo.\n5. Nos detalhes, permita o modo anônimo / InPrivate.\n6. Volte ao painel e confira a conexão. Os limites já estarão ativos.",26,85,640,160,11,false));
   var path=new TextBox {Text=Path.Combine(Store.Root,"extensao"),ReadOnly=true,Location=new Point(26,250),Size=new Size(638,55),Multiline=true};Controls.Add(path);
   var copy=Theme.Button("Copiar pasta",26,323,145,false);copy.Click+=(s,e)=>Clipboard.SetText(path.Text);Controls.Add(copy);
   var chrome=Theme.Button("Abrir Chrome",186,323,150,true);chrome.Click+=(s,e)=>OpenBrowser(false);Controls.Add(chrome);
   var edge=Theme.Button("Abrir Edge",352,323,150,true);edge.Click+=(s,e)=>OpenBrowser(true);Controls.Add(edge);
   Controls.Add(Theme.Label("A extensão acessa o endereço da aba ativa para contar o tempo e bloquear domínios. Não lê mensagens nem o conteúdo das páginas. Só salva os totais dos sites configurados neste computador.\n\nProtótipo pessoal: desativar a extensão, usar outro perfil ou outro navegador contorna os limites. A instalação é necessária em cada perfil.",26,384,638,113,10,false));
  }
  void OpenBrowser(bool edge) {try {string exe=edge?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Microsoft\Edge\Application\msedge.exe"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"Google\Chrome\Application\chrome.exe");Process.Start(exe,edge?"edge://extensions":"chrome://extensions");}catch(Exception e){MessageBox.Show(this,e.Message);}}
 }
}
