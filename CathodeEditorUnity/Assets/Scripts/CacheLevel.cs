/*
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System;
using CATHODE.LEGACY;
using static CATHODE.LEGACY.CathodeModels;
using CathodeLib;

public class CacheLevel : MonoBehaviour
{
    private alien_level levelData;
    private GameObject levelGO;

    void Start()
    {
#if UNITY_EDITOR
        levelData = AlienLevel.Load(SharedVals.instance.LevelName, SharedVals.instance.PathToEnv);
        levelGO = new GameObject(SharedVals.instance.LevelName);

        LoadTextureAssets();
        LoadMeshAssets();
        LoadMaterialAssets();
        LoadFlowgraphAssets();

        for (int i = 0; i < levelData.CommandsPAK.EntryPoints.Length; i++)
        {
            GameObject flowgraphGO = PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>(GetFlowgraphAssetPath(levelData.CommandsPAK.EntryPoints[i], true))) as GameObject;
            flowgraphGO.name = levelData.CommandsPAK.EntryPoints[i].name;
            flowgraphGO.transform.parent = levelGO.transform;
        }

        PrefabUtility.SaveAsPrefabAsset(levelGO, "Assets/Resources/" + SharedVals.instance.LevelName + "/" + SharedVals.instance.LevelName + ".prefab");
#endif
    }

    private void LoadTextureAssets()
    {
#if UNITY_EDITOR
        bool[] textureTracker = new bool[levelData.LevelTextures.Header.EntryCount];
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < levelData.LevelTextures.entryHeaders.Length; i++)
        {
            GenericPAKEntry Entry = levelData.LevelTextures.entryHeaders[i];
            TextureEntry InTexture = levelData.LevelTextures.Textures[Entry.BINIndex];

            Vector2 textureDims;
            int textureLength = 0;
            int mipLevels = 0;

            if (!textureTracker[Entry.BINIndex])
            {
                textureDims = new Vector2(InTexture.Size_V1[0], InTexture.Size_V1[1]);
                textureLength = InTexture.Length_V1;
                mipLevels = InTexture.MipLevelsV1;
            }
            else
            {
                textureDims = new Vector2(InTexture.Size_V2[0], InTexture.Size_V2[1]);
                textureLength = InTexture.Length_V2;
                mipLevels = InTexture.MipLevelsV2;
            }
            textureTracker[Entry.BINIndex] = true;

            if (textureLength == 0) continue;
            if (InTexture.Format == CATHODE.LEGACY.TextureFormat.SIGNED_DISTANCE_FIELD || InTexture.Format == CATHODE.LEGACY.TextureFormat.DDS_BC2) continue;

            UnityEngine.TextureFormat format = UnityEngine.TextureFormat.BC7;
            switch (InTexture.Format)
            {
                case CATHODE.LEGACY.TextureFormat.R32G32B32A32_SFLOAT:
                    format = UnityEngine.TextureFormat.RGBA32;
                    break;
                case CATHODE.LEGACY.TextureFormat.R8G8B8A8_UNORM:
                    format = UnityEngine.TextureFormat.ETC2_RGBA8; //?
                    break;
                case CATHODE.LEGACY.TextureFormat.R8G8B8A8_UNORM_0:
                    format = UnityEngine.TextureFormat.ETC2_RGBA8; //?
                    break;
                case CATHODE.LEGACY.TextureFormat.R8:
                    format = UnityEngine.TextureFormat.R8;
                    break;
                case CATHODE.LEGACY.TextureFormat.DDS_BC1:
                    format = UnityEngine.TextureFormat.DXT1;
                    break;
                case CATHODE.LEGACY.TextureFormat.DDS_BC5:
                    format = UnityEngine.TextureFormat.BC5; //Is this correct?
                    break;
                case CATHODE.LEGACY.TextureFormat.DDS_BC3:
                    format = UnityEngine.TextureFormat.DXT5;
                    break;
                case CATHODE.LEGACY.TextureFormat.DDS_BC7:
                    format = UnityEngine.TextureFormat.BC7;
                    break;
                case CATHODE.LEGACY.TextureFormat.R8G8:
                    format = UnityEngine.TextureFormat.BC5; // is this correct?
                    break;
            }

            BinaryReader tempReader = new BinaryReader(new MemoryStream(levelData.LevelTextures.dataStart));
            tempReader.BaseStream.Position = Entry.Offset;
            if (InTexture.Type == 7)
            {
                Cubemap cubemap = new Cubemap((int)textureDims.x, format, true);
                cubemap.name = levelData.LevelTextures.TextureFilePaths[Entry.BINIndex];
                cubemap.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveX);
                cubemap.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeX);
                cubemap.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveY);
                cubemap.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeY);
                cubemap.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.PositiveZ);
                cubemap.SetPixelData(tempReader.ReadBytes(textureLength / 6), 0, CubemapFace.NegativeZ);
                cubemap.Apply();

                string fullFilePath = "Assets/Resources/" + SharedVals.instance.LevelName + "/Cubemaps/" + cubemap.name.Substring(0, cubemap.name.Length - Path.GetExtension(cubemap.name).Length) + ".cubemap";
                string fileDirectory = GetDirectory(fullFilePath);
                if (!Directory.Exists(fileDirectory)) Directory.CreateDirectory(fileDirectory);
                AssetDatabase.CreateAsset(cubemap, fullFilePath);
            }
            else
            {
                Texture2D texture = new Texture2D((int)textureDims[0], (int)textureDims[1], format, mipLevels, true);
                texture.name = levelData.LevelTextures.TextureFilePaths[Entry.BINIndex];
                texture.LoadRawTextureData(tempReader.ReadBytes(textureLength));
                texture.Apply();

                string fullFilePath = "Assets/Resources/" + SharedVals.instance.LevelName + "/Textures/" + texture.name.Substring(0, texture.name.Length - Path.GetExtension(texture.name).Length) + ".asset";
                string fileDirectory = GetDirectory(fullFilePath);
                if (!Directory.Exists(fileDirectory)) Directory.CreateDirectory(fileDirectory);
                AssetDatabase.CreateAsset(texture, fullFilePath);
            }
            tempReader.Close();
        }
        AssetDatabase.StopAssetEditing();
#endif
    }

    private void LoadMeshAssets()
    {
#if UNITY_EDITOR
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < levelData.ModelsPAK.Models.Count; i++)
        {
            ModelData ChunkArray = levelData.ModelsPAK.Models[i];
            for (int ChunkIndex = 0; ChunkIndex < ChunkArray.Header.SubmeshCount; ++ChunkIndex)
            {
                int BINIndex = ChunkArray.Submeshes[ChunkIndex].binIndex;
                alien_model_bin_model_info Model = levelData.ModelsPAK.modelBIN.Models[BINIndex];
                //if (Model.BlockSize == 0) continue;

                alien_vertex_buffer_format VertexInput = levelData.ModelsPAK.modelBIN.VertexBufferFormats[Model.VertexFormatIndex];
                alien_vertex_buffer_format VertexInputLowDetail = levelData.ModelsPAK.modelBIN.VertexBufferFormats[Model.VertexFormatIndexLowDetail];

                BinaryReader Stream = new BinaryReader(new MemoryStream(ChunkArray.Submeshes[ChunkIndex].content));

                List<List<alien_vertex_buffer_format_element>> Elements = new List<List<alien_vertex_buffer_format_element>>();
                alien_vertex_buffer_format_element ElementHeader = new alien_vertex_buffer_format_element();
                foreach (alien_vertex_buffer_format_element Element in VertexInput.Elements)
                {
                    if (Element.ArrayIndex == 0xFF)
                    {
                        ElementHeader = Element;
                        continue;
                    }

                    while (Elements.Count - 1 < Element.ArrayIndex) Elements.Add(new List<alien_vertex_buffer_format_element>());
                    Elements[Element.ArrayIndex].Add(Element);
                }
                Elements.Add(new List<alien_vertex_buffer_format_element>() { ElementHeader });

                List<UInt16> InIndices = new List<UInt16>();
                List<Vector3> InVertices = new List<Vector3>();
                List<Vector3> InNormals = new List<Vector3>();
                List<Vector4> InTangents = new List<Vector4>();
                List<Vector2> InUVs0 = new List<Vector2>();
                List<Vector2> InUVs1 = new List<Vector2>();
                List<Vector2> InUVs2 = new List<Vector2>();
                List<Vector2> InUVs3 = new List<Vector2>();
                List<Vector2> InUVs7 = new List<Vector2>();

                //TODO: implement skeleton lookup for the indexes
                List<Vector4> InBoneIndexes = new List<Vector4>(); //The indexes of 4 bones that affect each vertex
                List<Vector4> InBoneWeights = new List<Vector4>(); //The weights for each bone

                for (int VertexArrayIndex = 0; VertexArrayIndex < Elements.Count; ++VertexArrayIndex)
                {
                    alien_vertex_buffer_format_element Inputs = Elements[VertexArrayIndex][0];
                    if (Inputs.ArrayIndex == 0xFF)
                    {
                        for (int x = 0; x < Model.IndexCount; x++)
                        {
                            InIndices.Add(Stream.ReadUInt16());
                        }
                    }
                    else
                    {
                        for (int VertexIndex = 0; VertexIndex < Model.VertexCount; ++VertexIndex)
                        {
                            for (int ElementIndex = 0; ElementIndex < Elements[VertexArrayIndex].Count; ++ElementIndex)
                            {
                                alien_vertex_buffer_format_element Input = Elements[VertexArrayIndex][ElementIndex];
                                switch (Input.VariableType)
                                {
                                    case alien_vertex_input_type.AlienVertexInputType_v3:
                                        {
                                            Vector3 Value = new Vector3(Stream.ReadSingle(), Stream.ReadSingle(), Stream.ReadSingle());
                                            switch (Input.ShaderSlot)
                                            {
                                                case alien_vertex_input_slot.AlienVertexInputSlot_N:
                                                    InNormals.Add(Value);
                                                    break;
                                                case alien_vertex_input_slot.AlienVertexInputSlot_T:
                                                    InTangents.Add(new Vector4(Value.x, Value.y, Value.z, 0));
                                                    break;
                                                case alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                    //TODO: 3D UVW
                                                    break;
                                            };
                                            break;
                                        }

                                    case alien_vertex_input_type.AlienVertexInputType_u32_C:
                                        {
                                            int Value = Stream.ReadInt32();
                                            switch (Input.ShaderSlot)
                                            {
                                                case alien_vertex_input_slot.AlienVertexInputSlot_C:
                                                    //??
                                                    break;
                                            }
                                            break;
                                        }

                                    case alien_vertex_input_type.AlienVertexInputType_v4u8_i:
                                        {
                                            Vector4 Value = new Vector4(Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte());
                                            switch (Input.ShaderSlot)
                                            {
                                                case alien_vertex_input_slot.AlienVertexInputSlot_BI:
                                                    InBoneIndexes.Add(Value);
                                                    break;
                                            }
                                            break;
                                        }

                                    case alien_vertex_input_type.AlienVertexInputType_v4u8_f:
                                        {
                                            Vector4 Value = new Vector4(Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte());
                                            Value /= 255.0f;
                                            switch (Input.ShaderSlot)
                                            {
                                                case alien_vertex_input_slot.AlienVertexInputSlot_BW:
                                                    float Sum = Value.x + Value.y + Value.z + Value.w;
                                                    InBoneWeights.Add(Value / Sum);
                                                    break;
                                                case alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                    InUVs2.Add(new Vector2(Value.x, Value.y));
                                                    InUVs3.Add(new Vector2(Value.z, Value.w));
                                                    break;
                                            }
                                            break;
                                        }

                                    case alien_vertex_input_type.AlienVertexInputType_v2s16_UV:
                                        {
                                            Vector2 Value = new Vector2(Stream.ReadInt16(), Stream.ReadInt16());
                                            Value /= 2048.0f;
                                            switch (Input.ShaderSlot)
                                            {
                                                case alien_vertex_input_slot.AlienVertexInputSlot_UV:
                                                    if (Input.VariantIndex == 0) InUVs0.Add(Value);
                                                    else if (Input.VariantIndex == 1)
                                                    {
                                                        // TODO: We can figure this out based on alien_vertex_buffer_format_element.
                                                        //Material->Material.Flags |= Material_HasTexCoord1;
                                                        InUVs1.Add(Value);
                                                    }
                                                    else if (Input.VariantIndex == 2) InUVs2.Add(Value);
                                                    else if (Input.VariantIndex == 3) InUVs3.Add(Value);
                                                    else if (Input.VariantIndex == 7) InUVs7.Add(Value);
                                                    break;
                                            }
                                            break;
                                        }

                                    case alien_vertex_input_type.AlienVertexInputType_v4s16_f:
                                        {
                                            Vector4 Value = new Vector4(Stream.ReadInt16(), Stream.ReadInt16(), Stream.ReadInt16(), Stream.ReadInt16());
                                            Value /= (float)Int16.MaxValue;
                                            switch (Input.ShaderSlot)
                                            {
                                                case alien_vertex_input_slot.AlienVertexInputSlot_P:
                                                    InVertices.Add(Value);
                                                    break;
                                            }
                                            break;
                                        }

                                    case alien_vertex_input_type.AlienVertexInputType_v4u8_NTB:
                                        {
                                            Vector4 Value = new Vector4(Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte(), Stream.ReadByte());
                                            Value /= (float)byte.MaxValue - 0.5f;
                                            Value.Normalize();
                                            switch (Input.ShaderSlot)
                                            {
                                                case alien_vertex_input_slot.AlienVertexInputSlot_N:
                                                    InNormals.Add(Value);
                                                    break;
                                                case alien_vertex_input_slot.AlienVertexInputSlot_T:
                                                    break;
                                                case alien_vertex_input_slot.AlienVertexInputSlot_B:
                                                    break;
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                    }
                    CathodeLib.Utilities.Align(Stream, 16);
                }

                if (InVertices.Count == 0) continue;

                Mesh thisMesh = new Mesh();
                thisMesh.name = levelData.ModelsPAK.modelBIN.ModelFilePaths[BINIndex] + ": " + levelData.ModelsPAK.modelBIN.ModelLODPartNames[BINIndex];
                thisMesh.SetVertices(InVertices);
                thisMesh.SetNormals(InNormals);
                thisMesh.SetIndices(InIndices, MeshTopology.Triangles, 0); //0??
                thisMesh.SetTangents(InTangents);
                thisMesh.SetUVs(0, InUVs0);
                thisMesh.SetUVs(1, InUVs1);
                thisMesh.SetUVs(2, InUVs2);
                thisMesh.SetUVs(3, InUVs3);
                thisMesh.SetUVs(7, InUVs7);
                //thisMesh.SetBoneWeights(InBoneWeights.ToArray());
                thisMesh.RecalculateBounds();
                thisMesh.RecalculateNormals();
                thisMesh.RecalculateTangents();

                //TODO: do this as proper submeshes
                string fullFilePath = "Assets/Resources/" + SharedVals.instance.LevelName + "/Meshes/" + levelData.ModelsPAK.modelBIN.ModelFilePaths[BINIndex] + "_" + levelData.ModelsPAK.modelBIN.ModelLODPartNames[BINIndex] + "_" + ChunkIndex + ".asset";
                string fileDirectory = GetDirectory(fullFilePath);
                if (!Directory.Exists(fileDirectory)) Directory.CreateDirectory(fileDirectory);
                AssetDatabase.CreateAsset(thisMesh, fullFilePath);
            }
        }
        AssetDatabase.StopAssetEditing();
#endif
    }

    private void LoadMaterialAssets()
    {

    }

    private void LoadFlowgraphAssets()
    {
        /*
        //First, make dummy prefabs of all flowgraphs
        GameObject rootGO = new GameObject();
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < levelData.CommandsPAK.AllFlowgraphs.Count; i++)
        {
            string fullFilePath = GetFlowgraphAssetPath(levelData.CommandsPAK.AllFlowgraphs[i]);
            string fileDirectory = GetDirectory(fullFilePath);
            if (!Directory.Exists(fileDirectory)) Directory.CreateDirectory(fileDirectory);
            if (!File.Exists(fullFilePath)) PrefabUtility.SaveAsPrefabAsset(rootGO, fullFilePath);
        }
        AssetDatabase.StopAssetEditing();

        //Then, populate the prefabs for all flowgraphs
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < levelData.CommandsPAK.AllFlowgraphs.Count; i++)
        {
            GameObject flowgraphGO = new GameObject(levelData.CommandsPAK.AllFlowgraphs[i].name);
            string nodeType = "";
            for (int x = 0; x < levelData.CommandsPAK.AllFlowgraphs[i].nodes.Count; x++)
            {
                CathodeFlowgraph flowgraphRef = levelData.CommandsPAK.GetFlowgraph(levelData.CommandsPAK.AllFlowgraphs[i].nodes[x].nodeType);
                GameObject nodeGO = null;
                if (flowgraphRef != null)
                {
                    //This is a reference to another flowgraph
                    GameObject flowgraphAsset = Resources.Load<GameObject>(GetFlowgraphAssetPath(flowgraphRef, true));
                    nodeGO = PrefabUtility.InstantiatePrefab(flowgraphAsset) as GameObject;
                    nodeGO.name = flowgraphRef.name;
                }
                else
                {
                    //This is a node
                    nodeGO = new GameObject(CathodeLib.NodeDB.GetFriendlyName(levelData.CommandsPAK.AllFlowgraphs[i].nodes[x].nodeID));
                    nodeType = CathodeLib.NodeDB.GetNodeTypeName(levelData.CommandsPAK.AllFlowgraphs[i].nodes[x].nodeType, ref levelData.CommandsPAK);
                }
                nodeGO.transform.parent = flowgraphGO.transform;
                //TODO: this can all be optimised massively
                List<uint> resourceIDs = new List<uint>();
                foreach (CathodeParameterReference paramRef in levelData.CommandsPAK.AllFlowgraphs[i].nodes[x].nodeParameterReferences)
                {
                    CathodeParameter param = levelData.CommandsPAK.GetParameter(paramRef.offset);
                    if (param == null) continue;
                    switch (param.dataType)
                    {
                        case CathodeDataType.POSITION:
                            CathodeTransform transform = (CathodeTransform)param;
                            nodeGO.transform.localPosition = transform.position;
                            nodeGO.transform.localRotation = Quaternion.Euler(transform.rotation);
                            break;
                        case CathodeDataType.SHORT_GUID:
                            resourceIDs.Add(((CathodeResource)param).resourceID);
                            break;
                    }
                }
                switch (nodeType)
                {
                    case "PlayerTriggerBox":
                        //nodeGO.AddComponent<BoxCollider>().bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(0, 0, 0));
                        break;
                    case "PositionMarker":
                        //debug render marker
                        break;
                    case "Sound":
                        //nodeGO.AddComponent<AudioSource>().clip = null;
                        break;
                    case "PlayEnvironmentAnimation":
                        break;
                    case "ParticleEmitterReference":
                        break;
                    case "ModelReference":
                        for (int y = 0; y < resourceIDs.Count; y++)
                        {
                            List<CathodeResourceReference> resourceReference = levelData.CommandsPAK.AllFlowgraphs[i].GetResourceReferencesByID(resourceIDs[y]);
                            for (int z = 0; z < resourceReference.Count; z++)
                            {
                                if (resourceReference[z].entryType != CathodeResourceReferenceType.RENDERABLE_INSTANCE) continue; //Ignoring collision maps, etc, for now
                                //TODO: This is kinda hacked for now while we're not saving with submeshes
                                for (int p = 0; p < resourceReference[z].entryCountREDS; p++)
                                {
                                    int thisIndex = levelData.RenderableREDS.Entries[resourceReference[z].entryIndexREDS].ModelIndex + p;
                                    string meshResourcePath = GetMeshAssetPath(thisIndex, true) + "_" + p;
                                    GameObject newSubmesh = new GameObject(meshResourcePath);
                                    newSubmesh.transform.parent = nodeGO.transform;
                                    newSubmesh.transform.localScale = new Vector3(1, 1, 1) * levelData.ModelsPAK.modelBIN.Models[thisIndex].ScaleFactor;
                                    newSubmesh.transform.localPosition = Vector3.zero;
                                    newSubmesh.transform.localRotation = Quaternion.identity;
                                    newSubmesh.AddComponent<MeshFilter>().sharedMesh = Resources.Load<Mesh>(GetMeshAssetPath(thisIndex, true) + "_" + p);
                                    newSubmesh.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("DUMMY"); //TODO: replace
                                }
                            }
                        }
                        break;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(flowgraphGO, GetFlowgraphAssetPath(levelData.CommandsPAK.AllFlowgraphs[i]));
            Destroy(flowgraphGO);
        }
        AssetDatabase.StopAssetEditing();
        Destroy(rootGO);
        *//*
    }

    private string GetMeshAssetPath(int binIndex, bool resourcePath = false)
    {
        string basePath = SharedVals.instance.LevelName + "/Meshes/" + levelData.ModelsPAK.modelBIN.ModelFilePaths[binIndex] + "_" + levelData.ModelsPAK.modelBIN.ModelLODPartNames[binIndex];
        if (resourcePath) return basePath;
        else return "Assets/Resources/" + basePath + ".asset";
    }
    private string GetFlowgraphAssetPath(CATHODE.Scripting.Composite flowgraph, bool resourcePath = false)
    {
        string basePath = SharedVals.instance.LevelName + "/Flowgraphs/" + flowgraph.name.Replace("\\", "/").Replace(":", "_");
        if (resourcePath) return basePath;
        else return "Assets/Resources/" + basePath + ".prefab";
    }

    private string GetDirectory(string path)
    {
        return path.Substring(0, path.Length - Path.GetFileName(path).Length);
    }
}
*/