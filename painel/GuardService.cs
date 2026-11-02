using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace FocoLocal
{
    public class GuardService : ServiceBase
    {
        readonly object gate = new object ();
        volatile bool stopping;
        Thread server;
        Timer timer;
        NamedPipeServerStream listener;
        readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FocoLocalGuard");
        readonly string hosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        GuardState state;
        string sid;
        string error = "";
        public GuardService()
        {
            ServiceName = "FocoLocalGuard";
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        void Save()
        {
            Save(state);
        }

        void Save(GuardState candidate)
        {
            string path = Path.Combine(root, "state.json");
            string temp = path + ".tmp";
            File.WriteAllText(temp, GuardStorage.Json.Serialize(candidate), new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temp, path, path + ".bak");
            else
                File.Move(temp, path);
        }

        protected override void OnStart(string[] args)
        {
            sid = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user.sid")).Trim();
            state = LoadState();
            stopping = false;
            Tick(null);
            timer = new Timer(Tick, null, 1000, 1000);
            server = new Thread(Serve)
            {
                IsBackground = true
            };
            server.Start();
        }

        GuardState LoadState()
        {
            string path = Path.Combine(root, "state.json");
            var loaded = File.Exists(path) ? GuardStorage.Json.Deserialize<GuardState>(File.ReadAllText(path)) : new GuardState();
            if (loaded == null || loaded.pauses == null || loaded.held == null)
                throw new Exception("Estado inválido; backup preservado.");
            return loaded;
        }

        void Tick(object unused)
        {
            lock (gate)
            {
                try
                {
                    if (stopping)
                        return;
                    UpdateHosts(false);
                    error = "";
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            }
        }

        void UpdateHosts(bool restoreOnly)
        {
            double now = Engine.Stamp(DateTime.UtcNow);
            bool activePause = state.pauses.Any(pause => !restoreOnly && !pause.cancelled && now >= pause.startsAt && now < pause.endsAt);
            if (state.held.Count == 0 && !activePause)
                return;
            using (var file = new FileStream(hosts, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                byte[] before = ReadHosts(file);
                int skip;
                Encoding encoding = DetectEncoding(before, out skip);
                string original = encoding.GetString(before, skip, before.Length - skip);
                string updated = GuardHosts.Transform(original, state, now, restoreOnly);
                // Persist the restoration journal BEFORE removing any hosts line.
                if (updated != original)
                {
                    Save();
                    byte[] after = encoding.GetPreamble().Concat(encoding.GetBytes(updated)).ToArray();
                    WriteHostsWithRollback(file, before, after);
                }

                int heldCount = state.held.Count;
                GuardHosts.Finish(state, now, restoreOnly);
                if (heldCount != state.held.Count)
                    Save();
                if (updated != original)
                    DnsFlushResolverCache();
            }
        }

        static byte[] ReadHosts(FileStream file)
        {
            if (file.Length > 4 * 1024 * 1024)
                throw new IOException("Arquivo hosts excede o limite suportado.");
            var bytes = new byte[(int)file.Length];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = file.Read(bytes, offset, bytes.Length - offset);
                if (read == 0)
                    throw new IOException("Leitura incompleta.");
                offset += read;
            }

            return bytes;
        }

        static Encoding DetectEncoding(byte[] bytes, out int skip)
        {
            skip = 0;
            if (bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191)
            {
                skip = 3;
                return new UTF8Encoding(true, true);
            }

            if (bytes.Length >= 2 && bytes[0] == 255 && bytes[1] == 254)
            {
                skip = 2;
                return Encoding.Unicode;
            }

            if (bytes.Length >= 2 && bytes[0] == 254 && bytes[1] == 255)
            {
                skip = 2;
                return Encoding.BigEndianUnicode;
            }

            return new UTF8Encoding(false, true);
        }

        static void WriteHostsWithRollback(FileStream file, byte[] before, byte[] after)
        {
            try
            {
                WriteHosts(file, after);
            }
            catch
            {
                WriteHosts(file, before);
                throw;
            }
        }

        static void WriteHosts(FileStream file, byte[] bytes)
        {
            file.Position = 0;
            file.Write(bytes, 0, bytes.Length);
            file.SetLength(bytes.Length);
            file.Flush(true);
        }

        [System.Runtime.InteropServices.DllImport("dnsapi.dll", SetLastError = true)]
        static extern bool DnsFlushResolverCache();
        void Serve()
        {
            while (!stopping)
            {
                try
                {
                    ServeConnection();
                }
                catch
                {
                    if (stopping)
                        return;
                    Thread.Sleep(250);
                }
            }
        }

        void ServeConnection()
        {
            using (var pipe = CreateListener())
            {
                listener = pipe;
                pipe.WaitForConnection();
                if (stopping)
                    return;
                GuardWire.Send(pipe, ReadReply(pipe));
            }
        }

        NamedPipeServerStream CreateListener()
        {
            var acl = new PipeSecurity();
            acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                PipeAccessRights.FullControl, AccessControlType.Allow));
            acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid), PipeAccessRights.ReadWrite, AccessControlType.Allow));
            return new NamedPipeServerStream(GuardWire.Pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, acl);
        }

        GuardReply ReadReply(Stream pipe)
        {
            try
            {
                var request = GuardWire.Receive<GuardRequest>(pipe);
                lock (gate)
                {
                    return Dispatch(request);
                }
            }
            catch (Exception ex)
            {
                return new GuardReply
                {
                    ok = false,
                    error = ex.Message
                };
            }
        }

        GuardReply Dispatch(GuardRequest request)
        {
            if (request == null)
                throw new Exception("Pedido inválido.");
            if (request.command == "request")
                return AcceptRequest(request);
            if (request.command == "cancel")
                return CancelRequest(request.id);
            if (request.command == "status")
                return new GuardReply
                {
                    ok = error == "",
                    error = error,
                    pauses = state.pauses.ToList()
                };
            throw new Exception("Comando inválido.");
        }

        GuardReply AcceptRequest(GuardRequest request)
        {
            if (error != "")
                throw new Exception(error);
            state = GuardStorage.Accept(state, request, DateTime.UtcNow, Save);
            return new GuardReply
            {
                ok = true,
                pause = state.pauses.First(pause => pause.id == request.id)
            };
        }

        GuardReply CancelRequest(string id)
        {
            var pause = state.pauses.FirstOrDefault(item => item.id == id);
            if (pause != null)
            {
                pause.cancelled = true;
                Save();
                UpdateHosts(false);
            }

            return new GuardReply
            {
                ok = true,
                pause = pause
            };
        }

        protected override void OnStop()
        {
            stopping = true;
            if (timer != null)
                timer.Dispose();
            if (listener != null)
                listener.Dispose();
            lock (gate)
            {
                UpdateHosts(true);
            }
        }

        protected override void OnShutdown()
        {
            OnStop();
        }

        public static void Main()
        {
            ServiceBase.Run(new GuardService());
        }
    }
}
