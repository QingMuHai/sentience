/*
    Fishfood stereo camera calibration utility
    Copyright (C) 2000-2008 Bob Mottram
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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace sentience.calibration
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            Test();
            
            
            // extract command line parameters
            ArrayList parameters = commandline.ParseCommandLineParameters(args, "-", GetValidParameters());

            string help = commandline.GetParameterValue("help", parameters);
            if (help != "")
            {
                ShowHelp();
            }
            else
            {
                string directory = commandline.GetParameterValue("dir", parameters);
                if (directory == "")
                {
                    Console.WriteLine("Please specify a directory for the calibration files");
                }
                else
                {
                    if (!Directory.Exists(directory))
                    {
                        Console.WriteLine("The directory " + directory + " was not found");
                    }
                    else
                    {
                        string baseline_str = commandline.GetParameterValue("baseline", parameters);
                        if (baseline_str == "")
                        {
                            Console.WriteLine("Please specify a baseline distance for the camera separation in millimetres");
                        }
                        else
                        {
                            int baseline_mm = Convert.ToInt32(baseline_str);

                            string dotdist_str = commandline.GetParameterValue("dotdist", parameters);
                            if (dotdist_str == "")
                            {
                                Console.WriteLine("Please specify the horizontal distance to the calibration pattern dot in millimetres");
                            }
                            else
                            {
                                int dotdist_mm = Convert.ToInt32(dotdist_str);

                                string height_str = commandline.GetParameterValue("height", parameters);
                                if (height_str == "")
                                {
                                    Console.WriteLine("Please specify the height above the calibration pattern in millimetres");
                                }
                                else
                                {
                                    int height_mm = Convert.ToInt32(height_str);
                                    
                                    string fov_str = commandline.GetParameterValue("fov", parameters);
                                    if (fov_str == "")
                                    {
                                        Console.WriteLine("Please specify the camera horizontal field of view in degrees");
                                    }
                                    else
                                    {
                                        float fov_degrees = Convert.ToSingle(fov_str);

                                        float dot_spacing_mm = 50;
                                        string dot_spacing_str = commandline.GetParameterValue("spacing", parameters);
                                        if (dot_spacing_str == "")
                                        {
                                            Console.WriteLine("Please specify the calibration pattern dot spacing in millimetres");
                                        }
                                        else
                                        {
                                            dot_spacing_mm = Convert.ToSingle(dot_spacing_str);
                                         
                                         
                                            
                                        }                                    
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        #region "calibration"
        
        private static hypergraph Detect(string filename, 
                                         float fov_degrees, float dist_to_centre_dot_mm, float dot_spacing_mm,
                                         ref double centre_of_distortion_x, ref double centre_of_distortion_y,
                                         ref polynomial lens_distortion_curve,
                                         ref double camera_rotation, ref double scale)
        {
            int image_width = 0, image_height = 0;
            hypergraph dots = DetectDots(filename, ref image_width, ref image_height);

            // how far is the centre dot from the centre of the image ?
            float centre_dot_horizontal_distance = float.MaxValue;
            float centre_dot_vertical_distance = 0;
            GetCentreDotDisplacement(image_width, image_height, dots, 
                                     ref centre_dot_horizontal_distance,
                                     ref centre_dot_vertical_distance);
                                     
            if ((Math.Abs(centre_dot_horizontal_distance) > image_width / 10) ||
                (Math.Abs(centre_dot_vertical_distance) > image_height / 5))
            {
                Console.WriteLine("The calibration pattern centre dot was not detected or is too far away from the centre of the image");
            }
            else
            {
                // square around the centre dot
                List<calibrationDot> centre_dots = null;
                polygon2D centre_square = GetCentreSquare(dots, ref centre_dots);

                ShowSquare(filename, centre_square, "centre_square.jpg");
                
                float ideal_centre_square_angular_diameter_degrees = (2.0f * (float)Math.Atan(0.5f * dot_spacing_mm / dist_to_centre_dot_mm)) * 180 / (float)Math.PI;
                float ideal_centre_square_dimension_pixels = ideal_centre_square_angular_diameter_degrees * image_width / fov_degrees;
                //scale = ((centre_square.getSideLength(0) + centre_square.getSideLength(2)) * 0.5f) / ideal_centre_square_dimension_pixels;
                scale = 1;
                
                Console.WriteLine("centre_square ideal_dimension: " + ideal_centre_square_dimension_pixels.ToString());
                Console.WriteLine("centre_square actual_dimension: " + ((centre_square.getSideLength(0) + centre_square.getSideLength(2)) * 0.5f).ToString());
                Console.WriteLine("scale: " + scale.ToString());

                List<calibrationDot> search_regions = new List<calibrationDot>();
                double horizontal_dx = centre_dots[1].x - centre_dots[0].x;
                double horizontal_dy = centre_dots[1].y - centre_dots[0].y;
                double vertical_dx = centre_dots[3].x - centre_dots[0].x;
                double vertical_dy = centre_dots[3].y - centre_dots[0].y;
                for (int i = 0; i < centre_dots.Count; i++)
                    LinkDots(dots, centre_dots[i], horizontal_dx, horizontal_dy, vertical_dx, vertical_dy, 0, search_regions);

                Console.WriteLine(dots.Links.Count.ToString() + " links created");

                ApplyGrid(dots, centre_dots);

                calibrationDot[,] grid = CreateGrid(dots);


                int grid_offset_x = 0, grid_offset_y = 0;
                List<calibrationDot> corners = new List<calibrationDot>();
                grid2D overlay_grid = OverlayIdealGrid(grid, corners, ref grid_offset_x, ref grid_offset_y);
                if (overlay_grid != null)
                {
                    ShowDots(corners, filename, "corners.jpg");
                    
                    /*
                    List<calibrationDot> grid_dots = new List<calibrationDot>();
                    for (int x = 0; x < grid.GetLength(0); x++)
                        for (int y = 0; y < grid.GetLength(1); y++)
                            if (grid[x, y] != null)
                                grid_dots.Add(grid[x, y]);
                    ShowDots(grid_dots, filename, "grid_dots.jpg");
                    */
                    
                    List<List<double>> lines = CreateLines(dots, grid);

                    lens_distortion_curve = null;
                    calibrationDot centre_of_distortion = null;
                    List<List<double>> best_rectified_lines = null;
                    DetectLensDistortion(image_width, image_height, grid, overlay_grid, lines,
                                         ref lens_distortion_curve, ref centre_of_distortion,
                                         ref best_rectified_lines,
                                         grid_offset_x, grid_offset_y);
                                         
                    if (lens_distortion_curve != null)
                    {
                        ShowDistortionCurve(lens_distortion_curve, "curve_fit.jpg");
                        ShowCurveVariance(lens_distortion_curve, "curve_variance.jpg");
                        ShowLensDistortion(image_width, image_height, centre_of_distortion, lens_distortion_curve, fov_degrees, 10, "lens_distortion.jpg");

                        // detect the camera rotation
                        List<List<double>> rectified_centre_line = null;
                        camera_rotation = DetectCameraRotation(image_width, image_height,
                                                               grid, lens_distortion_curve, centre_of_distortion,
                                                               ref rectified_centre_line);
                        double rotation_degrees = camera_rotation / Math.PI * 180;
                        
                        //scale = 1;

                        int[] calibration_map = null;
                        int[,,] calibration_map_inverse = null;
                        updateCalibrationMap(image_width, image_height,
                                             lens_distortion_curve,
                                             (float)scale, (float)camera_rotation,
                                             (float)centre_of_distortion.x, (float)centre_of_distortion.y,
                                             ref calibration_map,
                                             ref calibration_map_inverse);

                        Rectify(filename, calibration_map, "rectified.jpg");
                            
                        if (rectified_centre_line != null)
                            ShowLines(filename, rectified_centre_line, "rectified_centre_line.jpg");

                        if (best_rectified_lines != null)
                        {                        
                            ShowLines(filename, lines, "lines.jpg");
                            ShowLines(filename, best_rectified_lines, "rectified_lines.jpg");
                        }
                    }
                }

                ShowOverlayGrid(filename, overlay_grid, "grid_overlay.jpg");
                ShowOverlayGridPerimeter(filename, overlay_grid, "grid_overlay_perimeter.jpg");
                ShowGrid(filename, dots, search_regions, "grid.jpg");
                ShowGridCoordinates(filename, dots, "coordinates.jpg");
            
            }
            
            return (dots);
        }

        private static void Calibrate(string directory,
                                      int baseline_mm,
                                      int dotdist_mm,
                                      int height_mm,
                                      float fov_degrees,
                                      float dot_spacing_mm,
                                      string file_extension)
        {
            string[] filename = Directory.GetFiles(directory, "*." + file_extension);
            if (filename != null)
            {
                // two lists to store filenames for camera 0 and camera 1
                List<string>[] image_filename = new List<string>[2];
                image_filename[0] = new List<string>();
                image_filename[1] = new List<string>();
                
                // populate the lists
                for (int i = 0; i < filename.Length; i++)
                {
                    if (filename[i].StartsWith("raw0"))
                        image_filename[0].Add(filename[i]);
                    if (filename[i].StartsWith("raw1"))
                        image_filename[1].Add(filename[i]);
                }
                
                if (image_filename[0].Count == 0)
                {
                    Console.WriteLine("Did not find any calibration files.  Do they begin with raw0 or raw1 ?");
                }
                else
                {
                    if (image_filename[0].Count != image_filename[1].Count)
                    {
                        Console.WriteLine("There must be the same number of images from camera 0 and camera 1");
                    }
                    else
                    {
                        // distance to the centre dot
                        float dist_to_centre_dot_mm = (float)Math.Sqrt(dotdist_mm * dotdist_mm + height_mm * height_mm);
                    
                        // find dots within the images
                        polynomial[] distortion_curve = new polynomial[2];
                        double[] centre_x = new double[2];
                        double[] centre_y = new double[2];
                        double[] scale_factor = new double[2];
                        double[] rotation = new double[2];
                        
                        for (int cam = 0; cam < 2; cam++)
                        {
                            for (int i = 0; i < image_filename[cam].Count; i++)
                            {
                                double centre_of_distortion_x = 0;
                                double centre_of_distortion_y = 0;
                                polynomial lens_distortion_curve = null;
                                double camera_rotation = 0;
                                double scale = 0;
                                hypergraph dots =  
                                    Detect(image_filename[cam][i], fov_degrees, dist_to_centre_dot_mm, dot_spacing_mm,
                                    ref centre_of_distortion_x, ref centre_of_distortion_y,
                                    ref lens_distortion_curve,
                                    ref camera_rotation, ref scale);
                                    
                                if (lens_distortion_curve != null)
                                {
                                    bool update = false;
                                    if (distortion_curve[cam] == null)
                                    {
                                        update = true;
                                    }
                                    else
                                    {
                                        if (lens_distortion_curve.GetRMSerror() < distortion_curve[cam].GetRMSerror())
                                            update = true;
                                    }
                                    
                                    if (update)
                                    {
                                        // record the result with the smallest RMS error
                                        distortion_curve[cam] = lens_distortion_curve;
                                        centre_x[cam] = centre_of_distortion_x;
                                        centre_y[cam] = centre_of_distortion_y;
                                        rotation[cam] = camera_rotation;
                                        scale_factor[cam] = scale;
                                    }
                                }
                            }
                        }
                        
                        
                    }
                }
            }
            else
            {
                Console.WriteLine("No calibration " + file_extension + " images were found");
            }
        }
        
        #region "calculate the angular displacement of the centre dot"
        
        
        /// <summary>
        /// returns the horizontal and vertical distance of the centre dot from the centre of the image
        /// </summary>
        /// <param name="image_width">
        /// width of the image <see cref="System.Int32"/>
        /// </param>
        /// <param name="image_height">
        /// height of the image <see cref="System.Int32"/>
        /// </param>
        /// <param name="dots">
        /// detected dots <see cref="hypergraph"/>
        /// </param>
        /// <param name="horizontal_distance">
        /// horizontal distance from the image centre in pixels <see cref="System.Single"/>
        /// </param>
        /// <param name="vertical_distance">
        /// vertical distance from the image centre in pixels <see cref="System.Single"/>
        /// </param>
        private static void GetCentreDotDisplacement(int image_width, int image_height,
                                                     hypergraph dots,
                                                     ref float horizontal_distance,
                                                     ref float vertical_distance)
        {
            calibrationDot centre_dot = null;
            int i = 0;
            while ((i < dots.Nodes.Count) && (centre_dot == null))
            {
                calibrationDot dot = (calibrationDot)dots.Nodes[i];
                if (dot.centre)
                    centre_dot = dot;
                else
                    i++;
            }
            if (centre_dot != null)
            {
                horizontal_distance = (float)centre_dot.x - (image_width / 2.0f);
                vertical_distance = (float)centre_dot.y - (image_height / 2.0f);
            }
        }
        
        #endregion

        #region "creating calibration lookup tables, used to rectify whole images"

        protected static void rotatePoint(double x, double y, double ang,
                                          ref double rotated_x, ref double rotated_y)
        {
            double hyp;
            rotated_x = x;
            rotated_y = y;

            if (ang != 0)
            {
                hyp = Math.Sqrt((x * x) + (y * y));
                if (hyp > 0)
                {
                    double rot_angle = Math.Acos(y / hyp);
                    if (x < 0) rot_angle = (Math.PI * 2) - rot_angle;
                    double new_angle = ang + rot_angle;
                    rotated_x = hyp * Math.Sin(new_angle);
                    rotated_y = hyp * Math.Cos(new_angle);
                }
            }
        }

        /// <summary>
        /// update the calibration lookup table, which maps pixels
        /// in the rectified image into the original image
        /// </summary>
        /// <param name="image_width"></param>
        /// <param name="image_height"></param>
        /// <param name="curve">polynomial curve describing the lens distortion</param>
        /// <param name="scale"></param>
        /// <param name="rotation"></param>
        /// <param name="centre_of_distortion_x"></param>
        /// <param name="centre_of_distortion_y"></param>
        /// <param name="calibration_map"></param>
        /// <param name="calibration_map_inverse"></param>
        private static void updateCalibrationMap(int image_width,
                                                 int image_height,
                                                 polynomial curve,
                                                 float scale,
                                                 float rotation,
                                                 float centre_of_distortion_x,
                                                 float centre_of_distortion_y,
                                                 ref int[] calibration_map,
                                                 ref int[, ,] calibration_map_inverse)
        {
            int half_width = image_width / 2;
            int half_height = image_height / 2;
            calibration_map = new int[image_width * image_height];
            calibration_map_inverse = new int[image_width, image_height, 2];
            for (int x = 0; x < image_width; x++)
            {
                float dx = x - centre_of_distortion_x;

                for (int y = 0; y < image_height; y++)
                {
                    float dy = y - centre_of_distortion_y;

                    float radial_dist_rectified = (float)Math.Sqrt((dx * dx) + (dy * dy));
                    if (radial_dist_rectified >= 0.01f)
                    {
                        double radial_dist_original = curve.RegVal(radial_dist_rectified);
                        if (radial_dist_original > 0)
                        {
                            double ratio = radial_dist_original / radial_dist_rectified;
                            float x2 = (float)Math.Round(centre_of_distortion_x + (dx * ratio));
                            x2 = (x2 - (image_width / 2)) * scale;
                            float y2 = (float)Math.Round(centre_of_distortion_y + (dy * ratio));
                            y2 = (y2 - (image_height / 2)) * scale;

                            // apply rotation
                            double x3 = x2, y3 = y2;
                            rotatePoint(x2, y2, -rotation, ref x3, ref y3);

                            x3 += half_width;
                            y3 += half_height;

                            if (((int)x3 > -1) && ((int)x3 < image_width) &&
                                ((int)y3 > -1) && ((int)y3 < image_height))
                            {
                                int n = (y * image_width) + x;
                                int n2 = ((int)y3 * image_width) + (int)x3;

                                calibration_map[n] = n2;
                                calibration_map_inverse[(int)x3, (int)y3, 0] = x;
                                calibration_map_inverse[(int)x3, (int)y3, 1] = y;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region "making grids and lines out of the detected calibration dots"

        /// <summary>
        /// extracts lines from the given detected dots
        /// </summary>
        /// <param name="dots"></param>
        /// <returns></returns>
        private static List<List<double>> CreateLines(hypergraph dots,
                                                      calibrationDot[,] grid)
        {
            List<List<double>> lines = new List<List<double>>();            
            if (grid != null)
            {
                for (int grid_x = 0; grid_x < grid.GetLength(0); grid_x++)
                {
                    List<double> line = new List<double>();
                    for (int grid_y = 0; grid_y < grid.GetLength(1); grid_y++)
                    {
                        if (grid[grid_x, grid_y] != null)
                        {
                            line.Add(grid[grid_x, grid_y].x);
                            line.Add(grid[grid_x, grid_y].y);
                        }
                    }
                    if (line.Count > 6)
                    {
                        lines.Add(line);
                    }
                }
                for (int grid_y = 0; grid_y < grid.GetLength(1); grid_y++)
                {
                    List<double> line = new List<double>();
                    for (int grid_x = 0; grid_x < grid.GetLength(0); grid_x++)
                    {
                        if (grid[grid_x, grid_y] != null)
                        {
                            line.Add(grid[grid_x, grid_y].x);
                            line.Add(grid[grid_x, grid_y].y);
                        }
                    }
                    if (line.Count > 6)
                    {
                        lines.Add(line);
                    }
                }
            }
            return (lines);
        }


        /// <summary>
        /// puts the given dots into a grid for easy lookup
        /// </summary>
        /// <param name="dots">detected calibration dots</param>
        /// <returns>grid object</returns>
        private static calibrationDot[,] CreateGrid(hypergraph dots)
        {
            int grid_tx = 9999, grid_ty = 9999;
            int grid_bx = -9999, grid_by = -9999;
            for (int i = 0; i < dots.Nodes.Count; i++)
            {
                calibrationDot dot = (calibrationDot)dots.Nodes[i];
                if (Math.Abs(dot.grid_x) < 50)
                {
                    if (dot.grid_x < grid_tx) grid_tx = dot.grid_x;
                    if (dot.grid_y < grid_ty) grid_ty = dot.grid_y;
                    if (dot.grid_x > grid_bx) grid_bx = dot.grid_x;
                    if (dot.grid_y > grid_by) grid_by = dot.grid_y;
                }
            }

            calibrationDot[,] grid = null;
            if (grid_bx > grid_tx + 1)
            {
                grid = new calibrationDot[grid_bx - grid_tx + 1, grid_by - grid_ty + 1];

                for (int i = 0; i < dots.Nodes.Count; i++)
                {
                    calibrationDot dot = (calibrationDot)dots.Nodes[i];
                    if ((!dot.centre) && (Math.Abs(dot.grid_x) < 50))
                    {
                        grid[dot.grid_x - grid_tx, dot.grid_y - grid_ty] = dot;
                    }
                }
            }

            return (grid);
        }

        #endregion

        

        #region "detecting the curvature of lines"

        /// <summary>
        /// returns the average curvature value of the given set of line segments
        /// </summary>
        /// <param name="lines">list of line segments</param>
        /// <returns>average curvature of the lines</returns>
        private static double LineCurvature(List<List<double>> lines)
        {
            double curvature = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                double curv = LineCurvature(lines[i]);
                //if (curv > curvature) curvature = curv;
                curvature += curv*curv;
            }

            if (lines.Count > 0) curvature = Math.Sqrt(curvature / lines.Count);
            return (curvature);
        }

        /// <summary>
        /// returns a measure proportional to the curvature of the given line segment
        /// </summary>
        /// <param name="line">line segment as a list of coordinates</param>
        /// <returns>measure of curvature</returns>
        private static double LineCurvature(List<double> line)
        {
            double curvature = 0;

            double x0 = line[0];
            double y0 = line[1];
            double x1 = line[line.Count - 2];
            double y1 = line[line.Count - 1];

            float max_dist = 0;
            double prev_x = -1;
            double prev_y = -1;
            float dist_from_line;
            float x2 = 0, y2 = 0;
            for (int i = 2; i < line.Count - 2; i += 2)
            {
                double x = line[i];
                double y = line[i + 1];

                if (prev_x > -1)
                {
                    double dx = x - prev_x;
                    double dy = y - prev_y;
                    double ix = prev_x + (dx * 0.5);
                    double iy = prev_y + (dy * 0.5);

                    dist_from_line = Math.Abs(geometry.pointDistanceFromLine((float)x0, (float)y0, (float)x1, (float)y1, (float)ix, (float)iy, ref x2, ref y2));
                    if (dist_from_line > max_dist)
                    {
                        max_dist = dist_from_line;
                    }
                }

                dist_from_line = Math.Abs(geometry.pointDistanceFromLine((float)x0, (float)y0, (float)x1, (float)y1, (float)x, (float)y, ref x2, ref y2));
                if (dist_from_line > max_dist)
                {
                    max_dist = dist_from_line;
                }

                prev_x = x;
                prev_y = y;
            }
            curvature = max_dist;
            return (curvature);
        }

        #endregion

        #region "detecting the length of lines"

        /// <summary>
        /// returns the total length of all lines
        /// </summary>
        /// <param name="lines">list of line segments</param>
        /// <returns>total length</returns>
        private static float LineLength(List<List<float>> lines)
        {
            float length = 0;

            for (int i = 0; i < lines.Count; i++)
                length += LineLength(lines[i]);

            return (length);
        }

        /// <summary>
        /// returns the length of the given line segment
        /// </summary>
        /// <param name="line">line segment as a list of coordinates</param>
        /// <returns>line length</returns>
        private static float LineLength(List<float> line)
        {
            float length = 0;

            float prev_x = 0, prev_y = 0;
            for (int i = 0; i < line.Count; i += 2)
            {
                float x = line[i];
                float y = line[i + 1];
                if (i > 0)
                {
                    float dx = x - prev_x;
                    float dy = y - prev_y;
                    length += (float)Math.Sqrt((dx * dx) + (dy * dy));
                }
                prev_x = x;
                prev_y = y;
            }
            return (length);
        }


        #endregion

        #region "rectification"

        /// <summary>
        /// rectify the given image
        /// </summary>
        /// <param name="img">raw image</param>
        /// <param name="width">width of the image</param>
        /// <param name="height">height of the image</param>
        /// <returns></returns>
        private static byte[] Rectify(Byte[] img, int width, int height,
                                      int[] calibration_map)
        {
            byte[] rectified_image = null;
            if (calibration_map != null)
            {
                rectified_image = new Byte[width * height * 3];
                for (int i = 0; i < width * height; i++)
                {
                    int j = calibration_map[i];
                    if ((j < width * height) && (j > -1))
                    {
                        for (int col = 0; col < 3; col++)
                            rectified_image[(i * 3) + col] = img[(j * 3) + col];
                    }
                }
            }
            return (rectified_image);
        }


        private static byte[] Rectify(string filename, 
                                      int[] calibration_map,
                                      ref int image_width, ref int image_height)
        {
            Bitmap bmp = (Bitmap)Bitmap.FromFile(filename);
            byte[] img = new byte[bmp.Width * bmp.Height * 3];            
            BitmapArrayConversions.updatebitmap(bmp, img);

            byte[] rectified = Rectify(img, bmp.Width, bmp.Height, calibration_map);

            image_width = bmp.Width;
            image_height = bmp.Height;

            return (rectified);
        }

        private static void Rectify(string filename,
                                    int[] calibration_map,
                                    string rectified_filename)
        {
            int image_width = 0;
            int image_height = 0;
            byte[] rectified_img = Rectify(filename, calibration_map, ref image_width, ref image_height);

            Bitmap output_bmp = new Bitmap(image_width, image_height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(rectified_img, output_bmp);
            if (rectified_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(rectified_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (rectified_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(rectified_filename, System.Drawing.Imaging.ImageFormat.Bmp);

        }

        /// <summary>
        /// applies the given curve to the lines to produce rectified coordinates
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="distortion_curve"></param>
        /// <param name="centre_of_distortion"></param>
        /// <returns></returns>
        private static List<List<double>> RectifyLines(List<List<double>> lines,
                                                       int image_width, int image_height,
                                                       polynomial curve,
                                                       calibrationDot centre_of_distortion,
                                                       double rotation,
                                                       double scale)
        {
            List<List<double>> rectified_lines = new List<List<double>>();

            float half_width = image_width / 2;
            float half_height = image_height / 2;

            for (int i = 0; i < lines.Count; i++)
            {
                List<double> line = lines[i];
                List<double> rectified_line = new List<double>();
                for (int j = 0; j < line.Count; j += 2)
                {
                    double x = line[j];
                    double y = line[j + 1];

                    double dx = x - centre_of_distortion.x;
                    double dy = y - centre_of_distortion.y;
                    double radial_dist_rectified = Math.Sqrt((dx * dx) + (dy * dy));
                    if (radial_dist_rectified >= 0.01f)
                    {
                        double radial_dist_original = curve.RegVal(radial_dist_rectified);
                        if (radial_dist_original > 0)
                        {
                            double ratio = radial_dist_rectified / radial_dist_original;
                            double x2 = Math.Round(centre_of_distortion.x + (dx * ratio));
                            x2 = (x2 - (image_width / 2)) * scale;
                            double y2 = Math.Round(centre_of_distortion.y + (dy * ratio));
                            y2 = (y2 - (image_height / 2)) * scale;

                            // apply rotation
                            double rectified_x = x2, rectified_y = y2;
                            rotatePoint(x2, y2, -rotation, ref rectified_x, ref rectified_y);

                            rectified_x += half_width;
                            rectified_y += half_height;

                            rectified_line.Add(rectified_x);
                            rectified_line.Add(rectified_y);
                        }
                    }
                }

                if (rectified_line.Count > 6)
                    rectified_lines.Add(rectified_line);
            }

            return (rectified_lines);
        }

        #endregion

        #region "detecting the rotation of the camera"

        private static double DetectCameraRotation(int image_width, int image_height,
                                                   calibrationDot[,] grid,
                                                   polynomial curve,
                                                   calibrationDot centre_of_distortion,
                                                   ref List<List<double>> rectified_centre_line)
        {
            double rotation = 0;
            List<List<double>> centre_line = new List<List<double>>();
            List<double> line = new List<double>();

            // get the vertical centre line within the image
            for (int grid_y = 0; grid_y < grid.GetLength(1); grid_y++)
            {
                int grid_x = 0;
                bool found = false;
                while ((grid_x < grid.GetLength(0)) && (!found))
                {
                    if (grid[grid_x, grid_y] != null)
                    {
                        if (grid[grid_x, grid_y].grid_x == 0)
                        {
                            line.Add(grid[grid_x, grid_y].x);
                            line.Add(grid[grid_x, grid_y].y);
                            found = true;
                        }
                    }
                    grid_x++;
                }
            }
            centre_line.Add(line);

            // rectify the centre line
            rectified_centre_line =
                RectifyLines(centre_line, image_width, image_height,
                             curve, centre_of_distortion, 0, 1);

            if (rectified_centre_line != null)
            {
                if (rectified_centre_line.Count > 0)
                {
                    double[] px = new double[2];
                    double[] py = new double[2];
                    int[] hits = new int[2];
                    line = rectified_centre_line[0];
                    for (int i = 0; i < line.Count; i += 2)
                    {
                        double x = line[i];
                        double y = line[i + 1];
                        if (i < line.Count / 2)
                        {
                            px[0] += x;
                            py[0] += y;
                            hits[0]++;
                        }
                        else
                        {
                            px[1] += x;
                            py[1] += y;
                            hits[1]++;
                        }
                    }

                    if ((hits[0] > 0) && (hits[1] > 0))
                    {
                        px[0] /= hits[0];
                        py[0] /= hits[0];
                        px[1] /= hits[1];
                        py[1] /= hits[1];
                        double dx = px[1] - px[0];
                        double dy = py[1] - py[0];

                        double length = Math.Sqrt(dx * dx + dy * dy);
                        if (length > 0)
                            rotation = Math.Asin(dx / length);
                    }
                }
            }

            return (rotation);
        }

        #endregion

        #region "detecting lens distortion"

        private static void ScaleLines(List<List<float>> lines, float scale, 
                                       int image_width, int image_height,
                                       int centre_x, int centre_y)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                List<float> line = lines[i];
                for (int j = 0; j < line.Count; j += 2)
                {
                    float x = line[j];
                    float y = line[j + 1];
                    float dx = x - centre_x;
                    float dy = y - centre_y;
                    
                    line[j] = centre_x + (dx * scale);
                    line[j + 1] = centre_y + (dy * scale);
                }
            }
        }

        /// <summary>
        /// fits a curve to the given grid using the given centre of distortion
        /// </summary>
        /// <param name="image_width">width of the image in pixels</param>
        /// <param name="image_height">height of the image in pixels</param>
        /// <param name="grid">detected grid dots</param>
        /// <param name="overlay_grid">overlayed ideal rectified grid</param>
        /// <param name="centre_of_distortion">centre of lens distortion</param>
        /// <param name="centre_of_distortion_search_radius">search radius for the centre of distortion</param>
        /// <returns>fitted curve</returns>
        private static polynomial FitCurve(int image_width, int image_height,
                                           calibrationDot[,] grid,
                                           grid2D overlay_grid,
                                           calibrationDot centre_of_distortion,
                                           double centre_of_distortion_search_radius,
                                           int grid_offset_x, int grid_offset_y,
                                           List<List<double>> lines)
        {
            Random rnd = new Random(0);

            int degrees = 2;
            double minimum_error = double.MaxValue;
            List<double> prev_minimum_error = new List<double>();
            prev_minimum_error.Add(minimum_error);
            double increment = 1.0f;
            double noise = increment / 2;
            polynomial best_curve = null;

            double search_radius = (float)centre_of_distortion_search_radius;
            double half_width = image_width / 2;
            double half_height = image_height / 2;
            double half_noise = noise / 2;
            double max_radius_sqr = centre_of_distortion_search_radius * centre_of_distortion_search_radius;
            int scaled_up = 0;

            List<double> result = new List<double>();
            double abort_threshold = 0.000001;
            int abort_passes = 21;

            int max_passes = 300;
            for (int pass = 0; pass < max_passes; pass++)
            {
                double centre_x = 0;
                double centre_y = 0;
                double mass = 0;

                for (double cx = half_width - search_radius; cx < half_width + search_radius; cx += increment)
                {
                    double dx = cx - half_width;
                    for (double cy = half_height - search_radius; cy < half_height + search_radius; cy += increment)
                    {
                        double dy = cy - half_height;
                        double dist = dx * dx + dy * dy;
                        if (dist < max_radius_sqr)
                        {
                            polynomial curve = new polynomial();
                            curve.SetDegree(degrees);

                            centre_of_distortion.x = cx + (rnd.NextDouble() * noise) - half_noise;
                            centre_of_distortion.y = cy + (rnd.NextDouble() * noise) - half_noise;
                            FitCurve(grid, overlay_grid, centre_of_distortion, curve, noise, rnd, grid_offset_x, grid_offset_y);

                            double mean_variance = curve.GetVariance();

                            List<List<double>> rectified_lines = RectifyLines(lines, image_width, image_height, curve, centre_of_distortion, 0, 1);
                            double average_line_curvature = LineCurvature(rectified_lines);

                            double error = curve.GetSquaredError();
                            error = error * error * mean_variance * mean_variance * average_line_curvature * average_line_curvature;
                            //error = mean_variance * average_line_curvature * average_line_curvature;
                            //error = average_line_curvature * average_line_curvature;

                            if (error > 0.1)
                            {
                                error = 100000.0f / error;  // inverse
                                centre_x += centre_of_distortion.x * error;
                                centre_y += centre_of_distortion.y * error;
                                mass += error;
                            }
                            
                            
                        }
                    }
                }

                if (mass > 0)
                {
                    centre_x /= mass;
                    centre_y /= mass;

                    centre_of_distortion.x = centre_x;
                    centre_of_distortion.y = centre_y;

                    best_curve = new polynomial();
                    best_curve.SetDegree(degrees);
                    FitCurve(grid, overlay_grid, centre_of_distortion, best_curve, noise, rnd, grid_offset_x, grid_offset_y);

                    double mean_variance = best_curve.GetVariance();

                    List<List<double>> rectified_lines = RectifyLines(lines, image_width, image_height, best_curve, centre_of_distortion, 0, 1);
                    double average_line_curvature = LineCurvature(rectified_lines);

                    double RMS_error = best_curve.GetRMSerror();
                    double error_value = RMS_error * RMS_error * mean_variance * mean_variance * average_line_curvature * average_line_curvature;
                    //double error_value = mean_variance * average_line_curvature * average_line_curvature;
                    //double error_value = average_line_curvature * average_line_curvature;

                    double scaledown = 0.99999999;
                    if (error_value < minimum_error)
                    {
                        // cool down
                        prev_minimum_error.Add(minimum_error);
                        search_radius *= scaledown;
                        increment *= scaledown;
                        noise = increment/2;
                        half_noise = noise / 2;
                        half_width = centre_x;
                        half_height = centre_y;
                        minimum_error = error_value;
                        Console.WriteLine("Pass " + pass.ToString() + ": " + RMS_error.ToString());
                        scaled_up = 0;
                    }
                    else
                    {
                        if (scaled_up < 5)
                        {
                            // heat up
                            double scaleup = 1.0 / scaledown;
                            search_radius *= scaleup;
                            increment *= scaleup;
                            noise = increment / 2;
                            half_noise = noise / 2;
                            scaled_up++;
                            if (prev_minimum_error.Count > 0)
                            {
                                minimum_error = prev_minimum_error[prev_minimum_error.Count - 1];
                                prev_minimum_error.RemoveAt(prev_minimum_error.Count - 1);
                            }
                        }
                    }
                    
                    result.Add(RMS_error);
                    if (result.Count > abort_passes)
                    {
                        if (result[result.Count - 1 - abort_passes] - RMS_error < abort_threshold)
                        {
                            // it looks like the error is no longer converging
                            // time to bail out chaps
                            pass = max_passes;
                            Console.WriteLine("Error converged");
                        }
                    }
                }
            }

            minimum_error = Math.Sqrt(minimum_error);
            //Console.WriteLine("Minimum curvature (pixels): " + minimum_error.ToString());

            centre_of_distortion.x = half_width;
            centre_of_distortion.y = half_height;
            best_curve = new polynomial();
            best_curve.SetDegree(degrees);
            FitCurve(grid, overlay_grid, centre_of_distortion, best_curve, 0, rnd, grid_offset_x, grid_offset_y);

            return (best_curve);
        }

        /// <summary>
        /// fits a curve to the given grid using the given centre of distortion
        /// </summary>
        /// <param name="grid">detected grid dots</param>
        /// <param name="overlay_grid">overlayed ideal rectified grid</param>
        /// <param name="centre_of_distortion">centre of lens distortion</param>
        /// <param name="curve">curve to be fitted</param>
        private static void FitCurve(calibrationDot[,] grid,
                                     grid2D overlay_grid,
                                     calibrationDot centre_of_distortion,
                                     polynomial curve,
                                     double noise, Random rnd,
                                     int grid_offset_x, int grid_offset_y)
        {
            double[] prev_col = new double[grid.GetLength(1) * 2];
            double[] col = new double[prev_col.Length];

            double half_noise = noise / 2;
            // for every detected dot
            for (int grid_x = 0; grid_x < grid.GetLength(0); grid_x++)
            {
                double prev_rectified_radial_dist=0;
                double prev_actual_radial_dist=0;
                int prev_grid_y = -1;
                for (int grid_y = 0; grid_y < grid.GetLength(1); grid_y++)
                {
                    if (grid[grid_x, grid_y] != null)
                    {
                        if ((grid_x + grid_offset_x < overlay_grid.line_intercepts.GetLength(0)) &&
                            (grid_y + grid_offset_y < overlay_grid.line_intercepts.GetLength(1)) &&
                            (grid_x + grid_offset_x >= 0) && (grid_y + grid_offset_y >= 0))
                        {
                            // find the rectified distance of the dot from the centre of distortion
                            //double rectified_x = overlay_grid.line_intercepts[grid_x + grid_offset_x, grid_y + grid_offset_y, 0] + (rnd.NextDouble() * noise) - half_noise;
                            //double rectified_y = overlay_grid.line_intercepts[grid_x + grid_offset_x, grid_y + grid_offset_y, 1] + (rnd.NextDouble() * noise) - half_noise;
                            double rectified_x = overlay_grid.line_intercepts[grid_x + grid_offset_x, grid_y + grid_offset_y, 0];
                            double rectified_y = overlay_grid.line_intercepts[grid_x + grid_offset_x, grid_y + grid_offset_y, 1];
                            double rectified_dx = rectified_x - centre_of_distortion.x;
                            double rectified_dy = rectified_y - centre_of_distortion.y;
                            double rectified_radial_dist = Math.Sqrt(rectified_dx * rectified_dx + rectified_dy * rectified_dy);

                            // find the actual raw image distance of the dot from the centre of distortion
                            //double actual_x = grid[grid_x, grid_y].x + (rnd.NextDouble() * noise) - half_noise;
                            //double actual_y = grid[grid_x, grid_y].y + (rnd.NextDouble() * noise) - half_noise;
                            double actual_x = grid[grid_x, grid_y].x;
                            double actual_y = grid[grid_x, grid_y].y;
                            double actual_dx = actual_x - centre_of_distortion.x;
                            double actual_dy = actual_y - centre_of_distortion.y;
                            double actual_radial_dist = Math.Sqrt(actual_dx * actual_dx + actual_dy * actual_dy);

                            // plot
                            curve.AddPoint(rectified_radial_dist, actual_radial_dist);

                            if (prev_grid_y == grid_y - 1)
                            {
                                for (float inter = 0.25f; inter <= 0.75f; inter += 0.25f)
                                {
                                    double intermediate_rectified_radial_dist = prev_rectified_radial_dist + ((rectified_radial_dist - prev_rectified_radial_dist) * inter);
                                    double intermediate_actual_radial_dist = prev_actual_radial_dist + ((actual_radial_dist - prev_actual_radial_dist) * inter);
                                    curve.AddPoint(intermediate_rectified_radial_dist, intermediate_actual_radial_dist);
                                }
                            }
                            if (grid_x > 0)
                            {
                                if (grid[grid_x - 1, grid_y] != null)
                                {
                                    if (prev_col[grid_y * 2] != 0)
                                    {
                                        prev_rectified_radial_dist = prev_col[grid_y * 2];
                                        prev_actual_radial_dist = prev_col[(grid_y * 2) + 1];

                                        for (float inter = 0.25f; inter <= 0.75f; inter += 0.25f)
                                        {
                                            double intermediate_rectified_radial_dist = prev_rectified_radial_dist + ((rectified_radial_dist - prev_rectified_radial_dist) * inter);
                                            double intermediate_actual_radial_dist = prev_actual_radial_dist + ((actual_radial_dist - prev_actual_radial_dist) * inter);
                                            curve.AddPoint(intermediate_rectified_radial_dist, intermediate_actual_radial_dist);
                                        }
                                    }
                                }
                            }

                            col[(grid_y * 2)] = rectified_radial_dist;
                            col[(grid_y * 2) + 1] = actual_radial_dist;

                            prev_rectified_radial_dist = rectified_radial_dist;
                            prev_actual_radial_dist = actual_radial_dist;
                            prev_grid_y = grid_y;
                        }
                    }
                }

                for (int i = 0; i < col.Length; i++)
                    prev_col[i] = col[i];
            }

            // find the best fit curve
            curve.Solve();

            //if (curve.Coeff(5) > 0) curve.SetCoeff(5, 0);
            //if (curve.Coeff(6) > 0) curve.SetCoeff(6, 0);
            //Console.WriteLine("coeff 6: " + curve.Coeff(6).ToString());
        }


        private static void BestFit(int grid_tx, int grid_ty,
                                    calibrationDot[,] grid,
                                    grid2D overlay_grid,
                                    ref double min_dist,
                                    ref double min_dx, ref double min_dy,
                                    ref int min_hits,
                                    ref int grid_offset_x, ref int grid_offset_y)
        {
            for (int off_x = -1; off_x <= 1; off_x++)
            {
                for (int off_y = -1; off_y <= 1; off_y++)
                {
                    int grid_x_offset = -grid_tx + off_x;
                    int grid_y_offset = -grid_ty + off_y;

                    int grid_x_offset_start = 0;
                    int grid_x_offset_end = 0;
                    if (grid_x_offset < 0)
                    {
                        grid_x_offset_start = -grid_x_offset;
                        grid_x_offset_end = 0;
                    }
                    else
                    {
                        grid_x_offset_start = 0;
                        grid_x_offset_end = grid_x_offset;
                    }
                    int grid_y_offset_start = 0;
                    int grid_y_offset_end = 0;
                    if (grid_y_offset < 0)
                    {
                        grid_y_offset_start = -grid_y_offset;
                        grid_y_offset_end = 0;
                    }
                    else
                    {
                        grid_y_offset_start = 0;
                        grid_y_offset_end = grid_y_offset;
                    }
                    double dx = 0;
                    double dy = 0;
                    double dist = 0;
                    int hits = 0;
                    for (int grid_x = grid_x_offset_start; grid_x < grid.GetLength(0) - grid_x_offset_end; grid_x++)
                    {
                        for (int grid_y = grid_y_offset_start; grid_y < grid.GetLength(1) - grid_y_offset_end; grid_y++)
                        {
                            if (grid[grid_x, grid_y] != null)
                            {
                                if ((grid_x + grid_x_offset < overlay_grid.line_intercepts.GetLength(0)) &&
                                    (grid_y + grid_y_offset < overlay_grid.line_intercepts.GetLength(1)))
                                {
                                    double intercept_x = overlay_grid.line_intercepts[grid_x + grid_x_offset, grid_y + grid_y_offset, 0];
                                    double intercept_y = overlay_grid.line_intercepts[grid_x + grid_x_offset, grid_y + grid_y_offset, 1];
                                    double dxx = grid[grid_x, grid_y].x - intercept_x;
                                    double dyy = grid[grid_x, grid_y].y - intercept_y;
                                    dx += dxx;
                                    dy += dyy;
                                    dist += Math.Abs(dxx) + Math.Abs(dyy);
                                    hits++;
                                }
                            }
                        }
                    }

                    if (hits > 0)
                    {
                        dx /= hits;
                        dy /= hits;

                        //double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < min_dist)
                        {
                            min_dist = dist;
                            min_dx = dx;
                            min_dy = dy;
                            min_hits = hits;
                            grid_offset_x = grid_x_offset;
                            grid_offset_y = grid_y_offset;
                        }

                    }
                }
            }
        }

        /// <summary>
        /// returns an ideally spaced grid over the actual detected spots
        /// </summary>
        /// <param name="grid"></param>
        /// <returns></returns>
        private static grid2D OverlayIdealGrid(calibrationDot[,] grid,
                                               List<calibrationDot> corners,
                                               ref int grid_offset_x, ref int grid_offset_y)
        {
            grid2D overlay_grid = null;
            int grid_tx = -1;
            int grid_ty = -1;
            int grid_bx = -1;
            int grid_by = -1;

            int offset_x = 0;
            int offset_y = 0;

            bool found = false;
            int max_region_area = 0;

            // try searching horizontally and vertically
            // then pick the result with the greatest area
            for (int test_orientation = 0; test_orientation < 2; test_orientation++)
            {
                bool temp_found = false;

                int temp_grid_tx = -1;
                int temp_grid_ty = -1;
                int temp_grid_bx = -1;
                int temp_grid_by = -1;

                int temp_offset_x = 0;
                int temp_offset_y = 0;

                switch(test_orientation)
                {
                    case 0:
                        {
                            while ((temp_offset_y < 5) && (!temp_found))
                            {
                                temp_offset_x = 0;
                                while ((temp_offset_x < 3) && (!temp_found))
                                {
                                    temp_grid_tx = temp_offset_x;
                                    temp_grid_ty = temp_offset_y;
                                    temp_grid_bx = grid.GetLength(0) - 1 - temp_offset_x;
                                    temp_grid_by = grid.GetLength(1) - 1 - temp_offset_y;

                                    if ((grid[temp_grid_tx, temp_grid_ty] != null) &&
                                        (grid[temp_grid_bx, temp_grid_ty] != null) &&
                                        (grid[temp_grid_bx, temp_grid_by] != null) &&
                                        (grid[temp_grid_tx, temp_grid_by] != null))
                                    {
                                        temp_found = true;
                                    }

                                    temp_offset_x++;
                                }
                                temp_offset_y++;
                            }
                            break;
                        }
                    case 1:
                        {
                            while ((temp_offset_x < 3) && (!temp_found))
                            {
                                temp_offset_y = 0;
                                while ((temp_offset_y < 5) && (!temp_found))
                                {
                                    temp_grid_tx = temp_offset_x;
                                    temp_grid_ty = temp_offset_y;
                                    temp_grid_bx = grid.GetLength(0) - 1 - temp_offset_x;
                                    temp_grid_by = grid.GetLength(1) - 1 - temp_offset_y;

                                    if ((grid[temp_grid_tx, temp_grid_ty] != null) &&
                                        (grid[temp_grid_bx, temp_grid_ty] != null) &&
                                        (grid[temp_grid_bx, temp_grid_by] != null) &&
                                        (grid[temp_grid_tx, temp_grid_by] != null))
                                    {
                                        temp_found = true;
                                    }

                                    temp_offset_y++;
                                }
                                temp_offset_x++;
                            }
                            break;
                        }
                }

                temp_offset_y = temp_grid_ty - 1;
                while (temp_offset_y >= 0)
                {
                    if ((grid[temp_grid_tx, temp_offset_y] != null) &&
                        (grid[temp_grid_bx, temp_offset_y] != null))
                    {
                        temp_grid_ty = temp_offset_y;
                        temp_offset_y--;
                    }
                    else break;
                }

                temp_offset_y = temp_grid_by + 1;
                while (temp_offset_y < grid.GetLength(1))
                {
                    if ((grid[temp_grid_tx, temp_offset_y] != null) &&
                        (grid[temp_grid_bx, temp_offset_y] != null))
                    {
                        temp_grid_by = temp_offset_y;
                        temp_offset_y++;
                    }
                    else break;
                }

                if (temp_found)
                {     
                    int region_area = (temp_grid_bx - temp_grid_tx) * (temp_grid_by - temp_grid_ty);
                    if (region_area > max_region_area)
                    {
                        max_region_area = region_area;
                        found = true;

                        grid_tx = temp_grid_tx;
                        grid_ty = temp_grid_ty;
                        grid_bx = temp_grid_bx;
                        grid_by = temp_grid_by;

                        offset_x = temp_offset_x;
                        offset_y = temp_offset_y;
                    }
                }
            }

            // record the positions of the corners
            corners.Add(grid[grid_tx, grid_ty]);
            corners.Add(grid[grid_bx, grid_ty]);
            corners.Add(grid[grid_bx, grid_by]);
            corners.Add(grid[grid_tx, grid_by]);

            if (found)
            {
                double dx, dy;
                
                double x0 = grid[grid_tx, grid_ty].x;
                double y0 = grid[grid_tx, grid_ty].y;
                double x1 = grid[grid_bx, grid_ty].x;
                double y1 = grid[grid_bx, grid_ty].y;
                double x2 = grid[grid_tx, grid_by].x;
                double y2 = grid[grid_tx, grid_by].y;
                double x3 = grid[grid_bx, grid_by].x;
                double y3 = grid[grid_bx, grid_by].y;
                
                polygon2D perimeter = new polygon2D();
                perimeter.Add((float)x0, (float)y0);
                perimeter.Add((float)x1, (float)y1);
                perimeter.Add((float)x3, (float)y3);
                perimeter.Add((float)x2, (float)y2);
                
                int grid_width = grid_bx - grid_tx;
                int grid_height = grid_by - grid_ty;

                int min_hits = 0;                
                double min_dx = 0, min_dy = 0;

                // try various perimeter sizes
                double min_dist = double.MaxValue;
                int max_perim_size_tries = 100;
                polygon2D best_perimeter = perimeter;
                Random rnd = new Random(0);
                for (int perim_size = 0; perim_size < max_perim_size_tries; perim_size++)
                {
                    // try a small range of translations
                    for (int nudge_x = -10; nudge_x <= 10; nudge_x++)
                    {
                        for (int nudge_y = -5; nudge_y <= 5; nudge_y++)
                        {
                            // create a perimeter at this scale and translation
                            polygon2D temp_perimeter = perimeter.Scale(1.0f + (perim_size * 0.1f / max_perim_size_tries));
                            temp_perimeter = temp_perimeter.ScaleSideLength(0, 0.95f + ((float)rnd.NextDouble() * 0.1f));
                            temp_perimeter = temp_perimeter.ScaleSideLength(2, 0.95f + ((float)rnd.NextDouble() * 0.1f));
                            for (int i = 0; i < temp_perimeter.x_points.Count; i++)
                            {
                                temp_perimeter.x_points[i] += nudge_x;
                                temp_perimeter.y_points[i] += nudge_y;
                            }

                            // create a grid based upon the perimeter
                            grid2D temp_overlay_grid = new grid2D(grid_width, grid_height, temp_perimeter, 0, false);

                            // how closely does the grid fit the actual observations ?
                            double temp_min_dist = min_dist;
                            BestFit(grid_tx, grid_ty, grid,
                                    temp_overlay_grid, ref min_dist,
                                    ref min_dx, ref min_dy, ref min_hits,
                                    ref grid_offset_x, ref grid_offset_y);

                            // record the closest fit
                            if (temp_min_dist < min_dist)
                            {
                                best_perimeter = temp_perimeter;
                                overlay_grid = temp_overlay_grid;
                            }
                        }
                    }
                }

                if (min_hits > 0)
                {
                    dx = min_dx;
                    dy = min_dy;

                    Console.WriteLine("dx: " + dx.ToString());
                    Console.WriteLine("dy: " + dy.ToString());
                    
                    x0 += dx;
                    y0 += dy;
                    x1 += dx;
                    y1 += dy;
                    x2 += dx;
                    y2 += dy;
                    x3 += dx;
                    y3 += dy;

                    perimeter = new polygon2D();
                    perimeter.Add((float)x0, (float)y0);
                    perimeter.Add((float)x1, (float)y1);
                    perimeter.Add((float)x3, (float)y3);
                    perimeter.Add((float)x2, (float)y2);
                    overlay_grid = new grid2D(grid_width, grid_height, perimeter, 0, false);
                }
            }

            return (overlay_grid);
        }


        private static void DetectLensDistortion(int image_width, int image_height,
                                                 calibrationDot[,] grid,
                                                 grid2D overlay_grid,
                                                 List<List<double>> lines,
                                                 ref polynomial curve,
                                                 ref calibrationDot centre_of_distortion,
                                                 ref List<List<double>> best_rectified_lines,
                                                 int grid_offset_x, int grid_offset_y)
        {
            double centre_of_distortion_search_radius = image_width / 50.0f;
            centre_of_distortion = new calibrationDot();
            curve = FitCurve(image_width, image_height,
                             grid, overlay_grid,
                             centre_of_distortion,
                             centre_of_distortion_search_radius,
                             grid_offset_x, grid_offset_y, lines);

            double rotation = 0;
            double scale = 1;

            best_rectified_lines =
                RectifyLines(lines,
                             image_width, image_height,
                             curve,
                             centre_of_distortion,
                             rotation, scale);
        }

        #endregion

        #endregion

        #region "testing"

        private static void Test()
        {
            string filename = "/home/motters/calibrationdata/forward2/raw1_5000_2000.jpg";
            //string filename = "c:\\develop\\sentience\\calibrationimages\\raw0_5000_2000.jpg";
            //string filename = "c:\\develop\\sentience\\calibrationimages\\raw1_5250_2000.jpg";
            
            float dist_to_centre_dot_mm = 1000;
            float dot_spacing_mm = 50;
            double centre_of_distortion_x = 0;
            double centre_of_distortion_y = 0;
            polynomial lens_distortion_curve = null;
            double camera_rotation = 0;
            double scale = 0;
            
            Detect(filename, 78, dist_to_centre_dot_mm, dot_spacing_mm,
                   ref centre_of_distortion_x, ref centre_of_distortion_y,
                   ref lens_distortion_curve,
                   ref camera_rotation, ref scale);
        }

        #endregion

        #region "display"

        private static void ShowSquare(string filename, polygon2D square,
                                       string output_filename)
        {
            Bitmap bmp = (Bitmap)Bitmap.FromFile(filename);
            byte[] img = new byte[bmp.Width * bmp.Height * 3];
            BitmapArrayConversions.updatebitmap(bmp, img);
            square.show(img, bmp.Width, bmp.Height, 0, 255, 0, 0);

            Bitmap output_bmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }

        private static void ShowLines(string filename, List<List<double>> lines,
                                      string output_filename)
        {
            Bitmap bmp = (Bitmap)Bitmap.FromFile(filename);
            byte[] img = new byte[bmp.Width * bmp.Height * 3];
            BitmapArrayConversions.updatebitmap(bmp, img);

            Random rnd = new Random();

            for (int i = 0; i < lines.Count; i++)
            {
                int r = 0;
                int g = 255;
                int b = 0;

                r = rnd.Next(255);
                g = rnd.Next(255);
                b = rnd.Next(255);

                List<double> line = lines[i];
                double prev_x = 0, prev_y = 0;
                for (int j = 0; j < line.Count; j += 2)
                {
                    double x = line[j];
                    double y = line[j + 1];
                    if (j > 0)
                        drawing.drawLine(img, bmp.Width, bmp.Height, (int)prev_x, (int)prev_y, (int)x, (int)y, r, g, b, 0, false);
                    prev_x = x;
                    prev_y = y;
                }
            }

            Bitmap output_bmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }

        private static void ShowDistortionCurve(polynomial curve, string output_filename)
        {
            int img_width = 640;
            int img_height = 480;
            byte[] img = new byte[img_width * img_height * 3];
            
            curve.Show(img, img_width, img_height);

            Bitmap output_bmp = new Bitmap(img_width, img_height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }


        private static void ShowLensDistortion(int img_width, int img_height,
                                               calibrationDot centre_of_distortion,
                                               polynomial curve,
                                               float FOV_degrees, float increment_degrees,
                                               string output_filename)
        {
            byte[] img = new byte[img_width * img_height * 3];

            for (int i = 0; i < img.Length; i++) img[i] = 255;

            float max_radius = img_width / 2;

            float total_difference = 0;
            for (float r = 0; r < max_radius * 1.0f; r += 0.2f)
            {
                float diff = (float)Math.Abs(r - curve.RegVal(r));
                total_difference += diff;
            }

            if (total_difference > 0)
            {
                int rr, gg, bb;
                float diff_sum = 0;
                for (float r = 0; r < max_radius * 1.8f; r += 0.2f)
                {
                    float original_r = (float)curve.RegVal(r);
                    float d1 = r - original_r;
                    diff_sum += Math.Abs(d1);

                    float diff = diff_sum / total_difference;
                    if (diff > 1.0f) diff = 1.0f;
                    byte difference = (byte)(50 + (diff * 205));
                    if (d1 >= 0)
                    {
                        rr = difference;
                        gg = difference;
                        bb = difference;
                    }
                    else
                    {
                        rr = difference;
                        gg = difference;
                        bb = difference;
                    }
                    drawing.drawCircle(img, img_width, img_height,
                                       (float)centre_of_distortion.x,
                                       (float)centre_of_distortion.y,
                                       r, rr, gg, bb, 1, 300);
                }
            }
            
            float increment = max_radius * increment_degrees / FOV_degrees;
            float angle_degrees = increment_degrees;
            for (float r = increment; r <= max_radius*150/100; r += increment)
            {
                float radius = (float)curve.RegVal(r);
                drawing.drawCircle(img, img_width, img_height,
                                   (float)centre_of_distortion.x,
                                   (float)centre_of_distortion.y,
                                   radius, 0, 0, 0, 0, 360);
                
                drawing.AddText(img, img_width, img_height, angle_degrees.ToString(),
                                "Courier New", 10, 0,0,0,
                                (int)centre_of_distortion.x + (int)radius + 10, (int)centre_of_distortion.y);
                                
                angle_degrees += increment_degrees;
            }
            for (int x = 0; x < img_width; x += img_width / 20)
            {
                for (int y = 0; y < img_height; y += img_width / 20)
                {
                    float dx = x - (float)centre_of_distortion.x;
                    float dy = y - (float)centre_of_distortion.y;
                    float radius = (float)Math.Sqrt(dx * dx + dy * dy);
                    
                }
            }
            
            drawing.drawLine(img, img_width, img_height, 0, (int)centre_of_distortion.y, img_width-1, (int)centre_of_distortion.y, 0,0,0,0,false);
            drawing.drawLine(img, img_width, img_height, (int)centre_of_distortion.x, 0, (int)centre_of_distortion.x, img_height-1, 0,0,0,0,false);

            Bitmap output_bmp = new Bitmap(img_width, img_height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }


        private static void ShowCurveVariance(polynomial curve, string output_filename)
        {
            int img_width = 640;
            int img_height = 480;
            byte[] img = new byte[img_width * img_height * 3];

            curve.ShowVariance(img, img_width, img_height);

            Bitmap output_bmp = new Bitmap(img_width, img_height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }

        private static void ShowDots(List<calibrationDot> dots, string filename, string output_filename)
        {
            Bitmap bmp = (Bitmap)Bitmap.FromFile(filename);
            byte[] img = new byte[bmp.Width * bmp.Height * 3];
            BitmapArrayConversions.updatebitmap(bmp, img);

            for (int i = 0; i < dots.Count; i++)
                drawing.drawCross(img, bmp.Width, bmp.Height, (int)dots[i].x, (int)dots[i].y, 5, 255, 0,0, 0);

            Bitmap output_bmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }

        private static void ShowGrid(string filename, 
                                     hypergraph dots,
                                     List<calibrationDot> search_regions,
                                     string output_filename)
        {
            Bitmap bmp = (Bitmap)Bitmap.FromFile(filename);
            byte[] img = new byte[bmp.Width * bmp.Height * 3];
            BitmapArrayConversions.updatebitmap(bmp, img);

            if (search_regions != null)
            {
                for (int i = 0; i < search_regions.Count; i++)
                {
                    calibrationDot dot = (calibrationDot)search_regions[i];
                    drawing.drawCircle(img, bmp.Width, bmp.Height, (float)dot.x, (float)dot.y, (float)dot.radius, 255, 255, 0, 0);
                }
            }

            for (int i = 0; i < dots.Nodes.Count; i++)
            {
                calibrationDot dot = (calibrationDot)dots.Nodes[i];
                drawing.drawCircle(img, bmp.Width, bmp.Height, (float)dot.x, (float)dot.y, dot.radius, 0, 255, 0, 0);
            }

            for (int i = 0; i < dots.Links.Count; i++)
            {
                calibrationDot from_dot = (calibrationDot)dots.Links[i].From;
                calibrationDot to_dot = (calibrationDot)dots.Links[i].To;
                drawing.drawLine(img, bmp.Width, bmp.Height, (int)from_dot.x, (int)from_dot.y, (int)to_dot.x, (int)to_dot.y, 255, 0, 0, 0, false);
            }

            Bitmap output_bmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }

        private static void ShowOverlayGrid(string filename,
                                            grid2D overlay_grid,
                                            string output_filename)
        {
            Bitmap bmp = (Bitmap)Bitmap.FromFile(filename);
            byte[] img = new byte[bmp.Width * bmp.Height * 3];
            BitmapArrayConversions.updatebitmap(bmp, img);

            if (overlay_grid != null)
            {
                overlay_grid.ShowIntercepts(img, bmp.Width, bmp.Height, 255, 0, 0, 5, 0);
            }

            Bitmap output_bmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }

        private static void ShowOverlayGridPerimeter(string filename,
                                                     grid2D overlay_grid,
                                                     string output_filename)
        {
            Bitmap bmp = (Bitmap)Bitmap.FromFile(filename);
            byte[] img = new byte[bmp.Width * bmp.Height * 3];
            BitmapArrayConversions.updatebitmap(bmp, img);

            if (overlay_grid != null)
            {
                overlay_grid.perimeter.show(img, bmp.Width, bmp.Height, 255, 0, 0, 0);
            }

            Bitmap output_bmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }


        private static void ShowGridCoordinates(string filename, 
                                                hypergraph dots,
                                                string output_filename)
        {
            Bitmap bmp = (Bitmap)Bitmap.FromFile(filename);
            byte[] img = new byte[bmp.Width * bmp.Height * 3];
            BitmapArrayConversions.updatebitmap(bmp, img);

            for (int i = 0; i < dots.Nodes.Count; i++)
            {
                calibrationDot dot = (calibrationDot)dots.Nodes[i];
                drawing.drawCircle(img, bmp.Width, bmp.Height, (float)dot.x, (float)dot.y, dot.radius, 0, 255, 0, 0);
                
                if (dot.grid_x != 9999)
                {
                    string coord = dot.grid_x.ToString() + "," + dot.grid_y.ToString();
                    drawing.AddText(img, bmp.Width, bmp.Height, coord, "Courier New", 8, 0,0,0, (int)dot.x-0, (int)dot.y+5);
                }
            }

            Bitmap output_bmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapArrayConversions.updatebitmap_unsafe(img, output_bmp);
            if (output_filename.ToLower().EndsWith("jpg"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Jpeg);
            if (output_filename.ToLower().EndsWith("bmp"))
                output_bmp.Save(output_filename, System.Drawing.Imaging.ImageFormat.Bmp);
        }


        #endregion        

        #region "detecting dots"

        private static void ApplyGrid(hypergraph dots, 
                                      List<calibrationDot> centre_dots)
        {
            const int UNASSIGNED = 9999;
            
            // mark all dots as unassigned
            for (int i = 0; i < dots.Nodes.Count; i++)
            {
                calibrationDot dot = (calibrationDot)dots.Nodes[i];
                dot.grid_x = UNASSIGNED;
                dot.grid_y = UNASSIGNED;
            }
            
            // assign grid positions to the four dots
            // surrounding the centre dot
            centre_dots[0].grid_x = -1;
            centre_dots[0].grid_y = 1;

            centre_dots[1].grid_x = 0;
            centre_dots[1].grid_y = 1;

            centre_dots[2].grid_x = 0;
            centre_dots[2].grid_y = 0;

            centre_dots[3].grid_x = -1;
            centre_dots[3].grid_y = 0;
            
            int dots_assigned = 4;
            
            // recursively assign grid positions to dots
            for (int i = 0; i < centre_dots.Count; i++)
                ApplyGrid(centre_dots[i], UNASSIGNED, ref dots_assigned);
                
            Console.WriteLine(dots_assigned.ToString() + " dots assigned grid coordinates");
        }
        
        private static void ApplyGrid(calibrationDot current_dot, int unassigned_value, ref int dots_assigned)
        {
            for (int i = 0; i < current_dot.Links.Count; i++)
            {
                calibrationLink link = (calibrationLink)current_dot.Links[i];
                calibrationDot dot = (calibrationDot)link.From;
                if (dot.grid_x == unassigned_value)
                {
                    if (link.horizontal)
                    {
                        if (dot.x < current_dot.x)
                            dot.grid_x = current_dot.grid_x - 1;
                        else
                            dot.grid_x = current_dot.grid_x + 1;
                        dot.grid_y = current_dot.grid_y;
                    }
                    else
                    {
                        dot.grid_x = current_dot.grid_x;
                        if (dot.y > current_dot.y)
                            dot.grid_y = current_dot.grid_y - 1;
                        else
                            dot.grid_y = current_dot.grid_y + 1;
                    }
                    dots_assigned++;
                    ApplyGrid(dot, unassigned_value, ref dots_assigned);
                }
            }
        }

        private static void LinkDots(hypergraph dots,
                                     calibrationDot current_dot,
                                     double horizontal_dx, double horizontal_dy,
                                     double vertical_dx, double vertical_dy,
                                     int start_index,
                                     List<calibrationDot> search_regions)
        {
            if (!current_dot.centre)
            {
                int start_index2 = 0;
                double tollerance_divisor = 0.3f;
                double horizontal_tollerance = Math.Sqrt((horizontal_dx * horizontal_dx) + (horizontal_dy * horizontal_dy)) * tollerance_divisor;
                double vertical_tollerance = Math.Sqrt((vertical_dx * vertical_dx) + (vertical_dy * vertical_dy)) * tollerance_divisor;

                double x = 0, y = 0;
                List<int> indexes_found = new List<int>();
                List<bool> found_vertical = new List<bool>();

                // check each direction
                for (int i = 0; i < 4; i++)
                {
                    // starting direction offset
                    int ii = i + start_index;
                    if (ii >= 4) ii -= 4;

                    if (current_dot.Flags[ii] == false)
                    {
                        current_dot.Flags[ii] = true;
                        int opposite_flag = ii + 2;
                        if (opposite_flag >= 4) opposite_flag -= 4;

                        switch (ii)
                        {
                            case 0:
                                {
                                    // look above
                                    x = current_dot.x - vertical_dx;
                                    y = current_dot.y - vertical_dy;
                                    break;
                                }
                            case 1:
                                {
                                    // look right
                                    x = current_dot.x + horizontal_dx;
                                    y = current_dot.y + horizontal_dy;
                                    break;
                                }
                            case 2:
                                {
                                    // look below
                                    x = current_dot.x + vertical_dx;
                                    y = current_dot.y + vertical_dy;
                                    break;
                                }
                            case 3:
                                {
                                    // look left
                                    x = current_dot.x - horizontal_dx;
                                    y = current_dot.y - horizontal_dy;
                                    break;
                                }
                        }

                        calibrationDot search_region = new calibrationDot();
                        search_region.x = x;
                        search_region.y = y;
                        search_region.radius = (float)horizontal_tollerance;
                        search_regions.Add(search_region);

                        for (int j = 0; j < dots.Nodes.Count; j++)
                        {
                            if ((!((calibrationDot)dots.Nodes[j]).centre) &&
                                (dots.Nodes[j] != current_dot))
                            {
                                double dx = ((calibrationDot)dots.Nodes[j]).x - x;
                                double dy = ((calibrationDot)dots.Nodes[j]).y - y;
                                double dist_from_expected_position = Math.Sqrt(dx * dx + dy * dy);
                                bool dot_found = false;
                                if ((ii == 0) || (ii == 2))
                                {
                                    // vertical search
                                    if (dist_from_expected_position < vertical_tollerance)
                                    {
                                        dot_found = true;
                                        found_vertical.Add(true);
                                    }
                                }
                                else
                                {
                                    // horizontal search
                                    if (dist_from_expected_position < horizontal_tollerance)
                                    {
                                        dot_found = true;
                                        found_vertical.Add(false);
                                    }
                                }

                                if (dot_found)
                                {
                                    indexes_found.Add(j);
                                    j = dots.Nodes.Count;
                                }

                            }
                        }


                        
                    }
                }

                for (int i = 0; i < indexes_found.Count; i++)
                {
                    start_index2 = start_index + 1;
                    if (start_index2 >= 4) start_index2 -= 4;

                    double found_dx = ((calibrationDot)dots.Nodes[indexes_found[i]]).x - current_dot.x;
                    double found_dy = ((calibrationDot)dots.Nodes[indexes_found[i]]).y - current_dot.y;

                    calibrationLink link = new calibrationLink();
                    
                    if (found_vertical[i])
                    {
                        link.horizontal = false;
                        
                        if (((vertical_dy > 0) && (found_dy < 0)) ||
                            ((vertical_dy < 0) && (found_dy > 0)))
                        {
                            found_dx = -found_dx;
                            found_dy = -found_dy;
                        }
                        LinkDots(dots, (calibrationDot)dots.Nodes[indexes_found[i]], horizontal_dx, horizontal_dy, found_dx, found_dy, start_index2, search_regions);
                    }
                    else
                    {
                        link.horizontal = true;
                        
                        if (((horizontal_dx > 0) && (found_dx < 0)) ||
                            ((horizontal_dx < 0) && (found_dx > 0)))
                        {
                            found_dx = -found_dx;
                            found_dy = -found_dy;
                        }
                        LinkDots(dots, (calibrationDot)dots.Nodes[indexes_found[i]], found_dx, found_dy, vertical_dx, vertical_dy, start_index2, search_regions);
                    }
                    
                    dots.LinkByReference((calibrationDot)dots.Nodes[indexes_found[i]], current_dot, link);
                    
                }

            }
        }

        private static polygon2D GetCentreSquare(hypergraph dots,
                                                 ref List<calibrationDot> centredots)
        {
            centredots = new List<calibrationDot>();

            // find the centre dot
            calibrationDot centre = null;
            int i = 0;
            while ((i < dots.Nodes.Count) && (centre == null))
            {
                calibrationDot dot = (calibrationDot)dots.Nodes[i];
                if (dot.centre) centre = dot;
                i++;
            }

            // look for the four surrounding dots
            List<calibrationDot> centre_dots = new List<calibrationDot>();
            List<double> distances = new List<double>();
            i = 0;
            for (i = 0; i < dots.Nodes.Count; i++)
            {
                calibrationDot dot = (calibrationDot)dots.Nodes[i];
                if (!dot.centre)
                {
                    double dx = dot.x - centre.x;
                    double dy = dot.y - centre.y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (distances.Count == 4)
                    {
                        int index = -1;
                        double max_dist = 0;
                        for (int j = 0; j < 4; j++)
                        {
                            if (distances[j] > max_dist)
                            {
                                index = j;
                                max_dist = distances[j];
                            }
                        }
                        if (dist < max_dist)
                        {
                            distances[index] = dist;
                            centre_dots[index] = dot;
                        }
                    }
                    else
                    {
                        distances.Add(dist);
                        centre_dots.Add(dot);
                    }
                }
            }

            polygon2D centre_square = null;
            if (centre_dots.Count == 4)
            {
                centre_square = new polygon2D();
                for (i = 0; i < 4; i++)
                    centre_square.Add(0, 0);

                double xx = centre.x;
                double yy = centre.y;
                int index = 0;
                for (i = 0; i < 4; i++)
                {
                    if ((centre_dots[i].x < xx) &&
                        (centre_dots[i].y < yy))
                    {
                        xx = centre_dots[i].x;
                        yy = centre_dots[i].y;
                        centre_square.x_points[0] = (float)xx;
                        centre_square.y_points[0] = (float)yy;
                        index = i;
                    }
                }
                centredots.Add(centre_dots[index]);

                xx = centre.x;
                yy = centre.y;
                for (i = 0; i < 4; i++)
                {
                    if ((centre_dots[i].x > xx) &&
                        (centre_dots[i].y < yy))
                    {
                        xx = centre_dots[i].x;
                        yy = centre_dots[i].y;
                        centre_square.x_points[1] = (float)xx;
                        centre_square.y_points[1] = (float)yy;
                        index = i;
                    }
                }
                centredots.Add(centre_dots[index]);

                xx = centre.x;
                yy = centre.y;
                for (i = 0; i < 4; i++)
                {
                    if ((centre_dots[i].x > xx) &&
                        (centre_dots[i].y > yy))
                    {
                        xx = centre_dots[i].x;
                        yy = centre_dots[i].y;
                        centre_square.x_points[2] = (float)xx;
                        centre_square.y_points[2] = (float)yy;
                        index = i;
                    }
                }
                centredots.Add(centre_dots[index]);

                xx = centre.x;
                yy = centre.y;
                for (i = 0; i < 4; i++)
                {
                    if ((centre_dots[i].x < xx) &&
                        (centre_dots[i].y > yy))
                    {
                        xx = centre_dots[i].x;
                        yy = centre_dots[i].y;
                        centre_square.x_points[3] = (float)xx;
                        centre_square.y_points[3] = (float)yy;
                        index = i;
                    }
                }
                centredots.Add(centre_dots[index]);
            }
            return (centre_square);
        }

        /// <summary>
        /// detects calibration dots within the given image
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private static hypergraph DetectDots(string filename,
                                             ref int image_width, ref int image_height)
        {
            Console.WriteLine("Detecting dots in image " + filename);

            hypergraph dots = new hypergraph();

            int grouping_radius_percent = 2;
            int erosion_dilation = 0;
            float minimum_width = 0.0f;
            float maximum_width = 10;
            List<int> edges = null;
            
            List<calibrationDot> dot_shapes = shapes.DetectDots(filename, grouping_radius_percent, erosion_dilation,
                                                                minimum_width, maximum_width,
                                                                ref edges, "edges.jpg", "groups.jpg", "dots.jpg",
                                                                ref image_width, ref image_height);

            double tx = 99999;
            double ty = 99999;
            double bx = 0;
            double by = 0;
            for (int i = 0; i < dot_shapes.Count; i++)
            {
                if (dot_shapes[i].x < tx) tx = dot_shapes[i].x;
                if (dot_shapes[i].y < ty) ty = dot_shapes[i].y;
                if (dot_shapes[i].x > bx) bx = dot_shapes[i].x;
                if (dot_shapes[i].y > by) by = dot_shapes[i].y;
                dots.Add(dot_shapes[i]);
            }
            
            Console.WriteLine(dot_shapes.Count.ToString() + " dots discovered");

            return(dots);
        }
        
        #endregion
        
        #region "validation"

        /// <summary>
        /// returns a list of valid parameter names
        /// </summary>
        /// <returns>list of valid parameter names</returns>
        private static ArrayList GetValidParameters()
        {
            ArrayList ValidParameters = new ArrayList();

            ValidParameters.Add("help");         // show help info
            ValidParameters.Add("dir");          // directory where calibration files are stored
            ValidParameters.Add("baseline");     // baseline distance between the cameras
            ValidParameters.Add("dotdist");      // horizontal distance to the calibration pattern dot
            ValidParameters.Add("height");       // height above the calibration pattern in millimetres
            ValidParameters.Add("fov");          // field of view in degrees
            ValidParameters.Add("spacing");      // spacing between dots in millimetres

            return (ValidParameters);
        }

        #endregion

        #region "help information"

        /// <summary>
        /// shows help information
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("");
            Console.WriteLine("fishfood Help");
            Console.WriteLine("-------------");
            Console.WriteLine("");
            Console.WriteLine("Syntax:  fishfood");
            Console.WriteLine("                  -dir <calibration files directory>");
            Console.WriteLine("                  -baseline <stereo camera baseline in millimetres>");
            Console.WriteLine("                  -dotdist <horizontal distance to the calibration pattern dot in millimetres>");
            Console.WriteLine("                  -height <height above the calibration pattern in millimetres>");
            Console.WriteLine("                  -fov <horizontal field of view in degrees>");
            Console.WriteLine("                  -spacing <spacing between dots in millimetres>");
        }

        #endregion
    }
}