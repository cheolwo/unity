using System;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private bool campaignMode;
        private int campaignSpeed = 1;
        private int campaignResumeSpeed = 1;
        private double campaignAnchorReal;
        private double campaignAnchorMs;
        private bool campaignPauseTick;
        private bool campaignJumpPending;
        private long? campaignJumpRequestedAt;

        private bool CampaignPolicyOpen => state?.campaign?.phase == "policy";
        private bool AssignmentOpen => running && (state?.campaign == null || state.campaign.phase == "open");

        private void ResetCampaignClock()
        {
            campaignSpeed = 1;
            campaignResumeSpeed = 1;
            campaignPauseTick = false;
            campaignJumpPending = false;
            campaignJumpRequestedAt = null;
            AnchorCampaignClock(0, Time.realtimeSinceStartupAsDouble);
        }

        private void AnchorCampaignClock(double elapsedMs, double now)
        {
            campaignAnchorMs = elapsedMs;
            campaignAnchorReal = now;
        }

        private double CampaignElapsed(double now) => Math.Max(state.elapsedMs,
            campaignAnchorMs + (now - campaignAnchorReal) * 1000 * ClockMultiplier * campaignSpeed);

        private void SetCampaignSpeed(int speed)
        {
            if (!running || error != null || CampaignPolicyOpen || campaignJumpPending ||
                speed is not (0 or 1 or 2 or 4) || campaignSpeed == speed)
                return;

            var now = Time.realtimeSinceStartupAsDouble;
            AnchorCampaignClock(CampaignElapsed(now), now);
            campaignSpeed = speed;
            if (speed > 0) campaignResumeSpeed = speed;
            campaignPauseTick = speed == 0;
            nextTick = now;
        }

        private void AdvanceCampaignBoundary()
        {
            if (!running || queuedAssignment != null || campaignJumpPending || CampaignPolicyOpen ||
                (pending != null && pendingAction != "tick") || state?.campaign?.canFastForward != true)
                return;

            campaignJumpRequestedAt = state.campaign.nextBoundaryMs;
            if (pending == null) SendCampaignJump();
        }

        private bool SendCampaignJump()
        {
            if (campaignJumpRequestedAt is not { } boundary) return false;
            campaignJumpRequestedAt = null;
            if (!running || error != null || state.campaign?.canFastForward != true ||
                boundary != state.campaign.nextBoundaryMs || boundary <= state.elapsedMs)
                return false;

            campaignJumpPending = true;
            Send(new DispatchRequest { action = "tick", elapsedMs = boundary });
            return true;
        }

        private void ApplyCampaignPolicy(string policy)
        {
            if (!CampaignPolicyOpen || pending != null || string.IsNullOrEmpty(policy)) return;
            if (autopilotSentAction != "campaign-policy" && !직접업무가능()) return;
            Send(new DispatchRequest { action = "campaign-policy", elapsedMs = state.elapsedMs, policy = policy });
        }

        private void SynchronizeCampaignClock(DispatchCampaign previous, string action, bool accepted)
        {
            var campaign = state.campaign;
            var now = Time.realtimeSinceStartupAsDouble;
            if (previous?.day != campaign.day)
            {
                selectedDriver = null;
                actorFocus = null;
                notice = null;
            }

            var enteredPolicy = campaign.phase == "policy" && previous?.phase != "policy";
            if (enteredPolicy)
            {
                mapPointerDown = false;
                mapDragging = false;
                mapClick = false;
            }
            var appliedPolicy = action == "campaign-policy" && accepted;
            if (campaignJumpPending || enteredPolicy || appliedPolicy)
            {
                campaignJumpPending = false;
                campaignPauseTick = false;
                AnchorCampaignClock(state.elapsedMs, now);
                nextTick = now + 0.10;
            }
            if (appliedPolicy) campaignSpeed = campaignResumeSpeed;
        }
    }
}
