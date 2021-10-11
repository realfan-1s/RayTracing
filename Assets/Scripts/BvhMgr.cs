using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

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

public class BvhMgr
{
    private readonly Sphere[] spheres;
    public BvhNode[] bvhTrees;
    public ComputeBuffer bvhBuffer { get; private set; }
    public BvhMgr(Sphere[] spheres)
    {
        this.spheres = spheres;
        int depth = 1 + Mathf.CeilToInt((Mathf.Log(spheres.Length, 2)));
        bvhTrees = new BvhNode[(int)Mathf.Pow(2, depth)];
        bvhTrees[0] = new BvhNode();
    }
    public void CreateBoundingBox(){
        BvhNode root = new BvhNode();
        float3 minPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
        float3 maxPos = new float3(float.MinValue, float.MinValue, float.MinValue);
        uint sphereCount = (uint)spheres.Length;
        for (uint i = 0; i < sphereCount; ++i){
            minPos = math.min(minPos, spheres[i].pos - spheres[i].radius);
            maxPos = math.max(maxPos, spheres[i].pos + spheres[i].radius);
        }
        root.boundingBox = new AABB(minPos, maxPos);
        root.containSphereCount = sphereCount;
        root.spherePos = 0;
        DFS(spheres, 0, root, 1);
        bvhBuffer = new ComputeBuffer(bvhTrees.Length, 32, ComputeBufferType.Append);
        bvhBuffer.SetData<BvhNode>(new List<BvhNode>(bvhTrees));
    }
    public void ReleaseBuffer(){
        bvhBuffer.Dispose();
    }

    private void DFS(Sphere[] spheres, uint startPos, BvhNode node, int treePos){
        if (node.containSphereCount < 27){
            bvhTrees[treePos] = node;
            return;
        }
        BvhNode leftNode = new BvhNode();
        var rightNode = new BvhNode();
        uint leftLen = node.containSphereCount / 2;
        uint leftEnd = leftLen + startPos;
        float3 leftMinPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue), rightMinPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
        float3 leftMaxPos = new float3(float.MinValue, float.MinValue, float.MinValue), rightMaxPos = new float3(float.MinValue, float.MinValue, float.MinValue);
        for (uint i = startPos; i < leftEnd; ++i){
            leftMinPos = math.min(leftMinPos, spheres[i].pos - spheres[i].radius);
            leftMaxPos = math.max(leftMaxPos, spheres[i].pos + spheres[i].radius);
        }
        for (uint i = leftEnd; i < startPos + node.containSphereCount; ++i){
            rightMinPos = math.min(rightMinPos, spheres[i].pos - spheres[i].radius);
            rightMaxPos = math.max(rightMaxPos, spheres[i].pos + spheres[i].radius);
        }
        leftNode.boundingBox = new AABB(leftMinPos, leftMaxPos);
        leftNode.containSphereCount = leftLen;
        leftNode.spherePos = startPos;
        rightNode.boundingBox = new AABB(rightMinPos, rightMaxPos);
        rightNode.containSphereCount = node.containSphereCount - leftLen;
        rightNode.spherePos = leftEnd;

        bvhTrees[treePos] = node;
        DFS(spheres, startPos, leftNode, 2 * treePos);
        DFS(spheres, leftEnd, rightNode, 2 * treePos + 1);
    }

    // TODO : 表面启发式分布
    private void SAH(){

    }
}
