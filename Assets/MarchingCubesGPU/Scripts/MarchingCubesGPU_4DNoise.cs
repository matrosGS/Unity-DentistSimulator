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

        public Material DrawBuffer;

        public ComputeShader MarchingCubes;

        public ComputeShader Normals;

        public ComputeShader ClearBuffer;

        public ComputeShader Collisions;

        private ComputeBuffer voxelBuffer;
        private ComputeBuffer MeshBuffer;

        private RenderTexture normalsBuffer;

        private ComputeBuffer cubeEdgeFlags;
        private ComputeBuffer triangleConnectionTable;

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
            var meshVertices = new List<Vector3>();
            var voxelPositions = new Vector3[voxels.Length];

            if (meshReader != null)
            {
                Mesh mesh = meshReader.mesh;
                meshVertices = new List<Vector3>(mesh.vertices.Length);
                voxelPositions = new Vector3[voxels.Length];

                foreach (Vector3 meshVertex in mesh.vertices)
                {
                    var newVector = meshVertex;
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
                            if (meshVertices.Any(vertex =>
                                (vertex * 0.3f + Vector3.one * 0.1f).AlmostEquals(voxelPositions[idx])))
                            {
                                voxels[idx].Value = 1;
                                voxelValues[idx] = 1;
                            }
                        }

//                        ShowVoxel(voxels[idx].Position);

                        if (InitCube)
                        {
                            if (fx > 0 + (float) 1 / (N - 1) && fx < 1 - (float) 1 / (N - 1))
                            {
                                if (fy > 0 + (float) 1 / (N - 1) && fy < 1 - (float) 1 / (N - 1))
                                {
                                    if (fz > 0 + (float) 1 / (N - 1) && fz < 1 - (float) 1 / (N - 1))
                                    {
                                        voxels[idx].Value = 1;
                                        voxelValues[idx] = 1;
                                    }
                                }
                            }

//                            if (fx > 0.1 && fx < 0.9)
//                            {
//                                if (fy > 0.1 && fy < 0.9)
//                                {
//                                    if (fz > 0.1 && fz < 0.9)
//                                    {
//                                        voxels[idx].Value = 1;
//                                        voxelValues[idx] = 1;
//                                    }
//                                }
//                            }
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
            normalsBuffer = new RenderTexture(N, N, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            normalsBuffer.dimension = TextureDimension.Tex3D;
            normalsBuffer.enableRandomWrite = true;
            normalsBuffer.useMipMap = false;
            normalsBuffer.volumeDepth = N;
            normalsBuffer.Create();

            //Holds the verts generated by the marching cubes.
            MeshBuffer = new ComputeBuffer(size, sizeof(float) * 7);

            //These two buffers are just some settings needed by the marching cubes.
            cubeEdgeFlags = new ComputeBuffer(256, sizeof(int));
            cubeEdgeFlags.SetData(MarchingCubesTables.CubeEdgeFlags);
            triangleConnectionTable = new ComputeBuffer(256 * 16, sizeof(int));
            triangleConnectionTable.SetData(MarchingCubesTables.TriangleConnectionTable);

            Render();
        }

        void Update()
        {
//            Tool.Bounds.center = Tool.transform.position;
//
//            foreach (VoxelData voxelData in voxels)
//            {
//                if (voxelData.Value == 0)
//                {
//                    continue;
//                }
//
//                if (Tool.Bounds.Intersects(voxelData.Bounds))
//                {
//                    DisableVoxel(voxelData);
//                }
//            }
        }

        private void OnDrawGizmos()
        {
            if (voxels == null)
            {
                return;
            }

            for (int index = 0; index < voxels.Length; index += 1)
            {
//                Gizmos.DrawSphere(voxels[index].Position, 0.1f);
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
            DrawBuffer.SetBuffer("_Buffer", MeshBuffer);
            DrawBuffer.SetPass(0);

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
            ClearBuffer.SetInt("_Width", N);
            ClearBuffer.SetInt("_Height", N);
            ClearBuffer.SetInt("_Depth", N);
            ClearBuffer.SetBuffer(0, "_Buffer", MeshBuffer);

            ClearBuffer.Dispatch(0, N / 8, N / 8, N / 8);

            //Make the voxel normals.
            Normals.SetInt("_Width", N);
            Normals.SetInt("_Height", N);
            Normals.SetBuffer(0, "_Noise", voxelBuffer);
            Normals.SetTexture(0, "_Result", normalsBuffer);

            Normals.Dispatch(0, N / 8, N / 8, N / 8);

            Collisions.SetVector("_ToolPosition", Tool.transform.position);
            Collisions.SetBuffer(0, "_Voxels", voxelBuffer);
            Collisions.SetBuffer(0, "_CollidedVoxels", voxelBuffer);

            Collisions.Dispatch(0, N / 8, N / 8, N / 8);

            var test = new float[4000];
            voxelBuffer.GetData(test);
            
            //Make the mesh verts
            MarchingCubes.SetInt("_Width", N);
            MarchingCubes.SetInt("_Height", N);
            MarchingCubes.SetInt("_Depth", N);
            MarchingCubes.SetInt("_Border", 1);
            MarchingCubes.SetFloat("_Target", 0.0f);
            MarchingCubes.SetBuffer(0, "_Voxels", voxelBuffer);
            MarchingCubes.SetTexture(0, "_Normals", normalsBuffer);
            MarchingCubes.SetBuffer(0, "_Buffer", MeshBuffer);
            MarchingCubes.SetBuffer(0, "_CubeEdgeFlags", cubeEdgeFlags);
            MarchingCubes.SetBuffer(0, "_TriangleConnectionTable", triangleConnectionTable);

            MarchingCubes.Dispatch(0, N / 8, N / 8, N / 8);
        }

        void OnDestroy()
        {
            //MUST release buffers.
            voxelBuffer.Release();
            MeshBuffer.Release();
            cubeEdgeFlags.Release();
            triangleConnectionTable.Release();
            normalsBuffer.Release();

            PostRenderEvent.RemoveEvent(Camera.main, DrawMesh);
        }
    }
}