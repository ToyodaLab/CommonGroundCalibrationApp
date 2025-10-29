using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ViveTrackers;
using System.IO;
using System;
using System.Linq;


public enum RoomList {SelectARoom, ToyodaLab, ToyodaLabWindow, CGLL, CGLLV2, Unknown, OpenCampusLab, OpenCampusClass, DemoDayDoor, DemoDayWindow, FreeText};

public enum CalibrationTypesList { OneVive_NotReady, TwoVives, VivePerCamera};

public class PairMeasures : MonoBehaviour
{
    // Set in scene
    public ViveTrackersManager trackersManager;
    public GameObject arrowPrefab;

    // Set in scene
    public TMP_Dropdown roomDropdown;
    public TMP_Dropdown CalibrationTypeDropdown;
    public TextMeshProUGUI txtRef;
    public TextMeshProUGUI txtPos;
    public TextMeshProUGUI txtC;
    public TextMeshProUGUI txtSaved;

    private RoomList RoomName;
    private CalibrationTypesList CalibrationTypeName;


    // First B is -1 (preincrements)
    private int dataSetCount = -1;
    private int sensorCount = 0;

    // Save to file later
    private StreamWriter writer;

    private List<Vector3> tempRef_XYZs = new();
    private List<Vector3> tempPos_XYZs = new();
    private List<Quaternion> tempRef_PYRs = new();
    private List<Quaternion> tempPos_PYRs = new();

    private List<Vector3> tempC_XYZs = new();
    private List<Quaternion> tempC_PYRs = new();

    // Averaged transforms to be transmitted
    private List<Transform> relativeTransforms = new();

    private Transform averageATransform;
    private Transform averageBTransform;
    private Transform averageCTransform;


    public List<GameObject> SensorList = new List<GameObject>();
    public GameObject lastSensor = null;

    private static int hs_count_trackers = 0; // Variable to store the number of connected trackers

    private static bool flag_switchtrackers_ref_pos = false;  // alternates between reference (not to record) or position ()
    private static List<ViveTracker> hs_trackers = new List<ViveTracker>();

    private static Transform hs_tracker0 = null;
    private static Transform hs_tracker1 = null;
    private static Transform hs_tracker2 = null;


    private static bool sessionStarted = false; // Ensures session header is added only once

    private List<string> savedTransformData_ref = new List<string>();
    private List<string> savedTransformData_pos = new List<string>();


    private static string path_pos; // path to save captured positions 
    private static string path_cam; // path to save captured positions which correspond to cameras
    private static string path_cg0; // path to save all the positions taken with local origin in the last position (CG ZERO)


    static int hs_number_pos = -1;
    static int hs_number_cam = -1;

    // Location ToyodaLab 601 model
    static float room_pos_x = 1.88f;
    static float room_pos_y = -1.4f;
    static float room_pos_z = -5.9f;
    static float room_rot_x = 0f;
    static float room_rot_y = 90f;
    static float room_rot_z = 0f;

    private static string path_banzai;

    private void WriteSessionHeader()
    {
        string sessionHeader = $"\nSESSION {RoomName} : {CalibrationTypeName} : {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
        File.AppendAllText(path_pos, sessionHeader);
        File.AppendAllText(path_cam, sessionHeader);
        File.AppendAllText(path_cg0, sessionHeader);
        Debug.Log("Session header updated for: " + RoomName);
    }



    private string hsVector3ToString(Vector3 vector)
    {
        return $"{vector.x:F8} {vector.y:F8} {vector.z:F8}";
    }


