using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Utilz;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace UniFiler10.Services
{
    public class AudioRecorder : OpenableObservableData
    {
        #region properties
        private AudioGraph _audioGraph = null;
        private AudioFileOutputNode _fileOutputNode;
        private AudioDeviceOutputNode _deviceOutputNode;
        private AudioDeviceInputNode _deviceInputNode;
        private DeviceInformationCollection _outputDevices;
        private IMessageWriter _messageWriter;
        private IAudioFileGetter _fileGetter;
        #endregion properties

        #region construct, dispose, open, close
        public AudioRecorder(IMessageWriter messageWriter, IAudioFileGetter fileGetter)
        {
            _messageWriter = messageWriter;
            _fileGetter = fileGetter;
        }

        //public async Task<bool> OpenAsync(StorageFile file)
        //{
        //    bool isOk = await OpenAsync().ConfigureAwait(false);
        //    isOk = isOk && await RunFunctionWhileOpenAsyncTB(async delegate
        //    {
        //        _messageWriter.LastMessage = await SetFileAsync(file).ConfigureAwait(false);
        //        return string.IsNullOrEmpty(_messageWriter.LastMessage);
        //    });
        //    return isOk;
        //}
        protected override async Task OpenMayOverrideAsync()
        {
            _messageWriter.LastMessage = await CreateAudioGraphAsync().ConfigureAwait(false);
            _messageWriter.LastMessage = await SetFileAsync(_fileGetter.GetAudioFile()).ConfigureAwait(false);
        }
        protected override async Task CloseMayOverrideAsync()
        {
            DisposeAudioGraph();
            await Task.CompletedTask; // avoid the warning...
        }
        private void DisposeAudioGraph()
        {
            if (_audioGraph != null)
            {
                _audioGraph.UnrecoverableErrorOccurred -= OnGraph_UnrecoverableErrorOccurred;
                _audioGraph.Stop();
            }
            _audioGraph?.Dispose(); // Disposes the AudioGraph and its nodes.
            _audioGraph = null;
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
            _outputDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
            if (_outputDevices == null || _outputDevices.Count < 1)
            {
                return "AudioGraph Creation Error: no output devices found";
            }

            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;
            settings.PrimaryRenderDevice = _outputDevices[0];

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

            if (result.Status != AudioGraphCreationStatus.Success)
            {
                // Cannot create graph
                return string.Format("AudioGraph Creation Error because {0}", result.Status.ToString());
            }

            _audioGraph = result.Graph;

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

            // Because we are using lowest latency setting, we need to handle device disconnection errors
            _audioGraph.UnrecoverableErrorOccurred += OnGraph_UnrecoverableErrorOccurred;
            return string.Empty;
        }

        private async void OnGraph_UnrecoverableErrorOccurred(AudioGraph sender, AudioGraphUnrecoverableErrorOccurredEventArgs args)
        {
            // Recreate the graph and all nodes when this happens
            //sender.Dispose();
            DisposeAudioGraph();

            _messageWriter.LastMessage = args.Error.ToString();
            // Re-query for devices
            string errorMessage = await CreateAudioGraphAsync().ConfigureAwait(false);
        }
        /// <summary>
        /// Required before starting recording
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private async Task<string> SetFileAsync(StorageFile file)
        {
            // File can be null if cancel is hit in the file picker
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
            switch (file.FileType.ToString().ToLowerInvariant())
            {
                case ".wma":
                    return MediaEncodingProfile.CreateWma(AudioEncodingQuality.High);
                case ".mp3":
                    return MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High);
                case ".wav":
                    return MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                default:
                    throw new ArgumentException("AudioRecorder.CreateMediaEncodingProfile() : wrong media encoding profile");
            }
        }
        #endregion init properties before recording

        #region record
        public Task RecordStartAsync()
        {
            return RunFunctionWhileOpenAsyncA(delegate
            {
                _messageWriter.LastMessage = "Recording...";
                _audioGraph.Start();
            });
        }
        public Task<bool> RecordStopAsync()
        {
            return RunFunctionWhileOpenAsyncTB(async delegate
            {
                // Good idea to stop the graph to avoid data loss
                _audioGraph.Stop();

                TranscodeFailureReason finalizeResult = await _fileOutputNode.FinalizeAsync();
                if (finalizeResult != TranscodeFailureReason.None)
                {
                    // Finalization of file failed. Check result code to see why
                    _messageWriter.LastMessage = string.Format("Finalization of file failed because {0}", finalizeResult.ToString());
                    // Logger.Add_TPL(string.Format("Finalization of file failed because {0}", finalizeResult.ToString()), Logger.ForegroundLogFilename);
                    return false;
                }
                _messageWriter.LastMessage = string.Empty;
                return true;
            });
        }
        #endregion record
    }

    public interface IMessageWriter
    {
        string LastMessage { get; set; }
    }

    public interface IAudioFileGetter
    {
        StorageFile GetAudioFile();
    }
}
