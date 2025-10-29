using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class UtilityScripts
{
    public static Quaternion GetAverageQuaternion(Quaternion[] quats)
    {
        if (quats.Length == 0)
        {
            return Quaternion.identity;
        }

        Vector4 cumulative = new Vector4(0, 0, 0, 0);

        foreach (Quaternion quat in quats)
        {
            AverageQuaternion_Internal(ref cumulative, quat, quats[0]);
        }

        float addDet = 1f / (float)quats.Length;
        float x = cumulative.x * addDet;
        float y = cumulative.y * addDet;
        float z = cumulative.z * addDet;
        float w = cumulative.w * addDet;

        return NormalizeQuaternion(new Quaternion(x, y, z, w));
    }

    // Get an average from more then two quaternions
    // only works if all the quaternions are relatively close together.
    private static void AverageQuaternion_Internal(ref Vector4 cumulative, Quaternion newRotation, Quaternion firstRotation)
    {
        //Before we add the new rotation to the average (mean), we have to check whether the quaternion has to be inverted. Because
        //q and -q are the same rotation, but cannot be averaged, we have to make sure they are all the same.
        if (!AreQuaternionsClose(newRotation, firstRotation))
        {
            newRotation = InverseSignQuaternion(newRotation);
        }

        //Average the values
        cumulative.x += newRotation.x;
        cumulative.y += newRotation.y;
        cumulative.z += newRotation.z;
        cumulative.w += newRotation.w;
    }

    public static Quaternion NormalizeQuaternion(Quaternion quat)
    {
        float lengthD = 1.0f / Mathf.Sqrt(quat.w * quat.w + quat.x * quat.x + quat.y * quat.y + quat.z * quat.z);
        quat.x *= lengthD;
        quat.y *= lengthD;
        quat.z *= lengthD;
        quat.w *= lengthD;
        return quat;
    }

    //Changes the sign of the quaternion components. This is not the same as the inverse.
    private static Quaternion InverseSignQuaternion(Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, -q.z, -q.w);
    }

    //Returns true if the two input quaternions are close to each other. This can
    //be used to check whether or not one of two quaternions which are supposed to
    //be very similar but has its component signs reversed (q has the same rotation as -q)
    private static bool AreQuaternionsClose(Quaternion q1, Quaternion q2)
    {
        float dot = Quaternion.Dot(q1, q2);

        if (dot < 0.0f)
        {
            return false;
        }

        else
        {
            return true;
        }
    }

    // Average a list of Vectors to find midpoint
    public static Vector3 GetAverageFromList(List<Vector3> listToAverage)
    {
        Vector3 sum = Vector3.zero;
        // Sum all positions in the list
        foreach (Vector3 pos in listToAverage)
        {
            sum += pos;
        }

        // Divide the sum by the number of positions to get the average
        return sum / listToAverage.Count;
    }
}
