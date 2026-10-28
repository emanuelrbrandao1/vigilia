using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
using FocoLocal;
public class MakeAssets {
 public static void Main() {
  string root=AppDomain.CurrentDomain.BaseDirectory;Directory.CreateDirectory(Path.Combine(root,"assets"));Directory.CreateDirectory(Path.Combine(root,@"extensao\icons"));
  foreach(bool permanent in new[]{false,true}) {
   int[] sizes={16,20,24,32,40,48,64,128,256};var images=new List<byte[]>();
   foreach(int size in sizes)using(var b=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(b))using(var stream=new MemoryStream()) {
    g.Clear(Color.Transparent);Artwork.Logo(g,new RectangleF(0,0,size,size),permanent);b.Save(stream,ImageFormat.Png);images.Add(stream.ToArray());
    if(!permanent)b.Save(Path.Combine(root,@"extensao\icons\foco-"+size+".png"),ImageFormat.Png);
   }
   using(var file=File.Create(Path.Combine(root,"assets",permanent?"permanente.ico":"foco.ico")))using(var writer=new BinaryWriter(file)){
    writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
    for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
    foreach(var bytes in images)writer.Write(bytes);
   }
  }
 }
}
