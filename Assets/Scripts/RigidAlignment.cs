using System.Collections.Generic;
using UnityEngine;
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

        if (leftPoints.Count < 3 || rightPoints.Count < 3)
            return false;

        Vector3 l1 = leftPoints[0];
        Vector3 l2 = leftPoints[1];
        Vector3 l3 = leftPoints[2];

        Vector3 r1 = rightPoints[0];
        Vector3 r2 = rightPoints[1];
        Vector3 r3 = rightPoints[2];

        // 로컬 좌표를 스케일 적용하여 실제 크기 반영
        Vector3 sr1 = Vector3.Scale(r1, rightScale);
        Vector3 sr2 = Vector3.Scale(r2, rightScale);
        Vector3 sr3 = Vector3.Scale(r3, rightScale);

        // 방향 벡터 계산
        Vector3 lDir1 = (l2 - l1).normalized;
        Vector3 lDir2 = (l3 - l1).normalized;

        Vector3 rDir1 = (sr2 - sr1).normalized;
        Vector3 rDir2 = (sr3 - sr1).normalized;

        // 회전 계산
        Quaternion rot1 = Quaternion.FromToRotation(rDir1, lDir1);
        Vector3 rDir2Rot = rot1 * rDir2;
        Quaternion rot2 = Quaternion.FromToRotation(rDir2Rot, lDir2);

        rotation = rot2 * rot1;

        // 위치 계산 (스케일 반영된 로컬 좌표 사용)
        position = l1 - rotation * sr1;

        return true;
    }
}