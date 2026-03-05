using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 정합 결과 시각화: Clone 생성 · RMSE 텍스트 · 잔차 라벨.
/// 순수 C# 클래스 — AlignmentController 에서 생성하여 사용한다.
/// </summary>
public class AlignmentVisualizer
{
    // ─── 의존성 ───
    private readonly Transform _virtualModel;
    private readonly Material _cloneMaterial;
    private readonly Material _residualLabelMaterial;
    private readonly TMP_Text _rmseText;
    private readonly Camera _cam;

    // ─── 상태 ───
    private GameObject _clone;
    private readonly List<GameObject> _realResidualLabels = new();
    private readonly List<GameObject> _virtualResidualLabels = new();

    // ─── 계층 정리 ───
    private Transform _residualLabelRoot;

    private Transform ResidualLabelRoot
    {
        get
        {
            if (_residualLabelRoot == null)
                _residualLabelRoot = new GameObject("ResidualLabels").transform;
            return _residualLabelRoot;
        }
    }

    // ─── 라벨 스케일 ───
    private const float RefDistance = 5f;
    public float LabelScale { get; set; } = 1f;

    // ─── 생성자 ───

    public AlignmentVisualizer(
        Transform virtualModel, Material cloneMaterial,
        Material residualLabelMaterial, TMP_Text rmseText, Camera cam)
    {
        _virtualModel = virtualModel;
        _cloneMaterial = cloneMaterial;
        _residualLabelMaterial = residualLabelMaterial;
        _rmseText = rmseText;
        _cam = cam;
    }

    // ═══════════════════════════════════════════
    //  Clone
    // ═══════════════════════════════════════════

