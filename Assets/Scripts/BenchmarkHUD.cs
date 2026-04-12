using UnityEngine;

[RequireComponent(typeof(Camera))]
public class BenchmarkHUD : MonoBehaviour
{
    [Header("Graphs")]
    [Tooltip("Number of frames kept in graph history.")]
    public int historySize = 128;

    [Header("Scales (ms)")]
    public float maxFPS    = 200f;
    public float maxTimeMs = 50f;

    // ─────────────────────────────────────────────────────────────────────────

    [Header("Renderers")]
    public VATInstanceRenderer        vatInstanceRenderer;
    public InstancingInstanceRenderer instancingInstanceRenderer;
    public VFXGraphRenderer        mdiInstanceRenderer;

    Camera _cam;
    // 0 = Classic, 1 = Instancing, 2 = VAT, 3 = VFX
    int    _mode;
    int    _classicLayer;

    float[] _fps;
    float[] _cpu;
    float[] _gpu;
    float[] _rt;
    int     _head;

    FrameTiming[] _timings = new FrameTiming[1];

    // Rolling average accumulators over 0.5s
    float _accumCPU, _accumGPU, _accumRT, _accumTime;
    int   _accumCount;
    float _avgFPS, _avgCPU, _avgGPU, _avgRT;
    const float AVG_WINDOW = 0.5f;

    // Session average (since last mode change, warmup excluded)
    [Header("Session Avg")]
    [Tooltip("Number of frames to skip after a mode change (transition spike).")]
    public int warmupFrames = 20;

    float _sessCPU, _sessGPU, _sessRT, _sessTime;
    int   _sessCount, _sessSkipped;
    float _sessAvgFPS, _sessAvgCPU, _sessAvgGPU, _sessAvgRT;

    // Multi-mode comparison: last session avg per mode (0=Classic,1=Instancing,2=VAT,3=VFX)
    readonly float[] _cmpFPS = new float[4];
    readonly float[] _cmpCPU = new float[4];
    readonly float[] _cmpGPU = new float[4];
    readonly float[] _cmpRT  = new float[4];
    readonly bool[]  _cmpHasData = new bool[4];

    Texture2D _texBg, _texFPS, _texCPU, _texGPU, _texRT;

    GUIStyle _styleBig;
    GUIStyle _styleSmall;
    GUIStyle _styleSession;
    GUIStyle _styleClassic;
    GUIStyle _styleInstancing;
    GUIStyle _styleVAT;
    GUIStyle _styleMDI;
    GUIStyle _styleHeader;

    // System info strings — cached at startup, never change at runtime
    string _sysProcessor;
    string _sysGPU;
    string _sysRAM;
    string _sysScreen;

    // ─────────────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        _classicLayer = LayerMask.NameToLayer("Classic");

        // Cache system info — these never change mid-session
        _sysProcessor = $"{SystemInfo.processorType}  ×{SystemInfo.processorCount}";
        _sysGPU       = $"{SystemInfo.graphicsDeviceName}  {SystemInfo.graphicsMemorySize} MB";
        _sysRAM       = $"{SystemInfo.systemMemorySize / 1024f:F1} GB";
        _sysScreen    = $"{Screen.width} × {Screen.height}  {Screen.currentResolution.refreshRateRatio.value:F0} Hz";

        _fps = new float[historySize];
        _cpu = new float[historySize];
        _gpu = new float[historySize];
        _rt  = new float[historySize];

        _texBg  = MakeTex(new Color(0f,    0f,    0f,    0.65f));
        _texFPS = MakeTex(new Color(0.2f,  1f,    0.2f,  1f));
        _texCPU = MakeTex(new Color(0.25f, 0.6f,  1f,    1f));
        _texGPU = MakeTex(new Color(1f,    0.55f, 0.1f,  1f));
        _texRT  = MakeTex(new Color(1f,    0.2f,  0.45f, 1f));

