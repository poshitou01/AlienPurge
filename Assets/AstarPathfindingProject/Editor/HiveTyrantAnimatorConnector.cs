#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Connects 40 existing Hive Tyrant animator states without touching animation clips,
/// sprites, prefabs, or game scripts. Designed for Unity 2022.3.
/// Usage: select a COPY of the Hive Tyrant preview AnimatorController asset,
/// then AlienPurge > Hive Tyrant > Connect Selected 40-State Animator.
/// </summary>
public static class HiveTyrantAnimatorConnector
{
    private const string MenuPath =
        "AlienPurge/Hive Tyrant/Connect Selected 40-State Animator";

    private static readonly string[] Directions =
        { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };

    private static readonly string[] Actions =
        { "Idle", "Move", "Hit", "Death", "Idle_Phase2" };

    // Case-insensitive because some exports name Death as "Hivetyrant_Death".
    private static readonly Regex ClipPattern = new Regex(
        @"^hive_?tyrant_(idle_phase2|idle|move|hit|death)_(d|dr|r|ur|u|ul|l|dl)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return Selection.activeObject is AnimatorController;
    }

    [MenuItem(MenuPath)]
    private static void Connect()
    {
        AnimatorController controller = Selection.activeObject as AnimatorController;
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Hive Tyrant", "Please select an Animator Controller asset.", "OK");
            return;
        }

        if (!controller.name.ToLowerInvariant().Contains("hivetyrant"))
        {
            EditorUtility.DisplayDialog("Hive Tyrant", "The selected controller name does not contain 'HiveTyrant'. No changes made.", "OK");
            return;
        }

        if (controller.layers == null || controller.layers.Length == 0)
        {
            EditorUtility.DisplayDialog("Hive Tyrant", "Selected controller has no Base Layer. No changes made.", "OK");
            return;
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        if (machine == null)
        {
            EditorUtility.DisplayDialog("Hive Tyrant", "Base Layer has no state machine. No changes made.", "OK");
            return;
        }

        // Validation first: preserve the current controller unchanged if any state is missing.
        var map = new Dictionary<string, AnimatorState>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();
        int unmatched = 0;

        foreach (ChildAnimatorState child in machine.states)
        {
            AnimatorState state = child.state;
            if (state == null) continue;
            string key;
            if (!TryStateKey(state.name, out key) &&
                (state.motion == null || !TryStateKey(state.motion.name, out key)))
            {
                unmatched++;
                continue;
            }

            if (map.ContainsKey(key)) duplicates.Add(key);
            else map.Add(key, state);
        }

        var missing = new List<string>();
        foreach (string action in Actions)
            foreach (string dir in Directions)
                if (!map.ContainsKey(Key(action, dir)))
                    missing.Add("HiveTyrant_" + action + "_" + dir);

        if (duplicates.Count > 0 || missing.Count > 0)
        {
            string msg = "No changes made.\n\n" +
                (missing.Count > 0
                    ? "Missing states (" + missing.Count + "):\n" + string.Join("\n", missing.ToArray()) + "\n\n"
                    : "") +
                (duplicates.Count > 0
                    ? "Duplicate action/direction combinations:\n" + string.Join("\n", duplicates.ToArray())
                    : "");
            Debug.LogError("[HiveTyrantAnimatorConnector] " + msg);
            EditorUtility.DisplayDialog("Hive Tyrant - Validation Failed", msg, "OK");
            return;
        }

        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if ((p.name == "Direction" || p.name == "Action") &&
                p.type != AnimatorControllerParameterType.Int)
            {
                EditorUtility.DisplayDialog("Hive Tyrant", "Parameter '" + p.name + "' exists but is not Int. No changes made.", "OK");
                return;
            }
        }

