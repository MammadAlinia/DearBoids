using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public struct BoidData
{
    public Vector3 Position;
    public Vector3 Velocity;
}

public class FlockingBirds : MonoBehaviour
{
    public Transform birdPrefab;
    [Range(1, 1000)] public int birdCount;
    [Range(0.0001f, 10f)] public float speed;
    [Range(5, 100f)] public float scale = 15f;

    [Header("Strength")] [Range(0.0f, 10f)]
    public float separationStrength = 0.5f;

    [Range(0.0f, 1f)] public float alignmentStrength = 0.5f;
    [Range(0.0f, 1f)] public float cohesionStrength = 0.5f;

    [Header("Range")] [Range(0.01f, 10f)] public float separationRange = 2f;
    [Range(0.01f, 10f)] public float visibleRange = 5f;

    private Transform[] transforms;

    private BoidData[] boidsData;


    private void Start()
    {
        transforms = new Transform[birdCount];
        boidsData = new BoidData[birdCount];
        Application.targetFrameRate = 60;

        for (int i = 0; i < transforms.Length; i++)
        {
            boidsData[i].Position = Random.insideUnitCircle * 5;
            boidsData[i].Velocity = new Vector2(Random.value, Random.value).normalized;


            transforms[i] = Instantiate(birdPrefab, transform);
            transforms[i].localScale = Vector3.one + Vector3.up / 2;
            transforms[i].up = boidsData[i].Velocity;
        }
    }

    private void Update()
    {
        Camera.main.orthographicSize = scale;
        var cameraS = Camera.main.OrthographicBounds().size * 0.45f;

        float xRange = cameraS.x;
        float yRange = cameraS.y;

        for (int i = 0; i < boidsData.Length; i++)

        {
            var boid = boidsData[i];

            var transformObj = transforms[i];


            var velocity = boidsData[i].Velocity;


            var birdPosition = boid.Position;


            // separation

            var closeBoidIndices = new List<int>();
            var inRangeBoidIndices = new List<int>();

            var separationVector = Vector3.zero;
            var avgVisibleVelocity = Vector3.zero;
            var avgCenterPosition = Vector3.zero;

            for (int bi = 0; bi < boidsData.Length; bi++) // optimize using spatial hashing or other search methods
            {
                if (i == bi) continue; // don't count the current boid  

                var cBoid = boidsData[bi];
                var distance = Vector3.Distance(birdPosition, cBoid.Position);

                //separation rules
                if (distance <= separationRange)
                {
                    closeBoidIndices.Add(bi);
                    separationVector += (boid.Position - cBoid.Position);
                }

                // alignment
                if (distance <= visibleRange)
                {
                    inRangeBoidIndices.Add(bi);
                    avgVisibleVelocity += boidsData[bi].Velocity;

                    // Cohesion
                    avgCenterPosition += boidsData[bi].Position;
                }
            }

            if (closeBoidIndices.Count > 0)
            {
                // separationVector /= closeBoidIndices.Count;
                separationVector *= (separationStrength);
            }

            if (inRangeBoidIndices.Count > 0)
            {
                avgVisibleVelocity /= inRangeBoidIndices.Count;
                avgVisibleVelocity = (avgVisibleVelocity - boid.Velocity) * alignmentStrength;

                avgCenterPosition /= inRangeBoidIndices.Count;
                avgCenterPosition = (avgCenterPosition - boid.Position) * cohesionStrength;
            }


            // screen edges
            if (birdPosition.y < -yRange && velocity.y < 0)
            {
                velocity = Vector2.Reflect(velocity, Vector3.down);
                birdPosition = new Vector3(birdPosition.x, -yRange, birdPosition.z);
            }

            if (birdPosition.y > yRange && velocity.y > 0)
            {
                velocity = Vector2.Reflect(velocity, Vector3.up);
                birdPosition = new Vector3(birdPosition.x, yRange, birdPosition.z);
            }

            if (birdPosition.x < -xRange)
            {
                velocity = Vector2.Reflect(velocity, Vector3.left);
                birdPosition = new Vector3(-xRange, birdPosition.y, birdPosition.z);
            }

            if (birdPosition.x > xRange)
            {
                velocity = Vector2.Reflect(velocity, Vector3.right);
                birdPosition = new Vector3(xRange, birdPosition.y, birdPosition.z);
            }

            boid.Velocity = velocity;

            var finalVelocity = (boid.Velocity.normalized + separationVector + avgVisibleVelocity + avgCenterPosition);


            // final velocity
            boid.Position = birdPosition + new Vector3(finalVelocity.x, finalVelocity.y, 0) * (speed * Time.deltaTime);
            boid.Velocity = finalVelocity;
            boidsData[i] = boid;

            // visual
            transformObj.position = boid.Position;
            transformObj.up = boid.Velocity.normalized;
        }
    }
}

public static class CameraExtensions
{
    public static Bounds OrthographicBounds(this Camera camera)
    {
        float screenAspect = (float)Screen.width / (float)Screen.height;
        float cameraHeight = camera.orthographicSize * 2;
        Bounds bounds = new Bounds(
            camera.transform.position,
            new Vector3(cameraHeight * screenAspect, cameraHeight, 0));
        return bounds;
    }
}