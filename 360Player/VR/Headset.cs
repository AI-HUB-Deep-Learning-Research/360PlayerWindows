﻿using Bivrost.AnalyticsForVR;
using SharpDX.Direct3D11;
using System;
using System.Threading;
using System.Threading.Tasks;
using SharpDX;
using Bivrost.Bivrost360Player.Streaming;
using Bivrost.Log;

namespace Bivrost.Bivrost360Player
{

	public class HeadsetError : Exception
	{
		public HeadsetError(string msg) : base(msg) { }
		public HeadsetError(Exception inner, string msg) : base(msg, inner) { }
	}


	public abstract class Headset : ILookProvider, IUpdatableSceneSettings
    {
		public Texture2D textureL;
		public Texture2D textureR;


		private static ServiceResult nothingIsPlaying = new ServiceResult(null, "(none)", "nothing")
		{
			description = "",
			stereoscopy = MediaDecoder.VideoMode.Autodetect,
			projection = MediaDecoder.ProjectionMode.Sphere,
			title = ""
		};

		private ServiceResult _media;
		public ServiceResult Media
		{
			get => _media ?? nothingIsPlaying;
			set
			{
				_media = value;
				vrui?.EnqueueUIRedraw();
				UpdateSceneSettings(Media.projection, Media.stereoscopy);
			}
		}
		public bool _stereoVideo => Array.IndexOf(new[] { MediaDecoder.VideoMode.Mono, MediaDecoder.VideoMode.Autodetect }, Media.stereoscopy) < 0;
		public MediaDecoder.ProjectionMode Projection => Media.projection;
		protected string MovieTitle => Media.TitleWithFallback;
		protected float Duration => (float)MediaDecoder.Instance.Duration;


		protected Logger log;


		public void Start()
		{
			abort = false;
			pause = false;
			waitForRendererStop.Reset();
			if (Lock)
				return;
			Task.Factory.StartNew(() =>
			{
				try
				{
					Render();
				}
#if !DEBUG
				catch(Exception exc)
				{
					Console.WriteLine("[EXC] " + exc.Message);
				}
#endif
				finally
				{
					Lock = false;
				}
			});
		}


		protected abstract void Render();



		bool _playbackLock = false;
		public bool Lock { get { return _playbackLock; } protected set { this._playbackLock = value; } }

		protected object localCritical = new object();


		protected ManualResetEvent waitForRendererStop = new ManualResetEvent(false);
		protected bool abort = false;
		protected bool pause = false;

		protected float currentTime = 0;

		protected SharpDX.Toolkit.Graphics.Effect customEffectL;
		protected SharpDX.Toolkit.Graphics.Effect customEffectR;


		private static SharpDX.Toolkit.Graphics.EffectCompilerResult gammaShader = null;
		private static SharpDX.Toolkit.Graphics.EffectCompilerResult GammaShader
		{
			get
			{
				if (gammaShader == null)
				{
					string shaderSource = Properties.Resources.GammaShader;
					SharpDX.Toolkit.Graphics.EffectCompiler compiler = new SharpDX.Toolkit.Graphics.EffectCompiler();
					var shaderCode = compiler.Compile(shaderSource, "gamma shader", SharpDX.Toolkit.Graphics.EffectCompilerFlags.Debug | SharpDX.Toolkit.Graphics.EffectCompilerFlags.EnableBackwardsCompatibility | SharpDX.Toolkit.Graphics.EffectCompilerFlags.SkipOptimization);

					if (shaderCode.HasErrors)
						throw new HeadsetError("Shader compile error:\n" + string.Join("\n", shaderCode.Logger.Messages));
					gammaShader = shaderCode;
				}
				return gammaShader;
			}
		}


		public static SharpDX.Toolkit.Graphics.Effect GetCustomEffect(SharpDX.Toolkit.Graphics.GraphicsDevice gd)
		{
			var ce = new SharpDX.Toolkit.Graphics.Effect(gd, GammaShader.EffectData);
			ce.CurrentTechnique = ce.Techniques["ColorTechnique"];
			ce.CurrentTechnique.Passes[0].Apply();
			return ce;
		}


		protected VRUI vrui;


		public void Pause()
		{
			vrui?.EnqueueUIRedraw();
			pause = true;
		}
		public void UnPause() { pause = false; }

		public void UpdateTime(float time)
		{
			vrui?.EnqueueUIRedraw();
			currentTime = time;
		}


		public void Stop()
		{
			abort = true;
		}

		public void Reset()
		{
			abort = false;
		}


		protected SharpDX.Toolkit.Graphics.GraphicsDevice _gd;
		protected Device _device;