    private IEnumerator Start()
    {

        Debug.Log("PairMeasures::Start Running...");

        // Populate dropdown with enum names
        string[] roomNames = Enum.GetNames(typeof(RoomList));
        roomDropdown.ClearOptions();
        roomDropdown.AddOptions(roomNames.Select(name => new TMP_Dropdown.OptionData(name)).ToList());

        // Set dropdown to current RoomName value
        roomDropdown.value = (int)RoomName;

        // Register callback to update RoomName when dropdown changes
        roomDropdown.onValueChanged.AddListener(index =>
        {
            RoomName = (RoomList)index;
            Debug.Log("Selected Room: " + RoomName);
            WriteSessionHeader(); // <-- log new session

        });


        // Populate dropdown with enum names
        string[] CalibrationTypeNames = Enum.GetNames(typeof(CalibrationTypesList));
        CalibrationTypeDropdown.ClearOptions();
        CalibrationTypeDropdown.AddOptions(CalibrationTypeNames.Select(name => new TMP_Dropdown.OptionData(name)).ToList());

        // Set dropdown to current RoomName value
        CalibrationTypeDropdown.value = (int)CalibrationTypeName;

        // Register callback to update RoomName when dropdown changes
        CalibrationTypeDropdown.onValueChanged.AddListener(index =>
        {
            CalibrationTypesList selectedType = (CalibrationTypesList)index;

            if (
                (selectedType == CalibrationTypesList.OneVive_NotReady && hs_trackers.Count == 1) ||
                (selectedType == CalibrationTypesList.TwoVives && hs_trackers.Count > 1) ||
                (selectedType == CalibrationTypesList.VivePerCamera && hs_trackers.Count > 2))
            {
                CalibrationTypeName = (CalibrationTypesList)index;
                Debug.Log("Selected Calibration Type: " + CalibrationTypeName);
                WriteSessionHeader(); // <-- log new session

            }
            else 
                {
                   Debug.Log($"Only {hs_trackers.Count} Vive Trackers available! Please change Calibration Type."); 
                }
        });



        path_pos = Path.Combine(UnityEngine.Application.persistentDataPath, "transformData_pos.txt");
        path_cam = Path.Combine(UnityEngine.Application.persistentDataPath, "transformData_cam.txt");
        path_cg0 = Path.Combine(UnityEngine.Application.persistentDataPath, "transformData_cg0.txt");

        path_banzai = Path.Combine(Application.persistentDataPath, "transformData_trackers_banzai.txt");



        Debug.Log($"Paths initialized: path_pos={path_pos}, path_cam={path_cam}");

        if (!File.Exists(path_pos)) File.WriteAllText(path_pos, "");
        if (!File.Exists(path_cam)) File.WriteAllText(path_cam, "");
        if (!File.Exists(path_cg0)) File.WriteAllText(path_cg0, "");
        if (!File.Exists(path_banzai)) File.WriteAllText(path_banzai, "");



        if (!sessionStarted)
        {
            WriteSessionHeader();

            sessionStarted = true;
            Debug.Log("HS_SaveTransformData: Session header written.");
        }




        GameObject objA = new GameObject("TempA");
        averageATransform = objA.transform;
        GameObject objB = new GameObject("TempB");
        averageBTransform = objB.transform;
        lastSensor = null;
        GameObject objC = new GameObject("TempC");
        averageCTransform = objC.transform;

        // Find the tracker manager
        trackersManager = FindObjectOfType<ViveTrackersManager>();

        if (trackersManager == null)
        {
            Debug.LogError("ViveTrackersManager instance not found!");
            yield break;
        }

        // Wait for a short delay to ensure trackers are initialized
        yield return new WaitForSeconds(2f);

        // Refresh and retrieve tracker list
        trackersManager.RefreshTrackers();
        hs_trackers = trackersManager.hs_GetTrackedDevices();
        hs_count_trackers = hs_trackers.Count;

        Debug.Log($"Tracking {hs_count_trackers} devices");

        // Sort trackers by name
        if (hs_count_trackers > 0)
        {
            hs_trackers.Sort((a, b) => a.name.CompareTo(b.name));
        }

        Debug.Log("initialize hs_trackers");

        // Assign tracker references
        // LK MIRROR FIX
        // Now uses cameras to ensure the direction and rotation are correct.
        // Uses the camera transform rather than the parent object.
        hs_tracker0 = hs_count_trackers > 0 ? hs_trackers[0].GetComponentInChildren<Camera>().transform : null;
        hs_tracker1 = hs_count_trackers > 1 ? hs_trackers[1].GetComponentInChildren<Camera>().transform : null;
        hs_tracker2 = hs_count_trackers > 2 ? hs_trackers[2].GetComponentInChildren<Camera>().transform : null; 

        if (hs_tracker0 == null || hs_tracker1 == null || hs_tracker2 == null)
        {
            Debug.LogError("ERROR: One of the trackers is NULL!");
        }
        else
        {
            Debug.Log($"T NAMES: {hs_tracker0.name} and {hs_tracker1.name} and {hs_tracker2.name}");
        }



        for (int i = 0; i < hs_trackers.Count; i++)
        {
            hs_trackers[i].gameObject.name = i.ToString();
        }



        // Log detected tracker names
        if (hs_count_trackers > 0)
        {
            foreach (var tracker in hs_trackers)
            {
                Debug.Log($"Tracker found: {tracker.name}");
            }
        }
        else
        {
            Debug.LogError("No trackers detected!");
        }

        Debug.Log(" Name 1 " + hs_tracker0.name + " Name 2 " + hs_tracker1.name + " Name 3 " + hs_tracker2.name);

    }


