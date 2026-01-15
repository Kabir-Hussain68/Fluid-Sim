using UnityEngine;
using Unity.Mathematics;

public class ParticleSpawner : MonoBehaviour
{
    public int particleCount;
    public Vector2 initialVelocity;
    public Vector2 spawnCentre;
    public Vector2 spawnSize;
    public float jitterStr;
    public bool showSpawnBoundsGizmos;

    //Spawns the particles in a grid like structure
    public ParticleData Init()
    {
        ParticleData data = new ParticleData(particleCount);
        var rng = new Unity.Mathematics.Random(42);

        float2 s = spawnSize;
        int particleX = Mathf.CeilToInt(Mathf.Sqrt(s.x / s.y * particleCount + (s.x - s.y) * (s.x - s.y) / (4 * s.y * s.y)) - (s.x - s.y) / (2 * s.y));
        int particleY = Mathf.CeilToInt(particleCount / (float)particleX);

        int i = 0;
        for (int y = 0; y < particleY; y++)
        {
            for (int x = 0; x < particleX; x++)
            {
                if (i >= particleCount) break;

                float tx = particleX <= 1 ? 0.5f : x / (particleX - 1f);
                float ty = particleY <= 1 ? 0.5f : y / (particleY - 1f);

                float angle = (float)rng.NextDouble() * 3.14f * 2;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 jitter = dir * jitterStr * ((float)rng.NextDouble() - 0.5f);
                data.position[i] = new Vector2((tx - 0.5f) * spawnSize.x, (ty - 0.5f) * spawnSize.y) + jitter + spawnCentre;
                data.velocity[i] = initialVelocity;
                i++;
            }
        }

        return data;
    }

    public struct ParticleData
    {
        public float2[] position;
        public float2[] velocity;

        public ParticleData(int num)
        {
            position = new float2[num];
            velocity = new float2[num];
        }
    }


    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            Gizmos.color = new Color(1.0f, 0f, 1.0f);
            Gizmos.DrawWireCube(spawnCentre, spawnSize);
        }
    }
}
