using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace FocoLocal {
 public class Dashboard:Form {
  readonly Font siteFont=new Font("Segoe UI",11,FontStyle.Bold);
  DataGridView grid;Label connection,summary,ruleCount,legacy,selection,empty;Timer refresh;Snapshot snapshot;ToggleButton showArchived;TextBox search;Button archive,edit,remove,pause,pending;bool reloading;ToolTip tips=new ToolTip();StreakControl streak;string lastStreak;bool legacyActive;DateTime legacyChecked=DateTime.MinValue;
  string HostsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"drivers\etc\hosts");
  bool LegacyActive(){try{return File.ReadAllText(HostsPath).Contains("# BEGIN BLOQUEADOR-SIMPLES-LOCAL");}catch{return true;}}
  public Dashboard(){
   Text="Vigília | Foco local";ClientSize=new Size(1120,820);MinimumSize=new Size(1020,760);StartPosition=FormStartPosition.CenterScreen;Theme.Prepare(this);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28,20,28,16),ColumnCount=1,RowCount=7,BackColor=Theme.Paper};
   foreach(int height in new[]{100,96,84})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));foreach(int height in new[]{62,56,68})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));Controls.Add(layout);
   var header=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};layout.Controls.Add(header,0,0);
   header.Controls.Add(new LogoControl{Location=new Point(0,9),Size=new Size(56,56)});
   var brand=Theme.Label("V I G Í L I A",76,3,580,48,29,false);header.Controls.Add(brand);
   var intro=Theme.Label("Foco local. Tempo sob controle.",78,56,550,28,11,false);intro.ForeColor=Theme.Muted;header.Controls.Add(intro);
   streak=new StreakControl{Location=new Point(664,2),Size=new Size(400,96),Anchor=AnchorStyles.Top|AnchorStyles.Right,AccessibleName="Dias sem apps bloqueados"};header.Controls.Add(streak);streak.Record.LinkClicked+=(s,e)=>RecordUse();
   tips.SetToolTip(streak,"Conta os dias sem usar sites de bloqueio permanente (TikTok, X...).\nUsar um deles durante uma pausa zera a sequência.\nQuadrados: últimos 14 dias. Verde = sem uso, cinza = dentro da pausa de 10 min, vermelho = fora da regra.");tips.SetToolTip(streak.Record,"Use se você usou um app bloqueado fora do computador, como no celular.");
   var cards=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};foreach(int width in new[]{33,30,37})cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,width));cards.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.Controls.Add(cards,0,1);
   Func<int,string,Label> card=(col,label)=>{var p=new Card{Dock=DockStyle.Fill,Margin=new Padding(col==0?0:8,0,col==2?0:8,8)};cards.Controls.Add(p,col,0);var caption=Theme.Label(label,18,12,300,20,10,false);caption.ForeColor=Theme.Muted;p.Controls.Add(caption);var value=Theme.Label("",18,37,300,40,23,false);value.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;p.Controls.Add(value);return value;};
   summary=card(0,"Uso monitorado hoje");ruleCount=card(1,"Regras em vigor");connection=card(2,"Navegadores");connection.Font=new Font("Segoe UI",10.5f);connection.Height=44;
   var toolbar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=new Padding(0,18,0,10)};toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int w in new[]{190,150,158})toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,w));layout.Controls.Add(toolbar,0,2);
   var find=new Field("Buscar regra ou site"){Dock=DockStyle.Fill,Margin=new Padding(0,15,18,1)};search=find.Box;toolbar.Controls.Add(find,0,0);
   showArchived=new ToggleButton{Text="Ver arquivadas",Dock=DockStyle.Fill,Margin=new Padding(0,15,12,1),FlatStyle=FlatStyle.Flat,BackColor=Theme.Paper,ForeColor=Theme.Ink,Font=new Font("Segoe UI",10,FontStyle.Bold),AccessibleName="Mostrar regras arquivadas",Cursor=Cursors.Hand};toolbar.Controls.Add(showArchived,1,0);
   pending=Theme.Button("Pendentes",0,0,168,false);pending.Dock=DockStyle.Fill;pending.Margin=new Padding(0,15,12,1);toolbar.Controls.Add(pending,2,0);
   var add=Theme.Button("Nova regra",0,0,158,true);add.Dock=DockStyle.Fill;add.Margin=new Padding(0,15,0,1);toolbar.Controls.Add(add,3,0);
   toolbar.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var gridArea=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};layout.Controls.Add(gridArea,0,3);
   grid=new DataGridView{Dock=DockStyle.Fill,BackgroundColor=Theme.Paper,BorderStyle=BorderStyle.None,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,ReadOnly=true,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,EnableHeadersVisualStyles=false,ColumnHeadersHeight=40,ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing,AccessibleName="Regras de acesso"};
   grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Theme.Paper,ForeColor=Theme.Muted,Font=new Font("Segoe UI",10),Padding=new Padding(12,0,0,0),SelectionBackColor=Theme.Paper,SelectionForeColor=Theme.Muted};
   grid.DefaultCellStyle=new DataGridViewCellStyle{BackColor=Theme.Surface,ForeColor=Theme.Ink,Font=new Font("Segoe UI",10),SelectionBackColor=Theme.Hover,SelectionForeColor=Theme.Ink,Padding=new Padding(12,8,8,8),WrapMode=DataGridViewTriState.True};grid.RowTemplate.Height=88;grid.CellBorderStyle=DataGridViewCellBorderStyle.None;grid.GridColor=Theme.Paper;grid.AdvancedCellBorderStyle.All=DataGridViewAdvancedCellBorderStyle.None;grid.RowTemplate.DividerHeight=0;
   grid.Columns.Add("site","Site ou grupo");grid.Columns.Add("status","Acesso agora");grid.Columns.Add("usage","Consumo");grid.Columns.Add("rule","Regra");grid.Columns[0].FillWeight=22;grid.Columns[1].FillWeight=23;grid.Columns[2].FillWeight=21;grid.Columns[3].FillWeight=34;foreach(DataGridViewColumn c in grid.Columns)c.SortMode=DataGridViewColumnSortMode.NotSortable;gridArea.Controls.Add(grid);
   empty=Theme.Label("",0,0,500,160,16,false);empty.Dock=DockStyle.Fill;empty.TextAlign=ContentAlignment.MiddleCenter;empty.ForeColor=Theme.Muted;gridArea.Controls.Add(empty);empty.Visible=false;
   selection=Theme.Label("Selecione uma regra para ver as ações disponíveis.",0,0,1000,50,10,false);selection.Dock=DockStyle.Fill;selection.Margin=new Padding(0,13,0,5);selection.ForeColor=Theme.Muted;layout.Controls.Add(selection,0,4);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};layout.Controls.Add(actions,0,5);
   edit=Theme.Button("Editar",0,0,110,false);archive=Theme.Button("Arquivar",0,0,134,false);pause=Theme.Button("Pedir pausa",0,0,168,false);var study=Theme.Button("Sessão de estudo",0,0,202,true);remove=Theme.Button("Excluir",0,0,112,false);
   foreach(var b in new[]{edit,archive,pause,study,remove}){b.Margin=new Padding(0,0,12,0);actions.Controls.Add(b);}
   var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,Margin=new Padding(0,14,0,0)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,210));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,224));layout.Controls.Add(footer,0,6);
   legacy=Theme.Label("",0,0,590,48,9,false);legacy.Dock=DockStyle.Fill;legacy.ForeColor=Theme.Muted;footer.Controls.Add(legacy,0,0);
   var setup=Theme.Button("Navegadores",0,0,198,false);setup.Dock=DockStyle.Fill;setup.Margin=new Padding(0,0,12,14);footer.Controls.Add(setup,1,0);var permanent=Theme.Button("Bloqueio permanente",0,0,224,false);permanent.Dock=DockStyle.Fill;permanent.Margin=new Padding(0,0,0,14);footer.Controls.Add(permanent,2,0);
   tips.SetToolTip(search,"Filtra por nome ou domínio (Ctrl+F). Esc limpa. Não muda suas regras.");tips.SetToolTip(add,"Criar uma regra (Ctrl+N). Enter edita a regra selecionada.");tips.SetToolTip(pause,"Exceção para bloqueio permanente: espera de 30 minutos e acesso por 10 minutos.");tips.SetToolTip(pending,"Revisar ou cancelar mudanças que aguardam 24 horas.");tips.SetToolTip(archive,"Desativa a regra após a espera, mantendo a configuração.");tips.SetToolTip(study,"Bloqueie sites por um período de estudo.");
   search.TextChanged+=(s,e)=>Reload();showArchived.CheckedChanged+=(s,e)=>Reload();grid.SelectionChanged+=(s,e)=>{if(!reloading)UpdateSelection();};grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)Edit(false);};grid.CellPainting+=PaintCell;grid.RowPrePaint+=PaintRow;
   add.Click+=(s,e)=>Edit(true);edit.Click+=(s,e)=>Edit(false);archive.Click+=(s,e)=>Archive();remove.Click+=(s,e)=>Remove();pause.Click+=(s,e)=>{string id=SelectedId();if(id==null)return;try{using(var dialog=new PauseDialog(id))dialog.ShowDialog(this);Reload();}catch(Exception ex){MessageBox.Show(this,ex.Message);}};
   study.Click+=(s,e)=>{using(var dialog=new FocusDialog(SelectedId()))dialog.ShowDialog(this);Reload();};setup.Click+=(s,e)=>{using(var dialog=new Setup())dialog.ShowDialog(this);};permanent.Click+=(s,e)=>OpenPermanent();pending.Click+=(s,e)=>{using(var dialog=new ChangesDialog())dialog.ShowDialog(this);Reload();};
   refresh=new Timer{Interval=1500};refresh.Tick+=(s,e)=>Reload();FormClosed+=(s,e)=>{refresh.Dispose();siteFont.Dispose();tips.Dispose();};Reload();refresh.Start();
  }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
   if(keyData==(Keys.Control|Keys.F)){search.Focus();search.SelectAll();return true;}
   if(keyData==(Keys.Control|Keys.N)){Edit(true);return true;}
   if(keyData==Keys.Enter&&grid.Focused&&SelectedId()!=null&&(msg.LParam.ToInt64()&0x40000000)==0){Edit(false);return true;}
   if(keyData==Keys.Escape&&search.Focused&&search.Text.Length>0){search.Clear();return true;}
   return base.ProcessCmdKey(ref msg,keyData);
  }
  string SelectedId(){return grid.SelectedRows.Count==0?null:((Row)grid.SelectedRows[0].Tag).id;}
  static string HH(int m){return (m/60).ToString("00")+":"+(m%60).ToString("00");}
  static string Duration(double seconds){return ((int)seconds/60)+"m "+((int)seconds%60).ToString("00")+"s";}
  void UpdateSelection(){var r=grid.SelectedRows.Count==0?null:grid.SelectedRows[0].Tag as Row;edit.Enabled=archive.Enabled=remove.Enabled=r!=null;pause.Enabled=r!=null&&!r.archived&&r.always;archive.Text=r!=null&&r.archived?"Reativar":"Arquivar";selection.Text=r==null?"Selecione uma regra. Restringir é imediato; liberar mais acesso espera 24 horas.":String.Join("  ·  ",r.domains)+"\n"+(!String.IsNullOrEmpty(r.changeStatus)?r.changeStatus:r.archived?"Arquivada. A configuração está preservada.":r.always?r.pauseStatus:"Mais restrição: agora. Mais acesso: após 24 horas.");}
  // Each rule is its own rounded card; the gap between rows replaces grid lines.
  void PaintRow(object sender,DataGridViewRowPrePaintEventArgs e){
   float scale=e.Graphics.DpiX/96f;int gap=(int)(4*scale);bool selected=grid.Rows[e.RowIndex].Selected;
   e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;using(var b=new SolidBrush(Theme.Paper))e.Graphics.FillRectangle(b,e.RowBounds);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   using(var path=Artwork.Rounded(new RectangleF(e.RowBounds.X+0.5f,e.RowBounds.Y+gap+0.5f,e.RowBounds.Width-1,e.RowBounds.Height-gap*2-1),10*scale))using(var fill=new SolidBrush(selected?Theme.Hover:Theme.Surface))using(var pen=new Pen(selected?Theme.Muted:Theme.Line)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
   var info=grid.Rows[e.RowIndex].Tag as Row;double now=Engine.Stamp(DateTime.UtcNow);
   if(info!=null&&info.pauseEndsAt>now&&info.pauseEndsAt>info.pauseRequestedAt){
    // Timeline: wait segment then access segment; the fill shows where the pause is now.
    double total=info.pauseEndsAt-info.pauseRequestedAt,progress=Math.Max(0,Math.Min(1,(now-info.pauseRequestedAt)/total));float left=e.RowBounds.X+16*scale,width=e.RowBounds.Width-32*scale,y=e.RowBounds.Bottom-gap-10*scale,h=3*scale,split=width*(float)((info.pauseStartsAt-info.pauseRequestedAt)/total);
    e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;using(var track=new SolidBrush(Theme.Line))e.Graphics.FillRectangle(track,left,y,width,h);
    using(var bar=new SolidBrush(now<info.pauseStartsAt?Theme.Muted:Theme.Ink))e.Graphics.FillRectangle(bar,left,y,width*(float)progress,h);
    using(var cut=new SolidBrush(selected?Theme.Hover:Theme.Surface))e.Graphics.FillRectangle(cut,left+split-scale,y,2*scale,h);
   }
   e.PaintParts&=~(DataGridViewPaintParts.Background|DataGridViewPaintParts.SelectionBackground|DataGridViewPaintParts.Border|DataGridViewPaintParts.Focus);
  }
  void PaintCell(object sender,DataGridViewCellPaintingEventArgs e){
   if(e.RowIndex==-1&&e.ColumnIndex>=0){e.PaintBackground(e.ClipBounds,false);e.PaintContent(e.ClipBounds);e.Handled=true;return;}
   if(e.RowIndex<0||e.ColumnIndex!=1)return;var row=grid.Rows[e.RowIndex].Tag as Row;if(row==null)return;
   float scale=e.Graphics.DpiX/96f;int pad=(int)(12*scale);var rect=new Rectangle(e.CellBounds.X+pad,e.CellBounds.Y+pad,e.CellBounds.Width-pad*2,e.CellBounds.Height-pad*2);
   int mark=(int)(9*scale);var markRect=new Rectangle(e.CellBounds.X+pad,e.CellBounds.Y+(e.CellBounds.Height-mark)/2,mark,mark);Color markColor=row.archived?Theme.Line:row.blocked?Theme.Ink:Theme.Muted;
   e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var shape=Artwork.Rounded(new RectangleF(markRect.X+0.5f,markRect.Y+0.5f,mark-1,mark-1),2*scale)){if(row.blocked)using(var b=new SolidBrush(markColor))e.Graphics.FillPath(b,shape);else using(var pen=new Pen(markColor))e.Graphics.DrawPath(pen,shape);}
   TextRenderer.DrawText(e.Graphics,Convert.ToString(e.FormattedValue),e.CellStyle.Font,new Rectangle(markRect.Right+(int)(12*scale),rect.Y,rect.Right-markRect.Right-(int)(12*scale),rect.Height),row.archived?Theme.Muted:Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);e.Handled=true;
  }
  void Reload(){if(grid==null)return;try{
   string selected=SelectedId();int scroll=grid.FirstDisplayedScrollingRowIndex;reloading=true;snapshot=Store.With(s=>Engine.View(s,DateTime.UtcNow),false);double now=Engine.Stamp(DateTime.UtcNow);
   Func<string,string> online=b=>b+" "+(snapshot.seen.ContainsKey(b)&&now-snapshot.seen[b]<8&&snapshot.ready.ContainsKey(b)&&snapshot.ready[b]?"conectado":"sem conexão");
   connection.Text=online("Chrome")+"\n"+online("Edge");summary.Text=Duration(snapshot.rules.Concat(snapshot.archivedRules).Sum(r=>r.usedSeconds));ruleCount.Text=snapshot.rules.Count+(snapshot.rules.Count==1?" ativa":" ativas");string archivedText="Ver arquivadas ("+snapshot.archivedRules.Count+")";if(showArchived.Text!=archivedText)showArchived.Text=archivedText;string streakKey=snapshot.streakDays+"|"+snapshot.streakBest+"|"+snapshot.streakTracking+"|"+Store.Json.Serialize(snapshot.streakDayKinds)+"|"+DateTime.Today.ToString("yyyyMMdd");if(streakKey!=lastStreak){lastStreak=streakKey;streak.Days=snapshot.streakDays;streak.Best=snapshot.streakBest;streak.Tracking=snapshot.streakTracking??"";streak.Kinds=snapshot.streakDayKinds;streak.AccessibleDescription="Hoje: "+Streak.KindLabel(Streak.DayKind(streak.Tracking,streak.Kinds,DateTime.Today))+". Verde: sem uso. Cinza: dentro da pausa. Vermelho: fora da regra.";streak.Invalidate();}
   int pendingCount=snapshot.rules.Concat(snapshot.archivedRules).Count(r=>!String.IsNullOrEmpty(r.changeStatus));pending.Text="Pendentes"+(pendingCount>0?" ("+pendingCount+")":"");
   string query=search.Text.Trim();var rows=(showArchived.Checked?snapshot.archivedRules:snapshot.rules).Where(r=>query.Length==0||r.name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||r.domains.Any(d=>d.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
   // Rebuilding every 1.5 s made the list flicker and jump; update in place when the same rules are shown.
   var cells=new System.Collections.Generic.List<object[]>();
   foreach(var r in rows){
    string hours=r.start==r.end?"dia inteiro":HH(r.start)+"–"+HH(r.end);
    string when=r.always?"Bloqueio permanente":r.windowQuota?r.minutes+" min entre "+hours+"\nLivre fora da faixa":(r.blockWindow?"Bloqueia ":"Permite ")+hours+"\n"+(r.minutes==0?"Sem cota diária":r.minutes+" min por dia");
    string usage=r.windowQuota?Duration(r.windowUsedSeconds)+" / "+r.minutes+" min\nCota da faixa":Duration(r.usedSeconds)+(r.minutes>0&&!r.always?" / "+r.minutes+" min":"");
    string status=r.reason;if(r.always&&!r.archived&&r.pauseStatus!=null&&!r.pauseStatus.StartsWith("Pausa disponível"))status=r.pauseStatus;
    if(r.pauseEndsAt>now){bool waiting=r.pauseStartsAt>now;string left=PauseDialog.Countdown((waiting?r.pauseStartsAt:r.pauseEndsAt)-now);status=(waiting?"Libera em ":"Liberado · resta ")+left;
     r.pauseStatus=waiting?"Pausa em espera: libera às "+Engine.Epoch.AddSeconds(r.pauseStartsAt).ToLocalTime().ToString("HH:mm")+" (faltam "+left+"). Cancelar não reinicia as 24 h.":"Pausa aberta: bloqueia de novo às "+Engine.Epoch.AddSeconds(r.pauseEndsAt).ToLocalTime().ToString("HH:mm")+" (restam "+left+").";}
    if(!String.IsNullOrEmpty(r.changeStatus))when+="\nAlteração pendente";
    cells.Add(new object[]{r.name,status,usage,when});
   }
   bool same=grid.Rows.Count==rows.Length&&rows.Select((r,i)=>((Row)grid.Rows[i].Tag).id==r.id).All(x=>x);
   if(!same)grid.Rows.Clear();
   for(int i=0;i<rows.Length;i++){var r=rows[i];DataGridViewRow line;
    if(same){line=grid.Rows[i];var old=(Row)line.Tag;for(int c=0;c<4;c++)if(!Equals(line.Cells[c].Value,cells[i][c]))line.Cells[c].Value=cells[i][c];if(old.blocked!=r.blocked||old.archived!=r.archived||old.pauseEndsAt>0||r.pauseEndsAt>0)grid.InvalidateRow(i);}
    else{line=grid.Rows[grid.Rows.Add(cells[i])];line.Cells[0].Style.Font=siteFont;if(r.id==selected)line.Selected=true;}
    line.Tag=r;line.Cells[0].ToolTipText=String.Join(", ",r.domains);line.Cells[3].ToolTipText=r.changeStatus;
   }
   // While a pause runs, tick every second and show the countdown in the title (visible on the taskbar).
   var active=snapshot.rules.Where(x=>x.pauseEndsAt>now).OrderBy(x=>x.pauseStartsAt>now?x.pauseStartsAt:x.pauseEndsAt).FirstOrDefault();
   if(refresh!=null){int interval=active!=null?1000:1500;if(refresh.Interval!=interval)refresh.Interval=interval;}
   string title=active==null?"Vigília | Foco local":"Vigília | "+active.name+(active.pauseStartsAt>now?" libera em ":" liberado, resta ")+PauseDialog.Countdown((active.pauseStartsAt>now?active.pauseStartsAt:active.pauseEndsAt)-now);if(Text!=title)Text=title;
   if(selected==null||!rows.Any(r=>r.id==selected))grid.ClearSelection();if(!same&&scroll>=0&&scroll<grid.Rows.Count)grid.FirstDisplayedScrollingRowIndex=scroll;
   grid.Visible=rows.Length>0;empty.Visible=rows.Length==0;empty.Text=query.Length>0?"Nenhuma regra encontrada.\nTente outro nome ou domínio.":showArchived.Checked?"Nenhuma regra arquivada.":"Seu espaço começa aqui.\nAdicione uma regra para organizar seu acesso.";
   if(legacyChecked<DateTime.UtcNow.AddSeconds(-15)){legacyActive=LegacyActive();legacyChecked=DateTime.UtcNow;}legacy.Text="Conta a aba ativa. Pausa após 60 s sem atividade.\n"+(legacyActive?"O bloqueio do Windows tem uma lista independente.":"Você pode fechar este painel; a extensão continua ativa.");
  }catch(Exception e){connection.Text="Não foi possível atualizar.\n"+e.Message;}finally{reloading=false;UpdateSelection();}}
  void Edit(bool fresh) {
   string id=SelectedId(); if(!fresh && id==null) {MessageBox.Show(this,"Selecione um site para editar.");return;}
   Rule existing=fresh?null:Store.With(s=>s.rules.FirstOrDefault(r=>r.id==id),false);
   using(var editor=new Editor(existing)) if(editor.ShowDialog(this)==DialogResult.OK) {
    try {var pending=Store.With(s=>Changes.Save(s,editor.Result,DateTime.UtcNow),true);Changed(pending);Reload();}catch(Exception e){MessageBox.Show(this,e.Message,"Não foi possível salvar");}
   }
  }
  void Changed(PendingChange pending){MessageBox.Show(this,pending==null?"Alteração aplicada agora. Qualquer pedido anterior para esta regra foi cancelado.":"Esta mudança libera mais acesso e foi agendada para "+Engine.Epoch.AddSeconds(pending.executeAt).ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")+".\nA regra atual permanece ativa até lá. Consulte ou cancele em Alterações pendentes.",pending==null?"Regra atualizada":"Espera de 24 horas");}
  void Remove() {string id=SelectedId();if(id==null)return;if(MessageBox.Show(this,"Solicitar exclusão? Se isso liberar acesso, a regra continua ativa por 24 horas.","Excluir regra",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;try{Changed(Store.With(s=>Changes.Delete(s,id,DateTime.UtcNow),true));Reload();}catch(Exception e){MessageBox.Show(this,e.Message);}}
  void RecordUse(){using(var dialog=new RecordUseDialog()){if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   try{Store.With(s=>{Streak.Record(s,DateTime.Now,dialog.WithinPause,true);return true;},true);Reload();}catch(Exception e){MessageBox.Show(this,e.Message,"Não foi possível registrar");}}}
  void Archive(){string id=SelectedId();if(id==null)return;try{Changed(Store.With(s=>Changes.Archive(s,id,DateTime.UtcNow),true));Reload();}catch(Exception e){MessageBox.Show(this,e.Message);}}
  void OpenPermanent() {
   try {
    string script=Path.GetFullPath(Path.Combine(Store.Root,@"..\Bloqueador.ps1"));
    Process.Start(new ProcessStartInfo {FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),Arguments="-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File \""+script+"\"",UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden});
   } catch(Exception e) {MessageBox.Show(this,e.Message,"Não foi possível abrir o bloqueador");}
  }
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool PrintWindow(IntPtr window,IntPtr hdc,uint flags);
  [STAThread] public static void Main(string[] args) {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try {using(Form form=args.Length==2&&args[0]=="--preview-record"?(Form)new RecordUseDialog():args.Length==2&&args[0]=="--preview-reflection"?(Form)new ReflectionDialog(new Rule{name="Exemplo",domains=new[]{"example.com"}}):args.Length==2&&args[0]=="--preview-changes"?(Form)new ChangesDialog():args.Length==2&&args[0]=="--preview-pause"?(Form)new PauseDialog(Store.With(s=>s.rules.First(r=>r.always).id,false),true):args.Length==2&&args[0]=="--preview-work"?(Form)new Editor(new Rule {name="Pausa no trabalho",domains=new[]{"example.com"},always=false,windowQuota=true,minutes=20,start=750,end=1110,days=127}):args.Length==2&&args[0]=="--preview-focus"?(Form)new FocusDialog(null):new Dashboard()) {if(args.Length==2 && args[0].StartsWith("--preview")){form.Show();for(int i=0;i<15;i++){Application.DoEvents();System.Threading.Thread.Sleep(40);}form.Refresh();for(int i=0;i<5;i++){Application.DoEvents();System.Threading.Thread.Sleep(40);}using(var bitmap=new Bitmap(form.Width,form.Height)){bool printed;using(var g=Graphics.FromImage(bitmap)){IntPtr hdc=g.GetHdc();try{printed=PrintWindow(form.Handle,hdc,2);}finally{g.ReleaseHdc(hdc);}}var center=bitmap.GetPixel(bitmap.Width/2,bitmap.Height/2);if(!printed||center.A==0||(center.R==0&&center.G==0&&center.B==0))form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(args[1]);}return;}Application.Run(form);}}
   catch(Exception e){MessageBox.Show("Não foi possível abrir o Foco local.\n"+e.Message);}
  }
 }
}
