﻿using UnityEngine;
using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using System;

// http://wiki.unity3d.com/index.php/ObjExporter

public class ObjExporter
{

    public static string MeshToString(string meshName, Mesh m, Material[] mats, Transform meshTrans)
    {
        var local2WorldMat = meshTrans.localToWorldMatrix;

        StringBuilder sb = new StringBuilder();

        sb.Append("mtllib ").Append(meshName+".mtl").Append("\n");

        sb.Append("g ").Append(meshName).Append("\n");
        foreach (Vector3 v in m.vertices)
        {
            // 左手座標から右手座標へ変換するためにx軸反転
            var wv = local2WorldMat.MultiplyPoint(v);
            sb.Append(string.Format("v {0} {1} {2}\n", -wv.x, wv.y, wv.z));
        }
        sb.Append("\n");
        foreach (Vector3 n in m.normals)
        {
            var wn = local2WorldMat.MultiplyVector(n);
            sb.Append(string.Format("vn {0} {1} {2}\n", wn.x, wn.y, wn.z));
        }
        sb.Append("\n");
        foreach (Vector3 v in m.uv)
        {
            sb.Append(string.Format("vt {0} {1}\n", v.x, v.y));
        }
        for (int material = 0; material < m.subMeshCount; material++)
        {
            sb.Append("\n");
            sb.Append("usemtl ").Append(mats[material].name).Append("\n");
            sb.Append("usemap ").Append(mats[material].name).Append("\n");

            int[] triangles = m.GetTriangles(material);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // x軸反転によって裏表が反転したため元に合わせる
                sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n",
                    triangles[i + 1] + 1, triangles[i] + 1, triangles[i + 2] + 1));
            }
        }
        return sb.ToString();
    }

    public static void MeshToFile(string meshName, Mesh m, Material[] mats, Transform meshTrans, string filename)
    {
        filename = GetUniqueFilePath(filename);

        using (StreamWriter sw = new StreamWriter(filename, false))
        {
            sw.Write(MeshToString(meshName, m, mats, meshTrans));
        }
    }

    public static void MaterialsToMtl(Material[] mats, string fileName)
    {
        StringBuilder sb = new StringBuilder();
        string textureName;
        Color color;

        foreach (var mat in mats)
        {
            if (mat.HasProperty("_Color"))
            {
                var texture = mat.mainTexture;
                textureName = Path.GetFileName(AssetDatabase.GetAssetPath(texture));
            }
            else
                textureName = "";

            if (mat.HasProperty("_Color"))
                color = mat.GetColor("_Color");
            else
                color = Color.white;

            sb.Append("newmtl ").Append(mat.name).Append("\n");
            // 鏡面反射角度
            sb.Append("Ns ").Append("300").Append("\n");
            // アルファ
            sb.Append("d ").Append(color.a).Append("\n");
            // Shininess
            sb.Append("Ni ").Append("0.001").Append("\n");
            // 照明モデル(1:鏡面反射無効, 2:有効)
            sb.Append("illum ").Append("2").Append("\n");
            // Ambient color
            sb.Append(string.Format("Ka {0} {1} {2}\n", 1, 1, 1));
            // Diffuse color
            sb.Append(string.Format("Kd {0} {1} {2}\n", color.r, color.g, color.b));
            // Specular color
            sb.Append(string.Format("Ks {0} {1} {2}\n", 1, 1, 1));

            // テクスチャ
            if (textureName != "")
               sb.Append("map_Kd ").Append(textureName).Append("\n");

            sb.Append("\n");
        }

        string fileData = sb.ToString();

        using (StreamWriter sw = new StreamWriter(fileName, false))
        {
            sw.Write(fileData);
        }
    }

    private static string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var splitedFilePath = filePath.Split('.');
        var filePathWithoutExtension = splitedFilePath[0];
        var extension = splitedFilePath[1];

        for (int i = 1; i <= 100; i++)
        {
            filePath = string.Format("{0} ({1}).{2}", filePathWithoutExtension, i, extension);

            if (!File.Exists(filePath))
            {
                return filePath;
            }
        }

        throw new Exception("同名のファイルが多すぎます");
    }
}
