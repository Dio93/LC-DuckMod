using BepInEx.Logging;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static DuckMod.Behaviors.PetAI;

namespace DuckMod.Behaviors
{
    internal abstract class PetAI : NetworkBehaviour, IHittable, INoiseListener
    {
        public enum ShipState
        {
            InSpace,
            OnMoon
        }
        public static ManualLogSource mls;

        protected NavMeshAgent agent;
        protected AudioSource audioSource;
        protected PhysicsProp physicsProp;
        protected NetworkObject networkObject;

        protected int maxHp = 10;
        protected int hp;
        protected bool isInsideShip;
        protected bool isInFactory;

        protected float checkEnemyCooldown = 10;
        protected float lastCheckEnemy = -1f;

        protected static ShipState shipState;

        protected GrabbableObject targetItem;
        protected IList<GrabbableObject> grabbableItems = new List<GrabbableObject>();
        protected IList<GrabbableObject> grabbedItems = new List<GrabbableObject>();
        protected int maxGrabbedItems = 1;

        protected PlayerControllerB targetPlayer;
        protected Vector3 destination;
        protected float minPlayerDist = 5f;
        protected float maxPlayerDist = 20f;

        protected RaycastHit[] hits;

        private Vector3 serverPosition;
        private float updatePositionThreshold = 0.01f;
        private float previousYRotation;
        private short targetYRotation;
        private bool sendingGrabOrDropRPC;

        public virtual void Start()
        {
            this.agent = GetComponent<NavMeshAgent>();
            this.networkObject = GetComponent<NetworkObject>();
            this.audioSource = GetComponent<AudioSource>();
            this.physicsProp = GetComponent<PhysicsProp>();

            this.agent.enabled = false;

            if (base.IsOwner)
            {
                Init();
                this.hp = this.maxHp;
                SyncPosition();
                SyncRotation();
            }
        }

        public virtual void Update()
        {
            if (base.IsOwner)
            {
                Init();
                DoAI();
                SyncPosition();
                SyncRotation();
            }
        }

        public abstract void DoAI();

        protected bool IsInsideShip()
        {
            RaycastHit[] hits = Physics.RaycastAll(base.transform.position, Vector3.down, 0.2f);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.name == "ShipInside")
                {
                    return true;
                }
            }

