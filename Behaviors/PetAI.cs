using BepInEx.Logging;
using DunGen;
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
        private static PetAI petAI;
        public enum ShipState
        {
            InSpace,
            OnMoon
        }

        public static ManualLogSource mls;

        protected NavMeshAgent agent;
        protected AudioSource audioQuacking;
        protected AudioSource audioWalking;
        protected PhysicsProp physicsProp;
        protected NetworkObject networkObject;
        protected Animator animator;
        protected Transform itemHolder;
        protected Transform interactPatter;
        protected InteractTrigger interactTrigger;

        public int maxHp = 10;
        protected int hp;
        public bool hittable;
        protected bool isInsideShip;
        protected bool isInFactory;

        protected float checkEnemyCooldown = 10;
        protected float lastCheckEnemy = -1f;

        protected static ShipState shipState;

        protected float grabbingCooldown = 0f;
        protected GrabbableObject targetItem;
        protected IList<GrabbableObject> grabbableItems = new List<GrabbableObject>();
        protected IList<GrabbableObject> grabbedItems = new List<GrabbableObject>();
        public int itemCapacity = 1;

        public float speedFactor = 0.8f;
        private float nextSpeedCheck = 0f;
        private float nextSpeedCheckCooldown = 30f;
        protected PlayerControllerB targetPlayer;
        protected Vector3 destination;
        protected float minPlayerDist = 4f;
        protected float maxPlayerDist = Mathf.Infinity;

        protected RaycastHit[] hits;

        protected float sprintMultiplier = 2.25f;
        protected float speed;
        protected float curSpeed;
        private Vector3 serverPosition;
        private float updatePositionThreshold = 0.01f;
        private float previousYRotation;
        private short targetYRotation;
        private Vector3 tempVelocity = Vector3.zero;
        private float syncMovementSpeed = 0.1f;

        private bool freeze = false;

        public virtual void Start()
        {
            //if (petAI == null)
            //{
            //    petAI = this;
            //}
            //else
            //{
            //    Destroy(this.gameObject);
            //    return;
            //}

            this.hp = this.maxHp;
            this.agent = GetComponent<NavMeshAgent>();

            // this.agent.speed = 4.5f;

            shipState = StartOfRound.Instance.shipHasLanded ? ShipState.OnMoon : ShipState.InSpace;

            this.networkObject = GetComponent<NetworkObject>();

            foreach (AudioSource audioSource in GetComponents<AudioSource>())
            {
                if (audioSource.clip.name == "duck_quacking")
                {
                    Log("Quacking Audio Clip found.");
                    this.audioQuacking = audioSource;
                    this.audioQuacking.Play();
                }
                else if (audioSource.clip.name == "duck_walking")
                {
                    Log("Walking Audio Clip found.");
                    this.audioWalking = audioSource;
                }
            }

            this.audioQuacking = GetComponent<AudioSource>();
            this.physicsProp = GetComponent<PhysicsProp>();
            this.animator = GetComponentInChildren<Animator>();
            this.itemHolder = transform.GetChild(1);

            this.interactTrigger = itemHolder.gameObject.GetComponent<InteractTrigger>();
            this.interactTrigger.onInteract = new InteractEvent();
            this.interactTrigger.onInteract.AddListener(Interact);

            this.interactPatter = transform.GetChild(2);
            InteractTrigger iP = this.interactPatter.GetComponent<InteractTrigger>();
            iP.onInteract = new InteractEvent();
            iP.onInteract.AddListener(InteractPat);

            if (this.itemHolder == null)
            {
                this.itemHolder = this.transform;
            }

            if (base.IsOwner)
            {
                this.agent.enabled = IsOwner;
                Init();
                SyncPosition();
                SyncRotation();
            }
        }

        public virtual void Update()
        {
            Init();
            if (base.IsOwner)
            {
                if (this.agent.enabled)
                {
                    if (this.targetPlayer != null)
                    {
                        if (this.nextSpeedCheck <= 0)
                        {
                            this.speed = targetPlayer.movementSpeed * this.speedFactor;
                            this.nextSpeedCheck = this.nextSpeedCheckCooldown;
                        }
                        else
                        {
                            this.nextSpeedCheck -= Time.deltaTime;
                        }
                    }
                    DoAI();
                }
                SyncPosition();
                SyncRotation();
                SyncSpeedServerRpc(this.agent.velocity.magnitude);
            }
            else
            {
                this.transform.position = Vector3.SmoothDamp(base.transform.position, serverPosition, ref tempVelocity, syncMovementSpeed);
                //base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, targetYRotation, 15f * Time.deltaTime), base.transform.eulerAngles.z);
                //base.transform.position = this.serverPosition;
                this.transform.rotation = Quaternion.Euler(this.transform.rotation.eulerAngles.x, this.targetYRotation, this.transform.rotation.eulerAngles.z);
            }

            // check for enemies
            if (Time.time - this.lastCheckEnemy >= this.checkEnemyCooldown)
            {
                this.lastCheckEnemy = Time.time;

                if (this.CheckForEnemies())
                {
                    this.StartQuacking();
                    this.lastCheckEnemy = Time.time + 10;
                }
            }
        }

        public abstract void DoAI();

        protected void StartQuacking()
        {
            if (this.audioQuacking != null)
            {
                if (!this.audioQuacking.isPlaying)
                {
                    this.audioQuacking.Play();
                }
            }
        }

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
            this.grabbingCooldown -= Time.deltaTime;
            this.UpdateCollisions();
            this.isInsideShip = this.IsInsideShip();
            this.agent.speed = this.speed;

            switch (shipState)
            {
                case ShipState.InSpace:
                    if (StartOfRound.Instance.shipHasLanded)
                    {
                        shipState = ShipState.OnMoon;
                        Log("Ship has landed!");

                        StartRound(IsOwner);
                    }
                    break;

                case ShipState.OnMoon:
                    if (StartOfRound.Instance.shipIsLeaving)
                    {
                        shipState = ShipState.InSpace;
                        Log("Ship is leaving!");

                        EndRound();
                    }
                    break;
            }

            if (!freeze)
            {
                if (isInsideShip && base.transform.parent == null)
                {
                    EnterShip();
                }
                else if (!isInsideShip && base.transform.parent != null)
                {
                    LeaveShip();
                }
            }
        }

        protected void EnterShip()
        {
            base.transform.SetParent(StartOfRound.Instance.elevatorTransform, true);
            Log("Enters ship!");
            OnEnterShip();
        }

        protected void LeaveShip()
        {
            Log("Leaves ship!");
            base.transform.SetParent(null, true);
            OnLeaveShip();
        }

        protected void StartRound(bool enableAgent)
        {
            this.freeze = false;
            this.agent.enabled = enableAgent;
            this.physicsProp.enabled = false;
            this.grabbableItems.Clear();
            ItemDropship dropShip = FindObjectOfType<ItemDropship>();
            foreach (GrabbableObject item in FindObjectsOfType<GrabbableObject>())
            {
                if (item.GetComponent<PetAI>() == null) 
                {
                    if (dropShip != null && !item.isInShipRoom)
                    {
                        Log("Drop ship: " + dropShip.name);
                        if (Vector3.Distance(dropShip.transform.position, item.transform.position) <= 5f)
                        {
                            Log("Skip Item: " + item.name);
                            continue;
                        }
                    }
                    this.grabbableItems.Add(item);
                }
            }
            Log("Start Round!");
            OnStartRound();
        }

        protected void EndRound()
        {
            Log("End Round!");
            this.freeze = true;
            this.agent.enabled = false;
            this.physicsProp.enabled = true;
            OnEndRound();
        }

        protected virtual void OnEnterShip() { }
        protected virtual void OnLeaveShip() { }
        protected virtual void OnStartRound() { }
        protected virtual void OnEndRound() { }

        protected bool IsInSight(Transform target)
        {
            //Ray ray = new Ray(this.transform.position, target.position - this.transform.position);
            bool isInSight = Physics.Linecast(this.itemHolder.position, target.position, StartOfRound.Instance.collidersAndRoomMask);

            return !isInSight;
        }

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
                //mls.LogInfo("[PetDuck] hit: " + hit.transform.gameObject.name);
                if (hit.collider.gameObject.GetComponent<EnemyAI>() != null && hit.collider.gameObject.GetComponent<PetDuckAI>() == null)
                {
                    return true;
                }
            }
            return false;
        }

        protected GrabbableObject GetClosestItem()
        {
            if (this.grabbingCooldown > 0)
            {
                return null;
            }
            GrabbableObject targetItem = null;

            float nearest = 10f;
            foreach (GrabbableObject item in this.grabbableItems)
            {
                //Log("tmp item: " + item.name);

                Vector3 pos = RoundManager.Instance.GetNavMeshPosition(item.transform.position);

                if (item != null && item.GetComponent<PetDuckAI>() == null && RoundManager.Instance.GotNavMeshPositionResult 
                    && !item.isInShipRoom)
                {
                    float dist = Vector3.Distance(base.transform.position, item.transform.position);
                    if (dist <= nearest && item.grabbable && !item.isHeld && IsInSight(item.transform))
                    {
                        nearest = dist;
                        targetItem = item;
                    }
                }
            }

            return targetItem;
        }

        public void OpenDoorInfront()
        {
            Ray ray = new Ray(this.transform.position, this.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, 1f);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.gameObject.GetComponent<Door>() != null)
                {
                    Door door = hit.transform.gameObject.GetComponent<Door>();
                    door.SetDoorState(true);
                    return;
                }
            }
        }

        protected void Teleport()
        {
            Vector3 nextEntrance;
            Vector3 targetEntrance;

            if (this.isInFactory)
            {
                nextEntrance = RoundManager.FindMainEntrancePosition(false, false);
                targetEntrance  = RoundManager.FindMainEntrancePosition(false, true);
            }
            else
            {
                nextEntrance = RoundManager.FindMainEntrancePosition(false, true);
                targetEntrance = RoundManager.FindMainEntrancePosition(false, false); 
            }
            if (Vector3.Distance(this.transform.position, nextEntrance) > 2f)
            {
                this.agent.SetDestination(nextEntrance);
                return;
            }
            Log("Duck is teleporting");
            nextEntrance = RoundManager.Instance.GetNavMeshPosition(nextEntrance);
            targetEntrance = RoundManager.Instance.GetNavMeshPosition(targetEntrance);
            this.agent.enabled = false;
            base.transform.position = RoundManager.Instance.GetNavMeshPosition(targetEntrance);
            this.agent.enabled = true;
            this.isInFactory = !this.isInFactory;
            this.targetItem = null;
        }

        protected void ChangeParent(NetworkObject networkObject)
        {
            this.transform.parent = networkObject.transform;
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

        public override void OnDestroy()
        {
            if (petAI == this)
            {
                petAI = null;               
            }
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
                //mls.LogInfo("[Pet Duck] Calling UpdatePositionServerRpc Handle!");
                ServerRpcParams serverRpcParams = default(ServerRpcParams);
                FastBufferWriter bufferWriter = __beginSendServerRpc(4287979890u, serverRpcParams, RpcDelivery.Reliable);
                bufferWriter.WriteValueSafe(in newPos);
                __endSendServerRpc(ref bufferWriter, 4287979890u, serverRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
            {
                //mls.LogInfo("[Pet Duck] Calling UpdatePositionClientRpc!");
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
                    //mls.LogInfo("[Pet Duck] Calling UpdatePositionClientRpc Handle!");
                    ClientRpcParams clientRpcParams = default(ClientRpcParams);
                    FastBufferWriter bufferWriter = __beginSendClientRpc(4287979891u, clientRpcParams, RpcDelivery.Reliable);
                    bufferWriter.WriteValueSafe(in newPos);
                    __endSendClientRpc(ref bufferWriter, 4287979891u, clientRpcParams, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost) && !base.IsOwner)
                {
                    //mls.LogInfo("[Pet Duck] UpdatePositionClientRpc was called!");
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
                FastBufferWriter bufferWriter = __beginSendServerRpc(3079913700u, serverRpcParams, RpcDelivery.Reliable);
                BytePacker.WriteValueBitPacked(bufferWriter, rotationY);
                __endSendServerRpc(ref bufferWriter, 3079913700u, serverRpcParams, RpcDelivery.Reliable);
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
                    FastBufferWriter bufferWriter = __beginSendClientRpc(3079913701u, clientRpcParams, RpcDelivery.Reliable);
                    BytePacker.WriteValueBitPacked(bufferWriter, rotationY);
                    __endSendClientRpc(ref bufferWriter, 3079913701u, clientRpcParams, RpcDelivery.Reliable);
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

        // =====================================================================================================================
        // Grab and Drop

        protected void GrabItem(NetworkObject networkObject)
        {
            Log("Grabbing " + networkObject.name);
            GrabbableObject item = networkObject.GetComponent<GrabbableObject>();
            this.grabbedItems.Add(item);
            this.targetItem = null;
            item.parentObject = this.itemHolder;
            item.EnablePhysics(false);
            item.isHeld = true;
            item.hasHitGround = false;

            this.interactTrigger.interactable = true;
        }

        protected bool GrabAndSync()
        {
            if (!base.IsOwner)
            {
                return false;
            }
            if (targetItem != null && this.grabbedItems.Count < this.itemCapacity && Vector3.Distance(base.transform.position, targetItem.transform.position) < 0.75f)
            {
                NetworkObject networkObj = targetItem.GetComponent<NetworkObject>();
                // SwitchToBehaviourStateOnLocalClient(1);
                //GrabItem(networkObj);
                GrabItemServerRpc(networkObj);
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
                FastBufferWriter bufferWriter = __beginSendServerRpc(2358561450u, serverRpcParams, RpcDelivery.Reliable);
                bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
                __endSendServerRpc(ref bufferWriter, 2358561450u, serverRpcParams, RpcDelivery.Reliable);
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

        protected void DropItem(NetworkObject networkObject)
        {
            Log("Dropping " + networkObject.name);
            GrabbableObject item = networkObject.GetComponent<GrabbableObject>();

            item.parentObject = null;

            if (this.isInsideShip)
            {
                item.transform.SetParent(StartOfRound.Instance.elevatorTransform, worldPositionStays: true);
                item.scrapPersistedThroughRounds = true;
                item.isInShipRoom = true;
                item.isInFactory = false;
            }
            else
            {
                item.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
                item.scrapPersistedThroughRounds = false;
                item.isInShipRoom = false;
                if (this.isInFactory)
                {
                    item.isInFactory = true;
                }
                else
                {
                    item.isInShipRoom = false;
                }
            }
            
            item.EnablePhysics(enable: true);
            item.fallTime = 0f;
            item.startFallingPosition = item.transform.parent.InverseTransformPoint(item.transform.position);
            Vector3 floorPosition = RoundManager.Instance.GetNavMeshPosition(item.transform.position + this.transform.forward);
            item.targetFloorPosition = item.transform.parent.InverseTransformPoint(floorPosition);
            item.floorYRot = -1;
            item.DiscardItemFromEnemy();

            item.EnableItemMeshes(enable: true);
            item.isHeld = false;
            item.isPocketed = false;
            item.heldByPlayerOnServer = false;
            //SetItemInElevator(isInHangarShipRoom, isInElevator, placeObject);


            item.OnPlaceObject();

            this.grabbedItems.Remove(item);
            this.grabbingCooldown = 5f;
            if (this.grabbedItems.Count == 0)
            {
                this.interactTrigger.interactable = false;
            }
        }

        protected void DropAndSync(GrabbableObject item)
        {
            NetworkObject networkObject = item.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                return;
            }

            //DropItem(networkObject);
            DropItemServerRpc(networkObject);
        }


        [ServerRpc]
        public void DropItemServerRpc(NetworkObjectReference objectRef)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
            {
                ServerRpcParams serverRpcParams = default(ServerRpcParams);
                FastBufferWriter bufferWriter = __beginSendServerRpc(847487222u, serverRpcParams, RpcDelivery.Reliable);
                bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
                __endSendServerRpc(ref bufferWriter, 847487222u, serverRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
            {
                DropItemClientRpc(objectRef);
            }
        }

        [ClientRpc]
        public void DropItemClientRpc(NetworkObjectReference objectRef)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(847487223u, clientRpcParams, RpcDelivery.Reliable);
                bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
                __endSendClientRpc(ref bufferWriter, 847487223u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
            {
                if (objectRef.TryGet(out var networkObject))
                {
                    DropItem(networkObject);
                }
                else
                {
                    Debug.LogError(base.gameObject.name + ": Failed to get network object from network object reference (Drop item RPC)");
                }
            }
        }

        // =====================================================================================================================
        // Change State

        public void UpdateAnim()
        {
            if (curSpeed == 0)
            {
                this.animator.SetBool("IsWalking", false);
                this.animator.speed = 1;
                if (this.audioWalking != null)
                {
                    if (this.audioWalking.isPlaying)
                    {
                        this.audioWalking.Stop();
                    }
                }
                return;
            }
            // pet is moving

            float speedFactor = curSpeed / 4.5f;
            this.audioWalking.pitch = Mathf.Clamp(speedFactor, 1, 2);

            this.animator.SetBool("IsWalking", true);
            this.animator.speed = Mathf.Clamp(speedFactor, 1, speedFactor);

            if (this.audioWalking != null)
            {
                if (this.audioWalking.isPlaying)
                {
                    return;
                }
                this.audioWalking.Play();
            }
        }

        [ServerRpc]
        public void SyncSpeedServerRpc(float speed)
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
                FastBufferWriter bufferWriter = __beginSendServerRpc(2358561458u, serverRpcParams, RpcDelivery.Reliable);
                BytePacker.WriteValuePacked(bufferWriter, speed);
                __endSendServerRpc(ref bufferWriter, 2358561458u, serverRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
            {
                SyncSpeedClientRpc(speed);
            }
        }

        [ClientRpc]
        public void SyncSpeedClientRpc(float speed)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(847487229u, clientRpcParams, RpcDelivery.Reliable);
                BytePacker.WriteValuePacked(bufferWriter, speed);
                __endSendClientRpc(ref bufferWriter, 847487229u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
            {
                this.curSpeed = speed;
                UpdateAnim();
            }
        }

        // =====================================================================================================================

        public void Pat()
        {
            this.animator.SetTrigger("Pat");
            this.audioQuacking.Play();
        }

        [ServerRpc]
        public void PatServerRpc()
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
            {
                ServerRpcParams serverRpcParams = default(ServerRpcParams);
                FastBufferWriter bufferWriter = __beginSendServerRpc(2358561456u, serverRpcParams, RpcDelivery.Reliable);
                __endSendServerRpc(ref bufferWriter, 2358561456u, serverRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
            {
                PatClientRpc();
            }
        }

        [ClientRpc]
        public void PatClientRpc()
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(847487227u, clientRpcParams, RpcDelivery.Reliable);
                //BytePacker.WriteValuePacked(bufferWriter, speed);
                __endSendClientRpc(ref bufferWriter, 847487227u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
            {
                Pat();
            }
        }

        // =====================================================================================================================
        public virtual void OnHit(int force) 
        {
            if (hittable)
            {
                AddHP(-force);
            }
        }

        public bool Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            Log("Duck got hit.");
            HitServerRpc(force);

            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void HitServerRpc(int force)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
                {
                    ServerRpcParams serverRpcParams = default(ServerRpcParams);
                    FastBufferWriter bufferWriter = __beginSendServerRpc(847487300u, serverRpcParams, RpcDelivery.Reliable);
                    BytePacker.WriteValueBitPacked(bufferWriter, force);
                    __endSendServerRpc(ref bufferWriter, 847487300u, serverRpcParams, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
                {
                    HitClientRpc(force);
                }
            }
        }

        [ClientRpc]
        public void HitClientRpc(int force)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(847487301u, clientRpcParams, RpcDelivery.Reliable);
                BytePacker.WriteValueBitPacked(bufferWriter, force);
                __endSendClientRpc(ref bufferWriter, 847487301u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
            {
                OnHit(force);
            }
        }

        // =====================================================================================================================

        public virtual void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot, int noiseID)
        {

        }

        public virtual void Interact(PlayerControllerB player)
        {
            this.DropAndSync(this.grabbedItems[this.grabbedItems.Count - 1]);
        }

        public virtual void InteractPat(PlayerControllerB player)
        {
            this.PatServerRpc();
        }

        // =====================================================================================================================

        // RPC Handler
        [RuntimeInitializeOnLoadMethod]
        internal static void InitializeRPCS_PetAI()
        {
            NetworkManager.__rpc_func_table.Add(4287979890u, __rpc_handler_4287979890);
            NetworkManager.__rpc_func_table.Add(4287979891u, __rpc_handler_4287979891);
            NetworkManager.__rpc_func_table.Add(3079913700u, __rpc_handler_3079913700);
            NetworkManager.__rpc_func_table.Add(3079913701u, __rpc_handler_3079913701);
            NetworkManager.__rpc_func_table.Add(2358561450u, __rpc_handler_2358561450);
            NetworkManager.__rpc_func_table.Add(847487220u, __rpc_handler_847487220);
            NetworkManager.__rpc_func_table.Add(847487222u, __rpc_handler_847487222);
            NetworkManager.__rpc_func_table.Add(847487223u, __rpc_handler_847487223);
            NetworkManager.__rpc_func_table.Add(2358561458u, __rpc_handler_2358561458);
            NetworkManager.__rpc_func_table.Add(847487229u, __rpc_handler_847487229);
            NetworkManager.__rpc_func_table.Add(2358561456u, __rpc_handler_2358561456);
            NetworkManager.__rpc_func_table.Add(847487227u, __rpc_handler_847487227);
            NetworkManager.__rpc_func_table.Add(847487300u, __rpc_handler_847487300);
            NetworkManager.__rpc_func_table.Add(847487301u, __rpc_handler_847487301);
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
                //mls.LogInfo("[Pet Duck] UpdatePositionServerRpc Handle was called!");
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
                //mls.LogInfo("[Pet Duck] UpdatePositionClientRpc Handle was called!");
                reader.ReadValueSafe(out Vector3 value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((PetAI)target).UpdatePositionClientRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // UpdateRotationServerRpc
        private static void __rpc_handler_3079913700(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
                ByteUnpacker.ReadValueBitPacked(reader, out short value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Server;
                ((PetAI)target).UpdateRotationServerRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // UpdateRotationClientRpc
        private static void __rpc_handler_3079913701(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out short value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((PetAI)target).UpdateRotationClientRpc(value);
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

        // DropItemServerRpc
        private static void __rpc_handler_847487222(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            else
            {
                reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Server;
                ((PetAI)target).DropItemServerRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // DropItemClientRpc
        private static void __rpc_handler_847487223(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((PetAI)target).DropItemClientRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // ChangeStateServerRpc
        private static void __rpc_handler_2358561458(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
                ByteUnpacker.ReadValuePacked(reader, out float value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Server;
                ((PetAI)target).SyncSpeedServerRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // ChangeStateClientRpc
        private static void __rpc_handler_847487229(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                ByteUnpacker.ReadValuePacked(reader, out float value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((PetAI)target).SyncSpeedClientRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // PatServerRpc
        private static void __rpc_handler_2358561456(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            else
            {
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Server;
                ((PetAI)target).PatServerRpc();
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // PatClientRpc
        private static void __rpc_handler_847487227(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((PetAI)target).PatClientRpc();
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // HitServerRpc
        private static void __rpc_handler_847487300(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            else
            {
                ByteUnpacker.ReadValueBitPacked(reader, out int value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Server;
                ((PetAI)target).HitServerRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // HitClientRpc
        private static void __rpc_handler_847487301(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out int value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((PetAI)target).HitClientRpc(value);
                ((PetAI)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        public virtual void Log(string message)
        {
            if (mls != null)
            {
                mls.LogInfo("[PetAI] " + message);
            }
        }
    }
}
