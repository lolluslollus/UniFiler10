using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using Utilz;
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
		public event EventHandler UnrecoverableError;
		#region properties
		private AudioGraph _audioGraph = null;
		private AudioFileOutputNode _fileOutputNode;
		private AudioDeviceOutputNode _deviceOutputNode;
		private AudioDeviceInputNode _deviceInputNode;
		private DeviceInformationCollection _outputDevices;
		private IMessageWriter _messageWriter;
		private StorageFile _file;
		#endregion properties

		#region construct, dispose, open, close
		public AudioRecorder(IMessageWriter messageWriter, StorageFile file)
		{
			_messageWriter = messageWriter;
			_file = file;
		}

		protected override async Task OpenMayOverrideAsync()
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
			try
			{
				_deviceInputNode?.Dispose();
			}
			catch { }
			try
			{
				_deviceOutputNode?.Dispose();
			}
			catch { }
			try
			{
				_fileOutputNode?.Dispose();
			}
			catch { }
			await Task.CompletedTask; // avoid the warning...
		}
		#endregion construct, dispose, open, close

		#region init properties before recording
		/// <summary>
		/// Required before starting recording
		/// </summary>
		/// <param name="file"></param>
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

			// Create a device output node
			CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await _audioGraph.CreateDeviceOutputNodeAsync();
			if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
			{
				// Cannot create device output node
				return string.Format("Audio Device Output unavailable because {0}", deviceOutputNodeResult.Status.ToString());
			}
			_deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;

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
			CreateAudioFileOutputNodeResult fileOutputNodeResult = await _audioGraph.CreateFileOutputNodeAsync(file, fileProfile);
			if (fileOutputNodeResult.Status != AudioFileNodeCreationStatus.Success)
			{
				// FileOutputNode creation failed
				return string.Format("Cannot create output file because {0}", fileOutputNodeResult.Status.ToString());
			}
			_fileOutputNode = fileOutputNodeResult.FileOutputNode;

			// Connect the input node to both output nodes
			_deviceInputNode.AddOutgoingConnection(_fileOutputNode);
			_deviceInputNode.AddOutgoingConnection(_deviceOutputNode);

			return string.Empty;
		}

		private MediaEncodingProfile CreateMediaEncodingProfile(StorageFile file)
		{
			MediaEncodingProfile output = null;
			switch (file.FileType.ToString().ToLowerInvariant())
			{
				case ".wma":
					output = MediaEncodingProfile.CreateWma(AudioEncodingQuality.High); break;
				case ".mp3":
					output = MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High); break;
				case ".wav":
					output = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High); break;
				default:
					throw new ArgumentException("AudioRecorder.CreateMediaEncodingProfile() : wrong media encoding profile");
			}
			// var test = output.Audio.Properties["AudioDeviceController"];
			return output;
		}
		#endregion init properties before recording

		#region record
		public Task<bool> RecordStartAsync()
		{
			return RunFunctionWhileOpenAsyncB(delegate
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
		public Task<bool> RecordStopAsync()
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
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
					catch (Exception ex)
					{
						if (_messageWriter != null) _messageWriter.LastMessage = RuntimeData.GetText("AudioRecordingStopped");
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
