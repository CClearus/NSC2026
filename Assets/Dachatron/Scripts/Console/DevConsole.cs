using System;
using System.Collections.Generic;
using UnityEngine;

// In-game console. Press ` (backquote) to open/close. Starts itself, no scene setup needed.
//   help                         list commands
//   clear                        clear the output
//   /summon <thing> [args...]    spawn something registered with RegisterSummon
// Other scripts add commands with DevConsole.Register(...) / DevConsole.RegisterSummon(...).
public class DevConsole : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    static readonly Dictionary<string, (Action<string[]> run, string help)> Commands =
        new Dictionary<string, (Action<string[]>, string)>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, (Action<string[]> run, string usage)> Summons =
        new Dictionary<string, (Action<string[]>, string)>(StringComparer.OrdinalIgnoreCase);

    static DevConsole instance;

    const int MaxLines = 200;
    readonly List<string> lines = new List<string>();
    readonly List<string> history = new List<string>();
    int historyIndex;
    string input = "";
    Vector2 scroll;
    bool focusInput;
    Texture2D background;
    GUIStyle lineStyle, inputStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("DevConsole");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DevConsole>();
    }

    // ------------------------------------------------------------------ registration
    public static void Register(string name, string help, Action<string[]> run) => Commands[name] = (run, help);

    public static void RegisterSummon(string name, string usage, Action<string[]> run) => Summons[name] = (run, usage);

    public static void Log(string message)
    {
        if (instance == null) { Debug.Log(message); return; }
        instance.AddLine(message);
    }

    void Awake()
    {
        Register("help", "list commands", _ => PrintHelp());
        Register("clear", "clear the console", _ => lines.Clear());
        Register("summon", "summon <thing> [args]", Summon);

        background = new Texture2D(1, 1);
        background.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.05f, 0.88f));
        background.Apply();

        Application.logMessageReceived += OnUnityLog;
        AddLine("Console ready. Type 'help'.");
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnUnityLog;
        if (background != null) Destroy(background);
    }

    void OnUnityLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            AddLine((type == LogType.Warning ? "<color=#e0c060>" : "<color=#ff6060>") + message + "</color>");
    }

    void AddLine(string line)
    {
        lines.Add(line);
        if (lines.Count > MaxLines) lines.RemoveAt(0);
        scroll.y = float.MaxValue;
    }

    // ------------------------------------------------------------------ commands
    void Execute(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        AddLine("<color=#8fb8ff>> " + text + "</color>");
        history.Add(text);
        historyIndex = history.Count;

        if (text.StartsWith("/")) text = text.Substring(1);
        var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var args = new string[parts.Length - 1];
        Array.Copy(parts, 1, args, 0, args.Length);

        if (!Commands.TryGetValue(parts[0], out var cmd))
        {
            AddLine($"Unknown command '{parts[0]}'. Type 'help'.");
            return;
        }
        try { cmd.run(args); }
        catch (Exception e) { AddLine("<color=#ff6060>" + e.Message + "</color>"); }
    }

    void Summon(string[] args)
    {
        if (args.Length == 0)
        {
            AddLine("summon what? Options:");
            foreach (var s in Summons) AddLine("  /summon " + s.Value.usage);
            return;
        }
        if (!Summons.TryGetValue(args[0], out var summon))
        {
            AddLine($"Nothing called '{args[0]}' to summon.");
            return;
        }
        var rest = new string[args.Length - 1];
        Array.Copy(args, 1, rest, 0, rest.Length);
        summon.run(rest);
    }

    void PrintHelp()
    {
        foreach (var c in Commands) AddLine($"  {c.Key} - {c.Value.help}");
        foreach (var s in Summons) AddLine("  /summon " + s.Value.usage);
    }

    // ------------------------------------------------------------------ open/close
    void SetOpen(bool open)
    {
        IsOpen = open;
        focusInput = open;
        input = "";

        var player = FindAnyObjectByType<PlayerMovement>();
        if (player != null) player.SetMovementLocked(open);
    }

    // ------------------------------------------------------------------ UI
    void OnGUI()
    {
        var e = Event.current;

        // ` toggles the console; swallow the character so it never lands in the text box
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.BackQuote || e.character == '`'))
        {
            if (e.keyCode == KeyCode.BackQuote) SetOpen(!IsOpen);
            e.Use();
            return;
        }
        if (!IsOpen) return;

        if (lineStyle == null)
        {
            lineStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 14 };
            lineStyle.normal.textColor = new Color(0.85f, 0.88f, 0.9f);
            inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 15 };
        }

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Execute(input);
                input = "";
                focusInput = true;
                e.Use();
            }
            else if (e.keyCode == KeyCode.UpArrow && history.Count > 0)
            {
                historyIndex = Mathf.Max(0, historyIndex - 1);
                input = history[historyIndex];
                e.Use();
            }
            else if (e.keyCode == KeyCode.DownArrow && history.Count > 0)
            {
                historyIndex = Mathf.Min(history.Count, historyIndex + 1);
                input = historyIndex < history.Count ? history[historyIndex] : "";
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                SetOpen(false);
                e.Use();
                return;
            }
        }

        float h = Screen.height * 0.38f;
        var area = new Rect(0, 0, Screen.width, h);
        GUI.DrawTexture(area, background);

        GUILayout.BeginArea(new Rect(10, 8, Screen.width - 20, h - 16));
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var line in lines) GUILayout.Label(line, lineStyle);
        GUILayout.EndScrollView();

        GUI.SetNextControlName("DevConsoleInput");
        input = GUILayout.TextField(input, inputStyle, GUILayout.Height(26));
        GUILayout.EndArea();

        if (focusInput)
        {
            GUI.FocusControl("DevConsoleInput");
            focusInput = false;
        }
    }
}
