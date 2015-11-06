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
//        private SemaphoreSlimSafeRelease _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        //private bool _isOpen = false;
        //public bool IsOpen { get { return _isOpen; } private set { _isOpen = value; RaisePropertyChanged_UI(); } }

        private AudioGraph _audioGraph = null;
        private AudioFileOutputNode _fileOutputNode;
        private AudioDeviceOutputNode _deviceOutputNode;
        private AudioDeviceInputNode _deviceInputNode;
        private DeviceInformationCollection _outputDevices;

        public AudioRecorder() { }

        public async Task<bool> OpenAsync(StorageFile file)
        {
            bool isOk = await base.OpenAsync().ConfigureAwait(false);
            isOk = isOk && await RunFunctionWhileOpenAsyncTB(delegate
            {
                return SetFile2Async(file);
            });
            return isOk;
        }
        protected override async Task OpenMayOverrideAsync()
        {
            await PopulateDeviceList().ConfigureAwait(false);
            string errorMessage = await CreateAudioGraphAsync();
        }
        protected override async Task CloseMayOverrideAsync()
        {
            if (_audioGraph != null)
            {
                _audioGraph.UnrecoverableErrorOccurred -= OnGraph_UnrecoverableErrorOccurred;
                _audioGraph.Stop();
            }
            _audioGraph?.Dispose();
            _audioGraph = null;

            _fileOutputNode?.Dispose();
            _fileOutputNode = null;

            _deviceInputNode?.Dispose();
            _deviceInputNode = null;

            _deviceOutputNode?.Dispose();
            _deviceOutputNode = null;

            await Task.CompletedTask; // avoid the warning...
        }
        private async Task<string> CreateAudioGraphAsync()
        {
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
            sender.Dispose();
            // Re-query for devices
            await PopulateDeviceList();
        }

        public Task<bool> SetFileAsync(StorageFile file)
        {
            return RunFunctionWhileOpenAsyncTB(delegate 
            {
                return SetFile2Async(file);
            });
        }

        private async Task<bool> SetFile2Async(StorageFile file)
        {
            // File can be null if cancel is hit in the file picker
            if (file == null)
            {
                Logger.Add_TPL("file is empty", Logger.ForegroundLogFilename);
                return false;
            }

            MediaEncodingProfile fileProfile = CreateMediaEncodingProfile(file);

            // Operate node at the graph format, but save file at the specified format
            CreateAudioFileOutputNodeResult fileOutputNodeResult = await _audioGraph.CreateFileOutputNodeAsync(file, fileProfile);

            if (fileOutputNodeResult.Status != AudioFileNodeCreationStatus.Success)
            {
                // FileOutputNode creation failed
                Logger.Add_TPL(string.Format("Cannot create output file because {0}", fileOutputNodeResult.Status.ToString()), Logger.ForegroundLogFilename);
                return false;
            }

            _fileOutputNode = fileOutputNodeResult.FileOutputNode;

            // Connect the input node to both output nodes
            _deviceInputNode.AddOutgoingConnection(_fileOutputNode);
            _deviceInputNode.AddOutgoingConnection(_deviceOutputNode);

            return true;
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
                    throw new ArgumentException();
            }
        }
        private async Task PopulateDeviceList()
        {
            _outputDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
        }

        public Task RecordStartAsync()
        {
            return RunFunctionWhileOpenAsyncA(delegate 
            {
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
                    Logger.Add_TPL(string.Format("Finalization of file failed because {0}", finalizeResult.ToString()), Logger.ForegroundLogFilename);
                    return false;
                }

                return true;
            });
        }
    }
}
