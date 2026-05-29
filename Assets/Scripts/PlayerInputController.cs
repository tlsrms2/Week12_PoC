using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 입력 컨트롤러
/// MyGrid에 부착하여 키보드(Q/W/E) + 마우스 드래그로 삼각형 색칠
/// </summary>
public class PlayerInputController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Camera _mainCamera;
    
    [Header("설정")]
    [SerializeField] private bool _inputEnabled = true;
    
    // 현재 드래그 중 이미 칠한 타일 추적 (중복 방지)
    private HashSet<TriangleTile> _paintedThisDrag = new HashSet<TriangleTile>();
    
    // 토글 및 커서 상태
    private ColorChannel _toggledChannel = ColorChannel.None;
    private Texture2D _currentCursorTexture;
    
    /// <summary>현재 선택된 색상 채널 브러시</summary>
    public ColorChannel ToggledChannel => _toggledChannel;
    
    /// <summary>입력 활성화/비활성화</summary>
    public bool InputEnabled 
    { 
        get => _inputEnabled;
        set 
        {
            _inputEnabled = value;
            if (!_inputEnabled)
            {
                ResetToggleState();
            }
        }
    }

    private void Start()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        // 게임 시작 즉시 기본 마우스 커서를 흰색 조준선으로 교체
        UpdateCursor(ColorChannel.None);
    }

    private void OnDisable()
    {
        // 오브젝트 비활성화 시 커서 복원 및 리소스 해제
        if (_currentCursorTexture != null)
        {
            if (Application.isPlaying)
                Destroy(_currentCursorTexture);
            else
                DestroyImmediate(_currentCursorTexture);
            _currentCursorTexture = null;
        }
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private TriangleTile _currentHoveredTile = null;

    private void Update()
    {
        if (!_inputEnabled) return;
        
        // 새로운 인풋 시스템 기기 체크
        if (Keyboard.current == null || Mouse.current == null) return;
        
        HandleToggleInput();
        HandleColorInput();
        HandleResetInput();
        HandleHoverHighlight();
    }

    /// <summary>
    /// 마우스 오버랩 레이캐스트를 기반으로 플레이어 그리드 상의 타일 호버 상태 실시간 관리
    /// </summary>
    private void HandleHoverHighlight()
    {
        Vector2 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
        TriangleTile hitTile = null;

        if (hit != null)
        {
            hitTile = hit.GetComponent<TriangleTile>();
            // 선생 그리드가 아닌 플레이어 그리드(본인)의 타일인지 검증
            if (hitTile != null && !hitTile.transform.IsChildOf(transform))
            {
                hitTile = null;
            }
        }

        // 호버 타일이 달라진 경우 이전 타일 해제 및 신규 타일 활성화
        if (_currentHoveredTile != hitTile)
        {
            if (_currentHoveredTile != null)
            {
                _currentHoveredTile.IsHovered = false;
            }

            _currentHoveredTile = hitTile;

            if (_currentHoveredTile != null)
            {
                _currentHoveredTile.IsHovered = true;
            }
        }
    }

    /// <summary>
    /// Q/W/E 키 입력 상태 실시간 검출 (토글 방식을 제거하고 '누르고 있을 때만 작동'하도록 변경)
    /// </summary>
    private void HandleToggleInput()
    {
        ColorChannel currentPressed = ColorChannel.None;

        if (Keyboard.current.qKey.isPressed)
            currentPressed |= ColorChannel.Red;
        if (Keyboard.current.wKey.isPressed)
            currentPressed |= ColorChannel.Blue;
        if (Keyboard.current.eKey.isPressed)
            currentPressed |= ColorChannel.Green;

        // 누르고 있는 키 조합에 변화가 생겼을 때만 캐싱/텍스처 갱신 수행
        if (_toggledChannel != currentPressed)
        {
            _toggledChannel = currentPressed;

            UpdateCursor(_toggledChannel);
            _paintedThisDrag.Clear();

            // 현재 호버 중인 타일이 있다면 브러시 변경에 따른 미리보기 즉시 갱신
            if (_currentHoveredTile != null)
            {
                _currentHoveredTile.ApplyColor();
            }
        }
    }

    /// <summary>
    /// Q/W/E 키 입력 상태와 마우스 왼쪽 버튼 클릭/드래그 상태에 따른 색칠 처리
    /// </summary>
    private void HandleColorInput()
    {
        // QWE 키 브러시가 활성화되어 있고 마우스 왼쪽 버튼이 꾹 눌려있는 상태에서만 칠하기가 활성화됩니다.
        if (_toggledChannel == ColorChannel.None || !Mouse.current.leftButton.isPressed)
        {
            if (_paintedThisDrag.Count > 0)
            {
                _paintedThisDrag.Clear();
            }
            return;
        }
        
        Vector2 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
        
        if (hit != null)
        {
            TriangleTile tile = hit.GetComponent<TriangleTile>();
            if (tile != null && !_paintedThisDrag.Contains(tile))
            {
                if (tile.transform.IsChildOf(transform))
                {
                    // 누적 가산 혼합 적용
                    tile.AddColor(_toggledChannel);
                    _paintedThisDrag.Add(tile);
                    
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayPaintSFX();
                }
            }
        }
    }

    /// <summary>
    /// 마우스 우클릭으로 타일 색상 초기화
    /// </summary>
    private void HandleResetInput()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame) // 우클릭
        {
            Vector2 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
            
            if (hit != null)
            {
                TriangleTile tile = hit.GetComponent<TriangleTile>();
                if (tile != null && tile.transform.IsChildOf(transform))
                {
                    tile.ResetColor();
                }
            }
        }
    }

    /// <summary>
    /// 토글 브러시 상태 및 커서 비주얼 리셋 (한 패턴이 종료되거나 결과 돌입 시 호출)
    /// </summary>
    public void ResetToggleState()
    {
        _toggledChannel = ColorChannel.None;
        _paintedThisDrag.Clear();
        
        // 호버 상태 강제 초기화
        if (_currentHoveredTile != null)
        {
            _currentHoveredTile.IsHovered = false;
            _currentHoveredTile = null;
        }

        UpdateCursor(ColorChannel.None);
    }

    /// <summary>
    /// 현재 누르고 있는 색상 조합에 맞춰 마우스 커서 조준선 텍스처 업데이트
    /// </summary>
    private void UpdateCursor(ColorChannel activeChannel)
    {
        if (_currentCursorTexture != null)
        {
            if (Application.isPlaying)
                Destroy(_currentCursorTexture);
            else
                DestroyImmediate(_currentCursorTexture);
            _currentCursorTexture = null;
        }

        // 아무것도 누르지 않은 기본 상태(None)일 때는 중앙 도트를 회색(DefaultGray)으로 표현하여, QWE 다중 타건 시의 화이트(White) 피드백과 명확히 구분
        Color dotColor = activeChannel == ColorChannel.None ? ColorUtil.DefaultGray : ColorUtil.ToColor(activeChannel);
        _currentCursorTexture = CreateCursorTexture(dotColor);
        
        // 128x128 크기 텍스처의 정중앙(64, 64)을 핫스팟으로 설정
        Cursor.SetCursor(_currentCursorTexture, new Vector2(64, 64), CursorMode.Auto);
    }

    /// <summary>
    /// 절차적(Procedural)으로 굵고 뚜렷한 흰색 외곽 원과 컬러 중앙점을 가진 크로스헤어 커서 생성
    /// </summary>
    private Texture2D CreateCursorTexture(Color color)
    {
        int size = 128; // 크기를 128x128로 초대형 확장하여 울트라 고해상도(QHD/4K) 가시성 확보
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radiusInner = size * 0.28f; // 약 15.3픽셀 (엄청 크고 선명한 중앙 도트)
        float radiusOuter = size * 0.42f; // 약 35.8픽셀 (조준하기 편한 거대한 외곽 원)
        float borderThickness = 13.0f;     // 외곽 테두리 두께 (8픽셀로 압도적으로 굵게 렌더링)

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                
                // 1. 중앙 핫스팟 도트 (현재 선택한 붓 색상 대입)
                if (dist < radiusInner)
                {
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, 1f));
                }
                // 2. 외곽 원 (항상 선명하고 굵은 화이트 아웃라인)
                else if (dist >= radiusOuter - borderThickness && dist < radiusOuter)
                {
                    tex.SetPixel(x, y, Color.white);
                }
                // 3. 외각 투명 백그라운드
                else
                {
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                }
            }
        }
        tex.Apply();
        return tex;
    }
}
