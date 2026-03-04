using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// N점(N≥3) Kabsch 알고리즘 기반 rigid alignment.
/// SVD를 통해 최소자승 최적 회전·이동을 계산한다.
/// 아웃라이어 자동 제거 (σ 기반 반복 필터링)
/// </summary>
public class RigidAlignment
{
    /// <summary>
    /// 아웃라이어 제거 후 정합. outlierIndices에 제외된 페어 인덱스 반환.
    /// </summary>
    public bool Solve(
        List<Vector3> realPoints,
        List<Vector3> virtualPoints,
        Vector3 virtualScale,
        out Vector3 position,
        out Quaternion rotation,
        out List<int> outlierIndices,
        float sigmaThreshold = 2f,
        int maxIterations = 3)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        outlierIndices = new List<int>();

        int n = Mathf.Min(realPoints.Count, virtualPoints.Count);
        if (n < 3) return false;

        // 원본 인덱스 추적용 리스트
        var activeIndices = new List<int>();
        for (int i = 0; i < n; i++) activeIndices.Add(i);

        var realArr = new List<Vector3>(realPoints.GetRange(0, n));
        var virtualArr = new List<Vector3>();
        for (int i = 0; i < n; i++)
            virtualArr.Add(Vector3.Scale(virtualPoints[i], virtualScale));

        for (int iter = 0; iter < maxIterations; iter++)
        {
            if (activeIndices.Count < 3) break;

            // Kabsch 정합
            if (!SolveCore(realArr, virtualArr, activeIndices, out position, out rotation))
                return false;

            // 4점 미만이면 아웃라이어 제거 불가
            if (activeIndices.Count <= 3) break;

            // 각 점의 잔차 계산
            var residuals = new List<float>();
            foreach (int idx in activeIndices)
            {
                Vector3 transformed = rotation * virtualArr[idx] + position;
                float residual = Vector3.Distance(realArr[idx], transformed);
                residuals.Add(residual);
            }

            // 평균, 표준편차
            float mean = 0f;
            foreach (float r in residuals) mean += r;
            mean /= residuals.Count;

            float variance = 0f;
            foreach (float r in residuals) variance += (r - mean) * (r - mean);
            float sigma = Mathf.Sqrt(variance / residuals.Count);

            // σ가 매우 작으면 (거의 완벽한 정합) 종료
            if (sigma < 1e-6f) break;

            // threshold 초과 점 제거
            float threshold = mean + sigmaThreshold * sigma;
            var newActive = new List<int>();
            bool removed = false;

            for (int i = 0; i < activeIndices.Count; i++)
            {
                if (residuals[i] <= threshold)
                    newActive.Add(activeIndices[i]);
                else
                    removed = true;
            }

            if (!removed || newActive.Count < 3) break;

            activeIndices = newActive;
        }

        // 최종 정합
        if (activeIndices.Count < 3) return false;
        SolveCore(realArr, virtualArr, activeIndices, out position, out rotation);

        // 아웃라이어 인덱스 수집
        var activeSet = new HashSet<int>(activeIndices);
        for (int i = 0; i < n; i++)
        {
            if (!activeSet.Contains(i))
                outlierIndices.Add(i);
        }

