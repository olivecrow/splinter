using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Splinter.McpRemediationQualification
{
    public class RemediationTests
    {
        private static Assembly Mcp => typeof(MCPForUnity.Editor.Tools.ExecuteCode).Assembly;
        private static object Construct(Type type, params object[] args) => Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);

        [Test]
        public void DetailedTestResultsRoundTripWithImmutableConstructors()
        {
            var summaryType = Mcp.GetType("MCPForUnity.Editor.Services.TestRunSummary", true);
            var itemType = Mcp.GetType("MCPForUnity.Editor.Services.TestRunTestResult", true);
            var resultType = Mcp.GetType("MCPForUnity.Editor.Services.TestRunResult", true);
            var summary = Construct(summaryType, 2, 1, 1, 0, 1.25, "Failed");
            var items = Array.CreateInstance(itemType, 2);
            items.SetValue(Construct(itemType, "Pass", "Exact.Pass", "Passed", 0.5, "", "", "retained output"), 0);
            items.SetValue(Construct(itemType, "Fail", "Exact.Fail", "Failed", 0.75, "retained failure", "stack", "output"), 1);
            var result = Construct(resultType, summary, items);
            var text = JsonConvert.SerializeObject(result);
            var restored = JsonConvert.DeserializeObject(text, resultType);
            Assert.AreEqual(text, JsonConvert.SerializeObject(restored));
            Assert.AreEqual(2, ((IList)resultType.GetProperty("Results").GetValue(restored)).Count);
        }

        [Test]
        public void UnsafeRequestIsRejectedBeforeScheduling()
        {
            var type = Mcp.GetType("MCPForUnity.Editor.Tools.ExecuteCodeJobs", true);
            var reply = type.GetMethod("Start").Invoke(null, new object[]
                { new JObject { ["request_id"] = "../bad", ["code"] = "return 1;" } });
            Assert.IsFalse(JObject.FromObject(reply).Value<bool>("success"));
        }

        [Test]
        public void UnknownJobDoesNotRunCode()
        {
            var type = Mcp.GetType("MCPForUnity.Editor.Tools.ExecuteCodeJobs", true);
            var reply = type.GetMethod("Get").Invoke(null, new object[]
                { new JObject { ["request_id"] = "qualification-no-such-job" } });
            Assert.IsFalse(JObject.FromObject(reply).Value<bool>("success"));
        }
    }
}
