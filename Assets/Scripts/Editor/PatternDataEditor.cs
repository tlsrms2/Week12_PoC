using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// PatternData용 커스텀 Inspector
/// 비트 타임라인 시각화, 클릭으로 스텝 추가/제거, 색상 팔레트 제공
/// </summary>
[CustomEditor(typeof(PatternData))]
public class PatternDataEditor : Editor
{
    // 브러시 색상 플래그
    private bool _brushRed = true;
    private bool _brushGreen = false;
    private bool _brushBlue = false;

    // 타임라인 설정
    private const float CELL_WIDTH = 28f;
    private const float CELL_HEIGHT = 18f;
    private const float LABEL_WIDTH = 40f;
    private const int MAX_TILES = 24;
    private const float TIMELINE_HEADER_HEIGHT = 22f;

    // 스크롤 위치
    private Vector2 _scrollPos;
    
    // 표시할 비트 수
    private int _visibleBeats = 16;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        PatternData pattern = (PatternData)target;

        // --- 기본 정보 ---
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("패턴 정보", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("patternName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("graceTime"));
        if (pattern.graceTime < 0)
        {
            EditorGUILayout.HelpBox("유예 시간이 -1이므로 SongData의 기본 graceTime을 사용합니다.", MessageType.Info);
        }

        EditorGUILayout.Space(8);

        // --- 브러시 팔레트 ---
        DrawBrushPalette();

        EditorGUILayout.Space(8);

        // --- 비트 타임라인 ---
        DrawBeatTimeline(pattern);

        EditorGUILayout.Space(8);

        // --- 스텝 리스트 ---
        EditorGUILayout.LabelField("스텝 리스트", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("steps"), true);

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 색상 브러시 팔레트 UI
    /// </summary>
    private void DrawBrushPalette()
    {
        EditorGUILayout.LabelField("🎨 브러시", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // R 토글
        GUI.backgroundColor = _brushRed ? Color.red : Color.gray;
        if (GUILayout.Toggle(_brushRed, "R", "Button", GUILayout.Width(40)) != _brushRed)
            _brushRed = !_brushRed;

        // G 토글
        GUI.backgroundColor = _brushGreen ? Color.green : Color.gray;
        if (GUILayout.Toggle(_brushGreen, "G", "Button", GUILayout.Width(40)) != _brushGreen)
            _brushGreen = !_brushGreen;

        // B 토글
        GUI.backgroundColor = _brushBlue ? new Color(0.3f, 0.3f, 1f) : Color.gray;
        if (GUILayout.Toggle(_brushBlue, "B", "Button", GUILayout.Width(40)) != _brushBlue)
            _brushBlue = !_brushBlue;

        GUI.backgroundColor = Color.white;

        // 현재 브러시 색 미리보기
        ColorChannel brushChannel = GetBrushChannel();
        Color previewColor = brushChannel != ColorChannel.None ? ColorUtil.ToColor(brushChannel) : Color.gray;
        
        EditorGUILayout.Space(8);
        Rect previewRect = GUILayoutUtility.GetRect(24, 18);
        EditorGUI.DrawRect(previewRect, previewColor);
        EditorGUILayout.LabelField(GetChannelLabel(brushChannel), GUILayout.Width(80));

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 비트 타임라인 격자 UI
    /// 가로축: 비트, 세로축: 타일 인덱스(1~24)
    /// </summary>
    private void DrawBeatTimeline(PatternData pattern)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("📊 타임라인", EditorStyles.boldLabel);
        _visibleBeats = EditorGUILayout.IntSlider(_visibleBeats, 4, 64);
        EditorGUILayout.EndHorizontal();

        // 스텝 데이터를 (tileIndex, beatOffset) 기반 룩업으로 변환
        var stepLookup = new Dictionary<(int tile, float beat), int>(); // value = steps 배열 인덱스
        if (pattern.steps != null)
        {
            for (int i = 0; i < pattern.steps.Length; i++)
            {
                var step = pattern.steps[i];
                var key = (step.triangleIndex, step.beatOffset);
                stepLookup[key] = i;
            }
        }

        float totalWidth = LABEL_WIDTH + CELL_WIDTH * _visibleBeats + 20f;
        float totalHeight = TIMELINE_HEADER_HEIGHT + CELL_HEIGHT * MAX_TILES + 20f;

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, 
            GUILayout.Height(Mathf.Min(totalHeight, 480f)));

        // 전체 영역 확보
        Rect areaRect = GUILayoutUtility.GetRect(totalWidth, totalHeight);
        
        // 배경
        EditorGUI.DrawRect(areaRect, new Color(0.15f, 0.15f, 0.15f));

        // --- 비트 헤더 (가로축) ---
        for (int b = 0; b < _visibleBeats; b++)
        {
            Rect headerCell = new Rect(
                areaRect.x + LABEL_WIDTH + b * CELL_WIDTH,
                areaRect.y,
                CELL_WIDTH,
                TIMELINE_HEADER_HEIGHT
            );
            
            // 4비트마다 강조
            Color headerBg = (b % 4 == 0) ? new Color(0.3f, 0.3f, 0.4f) : new Color(0.22f, 0.22f, 0.22f);
            EditorGUI.DrawRect(headerCell, headerBg);
            
            GUI.Label(headerCell, b.ToString(), GetCenteredMiniStyle());
        }

        // --- 타일 행 (세로축) ---
        for (int t = 1; t <= MAX_TILES; t++)
        {
            float rowY = areaRect.y + TIMELINE_HEADER_HEIGHT + (t - 1) * CELL_HEIGHT;

            // 타일 인덱스 라벨
            Rect labelRect = new Rect(areaRect.x, rowY, LABEL_WIDTH, CELL_HEIGHT);
            Color labelBg = (t % 2 == 0) ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.2f, 0.2f, 0.2f);
            EditorGUI.DrawRect(labelRect, labelBg);
            GUI.Label(labelRect, $"T{t:D2}", GetCenteredMiniStyle());

            // 비트 셀
            for (int b = 0; b < _visibleBeats; b++)
            {
                Rect cellRect = new Rect(
                    areaRect.x + LABEL_WIDTH + b * CELL_WIDTH,
                    rowY,
                    CELL_WIDTH - 1f,
                    CELL_HEIGHT - 1f
                );

                var key = (t, (float)b);
                bool hasStep = stepLookup.ContainsKey(key);

                if (hasStep)
                {
                    // 기존 스텝 표시 — 해당 색상으로 셀 채우기
                    int idx = stepLookup[key];
                    ColorChannel stepColor = pattern.steps[idx].color;
                    Color displayColor = ColorUtil.ToColor(stepColor);
                    EditorGUI.DrawRect(cellRect, displayColor);
                    
                    // 셀 내부에 작은 마크 (어두운 아웃라인)
                    Rect innerRect = new Rect(cellRect.x + 1, cellRect.y + 1, cellRect.width - 2, cellRect.height - 2);
                    Color outlineColor = new Color(displayColor.r * 0.5f, displayColor.g * 0.5f, displayColor.b * 0.5f);
                    DrawRectOutline(innerRect, outlineColor);
                }
                else
                {
                    // 빈 셀
                    Color cellBg;
                    if (b % 4 == 0)
                        cellBg = new Color(0.25f, 0.25f, 0.3f);
                    else
                        cellBg = (t % 2 == 0) ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.2f, 0.2f, 0.2f);
                    EditorGUI.DrawRect(cellRect, cellBg);
                }

                // 클릭 감지
                if (Event.current.type == EventType.MouseDown && cellRect.Contains(Event.current.mousePosition))
                {
                    if (hasStep)
                    {
                        // 기존 스텝 제거
                        RemoveStep(pattern, stepLookup[key]);
                    }
                    else
                    {
                        // 새 스텝 추가
                        ColorChannel brush = GetBrushChannel();
                        if (brush != ColorChannel.None)
                        {
                            AddStep(pattern, t, b, brush);
                        }
                    }
                    Event.current.Use();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 스텝 추가
    /// </summary>
    private void AddStep(PatternData pattern, int tileIndex, float beatOffset, ColorChannel color)
    {
        Undo.RecordObject(pattern, "Add Pattern Step");
        
        var stepList = new List<PatternStep>(pattern.steps ?? new PatternStep[0]);
        stepList.Add(new PatternStep
        {
            triangleIndex = tileIndex,
            color = color,
            beatOffset = beatOffset
        });
        
        // beatOffset 기준 정렬
        stepList.Sort((a, b) => a.beatOffset.CompareTo(b.beatOffset));
        pattern.steps = stepList.ToArray();
        
        EditorUtility.SetDirty(pattern);
    }

    /// <summary>
    /// 스텝 제거
    /// </summary>
    private void RemoveStep(PatternData pattern, int index)
    {
        Undo.RecordObject(pattern, "Remove Pattern Step");
        
        var stepList = new List<PatternStep>(pattern.steps);
        if (index >= 0 && index < stepList.Count)
        {
            stepList.RemoveAt(index);
            pattern.steps = stepList.ToArray();
        }
        
        EditorUtility.SetDirty(pattern);
    }

    /// <summary>
    /// 현재 브러시의 ColorChannel 반환
    /// </summary>
    private ColorChannel GetBrushChannel()
    {
        ColorChannel channel = ColorChannel.None;
        if (_brushRed) channel |= ColorChannel.Red;
        if (_brushGreen) channel |= ColorChannel.Green;
        if (_brushBlue) channel |= ColorChannel.Blue;
        return channel;
    }

    /// <summary>
    /// ColorChannel의 한글 라벨
    /// </summary>
    private string GetChannelLabel(ColorChannel channel)
    {
        switch (channel)
        {
            case ColorChannel.None: return "없음";
            case ColorChannel.Red: return "빨강";
            case ColorChannel.Blue: return "파랑";
            case ColorChannel.Green: return "초록";
            case ColorChannel.Red | ColorChannel.Blue: return "마젠타";
            case ColorChannel.Red | ColorChannel.Green: return "옐로우";
            case ColorChannel.Blue | ColorChannel.Green: return "시안";
            case ColorChannel.Red | ColorChannel.Blue | ColorChannel.Green: return "화이트";
            default: return channel.ToString();
        }
    }

    /// <summary>
    /// 중앙 정렬 미니 스타일
    /// </summary>
    private GUIStyle _centeredMiniStyle;
    private GUIStyle GetCenteredMiniStyle()
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

    /// <summary>
    /// 사각형 아웃라인 그리기
    /// </summary>
    private void DrawRectOutline(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);           // 상단
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);    // 하단
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);           // 좌측
        EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);   // 우측
    }
}
