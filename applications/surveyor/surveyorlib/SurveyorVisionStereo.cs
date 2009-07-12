/*
    Stereo vision for Surveyor robots
    Copyright (C) 2008 Bob Mottram
    fuzzgun@gmail.com

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Xml;
using System.IO;
using System.Threading;
using System.Drawing;
using sluggish.utilities;

namespace surveyor.vision
{
    public class SurveyorVisionStereo : BaseVisionStereo
    {
        private SurveyorVisionClient[] camera;
        private string host;
        private int[] port_number;		
		
        #region "constructors"
        
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="host">host name or IP address</param>
        /// <param name="port_number_left">port number for the left camera</param>
        /// <param name="port_number_right">port number for the right camera</param>
        /// <param name="broadcast_port">port number on which to broadcast stereo feature data to other applications</param>
        /// <param name="fps">ideal frames per second</param>
        public SurveyorVisionStereo(
		    string host,
            int port_number_left,
            int port_number_right,
            int broadcast_port,
            float fps) : base(broadcast_port, fps)
        {
            device_name = "Surveyor stereo camera";
            this.host = host;
            
            camera = new SurveyorVisionClient[2];
            for (int cam = 0; cam < 2; cam++)
            {
                camera[cam] = new SurveyorVisionClient();
                camera[cam].grab_mode = SurveyorVisionClient.GRAB_MULTI_CAMERA;
				camera[cam].cam_index = cam;
            }
            
            port_number = new int[2];
            port_number[0] = port_number_left;
            port_number[1] = port_number_right;            
        }
        
        #endregion
		
		#region "enabling and disabling embedded stereo"
		
		public void EnableEmbeddedStereo()
		{
			for (int cam = 0; cam < 2; cam++)
			{
			    camera[cam].EnableEmbeddedStereo();
			    camera[cam].Embedded = true;
			}
		}
		
		public void DisableEmbeddedStereo()
		{
			for (int cam = 0; cam < 2; cam++)
			{		
			    camera[cam].DisableEmbeddedStereo();
			    camera[cam].Embedded = false;
			}
		}
		
		#endregion
		
		#region "state machine"
		
		private Bitmap bmp_state_left, bmp_state_right;
		private int svs_state, prev_svs_state = -1;
		public const int SVS_STATE_GRAB_IMAGES = 0;
		public const int SVS_STATE_RECEIVE_IMAGES = 1;
		public const int SVS_STATE_PROCESS_IMAGES = 2;
		private DateTime svs_state_last;
				
		public void update_state()
		{
			switch(svs_state)
			{
			    case SVS_STATE_GRAB_IMAGES:
			    {
				    svs_state_last = DateTime.Now;
				
	                // pause or resume grabbing frames from the cameras
				    bool is_paused = false;
	                if (correspondence != null)
	                {
	                    if ((!UpdateWhenClientsConnected) ||
	                        ((UpdateWhenClientsConnected) && (correspondence.GetNoOfClients() > 0)))
	                        is_paused = false;
	                    else
	                        is_paused = true;
	                }
				
				    if (!is_paused)
				    {
				        // request images                        
				        camera[0].RequestFrame();
				        camera[1].RequestFrame();					    
				        svs_state = SVS_STATE_RECEIVE_IMAGES;
				    }
				    break;
			    }
			    case SVS_STATE_RECEIVE_IMAGES:
			    {
				    // both frames arrived
			        if ((camera[0].frame_arrived) &&
			            (camera[1].frame_arrived))
				    {
                        bmp_state_left = (Bitmap)camera[0].current_frame;
                        bmp_state_right = (Bitmap)camera[1].current_frame;
	                    if ((bmp_state_left != null) && 
	                        (bmp_state_right != null))
					    {
						    // proceed to process the images
						    svs_state = SVS_STATE_PROCESS_IMAGES;
				    	}
					    else
					    {
						    // images were invalid - try again
						    svs_state = SVS_STATE_GRAB_IMAGES;
					    }
				    }
				    else
				    {
			            int timeout_mS = 100; //(int)(1000 / fps);
                        TimeSpan diff = DateTime.Now.Subtract(svs_state_last);
					    if (diff.TotalMilliseconds > timeout_mS)
					    {
						    // timed out - request images again
						    //Console.WriteLine("Timed out waiting for images");
						    if (!camera[0].frame_arrived) 
							    camera[0].StopSend();
						    if (!camera[1].frame_arrived) 
							    camera[1].StopSend();
						    svs_state = SVS_STATE_GRAB_IMAGES;
					    }
				    }
				    
				    break;
			    }
			    case SVS_STATE_PROCESS_IMAGES:
			    {
				    if (bmp_state_left != null)
				    {
		                image_width = bmp_state_left.Width;
				        if (bmp_state_left != null)
				        {
		                    image_height = bmp_state_left.Height;
	
		                    //busy_processing = true;
		                    if (calibration_pattern != null)
		                    {
		                        if (!show_left_image)
		                            SurveyorCalibration.DetectDots(bmp_state_left, ref edge_detector, calibration_survey[0], ref edges, ref linked_dots, ref grid, ref grid_diff, ref rectified[0]);
		                        else
		                            SurveyorCalibration.DetectDots(bmp_state_right, ref edge_detector, calibration_survey[1], ref edges, ref linked_dots, ref grid, ref grid_diff, ref rectified[1]);
		                    }
		
		                    RectifyImages(bmp_state_left, bmp_state_right);
		                                         
		                    Process(bmp_state_left, bmp_state_right);
		                    
		                    // save images to file
		                    if (Record)
		                    {
		                        RecordFrameNumber++;
		                        bmp_state_left.Save("raw0_" + RecordFrameNumber.ToString() + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
		                        bmp_state_right.Save("raw1_" + RecordFrameNumber.ToString() + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
		                        if ((rectified[0] != null) && (rectified[0] != null))
		                        {
		                            rectified[0].Save("rectified0_" + RecordFrameNumber.ToString() + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
		                            rectified[1].Save("rectified1_" + RecordFrameNumber.ToString() + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
		                        }
		                    }
					    }
				    }
				
				    svs_state = SVS_STATE_GRAB_IMAGES;
				    break;
			    }
			}
			if (prev_svs_state != svs_state)
			{
				string msg = "";
				switch(svs_state)
				{
				    case SVS_STATE_GRAB_IMAGES: { msg = "grab images"; break; }
				    case SVS_STATE_RECEIVE_IMAGES: { msg = "images received"; break; }
				    case SVS_STATE_PROCESS_IMAGES: { msg = "process images"; break; }
				}
				//if (Verbose) Console.WriteLine(msg);
			}
			prev_svs_state = svs_state;
		}
		
		#endregion
        
        #region "callbacks"

        protected bool busy_processing;

        /// <summary>
        /// both images have arrived and are awaiting processing
        /// </summary>
        /// <param name="state"></param>
        private void FrameGrabCallbackMulti(object state)
        {
			if (camera[0].Streaming)
			{
                sync_thread = new Thread(new ThreadStart(grab_frames.Execute));
                sync_thread.Priority = ThreadPriority.Normal;
                sync_thread.Start(); 			
			}
        }

        #endregion
        
        #region "starting and stopping"

        private SurveyorVisionThreadGrabFrameMulti grab_frames;
        
        public override void Run()
        {
            bool cameras_started = true;
            
            // start running the cameras
            for (int cam = 0; cam < 2; cam++)
            {
                camera[cam].fps = fps;
                camera[cam].Start(host, port_number[cam]);
                if (camera[cam].Running)
                {
                    camera[cam].StartStream();
                }
                else
                {
                    cameras_started = false;
                    break;
                }
            }
            
            if (cameras_started)
            {
                // create a thread to send the master pulse
                grab_frames = new SurveyorVisionThreadGrabFrameMulti(new WaitCallback(FrameGrabCallbackMulti), this);        
                sync_thread = new Thread(new ThreadStart(grab_frames.Execute));
                sync_thread.Priority = ThreadPriority.Normal;
                sync_thread.Start();   
                Running = true;
                Console.WriteLine("Stereo camera active on " + host);
            }
			else
			{
				Console.WriteLine("Cameras not started");
			}
        }

        public override void Stop()
        {
            if (Running)
            {
                for (int cam = 0; cam < 2; cam++)
                {
                    camera[cam].StopStream();
                    camera[cam].Stop();
                }
                if (sync_thread != null) sync_thread.Abort();
            }
        }
        
        #endregion
        
        #region "getters and setters"
        
        public override void SetFramesPerSecond(int fps)
        {
            this.fps = fps;
            camera[0].fps = fps;
            camera[1].fps = fps;
        }
        
        #endregion
        
        #region "displaying images"
       
        protected override void DisplayImages(Bitmap left_image, Bitmap right_image)
        {
        }

        #endregion
       
        #region "process images"

        public override void Process(Bitmap left_image, Bitmap right_image)
        {        
            DisplayImages(left_image, right_image);
            StereoCorrespondence();
        }
        
        #endregion
        
    }
}
