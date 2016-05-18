using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

public class PrefabsChangeEditor : EditorWindow
{

	[MenuItem("TeamWork/PrefabsChangeEditor")]
    static void ShowEditor()
    {
        PrefabsChangeEditor window = (PrefabsChangeEditor)EditorWindow.GetWindow(typeof(PrefabsChangeEditor), true, "PrefabsChangeEditor");
        window.Show();
    }

    public string sourcePath = @"Assets\Resources\UI\Prefabs";

    void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("替换路径",GUILayout.ExpandWidth(false));
        GUILayout.Label(sourcePath, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Browse...", GUILayout.MaxWidth(100)))
        {
            string outStr = EditorUtility.OpenFolderPanel("Select input folder path", sourcePath, "");
            if (!string.IsNullOrEmpty(outStr))
            {
                sourcePath = outStr.Replace(Application.dataPath, "Assets");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("替换Tags For UIClickEffect", GUILayout.ExpandWidth(true)))
        {
            BeginReplaceTags();
        }
        if (GUILayout.Button("删除BoxCollider", GUILayout.ExpandWidth(true)))
        {
            BeginDeleteUICollider();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    void BeginReplaceTags()
    {
        string[] allFiles = Directory.GetFiles(sourcePath, "*.prefab", SearchOption.AllDirectories);
        //sourcePath = sourcePath.Replace('/', '\\');
        foreach (string sPath in allFiles)
        {
            var path = sPath.Replace('\\', '/');
            //Common.ULogFile.sharedInstance.LogError(path);
            GameObject obj = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
            if (obj != null)
            {
                GameObject instanceObj = GameObject.Instantiate(obj) as GameObject;
                string name = instanceObj.name;
                instanceObj.name = name.Remove(name.Length - 7, 7);
                ReplaceTag(instanceObj.transform);
                PrefabUtility.CreatePrefab(path, instanceObj);
                AssetDatabase.SaveAssets();
                GameObject.DestroyImmediate(instanceObj);
            }
        }
        AssetDatabase.Refresh();
    }

    void ReplaceTag(Transform trans)
    {
        if (trans != null)
        {
            var childCount = trans.childCount;
            if (childCount > 0)
            {
                for (int i = 0; i < childCount; i++)
                {
                    ReplaceTag(trans.GetChild(i));
                }
            }
            if (trans.GetComponent<Button>() != null && trans.gameObject.tag == "Untagged")
            {
                trans.gameObject.tag = "UI_ClickEffect";
            }
        }
    }

    void BeginDeleteUICollider()
    {
        string[] allFiles = Directory.GetFiles(sourcePath, "*.prefab", SearchOption.AllDirectories);
        bool isChanged = false;
        foreach (string sPath in allFiles)
        {
            var path = sPath.Replace('\\', '/');
            GameObject obj = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
            if (obj != null)
            {
                GameObject instanceObj = GameObject.Instantiate(obj) as GameObject;
                isChanged = DeleteUICollider(instanceObj.transform);
                if (isChanged)
                {
                    string name = instanceObj.name;
                    instanceObj.name = name.Remove(name.Length - 7, 7);
                    PrefabUtility.CreatePrefab(path, instanceObj);
                    AssetDatabase.SaveAssets();
                }
                GameObject.DestroyImmediate(instanceObj);
            }
        }
        AssetDatabase.Refresh();
    }

    bool DeleteUICollider(Transform trans)
    {
        bool tag = false;
        if (trans != null)
        {
            var childCount = trans.childCount;
            if (childCount > 0)
            {
                for (int i = 0; i < childCount; i++)
                {
                    if (DeleteUICollider(trans.GetChild(i)))
                    {
                        tag = true;
                    }

                }
            }
            if (trans.GetComponent<BoxCollider>() != null)
            {
                GameObject.DestroyImmediate(trans.GetComponent<BoxCollider>());
                tag = true;
            }
            else if (trans.GetComponent<BoxCollider2D>() != null)
            {
                GameObject.DestroyImmediate(trans.GetComponent<BoxCollider2D>());
                tag = true;
            }
        }
        return tag;
    }
}
