using UnityEngine;

namespace DearBoids
{
    [CreateAssetMenu(fileName = "FlockingSetting", menuName = "DearBoids/FlockingSetting", order = 1)]
    public class FlockingSettingData : ScriptableObject
    {
        [Header("Flocking Settings")] [Range(0f, 10f)]
        public float avoidanceRange = 1.5f;

        [Range(0f, 10f)] public float alignmentRange = 3f;
        [Range(0.0f, 10f)] public float avoidanceStrength = 0.3f;

        [Range(0.0f, 10f)] public float alignmentStrength = 0.1f;
        [Range(0.0f, 10f)] public float cohesionStrength = 0.03f;
        [Range(0.00f, 10f)] public float turnFactor = 5f;


        [Header("Simulation Settings")] public float Speed = 1f;
        public float boidMinSpeed = 5f;
        public float boidMaxSpeed = 10f;
    }
}