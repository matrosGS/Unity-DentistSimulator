using MarchingCubesProject;
using UnityEngine;

namespace MarchingCubes.Marching
{
    public class VoxelControl : MonoBehaviour
    {
        [SerializeField] public VoxelData VoxelData;
//        private void OnMouseDown()
//        {
//            Debug.Log(VoxelData);
//            FindObjectOfType<Example>().ChangeVoxel(VoxelData);
//        }
//
//        private void OnTriggerEnter(Collider other)
//        {
//            Debug.Log("Test");
//            if (other.gameObject.GetComponent<Tool>() != null)
//            {
//                FindObjectOfType<Example>().DisableVoxel(VoxelData);
//            }
//        }
    }
}