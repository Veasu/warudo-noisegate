using System.Collections.Generic;
using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Data.Models;
using System.Linq;
using Warudo.Core.Graphs;
using Cysharp.Threading.Tasks;
using Warudo.Core.Data;
using System;
using System.Threading;

namespace veasu.noisegate
{
  [NodeType(Id = "com.veasu.noisegate", Title = "Microphone Noise Gate", Category = "Audio Control")]
  public class NoiseGateNode : Warudo.Core.Graphs.Node
  {
    [Markdown(-1001, false, false)]
    public string CurrentDB = "";

    [DataInput]
    [Label("MICROPHONE")]
    [AutoComplete("AutoCompleteMicrophone", true, "")]
    public string SelectedMicrophone = null;

    [DataInput]
    [Label("Threshold (Db)")]
    [FloatSlider(-160.0f, 100.0f)]
    public float Threshold = 0.0f;

    [DataInput]
    [Label("Release (Seconds)")]
    [FloatSlider(0f, 10f)]
    public float Release = 0f;

    [DataOutput]
    [Label("Enabled")]
    public bool enabled () => ShouldEnabled;

    private float[] _samples;

    private GameObject gameObject;
    private float rmsVal;
    private float dbVal;
    private bool ShouldEnabled = false;
    private AudioSource MicrophoneSource;
    private List<float> PeakVals = new List<float>();
    private const float RefValue = 0.1f;
    private const int Samples = 1024;
    private float ReleaseTime = 0.0f;

    protected async UniTask<AutoCompleteList> AutoCompleteMicrophone() => AutoCompleteList.Single((IEnumerable<AutoCompleteEntry>) Microphone.devices.Select<string, AutoCompleteEntry>((Func<string, AutoCompleteEntry>) (it => new AutoCompleteEntry()
    {
      label = it,
      value = it
    })).ToList<AutoCompleteEntry>());
    protected override void OnCreate() {
      base.OnCreate();
      _samples = new float[Samples];
      this.gameObject = new GameObject("NoiseGate Daemon");
      this.MicrophoneSource = gameObject.AddComponent<AudioSource>();

      Watch<float>(nameof(Release), (from, to) => {
        if (ReleaseTime != 0.0f) ReleaseTime -= (from - to);
      });
      
      Watch<string>(nameof(SelectedMicrophone),(from, to) => {
        MicrophoneSource.Stop();
        if (from != null) {
          Microphone.End(from);
          CurrentDB = "";
        }

        if (to != null) {
          MicrophoneSource.clip = Microphone.Start(SelectedMicrophone, true, 10, 48000);
          MicrophoneSource.loop = true;
          MicrophoneSource.volume = 0f;
          int num = 0;
          while (Microphone.GetPosition(SelectedMicrophone) <= 0)
          {
            if (++num >= 1000)
            {
              Debug.LogError((object) "Failed to get microphone.");
              return;
            }
            Thread.Sleep(1);
          }
          MicrophoneSource.Play();
        }
      });
    }

    public override void OnUpdate() {
      if (SelectedMicrophone != null) {
        int startPos = Microphone.GetPosition(SelectedMicrophone) - Samples;
        MicrophoneSource.clip.GetData(_samples, startPos);
        int i;
        float sum = 0;
        for (i = 0; i < Samples; i++)
        {
          sum += _samples[i] * _samples[i];
        }
        rmsVal = Mathf.Sqrt(sum / Samples);
        dbVal = 20 * Mathf.Log10(rmsVal / RefValue);
        if (dbVal < -160) dbVal = -160;
        bool threshHoldHit = dbVal > Threshold;
        CurrentDB = $"Current Db Level:<br />{dbVal.ToString()}";
        if(threshHoldHit) ReleaseTime = Time.time + (float)Release;

        if (Time.time < ReleaseTime) {
          ShouldEnabled = true;
        } else {
          ShouldEnabled = threshHoldHit;
        }
        this.BroadcastDataInput("CurrentDB");
      }
    }

    protected override void OnDestroy() {
      base.OnDestroy();
      UnityEngine.Object.Destroy((UnityEngine.Object) this.gameObject);
    }
  }

}
