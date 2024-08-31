using UnityEngine;

namespace Water {

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public class TextureDebugger : MonoBehaviour {

        public enum TextureToDebug {
            Waves,
            SpectrumSamples,
            ComplexSpectrum,
            CompleteComplexSpectrum,
            EvolvedComplexSpectrum,
            Displacement,
            Derivatives,
            Turbulence
        }

        public WaterSurface surface;

        public TextureToDebug textureToDebug;

        private MeshRenderer meshRenderer;

        void Awake() {
            meshRenderer = GetComponent<MeshRenderer>();

        }

        void Update() {
            if (meshRenderer == null) return;
            if (surface == null) return;

            switch (textureToDebug) {
                case TextureToDebug.Waves:
                    meshRenderer.sharedMaterial.mainTexture = surface.cascade0.wavesTexture;
                    break;
                case TextureToDebug.SpectrumSamples:
                    meshRenderer.sharedMaterial.mainTexture = surface.cascade0.spectrumSamplesTexture;
                    break;
                case TextureToDebug.ComplexSpectrum:
                    meshRenderer.sharedMaterial.mainTexture = surface.cascade0.initialSpectrumTexture;
                    break;
                case TextureToDebug.CompleteComplexSpectrum:
                    meshRenderer.sharedMaterial.mainTexture = surface.cascade0.complexSpectrumTexture;
                    break;
                case TextureToDebug.EvolvedComplexSpectrum:
                    meshRenderer.sharedMaterial.mainTexture = surface.cascade0.DxDz;
                    break;
                case TextureToDebug.Displacement:
                    meshRenderer.sharedMaterial.mainTexture = surface.cascade0.displacement;
                    break;
                case TextureToDebug.Derivatives:
                    meshRenderer.sharedMaterial.mainTexture = surface.cascade0.derivatives;
                    break;
                case TextureToDebug.Turbulence:
                    meshRenderer.sharedMaterial.mainTexture = surface.cascade0.turbulence;
                    break;
            }
        }

    }
}
