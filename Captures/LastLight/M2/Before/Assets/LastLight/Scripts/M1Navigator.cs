using UnityEngine;
using UnityEngine.AI;

namespace LastLight
{
    // Only adapts Unity's navigation to session pause and pooled actor lifetimes.
    public sealed class M1Navigator : MonoBehaviour
    {
        private NavMeshAgent agent;
        private M1Session session;
        private bool moving;
        public NavMeshAgent Agent => agent;
        public bool Available => agent != null && agent.enabled && agent.isOnNavMesh;
        public void Initialize(M1Session owner, float speed)
        {
            session = owner;
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.speed = speed; agent.angularSpeed = 220; agent.acceleration = 12;
            agent.radius = .4f; agent.height = 1.4f; agent.stoppingDistance = .15f;
            agent.autoRepath = true; moving = false;
            if (NavMesh.SamplePosition(transform.position, out var hit, 3, NavMesh.AllAreas))
            {
                transform.position = hit.position; agent.enabled = true; agent.Warp(hit.position); agent.isStopped = true;
            }
            else Debug.LogError("M1 导航起点不在 NavMesh 上：" + name, this);
        }
        private void Update()
        {
            if (Available) agent.isStopped = session == null || session.Paused || !moving;
        }
        public bool Go(Vector3 destination)
        {
            if (!Available) return false;
            moving = true; agent.isStopped = session.Paused;
            if (!agent.hasPath || (agent.destination - destination).sqrMagnitude > .16f)
                return agent.SetDestination(destination);
            return true;
        }
        public void Stop()
        {
            moving = false;
            if (Available) { agent.isStopped = true; agent.ResetPath(); }
        }
        public void Shutdown() { Stop(); if (agent != null) agent.enabled = false; }
        private void OnDisable() { Shutdown(); }
    }
}
