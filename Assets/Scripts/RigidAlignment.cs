using System.Collections.Generic;
using UnityEngine;
public class RigidAlignment
{
    public bool Solve(
        List<Vector3> leftPoints,
        List<Vector3> rightPoints,
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

        // 방향 벡터 계산
        Vector3 lDir1 = (l2 - l1).normalized;
        Vector3 lDir2 = (l3 - l1).normalized;

        Vector3 rDir1 = (r2 - r1).normalized;
        Vector3 rDir2 = (r3 - r1).normalized;

        // 회전 계산
        Quaternion rot1 = Quaternion.FromToRotation(rDir1, lDir1);
        Vector3 rDir2Rot = rot1 * rDir2;
        Quaternion rot2 = Quaternion.FromToRotation(rDir2Rot, lDir2);

        rotation = rot2 * rot1;

        // 위치 계산
        position = l1 - rotation * r1;

        return true;
    }
}