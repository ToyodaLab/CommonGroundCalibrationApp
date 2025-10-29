using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ViveTrackers;
using System.IO;
using System;
using System.Linq;
using UnityEngine.UI;
using UnityEditor;
using UnityEngine.UIElements;
using Unity.VisualScripting;

public enum StepTypes { Camera, Jump, Start, End };


public class CameraCalibration : MonoBehaviour
{
    // Set in scene
    public ViveTrackersManager trackersManager;
    public GameObject arrowPrefab;
    public ScrollRect horizontalScrollbar;
    public GameObject UIWindow;
    public TMP_InputField roomSelection;
    public TMP_InputField timeToMeasure;
    public TMP_Text debugText;

    // Tracker references
    private static List<ViveTracker> connectedTrackers = new List<ViveTracker>();
    private static Transform redTracker = null;
    private static Transform blueTracker = null;

    // Temporary variables for smoothing
    private List<Vector3> tempRef_XYZs = new();
    private List<Vector3> tempPos_XYZs = new();
    private List<Quaternion> tempRef_PYRs = new();
    private List<Quaternion> tempPos_PYRs = new(); 
    private Transform averageATransform;
    private Transform averageBTransform;

    // Metadata
    public List<GameObject> CapturesList = new List<GameObject>(); // public for visibility
    private GameObject lastCapture = null;
    private List<StepTypes> stepOrder = new List<StepTypes>();

    private void Start()
    {
        // flip the active state
        UIWindow.SetActive(false);
        StartCoroutine(WaitAndStart(1f));
    }

    private IEnumerator WaitAndStart(float waitTime)
    {
        // Wait for n seconds then start
        yield return new WaitForSeconds(waitTime);
        StartApplication();
    }

    private void StartApplication()
    {

        // Find the tracker manager
        trackersManager = FindObjectOfType<ViveTrackersManager>();

        if (trackersManager == null)
        {
            Debug.LogError("ViveTrackersManager instance not found. Restart application.");
            return;
        }

        // Refresh and retrieve tracker list
        trackersManager.RefreshTrackers();
        connectedTrackers = trackersManager.hs_GetTrackedDevices();
        

        // Sort trackers by name
        if (connectedTrackers.Count > 0)
        {
            Debug.Log($"Sorting {connectedTrackers.Count} devices");
            connectedTrackers.Sort((a, b) => a.name.CompareTo(b.name));
        }
        else
        {
            Debug.LogError("No trackers found. Restart application.");
            return;
        }

        // Assign tracker references
        redTracker = connectedTrackers[0].GetComponentInChildren<Camera>().transform;
        redTracker.name = connectedTrackers[0].ID.trackedDevice_SerialNumber;
        blueTracker = connectedTrackers[1].GetComponentInChildren<Camera>().transform;
        blueTracker.name = connectedTrackers[1].ID.trackedDevice_SerialNumber;
        FindInactiveChildByName(connectedTrackers[0].transform, "Red").SetActive(true);
        FindInactiveChildByName(connectedTrackers[1].transform, "Blue").SetActive(true);

        if (redTracker == null || blueTracker == null || connectedTrackers.Count != 2)
        {
            Debug.LogError("ERROR: Need exactly two trackers. Restart App"); 
            return;
        }

        GameObject objA = new GameObject("TempA");
        averageATransform = objA.transform;
        GameObject objB = new GameObject("TempB");
        averageBTransform = objB.transform;

    }

    public void PerformCalibrationProcess(StepTypes thisStepType, bool IsRedTrackerReference, TextMeshProUGUI distanceText, string sensorID)
    {
        // Requires IEnumerator to block whilst processing the tracker
        StartCoroutine(TimedPerformCalibrationProcess(thisStepType, IsRedTrackerReference, distanceText, sensorID));
    }

    public IEnumerator TimedPerformCalibrationProcess(StepTypes thisStepType, bool IsMovingRedTracker, TextMeshProUGUI distanceText, string sensorID)
    {
        // Is red or blue the reference (first) tracker
        Transform referenceTracker = IsMovingRedTracker ? blueTracker : redTracker;
        Transform nextTracker = IsMovingRedTracker ? redTracker : blueTracker;

        // Create average transforms for both camera positions in relative space
        yield return CaptureAverageTransformOverTime(referenceTracker, nextTracker, distanceText, sensorID);

        // This will wait until coroutine above is finished
        stepOrder.Add(thisStepType);

        // Are we finished? If so, write the calibration file
        if (thisStepType == StepTypes.End)
        {
            WriteCalibrationFile();
        }
    }

