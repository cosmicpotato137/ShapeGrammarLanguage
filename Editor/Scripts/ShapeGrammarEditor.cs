using System.IO;
using UnityEngine;
using UnityEditor;

// custom unity editor for ShapeGrammarDriver
namespace cosmicpotato.sgl
{
    [CustomEditor(typeof(ShapeGrammarDriver))]
    public class ShapeGrammarDriverEditor : Editor
    {
        ShapeGrammarDriver sgd;
        FileSystemWatcher watcher;
        bool changed = false;

        private void OnEnable()
        {
            sgd = target as ShapeGrammarDriver;

            watcher = new FileSystemWatcher(Application.dataPath);
            watcher.NotifyFilter = NotifyFilters.Attributes
                         | NotifyFilters.CreationTime
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.FileName
                         | NotifyFilters.LastAccess
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Security
                         | NotifyFilters.Size;

            watcher.Changed += OnChanged;

            watcher.Filter = "*.txt";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            changed = EditorGUI.EndChangeCheck() || changed;

            // UI buttons for calling driver methods
            if (GUILayout.Button("Generate Mesh"))
                sgd.GenerateMesh();
            if (GUILayout.Button("Clear Mesh"))
                sgd.ClearMesh();
            if (GUILayout.Button("Parse Grammar") || changed)
            {
                sgd.ParseGrammar();
                changed = false;
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            changed = true;
        }
    }
}