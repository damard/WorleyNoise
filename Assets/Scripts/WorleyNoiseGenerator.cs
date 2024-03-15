using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using UnityEditor;
using UnityEngine.Scripting;
using UnityEngine.Experimental.Rendering;

public class WorleyNoiseGenerator : MonoBehaviour
{
    public enum WorleyType
    {
        Worley2D,
        Worley3D
    }

    public WorleyType worleyType = WorleyType.Worley2D;
    [Range(0.0f, 1.0f)] public float sliceDepth;

    [SerializeField][HideInInspector] private Material displayMaterial;
    [SerializeField][HideInInspector] private ComputeShader computeShader;
    [SerializeField] private Material[] displayMaterials;
    [SerializeField] private ComputeShader[] computeShaders;
    [SerializeField] private int textureResolution = 128;
    [SerializeField] private int cellResolution = 32;
    [SerializeField] private int axisCellCount = 4;
    [SerializeField] private int seed = 0;
    [SerializeField] private RenderTexture noiseTexture;
    [SerializeField] private MeshRenderer quadMeshRenderer;

    private ComputeBuffer _computeBuffer;
    private const int _threadGroupSize = 8;

    private void OnValidate()
    {
        if (worleyType == WorleyType.Worley3D && displayMaterial != null)
        {
            displayMaterial.SetFloat("_SliceDepth", sliceDepth);
        }
    }

    public void Generate()
    {
        switch (worleyType)
        {
            case WorleyType.Worley2D: 
                GenerateWorley2D();
                break;
            case WorleyType.Worley3D:
                GenerateWorley3D();
                break;
        }
    }

    private void GenerateWorley2D()
    {
        displayMaterial = displayMaterials[0];
        computeShader = computeShaders[0];
        quadMeshRenderer.sharedMaterial = displayMaterial;

        CreateRenderTexture(ref noiseTexture, textureResolution, "_NoiseMap", TextureDimension.Tex2D);
        
        Random.InitState(seed);

        computeShader.SetInt("_Resolution", textureResolution);
        computeShader.SetInt("_CellResolution", cellResolution);
        computeShader.SetInt("_AxisCellCount", axisCellCount);
        computeShader.SetBuffer(0, "_FeaturePoints", CreateWorley2DFeaturePointsBuffer());
        computeShader.SetTexture(0, "_Result", noiseTexture);
        
        int threadsPerGroup = Mathf.CeilToInt(textureResolution / (float) _threadGroupSize);
        computeShader.Dispatch(0, threadsPerGroup, threadsPerGroup, 1);

        quadMeshRenderer.sharedMaterial.SetTexture("_BaseMap", noiseTexture);
        
        _computeBuffer.Release();
        _computeBuffer = null;
    }

    private void GenerateWorley3D()
    {
        displayMaterial = displayMaterials[1];
        computeShader = computeShaders[1];
        quadMeshRenderer.sharedMaterial = displayMaterial;

        CreateRenderTexture(ref noiseTexture, textureResolution, "_NoiseMap", TextureDimension.Tex3D);
     
        Random.InitState(seed);

        computeShader.SetInt("_Resolution", textureResolution);
        computeShader.SetInt("_CellResolution", cellResolution);
        computeShader.SetInt("_AxisCellCount", axisCellCount);
        computeShader.SetBuffer(0, "_FeaturePoints", CreateWorley3DFeaturePointsBuffer());
        computeShader.SetTexture(0, "_Result", noiseTexture);
        
        int threadsPerGroup = Mathf.CeilToInt(textureResolution / (float) _threadGroupSize);
        computeShader.Dispatch(0, threadsPerGroup, threadsPerGroup, threadsPerGroup);

        string worleyPath = "Assets/Textures/3DWorley.asset";
        
        /*
        Texture3D worleyTex = new Texture3D(textureResolution, textureResolution, textureResolution, TextureFormat.ARGB32, false){wrapMode = TextureWrapMode.Repeat, filterMode= FilterMode.Bilinear};
        Graphics.CopyTexture(noiseTexture, worleyTex);
        worleyTex.Apply(false, true);
        AssetDatabase.CreateAsset(worleyTex, worleyPath);
        AssetDatabase.SaveAssetIfDirty(worleyTex);
        AssetDatabase.SaveAssets();*/

        SaveRT3DToTexture3DAsset(noiseTexture, worleyPath);

        quadMeshRenderer.sharedMaterial.SetTexture("_BaseMap", noiseTexture);
        _computeBuffer.Release();
        _computeBuffer = null;
    }

    void SaveRT3DToTexture3DAsset(RenderTexture rt3D, string filepath)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        var a = new NativeArray<byte>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
        AsyncGPUReadback.RequestIntoNativeArray(ref a, rt3D, 0, (_) =>
        {
            Texture3D output = new Texture3D(width, height, depth, rt3D.graphicsFormat, TextureCreationFlags.None);
            output.SetPixelData(a, 0);
            output.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            AssetDatabase.CreateAsset(output, filepath);
            AssetDatabase.SaveAssetIfDirty(output);
            a.Dispose();
            rt3D.Release();
        });
    }

    private void CreateRenderTexture(ref RenderTexture renderTexture, int resolution, string name, TextureDimension textureDimension)
    {
        if (renderTexture == null || !renderTexture.IsCreated() || renderTexture.width != resolution || renderTexture.dimension != textureDimension)
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }

            renderTexture = new RenderTexture(resolution, resolution, 0)
            {
                enableRandomWrite = true,
                dimension = textureDimension,
                volumeDepth = textureDimension == TextureDimension.Tex3D ? resolution : 0,
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                graphicsFormat = GraphicsFormat.R8_UNorm
            };

            renderTexture.Create();
        }
    }

    private ComputeBuffer CreateWorley2DFeaturePointsBuffer()
    {
        // Create one feature point per cell.
        int count = axisCellCount * axisCellCount;
        float2[] points = new float2[count];
        for (int i = 0; i < count; i++)
        {
            points[i] = new float2(Random.value, Random.value);
        }

        ComputeBuffer computeBuffer = new ComputeBuffer(points.Length, sizeof(float) * 2, ComputeBufferType.Structured);
        computeBuffer.SetData(points);
        
        _computeBuffer = computeBuffer;

        return computeBuffer;
    }
    
    private ComputeBuffer CreateWorley3DFeaturePointsBuffer()
    {
        // Create one feature point per cell.
        int count = axisCellCount * axisCellCount * axisCellCount;
        float3[] points = new float3[count];
        for (int i = 0; i < count; i++)
        {
            points[i] = new float3(Random.value, Random.value, Random.value);
        }

        ComputeBuffer computeBuffer = new ComputeBuffer(points.Length, sizeof(float) * 3, ComputeBufferType.Structured);
        computeBuffer.SetData(points);
        
        _computeBuffer = computeBuffer;

        return computeBuffer;
    }
}
