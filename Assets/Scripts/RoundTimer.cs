using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 라운드 타이머 - Timer Canvas에 부착
/// Circle Timer(Image)의 fillAmount와 Circle Pointer의 회전으로 시각적 타이머 표현
/// </summary>
public class RoundTimer : MonoBehaviour
{
    [Header("UI 참조")]
    [Tooltip("원형 타이머 이미지 (Image.type = Filled)")]
    [SerializeField] private Image _circleTimerImage;
    
    [Tooltip("타이머 포인터 (회전)")]
    [SerializeField] private RectTransform _circlePointer;
    
    [Header("색상 설정")]
    [SerializeField] private Color _colorFull = new Color(0.2f, 0.8f, 0.2f);    // 시간 많이 남음 - 초록
    [SerializeField] private Color _colorMid = new Color(0.9f, 0.9f, 0.1f);     // 중간 - 노랑
    [SerializeField] private Color _colorLow = new Color(0.9f, 0.2f, 0.2f);     // 시간 부족 - 빨강
    
    /// <summary>타이머 만료 시 호출</summary>
    public event System.Action OnTimerExpired;
    
    /// <summary>현재 타이머 동작 중 여부</summary>
    public bool IsRunning { get; private set; }
    
    /// <summary>남은 시간 (초)</summary>
    public float RemainingTime { get; private set; }
    
    /// <summary>전체 시간 (초)</summary>
    public float TotalTime { get; private set; }
    
    /// <summary>남은 시간 비율 (0~1)</summary>
    public float RemainingRatio => TotalTime > 0 ? Mathf.Clamp01(RemainingTime / TotalTime) : 0f;

    private void Awake()
    {
        if (_circleTimerImage == null)
        {
            Transform t = transform.Find("Circle Timer");
            if (t != null)
                _circleTimerImage = t.GetComponent<Image>();
        }
        
        if (_circlePointer == null)
        {
            Transform t = transform.Find("Circle Pointer");
            if (t != null)
                _circlePointer = t as RectTransform;
        }

        // 초기 상태: 타이머 숨기기
        SetTimerVisible(false);
    }

    private void Update()
    {
        if (!IsRunning) return;
        
        RemainingTime -= Time.deltaTime;
        
        if (RemainingTime <= 0f)
        {
            RemainingTime = 0f;
            IsRunning = false;
            UpdateVisual();
            OnTimerExpired?.Invoke();
            return;
        }
        
        UpdateVisual();
    }

    /// <summary>
    /// 타이머 시작
    /// </summary>
    public void StartTimer(float duration)
    {
        TotalTime = duration;
        RemainingTime = duration;
        IsRunning = true;
        SetTimerVisible(true);
        UpdateVisual();
    }

    /// <summary>
    /// 남은 시간만 정밀하게 재조정 (TotalTime은 유지하여 시각적 점프 방지)
    /// </summary>
    public void CalibrateRemainingTime(float newRemainingTime)
    {
        RemainingTime = Mathf.Clamp(newRemainingTime, 0f, TotalTime);
        UpdateVisual();
    }

    /// <summary>
    /// 타이머 정지
    /// </summary>
    public void StopTimer()
    {
        IsRunning = false;
    }

    /// <summary>
    /// 타이머 리셋 및 숨기기
    /// </summary>
    public void ResetTimer()
    {
        IsRunning = false;
        RemainingTime = 0f;
        TotalTime = 0f;
        SetTimerVisible(false);
    }

    /// <summary>
    /// 타이머 시각적 요소 업데이트
    /// </summary>
    private void UpdateVisual()
    {
        float ratio = RemainingRatio;
        
        // Circle Timer fillAmount 업데이트
        if (_circleTimerImage != null)
        {
            _circleTimerImage.fillAmount = ratio;
            
            // 남은 시간에 따른 색상 변화
            Color timerColor;
            if (ratio > 0.5f)
                timerColor = Color.Lerp(_colorMid, _colorFull, (ratio - 0.5f) * 2f);
            else
                timerColor = Color.Lerp(_colorLow, _colorMid, ratio * 2f);
            
            _circleTimerImage.color = timerColor;
        }
        
        // Circle Pointer 회전 업데이트 (360도 회전)
        if (_circlePointer != null)
        {
            float angle = ratio * 360f;
            _circlePointer.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }

    /// <summary>
    /// 타이머 UI 표시/숨기기
    /// </summary>
    private void SetTimerVisible(bool visible)
    {
        if (_circleTimerImage != null)
            _circleTimerImage.gameObject.SetActive(visible);
        if (_circlePointer != null)
            _circlePointer.gameObject.SetActive(visible);
    }
}
