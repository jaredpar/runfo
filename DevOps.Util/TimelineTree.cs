#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DevOps.Util;

namespace DevOps.Util
{
    public sealed class TimelineTree
    {
        public sealed class TimelineNode
        {
            public TimelineRecord TimelineRecord { get; } 

            public TimelineNode? ParentNode { get; internal set; }

            public List<TimelineNode> Children { get; internal set; }

            public string Id => TimelineRecord.Id;

            public string Name => TimelineRecord.Name;

            public int Count => 1 + Children.Sum(x => x.Count);

            public TimelineNode(TimelineRecord record)
            {
                TimelineRecord = record;
                Children = new List<TimelineNode>();
            }

            public TimelineNode(TimelineRecord record, TimelineNode parentNode, List<TimelineNode> children)
            {
                TimelineRecord = record;
                ParentNode = parentNode;
                Children = children;
            }

            public override string ToString() => TimelineRecord.ToString();
        }

        private Dictionary<string, TimelineNode> IdToNodeMap { get; }

        public int Attempt { get; }

        public Timeline Timeline { get; }

        public List<TimelineNode> RootNodes { get; } 

        public IEnumerable<TimelineRecord> Roots => RootNodes.Select(x => x.TimelineRecord);

        public IEnumerable<TimelineNode> Nodes => IdToNodeMap.Values;

        public IEnumerable<TimelineRecord> Records => IdToNodeMap.Values.Select(x => x.TimelineRecord);

        public IEnumerable<TimelineNode> JobNodes => Nodes.Where(x => IsJob(x.Id));

        public IEnumerable<TimelineRecord> Jobs => JobNodes.Select(x => x.TimelineRecord);
        public int Count => RootNodes.Sum(x => x.Count);

        public TimelineTree(Timeline timeline, List<TimelineNode> rootNodes, Dictionary<string, TimelineNode> idToNodeMap)
        {
            Timeline = timeline;
            Attempt = timeline.GetAttempt();
            RootNodes = rootNodes;
            IdToNodeMap = idToNodeMap;
        }

        public bool IsRoot(string id) => RootNodes.Any(x => x.TimelineRecord.Id == id);

        public bool IsJob(string id) =>
            IdToNodeMap.TryGetValue(id, out var node) &&
            node.ParentNode is object &&
            IsRoot(node.ParentNode.TimelineRecord.Id);

        public bool TryGetParent(TimelineRecord record, [NotNullWhen(true)] out TimelineRecord? parent)
        {
            if (IdToNodeMap.TryGetValue(record.Id, out var node) &&
                node.ParentNode is object)
            {
                parent = node.ParentNode.TimelineRecord;
                return true;
            }

            parent = null;
            return false;
        }

        public bool TryGetNode(string id, [NotNullWhen(true)] out TimelineNode? node) => 
            IdToNodeMap.TryGetValue(id, out node);

        public bool TryGetNode(TimelineRecord record, [NotNullWhen(true)] out TimelineNode? node) => 
            IdToNodeMap.TryGetValue(record.Id, out node);

        public bool TryGetJob(TimelineRecord record, [NotNullWhen(true)] out TimelineRecord? job) =>
            TryFindUp(
                record.Id,
                node => IsJob(node.TimelineRecord.Id),
                out job);

        public bool TryGetRoot(TimelineRecord record, [NotNullWhen(true)] out TimelineRecord? root) =>
            TryFindUp(
                record.Id,
                node => IsRoot(node.TimelineRecord.Id),
                out root);

        private bool TryFindUp(string id, Func<TimelineNode, bool> predicate, [NotNullWhen(true)] out TimelineRecord? record)
        {
            if (TryFindUp(id, predicate, out TimelineNode? node))
            {
                record = node.TimelineRecord;
                return true;
            }

            record = null;
            return false;
        }

        private bool TryFindUp(string id, Func<TimelineNode, bool> predicate, [NotNullWhen(true)] out TimelineNode? node)
        {
            node = null;
            if (!IdToNodeMap.TryGetValue(id, out var current))
            {
                return false;
            }

            do
            {
                if (predicate(current))
                {
                    node = current;
                    return true;
                }

                current = current.ParentNode;
            } while (current is object);

            return false;
        }

        public TimelineTree Filter(Func<TimelineRecord, bool> predicate)
        {
            return new TimelineTree(Timeline, FilterList(newParentNode: null, RootNodes), IdToNodeMap);

            TimelineNode? FilterNode(TimelineNode node, TimelineNode? newParentNode)
            {
                var newNode = new TimelineNode(node.TimelineRecord);
                newNode.ParentNode = newParentNode;
                newNode.Children = FilterList(newNode, node.Children);

                return (newNode.Children.Count > 0 || predicate(node.TimelineRecord))
                    ? newNode
                    : null;
            }

            List<TimelineNode> FilterList(TimelineNode? newParentNode, List<TimelineNode> list)
            {
                var newList = new List<TimelineNode>();
                foreach (var node in list)
                {
                    var filteredNode = FilterNode(node, newParentNode);
                    if (filteredNode is object)
                    {
                        newList.Add(filteredNode);
                    }
                }

                return newList;
            }
        }

        public static TimelineTree Create(Timeline timeline)
        {
            var records = timeline.Records;
            var idComparer = StringComparer.OrdinalIgnoreCase;
            var recordMap = CreateRecordMap();
            var nodeMap = new Dictionary<string, TimelineNode>(idComparer);

            // Each stage will have a different root
            var roots = new List<TimelineNode>();
            foreach (var record in records)
            {
                var node = GetOrCreateNode(record.Id);
                Debug.Assert(object.ReferenceEquals(node.TimelineRecord, record));

                if (string.IsNullOrEmpty(record.ParentId))
                {
                    roots.Add(node);
                }
                else
                {
                    var parentNode = GetOrCreateNode(record.ParentId);
                    parentNode.Children.Add(node);
                    node.ParentNode = parentNode;
                }
            }

            // Now look for hidden roots
            foreach (var value in nodeMap.Values)
            {
                if (value.ParentNode is null && !roots.Contains(value))
                {
                    roots.Add(value);
                }
            }

            // TODO sort by start time, not name. The tree should reflect execution order
            var comparer = StringComparer.OrdinalIgnoreCase;
            foreach (var value in nodeMap.Values)
            {
                value.Children.Sort(Compare);
            }

            roots.Sort(Compare);

            var tree = new TimelineTree(timeline, roots, nodeMap);
            Debug.Assert(tree.Count == timeline.Records.Length);
            return tree;

            TimelineNode GetOrCreateNode(string id)
            {
                TimelineNode? node;
                if (!nodeMap.TryGetValue(id, out node))
                {
                    var record = recordMap[id];
                    node = new TimelineNode(record);
                    nodeMap.Add(id, node);
                }

                return node;
            } 

            Dictionary<string, TimelineRecord> CreateRecordMap()
            {
                var map = new Dictionary<string, TimelineRecord>(idComparer);
                foreach (var record in timeline.Records)
                {
                    map[record.Id] = record;
                }

                return map;
            }

            static int Compare(TimelineNode x, TimelineNode y)
            {
                var xStart = DevOpsUtil.ConvertFromRestTime(x.TimelineRecord.StartTime);
                var yStart = DevOpsUtil.ConvertFromRestTime(y.TimelineRecord.StartTime);
                if (xStart is null)
                {
                    if (yStart is null)
                    {
                        return 0;
                    }

                    return -1;
                }

                if (yStart is null)
                {
                    return 1;
                }

                return xStart.Value.CompareTo(yStart.Value);
            }
        }
    }
}