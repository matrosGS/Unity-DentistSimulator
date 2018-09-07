using System;
using UnityEngine;
using System.Collections.Generic;
using MarchingCubes;
using MarchingCubes.Marching;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace MarchingCubesProject
{
    public enum MARCHING_MODE
    {
        CUBES,
        TETRAHEDRON
    };

    public class Example : MonoBehaviour
    {
        public Material m_material;

        public MARCHING_MODE mode = MARCHING_MODE.CUBES;

        public int seed = 0;

        public Vector3 start;
        public Vector3 end;

        List<GameObject> meshes = new List<GameObject>();

        public Tool Tool;

        //The size of voxel array.
        public int Width = 4;
        public int Height = 4;
        public int Length = 4;

        public GameObject BuiltMesh;
        public GameObject Voxel;
        public Transform Parent;

        private VoxelData[] voxels;
        public bool InitCube;
        public bool IsActive;
        public bool ShowGrid;

        private Mesh mesh;
        private Renderer rend;
        private MeshFilter meshFilter;
        private Marching marching;
        private List<Vector3> verts;
        private List<int> indices;

        private void Start()
        {
            voxels = new VoxelData[Width * Height * Length];

            mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            BuiltMesh = new GameObject("Mesh");
            BuiltMesh.transform.parent = transform;
            meshFilter = BuiltMesh.AddComponent<MeshFilter>();
            BuiltMesh.AddComponent<MeshRenderer>();
            BuiltMesh.GetComponent<Renderer>().material = m_material;

            indices = new List<int>(Width * Height * Length * 8 * 3);
            verts = new List<Vector3>(Width * Height * Length * 8);
            
            if (mode == MARCHING_MODE.TETRAHEDRON)
                marching = new MarchingTertrahedron();
            else
                marching = new MarchingCubes();

            marching.Surface = 0.0f;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Length; z++)
                    {
                        float fx = x / (Width - 1.0f);
                        float fy = y / (Height - 1.0f);
                        float fz = z / (Length - 1.0f);

                        int idx = x + y * Height + z * Width * Length;
                        voxels[idx] =
                            new VoxelData(new Vector3(fx * (Width - 1.0f), fy * (Width - 1.0f), fz * (Width - 1.0f)), 0,
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
//                                        Debug.Log(transform.InverseTransformPoint(voxels[idx].Position));
                                    }
                                }
                            }
                        }

//                        voxels[idx] = fractal.Sample3D(fx, fy, fz);
                    }
                }
            }

            Build(voxels);
        }

        private void Build(VoxelData[] voxels)
        {
//            for (int i = Parent.childCount - 1; i > 0; i--)
//            {
//                Destroy(Parent.GetChild(i).gameObject);
//            }

            //Set the mode used to create the mesh.
            //Cubes is faster and creates less verts, tetrahedrons is slower and creates more verts but better represents the mesh surface.

            //Surface is the value that represents the surface of mesh
            //For example the perlin noise has a range of -1 to 1 so the mid point is where we want the surface to cut through.
            //The target value does not have to be the mid point it can be any value with in the range.

//            if (ShowGrid)
//            {
//                foreach (VoxelData voxelData in voxels)
//                {
//                    ShowVoxel(voxelData);
//                }
//            }
            
            Profiler.BeginSample("Clear array");
            
            verts.Clear();
            indices.Clear();
            Profiler.EndSample();

            //The mesh produced is not optimal. There is one vert for each index.
            //Would need to weld vertices for better quality mesh.
            marching.Generate(voxels, Width, Height, Length, verts, indices);

            //A mesh in unity can only be made up of 65000 verts.
            //Need to split the verts between multiple meshes.

//            int maxVertsPerMesh = 60000; //must be divisible by 3, ie 3 verts == 1 triangle
//            int numMeshes = verts.Count / maxVertsPerMesh + 1;

//            for (int i = 0; i < 1; i++)
//            {
//                List<Vector3> splitVerts = new List<Vector3>();
//                List<int> splitIndices = new List<int>();
//
//                for (int j = 0; j < maxVertsPerMesh; j++)
//                {
//                    int idx = i * maxVertsPerMesh + j;
//
//                    if (idx < verts.Count)
//                    {
//                        splitVerts.Add(verts[idx]);
//                        splitIndices.Add(j);
//                    }
//                }
//
//                if (splitVerts.Count == 0) continue;

//                Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

//                if (BuiltMesh != null)
//                {
//                    Destroy(BuiltMesh);
//                }

//                BuiltMesh = new GameObject("Mesh");
//                BuiltMesh.transform.parent = transform;
//                BuiltMesh.AddComponent<MeshFilter>();
//                BuiltMesh.AddComponent<MeshRenderer>();

            meshFilter.mesh = mesh;
//                go.transform.localPosition = new Vector3(-width / 2, -height / 2, -length / 2);

//            meshes.Add(BuiltMesh);
//            }
        }

        private void ShowVoxel(VoxelData voxelData)
        {
            var voxel = Instantiate(Voxel, voxelData.Position, Quaternion.identity, Parent);
            if (voxelData.Value == 1)
            {
                voxel.GetComponent<Renderer>().material.color = Color.black;
            }
            else
            {
                voxel.GetComponent<Renderer>().material.color = Color.white;
            }

            var isFrontSurface = voxelData.Index < Width * Height;
            var isBackSurface = voxelData.Index > voxels.Length - Width * Height;
            var isLeftSurface = voxelData.Index % Width == 0;
            var isRightSurface = (voxelData.Index - Length + 1) % Width == 0;
            if (!isFrontSurface && !isBackSurface && !isLeftSurface && !isRightSurface)
            {
                voxel.AddComponent<VoxelControl>().VoxelData = voxelData;
            }
        }

        public void ChangeVoxel(VoxelData voxelData)
        {
            voxels[voxelData.Index].Value = Mathf.Abs(voxels[voxelData.Index].Value - 1);
            Build(voxels);
        }

        public void DisableVoxel(VoxelData voxelData)
        {
            voxels[voxelData.Index].Value = 0;
            Build(voxels);
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

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
            Gizmos.color = Color.red;
            GUI.color = Color.black;

//            if (voxels != null)
//            {
//                foreach (VoxelData voxel in voxels)
//                {
//                    UnityEditor.Handles.BeginGUI();
//                    var view = UnityEditor.SceneView.currentDrawingSceneView;
//                    Vector3 screenPos = view.camera.WorldToScreenPoint(voxel.Position);
//                    Vector2 size = GUI.skin.label.CalcSize(new GUIContent("Index " + voxel.Index));
//                    GUI.Label(
//                        new Rect(screenPos.x - (size.x / 2), -screenPos.y + view.position.height + 4, size.x, size.y),
//                        "Index " + voxel.Index);
//                    UnityEditor.Handles.EndGUI();
//                }
//            }

            if (BuiltMesh == null)
            {
                return;
            }

            foreach (Vector3 vertex in BuiltMesh.GetComponent<MeshFilter>().mesh.vertices)
            {
                Gizmos.DrawCube(vertex, Vector3.one / 10);
            }
        }
    }
}