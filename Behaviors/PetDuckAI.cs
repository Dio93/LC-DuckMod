using BepInEx.Logging;
using GameNetcodeStuff;
using LethalLib.Modules;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.HighDefinition;

namespace DuckMod.Behaviors
{
    [RequireComponent(typeof(NavMeshAgent))]
    internal class PetDuckAI : PetAI
    {
        public static List<(float, Material)> materials = new List<(float, Material)>();
        private int shaderID;
        private float nextFlipCooldown = 60f;
        private float nextFlip = 120f;

        protected enum DuckState
        {
            LookingAround,
            Following,
            MovingToItem,
            MovingToEntrance,
            Searching
        }

        protected DuckState duckState;

        override public void Start()
        {
            base.Start();

            Log("Is pet duck owner: " + base.IsOwner +
            "\nSpeed: " + this.agent.speed +
            "\nVelocity: " + this.agent.velocity +
            "\nAcceleration: " + this.agent.acceleration);

            if (base.IsServer)
            {
                float sum = 0;

                // normalize probabilities
                foreach ((float, Material) material in materials)
                {
                    sum += material.Item1;
                }

                // select random material
                int i = -1;
                float sumP = 0;
                float r = UnityEngine.Random.Range(0f, 1f);

                foreach ((float, Material) material in materials)
                {
                    ++i;
                    float p = material.Item1 / sum;

                    if (r < p + sumP)
                    {
                        break;
                    }
                    sumP += p;
                }
                Log("Shader count: " + materials.Count);
                Log("sum: " + sum);
                Log("r: " + r);
                Log("i: " + i);

                this.shaderID = i;
                Log("sumP: " + sumP);
            }

            ChangeShaderServerRpc();
        }

        override public void Update()
        {
            base.Update();

            if (this.nextFlip <= 0)
            {
                DoFlip();
                this.nextFlip = nextFlipCooldown;
            }
            else
            {
                this.nextFlip -= Time.deltaTime;
            }
        }

        public override void DoAI()
        {
            if (GameNetworkManager.Instance == null)
            {
                return;
            }

            if (this.grabbedItems.Count > 0 && this.isInsideShip)
            {
                this.DropAndSync(this.grabbedItems[this.grabbedItems.Count-1]);
            }

            this.targetPlayer = this.GetClosestPlayer();

            if (this.grabbedItems.Count < this.itemCapacity & this.targetItem == null)
            {
                this.targetItem = this.GetClosestItem();
                if (this.targetItem != null)
                {
                    //Log("Target item: " + this.targetItem.name);
                }
            }

            if (this.targetPlayer == null)
            {
                return;
            }

            // open doors if they are infront
            GetDoor();

            float playerDistance = Vector3.Distance(base.transform.position, this.targetPlayer.transform.position);

            if (this.targetItem != null)
            {
                this.duckState = DuckState.MovingToItem;
            }

            // player on another level
            else if ((this.targetPlayer.isInsideFactory && !this.isInFactory) || (!this.targetPlayer.isInsideFactory && this.isInFactory))
            {
                this.duckState = DuckState.MovingToEntrance;
            }
            else if (playerDistance <= this.minPlayerDist)
            {
                this.duckState = DuckState.LookingAround;
            }
            else if (playerDistance <= this.maxPlayerDist)
            {
                this.duckState = DuckState.Following;
            }
            else
            {
                this.duckState = DuckState.Searching;
            }

            switch (this.duckState)
            {
                case DuckState.LookingAround:
                    this.agent.destination = this.transform.position;
                    break;

                case DuckState.Following:
                    if (this.agent.isOnNavMesh)
                    {
                        if (Vector3.Distance(base.transform.position, this.targetPlayer.transform.position) > 3f)
                        {
                            //Log("Walking towards " + this.targetPlayer.name);

                            Vector3 direction = (this.targetPlayer.transform.position - this.transform.position).normalized;

                            this.destination = this.targetPlayer.transform.position - direction * 1.5f;
                        }
                        else
                        {
                            this.destination = base.transform.position;
                        }

                        if (Vector3.Distance(this.transform.position, destination) >= 10f)
                        {
                            this.agent.speed = this.speed * this.sprintMultiplier;
                        }
                        agent.SetDestination(destination);
                    }
                    break;

                case DuckState.MovingToItem:
                    if (targetItem.isHeld)
                    {
                        this.targetItem = null;
                        return;
                    }
                    if (!GrabAndSync())
                    {
                        agent.SetDestination(this.targetItem.transform.position);
                    }
                    break;

                case DuckState.MovingToEntrance:
                    this.Teleport();
                    break;

                case DuckState.Searching:
                    break;
            }
            UseItemServerRpc();
        }