        public abstract event Action<Vector3, Quaternion, float> ProvideLook;

        abstract protected float Gamma { get; }
        public abstract string DescribeType { get; }


		private SharpDX.Toolkit.Graphics.Texture2D _defaultBackgroundTexture = null;
		public SharpDX.Toolkit.Graphics.Texture2D DefaultBackgroundTexture
		{
			get
			{
				if(_defaultBackgroundTexture == null)
				{
					var assembly = GetType().Assembly;
					var fullResourceName = "Bivrost.Bivrost360Player.Resources.default-background-requirectangular.png";
					using (var stream = assembly.GetManifestResourceStream(fullResourceName))
					{
						_defaultBackgroundTexture = SharpDX.Toolkit.Graphics.Texture2D.Load(_gd, stream);
					}

				}
				return _defaultBackgroundTexture;
			}
		}


		protected void ResizeTexture(Texture2D tL, Texture2D tR)
		{
			if(tL == null && tR == null)
			{
				log.Info("ResizeTexture got null textures, loading defaults...");

				lock (localCritical)
				{
					(customEffectL.Parameters["UserTex"]?.GetResource<IDisposable>())?.Dispose();
					(customEffectR.Parameters["UserTex"]?.GetResource<IDisposable>())?.Dispose();
					textureL = tL;
					textureR = tR;

					customEffectL.Parameters["UserTex"].SetResource(DefaultBackgroundTexture);
					customEffectL.Parameters["gammaFactor"].SetValue(Gamma);
					customEffectL.CurrentTechnique = customEffectL.Techniques["ColorTechnique"];
					customEffectL.CurrentTechnique.Passes[0].Apply();

					customEffectR.Parameters["UserTex"].SetResource(DefaultBackgroundTexture);
					customEffectR.Parameters["gammaFactor"].SetValue(Gamma);
					customEffectR.CurrentTechnique = customEffectR.Techniques["ColorTechnique"];
					customEffectR.CurrentTechnique.Passes[0].Apply();
				}

				vrui?.EnqueueUIRedraw();

				UpdateSceneSettings(MediaDecoder.ProjectionMode.Sphere, MediaDecoder.VideoMode.Mono);
				return;
			}



			log.Info($"ResizeTexture {tL}, {tR}");

			if (MediaDecoder.Instance.TextureReleased) {
				log.Error("MediaDecoder texture released");
				return;
			}

			lock (localCritical)
			{
				(customEffectL.Parameters["UserTex"]?.GetResource<IDisposable>())?.Dispose();
				(customEffectR.Parameters["UserTex"]?.GetResource<IDisposable>())?.Dispose();
				textureL = tL;
				textureR = tR;

				var resourceL = textureL.QueryInterface<SharpDX.DXGI.Resource>();
				var sharedTexL = _device.OpenSharedResource<Texture2D>(resourceL.SharedHandle);


				//basicEffectL.Texture = SharpDX.Toolkit.Graphics.Texture2D.New(_gd, sharedTexL);
				customEffectL.Parameters["UserTex"].SetResource(SharpDX.Toolkit.Graphics.Texture2D.New(_gd, sharedTexL));
				customEffectL.Parameters["gammaFactor"].SetValue(Gamma);
				customEffectL.CurrentTechnique = customEffectL.Techniques["ColorTechnique"];
				customEffectL.CurrentTechnique.Passes[0].Apply();

				resourceL?.Dispose();
				sharedTexL?.Dispose();

				//if (_stereoVideo)
				//{
					var resourceR = textureR.QueryInterface<SharpDX.DXGI.Resource>();
					var sharedTexR = _device.OpenSharedResource<Texture2D>(resourceR.SharedHandle);

					//basicEffectR.Texture = SharpDX.Toolkit.Graphics.Texture2D.New(_gd, sharedTexR);
					customEffectR.Parameters["UserTex"].SetResource(SharpDX.Toolkit.Graphics.Texture2D.New(_gd, sharedTexR));
					customEffectR.Parameters["gammaFactor"].SetValue(Gamma);
					customEffectR.CurrentTechnique = customEffectR.Techniques["ColorTechnique"];
					customEffectR.CurrentTechnique.Passes[0].Apply();

					resourceR?.Dispose();
					sharedTexR?.Dispose();
				//}


				//_device.ImmediateContext.Flush();
			}

			vrui?.EnqueueUIRedraw();
		}


		abstract public bool IsPresent();


		protected Bivrost.ActionQueue updateSettingsActionQueue = new Bivrost.ActionQueue();
		public abstract void UpdateSceneSettings(MediaDecoder.ProjectionMode projectionMode, MediaDecoder.VideoMode stereoscopy);
	}
}