    // TwoVives
    private IEnumerator hs_RepeatingLog(bool flag_ref_pos, bool flag_switchtrackers_ref_pos)
    {
        Debug.Log("flag_ref_pos " + flag_ref_pos);
        Debug.Log("flag_switchtrackers_ref_pos " + flag_switchtrackers_ref_pos);

        Debug.Log(" test 1 ");



        float durationSeconds = 0.11f; // seconds
        float intervalSeconds = 0.1f;
        float elapsedTime = 0f;
        int counter = 0;



        // Log detected tracker names
        if (hs_count_trackers > 0)
        {
            foreach (var tracker in hs_trackers)
            {
                Debug.Log($"Tracker found: {tracker.name}");
            }
        }
        else
        {
            Debug.LogError("No trackers detected!");
        }



        Debug.Log(" Name 1 " + hs_tracker0.name + " Name 2 " + hs_tracker1.name);



        Transform currentTransform = null;

        Transform currentTransform2 = null;


        Debug.Log(" test 3 ");

        while (elapsedTime < durationSeconds)
        {

            if (hs_count_trackers == 1)
            {
                currentTransform = hs_tracker0;
            }
            else if (hs_count_trackers >= 2)
            {
                if (!flag_switchtrackers_ref_pos)
                {

                    currentTransform = flag_ref_pos ? hs_tracker1.GetComponent<Camera>().transform : hs_tracker0.GetComponent<Camera>().transform;
                    currentTransform2 = flag_ref_pos ? hs_tracker0.GetComponent<Camera>().transform : hs_tracker1.GetComponent<Camera>().transform;

                }
                else
                {
                    currentTransform = flag_ref_pos ? hs_tracker0.GetComponent<Camera>().transform : hs_tracker1.GetComponent<Camera>().transform;
                    currentTransform2 = flag_ref_pos ? hs_tracker1.GetComponent<Camera>().transform : hs_tracker0.GetComponent<Camera>().transform;
                }
            }
            else
            {
                Debug.LogError("No trackers detected.");
                yield break;
            }

            if (currentTransform == null || currentTransform2 == null)
            {
                Debug.LogError("Tracker transform is null!");
                yield break;
            }


            Debug.Log(" test 4 ");


            Debug.Log(" Assigned to currentTransform.name " + currentTransform.name + " currentTransform.position " + currentTransform.position + " currentTransform.rotation " + currentTransform.rotation);
            Debug.Log(" Assigned to currentTransform.name " + currentTransform2.name + " currentTransform2.position " + currentTransform2.position + " currentTransform2.rotation " + currentTransform2.rotation);



            string stringToRecord = counter + "," + Mathf.RoundToInt(Time.realtimeSinceStartup * 1000) + "," +
                TransformToEulerString(currentTransform) + "," +
                QuaternionToString(currentTransform.rotation);



            if (!flag_ref_pos) // false save as ref // true save as position 
            {
                txtRef.text = "Reference from Tracker " + currentTransform.name + "\n";
                tempRef_XYZs.Add(currentTransform.position);
                tempRef_PYRs.Add(currentTransform.rotation);

                txtRef.text += counter + " of " + ((int)durationSeconds / intervalSeconds) + " \n\n";
                txtRef.text += "Last: " + stringToRecord + " \n";

                averageATransform.position = GetAverageFromList(tempRef_XYZs);
                averageATransform.rotation = GetAverageQuaternion(tempRef_PYRs.ToArray());
            }
            else
            {
                txtPos.text = "Position From Tracker " + currentTransform.name + "\n";
                tempPos_XYZs.Add(currentTransform.position);
                tempPos_PYRs.Add(currentTransform.rotation);

                txtPos.text += counter + " of " + ((int)durationSeconds / intervalSeconds) + " \n\n";
                txtPos.text += "Last: " + stringToRecord + " \n";

                averageBTransform.position = GetAverageFromList(tempPos_XYZs);
                averageBTransform.rotation = GetAverageQuaternion(tempPos_PYRs.ToArray());

                txtPos.text += "Mean:" + TransformToEulerString(averageBTransform);
            }

            Debug.Log(" test6 ");


            logReading("allCaptures", stringToRecord);
            yield return new WaitForSeconds(intervalSeconds); // Wait for interval (0.1) seconds
            elapsedTime += intervalSeconds; // Increment elapsed time by the interval
            counter++;
        }

        Debug.Log(durationSeconds + " seconds have passed. Stopping execution.");
    }

