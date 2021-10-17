using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Linq;

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
    private ComputeBuffer sphereBuffer;
    private ComputeBuffer triangleBuffer;
    #endregion

    #region
    [Header("Ray Tracing Parameters")]
    [SerializeField]
    private Texture skybox;
    private Material antiAliasing = null;
    private uint currentSample = 0;
    [Range(1, 10)]
    public uint reflectTimes = 8;
    public bool whiteStyleRayTracing = true;
    public bool UseOutsideModel = false;
    private static List<RayTracingObj> rayTracingList = new List<RayTracingObj>();
    private static List<MeshObject> meshList = new List<MeshObject>();
    private static List<Vector3> verticesList = new List<Vector3>();
    private static List<int> indicesList = new List<int>();
    private static List<Triangle> triangleList = new List<Triangle>();
    private static bool needRebuildComputeBuffer = false;
    #endregion

    #region ("Sphere Parameters")
    [Header("Sphere Parameters")]
    public float2 SphereRadius = new float2(3.0f, 8.0f);
    public float SpherePlacementRadius = 1000.0f;
    #endregion
    public Light directionalLight;
    [Range(0.3f, 1)]
    public float relativeIor;
    [Range(0, 1)]
    public float specular;
    public Color transColor;
    private BvhMgr bvhMgr;
    [Range(1, 1500)]
    public uint sphereCount = 500;
    public int sphereSeed = 1223832719;

    private void Awake() {
        kernelRayTracing = rayTracingCS.FindKernel("RayTracing");
        sphereBuffer = new ComputeBuffer((int)sphereCount, 72, ComputeBufferType.Append);
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
        BuildmeshComputeBuffer();
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
        sphereBuffer.Dispose();
        if (UseOutsideModel){
            verticesBuffer.Dispose();
            indicesBuffer.Dispose();
            meshObjectBuffer.Dispose();
        } else {
            bvhMgr.ReleaseBuffer();
        }
    }

    // private void OnDrawGizmos() {
    //     if (bvhMgr == null)
    //         return;

    //     Gizmos.color = Color.red;
    //     AABB box1 = bvhMgr.bvhTrees[2].boundingBox;
    //     float3 center = (box1.TMax + box1.TMin) / 2;
    //     float3 size = box1.TMax - box1.TMin;
    //     Gizmos.DrawWireCube(center, size);

    //     Gizmos.color = Color.blue;
    //     AABB box2 = bvhMgr.bvhTrees[4].boundingBox;
    //     float3 center2 = (box2.TMax + box2.TMin) / 2;
    //     float3 size2 = box2.TMax - box2.TMin;
    //     Gizmos.DrawWireCube(center2, size2);

    //     Gizmos.color = Color.yellow;
    //     AABB box3 = bvhMgr.bvhTrees[5].boundingBox;
    //     float3 center3 = (box3.TMax + box3.TMin) / 2;
    //     float3 size3 = box3.TMax - box3.TMin;
    //     Gizmos.DrawWireCube(center3, size3);
    // }
    void InitParams(){
        rayTracingCS.SetMatrix("CameraToWorld", Camera.main.cameraToWorldMatrix);
        rayTracingCS.SetMatrix("CameraInverseProjection", Camera.main.projectionMatrix.inverse);
        rayTracingCS.SetTexture(kernelRayTracing, "Skybox", skybox);
        rayTracingCS.SetBuffer(kernelRayTracing, "sphereBuffer", sphereBuffer);
        if (!UseOutsideModel)
            rayTracingCS.SetBuffer(kernelRayTracing, "bvhBuffer", bvhMgr.bvhBuffer);
        rayTracingCS.SetInt("ReflectTimes", (int)reflectTimes);
        rayTracingCS.SetFloats("Offset", new float[2] {UnityEngine.Random.value, UnityEngine.Random.value});
        rayTracingCS.SetFloat("randSeed", UnityEngine.Random.value);
        float3 lightDir = -1 * directionalLight.transform.forward;
        rayTracingCS.SetVector("directionalLight", new float4(lightDir.x, lightDir.y, lightDir.z, directionalLight.intensity));
        rayTracingCS.SetVector("lightColor", new float4(directionalLight.color.r, directionalLight.color.g, directionalLight.color.b, 1.0f));
#if UNITY_2020_1_OR_NEWER
        if (whiteStyleRayTracing)
            rayTracingCS.EnableKeyword("_WHITE_STYLE");
        else
            rayTracingCS.DisableKeyword("_WHITE_STYLE");
        if (UseOutsideModel)
            rayTracingCS.EnableKeyword("_USE_OUTSIDE_MODEL");
        else
            rayTracingCS.DisableKeyword("_USE_OUTSIDE_MODEL");
#endif
    }
    void InitRenderTexture(){
        if (targetRT == null || targetRT.width != Screen.width || targetRT.height != Screen.height){
            if (targetRT != null)
                RenderTexture.ReleaseTemporary(targetRT);
            targetRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            targetRT.enableRandomWrite = true;
            targetRT.Create();
        }
        if (convergeRT == null || convergeRT.width != Screen.width || convergeRT.height != Screen.height){
            if (convergeRT != null)
                RenderTexture.ReleaseTemporary(convergeRT);
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
        if (UseOutsideModel){
            return;
        }

        // Sphere[] data = new Sphere[55];
        // for (int i = 0; i < 55; i++)
        // {
        //     Sphere sphere = new Sphere();
        //     sphere.radius = 10;
        //     float intervalX = 3 * sphere.radius;
        //     float intervalZ = 4 * sphere.radius;
        //     float startX = -4.5f * intervalX;
        //     float startZ = 2 * intervalZ;

        //     int row = i / 11;
        //     int col = i % 11;
        //     sphere.pos = new float3(startX + col * intervalX, startZ - row * intervalZ,  0);
        //     sphere.emission = float3.zero;
        //     sphere.relativeIor = relativeIor;

        //     switch (row)
        //     {
        //         case 0: // metallic row
        //             sphere.albedo = new float3(1, 0.71f, 0);
        //             sphere.metallic = col * 0.1f;
        //             sphere.transColor = sphere.albedo;
        //             sphere.roughness = 0.1f;
        //             sphere.specular = 0.5f;
        //             sphere.specTrans = 0.0f;
        //             break;
        //         case 1: // specular row
        //             sphere.albedo = new float3(1, 0, 0);
        //             sphere.metallic = 0;
        //             sphere.transColor = sphere.albedo;
        //             sphere.roughness = 0.1f;
        //             sphere.specular = col * 0.1f;
        //             sphere.specTrans = 0.0f;

        //             break;
        //         case 2: // roughness row
        //             sphere.albedo = new float3(0, 0.8f, 0.2f);
        //             sphere.metallic = 0;
        //             sphere.transColor = sphere.albedo;
        //             sphere.roughness = col* 0.1f;
        //             sphere.specular = 0.5f;
        //             sphere.specTrans = 0.0f;
        //             break;
        //         case 3: // specTrans row
        //             sphere.albedo = new float3(0.5f, 0.2f, 1);
        //             sphere.metallic = 0;
        //             sphere.transColor = sphere.albedo;
        //             sphere.roughness = 0.1f;
        //             sphere.specular = 0.5f;
        //             sphere.specTrans = col * 0.1f;
        //             break;
        //         case 4: // roughness with specTrans = 1
        //             sphere.albedo = new float3(0.03f, 0.03f, 0.03f);
        //             sphere.metallic = 0;
        //             sphere.transColor = new float3(1, 1, 1);
        //             sphere.roughness = col * 0.1f;
        //             sphere.specular = 0.5f;
        //             sphere.specTrans = 1f;
        //             break;
        //     }

        //     data[i] = sphere;
        // }

        UnityEngine.Random.InitState(sphereSeed);
        Sphere[] data = new Sphere[sphereCount];
        for (int i = 0; i < sphereCount; ++i){
            Sphere sphere = new Sphere();
        SkipSphere:
            sphere.radius = SphereRadius.x + UnityEngine.Random.value * (SphereRadius.y - SphereRadius.x);
            float2 randomPos = UnityEngine.Random.insideUnitCircle * SpherePlacementRadius;
            sphere.pos = new float3(randomPos.x, sphere.radius, randomPos.y);
            foreach (Sphere other in data)
            {
                float minDist = sphere.radius + other.radius;
                if (math.lengthsq(sphere.pos - other.pos) < minDist * minDist){
                    goto SkipSphere;
                }
            }
            float chance = UnityEngine.Random.value;
            Color color = UnityEngine.Random.ColorHSV();
            sphere.albedo = new float3(color.r, color.g, color.b);
            sphere.specular = specular;
            sphere.metallic = math.cos(Mathf.PI * (UnityEngine.Random.value - 0.5f));
            sphere.roughness = math.sin(Mathf.PI * UnityEngine.Random.value);
            sphere.relativeIor = relativeIor;
            sphere.transColor = new float3(transColor.r, transColor.g, transColor.b);
            sphere.specTrans = Mathf.Abs(math.sin((chance + 1) * Mathf.PI));
            sphere.emission = float3.zero;

            data[i] = sphere;
        }

        bvhMgr = new BvhMgr(data);
        bvhMgr.CreateBoundingBox();
        sphereBuffer.SetData<Sphere>(new List<Sphere>(data));
    }

    void BuildmeshComputeBuffer(){
        if (!UseOutsideModel || !needRebuildComputeBuffer)
            return;

        needRebuildComputeBuffer = false;
        currentSample = 0;
        meshList.Clear();
        verticesList.Clear();
        indicesList.Clear();
        triangleList.Clear();

        int nums = rayTracingList.Count;
        for (int i = 0; i < nums; ++i){
            Mesh mesh = rayTracingList[i].GetComponent<MeshFilter>().sharedMesh;

            int firstVertex = verticesList.Count;
            verticesList.AddRange(mesh.vertices);
            int firstIndex = indicesList.Count;
            var indices = mesh.GetIndices(0);
            indicesList.AddRange(indices.Select(index => index + firstVertex));

            meshList.Add(new MeshObject(){
                localToWorldMatrix = rayTracingList[i].transform.localToWorldMatrix,
                indiceOffset = firstIndex,
                indiceCount = indices.Length,
                albedo = rayTracingList[i].albedo,
                specular = rayTracingList[i].specular,
                metallic = rayTracingList[i].metallic,
                roughness = rayTracingList[i].roughness,
                specTrans = rayTracingList[i].specTrans,
                relativeIor = rayTracingList[i].relativeIor,
                transColor =  rayTracingList[i].transColor,
                emission = rayTracingList[i].emission,
            });
        }

        GenerateComputeBuffer(ref meshObjectBuffer, meshList, 128);
        GenerateComputeBuffer(ref verticesBuffer, verticesList, 12);
        GenerateComputeBuffer(ref indicesBuffer, indicesList, 4);
        GenerateComputeBuffer(ref triangleBuffer, triangleList, 48);

        rayTracingCS.SetBuffer(kernelRayTracing, "meshObjectBuffer", meshObjectBuffer);
        rayTracingCS.SetBuffer(kernelRayTracing, "verticesBuffer", verticesBuffer);
        rayTracingCS.SetBuffer(kernelRayTracing, "indicesBuffer", indicesBuffer);
    }
    public static void RegisterMesh(RayTracingObj obj){
        rayTracingList.Add(obj);
        needRebuildComputeBuffer = true;
    }
    public static void UnregisterMesh(RayTracingObj obj){
        rayTracingList.Remove(obj);
        needRebuildComputeBuffer = true;
    }
    private static void GenerateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride) where T : struct{
        if (buffer != null)
        {
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }
        if (data.Count != 0)
        {
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }
            buffer.SetData<T>(data);
        }
    }
    private List<Triangle> TransformTriangles(GameObject mesh){
        return null;
    }
}
