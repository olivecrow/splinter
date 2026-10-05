using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPForUnity.Editor.Tools
{
    // This is an external Coplay patch, not a Splinter workspace API.
    internal static class ExecuteCodeJobs
    {
        internal const string PatchVersion = "splinter-mcp-remediation-v1";
        private const string SessionKey = "MCPForUnity.ExecuteCodeJobs.SplinterV1";
        private const int MaxJobs = 10;
        private const int MaxResultBytes = 1024 * 1024;
        private static readonly Dictionary<string, Job> Jobs = Restore();

        private sealed class Job
        {
            public string job_id;
            public string request_id;
            public string fingerprint;
            public string status;
            public long started_unix_ms;
            public long? finished_unix_ms;
            public JToken result;
            public string error;
        }

        private static Dictionary<string, Job> Restore()
        {
            var jobs = new Dictionary<string, Job>(StringComparer.Ordinal);
            var text = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(text)) return jobs;
            var saved = JsonConvert.DeserializeObject<List<Job>>(text)
                ?? throw new InvalidOperationException("Code job state is corrupt; preserve it.");
            if (saved.Count > MaxJobs) throw new InvalidOperationException("Code job state exceeds the limit.");
            foreach (var job in saved)
            {
                if (job == null || !ValidKey(job.request_id) ||
                    !Guid.TryParseExact(job.job_id, "N", out _) ||
                    string.IsNullOrEmpty(job.fingerprint) || jobs.ContainsKey(job.request_id))
                    throw new InvalidOperationException("Code job state is corrupt; preserve it.");
                if (job.status == "queued" || job.status == "running")
                {
                    // Reload is not authorization to repeat arbitrary code.
                    job.status = "interrupted";
                    job.error = "Domain reload interrupted the job; inspect effects before an explicit new request.";
                }
                jobs.Add(job.request_id, job);
            }
            return jobs;
        }

        private static bool ValidKey(string key) => key != null &&
            Regex.IsMatch(key, @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z");

        private static void Save() => SessionState.SetString(SessionKey,
            JsonConvert.SerializeObject(Jobs.Values.OrderBy(j => j.request_id)));

        private static object Response(Job job) => new SuccessResponse("Code job state", new
        {
            job.job_id, job.request_id, job.fingerprint, job.status,
            job.started_unix_ms, job.finished_unix_ms, job.result, job.error,
            patch_version = PatchVersion
        });

        public static object Start(JObject parameters)
        {
            var key = parameters.Value<string>("request_id");
            var code = parameters.Value<string>("code");
            if (!ValidKey(key)) return new ErrorResponse("A bounded request_id is required.");
            if (string.IsNullOrWhiteSpace(code) || code.Length > 50000)
                return new ErrorResponse("Code must be nonempty and at most 50000 characters.");
            var command = new JObject
            {
                ["action"] = "execute", ["code"] = code,
                ["compiler"] = parameters.Value<string>("compiler") ?? "auto",
                ["safety_checks"] = parameters.Value<bool?>("safety_checks") ?? true
            };
            string fingerprint;
            using (var hash = SHA256.Create())
                fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                    command.ToString(Formatting.None)))).Replace("-", "").ToLowerInvariant();
            if (Jobs.TryGetValue(key, out var prior))
                return prior.fingerprint == fingerprint ? Response(prior) :
                    new ErrorResponse("CodeJobRequestConflict: request_id is bound to different input.");
            if (Jobs.Count >= MaxJobs)
                return new ErrorResponse("CodeJobCapacity: preserve existing jobs; no automatic eviction or replay.");
            var job = new Job
            {
                job_id = Guid.NewGuid().ToString("N"), request_id = key,
                fingerprint = fingerprint, status = "queued",
                started_unix_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            Jobs.Add(key, job);
            try { Save(); }
            catch { Jobs.Remove(key); throw; }
            // Acknowledge the session intent before expensive main-thread code. delayCall
            // waits for Inspector updates and can stall in an unfocused Editor.
            EditorApplication.CallbackFunction runOnce = null;
            runOnce = () =>
            {
                EditorApplication.update -= runOnce;
                Run(job, command);
            };
            EditorApplication.update += runOnce;
            return Response(job);
        }

        public static object Get(JObject parameters)
        {
            var key = parameters.Value<string>("request_id");
            var id = parameters.Value<string>("job_id");
            var job = key != null && Jobs.TryGetValue(key, out var found) ? found :
                Jobs.Values.SingleOrDefault(j => j.job_id == id);
            if (job == null) return new ErrorResponse("Unknown code job in this Editor session.");
            if (id != null && id != job.job_id) return new ErrorResponse("CodeJobTargetMismatch");
            return Response(job);
        }

        private static void Run(Job job, JObject command)
        {
            if (job.status != "queued") return;
            job.status = "running";
            try
            {
                Save();
                var response = ExecuteCode.HandleCommand(command);
                var result = response == null ? JValue.CreateNull() : JToken.FromObject(response);
                if (Encoding.UTF8.GetByteCount(result.ToString(Formatting.None)) > MaxResultBytes)
                {
                    job.status = "completed_result_unavailable";
                    job.error = "Code ran; result exceeds 1 MiB. Inspect effects; do not repeat the request.";
                }
                else
                {
                    job.result = result;
                    job.status = result.Value<bool?>("success") == true ? "succeeded" : "failed";
                }
            }
            catch (Exception ex)
            {
                job.status = "failed";
                job.error = ex.Message;
            }
            finally
            {
                job.finished_unix_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Save();
            }
        }
    }
}
