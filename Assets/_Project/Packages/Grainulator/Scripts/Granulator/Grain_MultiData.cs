﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class GrainPlaybackData
{
    public bool _IsPlaying = true;
    public float[] _GrainSamples;
    public int _PlaybackIndex = 0;

    public int _StartSampleIndex;

    //public GrainPlaybackData(float[] grainSamples, int playbackIndex)
    //{
    //    _GrainSamples = grainSamples;
    //    _PlaybackIndex = playbackIndex;
    //}
}

public class Grain_MultiData : MonoBehaviour
{
    public int _CurrentDSPSampleIndex = 0;

    List<GrainPlaybackData> _GrainPlaybackData = new List<GrainPlaybackData>();

    public void Update()
    {
        for (int i = _GrainPlaybackData.Count - 1; i >= 0; i--)
        {
            if (!_GrainPlaybackData[i]._IsPlaying)
                _GrainPlaybackData.RemoveAt(i);
        }

    }

    //---------------------------------------------------------------------
    public void AddGrainData(GrainData gd, float[] clipSamples, int freq, AnimationCurve windowCurve, bool debugLog = false, bool traditionalWindowing = false)
    {
        GrainPlaybackData grainPlaybackData = new GrainPlaybackData();

        int playheadSampleIndex = (int)(gd._PlayheadPos * clipSamples.Length);
        int durationInSamples = (int)(freq / 1000 * gd._Duration);

        // -----------------------------------------BUILD SAMPLE ARRAY
        // Grain array to pull samples into
        grainPlaybackData._GrainSamples = new float[durationInSamples];

        var tempSamples = new float[durationInSamples];
        int sourceIndex;
        // Construct grain sample data
        for (int i = 0; i < grainPlaybackData._GrainSamples.Length; i++)
        {
            // Offset to source audio sample position for grain
            sourceIndex = playheadSampleIndex + i;

            // Ping-pong audio sample read
            sourceIndex = (int)Mathf.PingPong(sourceIndex, clipSamples.Length - 1);

            // Fill temp sample buffer
            tempSamples[i] = clipSamples[sourceIndex];
        }

        // Window samples
        for (int i = 0; i < tempSamples.Length; i++)
        {
            // Set start index
            //int index = gd._SampleOffset % tempSamples.Length;

            // find the norm along the array
            float norm = i / (tempSamples.Length - 1f);
            float windowedVolume = windowCurve.Evaluate(norm);

            float pitchedNorm = norm * gd._Pitch;
            float sample = GetValueFromNormPosInArray(tempSamples, pitchedNorm);

            grainPlaybackData._GrainSamples[i] = sample * windowedVolume * gd._Volume;
        }

        grainPlaybackData._IsPlaying = true;
        grainPlaybackData._StartSampleIndex = gd._StartSampleIndex;

        _GrainPlaybackData.Add(grainPlaybackData);

        //if (debugLog)
        //    Debug.Log(String.Format("Playhead pos {0}    Duration {1}   Pitch {2}    Time  {3} ", playheadSampleIndex + (int)startSample, durationInSamples, gd._Pitch, Time.time));
    }

    //---------------------------------------------------------------------
    // AUDIO BUFFER CALLS
    // DSP Buffer size in audio settings
    // Best performance - 46.43991
    // Good latency - 23.21995
    // Best latency - 11.60998
    //---------------------------------------------------------------------

    void OnAudioFilterRead(float[] data, int channels)
    {
        int samples = 0;
        // For length of audio buffer, populate with grain samples, maintaining index over successive buffers
        for (int dataIndex = 0; dataIndex < data.Length; dataIndex += channels)
        {
            samples++;
            for (int i = 0; i < _GrainPlaybackData.Count; i++)
            {
                GrainPlaybackData grainData = _GrainPlaybackData[i];

                if (grainData == null)
                    continue;

                if (_CurrentDSPSampleIndex >= grainData._StartSampleIndex)
                {
                    if (grainData._PlaybackIndex >= grainData._GrainSamples.Length)
                        grainData._IsPlaying = false;
                    else
                    {
                        //if (grainData._PlaybackIndex == 0)
                           // print("Grain start sample index: " + grainData._StartSampleIndex + "   Current DSP Sample Index: " + _CurrentDSPSampleIndex);

                        data[dataIndex] += grainData._GrainSamples[grainData._PlaybackIndex];
                        grainData._PlaybackIndex++;
                    }
                }

                //if (_CurrentDSPSampleIndex >= grainData._StartSampleIndex)
                //    print("Starting grain at sample index: " + _CurrentDSPSampleIndex + "    " + grainData._StartSampleIndex);
            }

            _CurrentDSPSampleIndex++;
        }
    }

    public static float GetValueFromNormPosInArray(float[] array, float norm)
    {
        norm %= 1;
        float floatIndex = norm * (array.Length - 1);

        int lowerIndex = (int)Mathf.Floor(floatIndex);
        int upperIndex = Mathf.Clamp(lowerIndex + 1, lowerIndex, array.Length - 1);
        float lerp = norm % 1;

        return Mathf.Lerp(array[lowerIndex], array[upperIndex], lerp);
    }
}


public class GrainData
{
    public Vector3 _WorldPos;
    public Vector3 _Velocity;
    public float _Mass;

    public int _StartDSPSampleIndex;

