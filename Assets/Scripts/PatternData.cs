using UnityEngine;

/// <summary>
/// 패턴의 개별 스텝 데이터
/// </summary>
[System.Serializable]
public class PatternStep
{
    [Tooltip("타일 인덱스 (1~24)")]
    public int triangleIndex;
    
    [Tooltip("칠할 색상 채널")]
    public ColorChannel color;
    
    [Tooltip("비트 기준 타이밍 오프셋 (0부터 시작, 1 = 1비트)")]
    public float beatOffset;
}

/// <summary>
/// 선생님 패턴 데이터 (ScriptableObject)
/// BPM은 SongData에서 관리. 유예 시간은 패턴별 개별 설정 가능 (미설정 시 SongData 기본값 사용)
/// Assets > Create > RhythmPuzzle > PatternData 로 생성 가능
/// </summary>
[CreateAssetMenu(fileName = "NewPattern", menuName = "RhythmPuzzle/PatternData")]
public class PatternData : ScriptableObject
{
    [Header("기본 설정")]
    [Tooltip("패턴 이름")]
    public string patternName = "New Pattern";
    
    [Tooltip("패턴별 유예 시간 (비트 수). -1이면 SongData의 graceTime 사용")]
    public float graceTime = -1f;

    [Tooltip("패턴별 판정 연출 대기 시간 (비트 수). -1이면 SongData의 judgmentBeats 사용")]
    public float judgmentBeats = -1f;
    
    [Header("패턴 스텝")]
    [Tooltip("순서대로 재생될 패턴 스텝들")]
    public PatternStep[] steps;

    /// <summary>
    /// 패턴 내 마지막 스텝의 비트 오프셋 (패턴 길이 계산용)
    /// </summary>
    public float MaxBeatOffset
    {
        get
        {
            if (steps == null || steps.Length == 0) return 0f;
            float maxBeat = 0f;
            foreach (var step in steps)
            {
                if (step.beatOffset > maxBeat)
                    maxBeat = step.beatOffset;
            }
            return maxBeat;
        }
    }
}
