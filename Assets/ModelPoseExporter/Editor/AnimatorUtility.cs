using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using System.Linq;
using System.Text;
using System;

// Copyright (c) 2019 gatosyocora
// MIT License

public static class AnimatorUtility {

    /// <summary>
    /// 特定のレイヤーに含まれるAnimationClipのフレーム数の合計を取得する
    /// </summary>
    /// <param name="animator"></param>
    /// <returns>sumAnimationsFrameNum</returns>
    public static int GetSumFrameNum(Animator animator, int layerIndex)
    {
        AnimationClip clip;
        int sumFrameCount = 0;
        
        var controller = animator.runtimeAnimatorController as AnimatorController;
        var layer = controller.layers[layerIndex];
        var stateMachine = layer.stateMachine;

        foreach (var state in stateMachine.states)
        {
            clip = state.state.motion as AnimationClip;
            if (clip != null)
                sumFrameCount += (int)(clip.length * clip.frameRate);
        }

        return sumFrameCount;
    }

    /// <summary>
    /// Animatorの再生がすべて終了しているか(ランタイムでのみ使用可能)
    /// </summary>
    /// <param name="animator"></param>
    /// <param name="existNextState"></param>
    /// <returns>isFinished:bool</returns>
    public static bool IsFinishedAnimator(Animator animator, Dictionary<int, bool> existNextState)
    {
        if (!Application.isPlaying)
            throw new Exception("Execute Runtime Only");

        AnimatorStateInfo animInfo;

        for (int layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
        {
            animator.Update(0);
            animInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);

            // 「現StateのAnimationがすべて再生されていない」または
            // 「次Stateが存在する」場合, まだAnimatorは終わっていないとする
            if (animInfo.normalizedTime <= 1f || existNextState[animInfo.fullPathHash])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// obj以下の有効かつAnimatorControllerが設定されたAnimatorコンポーネントを取得する
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>Animator[] activeAnimators</returns>
    public static Animator[] GetActiveAnimators(GameObject obj)
    {
        return obj
                .GetComponentsInChildren<Animator>()
                .Where(x => x.isActiveAndEnabled &&
                            x.runtimeAnimatorController != null)
                .ToArray();
    }

    /// <summary>
    /// stateのハッシュとそのstateからの遷移先が存在するかの対応付けを取得する
    /// </summary>
    /// <param name="animators"></param>
    /// <returns>Dictionary(fullPathHash:int, existNextState:bool)</returns>
    public static Dictionary<int, bool> GetExistNextStateDictionary(Animator animator)
    {
        Dictionary<int, bool> dict = new Dictionary<int, bool>();
        AnimatorController controller;
        ChildAnimatorState[] states;
        string layerName;
        string dotChar = ".";

        for (int layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
        {
            controller = animator.runtimeAnimatorController as AnimatorController;
            layerName = animator.GetLayerName(layerIndex);
            states = controller.layers[layerIndex].stateMachine.states;
            foreach (var state in states)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(layerName).Append(dotChar).Append(state.state.name);
                dict.Add(Animator.StringToHash(sb.ToString()), state.state.transitions != null);
            }
        }

        return dict;
    }
}
