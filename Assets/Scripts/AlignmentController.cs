using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Rigid Alignment 오케스트레이터.
/// IInputProvider · ISurfaceProvider · MarkerManager · AlignmentVisualizer · RigidAlignment 을 조합하여
/// 입력 → 표면 판별 → 마커 관리 → 정합 계산 → 시각화 파이프라인을 구동한다.
/// 이 스크립트가 RigidAlignmentMono를 완전히 대체한다.
/// </summary>
public class AlignmentController : MonoBehaviour
{
    // ─── Inspector ───
    [Header("참조")]
    [SerializeField] private Camera _cam;
    [SerializeField] private Transform _virtualModel;
    [SerializeField] private GameObject _markerPrefab;
    [SerializeField] private Material _cloneMaterial;
    [SerializeField] private TMP_Text _rmseText;
    [SerializeField] private Material _residualLabelMaterial;

    [Header("크기 설정")]
    [SerializeField, Tooltip("마커 표시 배율")]
    private float _markerScale = 1f;
    [SerializeField, Tooltip("잔차 라벨 표시 배율")]
    private float _labelScale = 1f;

    // ─── 내부 모듈 ───
    private IInputProvider _input;
    private ISurfaceProvider _surface;
    private MarkerManager _markers;
    private AlignmentVisualizer _visualizer;
    private RigidAlignment _solver;

    // ─── 드래그 상태 ───
    private const float DragThreshold = 5f; // 픽셀
    private GameObject _dragMarker;
    private int _dragIndex;
    private bool _dragIsVirtual;
    private Vector3 _dragStartWorldPos;
    private Vector3 _dragStartScreenPos;
    private bool _isDragging;

    // ─── 잔차 라벨 위치 캐시 (매 프레임 할당 방지) ───
    private readonly List<Vector3> _cachedRealPositions = new();
    private readonly List<Vector3> _cachedVirtualPositions = new();

    // ═══════════════════════════════════════════
    //  초기화
    // ═══════════════════════════════════════════

    void Awake()
    {
        _input = new ScreenInputProvider(_cam);
        _surface = new SceneSurfaceProvider(_virtualModel);
        _markers = new MarkerManager(_markerPrefab, _cam) { MarkerScale = _markerScale };
        _visualizer = new AlignmentVisualizer(
            _virtualModel, _cloneMaterial, _residualLabelMaterial, _rmseText, _cam)
        { LabelScale = _labelScale };
        _solver = new RigidAlignment();

        _surface.SetMarkerChecker(_markers.IsMarker);
    }

    // ═══════════════════════════════════════════
    //  메인 루프
    // ═══════════════════════════════════════════

    void Update()
    {
        // Inspector에서 실시간 변경된 스케일 반영
        _markers.MarkerScale = _markerScale;
        _visualizer.LabelScale = _labelScale;

        if (_input.ResetRequested())
        {
            ResetAlignment();
            return;
        }

        UpdatePreview();

        if (_input.TryGetPointerDown(out Ray ray))
            OnPointerDown(ray);

        if (_input.IsPointerHeld() && _dragMarker != null)
            OnPointerDrag();

        if (_input.TryGetPointerUp() && _dragMarker != null)
            OnPointerUp();
    }

    void LateUpdate()
    {
        _markers.UpdateFixedScales();
        _visualizer.UpdateLabelTransforms();
    }

    // ═══════════════════════════════════════════
    //  프리뷰 마커
    // ═══════════════════════════════════════════

    void UpdatePreview()
    {
        if (_dragMarker != null)
        {
            _markers.HidePreview();
            return;
        }

        Ray ray = _input.GetRay();
        if (_surface.TryHit(ray, out var result) && !result.IsMarker)
        {
            _markers.ShowPreview(result.Point, result.IsVirtual);
        }
        else
        {
            _markers.HidePreview();
        }
    }

    // ═══════════════════════════════════════════
    //  입력 핸들러
    // ═══════════════════════════════════════════