    // VivePerCamera
    private IEnumerator hs_RepeatingLog_ABC()
    {
        float durationSeconds = 5f;
        float intervalSeconds = 0.1f;
        float elapsedTime = 0f;
        int counter = 0;

        if (hs_tracker0 == null || hs_tracker1 == null || hs_tracker2 == null)
        {
            Debug.LogError("One or more trackers are null.");
            yield break;
        }

        while (elapsedTime < durationSeconds)
        {
            // TRACKER A
            Quaternion rotA = SwapQuarternionYAndZ(hs_tracker0.rotation);

            tempRef_XYZs.Add(hs_tracker0.position);
            tempRef_PYRs.Add(rotA);
            averageATransform.position = GetAverageFromList(tempRef_XYZs);
            averageATransform.rotation = GetAverageQuaternion(tempRef_PYRs.ToArray());

            // TRACKER B
            Quaternion rotB = SwapQuarternionYAndZ(hs_tracker1.rotation);

            tempPos_XYZs.Add(hs_tracker1.position);
            tempPos_PYRs.Add(rotB);
            averageBTransform.position = GetAverageFromList(tempPos_XYZs);
            averageBTransform.rotation = GetAverageQuaternion(tempPos_PYRs.ToArray());

            // TRACKER C
            Quaternion rotC = SwapQuarternionYAndZ(hs_tracker2.rotation);

            tempC_XYZs.Add(hs_tracker2.position);
            tempC_PYRs.Add(rotC);
            averageCTransform.position = GetAverageFromList(tempC_XYZs);
            averageCTransform.rotation = GetAverageQuaternion(tempC_PYRs.ToArray());

            // Log line
            string logLine = $"{counter},{Time.realtimeSinceStartup}," +
                             $"A:{TransformToEulerString(averageATransform)}," +
                             $"B:{TransformToEulerString(averageBTransform)}," +
                             $"C:{TransformToEulerString(averageCTransform)}";

            logReading("abcTracking", logLine);

            yield return new WaitForSeconds(intervalSeconds);
            elapsedTime += intervalSeconds;
            counter++;
        }

        Debug.Log("ABC tracking finished.");

        txtRef.text += "\nFinal Average A:\n" + TransformToEulerString(averageATransform);
        txtPos.text += "\nFinal Average B:\n" + TransformToEulerString(averageBTransform);
        txtC.text += "\nFinal Average C:\n" + TransformToEulerString(averageCTransform);
    }


    public void CaptureReference()
    {

        Debug.Log("Selected Calibration Type: " + CalibrationTypeName);

        if (CalibrationTypeName == CalibrationTypesList.OneVive_NotReady || CalibrationTypeName == CalibrationTypesList.TwoVives)

        {
            StartCoroutine(hs_RepeatingLog(false, flag_switchtrackers_ref_pos));
        }

        else if (CalibrationTypeName == CalibrationTypesList.VivePerCamera)
        {
            StartCoroutine(hs_RepeatingLog_ABC());
        }


    }

    Quaternion SwapQuarternionYAndZ(Quaternion input)
    {
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(input);

        Vector3 originalUp = rotationMatrix.GetColumn(1); // Y-axis
        Vector3 originalForward = rotationMatrix.GetColumn(2); // Z-axis

        rotationMatrix.SetColumn(1, originalForward); // Set Y-axis to Z-axis
        rotationMatrix.SetColumn(2, originalUp);     // Set Z-axis to Y-axis

        return Quaternion.LookRotation(rotationMatrix.GetColumn(2), rotationMatrix.GetColumn(1));
    }

    public void CapturePosition()
    {

        Debug.Log("Selected Calibration Type: " + CalibrationTypeName);

        if (CalibrationTypeName == CalibrationTypesList.OneVive_NotReady || CalibrationTypeName == CalibrationTypesList.TwoVives)

        {
            StartCoroutine(hs_RepeatingLog(true, flag_switchtrackers_ref_pos));
        }

        else if (CalibrationTypeName == CalibrationTypesList.VivePerCamera)
        {
            StartCoroutine(hs_RepeatingLog_ABC());
        }

    }

    public void logReading(string fileName, string reading)
    {
        // Save to file
        string filePath = UnityEngine.Application.dataPath;
        writer = new StreamWriter(filePath + "/Data/" + fileName + ".csv", true);

        writer.WriteLine(reading);
        writer.Close();
    }


