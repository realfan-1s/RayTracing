using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

public static class TemporalAA
{
    public static TAASetting taaSetting = new TAASetting();
    private static Camera renderCamera = Camera.main;
    private static RenderTexture _historyBuffer;
    private static RenderTexture _velocityBuffer;
    public static float4x4 _prevProjection;
    public static float4x4 _nonJitterProjection;
    // #region ("Shader部分")
    // private static Shader _antiAliasingShader;
    // public static Shader antiAliasingShader {
    //     get {
    //         if (_antiAliasingShader == null) {
    //             _antiAliasingShader = Shader.Find("Custom/AntiAliasing");
    //         }
    //         return _antiAliasingShader;
    //     }
    // }
    // private static Material _antiAliasingMat;
    // public static Material antiAliasingMat {
    //     get {
    //         if (_antiAliasingMat == null){
    //             if (antiAliasingShader == null || !antiAliasingShader.isSupported)
    //                 return null;
    //             _antiAliasingMat = new Material(antiAliasingShader);
    //         }
    //         return _antiAliasingMat;
    //     }
    // }
    // #endregion
    public static RenderTexture historyBuffer {
        get {
            if (_historyBuffer == null || !_historyBuffer.IsCreated()) {
                GenerateTexture(ref _historyBuffer);
            }
            return _historyBuffer;
        }
    }
    public static RenderTexture velocityBuffer {
        get {
            if (_velocityBuffer == null || !_velocityBuffer.IsCreated()) {
                GenerateTexture(ref _velocityBuffer);
            }
            return _velocityBuffer;
        }
    }
    private static void GenerateTexture(ref RenderTexture buffer) {
        buffer = new RenderTexture(renderCamera.pixelWidth, renderCamera.pixelHeight, 0);
        buffer.dimension = TextureDimension.Tex2D;
        buffer.format = RenderTextureFormat.ARGBFloat;
        buffer.Create();
    }
    // public static void Render() {

    // }
    public static void PostRender() {
        renderCamera.ResetProjectionMatrix();
        _prevProjection = renderCamera.nonJitteredProjectionMatrix;
    }
    public static Vector2 OnPreCull() {
        Vector2 jitter = GenerateRandomOffset();
        jitter *= taaSetting.jitter.spread;
        renderCamera.nonJitteredProjectionMatrix = renderCamera.projectionMatrix;
        _nonJitterProjection = renderCamera.nonJitteredProjectionMatrix;
        renderCamera.projectionMatrix = GetPerspectiveMatrix(jitter);
        jitter.x /= renderCamera.pixelWidth;
        jitter.y /= renderCamera.pixelHeight;
        return jitter;
    }

    private static float2 GenerateRandomOffset() {
        float2 offset;
        offset = new float2(GetHaltonValue(taaSetting.sampleIndex & 1023, 2), GetHaltonValue(taaSetting.sampleIndex & 1023, 3));

        if (++taaSetting.sampleIndex >= taaSetting.jitter.sampleCount) {
            taaSetting.sampleIndex = 0;
        }
        return offset;
    }
    private static float GetHaltonValue(int index, int radix) {
        float result = 0.0f;
        float digit = 1.0f / radix;
        while (index > 0) {
            result += (float)(index % radix) * digit;
            digit /= (float)radix;
            index /= radix;
        }
        return result;
    }

    // TODO: Why? 和unreal 实现不同
    // http://graphics.cs.williams.edu/papers/MotionBlurI3D12/McGuire12Blur.pdf
    // http://www.youtube.com/watch?v=WzpLWzGvFK4&t=18m
    private static float4x4 GetPerspectiveMatrix(float2 offset) {
        float vertical = math.tan(0.5f * math.radians(renderCamera.fieldOfView));
        float horizontal = vertical * renderCamera.aspect;
        offset.x *= horizontal / (0.5f * renderCamera.pixelWidth);
        offset.y *= vertical / (0.5f * renderCamera.pixelHeight);
        float left = offset.x - horizontal;
        float right = offset.x + horizontal;
        float top = offset.y + vertical;
        float bottom = offset.y - vertical;

        float4x4 matrix = new float4x4();
        matrix[0][0] = 2.0f / (right - left);
        matrix[0][1] = 0.0f;
        matrix[0][2] = (right + left) / (right - left);
        matrix[0][3] = 0.0f;
        matrix[1][0] = 0.0f;
        matrix[1][1] = 2.0f / (top - bottom);
        matrix[1][2] = (top + bottom) / (top - bottom);
        matrix[1][3] = 0.0f;
        matrix[2][0] = 0.0f;
        matrix[2][1] = 0.0f;
        matrix[2][2] = -(renderCamera.farClipPlane + renderCamera.nearClipPlane) / (renderCamera.farClipPlane - renderCamera.nearClipPlane);
        matrix[2][3] = -(2.0f * renderCamera.farClipPlane * renderCamera.nearClipPlane) / (renderCamera.farClipPlane - renderCamera.nearClipPlane);
        matrix[3][0] = 0.0f;
        matrix[3][1] = 0.0f;
        matrix[3][2] = -1.0f;
        matrix[3][3] = 0.0f;
        return matrix;
    }
}
