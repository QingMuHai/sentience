/*
    Test GUI for the Surveyor stereo camera
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
using System.IO;
using System.Drawing;
using System.Threading;
using Gtk;
using surveyor.vision;
using sluggish.utilities;
using sluggish.utilities.gtk;

public partial class MainWindow: Gtk.Window
{
    int image_width = 320;
    int image_height = 240;
    string stereo_camera_IP = "169.254.0.10";
    string calibration_filename = "calibration.xml";
         
    public SurveyorVisionStereoGtk stereo_camera;
    
    public MainWindow (): base (Gtk.WindowType.Toplevel)
    {
        Build ();

        byte[] img = new byte[image_width * image_height * 3];
        Bitmap left_bmp = new Bitmap(image_width, image_height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        Bitmap right_bmp = new Bitmap(image_width, image_height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        BitmapArrayConversions.updatebitmap_unsafe(img, left_bmp);
        BitmapArrayConversions.updatebitmap_unsafe(img, right_bmp);
        GtkBitmap.setBitmap(left_bmp, leftimage);
        GtkBitmap.setBitmap(right_bmp, rightimage);
        
        stereo_camera = new SurveyorVisionStereoGtk(stereo_camera_IP, 10001, 10002, 10010);
        stereo_camera.window = this;
        stereo_camera.display_image[0] = leftimage;
        stereo_camera.display_image[1] = rightimage;
        stereo_camera.Load(calibration_filename);
        stereo_camera.Run();
    }	
    
    private void CloseForm()
    {
        stereo_camera.Stop();
        Application.Quit ();
    }
    
    protected void OnDeleteEvent (object sender, DeleteEventArgs a)
    {
        CloseForm();
        a.RetVal = true;
    }

    protected virtual void OnExitActionActivated (object sender, System.EventArgs e)
    {
        CloseForm();
    }

    protected virtual void OnRecordImagesActionActivated (object sender, System.EventArgs e)
    {
        RecordImagesAction.Active = !RecordImagesAction.Active;
        stereo_camera.Record = RecordImagesAction.Active;
    }

    protected virtual void OnChkRecordClicked (object sender, System.EventArgs e)
    {
        stereo_camera.Record = !stereo_camera.Record;
        chkRecord.Active = stereo_camera.Record; 
    }

    private void ShowDotPattern(Gtk.Image dest_img)
    {
        stereo_camera.calibration_pattern = SurveyorCalibration.CreateDotPattern(image_width, image_height, SurveyorCalibration.dots_across, SurveyorCalibration.dot_radius_percent);
        GtkBitmap.setBitmap(stereo_camera.calibration_pattern, dest_img);
    }

    private void Calibrate(bool Active)
    {
        Gtk.Image dest_img = null;
        int window_index = 0;
        if (stereo_camera.show_left_image)
        {
            dest_img = leftimage;
            window_index = 0;
        }
        else
        {
            dest_img = rightimage;
            window_index = 1;
        }
    
        if (Active)
        {            
            ShowDotPattern(dest_img);
            stereo_camera.display_image[window_index] = dest_img;
            //stereo_camera.display_type = SurveyorVisionStereo.DISPLAY_CALIBRATION_DIFF;
            stereo_camera.display_type = SurveyorVisionStereo.DISPLAY_RECTIFIED;
            //stereo_camera.display_type = SurveyorVisionStereo.DISPLAY_DIFFERENCE;
            stereo_camera.ResetCalibration(1 - window_index);
			
			if (!stereo_camera.show_left_image)
			{
		        chkCalibrateRight.Active = false;
			}
			else
			{
		        chkCalibrateLeft.Active = false;
			}
        }
        else
        {
            stereo_camera.calibration_pattern = null;
            stereo_camera.display_image[0] = leftimage;
            stereo_camera.display_image[1] = rightimage;
            stereo_camera.display_type = SurveyorVisionStereo.DISPLAY_RAW;
        }
				
    }

    protected virtual void OnChkCalibrateLeftClicked (object sender, System.EventArgs e)
    {
        stereo_camera.show_left_image = false;
        Calibrate(chkCalibrateLeft.Active);
    }

    protected virtual void OnChkCalibrateRightClicked (object sender, System.EventArgs e)
    {
        stereo_camera.show_left_image = true;
        Calibrate(chkCalibrateRight.Active);
    }   

    private void ShowMessage(string message_str)
    {
        Console.WriteLine(message_str);
        /*
        Gtk.MessageDialog md = 
            new MessageDialog (this,
                               DialogFlags.DestroyWithParent,
    	                       MessageType.Info, 
                               ButtonsType.Close, 
                               message_str);
        this.GdkWindow.ProcessUpdates(true);
        md.Run();
        md.Destroy();
        */
    }

    protected virtual void OnCmdCalibrateAlignmentClicked (object sender, System.EventArgs e)
    {
        if (stereo_camera.CalibrateCameraAlignment())
        {
            stereo_camera.Save(calibration_filename);
            
            ShowMessage("Calibration complete");
        }
        else
        {
            ShowMessage("Please individually calibrate left and right cameras before the calibrating the alignment");
        }
    }

    protected virtual void OnCmdSimpleStereoClicked (object sender, System.EventArgs e)
    {
        stereo_camera.stereo_algorithm_type = StereoVision.SIMPLE;
    }

    protected virtual void OnCmdDenseStereoClicked (object sender, System.EventArgs e)
    {
        stereo_camera.stereo_algorithm_type = StereoVision.DENSE;
    }

    private void SaveCalibrationFile()
    {
        FileChooserDialog fc = 
            new FileChooserDialog("Save calibration file",
                                  this,
                                  FileChooserAction.Save,
                                  "Cancel", ResponseType.Cancel,
                                  "Save", ResponseType.Ok);

        int resp = fc.Run();
        fc.Filter = new Gtk.FileFilter();
        fc.Filter.AddPattern("Xml files (*.xml)|*.xml");
        fc.Hide();
        if (resp == (int)ResponseType.Ok)
        {
            if ((fc.Filename != null) && (fc.Filename != ""))
            {
                if (calibration_filename != fc.Filename)
                {
                    if (File.Exists(fc.Filename)) File.Delete(fc.Filename);
                    File.Copy(calibration_filename, fc.Filename);
                }
            }
        }
        fc.Destroy();
    }

    private void SaveCalibrationImage()
    {
        FileChooserDialog fc = 
            new FileChooserDialog("Save calibration image",
                                  this,
                                  FileChooserAction.Save,
                                  "Cancel", ResponseType.Cancel,
                                  "Save", ResponseType.Ok);

        fc.Filter = new Gtk.FileFilter();
        fc.Filter.AddPattern("Bitmap files (*.bmp)|*.bmp");
        int resp = fc.Run();
        fc.Hide();
        if (resp == (int)ResponseType.Ok)
        {
            if ((fc.Filename != null) && (fc.Filename != ""))
            {
                Bitmap bmp = SurveyorCalibration.CreateDotPattern(1000, image_height * 1000 / image_width, SurveyorCalibration.dots_across, SurveyorCalibration.dot_radius_percent);
                bmp.Save(fc.Filename, System.Drawing.Imaging.ImageFormat.Bmp);
            }
        }
        fc.Destroy();
    }


    protected virtual void OnCmdSaveCalibrationClicked (object sender, System.EventArgs e)
    {
        SaveCalibrationFile();
    }

    protected virtual void OnCmdSaveCalibrationImageClicked (object sender, System.EventArgs e)
    {
        SaveCalibrationImage();
    }

}