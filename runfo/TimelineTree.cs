using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using DevOps.Util;

internal sealed class TimelineTree
{
    internal sealed class TimelineNode
    {
        internal TimelineRecord TimelineRecord { get; set; }

        internal TimelineNode ParentNode { get; set; }

        internal List<TimelineNode> Children { get; set; }

        internal int Count => 1 + Children.Sum(x => x.Count);

        internal TimelineNode()
        {
            Children = new List<TimelineNode>();
        }

        internal TimelineNode(TimelineRecord record, TimelineNode parentNode, List<TimelineNode> children)
        {
            TimelineRecord = record;
            ParentNode = parentNode;
            Children = children;
        }

        public override string ToString() => TimelineRecord.ToString();
    }

    internal Timeline Timeline { get; }

    internal List<TimelineNode> Roots { get; } 

    internal int Count => Roots.Sum(x => x.Count);

    internal TimelineTree(Timeline timeline, List<TimelineNode> roots)
    {
        Timeline = timeline;
        Roots = roots;
    }

    internal TimelineTree Filter(Func<TimelineRecord, bool> predicate)
    {
        return new TimelineTree(Timeline, FilterList(newParentNode: null, Roots));

        TimelineNode FilterNode(TimelineNode node, TimelineNode newParentNode)
        {
            var newNode = new TimelineNode();
            newNode.TimelineRecord = node.TimelineRecord;
            newNode.ParentNode = newParentNode;
            newNode.Children = FilterList(newNode, node.Children);

            return (newNode.Children.Count > 0 || predicate(node.TimelineRecord))
                ? newNode
                : null;
        }

        List<TimelineNode> FilterList(TimelineNode newParentNode, List<TimelineNode> list)
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

    internal static TimelineTree Create(Timeline timeline)
    {
        var records = timeline.Records;
        var map = new Dictionary<string, TimelineNode>();

        // Each stage will have a different root
        var roots = new List<TimelineNode>();
        foreach (var record in records)
        {
            var node = GetOrCreateNode(record.Id);
            node.TimelineRecord = record;

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
        foreach (var value in map.Values)
        {
            if (value.ParentNode is null && !roots.Contains(value))
            {
                roots.Add(value);
            }
        }

        // TODO sort by start time, not name. The tree should reflect execution order
        var comparer = StringComparer.OrdinalIgnoreCase;
        foreach (var value in map.Values)
        {
            value.Children.Sort(Compare);
        }

        roots.Sort(Compare);

        var tree = new TimelineTree(timeline, roots);
        Debug.Assert(tree.Count == timeline.Records.Length);
        return tree;

        TimelineNode GetOrCreateNode(string id)
        {
            TimelineNode node;
            if (!map.TryGetValue(id, out node))
            {
                node = new TimelineNode();
                map.Add(id, node);
            }

            return node;
        } 

        static int Compare(TimelineNode x, TimelineNode y)
        {
            var xStart = DevOpsUtil.ConvertRestTime(x.TimelineRecord.StartTime);
            var yStart = DevOpsUtil.ConvertRestTime(y.TimelineRecord.StartTime);
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

