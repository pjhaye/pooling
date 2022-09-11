using System;
using UnityEngine;

namespace Pooling
{
    public interface IGameObjectPool
    {
        public void PreloadInstances(
            Action onComplete,
            Action<Exception> onError = null);
        
        public void ClearPool();
        
        public void SpawnFromPool(
            string gameObjectId,
            SpawnFromPoolParams spawnFromPoolParams,
            Action<GameObject> onSpawnGameObject = null,
            Action<Exception> onError = null);

        public void SpawnFromPool<T>(
            string gameObjectId,
            SpawnFromPoolParams spawnFromPoolParams,
            Action<T> onSpawnGameObject = null,
            Action<Exception> onError = null);

        public bool ReturnToPool(GameObject gameObject);
        
        public bool ReturnToPool<T>(T gameObjectByComponent) where T : MonoBehaviour;
    }
}