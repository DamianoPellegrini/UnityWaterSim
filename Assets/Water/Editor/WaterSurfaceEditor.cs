using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Water.Editor
{
    [CustomEditor(typeof(WaterSurface))]
    public class WaterSurfaceEditor : UnityEditor.Editor
    {
        private VisualTreeAsset m_VisualTreeAsset;
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();

            m_VisualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Water/Editor/WaterSurfaceEditor.uxml");

            // Instantiate UXML
            VisualElement uxmlRoot = m_VisualTreeAsset.Instantiate();
            root.Add(uxmlRoot);

            // Attach a default Inspector to the Foldout.
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            return root;
        }
    }
}
