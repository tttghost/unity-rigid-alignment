using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마커 CRUD · 컬러 페어링 · 프리뷰 · 고정 크기 스케일링을 담당하는 순수 C# 클래스.
/// MonoBehaviour가 아니므로 AlignmentController에서 생성하여 사용한다.
/// </summary>
public class MarkerManager
{
    // ─── 컬러 페어링 팔레트 ───
    private static readonly Color[] PairColors = new[]
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.cyan,
        Color.magenta,
        new Color(1f, 0.5f, 0f), // orange
        new Color(0.5f, 0f, 1f), // purple
    };

    private const float RefDistance = 5f;

    // ─── 의존성 ───
    private readonly GameObject _markerPrefab;
    private readonly Camera _cam;

    // ─── 마커 & 포인트 데이터 ───
    private readonly List<GameObject> _realMarkers = new();
    private readonly List<GameObject> _virtualMarkers = new();
    private readonly List<Vector3> _realPoints = new();
    private readonly List<Vector3> _virtualPoints = new();

    // ─── 계층 정리용 부모 ───
    private Transform _realMarkerRoot;
    private Transform _virtualMarkerRoot;

    // ─── 프리뷰 마커 ───
    private GameObject _previewMarker;

    // ─── 공개 프로퍼티 ───
    public float MarkerScale { get; set; } = 1f;

    public IReadOnlyList<Vector3> RealPoints => _realPoints;
    public IReadOnlyList<Vector3> VirtualPoints => _virtualPoints;
    public int RealCount => _realMarkers.Count;
    public int VirtualCount => _virtualMarkers.Count;

    // ─── 계층 루트 (lazy) ───
    private Transform RealMarkerRoot
    {
        get
        {
            if (_realMarkerRoot == null)
                _realMarkerRoot = new GameObject("RealMarkers").transform;
            return _realMarkerRoot;
        }
    }

    private Transform VirtualMarkerRoot
    {
        get
        {
            if (_virtualMarkerRoot == null)
                _virtualMarkerRoot = new GameObject("VirtualMarkers").transform;
            return _virtualMarkerRoot;
        }
    }

    // ─── 생성자 ───

    public MarkerManager(GameObject markerPrefab, Camera cam)
    {
        _markerPrefab = markerPrefab;
        _cam = cam;
    }

    // ═══════════════════════════════════════════
    //  마커 CRUD
    // ═══════════════════════════════════════════

    /// <summary>
    /// Real 측 마커 생성. worldPoint를 그대로 저장한다.
    /// </summary>
    public int AddRealMarker(Vector3 worldPoint)
    {
        var marker = UnityEngine.Object.Instantiate(
            _markerPrefab, worldPoint, Quaternion.identity, RealMarkerRoot);
        EnableCollider(marker);
        _realMarkers.Add(marker);
        _realPoints.Add(worldPoint);

        int idx = _realMarkers.Count - 1;
        SetColor(marker, GetPairColor(idx));
        SyncPairColor(idx);
        return idx;
    }

    /// <summary>
    /// Virtual 측 마커 생성. worldPoint에 마커를 배치하고, localPoint를 저장한다.
    /// (월드→로컬 변환은 호출부(Controller)가 ISurfaceProvider.WorldToVirtualLocal로 수행)
    /// </summary>
    public int AddVirtualMarker(Vector3 worldPoint, Vector3 localPoint)
    {
        var marker = UnityEngine.Object.Instantiate(
            _markerPrefab, worldPoint, Quaternion.identity, VirtualMarkerRoot);
        EnableCollider(marker);
        _virtualMarkers.Add(marker);
        _virtualPoints.Add(localPoint);

        int idx = _virtualMarkers.Count - 1;
        SetColor(marker, GetPairColor(idx));
        SyncPairColor(idx);
        return idx;
    }

    /// <summary>
    /// 마커 페어 삭제 (한쪽을 지우면 반대쪽도 함께 삭제).
    /// </summary>
    public void DeleteMarkerPair(int index, bool fromVirtual)
    {
        if (fromVirtual)
        {
            DestroyAt(_virtualMarkers, _virtualPoints, index);
            if (index < _realMarkers.Count)
                DestroyAt(_realMarkers, _realPoints, index);
        }
        else
        {
            DestroyAt(_realMarkers, _realPoints, index);
            if (index < _virtualMarkers.Count)
                DestroyAt(_virtualMarkers, _virtualPoints, index);
        }

        RefreshColors();
    }

    /// <summary>
    /// Real 마커 위치 갱신.
    /// </summary>
    public void UpdateRealPoint(int index, Vector3 worldPoint)
    {
        _realMarkers[index].transform.position = worldPoint;
        _realPoints[index] = worldPoint;
    }

    /// <summary>
    /// Virtual 마커 위치 갱신.
    /// </summary>
    public void UpdateVirtualPoint(int index, Vector3 worldPoint, Vector3 localPoint)
    {
        _virtualMarkers[index].transform.position = worldPoint;
        _virtualPoints[index] = localPoint;
    }

    // ═══════════════════════════════════════════
    //  마커 검색
    // ═══════════════════════════════════════════

    /// <summary>
    /// GameObject가 마커인지 확인한다.
    /// </summary>
    public bool IsMarker(GameObject go)
    {
        return _realMarkers.Contains(go) || _virtualMarkers.Contains(go);
    }

    /// <summary>
    /// 마커 GameObject에서 인덱스 및 종류(virtual/real) 조회.
    /// </summary>
    public bool TryFindMarker(GameObject go, out bool isVirtual, out int index)
    {
        index = _realMarkers.IndexOf(go);
        if (index >= 0) { isVirtual = false; return true; }

        index = _virtualMarkers.IndexOf(go);
        if (index >= 0) { isVirtual = true; return true; }

        isVirtual = false;
        return false;
    }

    /// <summary>
    /// 인덱스로 마커 GameObject 반환.
    /// </summary>
    public GameObject GetMarker(bool isVirtual, int index)
    {
        return isVirtual ? _virtualMarkers[index] : _realMarkers[index];
    }

    // ═══════════════════════════════════════════
    //  콜라이더
    // ═══════════════════════════════════════════

    public void SetColliderEnabled(GameObject marker, bool enabled)
    {
        var col = marker.GetComponent<Collider>();
        if (col != null) col.enabled = enabled;
    }

    public void EnableCollider(GameObject marker)
    {
        SetColliderEnabled(marker, true);
    }

    // ═══════════════════════════════════════════
    //  프리뷰 마커
    // ═══════════════════════════════════════════

    /// <summary>
    /// 호버 프리뷰 마커를 표시한다. 다음 페어 인덱스 색상이 반투명으로 적용된다.
    /// </summary>
    public void ShowPreview(Vector3 position, bool isVirtualSide)
    {
        if (_previewMarker == null)
            _previewMarker = UnityEngine.Object.Instantiate(_markerPrefab);

        _previewMarker.SetActive(true);
        _previewMarker.transform.position = position;

        int nextIdx = isVirtualSide ? _virtualMarkers.Count : _realMarkers.Count;
        Color c = GetPairColor(nextIdx);
        c.a = 0.4f;
        SetColor(_previewMarker, c);
    }

    public void HidePreview()
    {
        if (_previewMarker != null)
            _previewMarker.SetActive(false);
    }

    // ═══════════════════════════════════════════
    //  컬러 관리
    // ═══════════════════════════════════════════

    /// <summary>
    /// 아웃라이어 마커를 회색 반투명으로, 나머지를 정상 색상으로 복원.
    /// </summary>
    public void ApplyOutlierVisuals(HashSet<int> outliers)
    {
        int pairCount = Mathf.Min(_realMarkers.Count, _virtualMarkers.Count);
        for (int i = 0; i < pairCount; i++)
        {
            if (outliers.Contains(i))
            {
                var gray = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                SetColor(_realMarkers[i], gray);
                SetColor(_virtualMarkers[i], gray);
            }
            else
            {
                Color c = GetPairColor(i);
                SetColor(_realMarkers[i], c);
                SetColor(_virtualMarkers[i], c);
            }
        }
    }

    /// <summary>
    /// 모든 마커를 정상 페어 색상으로 복원.
    /// </summary>
    public void RefreshColors()
    {
        for (int i = 0; i < _realMarkers.Count; i++)
            SetColor(_realMarkers[i], GetPairColor(i));
        for (int i = 0; i < _virtualMarkers.Count; i++)
            SetColor(_virtualMarkers[i], GetPairColor(i));
    }

    // ═══════════════════════════════════════════
    //  고정 크기 스케일링 (LateUpdate에서 호출)
    // ═══════════════════════════════════════════

    /// <summary>
    /// 카메라 depth 기반 고정 크기 보상. Controller의 LateUpdate에서 호출한다.
    /// </summary>
    public void UpdateFixedScales()
    {
        if (_cam == null) return;

        ApplyScaleList(_realMarkers);
        ApplyScaleList(_virtualMarkers);

        if (_previewMarker != null && _previewMarker.activeSelf)
            ApplyFixedScale(_previewMarker);
    }

    // ═══════════════════════════════════════════
    //  리셋
    // ═══════════════════════════════════════════

    /// <summary>
    /// 모든 마커 · 포인트 · 프리뷰 · 계층 부모를 파괴하고 초기화한다.
    /// </summary>
    public void Reset()
    {
        foreach (var m in _realMarkers) UnityEngine.Object.Destroy(m);
        foreach (var m in _virtualMarkers) UnityEngine.Object.Destroy(m);
        _realMarkers.Clear();
        _virtualMarkers.Clear();
        _realPoints.Clear();
        _virtualPoints.Clear();

        if (_previewMarker != null)
        {
            UnityEngine.Object.Destroy(_previewMarker);
            _previewMarker = null;
        }

        if (_realMarkerRoot != null)
        {
            UnityEngine.Object.Destroy(_realMarkerRoot.gameObject);
            _realMarkerRoot = null;
        }
        if (_virtualMarkerRoot != null)
        {
            UnityEngine.Object.Destroy(_virtualMarkerRoot.gameObject);
            _virtualMarkerRoot = null;
        }
    }

    // ═══════════════════════════════════════════
    //  내부 유틸
    // ═══════════════════════════════════════════

    private Color GetPairColor(int index) => PairColors[index % PairColors.Length];

    private void SetColor(GameObject marker, Color color)
    {
        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    private void SyncPairColor(int pairIndex)
    {
        Color c = GetPairColor(pairIndex);
        if (pairIndex < _realMarkers.Count) SetColor(_realMarkers[pairIndex], c);
        if (pairIndex < _virtualMarkers.Count) SetColor(_virtualMarkers[pairIndex], c);
    }

    private void DestroyAt(List<GameObject> markers, List<Vector3> points, int index)
    {
        UnityEngine.Object.Destroy(markers[index]);
        markers.RemoveAt(index);
        points.RemoveAt(index);
    }

    private void ApplyScaleList(List<GameObject> markers)
    {
        foreach (var m in markers)
        {
            if (m == null || !m.activeSelf) continue;
            ApplyFixedScale(m);
        }
    }

    private void ApplyFixedScale(GameObject go)
    {
        float depth = Mathf.Max(
            Vector3.Dot(go.transform.position - _cam.transform.position, _cam.transform.forward),
            0.1f);
        float scale = MarkerScale * (depth / RefDistance);
        go.transform.localScale = Vector3.one * scale;
    }
}
