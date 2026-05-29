using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 게임 상태 열거형
/// </summary>
public enum GameState
{
    Idle,             // 대기 (게임 시작 전)
    TeacherPlaying,   // 선생님 패턴 재생 중 (플레이어 입력도 가능)
    PlayerInput,      // 유예 시간 (선생님 패턴 완료 후)
    Judging,          // 판정 중
    Result,           // 결과 표시
    Resetting         // 그리드 리셋 중
}

/// <summary>
/// 게임 매니저 - SongData 기반 전체 게임 플로우를 관리하는 상태 머신
/// SongData SO를 교체하면 스테이지가 변경됨
/// 씬의 빈 GameObject에 부착
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("그리드 참조")]
    [SerializeField] private GridManager _playerGrid;
    [SerializeField] private GridManager _teacherGrid;
    
    [Header("시스템 참조")]
    [SerializeField] private PlayerInputController _playerInput;
    [SerializeField] private TeacherPatternPlayer _teacherPatternPlayer;
    [SerializeField] private RoundTimer _roundTimer; // 플레이어 타이머
    [SerializeField] private RoundTimer _teacherTimer; // 선생님 타이머
    [SerializeField] private EffectManager _effectManager; // 연출 매니저
    
    [Header("곡 데이터 (스테이지)")]
    [Tooltip("현재 스테이지의 곡 데이터. 이 SO를 교체하면 스테이지가 변경됩니다.")]
    [SerializeField] private SongData _songData;
    
    [Header("게임 설정")]
    [Tooltip("리셋 후 다음 라운드 시작까지 대기 시간 (초)")]
    [SerializeField] private float _resetDelay = 1f;
    
    [Header("디버그")]
    [SerializeField] private GameState _currentState = GameState.Idle;
    
    /// <summary>현재 게임 상태</summary>
    public GameState CurrentState => _currentState;
    
    /// <summary>현재 라운드 번호 (0부터 시작)</summary>
    public int CurrentRound { get; private set; }
    
    /// <summary>마지막 판정 결과</summary>
    public JudgmentResult LastResult { get; private set; }
    
    /// <summary>판정 완료 시 호출 (UI 연동용)</summary>
    public event System.Action<JudgmentResult> OnJudgmentComplete;
    
    /// <summary>상태 변경 시 호출</summary>
    public event System.Action<GameState> OnStateChanged;
    
    /// <summary>게임 매니저 싱글턴 인스턴스</summary>
    public static GameManager Instance { get; private set; }

    /// <summary>현재 활성화된 곡 데이터</summary>
    public SongData CurrentSongData => _songData;
    
    /// <summary>플레이어 그리드 매니저</summary>
    public GridManager PlayerGrid => _playerGrid;
    
    /// <summary>선생님 그리드 매니저</summary>
    public GridManager TeacherGrid => _teacherGrid;
    
    /// <summary>현재 라운드의 유예 시간</summary>
    private float _currentGraceTime;

    // 연출매니저 이관으로 스케일 캐시/서클 캐시 제거됨

    private void Awake()
    {
        Instance = this;

        // 인스펙터 미할당 시 씬 또는 본인 게임오브젝트에서 자동 바인딩하여 자가치유합니다.
        if (_effectManager == null)
        {
            _effectManager = GetComponent<EffectManager>();
            if (_effectManager == null)
            {
                _effectManager = Object.FindFirstObjectByType<EffectManager>();
                if (_effectManager == null)
                {
                    // 최악의 상황 대비: 임시 연출 매니저를 동적 생성하여 먹통 현상을 완전 방지
                    GameObject go = new GameObject("Runtime_EffectManager");
                    _effectManager = go.AddComponent<EffectManager>();
                    Debug.LogWarning("[GameManager] EffectManager를 찾지 못해 런타임에 동적으로 생성했습니다. 인스펙터 설정을 확인해주세요.");
                }
            }
        }

        // 인스펙터 오설정 및 스위칭을 방지하고 계층 구조 경로 오차를 극복하기 위해 재귀 자식 검색 방식을 사용합니다.
        _teacherTimer = GetOrCreateTimer(_teacherGrid);
        _roundTimer = GetOrCreateTimer(_playerGrid);
    }

    private RoundTimer GetOrCreateTimer(GridManager grid)
    {
        if (grid == null) return null;

        // 1. 이미 자식 오브젝트 중 RoundTimer 컴포넌트가 존재한다면 즉시 반환
        RoundTimer timer = grid.GetComponentInChildren<RoundTimer>(true);
        if (timer != null) return timer;

        // 2. 컴포넌트가 없다면 자식 오브젝트 중 이름이 "Timer Canvas"인 오브젝트를 찾아 RoundTimer 추가 후 반환
        Transform[] allChildren = grid.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child.name == "Timer Canvas")
            {
                timer = child.GetComponent<RoundTimer>();
                if (timer == null)
                {
                    timer = child.gameObject.AddComponent<RoundTimer>();
                }
                return timer;
            }
        }

        return null;
    }

    private void Start()
    {
        // 이벤트 연결
        if (_teacherPatternPlayer != null)
            _teacherPatternPlayer.OnPatternComplete += OnTeacherPatternComplete;
        
        if (_roundTimer != null)
            _roundTimer.OnTimerExpired += OnTimerExpired;
        
        // 연출 관련 캐싱 및 초기화 비활성화는 이제 EffectManager가 담당합니다.

        // 입력 비활성화 상태로 시작
        if (_playerInput != null)
            _playerInput.InputEnabled = false;
        
        // 자동 시작 (곡 데이터가 있으면)
        if (_songData != null && _songData.patterns != null && _songData.patterns.Length > 0)
        {
            StartCoroutine(StartGameCoroutine());
        }
        else
        {
            Debug.LogWarning("[GameManager] SongData가 설정되지 않았거나 패턴이 없습니다!");
        }
    }

    private void OnDestroy()
    {
        // 이벤트 해제
        if (_teacherPatternPlayer != null)
            _teacherPatternPlayer.OnPatternComplete -= OnTeacherPatternComplete;
        
        if (_roundTimer != null)
            _roundTimer.OnTimerExpired -= OnTimerExpired;
    }

    private void Update()
    {
        if (_currentState == GameState.PlayerInput)
        {
            // 플레이어 그리드와 선생님 그리드가 100% 일치하는지 실시간 검사
            var result = JudgmentSystem.Judge(_playerGrid, _teacherGrid, 0f, 0f);
            if (result.MatchRatio >= 1.0f)
            {
                float remaining = _roundTimer != null ? _roundTimer.RemainingTime : 0f;
                Debug.Log($"[GameManager] 플레이어가 유예 시간 내 패턴 완성! 남은 시간: {remaining:F2}초");
                PerformJudgment(remaining);
            }
        }
    }

    /// <summary>
    /// 게임 시작 (약간의 딜레이 후 BGM 재생 및 첫 라운드 시작)
    /// </summary>
    private IEnumerator StartGameCoroutine()
    {
        yield return new WaitForSeconds(1f);
        
        // BGM 재생 시작
        if (AudioManager.Instance != null && _songData.bgmClip != null)
        {
            AudioManager.Instance.PlayBGM(_songData.bgmClip, _songData.audioStartOffset);
        }
        
        StartRound();
    }

    /// <summary>
    /// 라운드 시작
    /// </summary>
    public void StartRound()
    {
        if (_songData == null || _songData.patterns == null || CurrentRound >= _songData.patterns.Length)
        {
            Debug.Log("[GameManager] 모든 라운드 완료!");
            
            // BGM 정지
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopBGM();
            
            ChangeState(GameState.Idle);
            return;
        }
        
        PatternData currentPattern = _songData.patterns[CurrentRound];
        _currentGraceTime = _songData.graceTime;
        
        Debug.Log($"[GameManager] 라운드 {CurrentRound + 1} 시작: {currentPattern.patternName}");
        
        // 플레이어 입력 활성화 (선생님 턴과 동시에 입력 가능)
        if (_playerInput != null)
            _playerInput.InputEnabled = true;

        // 양쪽 타이머 비주얼 세팅 및 동시 시작
        float secondsPerBeat = 60f / _songData.bpm;
        float teacherDuration = (currentPattern.musicStartBeat + currentPattern.MaxBeatOffset) * secondsPerBeat;
        float playerDuration = teacherDuration + _songData.graceTime;

        if (_teacherTimer != null)
            _teacherTimer.StartTimer(teacherDuration);

        if (_roundTimer != null)
            _roundTimer.StartTimer(playerDuration);
        
        // 선생님 패턴 재생 시작 (SongData의 bpm, audioStartOffset, graceTime 전달)
        ChangeState(GameState.TeacherPlaying);
        _teacherPatternPlayer.PlayPattern(
            currentPattern, 
            _songData.bpm, 
            _songData.audioStartOffset, 
            _songData.graceTime
        );
    }

    /// <summary>
    /// 선생님 패턴 재생 완료 콜백
    /// </summary>
    private void OnTeacherPatternComplete(float graceTime)
    {
        Debug.Log($"[GameManager] 선생님 패턴 완료. 유예 시간: {graceTime}초");
        
        _currentGraceTime = graceTime;
        ChangeState(GameState.PlayerInput);

        // 선생님 타이머 완료에 따른 리셋/숨김
        if (_teacherTimer != null)
            _teacherTimer.ResetTimer();
        
        // 플레이어 타이머 잔여 시간(유예 시간) 정확하게 재보정 (시각적 리셋/점프 방지)
        if (_roundTimer != null)
            _roundTimer.CalibrateRemainingTime(graceTime);
    }

    /// <summary>
    /// 타이머 만료 콜백
    /// </summary>
    private void OnTimerExpired()
    {
        Debug.Log("[GameManager] 유예 시간 만료! 판정 시작.");
        PerformJudgment(0f);
    }

    /// <summary>
    /// 판정 수행
    /// </summary>
    private void PerformJudgment(float remainingTime)
    {
        ChangeState(GameState.Judging);
        
        // 플레이어 입력 비활성화
        if (_playerInput != null)
            _playerInput.InputEnabled = false;
        
        // 타이머 즉시 정지 및 숨김
        if (_roundTimer != null)
            _roundTimer.ResetTimer();
        
        // 판정 실행
        LastResult = JudgmentSystem.Judge(_playerGrid, _teacherGrid, remainingTime, _currentGraceTime);
        
        Debug.Log($"[GameManager] 판정 결과: {LastResult}");
        
        // 판정 결과 SFX 재생
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayJudgmentSFX(LastResult.Grade);
        
        // 판정 결과 이벤트
        OnJudgmentComplete?.Invoke(LastResult);
        
        // 결과 표시 및 리셋 시작
        StartCoroutine(ShowResultAndReset());
    }

    /// <summary>
    /// 결과 표시 후 리셋 코루틴 (성공/실패 연출 처리 포함)
    /// </summary>
    private IEnumerator ShowResultAndReset()
    {
        ChangeState(GameState.Result);
        
        PatternData currentPattern = _songData.patterns[CurrentRound];

        if (_effectManager != null)
        {
            // 연출 매니저를 통해 효과음과 쿵쿵 펄스/플로팅 텍스트 연출 재생 후 대기
            yield return StartCoroutine(_effectManager.PlayJudgmentEffect(LastResult.Grade, currentPattern, _playerGrid));
        }
        else
        {
            // 연출 매니저가 없는 경우 2초 대기 분기 (디버그 폴백)
            yield return new WaitForSeconds(2f);
        }
        
        ChangeState(GameState.Resetting);
        
        // 양쪽 그리드 리셋
        _playerGrid.ResetAllTiles();
        _teacherGrid.ResetAllTiles();
        
        // 타이머 리셋
        if (_roundTimer != null)
            _roundTimer.ResetTimer();
        if (_teacherTimer != null)
            _teacherTimer.ResetTimer();
        
        yield return new WaitForSeconds(_resetDelay);
        
        // 다음 라운드
        CurrentRound++;
        StartRound();
    }

    /// <summary>
    /// 상태 변경
    /// </summary>
    private void ChangeState(GameState newState)
    {
        _currentState = newState;

        // 연출 매니저를 통해 유예 시간 패널 관리 위임
        if (_effectManager != null)
        {
            _effectManager.SetGraceTimePanelActive(newState == GameState.PlayerInput);
        }

        OnStateChanged?.Invoke(newState);
    }

    /// <summary>
    /// SongData를 런타임에 교체하여 스테이지 변경
    /// </summary>
    public void LoadSong(SongData newSongData)
    {
        StopAllCoroutines();
        
        if (_teacherPatternPlayer != null)
            _teacherPatternPlayer.StopPattern();
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBGM();
        
        _songData = newSongData;
        CurrentRound = 0;
        _playerGrid.ResetAllTiles();
        _teacherGrid.ResetAllTiles();
        if (_roundTimer != null) _roundTimer.ResetTimer();
        if (_teacherTimer != null) _teacherTimer.ResetTimer();
        if (_playerInput != null) _playerInput.InputEnabled = false;
        
        if (_songData != null)
            StartCoroutine(StartGameCoroutine());
    }

    #region 디버그
    /// <summary>
    /// 수동 판정 트리거 (디버그용)
    /// </summary>
    [ContextMenu("Force Judgment")]
    public void ForceJudgment()
    {
        float remaining = _roundTimer != null ? _roundTimer.RemainingTime : 0f;
        PerformJudgment(remaining);
    }

    /// <summary>
    /// 게임 리스타트 (디버그용)
    /// </summary>
    [ContextMenu("Restart Game")]
    public void RestartGame()
    {
        if (_songData != null)
            LoadSong(_songData);
    }
    #endregion
}
