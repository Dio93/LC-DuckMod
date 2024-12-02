using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace DuckMod.Behaviors
{
    internal class DuckEggBehavior : NetworkBehaviour
    {
        public static GameObject petDuckPrefab;
        protected PhysicsProp physicsProp;
        protected NetworkObject networkObject;
        float wait;

        public void Start()
        {
            networkObject = GetComponent<NetworkObject>();
            physicsProp = GetComponent<PhysicsProp>();
            wait = UnityEngine.Random.Range(5, 30) * 60;
        }

        public void Update()
        {
            if (!physicsProp.isHeld && physicsProp.isInShipRoom)
            {
                wait -= Time.deltaTime;
                if (wait <= 0)
                {
                    // spawn duck and destroy itself
                    GameObject duck = Instantiate(petDuckPrefab, transform.position, transform.rotation);
                    duck.GetComponent<NetworkObject>().Spawn();
                    networkObject.Despawn();
                }
            }
        }
    }
}