using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RigidAlignmentMono : MonoBehaviour
{
    [SerializeField] private Camera _cam;
    [SerializeField] private Transform _virtualModel;
    [SerializeField] private GameObject _markerPrefab;
    [SerializeField] private Material _cloneMaterial;
    [SerializeField] private TMP_Text _rmseText;
    [SerializeField] private Material _residualLabelMaterial;

    private List<Vector3> _realPoints = new();
    private List<Vector3> _virtualPoints = new();
    private List<GameObject> _realMarkers = new();
    private List<GameObject> _virtualMarkers = new();

    private RigidAlignment _solver = new RigidAlignment();

    private GameObject _previewMarker;
    private GameObject _clone;
    private List<GameObject> _realResidualLabels = new();
    private List<GameObject> _virtualResidualLabels = new();

    // ─── 계층 정리용 부모 오브젝트 ───
    private Transform _realMarkerRoot;
    private Transform _virtualMarkerRoot;
    private Transform _residualLabelRoot;

    Transform RealMarkerRoot
    {
        get
        {
            if (_realMarkerRoot == null)
                _realMarkerRoot = new GameObject("RealMarkers").transform;
            return _realMarkerRoot;
        }
    }

    Transform VirtualMarkerRoot
    {
        get
        {
            if (_virtualMarkerRoot == null)
                _virtualMarkerRoot = new GameObject("VirtualMarkers").transform;
            return _virtualMarkerRoot;
        }
    }

    Transform ResidualLabelRoot
    {
        get
        {
            if (_residualLabelRoot == null)
                _residualLabelRoot = new GameObject("ResidualLabels").transform;
            return _residualLabelRoot;
        }
    }
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
    private bool _dragIsVirtual; // 드래그 중인 마커가 virtual쪽인지
    private bool _isDragging;

    // ─── 잔차 라벨 고정 크기 ───
    private const float LabelRefDistance = 5f;
    private const float LabelBaseScale = 1f;

    void LateUpdate()
    {
        // 잔차 라벨 빌보드 + 카메라 거리 무관 고정 크기
        if (_cam != null)
        {
            UpdateLabelTransforms(_realResidualLabels);
            UpdateLabelTransforms(_virtualResidualLabels);
        }
    }

    void UpdateLabelTransforms(List<GameObject> labels)
    {
        Vector3 camPos = _cam.transform.position;
        Vector3 camFwd = _cam.transform.forward;
        foreach (var label in labels)
        {
            if (label == null || !label.activeSelf) continue;
            label.transform.forward = camFwd;
            float dist = Vector3.Distance(camPos, label.transform.position);
            float scale = LabelBaseScale * (dist / LabelRefDistance);
            label.transform.localScale = Vector3.one * scale;
        }
    }

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

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit) && !IsMarker(hit.transform.gameObject))
        {
            if (_previewMarker == null)
                _previewMarker = Instantiate(_markerPrefab);

            _previewMarker.SetActive(true);
            _previewMarker.transform.position = hit.point;

            // 호버 위치에 따라 다음 마커의 페어 색상 적용
            int nextIdx = hit.transform == _virtualModel
                ? _virtualMarkers.Count
                : _realMarkers.Count;
            Color previewColor = GetPairColor(nextIdx);
            previewColor.a = 0.4f;
            SetMarkerColor(_previewMarker, previewColor);
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

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        // 마커를 눌렀으면 드래그 준비
        int ri = _realMarkers.IndexOf(hit.transform.gameObject);
        if (ri >= 0)
        {
            _dragMarker = _realMarkers[ri];
            _dragStartWorldPos = _dragMarker.transform.position;
            _dragIsVirtual = false;
            return;
        }

        int vi = _virtualMarkers.IndexOf(hit.transform.gameObject);
        if (vi >= 0)
        {
            _dragMarker = _virtualMarkers[vi];
            _dragStartWorldPos = _dragMarker.transform.position;
            _dragIsVirtual = true;
            return;
        }

        // 오브젝트 클릭 → 새 마커 생성
        if (hit.transform == _virtualModel)
        {
            // virtual 쪽
            var marker = Instantiate(_markerPrefab, hit.point, Quaternion.identity, VirtualMarkerRoot);
            EnableMarkerCollider(marker);
            _virtualMarkers.Add(marker);
            _virtualPoints.Add(_virtualModel.InverseTransformPoint(hit.point));
            int idx = _virtualMarkers.Count - 1;
            SetMarkerColor(marker, GetPairColor(idx));
            SyncPairColor(idx);
            TryAlign();
        }
        else
        {
            // real 쪽 (virtualModel이 아닌 모든 콜라이더)
            var marker = Instantiate(_markerPrefab, hit.point, Quaternion.identity, RealMarkerRoot);
            EnableMarkerCollider(marker);
            _realMarkers.Add(marker);
            _realPoints.Add(hit.point);
            int idx = _realMarkers.Count - 1;
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
            // 드래그 시작 시 마커 콜라이더 끄기 (표면 레이캐스트 방해 방지)
            var dragCol = _dragMarker.GetComponent<Collider>();
            if (dragCol != null) dragCol.enabled = false;
        }

        // 표면 위로 마커 이동 + 실시간 정합
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit) && IsValidDragSurface(hit.transform))
        {
            _dragMarker.transform.position = hit.point;
            UpdateMarkerPoint(_dragMarker, hit.point);
            TryAlign();
        }
        else
        {
            // 유효 표면 밖 → 드래그 시작 위치로 즉시 원복
            _dragMarker.transform.position = _dragStartWorldPos;
            UpdateMarkerPoint(_dragMarker, _dragStartWorldPos);
            TryAlign();
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

        // 드래그 중 실시간 정합 완료 — 콜라이더 복원만
        EnableMarkerCollider(_dragMarker);

        _dragMarker = null;
        _isDragging = false;
    }

    // ─── 유틸리티 ───

    bool IsMarker(GameObject go) =>
        _realMarkers.Contains(go) || _virtualMarkers.Contains(go);

    /// <summary>
    /// 드래그 중 유효한 표면인지: virtual 마커는 virtualModel만, real 마커는 virtualModel 외 모든 콜라이더
    /// </summary>
    bool IsValidDragSurface(Transform surface)
    {
        if (IsMarker(surface.gameObject)) return false;
        return _dragIsVirtual ? surface == _virtualModel : surface != _virtualModel;
    }

    // ─── 마커 관리 ───

    void UpdateMarkerPoint(GameObject marker, Vector3 worldPos)
    {
        int ri = _realMarkers.IndexOf(marker);
        if (ri >= 0) { _realPoints[ri] = worldPos; return; }

        int vi = _virtualMarkers.IndexOf(marker);
        if (vi >= 0) { _virtualPoints[vi] = _virtualModel.InverseTransformPoint(worldPos); }
    }

    void DeleteMarker(GameObject marker)
    {
        int ri = _realMarkers.IndexOf(marker);
        if (ri >= 0)
        {
            Destroy(_realMarkers[ri]);
            _realMarkers.RemoveAt(ri);
            _realPoints.RemoveAt(ri);

            // 페어 마커도 삭제
            if (ri < _virtualMarkers.Count)
            {
                Destroy(_virtualMarkers[ri]);
                _virtualMarkers.RemoveAt(ri);
                _virtualPoints.RemoveAt(ri);
            }

            RefreshAllColors();
            TryAlign();
            return;
        }

        int vi = _virtualMarkers.IndexOf(marker);
        if (vi >= 0)
        {
            Destroy(_virtualMarkers[vi]);
            _virtualMarkers.RemoveAt(vi);
            _virtualPoints.RemoveAt(vi);

            // 페어 마커도 삭제
            if (vi < _realMarkers.Count)
            {
                Destroy(_realMarkers[vi]);
                _realMarkers.RemoveAt(vi);
                _realPoints.RemoveAt(vi);
            }

            RefreshAllColors();
            TryAlign();
        }
    }

    void SyncPairColor(int pairIndex)
    {
        Color c = GetPairColor(pairIndex);
        if (pairIndex < _realMarkers.Count) SetMarkerColor(_realMarkers[pairIndex], c);
        if (pairIndex < _virtualMarkers.Count) SetMarkerColor(_virtualMarkers[pairIndex], c);
    }

    void RefreshAllColors()
    {
        for (int i = 0; i < _realMarkers.Count; i++)
            SetMarkerColor(_realMarkers[i], GetPairColor(i));
        for (int i = 0; i < _virtualMarkers.Count; i++)
            SetMarkerColor(_virtualMarkers[i], GetPairColor(i));
    }

    void EnableMarkerCollider(GameObject marker)
    {
        var col = marker.GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    // ─── 정합 ───

    void TryAlign()
    {
        if (_realPoints.Count >= 3 && _virtualPoints.Count >= 3)
        {
            if (_solver.Solve(_realPoints, _virtualPoints, _virtualModel.localScale,
                out var pos, out var rot, out var outliers, out var residuals))
            {
                EnsureClone();
                _clone.SetActive(true);
                _clone.transform.rotation = rot;
                _clone.transform.position = pos;

                // 아웃라이어 마커 시각화: 반투명 회색 처리
                var outlierSet = new HashSet<int>(outliers);
                int pairCount = Mathf.Min(_realMarkers.Count, _virtualMarkers.Count);

                for (int i = 0; i < pairCount; i++)
                {
                    if (outlierSet.Contains(i))
                    {
                        SetMarkerColor(_realMarkers[i], new Color(0.5f, 0.5f, 0.5f, 0.4f));
                        SetMarkerColor(_virtualMarkers[i], new Color(0.5f, 0.5f, 0.5f, 0.4f));
                    }
                    else
                    {
                        Color c = GetPairColor(i);
                        SetMarkerColor(_realMarkers[i], c);
                        SetMarkerColor(_virtualMarkers[i], c);
                    }
                }

                // RMSE 표시
                float rmse = RigidAlignment.ComputeRMSE(residuals, outliers);
                if (_rmseText != null)
                    _rmseText.text = $"RMSE: {rmse:F4} m  (inliers: {pairCount - outliers.Count}/{pairCount})";

                // 마커별 잔차 라벨
                UpdateResidualLabels(residuals, outlierSet);
            }
        }
        else
        {
            // 3쌍 미만이면 Clone 숨김
            if (_clone != null)
                _clone.SetActive(false);

            // 색상 복원
            RefreshAllColors();

            // 잔차 시각화 초기화
            ClearResidualLabels();
            if (_rmseText != null) _rmseText.text = "";
        }
    }

    // ─── 잔차 시각화 ───

    void UpdateResidualLabels(float[] residuals, HashSet<int> outlierSet)
    {
        float maxInlierResidual = 0f;
        for (int i = 0; i < residuals.Length; i++)
            if (!outlierSet.Contains(i))
                maxInlierResidual = Mathf.Max(maxInlierResidual, residuals[i]);

        int pairCount = Mathf.Min(_realMarkers.Count, residuals.Length);
        pairCount = Mathf.Min(pairCount, _virtualMarkers.Count);

        // 풀링: 필요한 만큼 라벨 확보
        while (_realResidualLabels.Count < pairCount)
            _realResidualLabels.Add(CreateResidualLabelObject());
        while (_virtualResidualLabels.Count < pairCount)
            _virtualResidualLabels.Add(CreateResidualLabelObject());

        // 기존 라벨 재사용
        for (int i = 0; i < pairCount; i++)
        {
            bool isOutlier = outlierSet.Contains(i);
            Color labelColor = isOutlier
                ? new Color(0.5f, 0.5f, 0.5f, 0.6f)
                : ResidualToColor(residuals[i], maxInlierResidual);
            string labelText = $"{residuals[i]:F4} m";

            // Real 쪽 라벨
            var realLabel = _realResidualLabels[i];
            realLabel.SetActive(true);
            realLabel.transform.position = _realMarkers[i].transform.position + Vector3.up * 0.05f;
            var realTmp = realLabel.GetComponent<TextMeshPro>();
            realTmp.text = labelText;
            realTmp.color = labelColor;

            // Virtual 쪽 라벨
            var virtualLabel = _virtualResidualLabels[i];
            virtualLabel.SetActive(true);
            virtualLabel.transform.position = _virtualMarkers[i].transform.position + Vector3.up * 0.05f;
            var virtualTmp = virtualLabel.GetComponent<TextMeshPro>();
            virtualTmp.text = labelText;
            virtualTmp.color = labelColor;
        }

        // 남는 라벨 숨김
        for (int i = pairCount; i < _realResidualLabels.Count; i++)
            _realResidualLabels[i].SetActive(false);
        for (int i = pairCount; i < _virtualResidualLabels.Count; i++)
            _virtualResidualLabels[i].SetActive(false);
    }

    GameObject CreateResidualLabelObject()
    {
        var go = new GameObject("ResidualLabel");
        go.transform.SetParent(ResidualLabelRoot);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize = 2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 10;
        if (_residualLabelMaterial != null)
            tmp.fontSharedMaterial = _residualLabelMaterial;
        return go;
    }

    Color ResidualToColor(float residual, float maxResidual)
    {
        if (maxResidual < 1e-6f) return Color.green;
        float t = Mathf.Clamp01(residual / maxResidual);
        // 녹색 → 노랑 → 빨강
        return t < 0.5f
            ? Color.Lerp(Color.green, Color.yellow, t * 2f)
            : Color.Lerp(Color.yellow, Color.red, (t - 0.5f) * 2f);
    }

    void ClearResidualLabels()
    {
        foreach (var label in _realResidualLabels)
            if (label != null) label.SetActive(false);
        foreach (var label in _virtualResidualLabels)
            if (label != null) label.SetActive(false);
    }

    void DestroyResidualLabels()
    {
        foreach (var label in _realResidualLabels)
            if (label != null) Destroy(label);
        _realResidualLabels.Clear();
        foreach (var label in _virtualResidualLabels)
            if (label != null) Destroy(label);
        _virtualResidualLabels.Clear();
    }

    void EnsureClone()
    {
        if (_clone != null) return;

        _clone = Instantiate(_virtualModel.gameObject);
        _clone.name = _virtualModel.name + "_Clone";

        // 자식(마커) 제거 — 비주얼만 남김
        var children = new List<Transform>();
        foreach (Transform child in _clone.transform)
            children.Add(child);
        foreach (var child in children)
            Destroy(child.gameObject);

        // 콜라이더 비활성화
        foreach (var col in _clone.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // 클론 머티리얼 적용
        if (_cloneMaterial != null)
        {
            foreach (var renderer in _clone.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = _cloneMaterial;
                renderer.materials = mats;
            }
        }
    }

    public void ResetAlignment()
    {
        // 잔차 시각화 초기화
        DestroyResidualLabels();
        if (_rmseText != null) _rmseText.text = "";

        // 마커 전부 삭제
        foreach (var m in _realMarkers) Destroy(m);
        foreach (var m in _virtualMarkers) Destroy(m);

        _realPoints.Clear();
        _virtualPoints.Clear();
        _realMarkers.Clear();
        _virtualMarkers.Clear();

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

        // 계층 부모 오브젝트 삭제
        if (_realMarkerRoot != null) { Destroy(_realMarkerRoot.gameObject); _realMarkerRoot = null; }
        if (_virtualMarkerRoot != null) { Destroy(_virtualMarkerRoot.gameObject); _virtualMarkerRoot = null; }
        if (_residualLabelRoot != null) { Destroy(_residualLabelRoot.gameObject); _residualLabelRoot = null; }
    }
}