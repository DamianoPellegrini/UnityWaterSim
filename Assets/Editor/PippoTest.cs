using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PippoTest : EditorWindow
{
    [MenuItem("Window/UI Toolkit/PippoTest")]
    public static void ShowExample()
    {
        PippoTest wnd = GetWindow<PippoTest>();
        wnd.titleContent = new GUIContent("PippoTest");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        VisualElement label = new Label("Hello World! From C#");
        root.Add(label);

    }
}
