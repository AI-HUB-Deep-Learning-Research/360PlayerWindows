﻿using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlayerUI.Streaming
{

	// https://videos.littlstar.com/22769966-332d-4f03-af2c-8ea68432a2c3/web.mp4
	// vs https://videos.littlstar.com/22769966-332d-4f03-af2c-8ea68432a2c3/web_hd.mp4


	public class LittlstarParser : ServiceParser
	{
		public override bool CanParse(string uri)
		{
			if (uri == null) return false;
			return Regex.IsMatch(uri, @"^(https?://)?(www.)?(embed.)?littlstar.com/videos/.+", RegexOptions.IgnoreCase);
		}


		private static HtmlDocument DownloadDocument(string uri)
		{
			string html=HTTPGetString(uri, false);

			try
			{
				HtmlDocument document = new HtmlDocument();
				document.LoadHtml(html);
				return document;
			}
			catch(Exception e)
			{
				throw new StreamParsingFailed("HTML parse error", e);
			}

		}


		public override ServiceResult Parse(string uri)
		{
			string id = Regex.Match(uri, @"^(https?://)?(www.)?(embed.)?littlstar.com/videos/([a-zA-Z0-9]+).*", RegexOptions.IgnoreCase).Groups[4].Value;
			string html = HTTPGetString("https://embed.littlstar.com/videos/" + id);
			string json = Regex.Match(html, @"window.__mediaData__\s*=\s*(.+);").Groups[1].Value;
			JObject jobject = JObject.Parse(json);

			//var node = document.DocumentNode.Descendants()
			//	.Where(n => n.Name == "a" && n.GetAttributeValue("class", "")
			//	.Contains("download"))
			//	.First()
			//	.GetAttributeValue("href", "");

			// naive implementation
			//var node = document.DocumentNode.Descendants()
			//	.Where(n => n.Name == "meta" && n.GetAttributeValue("property", "")
			//	.Contains("og:video") && n.GetAttributeValue("content", "")
			//	.EndsWith(".mp4"))
			//	.First()
			//	.GetAttributeValue("content", "");

			List<VideoStream> videostreams = new List<VideoStream>();
			foreach(JProperty kvp in jobject.GetValue("versions"))
			{
				switch(kvp.Name)
				{
					case "mobile":
						videostreams.Add(new VideoStream()
						{
							container = VideoContainer.mp4,
							quality = 1,
							width = 1334,
							height = 666,
							url = (string)kvp.Value
						});
						break;

					case "mobile_hd":
						videostreams.Add(new VideoStream()
						{
							container = VideoContainer.mp4,
							quality = 2,
							width = 1920,
							height = 960,
							url = (string)kvp.Value
						});
						break;

					case "web":
						videostreams.Add(new VideoStream()
						{
							container = VideoContainer.mp4,
							quality = 3,
							width = 1920,
							height = 960,
							url = (string)kvp.Value
						});
						break;

					case "alcatel":
						videostreams.Add(new VideoStream()
						{
							container = VideoContainer.mp4,
							quality = 4,
							width = 1920,
							height = 1080,
							url = (string)kvp.Value
						});
						break;

					case "oculus_rift":
						videostreams.Add(new VideoStream()
						{
							container = VideoContainer.mp4,
							quality = 5,
							width = 2160,
							height = 1080,
							url = (string)kvp.Value
						});
						break;

					case "gear_vr":
						videostreams.Add(new VideoStream()
						{
							container = VideoContainer.mp4,
							quality = 6,
							width = 2560,
							height = 1280,
							url = (string)kvp.Value
						});
						break;

					case "vr":
						videostreams.Add(new VideoStream()
						{
							container = VideoContainer.mp4,
							quality = 7,
							width = 2560,
							height = 1280,
							url = (string)kvp.Value
						});
						break;

					case "web_hd":
						videostreams.Add(new VideoStream()
						{
							container = VideoContainer.mp4,
							quality = 8,
							width = 2560,
							height = 1280,
							url = (string)kvp.Value
						});
						break;

					default:
						Warn("littlstar unknown version: " + kvp.Name);
						break;
				}
			}

			return new ServiceResult()
			{
				originalURL = "https://www.littlstar.com/videos/" + id,
				serviceName = "Littlstar",
				projection = MediaDecoder.ProjectionMode.Sphere,
				stereoscopy = MediaDecoder.VideoMode.Mono,
				title = (string)jobject["title"],
				description = (string)jobject["description"],
				videoStreams = videostreams
			};

		}
	}



}
