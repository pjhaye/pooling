using UnityEngine;

namespace Pooling
{
    public class SpawnFromPoolParams
    {
        public Vector3 Position = Vector3.zero;
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Scale = Vector3.one;
        public Transform Parent = null;
        public bool TransformsRelativeToParent = false;
    }
}