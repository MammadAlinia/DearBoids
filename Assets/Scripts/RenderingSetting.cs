using UnityEngine;

namespace DearBoids
{
    [CreateAssetMenu(fileName = "RenderingSetting", menuName = "DearBoids/RenderingSetting", order = 2)]
    public class RenderingSetting : ScriptableObject
    {
        public Mesh mesh;
        public Material material;
    }
}