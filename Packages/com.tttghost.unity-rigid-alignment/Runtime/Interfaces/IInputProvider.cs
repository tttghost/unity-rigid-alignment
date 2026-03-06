using UnityEngine;

/// <summary>
/// 포인터 입력 추상화.
/// 화면 좌표 기반(마우스/터치)이든 직접 Ray(VR 컨트롤러)이든
/// 동일한 인터페이스로 포인터 상태를 제공한다.
/// </summary>
public interface IInputProvider
{
    /// <summary>현재 포인터가 가리키는 방향의 Ray</summary>
    Ray GetRay();

    /// <summary>포인터 다운 이벤트 발생 시 true + Ray 반환</summary>
    bool TryGetPointerDown(out Ray ray);

    /// <summary>포인터가 눌린 상태 유지 중인지</summary>
    bool IsPointerHeld();

    /// <summary>포인터 업 이벤트 발생 시 true</summary>
    bool TryGetPointerUp();

    /// <summary>정합 초기화 요청 (Escape, UI 버튼 등)</summary>
    bool ResetRequested();
}