    // Helper function to get Transform from a tracked device
    private Transform FindTrackerTransform(UnityEngine.XR.InputDevice device)
    {
        GameObject trackerObject = GameObject.Find(device.name);
        return trackerObject ? trackerObject.transform : null;
    }
    public void SaveGlobal(GameObject obj, bool flag_pos_cam)
    {
        if (obj == null)
        {
            UnityEngine.Debug.LogWarning("Target object is null!");
            return;
        }

        // Ensure session header is written before any position data
        if (!sessionStarted)
        {
            string sessionHeader = $"\nSESSION: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            File.AppendAllText(path_pos, sessionHeader);
            File.AppendAllText(path_cam, sessionHeader);
            sessionStarted = true;
            Debug.Log("HS_SaveTransformData: Session header written (inside SaveGlobal).");
        }

        // Save the original parent before unparenting
        Transform originalParent = obj.transform.parent;

        // Step 1: Get Global Transform (before unparenting)
        Vector3 worldPosition = obj.transform.position;
        Quaternion worldRotation = obj.transform.rotation;

        // Step 2: Temporarily Unparent to capture independent transform
        obj.transform.SetParent(null, true);

        // Step 3: Capture Global Transform after unparenting
        Vector3 detachedPosition = obj.transform.position;
        Quaternion detachedRotation = obj.transform.rotation;

        // Step 4: Restore the Parent
        obj.transform.SetParent(originalParent, true);

        if (!flag_pos_cam)
        {
            hs_number_pos++;

            // Format position and rotation into a single line
            string data_pos = $"{hs_number_pos} " +
                              $"{hsVector3ToString(worldPosition)} " +
                              $"{hsVector3ToString(worldRotation.eulerAngles)}\n";

            Debug.Log("path_pos" + path_pos);
            Debug.Log("data_pos" + data_pos);

            // Append the data instead of overwriting
            File.AppendAllText(path_pos, data_pos);
            savedTransformData_ref.Add(data_pos);
            Debug.Log("Number of elements in savedTransformData_ref: " + savedTransformData_ref.Count);

        }
        else if (flag_pos_cam)
        {
            hs_number_cam++;

            // Format position and rotation into a single line
            string data_cam = $"{hs_number_cam}/{hs_number_pos} " +
                              $"{hsVector3ToString(worldPosition)} " +
                              $"{hsVector3ToString(worldRotation.eulerAngles)}\n";

            // Append the data instead of overwriting
            File.AppendAllText(path_cam, data_cam);
            savedTransformData_pos.Add(data_cam);
            Debug.Log("Number of elements in savedTransformData_pos: " + savedTransformData_pos.Count);
        }

        UnityEngine.Debug.Log($"Transform data saved for {obj.name} at: {(flag_pos_cam ? path_cam : path_pos)}");
    }

    public void SavePosition()
    {
        Debug.Log("Commit clicked");


       if (CalibrationTypeName == CalibrationTypesList.OneVive_NotReady || CalibrationTypeName == CalibrationTypesList.TwoVives)
          {
            SavePositionTwoVives();
        }
           
        else if (CalibrationTypeName == CalibrationTypesList.VivePerCamera)
        {
            SavePositionOnePerCamera();
        }


    }

    public void SavePositionTwoVives()

    {
        // if a or b is 0, need to take at least one measure for each

        if (hs_count_trackers > 1)
        {
            flag_switchtrackers_ref_pos = flag_switchtrackers_ref_pos ? false : true;
        }

        if (tempRef_XYZs.Count == 0 ^ tempPos_XYZs.Count == 0)
        {
            Debug.Log("Need a measurement for each point");
            return;
        }

        averageATransform.position = GetAverageFromList(tempRef_XYZs);
        averageATransform.rotation = GetAverageQuaternion(tempRef_PYRs.ToArray());
        //GameObject AArrow = Instantiate(arrowPrefab, averageATransform.position, averageATransform.rotation);

        averageBTransform.position = GetAverageFromList(tempPos_XYZs);
        averageBTransform.rotation = GetAverageQuaternion(tempPos_PYRs.ToArray());
        //GameObject BArrow = Instantiate(arrowPrefab, averageBTransform.position, averageBTransform.rotation); 

        Vector3 relativePosition = averageATransform.InverseTransformPoint(averageBTransform.position);
        Quaternion relativeRotation = Quaternion.Inverse(averageATransform.rotation) * averageBTransform.rotation;


        if (lastSensor == null)
        {
            lastSensor = (Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity));
            lastSensor.transform.name = "hs_position";
            SensorList.Add(lastSensor);
            dataSetCount++;
        }