    // Optimum 10ms - 60ms
    public float _Duration;
    public float _PlayheadPos;
    public float _Pitch;
    public float _Volume;

    public int _ClipIndex;

    public int _StartSampleIndex;

    public GrainData() { }
    public GrainData(Vector3 position, Vector3 velocity, float mass, int grainAudioClipIndex,
        float durationInMS, float playheadPosition, float pitch, float volume, int startSampleIndex)
    {
        _WorldPos = position;
        _Velocity = velocity;
        _Mass = mass;
        _ClipIndex = grainAudioClipIndex;
        _Duration = durationInMS;
        _PlayheadPos = playheadPosition;
        _Pitch = pitch;
        _Volume = volume;
        _StartSampleIndex = startSampleIndex;
    }

    public void Initialize(Vector3 position, Vector3 velocity, float mass, int grainAudioClipIndex,
        float durationInMS, float playheadPosition, float pitch, float volume, int startSampleIndex)
    {
        _WorldPos = position;
        _Velocity = velocity;
        _Mass = mass;
        _ClipIndex = grainAudioClipIndex;
        _Duration = durationInMS;
        _PlayheadPos = playheadPosition;
        _Pitch = pitch;
        _Volume = volume;
        _StartSampleIndex = startSampleIndex;
    }
}

[System.Serializable]
public class GrainEmissionProps
{
    public int _ClipIndex = 0;

    // Position (normalised)
    //---------------------------------------------------------------------
    [Range(0.0f, 1.0f)]
    [SerializeField]
    float _PlayheadPos = 0;
    [Range(0.0f, .1f)]
    [SerializeField]
    public float _PositionRandom = 0;
    public float Position
    {
        get
        {
            return Mathf.Clamp(_PlayheadPos + UnityEngine.Random.Range(0, _PositionRandom), 0f, 1f);
        }
        set
        {
            _PlayheadPos = Mathf.Clamp(value, 0f, 1f);
        }
    }

    // Duration (ms)
    //---------------------------------------------------------------------
    [Range(2.0f, 1000f)]
    [SerializeField]
    int _Duration = 100;
    [Range(0.0f, 500f)]
    [SerializeField]
    int _DurationRandom = 0;
    public float Duration
    {
        get
        {
            return Mathf.Clamp(_Duration + UnityEngine.Random.Range(0, _DurationRandom), 2, 1000);
        }
        set
        {
            _Duration = (int)Mathf.Clamp(value, 2, 1000);
        }
    }


    // Transpose
    //---------------------------------------------------------------------
    [Range(-4f, 4f)]
    [SerializeField]
    float _Transpose = 0;
    [Range(0f, 1f)]
    [SerializeField]
    float _TransposeRandom = 0;

    float _Pitch = 1;
    public float Pitch
    {
        get
        {
            _Pitch = TransposeToPitch(Mathf.Clamp(_Transpose + UnityEngine.Random.Range(-_TransposeRandom, _TransposeRandom), -5f, 5f));
            return Mathf.Clamp(_Pitch, 0.1f, 5f);
        }
        set
        {
            _Pitch = Mathf.Clamp(value, 0.1f, 5f);
        }
    }

    // Converts the more human-readable value of transpose to pitch values for the grains
    private float TransposeToPitch(float transpose)
    {
        float pitch = 1;

        if (transpose < 0)
            pitch = (1 / (1 + Mathf.Abs(transpose)));
        else if (transpose > 0)
            pitch = transpose + 1;

        return pitch;
    }


    // Volume
    //---------------------------------------------------------------------
    [Range(0.0f, 2.0f)]
    [SerializeField]
    float _Volume = 1;          // from 0 > 1
    [Range(0.0f, 1.0f)]
    [SerializeField]
    float _VolumeRandom = 0;      // from 0 > 1
    public float Volume
    {
        get
        {
            return Mathf.Clamp(_Volume + UnityEngine.Random.Range(-_VolumeRandom, _VolumeRandom), 0f, 3f);
        }
        set
        {
            _Volume = (int)Mathf.Clamp(value, 0f, 3f);
        }
    }

    public GrainEmissionProps(float pos, int duration, float pitch, float volume,
        float posRand = 0, int durationRand = 0, float pitchRand = 0, float volumeRand = 0)
    {
        _PlayheadPos = pos;
        _Duration = duration;
        _Pitch = pitch;
        _Volume = volume;

        _PositionRandom = posRand;
        _DurationRandom = durationRand;
        //_PitchRandom = pitchRand;
        _VolumeRandom = volumeRand;
    }
}


[System.Serializable]
public class AudioClipLibrary
{
    public AudioClip[] _Clips;
    public List<float[]> _ClipsDataArray = new List<float[]>();

    public void Initialize()
    {
        Debug.Log("Initializing clip library.");
        for (int i = 0; i < _Clips.Length; i++)
        {
            AudioClip audioClip = _Clips[i];

            if (audioClip.channels > 1)
            {
                Debug.LogError("Audio clip not mono");
            }

            float[] samples = new float[audioClip.samples];
            _Clips[i].GetData(samples, 0);
            _ClipsDataArray.Add(samples);

            Debug.Log(String.Format("Clip {0}      Samples: {1}        Time length: {2} ", _Clips[i].name, _ClipsDataArray[i].Length, _ClipsDataArray[i].Length / (float)_Clips[i].frequency));
        }
    }
}
