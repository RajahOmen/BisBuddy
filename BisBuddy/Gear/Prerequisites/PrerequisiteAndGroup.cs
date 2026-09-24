using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BisBuddy.Gear.Prerequisites
{
    public class PrerequisiteAndGroup : IReadOnlyList<PrerequisiteNode>
    {
        public PrerequisiteNode this[int index] => ((IReadOnlyList<PrerequisiteNode>)prerequisites)[index];

        public IReadOnlyList<PrerequisiteNode> Prerequisites { get; private set; }
        public PrerequisiteNodeSourceType SourceType { get; private set; }
        public bool IsActive { get; set; }
        public bool ParentCollected {
            get;
            set
            {
                if (field == value)
                    return;

                groups = getGroups();
                field = value;
            }
        }
        private List<(PrerequisiteNode Node, int Count)> groups;
        public IReadOnlyList<(PrerequisiteNode Node, int Count)> Groups => groups;
        private readonly List<PrerequisiteNode> prerequisites;

        public PrerequisiteAndGroup(
            List<PrerequisiteNode> prerequisites,
            PrerequisiteNodeSourceType sourceType,
            bool isActive = true
        )
        {
            this.prerequisites = prerequisites;
            Prerequisites = prerequisites;
            var groups = prerequisites.GroupBy(p => p.ItemId);

            foreach (var prereq in prerequisites)
            {
                prereq.OnPrerequisiteChange += handleOnPrerequisiteUpdate;
            }
            this.groups = getGroups();
            SourceType = sourceType;
            IsActive = isActive;
        }

        private void handleOnPrerequisiteUpdate()
        {
            groups = getGroups();
        }

        public CollectionStatusType CollectionStatus
        {
            get
            {
                if (ParentCollected)
                    return CollectionStatusType.ObtainedComplete;
                var (groupAllObtainable, groupAnyPartial) = Prerequisites
                    .Select(p => p.CollectionStatus)
                    .Aggregate((AllObtainable: true, AnyPartial: false), (acc, status) => (
                        acc.AllObtainable && status >= CollectionStatusType.Obtainable,
                        acc.AnyPartial || status >= CollectionStatusType.NotObtainablePartial
                    ));
                if (groupAllObtainable)
                    return CollectionStatusType.Obtainable;
                if (groupAnyPartial)
                    return CollectionStatusType.NotObtainablePartial;
                return CollectionStatusType.NotObtainable;
            }
        }

        public int Count => ((IReadOnlyCollection<PrerequisiteNode>)prerequisites).Count;


        private List<(PrerequisiteNode, int)> getGroups()
        {
            return Prerequisites
                .OrderBy(p => p.CollectionStatus)
                .ThenBy(p => p.ItemId)
                .GroupBy(p => $"{p.ItemId} | {p.CollectionStatus} | {p.CollectLock}")
                .Select(g => (g.First(), g.Count()))
                .ToList();
        }


        public IEnumerator<PrerequisiteNode> GetEnumerator()
        {
            foreach (var prerequisite in Prerequisites)
                yield return prerequisite;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public PrerequisiteAndGroup Clone() => new(
            prerequisites: [.. Prerequisites.Select(p => p.Clone())],
            sourceType: SourceType,
            isActive: IsActive
        );

        public bool Equals(PrerequisiteAndGroup? other)
        {
            if (other is null)
                return false;

            if (SourceType != other.SourceType)
                return false;

            return Prerequisites.Select(p => p.ItemId).OrderBy(i => i).SequenceEqual(other.Prerequisites.Select(n => n.ItemId).OrderBy(i => i));
        }

        public override string ToString()
        {
            return (
                $"[{Enum.GetName(SourceType)}] [{Prerequisites.Count}]" +
                (IsActive ? "" : " [inactive]") +
                "\n  " +
                string.Join(
                    "\n  ",
                    Prerequisites.Select(n => n.ToString().Replace("\n", "\n  "))
                )
            );
        }
    }
}
