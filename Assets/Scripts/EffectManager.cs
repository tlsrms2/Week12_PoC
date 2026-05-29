using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 연출 매니저 - 판정 텍스트 연출 및 성공/실패시의 매직 서클 리드미컬 비트 펄스 등 비주얼 연출을 단독 관리
/// </summary>
public class EffectManager : MonoBehaviour
{
    [Header("판정 텍스트 설정")]
    [Tooltip("Perfect 등급 판정 시 표시될 텍스트")]
    [SerializeField] private string _textPerfect = "PERFECT";
    
    [Tooltip("Great 등급 판정 시 표시될 텍스트")]
    [SerializeField] private string _textGreat = "GREAT";
    
    [Tooltip("Good 등급 판정 시 표시될 텍스트")]
    [SerializeField] private string _textGood = "GOOD";
    
    [Tooltip("Miss 등급 판정 시 표시될 텍스트")]
    [SerializeField] private string _textMiss = "MISS";

    [Tooltip("판정 텍스트를 출력할 TextMeshPro UI 컴포넌트")]
    [SerializeField] private TextMeshProUGUI _judgmentText;

    [Header("UI 패널 설정")]
    [Tooltip("선생님 턴이 끝나고 플레이어 유예 시간(PlayerInput 상태) 동안에만 활성화될 패널 오브젝트")]
    [SerializeField] private GameObject _graceTimePanel;

    [Tooltip("플레이어 그리드를 가릴 유예 시간 패널 오브젝트")]
    [SerializeField] private GameObject _playerGraceTimePanel;

    [Header("효과 연출 설정")]
    [Tooltip("결과 표시 및 연출 총 시간 (초)")]
    [SerializeField] private float _resultDisplayTime = 2f;

    [Tooltip("성공 연출 시 매직 서클 서클1과 배경들이 커지는 최대 강도 (원래 크기 대비 추가 비율)")]
    [Range(0f, 1f)]
    [SerializeField] private float _thumpIntensity = 0.05f;

    [Header("배경 비트 쿵쿵 연출 - 플레이어")]
    [Tooltip("플레이어 쪽 배경 Transform 목록")]
    [SerializeField] private List<Transform> _playerBeatBackgrounds = new List<Transform>();

    [Header("배경 비트 쿵쿵 연출 - 선생님")]
    [Tooltip("선생님 쪽 배경 Transform 목록")]
    [SerializeField] private List<Transform> _teacherBeatBackgrounds = new List<Transform>();

    [Header("비트 쿵쿵 세기 설정")]
    [Tooltip("노래 BPM 비트에 맞춘 평소 쿵쿵 연출의 최대 강도 (+0.05 기본)")]
    [SerializeField] private float _beatThumpIntensity = 0.05f;

    private struct ThumpTarget
    {
        public Transform transform;
        public Vector3 originalScale;
    }
    private List<ThumpTarget> _playerThumpTargets = new List<ThumpTarget>();
    private List<ThumpTarget> _teacherThumpTargets = new List<ThumpTarget>();
    private bool _backgroundScalesCached = false;

    // Circle (1) 캐싱 필드
    private Transform _playerCircle1 = null;
    private Vector3 _playerCircle1OriginalScale = Vector3.one;

    private Transform _teacherCircle1 = null;
    private Vector3 _teacherCircle1OriginalScale = Vector3.one;

    private Transform _teacherCircle3 = null;
    private Vector3 _teacherCircle3OriginalScale = Vector3.one;

    private bool _wasBGMPlaying = false;
    private bool _isSuccessAnimPlaying = false;

    private struct CircleCache
    {
        public SpriteRenderer renderer;
        public Color originalColor;
    }

    private Vector2 _initialJudgmentTextPos;
    private bool _hasInitialPos = false;

