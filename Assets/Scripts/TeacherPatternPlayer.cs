using UnityEngine;
using System.Collections;

/// <summary>
/// 선생님 패턴 재생기
/// TeacherGrid에 부착하여 PatternData에 정의된 패턴을 AudioSource.time 기반으로 재생
/// BGM과 동기화하여 비트에 맞춰 타일에 색상을 입힘
/// </summary>
public class TeacherPatternPlayer : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private GridManager _gridManager;
    
    /// <summary>패턴 재생 완료 시 호출 (유예 시간 파라미터 전달)</summary>
    public event System.Action<float> OnPatternComplete;
    
    /// <summary>현재 재생 중 여부</summary>
    public bool IsPlaying { get; private set; }
    
    private Coroutine _playCoroutine;

    private void Awake()
    {
        if (_gridManager == null)
            _gridManager = GetComponent<GridManager>();
    }

    /// <summary>
    /// 패턴 재생 시작 (AudioSource.time 기반 비트 싱크)
    /// </summary>
    /// <param name="pattern">재생할 패턴 데이터</param>
    /// <param name="bpm">곡의 BPM</param>
    /// <param name="audioStartOffset">곡의 오디오 시작 오프셋 (초)</param>
    /// <param name="graceTime">패턴 완료 후 유예 시간 (초)</param>
    /// <param name="startBeat">곡 내에서 이 패턴이 시작되는 비트 위치</param>
    public void PlayPattern(PatternData pattern, float bpm, float audioStartOffset, float graceTime, float startBeat)
    {
        if (pattern == null)
        {
            Debug.LogError("[TeacherPatternPlayer] PatternData가 null입니다!");
            return;
        }
        
        if (_playCoroutine != null)
            StopCoroutine(_playCoroutine);
        
        _playCoroutine = StartCoroutine(PlayPatternCoroutine(pattern, bpm, audioStartOffset, graceTime, startBeat));
    }

    /// <summary>
    /// 패턴 재생 중지
    /// </summary>
    public void StopPattern()
    {
        if (_playCoroutine != null)
        {
            StopCoroutine(_playCoroutine);
            _playCoroutine = null;
        }
        IsPlaying = false;
    }

    /// <summary>
    /// 패턴 순차 재생 코루틴 — AudioSource.time 기반 비트 싱크
    /// 매 프레임 AudioManager.Instance.CurrentBGMTime을 읽어 현재 비트를 계산하고,
    /// 스텝의 (startBeat + beatOffset)에 도달하면 해당 스텝을 실행
    /// </summary>
    private IEnumerator PlayPatternCoroutine(PatternData pattern, float bpm, float audioStartOffset, float graceTime, float startBeat)
    {
        IsPlaying = true;
        float secondsPerBeat = 60f / bpm;
        int stepIndex = 0;
        
        // 스텝을 beatOffset 기준으로 정렬
        PatternStep[] sortedSteps = (PatternStep[])pattern.steps.Clone();
        System.Array.Sort(sortedSteps, (a, b) => a.beatOffset.CompareTo(b.beatOffset));
        
        Debug.Log($"[TeacherPatternPlayer] 패턴 '{pattern.patternName}' 재생 시작 (BPM: {bpm}, startBeat: {startBeat})");
        
        float startTime = Time.time;
        
        while (stepIndex < sortedSteps.Length)
        {
            // AudioManager에서 현재 BGM 재생 시간을 읽어 비트 계산
            float currentTime;
            if (AudioManager.Instance != null && AudioManager.Instance.IsBGMPlaying)
            {
                currentTime = AudioManager.Instance.CurrentBGMTime - audioStartOffset;
            }
            else
            {
                // AudioManager가 없거나 BGM이 재생 중이 아니면 패턴 시작부터 경과 시간 사용 (폴백)
                currentTime = Time.time - startTime;
            }
            
            float currentBeat = currentTime / secondsPerBeat;
            
            // 현재 비트에서 해당 패턴의 스텝들이 도달했는지 확인
            // 스텝의 절대 비트 = startBeat + beatOffset
            while (stepIndex < sortedSteps.Length)
            {
                float stepAbsoluteBeat = startBeat + sortedSteps[stepIndex].beatOffset;
                
                if (currentBeat >= stepAbsoluteBeat)
                {
                    PatternStep step = sortedSteps[stepIndex];
                    _gridManager.AddTileColor(step.triangleIndex, step.color);
                    
                    Debug.Log($"[TeacherPatternPlayer] Beat {currentBeat:F2}: Tile {step.triangleIndex} → {step.color}");
                    stepIndex++;
                }
                else
                {
                    break; // 아직 도달하지 않은 스텝
                }
            }
            
            yield return null;
        }
        
        // 모든 스텝 재생 후, 0.5비트 동안 선생님 보드가 보이도록 지연 (대기 패널 활성화 유예)
        float endBeat = startBeat + pattern.MaxBeatOffset + 0.5f;
        while (true)
        {
            float currentTime;
            if (AudioManager.Instance != null && AudioManager.Instance.IsBGMPlaying)
            {
                currentTime = AudioManager.Instance.CurrentBGMTime - audioStartOffset;
            }
            else
            {
                currentTime = Time.time - startTime;
            }
            
            float currentBeat = currentTime / secondsPerBeat;
            if (currentBeat >= endBeat)
                break;
            
            yield return null;
        }

        IsPlaying = false;
        _playCoroutine = null;
        
        Debug.Log($"[TeacherPatternPlayer] 패턴 '{pattern.patternName}' 완료. 유예 시간: {graceTime}초");
        
        // 패턴 완료 알림 (유예 시간 전달)
        OnPatternComplete?.Invoke(graceTime);
    }
}
