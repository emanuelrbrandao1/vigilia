using System;
using System.IO;
using System.Text;

namespace FocoLocal {
 public static class NativeHost {
  static byte[] Read(Stream input,int count) {
   byte[] bytes=new byte[count]; int offset=0,n;
   while(offset<count) { n=input.Read(bytes,offset,count-offset); if(n==0) return null; offset+=n; }
   return bytes;
  }
  public static void Main() {
   var input=Console.OpenStandardInput(); var output=Console.OpenStandardOutput();
   string previousHost=""; double previousTime=0;
   while(true) {
    byte[] length=Read(input,4); if(length==null) break;
    int size=BitConverter.ToInt32(length,0); if(size<2 || size>16384) break;
    byte[] bytes=Read(input,size); if(bytes==null) break;
    object result;
    try {
     var message=Store.Json.Deserialize<Sample>(Encoding.UTF8.GetString(bytes));
     if(message==null || message.type!="sample" || (message.browser!="Chrome" && message.browser!="Edge")) throw new Exception("Mensagem inválida.");
     string host=message.active ? Engine.Normalize(message.domain) : "";
     DateTime now=DateTime.UtcNow; double stamp=Engine.Stamp(now);
     result=Store.With(s=> {
      Engine.Charge(s,previousHost,previousTime,stamp,now);
      s.seen[message.browser]=stamp; s.ready[message.browser]=message.ready;
      return Engine.View(s,now);
     },true);
     previousTime=stamp; previousHost=host;
    } catch(Exception e) { previousHost=""; result=new {ok=false,error=e.Message}; }
    byte[] response=Encoding.UTF8.GetBytes(Store.Json.Serialize(result));
    output.Write(BitConverter.GetBytes(response.Length),0,4); output.Write(response,0,response.Length); output.Flush();
   }
  }
 }
}
