using Unity.Mathematics;

public struct Sphere
{
    public float3 pos;
    public float radius;
    public float3 albedo;
    public float specular;
    public float metallic;
    public float roughness;
    public float specTrans;
    public float relativeIor;
    public float3 transColor;
    public float3 emission;
}

public struct Triangle{
    public float3 vert1;
    public float3 vert2;
    public float3 vert3;
    public float3 normal;
}

public struct MeshObject
{
    public float4x4 localToWorldMatrix;
    public int indiceOffset;
    public int indiceCount;
    public float3 albedo;
    public float specular;
    public float metallic;
    public float roughness;
    public float specTrans;
    public float relativeIor;
    public float3 transColor;
    public float3 emission;
}

public struct AABB{
    public float3 TMin;
    public float3 TMax;
    public AABB(float3 T_Min, float3 T_Max)
    {
        TMin = T_Min;
        TMax = T_Max;
    }
}

public struct BvhNode{
    public AABB boundingBox;
    public uint containSphereCount;
    public uint spherePos;
}
