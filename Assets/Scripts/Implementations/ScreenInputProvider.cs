using UnityEngine;

/// <summary>
/// 화면 좌표 기반 입력 (마우스 / 터치 공용).
/// Unity Old Input System에서 터치는 자동으로 마우스 이벤트로 시뮬레이션되므로
/// PC와 모바일 모두 이 구현체 하나로 동작한다.
/// </summary>
public class ScreenInputProvider : IInputProvider
{
    private readonly Camera _cam;

    public ScreenInputProvider(Camera cam)
    {
        _cam = cam;
    }

    public Ray GetRay()
    {
        return _cam.ScreenPointToRay(Input.mousePosition);
    }

    public bool TryGetPointerDown(out Ray ray)
    {
        ray = GetRay();
        return Input.GetMouseButtonDown(0);
    }

    public bool IsPointerHeld()
    {
        return Input.GetMouseButton(0);
    }

    public bool TryGetPointerUp()
    {
        return Input.GetMouseButtonUp(0);
    }

    public bool ResetRequested()
    {
        return Input.GetKeyDown(KeyCode.Escape);
    }
}
