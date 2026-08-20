using System.Collections.Generic;
using NUnit.Framework;
using Ssalddel.Unity.ImmersiveWorld;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 몰입경관InstanceTests
    {
        [Test]
        public void Nature는항상유지하고_준비된전문경관하나만원자활성화한다()
        {
            var owned = new List<GameObject>();
            try
            {
                var host = Create("Host", owned);
                var player = Create("Player", owned).transform;
                var nature = Binding(몰입WorldInstanceCodes.NatureHome,
                    Create("Nature", owned), true);
                var farm = Binding(몰입WorldInstanceCodes.Farm,
                    Create("Farm", owned), true);
                var town = Binding(몰입WorldInstanceCodes.Town,
                    Create("Town", owned), true);
                var city = Binding(몰입WorldInstanceCodes.CityHub,
                    Create("CityHub", owned), true);
                farm.EntryAnchor.position = new Vector3(30f, 0f, 10f);
                var controller = host.AddComponent<몰입경관InstanceController>();
                controller.Configure(nature.InstanceRoot, player,
                    new[] { nature, farm, town, city });

                Assert.That(nature.InstanceRoot.activeSelf, Is.True);
                Assert.That(controller.ActiveSpecialistInstanceCount, Is.Zero);
                Assert.That(controller.TryActivatePreparedInstance(
                    몰입WorldInstanceCodes.Farm), Is.True);

                Assert.That(nature.InstanceRoot.activeSelf, Is.True);
                Assert.That(farm.InstanceRoot.activeSelf, Is.True);
                Assert.That(town.InstanceRoot.activeSelf, Is.False);
                Assert.That(city.InstanceRoot.activeSelf, Is.False);
                Assert.That(controller.ActiveSpecialistInstanceCount, Is.EqualTo(1));
                Assert.That(player.position, Is.EqualTo(farm.EntryAnchor.position));
                Assert.That(controller.ChangesWorldState, Is.False);
                Assert.That(controller.PresentationOnly, Is.True);
            }
            finally
            {
                foreach (var value in owned)
                    if (value != null) Object.DestroyImmediate(value);
            }
        }

        [Test]
        public void 준비되지않은경관은_현재Nature와플레이어위치를보존한다()
        {
            var owned = new List<GameObject>();
            try
            {
                var host = Create("Host", owned);
                var player = Create("Player", owned).transform;
                var nature = Binding(몰입WorldInstanceCodes.NatureHome,
                    Create("Nature", owned), true);
                var farm = Binding(몰입WorldInstanceCodes.Farm,
                    Create("Farm", owned), false);
                var town = Binding(몰입WorldInstanceCodes.Town,
                    Create("Town", owned), false);
                var city = Binding(몰입WorldInstanceCodes.CityHub,
                    Create("CityHub", owned), false);
                nature.EntryAnchor.position = new Vector3(1f, 0f, 2f);
                farm.EntryAnchor.position = new Vector3(50f, 0f, 50f);
                var controller = host.AddComponent<몰입경관InstanceController>();
                controller.Configure(nature.InstanceRoot, player,
                    new[] { nature, farm, town, city });
                var before = player.position;

                Assert.That(controller.TryActivatePreparedInstance(
                    몰입WorldInstanceCodes.Farm), Is.False);
                Assert.That(controller.ActiveInstanceStableId,
                    Is.EqualTo(몰입WorldInstanceCodes.NatureHome));
                Assert.That(controller.ActiveSpecialistInstanceCount, Is.Zero);
                Assert.That(player.position, Is.EqualTo(before));
                Assert.That(nature.InstanceRoot.activeSelf, Is.True);
            }
            finally
            {
                foreach (var value in owned)
                    if (value != null) Object.DestroyImmediate(value);
            }
        }

        private static 몰입경관InstanceBinding Binding(
            string stableId, GameObject root, bool ready)
        {
            var anchor = new GameObject("EntryAnchor").transform;
            anchor.SetParent(root.transform, false);
            var binding = new 몰입경관InstanceBinding();
            binding.Configure(stableId, root, anchor, ready);
            return binding;
        }

        private static GameObject Create(string name, ICollection<GameObject> owned)
        {
            var value = new GameObject(name);
            owned.Add(value);
            return value;
        }
    }
}
