using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;

namespace DuckMod.Behaviors
{
    internal class DeadDuckBehavior : NetworkBehaviour
    {
        struct Data : INetworkSerializeByMemcpy
        {
            public Vector3 startFallingPos;
            public Vector3 targetFloorPos;
            public int shaderID;
            public Data(Vector3 startFallingPos, Vector3 targetFloorPos, int shaderID)
            {
                this.startFallingPos = startFallingPos;
                this.targetFloorPos = targetFloorPos;
                this.shaderID = shaderID;
            }
        }

        public static int shaderID;
        private int currShaderID;

        public static List<(float, Material)> materials = null;
        private SkinnedMeshRenderer[] meshRenderers;
        private PhysicsProp physicsProp;

        public void Start()
        {
            Debug.Log("Dead Duck Start");
            physicsProp = GetComponent<PhysicsProp>();
            physicsProp.scrapValue = 5;
            Debug.Log("Dead Duck PhysicsProp: " + physicsProp);
            Debug.Log("DEADDUCK: " + physicsProp.startFallingPosition + ", " + physicsProp.targetFloorPosition);

            this.currShaderID = shaderID;

            if (!IsOwner)
            {
                //InitServerRpc(physicsProp.startFallingPosition, physicsProp.targetFloorPosition, shaderID);
                InitServerRpc();
            }
        }

        public void Update()
        {
            //Debug.Log("DEADDUCK: " + physicsProp.startFallingPosition + ", " + physicsProp.targetFloorPosition);
        }

        //public void TriggerInit(int shaderID)
        //{
        //    if (IsOwner)
        //    {
        //        shaderID = shaderID;
        //        InitServerRpc(physicsProp.startFallingPosition, physicsProp.targetFloorPosition, shaderID);
        //    }
        //}

        public void Init(Vector3 startFallingPos, Vector3 targetFloorPos, int shaderID)
        {
            //physicsProp = GetComponent<PhysicsProp>();

            physicsProp.startFallingPosition = startFallingPos;
            physicsProp.targetFloorPosition = targetFloorPos;

            //transform.position = startFallingPos;

            Debug.Log("DEADDUCK INIT: " + physicsProp.startFallingPosition + ", " + physicsProp.targetFloorPosition);
            //Log("ShaderID: " + shaderID);

            if (materials == null)
            {
                materials = PetDuckAI.materials;
            }

            meshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

            Material material = materials[shaderID].Item2;

            this.meshRenderers[0].material = material;
            if (material.name == "DuckShader Gold")
            {
                //Log("Golden Duck!!!");
                HDAdditionalLightData lightData = this.gameObject.AddComponent<HDAdditionalLightData>();
                Light light = this.gameObject.GetComponent<Light>();
                light.intensity = 100f;
                light.color = new Color(1, 0.75f, 0.5f, 1);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void InitServerRpc()
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
                {
                    ServerRpcParams serverRpcParams = default(ServerRpcParams);
                    FastBufferWriter bufferWriter = __beginSendServerRpc(3079913600u, serverRpcParams, RpcDelivery.Reliable);
                    //BytePacker.WriteValueBitPacked(bufferWriter, shaderID);
                    __endSendServerRpc(ref bufferWriter, 3079913600u, serverRpcParams, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
                {
                    //Log("ShaderServerRPC: " + shaderID);
                    InitClientRpc(physicsProp.startFallingPosition, physicsProp.targetFloorPosition, currShaderID);
                }
            }
        }

        [ClientRpc]
        public void InitClientRpc(Vector3 startFallingPos, Vector3 targetFloorPos, int shaderID)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(3079913601u, clientRpcParams, RpcDelivery.Reliable);
                //BytePacker.WriteValueBitPacked(bufferWriter, shaderID);
                Data data = new Data(startFallingPos, targetFloorPos, shaderID);
                bufferWriter.WriteValueSafe(in data, default(FastBufferWriter.ForStructs));
                __endSendClientRpc(ref bufferWriter, 3079913601u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
            {
                //Log("ShaderClientRPC: " + shaderID);
                Init(startFallingPos, targetFloorPos, shaderID);
            }
        }
        // UpdateShaderServerRpc
        private static void __rpc_handler_3079913600(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            else
            {
                reader.ReadValueSafe(out Data data, default(FastBufferWriter.ForStructs));
                //ByteUnpacker.ReadValueBitPacked(reader, out short value);
                ((DeadDuckBehavior)target).__rpc_exec_stage = __RpcExecStage.Server;
                ((DeadDuckBehavior)target).InitServerRpc();
                ((DeadDuckBehavior)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        // UpdateShaderClientRpc
        private static void __rpc_handler_3079913601(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                reader.ReadValueSafe(out Data data, default(FastBufferWriter.ForStructs));
                ((DeadDuckBehavior)target).__rpc_exec_stage = __RpcExecStage.Client;
                ((DeadDuckBehavior)target).InitClientRpc(data.startFallingPos, data.targetFloorPos, data.shaderID);
                ((DeadDuckBehavior)target).__rpc_exec_stage = __RpcExecStage.None;
            }
        }

        [RuntimeInitializeOnLoadMethod]
        internal static void InitializeRPCS_DeadDuckBehavior()
        {
            NetworkManager.__rpc_func_table.Add(3079913600u, __rpc_handler_3079913600);
            NetworkManager.__rpc_func_table.Add(3079913601u, __rpc_handler_3079913601);
        }
    }
}
