using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// iPad ARKit 기본 테스트: 화면 탭 → AR 표면 히트 → 앵커(마커) 배치.
/// XROrigin 하위에 ARRaycastManager가 있어야 동작한다.
///
/// 씬 구성:
///   1. AR Session (ARSession 컴포넌트)
///   2. XR Origin (XROrigin + ARRaycastManager + ARPlaneManager)
///      └─ Camera Offset
///         └─ Main Camera (ARCameraManager + ARCameraBackground)
///   3. 빈 GameObject에 이 스크립트 부착
///   4. _markerPrefab에 Sphere 등 프리팹 할당
/// </summary>
public class ARTapToAnchor : MonoBehaviour
{
    [Header("AR")]
    [SerializeField] private ARRaycastManager _raycastManager;

    [Header("마커")]
    [SerializeField] private GameObject _markerPrefab;

    [Header("디버그")]
    [SerializeField] private TMPro.TMP_Text _debugText;

    private readonly List<ARRaycastHit> _hits = new();
    private readonly List<GameObject> _markers = new();

    // ─── 컬러 팔레트 (정합 때와 동일) ───
    private static readonly Color[] PairColors = new[]
    {
        Color.red, Color.blue, Color.green, Color.yellow,
        Color.cyan, Color.magenta,
        new Color(1f, 0.5f, 0f),
        new Color(0.5f, 0f, 1f),
    };

    void Update()
    {
        // ESC(키보드) 또는 3-finger tap으로 리셋
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ResetMarkers();
            return;
        }

        // 터치 입력 (단일 터치, 시작 시점만)
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
                TryPlaceMarker(touch.position);
        }

#if UNITY_EDITOR
        // 에디터: 마우스 클릭으로 테스트
        if (Input.GetMouseButtonDown(0))
            TryPlaceMarker(Input.mousePosition);
#endif
    }

    void TryPlaceMarker(Vector2 screenPos)
    {
        if (_raycastManager == null) return;

        // AR Raycast: 감지된 평면/메시에 대해 히트 테스트
        if (_raycastManager.Raycast(screenPos, _hits, TrackableType.AllTypes))
        {
            var hit = _hits[0];
            Pose hitPose = hit.pose;

            // 마커 생성
            var marker = Instantiate(_markerPrefab, hitPose.position, hitPose.rotation);
            int idx = _markers.Count;
            SetColor(marker, PairColors[idx % PairColors.Length]);
            _markers.Add(marker);

            // ARAnchor 부착 (표면에 안정적으로 고정)
            if (marker.GetComponent<ARAnchor>() == null)
                marker.AddComponent<ARAnchor>();

            // 디버그 텍스트
            if (_debugText != null)
            {
                _debugText.text = $"Marker #{idx}: ({hitPose.position.x:F3}, {hitPose.position.y:F3}, {hitPose.position.z:F3})\n"
                    + $"Trackable: {hit.trackableId}\n"
                    + $"Total: {_markers.Count}";
            }
        }
        else
        {
            if (_debugText != null)
                _debugText.text = "No surface detected";
        }
    }

    void ResetMarkers()
    {
        foreach (var m in _markers)
            if (m != null) Destroy(m);
        _markers.Clear();

        if (_debugText != null)
            _debugText.text = "Reset";
    }

    void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }
}
