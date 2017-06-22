using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MusicPlayer {

    public class TimerPlus: IDisposable {

        #region Wrapper Core Functions
        private Timer _timer = new Timer();
        public double Interval {
            get { return _timer.Interval; }
            set { _timer.Interval = value; }
        }
        public bool Enabled {
            get { return _timer.Enabled; }
            set { _timer.Enabled = value; }
        }
        public bool AutoReset {
            get { return _timer.AutoReset; }
            set { _timer.AutoReset = value; }
        }
        public event ElapsedEventHandler Elapsed;
        #endregion

        public DateTime StartTime { get; private set; }
        public TimeSpan ElapsedTime => Enabled ? (DateTime.Now - StartTime) : (new TimeSpan(0));
        public TimeSpan RemainingTime => Enabled ? (StartTime.AddMilliseconds(Interval) - DateTime.Now) : new TimeSpan(0);

        public TimerPlus() {
            _timer.Elapsed += (sender, args) => {
                Elapsed?.Invoke(sender, args);
            };
        }

        public void Start() {
            _timer.Start();
            StartTime = DateTime.Now;
        }
        public void Stop() {
            _timer.Stop();
        }

        public void Dispose() {
            _timer?.Dispose();
        }
    }

}
