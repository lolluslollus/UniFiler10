﻿using System;
using System.Threading.Tasks;
using UniFiler10.Data.Runtime;
using Utilz.Data;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Utilz
{
	public class AudioRecorder : OpenableObservableData
	{
		#region events
		public event EventHandler UnrecoverableError;
		#endregion events

		#region properties
		private AudioGraph _audioGraph = null;
		private AudioFileOutputNode _fileOutputNode;
		//private AudioDeviceOutputNode _deviceOutputNode; // away, so we get no echo
		private AudioDeviceInputNode _deviceInputNode;
		private DeviceInformationCollection _outputDevices;
		private readonly IMessageWriter _messageWriter;
		private readonly StorageFile _file;
		#endregion properties

		#region lifecycle
		public AudioRecorder(IMessageWriter messageWriter, StorageFile file)
		{
			_messageWriter = messageWriter;
			_file = file;
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			_messageWriter.LastMessage = await CreateAudioGraphAsync().ConfigureAwait(false);
			if (string.IsNullOrWhiteSpace(_messageWriter.LastMessage))
			{
				_messageWriter.LastMessage = await SetFileAsync(_file).ConfigureAwait(false);
			}
			if (!string.IsNullOrWhiteSpace(_messageWriter.LastMessage))
			{
				UnrecoverableError?.Invoke(this, EventArgs.Empty);
			}
		}
		protected override async Task CloseMayOverrideAsync()
		{
			var ag = _audioGraph;
			if (ag != null)
			{
				ag.UnrecoverableErrorOccurred -= OnGraph_UnrecoverableErrorOccurred;
				try
				{
					ag.Stop();
				}
				catch { }
				try
				{
					ag.Dispose();
				}
				catch { }
			}
			_audioGraph = null;
			//try
			//{
			//	_deviceInputNode?.Dispose();
			//}
			//catch { }
			//try
			//{
			//	_deviceOutputNode?.Dispose();
			//}
			//catch { }
			//try
			//{
			//	_fileOutputNode?.Dispose();
			//}
			//catch { }
			await Task.CompletedTask;
		}
		#endregion lifecycle

		#region init properties before recording
		/// <summary>
		/// Required before starting recording
		/// </summary>
		/// <returns></returns>
		private async Task<string> CreateAudioGraphAsync()
		{
			// var inputDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector()); // LOLLO TEST

			_outputDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
			if (_outputDevices == null || _outputDevices.Count < 1)
			{
				return "AudioGraph Creation Error: no output devices found";
			}

			AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media)
			{ QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency, PrimaryRenderDevice = _outputDevices[0] };

			CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
			if (result.Status != AudioGraphCreationStatus.Success)
			{
				// Cannot create graph
				return string.Format("AudioGraph Creation Error because {0}", result.Status.ToString());
			}
			_audioGraph = result.Graph;
			// Because we are using lowest latency setting, we need to handle device disconnection errors
			_audioGraph.UnrecoverableErrorOccurred += OnGraph_UnrecoverableErrorOccurred;

			//// Create a device output node // away, so we get no echo
			//CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await _audioGraph.CreateDeviceOutputNodeAsync();
			//if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
			//{
			//	// Cannot create device output node
			//	return string.Format("Audio Device Output unavailable because {0}", deviceOutputNodeResult.Status.ToString());
			//}
			//_deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;

			// Create a device input node using the default audio input device
			CreateAudioDeviceInputNodeResult deviceInputNodeResult = await _audioGraph.CreateDeviceInputNodeAsync(MediaCategory.Other);
			if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
			{
				// Cannot create device input node
				return string.Format("Audio Device Input unavailable because {0}", deviceInputNodeResult.Status.ToString());
			}
			_deviceInputNode = deviceInputNodeResult.DeviceInputNode;

			//// LOLLO set the volume, rather useless coz it is like a mixer and the default value is 1.
			//if (_deviceOutputNode.OutgoingGain < 1.0) _deviceOutputNode.OutgoingGain = 1.0;
			//if (_deviceInputNode.OutgoingGain < 1.0) _deviceInputNode.OutgoingGain = 1.0;

			return string.Empty;
		}

		private void OnGraph_UnrecoverableErrorOccurred(AudioGraph sender, AudioGraphUnrecoverableErrorOccurredEventArgs args)
		{
			_messageWriter.LastMessage = args.Error.ToString();
			// Recreate the graph and all nodes when this happens
			//sender.Dispose();
			//DisposeAudioGraph();

			UnrecoverableError?.Invoke(this, EventArgs.Empty);

			// Re-query for devices // LOLLO NO!
			// _messageWriter.LastMessage = await CreateAudioGraphAsync().ConfigureAwait(false);
		}
		/// <summary>
		/// Required before starting recording
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		private async Task<string> SetFileAsync(StorageFile file)
		{
			if (file == null)
			{
				return "file is empty";
			}

			MediaEncodingProfile fileProfile = CreateMediaEncodingProfile(file);

			// Operate node at the graph format, but save file at the specified format
			CreateAudioFileOutputNodeResult fileOutputNodeResult = await _audioGraph.CreateFileOutputNodeAsync(file, fileProfile); // LOLLO NOTE this fails on the phone with mp3, not with wav
			// CreateAudioFileOutputNodeResult fileOutputNodeResult = await _audioGraph.CreateFileOutputNodeAsync(file); // this does not fail but it records some crap that cannot be played back, on the phone
			if (fileOutputNodeResult.Status != AudioFileNodeCreationStatus.Success)
			{
				// FileOutputNode creation failed
				return string.Format("Cannot create output file because {0}", fileOutputNodeResult.Status.ToString());
			}
			_fileOutputNode = fileOutputNodeResult.FileOutputNode;

			// Connect the input node to both output nodes
			_deviceInputNode.AddOutgoingConnection(_fileOutputNode);
			//_deviceInputNode.AddOutgoingConnection(_deviceOutputNode); // away, so we get no echo

			return string.Empty;
		}

		private static MediaEncodingProfile CreateMediaEncodingProfile(IStorageFile file)
		{
			MediaEncodingProfile output = null;
			switch (file?.FileType?.ToLowerInvariant())
			{
				case ".wma":
					output = MediaEncodingProfile.CreateWma(AudioEncodingQuality.High);
					break;
				case ".mp3":
					output = MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High);
					break; // LOLLO NOTE error with phone Unknown failure as a consequence of this. Wav works instead.
				case ".wav":
					output = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
					break;
				default:
					throw new ArgumentException("AudioRecorder.CreateMediaEncodingProfile() : wrong media encoding profile");
			}
			// var test = output.Audio.Properties["AudioDeviceController"];
			return output;
		}
		#endregion init properties before recording

		#region record
		public Task<bool> StartRecordingAsync()
		{
			return RunFunctionIfOpenAsyncB(delegate
			{
				_messageWriter.LastMessage = RuntimeData.GetText("AudioRecordingStarted");
				try
				{
					_audioGraph.Start();
					return true;
				}
				catch
				{
					return false;
				}
			});
		}
		public Task<bool> StopRecordingAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				// Good idea to stop the graph to avoid data loss
				try
				{
					_audioGraph?.Stop();
				}
				catch { }

				if (_fileOutputNode != null)
				{
					try
					{
						TranscodeFailureReason finalizeResult = await _fileOutputNode.FinalizeAsync();
						if (finalizeResult != TranscodeFailureReason.None)
						{
							// Finalization of file failed. Check result code to see why
							if (_messageWriter != null) _messageWriter.LastMessage = string.Format("Finalization of file failed because {0}", finalizeResult.ToString());
							return false;
						}
					}
#pragma warning disable 0168
					catch (Exception ex)
#pragma warning restore 0168
					{
						if (_messageWriter != null) _messageWriter.LastMessage = RuntimeData.GetText("AudioRecordingInterrupted");
						// if (_messageWriter != null) _messageWriter.LastMessage = string.Format("Finalization of file failed because {0}", ex.ToString());
						return false;
					}
				}
				if (_messageWriter != null) _messageWriter.LastMessage = RuntimeData.GetText("AudioRecordingStopped");
				return true;
			});
		}
		#endregion record
	}

	public interface IMessageWriter
	{
		string LastMessage { get; set; }
	}
}