using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FocoLocal {
 public static class Artwork {
  public static GraphicsPath Rounded(RectangleF r,float radius) {
   float d=radius*2;var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
  }
  public static void Logo(Graphics g,RectangleF rect,bool permanent) {
   var state=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(rect.X,rect.Y);g.ScaleTransform(rect.Width/100,rect.Height/100);
   using(var path=Rounded(new RectangleF(2,2,96,96),14))using(var brush=new SolidBrush(Color.FromArgb(24,25,29)))using(var outline=new Pen(Color.FromArgb(142,146,156),2)){g.FillPath(brush,path);g.DrawPath(outline,path);}
   if(permanent) {
    using(var p=new GraphicsPath()){p.AddLines(new[]{new PointF(50,20),new PointF(77,31),new PointF(74,58)});p.AddBezier(74,58,70,73,58,80,50,84);p.AddBezier(50,84,42,80,30,73,26,58);p.AddLine(26,58,23,31);p.CloseFigure();using(var b=new SolidBrush(Color.White))g.FillPath(b,p);}
    using(var p=new Pen(Color.FromArgb(24,25,29),7)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLine(p,39,52,61,52);}
   } else {
    using(var p=new Pen(Color.FromArgb(242,241,238),7)){p.LineJoin=LineJoin.Miter;g.DrawLines(p,new[]{new PointF(25,28),new PointF(50,75),new PointF(75,28)});}
    using(var p=new Pen(Color.FromArgb(142,146,156),3))g.DrawLine(p,37,25,63,25);
   }
   g.Restore(state);
  }
  public static void Glyph(Graphics g,string name,RectangleF rect,Color color) {
   var state=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(rect.X,rect.Y);g.ScaleTransform(rect.Width/24,rect.Height/24);
   using(var p=new Pen(color,1.9f)){p.StartCap=p.EndCap=LineCap.Round;p.LineJoin=LineJoin.Round;
    switch(name){
     case "add":g.DrawLine(p,12,5,12,19);g.DrawLine(p,5,12,19,12);break;
     case "edit":g.DrawLines(p,new[]{new Point(5,16),new Point(16,5),new Point(20,9),new Point(9,20),new Point(4,21),new Point(5,16)});g.DrawLine(p,13,8,17,12);break;
     case "delete":g.DrawLine(p,5,6,19,6);g.DrawLine(p,9,3,15,3);g.DrawLines(p,new[]{new Point(7,6),new Point(8,21),new Point(16,21),new Point(17,6)});g.DrawLine(p,11,10,11,17);g.DrawLine(p,14,10,14,17);break;
     case "clock":g.DrawEllipse(p,3,3,18,18);g.DrawLine(p,12,7,12,12);g.DrawLine(p,12,12,16,14);break;
     case "link":g.DrawRectangle(p,3,4,18,13);g.DrawLine(p,9,21,15,21);g.DrawLine(p,12,17,12,21);break;
     case "shield":g.DrawLines(p,new[]{new Point(12,2),new Point(21,6),new Point(19,16),new Point(12,22),new Point(5,16),new Point(3,6),new Point(12,2)});g.DrawLine(p,8,12,11,15);g.DrawLine(p,11,15,16,9);break;
     case "save":g.DrawLine(p,4,12,9,17);g.DrawLine(p,9,17,20,6);break;
     case "close":g.DrawLine(p,6,6,18,18);g.DrawLine(p,6,18,18,6);break;
     case "archive":g.DrawRectangle(p,3,4,18,4);g.DrawRectangle(p,5,8,14,13);g.DrawLine(p,9,12,15,12);break;
    }
   }g.Restore(state);
  }
 }
}
