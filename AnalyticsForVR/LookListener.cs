﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Bivrost.Bivrost360Player.Streaming;
using Bivrost.Bivrost360Player;

namespace Bivrost.AnalyticsForVR
{
	public class LookListener: IDisposable
    {

		private static Bivrost.Log.Logger log = new Bivrost.Log.Logger("AnalyticsForVR");

        double _lastMediaTime = 0;
        Stopwatch _mediaTimeDelta = new Stopwatch();
        double MediaTime {
            get
            {
                return _lastMediaTime + _mediaTimeDelta.Elapsed.TotalSeconds;
            }
            set
            {
                _lastMediaTime = value;
                _mediaTimeDelta.Restart();
            }
        }

		public bool AnySinkRegistered {
			get
			{
				return sessionSinks.Count > 0;
			}
		}

		ILookProvider lookProvider = null;

        LookHistory history = null;
        private string filename;
        private DateTime startTime;
        private ServiceResult serviceResult;
		private List<ISessionSink> sessionSinks = new List<ISessionSink>();

		public LookListener()
        {
            MediaDecoder.OnInstantiated += MediaDecoder_OnInstantiated;
            ShellViewModel.OnInstantiated += ShellViewModel_OnInstantiated;

            //listeners.Clear();
            //listeners.Add(new TraceLogMsgOnlyListener());
        }

        private void ShellViewModel_OnInstantiated(ShellViewModel shellViewModel)
        {
            log.Info("ShellViewModel instantiated");
            ShellViewModel.OnInstantiated -= ShellViewModel_OnInstantiated;
            shellViewModel.HeadsetEnable += HandleHeadsetEnable;
            shellViewModel.HeadsetDisable += HandleHeadsetDisable;
        }

        private void MediaDecoder_OnInstantiated(MediaDecoder instance)
        {
            log.Info("Media decoder instantiated");
            MediaDecoder.OnInstantiated -= MediaDecoder_OnInstantiated;
            instance.OnTimeUpdate += HandleTimeUpdate;
            instance.OnPlay += HandlePlay;
            instance.OnStop += HandleStop;

        }

        private void HandleStop()
        {
            // session end
            log.Info("Ended history session " + filename);
            if (history == null)
                return;
			Session session = new Session(filename, startTime, DateTime.Now, history, lookProvider, serviceResult);
			foreach (var sink in sessionSinks)
				if (sink.Enabled)
					sink.UseSession(session);
            history = null;
        }


        private void HandlePlay()
        {
            history = new LookHistory(10, MediaDecoder.Instance.Duration);
            log.Info("New session: " + MediaDecoder.Instance.FileName);
            filename = MediaDecoder.Instance.FileName;
            serviceResult = ShellViewModel.Instance.SelectedServiceResult;
            startTime = DateTime.Now;
        }

        private void HandleHeadsetDisable(ILookProvider headset)
        {
			headset.ProvideLook -= HandleProvideLook;
			lookProvider = null;
        }

        private void HandleHeadsetEnable(ILookProvider headset)
        {
			headset.ProvideLook += HandleProvideLook;
			lookProvider = headset;
        }

        //Stopwatch hpls = new Stopwatch();
        private void HandleProvideLook(SharpDX.Vector3 position, SharpDX.Quaternion rotation, float fov)
        {
			history?.TrackData((float)MediaTime, rotation, (byte)fov);
			//Info($"{hpls.ElapsedMilliseconds:0000}   {_lastMediaTime:000.0000} + {_mediaTimeDelta.Elapsed.TotalSeconds:000.0000} = {_lastMediaTime + _mediaTimeDelta.Elapsed.TotalSeconds:000.0000}");
			//hpls.Restart();
		}

        public void Dispose()
        {
            MediaDecoder.OnInstantiated -= MediaDecoder_OnInstantiated;
            if (MediaDecoder.Instance != null)
            {
                MediaDecoder.Instance.OnTimeUpdate -= HandleTimeUpdate;
                MediaDecoder.Instance.OnPlay -= HandlePlay;
                MediaDecoder.Instance.OnStop -= HandleStop;
            }
            ShellViewModel.OnInstantiated -= ShellViewModel_OnInstantiated;
            if (ShellViewModel.Instance != null)
            {
                ShellViewModel.Instance.HeadsetEnable -= HandleHeadsetEnable;
                ShellViewModel.Instance.HeadsetDisable -= HandleHeadsetDisable;
            }
            if(lookProvider != null)
                lookProvider.ProvideLook -= HandleProvideLook;
        }

		internal void RegisterSessionSink(ISessionSink sink)
		{
			sessionSinks.Add(sink);
		}

		private void HandleTimeUpdate(double currentTime)
        {
            MediaTime = currentTime;
        }
    }

}