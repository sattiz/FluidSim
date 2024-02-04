using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Random = UnityEngine.Random;

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 44)]
public struct Particle
{
    public float pressure;
    public float density;
    public Vector3 currForce;
    public Vector3 velocity;
    public Vector3 position;
}

public class SPH : MonoBehaviour
{
    [Header("General")] 
    public bool showSpheres = true;
    public Transform collisionSphere;
    public Vector3Int numToSpawn = new Vector3Int(10, 10, 10);

    private int totalParticles
    {
        get { return numToSpawn.x * numToSpawn.y * numToSpawn.z; }
    }

    public Vector3 boxSize = new Vector3(4, 10, 3);
    public Vector3 spawnCenter;
    public float particleRadius = 0.1f;
    public float spawnJitter = 0.2f;

    [Header("Particle Rendering")]
    public Mesh particleMesh;
    public float particleRenderSize = 8f;
    public Material material;

    [Header("Compute")] 
    public ComputeShader shader;
    public Particle[] particles;

    [Header("Fluid Constants")]
    public float boundDamping;
    public float viscosity;
    public float particleMass;
    public float gasConstant;
    public float timestep;
    public float gravity_y = 9.8f;

    // private variables
    private ComputeBuffer _argsBuffer;
    public ComputeBuffer _particlesBuffer;

    private ComputeBuffer _particleIndices;
    private ComputeBuffer _particleCellIndices;
    private ComputeBuffer _cellOffsets;
    
    private static int SizeProperty = Shader.PropertyToID("_size");
    private static int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");
    
    private int IntegrateKernel;
    private int ComputeForceKernel;
    private int DensityPressureKernel;
    private int HashParticlesKernel;
    private int BitonicSortKernel;
    private int CalculateCellOffsetsKernel;
    
    private int thread_number = 256;

    private void Awake()
    {
        SpawnParticlesInBox();

        uint[] args =
        {
            particleMesh.GetIndexCount(0),
            (uint)totalParticles,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };

        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        // Setup Particles Buffer
        _particlesBuffer = new ComputeBuffer(totalParticles, 44);
        _particlesBuffer.SetData(particles);

        _particleIndices = new ComputeBuffer(totalParticles, 4);
        _particleCellIndices = new ComputeBuffer(totalParticles, 4);
        _cellOffsets = new ComputeBuffer(totalParticles, 4);

        int[] particleIndices = new int[totalParticles];

        for (int i = 0; i < totalParticles; i++) particleIndices[i] = i;

        _particleIndices.SetData(particleIndices);

        SetupComputeBuffers();
    }

    private void Update()
    {
        Vector3.Dot(new Vector3(1, 1, 1), new Vector3(1, 1, 1));
        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty, _particlesBuffer);
        
