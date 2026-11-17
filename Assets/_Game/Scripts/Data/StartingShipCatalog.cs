using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Resources catalog references native definitions without duplicating their assets.</summary>
    [CreateAssetMenu(menuName = "Naval/Starting Ship Catalog")]
    public sealed class StartingShipCatalog : ScriptableObject
    {
        [SerializeField] private StartingShipConcept[] ships = Array.Empty<StartingShipConcept>();
        public static IReadOnlyList<StartingShipConcept> All
        {
            get
            {
                var catalog = Resources.Load<StartingShipCatalog>("StartingShips/Catalog");
                return catalog != null ? catalog.ships : Array.Empty<StartingShipConcept>();
            }
        }

        /// <summary>id로 찾는다(저장 불러오기). 없으면 null.</summary>
        public static StartingShipConcept Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var ship in All) if (ship != null && ship.Id == id) return ship;
            return null;
        }

        public static StartingShipConcept Get(StartShipKind kind)
        {
            foreach (var ship in All) if (ship != null && ship.Kind == kind) return ship;
            return null;
        }
    }
}
