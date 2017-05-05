using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Sprites;
using System.Collections.Generic;
using System.Collections;
//using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

public class AssetSerializationEditor : EditorWindow
{
    #region members
    static AssetSerializationEditor _window = null;
    string _folderPath = Application.dataPath + "/GameAssets";
    string _filePath = Application.dataPath + "/GameAssets";
    int _toolBarIndex = 0;
    string[] _toolBarStrs =
    {
        "All These Types",
        ".anim",
        ".prefab",
        ".unity",
        //".mat",
        //".controller",
        //".asset",
    };
    #endregion
    #region initialize
    [MenuItem("TeamWork/AssetSerializationEditor")]
    static void Init()
    {
        _window = ScriptableObject.CreateInstance<AssetSerializationEditor>();
        _window.title = "资源序列化工具";
        _window.minSize = new Vector2(500, 300);
        _window.Show();
    }
    #endregion

    #region display
    void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        GUI.color = Color.white;
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("选择需要优化的文件类型: " + _toolBarStrs[_toolBarIndex]);
        _toolBarIndex = GUILayout.Toolbar(_toolBarIndex, _toolBarStrs);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("选择文件夹: " + _folderPath);
        if (GUILayout.Button("Browse...", GUILayout.MaxWidth(100)))
        {
            string outStr = EditorUtility.OpenFolderPanel("Select Folder Path", _folderPath, "");
            if (!string.IsNullOrEmpty(outStr))
            {
                _folderPath = outStr;
            }
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Modify all selected files in this folder"))
        {
            BatchFileProcess();
        }
        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("选择文件: " + _filePath);
        if (GUILayout.Button("Browse...", GUILayout.MaxWidth(100)))
        {
            string outStr;
            if (_toolBarIndex == 0)
            {
                outStr = EditorUtility.OpenFilePanel("Select File Path", _filePath, "");
            }
            else
            {
                outStr = EditorUtility.OpenFilePanel("Select File Path", _filePath, _toolBarStrs[_toolBarIndex].Substring(1));
            }
            if (!string.IsNullOrEmpty(outStr))
            {
                _filePath = outStr;
            }
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Modify this file"))
        {
            ModifyFile(_filePath);
            AssetDatabase.Refresh();
        }
        EditorGUILayout.EndVertical();
    }
    #endregion

    #region base function
    string OptimizeValueTpye(string str)
    {
        try
        {
            double num = double.Parse(str);
            num *= 1000;
            num += 0.5;
            num = Math.Floor(num) * 0.001;
            string result = num .ToString();
            if (num < 1 && num > -1)
            {
                result = result.Replace("0.", ".");
            }
            return result;
        }
        catch(Exception e)
        {
            var strException = e.ToString() + "  str: " + str;
            ShowNotification(new GUIContent(strException));
            Debug.LogError(strException);
            return "0";
        }
    }
    #endregion

    #region File Process
    string animPatterns = @": ([-.0-9e]+)";
    void ModifyFile(string path, bool showNotification = true)
    {
        bool isTypeMatch = false;
        if (_toolBarIndex == 0)
        {
            for (int i = 0; i < _toolBarStrs.Length; i++)
            {
                if (path.EndsWith(_toolBarStrs[i]))
                {
                    isTypeMatch = true;
                    break;
                }
            }
        }
        else
        {
            if (path.EndsWith(_toolBarStrs[_toolBarIndex]))
            {
                isTypeMatch = true;
            }
        }
        if (isTypeMatch && File.Exists(path))
        {
            var reg = new Regex(animPatterns);
            StringBuilder sb = new StringBuilder();
            FileStream fs = File.Open(path, FileMode.Open);
            StreamReader sr = new StreamReader(fs);
            bool checkTextMode = false;
            try
            {
                while (true)
                {
                    string str = sr.ReadLine();
                    if (!checkTextMode)
                    {
                        if (str.StartsWith("%YAML"))
                        {
                            checkTextMode = true;
                        }
                        else
                        {
                            throw new Exception("File: " + path + " is serialized as Binary mode!!!");
                        }
                    }
#if SERIALIAZE_TEST
                str = "        value: {x: .0719698966, y: .0011599079, z: .0411114171, w: .996558487}";
#endif
                    if (str != null)
                    {
                        if (str != string.Empty)
                        {
                            var matches = reg.Matches(str);
                            if (matches.Count > 0)
                            {
                                int indexValue = 0;
#if SERIALIAZE_TEST
                            Debug.LogError("test string: "+str);
#endif
                                foreach (Match match in matches)
                                {
                                    int matchIndex = match.Index;
                                    string matchValue = match.Value;
                                    int indexOfDot = matchValue.IndexOf('.');
                                    int indexOfE = matchValue.IndexOf('e');
                                    if ((indexOfDot < 0 && indexOfDot < 0) || (indexOfE > 0 && indexOfDot < 0))
                                    {
                                        continue;
                                    }
                                    var groups = match.Groups;
                                    string newValue = OptimizeValueTpye(groups[1].Value);
#if SERIALIAZE_TEST
                                Debug.LogError(match.Index);
                                Debug.LogError(matchValue);
                                Debug.LogError(newValue);
#endif
                                    if (matchIndex > indexValue)
                                    {
                                        sb.Append(str.Substring(indexValue, matchIndex - indexValue));
                                    }
                                    sb.Append(": ");
                                    sb.Append(newValue);
                                    indexValue = matchIndex + matchValue.Length;

                                }
                                sb.Append(str.Substring(indexValue));
                                sb.Append("\n");
#if SERIALIAZE_TEST
                            Debug.LogError(sb.ToString());
                            break;
#endif
                            }
                            else
                            {
                                sb.Append(str);
                                sb.Append('\n');
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                sr.Close();
                fs.Close();
                fs = File.Open(path, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs);
                sw.Write(sb.ToString());
                sw.Close();
                fs.Close();
                Debug.Log(path + "  Successed!!!");
            }
            catch (Exception e)
            {
                var strException = e.ToString();
                ShowNotification(new GUIContent(strException));
                Debug.LogError(strException);
            }
            finally
            {
                sr.Close();
                fs.Close();
            }
        }
        else
        {
            if (showNotification)
            {
                var strException = path + " is not exits!!";
                ShowNotification(new GUIContent(strException));
                Debug.LogError(strException);
            }
        }
    }
    #endregion

    #region Batch File Process
    void BatchFileProcess()
    {
        string[] allFiles = null;
        if (_toolBarIndex == 0)
        {
            var files = new List<string[]>();
            int fileCount = 0;
            for(int i = 1; i < _toolBarStrs.Length; i++)
            {
                var tempFiles = Directory.GetFiles(_folderPath, string.Concat('*', _toolBarStrs[i]), SearchOption.AllDirectories);
                if(tempFiles!=null && tempFiles.Length > 0)
                {
                    files.Add(tempFiles);
                    fileCount += tempFiles.Length;
                }
            }
            allFiles = new string[fileCount];
            int index = 0;
            for(int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                file.CopyTo(allFiles, index);
                index += file.Length;
            }
        }
        else
        {
            allFiles = Directory.GetFiles(_folderPath, string.Concat('*',_toolBarStrs[_toolBarIndex]), SearchOption.AllDirectories);
        }
        foreach (string sPath in allFiles)
        {
            ModifyFile(sPath, false);
        }
        AssetDatabase.Refresh();
        Debug.Log("批量序列化文件处理完成!!!");
    }
    #endregion
}
