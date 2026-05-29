using UnityEngine;

/// <summary>
/// RGB 색상 채널 플래그 열거형
/// </summary>
[System.Flags]
public enum ColorChannel
{
    None  = 0,
    Red   = 1 << 0,  // Q키
    Blue  = 1 << 1,  // W키  
    Green = 1 << 2,  // E키
}

/// <summary>
/// 색상 유틸리티 (가산 혼합 변환)
/// </summary>
public static class ColorUtil
{
    // 기본 회색 (아무것도 칠해지지 않은 상태)
    public static readonly Color DefaultGray = new Color(0.588f, 0.588f, 0.588f, 1f);

    /// <summary>
    /// ColorChannel 플래그 → Unity Color 변환 (가산 혼합)
    /// None → 회색, R → 빨강, B → 파랑, G → 초록
    /// R+B → 마젠타, R+G → 옐로우, B+G → 시안, R+G+B → 화이트
    /// </summary>
    public static Color ToColor(ColorChannel channels)
    {
        if (channels == ColorChannel.None)
            return DefaultGray;

        float r = (channels & ColorChannel.Red) != 0 ? 1f : 0f;
        float g = (channels & ColorChannel.Green) != 0 ? 1f : 0f;
        float b = (channels & ColorChannel.Blue) != 0 ? 1f : 0f;
        return new Color(r, g, b, 1f);
    }

    /// <summary>
    /// 두 ColorChannel을 가산 혼합 (OR 연산)
    /// </summary>
    public static ColorChannel Mix(ColorChannel a, ColorChannel b)
    {
        return a | b;
    }
}