    void OnPointerDown(Ray ray)
    {
        _isDragging = false;
        _dragStartScreenPos = Input.mousePosition;

        if (!_surface.TryHit(ray, out var result)) return;

        // 마커를 눌렀으면 → 드래그 준비
        if (result.IsMarker &&
            _markers.TryFindMarker(result.HitTransform.gameObject, out bool isVirtual, out int index))
        {
            _dragMarker = _markers.GetMarker(isVirtual, index);
            _dragIndex = index;
            _dragIsVirtual = isVirtual;
            _dragStartWorldPos = _dragMarker.transform.position;
            return;
        }

        // 표면 클릭 → 새 마커 생성
        if (result.IsVirtual)
        {
            Vector3 localPt = _surface.WorldToVirtualLocal(result.Point);
            _markers.AddVirtualMarker(result.Point, localPt);
        }
        else
        {
            _markers.AddRealMarker(result.Point);
        }
        TryAlign();
    }

    void OnPointerDrag()
    {
        // 드래그 임계값 체크
        if (!_isDragging)
        {
            if (Vector3.Distance(Input.mousePosition, _dragStartScreenPos) < DragThreshold)
                return;
            _isDragging = true;
            _markers.SetColliderEnabled(_dragMarker, false);
        }

        // 표면 위로 마커 이동 + 실시간 정합
        Ray ray = _input.GetRay();
        if (_surface.TryHit(ray, out var result) &&
            _surface.IsValidDragSurface(result, _dragIsVirtual))
        {
            if (_dragIsVirtual)
            {
                Vector3 localPt = _surface.WorldToVirtualLocal(result.Point);
                _markers.UpdateVirtualPoint(_dragIndex, result.Point, localPt);
            }
            else
            {
                _markers.UpdateRealPoint(_dragIndex, result.Point);
            }
            TryAlign();
        }
        else
        {
            // 유효 표면 밖 → 드래그 시작 위치로 즉시 원복
            if (_dragIsVirtual)
            {
                Vector3 localPt = _surface.WorldToVirtualLocal(_dragStartWorldPos);
                _markers.UpdateVirtualPoint(_dragIndex, _dragStartWorldPos, localPt);
            }
            else
            {
                _markers.UpdateRealPoint(_dragIndex, _dragStartWorldPos);
            }
            TryAlign();
        }
    }

    void OnPointerUp()
    {
        if (!_isDragging)
        {
            // 드래그 아닌 클릭 → 마커 페어 삭제
            _markers.DeleteMarkerPair(_dragIndex, _dragIsVirtual);
            _dragMarker = null;
            TryAlign();
            return;
        }

        // 드래그 완료 — 콜라이더 복원
        _markers.EnableCollider(_dragMarker);
        _dragMarker = null;
        _isDragging = false;
    }

    // ═══════════════════════════════════════════
    //  정합
    // ═══════════════════════════════════════════

    void TryAlign()
    {
        int pairCount = Mathf.Min(_markers.RealCount, _markers.VirtualCount);

        if (pairCount >= 3)
        {
            if (_solver.Solve(_markers.RealPoints, _markers.VirtualPoints,
                    _virtualModel.localScale, out var pos, out var rot,
                    out var outliers, out var residuals))
            {
                _visualizer.ShowClone(pos, rot);

                var outlierSet = new HashSet<int>(outliers);
                _markers.ApplyOutlierVisuals(outlierSet);

                float rmse = RigidAlignment.ComputeRMSE(residuals, outliers);
                _visualizer.ShowRMSE(rmse, pairCount - outliers.Count, pairCount);

                // 마커 월드 좌표 수집 (잔차 라벨용)
                CollectMarkerWorldPositions(pairCount);
                _visualizer.UpdateResidualLabels(
                    residuals, outlierSet,
                    _cachedRealPositions, _cachedVirtualPositions);
            }
        }
        else
        {
            _visualizer.HideClone();
            _markers.RefreshColors();
            _visualizer.ClearResidualLabels();
            _visualizer.ClearRMSE();
        }
    }

    void CollectMarkerWorldPositions(int count)
    {
        _cachedRealPositions.Clear();
        _cachedVirtualPositions.Clear();
        for (int i = 0; i < count; i++)
        {
            _cachedRealPositions.Add(_markers.GetMarker(false, i).transform.position);
            _cachedVirtualPositions.Add(_markers.GetMarker(true, i).transform.position);
        }
    }

    // ═══════════════════════════════════════════
    //  리셋
    // ═══════════════════════════════════════════

    void ResetAlignment()
    {
        _markers.Reset();
        _visualizer.Reset();
        _dragMarker = null;
        _isDragging = false;
    }
}
