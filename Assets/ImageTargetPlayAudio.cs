using UnityEngine;
using System.Collections;
using Vuforia;
using System;

public class ImageTargetPlayAudio : MonoBehaviour,
ITrackableEventHandler
{
	private TrackableBehaviour mTrackableBehaviour;

	void Start()
	{
		mTrackableBehaviour = GetComponent<TrackableBehaviour>();
		if (mTrackableBehaviour)
		{
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
			// Play audio when target is found
			gameObject.GetComponent<AudioSource>().Play();
			//	GetComponent<AudioSource>().Play();
			Debug.Log("REGEN FOUND NU NOG AUDIO");
		}
		else
		{
			// Stop audio when target is lost
			//	GetComponent<AudioSource>().Stop();
		}
	}   
}