        //override public void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
        //{
        //    if (mls != null)
        //    {
        //        mls.LogInfo("[Pet Duck] Is pet duck owner: " + base.IsOwner);
        //    }
        //}

        //override public void OnCollideWithPlayer(Collider other)
        //{
        //    if (mls != null)
        //    {
        //        mls.LogInfo("[Pet Duck] Is pet duck owner: " + base.IsOwner);
        //    }
        //}

        override public void DetectNoise(UnityEngine.Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot, int noiseID)
        {
            Log("Noise position: " + noisePosition);

            this.audioQuacking.Play();

            if (base.IsOwner)
            {
                if (this.agent.enabled && this.agent.isOnNavMesh)
                {
                    this.agent.SetDestination(noisePosition);
                    
                }
            }
        }

        public void DoFlip()
        {
            this.animator.SetTrigger("Flip");
        }



        // =====================================================================================================================

        public void ChangeShader(int shaderID)
        {
            this.shaderID = shaderID;

            Log("ShaderID: " + shaderID);

            Material material = materials[shaderID].Item2;

            this.meshRenderers[0].material = material;
            if (material.name == "DuckShader Gold")
            {
                Log("Golden Duck!!!");
                HDAdditionalLightData lightData = this.gameObject.AddComponent<HDAdditionalLightData>();
                Light light = this.gameObject.GetComponent<Light>();
                light.intensity = 100f;
                light.color = new Color(1, 0.75f, 0.5f, 1);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ChangeShaderServerRpc()
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
                {
                    ServerRpcParams serverRpcParams = default(ServerRpcParams);
                    FastBufferWriter bufferWriter = __beginSendServerRpc(3079913500u, serverRpcParams, RpcDelivery.Reliable);
                    //BytePacker.WriteValueBitPacked(bufferWriter, shaderID);
                    __endSendServerRpc(ref bufferWriter, 3079913500u, serverRpcParams, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
                {
                    Log("ShaderServerRPC: " + shaderID);
                    ChangeShaderClientRpc(this.shaderID);
                }
            }
        }

        [ClientRpc]
        public void ChangeShaderClientRpc(int shaderID)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(3079913501u, clientRpcParams, RpcDelivery.Reliable);
                BytePacker.WriteValueBitPacked(bufferWriter, shaderID);
                __endSendClientRpc(ref bufferWriter, 3079913501u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
            {
                Log("ShaderClientRPC: " + shaderID);
                ChangeShader(shaderID);
            }
        }
        // =====================================================================================================================

        // RPC Handler
        [RuntimeInitializeOnLoadMethod]
        internal static void InitializeRPCS_PetDuckAI()
        {
            NetworkManager.__rpc_func_table.Add(3079913500u, __rpc_handler_3079913500);
            NetworkManager.__rpc_func_table.Add(3079913501u, __rpc_handler_3079913501);
        }


        // UpdateShaderServerRpc
        private static void __rpc_handler_3079913500(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            else
            {
                //ByteUnpacker.ReadValueBitPacked(reader, out short value);
                ((PetDuckAI)target).__rpc_exec_stage = __RpcExecStage.Server;
                ((PetDuckAI)target).ChangeShaderServerRpc();
                ((PetDuckAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // UpdateShaderClientRpc
        private static void __rpc_handler_3079913501(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out int value);
                ((PetDuckAI)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((PetDuckAI)target).ChangeShaderClientRpc(value);
                ((PetDuckAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // =====================================================================================================================

        public override void Log(string message)
        {
            if (mls != null)
            {
                mls.LogInfo("[Pet Duck] " + message);
            }
        }
    }
}