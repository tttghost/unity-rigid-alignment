using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// N점(N≥3) Kabsch 알고리즘 기반 rigid alignment.
/// SVD를 통해 최소자승 최적 회전·이동을 계산한다.
/// </summary>
public class RigidAlignment
{
    public bool Solve(
        List<Vector3> leftPoints,
        List<Vector3> rightPoints,
        Vector3 rightScale,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        int n = Mathf.Min(leftPoints.Count, rightPoints.Count);
        if (n < 3) return false;

        // 1. 스케일 적용
        var left = new Vector3[n];
        var right = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            left[i] = leftPoints[i];
            right[i] = Vector3.Scale(rightPoints[i], rightScale);
        }

        // 2. 무게중심
        Vector3 centL = Vector3.zero, centR = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            centL += left[i];
            centR += right[i];
        }
        centL /= n;
        centR /= n;

        // 3. 교차공분산 행렬 H = Σ (l_i - centL)(r_i - centR)^T
        float[,] H = new float[3, 3];
        for (int i = 0; i < n; i++)
        {
            Vector3 l = left[i] - centL;
            Vector3 r = right[i] - centR;
            H[0, 0] += l.x * r.x; H[0, 1] += l.x * r.y; H[0, 2] += l.x * r.z;
            H[1, 0] += l.y * r.x; H[1, 1] += l.y * r.y; H[1, 2] += l.y * r.z;
            H[2, 0] += l.z * r.x; H[2, 1] += l.z * r.y; H[2, 2] += l.z * r.z;
        }

        // 4. SVD → H = U · diag(S) · V^T
        SVD3x3(H, out var U, out var S, out var V);

        // 5. 반사 보정: det(U)·det(V) < 0 이면 마지막 열 뒤집기
        if (Det(U) * Det(V) < 0)
        {
            U[0, 2] = -U[0, 2];
            U[1, 2] = -U[1, 2];
            U[2, 2] = -U[2, 2];
        }

        // 6. R = V · U^T
        float[,] R = Mul(V, Transpose(U));

        // 7. 결과
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