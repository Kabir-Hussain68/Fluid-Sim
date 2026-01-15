using UnityEngine;
using Unity.Mathematics;

public class Simulation2D : MonoBehaviour
{
    public event System.Action SimulationRunCompleted;
    [Header("Simulation Settings")]
    public float timeScale = 1;
    public bool fixedTimeStep;
    public int iterationsPerFrame;
    public float smoothingRadius = 2;
    public float gravity;
    [Range(0,1)]public float collisionDampening;
    public float nearPressureMultiplier;
    public float pressureMultiplier;
    public float targetDensity;
    public float viscocityStrength;
    public Vector2 boundBox;

    [Header("References")]
    public ComputeShader compute;
    public ParticleDisplay display;
    public ParticleSpawner spawner;

    //Buffers
    public ComputeBuffer positionBuffer{ get; private set;}
    public ComputeBuffer densityBuffer{ get; private set;}
    public ComputeBuffer velocityBuffer{ get; private set;}
    ComputeBuffer predictedPositionsBuffer;
    ComputeBuffer spatialOffsetsBuffer;
    ComputeBuffer spatialIndicesBuffer;
    GPUSort gpuSort;

    // Kernel IDs
    const int externalForcesKernel = 0;
    const int spatialHashKernel = 1;
    const int densityKernel = 2;
    const int pressureKernel = 3;
    const int viscosityKernel = 4;
    const int updatePositionKernel = 5;

    public int numParticles{ get; private set;}
    ParticleSpawner.ParticleData spawnData;

    //Called when the scene is loaded to initialize buffers, particles and their shaders
    void Start()
    {
        //Called to spawn the paricles and get there quantity
        spawnData = spawner.Init();
        numParticles = spawnData.position.Length;

        float deltaTime = 1 / 60f;
        Time.fixedDeltaTime = deltaTime;

        //Creating Buffers to pass data to the compute shader
        //Initializing data type then passing value to create the size of the buffer
        positionBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        predictedPositionsBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        spatialIndicesBuffer = ComputeHelper.CreateStructuredBuffer<uint3>(numParticles);
        spatialOffsetsBuffer = ComputeHelper.CreateStructuredBuffer<uint>(numParticles);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);

        compute.SetInt("numParticles", numParticles);

        //Binding the buffer to the compute shader kernels to read, have to be manually sent to the shader by dispatch  
        //positions would now be accessible by the kernel mentioned 
        ComputeHelper.SetBuffer(compute, positionBuffer, "positions", externalForcesKernel, spatialHashKernel, densityKernel , pressureKernel, viscosityKernel, updatePositionKernel);
        ComputeHelper.SetBuffer(compute, predictedPositionsBuffer, "predictedPositions", externalForcesKernel, spatialHashKernel, densityKernel , pressureKernel, viscosityKernel, updatePositionKernel);
        ComputeHelper.SetBuffer(compute, spatialIndicesBuffer, "spatialIndices", spatialHashKernel ,densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, spatialOffsetsBuffer, "spatialOffsets", spatialHashKernel , densityKernel, pressureKernel, viscosityKernel);        
        ComputeHelper.SetBuffer(compute, densityBuffer, "densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, velocityBuffer, "velocities", externalForcesKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionKernel);

        //Setting the initial position buffer and spawn positions for the particles
        setInitialPositionBuffer(spawnData);

        gpuSort = new();
        gpuSort.SetBuffers(spatialIndicesBuffer, spatialOffsetsBuffer);

        //Displaying the particles by calling the shader
        display.Init(this);

    }

    //Dispatching all the buffers to the compute shader
    void SimulationRun()
    {
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: updatePositionKernel);
    }

    //Limiting the time to only (i.e 60 FPS) if the fixedTimeStep varible is true via the inspector
    //It can be called multiple times per frame by setting time.fixedDeltaTime
    void FixedUpdate()
    {
        if (fixedTimeStep)
        {
            RunSimulationFrame(Time.fixedDeltaTime);
        }
    }

    void Update()
    {
        // Run simulation if not in fixed timestep mode
        // (skip running for first few frames as deltaTime can be disproportionaly large)
        if (!fixedTimeStep && Time.frameCount > 10)
        {
            RunSimulationFrame(Time.deltaTime);
        }

    }

    //Invoking the dispatch of the buffer and setting of the individual properties
    void RunSimulationFrame(float frameTime)
    {
            float timeStep = frameTime / iterationsPerFrame * timeScale;
            setSettings(timeStep);

            //How many times u want the simulation to run per frame
            for (int i = 0; i < iterationsPerFrame; i++)
            {
                SimulationRun();
                SimulationRunCompleted?.Invoke();
            }
    }

    //Used to pass individual values to the compute shader and values which maybe rapidly changing by inspector or logic
    void setSettings(float deltaTime)
    {
        compute.SetFloat("deltaTime", deltaTime);
        compute.SetFloat("iterationsPerFrame", iterationsPerFrame);
        compute.SetBool("fixedTimeStep", fixedTimeStep);
        compute.SetFloat("targetDensity", targetDensity);
        compute.SetFloat("pressureMultiplier", pressureMultiplier);
        compute.SetFloat("collisionDampening", collisionDampening);
        compute.SetFloat("gravity", gravity);
        compute.SetFloat("viscocityStrength", viscocityStrength);
        compute.SetFloat("nearPressureMultiplier",nearPressureMultiplier);
        compute.SetFloat("smoothingRadius", smoothingRadius);
        compute.SetVector("boundBox",boundBox);

        //Setting of Smoothing Volume properties for the Smoothing Function
        compute.SetFloat("Poly6ScalingFactor", 4 / Mathf.PI * Mathf.Pow(smoothingRadius, 8));
        compute.SetFloat("SpikyPow3ScalingFactor", 10 / Mathf.PI * Mathf.Pow(smoothingRadius, 5));
        compute.SetFloat("SpikyPow2ScalingFactor", 6 / (Mathf.PI * Mathf.Pow(smoothingRadius, 4)));
        compute.SetFloat("SpikyPow3DerivativeScalingFactor", 30 / (Mathf.Pow(smoothingRadius, 5) * Mathf.PI));
        compute.SetFloat("SpikyPow2DerivativeScalingFactor", 12 / (Mathf.Pow(smoothingRadius, 4) * Mathf.PI));
    }

    //Used for passing the initial position of the particles to the compute shader via a buffer
    void setInitialPositionBuffer(ParticleSpawner.ParticleData spawner)
    {
        float2[] allPoints = new float2[spawner.position.Length];
        System.Array.Copy(spawner.position, allPoints, spawner.position.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionsBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawner.velocity);
    }

    void OnDestroy()
    {
        ComputeHelper.Release(positionBuffer, velocityBuffer, densityBuffer, predictedPositionsBuffer, spatialIndicesBuffer, spatialOffsetsBuffer);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0f, 1f);

        Gizmos.DrawWireCube(Vector2.zero, boundBox);
    }
}