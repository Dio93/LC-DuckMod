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
            SearchingItem,
            Searching
        }

        protected PetState petState;

        override public void Start()
        {
            base.Start();

            // Initialize enemy variables
            this.agent.enabled = true;
            this.agent.speed = 4f;

            shipState = ShipState.InSpace;

            //this.enemyType = new EnemyType();
            //this.enemyType.name = "Pet Duck";
            //this.enemyType.isDaytimeEnemy = false;
            //this.enemyType.isOutsideEnemy = false;
            //this.enemyType.normalizedTimeInDayToLeave = Mathf.Infinity;
            //this.enemyType.PowerLevel = 0;
            //this.enemyType.canDie = true;
            //this.enemyType.pushPlayerForce = 0;
            //this.enemyType.canSeeThroughFog = false;
            //this.enemyType.destroyOnDeath = true;
            //this.enemyType.spawningDisabled = true;
            
            //this.enemyHP = this.maxHp;
            //this.AIIntervalTime = 1f;
            //this.ventAnimationFinished = true;
            //this.isOutside = true;
            //this.isEnemyDead = false;
            //this.inSpecialAnimation = false;
            //this.serverPosition = base.transform.position;
            //this.movingTowardsTargetPlayer = false;
            //this.moveTowardsDestination = true;

            if (mls != null)
            {
                mls.LogInfo("[Pet Duck] Is pet duck owner: " + base.IsOwner);
                mls.LogInfo("[Pet Duck] Speed: " + this.agent.speed);
                mls.LogInfo("[Pet Duck] Velocity: " + this.agent.velocity);
                mls.LogInfo("[Pet Duck] Acceleration: " + this.agent.acceleration);
            }

            //if (base.IsOwner)
            //{
            //    base.SyncPositionToClients();
            //}
            //else
            //{

            //}
        }

        override public void Update()
        {
            if (this.agent.enabled)
            {
                base.Update();
            }            
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
                foreach(GrabbableObject item in this.grabbedItems)
                {
                    this.DropItem(item);
                }
            }

            this.targetPlayer = this.GetClosestPlayer();

            this.UpdateCollisions();

            if (this.grabbedItems.Count < this.maxGrabbedItems & this.targetItem == null)
            {
                this.targetItem = this.GetClosestItem();
                if (mls != null && this.targetItem != null)
                {
                    mls.LogInfo("[Pet Duck] Target item: " + this.targetItem.name);
                }
            }

            if (this.targetPlayer == null)
            {
                return;
            }

            float playerDistance = Vector3.Distance(base.transform.position, this.targetPlayer.transform.position);

            if (this.targetItem != null)
            {
                this.petState = PetState.SearchingItem;
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

                case PetState.SearchingItem:
                    if (true)
                    {
                        agent.SetDestination(this.targetItem.transform.position);
                    }
                    break;

                case PetState.Searching:

                    // player on another level
                    if (this.targetPlayer.isInsideFactory && !this.isInFactory)
                    {
                        this.agent.enabled = false;
                        Vector3 entrancePos = RoundManager.FindMainEntrancePosition(false, false);
                        base.transform.position = RoundManager.Instance.GetNavMeshPosition(entrancePos);
                        this.agent.enabled = true;
                        this.isInFactory = true;
                    }
                    else if (!this.targetPlayer.isInsideFactory && this.isInFactory)
                    {
                        this.agent.enabled = false;
                        Vector3 entrancePos = RoundManager.FindMainEntrancePosition(false, true);
                        base.transform.position = RoundManager.Instance.GetNavMeshPosition(entrancePos);
                        this.agent.enabled = true;
                        this.isInFactory = false;
                    }
                    break;
            }

            // check for enemies
            if (Time.time - this.lastCheckEnemy >= this.checkEnemyCooldown)
            {
                this.lastCheckEnemy = Time.time;

                if (this.CheckForEnemies() && !this.audioSource.isPlaying)
                {
                    this.audioSource.Play();
                    this.lastCheckEnemy = Time.time + 60;
                }
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
            int playerWhoHitID = -1;
            if (playerWhoHit != null)
            {
                playerWhoHitID = (int)playerWhoHit.playerClientId;
                HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            }
            HitServerRpc(force, playerWhoHitID, playHitSFX, hitID);

            if (mls != null)
            {
                mls.LogInfo("[Pet DUCK] Duck was hit :(\nHP:\t" + this.hp);
            }
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