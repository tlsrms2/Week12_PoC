using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// 곡 타임라인 에디터 윈도우
/// SongData SO의 patternEntries를 시각적 타임라인으로 배치·편집하는 커스텀 EditorWindow.
/// 비트 단위 가로축 타임라인에 패턴 블록을 배치하고, 드래그로 위치 조정,
/// 우클릭 삭제, Object Picker를 통한 새 패턴 추가 등을 지원합니다.
/// </summary>
public class SongTimelineWindow : EditorWindow
{
    // ════════════════════════════════════════════════════════════
    //  상수
    // ════════════════════════════════════════════════════════════

    /// <summary>배경 색상</summary>
    private static readonly Color BgColor = new Color(0.12f, 0.12f, 0.15f);
    /// <summary>룰러 배경 색상</summary>
    private static readonly Color RulerBgColor = new Color(0.16f, 0.16f, 0.20f);
    /// <summary>일반 비트 그리드 라인 색상</summary>
    private static readonly Color GridLineColor = new Color(0.25f, 0.25f, 0.30f, 0.5f);
    /// <summary>4비트 마다 강조되는 그리드 라인 색상</summary>
    private static readonly Color GridLineAccentColor = new Color(0.40f, 0.40f, 0.50f, 0.7f);
    /// <summary>룰러 텍스트 색상</summary>
    private static readonly Color RulerTextColor = new Color(0.7f, 0.7f, 0.75f);
    /// <summary>선택된 블록의 아웃라인 색상</summary>
    private static readonly Color SelectionOutlineColor = new Color(1f, 0.92f, 0.23f, 1f);
    /// <summary>디테일 패널 배경 색상</summary>
    private static readonly Color DetailBgColor = new Color(0.15f, 0.15f, 0.18f);
    /// <summary>유예 시간 영역 오버레이 색상 (반투명 스트라이프)</summary>
    private static readonly Color GraceOverlayColor = new Color(0f, 0f, 0f, 0.35f);

    /// <summary>룰러 높이 (픽셀)</summary>
    private const float RulerHeight = 24f;
    /// <summary>패턴 레인 상단 패딩</summary>
    private const float LaneTopPadding = 12f;
    /// <summary>패턴 블록 높이</summary>
    private const float BlockHeight = 180f;
    /// <summary>디테일 패널 높이</summary>
    private const float DetailPanelHeight = 110f;
    /// <summary>정보 바 높이</summary>
    private const float InfoBarHeight = 22f;
    /// <summary>파형(Waveform) 레인 높이</summary>
    private const float WaveformLaneHeight = 80f;
    /// <summary>오디오 컨트롤 바 높이</summary>
    private const float AudioControlBarHeight = 28f;
    /// <summary>최소 비트/픽셀 줌 값</summary>
    private const float MinPixelsPerBeat = 10f;
    /// <summary>최대 비트/픽셀 줌 값</summary>
    private const float MaxPixelsPerBeat = 200f;
    /// <summary>선택 아웃라인 두께</summary>
    private const float SelectionOutlineThickness = 2f;
    /// <summary>오브젝트 피커 컨트롤 ID</summary>
    private const int ObjectPickerControlID = 19283746;

    // ── 파형/오디오 관련 색상 ──
    private static readonly Color WaveformBgColor = new Color(0.10f, 0.10f, 0.13f);
    private static readonly Color WaveformColor = new Color(0.30f, 0.65f, 0.95f, 0.85f);
    private static readonly Color WaveformPeakColor = new Color(0.50f, 0.80f, 1.0f, 0.95f);
    private static readonly Color PlayheadColor = new Color(1f, 0.35f, 0.25f, 1f);
    private static readonly Color AudioControlBgColor = new Color(0.14f, 0.14f, 0.18f);

    // ════════════════════════════════════════════════════════════
    //  상태
    // ════════════════════════════════════════════════════════════

    /// <summary>현재 편집 중인 SongData</summary>
    private SongData _songData;
    /// <summary>비트 당 픽셀 수 (줌 레벨)</summary>
    private float _pixelsPerBeat = 50f;
    /// <summary>타임라인 수평 스크롤 오프셋 (비트 단위)</summary>
    private float _scrollOffsetBeats = 0f;
    /// <summary>현재 선택된 PatternEntry 인덱스 (-1 = 선택 없음)</summary>
    private int _selectedEntryIndex = -1;

    // ── 드래그 상태 ──
    /// <summary>드래그 중 여부</summary>
    private bool _isDragging = false;
    /// <summary>드래그 시작 시 마우스 비트 위치</summary>
    private float _dragStartBeat;
    /// <summary>드래그 시작 시 블록의 원래 startBeat</summary>
    private float _dragOriginalStartBeat;

    // ── 패닝 상태 ──
    /// <summary>중간 마우스 버튼 패닝 중 여부</summary>
    private bool _isPanning = false;
    /// <summary>패닝 시작 시 마우스 X 위치</summary>
    private float _panStartMouseX;
    /// <summary>패닝 시작 시 스크롤 오프셋</summary>
    private float _panStartScrollOffset;

    // ── 오브젝트 피커 ──
    /// <summary>오브젝트 피커로 패턴을 추가할 때의 비트 위치</summary>
    private float _pendingAddBeat = -1f;

    // ── 오디오 재생 상태 ──
    /// <summary>에디터 오디오 재생 중 여부</summary>
    private bool _isAudioPlaying = false;
    /// <summary>현재 재생 위치 (초)</summary>
    private float _audioPlaybackTime = 0f;
    /// <summary>재생 시작 시 EditorApplication.timeSinceStartup 기록</summary>
    private double _audioPlayStartRealTime;
    /// <summary>재생 시작 시점의 곡 시간 (초), 중간 재개용</summary>
    private float _audioPlayStartSongTime = 0f;

    // ── 파형 캐시 ──
    /// <summary>캐시된 파형 텍스처 (AudioClip 변경 시 재생성)</summary>
    private Texture2D _waveformTexture;
    /// <summary>파형 캐시가 어떤 AudioClip에서 만들어졌는지 추적</summary>
    private AudioClip _cachedWaveformClip;
    /// <summary>파형 캐시 너비 (재생성 판단용)</summary>
    private int _cachedWaveformWidth;

    // ── 캐시된 GUIStyle ──
    private GUIStyle _rulerLabelStyle;
    private GUIStyle _blockLabelStyle;
    private GUIStyle _detailHeaderStyle;
    private GUIStyle _placeholderStyle;
    private GUIStyle _audioTimeLabelStyle;

    // ════════════════════════════════════════════════════════════
    //  메뉴 항목 / 초기화
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 RhythmPuzzle > 곡 타임라인 에디터 (단축키: Ctrl+Shift+T)로 윈도우를 엽니다.
    /// </summary>
    [MenuItem("RhythmPuzzle/곡 타임라인 에디터 %#t")]
    public static void ShowWindow()
    {
        var window = GetWindow<SongTimelineWindow>("곡 타임라인");
        window.minSize = new Vector2(750, 600);
    }

    /// <summary>
    /// 외부에서 특정 SongData를 열어주는 편의 메서드
    /// </summary>
    /// <param name="song">편집할 SongData</param>
    public static void OpenWithSong(SongData song)
    {
        var window = GetWindow<SongTimelineWindow>("곡 타임라인");
        window._songData = song;
        window.minSize = new Vector2(750, 600);
        window.Repaint();
    }