        GameObject newSensor = Instantiate(arrowPrefab, lastSensor.transform);
        newSensor.transform.SetLocalPositionAndRotation(relativePosition, relativeRotation);

        lastSensor = newSensor;
        SensorList.Add(newSensor);

        string textToWrite = dataSetCount + "," + sensorCount + "," +
            Mathf.RoundToInt(Time.realtimeSinceStartup * 1000) + ",localPosition," +
            Vector3ToString(relativePosition) + ",localEuler," +
            Vector3ToString(relativeRotation.eulerAngles) + ",localQuaternion" +
            QuaternionToString(relativeRotation) + ",globalPosition," +
            Vector3ToString(newSensor.transform.position) + ",globalEuler," +
            Vector3ToString(newSensor.transform.eulerAngles) + ",globalQuaternion," +
            QuaternionToString(newSensor.transform.rotation);

        sensorCount++;

        txtSaved.text += textToWrite;
        Debug.Log(textToWrite);
        Debug.Log(relativePosition.magnitude);

        logReading("goodData", textToWrite);


        SaveGlobal(newSensor, false);


        MoveLighthouse();
    }



    public void SavePositionOnePerCamera()
    {
       

        if (hs_count_trackers < 3)
        {
            Debug.LogWarning("You need at least 3 trackers to save A, B, and C.");
            return;
        }

        // Compute averages
        averageATransform.position = GetAverageFromList(tempRef_XYZs);
        averageATransform.rotation = GetAverageQuaternion(tempRef_PYRs.ToArray());

        averageBTransform.position = GetAverageFromList(tempPos_XYZs);
        averageBTransform.rotation = GetAverageQuaternion(tempPos_PYRs.ToArray());

        averageCTransform.position = GetAverageFromList(tempC_XYZs);
        averageCTransform.rotation = GetAverageQuaternion(tempC_PYRs.ToArray());

        // Create anchor if needed
        if (lastSensor == null)
        {
            lastSensor = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
            lastSensor.name = "hs_position";
            SensorList.Add(lastSensor);
            dataSetCount++;
        }

        // Create arrows
        GameObject arrowA = Instantiate(arrowPrefab, averageATransform.position, averageATransform.rotation, lastSensor.transform);
        arrowA.name = "Arrow_A_" + sensorCount;
        SensorList.Add(arrowA);

        GameObject arrowB = Instantiate(arrowPrefab, averageBTransform.position, averageBTransform.rotation, lastSensor.transform);
        arrowB.name = "Arrow_B_" + sensorCount;
        SensorList.Add(arrowB);

        GameObject arrowC = Instantiate(arrowPrefab, averageCTransform.position, averageCTransform.rotation, lastSensor.transform);
        arrowC.name = "Arrow_C_" + sensorCount;
        SensorList.Add(arrowC);

        // Save to custom file
        string path_custom = Path.Combine(Application.persistentDataPath, "transformData_trackers_banzai.txt");
        if (!File.Exists(path_custom)) File.WriteAllText(path_custom, "");

        string lineA = $"A {sensorCount} {hsVector3ToString(averageATransform.position)} {hsVector3ToString(averageATransform.rotation.eulerAngles)}\n";
        string lineB = $"B {sensorCount} {hsVector3ToString(averageBTransform.position)} {hsVector3ToString(averageBTransform.rotation.eulerAngles)}\n";
        string lineC = $"C {sensorCount} {hsVector3ToString(averageCTransform.position)} {hsVector3ToString(averageCTransform.rotation.eulerAngles)}\n";

        File.AppendAllText(path_custom, lineA);
        File.AppendAllText(path_custom, lineB);
        File.AppendAllText(path_custom, lineC);

        Debug.Log("Saved A, B, C to transformData_trackers_banzai.txt");

        // Optional logging to screen
        txtSaved.text += $"Saved A/B/C #{sensorCount} to custom file\n";
        sensorCount++;
        MoveLighthouse();
    }


    public void MoveLighthouse()
    {
        Debug.Log("Clear Button clicked");
        txtRef.text = "";
        txtPos.text = "";
        tempRef_XYZs.Clear();
        tempPos_XYZs.Clear();
        tempRef_PYRs.Clear();
        tempPos_PYRs.Clear();
        tempC_XYZs.Clear();
        tempC_PYRs.Clear();
    }

 

    public void SaveAsCamera()
    {
        string clipboardText = "";

        for (int i = 0; i < relativeTransforms.Count; i++)
        {
            clipboardText = TransformToEulerString(relativeTransforms[i]);
        } 

        // Copy the formatted string to the system clipboard
        GUIUtility.systemCopyBuffer = clipboardText;

        // Save to file
        string filePath = UnityEngine.Application.dataPath;
        writer = new StreamWriter(filePath + "/Data/" + "saved_XYZs.csv", true);
        clipboardText = Mathf.RoundToInt(Time.realtimeSinceStartup * 1000) + "," + clipboardText;
        writer.WriteLine(clipboardText);
        writer.Close();


        SaveGlobal(lastSensor,true);
    }

    private string TransformToEulerString (Transform toConvert)
    {
        Vector3 posToConvert = toConvert.position;
        Vector3 rotationToConvert = toConvert.rotation.eulerAngles;
        string outText = "Vector3(" +
            Math.Round(posToConvert.x, 4).ToString("F4") + ',' +
            Math.Round(posToConvert.y, 4).ToString("F4") + ',' +
            Math.Round(posToConvert.z, 4).ToString("F4") +
            ") Vector3(" +
            Math.Round(rotationToConvert.x, 4).ToString("F4") + ',' +
            Math.Round(rotationToConvert.y, 4).ToString("F4") + ',' +
            Math.Round(rotationToConvert.z, 4).ToString("F4") + ")";
        return outText;
    }

    private string Vector3ToString(Vector3 toConvert)
    {
        string outText = "Vector3(" +
            Math.Round(toConvert.x, 4).ToString("F4") + ',' +
            Math.Round(toConvert.y, 4).ToString("F4") + ',' +
            Math.Round(toConvert.z, 4).ToString("F4") + ")";
        return outText;
    }

    private string QuaternionToString(Quaternion toConvert)
    {
        string outText = "Quaternion(" +
            Math.Round(toConvert.x, 4).ToString("F4") + ',' +
            Math.Round(toConvert.y, 4).ToString("F4") + ',' +
            Math.Round(toConvert.z, 4).ToString("F4") + ',' +
            Math.Round(toConvert.w, 4).ToString("F4") + ")";
        return outText;
    }

    private Quaternion GetAverageQuaternion(Quaternion[] quats)
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

    // Get an average from more then two quaternions
    // only works if all the quaternions are relatively close together.
    private void AverageQuaternion_Internal(ref Vector4 cumulative, Quaternion newRotation, Quaternion firstRotation)
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
    private Quaternion InverseSignQuaternion(Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, -q.z, -q.w);
    }

    //Returns true if the two input quaternions are close to each other. This can
    //be used to check whether or not one of two quaternions which are supposed to
    //be very similar but has its component signs reversed (q has the same rotation as -q)
    private bool AreQuaternionsClose(Quaternion q1, Quaternion q2)
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
    private Vector3 GetAverageFromList(List<Vector3> listToAverage)
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


    public void SaveCG0()
    {
        // Check that there is at least one saved entry.
        if (savedTransformData_pos == null || savedTransformData_pos.Count == 0)
        {
            UnityEngine.Debug.LogWarning("No saved transform data to process.");
            return;
        }

        // Use the last entry as the new origin.
        string lastEntry = savedTransformData_pos[savedTransformData_pos.Count - 1];
        // Assume format: "index posX posY posZ rotX rotY rotZ"
        string[] tokens = lastEntry.Trim().Split(' ');
        if (tokens.Length < 7) // Needs at least index + 3 position + 3 rotation values
        {
            UnityEngine.Debug.LogError("Last entry is not in the expected format.");
            return;
        }

        // Extract reference position and rotation from the last entry
        Vector3 referencePosition = new Vector3(
            //float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3])
            float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3])
        );

        Vector3 referenceEuler = new Vector3(
            float.Parse(tokens[4]), float.Parse(tokens[5]), float.Parse(tokens[6])
           //  0f, float.Parse(tokens[5]), 0f  //hs test!
        );



        Quaternion referenceRotation = Quaternion.Euler(referenceEuler);

        Debug.Log($"Reference Position: {referencePosition}, Reference Rotation: {referenceRotation.eulerAngles}");


        //convert room origin position:
        // Extract position and rotation safely
        Vector3 room_origin_pos = new Vector3(0f, 0.0f, 0f);
        Vector3 room_origin_euler = new Vector3(0f, 0f, 0f);

        Quaternion room_origin_Rotation = Quaternion.Euler(room_origin_euler);

        Debug.Log($"Processed Entry - Pos: {room_origin_pos}, Euler: {room_origin_euler}, Rot: {room_origin_Rotation}");

        // Compute relative transformation.
        Vector3 room_origin_relativePosition = Quaternion.Inverse(referenceRotation) * (room_origin_pos - referencePosition);
        Quaternion room_origin_relativeRotation = Quaternion.Inverse(referenceRotation) * room_origin_Rotation;

        // Format the relative transform as a string.
        string room_origin_relativeData = $"ROOM0 " + // ROOM 0refers to that phisical location, 'never' changes 
                                          $"{hsVector3ToString(room_origin_relativePosition)} " +
                                          $"{hsVector3ToString(room_origin_relativeRotation.eulerAngles)}\n";

        File.AppendAllText(path_cg0, room_origin_relativeData);


        // Loop through all saved transform entries.
        for (int i = 0; i < savedTransformData_pos.Count - 1; i++) // Up to pre-last element
        {
            string entry = savedTransformData_pos[i];
            string[] parts = entry.Trim().Split(' ');

            if (parts.Length < 7) // Ensure there's enough data
            {
                UnityEngine.Debug.LogWarning("Skipping improperly formatted entry: " + entry);
                continue;
            }

            // Extract position and rotation safely
            Vector3 currentPosition = new Vector3(
                float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3])
            );
            Vector3 currentEuler = new Vector3(
                float.Parse(parts[4]), float.Parse(parts[5]), float.Parse(parts[6])
            );
            Quaternion currentRotation = Quaternion.Euler(currentEuler);

            Debug.Log($"Processed Entry {i} - Pos: {currentPosition}, Euler: {currentEuler}, Rot: {currentRotation}");

            GameObject justBeforeFinalSaveXYZ = Instantiate(arrowPrefab);
            justBeforeFinalSaveXYZ.name = "justBeforeFinalSaveXYZ" + 4;
            justBeforeFinalSaveXYZ.transform.SetParent(null);
            justBeforeFinalSaveXYZ.transform.SetPositionAndRotation(currentPosition, currentRotation);

            // Compute relative transformation.
            Vector3 relativePosition = Quaternion.Inverse(referenceRotation) * (currentPosition - referencePosition);
            Quaternion relativeRotation = Quaternion.Inverse(referenceRotation) * currentRotation;

            // Format the relative transform as a string.
            string relativeData = $"{parts[0]} " +
                                  $"{hsVector3ToString(relativePosition)} " +
                                  $"{hsVector3ToString(relativeRotation.eulerAngles)}\n";


            GameObject FinalSaveXYZ = Instantiate(arrowPrefab);
            FinalSaveXYZ.name = "FinalSaveXYZ" + 5;
            FinalSaveXYZ.transform.SetParent(null);
            FinalSaveXYZ.transform.SetPositionAndRotation(relativePosition, relativeRotation);

            // Append the converted data to the target file.
            File.AppendAllText(path_cg0, relativeData);
        }


        UnityEngine.Debug.Log("Converted saved transform data has been appended to file: " + path_cg0);


        //convert room position:
        // Extract position and rotation safely
        Vector3 room_currentPosition = new Vector3(room_pos_x, room_pos_y, room_pos_z);
        Vector3 room_currentEuler = new Vector3 (room_rot_x, room_rot_y, room_rot_z);
       
        Quaternion room_currentRotation = Quaternion.Euler(room_currentEuler);

        Debug.Log($"Processed Entry - Pos: {room_currentPosition}, Euler: {room_currentEuler}, Rot: {room_currentRotation}");

        // Compute relative transformation.
        Vector3 room_relativePosition = Quaternion.Inverse(referenceRotation) * (room_currentPosition - referencePosition);
        Quaternion room_relativeRotation = Quaternion.Inverse(referenceRotation) * room_currentRotation;

        // Format the relative transform as a string.
        string room_relativeData = $"ROOMLOCATION " +  // refers to the CG activity origin, changes for different activities within the same room
                                   $"{hsVector3ToString(room_relativePosition)} " +
                                   $"{hsVector3ToString(room_relativeRotation.eulerAngles)}\n";

        File.AppendAllText(path_cg0, room_relativeData);
    
    }

    public void NewSession()
    {
        Debug.Log("Reset Button clicked");
        relativeTransforms.Clear();
        txtSaved.text = "";
        sensorCount = 0;
        lastSensor = null;
        sessionStarted = false;
        MoveLighthouse();
        Start();
    }

}


