using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 패턴 디자이너 - 삼각형 그리드 기반 커스텀 EditorWindow
/// 탭 1: 그리드 편집기 (삼각형 클릭 → 세로 타임라인/색상 설정)
/// 탭 2: 미리보기 (패턴 순차 재생 시뮬레이션)
/// </summary>
public class PatternDesignerWindow : EditorWindow
{
    // ── 참조 ──
    private PatternData _pattern;
    private SongData _songData;

    // ── 탭 ──
    private int _selectedTab = 0;
    private readonly string[] _tabNames = { "🎨 그리드 편집기", "▶ 미리보기" };

    // ── 그리드 편집기 상태 ──
    private int _selectedTriangle = -1;
    private int _draggedTriangle = -1; // 드래그 앤 드롭 대상
    private Vector2 _gridScrollPos;
    private Vector2 _timelineScrollPos;
    
    // 브러시
    private bool _brushR = true, _brushG = false, _brushB = false;

    // ── 박자 세분화 ──
    private int _subdivisionIndex = 2; // 기본 1/4 박
    private static readonly int[] SubdivisionOptions = { 1, 2, 4, 8 };
    private static readonly string[] SubdivisionLabels = { "1 비트", "1/2 박", "1/4 박 (기본)", "1/8 박" };

    // ── 미리보기 상태 ──
    private float _previewBpm = 120f;
    private float _previewTime = 0f;
    private bool _isPreviewPlaying = false;
    private double _previewStartRealTime;
    private Dictionary<int, ColorChannel> _previewTileStates = new Dictionary<int, ColorChannel>();
    private Vector2 _previewScrollPos;

    // ── 삼각형 레이아웃 데이터 (씬과 동일) ──
    private static readonly TriangleLayout[] Triangles = new TriangleLayout[]
    {
        // Row 1 (top): T1~T5
        new TriangleLayout(1,  -1f,     0.8660254f, false),
        new TriangleLayout(2,  -0.5f,   1.153025f,  true),
        new TriangleLayout(3,   0f,     0.8660254f, false),
        new TriangleLayout(4,   0.5f,   1.153025f,  true),
        new TriangleLayout(5,   1f,     0.8660254f, false),
        // Row 2: T6~T12
        new TriangleLayout(6,  -1.5f,   0f,         false),
        new TriangleLayout(7,  -1f,     0.287f,     true),
        new TriangleLayout(8,  -0.5f,   0f,         false),
        new TriangleLayout(9,   0f,     0.287f,     true),
        new TriangleLayout(10,  0.5f,   0f,         false),
        new TriangleLayout(11,  1f,     0.287f,     true),
        new TriangleLayout(12,  1.5f,   0f,         false),
        // Row 3: T13~T19
        new TriangleLayout(13, -1.5f,  -0.5790254f, true),
        new TriangleLayout(14, -1f,    -0.8660254f, false),
        new TriangleLayout(15, -0.5f,  -0.5790254f, true),
        new TriangleLayout(16,  0f,    -0.8660254f, false),
        new TriangleLayout(17,  0.5f,  -0.5790254f, true),
        new TriangleLayout(18,  1f,    -0.8660254f, false),
        new TriangleLayout(19,  1.5f,  -0.5790254f, true),
        // Row 4 (bottom): T20~T24
        new TriangleLayout(20, -1f,    -1.4450507f, true),
        new TriangleLayout(21, -0.5f,  -1.7320508f, false),
        new TriangleLayout(22,  0f,    -1.4450507f, true),
        new TriangleLayout(23,  0.5f,  -1.7320508f, false),
        new TriangleLayout(24,  1f,    -1.4450507f, true),
    };

    private struct TriangleLayout
    {
        public int index;
        public float x, y;
        public bool flipped; // true = ▽ (z=180), false = ▲ (z=0)

        public TriangleLayout(int idx, float x, float y, bool flipped)
        {
            this.index = idx;
            this.x = x;
            this.y = y;
            this.flipped = flipped;
        }
    }

    // ── 타임라인 설정 ──
    private int _maxBeats = 8; // 기본 8비트로 수정
    private PatternData _lastPattern;

    [MenuItem("RhythmPuzzle/패턴 디자이너 %#p")]
    public static void ShowWindow()
    {
        var window = GetWindow<PatternDesignerWindow>("패턴 디자이너");
        window.minSize = new Vector2(700, 600);
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        _isPreviewPlaying = false;
    }

    private void OnEditorUpdate()
    {
        if (_isPreviewPlaying)
            Repaint();
    }

