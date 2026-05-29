using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 그리드 매니저 - MyGrid 또는 TeacherGrid에 부착
/// 자식 오브젝트의 TriangleTile 컴포넌트를 자동 수집하고 관리
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private bool _isPlayerGrid = true;
    
    private Dictionary<int, TriangleTile> _tiles = new Dictionary<int, TriangleTile>();
    private TriangleTile[] _tileArray;
    
    /// <summary>플레이어 그리드 여부</summary>
    public bool IsPlayerGrid => _isPlayerGrid;
    
    /// <summary>전체 타일 수</summary>
    public int TileCount 
    {
        get
        {
            EnsureTilesCollected();
            return _tiles.Count;
        }
    }
    
    /// <summary>모든 타일 목록</summary>
    public IReadOnlyDictionary<int, TriangleTile> Tiles 
    {
        get
        {
            EnsureTilesCollected();
            return _tiles;
        }
    }

    private void Awake()
    {
        CollectTiles();
    }

    /// <summary>
    /// 타일 수집이 필요하면 즉시 수집 수행
    /// </summary>
    private void EnsureTilesCollected()
    {
        if (_tiles == null || _tiles.Count == 0)
        {
            CollectTiles();
        }
    }

    /// <summary>
    /// GridVisual 하위의 모든 TriangleTile 컴포넌트를 수집
    /// </summary>
    private void CollectTiles()
    {
        _tiles.Clear();
        
        // GridVisual 자식 찾기
        Transform gridVisual = transform.Find("GridVisual");
        if (gridVisual == null)
        {
            Debug.LogError($"[GridManager] {gameObject.name}에 GridVisual 자식이 없습니다!");
            return;
        }

        // GridVisual의 자식에서 TriangleTile 수집
        TriangleTile[] tiles = gridVisual.GetComponentsInChildren<TriangleTile>();
        foreach (var tile in tiles)
        {
            if (!_tiles.ContainsKey(tile.TileIndex))
            {
                _tiles[tile.TileIndex] = tile;
            }
        }
        
        _tileArray = _tiles.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();
        Debug.Log($"[GridManager] {gameObject.name}: {_tiles.Count}개 타일 수집 완료");
    }

    /// <summary>
    /// 인덱스로 타일 가져오기
    /// </summary>
    public TriangleTile GetTile(int index)
    {
        EnsureTilesCollected();
        _tiles.TryGetValue(index, out TriangleTile tile);
        return tile;
    }

    /// <summary>
    /// 모든 타일 색상 초기화
    /// </summary>
    public void ResetAllTiles()
    {
        EnsureTilesCollected();
        foreach (var tile in _tiles.Values)
        {
            tile.ResetColor();
        }
    }

    /// <summary>
    /// 전체 타일의 색상 상태 스냅샷 반환 (판정용)
    /// Key: 타일 인덱스, Value: ColorChannel 상태
    /// </summary>
    public Dictionary<int, ColorChannel> GetAllTileStates()
    {
        EnsureTilesCollected();
        var states = new Dictionary<int, ColorChannel>();
        foreach (var kv in _tiles)
        {
            states[kv.Key] = kv.Value.CurrentColor;
        }
        return states;
    }

    /// <summary>
    /// 특정 타일에 색상 설정 (선생님 패턴 재생용)
    /// </summary>
    public void SetTileColor(int index, ColorChannel color)
    {
        EnsureTilesCollected();
        if (_tiles.TryGetValue(index, out TriangleTile tile))
        {
            tile.SetColor(color);
        }
    }
    
    /// <summary>
    /// 특정 타일에 색상 덧칠 (선생님 패턴 재생용)
    /// </summary>
    public void AddTileColor(int index, ColorChannel color)
    {
        EnsureTilesCollected();
        if (_tiles.TryGetValue(index, out TriangleTile tile))
        {
            tile.AddColor(color);
        }
    }
}
