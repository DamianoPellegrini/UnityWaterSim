using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Water;
using Water.Spectrum;

namespace Editor
{
    public class NNWaterDataGeneratorWindow : EditorWindow
    {
        [SerializeField] public WaterFrequencySpectrum spectrum;
        public Texture2D gaussianNoise;
        public WaterSimulationSettings.PatchSize size;
        public string savePath;

        private SpectrumCascade nnCascade;

        [MenuItem("Window/Neural Nets/Water Data Generator")]
        public static void ShowEditorMenuItemAction()
        {
            var wnd = GetWindow<NNWaterDataGeneratorWindow>();
            wnd.titleContent = new GUIContent("Water data generator");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            var sizeSelect = new DropdownField() {
                label = "Patch size",
                choices = Enum.GetNames(typeof(WaterSimulationSettings.PatchSize)).ToList()
            };
            sizeSelect.name = nameof(sizeSelect);
            root.Add(sizeSelect);

            // VisualElements objects can contain other VisualElement following a tree hierarchy
            var label = new Label("Hello World!");
            root.Add(label);

            // Create button
            var button = new Button
            {
                name = "button",
                text = "Button"
            };
            root.Add(button);

            // Create toggle
            var toggle = new Toggle
            {
                name = "toggle",
                label = "Toggle"
            };
            root.Add(toggle);
        }
    }

    public class MyCustomEditor : EditorWindow
    {
        [SerializeField] private int m_SelectedIndex = -1;
        private VisualElement m_RightPane;

        [MenuItem("Window/UI Toolkit/MyCustomEditor")]
        public static void ShowMyEditor()
        {
            // This method is called when the user selects the menu item in the Editor.
            var wnd = GetWindow<MyCustomEditor>();
            wnd.titleContent = new GUIContent("My Custom Editor");

            // Limit size of the window.
            wnd.minSize = new Vector2(450, 200);
            wnd.maxSize = new Vector2(1920, 720);
        }

        public void CreateGUI()
        {
            // Get a list of all sprites in the project.
            var allObjectGuids = AssetDatabase.FindAssets("t:Sprite");
            var allObjects = new List<Sprite>();
            foreach (var guid in allObjectGuids)
            {
                allObjects.Add(AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            // Create a two-pane view with the left pane being fixed.
            var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);

            // Add the panel to the visual tree by adding it as a child to the root element.
            rootVisualElement.Add(splitView);

            // A TwoPaneSplitView always needs two child elements.
            var leftPane = new ListView();
            splitView.Add(leftPane);
            m_RightPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            splitView.Add(m_RightPane);

            // Initialize the list view with all sprites' names.
            leftPane.makeItem = () => new Label();
            leftPane.bindItem = (item, index) => { (item as Label).text = allObjects[index].name; };
            leftPane.itemsSource = allObjects;

            // React to the user's selection.
            leftPane.selectionChanged += OnSpriteSelectionChange;

            // Restore the selection index from before the hot reload.
            leftPane.selectedIndex = m_SelectedIndex;

            // Store the selection index when the selection changes.
            leftPane.selectionChanged += (items) => { m_SelectedIndex = leftPane.selectedIndex; };
        }

        private void OnSpriteSelectionChange(IEnumerable<object> selectedItems)
        {
            // Clear all previous content from the pane.
            m_RightPane.Clear();

            var enumerator = selectedItems.GetEnumerator();
            if (enumerator.MoveNext())
            {
                var selectedSprite = enumerator.Current as Sprite;
                if (selectedSprite != null)
                {
                    // Add a new Image control and display the sprite.
                    var spriteImage = new Image
                    {
                        scaleMode = ScaleMode.ScaleToFit,
                        sprite = selectedSprite
                    };

                    // Add the Image control to the right-hand pane.
                    m_RightPane.Add(spriteImage);
                }
            }
        }
    }
}
