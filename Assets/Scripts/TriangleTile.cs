using UnityEngine;

/// <summary>
/// 개별 삼각형 타일 컴포넌트
/// 각 Triangle_N 오브젝트에 부착하여 색상 상태를 관리
/// SpriteRenderer.color를 직접 변경하여 색상 표현
/// </summary>
public class TriangleTile : MonoBehaviour
{
    [Header("현재 상태")]
    [SerializeField] private ColorChannel _currentColor = ColorChannel.None;
    
    private SpriteRenderer _spriteRenderer;
    
    private int _tileIndex = -1;
    
    /// <summary>현재 색상 채널 상태</summary>
    public ColorChannel CurrentColor => _currentColor;
    
    /// <summary>타일 인덱스 (1~24, 이름에서 파싱)</summary>
    public int TileIndex 
    { 
        get
        {
            if (_tileIndex == -1)
            {
                string name = gameObject.name;
                string indexStr = name.Replace("Triangle_", "");
                if (int.TryParse(indexStr, out int index))
                    _tileIndex = index;
                else
                    _tileIndex = 0;
            }
            return _tileIndex;
        }
    }

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 초기 색상 적용
        ApplyColor();
    }

    /// <summary>
    /// 색상 채널 덧칠 (가산 혼합)
    /// 기존 색에 새 색을 OR 연산으로 추가
    /// </summary>
    public void AddColor(ColorChannel channel)
    {
        _currentColor |= channel;
        ApplyColor();
    }

    /// <summary>
    /// 특정 색상으로 직접 설정 (선생님 패턴 재생용)
    /// </summary>
    public void SetColor(ColorChannel channel)
    {
        _currentColor = channel;
        ApplyColor();
    }

    /// <summary>
    /// 색상 초기화 (우클릭 또는 라운드 리셋)
    /// </summary>
    public void ResetColor()
    {
        _currentColor = ColorChannel.None;
        ApplyColor();
    }

    private Transform _symbolR;
    private Transform _symbolG;
    private Transform _symbolB;

    private SpriteRenderer _outlineRenderer;
    private readonly Color _defaultOutlineColor = new Color(1f, 1f, 1f, 0.49019608f);
    private bool _isHovered = false;

    /// <summary>마우스 커서 호버 상태 설정</summary>
    public bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                ApplyColor();
            }
        }
    }

    /// <summary>
    /// 현재 _currentColor 및 호버 상태를 SpriteRenderer, 아웃라인 및 심볼 표식에 반영 (미리보기 연출 포함)
    /// </summary>
    public void ApplyColor()
    {
        // 1. 호버 상태 및 플레이어 브러시에 따른 미리보기 색상 계산
        ColorChannel previewColor = _currentColor;
        PlayerInputController pic = GetComponentInParent<PlayerInputController>();
        bool isShowingPreview = false;

        if (_isHovered && pic != null && pic.ToggledChannel != ColorChannel.None && pic.InputEnabled)
        {
            previewColor |= pic.ToggledChannel;
            isShowingPreview = true;
        }

        // 2. 몸통 색상 처리
        if (_spriteRenderer != null)
        {
            if (previewColor == ColorChannel.None)
            {
                // 미채색 상태
                Color baseGray = ColorUtil.ToColor(previewColor);
                if (_isHovered)
                {
                    // 호버 시: 몸통을 훨씬 밝고 투명하게 반사하여 조준 가독성 향상
                    _spriteRenderer.color = Color.Lerp(baseGray, Color.white, 0.35f);
                }
                else
                {
                    _spriteRenderer.color = baseGray;
                }
            }
            else
            {
                // 채색 상태 또는 미리보기 상태 (셰이더/알파 채널 미지원 환경 대응: 65% 어두운 회색 혼합)
                Color baseColor = ColorUtil.ToColor(previewColor);
                Color bodyColor = Color.Lerp(baseColor, new Color(0.2f, 0.2f, 0.2f, 1f), 0.65f);
                
                if (_isHovered)
                {
                    // 호버/미리보기 시: 몸통을 좀 더 은은하고 밝게 활성화
                    float blendFactor = isShowingPreview ? 0.35f : 0.25f;
                    _spriteRenderer.color = Color.Lerp(bodyColor, Color.white, blendFactor);
                }
                else
                {
                    _spriteRenderer.color = bodyColor;
                }
            }
        }

        // 3. 아웃라인 오브젝트를 찾아 색상 적용
        if (_outlineRenderer == null)
        {
            Transform outlineTransform = transform.Find("Outline");
            if (outlineTransform != null)
                _outlineRenderer = outlineTransform.GetComponent<SpriteRenderer>();
        }

        if (_outlineRenderer != null)
        {
            if (previewColor == ColorChannel.None)
            {
                if (_isHovered)
                {
                    // 미채색 호버 시: 아웃라인을 100% 쨍한 흰색으로 빛나게 설정
                    _outlineRenderer.color = Color.white;
                }
                else
                {
                    _outlineRenderer.color = _defaultOutlineColor;
                }
            }
            else
            {
                if (_isHovered)
                {
                    // 채색/미리보기 호버 시: 테두리를 더욱 영롱하게 강조하기 위해 미세한 라이트 필터 믹싱
                    float blendFactor = isShowingPreview ? 0.35f : 0.2f;
                    _outlineRenderer.color = Color.Lerp(ColorUtil.ToColor(previewColor), Color.white, blendFactor);
                }
                else
                {
                    // 아웃라인은 100% 원본 색상 그대로 밝고 선명하게 발광하도록 설정
                    _outlineRenderer.color = ColorUtil.ToColor(previewColor);
                }
            }
        }
        
        // 4. 미리보기 상태에서도 심볼이 함께 연동되어 발광하도록 처리
        UpdateSymbols(previewColor);
    }

    /// <summary>
    /// R, G, B 심볼 캐싱
    /// </summary>
    private void CacheSymbols()
    {
        if (_symbolR == null) _symbolR = transform.Find("Symbol/R");
        if (_symbolG == null) _symbolG = transform.Find("Symbol/G");
        if (_symbolB == null) _symbolB = transform.Find("Symbol/B");
    }

    /// <summary>
    /// 현재 색상 구성에 따라 필요한 표식(R, G, B)만 활성화
    /// </summary>
    private void UpdateSymbols(ColorChannel targetColor)
    {
        CacheSymbols();

        bool hasRed = (targetColor & ColorChannel.Red) != 0;
        bool hasGreen = (targetColor & ColorChannel.Green) != 0;
        bool hasBlue = (targetColor & ColorChannel.Blue) != 0;

        if (_symbolR != null) _symbolR.gameObject.SetActive(hasRed);
        if (_symbolG != null) _symbolG.gameObject.SetActive(hasGreen);
        if (_symbolB != null) _symbolB.gameObject.SetActive(hasBlue);
    }
}
