#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Walk every approach/threshold with CharacterController.Move. No test teleport.
public static class FacilityEntranceChecks
{
    public static bool Running { get; private set; }
    static ShipyardExploreCameraController e; static FacilityEntrance entrance; static string folder, error, rigPose;
    static readonly int[] ids = { 10, 11, 12, 19, 1 }; static int index, stage, crossings, lastFrame; static double ready;
    static float started, movedAt; static Vector3 before; static ShipyardNavigationMode view;
    static readonly List<object> rows = new List<object>(); static readonly List<Vector3> trajectory = new List<Vector3>(); static Queue<Vector3> path;
    static Keyboard keyboard; static InputDevice[] devices; static InputSettings original, temporary; static HideFlags flags;
    static bool background; static int frameRate; static EditorWindow game; static MethodInfo repaint;
    static string Pose() { var t = GameObject.Find("TowerCraneRoot").transform; return t.position.ToString("F6") + t.rotation.ToString("F6") + t.localScale.ToString("F6") + (t.parent == null); }
    static void Require(bool v, string text) { if (!v) throw new Exception("F" + ids[Math.Min(index, 4)] + " stage " + stage + ": " + text); }
    static void Record(string test, object data = null) { rows.Add(new { test, passed = true, data }); }
    static void Key(params Key[] keys) { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    static void Aim(Vector3 target)
    { var d = target - (e.player.position + Vector3.up * e.eyeHeight); typeof(ShipyardExploreCameraController).GetProperty("Yaw").SetValue(e, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); typeof(ShipyardExploreCameraController).GetProperty("Pitch").SetValue(e, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg); e.CaptureCursor(); e.UpdateCamera(); }
    public static void Run(string output)
    {
        if (!Application.isPlaying || Running) throw new Exception("Fresh Play required"); folder = output; Directory.CreateDirectory(folder);
        e = UnityEngine.Object.FindFirstObjectByType<ShipyardExploreCameraController>(); rigPose = Pose(); index = stage = crossings = 0; lastFrame = -1; error = null; rows.Clear(); trajectory.Clear(); path = null;
        original = InputSystem.settings; flags = original.hideFlags; if (flags == HideFlags.HideAndDontSave) original.hideFlags = HideFlags.DontSave;
        temporary = UnityEngine.Object.Instantiate(original); temporary.hideFlags = HideFlags.DontSave; temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; InputSystem.settings = temporary;
        devices = InputSystem.devices.Where(x => x.enabled && (x is Keyboard || x is Mouse)).ToArray(); foreach (var d in devices) InputSystem.DisableDevice(d);
        keyboard = InputSystem.AddDevice<Keyboard>("EntranceTestKeyboard"); keyboard.MakeCurrent(); background = Application.runInBackground; frameRate = Application.targetFrameRate; Application.runInBackground = true; Application.targetFrameRate = 60;
        game = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")); game.Show(); game.Focus(); repaint = game.GetType().GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Running = true; started = Time.realtimeSinceStartup; ready = EditorApplication.timeSinceStartup + 1; EditorApplication.update += Tick;
    }
    // Test-only A*: Scene collider/ground predicates, no meter-domain conversion.
    static Queue<Vector3> Plan(Vector3 goal)
    {
        Vector3 start = e.player.position; const float cell = .8f; var from = new Dictionary<Vector2Int, Vector2Int>(); var cost = new Dictionary<Vector2Int, float>(); var open = new List<Vector2Int>(); var closed = new HashSet<Vector2Int>();
        var target = new Vector2Int(Mathf.RoundToInt((goal.x - start.x) / cell), Mathf.RoundToInt((goal.z - start.z) / cell)); var zero = Vector2Int.zero; cost[zero] = 0; open.Add(zero); bool found = false;
        Func<Vector2Int, Vector3> point = k => start + new Vector3(k.x * cell, 0, k.y * cell);
        while (open.Count > 0 && closed.Count < 24000)
        {
            var best = open.OrderBy(k => cost[k] + (k - target).magnitude).First(); open.Remove(best); if (best == target) { found = true; break; } closed.Add(best);
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var next = best + d; if (closed.Contains(next)) continue; var p = point(next); p.y = 0;
                if (!e.ValidGround(p, out var g) || Physics.CheckCapsule(g + Vector3.up * .3f, g + Vector3.up * 1.5f, .31f, e.solidMask, QueryTriggerInteraction.Ignore)) continue;
                float c = cost[best] + 1; if (cost.TryGetValue(next, out float old) && old <= c) continue; cost[next] = c; from[next] = best; if (!open.Contains(next)) open.Add(next);
            }
        }
        Require(found, "No walkable approach route"); var reverse = new List<Vector3> { goal }; var key = target; while (key != zero) { reverse.Add(point(key)); key = from[key]; } reverse.Reverse(); return new Queue<Vector3>(reverse);
    }
    static bool Walk()
    {
        for (int i = 0; i < 16 && path.Count > 0; i++)
        {
            var delta = path.Peek() - e.player.position; delta.y = 0; if (delta.magnitude < .055f) { path.Dequeue(); continue; }
            var last = e.player.position; e.character.Move(Vector3.ClampMagnitude(delta, .07f) + Vector3.down * .06f); entrance.Observe(e.player.position); trajectory.Add(e.player.position);
            if (Vector3.Distance(last, e.player.position) < .003f) { if (Time.realtimeSinceStartup - movedAt > 2) throw new Exception("Door/approach snag at " + e.player.position + " toward " + path.Peek()); } else movedAt = Time.realtimeSinceStartup;
        }
        return path.Count == 0;
    }
    static void Tick()
    {
        if (!Running) return;
        try
        {
            game.Focus(); repaint.Invoke(game, null); EditorApplication.QueuePlayerLoopUpdate(); if (lastFrame == Time.frameCount || EditorApplication.timeSinceStartup < ready) return; lastFrame = Time.frameCount;
            Require(Time.realtimeSinceStartup - started < 240, "Timeout"); Require(Pose() == rigPose, "LIVE rig changed");
            if (stage == 0)
            {
                var f = e.shipyard.catalog.Find(ids[index]); e.shipyard.selection.SelectFacility(f); e.shipyard.overview.EnterFacility(); e.Enter(index % 2 == 1); entrance = f.GetComponentsInChildren<FacilityEntrance>(true).Single(); view = e.Mode; stage = 1; return;
            }
            if (e.Blending) return;
            if (stage == 1) { e.CaptureCursor(); path = Plan(entrance.exteriorPoint.position); movedAt = Time.realtimeSinceStartup; stage = 2; }
            if (stage == 2)
            {
                if (!Walk()) return; Require(!entrance.Contains(e.player.position), "Approach not exterior"); entrance.Observe(e.player.position); crossings = 0;
                if (index == 0) { Aim(entrance.transform.position + Vector3.up * 1.4f); Require(e.EntranceTarget == entrance, "Aim resolver did not select door"); Key(UnityEngine.InputSystem.Key.F, UnityEngine.InputSystem.Key.E); stage = 3; ready = EditorApplication.timeSinceStartup + .25; return; } stage = 10;
            }
            if (stage == 3) { Key(); Require(!entrance.doorOpen && entrance.InteractionCount == 1 && !e.DetailsOpen, "F/E double action or door did not close"); Require(UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Count(t => t.name == "InteractionPrompt") == 1, "Duplicate interaction prompts"); Record("K: simultaneous E+F executes entrance only; one prompt", entrance.Prompt); stage = 4; ready = EditorApplication.timeSinceStartup + .15; return; }
            if (stage == 4) { Key(UnityEngine.InputSystem.Key.F); stage = 5; ready = EditorApplication.timeSinceStartup + .2; return; }
            if (stage == 5) { Key(); Require(entrance.doorOpen && entrance.InteractionCount == 2, "F did not reopen door"); Aim(entrance.transform.position - entrance.transform.right * 1.2f + Vector3.up * 1.5f); Require(e.Target != null && e.EntranceTarget == null, "POI facing selection"); Key(UnityEngine.InputSystem.Key.E); stage = 6; ready = EditorApplication.timeSinceStartup + .25; return; }
            if (stage == 6) { Key(); Require(e.DetailsOpen && entrance.InteractionCount == 2, "E POI regressed"); Record("L: E opens original POI details; F only toggles personnel leaf"); e.CloseDetails(); stage = 10; }
            if (stage == 10)
            {
                float side = crossings / 2 == 1 ? .22f : crossings / 2 == 2 ? -.22f : 0;
                var goal = (crossings % 2 == 0 ? entrance.interiorPoint.position : entrance.exteriorPoint.position) + entrance.transform.right * side;
                path = new Queue<Vector3>(new[] { goal }); movedAt = Time.realtimeSinceStartup; stage = 11;
            }
            if (stage == 11)
            {
                if (!Walk()) return; bool inside = crossings % 2 == 0; Require(entrance.IsInside == inside, "Interior/Exterior state mismatch"); Require(e.IsExploring && e.Facility == entrance.facility && e.Mode == view, "Explore session/camera/context changed"); Require(e.character.stepOffset == .25f, "stepOffset changed");
                Record("F" + ids[index].ToString("D2") + " " + (inside ? "entry" : "exit") + " " + (crossings / 2 + 1), new { position = new[] { e.player.position.x, e.player.position.y, e.player.position.z }, entrance.EntryCount, entrance.ExitCount, mode = e.Mode.ToString(), sideOffset = crossings / 2 == 1 ? .22f : crossings / 2 == 2 ? -.22f : 0 });
                crossings++; if (crossings <= 2) { before = e.player.position; Aim(e.player.position + (inside ? entrance.transform.forward : -entrance.transform.forward) * 4 + Vector3.up * 1.7f); Key(UnityEngine.InputSystem.Key.W); stage = 12; ready = EditorApplication.timeSinceStartup + .25; return; }
                stage = crossings < 6 ? 10 : 13;
            }
            if (stage == 12) { Key(); Require(Vector3.Distance(before, e.player.position) > .1f, "WASD did not resume after crossing"); Record("F" + ids[index].ToString("D2") + " WASD " + (crossings == 1 ? "inside" : "outside")); stage = crossings < 6 ? 10 : 13; return; }
            if (stage == 13)
            {
                Require(!entrance.IsInside && entrance.EntryCount == 3 && entrance.ExitCount == 3, "Not three repeatable crossing pairs"); Require(e.CanMove && !e.inputGuard.crane.enabled, "Exit released Explore input guard");
                if (ids[index] == 19) Require(entrance.transform.IsChildOf(entrance.facility.transform.Find("PublicDataVisual")) && !entrance.facility.transform.Find("Reference-based detailed visual").gameObject.activeSelf, "F19 source/legacy");
                Aim(entrance.transform.position + Vector3.up * 1.4f); Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(Path.Combine(folder, "F" + ids[index].ToString("D2") + "_Entrance.png")); if (view == ShipyardNavigationMode.THIRD_PERSON) { Require(e.EntranceTarget == entrance, "Third person entrance aim/reach"); Key(UnityEngine.InputSystem.Key.F); stage = 20; } else stage = 14; ready = EditorApplication.timeSinceStartup + .4; return;
            }
            if (stage == 20) { Key(); Require(!entrance.doorOpen, "Third person F close"); stage = 21; ready = EditorApplication.timeSinceStartup + .15; return; }
            if (stage == 21) { Key(UnityEngine.InputSystem.Key.F); stage = 22; ready = EditorApplication.timeSinceStartup + .2; return; }
            if (stage == 22) { Key(); Require(entrance.doorOpen && !e.DetailsOpen, "Third person F reopen"); Record("F" + ids[index].ToString("D2") + " third person F closes/reopens from 3UU actor approach"); stage = 14; ready = EditorApplication.timeSinceStartup + .3; return; }
            if (stage == 14) { e.Exit(); Require(!e.IsExploring && e.inputGuard.crane.enabled, "Explore exit restore"); index++; if (index == ids.Length) { Finish(); return; } stage = 0; }
        }
        catch (Exception ex) { error = ex.ToString(); Finish(); }
    }
    static void Finish()
    {
        Running = false; EditorApplication.update -= Tick; Key(); if (e.IsExploring) e.Exit(); InputSystem.RemoveDevice(keyboard); InputSystem.settings = original; original.hideFlags = flags; UnityEngine.Object.DestroyImmediate(temporary); foreach (var d in devices) if (d.added) InputSystem.EnableDevice(d); Application.runInBackground = background; Application.targetFrameRate = frameRate;
        var fault = GameObject.Find("TowerCraneRoot").GetComponent<CraneFaultInjector>();
        bool preserved = Pose() == rigPose && !fault.enabled && !fault.faultEnabled && fault.faultMode == CraneFaultInjector.FaultMode.NONE;
        File.WriteAllText(Path.Combine(folder, "Entrance_tests.json"), JsonConvert.SerializeObject(new { passed = error == null && preserved, error, count = rows.Count, rigPreserved = preserved, method = "Normal Explore spawn, collider-based A* approaches + CharacterController.Move at <=0.07UU increments, virtual W/E/F; no test teleport or player Transform writes", rows }, Formatting.Indented));
        File.WriteAllText(Path.Combine(folder, "WalkingTrajectory.json"), JsonConvert.SerializeObject(trajectory.Select(v => new[] { v.x, v.y, v.z }).ToArray()));
    }
}
#endif
