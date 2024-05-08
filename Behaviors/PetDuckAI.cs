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
        protected enum PetState
        {
            LookingAround,
            Following,
            MovingToItem,
            MovingToEntrance,
            Searching
        }

        protected PetState petState;

        override public void Start()
        {
            base.Start();

            // Initialize enemy variables
            this.agent.enabled = true;
            this.agent.speed = 4.5f;

            shipState = ShipState.InSpace;

            if (mls != null)
            {
                mls.LogInfo("[Pet Duck] Is pet duck owner: " + base.IsOwner);
                mls.LogInfo("[Pet Duck] Speed: " + this.agent.speed);
                mls.LogInfo("[Pet Duck] Velocity: " + this.agent.velocity);
                mls.LogInfo("[Pet Duck] Acceleration: " + this.agent.acceleration);
            }
        }

        override public void Update()
        {
            base.Update();          
            if (mls != null)
            {
                //mls.LogInfo("[Pet Duck] Is pet duck owner: " + base.IsOwner);
                //mls.LogInfo("[Pet Duck] Speed: " + this.agent.speed);
                //mls.LogInfo("[Pet Duck] Velocity: " + this.agent.velocity);
                //mls.LogInfo("[Pet Duck] Acceleration: " + this.agent.acceleration);
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

            if (this.grabbedItems.Count < this.maxGrabbedItems & this.targetItem == null)
            {
                this.targetItem = this.GetClosestItem();
                if (mls != null && this.targetItem != null)
                {
                    //mls.LogInfo("[Pet Duck] Target item: " + this.targetItem.name);
                }
            }

            if (this.targetPlayer == null)
            {
                return;
            }

            float playerDistance = Vector3.Distance(base.transform.position, this.targetPlayer.transform.position);

            // player on another level
            if ((this.targetPlayer.isInsideFactory && !this.isInFactory) || (!this.targetPlayer.isInsideFactory && this.isInFactory))
            {
                this.petState = PetState.MovingToEntrance;
            }
            else if (this.targetItem != null)
            {
                this.petState = PetState.MovingToItem;
            }
            else if (playerDistance <= this.minPlayerDist)
            {
                this.petState = PetState.LookingAround;
            }
            else if (playerDistance <= this.maxPlayerDist)
            {
                this.petState = PetState.Following;
            }
            else
            {
                this.petState = PetState.Searching;
            }

            switch (this.petState)
            {
                case PetState.LookingAround:
                    break;

                case PetState.Following:
                    if (this.agent.isOnNavMesh)
                    {
                        if (Vector3.Distance(base.transform.position, this.targetPlayer.transform.position) > 3f)
                        {
                            this.destination = this.targetPlayer.transform.position;
                        }
                        else
                        {
                            this.destination = base.transform.position;
                        }

                        agent.SetDestination(destination);
                    }
                    break;

                case PetState.MovingToItem:
                    if (!GrabAndSync())
                    {
                        agent.SetDestination(this.targetItem.transform.position);
                    }
                    break;

                case PetState.MovingToEntrance:
                    this.Teleport();
                    break;

                case PetState.Searching:
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

        override public bool Hit(int force, UnityEngine.Vector3 hitDirection, GameNetcodeStuff.PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            //int playerWhoHitID = -1;
            //if (playerWhoHit != null)
            //{
            //    playerWhoHitID = (int)playerWhoHit.playerClientId;
            //    AddHpServerRpc(force);
            //}

            //if (mls != null)
            //{
            //    mls.LogInfo("[Pet DUCK] Duck was hit :(\nHP:\t" + this.hp);
            //}
            return true;
        }

        override public void DetectNoise(UnityEngine.Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot, int noiseID)
        {
            mls.LogInfo("[Pet Duck] Noise position: " + noisePosition);

            this.audioSource.Play();

            if (base.IsOwner)
            {
                if (this.agent.enabled && this.agent.isOnNavMesh)
                {
                    this.agent.SetDestination(noisePosition);
                    
                }
            }
        }
    }
}