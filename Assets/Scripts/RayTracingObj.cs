using System;
using UnityEngine;
using Unity.Mathematics;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class RayTracingObj : MonoBehaviour
{
    public Color _albedo = new Color(0.6f, 0.6f, 0.6f);
    public Color _specular = new Color(0.04f, 0.04f, 0.04f);
    [Range(0, 1)]

    public float metallic = 0.5f;
    [Range(0, 1)]
    public float roughness = 0.5f;
    [Range(0, 1)]
    public float subsurface = 0.5f;
    [Range(0, 1)]
    public float smoothness = 0.95f;
    public Color _emission = new Color(1.0f, 1.0f, 1.0f);

    public float3 albedo { get => new float3(_albedo.r, _albedo.g, _albedo.b); }
    public float3 specular { get => new float3(_specular.r, _specular.g, _specular.b); }
    public float3 emission { get => new float3(_emission.r, _emission.g, _emission.b); }
    private void OnEnable() {
        RTMgr.RegisterMesh(this);
    }
    private void OnDisable() {
        RTMgr.UnregisterMesh(this);
    }
}