            return false;
        }

        protected void Init()
        {
            this.isInsideShip = this.IsInsideShip();

            if (isInsideShip && base.transform.parent == null)
            {
                EnterShip();
            }
            else if (!isInsideShip && base.transform.parent != null)
            {
                LeaveShip();
            }

            switch (shipState)
            {
                case ShipState.InSpace:
                    if (StartOfRound.Instance.shipHasLanded)
                    {
                        shipState = ShipState.OnMoon;
                        if (mls != null)
                        {
                            mls.LogInfo("[Pet Duck] Ship has landed!");
                        }

                        StartRound(true);
                    }
                    break;

                case ShipState.OnMoon:
                    if (StartOfRound.Instance.shipIsLeaving)
                    {
                        shipState = ShipState.InSpace;
                        if (mls != null)
                        {
                            mls.LogInfo("[Pet Duck] Ship is leaving!");
                        }

                        EndRound();
                    }
                    break;
            }
        }

        protected void EnterShip()
        {
            base.transform.SetParent(StartOfRound.Instance.elevatorTransform, true);
            OnEnterShip();
        }

        protected void LeaveShip()
        {
            base.transform.SetParent(null, true);
            OnLeaveShip();
        }

        protected void StartRound(bool enableAgent)
        {
            this.agent.enabled = enableAgent;
            this.physicsProp.enabled = false;
            this.grabbableItems.Clear();
            this.grabbableItems = FindObjectsOfType<GrabbableObject>();
            OnStartRound();
        }

        protected void EndRound()
        {
            this.agent.enabled = false;
            this.physicsProp.enabled = true;
            OnEndRound();
        }

        protected virtual void OnEnterShip() { }
        protected virtual void OnLeaveShip() { }
        protected virtual void OnStartRound() { }
        protected virtual void OnEndRound() { }

        protected PlayerControllerB GetClosestPlayer()
        {
            float foundMinDistance = Mathf.Infinity;
            PlayerControllerB closestPlayer = null;

            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                float distance = Vector3.Distance(base.transform.position, player.transform.position);
                if (distance < foundMinDistance)
                {
                    closestPlayer = player;
                    foundMinDistance = distance;
                }
            }

            return closestPlayer;
        }

        public void UpdateCollisions()
        {
            this.hits = Physics.BoxCastAll(base.transform.position, Vector3.one * 2f, base.transform.forward, base.transform.rotation, 10f);
        }

        public bool CheckForEnemies()
        {
            foreach (RaycastHit hit in this.hits)
            {
                mls.LogInfo("[PetDuck] hit: " + hit.transform.gameObject.name);
                if (hit.collider.gameObject.GetComponent<EnemyAI>() != null && hit.collider.gameObject.GetComponent<PetDuckAI>() == null)
                {
                    return true;
                }
            }
            return false;
        }

        protected GrabbableObject GetClosestItem()
        {
            GrabbableObject targetItem = null;
            float nearest = 10f;
            foreach (GrabbableObject item in this.grabbableItems)
            {
                if (mls != null)
                {
                    //mls.LogInfo("[Pet Duck] tmp item: " + item.name);
                }

                Vector3 pos = RoundManager.Instance.GetNavMeshPosition(item.transform.position);

                if (item != null && item.GetComponent<PetDuckAI>() == null && RoundManager.Instance.GotNavMeshPositionResult && !item.isInShipRoom)
                {
                    float dist = Vector3.Distance(base.transform.position, item.transform.position);
                    if (dist <= nearest && item.grabbable && !item.isHeld)
                    {
                        nearest = dist;
                        targetItem = item;
                    }
                }
            }

            return targetItem;
        }

        // =====================================================================================================================

        public void SetHP(int hp)
        {
            this.hp = Mathf.Clamp(hp, 0, this.maxHp);

            if (this.hp <= 0)
            {
                Destroy();
            }
        }

        public void AddHP(int value)
        {
            this.hp = Mathf.Clamp(this.hp + value, 0, this.maxHp);

            if (this.hp <= 0)
            {
                Destroy();
            }
        }

        public void Destroy()
        {
            OnDying();
            this.networkObject.Despawn();
        }

        protected virtual void OnDying()
        {

        }

        // =====================================================================================================================
        // Server Shit

        // Sync Position
        public void SyncPosition()
        {
            if (Vector3.Distance(serverPosition, base.transform.position) > updatePositionThreshold)
            {
                serverPosition = base.transform.position;
                if (base.IsServer)
                {
                    UpdatePositionClientRpc(serverPosition);
                }
                else
                {
                    UpdatePositionServerRpc(serverPosition);
                }
            }
        }


        [ServerRpc]
        private void UpdatePositionServerRpc(Vector3 newPos)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
            {
                if (base.OwnerClientId != networkManager.LocalClientId)
                {
                    if (networkManager.LogLevel <= Unity.Netcode.LogLevel.Normal)
                    {
                        Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
                    }
                    return;
                }
                mls.LogInfo("[Pet Duck] Calling UpdatePositionServerRpc Handle!");
                ServerRpcParams serverRpcParams = default(ServerRpcParams);
                FastBufferWriter bufferWriter = __beginSendServerRpc(4287979890u, serverRpcParams, RpcDelivery.Reliable);
                bufferWriter.WriteValueSafe(in newPos);
                __endSendServerRpc(ref bufferWriter, 4287979890u, serverRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
            {
                mls.LogInfo("[Pet Duck] Calling UpdatePositionClientRpc!");
                UpdatePositionClientRpc(newPos);
            }
        }

        [ClientRpc]
        private void UpdatePositionClientRpc(Vector3 newPos)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
                {
                    mls.LogInfo("[Pet Duck] Calling UpdatePositionClientRpc Handle!");
                    ClientRpcParams clientRpcParams = default(ClientRpcParams);
                    FastBufferWriter bufferWriter = __beginSendClientRpc(4287979891u, clientRpcParams, RpcDelivery.Reliable);
                    bufferWriter.WriteValueSafe(in newPos);
                    __endSendClientRpc(ref bufferWriter, 4287979891u, clientRpcParams, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost) && !base.IsOwner)
                {
                    mls.LogInfo("[Pet Duck] UpdatePositionClientRpc was called!");
                    serverPosition = newPos;
                    OnSyncPositionFromServer(newPos);
                }
            }
        }

        protected virtual void OnSyncPositionFromServer(Vector3 newPos)
        {

        }

        
        // =====================================================================================================================

        // Sync Rotation
        public void SyncRotation()
        {
            if (Vector3.Distance(serverPosition, base.transform.position) > updatePositionThreshold)
            {
                targetYRotation = (short)base.transform.rotation.eulerAngles.y;

                if (base.IsServer)
                {
                    UpdateRotationClientRpc(targetYRotation);
                }
                else
                {
                    UpdateRotationServerRpc(targetYRotation);
                }
            }
        }

        [ServerRpc]
        private void UpdateRotationServerRpc(short rotationY)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
            {
                if (base.OwnerClientId != networkManager.LocalClientId)
                {
                    if (networkManager.LogLevel <= Unity.Netcode.LogLevel.Normal)
                    {
                        Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
                    }
                    return;
                }
                ServerRpcParams serverRpcParams = default(ServerRpcParams);
                FastBufferWriter bufferWriter = __beginSendServerRpc(3079913705u, serverRpcParams, RpcDelivery.Reliable);
                BytePacker.WriteValueBitPacked(bufferWriter, rotationY);
                __endSendServerRpc(ref bufferWriter, 3079913705u, serverRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
            {
                UpdateRotationClientRpc(rotationY);
            }
        }

        [ClientRpc]
        private void UpdateRotationClientRpc(short rotationY)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
                {
                    ClientRpcParams clientRpcParams = default(ClientRpcParams);
                    FastBufferWriter bufferWriter = __beginSendClientRpc(1258118513u, clientRpcParams, RpcDelivery.Reliable);
                    BytePacker.WriteValueBitPacked(bufferWriter, rotationY);
                    __endSendClientRpc(ref bufferWriter, 1258118513u, clientRpcParams, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
                {
                    previousYRotation = base.transform.eulerAngles.y;
                    targetYRotation = rotationY;
                    OnSyncRotationFromServer(rotationY);
                }
            }
        }

        protected virtual void OnSyncRotationFromServer(short newYRot)
        {

        }

        // Grab and Drop

        protected void GrabItem(NetworkObject networkObject)
        {
            if (IsServer)
            {
                GrabbableObject item = networkObject.gameObject.GetComponent<GrabbableObject>();
                this.grabbedItems.Add(item);
                this.targetItem = null;
                item.parentObject = this.transform;
                item.EnablePhysics(false);
                item.isHeld = true;
                item.hasHitGround = false;

            }
        }

        protected void DropItem(GrabbableObject item)
        {
            item.parentObject = null;
            item.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
            item.EnablePhysics(enable: true);
            item.fallTime = 0f;
            item.startFallingPosition = item.transform.parent.InverseTransformPoint(item.transform.position);
            item.targetFloorPosition = item.transform.parent.InverseTransformPoint(item.transform.position);
            item.floorYRot = -1;
            item.DiscardItemFromEnemy();
        }

        protected bool GrabTargetItemIfClose()
        {
            if (targetItem != null && this.grabbedItems.Count < this.maxGrabbedItems && Vector3.Distance(base.transform.position, targetItem.transform.position) < 0.75f)
            {
                NetworkObject component = targetItem.GetComponent<NetworkObject>();
                // SwitchToBehaviourStateOnLocalClient(1);
                GrabItem(component);
                this.sendingGrabOrDropRPC = true;
                GrabItemServerRpc(component);
                return true;
            }
            return false;
        }

        [ServerRpc]
        private void GrabItemServerRpc(NetworkObjectReference objectRef)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
            {
                if (base.OwnerClientId != networkManager.LocalClientId)
                {
                    if (networkManager.LogLevel <= Unity.Netcode.LogLevel.Normal)
                    {
                        Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
                    }
                    return;
                }
                ServerRpcParams serverRpcParams = default(ServerRpcParams);
                FastBufferWriter bufferWriter = __beginSendServerRpc(2358561451u, serverRpcParams, RpcDelivery.Reliable);
                bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
                __endSendServerRpc(ref bufferWriter, 2358561451u, serverRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
            {
                GrabItemClientRpc(objectRef);
            }
        }

        [ClientRpc]
        private void GrabItemClientRpc(NetworkObjectReference objectRef)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(847487220u, clientRpcParams, RpcDelivery.Reliable);
                bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
                __endSendClientRpc(ref bufferWriter, 847487220u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
            {
                //SwitchToBehaviourStateOnLocalClient(1);
                if (objectRef.TryGet(out var networkObject))
                {
                    GrabItem(networkObject);
                }
                else
                {
                    Debug.LogError(base.gameObject.name + ": Failed to get network object from network object reference (Grab item RPC)");
                }
            }
        }

        // =====================================================================================================================
        //private void DropItem(NetworkObject item, Vector3 targetFloorPosition, bool droppingInNest = true)
        //{
        //    if (sendingGrabOrDropRPC)
        //    {
        //        sendingGrabOrDropRPC = false;
        //        return;
        //    }
        //    if (heldItem == null)
        //    {
        //        Debug.LogError("Hoarder bug: my held item is null when attempting to drop it!!");
        //        return;
        //    }
        //    GrabbableObject itemGrabbableObject = heldItem.itemGrabbableObject;
        //    itemGrabbableObject.parentObject = null;
        //    itemGrabbableObject.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
        //    itemGrabbableObject.EnablePhysics(enable: true);
        //    itemGrabbableObject.fallTime = 0f;
        //    itemGrabbableObject.startFallingPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(itemGrabbableObject.transform.position);
        //    itemGrabbableObject.targetFloorPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(targetFloorPosition);
        //    itemGrabbableObject.floorYRot = -1;
        //    itemGrabbableObject.DiscardItemFromEnemy();
        //    heldItem = null;
        //    if (!droppingInNest)
        //    {
        //        grabbableObjectsInMap.Add(itemGrabbableObject.gameObject);
        //    }
        //}

        //private void DropItemAndCallDropRPC(NetworkObject dropItemNetworkObject, bool droppedInNest = true)
        //{
        //    Vector3 targetFloorPosition = RoundManager.Instance.RandomlyOffsetPosition(heldItem.itemGrabbableObject.GetItemFloorPosition(), 1.2f, 0.4f);
        //    DropItem(dropItemNetworkObject, targetFloorPosition);
        //    sendingGrabOrDropRPC = true;
        //    DropItemServerRpc(dropItemNetworkObject, targetFloorPosition, droppedInNest);
        //}

        //[ClientRpc]
        //public void DropItemClientRpc(NetworkObjectReference objectRef, Vector3 targetFloorPosition, bool droppedInNest)
        //{
        //    NetworkManager networkManager = base.NetworkManager;
        //    if ((object)networkManager == null || !networkManager.IsListening)
        //    {
        //        return;
        //    }
        //    if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
        //    {
        //        ClientRpcParams clientRpcParams = default(ClientRpcParams);
        //        FastBufferWriter bufferWriter = __beginSendClientRpc(847487221u, clientRpcParams, RpcDelivery.Reliable);
        //        bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
        //        bufferWriter.WriteValueSafe(in targetFloorPosition);
        //        bufferWriter.WriteValueSafe(in droppedInNest, default(FastBufferWriter.ForPrimitives));
        //        __endSendClientRpc(ref bufferWriter, 847487221u, clientRpcParams, RpcDelivery.Reliable);
        //    }
        //    if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
        //    {
        //        if (objectRef.TryGet(out var networkObject))
        //        {
        //            DropItem(networkObject, targetFloorPosition, droppedInNest);
        //        }
        //        else
        //        {
        //            Debug.LogError(base.gameObject.name + ": Failed to get network object from network object reference (Drop item RPC)");
        //        }
        //    }
        //}


        //[ServerRpc]
        //public void DropItemServerRpc(NetworkObjectReference objectRef, Vector3 targetFloorPosition, bool droppedInNest)
        //{
        //    NetworkManager networkManager = base.NetworkManager;
        //    if ((object)networkManager == null || !networkManager.IsListening)
        //    {
        //        return;
        //    }
        //    if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
        //    {
        //        if (base.OwnerClientId != networkManager.LocalClientId)
        //        {
        //            if (networkManager.LogLevel <= Unity.Netcode.LogLevel.Normal)
        //            {
        //                Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
        //            }
        //            return;
        //        }
        //        ServerRpcParams serverRpcParams = default(ServerRpcParams);
        //        FastBufferWriter bufferWriter = __beginSendServerRpc(3510928244u, serverRpcParams, RpcDelivery.Reliable);
        //        bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
        //        bufferWriter.WriteValueSafe(in targetFloorPosition);
        //        bufferWriter.WriteValueSafe(in droppedInNest, default(FastBufferWriter.ForPrimitives));
        //        __endSendServerRpc(ref bufferWriter, 3510928244u, serverRpcParams, RpcDelivery.Reliable);
        //    }
        //    if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
        //    {
        //        DropItemClientRpc(objectRef, targetFloorPosition, droppedInNest);
        //    }
        //}

        // =====================================================================================================================

        public virtual void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            AddHP(-force);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HitServerRpc(int force, int playerWhoHit, bool playHitSFX, int hitID = -1)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
                {
                    ServerRpcParams serverRpcParams = default(ServerRpcParams);
                    FastBufferWriter bufferWriter = __beginSendServerRpc(3538577804u, serverRpcParams, RpcDelivery.Reliable);
                    BytePacker.WriteValueBitPacked(bufferWriter, force);
                    BytePacker.WriteValueBitPacked(bufferWriter, playerWhoHit);
                    bufferWriter.WriteValueSafe(in playHitSFX, default(FastBufferWriter.ForPrimitives));
                    BytePacker.WriteValueBitPacked(bufferWriter, hitID);
                    __endSendServerRpc(ref bufferWriter, 3538577804u, serverRpcParams, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
                {
                    HitClientRpc(force, playerWhoHit, playHitSFX, hitID);
                }
            }
        }

        [ClientRpc]
        public void HitClientRpc(int force, int playerWhoHit, bool playHitSFX, int hitID = -1)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(601871377u, clientRpcParams, RpcDelivery.Reliable);
                BytePacker.WriteValueBitPacked(bufferWriter, force);
                BytePacker.WriteValueBitPacked(bufferWriter, playerWhoHit);
                bufferWriter.WriteValueSafe(in playHitSFX, default(FastBufferWriter.ForPrimitives));
                BytePacker.WriteValueBitPacked(bufferWriter, hitID);
                __endSendClientRpc(ref bufferWriter, 601871377u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost) && playerWhoHit != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
            {
                if (playerWhoHit == -1)
                {
                    HitEnemy(force, null, playHitSFX, hitID);
                }
                else
                {
                    HitEnemy(force, StartOfRound.Instance.allPlayerScripts[playerWhoHit], playHitSFX, hitID);
                }
            }
        }

        // =====================================================================================================================

        public virtual void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot, int noiseID)
        {
        }

        public virtual bool Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {

            return false;
        }

        // =====================================================================================================================

        // RPC Handler
        [RuntimeInitializeOnLoadMethod]
        internal static void InitializeRPCS_PetAI()
        {
            NetworkManager.__rpc_func_table.Add(4287979890u, __rpc_handler_4287979890);
            NetworkManager.__rpc_func_table.Add(4287979891u, __rpc_handler_4287979891);
            //NetworkManager.__rpc_func_table.Add(3510928244u, __rpc_handler_3510928244);
            NetworkManager.__rpc_func_table.Add(2358561450u, __rpc_handler_2358561450);
            NetworkManager.__rpc_func_table.Add(847487220u, __rpc_handler_847487220);
            //NetworkManager.__rpc_func_table.Add(1536760829u, __rpc_handler_1536760829);
            //NetworkManager.__rpc_func_table.Add(1884379629u, __rpc_handler_1884379629);
            //NetworkManager.__rpc_func_table.Add(1031891902u, __rpc_handler_1031891902);
        }

        // UpdatePositionServerRpc
        private static void __rpc_handler_4287979890(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (rpcParams.Server.Receive.SenderClientId != target.OwnerClientId)
            {
                if (networkManager.LogLevel <= Unity.Netcode.LogLevel.Normal)
                {
                    Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
                }
            }
            else
            {
                mls.LogInfo("[Pet Duck] UpdatePositionServerRpc Handle was called!");
                reader.ReadValueSafe(out Vector3 value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Server;
                ((PetAI)target).UpdatePositionServerRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // UpdatePositionClientRpc
        private static void __rpc_handler_4287979891(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                mls.LogInfo("[Pet Duck] UpdatePositionClientRpc Handle was called!");
                reader.ReadValueSafe(out Vector3 value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((PetAI)target).UpdatePositionClientRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // GrabItemServerRpc
        private static void __rpc_handler_2358561450(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (rpcParams.Server.Receive.SenderClientId != target.OwnerClientId)
            {
                if (networkManager.LogLevel <= Unity.Netcode.LogLevel.Normal)
                {
                    Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
                }
            }
            else
            {
                reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Server;
                ((PetAI)target).GrabItemServerRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // GrabItemClientRpc
        private static void __rpc_handler_847487220(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((PetAI)target).GrabItemClientRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }
    }
}
