using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>정확 GIS 연구 대상의 비공개 표시 결속만 읽는다. 근접 건물이나 정본 동일성을 만들지 않는다.</summary>
    public static class 사가정영상건물BindingGate
    {
        public const string Schema = "private-diorama-building-binding-manifest.v1";

        public static string ValidateJson(string json,string targetId,string evidenceHash,string geometryHash,
            string baseMapHash,string spatialFileHash,Vector2[] expectedRing)
        {
            var data=JObject.Parse(json);
            if(data.Value<string>("schema")!=Schema || data.Value<int?>("canonicalAppliedCount")!=0
                || data.Value<bool?>("userApprovalCreated")==true || data["bindings"] is not JArray rows)
                throw new InvalidOperationException("VideoBuildingPrivateBindingRequired");
            var map=data["currentMap"];
            if(!SameHash(map?["reference"]?.Value<string>("sha256"),spatialFileHash)
                || !SameHash(map?.Value<string>("baseMapHash"),baseMapHash)
                || !SameHash(map?["baseReference"]?.Value<string>("sha256"),baseMapHash))
                throw new InvalidOperationException("VideoBuildingBindingMapVersionMismatch");
            var matches=rows.Where(r=>r.Value<string>("targetBuildingId")==targetId).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("VideoBuildingUniqueExactTargetRequired");
            var row=matches[0];
            if(row.Value<string>("status")!="SourceBackedPrivateLayer"
                || row.Value<string>("targetScope")!="SourceBackedPrivateLayer"
                || row.Value<string>("sourceBuildingId")!=targetId
                || row.Value<bool?>("canonicalApplied")!=false || row.Value<bool?>("userApprovalCreated")==true
                || row["canonicalBinding"]?.Value<string>("status")!="Held"
                || !SameHash(row["sourceEvidence"]?.Value<string>("sha256"),evidenceHash)
                || !SameHash(row.Value<string>("sourceGeometryHash"),geometryHash))
                throw new InvalidOperationException("VideoBuildingExactPrivateEvidenceMismatch");
            if(row["localParts"] is not JArray parts || parts.Count!=1
                || parts[0]["outer"]?["points"] is not JArray points || expectedRing==null
                || points.Count!=expectedRing.Length || (parts[0]["holes"] as JArray)?.Count!=0)
                throw new InvalidOperationException("VideoBuildingPrivateFootprintRequired");
            for(var i=0;i<points.Count;i++)
            {
                var x=points[i].Value<float?>("x");var z=points[i].Value<float?>("z");
                if(!x.HasValue || !z.HasValue || !float.IsFinite(x.Value) || !float.IsFinite(z.Value)
                    || Vector2.Distance(new Vector2(x.Value,z.Value),expectedRing[i])>.01f)
                    throw new InvalidOperationException("VideoBuildingPrivateFootprintMismatch");
            }
            return "SourceBackedPrivateLayer";
        }

        private static bool SameHash(string value,string expected)
            => value!=null && expected!=null && value.Length==64 && expected.Length==64
                && value.All(Uri.IsHexDigit) && expected.All(Uri.IsHexDigit)
                && string.Equals(value,expected,StringComparison.OrdinalIgnoreCase);
    }
}
