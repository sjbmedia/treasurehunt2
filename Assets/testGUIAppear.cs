﻿	using UnityEngine;
	using System.Collections;
	using Vuforia;
	using System;

	public class testGUIAppear : MonoBehaviour, ITrackableEventHandler {

		float native_width= 1024f;
		float native_height= 768f;
		public Texture btntexture;
		public Texture LogoTexture;
		public Texture MobiliyaTexture;


		private TrackableBehaviour mTrackableBehaviour;

		private bool mShowGUIButton = false;


		void Start () {


			mTrackableBehaviour = GetComponent<TrackableBehaviour>();
			if (mTrackableBehaviour) {
				mTrackableBehaviour.RegisterTrackableEventHandler(this);
			}
		}

		public void OnTrackableStateChanged(
			TrackableBehaviour.Status previousStatus,
			TrackableBehaviour.Status newStatus)
		{
			if (newStatus == TrackableBehaviour.Status.DETECTED ||
				newStatus == TrackableBehaviour.Status.TRACKED ||
				newStatus == TrackableBehaviour.Status.EXTENDED_TRACKED)
			{
				mShowGUIButton = true;
			}
			else
			{
				//mShowGUIButton = false;
			}
		}

		void OnGUI() {

			//set up scaling
			float rx = Screen.width / native_width;
			float ry = Screen.height / native_height;

		//	GUI.matrix = Matrix4x4.TRS (new Vector3(0, 0, 0), Quaternion.identity, new Vector3 (rx, ry, 1));

		GUI.matrix = Matrix4x4.TRS (new Vector3(0, 0, 0), Quaternion.identity, new Vector3 (rx, ry, 1));

	//	Rect mButtonRect = new Rect(200, 400, 750, 571);
			GUIStyle myTextStyle = new GUIStyle(GUI.skin.textField);
			myTextStyle.fontSize = 50;
			myTextStyle.richText=true;

	//	GUI.DrawTexture(new Rect(200, 400, 750, 571),LogoTexture); 
	//	GUI.DrawTexture (new Rect (200, 400, 750, 571), MobiliyaTexture);


			if (!btntexture) // This is the button that triggers AR and UI camera On/Off
			{
				Debug.LogError("Please assign a texture on the inspector");
				return;
			}

			if (mShowGUIButton) {

			//	GUI.Button(new Rect(40, 325, 1000, 70), "<b> MAP 'M' IN EPISODE DETECTED </b>",myTextStyle);

			Rect mButtonRect = new Rect(0, 0, native_width, native_height);
			//GUI.DrawTexture(new Rect(0, 0, native_width, native_height), btntexture, ScaleMode.ScaleToFit, true);

		//	new Rect(0, 0, native_width, native_height);
		//	(new Rect(0, 0, native_width, native_height), btntexture, ScaleMode.ScaleToFit, true);

		//	GUI.Button(new Rect(200, 400, 750, 571),btntexture);
			Debug.Log("texture 'M' neergezet");

			Debug.Log(native_width);

			Handheld.Vibrate();

				//GUI.Box (new Rect (0,0,100,50), "Top-left");
				//GUI.Box (new Rect (1920 - 100,0,100,50), "Top-right");
				//GUI.Box (new Rect (0,1080- 50,100,50), "Bottom-left");
		//	GUI.Box (new Rect (Screen.width /2,Screen.height /2,Screen.width,Screen.height), "Bottom right",myTextStyle);

				// draw the GUI button
				if (GUI.Button(mButtonRect, btntexture)) {


					// do something on button click 
								Debug.Log("Clicked the button");



					//Remove Button;
			mShowGUIButton = false;
				Debug.Log("AND removed it after the click");

				}
			}
		}

		public void OpenVideoActivity()
		{
			var androidJC = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			var jo = androidJC.GetStatic<AndroidJavaObject>("currentActivity");
			// Accessing the class to call a static method on it
			var jc = new AndroidJavaClass("com.mobiliya.gepoc.StartVideoActivity");
			// Calling a Call method to which the current activity is passed
		//	jc.CallStatic("Call", jo);

		}

	}