using BepInEx.Logging;
using GameNetcodeStuff;
using LethalLib.Modules;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace DuckMod.Behaviors
{
    [RequireComponent(typeof(NavMeshAgent))]
    internal class PetDuckAI : PetAI
    {
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
            OpenDoorInfront();

            float playerDistance = Vector3.Distance(base.transform.position, this.targetPlayer.transform.position);

            // player on another level
            if ((this.targetPlayer.isInsideFactory && !this.isInFactory) || (!this.targetPlayer.isInsideFactory && this.isInFactory))
            {
                this.duckState = DuckState.MovingToEntrance;
            }
            else if (this.targetItem != null)
            {
                this.duckState = DuckState.MovingToItem;
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
                            Log("Walking towards " + this.targetPlayer.name);

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

        //[ServerRpc]
        //public void DoFlipServerRpc()
        //{
        //    NetworkManager networkManager = base.NetworkManager;
        //    if ((object)networkManager == null || !networkManager.IsListening)
        //    {
        //        return;
        //    }
        //    if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
        //    {
        //        //if (base.OwnerClientId != networkManager.LocalClientId)
        //        //{
        //        //    if (networkManager.LogLevel <= Unity.Netcode.LogLevel.Normal)
        //        //    {
        //        //        Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
        //        //    }
        //        //    return;
        //        //}
        //        ServerRpcParams serverRpcParams = default(ServerRpcParams);
        //        FastBufferWriter bufferWriter = __beginSendServerRpc(847487222u, serverRpcParams, RpcDelivery.Reliable);
        //        bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
        //        __endSendServerRpc(ref bufferWriter, 847487222u, serverRpcParams, RpcDelivery.Reliable);
        //    }
        //    if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
        //    {
        //        DropItemClientRpc(objectRef);
        //    }
        //}



        //private static void __rpc_handler_847487222(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        //{
        //    NetworkManager networkManager = target.NetworkManager;
        //    if ((object)networkManager == null || !networkManager.IsListening)
        //    {
        //        return;
        //    }
        //    //if (rpcParams.Server.Receive.SenderClientId != target.OwnerClientId)
        //    //{
        //    //    if (networkManager.LogLevel <= Unity.Netcode.LogLevel.Normal)
        //    //    {
        //    //        Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
        //    //    }
        //    //}
        //    else
        //    {
        //        reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
        //        ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Server;
        //        ((PetAI)target).DropItemServerRpc(value);
        //        ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
        //    }
        //}

        //// DropItemClientRpc
        //private static void __rpc_handler_847487223(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        //{
        //    NetworkManager networkManager = target.NetworkManager;
        //    if ((object)networkManager != null && networkManager.IsListening)
        //    {
        //        reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
        //        ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Client;
        //        ((PetAI)target).DropItemClientRpc(value);
        //        ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
        //    }
        //}

        public override void Log(string message)
        {
            if (mls != null)
            {
                mls.LogInfo("[Pet Duck] " + message);
            }
        }
    }
}