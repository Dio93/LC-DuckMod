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
        float wait = 30f;

        public void Start()
        {
            networkObject = GetComponent<NetworkObject>();
            physicsProp = GetComponent<PhysicsProp>();
        }

        public void Update()
        {
            wait -= Time.deltaTime;
            if (wait <= 0)
            {
                wait = 10f;
                if (!physicsProp.isHeld && physicsProp.isInShipRoom && UnityEngine.Random.Range(0, 1f) < 0.01f)
                {
                    if (petDuckPrefab != null && IsOwner)
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
}