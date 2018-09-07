using System;
using UnityEngine;

namespace MarchingCubes.Marching
{
    [Serializable]
    public class VoxelData
    {
        public Vector3 Position;
        public int Value;
        public int Index;
        public Bounds Bounds;
        
        public VoxelData(Vector3 position, int value, int index)
        {
            Position = position;
            Value = value;
            Index = index;
            Bounds = new Bounds(Position, Vector3.one / 10);
        }

        public override string ToString()
        {
            return Position + " " + Value + " " + Index;
        }
    }
    
    
}