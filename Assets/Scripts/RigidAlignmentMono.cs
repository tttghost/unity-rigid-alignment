using System.Collections.Generic;
using UnityEngine;
public class RigidAlignmentMono : MonoBehaviour
{
    public Camera cam;

    public Transform rightCube;

    public GameObject markerPrefab;

    private List<Vector3> leftPoints = new();
    private List<Vector3> rightPoints = new();
    private List<GameObject> leftMarkers = new();
    private List<GameObject> rightMarkers = new();

    private RigidAlignment solver = new RigidAlignment();

    private GameObject _previewMarker;
    private GameObject _clone;

    // ─── 컬러 페어링 ───
    private static readonly Color[] PairColors = new[]
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.cyan,
        Color.magenta,
        new Color(1f, 0.5f, 0f),   // orange
        new Color(0.5f, 0f, 1f),   // purple
    };

    // ─── 드래그 상태 ───
    private const float DragThreshold = 5f; // 픽셀
    private GameObject _dragMarker;
    private Vector3 _dragStartWorldPos;
    private Vector3 _dragStartMousePos;
    private bool _dragIsRight; // 드래그 중인 마커가 right(virtual)쪽인지
    private bool _isDragging;

    void Update()
    {
        UpdatePreviewMarker();

        if (Input.GetMouseButtonDown(0))
            OnMouseDown();

        if (Input.GetMouseButton(0) && _dragMarker != null)
            OnMouseDrag();

        if (Input.GetMouseButtonUp(0) && _dragMarker != null)
            OnMouseUp();

        if (Input.GetKeyDown(KeyCode.Escape))
            ResetAlignment();
    }

    // ─── 프리뷰 마커 ───

    void UpdatePreviewMarker()
    {
        // 드래그 중이면 프리뷰 숨김
        if (_dragMarker != null)
        {
            if (_previewMarker != null) _previewMarker.SetActive(false);
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit) && !IsMarker(hit.transform.gameObject))
        {
            if (_previewMarker == null)
            {
                _previewMarker = Instantiate(markerPrefab);
                SetMarkerAlpha(_previewMarker, 0.4f);
            }

            _previewMarker.SetActive(true);
            _previewMarker.transform.position = hit.point;
        }
        else
        {
            if (_previewMarker != null)
                _previewMarker.SetActive(false);
        }
    }

    void SetMarkerColor(GameObject marker, Color color)
    {
        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    void SetMarkerAlpha(GameObject marker, float alpha)
    {
        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = renderer.material;
            var color = mat.color;
            color.a = alpha;
            mat.color = color;
        }
    }

    Color GetPairColor(int index) => PairColors[index % PairColors.Length];

    // ─── 마우스 입력 ───

    void OnMouseDown()
    {
        _isDragging = false;
        _dragStartMousePos = Input.mousePosition;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        // 마커를 눌렀으면 드래그 준비
        int li = leftMarkers.IndexOf(hit.transform.gameObject);
        if (li >= 0)
        {
            _dragMarker = leftMarkers[li];
            _dragStartWorldPos = _dragMarker.transform.position;
            _dragIsRight = false;
            return;
        }

        int ri = rightMarkers.IndexOf(hit.transform.gameObject);
        if (ri >= 0)
        {
            _dragMarker = rightMarkers[ri];
            _dragStartWorldPos = _dragMarker.transform.position;
            _dragIsRight = true;
            return;
        }

        // 오브젝트 클릭 → 새 마커 생성
        if (hit.transform == rightCube)
        {
            // virtual 쪽
            var marker = Instantiate(markerPrefab, hit.point, Quaternion.identity);
            EnableMarkerCollider(marker);
            rightMarkers.Add(marker);
            rightPoints.Add(rightCube.InverseTransformPoint(hit.point));
            int idx = rightMarkers.Count - 1;
            SetMarkerColor(marker, GetPairColor(idx));
            SyncPairColor(idx);
            TryAlign();
        }
        else
        {
            // real 쪽 (rightCube가 아닌 모든 콜라이더)
            var marker = Instantiate(markerPrefab, hit.point, Quaternion.identity);
            EnableMarkerCollider(marker);
            leftMarkers.Add(marker);
            leftPoints.Add(hit.point);
            int idx = leftMarkers.Count - 1;
            SetMarkerColor(marker, GetPairColor(idx));
            SyncPairColor(idx);
            TryAlign();
        }
    }

    void OnMouseDrag()
    {
        // 드래그 임계값 체크
        if (!_isDragging)
        {
            if (Vector3.Distance(Input.mousePosition, _dragStartMousePos) < DragThreshold)
                return;
            _isDragging = true;
            // 드래그 시작 시 마커 콜라이더 끄기 (큐브 표면 레이캐스트 방해 방지)
            var dragCol = _dragMarker.GetComponent<Collider>();
            if (dragCol != null) dragCol.enabled = false;
        }

        // 표면 위로 마커 이동
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit) && IsValidDragSurface(hit.transform))
        {
            _dragMarker.transform.position = hit.point;
        }
    }

    void OnMouseUp()
    {
        if (!_isDragging)
        {
            // 드래그 아닌 클릭 → 삭제
            DeleteMarker(_dragMarker);
            _dragMarker = null;
            return;
        }

        // 드래그 끝: 유효한 표면 위인지 확인
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        bool onSurface = Physics.Raycast(ray, out RaycastHit hit) && IsValidDragSurface(hit.transform);

        if (onSurface)
        {
            // 새 위치 확정
            _dragMarker.transform.position = hit.point;
            UpdateMarkerPoint(_dragMarker, hit.point);
        }
        else
        {
            // 큐브 밖 → 원위치 복원
            _dragMarker.transform.position = _dragStartWorldPos;
        }

        // 콜라이더 복원
        EnableMarkerCollider(_dragMarker);

        _dragMarker = null;
        _isDragging = false;

        TryAlign();
    }

    // ─── 유틸리티 ───

    bool IsMarker(GameObject go) =>
        leftMarkers.Contains(go) || rightMarkers.Contains(go);

    /// <summary>
    /// 드래그 중 유효한 표면인지: right마커는 rightCube만, left마커는 rightCube 외 모든 콜라이더
    /// </summary>
    bool IsValidDragSurface(Transform surface)
    {
        if (IsMarker(surface.gameObject)) return false;
        return _dragIsRight ? surface == rightCube : surface != rightCube;
    }

    // ─── 마커 관리 ───

    void UpdateMarkerPoint(GameObject marker, Vector3 worldPos)
    {
        int li = leftMarkers.IndexOf(marker);
        if (li >= 0) { leftPoints[li] = worldPos; return; }

        int ri = rightMarkers.IndexOf(marker);
        if (ri >= 0) { rightPoints[ri] = rightCube.InverseTransformPoint(worldPos); }
    }

    void DeleteMarker(GameObject marker)
    {
        int li = leftMarkers.IndexOf(marker);
        if (li >= 0)
        {
            Destroy(leftMarkers[li]);
            leftMarkers.RemoveAt(li);
            leftPoints.RemoveAt(li);

            // 페어 마커도 삭제
            if (li < rightMarkers.Count)
            {
                Destroy(rightMarkers[li]);
                rightMarkers.RemoveAt(li);
                rightPoints.RemoveAt(li);
            }

            RefreshAllColors();
            TryAlign();
            return;
        }

        int ri = rightMarkers.IndexOf(marker);
        if (ri >= 0)
        {
            Destroy(rightMarkers[ri]);
            rightMarkers.RemoveAt(ri);
            rightPoints.RemoveAt(ri);

            // 페어 마커도 삭제
            if (ri < leftMarkers.Count)
            {
                Destroy(leftMarkers[ri]);
                leftMarkers.RemoveAt(ri);
                leftPoints.RemoveAt(ri);
            }

            RefreshAllColors();
            TryAlign();
        }
    }

    void SyncPairColor(int pairIndex)
    {
        Color c = GetPairColor(pairIndex);
        if (pairIndex < leftMarkers.Count) SetMarkerColor(leftMarkers[pairIndex], c);
        if (pairIndex < rightMarkers.Count) SetMarkerColor(rightMarkers[pairIndex], c);
    }

    void RefreshAllColors()
    {
        for (int i = 0; i < leftMarkers.Count; i++)
            SetMarkerColor(leftMarkers[i], GetPairColor(i));
        for (int i = 0; i < rightMarkers.Count; i++)
            SetMarkerColor(rightMarkers[i], GetPairColor(i));
    }

    void EnableMarkerCollider(GameObject marker)
    {
        var col = marker.GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    // ─── 정합 ───

    void TryAlign()
    {
        if (leftPoints.Count >= 3 && rightPoints.Count >= 3)
        {
            if (solver.Solve(leftPoints, rightPoints, rightCube.localScale, out var pos, out var rot))
            {
                EnsureClone();
                _clone.SetActive(true);
                _clone.transform.rotation = rot;
                _clone.transform.position = pos;
            }
        }
        else
        {
            // 3쌍 미만이면 Clone 숨김
            if (_clone != null)
                _clone.SetActive(false);
        }
    }

    void EnsureClone()
    {
        if (_clone != null) return;

        _clone = Instantiate(rightCube.gameObject);
        _clone.name = rightCube.name + "_Clone";

        // 자식(마커) 제거 — 비주얼만 남김
        var children = new List<Transform>();
        foreach (Transform child in _clone.transform)
            children.Add(child);
        foreach (var child in children)
            Destroy(child.gameObject);

        // 콜라이더 비활성화
        foreach (var col in _clone.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    public void ResetAlignment()
    {
        // 마커 전부 삭제
        foreach (var m in leftMarkers) Destroy(m);
        foreach (var m in rightMarkers) Destroy(m);

        leftPoints.Clear();
        rightPoints.Clear();
        leftMarkers.Clear();
        rightMarkers.Clear();

        // Clone 삭제
        if (_clone != null)
        {
            Destroy(_clone);
            _clone = null;
        }

        // 드래그 상태 초기화
        _dragMarker = null;
        _isDragging = false;

        if (_previewMarker != null)
        {
            Destroy(_previewMarker);
            _previewMarker = null;
        }
    }
}