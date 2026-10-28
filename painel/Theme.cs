using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
namespace FocoLocal {
 public static class Theme {
  public static Color Ink=Color.FromArgb(242,241,238),Muted=Color.FromArgb(173,175,182),Green=Color.FromArgb(218,220,225),Paper=Color.FromArgb(17,18,21),Surface=Color.FromArgb(27,28,32),Hover=Color.FromArgb(43,45,51),Line=Color.FromArgb(65,67,74),Red=Color.FromArgb(241,166,158);
  public static Label Label(string text,int x,int y,int w,int h,float size,bool bold){return new Label {Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=Ink,BackColor=Color.Transparent};}
  public static Button Button(string text,int x,int y,int w,bool primary) {
   string glyph=text.Contains("Adicionar")||text.Contains("Nova regra")?"add":text=="Editar"?"edit":text.Contains("Excluir")?"delete":text.Contains("Estudar")||text.Contains("Sessão")||text.Contains("hora")||text.Contains("Iniciar")||text.Contains("pausa")?"clock":text.Contains("Conectar")||text.Contains("navegadores")||text=="Navegadores"||text.Contains("Abrir Chrome")||text.Contains("Abrir Edge")?"link":text.Contains("permanente")?"shield":text.Contains("Salvar")?"save":text.Contains("Cancelar")?"close":text.Contains("Arquivar")?"archive":"";
   return new ActionButton {Text=text.TrimStart('+',' '),Glyph=glyph,Primary=primary,Location=new Point(x,y),Size=new Size(w,40),FlatStyle=FlatStyle.Flat,BackColor=Paper,ForeColor=primary?Paper:Ink,Cursor=Cursors.Hand,Font=new Font("Segoe UI",10,FontStyle.Bold),AccessibleName=text};
  }
  public static void Prepare(Form form){form.AutoScaleDimensions=new SizeF(96,96);form.AutoScaleMode=AutoScaleMode.Dpi;form.Font=new Font("Segoe UI",10.5f);form.BackColor=Paper;form.ForeColor=Ink;try{form.Icon=new Icon(Path.Combine(Store.Root,@"assets\foco.ico"));}catch{}form.Shown+=(s,e)=>Apply(form);form.HandleCreated+=(s,e)=>{try{int on=1;DwmSetWindowAttribute(form.Handle,20,ref on,4);}catch{}};}
  [System.Runtime.InteropServices.DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  // Rounded surface used by cards and fields: corners show the parent color behind them.
  public static void Shape(Graphics g,Control c,float radius,Color fill,Color line){g.Clear(c.Parent==null?Paper:c.Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float k=g.DpiX/96f;using(var path=Artwork.Rounded(new RectangleF(0.5f,0.5f,c.Width-1,c.Height-1),radius*k))using(var b=new SolidBrush(fill))using(var p=new Pen(line)){g.FillPath(b,path);g.DrawPath(p,path);}}
  public static void Apply(Control root){foreach(Control c in root.Controls){
   if(c is TextBoxBase||c is ListBox||c is ComboBox||c is NumericUpDown){c.BackColor=Surface;c.ForeColor=Ink;}
   var text=c as TextBoxBase;if(text!=null&&!(c.Parent is Field||c.Parent is UpDownBase))text.BorderStyle=BorderStyle.FixedSingle;
   var combo=c as ComboBox;if(combo!=null)combo.FlatStyle=FlatStyle.Standard;var spin=c as UpDownBase;if(spin!=null)spin.BorderStyle=BorderStyle.FixedSingle;
   var check=c as CheckBox;if(check!=null){check.ForeColor=Ink;check.FlatStyle=FlatStyle.Flat;}
   var link=c as LinkLabel;if(link!=null){link.LinkColor=Ink;link.ActiveLinkColor=Color.White;link.VisitedLinkColor=Muted;link.BackColor=Color.Transparent;}
   Apply(c);
  }}
 }
 public class ActionButton:Button {
  public bool Primary,Active;public string Glyph="";bool hover,pressed;
  public ActionButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatAppearance.BorderSize=0;}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.Clear(Parent==null?Theme.Paper:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float scale=Height/40f;
   Color fill=!Enabled?Theme.Paper:Primary?(pressed?Color.FromArgb(196,197,201):hover?Color.White:Theme.Ink):(pressed?Theme.Line:hover||Active?Theme.Hover:Theme.Surface);
   using(var path=Artwork.Rounded(new RectangleF(0.5f,0.5f,Width-1,Height-1),6*scale))using(var brush=new SolidBrush(fill))using(var pen=new Pen(Primary&&Enabled?fill:Active?Theme.Muted:Theme.Line)){g.FillPath(brush,path);g.DrawPath(pen,path);}
   Color ink=!Enabled?Color.FromArgb(119,122,132):Primary?Theme.Paper:Theme.Ink;int icon=Glyph==""?0:(int)(17*scale),gap=icon==0?0:(int)(8*scale);Size text=TextRenderer.MeasureText(Text,Font);int left=Math.Max((int)(10*scale),(Width-text.Width-icon-gap)/2);
   g.SmoothingMode=SmoothingMode.AntiAlias;if(icon>0)Artwork.Glyph(g,Glyph,new RectangleF(left,(Height-icon)/2,icon,icon),ink);
   TextRenderer.DrawText(g,Text,Font,new Rectangle(left+icon+gap,0,Width-left-icon-gap-5,Height),ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   if(Focused&&ShowFocusCues)using(var focus=Artwork.Rounded(new RectangleF(3.5f,3.5f,Width-8,Height-8),4*scale))using(var pen=new Pen(Primary?Theme.Paper:Theme.Ink))g.DrawPath(pen,focus);
  }
 }
 public class LogoControl:Control {public LogoControl(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}protected override void OnPaint(PaintEventArgs e){Artwork.Logo(e.Graphics,new RectangleF(0,0,Width,Height),false);}}
 public class StreakControl:Control {
  // Header summary: current streak, record and the last 14 days. It only displays data.
  public int Days,Best;public string Tracking="";public System.Collections.Generic.Dictionary<string,int> Kinds=new System.Collections.Generic.Dictionary<string,int>();
  readonly ToolTip dayTip=new ToolTip();int hovered=-2;
  public static readonly Color NoUseColor=Color.FromArgb(112,194,148),WithinColor=Color.FromArgb(151,156,165);
  public LinkLabel Record;
  public StreakControl(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;
   Record=new LinkLabel{Text="Registrar uso",AutoSize=false,TextAlign=ContentAlignment.MiddleRight,Font=new Font("Segoe UI",9),LinkColor=Theme.Muted,ActiveLinkColor=Theme.Ink,BackColor=Color.Transparent,AccessibleName="Registrar uso de um app bloqueado"};Controls.Add(Record);}
  protected override void OnLayout(LayoutEventArgs e){float k=Height/96f;Record.Bounds=new Rectangle(0,(int)(62*k),Width-(int)(232*k),(int)(22*k));base.OnLayout(e);}
  Rectangle DayRect(int i){float k=Height/96f;int box=(int)(11*k),gap=(int)(5*k);return new Rectangle(Width-(14-i)*(box+gap)+gap-1,(int)(68*k),box,box);}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int hit=-1;for(int i=0;i<14;i++)if(DayRect(i).Contains(e.Location)){hit=i;break;}if(hit==hovered)return;hovered=hit;var day=DateTime.Today.AddDays(hit-13);dayTip.SetToolTip(this,hit<0?"Verde: sem uso registrado. Cinza: uso dentro da pausa de 10 min. Vermelho: uso fora da regra.\nTraço: uso antigo sem classificação. Contorno: sem acompanhamento.":day.ToString("dd/MM/yyyy")+" · "+Streak.KindLabel(Streak.DayKind(Tracking,Kinds,day))+(day==DateTime.Today?" (hoje, em andamento)":""));}
  protected override void OnMouseLeave(EventArgs e){hovered=-2;dayTip.SetToolTip(this,"");base.OnMouseLeave(e);}
  protected override void Dispose(bool disposing){if(disposing)dayTip.Dispose();base.Dispose(disposing);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float k=Height/96f;var right=TextFormatFlags.Right|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine;
   using(var caption=new Font("Segoe UI",13*k,GraphicsUnit.Pixel))TextRenderer.DrawText(g,"Dias sem apps bloqueados",caption,new Rectangle(0,(int)(2*k),Width,(int)(16*k)),Theme.Muted,right);
   string big=Days==1?"1 dia":Days+" dias";int bigLeft;
   using(var font=new Font("Segoe UI",30*k,GraphicsUnit.Pixel)){var size=TextRenderer.MeasureText(g,big,font,Size.Empty,right);bigLeft=Width-size.Width;TextRenderer.DrawText(g,big,font,new Rectangle(0,(int)(18*k),Width,(int)(40*k)),Theme.Ink,right);}
   using(var small=new Font("Segoe UI",12*k,GraphicsUnit.Pixel))TextRenderer.DrawText(g,"recorde "+Best,small,new Rectangle(0,(int)(36*k),bigLeft-(int)(14*k),(int)(18*k)),Theme.Muted,right);
   DateTime today=DateTime.Today;
   for(int i=0;i<14;i++){var day=today.AddDays(i-13);var rect=DayRect(i);int kind=Streak.DayKind(Tracking,Kinds,day);
    using(var shape=Artwork.Rounded(new RectangleF(rect.X+0.5f,rect.Y+0.5f,rect.Width-1,rect.Height-1),2*k)){
    if(kind==Streak.NoUse||kind==Streak.WithinPause||kind==Streak.OutsidePause)using(var b=new SolidBrush(kind==Streak.NoUse?NoUseColor:kind==Streak.WithinPause?WithinColor:Theme.Red))g.FillPath(b,shape);
    else using(var p=new Pen(Theme.Line))g.DrawPath(p,shape);
    if(kind==Streak.LegacyUnknown)using(var p=new Pen(Theme.Muted))g.DrawLine(p,rect.Left+3,rect.Bottom-3,rect.Right-3,rect.Top+3);
    if(day==today)using(var p=new Pen(Theme.Ink))g.DrawPath(p,shape);}
   }
   using(var legend=new Font("Segoe UI",10*k,GraphicsUnit.Pixel))TextRenderer.DrawText(g,"Verde: sem uso · Cinza: até 10 min · Vermelho: fora",legend,new Rectangle(0,(int)(83*k),Width,(int)(13*k)),Theme.Muted,right);
  }
 }
 public class Field:Panel {
  // Single-line input drawn with the same 40 px height and hairline as the buttons.
  public readonly TextBox Box;
  [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,string lParam);
  public Field(string hint){DoubleBuffered=true;BackColor=Theme.Surface;Padding=new Padding(12,0,12,0);
   Box=new TextBox{BorderStyle=BorderStyle.None,BackColor=Theme.Surface,ForeColor=Theme.Ink,Font=new Font("Segoe UI",10.5f),AccessibleName=hint};Controls.Add(Box);
   Box.HandleCreated+=(s,e)=>SendMessage(Box.Handle,0x1501,(IntPtr)1,hint);Box.GotFocus+=(s,e)=>Invalidate();Box.LostFocus+=(s,e)=>Invalidate();Click+=(s,e)=>Box.Focus();}
  protected override void OnLayout(LayoutEventArgs e){if(Box!=null)Box.SetBounds(Padding.Left,(Height-Box.PreferredHeight)/2,Width-Padding.Horizontal,Box.PreferredHeight);base.OnLayout(e);}
  protected override void OnPaintBackground(PaintEventArgs e){Theme.Shape(e.Graphics,this,6,Theme.Surface,Box!=null&&Box.Focused?Theme.Ink:Theme.Line);}
 }
 public class ToggleButton:ActionButton {
  // Two-state button sharing the ActionButton look; Active marks the "on" state.
  public event EventHandler CheckedChanged;
  public ToggleButton(){AccessibleRole=AccessibleRole.CheckButton;AccessibleDescription="Desativado";}
  protected override AccessibleObject CreateAccessibilityInstance(){return new ToggleAccessible(this);}
  class ToggleAccessible:ButtonBaseAccessibleObject{readonly ToggleButton owner;public ToggleAccessible(ToggleButton o):base(o){owner=o;}public override AccessibleStates State{get{return owner.Checked?base.State|AccessibleStates.Checked:base.State;}}}
  public bool Checked{get{return Active;}set{if(Active==value)return;Active=value;AccessibleDescription=value?"Ativado":"Desativado";Invalidate();if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);}}
  protected override void OnClick(EventArgs e){Checked=!Checked;base.OnClick(e);}
 }
 public class Card:Panel {
  public Card(){DoubleBuffered=true;BackColor=Theme.Surface;Padding=new Padding(18,12,18,12);}
  protected override void OnPaintBackground(PaintEventArgs e){Theme.Shape(e.Graphics,this,10,Theme.Surface,Theme.Line);}
 }
 public class TimeInput:UserControl {
  NumericUpDown hour,minute;bool updating;public event EventHandler ValueChanged;
  public TimeInput(){Size=new Size(140,34);BackColor=Theme.Surface;hour=new NumericUpDown{Minimum=0,Maximum=23,Location=new Point(0,0),Size=new Size(58,34),Font=new Font("Segoe UI",12),AccessibleName="Hora"};minute=new NumericUpDown{Minimum=0,Maximum=59,Location=new Point(78,0),Size=new Size(58,34),Font=new Font("Segoe UI",12),AccessibleName="Minuto"};Controls.Add(hour);Controls.Add(minute);Controls.Add(Theme.Label(":",61,3,16,28,12,true));hour.ValueChanged+=Changed;minute.ValueChanged+=Changed;Theme.Apply(this);}
  void Changed(object sender,EventArgs e){if(!updating&&ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}
  public DateTime Value{get{return DateTime.Today.AddHours((int)hour.Value).AddMinutes((int)minute.Value);}set{updating=true;hour.Value=value.Hour;minute.Value=value.Minute;updating=false;Changed(this,EventArgs.Empty);}}
 }
}
