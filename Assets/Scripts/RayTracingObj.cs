using System;
using UnityEngine;
using Unity.Mathematics;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class RayTracingObj : MonoBehaviour
{
    [SerializeField]
    private Color _albedo = new Color(0.6f, 0.6f, 0.6f);
    [Range(0, 1)]
    public float specular = 0.5f;
    [Range(0, 1)]

    public float metallic = 0.5f;
    [Range(0, 1)]
    public float roughness = 0.5f;
    [Range(0, 1)]
    public float specTrans = 0.5f;
    [Range(0, 1)]
    public float relativeIor = 0.5f;
    [SerializeField]
    private Color _emission = new Color(0.0f, 0.0f, 0.0f);
    [SerializeField]
    private Color _transColor = new Color(0.0f, 0.0f, 0.0f);

    public float3 albedo { get => new float3(_albedo.r, _albedo.g, _albedo.b); }
    public float3 emission { get => new float3(_emission.r, _emission.g, _emission.b); }
    public float3 transColor { get => new float3(_transColor.r, _transColor.g, _transColor.b); }
    private void OnEnable() {
        RTMgr.RegisterMesh(this);
    }
    private void OnDisable() {
        RTMgr.UnregisterMesh(this);
    }
}
