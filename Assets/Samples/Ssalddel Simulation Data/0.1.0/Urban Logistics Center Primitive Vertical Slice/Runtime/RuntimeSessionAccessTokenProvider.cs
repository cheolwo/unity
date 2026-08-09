using System;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter
{
    public sealed class RuntimeSessionAccessTokenProvider
        : MonoBehaviour, IRuntimeAccessTokenProvider
    {
        [NonSerialized]
        private string accessToken = string.Empty;

        public void SetAccessToken(string token)
        {
            accessToken = token?.Trim() ?? string.Empty;
        }

        public void ClearAccessToken()
        {
            accessToken = string.Empty;
        }

        public string GetAccessToken()
        {
            return accessToken;
        }
    }
}
