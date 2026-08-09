using System;
using Ssalddel.Unity.Npcs;
using UnityEngine;

namespace Ssalddel.Unity.Samples.NpcMovement
{
    public sealed class ZoneNpcMovementController : MonoBehaviour
    {
        [SerializeField]
        private NpcMovementView[] npcViews = Array.Empty<NpcMovementView>();

        private readonly NpcMovementApplicator applicator = new NpcMovementApplicator();

        public void Configure(NpcMovementView[] views)
        {
            npcViews = views ?? Array.Empty<NpcMovementView>();
        }

        public string[] ApplySnapshots(NpcMovementSnapshot[] snapshots)
        {
            var targets = new INpcMovementTarget[npcViews.Length];
            for (var index = 0; index < npcViews.Length; index++)
            {
                targets[index] = npcViews[index];
            }

            return applicator.Apply(snapshots, targets);
        }

        public string[] ApplyPresentations(NpcMovementPresentationModel[] models)
        {
            var targets = new INpcMovementPresentationTarget[npcViews.Length];
            for (var index = 0; index < npcViews.Length; index++)
            {
                targets[index] = npcViews[index];
            }

            return applicator.Apply(models, targets);
        }

        public bool ValidateWiring()
        {
            if (npcViews == null || npcViews.Length == 0)
            {
                return false;
            }

            foreach (var view in npcViews)
            {
                if (view == null || !view.ValidateWiring())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
