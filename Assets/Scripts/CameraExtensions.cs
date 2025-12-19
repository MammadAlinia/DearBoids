using UnityEngine;

public static class CameraExtensions
{
    public static Bounds OrthographicBounds(this Camera camera)
    {
        float cameraHeight = camera.orthographicSize * 2;
        float cameraWidth = cameraHeight * camera.aspect; // Use camera.aspect instead
    
        Bounds bounds = new Bounds(
            camera.transform.position,
            new Vector3(cameraWidth, cameraHeight, 0));
        return bounds;
    }
}