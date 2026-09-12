using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.NatureWoodcutting
{
    /// <summary>기존 imported 자산 참조만 저장한다. 기준 hash는 출처 표시이며 runtime 검증 결과가 아니다.</summary>
    public sealed class 벌목AnimationSources : ScriptableObject
    {
        public const string StudySha256 = "01150E4ECEF8D281284AC3B3213ACFF0670C026F4202B4E4C0A6A7841387586B";
        public const string ResourceKey = "NatureWoodcuttingSources";
        [SerializeField] private string baselineStudySha256 = StudySha256;
        [SerializeField] private Avatar targetAvatar;
        [SerializeField] private AnimationClip windUp;
        [SerializeField] private AnimationClip hit;
        [SerializeField] private AnimationClip followThrough;
        [SerializeField] private AnimationClip returnToIdle;
        [SerializeField] private AnimationClip idle;
        public string BaselineStudySha256 => baselineStudySha256;
        public Avatar TargetAvatar => targetAvatar;
        public AnimationClip GetClip(int index) => index switch
        {
            0 => windUp, 1 => hit, 2 => followThrough, 3 => returnToIdle, 4 => idle,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        public static string ExpectedName(int index) => index switch
        {
            0 => "A_Attack_HeavyCombo01A_WindUp_Sword",
            1 => "A_Attack_HeavyCombo01A_Hit_Sword",
            2 => "A_Attack_HeavyCombo01A_FollowThrough_Sword",
            3 => "A_Attack_HeavyCombo01A_ReturnToIdle_Sword",
            4 => "A_Idle_Standing_Masc",
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        public bool ValidateRuntime(Animator target, out string diagnostic)
        {
            diagnostic = "SourceOrTargetInvalid";
            if (baselineStudySha256 != StudySha256 || target == null || targetAvatar == null
                || target.avatar != targetAvatar || !targetAvatar.isHuman || !targetAvatar.isValid) return false;
            for (int i = 0; i < 5; i++)
            {
                var clip = GetClip(i);
                if (clip == null || clip.name != ExpectedName(i) || clip.empty || !clip.isHumanMotion
                    || clip.legacy || clip.length <= 0 || clip.events.Length != 0)
                { diagnostic = "SourceClipInvalid:" + i; return false; }
            }
            diagnostic = string.Empty;
            return true;
        }
    }
}
