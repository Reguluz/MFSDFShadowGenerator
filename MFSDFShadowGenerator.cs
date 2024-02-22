using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Moonflow
{
    public class MFSDFShadowGenerator : EditorWindow
    {
        private static MFSDFShadowGenerator _ins;

        public int level;
        public List<MixTex> manualTexList;
        public List<MixTex> autoTexList;

        private Object _folder;
        private Material _mixMat;

        private ReorderableList _orderList;
        private int _size = 128;
        private int tab;

        private void OnEnable()
        {
            manualTexList = new List<MixTex>();
            autoTexList = new List<MixTex>();
            _orderList = new ReorderableList(manualTexList, typeof(MixTex), true, false, true, true);
            _orderList.elementHeight = 60;
            _orderList.drawElementCallback += DrawElement;
            _orderList.onAddCallback += AddItem;
            _orderList.onRemoveCallback += RemoveItem;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.fieldWidth = 20;
                EditorGUILayout.LabelField("尺寸级别");
                level = EditorGUILayout.IntSlider(level, 0, 4);
            }

            _size = (int)Mathf.Pow(2.0f, 7.0f + level);
            EditorGUILayout.LabelField("当前尺寸: ", _size.ToString());
            tab = GUILayout.Toolbar(tab, new[] { "自动模式", "手动模式" });
            switch (tab)
            {
                case 0:
                {
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        if (GUILayout.Button("获取(选中路径)")) ReadSourceFolder(false);

                        if (GUILayout.Button("获取(选中路径)并按文件名赋值")) ReadSourceFolder(true);
                    }

                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        for (var i = 0; i < autoTexList.Count; i++) DrawElementAuto(i);
                    }
                }
                    break;
                case 1:
                {
                    _orderList.DoLayoutList();
                }
                    break;
            }

            var current = tab == 0 ? autoTexList : manualTexList;
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                if (GUILayout.Button("计算（保存在首图路径）"))
                {
                    if (!PreCheck(current))
                        return;
                    Reimport(current);
                    StraightMix(current);
                }

                if (GUILayout.Button("计算（指定路径）"))
                {
                    if (!PreCheck(current))
                        return;
                    Reimport(current);
                    var defaultPath = AssetDatabase.GetAssetPath(current[0].OriginTex);
                    var path = EditorUtility.SaveFilePanel("保存到", Path.GetDirectoryName(defaultPath),
                        "NewSDFMix", "tga");
                    StraightMix(current, path);
                }
            }

            CheckSize(current);
        }

        [MenuItem("Moonflow/Tools/Art/SDFShadowGenerator")]
        public static void ShowWindow()
        {
            _ins = GetWindow<MFSDFShadowGenerator>();
            _ins.minSize = new Vector2(300, 400);
            _ins.Show();
        }

        private void DrawElementAuto(int index)
        {
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                EditorGUI.BeginChangeCheck();
                autoTexList[index].OriginTex =
                    EditorGUILayout.ObjectField(autoTexList[index].OriginTex, typeof(Texture2D), false) as Texture2D;

                if (index > 0)
                    autoTexList[index].MixPoint =
                        Mathf.Max(autoTexList[index].MixPoint, autoTexList[index - 1].MixPoint);
                autoTexList[index].MixPoint =
                    EditorGUILayout.Slider(autoTexList[index].MixPoint, 0, 180);
                if (EditorGUI.EndChangeCheck())
                    if (autoTexList[index].OriginTex != null)
                        EditorUtility.SetDirty(autoTexList[index].OriginTex);
            }
        }

        private void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            manualTexList[index].OriginTex = EditorGUI.ObjectField(new Rect(rect.x, rect.y, 50, 50),
                manualTexList[index].OriginTex, typeof(Texture2D), false) as Texture2D;
            if (index > 0)
                manualTexList[index].MixPoint =
                    Mathf.Max(manualTexList[index].MixPoint, manualTexList[index - 1].MixPoint);

            manualTexList[index].MixPoint =
                EditorGUI.Slider(new Rect(rect.x + 60, rect.y, 200, 30), manualTexList[index].MixPoint, 0, 180);
            if (EditorGUI.EndChangeCheck())
                if (manualTexList[index].OriginTex != null)
                    EditorUtility.SetDirty(manualTexList[index].OriginTex);
        }

        private void AddItem(ReorderableList list)
        {
            manualTexList.Add(new MixTex(null, _size, _size));
        }

        private void RemoveItem(ReorderableList list)
        {
            manualTexList.RemoveAt(list.index);
        }

        private void ReadSourceFolder(bool angleByName)
        {
            var path = "";
            var obj = Selection.activeObject;
            if (obj == null) return;
            path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
            autoTexList = new List<MixTex>();
            var pathFolder = path;
            var filter = "t:texture2D";
            var assetPaths = AssetDatabase.FindAssets(filter, new[] { pathFolder });
            var length = assetPaths.Length;
            if (length < 2)
            {
                Debug.LogError("没有足够贴图用于混合");
                return;
            }

            for (var i = 0; i < length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(assetPaths[i]),
                    typeof(Texture2D)) as Texture2D;
                if (asset.width != _size || asset.height != _size)
                {
                    Debug.LogError($"{asset.name} 尺寸与设置不符");
                    continue;
                }

                var data = new MixTex(asset, _size, _size);
                if (angleByName)
                    data.MixPoint = Convert.ToInt32(asset.name);
                else
                    data.MixPoint = i * 180.0f / (length - 1);
                autoTexList.Add(data);
            }
        }

        private bool PreCheck(List<MixTex> list)
        {
            if (list.Count < 2)
            {
                Debug.LogError("没有足够图片用于混合");
                return false;
            }

            if (list.Any(t => t == null))
            {
                Debug.LogError("有图片为空");
                return false;
            }

            return true;
        }

        private void CheckSize(List<MixTex> list)
        {
            if (list == null || list.Count <= 0) return;
            foreach (var t in list
                         .Where(t => t.OriginTex != null)
                         .Where(t => _size != t.OriginTex.width || _size != t.OriginTex.height))
            {
                Debug.LogError($"贴图{t.OriginTex}大小不匹配");
                t.OriginTex = null;
                return;
            }
        }

        private void Reimport(List<MixTex> list)
        {
            var dirty = false;
            for (var i = 0; i < list.Count; i++) dirty = Reimport(list[i].OriginTex, dirty);
            if (dirty) AssetDatabase.StopAssetEditing();
        }

        private bool Reimport(Texture2D tex, bool dirty)
        {
            if (!AssetDatabase.Contains(tex)) return false;
            var path = AssetDatabase.GetAssetPath(tex);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!ti) return false;
            if (!SetTextureImporter(ti)) return false;
            if (!dirty) AssetDatabase.StartAssetEditing();
            ti.SaveAndReimport();
            return true;
        }

        private bool SetTextureImporter(TextureImporter ti)
        {
            var dirty = false;
            if (!ti.crunchedCompression)
            {
                ti.crunchedCompression = false;
                dirty = true;
            }

            if (!ti.isReadable)
            {
                ti.isReadable = true;
                dirty = true;
            }

            if (ti.mipmapEnabled)
            {
                ti.mipmapEnabled = false;
                dirty = true;
            }

            if (ti.sRGBTexture)
            {
                ti.sRGBTexture = false;
                dirty = true;
            }

            if (ti.textureCompression != TextureImporterCompression.Uncompressed)
            {
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            return dirty;
        }

        private void StraightMix(List<MixTex> list, string path = "")
        {
            var tGroupPt1 = new Thread[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                var it = i;
                list[it].maker.tex = list[it].OriginTex;
                list[it].maker.GetColor(_size);
                Debug.Log($"get color index{it.ToString()}");
                tGroupPt1[it] = new Thread(() =>
                {
                    list[it].maker.Process(_size);
                    Debug.Log($"End Process {it.ToString()}");
                });
            }

            var tGroupPt2 = new Thread[list.Count];
            for (var i2 = 0; i2 < list.Count; i2++)
            {
                var it2 = i2;
                tGroupPt2[it2] = new Thread(() =>
                {
                    tGroupPt1[it2].Join();
                    list[it2].SDFData = list[it2].maker.LimitSDF();
                    Debug.Log($"End Limit {it2.ToString()}");
                });
            }

            for (var i3 = 0; i3 < list.Count; i3++)
            {
                tGroupPt1[i3].Start();
                tGroupPt2[i3].Start();
            }

            for (var i4 = 0; i4 < list.Count; i4++) tGroupPt2[i4].Join();
            Debug.Log("Start Mix");
            var sdfTex = new Texture2D(_size, _size);
            for (var i = 0; i < _size; i++)
            for (var j = 0; j < _size; j++)
            {
                var last = list[0].MixPoint == 0 ? list[0].SDFData[i, j] : 0;
                float r = -999;
                for (var k = 1; k < list.Count; k++)
                    if (list[k].SDFData[i, j] < 0 && last < 0)
                    {
                        r = 0;
                        last = list[k].SDFData[i, j];
                    }
                    else if (list[k].SDFData[i, j] > 0 && last > 0)
                    {
                        r = 1;
                        last = list[k].SDFData[i, j];
                    }
                    else
                    {
                        r = 1 - (
                            Mathf.Abs(last) / (Mathf.Abs(last) + Mathf.Abs(list[k].SDFData[i, j])) *
                            (list[k].MixPoint - list[k - 1].MixPoint)
                            + list[k - 1].MixPoint) / 180;
                        break;
                    }

                sdfTex.SetPixel(i, j, new Color(r, r, r, 1));
            }

            sdfTex.Apply();
            sdfTex.name = "faceMix";
            Debug.Log("Start Save Mix");
            var bytes = sdfTex.EncodeToTGA();
            var filePath = sdfTex.name + ".TGA";
            var fileFullPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(list[0].OriginTex)) + "/" + filePath;
            File.WriteAllBytes(string.IsNullOrEmpty(path) ? fileFullPath : path, bytes);
            Debug.Log("result: " + fileFullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }


    public class MixTex
    {
        public MFSDFMaker maker;
        [Range(0, 180)] public float MixPoint;
        public Texture2D OriginTex;
        public float[,] SDFData;

        public MixTex(Texture2D o, int w, int h)
        {
            OriginTex = o;
            maker = new MFSDFMaker();
            maker.tex = OriginTex;
            SDFData = new float[w, h];
            MixPoint = 0;
        }

        public void EmptyTex(bool isBlack, int w, int h)
        {
            for (var index0 = 0; index0 < SDFData.GetLength(0); index0++)
            for (var index1 = 0; index1 < SDFData.GetLength(1); index1++)
                SDFData[index0, index1] = isBlack ? 0 : 1;
        }
    }
}