    private IEnumerator CaptureAverageTransformOverTime(Transform referenceTransform, Transform nextTransform, TextMeshProUGUI countdownText, string sensorID)
    {
        if(referenceTransform == null || nextTransform == null)
        {
            Debug.LogError("Reference or next transform is null. Cannot capture transforms.");
            yield break;
        }

        // Set measure duration time (can change between steps)
        float durationSeconds = 3f;
        // if cannot parse the input, then use default value
        if (float.TryParse(timeToMeasure.text, out durationSeconds) == false)
        {
            Debug.LogWarning("Invalid float input: " + timeToMeasure.text);
            durationSeconds = 3f;
        }

        // Internal variables for the capture process
        float intervalSeconds = 0.1f;
        float elapsed = 0f;
        float intervalElapsed = 0f;

        while (elapsed < durationSeconds)
        {
            yield return null; // Wait for next frame

            float dt = Time.deltaTime;
            elapsed += dt;
            intervalElapsed += dt;

            if (intervalElapsed >= intervalSeconds)
            {
                // Save the current position and rotation of both cameras to a temp list
                tempRef_XYZs.Add(referenceTransform.position);
                tempRef_PYRs.Add(referenceTransform.rotation);
                tempPos_XYZs.Add(nextTransform.position);
                tempPos_PYRs.Add(nextTransform.rotation);

                float secondsLeft = durationSeconds - elapsed;
                countdownText.text = secondsLeft.ToString("F1") + "s";

                intervalElapsed = 0f;
            }
        }

        Debug.Log(durationSeconds + " seconds have passed. Stopping capture.");

        averageATransform.position = UtilityScripts.GetAverageFromList(tempRef_XYZs);
        averageATransform.rotation = UtilityScripts.GetAverageQuaternion(tempRef_PYRs.ToArray());
        averageBTransform.position = UtilityScripts.GetAverageFromList(tempPos_XYZs);
        averageBTransform.rotation = UtilityScripts.GetAverageQuaternion(tempPos_PYRs.ToArray());

        // Get relative position from average referenceTransform to average nextTransform
        Vector3 relativePosition = averageATransform.InverseTransformPoint(averageBTransform.position);
        Quaternion relativeRotation = Quaternion.Inverse(averageATransform.rotation) * averageBTransform.rotation;

        // Update text with illusion delay to give rough distance reading
        yield return new WaitForSeconds(0.1f);
        countdownText.text = Vector3.Distance(averageATransform.position, averageBTransform.position).ToString("F2") + "m";

        // Empty lists after saving averages
        tempRef_XYZs.Clear(); tempRef_PYRs.Clear(); tempPos_XYZs.Clear(); tempPos_PYRs.Clear();

        // If no sensors exist yet, then first is the origin and is relative to the world
        if (lastCapture == null)
        {
            lastCapture = (Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity));
            lastCapture.transform.name = "Sensor_0";
            CapturesList.Add(lastCapture); 
            stepOrder.Add(StepTypes.Start);
        }

        GameObject newSensor = Instantiate(arrowPrefab, lastCapture.transform);

        newSensor.transform.SetLocalPositionAndRotation(relativePosition, relativeRotation);
        newSensor.name = "Sensor_" + CapturesList.Count + "_" + sensorID;

        lastCapture = newSensor;
        CapturesList.Add(newSensor);
    }

    private void WriteCalibrationFile()
    {
        // First file overwrites the most recent capture
        // Second file is a log of all captures for this room
        if (!Directory.Exists("C:\\CommonGround\\CalibrationFiles"))
        {
            Directory.CreateDirectory("C:\\CommonGround\\CalibrationFiles");
        }

        string roomFilePathLatest = Path.Combine("C:\\CommonGround\\CalibrationFiles\\", roomSelection.text + ".txt");
        string roomFilePathArchive = Path.Combine("C:\\CommonGround\\CalibrationFiles\\", roomSelection.text + "_archive.txt");

        Debug.Log(roomFilePathArchive);

        if (!File.Exists(roomFilePathArchive)) File.WriteAllText(roomFilePathArchive, "");
        File.WriteAllText(roomFilePathLatest, "");

        // Always write the header
        string stringToWrite = $"{roomSelection.text},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
        File.AppendAllText(roomFilePathArchive, stringToWrite);
        File.AppendAllText(roomFilePathLatest, stringToWrite);

        //  loop through captures to write contents
        for (int i = 0; i <= CapturesList.Count - 1; i++)
        {
            // Only add cameras to the save file
            if (stepOrder[i] == StepTypes.Camera)
            {
                string[] splitName = CapturesList[i].name.Split('_');
                if (splitName[2] == "SensorID" ^ splitName[2] == "CompleteLater"){
                    debugText.text = "Don't forget to set IDs manually in text file";
                }

                string cameraData = splitName[2] 
                    + "," 
                    + TransformToCsv(CapturesList[i].transform.position, CapturesList[i].transform.rotation) 
                    + "\n";

                File.AppendAllText(roomFilePathArchive, cameraData);
                File.AppendAllText(roomFilePathLatest, cameraData);
            }
        }
        File.AppendAllText(roomFilePathArchive, "\n");
        File.AppendAllText(roomFilePathLatest, "\n");
    }

    string TransformToCsv(Vector3 pos, Quaternion rot)
    {
        return $"{pos.x:F4},{pos.y:F4},{pos.z:F4},{rot.x:F4},{rot.y:F4},{rot.z:F4},{rot.w:F4}";
    }

    GameObject FindInactiveChildByName(Transform parent, string targetName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true)) // `true` includes inactive
        {
            if (child.name == targetName)
            {
                return child.gameObject;
            }
        }
        return null;
    }

    public void SnapToRight()
    {
        Canvas.ForceUpdateCanvases();
        horizontalScrollbar.horizontalNormalizedPosition = 1f;
    }

    public void UIToggle()
    {
        // flip the active state
        UIWindow.SetActive(!UIWindow.activeSelf);
    }

    public void RoomSelected()
    {
        if (roomSelection.text != "")
        {
            // A room was selected, allow the app to continue
            UIWindow.SetActive(true);
        } else { 
            UIWindow.SetActive(false);
        }
    }
}


