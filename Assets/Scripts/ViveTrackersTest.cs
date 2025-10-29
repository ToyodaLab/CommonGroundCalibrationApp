/******************************************************************************************************************************************************
* MIT License																																		  *
*																																					  *
* Copyright (c) 2020																																  *
* Emmanuel Badier <emmanuel.badier@gmail.com>																										  *
* 																																					  *
* Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),  *
* to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,  *
* and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:		  *
* 																																					  *
* The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.					  *
* 																																					  *
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, *
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 																							  *
* IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 		  *
* TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.							  *
******************************************************************************************************************************************************/

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ViveTrackers
{
	public sealed class ViveTrackersTest : MonoBehaviour
	{
		public bool useFake = false;
		public ViveTrackersManager viveTrackersManager;
		//public ViveTrackersManagerFake viveTrackersManagerFake;

		private ViveTrackersManagerBase _activeViveTrackersManager;
		private Transform _mainCameraTransform = null;
		private bool _debugActive = true;

		//public Transform originOffset;
		public int sessionCounter = 1;
		private StreamWriter writer;


        private void Start()
		{
			_mainCameraTransform = Camera.main.transform;
			if(useFake)
			{
				//_activeViveTrackersManager = viveTrackersManagerFake;
				viveTrackersManager.gameObject.SetActive(false);
			}
			else
			{
				_activeViveTrackersManager = viveTrackersManager;
				//viveTrackersManagerFake.gameObject.SetActive(false);
			}
			_activeViveTrackersManager.origin.Init("O", Color.white, _mainCameraTransform);
			_activeViveTrackersManager.TrackersFound += _OnTrackersFound;
			_activeViveTrackersManager.RefreshTrackers();

        }

		private void Update()
		{
			_activeViveTrackersManager.UpdateTrackers(Time.unscaledDeltaTime);

			if (Input.GetKeyUp(KeyCode.F1))
			{
				_debugActive = !_debugActive;
				_activeViveTrackersManager.SetDebugActive(_debugActive);
			}
			else if (Input.GetKeyUp(KeyCode.F5))
			{
				_activeViveTrackersManager.RefreshTrackers();
			}
			else if(Input.GetKeyUp(KeyCode.F6))
			{
				_activeViveTrackersManager.SaveTrackersCalibrations();
			}
			else if (Input.GetKeyUp(KeyCode.F7))
			{
				_activeViveTrackersManager.LoadTrackersCalibrations();
			}
			else if (Input.GetKeyUp(KeyCode.F8))
			{
				_activeViveTrackersManager.CalibrateTrackers();
			}
			// GET 0 Tracker position
			else if(Input.GetKeyUp(KeyCode.Space))
            {
                _getTrackerTransform(sessionCounter.ToString());
				sessionCounter++;
            }
            // GET 0 Tracker position
            else if (Input.GetMouseButtonDown(2))
            {
                _getTrackerTransform(sessionCounter.ToString());
                sessionCounter++;
            }
            // SET ORIGIN
            else if (Input.GetKeyUp(KeyCode.A))
            {
				//originOffset = _getTrackerTransform("origin");
				sessionCounter = 1;
            }
            // quit app
            else if (Input.GetKeyUp(KeyCode.Escape))
			{
                Application.Quit();
			}
        }

		private Transform _getTrackerTransform(string identifier)
		{
            //save current position in 3D space to Log
            Transform currentTrackerPosition = viveTrackersManager.getFirstTrackerTransform();

			string toWrite =
                identifier + "," +
                currentTrackerPosition.position.x + "," +
				currentTrackerPosition.position.y + "," +
				currentTrackerPosition.position.z + "," +
				currentTrackerPosition.rotation.eulerAngles.x + "," +
				currentTrackerPosition.rotation.eulerAngles.y + "," +
				currentTrackerPosition.rotation.eulerAngles.z;
			Debug.Log(toWrite);
			Debug.DrawLine(currentTrackerPosition.position, currentTrackerPosition.position + (currentTrackerPosition.forward * 1000));
			
            string filePath = Application.dataPath;
            writer = new StreamWriter(filePath + "/Data/" + "savedPositions.csv",true);
            writer.WriteLine(toWrite);
            writer.Close();
            return currentTrackerPosition;
        }


        private void _OnTrackersFound(List<ViveTracker> pTrackers)
		{
			foreach (ViveTracker viveTracker in pTrackers)
			{
				Color color = UnityEngine.Random.ColorHSV();
				viveTracker.debugTransform.Init(viveTracker.name, color, _mainCameraTransform);
				viveTracker.ConnectedStatusChanged += _OnTrackerConnectedStatusChanged;
				viveTracker.Calibrated += _OnTrackerCalibrated;
				// Buttons
				viveTracker.GripPressed += _OnGripPressed;
				viveTracker.GripReleased += _OnGripReleased;
				viveTracker.TriggerPressed += _OnTriggerPressed;
				viveTracker.TriggerReleased += _OnTriggerReleased;
				viveTracker.TouchPadPressed += _OnTouchPadPressed;
				viveTracker.TouchPadReleased += _OnTouchPadReleased;
				viveTracker.MenuPressed += _OnMenuPressed;
				viveTracker.MenuReleased += _OnMenuReleased;
				// Attach a sphere to the tracker.
				GameObject renderer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				renderer.transform.parent = viveTracker.transform;
				renderer.transform.localPosition = Vector3.zero;
				renderer.transform.localRotation = Quaternion.identity;
				renderer.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
				renderer.GetComponent<Renderer>().material.color = color;
			}
		}

		private void _OnTrackerCalibrated(ViveTracker pTracker)
		{
			Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " calibrated (calibration = rotation offset from Origin's transform).");
		}

		private void _OnTrackerConnectedStatusChanged(ViveTracker pTracker)
		{
			if(pTracker.Connected)
			{
				Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " connected.");
			}
			else
			{
				Debug.LogWarning("[ViveTrackersTest] ViveTracker " + pTracker.name + " disconnected !");
			}
		}

		private void _OnGripPressed(ViveTracker pTracker)
		{
			Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " GRIP pressed.");
		}

		private void _OnGripReleased(ViveTracker pTracker)
		{
			Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " GRIP released.");
		}

		private void _OnTriggerPressed(ViveTracker pTracker)
		{
			Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " TRIGGER pressed.");
		}

		private void _OnTriggerReleased(ViveTracker pTracker)
		{
			Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " TRIGGER released.");
		}

		private void _OnTouchPadPressed(ViveTracker pTracker)
		{
			Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " TOUCHPAD pressed.");
		}

		private void _OnTouchPadReleased(ViveTracker pTracker)
		{
			Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " TOUCHPAD released.");
		}

		private void _OnMenuPressed(ViveTracker pTracker)
		{
			Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " MENU pressed.");
		}

		private void _OnMenuReleased(ViveTracker pTracker)
		{
			Debug.Log("[ViveTrackersTest] ViveTracker " + pTracker.name + " MENU released.");
		}
	}
}