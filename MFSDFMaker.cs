using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Moonflow
{
    public class MFSDFMaker
    {
        private MFSDFPoint[,] _grids1;
        private MFSDFPoint[,] _grids2;
        private int _h;

        private Color[] _oriColor;
        private string _path = "";
        private float[,] _result;

        private int _w;
        public Texture2D tex;

        private void InitGrid()
        {
            if (tex == null)
            {
                Debug.LogError("没有贴图用于生成");
                return;
            }

            _w = tex.width;
            _h = tex.height;
            _grids1 = new MFSDFPoint[_w, _h];
            _grids2 = new MFSDFPoint[_w, _h];
            for (var i = 0; i < _w; i++)
            for (var j = 0; j < _h; j++)
            {
                var c = tex.GetPixel(i, j);
                if (c.r > 0.5f)
                {
                    _grids1[i, j] = new MFSDFPoint(0);
                    _grids2[i, j] = new MFSDFPoint(9999);
                }
                else
                {
                    _grids1[i, j] = new MFSDFPoint(9999);
                    _grids2[i, j] = new MFSDFPoint(0);
                }
            }

            GetPath(tex);
        }

        public void GetColor(int size)
        {
            _oriColor = tex.GetPixels(0, 0, size, size);
        }

        private void InitGrid(int s)
        {
            _w = s;
            _h = s;
            _grids1 = new MFSDFPoint[s, s];
            _grids2 = new MFSDFPoint[s, s];
            for (var i = 0; i < _w; i++)
            for (var j = 0; j < _h; j++)
                if (_oriColor[_w * i + j].r > 0.5f)
                {
                    _grids1[j, i] = new MFSDFPoint(0);
                    _grids2[j, i] = new MFSDFPoint(9999);
                }
                else
                {
                    _grids1[j, i] = new MFSDFPoint(9999);
                    _grids2[j, i] = new MFSDFPoint(0);
                }

            // GetPath(tex);
        }

        private void GetPath(Texture2D t)
        {
            _path = AssetDatabase.GetAssetPath(t);
            _path = Path.GetDirectoryName(_path);
        }

        // public void Process()
        // {
        //     InitGrid();
        //     if (_grids1 == null)
        //     {
        //         Debug.LogError("未能正确生成阵列");
        //         return;
        //     }
        //
        //     GenerateSDF(_grids1);
        //     GenerateSDF(_grids2);
        // }

        public void Process(int s)
        {
            InitGrid(s);
            if (_grids1 == null)
            {
                Debug.LogError("未能正确生成阵列");
                return;
            }

            var t1 = new Thread(() => { GenerateSDF(_grids1); });
            var t2 = new Thread(() => { GenerateSDF(_grids2); });
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
        }

        public float[,] LimitSDF()
        {
            _result = new float[_w, _h];
            for (var i = 0; i < _w; i++)
            for (var j = 0; j < _w; j++)
            {
                var dist1 = (int)Mathf.Sqrt(_grids1[i, j].dist);
                var dist2 = (int)Mathf.Sqrt(_grids2[i, j].dist);
                float dist = dist1 - dist2;
                _result[i, j] = -dist;
            }

            return _result;
        }
        // public void MakeSDFTex()
        // {
        //     Texture2D sdftex = new Texture2D(_w, _h);
        //     Color c = new Color(0,0,0,1);
        //     for (int i = 0; i < _w; i++)
        //     {
        //         for (int j = 0; j < _h; j++)
        //         {
        //             float dist1 = Mathf.Sqrt(_grids1[i, j].dist);
        //             float dist2 = Mathf.Sqrt(_grids2[i, j].dist);
        //             float dist = (dist1 - dist2)/500.0f;
        //             c.r = dist;
        //             sdftex.SetPixel(i, j, c);
        //         }
        //     }
        //     sdftex.Apply();
        //     SaveTex(sdftex);
        // }

        // private void SaveTex(Texture2D sdftex, string suffix = "_sdf")
        // {
        //     byte[] bytes = sdftex.EncodeToTGA();
        //     string filePath = tex.name + suffix+".TGA";
        //     string fileFullPath = _path + "/" + filePath;
        //     File.WriteAllBytes(fileFullPath, bytes);
        //     string assetPath = "Assets" + filePath;
        //     AssetDatabase.SaveAssets();
        // }

        private void GenerateSDF(MFSDFPoint[,] grid)
        {
            Debug.Log("Start Generate");
            for (var y = 0; y < _h; y++)
            {
                for (var x = 0; x < _w; x++)
                {
                    var p = grid[x, y];
                    Compare(ref grid, ref p, x, y, -1, 0);
                    Compare(ref grid, ref p, x, y, 0, -1);
                    Compare(ref grid, ref p, x, y, -1, -1);
                    Compare(ref grid, ref p, x, y, 1, -1);
                    grid[x, y] = p;
                }

                for (var x = _w - 1; x >= 0; x--)
                {
                    var p = grid[x, y];
                    Compare(ref grid, ref p, x, y, 1, 0);
                    grid[x, y] = p;
                }
            }

            for (var y = _h - 1; y >= 0; y--)
            {
                for (var x = _w - 1; x >= 0; x--)
                {
                    var p = grid[x, y];
                    Compare(ref grid, ref p, x, y, 1, 0);
                    Compare(ref grid, ref p, x, y, 0, 1);
                    Compare(ref grid, ref p, x, y, -1, 1);
                    Compare(ref grid, ref p, x, y, 1, 1);
                    grid[x, y] = p;
                }

                for (var x = 0; x < _w; x++)
                {
                    var p = grid[x, y];
                    Compare(ref grid, ref p, x, y, -1, 0);
                    grid[x, y] = p;
                }
            }
        }

        private MFSDFPoint GetPointData(ref MFSDFPoint[,] grid, int x, int y)
        {
            if (x < 0 || x > _w - 1 || y < 0 || y > _h - 1)
                return new MFSDFPoint
                {
                    x = 9999,
                    y = 9999
                };
            return grid[x, y];
        }

        private void Compare(ref MFSDFPoint[,] grid, ref MFSDFPoint point, int x, int y, int ox, int oy)
        {
            var other = GetPointData(ref grid, x + ox, y + oy);
            other.x += ox;
            other.y += oy;
            if (other.dist < point.dist)
            {
                point.x = other.x;
                point.y = other.y;
            }
        }
    }
}