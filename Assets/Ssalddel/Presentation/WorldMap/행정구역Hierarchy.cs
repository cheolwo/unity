using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    // 경계 좌표/업무 상태와 독립적인 포함 관계. 코드 접두사나 고정 깊이를 사용하지 않는다.
    public sealed class 행정구역Node
    {
        public string Id { get; }
        public string ParentId { get; }
        public string Name { get; }
        public 행정구역Node(string id, string parentId, string name)
        { Id=id; ParentId=parentId; Name=name; }
    }

    public sealed class 행정구역Hierarchy
    {
        private readonly Dictionary<string,행정구역Node> nodes;
        public 행정구역Hierarchy(IEnumerable<행정구역Node> source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            nodes=new Dictionary<string,행정구역Node>(StringComparer.Ordinal);
            foreach(var node in source)
            {
                if(node==null || string.IsNullOrWhiteSpace(node.Id) || nodes.ContainsKey(node.Id))
                    throw new ArgumentException("AdministrativeIdentityInvalid");
                nodes.Add(node.Id,node);
            }
            foreach(var node in nodes.Values)
            {
                var visited=new HashSet<string>(StringComparer.Ordinal);
                var current=node;
                while(current!=null)
                {
                    if(!visited.Add(current.Id))throw new ArgumentException("AdministrativeHierarchyCycle");
                    if(string.IsNullOrEmpty(current.ParentId))break;
                    if(!nodes.TryGetValue(current.ParentId,out current))throw new ArgumentException("AdministrativeParentMissing");
                }
            }
        }
        public 행정구역Node[] Children(string parentId) => nodes.Values
            .Where(n=>string.Equals(n.ParentId??"",parentId??"",StringComparison.Ordinal))
            .OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();
        public bool IsChild(string id,string parentId) => id!=null && nodes.TryGetValue(id,out var node)
            && string.Equals(node.ParentId,parentId,StringComparison.Ordinal);
        public bool HasChildren(string id) => id!=null && nodes.ContainsKey(id) && Children(id).Length>0;

        // v1은 중랑구 동만 포함한 과거 계약. 호환 지식을 이 경계에만 격리한다.
        public static 행정구역Hierarchy FromSeoul(서울경계사본 source)
        {
            source.Validate();
            return new 행정구역Hierarchy(source.areas.Select(a=>new 행정구역Node(a.code,
                a.level==0?null:source.schemaVersion=="seoul-intermediate-preview.v1"?"11260":a.parentCode,a.name)));
        }
    }
}