    private void Awake()
    {
        // 유예 시간 패널 초기 비활성화
        if (_graceTimePanel != null)
            _graceTimePanel.SetActive(false);

        if (_playerGraceTimePanel != null)
            _playerGraceTimePanel.SetActive(false);

        if (_judgmentText != null)
        {
            _initialJudgmentTextPos = _judgmentText.rectTransform.anchoredPosition;
            _hasInitialPos = true;
            _judgmentText.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        // 인스펙터 직렬화 오류로 0이 들어가 있거나 할 경우 안전한 기본값 보장
        if (_beatThumpIntensity <= 0f)
            _beatThumpIntensity = 0.05f;

        if (_thumpIntensity <= 0f)
            _thumpIntensity = 0.05f;

        CacheBackgroundScales();
    }

    private void Update()
    {
        if (GameManager.Instance == null || AudioManager.Instance == null) return;

        bool isBGMPlaying = AudioManager.Instance.IsBGMPlaying;

        if (GameManager.Instance.CurrentSongData != null)
        {
            _wasBGMPlaying = true;

            float bpm = GameManager.Instance.CurrentSongData.bpm;
            if (bpm > 0f)
            {
                float beatDuration = 60f / bpm;
                
                // BGM 오디오가 재생 중이면 오디오 싱크 타임을 사용하고, 아니면 Time.time 기반으로 완벽한 박자 폴백 작동
                float time = isBGMPlaying ? AudioManager.Instance.CurrentBGMTime : Time.time;
                float beatProgress = (time / beatDuration) % 1f;

                // 쿵쿵거리는 디제잉 비트 스케일 연출
                // 지수적으로 급격히 감소하도록 설계
                float scaleOffset = Mathf.Pow(1f - beatProgress, 4f) * _beatThumpIntensity;

                if (!_backgroundScalesCached)
                {
                    CacheBackgroundScales();
                }

                CacheCircles();

                // 성공 연출 중이 아닐 때만 업데이트 구동
                if (!_isSuccessAnimPlaying)
                {
                    // 1. 플레이어 부분 (항상 비트에 맞춰 움직임)
                    foreach (var target in _playerThumpTargets)
                    {
                        if (target.transform != null)
                        {
                            target.transform.localScale = target.originalScale * (1f + scaleOffset);
                        }
                    }
                    if (_playerCircle1 != null)
                    {
                        _playerCircle1.localScale = _playerCircle1OriginalScale * (1f + scaleOffset);
                    }

                    // 2. 선생님 부분
                    // 가림 패널이 켜지는 시점 (즉 PlayerInput 상태)에는 멈추고 원래 크기 유지
                    bool isPlayerInputState = (GameManager.Instance.CurrentState == GameState.PlayerInput);

                    if (isPlayerInputState)
                    {
                        // 선생님 부분 멈춤 (원래 크기로 고정)
                        foreach (var target in _teacherThumpTargets)
                        {
                            if (target.transform != null)
                            {
                                target.transform.localScale = target.originalScale;
                            }
                        }
                        if (_teacherCircle1 != null)
                        {
                            _teacherCircle1.localScale = _teacherCircle1OriginalScale;
                        }
                    }
                    else
                    {
                        // 선생님 턴 중이거나 평소 상태면 같이 쿵쿵거림
                        foreach (var target in _teacherThumpTargets)
                        {
                            if (target.transform != null)
                            {
                                target.transform.localScale = target.originalScale * (1f + scaleOffset);
                            }
                        }
                        if (_teacherCircle1 != null)
                        {
                            _teacherCircle1.localScale = _teacherCircle1OriginalScale * (1f + scaleOffset);
                        }
                    }
                }
            }
        }
        else
        {
            if (_wasBGMPlaying)
            {
                _wasBGMPlaying = false;
                RestoreOriginalScales();
            }
        }
    }

    private void CacheBackgroundScales()
    {
        _playerThumpTargets.Clear();
        if (_playerBeatBackgrounds != null)
        {
            foreach (var bg in _playerBeatBackgrounds)
            {
                if (bg != null)
                {
                    Vector3 original = bg.localScale;
                    if (original.sqrMagnitude < 0.001f) original = Vector3.one;
                    _playerThumpTargets.Add(new ThumpTarget { transform = bg, originalScale = original });
                }
            }
        }

        _teacherThumpTargets.Clear();
        if (_teacherBeatBackgrounds != null)
        {
            foreach (var bg in _teacherBeatBackgrounds)
            {
                if (bg != null)
                {
                    Vector3 original = bg.localScale;
                    if (original.sqrMagnitude < 0.001f) original = Vector3.one;
                    _teacherThumpTargets.Add(new ThumpTarget { transform = bg, originalScale = original });
                }
            }
        }
        _backgroundScalesCached = true;
    }

    private void CacheCircles()
    {
        if (GameManager.Instance == null) return;

        // 플레이어 Circle (1) 캐싱
        if (_playerCircle1 == null && GameManager.Instance.PlayerGrid != null)
        {
            Transform playerCircleTrans = FindMagicCircle(GameManager.Instance.PlayerGrid.transform);
            if (playerCircleTrans != null)
            {
                _playerCircle1 = FindChildRecursive(playerCircleTrans, "Circle (1)");
                if (_playerCircle1 != null)
                {
                    Vector3 original = _playerCircle1.localScale;
                    _playerCircle1OriginalScale = (original.sqrMagnitude < 0.001f) ? Vector3.one : original;
                }
            }
        }

        // 선생님 Circle (1) 캐싱
        if (_teacherCircle1 == null && GameManager.Instance.TeacherGrid != null)
        {
            Transform teacherCircleTrans = FindMagicCircle(GameManager.Instance.TeacherGrid.transform);
            if (teacherCircleTrans != null)
            {
                _teacherCircle1 = FindChildRecursive(teacherCircleTrans, "Circle (1)");
                if (_teacherCircle1 != null)
                {
                    Vector3 original = _teacherCircle1.localScale;
                    _teacherCircle1OriginalScale = (original.sqrMagnitude < 0.001f) ? Vector3.one : original;
                }
            }
        }

        // 선생님 Circle (3) 캐싱
        if (_teacherCircle3 == null && GameManager.Instance.TeacherGrid != null)
        {
            Transform teacherCircleTrans = FindMagicCircle(GameManager.Instance.TeacherGrid.transform);
            if (teacherCircleTrans != null)
            {
                _teacherCircle3 = FindChildRecursive(teacherCircleTrans, "Circle (3)");
                if (_teacherCircle3 != null)
                {
                    Vector3 original = _teacherCircle3.localScale;
                    _teacherCircle3OriginalScale = (original.sqrMagnitude < 0.001f) ? Vector3.one : original;
                }
            }
        }
    }

    private void RestoreOriginalScales()
    {
        foreach (var target in _playerThumpTargets)
        {
            if (target.transform != null)
                target.transform.localScale = target.originalScale;
        }

        foreach (var target in _teacherThumpTargets)
        {
            if (target.transform != null)
                target.transform.localScale = target.originalScale;
        }

        if (!_isSuccessAnimPlaying)
        {
            if (_playerCircle1 != null)
            {
                _playerCircle1.localScale = _playerCircle1OriginalScale;
            }
            if (_teacherCircle3 != null)
            {
                _teacherCircle3.localScale = _teacherCircle3OriginalScale;
            }
        }
    }

    /// <summary>
    /// 유예 시간 UI 패널 활성화 여부 설정
    /// </summary>
    public void SetGraceTimePanelActive(bool active)
    {
        if (_graceTimePanel != null)
            _graceTimePanel.SetActive(active);
    }

    /// <summary>
    /// 플레이어 가림막 UI 패널 활성화 여부 설정
    /// </summary>
    public void SetPlayerGraceTimePanelActive(bool active)
    {
        if (_playerGraceTimePanel != null)
            _playerGraceTimePanel.SetActive(active);
    }

    /// <summary>
    /// 판정에 따른 텍스트 및 원형 맥박 성공 연출 재생 코루틴
    /// </summary>
    public IEnumerator PlayJudgmentEffect(JudgmentGrade grade, PatternData pattern, GridManager playerGrid)
    {
        // 판정이 Miss가 아닐 때만 플레이어 가림막을 즉시 걷어줍니다.
        // Miss 판정이 떴을 때 플레이어 가림막(내 가리개)이 결과 연출(Result 2초) 동안 즉시 바로 덮여버리면 답답하므로,
        // 결과 연출 2초 동안에는 켜지 않고 가만히 놔두었다가 대기 상태(Resetting)로 돌입할 때 비로소 덮이게 지연 연출합니다.
        if (grade != JudgmentGrade.Miss)
        {
            SetPlayerGraceTimePanelActive(false);
        }

        // 1. 등급에 따른 텍스트 및 색상 결정
        string gradeText = "";
        Color gradeColor = Color.white;

        switch (grade)
        {
            case JudgmentGrade.Perfect:
                gradeText = _textPerfect;
                gradeColor = new Color(1f, 0.84f, 0f); // 골드
                break;
            case JudgmentGrade.Great:
                gradeText = _textGreat;
                gradeColor = new Color(0.9f, 0.3f, 0.9f); // 마젠타/보라
                break;
            case JudgmentGrade.Good:
                gradeText = _textGood;
                gradeColor = new Color(0f, 0.8f, 1f); // 시안/하늘색
                break;
            case JudgmentGrade.Miss:
                gradeText = _textMiss;
                gradeColor = new Color(1f, 0.2f, 0.2f); // 빨간색
                break;
        }

        // 플로팅 텍스트 효과 개시
        StartCoroutine(ShowJudgmentText(gradeText, gradeColor, playerGrid));

        // 2. MISS가 아닌 경우 성공 연출 재생
        if (grade != JudgmentGrade.Miss)
        {
            List<Color> patternColors = new List<Color>();
            if (pattern != null && pattern.steps != null)
            {
                foreach (var step in pattern.steps)
                {
                    if (step.color != ColorChannel.None)
                    {
                        Color c = ColorUtil.ToColor(step.color);
                        if (!patternColors.Contains(c))
                            patternColors.Add(c);
                    }
                }
            }

            if (patternColors.Count == 0)
                patternColors.Add(Color.white);

            yield return StartCoroutine(PlaySuccessAnimation(patternColors, playerGrid));
        }
        else
        {
            // MISS인 경우 연출 없이 단순히 대기
            yield return new WaitForSeconds(_resultDisplayTime);
        }
    }

    /// <summary>
    /// 쿵쿵거리는 디제잉 비트 스케일 및 색상 순환 성공 연출 코루틴
    /// </summary>
    private IEnumerator PlaySuccessAnimation(List<Color> patternColors, GridManager playerGrid)
    {
        if (playerGrid == null) yield break;

        _isSuccessAnimPlaying = true;

        List<CircleCache> cachedCircles = new List<CircleCache>();
        CacheCircleRenderers(playerGrid.transform, cachedCircles);

        // 선생님 그리드의 Circle (3)도 함께 무지개 색상이 통통 바뀌도록 추가 등록
        if (GameManager.Instance != null && GameManager.Instance.TeacherGrid != null)
        {
            CacheTeacherCircle3Renderer(GameManager.Instance.TeacherGrid.transform, cachedCircles);
        }

        CacheCircles();
        if (!_backgroundScalesCached)
        {
            CacheBackgroundScales();
        }

        int totalPulses = 7;
        float pulseDuration = _resultDisplayTime / totalPulses;

        for (int p = 0; p < totalPulses; p++)
        {
            Color currentPulseColor = patternColors[p % patternColors.Count];

            foreach (var cache in cachedCircles)
            {
                if (cache.renderer != null)
                    cache.renderer.color = currentPulseColor;
            }

            // 디제잉 쿵쿵(Thump) 펄스 연출: 날카로운 어택 및 강력한 지수 감쇄
            float pulseElapsed = 0f;
            while (pulseElapsed < pulseDuration)
            {
                pulseElapsed += Time.deltaTime;
                float progress = pulseElapsed / pulseDuration;

                float scaleOffset = Mathf.Max(0f, Mathf.Pow(1f - progress, 2f)) * _thumpIntensity;

                // 1. 내 그리드 Circle (1)
                if (_playerCircle1 != null)
                {
                    _playerCircle1.localScale = _playerCircle1OriginalScale * (1f + scaleOffset);
                }

                // 2. 선생님 그리드 Circle (1)
                if (_teacherCircle1 != null)
                {
                    _teacherCircle1.localScale = _teacherCircle1OriginalScale * (1f + scaleOffset);
                }

                // 선생님 그리드 Circle (3)
                if (_teacherCircle3 != null)
                {
                    _teacherCircle3.localScale = _teacherCircle3OriginalScale * (1f + scaleOffset);
                }

                // 3. 내 쪽 설정 배경들
                foreach (var target in _playerThumpTargets)
                {
                    if (target.transform != null)
                    {
                        target.transform.localScale = target.originalScale * (1f + scaleOffset);
                    }
                }

                // 4. 선생님 쪽 설정 배경들
                foreach (var target in _teacherThumpTargets)
                {
                    if (target.transform != null)
                    {
                        target.transform.localScale = target.originalScale * (1f + scaleOffset);
                    }
                }

                yield return null;
            }
        }

        // 스케일 및 색상 완벽 복원
        RestoreOriginalScales();

        foreach (var cache in cachedCircles)
        {
            if (cache.renderer != null)
                cache.renderer.color = cache.originalColor;
        }

        _isSuccessAnimPlaying = false;
    }

    /// <summary>
    /// TextMeshPro UI를 활용해 제자리에서 성공 시 쿵쿵거림(페이드 없음), MISS 시에는 쿵쿵거리지 않고(페이드 없음) 선명하게 유지되다 사라지는 판정 연출
    /// </summary>
    private IEnumerator ShowJudgmentText(string text, Color color, GridManager playerGrid)
    {
        if (_judgmentText == null) yield break;

        if (!_hasInitialPos)
        {
            _initialJudgmentTextPos = _judgmentText.rectTransform.anchoredPosition;
            _hasInitialPos = true;
        }

        // 제자리 지정을 위해 최초 위치 및 스케일 초기화
        _judgmentText.rectTransform.anchoredPosition = _initialJudgmentTextPos;
        _judgmentText.transform.localScale = Vector3.one;
        
        _judgmentText.gameObject.SetActive(true);
        _judgmentText.text = text;

        // 모든 판정 텍스트는 연출 중 흐릿해지지 않고 항상 100% 선명하게 유지됨 (알파값 1.0f 강제 지정)
        Color solidColor = color;
        solidColor.a = 1f;
        _judgmentText.color = solidColor;

        bool isMiss = text.Equals(_textMiss, System.StringComparison.OrdinalIgnoreCase);

        if (isMiss)
        {
            // 2. MISS 판정 연출: 쿵쿵거리지 않고, 흐려지지도 않고 제자리에 선명하게 유지되다가 종료 시 즉시 소멸
            float elapsed = 0f;
            while (elapsed < _resultDisplayTime)
            {
                elapsed += Time.deltaTime;

                // 제자리 고정 & 스케일 1.0x 고정 & 선명도 1.0f 유지
                _judgmentText.rectTransform.anchoredPosition = _initialJudgmentTextPos;
                _judgmentText.transform.localScale = Vector3.one;
                _judgmentText.color = solidColor;

                yield return null;
            }
        }
        else
        {
            // 1. 성공 판정(PERFECT, GREAT, GOOD) 연출: 
            // - 색이 흐릿해지면 안 됨 (알파값 1.0f 100% 불투명도 유지)
            // - 제자리에서 매직 서클의 쿵쿵 박자에 맞춰 동기화되어 쿵쿵거림 (7번의 펄스)
            int totalPulses = 7;
            float pulseDuration = _resultDisplayTime / totalPulses;

            for (int p = 0; p < totalPulses; p++)
            {
                float pulseElapsed = 0f;
                while (pulseElapsed < pulseDuration)
                {
                    pulseElapsed += Time.deltaTime;

                    float progress = pulseElapsed / pulseDuration;

                    // 제자리 고정
                    _judgmentText.rectTransform.anchoredPosition = _initialJudgmentTextPos;
                    _judgmentText.color = solidColor;

                    // 매직 서클의 쿵쿵거림과 완벽하게 1:1 동기화된 스케일 쿵쿵 연출
                    float scaleOffset = Mathf.Max(0f, Mathf.Pow(1f - progress, 2f)) * 0.35f;
                    float scale = 1f + scaleOffset;
                    _judgmentText.transform.localScale = new Vector3(scale, scale, 1f);

                    yield return null;
                }
            }
        }

        _judgmentText.gameObject.SetActive(false);
        _judgmentText.rectTransform.anchoredPosition = _initialJudgmentTextPos;
        _judgmentText.transform.localScale = Vector3.one;
    }

    private Transform FindMagicCircle(Transform gridRoot)
    {
        if (gridRoot == null) return null;
        Transform[] allChildren = gridRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in allChildren)
        {
            if (t.name == "MagicCircle")
                return t;
        }
        return null;
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private void CacheCircleRenderers(Transform gridRoot, List<CircleCache> cacheList)
    {
        Transform magicCircle = FindMagicCircle(gridRoot);
        if (magicCircle == null) return;

        Transform circle1 = FindChildRecursive(magicCircle, "Circle (1)");

        if (circle1 != null)
        {
            SpriteRenderer r = circle1.GetComponent<SpriteRenderer>();
            if (r != null)
                cacheList.Add(new CircleCache { renderer = r, originalColor = r.color });
        }
    }

    private void CacheTeacherCircle3Renderer(Transform gridRoot, List<CircleCache> cacheList)
    {
        Transform magicCircle = FindMagicCircle(gridRoot);
        if (magicCircle == null) return;

        Transform circle3 = FindChildRecursive(magicCircle, "Circle (3)");
        if (circle3 != null)
        {
            SpriteRenderer r = circle3.GetComponent<SpriteRenderer>();
            if (r != null)
                cacheList.Add(new CircleCache { renderer = r, originalColor = r.color });
        }
    }
}
