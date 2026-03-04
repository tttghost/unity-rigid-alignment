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

    private RigidAlignment solver = new RigidAlignment();

    private GameObject _previewMarker;

    private Vector3 _rightCubeInitPos;
    private Quaternion _rightCubeInitRot;

    void Start()
    {
        _rightCubeInitPos = rightCube.position;
        _rightCubeInitRot = rightCube.rotation;
    }

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
    void HandleClick()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 p = hit.point;

            if (hit.transform == leftCube)
            {
                var marker = Instantiate(markerPrefab, p, Quaternion.identity, leftCube);
                leftPoints.Add(p); // 월드 좌표 (목표 위치)
            }
            else if (hit.transform == rightCube)
            {
                var marker = Instantiate(markerPrefab, p, Quaternion.identity, rightCube);
                rightPoints.Add(rightCube.InverseTransformPoint(p)); // 로컬 좌표로 저장
            }

            TryAlign();
        }
    }
    void TryAlign()
    {
        if (leftPoints.Count >= 3 && rightPoints.Count >= 3)
        {
            if (solver.Solve(leftPoints, rightPoints, out var pos, out var rot))
            {
                rightCube.position = pos;
                rightCube.rotation = rot;
            }
        }
    }
    public void ResetAlignment()
    {
        leftPoints.Clear();
        rightPoints.Clear();

        // RightCube 원위치 복원
        rightCube.position = _rightCubeInitPos;
        rightCube.rotation = _rightCubeInitRot;

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