        return true;
    }

    /// <summary>
    /// 하위호환: 아웃라이어 정보 없이 호출
    /// </summary>
    public bool Solve(
        List<Vector3> realPoints,
        List<Vector3> virtualPoints,
        Vector3 virtualScale,
        out Vector3 position,
        out Quaternion rotation)
    {
        return Solve(realPoints, virtualPoints, virtualScale, out position, out rotation, out _);
    }

    /// <summary>
    /// activeIndices 부분집합으로 Kabsch 정합
    /// </summary>
    bool SolveCore(
        List<Vector3> real,
        List<Vector3> virt,
        List<int> indices,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        int n = indices.Count;
        if (n < 3) return false;

        // 무게중심
        Vector3 centL = Vector3.zero, centR = Vector3.zero;
        foreach (int i in indices)
        {
            centL += real[i];
            centR += virt[i];
        }
        centL /= n;
        centR /= n;

        // 교차공분산 행렬 H
        float[,] H = new float[3, 3];
        foreach (int i in indices)
        {
            Vector3 l = real[i] - centL;
            Vector3 r = virt[i] - centR;
            H[0, 0] += l.x * r.x; H[0, 1] += l.x * r.y; H[0, 2] += l.x * r.z;
            H[1, 0] += l.y * r.x; H[1, 1] += l.y * r.y; H[1, 2] += l.y * r.z;
            H[2, 0] += l.z * r.x; H[2, 1] += l.z * r.y; H[2, 2] += l.z * r.z;
        }

        // SVD
        SVD3x3(H, out var U, out var S, out var V);

        if (Det(U) * Det(V) < 0)
        {
            U[0, 2] = -U[0, 2];
            U[1, 2] = -U[1, 2];
            U[2, 2] = -U[2, 2];
        }

        float[,] R = Mul(V, Transpose(U));
        rotation = MatrixToQuaternion(R);
        position = centL - rotation * centR;
        return true;
    }

    // ─── 3×3 행렬 유틸리티 ───

    static float[,] Transpose(float[,] m) => new float[3, 3]
    {
        { m[0, 0], m[1, 0], m[2, 0] },
        { m[0, 1], m[1, 1], m[2, 1] },
        { m[0, 2], m[1, 2], m[2, 2] }
    };

    static float[,] Mul(float[,] a, float[,] b)
    {
        float[,] r = new float[3, 3];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                for (int k = 0; k < 3; k++)
                    r[i, j] += a[i, k] * b[k, j];
        return r;
    }

    static float Det(float[,] m) =>
        m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
      - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
      + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);

    // ─── SVD (Jacobi eigendecomposition 기반) ───

    /// <summary>
    /// 3×3 SVD: H = U · diag(S) · V^T
    /// H^T·H 의 고유분해로 V, S를 구한 뒤 U = H·V·S^-1
    /// </summary>
    static void SVD3x3(float[,] H, out float[,] U, out float[] S, out float[,] V)
    {
        float[,] HtH = Mul(Transpose(H), H);

        JacobiEigen(HtH, out float[] eigvals, out V);
        SortEigenDesc(ref eigvals, ref V);

        S = new float[3];
        for (int i = 0; i < 3; i++)
            S[i] = Mathf.Sqrt(Mathf.Max(0f, eigvals[i]));

        // U = H · V · diag(1/S)
        float[,] HV = Mul(H, V);
        U = new float[3, 3];
        for (int col = 0; col < 3; col++)
        {
            float inv = S[col] > 1e-8f ? 1f / S[col] : 0f;
            for (int row = 0; row < 3; row++)
                U[row, col] = HV[row, col] * inv;
        }
    }

    /// <summary>
    /// Jacobi 반복법으로 3×3 대칭행렬의 고유값·고유벡터 계산
    /// </summary>
    static void JacobiEigen(float[,] A, out float[] eigenvalues, out float[,] eigenvectors)
    {
        float[,] a = (float[,])A.Clone();
        float[,] v = new float[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

        for (int iter = 0; iter < 100; iter++)
        {
            // 가장 큰 비대각 원소 찾기
            int p = 0, q = 1;
            float best = Mathf.Abs(a[0, 1]);
            if (Mathf.Abs(a[0, 2]) > best) { p = 0; q = 2; best = Mathf.Abs(a[0, 2]); }
            if (Mathf.Abs(a[1, 2]) > best) { p = 1; q = 2; best = Mathf.Abs(a[1, 2]); }
            if (best < 1e-10f) break;

            float app = a[p, p], aqq = a[q, q], apq = a[p, q];
            float theta = Mathf.Abs(app - aqq) < 1e-10f
                ? Mathf.PI / 4f
                : 0.5f * Mathf.Atan2(2f * apq, app - aqq);

            float c = Mathf.Cos(theta), s = Mathf.Sin(theta);

            // a' = G^T · a · G
            float[,] b = (float[,])a.Clone();
            b[p, p] = c * c * app + 2 * s * c * apq + s * s * aqq;
            b[q, q] = s * s * app - 2 * s * c * apq + c * c * aqq;
            b[p, q] = b[q, p] = 0f;
            for (int r = 0; r < 3; r++)
            {
                if (r == p || r == q) continue;
                float arp = a[r, p], arq = a[r, q];
                b[r, p] = b[p, r] = c * arp + s * arq;
                b[r, q] = b[q, r] = -s * arp + c * arq;
            }
            a = b;

            // V = V · G
            for (int r = 0; r < 3; r++)
            {
                float vp = v[r, p], vq = v[r, q];
                v[r, p] = c * vp + s * vq;
                v[r, q] = -s * vp + c * vq;
            }
        }

        eigenvalues = new[] { a[0, 0], a[1, 1], a[2, 2] };
        eigenvectors = v;
    }

    static void SortEigenDesc(ref float[] vals, ref float[,] vecs)
    {
        for (int i = 0; i < 2; i++)
            for (int j = i + 1; j < 3; j++)
                if (vals[j] > vals[i])
                {
                    (vals[i], vals[j]) = (vals[j], vals[i]);
                    for (int r = 0; r < 3; r++)
                        (vecs[r, i], vecs[r, j]) = (vecs[r, j], vecs[r, i]);
                }
    }

    // ─── 회전행렬 → 쿼터니언 (Shepperd method) ───

    static Quaternion MatrixToQuaternion(float[,] m)
    {
        float tr = m[0, 0] + m[1, 1] + m[2, 2];
        float w, x, y, z;

        if (tr > 0f)
        {
            float s = Mathf.Sqrt(tr + 1f) * 2f;
            w = 0.25f * s;
            x = (m[2, 1] - m[1, 2]) / s;
            y = (m[0, 2] - m[2, 0]) / s;
            z = (m[1, 0] - m[0, 1]) / s;
        }
        else if (m[0, 0] > m[1, 1] && m[0, 0] > m[2, 2])
        {
            float s = Mathf.Sqrt(1f + m[0, 0] - m[1, 1] - m[2, 2]) * 2f;
            w = (m[2, 1] - m[1, 2]) / s;
            x = 0.25f * s;
            y = (m[0, 1] + m[1, 0]) / s;
            z = (m[0, 2] + m[2, 0]) / s;
        }
        else if (m[1, 1] > m[2, 2])
        {
            float s = Mathf.Sqrt(1f + m[1, 1] - m[0, 0] - m[2, 2]) * 2f;
            w = (m[0, 2] - m[2, 0]) / s;
            x = (m[0, 1] + m[1, 0]) / s;
            y = 0.25f * s;
            z = (m[1, 2] + m[2, 1]) / s;
        }
        else
        {
            float s = Mathf.Sqrt(1f + m[2, 2] - m[0, 0] - m[1, 1]) * 2f;
            w = (m[1, 0] - m[0, 1]) / s;
            x = (m[0, 2] + m[2, 0]) / s;
            y = (m[1, 2] + m[2, 1]) / s;
            z = 0.25f * s;
        }

        return new Quaternion(x, y, z, w).normalized;
    }
}