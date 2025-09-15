using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ImuToXInput
{
    public class TrackerState
    {
        public int TrackerId { get; set; }
        public string BodyPart { get; set; }
        public string Ip { get; set; }

        public Quaternion Rotation { get; set; }

        public Vector3 SmoothRotation { get; set; }

        // Button hysteresis states
        public bool ButtonAState { get; set; }
        public bool ButtonBState { get; set; }
        public bool ButtonXState { get; set; }
        public bool ButtonYState { get; set; }

        // Apply smoothing
        //public void ApplySmoothing(float alpha = 0.7f)
        //{
        //   SmoothRotation = new  Vector3(SmoothRotation.X * alpha + X * (1 - alpha);
        //    SmoothY = SmoothY * alpha + Y * (1 - alpha);
        //    SmoothZ = SmoothZ * alpha + Z * (1 - alpha);
        //}

        // Get trigger value (non-linear scaling)
        public byte GetTriggerValue()
        {
            float norm = (SmoothY + 1f) / 2f;
            norm = (float)Math.Pow(norm, 1.5f);
            return (byte)Math.Clamp(norm * 255f, 0, 255);
        }
    }
}
