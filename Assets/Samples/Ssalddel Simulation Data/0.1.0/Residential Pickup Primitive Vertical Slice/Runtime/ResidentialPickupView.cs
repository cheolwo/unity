using System;
using Ssalddel.Unity.ResidentialPickup;
using UnityEngine;

namespace Ssalddel.Unity.Samples.ResidentialPickup
{
    public sealed class ResidentialPickupView : MonoBehaviour
    {
        [SerializeField]
        private ResidentialPickupPointView[] pickupPoints =
            Array.Empty<ResidentialPickupPointView>();

        [SerializeField]
        private TextMesh statusText = null!;

        public void Configure(ResidentialPickupPointView[] points, TextMesh status)
        {
            pickupPoints = points ?? Array.Empty<ResidentialPickupPointView>();
            statusText = status;
        }

        public void ShowLoading(string roleCode)
        {
            statusText.text = "LOADING · " + roleCode;
        }

        public void ShowError(string message)
        {
            statusText.text = "ERROR\n" + message;
        }

        public string[] Render(
            ResidentialPickupPerspectiveSnapshot snapshot,
            ResidentialPickupPerspectiveApplicator applicator)
        {
            var targets = new IResidentialPickupPointTarget[pickupPoints.Length];
            for (var index = 0; index < pickupPoints.Length; index++)
            {
                targets[index] = pickupPoints[index];
            }

            var unresolved = applicator.Apply(snapshot, targets);
            statusText.text = snapshot.AuthorizedRoleCode
                + " · " + snapshot.PickupPoints.Length + " PICKUP OBJECTS"
                + "\nREAD ONLY · SERVER AUTHORIZED";
            return unresolved;
        }

        public bool ValidateWiring()
        {
            if (pickupPoints == null || pickupPoints.Length == 0 || statusText == null)
            {
                return false;
            }

            foreach (var point in pickupPoints)
            {
                if (point == null || !point.ValidateWiring())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
