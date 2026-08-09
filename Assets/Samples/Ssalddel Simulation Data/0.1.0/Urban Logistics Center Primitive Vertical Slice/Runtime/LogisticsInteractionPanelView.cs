using System.Collections.Generic;
using System.Text;
using Ssalddel.Unity.Perspectives;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter
{
    public sealed class LogisticsInteractionPanelView : MonoBehaviour, IRoleInteractionSink, IRolePresentationInteractionSink
    {
        [SerializeField]
        private TextMesh interactionText = null!;

        public void Configure(TextMesh text)
        {
            interactionText = text;
        }

        public void ReplaceAllowedInteractions(IReadOnlyList<역할허용Interaction> interactions)
        {
            var builder = new StringBuilder("TRANSPORTER ACTIONS");
            foreach (var interaction in interactions)
            {
                builder.Append('\n')
                    .Append(interaction.InteractionCode)
                    .Append(" · ")
                    .Append(interaction.EffectCode);
            }

            interactionText.text = builder.ToString();
        }

        public void ReplaceAllowedInteractions(IReadOnlyList<RoleInteractionPresentationModel> interactions)
        {
            var builder = new StringBuilder("TRANSPORTER ACTIONS");
            foreach (var interaction in interactions)
            {
                builder.Append('\n')
                    .Append(interaction.InteractionCode)
                    .Append(" · ")
                    .Append(interaction.EffectCode);
            }

            interactionText.text = builder.ToString();
        }

        public bool ValidateWiring()
        {
            return interactionText != null;
        }
    }
}