    /// <summary>
    /// 윈도우 활성화 시 호출. EditorApplication.update에 리페인트를 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        wantsMouseMove = true;
        EditorApplication.update += OnEditorUpdate;
    }

    /// <summary>
    /// 윈도우 비활성화 시 호출. 오디오 정지 및 업데이트 해제.
    /// </summary>
    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        StopAudioPlayback();
    }

    /// <summary>
    /// 에디터 업데이트 루프. 재생 중일 때 지속적으로 리페인트합니다.
    /// </summary>
    private void OnEditorUpdate()
    {
        if (_isAudioPlaying)
        {
            Repaint();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  GUIStyle 초기화
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 필요한 GUIStyle들을 지연 생성합니다.
    /// EditorStyles는 OnGUI 내에서만 안전하게 접근할 수 있으므로 여기서 초기화합니다.
    /// </summary>
    private void EnsureStyles()
    {
        if (_rulerLabelStyle == null)
        {
            _rulerLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = RulerTextColor },
                fontSize = 9
            };
        }

        if (_blockLabelStyle == null)
        {
            _blockLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
        }

        if (_detailHeaderStyle == null)
        {
            _detailHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.9f, 0.95f) }
            };
        }

        if (_placeholderStyle == null)
        {
            _placeholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 12
            };
        }

        if (_audioTimeLabelStyle == null)
        {
            _audioTimeLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.85f) },
                fontSize = 10
            };
        }
    }

    // ════════════════════════════════════════════════════════════
    //  OnGUI - 메인 렌더링
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// EditorWindow의 메인 GUI 콜백.
    /// 툴바, 정보 바, 타임라인, 디테일 패널을 순서대로 렌더링합니다.
    /// </summary>
    private void OnGUI()
    {
        EnsureStyles();

        DrawToolbar();
        DrawInfoBar();

        if (_songData == null)
        {
            EditorGUILayout.HelpBox("위 툴바에서 SongData SO를 선택하세요.", MessageType.Info);
            return;
        }

        // 오디오 재생 시간 업데이트
        UpdateAudioPlaybackTime();

        // 레이아웃 영역 계산: 타임라인 + 파형 레인 + 오디오 컨트롤 + 디테일 패널
        float lastY = GUILayoutUtility.GetLastRect().yMax;
        float bottomReserved = DetailPanelHeight + WaveformLaneHeight + AudioControlBarHeight;
        float timelineH = Mathf.Max(100f, position.height - lastY - bottomReserved);

        Rect timelineRect = new Rect(0, lastY, position.width, timelineH);
        Rect audioControlRect = new Rect(0, timelineRect.yMax, position.width, AudioControlBarHeight);
        Rect waveformRect = new Rect(0, audioControlRect.yMax, position.width, WaveformLaneHeight);
        Rect detailRect = new Rect(0, waveformRect.yMax, position.width, DetailPanelHeight);

        DrawTimeline(timelineRect);
        DrawAudioControlBar(audioControlRect);
        DrawWaveformLane(waveformRect);
        DrawDetailPanel(detailRect);

        // 오브젝트 피커 결과 처리
        HandleObjectPickerResult();

        // 키보드 이벤트 (Delete, Space)
        HandleKeyboardEvents();
    }

    // ════════════════════════════════════════════════════════════
    //  툴바
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 상단 툴바를 그립니다. SongData 오브젝트 필드를 포함합니다.
    /// </summary>
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUILayout.LabelField("곡 데이터:", GUILayout.Width(60));
        var newSong = (SongData)EditorGUILayout.ObjectField(
            _songData, typeof(SongData), false, GUILayout.Width(250));

        if (newSong != _songData)
        {
            _songData = newSong;
            _selectedEntryIndex = -1;
            _scrollOffsetBeats = 0f;
        }

        GUILayout.FlexibleSpace();

        if (_songData != null && GUILayout.Button("정렬", EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            SortEntries();
        }

        GUILayout.FlexibleSpace();

        // 오디오 유무 표시
        if (_songData != null && _songData.bgmClip != null)
        {
            GUILayout.Label($"♪ {_songData.bgmClip.name}", EditorStyles.toolbarButton, GUILayout.Width(140));
        }

        EditorGUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════════
    //  정보 바
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// BPM, 총 비트 수, 줌 슬라이더를 표시하는 정보 바를 그립니다.
    /// </summary>
    private void DrawInfoBar()
    {
        Rect barRect = GUILayoutUtility.GetRect(position.width, InfoBarHeight);
        EditorGUI.DrawRect(barRect, new Color(0.18f, 0.18f, 0.22f));

        if (_songData == null) return;

        float x = barRect.x + 8f;
        float y = barRect.y + 2f;
        float h = InfoBarHeight - 4f;

        // BPM 표시
        GUI.Label(new Rect(x, y, 100, h),
            $"BPM: {_songData.bpm:F0}", EditorStyles.miniLabel);
        x += 100f;

        // 총 비트 수
        float totalBeats = CalcTotalBeats();
        GUI.Label(new Rect(x, y, 120, h),
            $"총 비트: {totalBeats:F1}", EditorStyles.miniLabel);
        x += 120f;

        // 패턴 수
        int entryCount = _songData.patternEntries != null ? _songData.patternEntries.Length : 0;
        GUI.Label(new Rect(x, y, 100, h),
            $"패턴 수: {entryCount}", EditorStyles.miniLabel);
        x += 100f;

        // 줌 슬라이더
        GUI.Label(new Rect(barRect.xMax - 260, y, 40, h), "줌:", EditorStyles.miniLabel);
        _pixelsPerBeat = GUI.HorizontalSlider(
            new Rect(barRect.xMax - 220, y + 2, 140, h),
            _pixelsPerBeat, MinPixelsPerBeat, MaxPixelsPerBeat);

        GUI.Label(new Rect(barRect.xMax - 72, y, 68, h),
            $"{_pixelsPerBeat:F0} px/b", EditorStyles.miniLabel);
    }

    // ════════════════════════════════════════════════════════════
    //  타임라인 메인 영역
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 타임라인 메인 영역을 렌더링합니다.
    /// 배경, 비트 그리드, 룰러, 패턴 블록을 순서대로 그리고
    /// 마우스 인터랙션(패닝, 줌, 드래그, 클릭)을 처리합니다.
    /// </summary>
    /// <param name="rect">타임라인 영역 Rect</param>
    private void DrawTimeline(Rect rect)
    {
        // 클리핑
        GUI.BeginClip(rect);
        Rect localRect = new Rect(0, 0, rect.width, rect.height);

        // 배경
        EditorGUI.DrawRect(localRect, BgColor);

        // 비트 그리드 + 룰러
        DrawBeatGrid(localRect);

        // 패턴 블록 렌더링
        DrawPatternBlocks(localRect);

        // 재생 중 플레이헤드 그리기
        DrawPlayhead(localRect);

        // 마우스 인터랙션
        HandleTimelineInput(localRect, rect);

        GUI.EndClip();
    }

    /// <summary>
    /// 비트 그리드 라인과 상단 룰러(비트 번호)를 그립니다.
    /// 4비트마다 굵은 라인, 그 외 얇은 라인을 표시합니다.
    /// </summary>
    /// <param name="rect">로컬 좌표계 Rect</param>
    private void DrawBeatGrid(Rect rect)
    {
        // 룰러 배경
        Rect rulerRect = new Rect(0, 0, rect.width, RulerHeight);
        EditorGUI.DrawRect(rulerRect, RulerBgColor);

        // 보이는 비트 범위 계산
        float startBeat = Mathf.Floor(_scrollOffsetBeats);
        float endBeat = _scrollOffsetBeats + rect.width / _pixelsPerBeat;

        Handles.BeginGUI();
        for (float beat = startBeat; beat <= endBeat + 1; beat++)
        {
            float x = BeatToLocalX(beat);
            if (x < 0 || x > rect.width) continue;

            bool isMajor = Mathf.Approximately(beat % 4, 0);
            Color lineColor = isMajor ? GridLineAccentColor : GridLineColor;
            float thickness = isMajor ? 1.5f : 0.7f;

            // 그리드 수직선
            Handles.color = lineColor;
            Handles.DrawAAPolyLine(thickness,
                new Vector3(x, RulerHeight, 0),
                new Vector3(x, rect.height, 0));

            // 룰러 눈금 & 번호
            if (isMajor || _pixelsPerBeat >= 30f)
            {
                Handles.color = new Color(0.5f, 0.5f, 0.55f, 0.6f);
                Handles.DrawAAPolyLine(1f,
                    new Vector3(x, RulerHeight - 6, 0),
                    new Vector3(x, RulerHeight, 0));

                Rect labelRect = new Rect(x - 16, 2, 32, RulerHeight - 4);
                GUI.Label(labelRect, ((int)beat).ToString(), _rulerLabelStyle);
            }
        }
        Handles.EndGUI();

        // 룰러 하단 구분선
        EditorGUI.DrawRect(new Rect(0, RulerHeight - 1, rect.width, 1),
            new Color(0.35f, 0.35f, 0.40f));
    }

    /// <summary>
    /// 모든 PatternEntry를 컬러 블록으로 렌더링합니다.
    /// 각 블록은 패턴 구간 + 유예 시간 구간으로 구성되며,
    /// 내부에 미니 스텝 프리뷰 도트를 표시합니다.
    /// </summary>
    /// <param name="rect">로컬 좌표계 Rect</param>
    private void DrawPatternBlocks(Rect rect)
    {
        if (_songData.patternEntries == null) return;

        float laneY = RulerHeight + LaneTopPadding;

        for (int i = 0; i < _songData.patternEntries.Length; i++)
        {
            var entry = _songData.patternEntries[i];
            if (entry == null || entry.pattern == null) continue;

            float patternBeats = entry.pattern.MaxBeatOffset;
            float graceBeats = GetEffectiveGraceTime(entry);
            float judgmentBeats = entry.pattern.judgmentBeats >= 0f ? entry.pattern.judgmentBeats : _songData.judgmentBeats;
            
            // 직관적인 덧셈 점유 시간과 정확히 동치 (MaxBeatOffset + graceTime + judgmentBeats)
            float totalBeats = patternBeats + graceBeats + judgmentBeats;

            // 블록 위치 계산
            float blockX = BeatToLocalX(entry.startBeat);
            float blockW = totalBeats * _pixelsPerBeat;

            // 화면 밖이면 스킵
            if (blockX + blockW < 0 || blockX > rect.width) continue;

            Rect blockRect = new Rect(blockX, laneY, blockW, BlockHeight);
            bool isSelected = (i == _selectedEntryIndex);

            // 블록 색상 (인덱스 기반 HSV)
            float hue = (i * 0.618034f) % 1f; // 황금비로 고르게 분포
            Color blockColor = Color.HSVToRGB(hue, 0.55f, 0.65f);

            // ── 1. 패턴 구간 (실제 스텝이 있는 구간) ──
            float patternW = patternBeats * _pixelsPerBeat;
            Rect patternRect = new Rect(blockX, laneY, Mathf.Max(patternW, 2f), BlockHeight);
            EditorGUI.DrawRect(patternRect, blockColor);

            // ── 2. 유예 시간 구간 (사선 빗금) ──
            float graceX = blockX + patternW;
            float graceW = graceBeats * _pixelsPerBeat;
            if (graceBeats > 0.01f)
            {
                Rect graceRect = new Rect(graceX, laneY, graceW, BlockHeight);
                Color graceColor = new Color(blockColor.r, blockColor.g, blockColor.b, 0.35f);
                EditorGUI.DrawRect(graceRect, graceColor);
                DrawStripes(graceRect);
            }

            // ── 3. 판정 연출 대기 구간 (수평 미세 실선 영역) ──
            float judgeX = graceX + graceW;
            float judgeW = judgmentBeats * _pixelsPerBeat;
            Rect judgeRect = new Rect(judgeX, laneY, judgeW, BlockHeight);
            
            // 고급스러운 딥 로즈/플럼 (Deep Magenta-Plum) 반투명 오버레이
            Color judgeColor = new Color(0.32f, 0.12f, 0.25f, 0.50f); 
            EditorGUI.DrawRect(judgeRect, judgeColor);
            DrawHorizontalStripes(judgeRect);

            // 구분을 위한 경계 세로 실선
            EditorGUI.DrawRect(new Rect(judgeX, laneY, 1f, BlockHeight), new Color(1f, 1f, 1f, 0.15f));

            // ── 선택 아웃라인 ──
            if (isSelected)
            {
                DrawRectOutline(blockRect, SelectionOutlineColor, SelectionOutlineThickness);
            }

            // ── 블록 내 텍스트 (패턴 이름) ──
            string label = entry.pattern.patternName;
            float luminance = 0.2126f * blockColor.r + 0.7152f * blockColor.g + 0.0722f * blockColor.b;
            _blockLabelStyle.normal.textColor = luminance > 0.5f ? Color.black : Color.white;

            Rect textRect = new Rect(blockX + 8, laneY + 6, blockW - 16, 18);
            GUI.Label(textRect, label, _blockLabelStyle);

            // ── 패턴 그리드 미리보기 (24-삼각형 그리드) ──
            DrawPatternGridPreview(entry, patternRect);

            // ── 비트 위치 표시 ──
            GUIStyle beatInfoStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                normal = { textColor = new Color(luminance > 0.5f ? 0.15f : 0.85f,
                                                  luminance > 0.5f ? 0.15f : 0.85f,
                                                  luminance > 0.5f ? 0.15f : 0.85f, 0.85f) }
            };
            Rect beatInfoRect = new Rect(blockX + 8, laneY + BlockHeight - 20, blockW - 16, 14);
            GUI.Label(beatInfoRect, $"Beat {entry.startBeat:F1}", beatInfoStyle);
        }
    }

    /// <summary>
    /// 패턴 블록의 본체(약간의 그래디언트 효과 포함)를 그립니다.
    /// </summary>
    /// <param name="rect">블록 영역</param>
    /// <param name="color">블록 기본 색상</param>
    /// <param name="isSelected">선택 상태 여부</param>
    private void DrawBlockBody(Rect rect, Color color, bool isSelected)
    {
        // 상단 약간 밝게, 하단 약간 어둡게 하여 그래디언트 느낌
        float gradientSteps = 4;
        float stepH = rect.height / gradientSteps;

        for (int g = 0; g < gradientSteps; g++)
        {
            float t = g / (gradientSteps - 1f);
            Color gradColor = Color.Lerp(
                new Color(color.r * 1.15f, color.g * 1.15f, color.b * 1.15f, 1f),
                new Color(color.r * 0.75f, color.g * 0.75f, color.b * 0.75f, 1f),
                t);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + g * stepH, rect.width, stepH + 1), gradColor);
        }

        // 상단 하이라이트 라인
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1),
            new Color(1, 1, 1, 0.15f));
    }

    private void DrawStripes(Rect rect)
    {
        // GUI.BeginClip을 사용해 드로잉 영역을 강제 클리핑
        GUI.BeginClip(rect);
        Handles.BeginGUI();
        Handles.color = GraceOverlayColor;

        float stripeSpacing = 10f;
        float h = rect.height;
        float w = rect.width;

        // 로컬 좌표(0, 0) ~ (w, h) 기준으로 넉넉히 사선을 꽉 채워 그림
        for (float x = -h; x < w + h; x += stripeSpacing)
        {
            Handles.DrawAAPolyLine(1.5f,
                new Vector3(x, 0, 0),
                new Vector3(x + h * 0.5f, h, 0));
        }
        Handles.EndGUI();
        GUI.EndClip();
    }

    /// <summary>
    /// 판정 영역에 미세한 수평 실선 오버레이를 그립니다.
    /// </summary>
    /// <param name="rect">그릴 영역</param>
    private void DrawHorizontalStripes(Rect rect)
    {
        GUI.BeginClip(rect);
        Handles.BeginGUI();
        Handles.color = new Color(1f, 0.65f, 0.85f, 0.35f); // 돋보이는 반투명 핑크빛 미세 실선

        float spacing = 6f;
        for (float y = 0; y < rect.height; y += spacing)
        {
            Handles.DrawAAPolyLine(1f,
                new Vector3(0, y, 0),
                new Vector3(rect.width, y, 0));
        }
        Handles.EndGUI();
        GUI.EndClip();
    }

    /// <summary>
    /// 패턴 블록 내부에 각 스텝을 작은 컬러 도트/사각형으로 표시합니다.
    /// 스텝의 beatOffset에 비례한 가로 위치, triangleIndex에 비례한 세로 위치를 사용합니다.
    /// </summary>
    /// <param name="entry">패턴 엔트리</param>
    /// <param name="blockX">블록 시작 X (로컬 좌표)</param>
    /// <param name="blockY">블록 시작 Y (로컬 좌표)</param>
    /// <param name="patternW">패턴 구간 너비 (픽셀)</param>
    private void DrawMiniStepPreview(PatternEntry entry, float blockX, float blockY, float patternW)
    {
        if (entry.pattern.steps == null || entry.pattern.steps.Length == 0) return;

        float maxBeat = entry.pattern.MaxBeatOffset;
        if (maxBeat < 0.001f) maxBeat = 1f;

        float dotArea_Y = blockY + 18f;
        float dotArea_H = BlockHeight - 32f;
        float dotSize = Mathf.Clamp(_pixelsPerBeat * 0.12f, 2f, 6f);

        foreach (var step in entry.pattern.steps)
        {
            // 가로: beatOffset 비율
            float xRatio = step.beatOffset / maxBeat;
            float dotX = blockX + 4f + xRatio * Mathf.Max(patternW - 8f, 4f);

            // 세로: triangleIndex (1~24) 비율
            float yRatio = (step.triangleIndex - 1f) / 23f;
            float dotY = dotArea_Y + yRatio * dotArea_H;

            Color dotColor = ColorUtil.ToColor(step.color);
            Rect dotRect = new Rect(dotX - dotSize * 0.5f, dotY - dotSize * 0.5f, dotSize, dotSize);
            EditorGUI.DrawRect(dotRect, dotColor);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  타임라인 인터랙션
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 타임라인 영역의 모든 마우스 / 스크롤 입력을 처리합니다.
    /// 패닝 (중간 버튼), 줌 (Ctrl+스크롤), 수평 스크롤,
    /// 블록 선택, 드래그, 우클릭 메뉴, 빈 영역 클릭(패턴 추가)을 포함합니다.
    /// </summary>
    /// <param name="localRect">타임라인 로컬 영역</param>
    /// <param name="screenRect">타임라인 스크린 영역 (이벤트 좌표 변환용)</param>
    private void HandleTimelineInput(Rect localRect, Rect screenRect)
    {
        Event e = Event.current;

        // 마우스 위치를 로컬 좌표로 (GUI.BeginClip 되어 있으므로 이미 로컬)
        Vector2 mouseLocal = e.mousePosition;

        switch (e.type)
        {
            // ── 마우스 다운 ──
            case EventType.MouseDown:
                if (!localRect.Contains(mouseLocal)) break;

                if (e.button == 2) // 중간 버튼 = 패닝 시작
                {
                    _isPanning = true;
                    _panStartMouseX = mouseLocal.x;
                    _panStartScrollOffset = _scrollOffsetBeats;
                    e.Use();
                }
                else if (e.button == 0) // 좌클릭
                {
                    int hitIndex = HitTestBlock(mouseLocal);
                    if (hitIndex >= 0)
                    {
                        // 새로운 블록이 선택되거나 포커스를 변경하는 경우 이전 포커스를 해제하여 캐시 텍스트 강제 갱신
                        if (_selectedEntryIndex != hitIndex)
                        {
                            GUIUtility.keyboardControl = 0;
                        }

                        // 블록 선택 + 드래그 시작
                        _selectedEntryIndex = hitIndex;
                        _isDragging = true;
                        _dragStartBeat = LocalXToBeat(mouseLocal.x);
                        _dragOriginalStartBeat = _songData.patternEntries[hitIndex].startBeat;
                        e.Use();
                    }
                    else if (mouseLocal.y > RulerHeight)
                    {
                        // 빈 영역 좌클릭 드래그 → 패닝 시작
                        if (_selectedEntryIndex != -1)
                        {
                            GUIUtility.keyboardControl = 0;
                        }

                        _selectedEntryIndex = -1;
                        _isPanning = true;
                        _panStartMouseX = mouseLocal.x;
                        _panStartScrollOffset = _scrollOffsetBeats;
                        e.Use();
                    }
                    Repaint();
                }
                else if (e.button == 1) // 우클릭
                {
                    int hitIdx = HitTestBlock(mouseLocal);
                    if (hitIdx >= 0)
                    {
                        if (_selectedEntryIndex != hitIdx)
                        {
                            GUIUtility.keyboardControl = 0;
                        }
                        _selectedEntryIndex = hitIdx;
                        ShowBlockContextMenu(hitIdx);
                        e.Use();
                    }
                    else if (mouseLocal.y > RulerHeight)
                    {
                        // 빈 영역 우클릭 → 패턴 추가 메뉴
                        float beatPos = LocalXToBeat(mouseLocal.x);
                        ShowAddPatternContextMenu(beatPos);
                        e.Use();
                    }
                }
                break;

            // ── 마우스 드래그 ──
            case EventType.MouseDrag:
                if (_isPanning && (e.button == 2 || e.button == 0))
                {
                    float deltaPx = mouseLocal.x - _panStartMouseX;
                    _scrollOffsetBeats = _panStartScrollOffset - deltaPx / _pixelsPerBeat;
                    _scrollOffsetBeats = Mathf.Max(0, _scrollOffsetBeats);
                    e.Use();
                    Repaint();
                }
                else if (_isDragging && e.button == 0 && _selectedEntryIndex >= 0)
                {
                    float currentBeat = LocalXToBeat(mouseLocal.x);
                    float delta = currentBeat - _dragStartBeat;
                    float newStart = Mathf.Max(0, _dragOriginalStartBeat + delta);

                    // 0.1 비트 단위 스냅
                    newStart = Mathf.Round(newStart * 10f) / 10f;

                    Undo.RecordObject(_songData, "패턴 위치 이동");
                    _songData.patternEntries[_selectedEntryIndex].startBeat = newStart;
                    EditorUtility.SetDirty(_songData);
                    e.Use();
                    Repaint();
                }
                break;

            // ── 마우스 업 ──
            case EventType.MouseUp:
                if (_isPanning && (e.button == 2 || e.button == 0))
                {
                    _isPanning = false;
                    e.Use();
                }
                if (_isDragging && e.button == 0)
                {
                    _isDragging = false;
                    if (_selectedEntryIndex >= 0)
                    {
                        SortEntries();
                    }
                    e.Use();
                    Repaint();
                }
                break;

            // ── 스크롤 ──
            case EventType.ScrollWheel:
                if (!localRect.Contains(mouseLocal)) break;

                if (e.control) // Ctrl + 스크롤 = 줌
                {
                    float zoomFactor = 1f - e.delta.y * 0.05f;
                    float beatUnderMouse = LocalXToBeat(mouseLocal.x);

                    _pixelsPerBeat = Mathf.Clamp(_pixelsPerBeat * zoomFactor,
                        MinPixelsPerBeat, MaxPixelsPerBeat);

                    // 줌 시 마우스 위치 고정
                    _scrollOffsetBeats = beatUnderMouse - mouseLocal.x / _pixelsPerBeat;
                    _scrollOffsetBeats = Mathf.Max(0, _scrollOffsetBeats);

                    e.Use();
                    Repaint();
                }
                else // 일반 스크롤 = 수평 이동
                {
                    float scrollSpeed = 3f / _pixelsPerBeat;
                    _scrollOffsetBeats += e.delta.y * scrollSpeed;
                    _scrollOffsetBeats = Mathf.Max(0, _scrollOffsetBeats);
                    e.Use();
                    Repaint();
                }
                break;
        }
    }

    /// <summary>
    /// 블록 우클릭 시 나타나는 컨텍스트 메뉴를 표시합니다.
    /// </summary>
    /// <param name="entryIndex">대상 패턴 엔트리 인덱스</param>
    private void ShowBlockContextMenu(int entryIndex)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("삭제"), false, () => DeleteEntry(entryIndex));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("패턴 편집기 열기"), false, () =>
        {
            if (_songData.patternEntries[entryIndex].pattern != null)
            {
                OpenPatternDesigner(_songData.patternEntries[entryIndex].pattern);
            }
        });
        menu.ShowAsContext();
    }

    /// <summary>
    /// 빈 영역 우클릭 시 패턴 추가를 위한 컨텍스트 메뉴를 표시합니다.
    /// </summary>
    /// <param name="beatPos">추가할 비트 위치</param>
    private void ShowAddPatternContextMenu(float beatPos)
    {
        // 정수 비트 스냅
        beatPos = Mathf.Round(beatPos);
        beatPos = Mathf.Max(0, beatPos);

        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent($"Beat {(int)beatPos}에 패턴 추가..."), false, () =>
        {
            _pendingAddBeat = beatPos;
            EditorGUIUtility.ShowObjectPicker<PatternData>(null, false, "", ObjectPickerControlID);
        });
        menu.ShowAsContext();
    }

    /// <summary>
    /// EditorGUIUtility.ShowObjectPicker의 결과를 수신하여 새 패턴을 추가합니다.
    /// </summary>
    private void HandleObjectPickerResult()
    {
        if (Event.current.commandName == "ObjectSelectorClosed" &&
            EditorGUIUtility.GetObjectPickerControlID() == ObjectPickerControlID)
        {
            var pickedPattern = EditorGUIUtility.GetObjectPickerObject() as PatternData;
            if (pickedPattern != null && _pendingAddBeat >= 0f)
            {
                AddPatternEntry(pickedPattern, _pendingAddBeat);
            }
            _pendingAddBeat = -1f;
        }
    }

    /// <summary>
    /// Delete 키 / Space 키 입력을 처리합니다.
    /// </summary>
    private void HandleKeyboardEvents()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Delete && _selectedEntryIndex >= 0)
            {
                DeleteEntry(_selectedEntryIndex);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Space)
            {
                ToggleAudioPlayback();
                e.Use();
            }
        }
    }

    /// <summary>
    /// 마우스 위치에 해당하는 패턴 블록 인덱스를 반환합니다.
    /// </summary>
    /// <param name="localMouse">로컬 좌표 마우스 위치</param>
    /// <returns>히트된 블록 인덱스, 없으면 -1</returns>
    private int HitTestBlock(Vector2 localMouse)
    {
        if (_songData.patternEntries == null) return -1;

        float laneY = RulerHeight + LaneTopPadding;

        // 역순으로 탐색 (나중에 그려진 블록이 위에 있으므로)
        for (int i = _songData.patternEntries.Length - 1; i >= 0; i--)
        {
            var entry = _songData.patternEntries[i];
            if (entry == null || entry.pattern == null) continue;

            float judgmentBeats = entry.pattern.judgmentBeats >= 0f ? entry.pattern.judgmentBeats : _songData.judgmentBeats;
            float totalBeats = entry.pattern.MaxBeatOffset + GetEffectiveGraceTime(entry) + judgmentBeats;

            float blockX = BeatToLocalX(entry.startBeat);
            float blockW = totalBeats * _pixelsPerBeat;
            Rect blockRect = new Rect(blockX, laneY, blockW, BlockHeight);

            if (blockRect.Contains(localMouse))
                return i;
        }
        return -1;
    }

    // ════════════════════════════════════════════════════════════
    //  디테일 패널 (하단)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 하단 디테일 패널을 그립니다.
    /// 선택된 패턴의 이름, startBeat(편집 가능), 유예 시간, 지속 비트 수를 표시하고
    /// [패턴 편집기 열기], [삭제] 버튼을 제공합니다.
    /// </summary>
    /// <param name="rect">디테일 패널 영역</param>
    private void DrawDetailPanel(Rect rect)
    {
        // 배경
        EditorGUI.DrawRect(rect, DetailBgColor);
        // 상단 구분선
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1),
            new Color(0.3f, 0.3f, 0.35f));

        GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16));

        if (_selectedEntryIndex < 0 ||
            _songData.patternEntries == null ||
            _selectedEntryIndex >= _songData.patternEntries.Length)
        {
            GUILayout.Label("타임라인에서 패턴을 선택하세요", _placeholderStyle);
            GUILayout.EndArea();
            return;
        }

        var entry = _songData.patternEntries[_selectedEntryIndex];
        if (entry == null || entry.pattern == null)
        {
            GUILayout.Label("유효하지 않은 패턴 엔트리", _placeholderStyle);
            GUILayout.EndArea();
            return;
        }

        // ── 헤더 ──
        GUILayout.Label($"📋 {entry.pattern.patternName}", _detailHeaderStyle);

        GUILayout.BeginHorizontal();

        // 시작 비트 편집
        GUILayout.Label("시작 비트:", GUILayout.Width(65));
        EditorGUI.BeginChangeCheck();
        float newStartBeat = EditorGUILayout.FloatField(entry.startBeat, GUILayout.Width(60));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_songData, "시작 비트 변경");
            entry.startBeat = Mathf.Max(0f, Mathf.Round(newStartBeat * 10f) / 10f);
            SortEntries();
            EditorUtility.SetDirty(_songData);
        }

        GUILayout.Space(16);

        // 지속 비트
        float duration = entry.pattern.MaxBeatOffset;
        GUILayout.Label($"패턴 길이: {duration:F1} 비트", GUILayout.Width(130));

        GUILayout.Space(16);

        // 유예 시간 편집
        GUILayout.Label("유예 비트:", GUILayout.Width(65));
        EditorGUI.BeginChangeCheck();
        float newGraceTime = EditorGUILayout.FloatField(entry.pattern.graceTime, GUILayout.Width(45));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(entry.pattern, "패턴 유예 비트 변경");
            entry.pattern.graceTime = newGraceTime;
            EditorUtility.SetDirty(entry.pattern);
        }

        float graceBeats = GetEffectiveGraceTime(entry);
        string graceInfo = entry.pattern.graceTime < 0f
            ? $" (곡 기본값: {graceBeats:F1} 비트)"
            : $" ({graceBeats * _songData.SecondsPerBeat:F2}초)";

        GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.6f, 0.6f, 0.65f) }
        };
        GUILayout.Label(graceInfo, infoStyle, GUILayout.Width(130));

        GUILayout.Space(16);

        // 판정 시간 편집
        GUILayout.Label("판정 비트:", GUILayout.Width(65));
        EditorGUI.BeginChangeCheck();
        float newJudgeBeats = EditorGUILayout.FloatField(entry.pattern.judgmentBeats, GUILayout.Width(45));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(entry.pattern, "패턴 판정 비트 변경");
            entry.pattern.judgmentBeats = newJudgeBeats;
            EditorUtility.SetDirty(entry.pattern);
        }

        float effectiveJudge = entry.pattern.judgmentBeats >= 0f ? entry.pattern.judgmentBeats : _songData.judgmentBeats;
        string judgeInfo = entry.pattern.judgmentBeats < 0f
            ? $" (곡 기본값: {effectiveJudge:F1} 비트)"
            : $" ({effectiveJudge * _songData.SecondsPerBeat:F2}초)";

        GUILayout.Label(judgeInfo, infoStyle, GUILayout.Width(130));

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ── 버튼 ──
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("🎨 패턴 편집기 열기", GUILayout.Height(26), GUILayout.Width(160)))
        {
            OpenPatternDesigner(entry.pattern);
        }

        GUILayout.Space(8);

        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("🗑 삭제", GUILayout.Height(26), GUILayout.Width(80)))
        {
            DeleteEntry(_selectedEntryIndex);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    // ════════════════════════════════════════════════════════════
    //  데이터 수정 메서드
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 새 PatternEntry를 songData.patternEntries에 추가합니다.
    /// Undo 기록과 SetDirty를 수행하고, startBeat 기준으로 정렬합니다.
    /// </summary>
    /// <param name="pattern">추가할 PatternData</param>
    /// <param name="beat">삽입할 비트 위치</param>
    private void AddPatternEntry(PatternData pattern, float beat)
    {
        Undo.RecordObject(_songData, "패턴 추가");

        var list = _songData.patternEntries != null
            ? new List<PatternEntry>(_songData.patternEntries)
            : new List<PatternEntry>();

        var newEntry = new PatternEntry
        {
            startBeat = beat,
            pattern = pattern
        };
        list.Add(newEntry);
        list.Sort((a, b) => a.startBeat.CompareTo(b.startBeat));

        _songData.patternEntries = list.ToArray();
        _selectedEntryIndex = System.Array.IndexOf(_songData.patternEntries, newEntry);

        EditorUtility.SetDirty(_songData);
        Repaint();
    }

    /// <summary>
    /// 지정한 인덱스의 PatternEntry를 삭제합니다.
    /// Undo 기록과 SetDirty를 수행합니다.
    /// </summary>
    /// <param name="index">삭제할 엔트리 인덱스</param>
    private void DeleteEntry(int index)
    {
        if (_songData.patternEntries == null || index < 0 || index >= _songData.patternEntries.Length)
            return;

        Undo.RecordObject(_songData, "패턴 삭제");

        var list = new List<PatternEntry>(_songData.patternEntries);
        list.RemoveAt(index);
        _songData.patternEntries = list.ToArray();
        _selectedEntryIndex = -1;

        EditorUtility.SetDirty(_songData);
        Repaint();
    }

    /// <summary>
    /// patternEntries를 startBeat 기준으로 오름차순 정렬합니다.
    /// 선택 상태를 유지하기 위해 정렬 전후 인덱스를 추적합니다.
    /// </summary>
    private void SortEntries()
    {
        if (_songData == null || _songData.patternEntries == null || _songData.patternEntries.Length == 0)
            return;

        // 선택 상태 보존
        PatternEntry selectedEntry = null;
        if (_selectedEntryIndex >= 0 && _selectedEntryIndex < _songData.patternEntries.Length)
            selectedEntry = _songData.patternEntries[_selectedEntryIndex];

        Undo.RecordObject(_songData, "패턴 정렬");

        var sorted = _songData.patternEntries.OrderBy(e => e.startBeat).ToArray();
        _songData.patternEntries = sorted;

        // 선택 인덱스 복원
        if (selectedEntry != null)
            _selectedEntryIndex = System.Array.IndexOf(_songData.patternEntries, selectedEntry);

        EditorUtility.SetDirty(_songData);
    }

    // ════════════════════════════════════════════════════════════
    //  유틸리티
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 비트 값을 로컬 X 픽셀 좌표로 변환합니다.
    /// </summary>
    /// <param name="beat">비트 값</param>
    /// <returns>로컬 X 좌표 (픽셀)</returns>
    private float BeatToLocalX(float beat)
    {
        return (beat - _scrollOffsetBeats) * _pixelsPerBeat;
    }

    /// <summary>
    /// 로컬 X 픽셀 좌표를 비트 값으로 변환합니다.
    /// </summary>
    /// <param name="localX">로컬 X 좌표 (픽셀)</param>
    /// <returns>비트 값</returns>
    private float LocalXToBeat(float localX)
    {
        return localX / _pixelsPerBeat + _scrollOffsetBeats;
    }

    /// <summary>
    /// PatternEntry의 유효 유예 시간(비트 수)을 계산합니다.
    /// 패턴 자체에 graceTime이 설정되어 있으면 그 값을 사용하고,
    /// 그렇지 않으면 SongData의 기본 graceTime을 사용합니다.
    /// </summary>
    /// <param name="entry">패턴 엔트리</param>
    /// <returns>유효 유예 시간 (비트 수)</returns>
    private float GetEffectiveGraceTime(PatternEntry entry)
    {
        if (entry.pattern.graceTime >= 0f)
            return entry.pattern.graceTime;
        return _songData.graceTime;
    }

    /// <summary>
    /// 모든 패턴 엔트리를 고려한 전체 타임라인의 최대 비트 수를 계산합니다.
    /// </summary>
    /// <returns>마지막 패턴의 끝 비트 위치</returns>
    private float CalcTotalBeats()
    {
        if (_songData.patternEntries == null || _songData.patternEntries.Length == 0)
            return 0f;

        float maxBeat = 0f;
        foreach (var entry in _songData.patternEntries)
        {
            if (entry == null || entry.pattern == null) continue;
            float judgmentBeats = entry.pattern.judgmentBeats >= 0f ? entry.pattern.judgmentBeats : _songData.judgmentBeats;
            float end = entry.startBeat + entry.pattern.MaxBeatOffset + GetEffectiveGraceTime(entry) + judgmentBeats;
            if (end > maxBeat) maxBeat = end;
        }
        return maxBeat;
    }

    /// <summary>
    /// PatternDesignerWindow를 열어 지정된 패턴을 편집합니다.
    /// </summary>
    /// <param name="pattern">편집할 PatternData</param>
    private void OpenPatternDesigner(PatternData pattern)
    {
        var designerWindow = GetWindow<PatternDesignerWindow>("패턴 디자이너");
        // PatternDesignerWindow는 _pattern 필드를 가지고 있으므로
        // Selection을 통해 간접적으로 설정하거나, 직접 열기만 합니다.
        Selection.activeObject = pattern;
        designerWindow.Repaint();
    }

    // ════════════════════════════════════════════════════════════
    //  오디오 재생 & 파형 시스템
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 재생 시간을 업데이트합니다.
    /// </summary>
    private void UpdateAudioPlaybackTime()
    {
        if (!_isAudioPlaying) return;

        float elapsed = (float)(EditorApplication.timeSinceStartup - _audioPlayStartRealTime);
        _audioPlaybackTime = _audioPlayStartSongTime + elapsed;

        // 곡 끝에 도달하면 자동 정지
        if (_songData.bgmClip != null && _audioPlaybackTime >= _songData.bgmClip.length)
        {
            StopAudioPlayback();
        }
    }

    /// <summary>
    /// 오디오 재생/일시정지를 토글합니다.
    /// </summary>
    private void ToggleAudioPlayback()
    {
        if (_songData == null || _songData.bgmClip == null) return;

        if (_isAudioPlaying)
        {
            PauseAudioPlayback();
        }
        else
        {
            PlayAudioFromCurrentPosition();
        }
    }

    /// <summary>
    /// 현재 _audioPlaybackTime 위치부터 재생을 시작합니다.
    /// </summary>
    private void PlayAudioFromCurrentPosition()
    {
        if (_songData == null || _songData.bgmClip == null) return;

        _isAudioPlaying = true;
        _audioPlayStartRealTime = EditorApplication.timeSinceStartup;
        _audioPlayStartSongTime = _audioPlaybackTime;

        // 에디터에서 클립 미리 재생 (Unity 내장 유틸리티)
        PlayClipAtEditorPosition(_songData.bgmClip, _audioPlaybackTime);
    }

    /// <summary>
    /// 재생을 일시정지합니다.
    /// </summary>
    private void PauseAudioPlayback()
    {
        _isAudioPlaying = false;
        StopEditorClip();
    }

    /// <summary>
    /// 재생을 완전 정지하고 처음으로 돌아갑니다.
    /// </summary>
    private void StopAudioPlayback()
    {
        _isAudioPlaying = false;
        _audioPlaybackTime = 0f;
        _audioPlayStartSongTime = 0f;
        StopEditorClip();
    }

    /// <summary>
    /// 특정 비트 위치로 재생 위치를 이동합니다.
    /// </summary>
    private void SeekToBeat(float beat)
    {
        if (_songData == null) return;

        float timeInSong = beat * _songData.SecondsPerBeat + _songData.audioStartOffset;
        _audioPlaybackTime = Mathf.Max(0f, timeInSong);

        if (_isAudioPlaying)
        {
            _audioPlayStartRealTime = EditorApplication.timeSinceStartup;
            _audioPlayStartSongTime = _audioPlaybackTime;
            StopEditorClip();
            if (_songData.bgmClip != null)
                PlayClipAtEditorPosition(_songData.bgmClip, _audioPlaybackTime);
        }
    }

    /// <summary>
    /// 현재 재생 시간을 비트 단위로 반환합니다.
    /// </summary>
    private float GetCurrentPlaybackBeat()
    {
        if (_songData == null || _songData.bpm <= 0) return 0f;
        float songTime = _audioPlaybackTime - _songData.audioStartOffset;
        return songTime / _songData.SecondsPerBeat;
    }

    // ── 에디터 오디오 재생 유틸리티 (Reflection 기반) ──

    /// <summary>
    /// Unity 에디터의 내부 AudioUtil을 사용하여 클립을 재생합니다.
    /// </summary>
    private static void PlayClipAtEditorPosition(AudioClip clip, float startTime)
    {
        try
        {
            // AudioUtil 리플렉션을 통해 에디터 오디오 재생
            var audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilType == null) return;

            // 재생 중이면 먼저 정지
            var stopMethod = audioUtilType.GetMethod("StopAllPreviewClips",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            stopMethod?.Invoke(null, null);

            // PlayPreviewClip(clip, startTime, loop)
            var playMethod = audioUtilType.GetMethod("PlayPreviewClip",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null,
                new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null);

            if (playMethod != null)
            {
                int startSample = Mathf.FloorToInt(startTime * clip.frequency);
                startSample = Mathf.Clamp(startSample, 0, clip.samples - 1);
                playMethod.Invoke(null, new object[] { clip, startSample, false });
            }
        }
        catch (Exception)
        {
            // AudioUtil 접근 실패 시 무시 (시각적 기능은 계속 작동)
        }
    }

    /// <summary>
    /// 에디터 프리뷰 오디오를 정지합니다.
    /// </summary>
    private static void StopEditorClip()
    {
        try
        {
            var audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilType == null) return;

            var stopMethod = audioUtilType.GetMethod("StopAllPreviewClips",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            stopMethod?.Invoke(null, null);
        }
        catch (Exception) { }
    }

    // ════════════════════════════════════════════════════════════
    //  오디오 컨트롤 바 & 파형 레인 렌더링
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 오디오 컨트롤 바 (재생/정지 버튼, 시간 표시)를 그립니다.
    /// </summary>
    private void DrawAudioControlBar(Rect rect)
    {
        EditorGUI.DrawRect(rect, AudioControlBgColor);
        // 상단 구분선
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f),
            new Color(0.3f, 0.3f, 0.35f));

        GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 3, rect.width - 16, rect.height - 6));
        GUILayout.BeginHorizontal();

        bool hasClip = _songData != null && _songData.bgmClip != null;

        GUI.enabled = hasClip;

        // 재생/일시정지 버튼
        string playLabel = _isAudioPlaying ? "⏸ 일시정지" : "▶ 재생";
        if (GUILayout.Button(playLabel, GUILayout.Width(80), GUILayout.Height(20)))
        {
            ToggleAudioPlayback();
        }

        // 정지 버튼
        if (GUILayout.Button("⏹ 처음으로", GUILayout.Width(80), GUILayout.Height(20)))
        {
            StopAudioPlayback();
        }

        GUI.enabled = true;

        GUILayout.Space(16);

        // 현재 시간 / 전체 시간 표시
        if (hasClip)
        {
            float currentBeat = GetCurrentPlaybackBeat();
            float clipLength = _songData.bgmClip.length;
            string timeStr = $"🕐 {FormatTime(_audioPlaybackTime)} / {FormatTime(clipLength)}" +
                             $"    Beat {currentBeat:F1}";
            GUILayout.Label(timeStr, _audioTimeLabelStyle);
        }
        else
        {
            GUILayout.Label("SongData에 BGM 클립을 설정하세요", EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();

        // 스페이스바 안내
        if (hasClip)
        {
            GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.5f, 0.55f) },
                fontSize = 9
            };
            GUILayout.Label("[Space] 재생/정지", hintStyle);
        }

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    /// <summary>
    /// 파형 레인을 그립니다. 비트 그리드와 동기화된 파형을 표시합니다.
    /// </summary>
    private void DrawWaveformLane(Rect rect)
    {
        // 클리핑
        GUI.BeginClip(rect);
        Rect localRect = new Rect(0, 0, rect.width, rect.height);

        // 배경
        EditorGUI.DrawRect(localRect, WaveformBgColor);

        // 상단 구분선
        EditorGUI.DrawRect(new Rect(0, 0, localRect.width, 1f),
            new Color(0.25f, 0.25f, 0.30f));

        if (_songData != null && _songData.bgmClip != null)
        {
            // 비트 그리드 라인 (타임라인과 동기화)
            DrawWaveformBeatGrid(localRect);

            // 파형 그리기
            DrawWaveformData(localRect);

            // 플레이헤드
            DrawPlayhead(localRect);

            // 마우스 클릭 및 드래그 시 재생 위치 이동
            Event e = Event.current;
            if (e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                if (localRect.Contains(e.mousePosition))
                {
                    float clickedBeat = LocalXToBeat(e.mousePosition.x);
                    SeekToBeat(clickedBeat);
                    e.Use();
                    Repaint();
                }
            }
        }
        else
        {
            // 안내 텍스트
            GUIStyle noClipStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 11
            };
            GUI.Label(localRect, "🎵 BGM 클립 없음 — SongData에 AudioClip을 설정하세요", noClipStyle);
        }

        GUI.EndClip();
    }

    /// <summary>
    /// 파형 레인에 비트 그리드를 그립니다 (타임라인과 동일한 스크롤/줌).
    /// </summary>
    private void DrawWaveformBeatGrid(Rect rect)
    {
        float startBeat = Mathf.Floor(_scrollOffsetBeats);
        float endBeat = _scrollOffsetBeats + rect.width / _pixelsPerBeat;

        Handles.BeginGUI();
        for (float beat = startBeat; beat <= endBeat + 1; beat++)
        {
            float x = BeatToLocalX(beat);
            if (x < 0 || x > rect.width) continue;

            bool isMajor = Mathf.Approximately(beat % 4, 0);
            Color lineColor = isMajor
                ? new Color(0.30f, 0.30f, 0.38f, 0.6f)
                : new Color(0.20f, 0.20f, 0.25f, 0.4f);
            float thickness = isMajor ? 1.2f : 0.5f;

            Handles.color = lineColor;
            Handles.DrawAAPolyLine(thickness,
                new Vector3(x, 0, 0),
                new Vector3(x, rect.height, 0));
        }
        Handles.EndGUI();
    }

    /// <summary>
    /// AudioClip의 샘플 데이터를 기반으로 파형을 그립니다.
    /// </summary>
    private void DrawWaveformData(Rect rect)
    {
        AudioClip clip = _songData.bgmClip;
        if (clip == null) return;

        float clipLengthSec = clip.length;
        float bpm = _songData.bpm;
        float secPerBeat = _songData.SecondsPerBeat;
        float audioOffset = _songData.audioStartOffset;

        // 뷰에 보이는 시간 범위 (초)
        float viewStartBeat = _scrollOffsetBeats;
        float viewEndBeat = _scrollOffsetBeats + rect.width / _pixelsPerBeat;
        float viewStartSec = viewStartBeat * secPerBeat + audioOffset;
        float viewEndSec = viewEndBeat * secPerBeat + audioOffset;

        // 클립 시간 범위 제한
        viewStartSec = Mathf.Max(0f, viewStartSec);
        viewEndSec = Mathf.Min(clipLengthSec, viewEndSec);

        if (viewEndSec <= viewStartSec) return;

        // 샘플 데이터 가져오기
        int channels = clip.channels;
        int totalSamples = clip.samples;
        int startSample = Mathf.FloorToInt(viewStartSec * clip.frequency);
        int endSample = Mathf.CeilToInt(viewEndSec * clip.frequency);
        startSample = Mathf.Clamp(startSample, 0, totalSamples - 1);
        endSample = Mathf.Clamp(endSample, startSample + 1, totalSamples);

        int sampleCount = endSample - startSample;
        if (sampleCount <= 0) return;

        // 성능: 픽셀당 여러 샘플을 묶어서 min/max로 표현
        int pixelWidth = Mathf.Max(1, Mathf.FloorToInt(rect.width));
        float samplesPerPixel = (float)sampleCount / pixelWidth;

        float[] samples = new float[sampleCount * channels];
        clip.GetData(samples, startSample);

        float centerY = rect.y + rect.height * 0.5f;
        float amplitude = rect.height * 0.42f;

        Handles.BeginGUI();

        for (int px = 0; px < pixelWidth; px++)
        {
            int sStart = Mathf.FloorToInt(px * samplesPerPixel);
            int sEnd = Mathf.Min(Mathf.CeilToInt((px + 1) * samplesPerPixel), sampleCount);

            float minVal = 0f, maxVal = 0f;
            for (int s = sStart; s < sEnd; s++)
            {
                float val = samples[s * channels]; // 첫 번째 채널
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
            }

            float x = px;
            float yTop = centerY - maxVal * amplitude;
            float yBot = centerY - minVal * amplitude;

            // 파형 막대
            float barHeight = Mathf.Max(1f, yBot - yTop);
            Color barColor = (Mathf.Abs(maxVal) > 0.7f || Mathf.Abs(minVal) > 0.7f)
                ? WaveformPeakColor
                : WaveformColor;

            EditorGUI.DrawRect(new Rect(x, yTop, 1f, barHeight), barColor);
        }

        // 중앙선
        EditorGUI.DrawRect(new Rect(0, centerY - 0.5f, rect.width, 1f),
            new Color(0.4f, 0.4f, 0.45f, 0.3f));

        Handles.EndGUI();
    }

    /// <summary>
    /// 타임라인 위에 현재 재생 위치의 플레이헤드를 그립니다.
    /// </summary>
    private void DrawPlayhead(Rect localRect)
    {
        if (!_isAudioPlaying && _audioPlaybackTime <= 0.001f) return;

        float currentBeat = GetCurrentPlaybackBeat();
        float x = BeatToLocalX(currentBeat);

        if (x < 0 || x > localRect.width) return;

        Handles.BeginGUI();

        // 플레이헤드 세로선
        Handles.color = PlayheadColor;
        Handles.DrawAAPolyLine(2.5f,
            new Vector3(x, 0, 0),
            new Vector3(x, localRect.height, 0));

        // 상단 삼각형 마커
        float markerSize = 6f;
        Vector3[] marker = new Vector3[3]
        {
            new Vector3(x, 0, 0),
            new Vector3(x - markerSize, -markerSize, 0),
            new Vector3(x + markerSize, -markerSize, 0)
        };
        Handles.color = PlayheadColor;
        Handles.DrawAAConvexPolygon(marker);

        Handles.EndGUI();
    }

    /// <summary>
    /// 시간(초)을 mm:ss.ms 형식으로 포맷합니다.
    /// </summary>
    private string FormatTime(float seconds)
    {
        int min = Mathf.FloorToInt(seconds / 60f);
        float sec = seconds - min * 60f;
        return $"{min:D2}:{sec:05.2f}";
    }

    // ════════════════════════════════════════════════════════════
    //  24-삼각형 그리드 미리보기 관련 구조체 & 렌더링 함수
    // ════════════════════════════════════════════════════════════

    private struct TriangleLayout
    {
        public int index;
        public float x, y;
        public bool flipped; // true = ▽, false = ▲

        public TriangleLayout(int idx, float x, float y, bool flipped)
        {
            this.index = idx;
            this.x = x;
            this.y = y;
            this.flipped = flipped;
        }
    }

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

    /// <summary>
    /// 패턴 블록의 내부 공간에 24-삼각형 미니 배치 프리뷰를 렌더링합니다.
    /// </summary>
    /// <param name="entry">패턴 엔트리</param>
    /// <param name="patternRect">실제 패턴 영역</param>
    private void DrawPatternGridPreview(PatternEntry entry, Rect patternRect)
    {
        if (entry.pattern == null || entry.pattern.steps == null || entry.pattern.steps.Length == 0)
            return;

        // 카드 내 정중앙 Y축, X축을 기점으로 배치
        Vector2 center = new Vector2(patternRect.x + patternRect.width * 0.5f, patternRect.y + patternRect.height * 0.53f);
        float scale = 36f; // 블록 높이가 180f이므로 36f 스케일로 대형 렌더링

        // 시간순 정렬로 크로놀로지 순서 도출
        var sortedSteps = entry.pattern.steps
            .OrderBy(s => s.beatOffset)
            .ThenBy(s => s.triangleIndex)
            .ToList();

        Handles.BeginGUI();
        foreach (var tri in Triangles)
        {
            float sx = center.x + tri.x * scale;
            float sy = center.y - tri.y * scale;

            float triSize = scale * 0.85f;
            Vector3[] vertices = GetTriangleVertices(sx, sy, triSize, tri.flipped);

            // 해당 타일의 혼합 색상 및 채색 순서 채집
            ColorChannel tileColor = ColorChannel.None;
            List<int> orders = new List<int>();

            for (int i = 0; i < sortedSteps.Count; i++)
            {
                if (sortedSteps[i].triangleIndex == tri.index)
                {
                    tileColor |= sortedSteps[i].color;
                    orders.Add(i + 1);
                }
            }

            Color fillColor;
            Color strokeColor;
            float strokeWidth;

            if (tileColor != ColorChannel.None)
            {
                fillColor = ColorUtil.ToColor(tileColor);
                strokeColor = Color.yellow;
                strokeWidth = 1.5f;
            }
            else
            {
                fillColor = new Color(0.20f, 0.20f, 0.24f, 0.5f);
                strokeColor = new Color(0.35f, 0.35f, 0.40f, 0.5f);
                strokeWidth = 0.8f;
            }

            // 삼각형 그리기
            DrawTriangle(vertices, fillColor, strokeColor, strokeWidth);

            // 숫자 순서 표시
            if (orders.Count > 0)
            {
                string orderStr = string.Join("→", orders);
                float luminance = 0.2126f * fillColor.r + 0.7152f * fillColor.g + 0.0722f * fillColor.b;
                Color textColor = luminance > 0.5f ? Color.black : Color.white;

                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = textColor },
                    fontSize = scale > 28f ? 10 : 8,
                    fontStyle = FontStyle.Bold
                };
                Rect labelRect = new Rect(sx - 18f, sy - 9f, 36f, 18f);
                GUI.Label(labelRect, orderStr, labelStyle);
            }
        }
        Handles.EndGUI();
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
        Handles.color = fillColor;
        Handles.DrawAAConvexPolygon(vertices);

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

    /// <summary>
    /// 사각형의 아웃라인을 그립니다.
    /// </summary>
    /// <param name="rect">대상 사각형</param>
    /// <param name="color">아웃라인 색상</param>
    /// <param name="thickness">아웃라인 두께</param>
    private void DrawRectOutline(Rect rect, Color color, float thickness)
    {
        // 상단
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        // 하단
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        // 좌측
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        // 우측
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