        int ordinaryTransitions = machine.states.Sum(s =>
            s.state == null ? 0 : s.state.transitions.Length);
        if (ordinaryTransitions > 0)
        {
            EditorUtility.DisplayDialog("Hive Tyrant", "Existing state-to-state transitions found (" + ordinaryTransitions + "). The tool will not overwrite an already connected graph. No changes made.", "OK");
            return;
        }

        bool proceed = EditorUtility.DisplayDialog(
            "Connect Hive Tyrant Animator",
            "Controller: " + controller.name + "\n" +
            "Found: 40/40 matching states.\n" +
            "Unrelated states: " + unmatched + "\n" +
            "Existing Any State transitions: " + machine.anyStateTransitions.Length + "\n\n" +
            "Add Int parameters Direction and Action, set default Idle_DR, " +
            "and recreate 40 Any State transitions?\n\n" +
            "ONLY the selected controller will be changed. Use a duplicate for safety.",
            "Connect", "Cancel");
        if (!proceed) return;

        Undo.RegisterCompleteObjectUndo(controller, "Connect Hive Tyrant Animator");
        Undo.RegisterCompleteObjectUndo(machine, "Connect Hive Tyrant Animator");

        // Do not alter existing unrelated parameters.
        var parameters = new List<AnimatorControllerParameter>(controller.parameters);
        if (!parameters.Any(p => p.name == "Direction"))
            parameters.Add(new AnimatorControllerParameter
            {
                name = "Direction",
                type = AnimatorControllerParameterType.Int,
                defaultInt = 1
            });
        if (!parameters.Any(p => p.name == "Action"))
            parameters.Add(new AnimatorControllerParameter
            {
                name = "Action",
                type = AnimatorControllerParameterType.Int,
                defaultInt = 0
            });
        controller.parameters = parameters.ToArray();

        // This tool is meant for an unconnected preview controller. Recreate existing
        // Any State transitions so reruns don't stack duplicates.
        foreach (AnimatorStateTransition oldTransition in machine.anyStateTransitions.ToArray())
            machine.RemoveAnyStateTransition(oldTransition);

        machine.defaultState = map[Key("Idle", "DR")];

        int count = 0;
        for (int action = 0; action < Actions.Length; action++)
        {
            for (int dir = 0; dir < Directions.Length; dir++)
            {
                AnimatorState target = map[Key(Actions[action], Directions[dir])];
                AnimatorStateTransition transition = machine.AddAnyStateTransition(target);
                transition.name = "Action" + action + "_Direction" + dir;
                transition.hasExitTime = false;
                transition.duration = 0f;
                transition.offset = 0f;
                transition.canTransitionToSelf = false;
                transition.interruptionSource = TransitionInterruptionSource.None;
                transition.AddCondition(AnimatorConditionMode.Equals, action, "Action");
                transition.AddCondition(AnimatorConditionMode.Equals, dir, "Direction");
                count++;
            }
        }

        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        string success = "Connected " + count + " Any State transitions in " +
            controller.name + ".\n" +
            "Direction(Int): 0=D, 1=DR, 2=R, 3=UR, 4=U, 5=UL, 6=L, 7=DL.\n" +
            "Action(Int): 0=Idle, 1=Move, 2=Hit, 3=Death, 4=Idle_Phase2.\n\n" +
            "IMPORTANT: Game code must set Action back to Idle/Move after Hit; " +
            "lock Direction and Action=3 when dying. This tool does not add gameplay code.";
        Debug.Log("[HiveTyrantAnimatorConnector] " + success);
        EditorUtility.DisplayDialog("Hive Tyrant - Connected", success, "OK");
    }

    private static string Key(string action, string dir)
    {
        return action.ToLowerInvariant() + ":" + dir.ToUpperInvariant();
    }

    private static bool TryStateKey(string name, out string key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(name)) return false;
        Match m = ClipPattern.Match(name.Trim());
        if (!m.Success) return false;
        key = Key(m.Groups[1].Value, m.Groups[2].Value);
        return true;
    }
}
#endif
