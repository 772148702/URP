﻿using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack {

	enum Pass {
		BloomAdd,
		BloomHorizontal,
		BloomPrefilter,
		BloomPrefilterFireflies,
		BloomScatter,
		BloomScatterFinal,
		BloomVertical,
		Copy,
		ToneMappingACES,
		ToneMappingNeutral,
		ToneMappingReinhard
	}

	const string bufferName = "Post FX";

	const int maxBloomPyramidLevels = 16;

	int
		bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
		bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
		bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
		bloomResultId = Shader.PropertyToID("_BloomResult"),
		bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
		fxSourceId = Shader.PropertyToID("_PostFXSource"),
		fxSource2Id = Shader.PropertyToID("_PostFXSource2");

	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};

	ScriptableRenderContext context;

	Camera camera;

	PostFXSettings settings;

	int bloomPyramidId;

	bool useHDR;

	public bool IsActive => settings != null;

	public PostFXStack () {
		bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
		for (int i = 1; i < maxBloomPyramidLevels * 2; i++) {
			Shader.PropertyToID("_BloomPyramid" + i);
		}
	}

	public void Setup (
		ScriptableRenderContext context, Camera camera, PostFXSettings settings,
		bool useHDR
	) {
		this.useHDR = useHDR;
		this.context = context;
		this.camera = camera;
		this.settings =
			camera.cameraType <= CameraType.SceneView ? settings : null;
		ApplySceneViewState();
	}

	public void Render (int sourceId) {
		if (DoBloom(sourceId)) {
			DoToneMapping(bloomResultId);
			buffer.ReleaseTemporaryRT(bloomResultId);
		}
		else {
			DoToneMapping(sourceId);
		}
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	bool DoBloom (int sourceId) {
		PostFXSettings.BloomSettings bloom = settings.Bloom;
		int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
		
		if (
			bloom.maxIterations == 0 || bloom.intensity <= 0f ||
			height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2
		) {
			return false;
		}

		buffer.BeginSample("Bloom");
		Vector4 threshold;
		threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
		threshold.y = threshold.x * bloom.thresholdKnee;
		threshold.z = 2f * threshold.y;
		threshold.w = 0.25f / (threshold.y + 0.00001f);
		threshold.y -= threshold.x;
		buffer.SetGlobalVector(bloomThresholdId, threshold);

		RenderTextureFormat format = useHDR ?
			RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
		buffer.GetTemporaryRT(
			bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format
		);
		Draw(
			sourceId, bloomPrefilterId, bloom.fadeFireflies ?
				Pass.BloomPrefilterFireflies : Pass.BloomPrefilter
		);
		width /= 2;
		height /= 2;

		int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
		int i;
		for (i = 0; i < bloom.maxIterations; i++) {
			if (height < bloom.downscaleLimit || width < bloom.downscaleLimit) {
				break;
			}
			int midId = toId - 1;
			buffer.GetTemporaryRT(
				midId, width, height, 0, FilterMode.Bilinear, format
			);
			buffer.GetTemporaryRT(
				toId, width, height, 0, FilterMode.Bilinear, format
			);
			Draw(fromId, midId, Pass.BloomHorizontal);
			Draw(midId, toId, Pass.BloomVertical);
			fromId = toId;
			toId += 2;
			width /= 2;
			height /= 2;
		}

		buffer.ReleaseTemporaryRT(bloomPrefilterId);
		buffer.SetGlobalFloat(
			bloomBicubicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f
		);

		Pass combinePass, finalPass;
		float finalIntensity;
		if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive) {
			combinePass = finalPass = Pass.BloomAdd;
			buffer.SetGlobalFloat(bloomIntensityId, 1f);
			finalIntensity = bloom.intensity;
		}
		else {
			combinePass = Pass.BloomScatter;
			finalPass = Pass.BloomScatterFinal;
			buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
			finalIntensity = Mathf.Min(bloom.intensity, 1f);
		}

		if (i > 1) {
			buffer.ReleaseTemporaryRT(fromId - 1);
			toId -= 5;
			for (i -= 1; i > 0; i--) {
				buffer.SetGlobalTexture(fxSource2Id, toId + 1);
				Draw(fromId, toId, combinePass);
				buffer.ReleaseTemporaryRT(fromId);
				buffer.ReleaseTemporaryRT(toId + 1);
				fromId = toId;
				toId -= 2;
			}
		}
		else {
			buffer.ReleaseTemporaryRT(bloomPyramidId);
		}
		buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		buffer.GetTemporaryRT(
			bloomResultId, camera.pixelWidth, camera.pixelHeight, 0,
			FilterMode.Bilinear, format
		);
		Draw(fromId, bloomResultId, finalPass);
		buffer.ReleaseTemporaryRT(fromId);
		buffer.EndSample("Bloom");
		return true;
	}

	void DoToneMapping(int sourceId) {
		PostFXSettings.ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
		Pass pass = mode < 0 ? Pass.Copy : Pass.ToneMappingACES + (int)mode;
		Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
	}

	void Draw (
		RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass
	) {
		buffer.SetGlobalTexture(fxSourceId, from);
		buffer.SetRenderTarget(
			to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		buffer.DrawProcedural(
			Matrix4x4.identity, settings.Material, (int)pass,
			MeshTopology.Triangles, 3
		);
	}
}