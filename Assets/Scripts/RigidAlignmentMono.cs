using System.Collections.Generic;
using UnityEngine;
public class RigidAlignmentMono : MonoBehaviour
{
    public Camera cam;

    public Transform leftCube;
    public Transform rightCube;

    public GameObject markerPrefab;

    private List<Vector3> leftPoints = new();
    private List<Vector3> rightPoints = new();
    private List<GameObject> leftMarkers = new();
    private List<GameObject> rightMarkers = new();

    private RigidAlignment solver = new RigidAlignment();

    private GameObject _previewMarker;
    private GameObject _clone;

    void Update()
    {
        UpdatePreviewMarker();

        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ResetAlignment();
        }
    }

    void UpdatePreviewMarker()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit) &&
            (hit.transform == leftCube || hit.transform == rightCube))
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

    void CounterScaleMarker(Transform marker, Transform parent)
    {
        Vector3 originalScale = markerPrefab.transform.localScale;
        Vector3 parentScale = parent.lossyScale;
        marker.localScale = new Vector3(
            originalScale.x / parentScale.x,
            originalScale.y / parentScale.y,
            originalScale.z / parentScale.z
        );
    }

    void HandleClick()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // 기존 마커 클릭 → 삭제
            int leftIdx = leftMarkers.IndexOf(hit.transform.gameObject);
            if (leftIdx >= 0)
            {
                Destroy(leftMarkers[leftIdx]);
                leftMarkers.RemoveAt(leftIdx);
                leftPoints.RemoveAt(leftIdx);
                TryAlign();
                return;
            }

            int rightIdx = rightMarkers.IndexOf(hit.transform.gameObject);
            if (rightIdx >= 0)
            {
                Destroy(rightMarkers[rightIdx]);
                rightMarkers.RemoveAt(rightIdx);
                rightPoints.RemoveAt(rightIdx);
                TryAlign();
                return;
            }

            // 큐브 클릭 → 새 마커 생성
            Vector3 p = hit.point;

            if (hit.transform == leftCube)
            {
                var marker = Instantiate(markerPrefab, p, Quaternion.identity, leftCube);
                CounterScaleMarker(marker.transform, leftCube);
                EnableMarkerCollider(marker);
                leftMarkers.Add(marker);
                leftPoints.Add(p);
            }
            else if (hit.transform == rightCube)
            {
                var marker = Instantiate(markerPrefab, p, Quaternion.identity, rightCube);
                CounterScaleMarker(marker.transform, rightCube);
                EnableMarkerCollider(marker);
                rightMarkers.Add(marker);
                rightPoints.Add(rightCube.InverseTransformPoint(p));
            }

            TryAlign();
        }
    }

    void EnableMarkerCollider(GameObject marker)
    {
        var col = marker.GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

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

        // 마커 전부 삭제
        foreach (Transform child in leftCube)
        {
            if (child.gameObject != leftCube.gameObject)
                Destroy(child.gameObject);
        }
        foreach (Transform child in rightCube)
        {
            if (child.gameObject != rightCube.gameObject)
                Destroy(child.gameObject);
        }

        if (_previewMarker != null)
        {
            Destroy(_previewMarker);
            _previewMarker = null;
        }
    }
}