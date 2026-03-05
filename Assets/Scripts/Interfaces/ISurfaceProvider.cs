using UnityEngine;

/// <summary>
/// 표면 히트 결과.
/// Raycast 방식(Physics / AR)에 무관하게 동일한 구조로 결과를 전달한다.
/// </summary>
public struct SurfaceHitResult
{
    public Vector3 Point;
    public Vector3 Normal;
    public Transform HitTransform;
    public bool IsVirtual;
    public bool IsMarker;
}

/// <summary>
/// 표면 판별 추상화.
/// 에디터에서는 Physics.Raycast + Transform 비교,
/// AR에서는 ARRaycastManager + Physics.Raycast 하이브리드로 구현한다.
/// </summary>
public interface ISurfaceProvider
{
    /// <summary>
    /// Ray로 표면 히트 테스트.
    /// 구현체가 Physics.Raycast든 AR Raycast+Physics 하이브리드든 알아서 처리한다.
    /// </summary>
    bool TryHit(Ray ray, out SurfaceHitResult result);

    /// <summary>
    /// 드래그 중 유효한 표면인지 판별.
    /// virtual 마커는 virtual 표면만, real 마커는 real 표면만 허용.
    /// </summary>
    bool IsValidDragSurface(SurfaceHitResult result, bool isDraggingVirtual);

    /// <summary>
    /// 월드 좌표를 virtual 모델 로컬 좌표로 변환.
    /// </summary>
    Vector3 WorldToVirtualLocal(Vector3 worldPoint);

    /// <summary>
    /// 마커 판별용 콜백 등록. MarkerManager에서 제공하는 함수를 연결한다.
    /// </summary>
    void SetMarkerChecker(System.Func<GameObject, bool> isMarker);
}
