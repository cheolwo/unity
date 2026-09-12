using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable] public sealed class 주소공간연결자료
    {
        public string schema, inputHash, mapHash, mapRevision, addressRule, relation;
        public bool privateReviewOnly, gameStateConnected;
        public int inputCount, connectedCount, unlinkedCount;
        public 주소건물참고Group[] groups;
        public 주소자료Link[] links;
    }
    [Serializable] public sealed class 주소자료Item
    {
        public string id, kind, name, roadAddress, sourceId, datasetId, sourceHash, revision, evidenceAsOf, status, quality;
    }
    [Serializable] public sealed class 주소자료Link
    {
        public 주소자료Item item;
        public string addressKey, result;
        public string[] buildingIds;
    }
    [Serializable] public sealed class 주소건물참고Group
    {
        public string addressKey, buildingId, relation;
        public 주소자료Item[] records;
    }
    public static class 주소공간연결검사
    {
        public const string InputHash="9811BEE6CFD67B741CBD47830325C8408C3499390737AED63E960CC22645E536";
        public const string MapHash="4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3";
        public const string BundleHash="5FED8531798DFA63294C2BA201F864FFC5257E2DFA75B96F508B7850F7D5238C";
        public const string BusinessInputHash="a48b62059b2fe2b6b5d5165949c1c40ecfad25292154b59bd64fd20ec6a3f41d";
        public const string BusinessBundleHash="D109A0AF26608E9627551600B37FE8B22F473893D5F6FEA542E83E5D1FD176E2";
        public const string Unique="UniqueAddressCandidateNotVerifiedOccupancy";
        private static void Require(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
        public static string Hash(byte[] bytes)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
        }
        // 경계는 로컬 관찰 파일뿐. Resources/빌드에 비공개 사본을 포함하거나 DB를 직접 호출하지 않는다.
        public static byte[] ReadFrozen(string path,byte[] mapBytes,bool business=false)
        {
            Require(Hash(mapBytes)==MapHash,"AddressMapChanged");
            for(var p=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path)));p!=null;p=p.Parent)
                if(p.Exists)Require((p.Attributes&FileAttributes.ReparsePoint)==0,"AddressReparseRejected");
            var file=new FileInfo(path);
            Require(file.Exists&&file.Length<=16*1024*1024&&(file.Attributes&FileAttributes.ReparsePoint)==0,"AddressFileMissingOrInvalid");
            var bytes=File.ReadAllBytes(path);Require(Hash(bytes)==(business?BusinessBundleHash:BundleHash),"AddressBundleChanged");return bytes;
        }
        // true는 현재 운영 확인이 아니다. 의료의 명시적 비정상 이력을 기본 화면에서만 제외한다.
        public static bool Show(주소자료Item item,bool includeHistory)
        {
            return includeHistory || (item.kind!="clinic"&&item.kind!="hospital") || item.status=="영업/정상";
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static void Validate(주소공간연결자료 data,공간참고자료 map,bool business=false)
        {
            Require(data!=null&&map!=null&&map.buildings!=null,"AddressDataMissing");
            Require(data.schema=="myeonmok-building-reference.r1"&&data.privateReviewOnly&&!data.gameStateConnected
                &&data.inputHash==(business?BusinessInputHash:InputHash)&&data.mapHash==MapHash&&data.mapRevision=="sagajeong-reference.r3"
                &&map.revision==data.mapRevision&&data.addressRule=="kr-public-business-building-match-v1"&&data.relation==Unique,"AddressContractMismatch");
            Require(data.links!=null&&data.groups!=null&&data.inputCount>0&&data.inputCount<=20000&&data.groups.Length<=1000
                &&data.links.Length==data.inputCount&&data.connectedCount+data.unlinkedCount==data.inputCount,"AddressCountMismatch");
            var buildings=new Dictionary<string,참고건물>(StringComparer.Ordinal);
            foreach(var b in map.buildings)
            {
                Require(b!=null&&!string.IsNullOrEmpty(b.id)&&!buildings.ContainsKey(b.id),"AddressBuildingDuplicate");buildings.Add(b.id,b);
            }
            var links=new Dictionary<string,주소자료Link>(StringComparer.Ordinal);var uniqueCount=0;
            foreach(var l in data.links)
            {
                Require(l!=null&&l.item!=null&&!string.IsNullOrEmpty(l.item.id)&&!links.ContainsKey(l.item.id)&&l.buildingIds!=null,"AddressLinkInvalid");
                Require(!string.IsNullOrEmpty(l.item.sourceId)&&!string.IsNullOrEmpty(l.item.datasetId)&&l.item.sourceHash?.Length==64,"AddressProvenanceMissing");
                var candidates=new HashSet<string>(StringComparer.Ordinal);
                foreach(var id in l.buildingIds)Require(buildings.ContainsKey(id)&&candidates.Add(id),"AddressCandidateInvalid");
                if(l.result==Unique){Require(l.buildingIds.Length==1&&!string.IsNullOrEmpty(l.addressKey),"AddressUniqueMismatch");uniqueCount++;}
                else if(l.result=="MultipleBuildingsForAddress")Require(l.buildingIds.Length>1&&!string.IsNullOrEmpty(l.addressKey),"AddressAmbiguousMismatch");
                else if(l.result=="AddressMissing")Require(l.buildingIds.Length==0&&string.IsNullOrEmpty(l.addressKey),"AddressMissingMismatch");
                else Require(l.result=="NoExactBuildingInCurrentMap"&&l.buildingIds.Length==0&&!string.IsNullOrEmpty(l.addressKey),"AddressResultInvalid");
                links.Add(l.item.id,l);
            }
            var records=new HashSet<string>(StringComparer.Ordinal);var usedBuildings=new HashSet<string>(StringComparer.Ordinal);
            foreach(var g in data.groups)
            {
                Require(g!=null&&g.relation==Unique&&g.records!=null&&g.records.Length>0&&buildings.ContainsKey(g.buildingId)&&usedBuildings.Add(g.buildingId),"AddressGroupInvalid");
                var b=buildings[g.buildingId];
                Require(b.points!=null&&b.points.Length>=4&&Finite(b.height)&&b.height>0,"AddressShapeMissing");
                foreach(var p in b.points)Require(p!=null&&Finite(p.x)&&Finite(p.z),"AddressPointInvalid");
                foreach(var r in g.records)
                {
                    Require(r!=null&&records.Add(r.id)&&links.ContainsKey(r.id),"AddressGroupRecordInvalid");var l=links[r.id];
                    Require(l.result==Unique&&l.buildingIds[0]==g.buildingId&&l.addressKey==g.addressKey&&Same(r,l.item),"AddressGroupLinkMismatch");
                }
            }
            Require(records.Count==data.connectedCount&&uniqueCount==records.Count,"AddressConnectedCountMismatch");
        }
        private static bool Same(주소자료Item a,주소자료Item b)=>a.id==b.id&&a.kind==b.kind&&a.name==b.name&&a.roadAddress==b.roadAddress
            &&a.status==b.status&&a.quality==b.quality&&a.sourceId==b.sourceId&&a.datasetId==b.datasetId&&a.sourceHash==b.sourceHash&&a.revision==b.revision&&a.evidenceAsOf==b.evidenceAsOf;
    }
}
