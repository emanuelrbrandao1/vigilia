using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FocoLocal
{
    public class GuardRequest
    {
        public string command, id;
        public string[] domains;
    }

    public class GuardReply
    {
        public bool ok;
        public string error;
        public Pause pause;
        public List<Pause> pauses;
    }

    public class HeldLine
    {
        public string line;
        public string domain;
    }

    public class GuardState
    {
        public List<Pause> pauses = new List<Pause>();
        public List<HeldLine> held = new List<HeldLine>();
    }

    public static class GuardStorage
    {
        // The restoration journal may contain hosts lines from a file up to 4 MiB.
        // It must not share the much smaller IPC message budget.
        public static readonly JavaScriptSerializer Json = new JavaScriptSerializer
        {
            MaxJsonLength = 32 * 1024 * 1024
        };
        public static GuardState Copy(GuardState state)
        {
            return Json.Deserialize<GuardState>(Json.Serialize(state));
        }

        public static GuardState Accept(GuardState state, GuardRequest request, DateTime utc, Action<GuardState> persist)
        {
            var candidate = Copy(state);
            GuardHosts.Request(candidate, request, utc);
            persist(candidate);
            return candidate;
        }
    }

    public static class PauseRecovery
    {
        public static Pause Prepare(State state, Rule rule, DateTime utc)
        {
            var pause = Engine.LatestPause(state, rule);
            if (pause != null && pause.confirmationPending && !pause.cancelled && !pause.cancelRequested)
                return pause;
            pause = Engine.RequestPause(state, rule, utc);
            pause.windowsManaged = true;
            pause.confirmationPending = true;
            return pause;
        }

        public static void Merge(State state, IEnumerable<Pause> remote)
        {
            if (remote == null)
                return;
            foreach (var item in remote)
            {
                var serializer = new JavaScriptSerializer();
                var pause = serializer.Deserialize<Pause>(serializer.Serialize(item));
                pause.windowsManaged = true;
                pause.confirmationPending = false;
                int index = state.pauses.FindIndex(existing => existing.id == pause.id);
                if (index >= 0)
                {
                    var previous = state.pauses[index];
                    pause.cancelled = pause.cancelled || previous.cancelled;
                    pause.cancelRequested = !pause.cancelled && previous.cancelRequested;
                    state.pauses[index] = pause;
                }
                else
                {
                    state.pauses.Add(pause);
                }
            }
        }
    }

    // Only the application's marked hosts block is eligible for temporary changes.
    public static class GuardHosts
    {
        public const string Begin = "# BEGIN BLOQUEADOR-SIMPLES-LOCAL";
        public const string End = "# END BLOQUEADOR-SIMPLES-LOCAL";
        static readonly Regex Block = new Regex(@"(?ms)^" + Regex.Escape(Begin) + @"\r?\n(.*?)^" + Regex.Escape(End) + @"(?:\r?\n|$)");
        public static string Transform(string original, GuardState state, double now, bool restoreOnly)
        {
            var blocks = ReadBlocks(original);
            string newline = original.Contains("\r\n") ? "\r\n" : "\n";
            var active = state.pauses.Where(pause => !restoreOnly && !pause.cancelled && now >= pause.startsAt && now < pause.endsAt).ToArray();
            Func<string, bool> paused = domain => active.Any(pause => pause.domains.Any(root => Engine.Match(domain, root)));
            var lines = ReadBlockLines(blocks);
            HoldPausedLines(lines, state, paused);
            RestoreExpiredLines(lines, state, paused);
            // Keep held records until the caller has successfully written hosts.
            if (blocks.Count == 0 && lines.Count == 0)
                return original;
            string replacement = Begin + newline + String.Join(newline, lines) + (lines.Count > 0 ? newline : "") + End + newline;
            if (blocks.Count == 1)
            {
                var block = blocks[0];
                return original.Substring(0, block.Index) + replacement + original.Substring(block.Index + block.Length);
            }

            return original + (original.Length > 0 && !original.EndsWith("\n") ? newline : "") + replacement;
        }

        static MatchCollection ReadBlocks(string original)
        {
            var blocks = Block.Matches(original);
            int begins = Regex.Matches(original, @"(?m)^" + Regex.Escape(Begin) + @"\r?$").Count;
            int ends = Regex.Matches(original, @"(?m)^" + Regex.Escape(End) + @"\r?$").Count;
            if (begins != blocks.Count || ends != blocks.Count || blocks.Count > 1)
                throw new Exception("Bloco hosts incompleto; restauração pendente.");
            return blocks;
        }

        static List<string> ReadBlockLines(MatchCollection blocks)
        {
            var lines = blocks.Count == 0 ? new List<string>() : Regex.Split(blocks[0].Groups[1].Value, @"\r?\n").ToList();
            if (lines.Count > 0 && lines[lines.Count - 1] == "")
                lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        static void HoldPausedLines(List<string> lines, GuardState state, Func<string, bool> paused)
        {
            foreach (string line in lines.ToArray())
            {
                var match = Regex.Match(line, @"^(?:0\.0\.0\.0|::)\s+([a-zA-Z0-9.-]+)\s*$");
                if (!match.Success)
                    continue;
                string domain = match.Groups[1].Value.ToLowerInvariant();
                if (!paused(domain))
                    continue;
                if (!state.held.Any(held => held.line == line))
                    state.held.Add(new HeldLine { line = line, domain = domain });
                lines.Remove(line);
            }
        }

        static void RestoreExpiredLines(List<string> lines, GuardState state, Func<string, bool> paused)
        {
            foreach (var held in state.held.Where(line => !paused(line.domain)).ToArray())
            {
                if (!lines.Any(line => line.Trim() == held.line.Trim()))
                    lines.Add(held.line);
            }
        }

        public static void Finish(GuardState state, double now, bool restoreOnly)
        {
            state.held.RemoveAll(held => restoreOnly || !state.pauses.Any(pause =>
                !pause.cancelled && now >= pause.startsAt && now < pause.endsAt
                && pause.domains.Any(domain => Engine.Match(held.domain, domain))));
        }

        public static Pause Request(GuardState state, GuardRequest request, DateTime utc)
        {
            Guid id;
            if (!Guid.TryParseExact(request.id, "N", out id))
                throw new Exception("Identificador inválido.");
            var existing = state.pauses.FirstOrDefault(item => item.id == request.id);
            if (existing != null)
                return existing;
            if (request.domains == null || request.domains.Length < 1 || request.domains.Length > 32)
                throw new Exception("Domínios inválidos.");
            var rule = new Rule
            {
                domains = Engine.PauseDomains(request.domains)
            };
            // Admission is transactional: a rejected request must leave the current
            // restoration state usable. Reserve room for the complete status reply.
            var candidate = new State
            {
                pauses = new List<Pause>(state.pauses)
            };
            var pause = Engine.RequestPause(candidate, rule, utc);
            pause.id = request.id;
            ValidateAdmission(candidate.pauses);
            state.pauses = candidate.pauses;
            return pause;
        }

        static void ValidateAdmission(List<Pause> pauses)
        {
            if (pauses.Count > 128 || Encoding.UTF8.GetByteCount(GuardStorage.Json.Serialize(new GuardReply { ok = true, pauses = pauses })) > 48 * 1024)
                throw new Exception("Limite de pedidos simultâneos atingido. Aguarde os pedidos anteriores expirarem.");
        }
    }

    public static class GuardWire
    {
        public const string Pipe = "FocoLocalGuard_v1";
        public static readonly JavaScriptSerializer Json = new JavaScriptSerializer
        {
            MaxJsonLength = 65536
        };
        public static byte[] Read(Stream stream, int count)
        {
            var bytes = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                var pending = stream.BeginRead(bytes, offset, count - offset, null, null);
                if (!pending.AsyncWaitHandle.WaitOne(4000))
                {
                    stream.Dispose();
                    throw new IOException("O serviço não respondeu a tempo.");
                }

                int read = stream.EndRead(pending);
                pending.AsyncWaitHandle.Close();
                if (read == 0)
                    throw new IOException("Conexão encerrada.");
                offset += read;
            }

            return bytes;
        }

        public static T Receive<T>(Stream stream)
        {
            int length = BitConverter.ToInt32(Read(stream, 4), 0);
            if (length < 2 || length > 65536)
                throw new IOException("Mensagem inválida.");
            return Json.Deserialize<T>(Encoding.UTF8.GetString(Read(stream, length)));
        }

        public static void Send(Stream stream, object value)
        {
            var data = Encoding.UTF8.GetBytes(Json.Serialize(value));
            stream.Write(BitConverter.GetBytes(data.Length), 0, 4);
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }

        public static GuardReply Call(GuardRequest request)
        {
            using (var pipe = new System.IO.Pipes.NamedPipeClientStream(
                ".", Pipe, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous))
            {
                pipe.Connect(2500);
                Send(pipe, request);
                var result = Receive<GuardReply>(pipe);
                if (!result.ok)
                    throw new Exception(result.error);
                return result;
            }
        }
    }
}
