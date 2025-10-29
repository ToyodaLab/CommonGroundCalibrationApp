using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CameraCalibrationUIComponent : MonoBehaviour
{
    // Seems faster to just set once in prefab
    public GameObject imageViveTrackerBlue;
    public GameObject imageViveTrackerRed;
    public GameObject imageStart;
    public GameObject imageCamera;
    public GameObject imageJump;
    public GameObject imageEnd;

    public GameObject buttons;

    public TextMeshProUGUI textDistance;

    public GameObject thisPrefab;

    public TMP_Dropdown sensorIDDropdown;

    // external so found in code
    private CameraCalibration _cameraCalibration;

    // Start is called before the first frame update
    void Start()
    {
        _cameraCalibration = GetComponentInParent<CameraCalibration>();
        _cameraCalibration.SnapToRight();

        // Populate known sensors IDs
        List<string> knownSensorIDs = new List<string>();

        knownSensorIDs.Add("SensorID");
        knownSensorIDs.Add("CL8LC4200P5");
        knownSensorIDs.Add("CL8E751009N");
        knownSensorIDs.Add("CL8LC42003X");
        knownSensorIDs.Add("CL8LC4200W9");
        knownSensorIDs.Add("CL8LC42002E");
        knownSensorIDs.Add("CL8E75100DB");
        knownSensorIDs.Add("CL8E751001J");
        knownSensorIDs.Add("CompleteLater");

        sensorIDDropdown.AddOptions(knownSensorIDs);
        sensorIDDropdown.value = 0; 

    }

    void updateButtons(bool showButtons)
    {
        buttons.SetActive(showButtons);
        if (!showButtons)
        {
            textDistance.gameObject.SetActive(true);
            setDistance(Random.Range(0.0f, 10.0f));
        }
    }

    public void onNextIsCamera()
    {
        StartCoroutine(WaitForCalibrationProcess(StepTypes.Camera));
    }

    public void onNextIsJump()
    {
        StartCoroutine(WaitForCalibrationProcess(StepTypes.Jump));
    }

    public void onNextIsFinish()
    {
        StartCoroutine(WaitForCalibrationProcess(StepTypes.End));
    }

    private IEnumerator WaitForCalibrationProcess(StepTypes step)
    {
        updateButtons(false);
        
        // Wait for the calibration process to finish
        yield return StartCoroutine(
            _cameraCalibration.TimedPerformCalibrationProcess(
                step,
                imageViveTrackerRed.activeSelf, textDistance,
                sensorIDDropdown.options[sensorIDDropdown.value].text
                )
            );

        switch(step)
        {
            case StepTypes.Camera:
                imageCamera.SetActive(true);
                newMe();
                break;
            case StepTypes.Jump:
                imageJump.SetActive(true);
                newMe();
                break;
            case StepTypes.End:
                imageEnd.SetActive(true);
                break;
        }
    }
    void resetVisuals(bool isRedTracker)
    {
        Debug.Log("Now move the " + (isRedTracker ? "Red Tracker" : "Blue Tracker"));
        imageViveTrackerBlue.SetActive(!isRedTracker);
        imageViveTrackerRed.SetActive(isRedTracker);
        imageStart.SetActive(false);
        imageCamera.SetActive(false);
        imageJump.SetActive(false);
        imageEnd.SetActive(false);
        textDistance.gameObject.SetActive(false);

        updateButtons(true);
    }
    void newMe()
    {
        Transform newMe = Instantiate(thisPrefab.transform, this.transform.parent);
        newMe.GetComponent<CameraCalibrationUIComponent>().resetVisuals(!imageViveTrackerRed.activeSelf);
        _cameraCalibration.SnapToRight();
    }

    public void setDistance(float distance)
    {
        setDistance(distance.ToString("F2") + "m");
    }

    public void setDistance(string newText)
    {
        textDistance.text = newText;
    }

}
