using System;
using System.IO;
using System.Text;

namespace FocoLocal
{
    public static class NativeHost
    {
        static byte[] Read(Stream input, int count)
        {
            byte[] bytes = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = input.Read(bytes, offset, count - offset);
                if (read == 0)
                    return null;
                offset += read;
            }

            return bytes;
        }

        static byte[] ReadMessage(Stream input)
        {
            byte[] header = Read(input, 4);
            if (header == null)
                return null;
            int size = BitConverter.ToInt32(header, 0);
            if (size < 2 || size > 16384)
                return null;
            return Read(input, size);
        }

        static Sample ParseSample(byte[] bytes)
        {
            var message = Store.Json.Deserialize<Sample>(Encoding.UTF8.GetString(bytes));
            if (message == null || message.type != "sample" || (message.browser != "Chrome" && message.browser != "Edge"))
                throw new Exception("Mensagem inválida.");
            return message;
        }

        static void WriteReply(Stream output, object result)
        {
            byte[] response = Encoding.UTF8.GetBytes(Store.Json.Serialize(result));
            output.Write(BitConverter.GetBytes(response.Length), 0, 4);
            output.Write(response, 0, response.Length);
            output.Flush();
        }

        public static void Main()
        {
            var input = Console.OpenStandardInput();
            var output = Console.OpenStandardOutput();
            string previousHost = "";
            double previousTime = 0;
            byte[] bytes;
            while ((bytes = ReadMessage(input)) != null)
            {
                object result;
                try
                {
                    var message = ParseSample(bytes);
                    string host = message.active ? Engine.Normalize(message.domain) : "";
                    DateTime now = DateTime.UtcNow;
                    double stamp = Engine.Stamp(now);
                    result = Store.With(state =>
                    {
                        Engine.Charge(state, previousHost, previousTime, stamp, now);
                        state.seen[message.browser] = stamp;
                        state.ready[message.browser] = message.ready;
                        return Engine.BrowserView(state, now);
                    }, true);
                    previousTime = stamp;
                    previousHost = host;
                }
                catch (Exception error)
                {
                    previousHost = "";
                    result = new
                    {
                        ok = false,
                        error = error.Message
                    };
                }

                WriteReply(output, result);
            }
        }
    }
}
