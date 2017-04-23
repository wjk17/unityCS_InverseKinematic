using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IKBoneChain // IK骨骼链
{
    public Joint[] joints;
    public Transform end;
    public Transform target;
    public int iteration; // 计算的迭代次数
    public float maxDeltaAngle;
    public void Solve()
    {
        int i;
        for (i = 0; i < iteration; i++)
        {
            foreach (var joint in joints) IKTool.IKSolve(joint, target, end, maxDeltaAngle);
            //if (IKTool.Close(target.position, end.position)) { Debug.Log(i.ToString() + " 次迭代   完成"); return; }
            if (IKTool.Close(target.position, end.position)) return;
        }
        //Debug.Log(i.ToString() + " 次迭代  未完成");
    }
    public WRay Solve(int i)
    {
        var result = IKTool.IKSolve(joints[i], target, end, maxDeltaAngle);
        if (IKTool.Close(target.position, end.position)) Debug.Log("完成");
        return result;
    }
}
public class WRay
{
    public Vector3 pos;
    public Vector3 dir;
    public Color color;
    public void Draw()
    {
        Debug.DrawRay(pos, dir, color);
    }
    public WRay(Vector3 pos, Vector3 dir, Color color)
    {
        this.pos = pos;
        this.dir = dir * 3f;
        this.color = color;
    }
}
public static class IKTool
{
    public static float closest = 0.0001f;
    public static bool Close(Vector3 a, Vector3 b)
    {
        return CloseZero((a - b).sqrMagnitude);
    }
    public static bool Close(float a, float b)
    {
        return Mathf.Approximately(a, b) || Mathf.Abs(a - b) <= closest;
    }
    public static bool CloseZero(float f)
    {
        return Mathf.Approximately(f, 0f) || float.IsNaN(f) || Mathf.Abs(f) <= closest;
    }
    /// <summary>
    /// 计算骨骼链
    /// </summary>
    /// <param name="关节"></param>
    /// <param name="目标位置"></param>
    /// <param name="末端效应器"></param>
    /// <param name="每次迭代最大旋转角度"></param>
    /// <param name="迭代次数"></param>
    public static WRay IKSolve(Joint joint, Transform target, Transform end, float limitAngle)
    {
        // 关节相对于目标位置和末端效应器的位置
        Vector3 absJoint2End = end.position - joint.transform.position;
        Vector3 absJoint2Target = target.position - joint.transform.position;

        // 转为本地坐标系
        Quaternion invRotation = joint.transform.rotation.Conjugate();

        Vector3 localJoint2End = invRotation * absJoint2End;
        Vector3 localJoint2Target = invRotation * absJoint2Target;
        float deltaAngle = Mathf.Rad2Deg * Mathf.Acos(Vector3.Dot(localJoint2End.normalized, localJoint2Target.normalized));
        if (CloseZero(deltaAngle)) return null;

        Vector3 rotateAxis = Vector3.Cross(localJoint2Target, localJoint2End).normalized;
        Quaternion deltaRotation = Quaternion.AngleAxis(deltaAngle, rotateAxis);
        deltaRotation = deltaRotation.Conjugate();

        var ray = new WRay(joint.transform.position, rotateAxis, Color.green);

        var delta = deltaRotation.eulerAngles;
        var cur = joint.transform.localRotation.eulerAngles;
        var deltaV = Vector3.zero;
        if ((joint.constraints & RigidbodyConstraints.FreezeRotationX) == 0)
        {
            deltaV += new Vector3(limit(delta.x, limitAngle), 0f, 0f);
        }
        if ((joint.constraints & RigidbodyConstraints.FreezeRotationY) == 0)
        {
            deltaV += new Vector3(0f, limit(delta.y, limitAngle), 0f);
        }
        if ((joint.constraints & RigidbodyConstraints.FreezeRotationZ) == 0)
        {
            deltaV += new Vector3(0f, 0f, limit(delta.z, limitAngle));
        }
        deltaRotation.eulerAngles = deltaV;
        joint.transform.localRotation *= deltaRotation;
        var euler = joint.transform.localRotation.eulerAngles;
        var quat = new Quaternion();
        quat.eulerAngles = Vector3.zero;

        if ((joint.constraints & RigidbodyConstraints.FreezeRotationX) == 0)
        {
            quat.eulerAngles += new Vector3(Mathf.Clamp(euler.x, joint.minAngle, joint.maxAngle), 0f, 0f);
        }
        if ((joint.constraints & RigidbodyConstraints.FreezeRotationY) == 0)
        {
            quat.eulerAngles += new Vector3(0f, Mathf.Clamp(euler.y, joint.minAngle, joint.maxAngle), 0f);
        }
        if ((joint.constraints & RigidbodyConstraints.FreezeRotationZ) == 0)
        {
            quat.eulerAngles += new Vector3(0f, 0f, Mathf.Clamp(euler.z, joint.minAngle, joint.maxAngle));
        }

        joint.transform.localRotation = quat;
        return ray;
    }
    public static float limit(float angle, float max)
    {
        bool inverse = false;
        if (angle > 180)
        {
            angle = 360 - angle;
            inverse = true;
        }
        angle = Mathf.Clamp(angle, -max, max);
        return inverse ? -angle : angle;
    }
}

[System.Serializable]
public class Joint
{
    public RigidbodyConstraints constraints;
    public float maxAngle;
    public float minAngle;
    public Transform transform;
    public Joint(Transform transform)
    {
        this.transform = transform;
        constraints = RigidbodyConstraints.None;
    }
    public Joint(Transform transform, RigidbodyConstraints constraints)
    {
        this.transform = transform;
        this.constraints = constraints;
    }
}

public static class QuaternionTool
{
    public static Quaternion Conjugate(this Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, -q.z, q.w);
    }
}
