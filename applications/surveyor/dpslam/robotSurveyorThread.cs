/*
    Copyright (C) 2009 Bob Mottram
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
using System.Diagnostics;
using System.Collections;
using System.Threading;

namespace dpslam.core
{
    /// <summary>
    /// 
    /// </summary>
    public class robotSurveyorThread
    {
        private WaitCallback _callback;
        public bool Pause;
        private robotSurveyor state;
        
        /// <summary>
        /// constructor
        /// </summary>
        public robotSurveyorThread(
            WaitCallback callback,
            robotSurveyor state)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            _callback = callback;
            this.state = state;
        }

        /// <summary>
        /// ThreadStart delegate
        /// </summary>
        public void Execute()
        {
            Update();
        }

		public void StopUpdating()
		{
			state.updating = false;
		}
		
        /// <summary>
        /// 
        /// </summary>
        private void Update()
        {
            state.UpdateSurveyor();
                         
            _callback(state);
        }
        
    }
}
