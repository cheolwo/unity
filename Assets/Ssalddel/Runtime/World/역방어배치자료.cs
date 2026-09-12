using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable] public sealed class 역방어배치자료
    {
        public string schemaVersion,mapRevision,mapHash;
        public 역방어옥상자리[] slots;
    }
    [Serializable] public sealed class 역방어옥상자리
    {
        public string id,label,buildingId;
        public float x,z,height;
    }
    public static class 역방어배치검사
    {
        public static void Validate(역방어배치자료 data,공간참고자료 map,byte[] mapBytes)
        {
            if(data==null||map==null||mapBytes==null||data.schemaVersion!="station-defense-placement.v1"||data.mapRevision!=map.revision)
                throw new ArgumentException("DefenseMapRevisionMismatch");
            using(var sha=SHA256.Create())
                if(!string.Equals(BitConverter.ToString(sha.ComputeHash(mapBytes)).Replace("-",""),data.mapHash,StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("DefenseMapHashMismatch");
            if(data.slots==null||data.slots.Length!=2)throw new ArgumentException("DefenseSlotCountInvalid");
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var slot in data.slots)
            {
                if(slot==null||string.IsNullOrWhiteSpace(slot.id)||!seen.Add(slot.id)||string.IsNullOrWhiteSpace(slot.label)
                    ||!Finite(slot.x)||!Finite(slot.z)||!Finite(slot.height))throw new ArgumentException("DefenseSlotInvalid");
                var building=map.buildings?.SingleOrDefault(x=>x.id==slot.buildingId);
                if(building==null||building.heightKind!="OsmHeightTag"||Math.Abs(building.height-slot.height)>.01f
                    // 연구 r2: 표식 반경 6m + 추가 경계 여유 2m. 실제 옥상 안전 기준이 아니다.
                    ||!InsideWithClearance(building.points,slot.x,slot.z,8))throw new ArgumentException("DefenseRoofCandidateRejected");
            }
        }
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public static bool InsideWithClearance(참고점[] points,float x,float z,float clearance)
        {
            if(points==null||points.Length<4||!Finite(x)||!Finite(z)||!Finite(clearance)||clearance<0)return false;
            var inside=false;double nearest=double.MaxValue;
            for(int i=0,j=points.Length-1;i<points.Length;j=i++)
            {
                var a=points[j];var b=points[i];
                if(a==null||b==null||!Finite(a.x)||!Finite(a.z)||!Finite(b.x)||!Finite(b.z))return false;
                if((a.z>z)!=(b.z>z)&&x<(b.x-a.x)*(z-a.z)/(b.z-a.z)+a.x)inside=!inside;
                double dx=b.x-a.x,dz=b.z-a.z,den=dx*dx+dz*dz;
                if(den<.000001)continue;
                var t=Math.Max(0,Math.Min(1,((x-a.x)*dx+(z-a.z)*dz)/den));
                var ex=x-(a.x+t*dx);var ez=z-(a.z+t*dz);
                nearest=Math.Min(nearest,Math.Sqrt(ex*ex+ez*ez));
            }
            return inside&&nearest>=clearance;
        }
    }
}
