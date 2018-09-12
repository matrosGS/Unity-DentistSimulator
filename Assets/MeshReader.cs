using UnityEngine;

public class MeshReader : MonoBehaviour
{

    public MeshFilter MeshFilter;

    public Mesh mesh;

    private void Awake()
    {
        mesh = MeshFilter.sharedMesh;
    }
}