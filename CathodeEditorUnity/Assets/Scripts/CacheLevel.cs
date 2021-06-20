using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CATHODE.Commands;
using UnityEditor;
using System.IO;
using System.Linq;
using System;

public class CacheLevel : MonoBehaviour
{
    private alien_level levelData;
    private GameObject levelGO;

    void Start()
    {
        levelData = CATHODE.AlienLevel.Load(SharedVals.instance.LevelName, SharedVals.instance.PathToEnv);
        levelGO = new GameObject(SharedVals.instance.LevelName);

        LoadTextureAssets();
        LoadMeshAssets();
        LoadMaterialAssets();
        LoadFlowgraphAssets(levelData.CommandsPAK);

        for (int i = 0; i < levelData.CommandsPAK.EntryPoints.Count; i++)
        {
            GameObject flowgraphGO = PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>(GetFlowgraphAssetPath(levelData.CommandsPAK.EntryPoints[i], true))) as GameObject;
            flowgraphGO.name = levelData.CommandsPAK.EntryPoints[i].name;
            flowgraphGO.transform.parent = levelGO.transform;
        }

        PrefabUtility.SaveAsPrefabAsset(levelGO, "Assets/Resources/" + SharedVals.instance.LevelName + "/" + SharedVals.instance.LevelName + ".prefab");
    }

    private void LoadTextureAssets()
    {
        bool[] textureTracker = new bool[levelData.LevelTextures.BIN.Header.EntryCount];
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < levelData.LevelTextures.PAK.Entries.Count; i++)
        {
            alien_pak_entry Entry = levelData.LevelTextures.PAK.Entries[i];
            alien_texture_bin_texture InTexture = levelData.LevelTextures.BIN.Textures[Entry.BINIndex];

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
            if (InTexture.Format == alien_texture_format.Alien_FORMAT_SIGNED_DISTANCE_FIELD || InTexture.Format == alien_texture_format.Alien_FORMAT_BC2) continue;

            TextureFormat format = TextureFormat.BC7;
            switch (InTexture.Format)
            {
                case alien_texture_format.Alien_R32G32B32A32_SFLOAT:
                    format = TextureFormat.RGBA32;
                    break;
                case alien_texture_format.Alien_FORMAT_R8G8B8A8_UNORM:
                    format = TextureFormat.ETC2_RGBA8; //?
                    break;
                case alien_texture_format.Alien_FORMAT_R8G8B8A8_UNORM_0:
                    format = TextureFormat.ETC2_RGBA8; //?
                    break;
                case alien_texture_format.Alien_FORMAT_R8:
                    format = TextureFormat.R8;
                    break;
                case alien_texture_format.Alien_FORMAT_BC1:
                    format = TextureFormat.DXT1;
                    break;
                case alien_texture_format.Alien_FORMAT_BC5:
                    format = TextureFormat.BC5; //Is this correct?
                    break;
                case alien_texture_format.Alien_FORMAT_BC3:
                    format = TextureFormat.DXT5;
                    break;
                case alien_texture_format.Alien_FORMAT_BC7:
                    format = TextureFormat.BC7;
                    break;
                case alien_texture_format.Alien_FORMAT_R8G8:
                    format = TextureFormat.BC5; // is this correct?
                    break;
            }

            BinaryReader tempReader = new BinaryReader(new MemoryStream(levelData.LevelTextures.PAK.DataStart));
            tempReader.BaseStream.Position = Entry.Offset;
            if (InTexture.Type == 7)
            {
                Cubemap cubemap = new Cubemap((int)textureDims.x, format, true);
                cubemap.name = levelData.LevelTextures.BIN.TextureFilePaths[Entry.BINIndex];
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
                texture.name = levelData.LevelTextures.BIN.TextureFilePaths[Entry.BINIndex];
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
    }

    private void LoadMeshAssets()
    {
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < levelData.ModelsPAK.Models.Count; i++)
        {
            alien_pak_model_entry ChunkArray = levelData.ModelsPAK.Models[i];
            for (int ChunkIndex = 0; ChunkIndex < ChunkArray.Header.ChunkCount; ++ChunkIndex)
            {
                int BINIndex = ChunkArray.ChunkInfos[ChunkIndex].BINIndex;
                alien_model_bin_model_info Model = levelData.ModelsBIN.Models[BINIndex];
                //if (Model.BlockSize == 0) continue;

                alien_vertex_buffer_format VertexInput = levelData.ModelsBIN.VertexBufferFormats[Model.VertexFormatIndex];
                alien_vertex_buffer_format VertexInputLowDetail = levelData.ModelsBIN.VertexBufferFormats[Model.VertexFormatIndexLowDetail];

                BinaryReader Stream = new BinaryReader(new MemoryStream(ChunkArray.Chunks[ChunkIndex]));

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
                    CATHODE.Utilities.Align(ref Stream, 16);
                }

                if (InVertices.Count == 0) continue;

                Mesh thisMesh = new Mesh();
                thisMesh.name = levelData.ModelsBIN.ModelFilePaths[BINIndex] + ": " + levelData.ModelsBIN.ModelLODPartNames[BINIndex];
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
                string fullFilePath = "Assets/Resources/" + SharedVals.instance.LevelName + "/Meshes/" + levelData.ModelsBIN.ModelFilePaths[BINIndex] + "_" + levelData.ModelsBIN.ModelLODPartNames[BINIndex] + "_" + ChunkIndex + ".asset";
                string fileDirectory = GetDirectory(fullFilePath);
                if (!Directory.Exists(fileDirectory)) Directory.CreateDirectory(fileDirectory);
                AssetDatabase.CreateAsset(thisMesh, fullFilePath);
            }
        }
        AssetDatabase.StopAssetEditing();
    }

    private void LoadMaterialAssets()
    {

    }

    private void LoadFlowgraphAssets(CommandsPAK commandsPAK)
    {
        //First, make dummy prefabs of all flowgraphs
        GameObject rootGO = new GameObject();
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < commandsPAK.AllFlowgraphs.Count; i++)
        {
            string fullFilePath = GetFlowgraphAssetPath(commandsPAK.AllFlowgraphs[i]);
            string fileDirectory = GetDirectory(fullFilePath);
            if (!Directory.Exists(fileDirectory)) Directory.CreateDirectory(fileDirectory);
            if (!File.Exists(fullFilePath)) PrefabUtility.SaveAsPrefabAsset(rootGO, fullFilePath);
        }
        AssetDatabase.StopAssetEditing();

        //Then, populate the prefabs for all flowgraphs
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < commandsPAK.AllFlowgraphs.Count; i++)
        {
            GameObject flowgraphGO = new GameObject(commandsPAK.AllFlowgraphs[i].name);
            for (int x = 0; x < commandsPAK.AllFlowgraphs[i].nodes.Count; x++)
            {
                CathodeFlowgraph flowgraphRef = commandsPAK.GetFlowgraph(commandsPAK.AllFlowgraphs[i].nodes[x].nodeType);
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
                    nodeGO = new GameObject(CathodeLib.NodeDB.GetFriendlyName(commandsPAK.AllFlowgraphs[i].nodes[x].nodeID));
                }
                nodeGO.transform.parent = flowgraphGO.transform;
                foreach (CathodeParameterReference paramRef in commandsPAK.AllFlowgraphs[i].nodes[x].nodeParameterReferences)
                {
                    CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
                    if (param == null) continue;
                    if (param.dataType != CathodeDataType.POSITION) continue;
                    CathodeTransform transform = (CathodeTransform)param;
                    nodeGO.transform.position = transform.position;
                    nodeGO.transform.rotation = Quaternion.Euler(transform.rotation);
                    break;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(flowgraphGO, GetFlowgraphAssetPath(commandsPAK.AllFlowgraphs[i]));
            Destroy(flowgraphGO);
        }
        AssetDatabase.StopAssetEditing();
        Destroy(rootGO);
    }

    private string GetFlowgraphAssetPath(CathodeFlowgraph flowgraph, bool resourcePath = false)
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