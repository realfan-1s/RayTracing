using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public struct Sphere
{
    public float3 pos;
    public float radius;
    public float3 albedo;
    public float3 specular;
    public float smoothness;
    public float3 emission;
}

public struct MeshObject
{
    public float4x4 localToWorldMatrix;
    public int indiceOffset;
    public int indiceCount;
    public float3 albedo;
    public float3 specular;
    public float smoothness;
    public float3 emission;

}

public class RTMgr : MonoBehaviour
{
    public ComputeShader rayTracingCS;
    // Unity 不会给予HDR 纹理作为 OnRenderImage 的目标。因此需要一个缓冲区累积结果
    private RenderTexture convergeRT;
    private RenderTexture targetRT;
    #region ("Compute Shader Parametres")
#if UNITY_2020_1_OR_NEWER
    private int kernelRayTracing;
#endif
    [Header("Mesh Parameters")]
    private ComputeBuffer verticesBuffer;
    private ComputeBuffer indicesBuffer;
    private ComputeBuffer meshObjectBuffer;
    private ComputeBuffer SphereBuffer;
    #endregion

    #region
    [Header("Ray Tracing Parameters")]
    [SerializeField]
    private Texture skybox;
    private Material antiAliasing = null;
    private uint currentSample = 0;
    [Range(1, 8192)]
    public uint reflectTimes = 8;
    public bool WhiteStyleRayTracing = true;
    private static List<RayTracingObj> meshList = new List<RayTracingObj>();
    #endregion

    #region ("Sphere Parameters")
    [Header("Sphere Parameters")]
    public float2 SphereRadius = new float2(3.0f, 8.0f);
    public float SpherePlacementRadius = 100.0f;
    [Range(1, 100)]
    public uint sphereCount = 4;
    public int sphereSeed = 1223832719;
    #endregion
    public Light directionalLight;
    private void Awake() {
        kernelRayTracing = rayTracingCS.FindKernel("RayTracing");
        SphereBuffer = new ComputeBuffer((int)sphereCount, 56, ComputeBufferType.Append);
    }

    private void Start() {
        SetSphere();
    }
    /// <summary>
    /// OnRenderImage is called after all rendering is complete to render image.
    /// </summary>
    /// <param name="src">The source RenderTexture.</param>
    /// <param name="dest">The destination RenderTexture.</param>
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        InitParams();
        Render(dest);
    }

    private void Update() {
        if (transform.hasChanged){
            currentSample = 0;
            transform.hasChanged = false;
        }
    }

    private void OnDestroy() {
        SphereBuffer.Dispose();
        verticesBuffer.Dispose();
        indicesBuffer.Dispose();
        meshObjectBuffer.Dispose();
    }

    void InitParams(){
        rayTracingCS.SetMatrix("CameraToWorld", Camera.main.cameraToWorldMatrix);
        rayTracingCS.SetMatrix("CameraInverseProjection", Camera.main.projectionMatrix.inverse);
        rayTracingCS.SetTexture(kernelRayTracing, "Skybox", skybox);
        rayTracingCS.SetBuffer(kernelRayTracing, "SphereBuffer", SphereBuffer);
        rayTracingCS.SetInt("ReflectTimes", (int)reflectTimes);
        rayTracingCS.SetFloats("Offset", new float[2] {UnityEngine.Random.value, UnityEngine.Random.value});
        rayTracingCS.SetFloat("randSeed", UnityEngine.Random.value);
        float3 lightDir = directionalLight.transform.forward;
        rayTracingCS.SetVector("directionalLight", new float4(lightDir.x, lightDir.y, lightDir.z, directionalLight.intensity));
#if UNITY_2020_1_OR_NEWER
        if (WhiteStyleRayTracing)
            rayTracingCS.EnableKeyword("_WHITE_STYLE");
        else
            rayTracingCS.DisableKeyword("_WHITE_STYLE");
#endif
    }
    void InitRenderTexture(){
        if (targetRT == null || targetRT.width != Screen.width || targetRT.height != Screen.height){
            if (targetRT != null)
            {
                RenderTexture.ReleaseTemporary(targetRT);
                RenderTexture.ReleaseTemporary(convergeRT);
            }
            targetRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            targetRT.enableRandomWrite = true;
            targetRT.Create();
            convergeRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            convergeRT.enableRandomWrite = true;
            convergeRT.Create();
        }
    }
    void SetKernelGroup(out int groupX, out int groupY){
        uint threadX, threadY, threadZ;
        rayTracingCS.GetKernelThreadGroupSizes(kernelRayTracing, out threadX, out threadY, out threadZ);

        groupX = Mathf.CeilToInt(Screen.width / threadX);
        groupY = Mathf.CeilToInt(Screen.height / threadY);
    }
    void Render(RenderTexture dest){
        InitRenderTexture();
        rayTracingCS.SetTexture(kernelRayTracing, "Result", targetRT);
        int groupX, groupY;
        SetKernelGroup(out groupX, out groupY);
        rayTracingCS.Dispatch(kernelRayTracing, groupX, groupY, 1);
        if (antiAliasing == null)
            antiAliasing = new Material(Shader.Find("Custom/AntiAliasing"));
        antiAliasing.SetFloat("_Sample", currentSample);
        Graphics.Blit(targetRT, convergeRT, antiAliasing);
        Graphics.Blit(convergeRT, dest);
        currentSample++;
    }
    void SetSphere(){
        UnityEngine.Random.InitState(sphereSeed);
        Sphere[] data = new Sphere[sphereCount];
        for (int i = 0; i < sphereCount; ++i){
            Sphere sphere = new Sphere();
            // Radius and radius
            sphere.radius = SphereRadius.x + UnityEngine.Random.value * (SphereRadius.y - SphereRadius.x);
            float2 randomPos = UnityEngine.Random.insideUnitCircle * SpherePlacementRadius;
            sphere.pos = new float3(randomPos.x, sphere.radius, randomPos.y);
            // Reject spheres that are intersecting others
            foreach (Sphere other in data)
            {
                float minDist = sphere.radius + other.radius;
                if (math.lengthsq(sphere.pos - other.pos) < minDist * minDist)
                    goto SkipSphere;
            }
            // Albedo and specular color
            float chance = UnityEngine.Random.value;
            if (chance < 0.8f){
                Color color = UnityEngine.Random.ColorHSV();
                sphere.albedo = chance < 0.4f ? float3.zero : new float3(color.r, color.g, color.b);
                sphere.specular = chance < 0.4f ? new float3(color.r, color.g, color.b) : new float3(0.04f, 0.04f, 0.04f);
                sphere.smoothness = UnityEngine.Random.value;
                sphere.emission = float3.zero;
            } else {
                sphere.albedo = float3.zero;
                sphere.specular = float3.zero;
                sphere.smoothness = 0.0f;
                Color emission = UnityEngine.Random.ColorHSV();
                sphere.emission = new float3(emission.r, emission.g, emission.b);
            }
            data[i] = sphere;

            SkipSphere:
                continue;
        }
        SphereBuffer.SetData(data);
    }
    public static void RegisterMesh(RayTracingObj obj){
        meshList.Add(obj);
    }
    public static void UnregisterMesh(RayTracingObj obj){
        meshList.Remove(obj);
    }

}
