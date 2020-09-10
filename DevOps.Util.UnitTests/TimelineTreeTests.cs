using DevOps.Util;
using Newtonsoft.Json;
using System;
using System.Linq;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class TimelineTreeTests
    {
        [Fact]
        public void Roots()
        {
            var timeline = ResourceUtil.GetTimeline("timeline-1.json");
            var tree = TimelineTree.Create(timeline);
            Assert.Single(tree.Roots);
        }

        [Fact]
        public void Jobs()
        {
            var timeline = ResourceUtil.GetTimeline("timeline-1.json");
            var tree = TimelineTree.Create(timeline);
            var jobs = tree.Nodes.Where(x => tree.IsJob(x.TimelineRecord.Id));
            Assert.Equal(142, jobs.Count());
        }

        [Fact]
        public void TryGetJob()
        {
            var timeline = ResourceUtil.GetTimeline("timeline-1.json");
            var tree = TimelineTree.Create(timeline);
            Assert.True(tree.TryGetNode("dfefcd06-03ef-5951-c8ec-02f90019bee7", out var node));
            Assert.True(tree.TryGetJob(node!.TimelineRecord, out var job));
            Assert.Equal("CoreCLR Common Pri0 Test Build Windows_NT arm64 checked", job!.Name);
        }

        [Fact]
        public void Repro1()
        {
            var timeline = ResourceUtil.GetTimeline("timeline-2-attempt-1.json");
            var tree = TimelineTree.Create(timeline);
        }
    }
}