        if (showSpheres)
            Graphics.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                material,
                new Bounds(Vector3.zero, boxSize*2),
                _argsBuffer,
                castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
            );
    }

    private void FixedUpdate()
    {
        UpdateBuffers();
        
        shader.Dispatch(HashParticlesKernel, 
            totalParticles/thread_number, 1, 1);
        SortParticles();
        shader.Dispatch(CalculateCellOffsetsKernel, 
            totalParticles/thread_number, 1, 1);
        
        shader.Dispatch(DensityPressureKernel, totalParticles/thread_number, 1,1 );
        shader.Dispatch(ComputeForceKernel, totalParticles/thread_number, 1, 1);
        shader.Dispatch(IntegrateKernel, totalParticles / thread_number, 1, 1);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);

        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnCenter, 0.1f);
        }
    }

    private void SpawnParticlesInBox()
    {
        Vector3 spawnPoint = spawnCenter;
        List<Particle> _particles = new List<Particle>();

        for (int x = 0; x < numToSpawn.x; x++)
        for (int y = 0; y < numToSpawn.y; y++)
        for (int z = 0; z < numToSpawn.z; z++)
        {
            Vector3 spawnPos = spawnPoint + new Vector3(x * particleRadius * 2,
                y * particleRadius * 2,
                z * particleRadius * 2);
            spawnPos += Random.onUnitSphere * particleRadius * spawnJitter;

            Particle p = new Particle
            {
                position = spawnPos
            };

            _particles.Add(p);
        }

        particles = _particles.ToArray();
    }

    private void SetupComputeBuffers()
    {
        IntegrateKernel = shader.FindKernel("Integrate");
        ComputeForceKernel = shader.FindKernel("ComputeForces");
        DensityPressureKernel = shader.FindKernel("ComputeDencityPressure");
        HashParticlesKernel = shader.FindKernel("HashParticles");
        BitonicSortKernel = shader.FindKernel("BitonicSort");
        CalculateCellOffsetsKernel = shader.FindKernel("CalculateCellOffsets");

        shader.SetInt("particlesLength", totalParticles);

        shader.SetFloat("gravity_y", gravity_y);
        shader.SetFloat("particleMass", particleMass);
        shader.SetFloat("viscosity", viscosity);
        shader.SetFloat("gasConstant", gasConstant);
        shader.SetFloat("boundDamping", boundDamping);
        shader.SetFloat("pi", Mathf.PI);

        shader.SetFloat("radius", particleRadius);
        shader.SetFloat("radius2", Mathf.Pow(particleRadius, 2f));
        shader.SetFloat("radius3", Mathf.Pow(particleRadius, 3f));
        shader.SetFloat("radius4", Mathf.Pow(particleRadius, 4f));
        shader.SetFloat("radius5", Mathf.Pow(particleRadius, 5f));

        shader.SetBuffer(IntegrateKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(ComputeForceKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(DensityPressureKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(HashParticlesKernel, "_particles", _particlesBuffer);
        
        shader.SetBuffer(ComputeForceKernel, "_particleIndices", _particleIndices);
        shader.SetBuffer(DensityPressureKernel, "_particleIndices", _particleIndices);
        shader.SetBuffer(HashParticlesKernel, "_particleIndices", _particleIndices);
        shader.SetBuffer(BitonicSortKernel, "_particleIndices", _particleIndices);
        shader.SetBuffer(CalculateCellOffsetsKernel, "_particleIndices", _particleIndices);
        
        shader.SetBuffer(ComputeForceKernel, "_particleCellIndices", _particleCellIndices);
        shader.SetBuffer(DensityPressureKernel, "_particleCellIndices", _particleCellIndices);
        shader.SetBuffer(HashParticlesKernel, "_particleCellIndices", _particleCellIndices);
        shader.SetBuffer(BitonicSortKernel, "_particleCellIndices", _particleCellIndices);
        shader.SetBuffer(CalculateCellOffsetsKernel, "_particleCellIndices", _particleCellIndices);
        
        shader.SetBuffer(ComputeForceKernel, "_cellOffsets", _cellOffsets);
        shader.SetBuffer(DensityPressureKernel, "_cellOffsets", _cellOffsets);
        shader.SetBuffer(HashParticlesKernel, "_cellOffsets", _cellOffsets);
        shader.SetBuffer(CalculateCellOffsetsKernel, "_cellOffsets", _cellOffsets);
    }

    private void UpdateBuffers()
    {
        shader.SetFloat("particleMass", particleMass);
        shader.SetFloat("viscosity", viscosity);
        shader.SetFloat("gasConstant", gasConstant);
        shader.SetFloat("boundDamping", boundDamping);
        
        shader.SetVector("boxSize", boxSize);
        shader.SetFloat("timestep", timestep);
        shader.SetVector("spherePos", collisionSphere.transform.position);
        shader.SetFloat("sphereRadius", collisionSphere.transform.localScale.x/2);
    }
    

    private void SortParticles()
    {
        for (var dim = 2; dim <= totalParticles; dim <<= 1)
        {
            shader.SetInt("dim", dim);
            for (var block = dim >> 1; block > 0; block >>= 1)
            {
                shader.SetInt("block", block);
                shader.Dispatch(BitonicSortKernel, 
                    totalParticles/thread_number, 1, 1);
            }
        }
    }
}