﻿using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using MarchingCubes.Marching;
#pragma warning disable 162

using ImprovedPerlinNoiseProject;

namespace MarchingCubesGPUProject
{
    public class MarchingCubesGPUTest : MonoBehaviour
    {
        //The size of the voxel array for each dimension
        const int N = 64;

        //The size of the buffer that holds the verts.
        //This is the maximum number of verts that the 
        //marching cube can produce, 5 triangles for each voxel.
        const int SIZE = N * N * N * 3 * 5;

        public int m_seed = 0;

        public Material m_drawBuffer;


        public ComputeShader m_marchingCubes;

        public ComputeShader m_normals;

        ComputeBuffer voxelBuffer, m_meshBuffer;

        RenderTexture m_normalsBuffer;

        ComputeBuffer m_cubeEdgeFlags, m_triangleConnectionTable;

//        GPUPerlinNoise perlin;

        private VoxelData[] voxels;
        
        public Vector3 start = new Vector3(0.2f, 0.2f, 0.2f);
        public Vector3 end = new Vector3(0.8f, 0.8f, 0.8f);
        
        public bool InitCube;
        
        void Start()
        {
            
            voxels = new VoxelData[N * N * N];

            for (int x = 0; x < N; x++)
            {
                for (int y = 0; y < N; y++)
                {
                    for (int z = 0; z < N; z++)
                    {
                        float fx = x / (N - 1.0f);
                        float fy = y / (N - 1.0f);
                        float fz = z / (N - 1.0f);

                        int idx = x + y * N + z * N * N;
                        voxels[idx] =
                            new VoxelData(new Vector3(fx * (N - 1.0f), fy * (N - 1.0f), fz * (N - 1.0f)), 0,
                                idx);

                        if (InitCube)
                        {
                            if (fx > start.x && fx < end.x)
                            {
                                if (fy > start.y && fy < end.y)
                                {
                                    if (fz > start.z && fz < end.z)
                                    {
                                        voxels[idx].Value = 1;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //Allows this camera to draw mesh procedurally.
            PostRenderEvent.AddEvent(Camera.main, DrawMesh);

            //There are 8 threads run per group so N must be divisible by 8.
            if (N % 8 != 0)
                throw new System.ArgumentException("N must be divisible be 8");

            //Holds the voxel values, generated from perlin noise.
            voxelBuffer = new ComputeBuffer(N * N * N, sizeof(float));

            //Holds the normals of the voxels.
            m_normalsBuffer = new RenderTexture(N, N, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            m_normalsBuffer.dimension = TextureDimension.Tex3D;
            m_normalsBuffer.enableRandomWrite = true;
            m_normalsBuffer.useMipMap = false;
            m_normalsBuffer.volumeDepth = N;
            m_normalsBuffer.Create();

            //Holds the verts generated by the marching cubes.
            m_meshBuffer = new ComputeBuffer(SIZE, sizeof(float) * 7);

            //Clear the mesh verts to -1. See the TriangleConnectionTable.
            //Only verts that get generated will then have a value of 1.
            //Only required if reading back the mesh.
            //Could also use the ClearMesh compute shader provided.
            float[] val = new float[SIZE * 7];
            for (int i = 0; i < SIZE * 7; i++)
                val[i] = -1.0f;

            m_meshBuffer.SetData(val);

            //These two buffers are just some settings needed by the marching cubes.
            m_cubeEdgeFlags = new ComputeBuffer(256, sizeof(int));
            m_cubeEdgeFlags.SetData(MarchingCubesTables.CubeEdgeFlags);
            m_triangleConnectionTable = new ComputeBuffer(256 * 16, sizeof(int));
            m_triangleConnectionTable.SetData(MarchingCubesTables.TriangleConnectionTable);

            //Make the perlin noise, make sure to load resources to match shader used.
//            perlin = new GPUPerlinNoise(m_seed);
//            perlin.LoadResourcesFor3DNoise();

//            //Make the voxels.
//            m_perlinNoise.SetInt("_Width", N);
//            m_perlinNoise.SetInt("_Height", N);
//            m_perlinNoise.SetFloat("_Frequency", 0.02f);
//            m_perlinNoise.SetFloat("_Lacunarity", 2.0f);
//            m_perlinNoise.SetFloat("_Gain", 0.5f);
//            m_perlinNoise.SetTexture(0, "_PermTable2D", perlin.PermutationTable2D);
//            m_perlinNoise.SetTexture(0, "_Gradient3D", perlin.Gradient3D);
//            m_perlinNoise.SetBuffer(0, "_Result", voxelBuffer);
//
//            m_perlinNoise.Dispatch(0, N / 8, N / 8, N / 8);

            
            //Make the voxel normals.
            m_normals.SetInt("_Width", N);
            m_normals.SetInt("_Height", N);
            m_normals.SetBuffer(0, "_Noise", voxelBuffer);
            m_normals.SetTexture(0, "_Result", m_normalsBuffer);

            m_normals.Dispatch(0, N / 8, N / 8, N / 8);

            //Make the mesh verts
            m_marchingCubes.SetInt("_Width", N);
            m_marchingCubes.SetInt("_Height", N);
            m_marchingCubes.SetInt("_Depth", N);
            m_marchingCubes.SetInt("_Border", 1);
            m_marchingCubes.SetFloat("_Target", 0.0f);
            m_marchingCubes.SetBuffer(0, "_Voxels", voxelBuffer);
            m_marchingCubes.SetTexture(0, "_Normals", m_normalsBuffer);
            m_marchingCubes.SetBuffer(0, "_Buffer", m_meshBuffer);
            m_marchingCubes.SetBuffer(0, "_CubeEdgeFlags", m_cubeEdgeFlags);
            m_marchingCubes.SetBuffer(0, "_TriangleConnectionTable", m_triangleConnectionTable);

            m_marchingCubes.Dispatch(0, N / 8, N / 8, N / 8);

            //Reads back the mesh data from the GPU and turns it into a standard unity mesh.
            //ReadBackMesh(m_meshBuffer);

        }

        void Update()
        {

        }

        /// <summary>
        /// Draws the mesh when cameras OnPostRender called.
        /// </summary>
        /// <param name="camera"></param>
        void DrawMesh(Camera camera)
        {
            //Since mesh is in a buffer need to use DrawProcedual called from OnPostRender
            m_drawBuffer.SetBuffer("_Buffer", m_meshBuffer);
            m_drawBuffer.SetPass(0);

            Graphics.DrawProcedural(MeshTopology.Triangles, SIZE);
        }

        void OnDestroy()
        {
            //MUST release buffers.
            voxelBuffer.Release();
            m_meshBuffer.Release();
            m_cubeEdgeFlags.Release();
            m_triangleConnectionTable.Release();
            m_normalsBuffer.Release();

            PostRenderEvent.RemoveEvent(Camera.main, DrawMesh);
        }

        struct Vert
        {
            public Vector4 position;
            public Vector3 normal;
        };

        /// <summary>
        /// Reads back the mesh data from the GPU and turns it into a standard unity mesh.
        /// </summary>
        /// <returns></returns>
        List<GameObject> ReadBackMesh(ComputeBuffer meshBuffer)
        {
            //Get the data out of the buffer.
            Vert[] verts = new Vert[SIZE];
            meshBuffer.GetData(verts);

            //Extract the positions, normals and indexes.
            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> index = new List<int>();

            List<GameObject> objects = new List<GameObject>();

            int idx = 0;
            for (int i = 0; i < SIZE; i++)
            {
                //If the marching cubes generated a vert for this index
                //then the position w value will be 1, not -1.
                if (verts[i].position.w != -1)
                {
                    positions.Add(verts[i].position);
                    normals.Add(verts[i].normal);
                    index.Add(idx++);
                }

                int maxTriangles = 65000 / 3;

                if(idx >= maxTriangles)
                {
                    objects.Add(MakeGameObject(positions, normals, index));
                    idx = 0;
                    positions.Clear();
                    normals.Clear();
                    index.Clear();
                }
            }

            return objects;
        }

        GameObject MakeGameObject(List<Vector3> positions, List<Vector3> normals, List<int> index)
        {
            Mesh mesh = new Mesh();
            mesh.vertices = positions.ToArray();
            mesh.normals = normals.ToArray();
            mesh.bounds = new Bounds(new Vector3(0, N / 2, 0), new Vector3(N, N, N));
            mesh.SetTriangles(index.ToArray(), 0);

            GameObject go = new GameObject("Voxel Mesh");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));
            go.GetComponent<MeshFilter>().mesh = mesh;
            go.isStatic = true;

            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            go.transform.parent = transform;

            //Draw mesh next too the one draw procedurally.
            go.transform.localPosition = new Vector3(N + 2, 0, 0);

            return go;
        }
    }
}





























