using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Pooling
{
    [CreateAssetMenu]
    public class AddressablesGameObjectPool : ScriptableObject, IGameObjectPool
    {
        private Dictionary<string, AddressableGameObjectPoolPreloadParams> _gameObjectsToPreload =
            new Dictionary<string, AddressableGameObjectPoolPreloadParams>();

        private Dictionary<string, Queue<GameObject>> _pooledInstances = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, GameObject> _gameObjectGroups = new Dictionary<string, GameObject>();
        private Dictionary<string, GameObject> _masterCopies = new Dictionary<string, GameObject>();

        private Dictionary<string, AsyncOperationHandle<GameObject>> _asyncOperationHandles =
            new Dictionary<string, AsyncOperationHandle<GameObject>>();

        private GameObject _poolGroup;
        
        public void AddAddressableForPreload(AddressableGameObjectPoolPreloadParams preloadParams)
        {
            if (_gameObjectsToPreload.ContainsKey(preloadParams.Address))
            {
                return;
            }
            _gameObjectsToPreload.Add(preloadParams.Address, preloadParams);
        }
        
        public async void PreloadInstances(Action onComplete, Action<Exception> onError = null)
        {
            if (_poolGroup == null)
            {
                _poolGroup = new GameObject("Pooled Instances");
                GameObject.DontDestroyOnLoad(_poolGroup);
            }

            var gameObjectsList = _gameObjectsToPreload.Values.ToList();
            foreach (var gameObjectToPreload in gameObjectsList)
            {
                var key = gameObjectToPreload.Address;
                var value = gameObjectToPreload;

                var asyncOperationHandle = Addressables.LoadAssetAsync<GameObject>(key);
                _asyncOperationHandles[key] = asyncOperationHandle;
                asyncOperationHandle.Completed += delegate(AsyncOperationHandle<GameObject> handle)
                {
                    var prefab = handle.Result;

                    if (!_gameObjectGroups.TryGetValue(key, out var gameObjectGroup))
                    {
                        gameObjectGroup = new GameObject($"{key} Pool Group");
                        GameObject.DontDestroyOnLoad(gameObjectGroup);
                        _gameObjectGroups.Add(key, gameObjectGroup);
                    }

                    gameObjectGroup.transform.SetParent(_poolGroup.transform);
                    
                    var wasPrefabEnabled = prefab.activeSelf;
                    prefab.SetActive(false);
                    _masterCopies[key] = prefab;
                    
                    var queue = new Queue<GameObject>();

                    if (!_pooledInstances.ContainsKey(key))
                    {
                        _pooledInstances.Add(key, queue);
                    }

                    for (var i = 0; i < value.NumInstances; i++)
                    {
                        var newInstance = EnqueueNewInstanceFromPrefab(prefab, key);
                        newInstance.transform.SetParent(gameObjectGroup.transform);
                    }

                    prefab.SetActive(wasPrefabEnabled);
                };
                await asyncOperationHandle.Task;
            }
            
            _gameObjectsToPreload.Clear();
            
            onComplete?.Invoke();
        }

        private GameObject EnqueueNewInstanceFromPrefab(GameObject prefab, string address)
        {
            if (!_pooledInstances.TryGetValue(address, out var queue))
            {
                return null;
            }
            
            var newInstance = GameObject.Instantiate(prefab);
            newInstance.name = newInstance.name.Replace("(Clone)", string.Empty);
            GameObject.DontDestroyOnLoad(newInstance);
            
            var pooledGameObject = newInstance.AddComponent<PooledGameObject>();
            pooledGameObject.GameObjectPool = this;
            pooledGameObject.PoolGroupId = address;
            
            queue.Enqueue(newInstance);
            return newInstance;
        }

        public void ClearPool()
        {
            if (_poolGroup == null)
            {
                return;
            }
            
            foreach (var kvp in _pooledInstances)
            {
                while (kvp.Value.Count > 0)
                {
                    var currentInstance = kvp.Value.Dequeue();
                    GameObject.Destroy(currentInstance);
                }

                if (_gameObjectGroups.TryGetValue(kvp.Key, out var gameObjectGroup))
                {
                    GameObject.Destroy(gameObjectGroup);
                }

                if (_asyncOperationHandles.TryGetValue(kvp.Key, out var asyncOperationHandle))
                {
                    Addressables.Release(asyncOperationHandle);
                }
            }
            _masterCopies.Clear();
            _pooledInstances.Clear();
            _gameObjectGroups.Clear();
            
            GameObject.Destroy(_poolGroup);
        }

        public void SpawnFromPool(
            string gameObjectId,
            SpawnFromPoolParams spawnFromPoolParams,
            Action<GameObject> onSpawnGameObject,
            Action<Exception> onError = null)
        {
            if (string.IsNullOrEmpty(gameObjectId))
            {
                Debug.LogError($"{nameof(gameObjectId)} is null or empty!");
                onError?.Invoke(new InvalidOperationException($"{nameof(gameObjectId)} is null or empty!"));
                return;
            }
            if (!_pooledInstances.TryGetValue(gameObjectId, out var queue))
            {
                AddAddressableForPreload(new AddressableGameObjectPoolPreloadParams()
                {
                    Address = gameObjectId,
                    NumInstances = 1
                });
                PreloadInstances(delegate
                {
                    SpawnFromPool(gameObjectId, spawnFromPoolParams, onSpawnGameObject, onError);
                });
                Debug.LogWarning($"Pool for {gameObjectId} did not exist; Needed to instantiate");
                return;
            }

            if (queue.Count == 0)
            {
                var prefab = _masterCopies[gameObjectId];
                var wasPrefabEnabled = prefab.activeSelf;
                prefab.SetActive(false);
                EnqueueNewInstanceFromPrefab(prefab, gameObjectId);
                prefab.SetActive(wasPrefabEnabled);
                Debug.LogWarning($"Needed to instantiate a new instance of {gameObjectId}");
            }

            var newInstance = queue.Dequeue();
            
            newInstance.transform.position = spawnFromPoolParams.Position;
            newInstance.transform.rotation = spawnFromPoolParams.Rotation;
            newInstance.transform.localScale = spawnFromPoolParams.Scale;
            newInstance.transform.SetParent(spawnFromPoolParams.Parent, !spawnFromPoolParams.TransformsRelativeToParent);
            
            newInstance.gameObject.SetActive(true);

            onSpawnGameObject?.Invoke(newInstance);
        }

        public void SpawnFromPool<T>(
            string gameObjectId,
            SpawnFromPoolParams spawnFromPoolParams,
            Action<T> onSpawnGameObject = null,
            Action<Exception> onError = null)
        {
            SpawnFromPool(gameObjectId, spawnFromPoolParams, delegate(GameObject gameObject)
                {
                    var component = gameObject.GetComponent<T>();

                    if (component == null)
                    {
                        onError?.Invoke(new InvalidOperationException($"Could not spawn GameObject by Component Type of {nameof(T)}"));
                    }
                    
                    onSpawnGameObject?.Invoke(component);
                });
        }

        public bool ReturnToPool(GameObject gameObject)
        {
            var pooledGameObject = gameObject.GetComponent<PooledGameObject>();
            if (pooledGameObject == null)
            {
                Debug.LogWarning($"{nameof(gameObject.name)} was never pooled. Destroying instead.");
                GameObject.Destroy(gameObject);
                return false;
            }

            if (!_pooledInstances.TryGetValue(pooledGameObject.PoolGroupId, out var queue))
            {
                return false;
            }

            if (!_gameObjectGroups.TryGetValue(pooledGameObject.PoolGroupId, out var gameObjectGroup))
            {
                return false;
            }

            if (queue.Contains(gameObject))
            {
                Debug.LogError($"{nameof(gameObject)} already exists in pool queue!", gameObject);
                return false;
            }
            
            gameObject.SetActive(false);
            gameObject.transform.SetParent(gameObjectGroup.transform);
            queue.Enqueue(gameObject);
            return true;
        }

        public bool ReturnToPool<T>(T gameObjectByComponent) where T : MonoBehaviour
        {
            return ReturnToPool(gameObjectByComponent.gameObject);
        }
    }
}