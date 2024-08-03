using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine.PlayerLoop;

namespace DuckMod.Behaviors
{
    internal class DuckEggBehavior : UnityEngine.MonoBehaviour 
    {
        public static PetDuckAI petDuckPrefab;
        int startDay;

        public void Start()
        {
            startDay = StartOfRound.Instance.daysPlayersSurvivedInARow;
        }

        public void Update()
        {
            if (StartOfRound.Instance.daysPlayersSurvivedInARow - startDay == 2) 
            {
                if (petDuckPrefab != null)
                {
                    NetworkManager.Instantiate(petDuckPrefab, this.transform.position, this.transform.rotation);
                }
            }
        }
    }
}