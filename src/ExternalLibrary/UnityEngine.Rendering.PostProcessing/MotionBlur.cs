using System;

namespace UnityEngine.Rendering.PostProcessing;

[Serializable]
[PostProcess(typeof(MotionBlurRenderer), "Unity/Motion Blur", false)]
public sealed class MotionBlur : PostProcessEffectSettings
{
	[Tooltip("The angle of rotary shutter. Larger values give longer exposure.")]
	[Range(0f, 360f)]
	public FloatParameter shutterAngle = new FloatParameter
	{
		value = 270f
	};

	[Tooltip("The amount of sample points, which affects quality and performances.")]
	[Range(4f, 32f)]
	public IntParameter sampleCount = new IntParameter
	{
		value = 10
	};

	public override bool IsEnabledAndSupported(PostProcessRenderContext context)
	{
		return enabled.value && shutterAngle.value > 0f && SystemInfo.supportsMotionVectors && RenderTextureFormat.RGHalf.IsSupported() && !RuntimeUtilities.isVREnabled;
	}
}
