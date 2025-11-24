using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class FlockingBirds : MonoBehaviour
{
    public Transform birdPrefab;
    [Range(1, 1000)] public int birdCount;
    [Range(0f, 100f)] public float speed;
    [Range(5, 100f)] public float scale = 15f;
    [Range(0.01f, 5f)] public float separationStrength = 0.5f;
    [Range(0.01f, 5f)] public float alignmentFactor = 0.5f;

    [Range(0.01f, 10f)] public float separationRange = 2f;
    [Range(0.01f, 10f)] public float visibleRange = 5f;

    private Transform[] _birds;

    private Vector2[] _velocities;


    private void Start()
    {
        _birds = new Transform[birdCount];
        _velocities = new Vector2[birdCount];

        for (int i = 0; i < _birds.Length; i++)
        {
            _birds[i] = Instantiate(birdPrefab, transform);
            _birds[i].position = Random.insideUnitCircle * 5;
            _birds[i].localScale = Vector3.one + Vector3.up / 2;
            _velocities[i] = new Vector2(Random.value, Random.value);
            _birds[i].up = _velocities[i];
        }
    }

    private void Update()
    {
        Camera.main.orthographicSize = scale;

        for (int i = 0; i < _birds.Length; i++)
        {
            var bird = _birds[i];

            //visual
            var velocity = _velocities[i];


            var cameraS = Camera.main.OrthographicBounds().size * 0.45f;
            float xRange = cameraS.x;
            float yRange = cameraS.y;
            var birdPosition = bird.position;

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


            // separation
            var closeNeighbors = _birds
                .Where(x => Vector2.Distance(x.position, birdPosition) < separationRange && x != bird).ToArray();
            var separationVector = Vector2.zero;

            if (closeNeighbors.Length > 0)
            {
                foreach (var neighbor in closeNeighbors)
                {
                    separationVector += ((Vector2)birdPosition - (Vector2)neighbor.position);
                }

                //     separationVector /= closeNeighbors.Length;
                separationVector *= separationStrength;
            }


            // alignment

            var visibleNeighbors = _birds
                .Where(x =>
                {
                    var dist = Vector2.Distance(x.position, birdPosition);
                    return dist < visibleRange && dist > separationRange && x != bird;
                }).ToArray();

            var avgNeighborVelocity = Vector2.zero;

            if (visibleNeighbors.Length > 0)
            {
                foreach (var neighbor in visibleNeighbors)
                {
                    avgNeighborVelocity += (Vector2)neighbor.position;
                }

                avgNeighborVelocity /= visibleNeighbors.Length;
                avgNeighborVelocity *= alignmentFactor;
            }

            // Cohesion

            // final velocity
            var finalVelocity = (velocity.normalized + separationVector - avgNeighborVelocity) *
                                (speed * Time.deltaTime);
            _velocities[i] = finalVelocity;
            bird.position = birdPosition + new Vector3(finalVelocity.x, finalVelocity.y, 0);
            bird.up = velocity.normalized;
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