using System;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    public interface IObservationDetailNavigator
    {
        bool Navigate(PublicWorldMarker marker);
    }

    public sealed class ObservationDetailNavigator : IObservationDetailNavigator
    {
        private readonly Uri baseAddress;
        private readonly Action<string> open;

        public ObservationDetailNavigator(string detailBaseUrl, Action<string> openUrl = null)
        {
            baseAddress = new Uri((detailBaseUrl ?? string.Empty).TrimEnd('/') + "/", UriKind.Absolute);
            open = openUrl ?? UnityEngine.Application.OpenURL;
        }

        public bool Navigate(PublicWorldMarker marker)
        {
            if (marker == null || !TryResolve(marker.DetailHref, out var target)) return false;
            open(target.AbsoluteUri);
            return true;
        }

        public bool TryResolve(string detailHref, out Uri target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(detailHref)
                || !detailHref.StartsWith("/", StringComparison.Ordinal)
                || detailHref.StartsWith("//", StringComparison.Ordinal)
                || detailHref.Contains("..")) return false;

            target = new Uri(baseAddress, detailHref.TrimStart('/'));
            return target.Scheme == Uri.UriSchemeHttp || target.Scheme == Uri.UriSchemeHttps;
        }
    }
}
