using System;
using System.IO;//Path
using System.Windows.Forms;//FolderBrowserDialog
using Vlc.DotNet.Core;//VlcMediaPlayer
using Vlc.DotNet.Core.Interops.Signatures;//MediaStates
using System.Threading;//Timers

namespace ConsoleApp1
{
    class StreamRecorder
    {
        private VlcMediaPlayer _vlcMediaPlayer;
        private string _streamUrl;
        private bool _snapshotRecording;
        private bool _videoRecording;
        private string _snapshotRecordingPath;
        private string _videoRecordingPath;
        private int _snapshotInterval;
        private int _videoAutosaveInterval;

        private const string _fileExtensionSnapshot = "png";//png or jpg
        private const string _fileExtensionVideo = "mp4";//mp4 or avi

        private System.Timers.Timer _timSnapRecorder = new System.Timers.Timer();
        private System.Timers.Timer _timVideoRecorder = new System.Timers.Timer();

        /// <summary>
        /// Create a stream recorder.
        /// </summary>
        /// <param name="streamUrl">Url of the stream to record.</param>
        /// <param name="snapshotRecording">Precise if you want to record using snapshots.</param>
        /// <param name="videoRecording">Precise if you want to record using videos.</param>
        /// <param name="snapshotRecordingPath">Precise the path of the folder you want your snapshot recordings to be saved.</param>
        /// <param name="videoRecordingPath">Precise the path of the folder you want your video recordings to be saved.</param>
        /// <param name="snapshotInterval">Precise the number of seconds between each snapshot taken.</param>
        /// <param name="recordingAutosaveInterval">Precise the number of minutes between each video autosave.</param>
        public StreamRecorder(string streamUrl, bool snapshotRecording, bool videoRecording,
                                string snapshotRecordingPath = ".", string videoRecordingPath = ".",
                                int snapshotInterval = 5, int videoAutosaveInterval = 15)
        {
            //--->ADD VERIFS REGEX HERE<---
            _streamUrl = streamUrl;

            //Verify vlc libs
            DirectoryInfo di;

            //Get vlc libs
            if (IntPtr.Size == 4)
                di = new DirectoryInfo(Path.GetFullPath(@".\libvlc\win-x86\"));
            else
                di = new DirectoryInfo(Path.GetFullPath(@".\libvlc\win-x64\"));

            if (!di.Exists)
            {
                //Find vlc libs folder
                var folderBrowserDialog = new FolderBrowserDialog
                {
                    Description = "Select Vlc libraries folder.",
                    RootFolder = Environment.SpecialFolder.Desktop,
                    ShowNewFolderButton = true
                };
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    di = new DirectoryInfo(folderBrowserDialog.SelectedPath);
                }
            }

            //Create player
            _vlcMediaPlayer = new VlcMediaPlayer(di);
            _vlcMediaPlayer.EncounteredError += new EventHandler<VlcMediaPlayerEncounteredErrorEventArgs>(VlcMediaPlayer_EncounteredError);
            _vlcMediaPlayer.EndReached += new EventHandler<VlcMediaPlayerEndReachedEventArgs>(VlcMediaPlayer_EndReached);

            //If we want snapshots every x sec
            if (snapshotRecording)
            {
                //Keep important stuff
                _snapshotRecording = snapshotRecording;
                _snapshotRecordingPath = snapshotRecordingPath;
                _snapshotInterval = snapshotInterval * 1000;

                //Set timer
                _timSnapRecorder.Interval = _snapshotInterval;
                _timSnapRecorder.Elapsed += new System.Timers.ElapsedEventHandler(TimSnapRecorder_Elapsed);
                _timSnapRecorder.AutoReset = true;

                //Launch the player to get pictures from NO RECORDING
                if (!videoRecording)
                    _vlcMediaPlayer.Play(streamUrl, new string[] { ":network-caching=100" });

                _timSnapRecorder.Start();
            }

            //If we want full recordings every x min
            if (videoRecording)
            {
                //Keep important stuff
                _videoRecording = videoRecording;
                _videoRecordingPath = videoRecordingPath;
                _videoAutosaveInterval = videoAutosaveInterval * 1000 * 60;

                //Set timer
                _timVideoRecorder.Interval = _videoAutosaveInterval;
                _timVideoRecorder.Elapsed += new System.Timers.ElapsedEventHandler(TimVideoRecorder_Elapsed);
                _timVideoRecorder.AutoReset = true;
                _timVideoRecorder.Start();

                //Launch the player to get recording AND/OR pictures from
                StartVideoRecording(_videoRecordingPath);
            }
        }

        private void TimSnapRecorder_Elapsed(object sender, EventArgs e)
        {
            if (_snapshotRecording)
                TakeSnapshot(_snapshotRecordingPath);
            else
                _timSnapRecorder.Stop();
        }

        private void TimVideoRecorder_Elapsed(object sender, EventArgs e)
        {
            if (_videoRecording)
            {
                _vlcMediaPlayer.Stop();
                StartVideoRecording(_videoRecordingPath);
            }
            else
                _timVideoRecorder.Stop();
        }

        private bool TakeSnapshot(string path)
        {
            bool snapTaken = false;

            path += "\\"; //ensure that it's a folder
            string savePath = string.Format("{0}{1:yyyy_MM_dd_HH-mm-ss-fff}.{2}", path, DateTime.Now, _fileExtensionSnapshot);
            try
            {
                //ensure that the wanted directory exists
                Directory.CreateDirectory(path);

                //ensure that there isn't a second image saved at the same time (with the same file name)
                if (!File.Exists(savePath))
                {
                    _vlcMediaPlayer.TakeSnapshot(new FileInfo(savePath));
                    snapTaken = true;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Snapshot recording error.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return snapTaken;
        }

        private bool StartVideoRecording(string path)
        {
            bool videoRecordingStarted = false;

            path += "\\"; //ensure that it's a folder
            string savePath = string.Format("{0}{1:yyyy_MM_dd_HH-mm-ss}.{2}", path, DateTime.Now, _fileExtensionVideo);
            try
            {
                //ensure that the wanted directory exists
                Directory.CreateDirectory(path);

                //ensure that there isn't a second recording saved at the same time (with the same file name)
                if (!File.Exists(savePath))
                {
                    string[] options = {":network-caching=100",
                                        ":sout=#duplicate{dst=std{access=file,mux=" + _fileExtensionVideo + ",dst='" + savePath + "'},dst=display}"
                    };
                    _vlcMediaPlayer.Play(_streamUrl, options);

                    //wait for opening
                    while (_vlcMediaPlayer.State == MediaStates.NothingSpecial) { }
                    while (_vlcMediaPlayer.State == MediaStates.Opening) { }

                    //verify if not playing
                    if (_vlcMediaPlayer.State != MediaStates.Playing)
                    {
                        _vlcMediaPlayer.Stop();
                        MessageBox.Show("Too many players.", "Recording error.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //delete the corrupted file from the video folder
                        File.Delete(savePath);
                    }
                    else//everything is fine
                    {
                        videoRecordingStarted = true;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Recording error.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return videoRecordingStarted;
        }

        private void VlcMediaPlayer_EncounteredError(object sender, Vlc.DotNet.Core.VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            if (_snapshotRecording || _videoRecording)
                ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadAutoRestartPlaying));
        }

        private void VlcMediaPlayer_EndReached(object sender, Vlc.DotNet.Core.VlcMediaPlayerEndReachedEventArgs e)
        {
            if (_snapshotRecording || _videoRecording)
                ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadAutoRestartPlaying));
        }

        private void ThreadAutoRestartPlaying(object o)
        {
            try
            {
                _vlcMediaPlayer.Stop();
                if (_videoRecording)
                    StartVideoRecording(_videoRecordingPath);
                else
                {
                    if (_snapshotRecording)
                        _vlcMediaPlayer.Play(_streamUrl);
                }
            }
            catch { }
        }

    }
}
