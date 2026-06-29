using Runtime.Gameplay.Entities;
using UnityEditor;
using UnityEngine;

// [CustomEditor(typeof(Unit))]
public class TestComponentEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        Debug.Log("OnInspectorGUI forced");
        DrawDefaultInspector();
    }
}