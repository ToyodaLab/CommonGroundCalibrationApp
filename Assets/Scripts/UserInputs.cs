using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using ViveTrackers;

public class UserInputs : MonoBehaviour
{
    private List<Vector3> tempLocations = new();
    private List<Quaternion> tempRotations = new();


    private List<Vector3> hs_storedLocations = new List<Vector3>();
    private List<Quaternion> hs_storedRotations = new List<Quaternion>();

    public void hs_zero_origin()
    {
        // Debug log before transformations
        for (int i = 0; i < hs_storedLocations.Count; ++i)
        {
            Debug.Log("Captured " + i + ":");
            Debug.Log("x: " + hs_storedLocations[i].x);
            Debug.Log("y: " + hs_storedLocations[i].y);
            Debug.Log("z: " + hs_storedLocations[i].z);
            Debug.Log("quaternion: " + hs_storedRotations[i]);
        }

        Vector3 originLocation = hs_storedLocations[0];
        Quaternion originRotation = hs_storedRotations[0];

        // Conjugate the origin quaternion (invert it)
        Quaternion originRotationInverse = Quaternion.Inverse(originRotation);

        for (int i = 0; i < hs_storedLocations.Count; ++i)
        {
            hs_storedLocations[i] -= originLocation;
            hs_storedRotations[i] = originRotationInverse * hs_storedRotations[i];
        }

        // Debug log after transformations
        for (int i = 0; i < hs_storedLocations.Count; ++i)
        {
            Debug.Log("New " + i + ":");
            Debug.Log("x: " + hs_storedLocations[i].x);
            Debug.Log("y: " + hs_storedLocations[i].y);
            Debug.Log("z: " + hs_storedLocations[i].z);
            Debug.Log("quaternion: " + hs_storedRotations[i]);
        }
    }

    public void hs_CopyToClipboard()
    {
        StringBuilder clipboardText = new StringBuilder();

        for (int i = 0; i < hs_storedLocations.Count; i++)
        {
            clipboardText.AppendLine($"Location {i}: {hs_storedLocations[i].x}, {hs_storedLocations[i].y}, {hs_storedLocations[i].z}");
            clipboardText.AppendLine($"Rotation {i}: {hs_storedRotations[i].x}, {hs_storedRotations[i].y}, {hs_storedRotations[i].z}, {hs_storedRotations[i].w}");
        }

        // Copy the formatted string to the system clipboard
        GUIUtility.systemCopyBuffer = clipboardText.ToString();

        Debug.Log("Data copied to clipboard.");
    }



    private Vector3 originPosition;
    private Quaternion originRotation;
    private GameObject originObject;

    private Vector3 hs_Position;
    private Quaternion hs_Rotation;


    public TextMeshProUGUI tempText;
    public TextMeshProUGUI storedText;

    public ViveTrackersManager trackersManager;
    private int camID = -1;
    private StreamWriter writer;

    public GameObject arrowPrefab;


    public void OnAddButtonClick()
    {
        // add new transform to templocations and temprotations
        // add to tempText as new line
        Debug.Log("Add Button clicked!");
        Transform currentTransform = trackersManager.getFirstTrackerTransform();
        tempLocations.Add(currentTransform.position);
        tempRotations.Add(currentTransform.rotation);
        tempText.text += currentTransform.position.ToString() + " " + currentTransform.rotation.eulerAngles + " \n";
    }
    public void OnClearButtonClick()
    {
        tempText.text = "";
        tempLocations.Clear();
        tempRotations.Clear();
        Debug.Log("Clear Button clicked!");
    }

    public void OnNextButtonClick()
    {
        Debug.Log("Store Button clicked!");
        Vector3 averageLocation = GetAverageFromList(tempLocations);
        Quaternion averageQuart = AverageQuaternion(tempRotations.ToArray());

        if (camID == -1)
        {
            storedText.text += "origin,";
            originPosition = averageLocation;
            originRotation = averageQuart;
            hs_Position = averageLocation;
            hs_Rotation = averageQuart;
        }
        else
        {
            storedText.text += camID + ",";

            hs_Position = averageLocation;
            hs_Rotation = averageQuart;
            GameObject newArrow = Instantiate(arrowPrefab, averageLocation, averageQuart);
            newArrow.transform.parent = originObject.transform;
        }
        storedText.text += averageLocation.ToString() + "," + averageQuart.eulerAngles.ToString() + "\n";

        hs_storedLocations.Add(hs_Position);
        hs_storedRotations.Add(hs_Rotation);
        
        camID++;
        OnClearButtonClick();
    }
    public void OnCommitButtonClick()
    {
        string filePath = Application.dataPath;
        writer = new StreamWriter(filePath + "/Data/" + "savedPositions.csv", true);

        writer.WriteLine(storedText.text);
        writer.Close();

        hs_zero_origin();
        hs_CopyToClipboard();

        Debug.Log("Commit Button clicked yeah!!");
        OnResetButtonClick();
    }
    public void OnResetButtonClick()
    {
        Debug.Log("Reset Button clicked!");
        storedText.text = "";
        camID = -1;
        originPosition = Vector3.zero;
        originRotation = Quaternion.identity;
        OnClearButtonClick();
    }

    public void TransmitAllPositions()
    {
        //TODO Send over network
    }

    Vector3 GetAverageFromList(List<Vector3> listToAverage)
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

    public Quaternion AverageQuaternion(Quaternion[] quats)
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
        //note: if speed is an issue, you can skip the normalization step
        return NormalizeQuaternion(new Quaternion(x, y, z, w));
    }

    //Get an average (mean) from more then two quaternions (with two, slerp would be used).
    //Note: this only works if all the quaternions are relatively close together.
    //Usage:
    //-Cumulative is an external Vector4 which holds all the added x y z and w components.
    //-newRotation is the next rotation to be added to the average pool
    //-firstRotation is the first quaternion of the array to be averaged
    //-addAmount holds the total amount of quaternions which are currently added
    static void AverageQuaternion_Internal(ref Vector4 cumulative, Quaternion newRotation, Quaternion firstRotation)
    {
        //Before we add the new rotation to the average (mean), we have to check whether the quaternion has to be inverted. Because
        //q and -q are the same rotation, but cannot be averaged, we have to make sure they are all the same.
        if (!AreQuaternionsClose(newRotation, firstRotation))
        {
            newRotation = InverseSignQuaternion(newRotation);
        }

        //Average the values
        cumulative.w += newRotation.w;
        cumulative.x += newRotation.x;
        cumulative.y += newRotation.y;
        cumulative.z += newRotation.z;
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
    public static Quaternion InverseSignQuaternion(Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, -q.z, -q.w);
    }

    //Returns true if the two input quaternions are close to each other. This can
    //be used to check whether or not one of two quaternions which are supposed to
    //be very similar but has its component signs reversed (q has the same rotation as
    //-q)
    public static bool AreQuaternionsClose(Quaternion q1, Quaternion q2)
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
}
