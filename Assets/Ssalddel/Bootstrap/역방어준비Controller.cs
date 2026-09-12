using System;
using System.Linq;
using Ssalddel.Simulation.Application;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(역방어준비View))]
    public sealed class 역방어준비Controller:MonoBehaviour
    {
        private 역방어준비View view;
        private void OnEnable()
        {
            view=GetComponent<역방어준비View>();
            var catalog=view.LoadCatalog();
            if(catalog==null)return;
            view.Configure(역방어준비RuntimeFactory.Create("preparation:"+Guid.NewGuid().ToString("N"),
                catalog.mapRevision,catalog.mapHash,catalog.slots.Select(x=>x.id)));
        }
        private void OnDisable(){if(view!=null)view.Configure(null);}
    }
}
