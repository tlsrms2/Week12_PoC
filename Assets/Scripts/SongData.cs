using UnityEngine;

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
    [Tooltip("각 패턴 완료 후 플레이어에게 주어지는 유예 시간 (초)")]
    public float graceTime = 2f;

    [Header("라운드 패턴 목록")]
    [Tooltip("라운드별로 순서대로 재생될 패턴 데이터들")]
    public PatternData[] patterns;

    /// <summary>
    /// 1비트의 시간(초)을 계산
    /// </summary>
    public float SecondsPerBeat => 60f / bpm;

    /// <summary>
    /// 전체 라운드 수
    /// </summary>
    public int RoundCount => patterns != null ? patterns.Length : 0;
}
