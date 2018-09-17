using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MarchingCubes.Marching;
using UnityEditor;
using UnityEngine.Serialization;
using Tool = MarchingCubes.Tool;
#pragma warning disable 162
using ImprovedPerlinNoiseProject;

namespace MarchingCubesGPUProject
{
    public class MarchingCubesGPU_4DNoise : MonoBehaviour
    {
        //The size of the voxel array for each dimension
        public int N = 40;

        //The size of the buffer that holds the verts.
        //This is the maximum number of verts that the 
        //marching cube can produce, 5 triangles for each voxel.
        private int size;

        public int m_seed;

        public float m_speed = 2.0f;

        public Material m_drawBuffer;

//        public ComputeShader m_perlinNoise;

        public ComputeShader m_marchingCubes;

        public ComputeShader m_normals;

        public ComputeShader m_clearBuffer;

        ComputeBuffer voxelBuffer, m_meshBuffer;

        RenderTexture m_normalsBuffer;

        ComputeBuffer m_cubeEdgeFlags, m_triangleConnectionTable;

//        private VoxelData[] voxels;
        private VoxelData[] voxels;
        private float[] voxelValues;

        public bool InitCube;

        public Tool Tool;
        public GameObject VoxelPrefab;

        private bool isDirty;
        
        private void Start()
        {

            size = N * N * N * 3 * 5;
            voxels = new VoxelData[N * N * N];
            voxelValues = new float[N * N * N];
            
            MeshReader meshReader = FindObjectOfType<MeshReader>();
            Mesh mesh = null;
            var meshVertices = new List<Vector3>();
            var voxelPositions = new Vector3[voxels.Length];
            
            if (meshReader != null)
            {
                mesh = meshReader.mesh;
                meshVertices = new List<Vector3>(mesh.vertices.Length);
                voxelPositions = new Vector3[voxels.Length];
            
                foreach (Vector3 meshVertex in mesh.vertices)
                {
//                Debug.Log(meshVertex * 0.5f);
                    var newVector = meshVertex;
//                var newVector = meshVertex + Vector3.one;
//                newVector *= 0.5f;
//                newVector = new Vector3(Mathf.Clamp(newVector.x, (float)1 / (N - 1), 1 - (float)1 / (N - 1)),
//                    Mathf.Clamp(newVector.y, (float)1 / (N - 1), 1 - (float)1 / (N - 1)),
//                    Mathf.Clamp(newVector.z, (float)1 / (N - 1), 1 - (float)1 / (N - 1)));
                    meshVertices.Add(newVector);
                }
            }
            
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
                        voxelValues[idx] = 0;
                        if (meshReader != null)
                        {
                            voxelPositions[idx] = new Vector3(fx, fy, fz);
                            if (meshVertices.Any(vertex => (vertex * 0.3f + Vector3.one * 0.1f).AlmostEquals(voxelPositions[idx])))
                            {
                                voxels[idx].Value = 1;
                                voxelValues[idx] = 1;
//                            Debug.Log(idx + " " + voxels[idx].Position);
                            }
                        }
                       
//                        ShowVoxel(voxels[idx].Position);
                        
                        if (InitCube)
                        {
//                            if (fx > start.x && fx < end.x)
//                            {
//                                if (fy > start.y && fy < end.y)
//                                {
//                                    if (fz > start.z && fz < end.z)
//                                    {
//                                        voxels[idx].Value = 1;
//                                        voxelValues[idx] = 1;
//                                    }
//                                }
//                            } 
                            
//                            if (fx > 0 + (float)1 / (N - 1) && fx < 1 - (float)1 / (N - 1))
//                            {
//                                if (fy > 0 + (float)1 / (N - 1) && fy < 1 - (float)1 / (N - 1))
//                                {
//                                    if (fz > 0 + (float)1 / (N - 1) && fz < 1 - (float)1 / (N - 1))
//                                    {
//                                        voxels[idx].Value = 1;
//                                        voxelValues[idx] = 1;
//                                    }
//                                }
//                            }
                            
                            if (fx > 0.1 && fx < 0.9)
                            {
                                if (fy > 0.1 && fy < 0.9)
                                {
                                    if (fz > 0.1 && fz < 0.9)
                                    {
                                        voxels[idx].Value = 1;
                                        voxelValues[idx] = 1;
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
            m_meshBuffer = new ComputeBuffer(size, sizeof(float) * 7);

            //These two buffers are just some settings needed by the marching cubes.
            m_cubeEdgeFlags = new ComputeBuffer(256, sizeof(int));
            m_cubeEdgeFlags.SetData(MarchingCubesTables.CubeEdgeFlags);
            m_triangleConnectionTable = new ComputeBuffer(256 * 16, sizeof(int));
            m_triangleConnectionTable.SetData(MarchingCubesTables.TriangleConnectionTable);
            
            Render();
        }

        void Update()
        {
//            m_normals.SetVector("_Position", ct.forward);
            
            Tool.Bounds.center = Tool.transform.position;

            foreach (VoxelData voxelData in voxels)
            {
                if (voxelData.Value == 0)
                {
                    continue;
                }

                if (Tool.Bounds.Intersects(voxelData.Bounds))
                {
                    DisableVoxel(voxelData);
                }
            }
        }
        
        private void OnDrawGizmos()
        {
            if (voxels == null)
            {
                return;
            }

            for (int index = 0; index < voxels.Length; index += 1)
            {
              //  Gizmos.DrawSphere(voxels[index].Position, 0.1f);
//                Handles.Label(voxels[index].Position, "Index " + index);
            }
        }

        /// <summary>
        /// Draws the mesh when cameras OnPostRender called.
        /// </summary>
        /// <param name="camera"></param>
        void DrawMesh(Camera camera)
        {
            //Since mesh is in a buffer need to use DrawProcedual called from OnPostRender or OnRenderObject
            m_drawBuffer.SetBuffer("_Buffer", m_meshBuffer);
            m_drawBuffer.SetPass(0);

            Graphics.DrawProcedural(MeshTopology.Triangles, size);
        }

        public void DisableVoxel(VoxelData voxelData)
        {
            voxels[voxelData.Index].Value = 0;
            voxelValues[voxelData.Index] = 0;
            isDirty = true;
        }

        private void LateUpdate()
        {
            if (isDirty)
            {
                isDirty = false;
                Render();
            }
        }

        
        public void ShowVoxel(Vector3 position)
        {
            Instantiate(VoxelPrefab, position, Quaternion.identity, transform);
        }

        private void Render()
        {
            voxelBuffer.SetData(voxelValues);
            voxelBuffer.SetCounterValue((uint) voxelValues.Length);

            //Clear the buffer from last frame.
            m_clearBuffer.SetInt("_Width", N);
            m_clearBuffer.SetInt("_Height", N);
            m_clearBuffer.SetInt("_Depth", N);
            m_clearBuffer.SetBuffer(0, "_Buffer", m_meshBuffer);

            m_clearBuffer.Dispatch(0, N / 8, N / 8, N / 8);

//            //Make the voxels.
//            m_perlinNoise.SetInt("_Width", N);
//            m_perlinNoise.SetInt("_Height", N);
//            m_perlinNoise.SetFloat("_Frequency", 0.02f);
//            m_perlinNoise.SetFloat("_Lacunarity", 2.0f);
//            m_perlinNoise.SetFloat("_Gain", 0.5f);
//            m_perlinNoise.SetFloat("_Time", Time.realtimeSinceStartup * m_speed);
//            m_perlinNoise.SetTexture(0, "_PermTable1D", perlin.PermutationTable1D);
//            m_perlinNoise.SetTexture(0, "_PermTable2D", perlin.PermutationTable2D);
//            m_perlinNoise.SetTexture(0, "_Gradient4D", perlin.Gradient4D);
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
    }
}