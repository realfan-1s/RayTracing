using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TAASetting {
    public struct Jitter { // 选择Halton Sequence
        [Range(0.1f, 3.0f)]
        public float spread; // 抖动的尺度
        [Range(4, 64)]
        public int sampleCount; // 采样周围像素的个数
        [Range(0, 3.0f)]
        public float sharpenAmount; // 锐化程度
    }
    public struct Blend {
        [Range(0, 1)]
        public float stationary; // 静止时的混合比例
        [Range(0, 1)]
        public float move; // 运动时的混合比例
        [Range(30.0f, 100.0f)]
        public float motionAmplification; // 运动放大量，数值越大对细微运动越敏感
    }
    public Jitter jitter;
    public Blend blend;
    public TAASetting()
    {
        jitter = new Jitter() {
            spread = 1.0f,
            sampleCount = 8,
            sharpenAmount = 0.35f
        };
        blend = new Blend() {
            stationary = 0.98f,
            move = 0.8f,
            motionAmplification = 60.0f
        };
    }
    public int sampleIndex = 0;
}
