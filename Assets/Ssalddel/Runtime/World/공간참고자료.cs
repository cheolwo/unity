using System;
using System.Collections.Generic;

namespace Ssalddel.Unity.Runtime.World
{
    // 원문 지리 참고 데이터. LH의 EPSG:5186 셀이나 업무/통행 권위로 승격하지 않는다.
    [Serializable] public sealed class 공간참고자료
    {
        public string revision, attribution, coordinateMethod;
        public float offsetX, offsetZ, halfExtent;
        public 참고도로[] roads;
        public 참고건물[] buildings;
    }
    [Serializable] public sealed class 참고도로 { public string id, kind; public float x1,z1,x2,z2; }
    [Serializable] public sealed class 참고건물
    {
        public string id, name, street, houseNumber, unit, levelsText, buildingKind, heightKind;
        public int version;
        public float height;
        public 참고점[] points;
    }
    [Serializable] public sealed class 참고점 { public float x,z; }

    /// <summary>기존 LH 동심 창 계산기를 재사용하는 읽기 전용 참고 건물 조회.
    /// 화면 조립·다운로드·상태 변경은 수행하지 않는다. 입력 좌표는 동일 ENU 배치 좌표다.</summary>
    public static class 공간참고건물WindowQuery
    {
        public static 참고건물[] Query(공간참고자료 data, double x, double z,
            공간동적MapWindowPolicyData policy, string movementDirection = "")
        {
            if(data == null || data.buildings == null) throw new ArgumentException("ReferenceDataMissing");
            if(double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(z)||double.IsInfinity(z))
                throw new ArgumentException("ReferenceFocusInvalid");
            if(data.halfExtent <= 0 || float.IsNaN(data.halfExtent)||float.IsInfinity(data.halfExtent)
                || float.IsNaN(data.offsetX)||float.IsInfinity(data.offsetX)
                || float.IsNaN(data.offsetZ)||float.IsInfinity(data.offsetZ)) throw new ArgumentException("ReferenceBoundsInvalid");
            // 500m 참고 타일: 좌표계·키가 다른 기존 L2 타일에 강제 대입하지 않는다.
            const double size=500;
            var fx=Math.Floor((x-data.offsetX)/size);
            var fz=Math.Floor((z-data.offsetZ)/size);
            if(Math.Abs(fx)>1000000 || Math.Abs(fz)>1000000) throw new ArgumentException("ReferenceFocusOutOfRange");
            if(policy == null || policy.PrefetchRadius>16) throw new ArgumentException("ReferenceWindowBudgetExceeded");
            var cells=공간동적MapWindowPlanner.Plan((int)fx,(int)fz,policy,movementDirection);
            var result=new List<참고건물>();var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var building in data.buildings)
            {
                if(building==null || string.IsNullOrWhiteSpace(building.id) || !ids.Add(building.id)
                    || building.points==null || building.points.Length<4) throw new ArgumentException("ReferenceBuildingInvalid");
                double minX=double.PositiveInfinity,minZ=minX,maxX=double.NegativeInfinity,maxZ=maxX;
                foreach(var p in building.points)
                {
                    if(p==null || float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.z)||float.IsInfinity(p.z))
                        throw new ArgumentException("ReferencePointInvalid");
                    minX=Math.Min(minX,p.x);maxX=Math.Max(maxX,p.x);minZ=Math.Min(minZ,p.z);maxZ=Math.Max(maxZ,p.z);
                }
                foreach(var cell in cells)
                {
                    var left=data.offsetX+cell.CellX*size;var bottom=data.offsetZ+cell.CellY*size;
                    // 경계 건물은 잘라내지 않고 같은 식별자 한 번만 반환한다.
                    if(maxX<left || minX>left+size || maxZ<bottom || minZ>bottom+size)continue;
                    result.Add(building);break;
                }
            }
            return result.ToArray();
        }
    }
}
