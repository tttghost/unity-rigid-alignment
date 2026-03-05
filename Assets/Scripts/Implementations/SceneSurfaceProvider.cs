using System;
using UnityEngine;

/// <summary>
/// 씬 오브젝트 기반 표면 판별 (에디터 / PC 빌드용).
/// Physics.Raycast 로 콜라이더를 검출하고,
/// hit.transform == _virtualModel 비교로 virtual/real 을 구분한다.
/// </summary>
public class SceneSurfaceProvider : ISurfaceProvider
{
    private readonly Transform _virtualModel;
    private Func<GameObject, bool> _isMarker;

    public SceneSurfaceProvider(Transform virtualModel)
    {
        _virtualModel = virtualModel;
    }

    // ─── ISurfaceProvider ───

    public bool TryHit(Ray ray, out SurfaceHitResult result)
    {
        result = default;

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return false;

        bool isMarker = _isMarker != null && _isMarker(hit.transform.gameObject);

        result = new SurfaceHitResult
        {
            Point        = hit.point,
            Normal       = hit.normal,
            HitTransform = hit.transform,
            IsVirtual    = hit.transform == _virtualModel,
            IsMarker     = isMarker,
        };
        return true;
    }

    public bool IsValidDragSurface(SurfaceHitResult result, bool isDraggingVirtual)
    {
        if (result.IsMarker) return false;
        return isDraggingVirtual ? result.IsVirtual : !result.IsVirtual;
    }

    public Vector3 WorldToVirtualLocal(Vector3 worldPoint)
    {
        return _virtualModel.InverseTransformPoint(worldPoint);
    }

    public void SetMarkerChecker(Func<GameObject, bool> isMarker)
    {
        _isMarker = isMarker;
    }
}
