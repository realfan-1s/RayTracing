using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class BvhMgr
{
    private readonly Sphere[] spheres;
    private readonly List<Triangle> triangles;
    public BvhNode[] bvhTrees;
    public ComputeBuffer bvhBuffer { get; private set; }
    public BvhMgr(Sphere[] spheres)
    {
        this.spheres = spheres;
        int depth = 1 + Mathf.CeilToInt((Mathf.Log(spheres.Length, 2)));
        bvhTrees = new BvhNode[(int)Mathf.Pow(2, depth)];
        bvhTrees[0] = new BvhNode();
    }
    public BvhMgr(List<Triangle> triangles){
        this.triangles = triangles;
        int depth = 1 + Mathf.CeilToInt((Mathf.Log(spheres.Length, 2)));
        bvhTrees = new BvhNode[(int)Mathf.Pow(2, depth)];
        bvhTrees[0] = new BvhNode();
    }
    public void CreateBoundingBox(){
        BvhNode root = new BvhNode();
        float3 minPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
        float3 maxPos = new float3(float.MinValue, float.MinValue, float.MinValue);
        uint sphereCount = (uint)spheres.Length;
        int triangleCount = triangles == null ? 0 : triangles.Count;
        if (sphereCount > 0){
            for (uint i = 0; i < sphereCount; ++i){
                minPos = math.min(minPos, spheres[i].pos - spheres[i].radius);
                maxPos = math.max(maxPos, spheres[i].pos + spheres[i].radius);
            }
            root.boundingBox = new AABB(minPos, maxPos);
            root.containSphereCount = sphereCount;
            root.spherePos = 0;
            DFS(spheres, 0, root, 1);
        } else if (triangleCount > 0){
            for (int i = 0; i < triangleCount; ++i){
                minPos = math.min(minPos, math.min(triangles[i].vert1, math.min(triangles[i].vert2, triangles[i].vert3)));
                maxPos = math.max(maxPos, math.max(triangles[i].vert1, math.max(triangles[i].vert2, triangles[i].vert3)));
            }
            root.boundingBox = new AABB(minPos, maxPos);
            root.containSphereCount = (uint)triangleCount;
            root.spherePos = 0;
            DFS(spheres, 0, root, 1);
        }
        bvhBuffer = new ComputeBuffer(bvhTrees.Length, 32, ComputeBufferType.Append);
        bvhBuffer.SetData<BvhNode>(new List<BvhNode>(bvhTrees));
    }
    public void ReleaseBuffer(){
        bvhBuffer.Dispose();
    }

    // 表面启发式分布, https://medium.com/@bromanz/how-to-create-awesome-accelerators-the-surface-area-heuristic-e14b5dec6160
    private void DFS(Sphere[] spheres, uint startPos, BvhNode node, int treePos){
        if (node.containSphereCount < 27){
            bvhTrees[treePos] = node;
            return;
        }
        var leftNode = new BvhNode();
        var rightNode = new BvhNode();
        float3 leftMinPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue), rightMinPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
        float3 leftMaxPos = new float3(float.MinValue, float.MinValue, float.MinValue), rightMaxPos = new float3(float.MinValue, float.MinValue, float.MinValue);

        // float3 len = node.boundingBox.TMax - node.boundingBox.TMin;
        // if (len.x >= len.y && len.x >= len.z)
        //     Array.Sort<Sphere>(spheres, (int)startPos, (int)node.containSphereCount, new xComparer());
        // if (len.y >= len.x && len.y >= len.z)
        //     Array.Sort<Sphere>(spheres, (int)startPos, (int)node.containSphereCount, new yComparer());
        // if (len.z >= len.x && len.z >= len.y)
        //     Array.Sort<Sphere>(spheres, (int)startPos, (int)node.containSphereCount, new zComparer());

        uint nodeSize = startPos + node.containSphereCount;
        float cost = float.MaxValue;
        uint leftLen = 1;
        SortType bestSort = SortType.xSort;
        for (int axis = 0; axis < 3; ++axis){
            SortType bestSort_Temp = SortType.xSort;
            // TODO: 可能出现cost更大但已经排序了的可能
            List<Sphere> temp = new List<Sphere>();
            switch (axis){
                case 0:
                    bestSort_Temp = SortType.xSort;
                    Array.Sort<Sphere>(spheres, (int)startPos, (int)node.containSphereCount, new xComparer());
                    break;
                case 1:
                    bestSort_Temp = SortType.ySort;
                    Array.Sort<Sphere>(spheres, (int)startPos, (int)node.containSphereCount, new yComparer());
                    break;
                case 2:
                    bestSort_Temp = SortType.zSort;
                    Array.Sort<Sphere>(spheres, (int)startPos, (int)node.containSphereCount, new zComparer());
                    break;
            }
            for (uint i = startPos + 1; i < nodeSize; ++i){
                float3 leftMinPos_Temp = new float3(float.MaxValue, float.MaxValue, float.MaxValue), rightMinPos_Temp = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
                float3 leftMaxPos_Temp = new float3(float.MinValue, float.MinValue, float.MinValue), rightMaxPos_Temp = new float3(float.MinValue, float.MinValue, float.MinValue);
                for (uint leftPos = startPos; leftPos < i; ++leftPos){
                    leftMinPos_Temp = math.min(leftMinPos_Temp, spheres[leftPos].pos - spheres[leftPos].radius);
                    leftMaxPos_Temp = math.max(leftMaxPos_Temp, spheres[leftPos].pos + spheres[leftPos].radius);
                }
                for (uint rightPos = i; rightPos < nodeSize; ++rightPos){
                    rightMinPos_Temp = math.min(rightMinPos_Temp, spheres[rightPos].pos - spheres[rightPos].radius);
                    rightMaxPos_Temp = math.max(rightMaxPos_Temp, spheres[rightPos].pos + spheres[rightPos].radius);
                }
                float3 left_lwh = leftMaxPos_Temp - leftMinPos_Temp; // lwh = length、width、height
                float leftSurf = left_lwh.x * left_lwh.y + left_lwh.y * left_lwh.z + left_lwh.x * left_lwh.z;
                float3 right_lwh = rightMaxPos_Temp - rightMinPos_Temp;
                float rightSurf = right_lwh.x *  right_lwh.y + right_lwh.y * right_lwh.z + right_lwh.x * right_lwh.z;
                float totalCost = leftSurf * (i - startPos) + rightSurf * (nodeSize - i);

                if (totalCost < cost){
                    bestSort = bestSort_Temp;
                    cost = totalCost;
                    leftLen = i - startPos;
                    leftMinPos = leftMinPos_Temp;
                    leftMaxPos = leftMaxPos_Temp;
                    rightMinPos = rightMinPos_Temp;
                    rightMaxPos = rightMaxPos_Temp;
                }
            }
        }
        uint leftEnd = startPos + leftLen;
        switch (bestSort)
        {
            case SortType.xSort:
                bestSort = SortType.xSort;
                Array.Sort<Sphere>(spheres, (int)startPos, (int)node.containSphereCount, new xComparer());
                break;
            case SortType.ySort:
                bestSort = SortType.ySort;
                Array.Sort<Sphere>(spheres, (int)startPos, (int)node.containSphereCount, new yComparer());
                break;
            case SortType.zSort:
                bestSort = SortType.zSort;
                Array.Sort<Sphere>(spheres, (int)startPos, (int)node.containSphereCount, new zComparer());
                break;
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
    private void DFS(List<Triangle> triangles, int startPos, BvhNode node,int treePos){

    }
}
