using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable] public sealed class 도로명표현자료
    {
        public string schema, revision, mapRevision, mapHash, rawSourceHash, sourceUrl, attribution, boundary;
        public bool presentationOnly;
        public int namedSegmentCount, linkedBuildingCount;
        public 도로명표현항목[] roads;
    }

    [Serializable] public sealed class 도로명표현항목
    {
        public string name, importanceCode;
        public float x, z, yaw;
        public int segmentCount, buildingCount;
        public string[] wayIds;
    }

    public sealed class 도로명건물주소
    {
        public string BuildingId { get; }
        public string Label { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        internal 도로명건물주소(string buildingId,string label,float x,float y,float z)
        { BuildingId=buildingId;Label=label;X=x;Y=y;Z=z; }
    }

    /// <summary>공개 OSM 도로명과 건물번호의 읽기 전용 표현 사본. 이동·주소·입주의 권위를 만들지 않는다.</summary>
    public sealed class 도로명주소표현조회
    {
        public const string MapHash="4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3";
        public const string RawHash="3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3";
        private readonly ReadOnlyCollection<도로명표현항목> labels;
        private readonly Dictionary<string,ReadOnlyCollection<도로명건물주소>> addresses;
        public IReadOnlyList<도로명표현항목> Labels=>labels;
        public int NamedSegmentCount { get; }
        public int LinkedBuildingCount { get; }
        private 도로명주소표현조회(List<도로명표현항목> values,
            Dictionary<string,ReadOnlyCollection<도로명건물주소>> buildingAddresses,int segmentCount,int buildingCount)
        { labels=values.AsReadOnly();addresses=buildingAddresses;NamedSegmentCount=segmentCount;LinkedBuildingCount=buildingCount; }

        public IReadOnlyList<도로명건물주소> Addresses(string roadName)
            => roadName!=null&&addresses.TryGetValue(roadName,out var found)?found:Array.Empty<도로명건물주소>();

        public IReadOnlyList<도로명표현항목> SelectLabels(float orthographicSize,float focusX,float focusZ,int maximum)
        {
            if(!float.IsFinite(orthographicSize)||orthographicSize<=0||!float.IsFinite(focusX)||!float.IsFinite(focusZ)
                ||maximum<1||maximum>40)throw new ArgumentException("RoadLabelQueryInvalid");
            var local=orthographicSize<=220;
            return labels.Where(x=>x.importanceCode=="Major"||local)
                .OrderBy(x=>x.importanceCode=="Major"?0:1)
                .ThenBy(x=>(x.x-focusX)*(x.x-focusX)+(x.z-focusZ)*(x.z-focusZ))
                .ThenBy(x=>x.name,StringComparer.Ordinal).Take(maximum).ToArray();
        }

        public static 도로명주소표현조회 Validate(도로명표현자료 data,공간참고자료 map,byte[] mapBytes)
        {
            if(data==null||map?.roads==null||map.buildings==null||mapBytes==null||data.roads==null)
                throw new ArgumentException("RoadNameDataMissing");
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(mapBytes)).Replace("-","");
            if(data.schema!="sagajeong-road-name-overlay.v1"||data.revision!="sagajeong-road-address-presentation.r1"
                ||data.mapRevision!=map.revision||!string.Equals(data.mapHash,hash,StringComparison.OrdinalIgnoreCase)
                ||data.mapHash!=MapHash||data.rawSourceHash!=RawHash||!data.presentationOnly
                ||data.sourceUrl!="https://api.openstreetmap.org/api/0.6/map?bbox=127.0825,37.5762,127.0942,37.5854"
                ||data.attribution!="© OpenStreetMap contributors · ODbL 1.0"
                ||data.boundary!="PublicOpenData;PresentationOnly;NoNavigation;NoAddressAuthority")
                throw new ArgumentException("RoadNameBoundaryMismatch");
            var byWay=new Dictionary<string,List<참고도로>>(StringComparer.Ordinal);
            foreach(var segment in map.roads)
            {
                if(segment==null||string.IsNullOrWhiteSpace(segment.id)||!float.IsFinite(segment.x1)||!float.IsFinite(segment.z1)
                    ||!float.IsFinite(segment.x2)||!float.IsFinite(segment.z2))throw new ArgumentException("RoadSegmentInvalid");
                var split=segment.id.LastIndexOf(":segment:",StringComparison.Ordinal);
                if(split<=0)throw new ArgumentException("RoadSegmentIdInvalid");
                var way=segment.id.Substring(0,split);
                if(!byWay.TryGetValue(way,out var list))byWay.Add(way,list=new List<참고도로>());
                list.Add(segment);
            }
            var names=new HashSet<string>(StringComparer.Ordinal);var usedWays=new HashSet<string>(StringComparer.Ordinal);
            var copied=new List<도로명표현항목>();var addressGroups=new Dictionary<string,ReadOnlyCollection<도로명건물주소>>(StringComparer.Ordinal);
            var totalSegments=0;var totalBuildings=0;var majorCount=0;
            foreach(var item in data.roads)
            {
                if(item==null||string.IsNullOrWhiteSpace(item.name)||!names.Add(item.name)||item.wayIds==null||item.wayIds.Length==0
                    ||!float.IsFinite(item.x)||!float.IsFinite(item.z)||!float.IsFinite(item.yaw)||item.yaw< -90||item.yaw>90
                    ||Math.Abs(item.x-map.offsetX)>map.halfExtent||Math.Abs(item.z-map.offsetZ)>map.halfExtent)
                    throw new ArgumentException("RoadNameItemInvalid");
                var segments=new List<(string Way,참고도로 Segment)>();
                foreach(var way in item.wayIds)
                {
                    if(string.IsNullOrWhiteSpace(way)||!usedWays.Add(way)||!byWay.TryGetValue(way,out var found))
                        throw new ArgumentException("RoadNameWayMismatch");
                    segments.AddRange(found.Select(x=>(way,x)));
                }
                var expectedImportance=segments.Any(x=>x.Segment.kind=="primary"||x.Segment.kind=="secondary"||x.Segment.kind=="tertiary")?"Major":"Local";
                if(item.importanceCode!=expectedImportance||item.segmentCount!=segments.Count)
                    throw new ArgumentException("RoadNameClassificationMismatch");
                var anchor=segments.OrderByDescending(x=>Length2(x.Segment)).ThenBy(x=>x.Way,StringComparer.Ordinal).First().Segment;
                var yaw=(float)(Math.Atan2(anchor.z2-anchor.z1,anchor.x2-anchor.x1)*180/Math.PI);
                if(yaw>90)yaw-=180;if(yaw< -90)yaw+=180;
                if(Math.Abs(item.x-(anchor.x1+anchor.x2)*.5f)>.011f||Math.Abs(item.z-(anchor.z1+anchor.z2)*.5f)>.011f||Math.Abs(item.yaw-yaw)>.011f)
                    throw new ArgumentException("RoadNameAnchorMismatch");
                var buildings=map.buildings.Where(x=>x!=null&&x.street==item.name).OrderBy(x=>x.houseNumber,StringComparer.Ordinal).ToArray();
                if(item.buildingCount!=buildings.Length)throw new ArgumentException("RoadNameBuildingCountMismatch");
                var addressesForRoad=new List<도로명건물주소>();
                foreach(var building in buildings)
                {
                    if(string.IsNullOrWhiteSpace(building.id)||string.IsNullOrWhiteSpace(building.houseNumber)||building.points==null||building.points.Length<4)
                        throw new ArgumentException("RoadAddressBuildingInvalid");
                    var minX=building.points.Min(p=>p.x);var maxX=building.points.Max(p=>p.x);
                    var minZ=building.points.Min(p=>p.z);var maxZ=building.points.Max(p=>p.z);
                    addressesForRoad.Add(new 도로명건물주소(building.id,item.name+" "+building.houseNumber,(minX+maxX)*.5f,building.height*.5f,(minZ+maxZ)*.5f));
                }
                addressGroups.Add(item.name,addressesForRoad.AsReadOnly());
                copied.Add(new 도로명표현항목{name=item.name,importanceCode=item.importanceCode,x=item.x,z=item.z,yaw=item.yaw,
                    segmentCount=item.segmentCount,buildingCount=item.buildingCount,wayIds=item.wayIds.ToArray()});
                totalSegments+=segments.Count;totalBuildings+=buildings.Length;if(expectedImportance=="Major")majorCount++;
            }
            if(copied.Count!=78||majorCount!=4||totalSegments!=1109||totalBuildings!=568
                ||data.namedSegmentCount!=totalSegments||data.linkedBuildingCount!=totalBuildings)
                throw new ArgumentException("RoadNameFrozenCountMismatch");
            return new 도로명주소표현조회(copied,addressGroups,totalSegments,totalBuildings);
        }

        private static double Length2(참고도로 x)
        { var dx=(double)x.x2-x.x1;var dz=(double)x.z2-x.z1;return dx*dx+dz*dz; }
    }
}