    private void OnGUI()
    {
        // ── 상단: 패턴/곡 SO 선택 ──
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        EditorGUILayout.LabelField("패턴:", GUILayout.Width(40));
        _pattern = (PatternData)EditorGUILayout.ObjectField(_pattern, typeof(PatternData), false, GUILayout.Width(200));
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("곡 SO:", GUILayout.Width(40));
        _songData = (SongData)EditorGUILayout.ObjectField(_songData, typeof(SongData), false, GUILayout.Width(200));
        
        if (_songData != null)
            _previewBpm = _songData.bpm;

        EditorGUILayout.EndHorizontal();

        if (_pattern == null)
        {
            EditorGUILayout.HelpBox("위에서 편집할 PatternData SO를 선택하세요.", MessageType.Info);
            _lastPattern = null;
            return;
        }

        // 로드된 패턴에 따라 타임라인 길이 동적 동기화
        if (_pattern != _lastPattern)
        {
            _lastPattern = _pattern;
            _maxBeats = Mathf.Max(8, Mathf.CeilToInt(_pattern.MaxBeatOffset));
        }

        // ── 탭 ──
        _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames, GUILayout.Height(28));

        EditorGUILayout.Space(4);

        switch (_selectedTab)
        {
            case 0: DrawGridEditorTab(); break;
            case 1: DrawPreviewTab(); break;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  탭 1: 그리드 편집기
    // ════════════════════════════════════════════════════════════

    private void DrawGridEditorTab()
    {
        EditorGUILayout.BeginHorizontal();
        
        // ── 좌측: 삼각형 그리드 ──
        EditorGUILayout.BeginVertical(GUILayout.Width(360));
        DrawTriangleGrid();
        
        // ── 패턴 설정 (graceTime) ──
        EditorGUILayout.Space(8);
        DrawPatternSettings();
        
        EditorGUILayout.EndVertical();

        // ── 우측: 선택된 삼각형의 세로 타임라인 ──
        EditorGUILayout.BeginVertical();
        DrawSelectedTriangleVerticalTimeline();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    /// <summary> 패턴 설정 UI (graceTime, judgmentBeats 등) </summary>
    private void DrawPatternSettings()
    {
        EditorGUILayout.LabelField("패턴 설정", EditorStyles.boldLabel);
        
        // ── 유예 시간 설정 ──
        EditorGUI.BeginChangeCheck();
        float newGraceTime = EditorGUILayout.FloatField("유예 시간 (비트 수)", _pattern.graceTime);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_pattern, "Change Grace Time");
            _pattern.graceTime = newGraceTime;
            EditorUtility.SetDirty(_pattern);
        }
        
        if (_pattern.graceTime < 0)
        {
            string fallbackText = _songData != null 
                ? $"SongData 기본값 사용: {_songData.graceTime}비트" 
                : "SongData 기본값 사용 (-1)";
            EditorGUILayout.HelpBox(fallbackText, MessageType.Info);
        }

        EditorGUILayout.Space(4);

        // ── 판정 연출 대기 시간 설정 ──
        EditorGUI.BeginChangeCheck();
        float newJudgeBeats = EditorGUILayout.FloatField("판정 시간 (비트 수)", _pattern.judgmentBeats);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_pattern, "Change Judgment Beats");
            _pattern.judgmentBeats = newJudgeBeats;
            EditorUtility.SetDirty(_pattern);
        }

        if (_pattern.judgmentBeats < 0)
        {
            string fallbackText = _songData != null 
                ? $"SongData 기본값 사용: {_songData.judgmentBeats}비트" 
                : "SongData 기본값 사용 (-1)";
            EditorGUILayout.HelpBox(fallbackText, MessageType.Info);
        }
    }

    /// <summary> 삼각형 그리드를 씬과 동일한 배치로 그리기 </summary>
    /// <summary> 삼각형 그리드를 씬과 동일한 배치로 그리기 </summary>
    private void DrawTriangleGrid()
    {
        EditorGUILayout.LabelField("삼각형 그리드", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("삼각형을 클릭하면 우측에 세로 타임라인 편집기가 나타납니다.\n설정해둔 값을 유지한 채 마우스 좌클릭으로 삼각형을 드래그해서 다른 삼각형 위로 옮길 수 있습니다.", MessageType.None);

        float gridW = 340f;
        float gridH = 340f;
        float scale = 85f; // 월드 좌표 → 픽셀 스케일

        Rect gridArea = GUILayoutUtility.GetRect(gridW, gridH);
        EditorGUI.DrawRect(gridArea, new Color(0.12f, 0.12f, 0.15f));

        // 중심 계산
        Vector2 center = new Vector2(gridArea.x + gridW * 0.5f, gridArea.y + gridH * 0.45f);

        // 현재 마우스 아래에 위치한 삼각형 찾기 (드래그 하이라이트용)
        int hoverTarget = -1;
        if (_draggedTriangle != -1)
        {
            foreach (var t in Triangles)
            {
                float sx = center.x + t.x * scale;
                float sy = center.y - t.y * scale;
                float triSize = scale * 0.85f;
                Vector3[] vertices = GetTriangleVertices(sx, sy, triSize, t.flipped);
                if (IsPointInTriangle(Event.current.mousePosition, vertices[0], vertices[1], vertices[2]))
                {
                    hoverTarget = t.index;
                    break;
                }
            }
        }

        Handles.BeginGUI();
        foreach (var tri in Triangles)
        {
            // 월드 → 스크린 변환 (Y 반전)
            float sx = center.x + tri.x * scale;
            float sy = center.y - tri.y * scale;

            float triSize = scale * 0.85f;
            Vector3[] vertices = GetTriangleVertices(sx, sy, triSize, tri.flipped);

            // 삼각형의 색상 결정 (패턴에 이 타일의 스텝이 있으면 표시)
            ColorChannel tileColor = GetTileColorFromPattern(tri.index);
            Color fillColor = tileColor != ColorChannel.None
                ? ColorUtil.ToColor(tileColor)
                : new Color(0.24f, 0.24f, 0.28f);

            bool isSelected = (_selectedTriangle == tri.index);
            if (isSelected) fillColor = Color.Lerp(fillColor, Color.yellow, 0.2f);

            Color strokeColor = isSelected ? Color.yellow : new Color(0.4f, 0.4f, 0.45f);
            float strokeWidth = isSelected ? 3.0f : 1.5f;

            // 드래그 중인 원본 삼각형 강조 (주황색 테두리)
            if (_draggedTriangle == tri.index)
            {
                strokeColor = new Color(1f, 0.5f, 0f);
                strokeWidth = 3.5f;
            }

            // 드래그 마우스가 올라간 드롭 대상 삼각형 강조 (에메랄드색 테두리 및 틴트)
            if (_draggedTriangle != -1 && hoverTarget == tri.index && tri.index != _draggedTriangle)
            {
                strokeColor = Color.cyan;
                strokeWidth = 3.5f;
                fillColor = Color.Lerp(fillColor, Color.cyan, 0.25f);
            }

            // 실제 삼각형 그리기
            DrawTriangle(vertices, fillColor, strokeColor, strokeWidth);

            // 텍스트 가독성을 위한 휘도 계산
            float luminance = 0.2126f * fillColor.r + 0.7152f * fillColor.g + 0.0722f * fillColor.b;
            Color textColor = luminance > 0.5f ? Color.black : Color.white;

            // 인덱스 번호 (삼각형 중앙에 표시)
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor },
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            Rect labelRect = new Rect(sx - 20f, sy - 10f, 40f, 20f);
            string orderLabel = GetTriangleOrderLabel(tri.index);
            GUI.Label(labelRect, orderLabel, labelStyle);

            // 클릭 감지 (정밀한 삼각형 내 충돌 판정)
            if (Event.current.type == EventType.MouseDown && IsPointInTriangle(Event.current.mousePosition, vertices[0], vertices[1], vertices[2]))
            {
                if (Event.current.button == 0) // 좌클릭: 선택 및 드래그 시작
                {
                    _selectedTriangle = tri.index;
                    _draggedTriangle = tri.index;
                }
                else if (Event.current.button == 1) // 우클릭: 패턴 지우기
                {
                    ClearStepsForTile(tri.index);
                }
                Event.current.Use();
                Repaint();
            }
        }

        // 전역 마우스 이벤트 핸들링 (드래그 취소 및 드롭 실행)
        Event e = Event.current;
        if (_draggedTriangle != -1)
        {
            if (e.type == EventType.MouseDrag)
            {
                Repaint();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (hoverTarget != -1 && hoverTarget != _draggedTriangle)
                {
                    MoveStepsToTile(_draggedTriangle, hoverTarget);
                    _selectedTriangle = hoverTarget;
                }
                _draggedTriangle = -1;
                e.Use();
                Repaint();
            }
        }

        Handles.EndGUI();
    }

    /// <summary> 선택된 삼각형의 세로 타임라인 편집 UI (비트가 위→아래로 흐름) </summary>
    private void DrawSelectedTriangleVerticalTimeline()
    {
        if (_selectedTriangle < 1)
        {
            EditorGUILayout.HelpBox("좌측 그리드에서 삼각형을 선택하세요.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"▷ Triangle_{_selectedTriangle} 타임라인", EditorStyles.boldLabel);

        // ── 브러시 팔레트 ──
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("브러시:", GUILayout.Width(45));

        GUI.backgroundColor = _brushR ? Color.red : Color.gray;
        if (GUILayout.Toggle(_brushR, "R", "Button", GUILayout.Width(30)) != _brushR) _brushR = !_brushR;
        GUI.backgroundColor = _brushG ? Color.green : Color.gray;
        if (GUILayout.Toggle(_brushG, "G", "Button", GUILayout.Width(30)) != _brushG) _brushG = !_brushG;
        GUI.backgroundColor = _brushB ? new Color(0.3f, 0.3f, 1f) : Color.gray;
        if (GUILayout.Toggle(_brushB, "B", "Button", GUILayout.Width(30)) != _brushB) _brushB = !_brushB;
        GUI.backgroundColor = Color.white;

        ColorChannel brushColor = GetBrushChannel();
        Rect previewRect = GUILayoutUtility.GetRect(20, 18, GUILayout.Width(20));
        EditorGUI.DrawRect(previewRect, brushColor != ColorChannel.None ? ColorUtil.ToColor(brushColor) : Color.gray);
        EditorGUILayout.LabelField(GetChannelLabel(brushColor), GUILayout.Width(60));

        EditorGUILayout.EndHorizontal();

        // ── 설정 행 ──
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"비트 수: {_maxBeats}", GUILayout.Width(80));
        if (GUILayout.Button("+1 비트", GUILayout.Width(60)))
        {
            _maxBeats = Mathf.Clamp(_maxBeats + 1, 4, 128);
        }
        if (GUILayout.Button("+4 비트", GUILayout.Width(60)))
        {
            _maxBeats = Mathf.Clamp(_maxBeats + 4, 4, 128);
        }
        if (GUILayout.Button("-1 비트", GUILayout.Width(60)))
        {
            _maxBeats = Mathf.Clamp(_maxBeats - 1, 4, 128);
        }
        if (GUILayout.Button("-4 비트", GUILayout.Width(60)))
        {
            _maxBeats = Mathf.Clamp(_maxBeats - 4, 4, 128);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (_songData != null)
        {
            EditorGUILayout.LabelField($"BPM: {_songData.bpm} (곡 SO 기준)", GUILayout.Width(150));
        }
        else
        {
            EditorGUILayout.LabelField("BPM: 120 (기본값)", GUILayout.Width(150));
        }
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("세분화:", GUILayout.Width(50));
        _subdivisionIndex = EditorGUILayout.Popup(_subdivisionIndex, SubdivisionLabels, GUILayout.Width(110));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── 세로 비트 타임라인 (1비트 = 1가로줄, 가로로 세분화 슬롯 배치) ──
        int subdivision = SubdivisionOptions[_subdivisionIndex];
        float slotStep = 1f / subdivision; // 한 슬롯의 비트 값

        float cellW = Mathf.Clamp(280f / subdivision, 35f, 75f);
        float cellH = 26f;
        float headerH = 24f;
        float labelW = 60f; // 비트 라벨 너비

        float totalH = headerH + cellH * _maxBeats + 10;

        _timelineScrollPos = EditorGUILayout.BeginScrollView(_timelineScrollPos, GUILayout.ExpandHeight(true));

        Rect areaRect = GUILayoutUtility.GetRect(labelW + cellW * subdivision + 20, totalH);
        EditorGUI.DrawRect(areaRect, new Color(0.13f, 0.13f, 0.16f));

        // 이 삼각형에 대한 스텝 목록
        var mySteps = GetStepsForTile(_selectedTriangle);

        // 헤더 그리기
        Rect labelHeaderRect = new Rect(areaRect.x, areaRect.y, labelW, headerH);
        EditorGUI.DrawRect(labelHeaderRect, new Color(0.18f, 0.18f, 0.22f));
        GUI.Label(labelHeaderRect, "비트", CenteredMiniStyle);

        for (int col = 0; col < subdivision; col++)
        {
            float offset = col * slotStep;
            Rect colHeaderRect = new Rect(areaRect.x + labelW + col * cellW, areaRect.y, cellW - 1, headerH);
            EditorGUI.DrawRect(colHeaderRect, new Color(0.2f, 0.2f, 0.25f));
            string offsetStr = col == 0 ? "+0.0" : $"+{offset:F3}".TrimEnd('0').TrimEnd('.');
            GUI.Label(colHeaderRect, offsetStr, CenteredMiniStyle);
        }

        // 비트별 행 그리기
        for (int b = 0; b < _maxBeats; b++)
        {
            bool isMeasureLine = (b % 4 == 0);

            // 비트 라벨 (좌측)
            Rect labelRect = new Rect(areaRect.x, areaRect.y + headerH + b * cellH, labelW, cellH - 1);
            Color labelBg = isMeasureLine ? new Color(0.28f, 0.28f, 0.38f) : new Color(0.18f, 0.18f, 0.22f);
            EditorGUI.DrawRect(labelRect, labelBg);

            GUIStyle lblStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isMeasureLine ? Color.white : new Color(0.8f, 0.8f, 0.8f) },
                fontSize = isMeasureLine ? 10 : 9,
                fontStyle = isMeasureLine ? FontStyle.Bold : FontStyle.Normal
            };
            GUI.Label(labelRect, $"Beat {b}", lblStyle);

            // 각 세분화 열(셀) 그리기
            for (int col = 0; col < subdivision; col++)
            {
                float beatValue = b + col * slotStep;
                Rect cell = new Rect(areaRect.x + labelW + col * cellW, areaRect.y + headerH + b * cellH, cellW - 1, cellH - 1);

                int stepIdx = FindStepIndex(_selectedTriangle, beatValue);
                if (stepIdx >= 0)
                {
                    // 있는 스텝 — 색상으로 표시
                    Color c = ColorUtil.ToColor(_pattern.steps[stepIdx].color);
                    EditorGUI.DrawRect(cell, c);
                    DrawRectOutline(cell, Color.white, 1.5f);
                }
                else
                {
                    // 빈 셀
                    Color bg;
                    if (isMeasureLine)
                        bg = new Color(0.22f, 0.22f, 0.30f);
                    else
                        bg = (col % 2 == 0) ? new Color(0.17f, 0.17f, 0.19f) : new Color(0.14f, 0.14f, 0.16f);
                    EditorGUI.DrawRect(cell, bg);
                    
                    // 아주 미세한 안쪽 테두리
                    DrawRectOutline(cell, new Color(0.25f, 0.25f, 0.28f, 0.3f), 0.5f);
                }

                // 클릭 감지
                if (Event.current.type == EventType.MouseDown && cell.Contains(Event.current.mousePosition))
                {
                    if (stepIdx >= 0)
                    {
                        RemoveStep(stepIdx);
                    }
                    else if (brushColor != ColorChannel.None)
                    {
                        AddStep(_selectedTriangle, beatValue, brushColor);
                    }
                    Event.current.Use();
                }
            }
        }

        EditorGUILayout.EndScrollView();

        // ── 이 삼각형의 스텝 리스트 ──
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Triangle_{_selectedTriangle}의 스텝:", EditorStyles.boldLabel);

        if (mySteps.Count == 0)
        {
            EditorGUILayout.LabelField("  (없음)", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var s in mySteps)
            {
                EditorGUILayout.BeginHorizontal();
                Rect colorBlock = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
                EditorGUI.DrawRect(colorBlock, ColorUtil.ToColor(s.step.color));
                EditorGUILayout.LabelField($"Beat {s.step.beatOffset:F2}  →  {GetChannelLabel(s.step.color)}", GUILayout.Width(180));
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    RemoveStep(s.globalIndex);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ── 전체 패턴 통계 ──
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("전체 패턴 통계", EditorStyles.boldLabel);
        int totalSteps = _pattern.steps != null ? _pattern.steps.Length : 0;
        int usedTiles = _pattern.steps != null ? _pattern.steps.Select(s => s.triangleIndex).Distinct().Count() : 0;
        float maxBeat = _pattern.MaxBeatOffset;
        EditorGUILayout.LabelField($"  총 스텝: {totalSteps}   |   사용 타일: {usedTiles}/24   |   마지막 비트: {maxBeat:F2}");
    }

    // ════════════════════════════════════════════════════════════
    //  탭 2: 미리보기
    // ════════════════════════════════════════════════════════════

    private void DrawPreviewTab()
    {
        EditorGUILayout.BeginHorizontal();
        
        // ── 좌측: 미리보기 그리드 ──
        EditorGUILayout.BeginVertical(GUILayout.Width(360));
        DrawPreviewGrid();
        EditorGUILayout.EndVertical();

        // ── 우측: 재생 컨트롤 + 스텝 로그 ──
        EditorGUILayout.BeginVertical();
        DrawPreviewControls();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPreviewGrid()
    {
        EditorGUILayout.LabelField("미리보기 그리드", EditorStyles.boldLabel);

        float gridW = 340f, gridH = 340f, scale = 85f;
        Rect gridArea = GUILayoutUtility.GetRect(gridW, gridH);
        EditorGUI.DrawRect(gridArea, new Color(0.1f, 0.1f, 0.12f));

        Vector2 center = new Vector2(gridArea.x + gridW * 0.5f, gridArea.y + gridH * 0.45f);

        Handles.BeginGUI();
        foreach (var tri in Triangles)
        {
            float sx = center.x + tri.x * scale;
            float sy = center.y - tri.y * scale;
            float triSize = scale * 0.85f;
            Vector3[] vertices = GetTriangleVertices(sx, sy, triSize, tri.flipped);

            _previewTileStates.TryGetValue(tri.index, out ColorChannel tileColor);
            Color fillColor = tileColor != ColorChannel.None
                ? ColorUtil.ToColor(tileColor)
                : new Color(0.2f, 0.2f, 0.24f);

            Color strokeColor = new Color(0.35f, 0.35f, 0.4f);
            float strokeWidth = 1.5f;

            // 실제 삼각형 그리기
            DrawTriangle(vertices, fillColor, strokeColor, strokeWidth);

            // 텍스트 가독성을 위한 휘도 계산
            float luminance = 0.2126f * fillColor.r + 0.7152f * fillColor.g + 0.0722f * fillColor.b;
            Color textColor = luminance > 0.5f ? Color.black : Color.white;

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor },
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            Rect labelRect = new Rect(sx - 20f, sy - 10f, 40f, 20f);
            string orderLabel = GetTriangleOrderLabel(tri.index);
            GUI.Label(labelRect, orderLabel, labelStyle);
        }
        Handles.EndGUI();
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.LabelField("재생 컨트롤", EditorStyles.boldLabel);

        // BPM (곡 SO 기준 읽기 전용으로 표시)
        EditorGUILayout.BeginHorizontal();
        if (_songData != null)
        {
            EditorGUILayout.LabelField($"BPM: {_songData.bpm} (곡 SO 기준)", GUILayout.Width(200));
        }
        else
        {
            EditorGUILayout.LabelField("BPM: 120 (기본값)", GUILayout.Width(200));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 재생/정지/리셋 버튼
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(_isPreviewPlaying ? "⏸ 정지" : "▶ 재생", GUILayout.Height(30)))
        {
            if (_isPreviewPlaying)
            {
                _isPreviewPlaying = false;
            }
            else
            {
                StartPreview();
            }
        }
        if (GUILayout.Button("⏹ 리셋", GUILayout.Height(30)))
        {
            ResetPreview();
        }
        EditorGUILayout.EndHorizontal();

        // 현재 시간/비트 표시
        float secPerBeat = 60f / _previewBpm;
        float currentBeat = _previewTime / secPerBeat;

        EditorGUILayout.Space(4);

        // 프로그레스 바
        float maxBeat = _pattern.MaxBeatOffset;
        float progress = maxBeat > 0 ? Mathf.Clamp01(currentBeat / (maxBeat + 1)) : 0f;
        Rect progressRect = GUILayoutUtility.GetRect(0, 20);
        EditorGUI.DrawRect(progressRect, new Color(0.15f, 0.15f, 0.15f));
        Rect fillRect = new Rect(progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height);
        EditorGUI.DrawRect(fillRect, new Color(0.2f, 0.6f, 0.9f));

        GUIStyle progressLabel = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Label(progressRect, $"Beat {currentBeat:F1}  /  {maxBeat:F1}", progressLabel);

        // 미리보기 업데이트
        if (_isPreviewPlaying)
        {
            _previewTime = (float)(EditorApplication.timeSinceStartup - _previewStartRealTime);
            UpdatePreviewState();

            // 패턴 끝 도달 시 자동 정지
            if (currentBeat > maxBeat + 1)
            {
                _isPreviewPlaying = false;
            }
        }

        // ── 스텝 로그 ──
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("스텝 순서", EditorStyles.boldLabel);

        if (_pattern.steps != null && _pattern.steps.Length > 0)
        {
            var sorted = _pattern.steps
                .OrderBy(s => s.beatOffset)
                .ThenBy(s => s.triangleIndex)
                .ToArray();

            _previewScrollPos = EditorGUILayout.BeginScrollView(_previewScrollPos, GUILayout.Height(200));
            foreach (var step in sorted)
            {
                bool isPast = currentBeat >= step.beatOffset;
                
                EditorGUILayout.BeginHorizontal();
                
                // 재생 상태 마커
                string marker = isPast ? "✓" : "  ";
                EditorGUILayout.LabelField(marker, GUILayout.Width(16));

                // 색상 블록
                Rect colorBlock = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
                EditorGUI.DrawRect(colorBlock, ColorUtil.ToColor(step.color));

                // 정보
                GUIStyle stepStyle = new GUIStyle(EditorStyles.label);
                if (isPast) stepStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
                
                EditorGUILayout.LabelField(
                    $"Beat {step.beatOffset:F2}  |  T{step.triangleIndex:D2}  |  {GetChannelLabel(step.color)}",
                    stepStyle);

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void StartPreview()
    {
        _isPreviewPlaying = true;
        _previewStartRealTime = EditorApplication.timeSinceStartup;
        _previewTime = 0f;
        _previewTileStates.Clear();
    }

    private void ResetPreview()
    {
        _isPreviewPlaying = false;
        _previewTime = 0f;
        _previewTileStates.Clear();
    }

    private void UpdatePreviewState()
    {
        if (_pattern.steps == null) return;

        float secPerBeat = 60f / _previewBpm;
        float currentBeat = _previewTime / secPerBeat;

        _previewTileStates.Clear();
        foreach (var step in _pattern.steps)
        {
            if (currentBeat >= step.beatOffset)
            {
                if (_previewTileStates.ContainsKey(step.triangleIndex))
                    _previewTileStates[step.triangleIndex] |= step.color;
                else
                    _previewTileStates[step.triangleIndex] = step.color;
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    //  유틸리티
    // ════════════════════════════════════════════════════════════

    /// <summary> 타일이 채색되는 순서 라벨을 구합니다. (동일 삼각형 다중 채색 시 화살표로 연결) </summary>
    private string GetTriangleOrderLabel(int tileIndex)
    {
        if (_pattern == null || _pattern.steps == null || _pattern.steps.Length == 0)
            return "";

        // 모든 스텝을 beatOffset 기준으로 1차 정렬, 같으면 인덱스 순 정렬
        var sortedSteps = _pattern.steps
            .OrderBy(s => s.beatOffset)
            .ThenBy(s => s.triangleIndex)
            .ToList();

        // 1-indexed 순서를 채집
        List<int> orders = new List<int>();
        for (int i = 0; i < sortedSteps.Count; i++)
        {
            if (sortedSteps[i].triangleIndex == tileIndex)
            {
                orders.Add(i + 1);
            }
        }

        if (orders.Count == 0)
            return "";

        return string.Join("→", orders);
    }

    private ColorChannel GetTileColorFromPattern(int tileIndex)
    {
        if (_pattern == null || _pattern.steps == null) return ColorChannel.None;
        ColorChannel result = ColorChannel.None;
        foreach (var step in _pattern.steps)
        {
            if (step.triangleIndex == tileIndex)
                result |= step.color;
        }
        return result;
    }

    private struct StepRef
    {
        public PatternStep step;
        public int globalIndex;
    }

    private List<StepRef> GetStepsForTile(int tileIndex)
    {
        var list = new List<StepRef>();
        if (_pattern.steps == null) return list;
        for (int i = 0; i < _pattern.steps.Length; i++)
        {
            if (_pattern.steps[i].triangleIndex == tileIndex)
                list.Add(new StepRef { step = _pattern.steps[i], globalIndex = i });
        }
        list.Sort((a, b) => a.step.beatOffset.CompareTo(b.step.beatOffset));
        return list;
    }

    private int FindStepIndex(int tileIndex, float beatOffset)
    {
        if (_pattern.steps == null) return -1;
        for (int i = 0; i < _pattern.steps.Length; i++)
        {
            if (_pattern.steps[i].triangleIndex == tileIndex &&
                Mathf.Approximately(_pattern.steps[i].beatOffset, beatOffset))
                return i;
        }
        return -1;
    }

    private void AddStep(int tileIndex, float beatOffset, ColorChannel color)
    {
        Undo.RecordObject(_pattern, "Add Step");
        var list = new List<PatternStep>(_pattern.steps ?? new PatternStep[0]);
        list.Add(new PatternStep { triangleIndex = tileIndex, color = color, beatOffset = beatOffset });
        list.Sort((a, b) => a.beatOffset.CompareTo(b.beatOffset));
        _pattern.steps = list.ToArray();
        EditorUtility.SetDirty(_pattern);
    }

    private void RemoveStep(int index)
    {
        Undo.RecordObject(_pattern, "Remove Step");
        var list = new List<PatternStep>(_pattern.steps);
        if (index >= 0 && index < list.Count)
        {
            list.RemoveAt(index);
            _pattern.steps = list.ToArray();
        }
        EditorUtility.SetDirty(_pattern);
    }

    private void ClearStepsForTile(int tileIndex)
    {
        if (_pattern == null || _pattern.steps == null) return;

        Undo.RecordObject(_pattern, "Clear Tile Steps");
        var list = new List<PatternStep>(_pattern.steps);
        list.RemoveAll(s => s.triangleIndex == tileIndex);
        _pattern.steps = list.ToArray();
        EditorUtility.SetDirty(_pattern);
    }

    private void MoveStepsToTile(int fromTile, int toTile)
    {
        if (_pattern == null || _pattern.steps == null) return;

        Undo.RecordObject(_pattern, "Swap Tile Steps");

        var list = new List<PatternStep>(_pattern.steps);
        bool changed = false;

        for (int i = 0; i < list.Count; i++)
        {
            var step = list[i];
            if (step.triangleIndex == fromTile)
            {
                step.triangleIndex = toTile;
                list[i] = step;
                changed = true;
            }
            else if (step.triangleIndex == toTile)
            {
                step.triangleIndex = fromTile;
                list[i] = step;
                changed = true;
            }
        }

        if (changed)
        {
            _pattern.steps = list.ToArray();
            EditorUtility.SetDirty(_pattern);
        }
    }

    private ColorChannel GetBrushChannel()
    {
        ColorChannel c = ColorChannel.None;
        if (_brushR) c |= ColorChannel.Red;
        if (_brushG) c |= ColorChannel.Green;
        if (_brushB) c |= ColorChannel.Blue;
        return c;
    }

    private string GetChannelLabel(ColorChannel ch)
    {
        switch (ch)
        {
            case ColorChannel.None: return "없음";
            case ColorChannel.Red: return "빨강";
            case ColorChannel.Blue: return "파랑";
            case ColorChannel.Green: return "초록";
            case ColorChannel.Red | ColorChannel.Blue: return "마젠타";
            case ColorChannel.Red | ColorChannel.Green: return "옐로우";
            case ColorChannel.Blue | ColorChannel.Green: return "시안";
            case ColorChannel.Red | ColorChannel.Blue | ColorChannel.Green: return "화이트";
            default: return ch.ToString();
        }
    }

    private GUIStyle _centeredMiniStyle;
    private GUIStyle CenteredMiniStyle
    {
        get
        {
            if (_centeredMiniStyle == null)
            {
                _centeredMiniStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }
            return _centeredMiniStyle;
        }
    }

    private Vector3[] GetTriangleVertices(float sx, float sy, float s, bool flipped)
    {
        float h = s * Mathf.Sqrt(3f) / 2f;
        float offsetTop = 2f / 3f * h;
        float offsetBottom = 1f / 3f * h;

        Vector3[] vertices = new Vector3[3];
        if (!flipped)
        {
            // Upward triangle ▲
            vertices[0] = new Vector3(sx, sy - offsetTop, 0f);
            vertices[1] = new Vector3(sx - s * 0.5f, sy + offsetBottom, 0f);
            vertices[2] = new Vector3(sx + s * 0.5f, sy + offsetBottom, 0f);
        }
        else
        {
            // Downward triangle ▽
            vertices[0] = new Vector3(sx, sy + offsetTop, 0f);
            vertices[1] = new Vector3(sx - s * 0.5f, sy - offsetBottom, 0f);
            vertices[2] = new Vector3(sx + s * 0.5f, sy - offsetBottom, 0f);
        }
        return vertices;
    }

    private void DrawTriangle(Vector3[] vertices, Color fillColor, Color strokeColor, float strokeWidth)
    {
        // Fill
        Handles.color = fillColor;
        Handles.DrawAAConvexPolygon(vertices);

        // Outline
        if (strokeWidth > 0f)
        {
            Handles.color = strokeColor;
            Vector3[] outlinePoints = new Vector3[4];
            outlinePoints[0] = vertices[0];
            outlinePoints[1] = vertices[1];
            outlinePoints[2] = vertices[2];
            outlinePoints[3] = vertices[0];
            Handles.DrawAAPolyLine(strokeWidth, outlinePoints);
        }
    }

    private bool IsPointInTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        float s = (p0.x - p2.x) * (p.y - p2.y) - (p0.y - p2.y) * (p.x - p2.x);
        float t = (p1.x - p0.x) * (p.y - p0.y) - (p1.y - p0.y) * (p.x - p0.x);

        if ((s < 0) != (t < 0) && s != 0 && t != 0)
            return false;

        float d = (p2.x - p1.x) * (p.y - p1.y) - (p2.y - p1.y) * (p.x - p1.x);
        return d == 0 || (d < 0) == (s + t <= 0);
    }

    private void DrawRectOutline(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
