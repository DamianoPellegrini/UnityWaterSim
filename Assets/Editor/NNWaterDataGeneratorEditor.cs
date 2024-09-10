using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Water;
using Water.Spectrum;

namespace NN
{
    public class NNWaterDataGeneratorEditor : EditorWindow
    {
        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

        [MenuItem("Window/Neural Nets/Water Data Generator")]
        public static void ShowEditorMenuItemAction()
        {
            NNWaterDataGeneratorEditor wnd = GetWindow<NNWaterDataGeneratorEditor>();
            wnd.titleContent = new GUIContent("Water data generator");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement uxmlRoot = m_VisualTreeAsset.Instantiate();
            root.Add(uxmlRoot);

            // Btn callback
            var btnGenerate = root.Q<Button>("generateButton");
            if (btnGenerate != null)
            {
                btnGenerate.RegisterCallback<ClickEvent>(OnGenerateClick);
            }

            // Btn active state
            var spectrumField = rootVisualElement.Q<ObjectField>("waterSpectrumField");
            var noiseField = rootVisualElement.Q<ObjectField>("gaussianNoiseField");
            spectrumField.RegisterValueChangedCallback(DisableButtonIfNullRef);
            noiseField.RegisterValueChangedCallback(DisableButtonIfNullRef);
            DisableButtonIfNullRef(null); // Check on create

            // Total entries
            var sampleCountSlider = root.Q<SliderInt>("sampleCountSlider");
            if (sampleCountSlider != null)
            {
                sampleCountSlider.RegisterValueChangedCallback(OnSamplesChange);
            }
            var patchSizeField = root.Q<EnumField>("patchSizeField");
            if (patchSizeField != null)
            {
                patchSizeField.RegisterValueChangedCallback(OnPatchSizeChange);
            }
            RecalculateTotalEntries(sampleCountSlider.value, (int)(WaterSimulationSettings.PatchSize)patchSizeField.value); // Calculate on create
        }

        private void DisableButtonIfNullRef(ChangeEvent<UnityEngine.Object> e)
        {
            var spectrumField = rootVisualElement.Q<ObjectField>("waterSpectrumField");
            var noiseField = rootVisualElement.Q<ObjectField>("gaussianNoiseField");
            var btnGenerate = rootVisualElement.Q<Button>("generateButton");

            var oneIsNull = spectrumField.value == null || noiseField.value == null;

            btnGenerate.SetEnabled(!oneIsNull);
        }

        private void OnSamplesChange(ChangeEvent<int> e)
        {
            var patchSizeField = rootVisualElement.Q<EnumField>("patchSizeField");
            if (patchSizeField == null) return;

            RecalculateTotalEntries(e.newValue, (int)(WaterSimulationSettings.PatchSize)patchSizeField.value);
        }

        private void OnPatchSizeChange(ChangeEvent<System.Enum> e)
        {
            var sampleCountSlider = rootVisualElement.Q<SliderInt>("sampleCountSlider");
            if (sampleCountSlider == null) return;

            RecalculateTotalEntries(sampleCountSlider.value, (int)(WaterSimulationSettings.PatchSize)e.newValue);
        }

        private void RecalculateTotalEntries(int samples, int patchSize)
        {
            var entriesLabel = rootVisualElement.Q<Label>("totalEntriesLabel");
            var entriesCount = samples * patchSize * patchSize;
            var aaa = 1 == entriesCount ? "entry" : "entries";
            entriesLabel.text = $"{entriesCount} {aaa} will be generated.";
        }

        private void OnGenerateClick(ClickEvent e)
        {
            var spectrumField = rootVisualElement.Q<ObjectField>("waterSpectrumField");
            var noiseField = rootVisualElement.Q<ObjectField>("gaussianNoiseField");
            var patchField = rootVisualElement.Q<EnumField>("patchSizeField");
            var sampleCountSlider = rootVisualElement.Q<SliderInt>("sampleCountSlider");

            if (new object[] { spectrumField, noiseField, patchField, sampleCountSlider }.Any(v => v == null)) return;

            using (var gen = new NNDataGenerator((int)(WaterSimulationSettings.PatchSize)patchField.value, new Vector3(1, 0.0001f, 1000f)))
            {
                var spectrum = spectrumField.value as WaterFrequencySpectrum;
                var noise = noiseField.value as Texture;

                var path = EditorUtility.SaveFilePanel(
                    "Save generated data as CSV",
                    Application.dataPath,
                    $"{spectrum.name}.csv",
                    "csv"
                );
                if (path.Length <= 0) return;

                // TODO: Sistema stream che non va dopo il primo round

                File.CreateText(path);

                using (var fStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using (var bufStream = new BufferedStream(fStream))
                    {
                        // Write header
                        var bytes = new ASCIIEncoding().GetBytes(NNDataGenerator.CSVFormat(spectrum) + Environment.NewLine);
                        bufStream.Write(bytes, 0, bytes.Length);

                        for (int i = 0; i < sampleCountSlider.value; i++)
                        {
                            gen.Generate(bufStream, spectrum, Time.time, Time.deltaTime, noise);
                        }
                    }
                }
            }
        }
    }

}
