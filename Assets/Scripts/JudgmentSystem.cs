using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 판정 결과 등급
/// 유예 시간 남은 초에 따라 결정
/// </summary>
public enum JudgmentGrade
{
    Perfect,   // 색이 모두 일치 + 빠른 완성
    Great,     // 색이 모두 일치 + 보통 속도
    Good,      // 색이 모두 일치 + 느린 완성  
    Miss       // 색이 불일치
}

/// <summary>
/// 판정 결과 데이터
/// </summary>
public struct JudgmentResult
{
    public JudgmentGrade Grade;
    public int CorrectCount;     // 일치한 타일 수
    public int TotalCount;       // 전체 타일 수  
    public float MatchRatio;     // 일치 비율 (0~1)
    public float RemainingTime;  // 남은 유예 시간
    
    public override string ToString()
    {
        return $"[{Grade}] {CorrectCount}/{TotalCount} ({MatchRatio:P0}) 남은시간: {RemainingTime:F1}s";
    }
}

/// <summary>
/// 판정 시스템 - 플레이어 그리드와 선생님 그리드를 비교하여 판정
/// </summary>
public static class JudgmentSystem
{
    /// <summary>
    /// 두 그리드의 타일 상태를 비교하여 판정 결과 반환
    /// </summary>
    /// <param name="playerGrid">플레이어 그리드</param>
    /// <param name="teacherGrid">선생님 그리드</param>
    /// <param name="remainingTime">유예 시간 남은 초</param>
    /// <param name="totalGraceTime">전체 유예 시간</param>
    public static JudgmentResult Judge(
        GridManager playerGrid, 
        GridManager teacherGrid, 
        float remainingTime,
        float totalGraceTime)
    {
        var playerStates = playerGrid.GetAllTileStates();
        var teacherStates = teacherGrid.GetAllTileStates();
        
        int correctCount = 0;
        int totalCount = 0;
        
        // 선생님 그리드의 칠해진 타일만 비교 (칠해지지 않은 타일은 무시하거나, 
        // 전체 타일을 비교할 수도 있음 - 여기서는 전체 비교)
        HashSet<int> allIndices = new HashSet<int>();
        foreach (var key in playerStates.Keys) allIndices.Add(key);
        foreach (var key in teacherStates.Keys) allIndices.Add(key);
        
        foreach (int index in allIndices)
        {
            totalCount++;
            
            ColorChannel playerColor = playerStates.ContainsKey(index) 
                ? playerStates[index] : ColorChannel.None;
            ColorChannel teacherColor = teacherStates.ContainsKey(index) 
                ? teacherStates[index] : ColorChannel.None;
            
            if (playerColor == teacherColor)
                correctCount++;
        }
        
        float matchRatio = totalCount > 0 ? (float)correctCount / totalCount : 0f;
        
        // 등급 판정: 먼저 일치 여부 확인, 그 다음 남은 시간으로 세분화
        JudgmentGrade grade;
        if (matchRatio < 1.0f)
        {
            // 불일치가 있으면 Miss
            grade = JudgmentGrade.Miss;
        }
        else
        {
            // 100% 일치 시 남은 유예 시간 비율로 등급 결정
            float timeRatio = totalGraceTime > 0 ? remainingTime / totalGraceTime : 0f;
            
            if (timeRatio >= 0.66f)
                grade = JudgmentGrade.Perfect;  // 유예 시간 2/3 이상 남음
            else if (timeRatio >= 0.33f)
                grade = JudgmentGrade.Great;    // 유예 시간 1/3 이상 남음
            else
                grade = JudgmentGrade.Good;     // 간신히 완성
        }
        
        return new JudgmentResult
        {
            Grade = grade,
            CorrectCount = correctCount,
            TotalCount = totalCount,
            MatchRatio = matchRatio,
            RemainingTime = remainingTime
        };
    }
}