        SetMode(0);
    }

    void OnDisable()
    {
        Destroy(_texBg);
        Destroy(_texFPS);
        Destroy(_texCPU);
        Destroy(_texGPU);
        Destroy(_texRT);
    }

    // ─────────────────────────────────────────────────────────────────────────

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
            SetMode((_mode + 1) % 4);

        FrameTimingManager.CaptureFrameTimings();
        uint count = FrameTimingManager.GetLatestTimings(1, _timings);

        float dt     = Time.unscaledDeltaTime;
        float curCPU = dt * 1000f;
        float curGPU  = count > 0 ? (float)_timings[0].gpuFrameTime              : 0f;
        float curRT   = count > 0 ? (float)_timings[0].cpuRenderThreadFrameTime  : 0f;

        _fps[_head] = 1f / dt;
        _cpu[_head] = curCPU;
        _gpu[_head] = curGPU;
        _rt[_head]  = curRT;

        _accumCPU  += curCPU;
        _accumGPU  += curGPU;
        _accumRT   += curRT;
        _accumTime += dt;
        _accumCount++;

        if (_accumTime >= AVG_WINDOW)
        {
            _avgFPS = _accumCount / _accumTime;
            _avgCPU = _accumCPU  / _accumCount;
            _avgGPU = _accumGPU  / _accumCount;
            _avgRT  = _accumRT   / _accumCount;
            _accumCPU = _accumGPU = _accumRT = _accumTime = 0f;
            _accumCount = 0;
        }

        // Session average — skip first frames after mode change
        if (_sessSkipped < warmupFrames)
        {
            _sessSkipped++;
        }
        else
        {
            _sessCPU  += curCPU;
            _sessGPU  += curGPU;
            _sessRT   += curRT;
            _sessTime += dt;
            _sessCount++;

            if (_sessCount > 0)
            {
                _sessAvgFPS = _sessCount / _sessTime;
                _sessAvgCPU = _sessCPU  / _sessCount;
                _sessAvgGPU = _sessGPU  / _sessCount;
                _sessAvgRT  = _sessRT   / _sessCount;

                _cmpFPS[_mode] = _sessAvgFPS;
                _cmpCPU[_mode] = _sessAvgCPU;
                _cmpGPU[_mode] = _sessAvgGPU;
                _cmpRT [_mode] = _sessAvgRT;
                _cmpHasData[_mode] = true;
            }
        }

        _head = (_head + 1) % historySize;
    }

    void SetMode(int mode)
    {
        _mode = mode;

        _sessCPU = _sessGPU = _sessRT = _sessTime = 0f;
        _sessCount = _sessSkipped = 0;
        _sessAvgFPS = _sessAvgCPU = _sessAvgGPU = _sessAvgRT = 0f;

        // Classic layer — visible only in Classic mode (0)
        int mask = _cam.cullingMask;
        if (_classicLayer != -1)
            mask = (mode == 0) ? mask | (1 << _classicLayer)
                               : mask & ~(1 << _classicLayer);
        _cam.cullingMask = mask;

        // VAT, Instancing and VFX manage visibility through their own draw calls
        if (vatInstanceRenderer        != null) vatInstanceRenderer.SetVATActive(mode == 2);
        if (instancingInstanceRenderer != null) instancingInstanceRenderer.SetInstancingActive(mode == 1);
        if (mdiInstanceRenderer        != null) mdiInstanceRenderer.SetMDIActive(mode == 3);
    }

    // ─────────────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (_styleBig == null)
        {
            _styleBig = new GUIStyle(GUI.skin.label)
                { fontSize = 18, fontStyle = FontStyle.Bold };
            _styleSmall = new GUIStyle(GUI.skin.label)
                { fontSize = 12, normal = { textColor = Color.white } };
            _styleSession = new GUIStyle(_styleSmall)
                { normal = { textColor = new Color(1f, 0.85f, 0.3f) } };
            _styleHeader = new GUIStyle(_styleSmall)
                { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            _styleClassic    = new GUIStyle(_styleSmall) { normal = { textColor = new Color(0.3f, 0.9f, 1f) } };
            _styleInstancing = new GUIStyle(_styleSmall) { normal = { textColor = Color.green } };
            _styleVAT        = new GUIStyle(_styleSmall) { normal = { textColor = Color.yellow } };
            _styleMDI        = new GUIStyle(_styleSmall) { normal = { textColor = new Color(1f, 0.3f, 0.9f) } };
        }

        const int W = 290;
        const int graphH = 42;
        const int pad = 8;
        int totalH = pad + 28 + 20 + 20 + pad + 18 + 20 + 20 + pad + 4 * (graphH + 4) + pad;

        Rect panel = new Rect(10, 10, W, totalH);
        GUI.DrawTexture(panel, _texBg);

        GUILayout.BeginArea(new Rect(panel.x + pad, panel.y + pad,
                                     panel.width - pad * 2, panel.height - pad * 2));

        _styleBig.normal.textColor = _mode switch
        {
            0 => new Color(0.3f, 0.9f, 1f),
            1 => Color.green,
            2 => Color.yellow,
            _ => new Color(1f, 0.3f, 0.9f),
        };
        string modeLabel = _mode switch { 0 => "▶ CLASSIC", 1 => "▶ INSTANCING", 2 => "▶ VAT", _ => "▶ VFX" };
        GUILayout.Label(modeLabel + "  [F]", _styleBig);

        GUILayout.Label($"FPS  {_avgFPS:F0}   CPU  {_avgCPU:F1} ms", _styleSmall);
        GUILayout.Label($"GPU  {_avgGPU:F1} ms   RT  {_avgRT:F1} ms", _styleSmall);
        GUILayout.Space(pad);

        string sessHeader = _sessSkipped < warmupFrames
            ? $"SESSION AVG  (warmup {_sessSkipped}/{warmupFrames})"
            : $"SESSION AVG  ({_sessCount} frames)";
        GUILayout.Label(sessHeader, _styleSession);
        GUILayout.Label($"FPS  {_sessAvgFPS:F0}   CPU  {_sessAvgCPU:F1} ms", _styleSmall);
        GUILayout.Label($"GPU  {_sessAvgGPU:F1} ms   RT  {_sessAvgRT:F1} ms", _styleSmall);
        GUILayout.Space(pad);

        DrawGraph("FPS",    _fps, _texFPS, graphH, 0f, maxFPS);
        DrawGraph("CPU ms", _cpu, _texCPU, graphH, 0f, maxTimeMs);
        DrawGraph("GPU ms", _gpu, _texGPU, graphH, 0f, maxTimeMs);
        DrawGraph("RT  ms", _rt,  _texRT,  graphH, 0f, maxTimeMs);

        GUILayout.EndArea();

        DrawComparisonPanel(pad);
    }

    void DrawComparisonPanel(int pad)
    {
        const int CW      = 310;
        const int lineH   = 24;
        const int titleH  = 30;
        // comparison table + separator + system info section (title + 4 lines)
        int       totalH  = pad + titleH + lineH + 4 * lineH
                          + pad + 18 + 4 * 18
                          + pad * 2;

        float x = Screen.width - CW - 10;
        Rect panel = new Rect(x, 10, CW, totalH);
        GUI.DrawTexture(panel, _texBg);

        Rect inner = new Rect(panel.x + pad, panel.y + pad,
                              panel.width - pad * 2, panel.height - pad * 2);
        GUILayout.BeginArea(inner);

        _styleBig.normal.textColor = Color.white;
        GUILayout.Label("COMPARISON", _styleBig);

        DrawCompRow("              ", "FPS", "CPU ms", "GPU ms", "RT ms", _styleHeader);

        DrawModeRow(0, "Classic    ", _styleClassic);
        DrawModeRow(1, "Instancing ", _styleInstancing);
        DrawModeRow(2, "VAT        ", _styleVAT);
        DrawModeRow(3, "VFX        ", _styleMDI);

        GUILayout.Space(pad);
        GUILayout.Label("SYSTEM", _styleHeader);
        GUILayout.Label($"CPU  {_sysProcessor}",  _styleSmall);
        GUILayout.Label($"GPU  {_sysGPU}",        _styleSmall);
        GUILayout.Label($"RAM  {_sysRAM}",         _styleSmall);
        GUILayout.Label($"SCR  {_sysScreen}",      _styleSmall);

        GUILayout.EndArea();
    }

    void DrawModeRow(int m, string label, GUIStyle style)
    {
        string fps = _cmpHasData[m] ? _cmpFPS[m].ToString("F0")  : "—";
        string cpu = _cmpHasData[m] ? _cmpCPU[m].ToString("F1")  : "—";
        string gpu = _cmpHasData[m] ? _cmpGPU[m].ToString("F1")  : "—";
        string rt  = _cmpHasData[m] ? _cmpRT [m].ToString("F1")  : "—";

        string prefix = (_mode == m) ? "▶ " : "  ";
        DrawCompRow(prefix + label, fps, cpu, gpu, rt, style);
    }

    void DrawCompRow(string label, string fps, string cpu, string gpu, string rt, GUIStyle style)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, style, GUILayout.Width(110));
        GUILayout.Label(fps,   style, GUILayout.Width(42));
        GUILayout.Label(cpu,   style, GUILayout.Width(48));
        GUILayout.Label(gpu,   style, GUILayout.Width(48));
        GUILayout.Label(rt,    style, GUILayout.Width(40));
        GUILayout.EndHorizontal();
    }

    void DrawGraph(string label, float[] data, Texture2D barTex, int height, float min, float max)
    {
        Rect r = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));

        GUI.DrawTexture(r, _texBg);

        float barW = r.width / historySize;

        for (int i = 0; i < historySize; i++)
        {
            int   idx = (_head + i) % historySize;
            float v   = Mathf.Clamp01(Mathf.InverseLerp(min, max, data[idx]));
            float bh  = v * r.height;
            GUI.DrawTexture(new Rect(r.x + i * barW, r.y + r.height - bh, Mathf.Max(barW, 1f), bh), barTex);
        }

        GUI.Label(new Rect(r.x + 3, r.y + 2, 60, 16), label, _styleSmall);

        GUILayout.Space(4);
    }

    static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
