using ServiceLocator;
using UnityEngine;

namespace Pooling
{
    public class PooledGameObject : MonoBehaviour
    {
        public string _poolGroupId;

        public IGameObjectPool GameObjectPool
        {
            get;
            set;
        }

        public string PoolGroupId
        {
            get
            {
                return _poolGroupId;
            }
            set
            {
                _poolGroupId = value;
            }
        }

        public void ReturnToPool()
        {
            GameObjectPool.ReturnToPool(gameObject);
        }
    }
}