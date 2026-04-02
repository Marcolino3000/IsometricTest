using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    public class DebugViewWindow : EditorWindow
    {
        private VisualElement root;
        private List<PropertyField> propertyFields = new();
        private Button resetButton;

        [MenuItem("Tools/Debug View")]
        public static void ShowExample()
        {
            DebugViewWindow wnd = GetWindow<DebugViewWindow>();
            wnd.titleContent = new GUIContent("DebugWindow");
        }

        public void AddElement(PropertyField element)
        {
            propertyFields.Add(element);
            // Repaint();
            CreateGUI();
        }

        public void CreateGUI()
        {
            root = rootVisualElement;
            root.Clear();
            
            resetButton = new Button
            {
                text = "Reset"
            };
            resetButton.clicked += () =>
            {
                propertyFields.Clear();
                CreateGUI();
            };
            
            root.Add(resetButton);
            
            foreach (var propertyField in propertyFields)
            {
                root.Add(propertyField);
            }
        }
    }
}