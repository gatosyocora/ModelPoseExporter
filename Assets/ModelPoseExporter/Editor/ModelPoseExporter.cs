using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.Animations;
using System;
using System.Text;

// Copyright (c) 2019 gatosyocora
// MIT License

public class ModelPoseExporter : EditorWindow {

    private GameObject animObject;
    private Animator[] animators;
    private int maxAnimationFrameNum = 0;
    private int startFrame = 0;

    private SkinnedMeshRenderer[] renderers;

    private bool isCombine = false;
    private string saveFolder = "";

    private bool isFinished = false;

    [MenuItem("GatoTool/ModelPoseExporter")]
	private static void Open()
    {
        GetWindow<ModelPoseExporter>("ModelPoseExporter");
    }

    private void OnGUI()
    {
        using (var check = new EditorGUI.ChangeCheckScope())
        {
            animObject = EditorGUILayout.ObjectField("Model", animObject, typeof(GameObject)) as GameObject;
            if (check.changed)
            {
                if (animObject != null)
                {
                    animators = GetActiveAnimators(animObject);
                    maxAnimationFrameNum = GetMaxFrameNum(animators);
                    renderers = animObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                }
            }
        }

        EditorGUILayout.LabelField("Animators");
        using (new EditorGUI.IndentLevelScope())
        {
            if (animators != null)
            {
                for (int i = 0; i < animators.Length; i++)
                {
                    animators[i] = EditorGUILayout.ObjectField("Animator " + (i + 1), animators[i], typeof(Animator)) as Animator;
                }
            }
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Max Frame Num", maxAnimationFrameNum.ToString());
        startFrame = EditorGUILayout.IntSlider("Start Frame", startFrame, 0, maxAnimationFrameNum);

        EditorGUILayout.Space();
        //isCombine = EditorGUILayout.Toggle("Is Combine", isCombine);

        if (saveFolder == "")
            saveFolder = Application.dataPath;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("ObjSaveFolder", saveFolder);

            if (GUILayout.Button("Select Folder", GUILayout.Width(100)))
            {
                saveFolder = EditorUtility.OpenFolderPanel("Select saved folder", saveFolder, "");
                if (saveFolder == "")
                    saveFolder = Application.dataPath;
            }
        }

        if (saveFolder.Contains(Application.dataPath))
            EditorGUILayout.HelpBox("Assetsフォルダの下以外にすることを推奨", MessageType.Warning);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledGroupScope(Application.isPlaying))
        {
            if (GUILayout.Button("Play"))
            {
                EditorApplication.isPlaying = true;
                EditorApplication.isPaused = true;
                isFinished = false;
            }
        }

