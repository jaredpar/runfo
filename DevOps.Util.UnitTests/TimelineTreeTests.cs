using DevOps.Util;
using Newtonsoft.Json;
using System;
using System.Linq;
using Xunit;

namespace Query.Test
{
    public class TimelineTreeTests
    {
        private static Timeline GetTimeline(string resourceFileName)
        {
            var json = ResourceUtil.GetJsonFile(resourceFileName);
            return JsonConvert.DeserializeObject<Timeline>(json);
        }

        [Fact]
        public void Roots()
        {
            var timeline = GetTimeline("timeline-1.json");
            var tree = TimelineTree.Create(timeline);
            Assert.Single(tree.Roots);
        }

        [Fact]
        public void Jobs()
        {
            var timeline = GetTimeline("timeline-1.json");
            var tree = TimelineTree.Create(timeline);
            var jobs = tree.Nodes.Where(x => tree.IsJob(x.TimelineRecord.Id));
            Assert.Equal(143, jobs.Count());
        }

        [Fact]
        public void TryGetJob()
        {
            var timeline = GetTimeline("timeline-1.json");
            var tree = TimelineTree.Create(timeline);
            Assert.True(tree.TryGetNode("dfefcd06-03ef-5951-c8ec-02f90019bee7", out var node));
            Assert.True(tree.TryGetJob(node.TimelineRecord, out var job));
            Assert.Equal("CoreCLR Common Pri0 Test Build Windows_NT arm64 checked", job.Name);
        }
    }
}