using UnityEngine;

/// <summary>
/// 곡 타임라인에 배치된 개별 패턴 엔트리
/// startBeat으로 곡 내 위치를 지정하고, 해당 위치에서 패턴을 재생
/// </summary>
[System.Serializable]
public class PatternEntry
{
    [Tooltip("이 패턴이 곡에서 시작되는 비트 위치 (0 = 곡 처음)")]
    public float startBeat;
    
    [Tooltip("재생할 패턴 데이터")]
    public PatternData pattern;
}

/// <summary>
/// 곡(스테이지) 데이터 ScriptableObject
/// BGM 클립, BPM, 유예 시간, 라운드별 패턴을 하나로 묶어 관리
/// 이 SO를 교체하면 스테이지 전체가 변경됨
/// Assets > Create > RhythmPuzzle > SongData 로 생성 가능
/// </summary>
[CreateAssetMenu(fileName = "NewSong", menuName = "RhythmPuzzle/SongData")]
public class SongData : ScriptableObject
{
    [Header("곡 정보")]
    [Tooltip("곡 이름")]
    public string songName = "New Song";
    
    [Tooltip("BPM (Beats Per Minute) - 비트 간격 계산에 사용")]
    public float bpm = 120f;

    [Header("오디오")]
    [Tooltip("BGM 음악 파일")]
    public AudioClip bgmClip;
    
    [Tooltip("음악 시작 전 오프셋 (초). 음악 파일 앞쪽 무음 구간 보정용")]
    public float audioStartOffset = 0f;

    [Header("유예 시간")]
    [Tooltip("기본 유예 시간 (비트 수). 패턴별 개별 설정이 없으면 이 값을 사용")]
    public float graceTime = 2f;

    [Tooltip("기본 판정 연출 대기 시간 (비트 수). 패턴별 개별 설정이 없으면 이 값을 사용")]
    public float judgmentBeats = 4f;

    [Header("라운드 패턴 목록")]
    [Tooltip("비트 위치에 배치된 패턴 엔트리들 (startBeat 순으로 정렬 권장)")]
    public PatternEntry[] patternEntries;

    /// <summary>
    /// 1비트의 시간(초)을 계산
    /// </summary>
    public float SecondsPerBeat => 60f / bpm;

    /// <summary>
    /// 전체 라운드 수
    /// </summary>
    public int RoundCount => patternEntries != null ? patternEntries.Length : 0;
}
