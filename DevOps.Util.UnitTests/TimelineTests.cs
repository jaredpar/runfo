using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public sealed class TimelineTests
    {
        /// <summary>
        /// This timeline requires multi-patching to repair earlier timelines. That means essentially that we have 
        /// to get multiple previous attempt calls to fully patch the timeline data.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task GetAttemptOneMultiPatch()
        {
            var handler = new TestableHttpMessageHandler();
            handler.AddJson(
                "https://dev.azure.com/dnceng/public/_apis/build/builds/799195/timeline?api-version=5.0",
                ResourceUtil.GetJsonFile("timeline2.json"));
            handler.AddJson(
                "https://dev.azure.com/dnceng/public/_apis/build/builds/799195/timeline/5c657fa2-65d2-55e4-f9c9-8fbd97185e37?api-version=5.0",
                ResourceUtil.GetJsonFile("timeline2-part1.json")) ;
            handler.AddJson(
                "https://dev.azure.com/dnceng/public/_apis/build/builds/799195/timeline/0254bc91-8160-5748-ab06-8ddd0ae17493?api-version=5.0",
                ResourceUtil.GetJsonFile("timeline2-part2.json")) ;
            handler.AddJson(
                "https://dev.azure.com/dnceng/public/_apis/build/builds/799195/timeline/b02ddad0-c847-5898-a3e9-29c2612003f0?api-version=5.0",
                ResourceUtil.GetJsonFile("timeline2-part3.json")) ;

            var server = new DevOpsServer("dnceng", httpClient: new HttpClient(handler));
            var timeline = await server.GetTimelineAttemptAsync("public", 799195, attempt: 1);
            Assert.NotNull(timeline);
            var timelineTree = TimelineTree.Create(timeline!);
            Assert.NotNull(timelineTree);
        }

        /// <summary>
        /// Get attempt one when there are jobs that appear in attempt 2 that don't appear at all in attempt 1
        /// </summary>
        [Fact]
        public async Task GetAttemptOneMissingRecords()
        {
            var handler = new TestableHttpMessageHandler();
            handler.AddJson(
                "https://dev.azure.com/dnceng/public/_apis/build/builds/795146/timeline?api-version=5.0",
                ResourceUtil.GetJsonFile("timeline3.json"));
            handler.AddJson(
                "https://dev.azure.com/dnceng/public/_apis/build/builds/795146/timeline/96ac2280-8cb4-5df5-99de-dd2da759617d?api-version=5.0",
                ResourceUtil.GetJsonFile("timeline3-part1.json"));

            var server = new DevOpsServer("dnceng", httpClient: new HttpClient(handler));
            var timeline = await server.GetTimelineAttemptAsync("public", 795146, attempt: 1);
            Assert.NotNull(timeline);
            var timelineTree = TimelineTree.Create(timeline!);
            Assert.NotNull(timelineTree);
            Assert.True(timelineTree.TryGetNode("711d63f1-27de-5afd-fdbe-3b9edb784e9f", out var node));
            Assert.Equal(TaskResult.Failed, node!.TimelineRecord.Result);
        }
    }
}