    /// <summary>
    /// Clone이 없으면 생성한다. 자식(마커)은 제거, 콜라이더 비활성화, 클론 머티리얼 적용.
    /// </summary>
    public void EnsureClone()
    {
        if (_clone != null) return;

        _clone = Object.Instantiate(_virtualModel.gameObject);
        _clone.name = _virtualModel.name + "_Clone";

        // 자식(마커) 제거 — 비주얼만 남김
        var children = new List<Transform>();
        foreach (Transform child in _clone.transform)
            children.Add(child);
        foreach (var child in children)
            Object.Destroy(child.gameObject);

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

    /// <summary>
    /// Clone 위치/회전 설정 및 활성화.
    /// </summary>
    public void ShowClone(Vector3 position, Quaternion rotation)
    {
        EnsureClone();
        _clone.SetActive(true);
        _clone.transform.position = position;
        _clone.transform.rotation = rotation;
    }

    /// <summary>
    /// Clone 숨김.
    /// </summary>
    public void HideClone()
    {
        if (_clone != null)
            _clone.SetActive(false);
    }

    // ═══════════════════════════════════════════
    //  RMSE 텍스트
    // ═══════════════════════════════════════════

    /// <summary>
    /// RMSE 값과 inlier 비율을 표시한다.
    /// </summary>
    public void ShowRMSE(float rmse, int inlierCount, int totalCount)
    {
        if (_rmseText != null)
            _rmseText.text = $"RMSE: {rmse:F4} m  (inliers: {inlierCount}/{totalCount})";
    }

    /// <summary>
    /// RMSE 텍스트 초기화.
    /// </summary>
    public void ClearRMSE()
    {
        if (_rmseText != null)
            _rmseText.text = "";
    }

    // ═══════════════════════════════════════════
    //  잔차 라벨
    // ═══════════════════════════════════════════

    /// <summary>
    /// 마커별 잔차 라벨 갱신.
    /// realPositions, virtualPositions는 마커 월드 좌표.
    /// </summary>
    public void UpdateResidualLabels(
        float[] residuals, HashSet<int> outlierSet,
        IReadOnlyList<Vector3> realMarkerPositions,
        IReadOnlyList<Vector3> virtualMarkerPositions)
    {
        float maxInlierResidual = 0f;
        for (int i = 0; i < residuals.Length; i++)
            if (!outlierSet.Contains(i))
                maxInlierResidual = Mathf.Max(maxInlierResidual, residuals[i]);

        int pairCount = Mathf.Min(realMarkerPositions.Count, residuals.Length);
        pairCount = Mathf.Min(pairCount, virtualMarkerPositions.Count);

        // 풀링: 필요한 만큼 라벨 확보
        while (_realResidualLabels.Count < pairCount)
            _realResidualLabels.Add(CreateLabelObject());
        while (_virtualResidualLabels.Count < pairCount)
            _virtualResidualLabels.Add(CreateLabelObject());

        for (int i = 0; i < pairCount; i++)
        {
            bool isOutlier = outlierSet.Contains(i);
            Color labelColor = isOutlier
                ? new Color(0.5f, 0.5f, 0.5f, 0.6f)
                : ResidualToColor(residuals[i], maxInlierResidual);
            string text = $"{residuals[i]:F4} m";

            // Real 쪽
            var realLabel = _realResidualLabels[i];
            realLabel.SetActive(true);
            realLabel.transform.position = realMarkerPositions[i] + Vector3.up * 0.05f;
            var realTmp = realLabel.GetComponent<TextMeshPro>();
            realTmp.text = text;
            realTmp.color = labelColor;

            // Virtual 쪽
            var virtualLabel = _virtualResidualLabels[i];
            virtualLabel.SetActive(true);
            virtualLabel.transform.position = virtualMarkerPositions[i] + Vector3.up * 0.05f;
            var virtualTmp = virtualLabel.GetComponent<TextMeshPro>();
            virtualTmp.text = text;
            virtualTmp.color = labelColor;
        }

        // 남는 라벨 숨김
        for (int i = pairCount; i < _realResidualLabels.Count; i++)
            _realResidualLabels[i].SetActive(false);
        for (int i = pairCount; i < _virtualResidualLabels.Count; i++)
            _virtualResidualLabels[i].SetActive(false);
    }

    /// <summary>
    /// 잔차 라벨 숨김 (오브젝트 유지, 재활용).
    /// </summary>
    public void ClearResidualLabels()
    {
        foreach (var label in _realResidualLabels)
            if (label != null) label.SetActive(false);
        foreach (var label in _virtualResidualLabels)
            if (label != null) label.SetActive(false);
    }

    /// <summary>
    /// 잔차 라벨 파괴 (완전 초기화).
    /// </summary>
    public void DestroyResidualLabels()
    {
        foreach (var label in _realResidualLabels)
            if (label != null) Object.Destroy(label);
        _realResidualLabels.Clear();
        foreach (var label in _virtualResidualLabels)
            if (label != null) Object.Destroy(label);
        _virtualResidualLabels.Clear();
    }

    // ═══════════════════════════════════════════
    //  라벨 빌보드 + 고정 크기 (LateUpdate)
    // ═══════════════════════════════════════════

    /// <summary>
    /// 잔차 라벨을 카메라 방향으로 회전 + depth 기반 고정 크기 보상.
    /// Controller의 LateUpdate에서 호출한다.
    /// </summary>
    public void UpdateLabelTransforms()
    {
        if (_cam == null) return;
        UpdateLabelList(_realResidualLabels);
        UpdateLabelList(_virtualResidualLabels);
    }

    // ═══════════════════════════════════════════
    //  리셋
    // ═══════════════════════════════════════════

    /// <summary>
    /// 모든 시각화 요소를 파괴하고 초기화한다.
    /// </summary>
    public void Reset()
    {
        DestroyResidualLabels();
        ClearRMSE();

        if (_clone != null)
        {
            Object.Destroy(_clone);
            _clone = null;
        }

        if (_residualLabelRoot != null)
        {
            Object.Destroy(_residualLabelRoot.gameObject);
            _residualLabelRoot = null;
        }
    }

    // ═══════════════════════════════════════════
    //  내부 유틸
    // ═══════════════════════════════════════════

    private GameObject CreateLabelObject()
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

    private Color ResidualToColor(float residual, float maxResidual)
    {
        if (maxResidual < 1e-6f) return Color.green;
        float t = Mathf.Clamp01(residual / maxResidual);
        return t < 0.5f
            ? Color.Lerp(Color.green, Color.yellow, t * 2f)
            : Color.Lerp(Color.yellow, Color.red, (t - 0.5f) * 2f);
    }

    private void UpdateLabelList(List<GameObject> labels)
    {
        Vector3 camPos = _cam.transform.position;
        Vector3 camFwd = _cam.transform.forward;
        foreach (var label in labels)
        {
            if (label == null || !label.activeSelf) continue;
            label.transform.forward = camFwd;
            float depth = Mathf.Max(
                Vector3.Dot(label.transform.position - camPos, camFwd), 0.1f);
            float scale = LabelScale * (depth / RefDistance);
            label.transform.localScale = Vector3.one * scale;
        }
    }
}
