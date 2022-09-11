using System;
using UnityEngine;

namespace Pooling
{
    [RequireComponent(typeof(ParticleSystem))]
    public class ReturnParticleSystemToPoolOnComplete : MonoBehaviour
    {
        private PooledGameObject _poolableObject;
        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _poolableObject = GetComponent<PooledGameObject>();
            _particleSystem = GetComponent<ParticleSystem>();

            if (_poolableObject == null)
            {
                Debug.LogError($"{nameof(_poolableObject)} is null!", this);
            }
            
            if (_particleSystem == null)
            {
                Debug.LogError($"{nameof(_particleSystem)} is null!", this);
            }
        }

        private void OnEnable()
        {
            _particleSystem.Play();
        }

        private void Update()
        {
            if (_particleSystem == null)
            {
                return;
            }

            if (_poolableObject == null)
            {
                return;
            }
            
            if (!_particleSystem.IsAlive(true))
            {
                _poolableObject.ReturnToPool();
            }
        }
    }
}