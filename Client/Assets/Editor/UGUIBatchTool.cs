using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
public class UGUIBatchTool : EditorWindow
{
    GameObject tempObj;
    EditorApplication.HierarchyWindowItemCallback hiearchyItemCallback;
    static UGUIBatchTool batchWindow = null;

    [MenuItem("TeamWork/UGUIBatchTool")]
    static void ShowEditor()
    {
        batchWindow = (UGUIBatchTool)EditorWindow.GetWindow(typeof(UGUIBatchTool), true, "UGUI Batch Tool");
        batchWindow.Show();
        batchWindow.hiearchyItemCallback = new EditorApplication.HierarchyWindowItemCallback(batchWindow.DrawHierarchyIcon);
        EditorApplication.hierarchyWindowItemOnGUI = (EditorApplication.HierarchyWindowItemCallback)Delegate.Combine(
            EditorApplication.hierarchyWindowItemOnGUI,
            batchWindow.hiearchyItemCallback);
    }

    void DrawHierarchyIcon(int instanceID, Rect selectionRect)
    {
        if (selectedGameObjectID == instanceID)
        {
            Rect rect = new Rect(selectionRect.x - 50f, selectionRect.y, 50f, 16f);
            GUI.Label(rect, "DC: "+_drawCall);
        }
        BatchStruct batch;
        if (_uiDatas != null && _uiDatas.TryGetValue(instanceID, out batch))
        {
            Rect rect = new Rect(selectionRect.x + selectionRect.width - 70f, selectionRect.y, 70f, 16f);
            GUI.Label(rect, batch.ToString());
        }
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        #region updateBatch
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("UpdateSelectedGameObject", GUILayout.ExpandWidth(true)))
        {
            GameObject[] gameObjects = Selection.gameObjects;
            if (gameObjects != null && gameObjects.Length > 0)
            {
                var selectedGameObject = gameObjects[0];
                if (selectedGameObject)
                {
                    UpdateBatch(selectedGameObject);
                    if (tempObj)
                    {
                        DestroyImmediate(tempObj);
                    }
                    tempObj = new GameObject("UGUIBatchOn");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        #endregion
        EditorGUILayout.EndVertical();
    }

    void OnDestroy()
    {
        EditorApplication.hierarchyWindowItemOnGUI = (EditorApplication.HierarchyWindowItemCallback)Delegate.Remove(
           EditorApplication.hierarchyWindowItemOnGUI,
           hiearchyItemCallback);
        if (tempObj)
        {
            DestroyImmediate(tempObj);
        }
    }

    class BatchStruct
    {
        public Graphic graphic;
        public int batchIndex;
        public int depth;
        public int textureIndex;
        public int materialIndex;
        public BatchStruct(Graphic g, int b, int d,int t, int m)
        {
            graphic = g;
            batchIndex = b;
            depth = d;
            textureIndex = t;
            materialIndex = m;
        }
        public override string ToString()
        {
            return string.Format("{0}/{1}/{2}/{3}", batchIndex, depth, textureIndex, materialIndex);
        }
    }

    Dictionary<int, BatchStruct> _uiDatas = null;
    int _drawCall = 0;
    int selectedGameObjectID = 0;

    void UpdateBatch(GameObject root)
    {
        _uiDatas = new Dictionary<int, BatchStruct>();
        if (root)
        {
            selectedGameObjectID = root.GetInstanceID();
            _drawCall = 0;
            var renderers = root.GetComponentsInChildren<CanvasRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var go = renderer.gameObject;
                var graphic = go.GetComponent<Graphic>();
                if (graphic && graphic.enabled && graphic.canvasRenderer.GetColor().a>=0.01f)
                {
                    var hitR = graphic as HitReceiver;
                    if (hitR && !hitR.m_needDisplay)
                    {
                        continue;
                    }
                    int highestIntersectingDepth = 0;
                    foreach (BatchStruct data in _uiDatas.Values)
                    {
                        var g = data.graphic;
                        if (Intersects(graphic, g))
                        {
                            if (CanBatch(graphic, g))
                            {
                                highestIntersectingDepth = Math.Max(data.depth, highestIntersectingDepth);
                            }
                            else
                            {
                                highestIntersectingDepth = Math.Max(data.depth + 1, highestIntersectingDepth);
                            }
                        }
                    }
                    _uiDatas.Add(go.GetInstanceID(), new BatchStruct(graphic, 0, highestIntersectingDepth, 0, 0));
                }
            }
            List<BatchStruct> _sortedUIDatas = new List<BatchStruct>(_uiDatas.Count);
            foreach (BatchStruct data in _uiDatas.Values)
            {
                _sortedUIDatas.Add(data);
            }
            _sortedUIDatas.Sort(new BatchComparer());
            BatchStruct cachedData = null;
            List<int> _matIDs = new List<int>();
            List<int> _texIDs = new List<int>();
            int batchID = 1;
            for (int i = 0; i < _sortedUIDatas.Count; i++)
            {
                var data = _sortedUIDatas[i];
                if (cachedData == null)
                {
                    data.batchIndex = batchID;
                }
                else if (CanBatch(data.graphic, cachedData.graphic))
                {
                    data.batchIndex = batchID;
                }
                else
                {
                    batchID++;
                    data.batchIndex = batchID;
                }
                int matID = data.graphic.GetMaterialInstanceID();
                if (_matIDs.Contains(matID))
                {
                    data.materialIndex = _matIDs.IndexOf(matID);
                }
                else
                {
                    data.materialIndex = _matIDs.Count;
                    _matIDs.Add(matID);
                }
                int texID = data.graphic.GetTextureInstanceID();
                if (_texIDs.Contains(texID))
                {
                    data.textureIndex = _texIDs.IndexOf(texID);
                }
                else
                {
                    data.textureIndex = _texIDs.Count;
                    _texIDs.Add(texID);
                }
                cachedData = data;
            }
            _drawCall = batchID;
        }
    }

    void GetTextVBO(Text t, List<UIVertex> vbo)
    {
        if (t.font == null)
            return;
        var rectTransform = t.rectTransform;
        Rect inputRect = rectTransform.rect;
        Vector2 textAnchorPivot = Text.GetTextAnchorPivot(t.alignment);
        Vector2 refPoint = Vector2.zero;
        refPoint.x = (textAnchorPivot.x == 1 ? inputRect.xMax : inputRect.xMin);
        refPoint.y = (textAnchorPivot.y == 0 ? inputRect.yMin : inputRect.yMax);
        Vector2 roundingOffset = t.PixelAdjustPoint(refPoint) - refPoint;
        IList<UIVertex> verts = t.cachedTextGenerator.verts;
        float unitsPerPixel = 1 / t.pixelsPerUnit;
        if (roundingOffset != Vector2.zero)
        {
            for (int i = 0; i < verts.Count; i++)
            {
                UIVertex uiv = verts[i];
                uiv.position *= unitsPerPixel;
                uiv.position.x += roundingOffset.x;
                uiv.position.y += roundingOffset.y;
                vbo.Add(uiv);
            }
        }
        else
        {
            for (int i = 0; i < verts.Count; i++)
            {
                UIVertex uiv = verts[i];
                uiv.position *= unitsPerPixel;
                vbo.Add(uiv);
            }
        }
    }

    void GetGraphicCorner(Graphic g, out float minX, out float maxX, out float minY, out float maxY)
    {
        Vector3[] corners = new Vector3[4];
        var trans = g.rectTransform;
        var textComponent = g as Text;
        if (textComponent)
        {
            List<UIVertex> vbo = new List<UIVertex>();
            GetTextVBO(textComponent, vbo);
            minX = vbo[0].position.x;
            maxX = vbo[0].position.x;
            minY = vbo[0].position.y;
            maxY = vbo[0].position.y;
            for (int i = 0; i < vbo.Count; i++)
            {
                var vec = vbo[i].position;
                //Debug.LogError("vbo " + i + " " + vec);
                if (vec.x < minX)
                {
                    minX = vec.x;
                }
                else if (vec.x > maxX)
                {
                    maxX = vec.x;
                }
                if (vec.y < minY)
                {
                    minY = vec.y;
                }
                else if (vec.y > maxY)
                {
                    maxY = vec.y;
                }
            }
            corners[0] = trans.localToWorldMatrix.MultiplyPoint(new Vector3(minX, minY, 0));
            corners[1] = trans.localToWorldMatrix.MultiplyPoint(new Vector3(minX, maxY, 0));
            corners[2] = trans.localToWorldMatrix.MultiplyPoint(new Vector3(maxX, maxY, 0));
            corners[3] = trans.localToWorldMatrix.MultiplyPoint(new Vector3(maxX, minY, 0));
        }
        else
        {
            trans.GetWorldCorners(corners);
        }
        minX = corners[0].x;
        maxX = corners[0].x;
        minY = corners[0].y;
        maxY = corners[0].y;
        for (int i = 0; i < 4; i++)
        {
            var vec = corners[i];
            if (vec.x < minX)
            {
                minX = vec.x;
            }
            else if (vec.x > maxX)
            {
                maxX = vec.x;
            }
            if (vec.y < minY)
            {
                minY = vec.y;
            }
            else if (vec.y > maxY)
            {
                maxY = vec.y;
            }
        }
        //if (textComponent)
        //{
        //    Debug.LogError(g.gameObject.name + "   " + minX + "  " + maxX + "  " + minY + "  " + maxY);
        //    trans.GetWorldCorners(corners);
        //    float testMinX = corners[0].x;
        //    float testMaxX = corners[0].x;
        //    float testMinY = corners[0].y;
        //    float testMaxY = corners[0].y;
        //    for (int i = 0; i < 4; i++)
        //    {
        //        var vec = corners[i];
        //        if (vec.x < testMinX)
        //        {
        //            testMinX = vec.x;
        //        }
        //        else if (vec.x > testMaxX)
        //        {
        //            testMaxX = vec.x;
        //        }
        //        if (vec.y < testMinY)
        //        {
        //            testMinY = vec.y;
        //        }
        //        else if (vec.y > testMaxY)
        //        {
        //            testMaxY = vec.y;
        //        }
        //    }
        //    Debug.LogError(testMinX + "  " + testMaxX + "  " + testMinY + "  " + testMaxY);
        //}
    }

    bool Intersects(Graphic a, Graphic b)
    {
        float aMinX = 0;
        float aMaxX = 0;
        float aMinY = 0;
        float aMaxY = 0;
        GetGraphicCorner(a, out aMinX, out aMaxX, out aMinY, out aMaxY);        
        float bMinX = 0;
        float bMaxX = 0;
        float bMinY = 0;
        float bMaxY = 0;
        GetGraphicCorner(b, out bMinX, out bMaxX, out bMinY, out bMaxY);
        if (aMinX >= bMaxX || aMaxX <= bMinX || aMinY >= bMaxY || aMaxY <= bMinY)
        {
            return false;
        }
        //Debug.LogError(a.gameObject.name + "  相交  "+b.gameObject.name);
        return true;
    }

    bool CanBatch(Graphic a, Graphic b)
    {
        //Debug.LogError("Check Batch " + a.gameObject.name + "   " + b.gameObject.name);
        //Debug.LogError(a.mainTexture.GetInstanceID());
        //Debug.LogError(b.mainTexture.GetInstanceID());
        //Debug.LogError(a.materialForRendering.GetInstanceID());
        //Debug.LogError(b.materialForRendering.GetInstanceID());
        if (a.GetTextureInstanceID() == b.GetTextureInstanceID() && a.GetMaterialInstanceID() == b.GetMaterialInstanceID())
        {
            //Debug.LogError("Can Batch " + a.gameObject.name + "   " + b.gameObject.name);
            return true;
        }
        return false;
    }

    class BatchComparer : IComparer<BatchStruct>
    {
        public int Compare(BatchStruct aData, BatchStruct bData)
        {
            if (aData.depth != bData.depth)
            {
                return aData.depth.CompareTo(bData.depth);
            }
            if (aData.graphic.GetTextureInstanceID() != bData.graphic.GetTextureInstanceID())
            {
                return aData.graphic.GetTextureInstanceID().CompareTo(bData.graphic.GetTextureInstanceID());
            }
            if (aData.graphic.GetMaterialInstanceID() != bData.graphic.GetMaterialInstanceID())
            {
                return aData.graphic.GetMaterialInstanceID().CompareTo(bData.graphic.GetMaterialInstanceID());
            }
            return aData.graphic.canvas.renderOrder.CompareTo(bData.graphic.canvas.renderOrder);
        }
    }
}

public static class EditorFunctionExtend
{
    public static int GetTextureInstanceID(this Graphic g)
    {
        return g.mainTexture.GetInstanceID();
    }

    public static int GetMaterialInstanceID(this Graphic g)
    {
        return g.materialForRendering.GetInstanceID();
    }
}