        using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Finish"))
            {
                EditorApplication.isPaused = false;
                EditorApplication.isPlaying = false;
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledGroupScope(!Application.isPlaying || isFinished))
        {
            if (GUILayout.Button("Export Obj Files"))
            {
                ExportPose(animObject, animators, maxAnimationFrameNum, startFrame, renderers);
                isFinished = true;
            }
        }
        /*
        if (GUILayout.Button("Export Obj File 1 frame"))
        {
            ExportPose1Frame(animObject, animators, maxAnimationFrameNum, startFrame);
        }
        */
    }

    private void ExportPose(GameObject obj, Animator[] animators, int frameMaxCount, int startFrame, SkinnedMeshRenderer[] renderers)
    {
        var frameMaxCountLength = ((int)Mathf.Log10(frameMaxCount) + 1);

        var combineList = new List<CombineInstance>();
        var materialList = new List<Material>();
        int frameCount = 0;
        AnimatorStateInfo animInfo;
        bool isAnimatorFinished = true;

        var isNotEndState = GetExistNextStateDictionary(animators);

        for (int skip = 0; skip < startFrame; skip++)
        {
            EditorApplication.Step();
        }

        while (frameCount < frameMaxCount-startFrame)
        {
            EditorApplication.Step();

            for (int i = 0; i < animators.Length; i++)
            {
                // 一つでも最後までいっていないレイヤーがあるなら書き出しを続ける
                animators[i].Update(0);
                for (int layerIndex = 0; layerIndex < animators[i].layerCount; layerIndex++)
                {
                    animInfo = animators[i].GetCurrentAnimatorStateInfo(layerIndex);

                    if (animInfo.normalizedTime <= 1f || isNotEndState[animInfo.fullPathHash])
                    {
                        isAnimatorFinished = false;
                        break;
                    }
                }
                if (!isAnimatorFinished) break;
            }
            // すべて最後までいっていれば書き出し終わり
            if (isAnimatorFinished) return;

            if (isCombine)
            {
                foreach (var renderer in renderers)
                {
                    Mesh mesh = new Mesh();
                    if (renderer.enabled)
                        renderer.BakeMesh(mesh);

                    for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                    {
                        var combineInstance = new CombineInstance();
                        combineInstance.mesh = mesh;
                        combineInstance.transform = renderer.transform.localToWorldMatrix;
                        combineInstance.subMeshIndex = subMeshIndex;
                        combineList.Add(combineInstance);
                    }
                    materialList.AddRange(renderer.sharedMaterials);
                }
                var combinedMesh = new Mesh();
                combinedMesh.name = obj.name;
                combinedMesh.CombineMeshes(combineList.ToArray(), false);
                Unwrapping.GenerateSecondaryUVSet(combinedMesh);

                //var newObj = InstantiateMeshObject(obj.name + "_combined", combinedMesh, materialList.ToArray());

                string fileName = obj.name + "_" + String.Format("{0:D"+frameMaxCountLength+"}", frameCount) + ".obj";
                //ExportObjFile(obj.name, combinedMesh, materialList.ToArray(), saveFolder, fileName);
            }
            else
            {
                foreach (var renderer in renderers)
                {
                    Mesh mesh = new Mesh();
                    renderer.BakeMesh(mesh);
                    var rendererObj = renderer.gameObject;
                    string fileName = rendererObj.name + "_" + String.Format("{0:D" + frameMaxCountLength + "}", frameCount) + ".obj";
                    ExportObjFile(rendererObj.name, mesh, renderer.sharedMaterials, saveFolder, fileName, rendererObj.transform);
                }
            }

            combineList.Clear();
            materialList.Clear();
            
            frameCount++;

            if (EditorUtility.DisplayCancelableProgressBar("Export obj files",  frameCount+"/"+(frameMaxCount - startFrame),
                                                                                frameCount/(float)(frameMaxCount - startFrame)))
            {
                EditorUtility.ClearProgressBar();

                if (saveFolder.Contains(Application.dataPath))
                    AssetDatabase.Refresh();

                return;
            }
        }
        EditorUtility.ClearProgressBar();

        if (saveFolder.Contains(Application.dataPath))
            AssetDatabase.Refresh();

    }

    // 確認用
    private void ExportPose1Frame(GameObject obj, Animator[] animators, int frameMaxCount, int startFrame)
    {
        var renderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();

        var combineList = new List<CombineInstance>();
        var materialList = new List<Material>();
        int frameCount = 0;

        for (int skip = 0; skip < startFrame; skip++)
        {
            EditorApplication.Step();
            frameCount++;
        }

        if (isCombine)
        {
            foreach (var renderer in renderers)
            {
                Mesh mesh = new Mesh();
                renderer.BakeMesh(mesh);

                for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                {
                    var combineInstance = new CombineInstance();
                    combineInstance.mesh = mesh;
                    combineInstance.transform = renderer.transform.localToWorldMatrix;
                    combineInstance.subMeshIndex = subMeshIndex;
                    combineList.Add(combineInstance);
                }
                materialList.AddRange(renderer.sharedMaterials);
            }
            var combinedMesh = new Mesh();
            combinedMesh.name = obj.name;
            combinedMesh.CombineMeshes(combineList.ToArray(), false);
            Unwrapping.GenerateSecondaryUVSet(combinedMesh);

            //var newObj = InstantiateMeshObject(obj.name + "_combined", combinedMesh, materialList.ToArray());

            string fileName = obj.name + "_" + frameCount + ".obj";
            //ExportObjFile(obj.name, combinedMesh, materialList.ToArray(), saveFolder, fileName);
        }
        else
        {
            foreach (var renderer in renderers)
            {
                Mesh mesh = new Mesh();
                renderer.BakeMesh(mesh);
                var rendererObj = renderer.gameObject;
                string fileName = rendererObj.name + "_" + frameCount + ".obj";
                ExportObjFile(rendererObj.name, mesh, renderer.sharedMaterials, saveFolder, fileName, rendererObj.transform);
            }
        }

        if (saveFolder.Contains(Application.dataPath))
            AssetDatabase.Refresh();

    }

    // メッシュオブジェクトを生成
    // 確認用
    private GameObject InstantiateMeshObject(string name, Mesh mesh, Material[] materials)
    {
        var obj = new GameObject(name);
        var filter = obj.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        var newRenderer = obj.AddComponent<MeshRenderer>();
        newRenderer.sharedMaterials = materials;
        return obj;
    }

    // ObjファイルとMtlファイルを書き出す
    private void ExportObjFile(string objName, Mesh mesh, Material[] mats, string folderPath, string fileName, Transform meshTrans)
    {
        ObjExporter.MeshToFile(objName, mesh, mats, meshTrans, folderPath + "/" + fileName);
        ObjExporter.MaterialsToMtl(mats, folderPath + "/" + objName + ".mtl");
    }

    private int GetMaxFrameNum(Animator[] animators)
    {
        int maxFrameNum = 0;
        
        AnimationClip clip;
        int sumFrameCount = 0;
        AnimatorController controller;
        AnimatorControllerLayer[] layers;
        AnimatorStateMachine stateMachine;

        foreach (var animator in animators)
        {
            controller = animator.runtimeAnimatorController as AnimatorController;
            layers = controller.layers;
            foreach (var layer in layers)
            {
                sumFrameCount = 0;
                stateMachine = layer.stateMachine;
                foreach (var state in stateMachine.states)
                {
                    clip = state.state.motion as AnimationClip;
                    if (clip != null)
                        sumFrameCount += (int)(clip.length * clip.frameRate);
                }

                if (sumFrameCount > maxFrameNum)
                    maxFrameNum = sumFrameCount;
            }
        }
        return maxFrameNum;
    }

    private Animator[] GetActiveAnimators(GameObject obj)
    {
        var animators = animObject.GetComponentsInChildren<Animator>();
        animators = animators
                        .Where(x => x.isActiveAndEnabled &&
                                    x.runtimeAnimatorController != null)
                        .ToArray();

        return animators;
    }

    // stateのハッシュとそのstateからの遷移先が存在するかの対応付けを取得する
    private Dictionary<int, bool> GetExistNextStateDictionary(Animator[] animators)
    {
        Dictionary<int, bool> dict = new Dictionary<int, bool>();
        AnimatorController controller;
        ChildAnimatorState[] states;
        string layerName;
        
        for (int i = 0; i < animators.Length; i++)
        {
            for (int layerIndex = 0; layerIndex < animators[i].layerCount; layerIndex++)
            {
                controller = animators[i].runtimeAnimatorController as AnimatorController;
                layerName = animators[i].GetLayerName(layerIndex);
                states = controller.layers[i].stateMachine.states;
                foreach (var state in states)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(layerName).Append(".").Append(state.state.name);
                    dict.Add(Animator.StringToHash(sb.ToString()), state.state.transitions != null);
                }
            }
        }
        return dict;
    }
}
