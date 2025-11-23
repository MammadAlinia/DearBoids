using System;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class FlockingBirds : MonoBehaviour
{
    public Transform birdPrefab;
    [Range(1, 1000)] public int birdCount;
    [Range(0f, 100f)] public float speed;
    [Range(0.01f, 2f)] public float scale = 0.3f;
    private Transform[] _birds;


    private void Start()
    {
        _birds = new Transform[birdCount];

        for (int i = 0; i < _birds.Length; i++)
        {
            _birds[i] = Instantiate(birdPrefab, transform);
            _birds[i].position = Random.insideUnitCircle * 5;
            _birds[i].localScale = Vector3.one * scale + Vector3.up * scale / 2;
            _birds[i].rotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
        }
    }

    private void Update()
    {
        for (int i = 0; i < _birds.Length; i++)
        {
            var bird = _birds[i];
            
            //visual
            bird.localScale = Vector3.one * scale + Vector3.up * scale / 2;
            
            
            var birdPosition = bird.position;
            
            //bounding box reflection
            var xRange = 10f;
            var yRange = 5f;

            var up = bird.up;

            if (bird.position.y < -yRange)
            {
                up = Vector3.Reflect(up, Vector3.down);
                bird.position = new Vector3(birdPosition.x,-yRange, birdPosition.z);
            }

            if (bird.position.y > yRange)
            {
                up = Vector3.Reflect(up, Vector3.up);
                bird.position = new Vector3(birdPosition.x,yRange, birdPosition.z);
            }

            if (bird.position.x < -xRange)
            {
                up = Vector3.Reflect(up, Vector3.left);
                bird.position = new Vector3(-xRange, birdPosition.y, birdPosition.z);
            }

            if (bird.position.x > xRange)
            {
                up = Vector3.Reflect(up, Vector3.right);
                bird.position = new Vector3(xRange, birdPosition.y, birdPosition.z);
            }

            bird.up = up;

            // final velocity
            var velocity = bird.up * (speed * Time.deltaTime);
            bird.position += velocity;
        }
    }
    
}