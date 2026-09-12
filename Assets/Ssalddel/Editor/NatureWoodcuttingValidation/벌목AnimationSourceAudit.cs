using System;
using System.IO;
using System.Security.Cryptography;
using Ssalddel.Unity.Presentation.NatureWoodcutting;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor.NatureWoodcuttingValidation
{
    /// <summary>기존 importer를 변경하지 않고 원본 bytes·GUID/fileID·참조를 읽기 검증한다.</summary>
    public static class 벌목AnimationSourceAudit
    {
        public const string SourcesPath = "Assets/Ssalddel/Presentation/NatureWoodcutting/Resources/NatureWoodcuttingSources.asset";
        public const string ActorPath = "Assets/Synty/PolygonFarm/Prefabs/Characters/SM_Chr_Farmer_Male_01.prefab";
        public const string AvatarPath = "Assets/Synty/PolygonFarm/Models/Characters.fbx";
        private const string AttackPath = "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/HeavyCombo01/A_Attack_HeavyCombo01A_Sword.fbx";
        private const string ReturnPath = "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/HeavyCombo01/A_Attack_HeavyCombo01A_ReturnToIdle_Sword.fbx";
        private const string IdlePath = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/Masculine/Idle/A_Idle_Standing_Masc.fbx";
        private static readonly long[] Ids = { 9216853843792190869L, 114646337594296348L,
            6240718372927078310L, -7354800201745137062L, 1827226128182048838L };

        public static bool Validate(out string diagnostic)
        {
            try
            {
                CheckFile(ActorPath, "D176B368838CBED5024D89CB953B65FC8A0D3630E3FE11F214A0108B262E2343",
                    "6ED96896ADBEA6C5C7BB08DDAAAF5C89084D86CBE84DC91B5172DAE3C34E3266");
                CheckFile(AvatarPath, "EA7A567F52F593024854AD2947A64B7C4540985B2C71E93F79FF6574F2DB810F",
                    "195FC2247303913F44D3AC767683985288617E5FB15C3B4AEF51AE7A87A7625D");
                CheckFile(AttackPath, "58E5D031CB15D58965599BFD0545B3FE56E2E9743A4737C252D5D30325A6B32B",
                    "49842410E318C6F8F0671CA274C66E2D1CFF8E342A571BFA0640D35D66772ECD");
                CheckFile(ReturnPath, "BF3745902F8E39AEF42B8A0013184014CC5C6161D7B0FF63E91090F90F2DBCDA",
                    "DFA9C140379E5A0B81BF0D99F93493F736CF5262F4B743F1B11EC046DEFD3C92");
                CheckFile(IdlePath, "76DC31C92FD3CA69F28E92F1F530EAC6F60EE71EA0F25D490A633F49D2088DD0",
                    "28B56AAE249214D9D69E94E1C6E97DA77EF269E2E3C964B5763637F25FD94BF9");
                var sources = AssetDatabase.LoadAssetAtPath<벌목AnimationSources>(SourcesPath);
                if (sources == null || sources.BaselineStudySha256 != 벌목AnimationSources.StudySha256)
                    throw new InvalidOperationException("SourceAssetMissingOrBaselineChanged");
                CheckIdentity(sources.TargetAvatar, AvatarPath, "0125309e38bea8a48bc578cf8f220634", 9000000L);
                for (int i = 0; i < 5; i++)
                {
                    var clip = sources.GetClip(i);
                    var path = i < 3 ? AttackPath : i == 3 ? ReturnPath : IdlePath;
                    var guid = i < 3 ? "f3800f1bd189abc469fcc7c374371a91" : i == 3
                        ? "20aa50f251da63047ab4ac1045839566" : "a4b8f2deec6a99945987cfc59b1b4e54";
                    CheckIdentity(clip, path, guid, Ids[i]);
                    if (clip.name != 벌목AnimationSources.ExpectedName(i) || clip.empty
                        || !clip.isHumanMotion || clip.events.Length != 0)
                        throw new InvalidOperationException("ClipStructureChanged:" + i);
                }
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception) { diagnostic = exception.Message; return false; }
        }
        private static void CheckIdentity(UnityEngine.Object asset, string path, string guid, long id)
        {
            if (asset == null || AssetDatabase.GetAssetPath(asset) != path
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string actualGuid, out long actualId)
                || actualGuid != guid || actualId != id) throw new InvalidOperationException("SourceIdentityMismatch:" + path);
        }
        private static void CheckFile(string path, string hash, string metaHash)
        {
            if (Hash(path) != hash || Hash(path + ".meta") != metaHash)
                throw new InvalidOperationException("SourceHashDrift:" + path);
        }
        private static string Hash(string path)
        {
            using var stream = File.OpenRead(Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), path));
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
