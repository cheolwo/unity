using System;
using System.Collections.Generic;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Bootstrap
{
    public sealed partial class 가상배달관찰Controller
    {
        private readonly List<Mesh> 참고Meshes = new();

        private void 사가정참고조립()
        {
            var asset = Resources.Load<TextAsset>("SagajeongReference");
            if (asset == null) { Debug.LogError("사가정 공간 참고 자료가 없습니다."); return; }
            var data=JsonUtility.FromJson<공간참고자료>(asset.text);
            Ssalddel.Unity.Presentation.World.사가정지리MeshBuilder.Build(data, 참고Mesh);
            var station=Block("사가정역 기준점",new Vector3(550,5,8),new Vector3(14,10,14),new Color(.65f,.75f,.15f));
            Label(station,"사가정역\n1km × 1km",Color.white);
            var label=station.GetComponentInChildren<TextMesh>(); if(label!=null)label.transform.localScale*=8;
            // 자료가 없거나 판본이 바뀌면 주소 패널만 보류하고 기존 지도/업무 상태는 보존한다.
            var addressView=GetComponent<면목동주소연결View>()??gameObject.AddComponent<면목동주소연결View>();
            addressView.Initialize(data,asset.bytes,observer);
            var wide=GetComponent<면목동전체자료View>()??gameObject.AddComponent<면목동전체자료View>();
            wide.Initialize();
        }

        private void 참고Mesh(string name,List<Vector3> vertices,List<int> triangles,Color color)
        {
            var obj=new GameObject(name);obj.layer=31;obj.transform.SetParent(transform,false);
            var mesh=new Mesh {name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            참고Meshes.Add(mesh);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var shader=Resources.Load<Shader>("관찰도형명암")??Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");
            var material=new Material(shader);material.color=color;materials.Add(material);
            obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

    }
}
